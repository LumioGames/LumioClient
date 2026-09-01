using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Lumio.Client.Hello;

public sealed record CommandRecord(
    string Sender,
    long Sequence,
    string Payload,
    string PayloadSha256,
    long SentAtMs);

public sealed record DeltaRecord(
    string Sender,
    long Sequence,
    long TickId,
    long Revision,
    string Payload,
    string PayloadSha256,
    long OriginSentAtMs,
    long CommittedAtMs,
    long CommandSequence,
    long LatencyMs,
    long ReceivedAtMs);

public sealed record SnapshotInfo(string SessionId, long TickId, long Revision, int HelloLogCount);

public sealed record WireError(string Code, string Detail);

public sealed record HelloClientEvent(string Kind, IReadOnlyDictionary<string, object?> Fields);

public sealed class HelloWireException : Exception
{
    public HelloWireException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed class HelloWireClientOptions
{
    public required string ServerUrl { get; init; }

    public required string Role { get; init; }

    public required string ClientName { get; init; }

    public required HelloContract Contract { get; init; }
}

/// <summary>
/// hello-wire-v1 客户端:ClientWebSocket 连接(子协议协商)、Handshake/HandshakeAck、
/// FullSnapshot/BaselineAck、InputCommand 发送(sha256/sequence 递增)与 Delta 校验。
/// 消息合法性由 <see cref="HelloContract"/> 按契约 required 字段动态判定;任一校验失败
/// (缺字段、坏 hash、revision 非严格递增、未知 messageType、Error 消息)即置失败并让
/// 全部等待方抛出——不静默丢弃(fieldSemantics 要求)。
/// </summary>
public sealed class HelloWireClient : IAsyncDisposable
{
    private readonly HelloWireClientOptions _options;
    private readonly HelloContract _contract;
    private readonly ClientWebSocket _socket = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly CancellationTokenSource _receiveCts = new();
    private readonly object _gate = new();
    private readonly List<DeltaRecord> _receivedDeltas = new();
    private readonly List<DeltaRecord> _consumedDeltas = new();
    private readonly List<WireError> _errors = new();
    private readonly List<(string Sender, TaskCompletionSource<DeltaRecord> Tcs)> _deltaWaiters = new();
    private readonly TaskCompletionSource<HandshakeOutcome> _handshakeTcs = NewTcs<HandshakeOutcome>();
    private readonly TaskCompletionSource<SnapshotInfo> _baselineTcs = NewTcs<SnapshotInfo>();
    private readonly TaskCompletionSource<string> _closeTcs = NewTcs<string>();
    private Task? _receiveLoop;
    private HelloWireException? _failure;
    private long _lastRevision = long.MinValue;
    private long _nextSequence = 1L;
    private long _maxLatencyMs = -1L;
    private bool _aborted;
    private bool _disposed;

    public HelloWireClient(HelloWireClientOptions options)
    {
        _options = options;
        _contract = options.Contract;
    }

    public event Action<HelloClientEvent>? Event;

    public string? SessionId { get; private set; }

    public string? NegotiatedSubProtocol { get; private set; }

    public IReadOnlyList<DeltaRecord> ReceivedDeltas
    {
        get
        {
            lock (_gate)
            {
                return new List<DeltaRecord>(_receivedDeltas);
            }
        }
    }

    public IReadOnlyList<WireError> Errors
    {
        get
        {
            lock (_gate)
            {
                return new List<WireError>(_errors);
            }
        }
    }

    public CommandRecord? LastSentCommand { get; private set; }

    public long MaxLatencyMs
    {
        get
        {
            lock (_gate)
            {
                return _maxLatencyMs;
            }
        }
    }

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        return ConnectInternalAsync(cancellationToken);
    }

    public Task HandshakeAsync(CancellationToken cancellationToken)
    {
        return HandshakeInternalAsync(cancellationToken);
    }

    public Task<SnapshotInfo> WaitForBaselineAsync(CancellationToken cancellationToken)
    {
        return WaitForBaselineInternalAsync(cancellationToken);
    }

    public Task<DeltaRecord> WaitForDeltaAsync(string sender, CancellationToken cancellationToken)
    {
        return WaitForDeltaInternalAsync(sender, cancellationToken);
    }

    public Task<CommandRecord> SendCommandAsync(string payload, CancellationToken cancellationToken)
    {
        return SendCommandInternalAsync(payload, cancellationToken);
    }

    public Task WaitForServerCloseAsync(CancellationToken cancellationToken)
    {
        return WaitForServerCloseInternalAsync(cancellationToken);
    }

    public Task AbortAsync(string reason)
    {
        lock (_gate)
        {
            if (_aborted || _disposed)
            {
                return Task.CompletedTask;
            }

            _aborted = true;
        }

        return AbortInternalAsync(reason);
    }

    public async ValueTask DisposeAsync()
    {
        await AbortAsync("disposed").ConfigureAwait(false);
        lock (_gate)
        {
            _disposed = true;
        }

        _socket.Dispose();
        _receiveCts.Dispose();
        _sendGate.Dispose();
    }

    // ---------- 连接与握手 ----------

    private async Task ConnectInternalAsync(CancellationToken cancellationToken)
    {
        ThrowIfFailed();
        _socket.Options.AddSubProtocol(_contract.Subprotocol);
        await _socket.ConnectAsync(new Uri(_options.ServerUrl, UriKind.Absolute), cancellationToken).ConfigureAwait(false);

        if (!string.Equals(_socket.SubProtocol, _contract.Subprotocol, StringComparison.Ordinal))
        {
            var mismatch = new HelloWireException(
                "unsupported_contract",
                $"子协议协商不符:negotiated='{_socket.SubProtocol}' expected='{_contract.Subprotocol}'");
            Fail(mismatch);
            throw mismatch;
        }

        NegotiatedSubProtocol = _socket.SubProtocol;
        _receiveLoop = Task.Run(ReceiveLoopAsync, CancellationToken.None);
    }

    private async Task HandshakeInternalAsync(CancellationToken cancellationToken)
    {
        ThrowIfFailed();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(_contract.Limits.HandshakeTimeoutMs));

        await SendMessageAsync(
            new Dictionary<string, object?>
            {
                ["messageType"] = "Handshake",
                ["role"] = _options.Role,
                ["clientName"] = _options.ClientName,
                ["contractId"] = _contract.ContractId,
            },
            cancellationToken).ConfigureAwait(false);

        try
        {
            await _handshakeTcs.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new HelloWireException("handshake_timeout", "HandshakeAck 未在 handshakeTimeoutMs 内到达");
        }
    }

    // ---------- 基线 ----------

    private async Task<SnapshotInfo> WaitForBaselineInternalAsync(CancellationToken cancellationToken)
    {
        ThrowIfFailed();
        lock (_gate)
        {
            if (_baselineTcs.Task.IsCompletedSuccessfully)
            {
                return _baselineTcs.Task.Result;
            }
        }

        try
        {
            return await _baselineTcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HelloWireException("baseline_failed", "等待基线时连接终止");
        }
    }

    // ---------- Delta ----------

    private async Task<DeltaRecord> WaitForDeltaInternalAsync(string sender, CancellationToken cancellationToken)
    {
        ThrowIfFailed();
        TaskCompletionSource<DeltaRecord> waiter;
        lock (_gate)
        {
            ThrowIfFailed();
            DeltaRecord? buffered = FindBufferedDelta(sender);
            if (buffered is not null)
            {
                return buffered;
            }

            waiter = NewTcs<DeltaRecord>();
            _deltaWaiters.Add((sender, waiter));
        }

        try
        {
            return await waiter.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HelloWireException("connection_lost", $"等待 {sender} Delta 时连接终止");
        }
    }

    private DeltaRecord? FindBufferedDelta(string sender)
    {
        foreach (DeltaRecord delta in _receivedDeltas)
        {
            if (string.Equals(delta.Sender, sender, StringComparison.Ordinal)
                && !_consumedDeltas.Contains(delta))
            {
                _consumedDeltas.Add(delta);
                return delta;
            }
        }

        return null;
    }

    // ---------- 命令 ----------

    private async Task<CommandRecord> SendCommandInternalAsync(string payload, CancellationToken cancellationToken)
    {
        ThrowIfFailed();
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
        if (payloadBytes.Length > _contract.Limits.MaxPayloadBytes)
        {
            throw new HelloWireException("bad_envelope", $"payload 超过 maxPayloadBytes({payloadBytes.Length})");
        }

        long sequence;
        CommandRecord record;
        lock (_gate)
        {
            sequence = _nextSequence++;
        }

        string sha = Sha256Hex(payload);
        long sentAtMs = Now();
        record = new CommandRecord(_options.Role, sequence, payload, sha, sentAtMs);

        string? kind = _contract.ConstValue("InputCommand", "kind");
        await SendMessageAsync(
            new Dictionary<string, object?>
            {
                ["messageType"] = "InputCommand",
                ["sender"] = _options.Role,
                ["sequence"] = sequence,
                ["kind"] = kind,
                ["payload"] = payload,
                ["payloadSha256"] = sha,
                ["sentAtMs"] = sentAtMs,
            },
            cancellationToken).ConfigureAwait(false);

        lock (_gate)
        {
            LastSentCommand = record;
        }

        RaiseEvent("command_sent", new Dictionary<string, object?>
        {
            ["sender"] = _options.Role,
            ["sequence"] = sequence,
            ["payloadSha256"] = sha,
            ["sentAtMs"] = sentAtMs,
        });
        return record;
    }

    // ---------- 关闭 ----------

    private async Task WaitForServerCloseInternalAsync(CancellationToken cancellationToken)
    {
        ThrowIfFailed();
        await _closeTcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task AbortInternalAsync(string reason)
    {
        _receiveCts.Cancel();
        _socket.Dispose();
        if (_receiveLoop is not null)
        {
            try
            {
                await _receiveLoop.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // 主动中止后的循环收尾不是被测行为。
            }
        }
    }

    // ---------- 接收循环 ----------

    private async Task ReceiveLoopAsync()
    {
        byte[] buffer = new byte[16384];
        using var message = new MemoryStream();
        try
        {
            while (_socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await _socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    _receiveCts.Token).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await HandleServerCloseAsync(result).ConfigureAwait(false);
                    return;
                }

                message.Write(buffer, 0, result.Count);
                if (message.Length > _contract.MaxFrameBytes)
                {
                    Fail(new HelloWireException("bad_envelope", $"帧超过 maxFrameBytes({_contract.MaxFrameBytes})"));
                    return;
                }

                if (!result.EndOfMessage)
                {
                    continue;
                }

                byte[] payload = message.ToArray();
                message.SetLength(0);
                await HandleMessageAsync(payload).ConfigureAwait(false);
                lock (_gate)
                {
                    if (_failure is not null)
                    {
                        return;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (_receiveCts.IsCancellationRequested)
        {
            // 本端主动中止,预期路径。
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                if (!_aborted)
                {
                    Fail(new HelloWireException("session_closed", "连接异常终止: " + ex.Message));
                }
            }
        }
    }

    private async Task HandleServerCloseAsync(WebSocketReceiveResult result)
    {
        bool normal = result.CloseStatus == WebSocketCloseStatus.NormalClosure;
        try
        {
            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // 对端可能在 close 握手中途消失;close 事实已从 result 读到。
        }

        lock (_gate)
        {
            if (_failure is not null)
            {
                return;
            }

            bool waiting = !_handshakeTcs.Task.IsCompleted
                || !_baselineTcs.Task.IsCompleted
                || _deltaWaiters.Count > 0;
            if (!normal)
            {
                FailLocked(new HelloWireException("session_closed", $"server 以 {result.CloseStatus} 关闭"));
                return;
            }

            if (waiting)
            {
                FailLocked(new HelloWireException("session_closed", "连接在流程完成前被 server 关闭"));
                return;
            }

            _closeTcs.TrySetResult("server closed: " + result.CloseStatus);
        }
    }

    private async Task HandleMessageAsync(byte[] payload)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException ex)
        {
            Fail(new HelloWireException("bad_envelope", "invalid JSON: " + ex.Message));
            return;
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            IReadOnlyList<ContractFieldError> errors = _contract.ValidateMessage(root);
            if (errors.Count > 0)
            {
                bool unknownMapping = false;
                foreach (ContractFieldError error in errors)
                {
                    unknownMapping |= error.Problem.Contains("unknown_mapping", StringComparison.Ordinal);
                }

                string detail = string.Join("; ", errors.Select(e => e.Field + ": " + e.Problem));
                Fail(new HelloWireException(unknownMapping ? "unknown_mapping" : "bad_envelope", detail));
                return;
            }

            switch (root.GetProperty("messageType").GetString())
            {
                case "HandshakeAck":
                    HandleHandshakeAck(root);
                    break;
                case "FullSnapshot":
                    await HandleFullSnapshotAsync(root).ConfigureAwait(false);
                    break;
                case "Delta":
                    HandleDelta(root);
                    break;
                case "Error":
                    HandleError(root);
                    break;
                default:
                    Fail(new HelloWireException(
                        "unknown_mapping",
                        "server 下发了客户端方向的消息: " + root.GetProperty("messageType").GetString()));
                    break;
            }
        }
    }

    private void HandleHandshakeAck(JsonElement root)
    {
        bool accepted = root.GetProperty("accepted").GetBoolean();
        string role = root.GetProperty("role").GetString()!;
        string sessionId = root.GetProperty("sessionId").GetString()!;
        string ackContractId = root.GetProperty("contractId").GetString()!;

        RaiseEvent("handshake_ack", new Dictionary<string, object?>
        {
            ["role"] = role,
            ["accepted"] = accepted,
        });

        if (!accepted)
        {
            string reason = root.TryGetProperty("reason", out JsonElement reasonElement)
                && reasonElement.ValueKind == JsonValueKind.String
                ? reasonElement.GetString()!
                : "handshake rejected";
            Fail(new HelloWireException("handshake_rejected", reason));
            return;
        }

        if (!string.Equals(ackContractId, _contract.ContractId, StringComparison.Ordinal))
        {
            Fail(new HelloWireException(
                "unsupported_contract",
                $"server contractId '{ackContractId}' != 客户端契约 '{_contract.ContractId}'"));
            return;
        }

        lock (_gate)
        {
            SessionId = sessionId;
        }

        RaiseEvent("connected", new Dictionary<string, object?>
        {
            ["sessionId"] = sessionId,
        });
        _handshakeTcs.TrySetResult(new HandshakeOutcome(sessionId, role, accepted));
    }

    private async Task HandleFullSnapshotAsync(JsonElement root)
    {
        string sessionId = root.GetProperty("sessionId").GetString()!;
        long tickId = root.GetProperty("tickId").GetInt64();
        long revision = root.GetProperty("revision").GetInt64();
        int helloLogCount = root.GetProperty("helloLog").GetArrayLength();

        lock (_gate)
        {
            SessionId ??= sessionId;
            _lastRevision = Math.Max(_lastRevision, revision);
        }

        RaiseEvent("baseline_received", new Dictionary<string, object?>
        {
            ["revision"] = revision,
            ["tickId"] = tickId,
            ["helloLogCount"] = helloLogCount,
        });

        await SendMessageAsync(
            new Dictionary<string, object?>
            {
                ["messageType"] = "BaselineAck",
                ["revision"] = revision,
            },
            CancellationToken.None).ConfigureAwait(false);

        RaiseEvent("baseline_ack_sent", new Dictionary<string, object?>
        {
            ["revision"] = revision,
        });
        _baselineTcs.TrySetResult(new SnapshotInfo(sessionId, tickId, revision, helloLogCount));
    }

    private void HandleDelta(JsonElement root)
    {
        long receivedAtMs = Now();
        string sender = root.GetProperty("sender").GetString()!;
        long sequence = root.GetProperty("sequence").GetInt64();
        long tickId = root.GetProperty("tickId").GetInt64();
        long revision = root.GetProperty("revision").GetInt64();
        string payload = root.GetProperty("payload").GetString()!;
        string payloadSha256 = root.GetProperty("payloadSha256").GetString()!;
        long originSentAtMs = root.GetProperty("originSentAtMs").GetInt64();
        long committedAtMs = root.GetProperty("committedAtMs").GetInt64();
        long commandSequence = root.GetProperty("commandSequence").GetInt64();

        string recomputed = Sha256Hex(payload);
        if (!string.Equals(recomputed, payloadSha256, StringComparison.Ordinal))
        {
            Fail(new HelloWireException(
                "bad_payload_hash",
                $"Delta {sender}/{sequence} payloadSha256 不符:wire='{payloadSha256}' recomputed='{recomputed}'"));
            return;
        }

        lock (_gate)
        {
            if (revision <= _lastRevision)
            {
                FailLocked(new HelloWireException(
                    "stale_revision",
                    $"Delta revision 非严格递增:got={revision} last={_lastRevision}"));
                return;
            }

            _lastRevision = revision;
        }

        long latencyMs = receivedAtMs - originSentAtMs;
        var record = new DeltaRecord(
            sender, sequence, tickId, revision, payload, payloadSha256,
            originSentAtMs, committedAtMs, commandSequence, latencyMs, receivedAtMs);

        TaskCompletionSource<DeltaRecord>? waiter = null;
        lock (_gate)
        {
            _receivedDeltas.Add(record);
            if (latencyMs > _maxLatencyMs)
            {
                _maxLatencyMs = latencyMs;
            }

            for (int i = 0; i < _deltaWaiters.Count; i++)
            {
                if (string.Equals(_deltaWaiters[i].Sender, sender, StringComparison.Ordinal))
                {
                    waiter = _deltaWaiters[i].Tcs;
                    _deltaWaiters.RemoveAt(i);
                    break;
                }
            }
        }

        RaiseEvent("delta_received", new Dictionary<string, object?>
        {
            ["sender"] = sender,
            ["sequence"] = sequence,
            ["tickId"] = tickId,
            ["revision"] = revision,
            ["payloadSha256"] = payloadSha256,
            ["latencyMs"] = latencyMs,
        });
        waiter?.TrySetResult(record);
    }

    private void HandleError(JsonElement root)
    {
        string code = root.GetProperty("code").GetString()!;
        string detail = root.GetProperty("detail").GetString()!;
        lock (_gate)
        {
            _errors.Add(new WireError(code, detail));
        }

        RaiseEvent("error_received", new Dictionary<string, object?>
        {
            ["code"] = code,
            ["detail"] = detail,
        });
        Fail(new HelloWireException(code, detail));
    }

    // ---------- 发送 ----------

    private async Task SendMessageAsync(IReadOnlyDictionary<string, object?> message, CancellationToken cancellationToken)
    {
        byte[] bytes = Serialize(message);
        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private static byte[] Serialize(IReadOnlyDictionary<string, object?> message)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (KeyValuePair<string, object?> field in message)
            {
                switch (field.Value)
                {
                    case string s:
                        writer.WriteString(field.Key, s);
                        break;
                    case bool b:
                        writer.WriteBoolean(field.Key, b);
                        break;
                    case long l:
                        writer.WriteNumber(field.Key, l);
                        break;
                    case int i:
                        writer.WriteNumber(field.Key, i);
                        break;
                    default:
                        throw new InvalidOperationException("unsupported wire value " + field.Value?.GetType().Name);
                }
            }

            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    // ---------- 失败与事件 ----------

    private void ThrowIfFailed()
    {
        lock (_gate)
        {
            if (_failure is not null)
            {
                throw _failure;
            }
        }
    }

    private void Fail(HelloWireException exception)
    {
        lock (_gate)
        {
            FailLocked(exception);
        }
    }

    /// <summary>要求已持有 <c>_gate</c>。</summary>
    private void FailLocked(HelloWireException exception)
    {
        if (_failure is not null)
        {
            return;
        }

        _failure = exception;
        _handshakeTcs.TrySetException(exception);
        _baselineTcs.TrySetException(exception);
        _closeTcs.TrySetException(exception);
        foreach ((_, TaskCompletionSource<DeltaRecord> waiter) in _deltaWaiters)
        {
            waiter.TrySetException(exception);
        }

        _deltaWaiters.Clear();
    }

    private void RaiseEvent(string kind, IReadOnlyDictionary<string, object?> fields)
    {
        Action<HelloClientEvent>? handler = Event;
        handler?.Invoke(new HelloClientEvent(kind, fields));
    }

    private static TaskCompletionSource<T> NewTcs<T>()
    {
        return new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static long Now()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private static string Sha256Hex(string payload)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record HandshakeOutcome(string SessionId, string Role, bool Accepted);
}

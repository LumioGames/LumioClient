using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Lumio.Client.Hello.Tests;

/// <summary>
/// 测试专用的契约 speaking loopback server,只存在于 modules/hello/tests/。
/// 与 modules/connection/tests 的 LoopbackWebSocketServer 同款做法:手写 RFC 6455 的 101 握手
/// (HttpListener.AcceptWebSocketAsync 在非 Windows 上抛 PlatformNotSupportedException),
/// 再用 WebSocket.CreateFromStream(isServer: true) 接管。
/// </summary>
internal sealed class HelloLoopbackServer : IAsyncDisposable
{
    private const string HandshakeGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private const string Subprotocol = "lumio-hello-v1";
    private const string Payload = "Hello World";
    private const int MaxFrameBytes = 65536;

    private readonly TcpListener _listener;
    private readonly HelloServerScript _script;
    private readonly HelloContract _contract;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _gate = new();
    private readonly List<string> _receivedTypes = new();
    private readonly List<JsonElement> _receivedMessages = new();
    private Task? _accept;
    private TcpClient? _client;
    private long? _browserDeltaSentAt;
    private long? _commandReceivedAt;

    private HelloLoopbackServer(HelloServerScript script, HelloContract contract)
    {
        _script = script;
        _contract = contract;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Uri = "ws://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/hello";
    }

    public static HelloLoopbackServer Start(HelloServerScript script, HelloContract contract)
    {
        var server = new HelloLoopbackServer(script, contract);
        server._accept = Task.Run(() => server.RunAsync(server._cts.Token));
        return server;
    }

    public string Uri { get; }

    /// <summary>升级请求是否带上了 lumio-hello-v1 子协议。</summary>
    public bool ProtocolHeaderValid { get; private set; }

    /// <summary>夹具诊断:server 侧未预期异常(测试失败时打印)。</summary>
    public string? LastServerError { get; private set; }

    /// <summary>收到的应用消息 messageType 有序记录(Handshake/BaselineAck/InputCommand)。</summary>
    public IReadOnlyList<string> ReceivedTypes
    {
        get
        {
            lock (_gate)
            {
                return _receivedTypes.ToArray();
            }
        }
    }

    public IReadOnlyList<JsonElement> ReceivedMessages
    {
        get
        {
            lock (_gate)
            {
                return _receivedMessages.ToArray();
            }
        }
    }

    /// <summary>顺序断言:bot 的 InputCommand 不得早于 server 下发的 browser Delta。</summary>
    public bool OrderingHeld
    {
        get
        {
            lock (_gate)
            {
                return _commandReceivedAt is null
                    || (_browserDeltaSentAt is not null && _commandReceivedAt >= _browserDeltaSentAt);
            }
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        TcpClient? client = null;
        try
        {
            client = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            _client = client;
            NetworkStream stream = client.GetStream();
            string request = await ReadRequestHeadersAsync(stream, ct).ConfigureAwait(false);
            string? offered = ReadHeader(request, "sec-websocket-protocol");
            ProtocolHeaderValid = offered is not null
                && offered.Split(',').Select(p => p.Trim()).Contains(Subprotocol, StringComparer.Ordinal);
            string key = ReadHeader(request, "sec-websocket-key") ?? string.Empty;

            await WriteHandshakeResponseAsync(stream, key, ProtocolHeaderValid ? Subprotocol : null, ct).ConfigureAwait(false);

            using WebSocket socket = WebSocket.CreateFromStream(
                stream,
                isServer: true,
                subProtocol: ProtocolHeaderValid ? Subprotocol : null,
                keepAliveInterval: TimeSpan.FromSeconds(30));

            await RunScriptAsync(socket, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // 停止竞态与被测失败路径造成的断开都不是夹具断言对象;证据从 Received*/Ordering* 读。
            LastServerError = ex.ToString();
        }
        finally
        {
            client?.Dispose();
        }
    }

    private async Task RunScriptAsync(WebSocket socket, CancellationToken ct)
    {
        // 1. 期待 Handshake。
        JsonElement handshake = await ReceiveJsonAsync(socket, ct).ConfigureAwait(false);
        Record(handshake);

        // 2. HandshakeAck(contractId 不符 / accepted=false 是失败矩阵的注入点)。
        string ackContractId = string.IsNullOrEmpty(_script.HandshakeAckContractId)
            ? _contract.ContractId
            : _script.HandshakeAckContractId;
        await SendJsonAsync(socket, new Dictionary<string, object?>
        {
            ["messageType"] = "HandshakeAck",
            ["sessionId"] = _script.SessionId,
            ["role"] = GetString(handshake, "role") ?? "bot",
            ["accepted"] = _script.AcceptHandshake,
            ["contractId"] = ackContractId,
        }, ct).ConfigureAwait(false);

        if (!_script.AcceptHandshake)
        {
            await SendJsonAsync(socket, new Dictionary<string, object?>
            {
                ["messageType"] = "Error",
                ["code"] = "role_taken",
                ["detail"] = "scripted rejection",
            }, ct).ConfigureAwait(false);
        }

        if (_script.AbortTcpAfterHandshakeAck)
        {
            // 中途断线矩阵:不做 WebSocket close,直接掐 TCP。
            AbortTcp();
            return;
        }

        // 3. 基线(FullSnapshot),或按脚本扣住不发(baseline 超时矩阵)。
        if (_script.SendBaseline)
        {
            await SendJsonAsync(socket, new Dictionary<string, object?>
            {
                ["messageType"] = "FullSnapshot",
                ["sessionId"] = _script.SessionId,
                ["tickId"] = _script.BaselineTickId,
                ["revision"] = _script.BaselineRevision,
                ["helloLog"] = Array.Empty<object>(),
            }, ct).ConfigureAwait(false);

            JsonElement baselineAck = await ReceiveJsonAsync(socket, ct).ConfigureAwait(false);
            Record(baselineAck);
        }

        // 4. 静默窗口:bot 在收到 browser Delta 前不得发 InputCommand(发送前提的顺序断言)。
        await Task.Delay(_script.QuietWindowMs, ct).ConfigureAwait(false);

        if (_script.AbortTcpAfterBaseline)
        {
            // 中途断线矩阵:不做 WebSocket close,直接掐 TCP。
            AbortTcp();
            return;
        }

        if (!_script.SendBrowserDelta)
        {
            // 发送前置永远不满足:server 主动收摊。bot 必须以失败退出,而不是发送命令。
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "server shutdown", ct).ConfigureAwait(false);
            await PumpUntilClosedAsync(socket, ct).ConfigureAwait(false);
            return;
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (_script.SendBrowserDelta)
        {
            string sha = string.IsNullOrEmpty(_script.BrowserDeltaPayloadSha256)
                ? Sha256Hex(Payload)
                : _script.BrowserDeltaPayloadSha256;
            await SendJsonAsync(socket, new Dictionary<string, object?>
            {
                ["messageType"] = "Delta",
                ["tickId"] = 1L,
                ["revision"] = _script.BrowserDeltaRevision,
                ["sender"] = "browser",
                ["sequence"] = 1L,
                ["kind"] = "hello",
                ["payload"] = Payload,
                ["payloadSha256"] = sha,
                ["originSentAtMs"] = now - 10L,
                ["committedAtMs"] = now,
                ["commandSequence"] = 1L,
            }, ct).ConfigureAwait(false);
            lock (_gate)
            {
                _browserDeltaSentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            if (_script.SendDuplicateRevisionDelta)
            {
                await SendJsonAsync(socket, new Dictionary<string, object?>
                {
                    ["messageType"] = "Delta",
                    ["tickId"] = 1L,
                    ["revision"] = _script.BrowserDeltaRevision,
                    ["sender"] = "browser",
                    ["sequence"] = 2L,
                    ["kind"] = "hello",
                    ["payload"] = Payload,
                    ["payloadSha256"] = Sha256Hex(Payload),
                    ["originSentAtMs"] = now,
                    ["committedAtMs"] = now,
                    ["commandSequence"] = 2L,
                }, ct).ConfigureAwait(false);
            }
        }

        if (_script.SendUnknownMessage)
        {
            await SendJsonAsync(socket, new Dictionary<string, object?>
            {
                ["messageType"] = "Nope",
                ["foo"] = 1L,
            }, ct).ConfigureAwait(false);
        }

        if (_script.SendError)
        {
            await SendJsonAsync(socket, new Dictionary<string, object?>
            {
                ["messageType"] = "Error",
                ["code"] = "runtime_failure",
                ["detail"] = "scripted runtime failure",
            }, ct).ConfigureAwait(false);
        }

        // 5. 期待 bot 的 InputCommand(browser Delta 缺席时等不到,走关闭)。
        int commands = 0;
        while (commands < _script.MaxCommands)
        {
            using var commandCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            commandCts.CancelAfter(_script.CommandWaitMs);
            try
            {
                JsonElement command = await ReceiveJsonAsync(socket, commandCts.Token).ConfigureAwait(false);
                Record(command);
                commands++;
                lock (_gate)
                {
                    _commandReceivedAt ??= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                }
            }
            catch (OperationCanceledException)
            {
                // bot 未发送(失败矩阵的预期形态)。
                break;
            }
        }

        if (_script.CloseAfterCommand && commands > 0)
        {
            await Task.Delay(_script.CloseDelayMs, ct).ConfigureAwait(false);
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "server shutdown", ct).ConfigureAwait(false);
        }

        await PumpUntilClosedAsync(socket, ct).ConfigureAwait(false);
    }

    private void AbortTcp()
    {
        TcpClient? client = _client;
        if (client is not null)
        {
            client.Client.LingerState = new LingerOption(true, 0);
            client.Close();
        }
    }

    private void Record(JsonElement message)
    {
        lock (_gate)
        {
            _receivedTypes.Add(GetString(message, "messageType") ?? "<missing>");
            _receivedMessages.Add(message.Clone());
        }
    }

    private static string? GetString(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static async Task<JsonElement> ReceiveJsonAsync(WebSocket socket, CancellationToken ct)
    {
        byte[] buffer = new byte[8192];
        var message = new List<byte>();
        while (true)
        {
            WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, ct).ConfigureAwait(false);
                throw new OperationCanceledException("peer closed before expected message");
            }

            message.AddRange(new ArraySegment<byte>(buffer, 0, result.Count));
            if (message.Count > MaxFrameBytes)
            {
                throw new InvalidOperationException("test server frame overflow");
            }

            if (result.EndOfMessage)
            {
                using var document = JsonDocument.Parse(Encoding.UTF8.GetString(message.ToArray()));
                return document.RootElement.Clone();
            }
        }
    }

    private static async Task SendJsonAsync(WebSocket socket, IReadOnlyDictionary<string, object?> message, CancellationToken ct)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(ToJson(message));
        await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, endOfMessage: true, ct).ConfigureAwait(false);
    }

    private static async Task PumpUntilClosedAsync(WebSocket socket, CancellationToken ct)
    {
        byte[] buffer = new byte[4096];
        while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, ct).ConfigureAwait(false);
                return;
            }

            if (result.EndOfMessage && result.MessageType == WebSocketMessageType.Text)
            {
                // 迟到消息只记录,不再驱动脚本。
            }
        }
    }

    private static string ToJson(IReadOnlyDictionary<string, object?> message)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (KeyValuePair<string, object?> field in message)
            {
                WriteValue(writer, field.Key, field.Value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteValue(Utf8JsonWriter writer, string key, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNull(key);
                break;
            case string s:
                writer.WriteString(key, s);
                break;
            case bool b:
                writer.WriteBoolean(key, b);
                break;
            case long l:
                writer.WriteNumber(key, l);
                break;
            case int i:
                writer.WriteNumber(key, i);
                break;
            case object?[] array:
                writer.WriteStartArray(key);
                foreach (object? element in array)
                {
                    switch (element)
                    {
                        case null:
                            writer.WriteNullValue();
                            break;
                        case string s:
                            writer.WriteStringValue(s);
                            break;
                        case bool b:
                            writer.WriteBooleanValue(b);
                            break;
                        case long l:
                            writer.WriteNumberValue(l);
                            break;
                        case int i:
                            writer.WriteNumberValue(i);
                            break;
                        default:
                            throw new InvalidOperationException("unexpected test array value type " + element.GetType().Name);
                    }
                }

                writer.WriteEndArray();
                break;
            default:
                throw new InvalidOperationException("unexpected test value type " + value.GetType().Name);
        }
    }

    private static string Sha256Hex(string payload)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<string> ReadRequestHeadersAsync(Stream stream, CancellationToken ct)
    {
        var builder = new StringBuilder();
        byte[] one = new byte[1];
        while (!builder.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            int read = await stream.ReadAsync(one.AsMemory(0, 1), ct).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            builder.Append((char)one[0]);
        }

        return builder.ToString();
    }

    private static async Task WriteHandshakeResponseAsync(Stream stream, string key, string? chosen, CancellationToken ct)
    {
        // RFC 6455 §4.2.2 把 SHA-1 写死在握手里;这里只复现协议要求的 Sec-WebSocket-Accept 计算,
        // 且只存在于测试夹具中。
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
        string accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + HandshakeGuid)));
#pragma warning restore CA5350

        var response = new StringBuilder()
            .Append("HTTP/1.1 101 Switching Protocols\r\n")
            .Append("Upgrade: websocket\r\n")
            .Append("Connection: Upgrade\r\n")
            .Append("Sec-WebSocket-Accept: ").Append(accept).Append("\r\n");
        if (chosen is not null)
        {
            response.Append("Sec-WebSocket-Protocol: ").Append(chosen).Append("\r\n");
        }

        response.Append("\r\n");
        byte[] bytes = Encoding.ASCII.GetBytes(response.ToString());
        await stream.WriteAsync(bytes.AsMemory(), ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    private static string? ReadHeader(string request, string name)
    {
        foreach (string line in request.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            int colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            if (string.Equals(line.Substring(0, colon).Trim(), name, StringComparison.OrdinalIgnoreCase))
            {
                return line.Substring(colon + 1).Trim();
            }
        }

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener.Stop();
        if (_accept is not null)
        {
            try
            {
                await _accept.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // 停止竞态不是被测行为。
            }
        }

        _cts.Dispose();
    }
}

internal sealed class HelloServerScript
{
    public string SessionId { get; set; } = "srv-session-1";

    public bool AcceptHandshake { get; set; } = true;

    /// <summary>非空时 HandshakeAck 用它替换 contractId(契约不符矩阵)。</summary>
    public string? HandshakeAckContractId { get; set; }

    public bool SendBaseline { get; set; } = true;

    public long BaselineRevision { get; set; }

    public long BaselineTickId { get; set; }

    public bool SendBrowserDelta { get; set; } = true;

    /// <summary>非空时 Delta 用它替换 payloadSha256(坏 hash 矩阵)。</summary>
    public string? BrowserDeltaPayloadSha256 { get; set; }

    public long BrowserDeltaRevision { get; set; } = 1L;

    public bool SendDuplicateRevisionDelta { get; set; }

    public bool SendUnknownMessage { get; set; }

    public bool SendError { get; set; }

    /// <summary>HandshakeAck 后不做 close 直接掐 TCP(中途断线矩阵)。</summary>
    public bool AbortTcpAfterHandshakeAck { get; set; }

    /// <summary>BaselineAck 后不做 close 直接掐 TCP(拿到基线后断线矩阵)。</summary>
    public bool AbortTcpAfterBaseline { get; set; }

    /// <summary>bot 命令窗口:默认宽到成功路径必达,失败矩阵下 bot 自己先退出。</summary>
    public int CommandWaitMs { get; set; } = 5000;

    /// <summary>server 读取的 InputCommand 条数(顺序/递增断言可读多条)。</summary>
    public int MaxCommands { get; set; } = 1;

    public int QuietWindowMs { get; set; } = 200;

    public int CloseDelayMs { get; set; } = 50;

    public bool CloseAfterCommand { get; set; } = true;
}

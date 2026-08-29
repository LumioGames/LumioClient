using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lumio.Client.Connection.Tests.Transport;

/// <summary>
/// 测试专用的 loopback WebSocket 端。**只允许存在于 modules/connection/tests/**（T-00003 边界）：
/// 监听侧归 LumioServer，本仓只拥有拨号侧。本类跑绿不代表跨仓联调成功。
/// </summary>
/// <remarks>
/// 不用 <c>HttpListener.AcceptWebSocketAsync</c>：.NET 在非 Windows 上走托管 HttpListener 实现，
/// 该方法直接抛 <c>PlatformNotSupportedException</c>。这里手写 RFC 6455 的 101 握手，
/// 再用 <c>WebSocket.CreateFromStream(isServer: true, ...)</c> 接管。
/// </remarks>
internal sealed class LoopbackWebSocketServer : IAsyncDisposable
{
    private const string HandshakeGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    private readonly TcpListener _listener;
    private readonly LoopbackWebSocketScript _script;
    private readonly CancellationTokenSource _cts = new();
    private readonly TaskCompletionSource<bool> _handshakeSeen =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<byte[]> _receivedMessages = new();
    private readonly List<byte> _applicationBytesReceived = new();
    private readonly object _gate = new();
    private Task? _accept;

    private LoopbackWebSocketServer(LoopbackWebSocketScript script)
    {
        _script = script;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Uri = "ws://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/session";
    }

    public static LoopbackWebSocketServer Start(LoopbackWebSocketScript script)
    {
        var server = new LoopbackWebSocketServer(script);
        server._accept = Task.Run(() => server.RunAsync(server._cts.Token));
        return server;
    }

    public string Uri { get; }

    /// <summary>Upgrade 请求里 <c>Sec-WebSocket-Protocol</c> 的原始值（凭据按设计就在这里，不在应用数据里）。</summary>
    public string? RequestSubProtocolHeader { get; private set; }

    public IReadOnlyList<byte[]> ReceivedMessages
    {
        get { lock (_gate) { return _receivedMessages.ToArray(); } }
    }

    /// <summary>服务端见到的**全部应用数据字节**。凭据遏制断言以此为准。</summary>
    public byte[] ApplicationBytesReceived
    {
        get { lock (_gate) { return _applicationBytesReceived.ToArray(); } }
    }

    public Task<bool> HandshakeSeen => _handshakeSeen.Task;

    private async Task RunAsync(CancellationToken ct)
    {
        TcpClient? client = null;
        try
        {
            client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
            NetworkStream stream = client.GetStream();
            string request = await ReadRequestHeadersAsync(stream, ct).ConfigureAwait(false);
            RequestSubProtocolHeader = ReadHeader(request, "Sec-WebSocket-Protocol");
            string key = ReadHeader(request, "Sec-WebSocket-Key") ?? string.Empty;
            string? chosen = _script.NegotiateSubProtocol ? PickSubProtocol(RequestSubProtocolHeader) : null;

            await WriteHandshakeResponseAsync(stream, key, chosen, ct).ConfigureAwait(false);
            _handshakeSeen.TrySetResult(true);

            if (_script.Kind == LoopbackScriptKind.AbortTcpWithoutCloseFrame)
            {
                client.Client.LingerState = new LingerOption(true, 0);
                client.Close();
                return;
            }

            using WebSocket socket = WebSocket.CreateFromStream(
                stream,
                isServer: true,
                subProtocol: chosen,
                keepAliveInterval: TimeSpan.FromSeconds(30));

            await RunScriptAsync(socket, ct).ConfigureAwait(false);
        }
        catch (Exception)
        {
            _handshakeSeen.TrySetResult(false);
        }
        finally
        {
            client?.Dispose();
        }
    }

    private async Task RunScriptAsync(WebSocket socket, CancellationToken ct)
    {
        switch (_script.Kind)
        {
            case LoopbackScriptKind.RejectWithPolicyViolation:
                // 通道认证失败：升级完成后立刻 close 1008，**此前零字节应用数据**。
                await socket.CloseAsync(
                    WebSocketCloseStatus.PolicyViolation,
                    "channel auth rejected",
                    ct).ConfigureAwait(false);
                return;

            case LoopbackScriptKind.CloseNormallyImmediately:
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", ct).ConfigureAwait(false);
                return;

            case LoopbackScriptKind.StaySilent:
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                return;

            case LoopbackScriptKind.SendScriptedMessages:
                foreach (byte[] payload in _script.OutboundMessages)
                {
                    await socket.SendAsync(
                        new ArraySegment<byte>(payload),
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        ct).ConfigureAwait(false);
                }

                await PumpInboundAsync(socket, echo: false, ct).ConfigureAwait(false);
                return;

            default:
                await PumpInboundAsync(socket, echo: true, ct).ConfigureAwait(false);
                return;
        }
    }

    private async Task PumpInboundAsync(WebSocket socket, bool echo, CancellationToken ct)
    {
        byte[] buffer = new byte[4096];
        var message = new List<byte>();
        while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result = await socket
                .ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, ct).ConfigureAwait(false);
                return;
            }

            for (int i = 0; i < result.Count; i++)
            {
                message.Add(buffer[i]);
            }

            if (!result.EndOfMessage)
            {
                continue;
            }

            byte[] complete = message.ToArray();
            message.Clear();
            lock (_gate)
            {
                _receivedMessages.Add(complete);
                _applicationBytesReceived.AddRange(complete);
            }

            if (echo)
            {
                await socket.SendAsync(
                    new ArraySegment<byte>(complete),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    ct).ConfigureAwait(false);
            }
        }
    }

    private static async Task<string> ReadRequestHeadersAsync(Stream stream, CancellationToken ct)
    {
        var builder = new StringBuilder();
        byte[] one = new byte[1];
        while (!builder.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            int read = await stream.ReadAsync(one, 0, 1, ct).ConfigureAwait(false);
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
        string accept;
        using (var sha1 = SHA1.Create())
        {
            accept = Convert.ToBase64String(sha1.ComputeHash(Encoding.ASCII.GetBytes(key + HandshakeGuid)));
        }

        var response = new StringBuilder()
            .Append("HTTP/1.1 101 Switching Protocols\r\n")
            .Append("Upgrade: websocket\r\n")
            .Append("Connection: Upgrade\r\n")
            .Append("Sec-WebSocket-Accept: ").Append(accept).Append("\r\n");
        if (chosen != null)
        {
            response.Append("Sec-WebSocket-Protocol: ").Append(chosen).Append("\r\n");
        }

        response.Append("\r\n");
        byte[] bytes = Encoding.ASCII.GetBytes(response.ToString());
        await stream.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    private static string? PickSubProtocol(string? header)
    {
        if (string.IsNullOrEmpty(header))
        {
            return null;
        }

        foreach (string candidate in header!.Split(','))
        {
            string trimmed = candidate.Trim();
            if (string.Equals(trimmed, "lumio.mvp.v0", StringComparison.Ordinal))
            {
                return trimmed;
            }
        }

        return null;
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
        if (_accept != null)
        {
            try
            {
                await _accept.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // 关停竞态不是被测行为。
            }
        }

        _cts.Dispose();
    }
}

internal enum LoopbackScriptKind
{
    EchoUntilClosed,
    RejectWithPolicyViolation,
    CloseNormallyImmediately,
    AbortTcpWithoutCloseFrame,
    StaySilent,
    SendScriptedMessages
}

internal sealed class LoopbackWebSocketScript
{
    public LoopbackScriptKind Kind { get; set; } = LoopbackScriptKind.EchoUntilClosed;

    public bool NegotiateSubProtocol { get; set; } = true;

    public IReadOnlyList<byte[]> OutboundMessages { get; set; } = Array.Empty<byte[]>();
}

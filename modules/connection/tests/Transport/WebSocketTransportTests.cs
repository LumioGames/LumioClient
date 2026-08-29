using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Lumio.Client.Connection;
using Lumio.Client.Connection.Tests.Contract;

namespace Lumio.Client.Connection.Tests.Transport;

public sealed class WebSocketTransportTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);
    private static readonly byte[] Credential = { 0xDE, 0xAD, 0xBE, 0xEF, 0x11, 0x22 };
    private static readonly byte[] Nonce = { 0xFE, 0xED, 0xFA, 0xCE };

    // 载荷一律 ASCII：出站按双端约定用 WebSocketMessageType.Text（LumioServer 对称卡同款），
    // 而 .NET 的托管 WebSocket 在收 Text 时会校验 UTF-8，任意二进制会被判定为协议错误。
    private static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);

    private static ClientEndpoint EndpointFor(string uri, TimeSpan? connectTimeout = null)
    {
        return new ClientEndpoint(uri, Credential, Nonce, connectTimeout ?? TimeSpan.FromSeconds(10));
    }

    private static WebSocketClientConnection Connect(
        LoopbackWebSocketServer server,
        WebSocketTransportOptions options,
        out ClientConnectionCreateResult created)
    {
        var factory = new WebSocketClientConnectionFactory(options);
        created = factory.Create(
            new ClientConnectionCreateRequest(1, 64, 32, EndpointFor(server.Uri)),
            out IClientConnection connection);
        return (WebSocketClientConnection)connection;
    }

    private static List<ConnectionEvent> DrainUntil(
        IClientConnection connection,
        Func<List<ConnectionEvent>, bool> satisfied,
        TimeSpan timeout)
    {
        var seen = new List<ConnectionEvent>();
        var buffer = new ConnectionEvent[64];
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            int n = connection.DrainEvents(buffer);
            for (int i = 0; i < n; i++)
            {
                seen.Add(buffer[i]);
            }

            if (satisfied(seen))
            {
                return seen;
            }

            Thread.Sleep(5);
        }

        return seen;
    }

    private static List<ConnectionEvent> DrainUntilTerminal(IClientConnection connection, TimeSpan timeout)
    {
        return DrainUntil(connection, s => s.Any(e => e.Terminal), timeout);
    }

    private static List<ConnectionEvent> DrainUntilFrames(IClientConnection connection, int count, TimeSpan timeout)
    {
        return DrainUntil(
            connection,
            s => s.Count(e => e.Kind == ConnectionEventKind.FrameReceived) >= count,
            timeout);
    }

    // ---------- 子协议位序与协商 ----------

    [Fact]
    public async Task NegotiatedSubProtocolIsExactlyMvpV0()
    {
        await using var server = LoopbackWebSocketServer.Start(new LoopbackWebSocketScript());
        var connection = Connect(server, WebSocketTransportOptions.Default, out var created);
        using (connection)
        {
            Assert.True(created.Succeeded);
            Assert.False(created.HasLoopback);
            Assert.True(connection.Start().Succeeded);
            Assert.True(connection.WaitForOpen(Patience), "WS 通道未在期限内打开");

            Assert.Equal("lumio.mvp.v0", connection.NegotiatedSubProtocol);
        }
    }

    [Fact]
    public async Task SubProtocolOfferCarriesThreeSegmentsInDeclaredOrder()
    {
        await using var server = LoopbackWebSocketServer.Start(new LoopbackWebSocketScript());
        var connection = Connect(server, WebSocketTransportOptions.Default, out _);
        using (connection)
        {
            connection.Start();
            Assert.True(connection.WaitForOpen(Patience));

            string header = Assert.IsType<string>(server.RequestSubProtocolHeader);
            string[] segments = header.Split(',').Select(s => s.Trim()).ToArray();
            Assert.Equal(3, segments.Length);
            Assert.Equal("lumio.mvp.v0", segments[0]);
            Assert.Equal(MvpChannelAuth.ToBase64Url(Credential), segments[1]);
            Assert.Equal(MvpChannelAuth.ToBase64Url(Nonce), segments[2]);

            // base64url 必须去 padding：'=' 不是 RFC 7230 token 合法字符，
            // ClientWebSocket.Options.AddSubProtocol 会直接拒绝。
            Assert.DoesNotContain('=', segments[1]);
            Assert.DoesNotContain('=', segments[2]);
        }
    }

    [Fact]
    public async Task SubProtocolNotEchoedByPeerIsRejected()
    {
        await using var server = LoopbackWebSocketServer.Start(
            new LoopbackWebSocketScript { NegotiateSubProtocol = false });
        var connection = Connect(server, WebSocketTransportOptions.Default, out _);
        using (connection)
        {
            connection.Start();
            var seen = DrainUntilTerminal(connection, Patience);

            Assert.Contains(seen, e => e.Kind == ConnectionEventKind.Faulted && e.Terminal);
            Assert.DoesNotContain(seen, e => e.Kind == ConnectionEventKind.FrameReceived);
        }
    }

    // ---------- 一 WS 消息 = 一 Envelope ----------

    [Fact]
    public async Task EachWebSocketMessageBecomesExactlyOneFrameEvent()
    {
        var payloads = new[] { Ascii("alpha"), Ascii("bravo"), Ascii("charlie") };
        await using var server = LoopbackWebSocketServer.Start(new LoopbackWebSocketScript
        {
            Kind = LoopbackScriptKind.SendScriptedMessages,
            OutboundMessages = payloads
        });

        var connection = Connect(server, WebSocketTransportOptions.Default, out _);
        using (connection)
        {
            connection.Start();
            var seen = DrainUntilFrames(connection, 3, Patience);
            var frames = seen.Where(e => e.Kind == ConnectionEventKind.FrameReceived).ToArray();

            Assert.Equal(3, frames.Length);
            for (int i = 0; i < payloads.Length; i++)
            {
                Assert.Equal(payloads[i], frames[i].Frame.Bytes.ToArray());
            }
        }
    }

    [Fact]
    public async Task MessageSpanningManyReceiveCallsIsDeliveredExactlyOnce()
    {
        // 40KB ≫ 8KB 接收缓冲：一条 WS 消息会跨多次 ReceiveAsync 返回，
        // 只有 EndOfMessage 为真时才允许交付，且必须是一条事件、不是多条。
        byte[] payload = Ascii(new string('x', 40 * 1024));
        await using var server = LoopbackWebSocketServer.Start(new LoopbackWebSocketScript
        {
            Kind = LoopbackScriptKind.SendScriptedMessages,
            OutboundMessages = new[] { payload }
        });

        var options = new WebSocketTransportOptions(65536, 8192, TimeSpan.FromSeconds(30));
        var connection = Connect(server, options, out _);
        using (connection)
        {
            connection.Start();
            var seen = DrainUntilFrames(connection, 1, Patience);
            Thread.Sleep(200);
            var extra = new ConnectionEvent[16];
            int more = connection.DrainEvents(extra);
            for (int i = 0; i < more; i++)
            {
                seen.Add(extra[i]);
            }

            var frames = seen.Where(e => e.Kind == ConnectionEventKind.FrameReceived).ToArray();
            Assert.Single(frames);
            Assert.Equal(payload, frames[0].Frame.Bytes.ToArray());
        }
    }

    [Fact]
    public async Task RoundTripsFrameThroughRealWebSocket()
    {
        await using var server = LoopbackWebSocketServer.Start(new LoopbackWebSocketScript());
        var connection = Connect(server, WebSocketTransportOptions.Default, out _);
        using (connection)
        {
            connection.Start();
            Assert.True(connection.WaitForOpen(Patience));

            byte[] payload = Ascii("{\"probe\":1}");
            Assert.True(connection.TrySend(new EncodedFrame(payload)).Accepted);

            var seen = DrainUntilFrames(connection, 1, Patience);
            var frame = seen.Single(e => e.Kind == ConnectionEventKind.FrameReceived);
            Assert.Equal(payload, frame.Frame.Bytes.ToArray());
            Assert.Equal(new[] { payload }, server.ReceivedMessages);
        }
    }

    // ---------- 超限在分配前拒绝 ----------

    [Fact]
    public async Task OversizeMessageAbortsBeforeGrowingAllocation()
    {
        const int cap = 16384;
        const int receiveBuffer = 4096;
        byte[] payload = Ascii(new string('y', 256 * 1024));
        await using var server = LoopbackWebSocketServer.Start(new LoopbackWebSocketScript
        {
            Kind = LoopbackScriptKind.SendScriptedMessages,
            OutboundMessages = new[] { payload }
        });

        var options = new WebSocketTransportOptions(cap, receiveBuffer, TimeSpan.FromSeconds(30));
        var connection = Connect(server, options, out _);
        using (connection)
        {
            connection.Start();
            var seen = DrainUntilTerminal(connection, Patience);

            Assert.Contains(seen, e => e.Terminal);
            Assert.DoesNotContain(seen, e => e.Kind == ConnectionEventKind.FrameReceived);
            Assert.True(connection.OversizeRejected, "超限必须被识别为可拒绝，而不是静默丢弃");

            // 关键断言：过程中单次分配不超过固定接收缓冲，绝不按对端声称的长度分配。
            Assert.True(
                connection.LargestReceiveAllocationBytes <= receiveBuffer,
                "单次分配 " + connection.LargestReceiveAllocationBytes + "B 超过接收缓冲 " + receiveBuffer + "B");
            Assert.True(connection.ApplicationBytesReceived <= cap + receiveBuffer);
        }
    }

    [Fact]
    public void OptionsRejectCapAboveRegisteredCeiling()
    {
        Assert.Equal(65536, WebSocketTransportOptions.DefaultMaxMessageBytes);
        Assert.Equal(1048576, WebSocketTransportOptions.MaxAllowedMessageBytes);
        Assert.Equal(65536, WebSocketTransportOptions.DeclaredMaxFragmentBytes);

        var over = new WebSocketTransportOptions(1048577, 8192, TimeSpan.FromSeconds(15));
        Assert.False(over.TryValidate(out string reason));
        Assert.Contains("1048576", reason, StringComparison.Ordinal);

        Assert.True(WebSocketTransportOptions.Default.TryValidate(out _));
    }

    // ---------- 断线检测三源 ----------

    [Fact]
    public async Task PeerCloseFrameProducesTerminalEvent()
    {
        await using var server = LoopbackWebSocketServer.Start(
            new LoopbackWebSocketScript { Kind = LoopbackScriptKind.CloseNormallyImmediately });
        var connection = Connect(server, WebSocketTransportOptions.Default, out _);
        using (connection)
        {
            connection.Start();
            var seen = DrainUntilTerminal(connection, Patience);

            var terminal = Assert.Single(seen.Where(e => e.Terminal));
            Assert.Equal(ConnectionEventKind.Disconnected, terminal.Kind);
        }
    }

    [Fact]
    public async Task UnderlyingReceiveThrowProducesTerminalEvent()
    {
        await using var server = LoopbackWebSocketServer.Start(
            new LoopbackWebSocketScript { Kind = LoopbackScriptKind.AbortTcpWithoutCloseFrame });
        var connection = Connect(server, WebSocketTransportOptions.Default, out _);
        using (connection)
        {
            connection.Start();
            var seen = DrainUntilTerminal(connection, Patience);

            var terminal = Assert.Single(seen.Where(e => e.Terminal));
            Assert.Equal(ConnectionEventKind.Faulted, terminal.Kind);
        }
    }

    [Fact]
    public async Task IdleDeadlineProducesTerminalEvent()
    {
        await using var server = LoopbackWebSocketServer.Start(
            new LoopbackWebSocketScript { Kind = LoopbackScriptKind.StaySilent });
        var options = new WebSocketTransportOptions(65536, 8192, TimeSpan.FromMilliseconds(250));
        var connection = Connect(server, options, out _);
        using (connection)
        {
            connection.Start();
            var seen = DrainUntilTerminal(connection, Patience);

            var terminal = Assert.Single(seen.Where(e => e.Terminal));
            Assert.Equal(ConnectionEventKind.Disconnected, terminal.Kind);
            Assert.True(connection.IdleDeadlineExpired, "空闲截止必须是可诊断的断线来源");
        }
    }

    // ---------- 通道认证失败：close 1008 ----------

    [Fact]
    public async Task ChannelAuthRejectionClosesWithZeroApplicationBytes()
    {
        await using var server = LoopbackWebSocketServer.Start(
            new LoopbackWebSocketScript { Kind = LoopbackScriptKind.RejectWithPolicyViolation });
        var connection = Connect(server, WebSocketTransportOptions.Default, out _);
        using (connection)
        {
            connection.Start();
            var seen = DrainUntilTerminal(connection, Patience);

            var terminal = Assert.Single(seen.Where(e => e.Terminal));
            Assert.Equal(ConnectionEventKind.Faulted, terminal.Kind);
            Assert.True(connection.ChannelAuthRejected);

            // 此前零字节应用数据 —— 两个方向都断言。
            Assert.Equal(0, connection.ApplicationBytesReceived);
            Assert.Empty(server.ApplicationBytesReceived);
            Assert.DoesNotContain(seen, e => e.Kind == ConnectionEventKind.FrameReceived);
        }
    }

    // ---------- 凭据遏制（本轮真正带电的那条） ----------

    [Fact]
    public async Task CredentialsNeverReachApplicationBytesEventsOrRenderings()
    {
        await using var server = LoopbackWebSocketServer.Start(new LoopbackWebSocketScript());
        var connection = Connect(server, WebSocketTransportOptions.Default, out _);
        using (connection)
        {
            connection.Start();
            Assert.True(connection.WaitForOpen(Patience));
            Assert.True(connection.TrySend(new EncodedFrame(Ascii("{\"probe\":2}"))).Accepted);
            var seen = DrainUntilFrames(connection, 1, Patience);

            // ① 出站方向：服务端见到的全部应用数据里不得出现凭据 / nonce 字节。
            byte[] outbound = server.ApplicationBytesReceived;
            Assert.NotEmpty(outbound);
            Assert.False(ContainsSequence(outbound, Credential), "凭据字节泄漏进出站应用数据");
            Assert.False(ContainsSequence(outbound, Nonce), "nonce 字节泄漏进出站应用数据");

            // ② 入站方向：drain 出的事件与其渲染都不得回显凭据。
            foreach (var evt in seen)
            {
                Assert.False(ContainsSequence(evt.Frame.Bytes.ToArray(), Credential));
                Assert.False(ContainsSequence(evt.Frame.Bytes.ToArray(), Nonce));
                ClientEndpointTests.AssertNoSecretSpelling(evt.ToString() ?? string.Empty, Credential);
                ClientEndpointTests.AssertNoSecretSpelling(evt.ToString() ?? string.Empty, Nonce);
            }

            ClientEndpointTests.AssertNoSecretSpelling(connection.GetSnapshot().ToString() ?? string.Empty, Credential);
            ClientEndpointTests.AssertNoSecretSpelling(connection.ToString() ?? string.Empty, Credential);
            ClientEndpointTests.AssertNoSecretSpelling(connection.DescribeForDiagnostics(), Credential);
            ClientEndpointTests.AssertNoSecretSpelling(connection.DescribeForDiagnostics(), Nonce);
        }
    }

    // ---------- 生命周期语义与 LocalEmbedded 一致 ----------

    [Fact]
    public async Task LifecycleMatchesLocalEmbeddedSemantics()
    {
        await using var server = LoopbackWebSocketServer.Start(new LoopbackWebSocketScript());
        var connection = Connect(server, WebSocketTransportOptions.Default, out _);
        using (connection)
        {
            // 未 Start 不可发送。
            Assert.False(connection.TrySend(new EncodedFrame(Ascii("nope"))).Accepted);

            Assert.True(connection.Start().Succeeded);
            Assert.False(connection.Start().Succeeded, "重复 Start 必须失败");
            Assert.True(connection.WaitForOpen(Patience));

            // 空帧不可发送（与 ConnectionStateMachine.CanSend 一致）。
            Assert.False(connection.TrySend(new EncodedFrame(Array.Empty<byte>())).Accepted);

            // 代次不匹配的迟到回调不得被接受。
            Assert.True(connection.DeliverCallback(new ConnectionGeneration(1)));
            Assert.False(connection.DeliverCallback(new ConnectionGeneration(2)));

            Assert.True(connection.RequestClose(ConnectionCloseReason.OwnerRequest).Succeeded);
            Assert.False(connection.RequestClose(ConnectionCloseReason.OwnerRequest).Succeeded, "重复关闭必须幂等");

            // 终态后拒绝发送。
            Assert.False(connection.TrySend(new EncodedFrame(Ascii("after-close"))).Accepted);
            Assert.True(connection.GetSnapshot().Terminal);
            Assert.False(connection.DeliverCallback(new ConnectionGeneration(1)), "终态后迟到回调不得被接受");
        }
    }

    [Fact]
    public void GenerationIsCarriedFromCreateRequest()
    {
        var factory = new WebSocketClientConnectionFactory();
        factory.Create(
            new ClientConnectionCreateRequest(7, 16, 8, EndpointFor("ws://127.0.0.1:1/session")),
            out IClientConnection connection);
        using ((IDisposable)connection)
        {
            Assert.Equal(7UL, connection.Generation.Value);
        }
    }

    // ---------- Endpoint 不合法：拨号前拒绝 ----------

    [Theory]
    [InlineData("")]
    [InlineData("not-a-uri")]
    [InlineData("http://127.0.0.1:9/session")]
    [InlineData("ws://127.0.0.1:9/session?token=leak")]
    [InlineData("wss://user:pass@127.0.0.1:9/session")]
    [InlineData("ws://127.0.0.1:9/session#frag")]
    public void MalformedEndpointIsRejectedBeforeDialing(string uri)
    {
        var factory = new WebSocketClientConnectionFactory();
        var created = factory.Create(
            new ClientConnectionCreateRequest(1, 16, 8, EndpointFor(uri)),
            out IClientConnection connection);
        using ((IDisposable)connection)
        {
            Assert.False(created.Succeeded);
            Assert.False(created.HasLoopback);

            // 拒绝必须是可观测的终态，而不是静默不动。
            Assert.False(connection.Start().Succeeded);
            var buffer = new ConnectionEvent[8];
            int n = connection.DrainEvents(buffer);
            Assert.True(n > 0);
            Assert.Contains(buffer.Take(n), e => e.Kind == ConnectionEventKind.Faulted && e.Terminal);
            Assert.True(connection.GetSnapshot().Terminal);
        }
    }

    [Fact]
    public void BothWsAndWssSchemesAreAccepted()
    {
        // A1-α 全程 ws://，但 endpoint 值类型必须同时容纳 wss://（CC-5 的 --transport ws|wss）。
        Assert.True(EndpointFor("ws://127.0.0.1:8080/session").TryValidate(out _));
        Assert.True(EndpointFor("wss://host.example:443/session").TryValidate(out _));
    }

    private static bool ContainsSequence(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
        {
            return false;
        }

        for (int i = 0; i + needle.Length <= haystack.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return true;
            }
        }

        return false;
    }
}

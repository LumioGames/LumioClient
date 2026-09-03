using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using Lumio.Client.Connection;
using Lumio.Client.Handshake;
using Lumio.Client.Input;
using Lumio.Client.Observability;
using Lumio.Client.Persistence;
using Lumio.Client.Prediction;
using Lumio.Client.Replica;
using Lumio.Client.Session;
using Lumio.Client.Session.Tests.Support;

namespace Lumio.Client.Session.Tests.Unit;

public sealed class SessionConnectionSupersededTests
{
    private static readonly byte[] Hello = Encoding.ASCII.GetBytes("HELLO");
    private static readonly byte[] Credential = { 0xDE, 0xAD, 0xBE, 0xEF };
    private static readonly byte[] Nonce = { 0xFE, 0xED, 0xFA, 0xCE };

    [Fact]
    public void ConnectionSupersededOnLocalEmbeddedDoesNotReconnect()
    {
        var harness = new SessionHarness(runtimeCommitted: true);
        harness.HappyPathToActive();
        Assert.Equal(ClientSessionState.Active, harness.Session.GetSnapshot().State);
        ulong generation = harness.Session.GetSnapshot().Generation;
        byte[] superseded = Encoding.UTF8.GetBytes(
            "{\"messageType\":\"ConnectionSuperseded\",\"reasonCode\":\"connection_superseded\",\"netEntityId\":101,\"newConnectionGeneration\":2}");
        harness.Deliver(superseded);
        harness.Tick();
        Assert.Equal(ClientSessionState.Superseded, harness.Session.GetSnapshot().State);
        Assert.True(harness.Session.TryDequeueSuperseded(out SessionSupersededNotice notice));
        Assert.Equal("connection_superseded", notice.ReasonCode);
        Assert.False(harness.Session.RequestConnect(new SessionConnectRequest(generation), CancellationToken.None).Succeeded);
        harness.Tick();
        Assert.Equal(ClientSessionState.Superseded, harness.Session.GetSnapshot().State);
        Assert.Equal(generation, harness.Session.GetSnapshot().Generation);
        Assert.True(harness.Session.Login(new SessionConnectRequest(generation + 1), CancellationToken.None).Succeeded);
        Assert.True(harness.Session.GetSnapshot().Generation > generation);
        Assert.NotEqual(ClientSessionState.Superseded, harness.Session.GetSnapshot().State);
    }

    [Fact]
    public async Task ConnectionSupersededStopsAtLoginStateAndDoesNotReconnect()
    {
        byte[] superseded = Encoding.UTF8.GetBytes(
            "{\"messageType\":\"ConnectionSuperseded\",\"reasonCode\":\"connection_superseded\",\"netEntityId\":101,\"newConnectionGeneration\":2}");
        await using var server = LoopbackSessionServer.Start(new[]
        {
            Hello,
            ReplicaC1Frames.EmptyFullSnapshot,
            superseded
        });

        var harness = new RemoteSessionHarness(server.Uri);
        Assert.True(harness.Session.Login(
            new SessionConnectRequest(1, new ClientEndpoint(server.Uri, Credential, Nonce, TimeSpan.FromSeconds(10))),
            CancellationToken.None).Succeeded);

        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            ClientSessionState current = harness.Session.GetSnapshot().State;
            if (current == ClientSessionState.Superseded || current == ClientSessionState.Faulted || current == ClientSessionState.Closed)
            {
                break;
            }

            harness.Session.Tick(new ClientOwnerTick(1));
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        ClientSessionSnapshot snap = harness.Session.GetSnapshot();
        Assert.Equal(ClientSessionState.Superseded, snap.State);
        ulong generation = snap.Generation;
        Assert.True(harness.Session.TryDequeueSuperseded(out SessionSupersededNotice notice));
        Assert.Equal("connection_superseded", notice.ReasonCode);
        Assert.False(harness.Session.RequestConnect(new SessionConnectRequest(generation), CancellationToken.None).Succeeded);
        Assert.Equal(ClientSessionState.Superseded, harness.Session.GetSnapshot().State);
        Assert.Equal(generation, harness.Session.GetSnapshot().Generation);

        harness.Session.Tick(new ClientOwnerTick(2));
        Assert.Equal(ClientSessionState.Superseded, harness.Session.GetSnapshot().State);
        Assert.Equal(generation, harness.Session.GetSnapshot().Generation);

        harness.Session.Login(
            new SessionConnectRequest(generation + 1, new ClientEndpoint(server.Uri, Credential, Nonce, TimeSpan.FromSeconds(10))),
            CancellationToken.None);
        Assert.True(harness.Session.GetSnapshot().Generation > generation);
        Assert.NotEqual(ClientSessionState.Superseded, harness.Session.GetSnapshot().State);
    }

    private sealed class RemoteSessionHarness
    {
        public RemoteSessionHarness(string uri)
        {
            _ = uri;
            Ingress = new InputSampleIngress(16);
            var options = new ClientEventPipelineOptions(8, 4, TimeSpan.FromSeconds(1));
            new ClientEventPipelineFactory().Create(in options, new InMemoryClientEventSink(8), out var writer);
            var deps = new ClientSessionDependencies(
                new WebSocketClientConnectionFactory(),
                new ClientHandshakeFactory(),
                new OkCapability(),
                new AsciiHelloClassifier(),
                Ingress,
                new InputCommandSource(Ingress, new PassThroughMapper()),
                IClientPersistenceFactory.CreateMemory().CreateVerifiedSessionArtifactSource(),
                writer,
                new RecordingRuntime(true),
                new ClientReplicaFactory(),
                new ClientPredictionFactory(),
                new ImmediateGameplayScopeActivator(),
                new NullPresentationSink(),
                new JsonSessionMessageKindMap());
            new ClientSessionFactory().Create(in deps, out IClientSession session);
            Session = session;
        }

        public IClientSession Session { get; }

        public InputSampleIngress Ingress { get; }
    }

    private sealed class AsciiHelloClassifier : IHandshakeFrameClassifier
    {
        public HandshakeOpaqueFrameRole Classify(ReadOnlyMemory<byte> frame)
        {
            return frame.Span.SequenceEqual(Hello)
                ? HandshakeOpaqueFrameRole.ServerHello
                : HandshakeOpaqueFrameRole.Unclassified;
        }
    }

    private sealed class OkCapability : IPlatformCapabilityProvider
    {
        public ValueTask<PlatformCapabilityResult> QueryAsync(in PlatformCapabilityQuery query, CancellationToken cancellationToken)
        {
            return new ValueTask<PlatformCapabilityResult>(new PlatformCapabilityResult(query.Attempt, query.Generation, true));
        }
    }

    private sealed class PassThroughMapper : IGameInputMapper
    {
        public bool TryMap(in SequencedInputSample sample, in InputDrainContext context, out GameplayCommandCandidate candidate)
        {
            candidate = new GameplayCommandCandidate(sample.Sequence, new byte[] { 0x42 });
            return true;
        }
    }

    private sealed class LoopbackSessionServer : IAsyncDisposable
    {
        private const string HandshakeGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        private readonly TcpListener _listener;
        private readonly byte[][] _outbound;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _accept;

        private LoopbackSessionServer(byte[][] outbound)
        {
            _outbound = outbound;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Uri = "ws://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/session";
            _accept = Task.Run(() => RunAsync(_cts.Token));
        }

        public static LoopbackSessionServer Start(byte[][] outbound) => new(outbound);

        public string Uri { get; }

        private async Task RunAsync(CancellationToken ct)
        {
            TcpClient? client = null;
            try
            {
                client = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                NetworkStream stream = client.GetStream();
                string request = await ReadRequestHeadersAsync(stream, ct).ConfigureAwait(false);
                string key = ReadHeader(request, "Sec-WebSocket-Key") ?? string.Empty;
                string? chosen = PickSubProtocol(ReadHeader(request, "Sec-WebSocket-Protocol"));
                await WriteHandshakeResponseAsync(stream, key, chosen, ct).ConfigureAwait(false);
                using WebSocket socket = WebSocket.CreateFromStream(stream, isServer: true, subProtocol: chosen, keepAliveInterval: TimeSpan.FromSeconds(30));
                foreach (byte[] payload in _outbound)
                {
                    await socket.SendAsync(payload, WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
                }

                await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "superseded", ct).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
            finally
            {
                client?.Dispose();
            }
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
#pragma warning disable CA5350
            string accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + HandshakeGuid)));
#pragma warning restore CA5350
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
            await stream.WriteAsync(bytes.AsMemory(), ct).ConfigureAwait(false);
        }

        private static string? PickSubProtocol(string? header)
        {
            if (string.IsNullOrEmpty(header))
            {
                return null;
            }

            foreach (string candidate in header.Split(','))
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
            foreach (string line in request.Split(new[] { "\r\n" }, StringSplitOptions.None))
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
            try
            {
                await _accept.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            _cts.Dispose();
        }
    }
}

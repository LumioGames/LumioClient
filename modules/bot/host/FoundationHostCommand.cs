using Lumio.Client.Bot;
using Lumio.Client.Connection;
using Lumio.Client.Handshake;
using Lumio.Client.Input;
using Lumio.Client.Observability;
using Lumio.Client.Persistence;
using Lumio.Client.Prediction;
using Lumio.Client.Replica;
using Lumio.Client.Session;

namespace Lumio.Client.Bot.Host;

public static class FoundationHostCommand
{
    public static readonly byte[] Hello = { 0xA5, 0x3C, 0x91, 0x07, 0xD2, 0x4E, 0xB8, 0x11 };

    public static readonly byte[] Snapshot = { 0x10, 0x32, 0x54, 0x76, 0x98, 0xBA, 0xDC, 0xFE };

    public static readonly byte[] Gap = { 0x91, 0xA9, 0xB0, 0xC3 };

    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        bool foundation = false;
        string transport = "local-embedded";
        string fixture = "foundation-happy-path";
        if (args != null)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "foundation", StringComparison.OrdinalIgnoreCase))
                {
                    foundation = true;
                }
                else if (string.Equals(args[i], "--transport", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    i++;
                    transport = args[i];
                }
                else if (string.Equals(args[i], "--fixture", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    i++;
                    fixture = args[i];
                }
            }
        }

        if (args == null || args.Length == 0)
        {
            foundation = true;
        }

        if (!foundation)
        {
            return 2;
        }

        if (!string.Equals(transport, "local-embedded", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (!string.Equals(fixture, "foundation-happy-path", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        var connections = new CapturingConnectionFactory();
        var ingress = new InputSampleIngress(16);
        var options = new ClientEventPipelineOptions(8, 4, TimeSpan.FromSeconds(1));
        new ClientEventPipelineFactory().Create(in options, new InMemoryClientEventSink(8), out var writer);
        var deps = new ClientSessionDependencies(
            connections,
            new ClientHandshakeFactory(),
            new HostCapability(),
            new HelloClassifier(),
            ingress,
            new InputCommandSource(ingress, new HostInputMapper()),
            IClientPersistenceFactory.CreateMemory().CreateVerifiedSessionArtifactSource(),
            writer,
            new HostRuntime(),
            new ClientReplicaFactory(),
            new ClientPredictionFactory(),
            new ImmediateGameplayScopeActivator(),
            new NullPresentationSink(),
            new FixtureMessageMap());
        new ClientSessionFactory().Create(in deps, out IClientSession session);
        var hook = new FoundationPeer(connections);
        var host = new HeadlessBotHost(session, new DeterministicBotDriver(), ingress, hook);
        int code = await host.RunAsync(new BotRunRequest(5, 0), cancellationToken);
        ClientSessionSnapshot snap = session.GetSnapshot();
        if (snap.State == ClientSessionState.Faulted)
        {
            return 1;
        }

        if (snap.State != ClientSessionState.Closed)
        {
            return 5;
        }

        if (connections.Loopback.EncodeCalls < 1 || connections.Loopback.DecodeCalls < 1)
        {
            return 6;
        }

        return code;
    }

    private sealed class FoundationPeer : IBotTickHook
    {
        private readonly CapturingConnectionFactory _connections;

        public FoundationPeer(CapturingConnectionFactory connections)
        {
            _connections = connections;
        }

        public void BeforeTick(int tick)
        {
            if (_connections.Loopback == null)
            {
                return;
            }

            if (tick == 0)
            {
                _connections.Loopback.TryDeliverToClient(new EncodedFrame(Hello));
            }
            else if (tick == 1)
            {
                _connections.Loopback.TryDeliverToClient(new EncodedFrame(Snapshot));
            }
            else if (tick == 2)
            {
                _connections.Loopback.TryDeliverToClient(new EncodedFrame(Gap));
            }
            else if (tick == 3)
            {
                _connections.Loopback.TryDeliverToClient(new EncodedFrame(Snapshot));
            }
        }
    }

    internal sealed class CapturingConnectionFactory : IClientConnectionFactory
    {
        public LocalEmbeddedLoopback Loopback { get; private set; } = default!;

        public ClientConnectionCreateResult Create(in ClientConnectionCreateRequest request, out IClientConnection connection)
        {
            ClientConnectionCreateResult result = new ClientConnectionFactory().Create(in request, out connection);
            Loopback = result.Loopback;
            return result;
        }
    }

    private sealed class HelloClassifier : IHandshakeFrameClassifier
    {
        public HandshakeOpaqueFrameRole Classify(ReadOnlyMemory<byte> frame)
        {
            if (frame.Span.SequenceEqual(Hello))
            {
                return HandshakeOpaqueFrameRole.ServerHello;
            }

            return HandshakeOpaqueFrameRole.Unclassified;
        }
    }

    private sealed class FixtureMessageMap : ISessionMessageKindMap
    {
        public SessionMessageKind Map(ReadOnlyMemory<byte> frame)
        {
            if (frame.Span.SequenceEqual(Snapshot))
            {
                return SessionMessageKind.FullSnapshot;
            }

            if (frame.Span.SequenceEqual(Gap))
            {
                return SessionMessageKind.Gap;
            }

            return SessionMessageKind.Unknown;
        }
    }

    private sealed class HostCapability : IPlatformCapabilityProvider
    {
        public ValueTask<PlatformCapabilityResult> QueryAsync(in PlatformCapabilityQuery query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<PlatformCapabilityResult>(new PlatformCapabilityResult(query.Attempt, query.Generation, true));
        }
    }

    private sealed class HostInputMapper : IGameInputMapper
    {
        public bool TryMap(in SequencedInputSample sample, in InputDrainContext context, out GameplayCommandCandidate candidate)
        {
            _ = context;
            candidate = new GameplayCommandCandidate(sample.Sequence, new byte[] { 0x42 });
            return true;
        }
    }

    private sealed class HostRuntime : IClientRuntimePort
    {
        public ValueTask<RuntimeTransactionOutcome> ApplyAuthoritativeTransaction(in RuntimeTransactionRequest request, CancellationToken cancellationToken)
        {
            _ = request;
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<RuntimeTransactionOutcome>(new RuntimeTransactionOutcome(true));
        }

        public ValueTask<RuntimeTransactionOutcome> ApplyLocalPrediction(in RuntimeTransactionRequest request, CancellationToken cancellationToken)
        {
            _ = request;
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<RuntimeTransactionOutcome>(new RuntimeTransactionOutcome(true));
        }
    }
}

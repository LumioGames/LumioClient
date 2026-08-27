using Lumio.Client.Connection;
using Lumio.Client.Handshake;
using Lumio.Client.Input;
using Lumio.Client.Observability;
using Lumio.Client.Persistence;
using Lumio.Client.Prediction;
using Lumio.Client.Replica;
using Lumio.Client.Session;

namespace Lumio.Client.Session.Tests.Support;

internal static class SessionTestBytes
{
    public static readonly byte[] Hello = { 0xA5, 0x3C, 0x91, 0x07, 0xD2, 0x4E, 0xB8, 0x11 };

    public static readonly byte[] Reject = { 0x5A, 0xC3, 0x0E, 0xF4 };

    public static readonly byte[] Snapshot = { 0x10, 0x32, 0x54, 0x76, 0x98, 0xBA, 0xDC, 0xFE };

    public static readonly byte[] Gap = { 0x91, 0xA9, 0xB0, 0xC3 };
}

internal sealed class SessionHarness
{
    public SessionHarness(bool runtimeCommitted)
        : this(runtimeCommitted, false)
    {
    }

    public SessionHarness(bool runtimeCommitted, bool indeterminate)
    {
        Connections = new CapturingConnectionFactory();
        Scope = new ImmediateGameplayScopeActivator();
        Presentation = new NullPresentationSink();
        Runtime = new RecordingRuntime(runtimeCommitted, indeterminate);
        Ingress = new InputSampleIngress(16);
        Commands = new InputCommandSource(Ingress, new PassThroughMapper());
        var options = new ClientEventPipelineOptions(8, 4, TimeSpan.FromSeconds(1));
        new ClientEventPipelineFactory().Create(in options, new InMemoryClientEventSink(8), out var writer);
        var deps = new ClientSessionDependencies(
            Connections,
            new ClientHandshakeFactory(),
            new OkCapability(),
            new HelloClassifier(),
            Ingress,
            Commands,
            IClientPersistenceFactory.CreateMemory().CreateVerifiedSessionArtifactSource(),
            writer,
            Runtime,
            new ClientReplicaFactory(),
            new ClientPredictionFactory(),
            Scope,
            Presentation,
            new FixtureMessageMap());
        new ClientSessionFactory().Create(in deps, out var session);
        Session = session;
    }

    public CapturingConnectionFactory Connections { get; }

    public ImmediateGameplayScopeActivator Scope { get; }

    public NullPresentationSink Presentation { get; }

    public RecordingRuntime Runtime { get; }

    public InputSampleIngress Ingress { get; }

    public IInputCommandSource Commands { get; }

    public IClientSession Session { get; }

    public void Connect(ulong generation = 1)
    {
        Session.RequestConnect(new SessionConnectRequest(generation), CancellationToken.None);
    }

    public void Tick()
    {
        Session.Tick(new ClientOwnerTick(1));
    }

    public void Deliver(byte[] bytes)
    {
        Connections.Loopback.TryDeliverToClient(new EncodedFrame(bytes));
    }

    public void HappyPathToActive()
    {
        Connect();
        Tick();
        Deliver(SessionTestBytes.Hello);
        Tick();
        Deliver(SessionTestBytes.Snapshot);
        Tick();
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
            if (frame.Span.SequenceEqual(SessionTestBytes.Hello))
            {
                return HandshakeOpaqueFrameRole.ServerHello;
            }

            if (frame.Span.SequenceEqual(SessionTestBytes.Reject))
            {
                return HandshakeOpaqueFrameRole.HandshakeReject;
            }

            return HandshakeOpaqueFrameRole.Unclassified;
        }
    }

    private sealed class FixtureMessageMap : ISessionMessageKindMap
    {
        public SessionMessageKind Map(ReadOnlyMemory<byte> frame)
        {
            if (frame.Span.SequenceEqual(SessionTestBytes.Snapshot))
            {
                return SessionMessageKind.FullSnapshot;
            }

            if (frame.Span.SequenceEqual(SessionTestBytes.Gap))
            {
                return SessionMessageKind.Gap;
            }

            return SessionMessageKind.Unknown;
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
}

internal sealed class RecordingRuntime : IClientRuntimePort
{
    private readonly bool _committed;
    private readonly bool _indeterminate;

    public RecordingRuntime(bool committed)
        : this(committed, false)
    {
    }

    public RecordingRuntime(bool committed, bool indeterminate)
    {
        _committed = committed;
        _indeterminate = indeterminate;
    }

    public int AuthorityCalls { get; private set; }

    public int LocalCalls { get; private set; }

    public ValueTask<RuntimeTransactionOutcome> ApplyAuthoritativeTransaction(in RuntimeTransactionRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        cancellationToken.ThrowIfCancellationRequested();
        AuthorityCalls++;
        if (_indeterminate)
        {
            return new ValueTask<RuntimeTransactionOutcome>(RuntimeTransactionOutcome.IndeterminateOutcome());
        }

        return new ValueTask<RuntimeTransactionOutcome>(new RuntimeTransactionOutcome(_committed));
    }

    public ValueTask<RuntimeTransactionOutcome> ApplyLocalPrediction(in RuntimeTransactionRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        cancellationToken.ThrowIfCancellationRequested();
        LocalCalls++;
        if (_indeterminate)
        {
            return new ValueTask<RuntimeTransactionOutcome>(RuntimeTransactionOutcome.IndeterminateOutcome());
        }

        return new ValueTask<RuntimeTransactionOutcome>(new RuntimeTransactionOutcome(_committed));
    }
}

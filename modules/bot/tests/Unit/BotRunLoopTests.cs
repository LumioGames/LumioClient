using Lumio.Client.Bot;
using Lumio.Client.Bot.Tests.Support;
using Lumio.Client.Connection;
using Lumio.Client.Handshake;
using Lumio.Client.Input;
using Lumio.Client.Observability;
using Lumio.Client.Persistence;
using Lumio.Client.Prediction;
using Lumio.Client.Replica;
using Lumio.Client.Session;

namespace Lumio.Client.Bot.Tests.Unit;

public sealed class BotRunLoopTests
{
    [Fact]
    public async Task BotRunLoop_CompletesWithExitCode()
    {
        var ingress = new InputSampleIngress(8);
        var session = BotSessionFactory.Create(ingress);
        var host = new HeadlessBotHost(session, new DeterministicBotDriver(), ingress);
        int code = await host.RunAsync(new BotRunRequest(3, 0), CancellationToken.None);
        Assert.Equal(0, code);
        Assert.Equal(ClientSessionState.Closed, session.GetSnapshot().State);
    }

    [Fact]
    public async Task BotRunLoop_FillEnqueueSessionTickObserve()
    {
        var order = new List<string>();
        var timer = new ClientTimerManager(new C4TickFrameAbi());
        var host = new HeadlessBotHost(
            new RecordingSession(order),
            new RecordingDriver(order),
            new RecordingIngress(order),
            new NullTickHook(),
            timer);
        int code = await host.RunAsync(new BotRunRequest(5, 0), CancellationToken.None);
        Assert.Equal(0, code);
        Assert.Equal(new ulong[] { 5 }, host.SubmittedTicks.ToArray());
        Assert.Contains("fill", order);
        Assert.Contains("enqueue", order);
        Assert.Contains("tick", order);
        Assert.Contains("observe", order);
    }

    [Fact]
    public async Task BotQueueFull_DoesNotThrow()
    {
        var ingress = new InputSampleIngress(1);
        Assert.True(ingress.TryEnqueue(new RawInputSample(1, 0, 0)).Accepted);
        var session = BotSessionFactory.Create(ingress);
        var host = new HeadlessBotHost(session, new DeterministicBotDriver(), ingress);
        int code = await host.RunAsync(new BotRunRequest(2, 0), CancellationToken.None);
        Assert.Equal(0, code);
        Assert.Equal(ClientSessionState.Closed, session.GetSnapshot().State);
    }

    private sealed class NullTickHook : IBotTickHook
    {
        public void BeforeTick(int tick)
        {
            _ = tick;
        }
    }

    private sealed class RecordingDriver : IBotScenarioDriver
    {
        private readonly List<string> _order;

        public RecordingDriver(List<string> order)
        {
            _order = order;
        }

        public int FillSamples(in BotDriverContext context, Span<RawInputSample> destination)
        {
            _ = context;
            _order.Add("fill");
            if (destination.Length == 0)
            {
                return 0;
            }

            destination[0] = new RawInputSample(1, 0, 0);
            return 1;
        }
    }

    private sealed class RecordingIngress : IInputSampleIngress
    {
        private readonly List<string> _order;

        public RecordingIngress(List<string> order)
        {
            _order = order;
        }

        public InputEnqueueReceipt TryEnqueue(in RawInputSample sample)
        {
            _ = sample;
            _order.Add("enqueue");
            return new InputEnqueueReceipt(true, default, default);
        }

        public SequencedInputSample[] DrainAccepted()
        {
            return Array.Empty<SequencedInputSample>();
        }
    }

    private sealed class RecordingSession : IClientSession
    {
        private readonly List<string> _order;
        private ClientSessionState _state = ClientSessionState.Disconnected;

        public RecordingSession(List<string> order)
        {
            _order = order;
        }

        public SessionCommandResult RequestConnect(in SessionConnectRequest request, CancellationToken cancellationToken)
        {
            _ = request;
            cancellationToken.ThrowIfCancellationRequested();
            _state = ClientSessionState.Negotiating;
            return new SessionCommandResult(true);
        }

        public SessionTickResult Tick(in ClientOwnerTick tick)
        {
            _ = tick;
            _order.Add("tick");
            return new SessionTickResult(_state);
        }

        public SessionCommandResult RequestClose(in SessionCloseRequest request)
        {
            _ = request;
            _state = ClientSessionState.Closed;
            return new SessionCommandResult(true);
        }

        public SessionCommandResult Login(in SessionConnectRequest request, CancellationToken cancellationToken)
        {
            return RequestConnect(in request, cancellationToken);
        }

        public bool TryDequeueSuperseded(out SessionSupersededNotice notice)
        {
            notice = default;
            return false;
        }

        public bool TryGetReplicaWorld(out IReplicaWorld world)
        {
            world = default!;
            return false;
        }

        public ClientSessionSnapshot GetSnapshot()
        {
            _order.Add("observe");
            return new ClientSessionSnapshot(
                _state,
                1,
                false,
                0,
                false,
                false,
                false,
                0,
                0,
                0,
                0,
                0,
                0,
                Array.Empty<string>());
        }
    }
}

internal static class BotSessionFactory
{
    public static IClientSession Create(IInputSampleIngress ingress)
    {
        var options = new ClientEventPipelineOptions(8, 4, TimeSpan.FromSeconds(1));
        new ClientEventPipelineFactory().Create(in options, new InMemoryClientEventSink(8), out var writer);
        var deps = new ClientSessionDependencies(
            new ClientConnectionFactory(),
            new ClientHandshakeFactory(),
            new AlwaysCompatibleCapability(),
            new UnpublishedHandshakeFrameClassifier(),
            ingress,
            new InputCommandSource(ingress, new OpaqueInputMapper()),
            IClientPersistenceFactory.CreateMemory().CreateVerifiedSessionArtifactSource(),
            writer,
            new CommitRuntime(),
            new ClientReplicaFactory(),
            new ClientPredictionFactory(),
            new ImmediateGameplayScopeActivator(),
            new NullPresentationSink(),
            new UnpublishedSessionMessageKindMap());
        new ClientSessionFactory().Create(in deps, out var session);
        return session;
    }

    private sealed class AlwaysCompatibleCapability : IPlatformCapabilityProvider
    {
        public ValueTask<PlatformCapabilityResult> QueryAsync(in PlatformCapabilityQuery query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<PlatformCapabilityResult>(new PlatformCapabilityResult(query.Attempt, query.Generation, true));
        }
    }

    private sealed class OpaqueInputMapper : IGameInputMapper
    {
        public bool TryMap(in SequencedInputSample sample, in InputDrainContext context, out GameplayCommandCandidate candidate)
        {
            _ = context;
            candidate = new GameplayCommandCandidate(sample.Sequence, new byte[] { 0x42 });
            return true;
        }
    }

    private sealed class CommitRuntime : IClientRuntimePort
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

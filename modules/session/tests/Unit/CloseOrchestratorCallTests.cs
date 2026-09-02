using Lumio.Client.Connection;
using Lumio.Client.Handshake;
using Lumio.Client.Input;
using Lumio.Client.Prediction;
using Lumio.Client.Replica;
using Lumio.Client.Session;

namespace Lumio.Client.Session.Tests.Unit;

public sealed class CloseOrchestratorCallTests
{
    [Fact]
    public void Release_InvokesPortsInInputPredictionReplicaVoxelEcsScopeHandshakeConnectionOrder()
    {
        var order = new List<string>();
        var ledger = new SessionResourceLedger();
        ledger.Acquire("input");
        ledger.Acquire("prediction");
        ledger.Acquire("replica");
        ledger.Acquire("voxel");
        ledger.Acquire("ecs");
        ledger.Acquire("scope");
        ledger.Acquire("handshake");
        ledger.Acquire("connection");
        var handles = new RuntimeHandleLedger();
        handles.TryCreateEcs();
        handles.TryCreateVoxel();
        var gate = new GameplayScopeActivationGate();
        gate.TryPrepare();
        gate.TryActivate();
        var close = new CloseOrchestrator();
        close.Release(
            ledger,
            handles,
            gate,
            new SpyScope(order),
            new SpyIngress(order),
            new SpyHandshake(order),
            new SpyConnection(order),
            new SpyReplica(order),
            new SpyPrediction(order),
            1UL);
        Assert.Equal(new[] { "input", "prediction", "replica" }, order.GetRange(0, 3));
        Assert.Equal(new[] { "voxel", "ecs" }, handles.DestroyOrder);
        Assert.Equal("scope", order[3]);
        Assert.Equal("handshake", order[4]);
        Assert.Equal("connection", order[5]);
    }

    private sealed class SpyIngress : IInputSampleIngress
    {
        private readonly List<string> _order;

        public SpyIngress(List<string> order)
        {
            _order = order;
        }

        public InputEnqueueReceipt TryEnqueue(in RawInputSample sample)
        {
            _ = sample;
            return new InputEnqueueReceipt(false, default, default);
        }

        public SequencedInputSample[] DrainAccepted()
        {
            _order.Add("input");
            return Array.Empty<SequencedInputSample>();
        }
    }

    private sealed class SpyReplica : IClientReplica
    {
        private readonly List<string> _order;

        public SpyReplica(List<string> order)
        {
            _order = order;
            World = new ReplicaWorld();
        }

        public IReplicaWorld World { get; }

        public ReplicaStageResult StageAuthority(in ReplicaStageRequest request, out ReplicaStageHandle stageHandle, out ReadOnlyMemory<byte> applyPlan)
        {
            _ = request;
            stageHandle = default;
            applyPlan = ReadOnlyMemory<byte>.Empty;
            return new ReplicaStageResult(ReplicaStageStatus.Rejected);
        }

        public ReplicaOutcomeStatus DiscardStage(ReplicaStageHandle stageHandle, ReplicaStageDiscardReason reason)
        {
            _ = stageHandle;
            _ = reason;
            return ReplicaOutcomeStatus.Stale;
        }

        public ReplicaOutcomeStatus ObserveRuntimeOutcome(ReplicaStageHandle stageHandle, in ReplicaRuntimeOutcome outcome, out ReplicaCommittedMetadata committedMetadata)
        {
            _ = stageHandle;
            _ = outcome;
            committedMetadata = default;
            return ReplicaOutcomeStatus.Stale;
        }

        public ReplicaResetResult ResetForNewSession(in ReplicaResetRequest request)
        {
            _ = request;
            _order.Add("replica");
            return new ReplicaResetResult(true);
        }

        public bool TryObserveConnectionSuperseded(ReadOnlyMemory<byte> utf8, out ReplicaConnectionSuperseded notice)
        {
            _ = utf8;
            notice = default;
            return false;
        }

        public ReplicaSnapshot GetSnapshot()
        {
            return default;
        }
    }

    private sealed class SpyPrediction : IClientPrediction
    {
        private readonly List<string> _order;

        public SpyPrediction(List<string> order)
        {
            _order = order;
        }

        public PredictionCandidateResult AcceptCandidate(in PredictionCandidate candidate, in PredictionCandidateContext context, out PredictionCandidateStage stage, out LocalPredictionPlan localPlan)
        {
            _ = candidate;
            _ = context;
            stage = default;
            localPlan = default;
            return new PredictionCandidateResult(PredictionCandidateStatus.Rejected);
        }

        public PredictionLocalOutcomeResult DiscardCandidateStage(PredictionCandidateStage stage, PredictionStageDiscardReason reason)
        {
            _ = stage;
            _ = reason;
            return new PredictionLocalOutcomeResult(PredictionLocalOutcomeStatus.StaleStage);
        }

        public PredictionLocalOutcomeResult ObserveLocalPredictionOutcome(PredictionCandidateStage stage, in LocalPredictionOutcome outcome, out AcceptedPredictionCommand acceptedCommand)
        {
            _ = stage;
            _ = outcome;
            acceptedCommand = default;
            return new PredictionLocalOutcomeResult(PredictionLocalOutcomeStatus.StaleStage);
        }

        public PredictionAuthorityResult StageAuthority(in AuthorityPredictionUpdate update, in PredictionAuthorityContext context, out PredictionAuthorityStage stage, out PredictionReconcilePlan reconcilePlan)
        {
            _ = update;
            _ = context;
            stage = default;
            reconcilePlan = default;
            return new PredictionAuthorityResult(PredictionAuthorityStatus.Rejected);
        }

        public PredictionAuthorityOutcomeResult DiscardAuthorityStage(PredictionAuthorityStage stage, PredictionStageDiscardReason reason)
        {
            _ = stage;
            _ = reason;
            return new PredictionAuthorityOutcomeResult(PredictionAuthorityOutcomeStatus.StaleStage);
        }

        public PredictionAuthorityOutcomeResult ObserveRuntimeOutcome(PredictionAuthorityStage stage, in AuthorityRuntimeOutcome outcome)
        {
            _ = stage;
            _ = outcome;
            return new PredictionAuthorityOutcomeResult(PredictionAuthorityOutcomeStatus.StaleStage);
        }

        public PredictionResetResult ResetForNewSession(in PredictionResetRequest request)
        {
            _ = request;
            _order.Add("prediction");
            return new PredictionResetResult(true);
        }

        public PredictionSnapshot GetSnapshot()
        {
            return default;
        }
    }

    private sealed class SpyHandshake : IClientHandshake
    {
        private readonly List<string> _order;

        public SpyHandshake(List<string> order)
        {
            _order = order;
        }

        public HandshakeCommandResult Begin(in HandshakeBeginRequest request)
        {
            _ = request;
            return new HandshakeCommandResult(true);
        }

        public HandshakeCommandResult HandleFrame(ReadOnlyMemory<byte> frame)
        {
            _ = frame;
            return new HandshakeCommandResult(true);
        }

        public HandshakeOutcome Poll()
        {
            return default;
        }

        public HandshakeCommandResult Cancel()
        {
            _order.Add("handshake");
            return new HandshakeCommandResult(true);
        }

        public HandshakeOutcome GetSnapshot()
        {
            return default;
        }
    }

    private sealed class SpyConnection : IClientConnection
    {
        private readonly List<string> _order;

        public SpyConnection(List<string> order)
        {
            _order = order;
        }

        public ConnectionGeneration Generation
        {
            get { return new ConnectionGeneration(1); }
        }

        public ConnectionCommandResult Start()
        {
            return new ConnectionCommandResult(true);
        }

        public ConnectionSendResult TrySend(in EncodedFrame frame)
        {
            _ = frame;
            return new ConnectionSendResult(true);
        }

        public int DrainEvents(Span<ConnectionEvent> destination)
        {
            _ = destination;
            return 0;
        }

        public ConnectionCommandResult RequestClose(ConnectionCloseReason reason)
        {
            _ = reason;
            _order.Add("connection");
            return new ConnectionCommandResult(true);
        }

        public ClientConnectionSnapshot GetSnapshot()
        {
            return new ClientConnectionSnapshot(new ConnectionGeneration(1), true, 0);
        }
    }

    private sealed class SpyScope : IClientGameplayScopeActivator
    {
        private readonly List<string> _order;

        public SpyScope(List<string> order)
        {
            _order = order;
        }

        public ValueTask<GameplayScopePrepareResult> PrepareAsync(in GameplayScopePrepareRequest request, CancellationToken cancellationToken)
        {
            _ = request;
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<GameplayScopePrepareResult>(new GameplayScopePrepareResult(true));
        }

        public GameplayScopeActivationResult ActivateAtTickBarrier(in GameplayScopeActivationRequest request)
        {
            _ = request;
            return new GameplayScopeActivationResult(true);
        }

        public ValueTask<GameplayScopeReleaseResult> ReleaseAsync(GameplayScopeLease lease, CancellationToken cancellationToken)
        {
            _ = lease;
            cancellationToken.ThrowIfCancellationRequested();
            _order.Add("scope");
            return new ValueTask<GameplayScopeReleaseResult>(new GameplayScopeReleaseResult(true));
        }
    }
}

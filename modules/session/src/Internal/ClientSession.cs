using System;
using System.Threading;
using Lumio.Client.Connection;
using Lumio.Client.Handshake;
using Lumio.Client.Prediction;
using Lumio.Client.Replica;

namespace Lumio.Client.Session
{
    internal sealed class ClientSession : IClientSession
    {
        private readonly ClientSessionDependencies _dependencies;
        private readonly SessionStateMachine _machine = new SessionStateMachine();
        private readonly SessionGenerationAllocator _generations = new SessionGenerationAllocator();
        private readonly SessionEventInbox _inbox = new SessionEventInbox();
        private readonly SessionEventArbiter _arbiter = new SessionEventArbiter();
        private readonly SessionResourceLedger _ledger = new SessionResourceLedger();
        private readonly RuntimeHandleLedger _handles = new RuntimeHandleLedger();
        private readonly GameplayScopeActivationGate _scopeGate = new GameplayScopeActivationGate();
        private readonly ClientConfigStagingArea _config = new ClientConfigStagingArea();
        private readonly ActiveMessageGate _messageGate = new ActiveMessageGate();
        private readonly HandshakeOrchestrator _handshakeOrch = new HandshakeOrchestrator();
        private readonly FirstConnectOrchestrator _firstConnect = new FirstConnectOrchestrator();
        private readonly ScopeAndRuntimeActivationOrchestrator _activation = new ScopeAndRuntimeActivationOrchestrator();
        private readonly AuthorityUpdateOrchestrator _authority = new AuthorityUpdateOrchestrator();
        private readonly LocalPredictionOrchestrator _localPrediction = new LocalPredictionOrchestrator();
        private readonly ResyncOrchestrator _resync = new ResyncOrchestrator();
        private readonly ReconnectOrchestrator _reconnect = new ReconnectOrchestrator();
        private readonly CloseOrchestrator _close = new CloseOrchestrator();
        private readonly AuthorityStageBundle _bundle = new AuthorityStageBundle();
        private readonly TerminalSessionState _terminal = new TerminalSessionState();
        private readonly object _gate = new object();
        private IClientConnection _connection = default!;
        private IClientReplica _replica = default!;
        private IClientPrediction _prediction = default!;
        private bool _runtimeCommitted;
        private bool _baselineAck;
        private bool _presented;
        private int _replicaStages;
        private int _predictionStages;
        private int _runtimeCalls;
        private ulong _snapshotSequence;

        public ClientSession(ClientSessionDependencies dependencies)
        {
            _dependencies = dependencies;
        }

        public SessionCommandResult RequestConnect(in SessionConnectRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_machine.IsTerminal && _machine.State != ClientSessionState.Closed)
                {
                    return new SessionCommandResult(false);
                }

                if (_machine.State != ClientSessionState.Disconnected
                    && _machine.State != ClientSessionState.Closed
                    && _machine.State != ClientSessionState.Reconnecting)
                {
                    return new SessionCommandResult(false);
                }

                return StartGeneration(request.Generation == 0 ? 1UL : request.Generation);
            }
        }

        public SessionTickResult Tick(in ClientOwnerTick tick)
        {
            _ = tick;
            lock (_gate)
            {
                if (_connection != null && !_machine.IsTerminal)
                {
                    var buffer = new ConnectionEvent[16];
                    int n = _connection.DrainEvents(buffer);
                    for (int i = 0; i < n; i++)
                    {
                        ConnectionEvent evt = buffer[i];
                        if (evt.Generation.Value != _machine.Generation)
                        {
                            continue;
                        }

                        SessionEventPriority priority = _arbiter.MapConnection(evt.Kind);
                        if (evt.Kind == ConnectionEventKind.FrameReceived)
                        {
                            SessionMessageKind kind = _dependencies.Messages.Map(evt.Frame.Bytes);
                            priority = _arbiter.MapMessage(kind);
                        }

                        _inbox.Enqueue(priority, evt.Generation.Value, evt);
                    }

                    while (_inbox.TryDequeue(out SessionEvent next))
                    {
                        Dispatch(in next);
                    }

                    if (_machine.State == ClientSessionState.Active)
                    {
                        _localPrediction.Tick(_dependencies.Commands, _prediction, _dependencies.Runtime, _machine.Generation);
                    }
                }

                return new SessionTickResult(_machine.State);
            }
        }

        public SessionCommandResult RequestClose(in SessionCloseRequest request)
        {
            lock (_gate)
            {
                if (_terminal.Frozen && _machine.IsTerminal)
                {
                    return new SessionCommandResult(true);
                }

                if (request.Fault)
                {
                    _terminal.Freeze();
                    _machine.TryEnter(ClientSessionState.Faulted);
                }

                ReleaseAll();
                if (!request.Fault)
                {
                    _machine.TryEnter(ClientSessionState.Closed);
                }

                _terminal.Freeze();
                return new SessionCommandResult(true);
            }
        }

        public ClientSessionSnapshot GetSnapshot()
        {
            lock (_gate)
            {
                return new ClientSessionSnapshot(
                    _machine.State,
                    _machine.Generation,
                    _runtimeCommitted,
                    _ledger.Count,
                    _scopeGate.Activated,
                    _baselineAck,
                    _presented,
                    _replicaStages,
                    _predictionStages,
                    _runtimeCalls,
                    _handshakeOrch.BeginCount,
                    _handles.EcsCount,
                    _handles.VoxelCount,
                    _ledger.ReleaseOrder);
            }
        }

        private SessionCommandResult StartGeneration(ulong generation)
        {
            _generations.Seed(generation);
            _machine.SetGeneration(generation);
            _runtimeCommitted = false;
            _baselineAck = false;
            _presented = false;
            _snapshotSequence = 0;
            _machine.TryEnter(ClientSessionState.Connecting);
            ClientConnectionCreateResult created = _dependencies.Connections.Create(
                new ClientConnectionCreateRequest(generation, 32),
                out _connection);
            if (!created.Succeeded)
            {
                _machine.TryEnter(ClientSessionState.Faulted);
                return new SessionCommandResult(false);
            }

            _connection.Start();
            _ledger.Acquire("connection");
            IClientHandshake handshake = _dependencies.Handshakes.Create(_dependencies.Capabilities, _dependencies.HandshakeFrames);
            _handshakeOrch.Begin(handshake, new HandshakeAttemptId(generation), generation);
            _ledger.Acquire("handshake");
            _replica = _dependencies.Replicas.Create();
            _replica.ResetForNewSession(new ReplicaResetRequest(generation));
            _prediction = _dependencies.Predictions.Create(new PredictionCreateRequest(generation, 8));
            _ledger.Acquire("replica");
            _ledger.Acquire("prediction");
            _ledger.Acquire("input");
            _machine.TryEnter(ClientSessionState.Negotiating);
            return new SessionCommandResult(true);
        }

        private void Dispatch(in SessionEvent evt)
        {
            if (evt.Priority == SessionEventPriority.Fault)
            {
                _terminal.Freeze();
                _machine.TryEnter(ClientSessionState.Faulted);
                ReleaseAll();
                return;
            }

            if (evt.Priority == SessionEventPriority.ForcedClose || evt.Priority == SessionEventPriority.Cancel)
            {
                ReleaseAll();
                _machine.TryEnter(ClientSessionState.Closed);
                _terminal.Freeze();
                return;
            }

            if (evt.Priority == SessionEventPriority.Disconnect)
            {
                HandleDisconnect();
                return;
            }

            if (evt.Connection.Kind != ConnectionEventKind.FrameReceived)
            {
                return;
            }

            if (_machine.State == ClientSessionState.Negotiating)
            {
                HandshakeOutcome outcome = _handshakeOrch.HandleOpaqueFrame(evt.Connection.Frame.Bytes);
                if (outcome.Accepted)
                {
                    if (_firstConnect.TryEnterSynchronizing(
                        outcome,
                        _config,
                        _activation,
                        _dependencies.Scope,
                        _scopeGate,
                        _handles,
                        _machine.Generation))
                    {
                        _ledger.Acquire("scope");
                        _ledger.Acquire("ecs");
                        _ledger.Acquire("voxel");
                        _machine.TryEnter(ClientSessionState.Synchronizing);
                    }
                    else
                    {
                        _machine.TryEnter(ClientSessionState.Faulted);
                    }
                }
                else if (outcome.Phase == HandshakePhase.Rejected)
                {
                    _machine.TryEnter(ClientSessionState.Closed);
                    ReleaseAll();
                }

                return;
            }

            SessionMessageKind kind = _dependencies.Messages.Map(evt.Connection.Frame.Bytes);
            if (kind == SessionMessageKind.Gap && _machine.State == ClientSessionState.Active)
            {
                _resync.Enter(_dependencies.Commands, _machine.Generation);
                _machine.TryEnter(ClientSessionState.Resyncing);
                return;
            }

            if (!_messageGate.Allow(_machine.State, evt.Generation, _machine.Generation, kind))
            {
                return;
            }

            if (kind == SessionMessageKind.FullSnapshot || kind == SessionMessageKind.Delta || kind == SessionMessageKind.AuthorityUpdate)
            {
                ApplyAuthority(evt.Connection.Frame.Bytes, kind == SessionMessageKind.FullSnapshot ? ReplicaUpdateKind.FullSnapshot : ReplicaUpdateKind.Delta);
            }
        }

        private void ApplyAuthority(ReadOnlyMemory<byte> update, ReplicaUpdateKind kind)
        {
            _replicaStages++;
            _predictionStages++;
            _runtimeCalls++;
            ulong sequence = ++_snapshotSequence;
            bool resyncHint;
            bool committed;
            bool ack;
            bool presented;
            resyncHint = _authority.TryCommit(
                _replica,
                _prediction,
                _dependencies.Runtime,
                _dependencies.Presentation,
                _bundle,
                _machine.Generation,
                update,
                kind,
                sequence,
                out ack,
                out presented,
                out committed);
            if (committed)
            {
                _runtimeCommitted = true;
                _baselineAck = ack;
                _presented = presented;
                _machine.TryEnter(ClientSessionState.Active);
                return;
            }

            if (resyncHint && _machine.State != ClientSessionState.Faulted)
            {
                _resync.Enter(_dependencies.Commands, _machine.Generation);
                _machine.TryEnter(ClientSessionState.Resyncing);
                return;
            }

            if (_machine.State == ClientSessionState.Synchronizing)
            {
                _machine.TryEnter(ClientSessionState.Faulted);
            }
        }

        private void HandleDisconnect()
        {
            ulong next = _reconnect.NextGeneration(_generations);
            ReleaseAll();
            _scopeGate.Reset();
            _config.Clear();
            _machine.TryEnter(ClientSessionState.Reconnecting);
            StartGeneration(next);
        }

        private void ReleaseAll()
        {
            _close.Release(
                _ledger,
                _handles,
                _scopeGate,
                _dependencies.Scope,
                _handshakeOrch.Handshake,
                _connection,
                _replica,
                _prediction,
                _machine.Generation);
        }
    }
}

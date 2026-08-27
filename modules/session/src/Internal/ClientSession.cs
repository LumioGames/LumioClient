using System;
using System.Threading;
using System.Threading.Tasks;
using Lumio.Client.Connection;
using Lumio.Client.Handshake;

namespace Lumio.Client.Session
{
    internal sealed class ClientSession : IClientSession
    {
        private readonly ClientSessionDependencies _dependencies;
        private readonly ResourceLedger _ledger = new ResourceLedger();
        private readonly object _gate = new object();
        private ClientSessionState _state = ClientSessionState.Disconnected;
        private ulong _generation;
        private IClientConnection _connection = default!;
        private IClientHandshake _handshake = default!;
        private bool _runtimeCommitted;

        public ClientSession(ClientSessionDependencies dependencies)
        {
            _dependencies = dependencies;
        }

        public SessionCommandResult RequestConnect(in SessionConnectRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_state != ClientSessionState.Disconnected && _state != ClientSessionState.Closed)
                {
                    return new SessionCommandResult(false);
                }

                _generation = request.Generation;
                _runtimeCommitted = false;
                _state = ClientSessionState.Connecting;
                ClientConnectionCreateResult created = _dependencies.Connections.Create(
                    new ClientConnectionCreateRequest(_generation, 16),
                    out _connection);
                if (!created.Succeeded)
                {
                    _state = ClientSessionState.Faulted;
                    return new SessionCommandResult(false);
                }

                _connection.Start();
                _ledger.Acquire("connection");
                _handshake = _dependencies.Handshakes.Create(_dependencies.Capabilities);
                _handshake.Begin(new HandshakeBeginRequest(new HandshakeAttemptId(_generation), _generation));
                _ledger.Acquire("handshake");
                _state = ClientSessionState.Negotiating;
                return new SessionCommandResult(true);
            }
        }

        public SessionTickResult Tick(in ClientOwnerTick tick)
        {
            _ = tick;
            lock (_gate)
            {
                if (_state == ClientSessionState.Negotiating && _handshake != null)
                {
                    _handshake.HandleFrame(new byte[] { 1 });
                    HandshakeOutcome outcome = _handshake.Poll();
                    if (outcome.Accepted)
                    {
                        _state = ClientSessionState.Synchronizing;
                        ValueTask<RuntimeTransactionOutcome> pending = _dependencies.Runtime.ApplyAuthoritativeTransaction(
                            new RuntimeTransactionRequest(_generation, ReadOnlyMemory<byte>.Empty),
                            CancellationToken.None);
                        RuntimeTransactionOutcome runtime = pending.IsCompleted
                            ? pending.Result
                            : new RuntimeTransactionOutcome(false);
                        if (runtime.Committed)
                        {
                            _runtimeCommitted = true;
                            _state = ClientSessionState.Active;
                        }
                        else
                        {
                            _state = ClientSessionState.Faulted;
                        }
                    }
                }

                return new SessionTickResult(_state);
            }
        }

        public SessionCommandResult RequestClose(in SessionCloseRequest request)
        {
            lock (_gate)
            {
                if (_connection != null)
                {
                    _connection.RequestClose(request.Fault ? ConnectionCloseReason.Fault : ConnectionCloseReason.OwnerRequest);
                }

                if (_handshake != null)
                {
                    _handshake.Cancel();
                }

                _ledger.ReleaseAll();
                _state = request.Fault ? ClientSessionState.Faulted : ClientSessionState.Closed;
                return new SessionCommandResult(true);
            }
        }

        public ClientSessionSnapshot GetSnapshot()
        {
            lock (_gate)
            {
                return new ClientSessionSnapshot(_state, _generation, _runtimeCommitted, _ledger.Count);
            }
        }
    }
}

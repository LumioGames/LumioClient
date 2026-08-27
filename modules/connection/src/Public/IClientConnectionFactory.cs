using System;

namespace Lumio.Client.Connection
{
    public interface IClientConnectionFactory
    {
        ClientConnectionCreateResult Create(in ClientConnectionCreateRequest request, out IClientConnection connection);
    }

    public sealed class ClientConnectionFactory : IClientConnectionFactory
    {
        public ClientConnectionCreateResult Create(in ClientConnectionCreateRequest request, out IClientConnection connection)
        {
            var owner = new OwnerConnection(request.Generation, request.EventCapacity);
            connection = owner;
            return new ClientConnectionCreateResult(true, new LocalEmbeddedLoopback(owner));
        }
    }

    internal sealed class OwnerConnection : IClientConnection
    {
        private readonly object _gate = new object();
        private readonly ConnectionStateMachine _machine;
        private readonly LocalEmbeddedTransport _transport;
        private readonly ConnectionSendQueue _sendQueue;
        private readonly ReplayWindow _inboundReplay = new ReplayWindow();
        private readonly FaultDecoratingTransport _faults = new FaultDecoratingTransport(new PassThroughFaultPolicy());
        private ulong _inboundSequence;

        public OwnerConnection(ConnectionGeneration generation, int eventCapacity)
        {
            int capacity = Math.Max(eventCapacity, 1);
            _machine = new ConnectionStateMachine(generation, Math.Max(capacity, 8));
            _transport = new LocalEmbeddedTransport(capacity);
            _sendQueue = new ConnectionSendQueue(capacity);
        }

        internal LocalEmbeddedTransport Transport
        {
            get { return _transport; }
        }

        public ConnectionGeneration Generation
        {
            get { return _machine.Generation; }
        }

        public ConnectionCommandResult Start()
        {
            lock (_gate)
            {
                return _machine.Start();
            }
        }

        public ConnectionSendResult TrySend(in EncodedFrame frame)
        {
            lock (_gate)
            {
                ConnectionSendResult allowed = _machine.TrySend(in frame);
                if (!allowed.Accepted)
                {
                    return allowed;
                }

                if (!_sendQueue.TryEnqueue(in frame))
                {
                    return new ConnectionSendResult(false);
                }

                FlushSendQueue();
                return new ConnectionSendResult(true);
            }
        }

        public int DrainEvents(Span<ConnectionEvent> destination)
        {
            lock (_gate)
            {
                PumpInbound();
                return _machine.Drain(destination);
            }
        }

        public ConnectionCommandResult RequestClose(ConnectionCloseReason reason)
        {
            lock (_gate)
            {
                return new ConnectionCommandResult(_machine.TryClose(reason));
            }
        }

        public ClientConnectionSnapshot GetSnapshot()
        {
            lock (_gate)
            {
                return new ClientConnectionSnapshot(_machine.Generation, _machine.Terminal, _machine.EventCount);
            }
        }

        public bool DeliverCallback(ConnectionGeneration generation)
        {
            lock (_gate)
            {
                return _machine.TryDeliverLate(generation);
            }
        }

        internal bool DeliverDisconnect()
        {
            lock (_gate)
            {
                return _machine.TryDeliverDisconnect();
            }
        }

        private void FlushSendQueue()
        {
            while (_sendQueue.TryPeek(out EncodedFrame next))
            {
                TransportFaultAction action = _faults.Next(0);
                if (action == TransportFaultAction.Drop)
                {
                    _sendQueue.TryDequeue(out _);
                    continue;
                }

                if (!_transport.TrySendClient(in next))
                {
                    return;
                }

                _sendQueue.TryDequeue(out _);
                if (action == TransportFaultAction.Duplicate)
                {
                    _transport.TrySendClient(in next);
                }
            }
        }

        private void PumpInbound()
        {
            EncodedFrame frame;
            while (_transport.TryReceiveClient(out frame))
            {
                _inboundSequence++;
                if (!_inboundReplay.Accept(_inboundSequence))
                {
                    continue;
                }

                _machine.TryDeliverInbound(in frame);
            }
        }
    }

    internal sealed class PassThroughFaultPolicy : ITransportFaultPolicy
    {
        public TransportFaultAction Decide(in TransportFaultContext context)
        {
            _ = context;
            return TransportFaultAction.Pass;
        }
    }
}

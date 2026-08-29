using System;

namespace Lumio.Client.Connection
{
    public interface IClientConnectionFactory
    {
        ClientConnectionCreateResult Create(in ClientConnectionCreateRequest request, out IClientConnection connection);
    }

    public sealed class ClientConnectionFactory : IClientConnectionFactory
    {
        private readonly ITransportFaultPolicy _faultPolicy;

        public ClientConnectionFactory()
            : this(new PassThroughFaultPolicy())
        {
        }

        public ClientConnectionFactory(ITransportFaultPolicy faultPolicy)
        {
            _faultPolicy = faultPolicy ?? new PassThroughFaultPolicy();
        }

        public ClientConnectionCreateResult Create(in ClientConnectionCreateRequest request, out IClientConnection connection)
        {
            var owner = new OwnerConnection(request.Generation, request.EventCapacity, _faultPolicy);
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
        private readonly FaultDecoratingTransport _faults;
        private ulong _inboundSequence;

        public OwnerConnection(ConnectionGeneration generation, int eventCapacity)
            : this(generation, eventCapacity, new PassThroughFaultPolicy())
        {
        }

        public OwnerConnection(ConnectionGeneration generation, int eventCapacity, ITransportFaultPolicy faultPolicy)
        {
            _faults = new FaultDecoratingTransport(faultPolicy ?? new PassThroughFaultPolicy());
            int capacity = Math.Max(eventCapacity, 1);
            _machine = new ConnectionStateMachine(generation, capacity);
            _transport = new LocalEmbeddedTransport(Math.Max(capacity, 4));
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
                if (!_machine.CanSend(in frame))
                {
                    return new ConnectionSendResult(false);
                }

                FlushSendQueue();
                if (_transport.TrySendClient(in frame))
                {
                    return new ConnectionSendResult(true);
                }

                _sendQueue.TryEnqueue(in frame);
                return new ConnectionSendResult(false);
            }
        }

        public int DrainEvents(Span<ConnectionEvent> destination)
        {
            lock (_gate)
            {
                FlushSendQueue();
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

                if (!_machine.TryDeliverInbound(in frame))
                {
                    return;
                }
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

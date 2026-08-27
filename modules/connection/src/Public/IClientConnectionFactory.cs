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
            connection = new OwnerConnection(request.Generation, request.EventCapacity);
            return new ClientConnectionCreateResult(true);
        }
    }

    internal sealed class OwnerConnection : IClientConnection
    {
        private readonly object _gate = new object();
        private readonly ConnectionStateMachine _machine;

        public OwnerConnection(ConnectionGeneration generation, int eventCapacity)
        {
            _machine = new ConnectionStateMachine(generation, eventCapacity);
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
                return _machine.TrySend(in frame);
            }
        }

        public int DrainEvents(Span<ConnectionEvent> destination)
        {
            lock (_gate)
            {
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
    }
}

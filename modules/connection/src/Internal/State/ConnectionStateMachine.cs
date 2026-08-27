using System;

namespace Lumio.Client.Connection
{
    internal sealed class ConnectionStateMachine
    {
        private readonly ConnectionGeneration _generation;
        private readonly ConnectionEvent[] _events;
        private int _count;
        private bool _started;
        private bool _terminal;

        public ConnectionStateMachine(ConnectionGeneration generation, int eventCapacity)
        {
            _generation = generation;
            _events = new ConnectionEvent[Math.Max(eventCapacity, 4)];
        }

        public ConnectionGeneration Generation
        {
            get { return _generation; }
        }

        public bool Terminal
        {
            get { return _terminal; }
        }

        public int EventCount
        {
            get { return _count; }
        }

        public ConnectionCommandResult Start()
        {
            if (_terminal || _started)
            {
                return new ConnectionCommandResult(false);
            }

            _started = true;
            Enqueue(new ConnectionEvent(ConnectionEventKind.Started, _generation, false));
            return new ConnectionCommandResult(true);
        }

        public ConnectionSendResult TrySend(in EncodedFrame frame)
        {
            if (_terminal || !_started || frame.Bytes.IsEmpty)
            {
                return new ConnectionSendResult(false);
            }

            return new ConnectionSendResult(true);
        }

        public bool TryClose(ConnectionCloseReason reason)
        {
            if (_terminal)
            {
                return false;
            }

            ConnectionEventKind kind = reason == ConnectionCloseReason.Disconnect
                ? ConnectionEventKind.Disconnected
                : reason == ConnectionCloseReason.Fault
                    ? ConnectionEventKind.Faulted
                    : ConnectionEventKind.Closed;
            _terminal = true;
            Enqueue(new ConnectionEvent(kind, _generation, true));
            return true;
        }

        public bool TryDeliverLate(ConnectionGeneration generation)
        {
            if (generation.Value != _generation.Value || _terminal)
            {
                return false;
            }

            return true;
        }

        public int Drain(Span<ConnectionEvent> destination)
        {
            int n = Math.Min(destination.Length, _count);
            for (int i = 0; i < n; i++)
            {
                destination[i] = _events[i];
            }

            if (n < _count)
            {
                Array.Copy(_events, n, _events, 0, _count - n);
            }

            _count -= n;
            return n;
        }

        private void Enqueue(ConnectionEvent evt)
        {
            if (_count == _events.Length)
            {
                return;
            }

            _events[_count] = evt;
            _count++;
        }
    }
}

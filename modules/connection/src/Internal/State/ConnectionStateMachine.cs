using System;

namespace Lumio.Client.Connection
{
    internal sealed class ConnectionStateMachine
    {
        private readonly ConnectionGeneration _generation;
        private readonly ConnectionEventQueue _events;
        private bool _started;
        private bool _terminal;

        public ConnectionStateMachine(ConnectionGeneration generation, int eventCapacity)
        {
            _generation = generation;
            _events = new ConnectionEventQueue(eventCapacity);
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
            get { return _events.Count; }
        }

        public ConnectionCommandResult Start()
        {
            if (_terminal || _started)
            {
                return new ConnectionCommandResult(false);
            }

            _started = true;
            _events.TryEnqueue(new ConnectionEvent(ConnectionEventKind.Started, _generation, false));
            return new ConnectionCommandResult(true);
        }

        public bool CanSend(in EncodedFrame frame)
        {
            return _started && !_terminal && !frame.Bytes.IsEmpty;
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
            _events.TryEnqueue(new ConnectionEvent(kind, _generation, true));
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

        public bool TryDeliverInbound(in EncodedFrame frame)
        {
            if (_terminal || !_started)
            {
                return false;
            }

            return _events.TryEnqueue(new ConnectionEvent(ConnectionEventKind.FrameReceived, _generation, false, frame));
        }

        public bool TryDeliverDisconnect()
        {
            return TryClose(ConnectionCloseReason.Disconnect);
        }

        public int Drain(Span<ConnectionEvent> destination)
        {
            return _events.Drain(destination);
        }
    }
}

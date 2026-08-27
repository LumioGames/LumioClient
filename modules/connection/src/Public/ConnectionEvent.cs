namespace Lumio.Client.Connection
{
    public enum ConnectionEventKind
    {
        Started,
        FrameReceived,
        Closed,
        Disconnected,
        Faulted
    }

    public readonly struct ConnectionEvent
    {
        public ConnectionEvent(ConnectionEventKind kind, ConnectionGeneration generation, bool terminal)
            : this(kind, generation, terminal, default(EncodedFrame))
        {
        }

        public ConnectionEvent(ConnectionEventKind kind, ConnectionGeneration generation, bool terminal, EncodedFrame frame)
        {
            Kind = kind;
            Generation = generation;
            Terminal = terminal;
            Frame = frame;
        }

        public ConnectionEventKind Kind { get; }

        public ConnectionGeneration Generation { get; }

        public bool Terminal { get; }

        public EncodedFrame Frame { get; }
    }
}

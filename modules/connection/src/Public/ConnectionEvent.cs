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
        {
            Kind = kind;
            Generation = generation;
            Terminal = terminal;
        }

        public ConnectionEventKind Kind { get; }

        public ConnectionGeneration Generation { get; }

        public bool Terminal { get; }
    }
}

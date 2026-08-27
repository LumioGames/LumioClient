namespace Lumio.Client.Connection
{
    public readonly struct ConnectionCommandResult
    {
        public ConnectionCommandResult(bool succeeded)
        {
            Succeeded = succeeded;
        }

        public bool Succeeded { get; }
    }

    public readonly struct ConnectionSendResult
    {
        public ConnectionSendResult(bool accepted)
        {
            Accepted = accepted;
        }

        public bool Accepted { get; }
    }

    public readonly struct ClientConnectionSnapshot
    {
        public ClientConnectionSnapshot(ConnectionGeneration generation, bool terminal, int eventCount)
        {
            Generation = generation;
            Terminal = terminal;
            EventCount = eventCount;
        }

        public ConnectionGeneration Generation { get; }

        public bool Terminal { get; }

        public int EventCount { get; }
    }

    public readonly struct ClientConnectionCreateRequest
    {
        public ClientConnectionCreateRequest(ulong generation, int eventCapacity)
        {
            Generation = new ConnectionGeneration(generation);
            EventCapacity = eventCapacity;
        }

        public ConnectionGeneration Generation { get; }

        public int EventCapacity { get; }
    }

    public readonly struct ClientConnectionCreateResult
    {
        public ClientConnectionCreateResult(bool succeeded)
        {
            Succeeded = succeeded;
        }

        public bool Succeeded { get; }
    }
}

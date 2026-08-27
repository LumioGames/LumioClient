namespace Lumio.Client.Session
{
    public readonly struct SessionConnectRequest
    {
        public SessionConnectRequest(ulong generation)
        {
            Generation = generation;
        }

        public ulong Generation { get; }
    }

    public readonly struct SessionCloseRequest
    {
        public SessionCloseRequest(bool fault)
        {
            Fault = fault;
        }

        public bool Fault { get; }
    }

    public readonly struct ClientOwnerTick
    {
        public ClientOwnerTick(ulong tick)
        {
            Tick = tick;
        }

        public ulong Tick { get; }
    }

    public readonly struct SessionCommandResult
    {
        public SessionCommandResult(bool succeeded)
        {
            Succeeded = succeeded;
        }

        public bool Succeeded { get; }
    }

    public readonly struct SessionTickResult
    {
        public SessionTickResult(ClientSessionState state)
        {
            State = state;
        }

        public ClientSessionState State { get; }
    }

    public readonly struct ClientSessionSnapshot
    {
        public ClientSessionSnapshot(ClientSessionState state, ulong generation, bool runtimeCommitted, int ledgerCount)
        {
            State = state;
            Generation = generation;
            RuntimeCommitted = runtimeCommitted;
            LedgerCount = ledgerCount;
        }

        public ClientSessionState State { get; }

        public ulong Generation { get; }

        public bool RuntimeCommitted { get; }

        public int LedgerCount { get; }
    }
}

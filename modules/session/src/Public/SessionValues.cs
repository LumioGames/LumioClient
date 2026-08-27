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
        public ClientSessionSnapshot(
            ClientSessionState state,
            ulong generation,
            bool runtimeCommitted,
            int ledgerCount,
            bool scopeActivated,
            bool baselineAckSent,
            bool presentationWritten,
            int replicaStageCalls,
            int predictionAuthorityStageCalls,
            int runtimeAuthorityCalls,
            int handshakeBeginCount,
            int ecsHandles,
            int voxelHandles,
            string[] releaseOrder)
        {
            State = state;
            Generation = generation;
            RuntimeCommitted = runtimeCommitted;
            LedgerCount = ledgerCount;
            ScopeActivated = scopeActivated;
            BaselineAckSent = baselineAckSent;
            PresentationWritten = presentationWritten;
            ReplicaStageCalls = replicaStageCalls;
            PredictionAuthorityStageCalls = predictionAuthorityStageCalls;
            RuntimeAuthorityCalls = runtimeAuthorityCalls;
            HandshakeBeginCount = handshakeBeginCount;
            EcsHandles = ecsHandles;
            VoxelHandles = voxelHandles;
            ReleaseOrder = releaseOrder;
        }

        public ClientSessionState State { get; }

        public ulong Generation { get; }

        public bool RuntimeCommitted { get; }

        public int LedgerCount { get; }

        public bool ScopeActivated { get; }

        public bool BaselineAckSent { get; }

        public bool PresentationWritten { get; }

        public int ReplicaStageCalls { get; }

        public int PredictionAuthorityStageCalls { get; }

        public int RuntimeAuthorityCalls { get; }

        public int HandshakeBeginCount { get; }

        public int EcsHandles { get; }

        public int VoxelHandles { get; }

        public string[] ReleaseOrder { get; }
    }
}

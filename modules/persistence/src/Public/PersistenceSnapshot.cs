namespace Lumio.Client.Persistence
{
    public readonly struct PersistenceSnapshot
    {
        public PersistenceSnapshot(int inFlightCount, ulong latestCommittedGeneration)
        {
            InFlightCount = inFlightCount;
            LatestCommittedGeneration = latestCommittedGeneration;
        }

        public int InFlightCount { get; }

        public ulong LatestCommittedGeneration { get; }
    }
}

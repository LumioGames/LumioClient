namespace Lumio.Client.Input
{
    public readonly struct InputDrainContext
    {
        public InputDrainContext(ulong generation, int maxCandidates)
        {
            Generation = generation;
            MaxCandidates = maxCandidates;
        }

        public ulong Generation { get; }

        public int MaxCandidates { get; }
    }
}

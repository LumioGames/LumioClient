namespace Lumio.Client.Replica
{
    public readonly struct ReplicaCommittedMetadata
    {
        public ReplicaCommittedMetadata(
            ulong generation,
            ulong baseline,
            ulong revision,
            ulong sequence,
            bool hasBaseline,
            bool frozen)
        {
            Generation = generation;
            Baseline = baseline;
            Revision = revision;
            Sequence = sequence;
            HasBaseline = hasBaseline;
            Frozen = frozen;
        }

        public ulong Generation { get; }

        public ulong Baseline { get; }

        public ulong Revision { get; }

        public ulong Sequence { get; }

        public bool HasBaseline { get; }

        public bool Frozen { get; }
    }
}

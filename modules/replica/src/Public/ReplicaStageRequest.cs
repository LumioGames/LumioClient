using System;

namespace Lumio.Client.Replica
{
    public enum ReplicaUpdateKind
    {
        FullSnapshot = 0,
        Delta = 1
    }

    public readonly struct ReplicaStageRequest
    {
        public ReplicaStageRequest(
            ulong generation,
            ReplicaUpdateKind kind,
            ulong baseline,
            ulong fromRevision,
            ulong toRevision,
            ulong sequence,
            ReadOnlyMemory<byte> update,
            ReadOnlyMemory<ulong> tombstoneEntityIds,
            ReadOnlyMemory<ulong> touchedEntityIds)
        {
            Generation = generation;
            Kind = kind;
            Baseline = baseline;
            FromRevision = fromRevision;
            ToRevision = toRevision;
            Sequence = sequence;
            Update = update;
            TombstoneEntityIds = tombstoneEntityIds;
            TouchedEntityIds = touchedEntityIds;
        }

        public ulong Generation { get; }

        public ReplicaUpdateKind Kind { get; }

        public ulong Baseline { get; }

        public ulong FromRevision { get; }

        public ulong ToRevision { get; }

        public ulong Sequence { get; }

        public ReadOnlyMemory<byte> Update { get; }

        public ReadOnlyMemory<ulong> TombstoneEntityIds { get; }

        public ReadOnlyMemory<ulong> TouchedEntityIds { get; }
    }
}

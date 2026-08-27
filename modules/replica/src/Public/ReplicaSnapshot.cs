using System;

namespace Lumio.Client.Replica
{
    public readonly struct ReplicaSnapshot
    {
        public ReplicaSnapshot(
            in ReplicaCommittedMetadata committed,
            int openStageCount,
            ReplicaStageStatus lastStageStatus,
            ReadOnlyMemory<byte> evidence)
        {
            Committed = committed;
            OpenStageCount = openStageCount;
            LastStageStatus = lastStageStatus;
            Evidence = evidence;
        }

        public ReplicaCommittedMetadata Committed { get; }

        public int OpenStageCount { get; }

        public ReplicaStageStatus LastStageStatus { get; }

        public ReadOnlyMemory<byte> Evidence { get; }
    }
}

using System;

namespace Lumio.Client.Replica
{
    internal sealed class ReplicaMetadataState
    {
        public ulong Generation { get; private set; }

        public ulong Baseline { get; private set; }

        public ulong Revision { get; private set; }

        public ulong Sequence { get; private set; }

        public bool HasBaseline { get; private set; }

        public bool Frozen { get; private set; }

        public ReadOnlyMemory<byte> FreezeEvidence { get; private set; }

        public void Reset(ulong generation)
        {
            Generation = generation;
            Baseline = 0;
            Revision = 0;
            Sequence = 0;
            HasBaseline = false;
            Frozen = false;
            FreezeEvidence = ReadOnlyMemory<byte>.Empty;
        }

        public void Freeze(ReadOnlyMemory<byte> evidence)
        {
            Frozen = true;
            FreezeEvidence = evidence.ToArray();
        }

        public void ApplyCommitted(in ReplicaStageRequest request)
        {
            if (request.Kind == ReplicaUpdateKind.FullSnapshot)
            {
                HasBaseline = true;
                Baseline = request.Baseline;
            }

            Revision = request.ToRevision;
            Sequence = request.Sequence;
        }

        public ReplicaCommittedMetadata ToCommittedMetadata()
        {
            return new ReplicaCommittedMetadata(Generation, Baseline, Revision, Sequence, HasBaseline, Frozen);
        }
    }
}

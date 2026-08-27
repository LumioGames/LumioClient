namespace Lumio.Client.Replica
{
    internal enum ReplicaGapClassification
    {
        Accept = 0,
        Duplicate = 1,
        Gap = 2,
        TombstoneConflict = 3,
        BaselineMismatch = 4,
        StaleRevision = 5
    }

    internal sealed class ReplicaGapDetector
    {
        private readonly bool _enabled = true;

        public ReplicaGapClassification Classify(
            in ReplicaStageRequest request,
            ReplicaMetadataState metadata,
            TombstoneEvidence tombstones)
        {
            if (!_enabled)
            {
                return ReplicaGapClassification.Gap;
            }

            if (request.Kind == ReplicaUpdateKind.FullSnapshot)
            {
                if (metadata.HasBaseline
                    && request.Baseline == metadata.Baseline
                    && request.ToRevision == metadata.Revision
                    && request.Sequence == metadata.Sequence)
                {
                    return ReplicaGapClassification.Duplicate;
                }

                return ReplicaGapClassification.Accept;
            }

            if (!metadata.HasBaseline)
            {
                return ReplicaGapClassification.Gap;
            }

            if (request.Baseline != metadata.Baseline)
            {
                return ReplicaGapClassification.BaselineMismatch;
            }

            if (request.Sequence == metadata.Sequence)
            {
                return ReplicaGapClassification.Duplicate;
            }

            if (request.Sequence < metadata.Sequence)
            {
                return ReplicaGapClassification.StaleRevision;
            }

            if (request.Sequence != metadata.Sequence + 1 || request.FromRevision != metadata.Revision)
            {
                return ReplicaGapClassification.Gap;
            }

            if (tombstones.Conflicts(request.TouchedEntityIds))
            {
                return ReplicaGapClassification.TombstoneConflict;
            }

            return ReplicaGapClassification.Accept;
        }
    }
}

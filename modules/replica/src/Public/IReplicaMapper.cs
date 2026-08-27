using System;

namespace Lumio.Client.Replica
{
    public interface IReplicaMapper
    {
        ReplicaMappingResult Map(
            in ReplicaStageRequest request,
            in ReplicaMappingContext context,
            out ReadOnlyMemory<byte> applyPlan);
    }

    public readonly struct ReplicaMappingResult
    {
        public ReplicaMappingResult(bool succeeded)
        {
            Succeeded = succeeded;
        }

        public bool Succeeded { get; }
    }

    public readonly struct ReplicaMappingContext
    {
        public ReplicaMappingContext(ulong generation, ulong committedBaseline, ulong committedRevision)
        {
            Generation = generation;
            CommittedBaseline = committedBaseline;
            CommittedRevision = committedRevision;
        }

        public ulong Generation { get; }

        public ulong CommittedBaseline { get; }

        public ulong CommittedRevision { get; }
    }
}

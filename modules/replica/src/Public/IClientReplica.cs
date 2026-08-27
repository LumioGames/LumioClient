using System;

namespace Lumio.Client.Replica
{
    public interface IClientReplica
    {
        ReplicaStageResult StageAuthority(
            in ReplicaStageRequest request,
            out ReplicaStageHandle stageHandle,
            out ReadOnlyMemory<byte> applyPlan);

        ReplicaOutcomeStatus DiscardStage(
            ReplicaStageHandle stageHandle,
            ReplicaStageDiscardReason reason);

        ReplicaOutcomeStatus ObserveRuntimeOutcome(
            ReplicaStageHandle stageHandle,
            in ReplicaRuntimeOutcome outcome,
            out ReplicaCommittedMetadata committedMetadata);

        ReplicaResetResult ResetForNewSession(in ReplicaResetRequest request);

        ReplicaSnapshot GetSnapshot();
    }

    public enum ReplicaStageStatus
    {
        None = 0,
        Staged = 1,
        DuplicateIgnored = 2,
        RequiresResync = 3,
        Rejected = 4,
        Frozen = 5,
        Retryable = 6
    }

    public enum ReplicaOutcomeStatus
    {
        Observed = 0,
        Discarded = 1,
        Aborted = 2,
        Stale = 3,
        Frozen = 4,
        Rejected = 5
    }

    public enum ReplicaStageDiscardReason
    {
        RuntimeAborted = 0,
        PeerStageFailed = 1,
        SessionReset = 2,
        Replaced = 3
    }

    public readonly struct ReplicaStageResult
    {
        public ReplicaStageResult(ReplicaStageStatus status)
        {
            Status = status;
        }

        public ReplicaStageStatus Status { get; }
    }

    public readonly struct ReplicaResetRequest
    {
        public ReplicaResetRequest(ulong generation)
        {
            Generation = generation;
        }

        public ulong Generation { get; }
    }

    public readonly struct ReplicaResetResult
    {
        public ReplicaResetResult(bool succeeded)
        {
            Succeeded = succeeded;
        }

        public bool Succeeded { get; }
    }
}

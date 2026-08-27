using Lumio.Client.Replica;
using Lumio.Client.Replica.Tests.Unit;

namespace Lumio.Client.Replica.Tests.Fault;

public sealed class ReplicaOutcomeFaultTests
{
    [Fact]
    public void StaleStage_CannotAdvanceMetadata()
    {
        var replica = new ClientReplicaFactory().Create();
        replica.ResetForNewSession(new ReplicaResetRequest(4));
        ReplicaStageRequest snapshot = ReplicaRequests.FullSnapshot(4, 12, 2, 1);
        replica.StageAuthority(in snapshot, out ReplicaStageHandle handle, out _);
        ReplicaCommittedMetadata before = replica.GetSnapshot().Committed;

        ReplicaOutcomeStatus discarded = replica.DiscardStage(handle, ReplicaStageDiscardReason.SessionReset);
        ReplicaOutcomeStatus stale = replica.ObserveRuntimeOutcome(
            handle,
            ReplicaRuntimeOutcome.CommittedOutcome(),
            out ReplicaCommittedMetadata afterStale);

        Assert.Equal(ReplicaOutcomeStatus.Discarded, discarded);
        Assert.Equal(ReplicaOutcomeStatus.Stale, stale);
        Assert.False(afterStale.HasBaseline);
        Assert.Equal(before.Revision, afterStale.Revision);
        Assert.Equal(before.Sequence, afterStale.Sequence);

        replica.StageAuthority(in snapshot, out ReplicaStageHandle liveHandle, out _);
        replica.ResetForNewSession(new ReplicaResetRequest(5));
        ReplicaOutcomeStatus lateGeneration = replica.ObserveRuntimeOutcome(
            liveHandle,
            ReplicaRuntimeOutcome.CommittedOutcome(),
            out ReplicaCommittedMetadata afterReset);
        Assert.Equal(ReplicaOutcomeStatus.Stale, lateGeneration);
        Assert.Equal(5UL, afterReset.Generation);
        Assert.False(afterReset.HasBaseline);
        Assert.Equal(0UL, afterReset.Revision);
    }
}

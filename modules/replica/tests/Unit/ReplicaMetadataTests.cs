using Lumio.Client.Replica;

namespace Lumio.Client.Replica.Tests.Unit;

public sealed class ReplicaMetadataTests
{
    [Fact]
    public void CommittedAdvances_AbortedDoesNot()
    {
        var replica = new ClientReplicaFactory().Create();
        replica.ResetForNewSession(new ReplicaResetRequest(7));

        ReplicaStageRequest snapshot = ReplicaRequests.FullSnapshot(7, 21, 3, 1);
        Assert.Equal(
            ReplicaStageStatus.Staged,
            replica.StageAuthority(in snapshot, out ReplicaStageHandle snapshotHandle, out _).Status);

        ReplicaOutcomeStatus committedStatus = replica.ObserveRuntimeOutcome(
            snapshotHandle,
            ReplicaRuntimeOutcome.CommittedOutcome(),
            out ReplicaCommittedMetadata committed);
        Assert.Equal(ReplicaOutcomeStatus.Observed, committedStatus);
        Assert.True(committed.HasBaseline);
        Assert.Equal(7UL, committed.Generation);
        Assert.Equal(21UL, committed.Baseline);
        Assert.Equal(3UL, committed.Revision);
        Assert.Equal(1UL, committed.Sequence);
        Assert.False(committed.Frozen);

        ReplicaStageRequest delta = ReplicaRequests.Delta(7, 21, 3, 4, 2);
        Assert.Equal(
            ReplicaStageStatus.Staged,
            replica.StageAuthority(in delta, out ReplicaStageHandle deltaHandle, out _).Status);

        ReplicaOutcomeStatus abortedStatus = replica.ObserveRuntimeOutcome(
            deltaHandle,
            ReplicaRuntimeOutcome.AbortedOutcome(),
            out ReplicaCommittedMetadata afterAbort);
        Assert.Equal(ReplicaOutcomeStatus.Aborted, abortedStatus);
        Assert.Equal(committed.Baseline, afterAbort.Baseline);
        Assert.Equal(committed.Revision, afterAbort.Revision);
        Assert.Equal(committed.Sequence, afterAbort.Sequence);
        Assert.Equal(committed.HasBaseline, afterAbort.HasBaseline);
        Assert.False(afterAbort.Frozen);
        Assert.Equal(0, replica.GetSnapshot().OpenStageCount);
    }

    [Fact]
    public void IndeterminateFreezesAndRetainsEvidence()
    {
        var replica = new ClientReplicaFactory().Create();
        replica.ResetForNewSession(new ReplicaResetRequest(3));
        ReplicaStageRequest snapshot = ReplicaRequests.FullSnapshot(3, 8, 1, 1);
        replica.StageAuthority(in snapshot, out ReplicaStageHandle handle, out _);
        var evidence = new byte[] { 9, 7, 3 };

        ReplicaOutcomeStatus status = replica.ObserveRuntimeOutcome(
            handle,
            ReplicaRuntimeOutcome.IndeterminateOutcome(evidence),
            out ReplicaCommittedMetadata metadata);

        ReplicaSnapshot frozen = replica.GetSnapshot();
        Assert.Equal(ReplicaOutcomeStatus.Frozen, status);
        Assert.True(metadata.Frozen);
        Assert.True(frozen.Committed.Frozen);
        Assert.False(frozen.Committed.HasBaseline);
        Assert.Equal(0UL, frozen.Committed.Revision);
        Assert.True(frozen.Evidence.Span.SequenceEqual(evidence));

        ReplicaStageResult later = replica.StageAuthority(
            ReplicaRequests.FullSnapshot(3, 8, 2, 2),
            out ReplicaStageHandle laterHandle,
            out ReadOnlyMemory<byte> laterPlan);
        Assert.Equal(ReplicaStageStatus.Frozen, later.Status);
        Assert.True(laterHandle.IsEmpty);
        Assert.True(laterPlan.IsEmpty);
        Assert.True(replica.GetSnapshot().Evidence.Span.SequenceEqual(evidence));
    }
}

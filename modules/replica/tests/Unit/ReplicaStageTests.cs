using Lumio.Client.Replica;

namespace Lumio.Client.Replica.Tests.Unit;

public sealed class ReplicaStageTests
{
    [Fact]
    public void Stage_HasNoVisibleMetadataMutation()
    {
        var mapper = new RecordingMapper();
        var replica = new ClientReplicaFactory().Create(mapper);
        replica.ResetForNewSession(new ReplicaResetRequest(1));
        ReplicaCommittedMetadata before = replica.GetSnapshot().Committed;

        ReplicaStageResult result = replica.StageAuthority(
            ReplicaRequests.FullSnapshot(generation: 1, baseline: 10, toRevision: 4, sequence: 1),
            out ReplicaStageHandle handle,
            out ReadOnlyMemory<byte> applyPlan);

        ReplicaSnapshot after = replica.GetSnapshot();
        Assert.Equal(ReplicaStageStatus.Staged, result.Status);
        Assert.False(handle.IsEmpty);
        Assert.False(applyPlan.IsEmpty);
        Assert.Equal(1, mapper.Calls);
        Assert.Equal(1, after.OpenStageCount);
        AssertEqualCommitted(before, after.Committed);
        Assert.False(after.Committed.HasBaseline);
        Assert.Equal(0UL, after.Committed.Revision);
        Assert.Equal(0UL, after.Committed.Sequence);
    }

    [Fact]
    public void SecondStageCanBeDiscardedWithoutRuntimeCall()
    {
        var mapper = new RecordingMapper();
        var replica = new ClientReplicaFactory().Create(mapper);
        replica.ResetForNewSession(new ReplicaResetRequest(1));

        ReplicaStageResult first = replica.StageAuthority(
            ReplicaRequests.FullSnapshot(generation: 1, baseline: 10, toRevision: 1, sequence: 1),
            out ReplicaStageHandle firstHandle,
            out _);
        Assert.Equal(ReplicaStageStatus.Staged, first.Status);
        Assert.False(firstHandle.IsEmpty);

        ReplicaStageResult second = replica.StageAuthority(
            ReplicaRequests.FullSnapshot(generation: 1, baseline: 11, toRevision: 2, sequence: 2),
            out ReplicaStageHandle secondHandle,
            out _);
        Assert.Equal(ReplicaStageStatus.Staged, second.Status);
        Assert.False(secondHandle.IsEmpty);
        int callsAfterStage = mapper.Calls;
        ReplicaCommittedMetadata beforeDiscard = replica.GetSnapshot().Committed;

        ReplicaOutcomeStatus discarded = replica.DiscardStage(secondHandle, ReplicaStageDiscardReason.PeerStageFailed);

        ReplicaSnapshot afterDiscard = replica.GetSnapshot();
        Assert.Equal(ReplicaOutcomeStatus.Discarded, discarded);
        Assert.Equal(callsAfterStage, mapper.Calls);
        Assert.Equal(1, afterDiscard.OpenStageCount);
        AssertEqualCommitted(beforeDiscard, afterDiscard.Committed);
        Assert.False(afterDiscard.Committed.HasBaseline);

        ReplicaOutcomeStatus firstDiscard = replica.DiscardStage(firstHandle, ReplicaStageDiscardReason.RuntimeAborted);
        Assert.Equal(ReplicaOutcomeStatus.Discarded, firstDiscard);
        Assert.Equal(0, replica.GetSnapshot().OpenStageCount);
        Assert.Equal(callsAfterStage, mapper.Calls);
    }

    [Fact]
    public void Gap_ReturnsRequiresResyncAndNeverCallsMapper()
    {
        var mapper = new RecordingMapper();
        var replica = new ClientReplicaFactory().Create(mapper);
        replica.ResetForNewSession(new ReplicaResetRequest(1));
        Assert.Equal(
            ReplicaStageStatus.Staged,
            replica.StageAuthority(
                ReplicaRequests.FullSnapshot(generation: 1, baseline: 10, toRevision: 1, sequence: 1),
                out ReplicaStageHandle snapshotHandle,
                out _).Status);
        Assert.Equal(
            ReplicaOutcomeStatus.Observed,
            replica.ObserveRuntimeOutcome(
                snapshotHandle,
                ReplicaRuntimeOutcome.CommittedOutcome(),
                out _));
        mapper.ResetCalls();
        ReplicaCommittedMetadata committed = replica.GetSnapshot().Committed;

        ReplicaStageResult gap = replica.StageAuthority(
            ReplicaRequests.Delta(generation: 1, baseline: 10, fromRevision: 1, toRevision: 3, sequence: 3),
            out ReplicaStageHandle handle,
            out ReadOnlyMemory<byte> applyPlan);

        ReplicaSnapshot after = replica.GetSnapshot();
        Assert.Equal(ReplicaStageStatus.RequiresResync, gap.Status);
        Assert.True(handle.IsEmpty);
        Assert.True(applyPlan.IsEmpty);
        Assert.Equal(0, mapper.Calls);
        Assert.Equal(0, after.OpenStageCount);
        AssertEqualCommitted(committed, after.Committed);
        Assert.Equal(ReplicaStageStatus.RequiresResync, after.LastStageStatus);
    }

    private static void AssertEqualCommitted(in ReplicaCommittedMetadata left, in ReplicaCommittedMetadata right)
    {
        Assert.Equal(left.Generation, right.Generation);
        Assert.Equal(left.Baseline, right.Baseline);
        Assert.Equal(left.Revision, right.Revision);
        Assert.Equal(left.Sequence, right.Sequence);
        Assert.Equal(left.HasBaseline, right.HasBaseline);
        Assert.Equal(left.Frozen, right.Frozen);
    }

    private sealed class RecordingMapper : IReplicaMapper
    {
        public int Calls { get; private set; }

        public ReplicaMappingResult Map(
            in ReplicaStageRequest request,
            in ReplicaMappingContext context,
            out ReadOnlyMemory<byte> applyPlan)
        {
            Calls++;
            _ = context;
            if (request.Update.IsEmpty || request.Update.Span[0] == 0)
            {
                applyPlan = ReadOnlyMemory<byte>.Empty;
                return new ReplicaMappingResult(false);
            }

            applyPlan = request.Update.ToArray();
            return new ReplicaMappingResult(true);
        }

        public void ResetCalls()
        {
            Calls = 0;
        }
    }
}

internal static class ReplicaRequests
{
    public static ReplicaStageRequest FullSnapshot(
        ulong generation,
        ulong baseline,
        ulong toRevision,
        ulong sequence,
        byte[]? update = null,
        ulong[]? tombstones = null,
        ulong[]? touched = null)
    {
        return new ReplicaStageRequest(
            generation,
            ReplicaUpdateKind.FullSnapshot,
            baseline,
            0,
            toRevision,
            sequence,
            update ?? new byte[] { 1 },
            tombstones ?? Array.Empty<ulong>(),
            touched ?? Array.Empty<ulong>());
    }

    public static ReplicaStageRequest Delta(
        ulong generation,
        ulong baseline,
        ulong fromRevision,
        ulong toRevision,
        ulong sequence,
        byte[]? update = null,
        ulong[]? tombstones = null,
        ulong[]? touched = null)
    {
        return new ReplicaStageRequest(
            generation,
            ReplicaUpdateKind.Delta,
            baseline,
            fromRevision,
            toRevision,
            sequence,
            update ?? new byte[] { 2 },
            tombstones ?? Array.Empty<ulong>(),
            touched ?? Array.Empty<ulong>());
    }
}

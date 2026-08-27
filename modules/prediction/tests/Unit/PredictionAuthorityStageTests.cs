using Lumio.Client.Prediction;

namespace Lumio.Client.Prediction.Tests.Unit;

public sealed class PredictionAuthorityStageTests
{
    [Fact]
    public void Stage_HasNoHistoryMutation()
    {
        IClientPrediction prediction = Create();
        AcceptedPredictionCommand committed = Commit(prediction, 3);
        Assert.Equal(1UL, committed.CommandSeq.Value);
        PredictionSnapshot before = prediction.GetSnapshot();

        var update = new AuthorityPredictionUpdate(new byte[] { 1 }, 1UL);
        var context = new PredictionAuthorityContext(1UL);
        PredictionAuthorityResult staged = prediction.StageAuthority(
            in update,
            in context,
            out PredictionAuthorityStage stage,
            out PredictionReconcilePlan reconcilePlan);

        PredictionSnapshot after = prediction.GetSnapshot();
        Assert.Equal(PredictionAuthorityStatus.Staged, staged.Status);
        Assert.True(stage.Id != 0UL);
        Assert.False(reconcilePlan.OpaqueBytes.IsEmpty);
        Assert.Equal(before.HistoryCount, after.HistoryCount);
        Assert.Equal(before.LastAssignedSeq, after.LastAssignedSeq);
        Assert.Equal(before.ConfirmedSeq, after.ConfirmedSeq);
        Assert.Equal(1, after.HistoryCount);
        Assert.Equal(0UL, after.ConfirmedSeq);
    }

    [Fact]
    public void CorrectionPlan_ComposesWithReplicaPlan()
    {
        IClientPrediction prediction = Create();
        Commit(prediction, 1);
        Commit(prediction, 2);
        PredictionSnapshot before = prediction.GetSnapshot();

        var update = new AuthorityPredictionUpdate(new byte[] { 2 }, 0UL);
        var context = new PredictionAuthorityContext(1UL);
        PredictionAuthorityResult staged = prediction.StageAuthority(
            in update,
            in context,
            out _,
            out PredictionReconcilePlan reconcilePlan);

        Assert.Equal(PredictionAuthorityStatus.Staged, staged.Status);
        Assert.False(reconcilePlan.OpaqueBytes.IsEmpty);
        Assert.Contains((byte)3, reconcilePlan.OpaqueBytes.ToArray());

        byte[] replicaPlan = { 11, 22, 33 };
        ReadOnlyMemory<byte> composed = RuntimePredictionPlanAdapter.Compose(replicaPlan, reconcilePlan.OpaqueBytes);
        Assert.True(ContainsSlice(composed, replicaPlan));
        Assert.True(ContainsSlice(composed, reconcilePlan.OpaqueBytes));
        Assert.Equal(11, replicaPlan[0]);
        Assert.Equal(before.HistoryCount, prediction.GetSnapshot().HistoryCount);
        Assert.Equal(before.ConfirmedSeq, prediction.GetSnapshot().ConfirmedSeq);
        Assert.Equal(before.LastAssignedSeq, prediction.GetSnapshot().LastAssignedSeq);
    }

    private static IClientPrediction Create()
    {
        return new ClientPredictionFactory().Create(new PredictionCreateRequest(1UL, 8));
    }

    private static AcceptedPredictionCommand Commit(IClientPrediction prediction, byte payload)
    {
        var candidate = new PredictionCandidate(1UL, new byte[] { payload });
        var context = new PredictionCandidateContext(1UL);
        prediction.AcceptCandidate(in candidate, in context, out PredictionCandidateStage stage, out _);
        var outcome = new LocalPredictionOutcome(PredictionOutcomeKind.Committed, stage.Id, stage.Generation);
        prediction.ObserveLocalPredictionOutcome(stage, in outcome, out AcceptedPredictionCommand accepted);
        return accepted;
    }

    private static bool ContainsSlice(ReadOnlyMemory<byte> haystack, ReadOnlyMemory<byte> needle)
    {
        ReadOnlySpan<byte> h = haystack.Span;
        ReadOnlySpan<byte> n = needle.Span;
        if (n.Length == 0 || n.Length > h.Length)
        {
            return false;
        }

        for (int i = 0; i <= h.Length - n.Length; i++)
        {
            if (h.Slice(i, n.Length).SequenceEqual(n))
            {
                return true;
            }
        }

        return false;
    }
}

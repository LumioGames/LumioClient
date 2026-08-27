using Lumio.Client.Prediction;

namespace Lumio.Client.Prediction.Tests.Fault;

public sealed class PredictionOutcomeFaultTests
{
    [Fact]
    public void IndeterminateFreezesHistory()
    {
        IClientPrediction prediction = new ClientPredictionFactory().Create(new PredictionCreateRequest(1UL, 8));
        Commit(prediction, 5);
        var confirmation = new AuthorityPredictionUpdate(new byte[] { 1 }, 1UL);
        var authorityContext = new PredictionAuthorityContext(1UL);
        prediction.StageAuthority(in confirmation, in authorityContext, out PredictionAuthorityStage stage, out _);

        var indeterminate = new AuthorityRuntimeOutcome(PredictionOutcomeKind.Indeterminate, stage.Id, stage.Generation);
        PredictionAuthorityOutcomeResult observed = prediction.ObserveRuntimeOutcome(stage, in indeterminate);

        PredictionSnapshot frozen = prediction.GetSnapshot();
        Assert.Equal(PredictionAuthorityOutcomeStatus.Indeterminate, observed.Status);
        Assert.True(frozen.Frozen);
        Assert.Equal(1, frozen.HistoryCount);
        Assert.Equal(0UL, frozen.ConfirmedSeq);
        Assert.Equal(1UL, frozen.LastAssignedSeq);

        var candidate = new PredictionCandidate(9UL, new byte[] { 8 });
        var candidateContext = new PredictionCandidateContext(1UL);
        PredictionCandidateResult accept = prediction.AcceptCandidate(in candidate, in candidateContext, out _, out _);
        Assert.Equal(PredictionCandidateStatus.Frozen, accept.Status);
        Assert.Equal(1UL, prediction.GetSnapshot().LastAssignedSeq);

        PredictionAuthorityResult authority = prediction.StageAuthority(in confirmation, in authorityContext, out _, out _);
        Assert.Equal(PredictionAuthorityStatus.Frozen, authority.Status);
        Assert.Equal(1, prediction.GetSnapshot().HistoryCount);
        Assert.Equal(0UL, prediction.GetSnapshot().ConfirmedSeq);
    }

    [Fact]
    public void StaleStage_DoesNotMutateHistory()
    {
        IClientPrediction prediction = new ClientPredictionFactory().Create(new PredictionCreateRequest(1UL, 8));
        Commit(prediction, 1);
        var candidate = new PredictionCandidate(2UL, new byte[] { 2 });
        var context = new PredictionCandidateContext(1UL);
        prediction.AcceptCandidate(in candidate, in context, out PredictionCandidateStage stage, out _);
        prediction.DiscardCandidateStage(stage, PredictionStageDiscardReason.Cancelled);

        var committed = new LocalPredictionOutcome(PredictionOutcomeKind.Committed, stage.Id, stage.Generation);
        PredictionLocalOutcomeResult stale = prediction.ObserveLocalPredictionOutcome(
            stage,
            in committed,
            out AcceptedPredictionCommand accepted);

        Assert.Equal(PredictionLocalOutcomeStatus.StaleStage, stale.Status);
        Assert.Equal(0UL, accepted.CommandSeq.Value);
        Assert.Equal(1UL, prediction.GetSnapshot().LastAssignedSeq);
        Assert.Equal(1, prediction.GetSnapshot().HistoryCount);

        var missing = new PredictionAuthorityStage(99UL, 1UL);
        var outcome = new AuthorityRuntimeOutcome(PredictionOutcomeKind.Committed, 99UL, 1UL);
        PredictionAuthorityOutcomeResult authorityStale = prediction.ObserveRuntimeOutcome(missing, in outcome);
        Assert.Equal(PredictionAuthorityOutcomeStatus.StaleStage, authorityStale.Status);
        Assert.Equal(0UL, prediction.GetSnapshot().ConfirmedSeq);
        Assert.False(prediction.GetSnapshot().Frozen);
    }

    [Fact]
    public void LateGeneration_DoesNotConsumeSequence()
    {
        IClientPrediction prediction = new ClientPredictionFactory().Create(new PredictionCreateRequest(1UL, 8));
        var candidate = new PredictionCandidate(1UL, new byte[] { 1 });
        var late = new PredictionCandidateContext(2UL);
        PredictionCandidateResult result = prediction.AcceptCandidate(in candidate, in late, out _, out _);
        Assert.Equal(PredictionCandidateStatus.StaleGeneration, result.Status);
        Assert.Equal(0UL, prediction.GetSnapshot().LastAssignedSeq);
    }

    private static void Commit(IClientPrediction prediction, byte payload)
    {
        var candidate = new PredictionCandidate(1UL, new byte[] { payload });
        var context = new PredictionCandidateContext(1UL);
        prediction.AcceptCandidate(in candidate, in context, out PredictionCandidateStage stage, out _);
        var outcome = new LocalPredictionOutcome(PredictionOutcomeKind.Committed, stage.Id, stage.Generation);
        prediction.ObserveLocalPredictionOutcome(stage, in outcome, out _);
    }
}

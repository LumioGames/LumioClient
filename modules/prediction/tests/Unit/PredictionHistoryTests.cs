using Lumio.Client.Prediction;

namespace Lumio.Client.Prediction.Tests.Unit;

public sealed class PredictionHistoryTests
{
    [Fact]
    public void ConfirmationPrunesOnlyAfterAuthorityCommit()
    {
        IClientPrediction prediction = new ClientPredictionFactory().Create(new PredictionCreateRequest(1UL, 8));
        Commit(prediction, 3);
        Commit(prediction, 4);
        Assert.Equal(2, prediction.GetSnapshot().HistoryCount);
        Assert.Equal(0UL, prediction.GetSnapshot().ConfirmedSeq);

        var context = new PredictionAuthorityContext(1UL);
        var confirmation = new AuthorityPredictionUpdate(new byte[] { 1 }, 1UL);
        PredictionAuthorityResult staged = prediction.StageAuthority(
            in confirmation,
            in context,
            out PredictionAuthorityStage stage,
            out _);
        Assert.Equal(PredictionAuthorityStatus.Staged, staged.Status);
        Assert.Equal(2, prediction.GetSnapshot().HistoryCount);

        var aborted = new AuthorityRuntimeOutcome(PredictionOutcomeKind.Aborted, stage.Id, stage.Generation);
        PredictionAuthorityOutcomeResult abortedResult = prediction.ObserveRuntimeOutcome(stage, in aborted);
        Assert.Equal(PredictionAuthorityOutcomeStatus.Discarded, abortedResult.Status);
        Assert.Equal(2, prediction.GetSnapshot().HistoryCount);
        Assert.Equal(0UL, prediction.GetSnapshot().ConfirmedSeq);

        prediction.StageAuthority(in confirmation, in context, out PredictionAuthorityStage committedStage, out _);
        var committed = new AuthorityRuntimeOutcome(PredictionOutcomeKind.Committed, committedStage.Id, committedStage.Generation);
        PredictionAuthorityOutcomeResult applied = prediction.ObserveRuntimeOutcome(committedStage, in committed);
        Assert.Equal(PredictionAuthorityOutcomeStatus.Applied, applied.Status);
        Assert.Equal(1, prediction.GetSnapshot().HistoryCount);
        Assert.Equal(1UL, prediction.GetSnapshot().ConfirmedSeq);
        Assert.Equal(2UL, prediction.GetSnapshot().LastAssignedSeq);
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

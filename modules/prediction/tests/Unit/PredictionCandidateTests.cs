using Lumio.Client.Prediction;

namespace Lumio.Client.Prediction.Tests.Unit;

public sealed class PredictionCandidateTests
{
    [Fact]
    public void RejectedCandidate_DoesNotConsumeClientCommandSeq()
    {
        IClientPrediction prediction = Create();
        var candidate = new PredictionCandidate(1UL, ReadOnlyMemory<byte>.Empty);
        var context = new PredictionCandidateContext(1UL);

        PredictionCandidateResult result = prediction.AcceptCandidate(
            in candidate,
            in context,
            out PredictionCandidateStage stage,
            out LocalPredictionPlan localPlan);

        PredictionSnapshot snapshot = prediction.GetSnapshot();
        Assert.Equal(PredictionCandidateStatus.Rejected, result.Status);
        Assert.Equal(0UL, stage.Id);
        Assert.True(localPlan.OpaqueBytes.IsEmpty);
        Assert.Equal(0UL, snapshot.LastAssignedSeq);
        Assert.Equal(0, snapshot.HistoryCount);
        Assert.Equal(0, snapshot.OpenCandidateStages);
    }

    [Fact]
    public void LocalAborted_DoesNotConsumeOrEnterHistory()
    {
        IClientPrediction prediction = Create();
        var candidate = new PredictionCandidate(2UL, new byte[] { 7 });
        var context = new PredictionCandidateContext(1UL);
        PredictionCandidateResult staged = prediction.AcceptCandidate(
            in candidate,
            in context,
            out PredictionCandidateStage stage,
            out LocalPredictionPlan localPlan);

        Assert.Equal(PredictionCandidateStatus.Staged, staged.Status);
        Assert.False(localPlan.OpaqueBytes.IsEmpty);
        Assert.Equal(0UL, prediction.GetSnapshot().LastAssignedSeq);
        Assert.Equal(0, prediction.GetSnapshot().HistoryCount);

        var aborted = new LocalPredictionOutcome(PredictionOutcomeKind.Aborted, stage.Id, stage.Generation);
        PredictionLocalOutcomeResult observed = prediction.ObserveLocalPredictionOutcome(
            stage,
            in aborted,
            out AcceptedPredictionCommand accepted);

        PredictionSnapshot snapshot = prediction.GetSnapshot();
        Assert.Equal(PredictionLocalOutcomeStatus.Discarded, observed.Status);
        Assert.Equal(0UL, accepted.CommandSeq.Value);
        Assert.Equal(0UL, accepted.Key.Value);
        Assert.Equal(0UL, snapshot.LastAssignedSeq);
        Assert.Equal(0, snapshot.HistoryCount);
        Assert.Equal(0, snapshot.OpenCandidateStages);

        AcceptedPredictionCommand first = Commit(prediction, 3);
        Assert.Equal(1UL, first.CommandSeq.Value);
        Assert.Equal(1UL, first.Key.Value);
    }

    [Fact]
    public void LocalCommitted_AssignsSeqAndKeyOnce()
    {
        IClientPrediction prediction = Create();
        var candidate = new PredictionCandidate(4UL, new byte[] { 9 });
        var context = new PredictionCandidateContext(1UL);
        PredictionCandidateResult staged = prediction.AcceptCandidate(
            in candidate,
            in context,
            out PredictionCandidateStage stage,
            out LocalPredictionPlan localPlan);

        Assert.Equal(PredictionCandidateStatus.Staged, staged.Status);
        Assert.False(localPlan.OpaqueBytes.IsEmpty);
        Assert.Equal(0UL, prediction.GetSnapshot().LastAssignedSeq);
        Assert.Equal(0, prediction.GetSnapshot().HistoryCount);

        var committed = new LocalPredictionOutcome(PredictionOutcomeKind.Committed, stage.Id, stage.Generation);
        PredictionLocalOutcomeResult observed = prediction.ObserveLocalPredictionOutcome(
            stage,
            in committed,
            out AcceptedPredictionCommand accepted);

        Assert.Equal(PredictionLocalOutcomeStatus.Assigned, observed.Status);
        Assert.Equal(1UL, accepted.CommandSeq.Value);
        Assert.Equal(1UL, accepted.Key.Value);
        Assert.Equal(1UL, prediction.GetSnapshot().LastAssignedSeq);
        Assert.Equal(1, prediction.GetSnapshot().HistoryCount);

        PredictionLocalOutcomeResult again = prediction.ObserveLocalPredictionOutcome(
            stage,
            in committed,
            out AcceptedPredictionCommand second);

        Assert.Equal(PredictionLocalOutcomeStatus.StaleStage, again.Status);
        Assert.Equal(0UL, second.CommandSeq.Value);
        Assert.Equal(1UL, prediction.GetSnapshot().LastAssignedSeq);
        Assert.Equal(1, prediction.GetSnapshot().HistoryCount);

        AcceptedPredictionCommand next = Commit(prediction, 8);
        Assert.Equal(2UL, next.CommandSeq.Value);
        Assert.Equal(2UL, next.Key.Value);
        Assert.Equal(2, prediction.GetSnapshot().HistoryCount);
    }

    [Fact]
    public void DiscardCandidateStage_DoesNotConsumeOrEnterHistory()
    {
        IClientPrediction prediction = Create();
        var candidate = new PredictionCandidate(5UL, new byte[] { 4 });
        var context = new PredictionCandidateContext(1UL);
        prediction.AcceptCandidate(in candidate, in context, out PredictionCandidateStage stage, out _);

        PredictionLocalOutcomeResult discarded = prediction.DiscardCandidateStage(
            stage,
            PredictionStageDiscardReason.Aborted);

        Assert.Equal(PredictionLocalOutcomeStatus.Discarded, discarded.Status);
        Assert.Equal(0UL, prediction.GetSnapshot().LastAssignedSeq);
        Assert.Equal(0, prediction.GetSnapshot().HistoryCount);
        Assert.Equal(1UL, Commit(prediction, 1).CommandSeq.Value);
    }

    private static IClientPrediction Create()
    {
        return new ClientPredictionFactory().Create(new PredictionCreateRequest(1UL, 8));
    }

    private static AcceptedPredictionCommand Commit(IClientPrediction prediction, byte payload)
    {
        var candidate = new PredictionCandidate(10UL, new byte[] { payload });
        var context = new PredictionCandidateContext(1UL);
        PredictionCandidateResult staged = prediction.AcceptCandidate(
            in candidate,
            in context,
            out PredictionCandidateStage stage,
            out _);
        Assert.Equal(PredictionCandidateStatus.Staged, staged.Status);
        var outcome = new LocalPredictionOutcome(PredictionOutcomeKind.Committed, stage.Id, stage.Generation);
        PredictionLocalOutcomeResult observed = prediction.ObserveLocalPredictionOutcome(
            stage,
            in outcome,
            out AcceptedPredictionCommand accepted);
        Assert.Equal(PredictionLocalOutcomeStatus.Assigned, observed.Status);
        return accepted;
    }
}

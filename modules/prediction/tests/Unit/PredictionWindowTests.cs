using Lumio.Client.Prediction;

namespace Lumio.Client.Prediction.Tests.Unit;

public sealed class PredictionWindowTests
{
    [Fact]
    public void Full_ReturnsExplicitBackpressureWithoutSequenceUse()
    {
        IClientPrediction prediction = new ClientPredictionFactory().Create(new PredictionCreateRequest(1UL, 1));
        var context = new PredictionCandidateContext(1UL);
        var first = new PredictionCandidate(1UL, new byte[] { 1 });
        PredictionCandidateResult staged = prediction.AcceptCandidate(in first, in context, out PredictionCandidateStage stage, out _);
        Assert.Equal(PredictionCandidateStatus.Staged, staged.Status);
        var committed = new LocalPredictionOutcome(PredictionOutcomeKind.Committed, stage.Id, stage.Generation);
        prediction.ObserveLocalPredictionOutcome(stage, in committed, out AcceptedPredictionCommand accepted);
        Assert.Equal(1UL, accepted.CommandSeq.Value);
        Assert.Equal(1, prediction.GetSnapshot().HistoryCount);

        var second = new PredictionCandidate(2UL, new byte[] { 2 });
        PredictionCandidateResult busy = prediction.AcceptCandidate(
            in second,
            in context,
            out PredictionCandidateStage busyStage,
            out LocalPredictionPlan busyPlan);

        PredictionSnapshot snapshot = prediction.GetSnapshot();
        Assert.Equal(PredictionCandidateStatus.WindowBusy, busy.Status);
        Assert.Equal(0UL, busyStage.Id);
        Assert.True(busyPlan.OpaqueBytes.IsEmpty);
        Assert.Equal(1UL, snapshot.LastAssignedSeq);
        Assert.Equal(1, snapshot.HistoryCount);
        Assert.Equal(0, snapshot.OpenCandidateStages);
        Assert.Equal(1, snapshot.HighWatermark);

        var empty = new PredictionCandidate(3UL, ReadOnlyMemory<byte>.Empty);
        PredictionCandidateResult rejected = prediction.AcceptCandidate(in empty, in context, out _, out _);
        Assert.Equal(PredictionCandidateStatus.Rejected, rejected.Status);
        Assert.Equal(1UL, prediction.GetSnapshot().LastAssignedSeq);
    }
}

using Lumio.Client.Prediction;

namespace Lumio.Client.Prediction.Tests.Properties;

public sealed class PredictionSequenceProperties
{
    [Fact]
    public void AcceptedSequencesStrictlyIncrease()
    {
        IClientPrediction prediction = new ClientPredictionFactory().Create(new PredictionCreateRequest(1UL, 64));
        ulong previous = 0;
        for (int i = 0; i < 16; i++)
        {
            var rejected = new PredictionCandidate((ulong)i, ReadOnlyMemory<byte>.Empty);
            var context = new PredictionCandidateContext(1UL);
            PredictionCandidateResult rejectResult = prediction.AcceptCandidate(
                in rejected,
                in context,
                out _,
                out _);
            Assert.Equal(PredictionCandidateStatus.Rejected, rejectResult.Status);

            var abortCandidate = new PredictionCandidate((ulong)(100 + i), new byte[] { 2 });
            prediction.AcceptCandidate(in abortCandidate, in context, out PredictionCandidateStage abortStage, out _);
            var aborted = new LocalPredictionOutcome(PredictionOutcomeKind.Aborted, abortStage.Id, abortStage.Generation);
            prediction.ObserveLocalPredictionOutcome(abortStage, in aborted, out _);

            var candidate = new PredictionCandidate((ulong)(200 + i), new byte[] { (byte)(i + 1) });
            PredictionCandidateResult staged = prediction.AcceptCandidate(
                in candidate,
                in context,
                out PredictionCandidateStage stage,
                out _);
            Assert.Equal(PredictionCandidateStatus.Staged, staged.Status);
            var committed = new LocalPredictionOutcome(PredictionOutcomeKind.Committed, stage.Id, stage.Generation);
            PredictionLocalOutcomeResult observed = prediction.ObserveLocalPredictionOutcome(
                stage,
                in committed,
                out AcceptedPredictionCommand accepted);
            Assert.Equal(PredictionLocalOutcomeStatus.Assigned, observed.Status);
            Assert.True(accepted.CommandSeq.Value > previous);
            Assert.Equal(accepted.CommandSeq.Value, accepted.Key.Value);
            previous = accepted.CommandSeq.Value;
        }

        Assert.Equal(16UL, previous);
        Assert.Equal(16UL, prediction.GetSnapshot().LastAssignedSeq);
        Assert.Equal(16, prediction.GetSnapshot().HistoryCount);
    }
}

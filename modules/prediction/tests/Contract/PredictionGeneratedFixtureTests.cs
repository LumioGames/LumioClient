using Lumio.Client.Prediction;

namespace Lumio.Client.Prediction.Tests.Contract;

public sealed class PredictionGeneratedFixtureTests
{
    [Fact]
    public void ConfirmationCorrectionVectors()
    {
        Assert.True(GeneratedPredictionAdapter.TryClassify(new byte[] { 1 }, out PredictionUpdateKind confirmation));
        Assert.Equal(PredictionUpdateKind.Confirmation, confirmation);
        Assert.True(GeneratedPredictionAdapter.TryClassify(new byte[] { 2, 7 }, out PredictionUpdateKind correction));
        Assert.Equal(PredictionUpdateKind.Correction, correction);
        Assert.False(GeneratedPredictionAdapter.TryClassify(ReadOnlyMemory<byte>.Empty, out _));
        Assert.True(GeneratedPredictionAdapter.TryClassify(new byte[] { 9 }, out PredictionUpdateKind opaqueKind));
        Assert.Equal(PredictionUpdateKind.Confirmation, opaqueKind);

        IClientPrediction prediction = new ClientPredictionFactory().Create(new PredictionCreateRequest(1UL, 8));
        var candidate = new PredictionCandidate(1UL, new byte[] { 4 });
        var candidateContext = new PredictionCandidateContext(1UL);
        prediction.AcceptCandidate(in candidate, in candidateContext, out PredictionCandidateStage localStage, out _);
        var localOutcome = new LocalPredictionOutcome(PredictionOutcomeKind.Committed, localStage.Id, localStage.Generation);
        prediction.ObserveLocalPredictionOutcome(localStage, in localOutcome, out _);
        PredictionSnapshot before = prediction.GetSnapshot();

        var authorityContext = new PredictionAuthorityContext(1UL);
        var empty = new AuthorityPredictionUpdate(ReadOnlyMemory<byte>.Empty, 1UL);
        PredictionAuthorityResult rejected = prediction.StageAuthority(
            in empty,
            in authorityContext,
            out PredictionAuthorityStage rejectedStage,
            out PredictionReconcilePlan rejectedPlan);
        Assert.Equal(PredictionAuthorityStatus.Rejected, rejected.Status);
        Assert.Equal(0UL, rejectedStage.Id);
        Assert.True(rejectedPlan.OpaqueBytes.IsEmpty);
        Assert.Equal(before.HistoryCount, prediction.GetSnapshot().HistoryCount);

        var opaque = new AuthorityPredictionUpdate(new byte[] { 9 }, 1UL);
        Assert.Equal(
            PredictionAuthorityStatus.Staged,
            prediction.StageAuthority(in opaque, in authorityContext, out _, out _).Status);

        var confirmationUpdate = new AuthorityPredictionUpdate(new byte[] { 1 }, 1UL);
        Assert.Equal(
            PredictionAuthorityStatus.Staged,
            prediction.StageAuthority(in confirmationUpdate, in authorityContext, out _, out _).Status);

        var missingHistory = new AuthorityPredictionUpdate(new byte[] { 1 }, 99UL);
        Assert.Equal(
            PredictionAuthorityStatus.RequiresResync,
            prediction.StageAuthority(in missingHistory, in authorityContext, out _, out _).Status);
    }
}

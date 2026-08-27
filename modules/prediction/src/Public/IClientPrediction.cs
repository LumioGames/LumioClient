namespace Lumio.Client.Prediction
{
    public interface IClientPrediction
    {
        PredictionCandidateResult AcceptCandidate(
            in PredictionCandidate candidate,
            in PredictionCandidateContext context,
            out PredictionCandidateStage stage,
            out LocalPredictionPlan localPlan);

        PredictionLocalOutcomeResult DiscardCandidateStage(
            PredictionCandidateStage stage,
            PredictionStageDiscardReason reason);

        PredictionLocalOutcomeResult ObserveLocalPredictionOutcome(
            PredictionCandidateStage stage,
            in LocalPredictionOutcome outcome,
            out AcceptedPredictionCommand acceptedCommand);

        PredictionAuthorityResult StageAuthority(
            in AuthorityPredictionUpdate update,
            in PredictionAuthorityContext context,
            out PredictionAuthorityStage stage,
            out PredictionReconcilePlan reconcilePlan);

        PredictionAuthorityOutcomeResult DiscardAuthorityStage(
            PredictionAuthorityStage stage,
            PredictionStageDiscardReason reason);

        PredictionAuthorityOutcomeResult ObserveRuntimeOutcome(
            PredictionAuthorityStage stage,
            in AuthorityRuntimeOutcome outcome);

        PredictionResetResult ResetForNewSession(in PredictionResetRequest request);

        PredictionSnapshot GetSnapshot();
    }
}

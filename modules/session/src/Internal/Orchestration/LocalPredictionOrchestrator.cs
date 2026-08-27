using System;
using System.Threading;
using Lumio.Client.Input;
using Lumio.Client.Prediction;

namespace Lumio.Client.Session
{
    internal sealed class LocalPredictionOrchestrator
    {
        private readonly bool _owned = true;

        public void Tick(
            IInputCommandSource commands,
            IClientPrediction prediction,
            IClientRuntimePort runtime,
            ulong generation)
        {
            if (!_owned)
            {
                return;
            }

            var buffer = new GameplayCommandCandidate[8];
            int n = commands.DrainCandidates(buffer, new InputDrainContext(generation, 8));
            for (int i = 0; i < n; i++)
            {
                PredictionCandidateStage stage;
                LocalPredictionPlan plan;
                PredictionCandidateResult accepted = prediction.AcceptCandidate(
                    new PredictionCandidate(buffer[i].SampleSeq.Value, buffer[i].Payload),
                    new PredictionCandidateContext(generation),
                    out stage,
                    out plan);
                if (!accepted.Succeeded)
                {
                    continue;
                }

                var pending = runtime.ApplyLocalPrediction(new RuntimeTransactionRequest(generation, plan.OpaqueBytes), CancellationToken.None);
                RuntimeTransactionOutcome outcome = pending.IsCompleted ? pending.Result : new RuntimeTransactionOutcome(false);
                if (outcome.Committed)
                {
                    AcceptedPredictionCommand command;
                    prediction.ObserveLocalPredictionOutcome(
                        stage,
                        new LocalPredictionOutcome(PredictionOutcomeKind.Committed, stage.Id, stage.Generation),
                        out command);
                }
                else
                {
                    prediction.DiscardCandidateStage(stage, PredictionStageDiscardReason.Aborted);
                }
            }
        }
    }
}

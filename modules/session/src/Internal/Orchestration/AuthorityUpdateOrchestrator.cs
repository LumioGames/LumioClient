using System;
using System.Threading;
using Lumio.Client.Prediction;
using Lumio.Client.Replica;

namespace Lumio.Client.Session
{
    internal sealed class AuthorityUpdateOrchestrator
    {
        private readonly bool _owned = true;

        public bool TryCommit(
            IClientReplica replica,
            IClientPrediction prediction,
            IClientRuntimePort runtime,
            IClientPresentationSink presentation,
            AuthorityStageBundle bundle,
            ulong generation,
            ReadOnlyMemory<byte> update,
            ReplicaUpdateKind kind,
            ulong sequence,
            out bool baselineAck,
            out bool presented,
            out bool committed)
        {
            baselineAck = false;
            presented = false;
            committed = false;
            if (!_owned)
            {
                return false;
            }
            ReplicaStageHandle replicaHandle;
            ReadOnlyMemory<byte> replicaPlan;
            ReplicaStageResult replicaStage = replica.StageAuthority(
                new ReplicaStageRequest(generation, kind, sequence == 0 ? 0 : sequence - 1, sequence == 0 ? 0 : sequence - 1, sequence, sequence, update, ReadOnlyMemory<ulong>.Empty, ReadOnlyMemory<ulong>.Empty),
                out replicaHandle,
                out replicaPlan);
            if (replicaStage.Status != ReplicaStageStatus.Staged)
            {
                return replicaStage.Status == ReplicaStageStatus.RequiresResync;
            }

            bundle.Replica = replicaHandle;
            bundle.ReplicaStaged = true;

            PredictionAuthorityStage predictionStage;
            PredictionReconcilePlan reconcile;
            PredictionAuthorityResult predictionResult = prediction.StageAuthority(
                new AuthorityPredictionUpdate(update, 0),
                new PredictionAuthorityContext(generation),
                out predictionStage,
                out reconcile);
            if (!predictionResult.Succeeded)
            {
                replica.DiscardStage(replicaHandle, ReplicaStageDiscardReason.PeerStageFailed);
                bundle.Clear();
                return predictionResult.Status == PredictionAuthorityStatus.RequiresResync;
            }

            bundle.Prediction = predictionStage;
            bundle.PredictionStaged = true;

            var pending = runtime.ApplyAuthoritativeTransaction(new RuntimeTransactionRequest(generation, replicaPlan), CancellationToken.None);
            RuntimeTransactionOutcome outcome = pending.IsCompleted ? pending.Result : new RuntimeTransactionOutcome(false);
            ReplicaRuntimeOutcome replicaOutcome = outcome.Committed
                ? ReplicaRuntimeOutcome.CommittedOutcome()
                : ReplicaRuntimeOutcome.AbortedOutcome();
            ReplicaCommittedMetadata metadata;
            replica.ObserveRuntimeOutcome(replicaHandle, in replicaOutcome, out metadata);
            prediction.ObserveRuntimeOutcome(
                predictionStage,
                new AuthorityRuntimeOutcome(
                    outcome.Committed ? PredictionOutcomeKind.Committed : PredictionOutcomeKind.Aborted,
                    predictionStage.Id,
                    predictionStage.Generation));
            if (!outcome.Committed)
            {
                replica.DiscardStage(replicaHandle, ReplicaStageDiscardReason.RuntimeAborted);
                prediction.DiscardAuthorityStage(predictionStage, PredictionStageDiscardReason.Aborted);
                bundle.Clear();
                return false;
            }

            committed = true;
            baselineAck = true;
            presented = presentation.TryWrite(replicaPlan, generation).Accepted;
            bundle.Clear();
            return true;
        }
    }
}

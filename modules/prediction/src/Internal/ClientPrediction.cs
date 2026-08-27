namespace Lumio.Client.Prediction
{
    internal sealed class ClientPrediction : IClientPrediction
    {
        private readonly PredictionSequenceAllocator _allocator = new PredictionSequenceAllocator();
        private readonly PredictionHistory _history = new PredictionHistory();
        private readonly PredictionStageLedger _ledger = new PredictionStageLedger();
        private readonly PredictionWindowPolicy _window;
        private ulong _generation;
        private ulong _confirmedSeq;
        private bool _frozen;

        public ClientPrediction(ulong generation, int windowCapacity)
        {
            _generation = generation;
            _window = new PredictionWindowPolicy(windowCapacity);
        }

        public PredictionCandidateResult AcceptCandidate(
            in PredictionCandidate candidate,
            in PredictionCandidateContext context,
            out PredictionCandidateStage stage,
            out LocalPredictionPlan localPlan)
        {
            stage = default;
            localPlan = default;
            if (_frozen)
            {
                return new PredictionCandidateResult(PredictionCandidateStatus.Frozen);
            }

            if (context.Generation != _generation)
            {
                return new PredictionCandidateResult(PredictionCandidateStatus.StaleGeneration);
            }

            if (candidate.Payload.IsEmpty)
            {
                return new PredictionCandidateResult(PredictionCandidateStatus.Rejected);
            }

            if (!_window.CanAccept(Occupancy))
            {
                return new PredictionCandidateResult(PredictionCandidateStatus.WindowBusy);
            }

            stage = _ledger.OpenCandidate(_generation, candidate.SampleSeq, candidate.Payload);
            localPlan = RuntimePredictionPlanAdapter.CreateLocal(stage.Id, stage.Generation, candidate.Payload);
            _window.NoteOccupancy(Occupancy);
            return new PredictionCandidateResult(PredictionCandidateStatus.Staged);
        }

        public PredictionLocalOutcomeResult DiscardCandidateStage(
            PredictionCandidateStage stage,
            PredictionStageDiscardReason reason)
        {
            _ = reason;
            if (_ledger.TryDiscardCandidate(in stage))
            {
                return new PredictionLocalOutcomeResult(PredictionLocalOutcomeStatus.Discarded);
            }

            if (_frozen)
            {
                return new PredictionLocalOutcomeResult(PredictionLocalOutcomeStatus.Frozen);
            }

            return new PredictionLocalOutcomeResult(PredictionLocalOutcomeStatus.StaleStage);
        }

        public PredictionLocalOutcomeResult ObserveLocalPredictionOutcome(
            PredictionCandidateStage stage,
            in LocalPredictionOutcome outcome,
            out AcceptedPredictionCommand acceptedCommand)
        {
            acceptedCommand = default;
            if (!_ledger.TryTakeCandidate(in stage, out CandidateStageRecord record))
            {
                if (_frozen)
                {
                    return new PredictionLocalOutcomeResult(PredictionLocalOutcomeStatus.Frozen);
                }

                return new PredictionLocalOutcomeResult(PredictionLocalOutcomeStatus.StaleStage);
            }

            if (outcome.StageId != record.Id || outcome.Generation != record.Generation)
            {
                Freeze();
                return new PredictionLocalOutcomeResult(PredictionLocalOutcomeStatus.Indeterminate);
            }

            if (_frozen)
            {
                return new PredictionLocalOutcomeResult(PredictionLocalOutcomeStatus.Frozen);
            }

            if (outcome.Kind == PredictionOutcomeKind.Indeterminate)
            {
                Freeze();
                return new PredictionLocalOutcomeResult(PredictionLocalOutcomeStatus.Indeterminate);
            }

            if (outcome.Kind == PredictionOutcomeKind.Aborted)
            {
                return new PredictionLocalOutcomeResult(PredictionLocalOutcomeStatus.Discarded);
            }

            if (outcome.Kind != PredictionOutcomeKind.Committed)
            {
                Freeze();
                return new PredictionLocalOutcomeResult(PredictionLocalOutcomeStatus.Indeterminate);
            }

            // ClientCommandSeq/PredictionKey exist only after a committed local runtime outcome.
            _allocator.Allocate(out ClientCommandSeq commandSeq, out PredictionKey key);
            acceptedCommand = new AcceptedPredictionCommand(commandSeq, key, record.Payload);
            _history.Append(in acceptedCommand);
            _window.NoteOccupancy(Occupancy);
            return new PredictionLocalOutcomeResult(PredictionLocalOutcomeStatus.Assigned);
        }

        public PredictionAuthorityResult StageAuthority(
            in AuthorityPredictionUpdate update,
            in PredictionAuthorityContext context,
            out PredictionAuthorityStage stage,
            out PredictionReconcilePlan reconcilePlan)
        {
            stage = default;
            reconcilePlan = default;
            if (_frozen)
            {
                return new PredictionAuthorityResult(PredictionAuthorityStatus.Frozen);
            }

            if (context.Generation != _generation)
            {
                return new PredictionAuthorityResult(PredictionAuthorityStatus.StaleGeneration);
            }

            if (!GeneratedPredictionAdapter.TryClassify(update.Payload, out PredictionUpdateKind kind))
            {
                return new PredictionAuthorityResult(PredictionAuthorityStatus.Rejected);
            }

            if (update.ConfirmedThroughSeq > _allocator.LastAssigned)
            {
                return new PredictionAuthorityResult(PredictionAuthorityStatus.RequiresResync);
            }

            int replayCount = _history.UnconfirmedCountAfter(update.ConfirmedThroughSeq);
            stage = _ledger.OpenAuthority(_generation, update.ConfirmedThroughSeq, kind, update.Payload);
            reconcilePlan = RuntimePredictionPlanAdapter.CreateReconcile(
                kind,
                stage.Id,
                stage.Generation,
                update.ConfirmedThroughSeq,
                replayCount,
                update.Payload);
            return new PredictionAuthorityResult(PredictionAuthorityStatus.Staged);
        }

        public PredictionAuthorityOutcomeResult DiscardAuthorityStage(
            PredictionAuthorityStage stage,
            PredictionStageDiscardReason reason)
        {
            _ = reason;
            if (_ledger.TryDiscardAuthority(in stage))
            {
                return new PredictionAuthorityOutcomeResult(PredictionAuthorityOutcomeStatus.Discarded);
            }

            if (_frozen)
            {
                return new PredictionAuthorityOutcomeResult(PredictionAuthorityOutcomeStatus.Frozen);
            }

            return new PredictionAuthorityOutcomeResult(PredictionAuthorityOutcomeStatus.StaleStage);
        }

        public PredictionAuthorityOutcomeResult ObserveRuntimeOutcome(
            PredictionAuthorityStage stage,
            in AuthorityRuntimeOutcome outcome)
        {
            if (!_ledger.TryTakeAuthority(in stage, out AuthorityStageRecord record))
            {
                if (_frozen)
                {
                    return new PredictionAuthorityOutcomeResult(PredictionAuthorityOutcomeStatus.Frozen);
                }

                return new PredictionAuthorityOutcomeResult(PredictionAuthorityOutcomeStatus.StaleStage);
            }

            if (outcome.StageId != record.Id || outcome.Generation != record.Generation)
            {
                Freeze();
                return new PredictionAuthorityOutcomeResult(PredictionAuthorityOutcomeStatus.Indeterminate);
            }

            if (_frozen)
            {
                return new PredictionAuthorityOutcomeResult(PredictionAuthorityOutcomeStatus.Frozen);
            }

            if (outcome.Kind == PredictionOutcomeKind.Indeterminate)
            {
                Freeze();
                return new PredictionAuthorityOutcomeResult(PredictionAuthorityOutcomeStatus.Indeterminate);
            }

            if (outcome.Kind == PredictionOutcomeKind.Aborted)
            {
                return new PredictionAuthorityOutcomeResult(PredictionAuthorityOutcomeStatus.Discarded);
            }

            if (outcome.Kind != PredictionOutcomeKind.Committed)
            {
                Freeze();
                return new PredictionAuthorityOutcomeResult(PredictionAuthorityOutcomeStatus.Indeterminate);
            }

            _history.PruneThrough(record.ConfirmedThrough);
            if (record.ConfirmedThrough > _confirmedSeq)
            {
                _confirmedSeq = record.ConfirmedThrough;
            }

            return new PredictionAuthorityOutcomeResult(PredictionAuthorityOutcomeStatus.Applied);
        }

        public PredictionResetResult ResetForNewSession(in PredictionResetRequest request)
        {
            if (request.WindowCapacity < 0)
            {
                return new PredictionResetResult(false);
            }

            _generation = request.Generation;
            _allocator.Reset();
            _history.Clear();
            _ledger.Reset();
            _window.Reset(request.WindowCapacity);
            _confirmedSeq = 0;
            _frozen = false;
            return new PredictionResetResult(true);
        }

        public PredictionSnapshot GetSnapshot()
        {
            return new PredictionSnapshot(
                _generation,
                _allocator.LastAssigned,
                _confirmedSeq,
                _history.Count,
                _window.Capacity,
                _window.HighWatermark,
                _ledger.OpenCandidateCount,
                _ledger.OpenAuthorityCount,
                _frozen);
        }

        private int Occupancy
        {
            get { return _history.Count + _ledger.OpenCandidateCount; }
        }

        private void Freeze()
        {
            _frozen = true;
        }
    }
}

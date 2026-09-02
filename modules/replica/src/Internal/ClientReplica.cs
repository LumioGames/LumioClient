using System;

namespace Lumio.Client.Replica
{
    internal sealed class ClientReplica : IClientReplica
    {
        private readonly IReplicaMapper _mapper;
        private readonly ReplicaMetadataState _metadata = new ReplicaMetadataState();
        private readonly ReplicaStageLedger _ledger = new ReplicaStageLedger();
        private readonly ReplicaGapDetector _gaps = new ReplicaGapDetector();
        private readonly TombstoneEvidence _tombstones = new TombstoneEvidence();
        private readonly ReplicaWorld _world = new ReplicaWorld();
        private ReplicaStageStatus _lastStageStatus;

        public ClientReplica(IReplicaMapper mapper)
        {
            _mapper = mapper;
        }

        public IReplicaWorld World
        {
            get { return _world; }
        }

        public ReplicaStageResult StageAuthority(
            in ReplicaStageRequest request,
            out ReplicaStageHandle stageHandle,
            out ReadOnlyMemory<byte> applyPlan)
        {
            stageHandle = default(ReplicaStageHandle);
            applyPlan = ReadOnlyMemory<byte>.Empty;

            if (_metadata.Frozen)
            {
                return CompleteStage(ReplicaStageStatus.Frozen);
            }

            if (request.Generation != _metadata.Generation)
            {
                return CompleteStage(ReplicaStageStatus.Rejected);
            }

            ReplicaGapClassification classification = _gaps.Classify(in request, _metadata, _tombstones);
            if (classification == ReplicaGapClassification.Duplicate)
            {
                return CompleteStage(ReplicaStageStatus.DuplicateIgnored);
            }

            if (classification != ReplicaGapClassification.Accept)
            {
                return CompleteStage(ReplicaStageStatus.RequiresResync);
            }

            if (!_world.TryValidateAuthority(in request, out _))
            {
                return CompleteStage(ReplicaStageStatus.Rejected);
            }

            ReplicaMappingContext context = new ReplicaMappingContext(
                _metadata.Generation,
                _metadata.Baseline,
                _metadata.Revision);
            ReplicaMappingResult mapped = _mapper.Map(in request, in context, out applyPlan);
            if (!mapped.Succeeded || applyPlan.IsEmpty)
            {
                applyPlan = ReadOnlyMemory<byte>.Empty;
                return CompleteStage(ReplicaStageStatus.Rejected);
            }

            stageHandle = _ledger.Add(in request);
            return CompleteStage(ReplicaStageStatus.Staged);
        }

        public ReplicaOutcomeStatus DiscardStage(
            ReplicaStageHandle stageHandle,
            ReplicaStageDiscardReason reason)
        {
            _ = reason;
            if (!_ledger.TryRemove(stageHandle, out _))
            {
                return ReplicaOutcomeStatus.Stale;
            }

            return ReplicaOutcomeStatus.Discarded;
        }

        public ReplicaOutcomeStatus ObserveRuntimeOutcome(
            ReplicaStageHandle stageHandle,
            in ReplicaRuntimeOutcome outcome,
            out ReplicaCommittedMetadata committedMetadata)
        {
            committedMetadata = _metadata.ToCommittedMetadata();
            if (!_ledger.TryGet(stageHandle, out ReplicaStageRequest staged))
            {
                return ReplicaOutcomeStatus.Stale;
            }

            if (_metadata.Frozen)
            {
                return ReplicaOutcomeStatus.Frozen;
            }

            _ledger.TryRemove(stageHandle, out _);
            if (outcome.Indeterminate)
            {
                _metadata.Freeze(outcome.Evidence);
                committedMetadata = _metadata.ToCommittedMetadata();
                return ReplicaOutcomeStatus.Frozen;
            }

            if (!outcome.Committed)
            {
                return ReplicaOutcomeStatus.Aborted;
            }

            _metadata.ApplyCommitted(in staged);
            _world.ApplyCommitted(in staged);
            if (staged.Kind == ReplicaUpdateKind.FullSnapshot)
            {
                _tombstones.Replace(staged.TombstoneEntityIds);
            }
            else
            {
                _tombstones.Add(staged.TombstoneEntityIds);
            }

            committedMetadata = _metadata.ToCommittedMetadata();
            return ReplicaOutcomeStatus.Observed;
        }

        public ReplicaResetResult ResetForNewSession(in ReplicaResetRequest request)
        {
            _ledger.Clear();
            _tombstones.Clear();
            _world.Reset();
            _metadata.Reset(request.Generation);
            _lastStageStatus = ReplicaStageStatus.None;
            return new ReplicaResetResult(true);
        }

        public bool TryObserveConnectionSuperseded(ReadOnlyMemory<byte> utf8, out ReplicaConnectionSuperseded notice)
        {
            if (!GameplayCodec.TryDecodeConnectionSuperseded(utf8, out notice, out _))
            {
                notice = default(ReplicaConnectionSuperseded);
                return false;
            }

            _world.ObserveSuperseded(in notice);
            return true;
        }

        public ReplicaSnapshot GetSnapshot()
        {
            return new ReplicaSnapshot(
                _metadata.ToCommittedMetadata(),
                _ledger.OpenCount,
                _lastStageStatus,
                _metadata.FreezeEvidence);
        }

        private ReplicaStageResult CompleteStage(ReplicaStageStatus status)
        {
            _lastStageStatus = status;
            return new ReplicaStageResult(status);
        }
    }
}

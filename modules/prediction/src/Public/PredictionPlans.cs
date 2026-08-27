using System;

namespace Lumio.Client.Prediction
{
    public enum PredictionOutcomeKind
    {
        None = 0,
        Committed = 1,
        Aborted = 2,
        Indeterminate = 3
    }

    public readonly struct LocalPredictionPlan
    {
        public LocalPredictionPlan(ReadOnlyMemory<byte> opaqueBytes)
        {
            OpaqueBytes = opaqueBytes;
        }

        public ReadOnlyMemory<byte> OpaqueBytes { get; }
    }

    public readonly struct PredictionReconcilePlan
    {
        public PredictionReconcilePlan(ReadOnlyMemory<byte> opaqueBytes)
        {
            OpaqueBytes = opaqueBytes;
        }

        public ReadOnlyMemory<byte> OpaqueBytes { get; }
    }

    public readonly struct LocalPredictionOutcome
    {
        public LocalPredictionOutcome(PredictionOutcomeKind kind, ulong stageId, ulong generation)
        {
            Kind = kind;
            StageId = stageId;
            Generation = generation;
        }

        public PredictionOutcomeKind Kind { get; }

        public ulong StageId { get; }

        public ulong Generation { get; }
    }

    public readonly struct AuthorityRuntimeOutcome
    {
        public AuthorityRuntimeOutcome(PredictionOutcomeKind kind, ulong stageId, ulong generation)
        {
            Kind = kind;
            StageId = stageId;
            Generation = generation;
        }

        public PredictionOutcomeKind Kind { get; }

        public ulong StageId { get; }

        public ulong Generation { get; }
    }
}

using System;

namespace Lumio.Client.Prediction
{
    public readonly struct PredictionCandidateStage : IEquatable<PredictionCandidateStage>
    {
        public PredictionCandidateStage(ulong id, ulong generation)
        {
            Id = id;
            Generation = generation;
        }

        public ulong Id { get; }

        public ulong Generation { get; }

        public bool Equals(PredictionCandidateStage other)
        {
            return Id == other.Id && Generation == other.Generation;
        }

        public override bool Equals(object obj)
        {
            return obj is PredictionCandidateStage other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode() ^ Generation.GetHashCode();
        }

        public static bool operator ==(PredictionCandidateStage left, PredictionCandidateStage right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PredictionCandidateStage left, PredictionCandidateStage right)
        {
            return !left.Equals(right);
        }
    }
}

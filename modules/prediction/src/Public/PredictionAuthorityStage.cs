using System;

namespace Lumio.Client.Prediction
{
    public readonly struct PredictionAuthorityStage : IEquatable<PredictionAuthorityStage>
    {
        public PredictionAuthorityStage(ulong id, ulong generation)
        {
            Id = id;
            Generation = generation;
        }

        public ulong Id { get; }

        public ulong Generation { get; }

        public bool Equals(PredictionAuthorityStage other)
        {
            return Id == other.Id && Generation == other.Generation;
        }

        public override bool Equals(object obj)
        {
            return obj is PredictionAuthorityStage other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode() ^ Generation.GetHashCode();
        }

        public static bool operator ==(PredictionAuthorityStage left, PredictionAuthorityStage right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PredictionAuthorityStage left, PredictionAuthorityStage right)
        {
            return !left.Equals(right);
        }
    }
}

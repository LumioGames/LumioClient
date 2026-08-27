using System;

namespace Lumio.Client.Prediction
{
    public readonly struct PredictionKey : IEquatable<PredictionKey>
    {
        public PredictionKey(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }

        public bool Equals(PredictionKey other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is PredictionKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(PredictionKey left, PredictionKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PredictionKey left, PredictionKey right)
        {
            return !left.Equals(right);
        }
    }
}

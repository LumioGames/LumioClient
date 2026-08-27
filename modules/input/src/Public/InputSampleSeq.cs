using System;

namespace Lumio.Client.Input
{
    public readonly struct InputSampleSeq : IEquatable<InputSampleSeq>
    {
        public InputSampleSeq(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }

        public bool Equals(InputSampleSeq other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is InputSampleSeq other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(InputSampleSeq left, InputSampleSeq right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(InputSampleSeq left, InputSampleSeq right)
        {
            return !left.Equals(right);
        }
    }
}

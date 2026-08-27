using System;

namespace Lumio.Client.Prediction
{
    public readonly struct ClientCommandSeq : IEquatable<ClientCommandSeq>
    {
        public ClientCommandSeq(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }

        public bool Equals(ClientCommandSeq other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is ClientCommandSeq other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(ClientCommandSeq left, ClientCommandSeq right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ClientCommandSeq left, ClientCommandSeq right)
        {
            return !left.Equals(right);
        }
    }
}

using System;

namespace Lumio.Client.Connection
{
    public readonly struct ConnectionGeneration : IEquatable<ConnectionGeneration>
    {
        public ConnectionGeneration(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }

        public bool Equals(ConnectionGeneration other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is ConnectionGeneration other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(ConnectionGeneration left, ConnectionGeneration right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ConnectionGeneration left, ConnectionGeneration right)
        {
            return !left.Equals(right);
        }
    }
}

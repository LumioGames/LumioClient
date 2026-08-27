using System;

namespace Lumio.Client.Handshake
{
    public readonly struct HandshakeAttemptId : IEquatable<HandshakeAttemptId>
    {
        public HandshakeAttemptId(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }

        public bool Equals(HandshakeAttemptId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is HandshakeAttemptId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(HandshakeAttemptId left, HandshakeAttemptId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HandshakeAttemptId left, HandshakeAttemptId right)
        {
            return !left.Equals(right);
        }
    }
}

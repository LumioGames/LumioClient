using System;

namespace Lumio.Client.Persistence
{
    public readonly struct VerifiedArtifactReadResult
    {
        public VerifiedArtifactReadResult(
            bool succeeded,
            bool verified,
            ReadOnlyMemory<byte> payload,
            ulong generation)
        {
            if (!verified)
            {
                Succeeded = false;
                Verified = false;
                Payload = ReadOnlyMemory<byte>.Empty;
                Generation = generation;
                return;
            }

            Succeeded = succeeded;
            Verified = succeeded;
            Payload = succeeded ? payload : ReadOnlyMemory<byte>.Empty;
            Generation = generation;
        }

        public bool Succeeded { get; }

        public bool Verified { get; }

        public ReadOnlyMemory<byte> Payload { get; }

        public ulong Generation { get; }

        public static VerifiedArtifactReadResult Success(ReadOnlyMemory<byte> payload, ulong generation)
        {
            return new VerifiedArtifactReadResult(true, true, payload, generation);
        }

        public static VerifiedArtifactReadResult NotVerified(ulong generation)
        {
            return new VerifiedArtifactReadResult(false, false, ReadOnlyMemory<byte>.Empty, generation);
        }
    }
}

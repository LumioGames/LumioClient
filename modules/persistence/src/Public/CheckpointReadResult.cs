using System;

namespace Lumio.Client.Persistence
{
    public readonly struct CheckpointReadResult
    {
        public CheckpointReadResult(bool succeeded, ReadOnlyMemory<byte> payload, ulong generation)
        {
            Succeeded = succeeded;
            Payload = succeeded ? payload : ReadOnlyMemory<byte>.Empty;
            Generation = generation;
        }

        public bool Succeeded { get; }

        public ReadOnlyMemory<byte> Payload { get; }

        public ulong Generation { get; }

        public static CheckpointReadResult Success(ReadOnlyMemory<byte> payload, ulong generation)
        {
            return new CheckpointReadResult(true, payload, generation);
        }

        public static CheckpointReadResult NotFound(ulong generation)
        {
            return new CheckpointReadResult(false, ReadOnlyMemory<byte>.Empty, generation);
        }
    }
}

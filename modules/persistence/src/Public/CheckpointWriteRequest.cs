using System;

namespace Lumio.Client.Persistence
{
    public readonly struct CheckpointWriteRequest
    {
        public CheckpointWriteRequest(string key, ReadOnlyMemory<byte> payload, ulong generation)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            Key = key;
            Payload = payload;
            Generation = generation;
        }

        public string Key { get; }

        public ReadOnlyMemory<byte> Payload { get; }

        public ulong Generation { get; }
    }
}

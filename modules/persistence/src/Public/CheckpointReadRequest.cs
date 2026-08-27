using System;

namespace Lumio.Client.Persistence
{
    public readonly struct CheckpointReadRequest
    {
        public CheckpointReadRequest(string key, ulong generation)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            Key = key;
            Generation = generation;
        }

        public string Key { get; }

        public ulong Generation { get; }
    }
}

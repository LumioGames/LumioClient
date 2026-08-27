using System;

namespace Lumio.Client.Observability
{
    public readonly struct ClientEventMemorySnapshot
    {
        public ClientEventMemorySnapshot(
            ReadOnlyMemory<ClientEventRecord> records,
            int capacity,
            int droppedCount,
            bool closed)
        {
            if (records.Length == 0)
            {
                Records = ReadOnlyMemory<ClientEventRecord>.Empty;
            }
            else
            {
                var copy = new ClientEventRecord[records.Length];
                records.Span.CopyTo(copy);
                Records = copy;
            }

            Capacity = capacity;
            DroppedCount = droppedCount;
            Closed = closed;
        }

        public ReadOnlyMemory<ClientEventRecord> Records { get; }

        public int Capacity { get; }

        public int DroppedCount { get; }

        public bool Closed { get; }
    }
}

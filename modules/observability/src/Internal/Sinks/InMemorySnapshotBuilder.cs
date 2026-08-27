using System;

namespace Lumio.Client.Observability
{
    internal static class InMemorySnapshotBuilder
    {
        public static ClientEventMemorySnapshot Build(InMemoryEventBuffer buffer)
        {
            if (buffer is null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.CopySnapshot(out ClientEventRecord[] records, out int droppedCount, out bool closed);
            return new ClientEventMemorySnapshot(records, buffer.Capacity, droppedCount, closed);
        }
    }
}

using System;

namespace Lumio.Client.Replica
{
    internal sealed class GeneratedReplicaAdapter
    {
        private readonly bool _enabled = true;

        public bool TryValidate(ReplicaUpdateKind kind, ReadOnlyMemory<byte> update)
        {
            if (!_enabled || update.IsEmpty)
            {
                return false;
            }

            byte marker = update.Span[0];
            if (kind == ReplicaUpdateKind.FullSnapshot)
            {
                return marker == 1;
            }

            if (kind == ReplicaUpdateKind.Delta)
            {
                return marker == 2;
            }

            return false;
        }
    }
}

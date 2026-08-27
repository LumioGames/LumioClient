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

            return kind == ReplicaUpdateKind.FullSnapshot || kind == ReplicaUpdateKind.Delta;
        }
    }
}

using System;
using System.Collections.Generic;

namespace Lumio.Client.Replica
{
    internal sealed class TombstoneEvidence
    {
        private readonly HashSet<ulong> _ids = new HashSet<ulong>();

        public bool Conflicts(ReadOnlyMemory<ulong> touchedEntityIds)
        {
            ReadOnlySpan<ulong> span = touchedEntityIds.Span;
            for (int i = 0; i < span.Length; i++)
            {
                if (_ids.Contains(span[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public void Add(ReadOnlyMemory<ulong> tombstoneEntityIds)
        {
            ReadOnlySpan<ulong> span = tombstoneEntityIds.Span;
            for (int i = 0; i < span.Length; i++)
            {
                _ids.Add(span[i]);
            }
        }

        public void Replace(ReadOnlyMemory<ulong> tombstoneEntityIds)
        {
            _ids.Clear();
            Add(tombstoneEntityIds);
        }

        public void Clear()
        {
            _ids.Clear();
        }
    }
}

using System.Collections.Generic;

namespace Lumio.Client.Connection
{
    internal sealed class ReplayWindow
    {
        private readonly HashSet<ulong> _seen = new HashSet<ulong>();
        private ulong _highest;

        public bool Accept(ulong sequence)
        {
            if (sequence + 1 < _highest && _highest - sequence > 64)
            {
                return false;
            }

            if (!_seen.Add(sequence))
            {
                return false;
            }

            if (sequence > _highest)
            {
                _highest = sequence;
            }

            return true;
        }
    }
}

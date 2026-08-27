using System.Collections.Generic;
using Lumio.Client.Connection;

namespace Lumio.Client.Session
{
    internal sealed class SessionEventInbox
    {
        private readonly List<SessionEvent> _items = new List<SessionEvent>();
        private ulong _sequence;

        public void Enqueue(SessionEventPriority priority, ulong generation, ConnectionEvent connection)
        {
            _sequence++;
            _items.Add(new SessionEvent(priority, generation, _sequence, connection));
        }

        public bool TryDequeue(out SessionEvent evt)
        {
            if (_items.Count == 0)
            {
                evt = default;
                return false;
            }

            int best = 0;
            for (int i = 1; i < _items.Count; i++)
            {
                SessionEvent candidate = _items[i];
                SessionEvent current = _items[best];
                if (candidate.Priority < current.Priority
                    || (candidate.Priority == current.Priority && candidate.Sequence < current.Sequence))
                {
                    best = i;
                }
            }

            evt = _items[best];
            _items.RemoveAt(best);
            return true;
        }
    }
}

using System.Collections.Generic;

namespace Lumio.Client.Session
{
    internal sealed class SessionResourceLedger
    {
        private readonly List<string> _acquired = new List<string>();
        private readonly List<string> _released = new List<string>();

        public int Count
        {
            get { return _acquired.Count; }
        }

        public string[] ReleaseOrder
        {
            get { return _released.ToArray(); }
        }

        public void Acquire(string name)
        {
            _acquired.Add(name);
        }

        public void ReleaseInOrder(IReadOnlyList<string> order)
        {
            for (int i = 0; i < order.Count; i++)
            {
                string name = order[i];
                if (_acquired.Remove(name))
                {
                    _released.Add(name);
                }
            }
        }

        public void ReleaseRemaining()
        {
            for (int i = _acquired.Count - 1; i >= 0; i--)
            {
                _released.Add(_acquired[i]);
                _acquired.RemoveAt(i);
            }
        }
    }
}

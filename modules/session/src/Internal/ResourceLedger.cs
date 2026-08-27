using System.Collections.Generic;

namespace Lumio.Client.Session
{
    internal sealed class ResourceLedger
    {
        private readonly Stack<string> _items = new Stack<string>();

        public int Count
        {
            get { return _items.Count; }
        }

        public void Acquire(string name)
        {
            _items.Push(name);
        }

        public void ReleaseAll()
        {
            _items.Clear();
        }
    }
}

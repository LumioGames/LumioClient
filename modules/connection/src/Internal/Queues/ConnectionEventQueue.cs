using System;

namespace Lumio.Client.Connection
{
    internal sealed class ConnectionEventQueue
    {
        private readonly ConnectionEvent[] _items;
        private int _head;
        private int _count;

        public ConnectionEventQueue(int capacity)
        {
            _items = new ConnectionEvent[Math.Max(capacity, 1)];
        }

        public bool TryEnqueue(in ConnectionEvent evt)
        {
            if (_count == _items.Length)
            {
                return false;
            }

            _items[(_head + _count) % _items.Length] = evt;
            _count++;
            return true;
        }

        public int Count
        {
            get { return _count; }
        }

        public int Drain(Span<ConnectionEvent> destination)
        {
            int n = Math.Min(destination.Length, _count);
            for (int i = 0; i < n; i++)
            {
                destination[i] = _items[(_head + i) % _items.Length];
            }

            _head = (_head + n) % _items.Length;
            _count -= n;
            return n;
        }
    }
}

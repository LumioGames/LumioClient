using System;

namespace Lumio.Client.Connection
{
    internal sealed class ConnectionSendQueue
    {
        private readonly EncodedFrame[] _items;
        private int _count;

        public ConnectionSendQueue(int capacity)
        {
            _items = new EncodedFrame[Math.Max(capacity, 1)];
        }

        public bool TryEnqueue(in EncodedFrame frame)
        {
            if (_count == _items.Length)
            {
                return false;
            }

            _items[_count] = frame;
            _count++;
            return true;
        }
    }
}

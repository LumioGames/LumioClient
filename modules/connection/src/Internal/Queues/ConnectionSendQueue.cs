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

        public int Count
        {
            get { return _count; }
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

        public bool TryPeek(out EncodedFrame frame)
        {
            if (_count == 0)
            {
                frame = default(EncodedFrame);
                return false;
            }

            frame = _items[0];
            return true;
        }

        public bool TryDequeue(out EncodedFrame frame)
        {
            if (!TryPeek(out frame))
            {
                return false;
            }

            _count--;
            if (_count > 0)
            {
                Array.Copy(_items, 1, _items, 0, _count);
            }

            _items[_count] = default(EncodedFrame);
            return true;
        }
    }
}

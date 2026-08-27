using System;

namespace Lumio.Client.Input
{
    internal sealed class InputSampleQueue
    {
        private readonly SequencedInputSample[] _items;
        private int _head;
        private int _count;

        public InputSampleQueue(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _items = new SequencedInputSample[capacity];
        }

        public int Count
        {
            get { return _count; }
        }

        public int Capacity
        {
            get { return _items.Length; }
        }

        public bool TryEnqueue(in SequencedInputSample sample)
        {
            if (_count == _items.Length)
            {
                return false;
            }

            int index = (_head + _count) % _items.Length;
            _items[index] = sample;
            _count++;
            return true;
        }

        public SequencedInputSample[] Drain()
        {
            var copy = new SequencedInputSample[_count];
            for (int i = 0; i < _count; i++)
            {
                copy[i] = _items[(_head + i) % _items.Length];
            }

            _head = 0;
            _count = 0;
            return copy;
        }
    }
}

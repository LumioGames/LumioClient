using System.Collections.Generic;

namespace Lumio.Client.Connection
{
    internal sealed class DeterministicDelayQueue
    {
        private readonly Queue<EncodedFrame> _held = new Queue<EncodedFrame>();

        public void Hold(in EncodedFrame frame)
        {
            _held.Enqueue(frame);
        }

        public bool TryRelease(out EncodedFrame frame)
        {
            if (_held.Count == 0)
            {
                frame = default;
                return false;
            }

            frame = _held.Dequeue();
            return true;
        }
    }
}

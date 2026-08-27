using System.Collections.Generic;

namespace Lumio.Client.Handshake
{
    internal sealed class CapabilityCompletionQueue
    {
        private readonly Queue<PlatformCapabilityResult> _items = new Queue<PlatformCapabilityResult>();

        public void Enqueue(in PlatformCapabilityResult result)
        {
            _items.Enqueue(result);
        }

        public bool TryDequeue(out PlatformCapabilityResult result)
        {
            if (_items.Count == 0)
            {
                result = default;
                return false;
            }

            result = _items.Dequeue();
            return true;
        }
    }
}

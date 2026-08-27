namespace Lumio.Client.Session
{
    internal sealed class ReconnectOrchestrator
    {
        private readonly bool _owned = true;

        public ulong NextGeneration(SessionGenerationAllocator allocator)
        {
            if (!_owned)
            {
                return 0;
            }

            return allocator.Next();
        }
    }
}

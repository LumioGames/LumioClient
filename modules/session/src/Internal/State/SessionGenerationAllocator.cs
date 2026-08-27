namespace Lumio.Client.Session
{
    internal sealed class SessionGenerationAllocator
    {
        private ulong _current;

        public ulong Current
        {
            get { return _current; }
        }

        public ulong Next()
        {
            _current++;
            return _current;
        }

        public void Seed(ulong generation)
        {
            _current = generation;
        }
    }
}

namespace Lumio.Client.Input
{
    internal sealed class InputSampleSequenceAllocator
    {
        private ulong _next = 1;
        private ulong _lastAccepted;

        public InputSampleSeq LastAccepted
        {
            get { return new InputSampleSeq(_lastAccepted); }
        }

        public InputSampleSeq AllocateAccepted()
        {
            ulong value = _next;
            _next++;
            _lastAccepted = value;
            return new InputSampleSeq(value);
        }
    }
}

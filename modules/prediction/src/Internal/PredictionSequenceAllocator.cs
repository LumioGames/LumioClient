namespace Lumio.Client.Prediction
{
    internal sealed class PredictionSequenceAllocator
    {
        private ulong _next = 1;
        private ulong _lastAssigned;

        public ulong LastAssigned
        {
            get { return _lastAssigned; }
        }

        public void Allocate(out ClientCommandSeq commandSeq, out PredictionKey key)
        {
            ulong value = _next;
            _next++;
            _lastAssigned = value;
            commandSeq = new ClientCommandSeq(value);
            key = new PredictionKey(value);
        }

        public void Reset()
        {
            _next = 1;
            _lastAssigned = 0;
        }
    }
}

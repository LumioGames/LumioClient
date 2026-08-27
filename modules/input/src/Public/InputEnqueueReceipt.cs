namespace Lumio.Client.Input
{
    public readonly struct InputEnqueueReceipt
    {
        public InputEnqueueReceipt(bool accepted, InputSampleSeq sequence, InputSampleSeq lastAcceptedSequence)
        {
            Accepted = accepted;
            Sequence = sequence;
            LastAcceptedSequence = lastAcceptedSequence;
        }

        public bool Accepted { get; }

        public InputSampleSeq Sequence { get; }

        public InputSampleSeq LastAcceptedSequence { get; }
    }
}

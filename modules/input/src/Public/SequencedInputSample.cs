namespace Lumio.Client.Input
{
    public readonly struct SequencedInputSample
    {
        public SequencedInputSample(InputSampleSeq sequence, RawInputSample sample)
        {
            Sequence = sequence;
            Sample = sample;
        }

        public InputSampleSeq Sequence { get; }

        public RawInputSample Sample { get; }
    }
}

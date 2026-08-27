namespace Lumio.Client.Input
{
    public interface IInputSampleIngress
    {
        InputEnqueueReceipt TryEnqueue(in RawInputSample sample);

        SequencedInputSample[] DrainAccepted();
    }
}

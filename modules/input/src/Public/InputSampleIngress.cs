using System;

namespace Lumio.Client.Input
{
    public sealed class InputSampleIngress : IInputSampleIngress
    {
        private readonly object _gate = new object();
        private readonly InputSampleQueue _queue;
        private readonly InputSampleSequenceAllocator _sequences = new InputSampleSequenceAllocator();

        public InputSampleIngress(int capacity)
        {
            _queue = new InputSampleQueue(capacity);
        }

        public InputEnqueueReceipt TryEnqueue(in RawInputSample sample)
        {
            lock (_gate)
            {
                if (_queue.Count == _queue.Capacity)
                {
                    return new InputEnqueueReceipt(false, default(InputSampleSeq), _sequences.LastAccepted);
                }

                InputSampleSeq sequence = _sequences.AllocateAccepted();
                if (!_queue.TryEnqueue(new SequencedInputSample(sequence, sample)))
                {
                    throw new InvalidOperationException("queue accepted then rejected");
                }

                return new InputEnqueueReceipt(true, sequence, sequence);
            }
        }

        public SequencedInputSample[] DrainAccepted()
        {
            lock (_gate)
            {
                return _queue.Drain();
            }
        }
    }
}

namespace Lumio.Client.Observability
{
    public readonly struct ClientEventPipelineSnapshot
    {
        public ClientEventPipelineSnapshot(
            int queueDepth,
            int dropCount,
            int highWatermark,
            ulong lastProducerSequence,
            bool sinkFaulted,
            bool closed)
        {
            QueueDepth = queueDepth;
            DropCount = dropCount;
            HighWatermark = highWatermark;
            LastProducerSequence = lastProducerSequence;
            SinkFaulted = sinkFaulted;
            Closed = closed;
        }

        public int QueueDepth { get; }

        public int DropCount { get; }

        public int HighWatermark { get; }

        public ulong LastProducerSequence { get; }

        public bool SinkFaulted { get; }

        public bool Closed { get; }
    }
}

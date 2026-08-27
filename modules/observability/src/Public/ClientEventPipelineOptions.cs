using System;

namespace Lumio.Client.Observability
{
    public readonly struct ClientEventPipelineOptions
    {
        public ClientEventPipelineOptions(int capacity, int batchSize, TimeSpan sinkTimeout)
        {
            Capacity = capacity;
            BatchSize = batchSize;
            SinkTimeout = sinkTimeout;
        }

        public int Capacity { get; }

        public int BatchSize { get; }

        public TimeSpan SinkTimeout { get; }
    }
}

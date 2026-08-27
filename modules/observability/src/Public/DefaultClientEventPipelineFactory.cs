using System;

namespace Lumio.Client.Observability
{
    public sealed class DefaultClientEventPipelineFactory : IClientEventPipelineFactory
    {
        public ClientEventPipelineCreateResult Create(
            in ClientEventPipelineOptions options,
            IClientEventSink sink,
            out IClientEventWriter writer)
        {
            if (sink is null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            if (options.Capacity <= 0 || options.BatchSize <= 0)
            {
                writer = BoundedEventWriter.CreateRejected();
                return new ClientEventPipelineCreateResult(false);
            }

            writer = BoundedEventWriter.Start(in options, sink);
            return new ClientEventPipelineCreateResult(true);
        }
    }
}

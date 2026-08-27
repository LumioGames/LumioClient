using System;

namespace Lumio.Client.Observability
{
    public interface IClientEventPipelineFactory
    {
        ClientEventPipelineCreateResult Create(
            in ClientEventPipelineOptions options,
            IClientEventSink sink,
            out IClientEventWriter writer);
    }

    public sealed class ClientEventPipelineFactory : IClientEventPipelineFactory
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

            writer = new StubClientEventWriter();
            bool succeeded = options.Capacity > 0 && options.BatchSize > 0;
            return new ClientEventPipelineCreateResult(succeeded);
        }
    }

    internal sealed class StubClientEventWriter : IClientEventWriter
    {
        public ClientEventWriteResult TryWrite(in ClientEventRecord record)
        {
            if (!IsAcceptedSchemaClass(record.SchemaClass))
            {
                return new ClientEventWriteResult(ClientEventWriteOutcome.Rejected);
            }

            return new ClientEventWriteResult(ClientEventWriteOutcome.Accepted);
        }

        public ClientEventPipelineSnapshot GetSnapshot()
        {
            return new ClientEventPipelineSnapshot(0, 0, 0, 0UL, false, false);
        }

        private static bool IsAcceptedSchemaClass(EventSchemaClass schemaClass)
        {
            return schemaClass == EventSchemaClass.Critical
                || schemaClass == EventSchemaClass.Durable
                || schemaClass == EventSchemaClass.Droppable;
        }
    }
}

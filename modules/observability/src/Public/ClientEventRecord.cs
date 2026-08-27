using System;

namespace Lumio.Client.Observability
{
    public readonly struct ClientEventRecord
    {
        public ClientEventRecord(EventSchemaClass schemaClass, ReadOnlyMemory<byte> payload)
            : this(schemaClass, payload, null)
        {
        }

        public ClientEventRecord(EventSchemaClass schemaClass, ReadOnlyMemory<byte> payload, ulong? producerSequence)
        {
            SchemaClass = schemaClass;
            Payload = payload;
            ProducerSequence = producerSequence;
        }

        public EventSchemaClass SchemaClass { get; }

        public ReadOnlyMemory<byte> Payload { get; }

        public ulong? ProducerSequence { get; }
    }
}

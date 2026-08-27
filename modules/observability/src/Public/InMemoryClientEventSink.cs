using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lumio.Client.Observability
{
    public sealed class InMemoryClientEventSink : IClientEventSink, IClientEventMemorySnapshotSource
    {
        private readonly InMemoryEventBuffer _buffer;

        public InMemoryClientEventSink(int capacity)
        {
            _buffer = new InMemoryEventBuffer(capacity);
        }

        public ValueTask<ClientEventSinkResult> WriteBatchAsync(
            ReadOnlyMemory<ClientEventRecord> records,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<ClientEventSinkResult>(_buffer.TryAppend(records.Span));
        }

        public ClientEventMemorySnapshot Capture()
        {
            return InMemorySnapshotBuilder.Build(_buffer);
        }

        public void Close()
        {
            _buffer.Close();
        }
    }
}

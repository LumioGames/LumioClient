using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lumio.Client.Observability
{
    public interface IClientEventSink
    {
        ValueTask<ClientEventSinkResult> WriteBatchAsync(
            ReadOnlyMemory<ClientEventRecord> records,
            CancellationToken cancellationToken);
    }
}

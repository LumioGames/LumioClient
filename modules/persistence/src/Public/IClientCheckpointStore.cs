using System.Threading;
using System.Threading.Tasks;

namespace Lumio.Client.Persistence
{
    public interface IClientCheckpointStore
    {
        ValueTask<CheckpointReadResult> ReadLatestAsync(
            in CheckpointReadRequest request,
            CancellationToken cancellationToken);

        ValueTask<CheckpointWriteResult> WriteAsync(
            in CheckpointWriteRequest request,
            CancellationToken cancellationToken);

        PersistenceSnapshot GetSnapshot();
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Lumio.Client.Persistence
{
    internal sealed class MemoryClientCheckpointStore : IClientCheckpointStore
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, MemoryCheckpoint> _checkpoints = new Dictionary<string, MemoryCheckpoint>(StringComparer.Ordinal);
        private ulong _latestCommittedGeneration;

        public ValueTask<CheckpointReadResult> ReadLatestAsync(
            in CheckpointReadRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            MemoryCheckpoint checkpoint;
            lock (_gate)
            {
                if (request.Key is null || !_checkpoints.TryGetValue(request.Key, out checkpoint))
                {
                    return new ValueTask<CheckpointReadResult>(CheckpointReadResult.NotFound(request.Generation));
                }
            }

            return new ValueTask<CheckpointReadResult>(
                CheckpointReadResult.Success(Copy(checkpoint.Payload), request.Generation));
        }

        public ValueTask<CheckpointWriteResult> WriteAsync(
            in CheckpointWriteRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request.Key is null)
            {
                return new ValueTask<CheckpointWriteResult>(CheckpointWriteResult.Failed(request.Generation));
            }

            var stored = new MemoryCheckpoint(Copy(request.Payload), request.Generation);
            lock (_gate)
            {
                _checkpoints[request.Key] = stored;
                if (request.Generation > _latestCommittedGeneration)
                {
                    _latestCommittedGeneration = request.Generation;
                }
            }

            return new ValueTask<CheckpointWriteResult>(CheckpointWriteResult.Success(request.Generation));
        }

        public PersistenceSnapshot GetSnapshot()
        {
            lock (_gate)
            {
                return new PersistenceSnapshot(0, _latestCommittedGeneration);
            }
        }

        private static ReadOnlyMemory<byte> Copy(ReadOnlyMemory<byte> source)
        {
            if (source.IsEmpty)
            {
                return ReadOnlyMemory<byte>.Empty;
            }

            return source.ToArray();
        }

        private readonly struct MemoryCheckpoint
        {
            public MemoryCheckpoint(ReadOnlyMemory<byte> payload, ulong generation)
            {
                Payload = payload;
                Generation = generation;
            }

            public ReadOnlyMemory<byte> Payload { get; }

            public ulong Generation { get; }
        }
    }
}

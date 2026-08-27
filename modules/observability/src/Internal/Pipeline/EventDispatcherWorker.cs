using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Lumio.Client.Observability
{
    internal sealed class EventDispatcherWorker : IDisposable
    {
        private readonly ChannelReader<ClientEventRecord> _reader;
        private readonly IClientEventSink _sink;
        private readonly int _batchSize;
        private readonly TimeSpan _sinkTimeout;
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private readonly object _retainGate = new object();
        private readonly Task _run;
        private ClientEventRecord[] _retained = Array.Empty<ClientEventRecord>();
        private int _retainedCount;
        private int _inFlight;
        private int _sinkFaulted;
        private int _disposed;
        private ReadOnlyMemory<byte> _lastFailureEvidence;

        public EventDispatcherWorker(
            ChannelReader<ClientEventRecord> reader,
            IClientEventSink sink,
            int batchSize,
            TimeSpan sinkTimeout)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _batchSize = batchSize;
            _sinkTimeout = sinkTimeout;
            _run = Task.Run((Func<Task>)RunAsync);
        }

        public bool SinkFaulted
        {
            get { return Volatile.Read(ref _sinkFaulted) != 0; }
        }

        public int PendingCount
        {
            get
            {
                int retained;
                lock (_retainGate)
                {
                    retained = _retainedCount;
                }

                return retained + Volatile.Read(ref _inFlight);
            }
        }

        public ReadOnlyMemory<byte> LastFailureEvidence
        {
            get { return _lastFailureEvidence; }
        }

        public void StopAndWait(TimeSpan timeout)
        {
            try
            {
                if (!_stop.IsCancellationRequested)
                {
                    _stop.Cancel();
                }
            }
            catch (ObjectDisposedException)
            {
            }

            TimeSpan wait = timeout > TimeSpan.Zero ? timeout : TimeSpan.FromSeconds(1);
            _run.Wait(wait);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            StopAndWait(_sinkTimeout);
            _stop.Dispose();
        }

        private async Task RunAsync()
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    bool readable;
                    try
                    {
                        readable = await _reader.WaitToReadAsync(_stop.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    if (!readable)
                    {
                        break;
                    }

                    ClientEventRecord[] batch = DequeueBatch(_batchSize);
                    if (batch.Length == 0)
                    {
                        continue;
                    }

                    bool delivered = await DeliverAsync(batch).ConfigureAwait(false);
                    if (!delivered)
                    {
                        await WaitForStopAsync().ConfigureAwait(false);
                        return;
                    }
                }
            }
            finally
            {
                if (!SinkFaulted)
                {
                    ClientEventRecord[] leftover = DequeueBatch(int.MaxValue);
                    if (leftover.Length > 0)
                    {
                        await DeliverAsync(leftover).ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task<bool> DeliverAsync(ClientEventRecord[] batch)
        {
            Interlocked.Exchange(ref _inFlight, batch.Length);
            try
            {
                using (CancellationTokenSource timeout = new CancellationTokenSource())
                {
                    if (_sinkTimeout > TimeSpan.Zero)
                    {
                        timeout.CancelAfter(_sinkTimeout);
                    }

                    ClientEventSinkResult result = await _sink.WriteBatchAsync(batch, timeout.Token).ConfigureAwait(false);
                    if (result.Succeeded)
                    {
                        return true;
                    }

                    Retain(batch);
                    MarkFaulted(FailureEvidenceEncoder.EncodeSinkFault("SinkResult", batch.Length));
                    return false;
                }
            }
            catch (Exception ex)
            {
                Retain(batch);
                string name = ex is OperationCanceledException
                    ? "OperationCanceledException"
                    : ex.GetType().Name;
                MarkFaulted(FailureEvidenceEncoder.EncodeSinkFault(name, batch.Length));
                return false;
            }
            finally
            {
                Interlocked.Exchange(ref _inFlight, 0);
            }
        }

        private ClientEventRecord[] DequeueBatch(int max)
        {
            if (max <= 0)
            {
                return Array.Empty<ClientEventRecord>();
            }

            int capacity = max == int.MaxValue ? _batchSize : max;
            var list = new List<ClientEventRecord>(capacity);
            while (list.Count < max && _reader.TryRead(out ClientEventRecord item))
            {
                list.Add(item);
            }

            if (list.Count == 0)
            {
                return Array.Empty<ClientEventRecord>();
            }

            return list.ToArray();
        }

        private void Retain(ClientEventRecord[] batch)
        {
            lock (_retainGate)
            {
                if (_retainedCount == 0)
                {
                    _retained = batch;
                    _retainedCount = batch.Length;
                    return;
                }

                var merged = new ClientEventRecord[_retainedCount + batch.Length];
                Array.Copy(_retained, 0, merged, 0, _retainedCount);
                Array.Copy(batch, 0, merged, _retainedCount, batch.Length);
                _retained = merged;
                _retainedCount = merged.Length;
            }
        }

        private void MarkFaulted(ReadOnlyMemory<byte> evidence)
        {
            _lastFailureEvidence = evidence;
            Volatile.Write(ref _sinkFaulted, 1);
        }

        private async Task WaitForStopAsync()
        {
            if (_stop.IsCancellationRequested)
            {
                return;
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (_stop.Token.Register(delegate { tcs.TrySetResult(true); }))
            {
                if (_stop.IsCancellationRequested)
                {
                    return;
                }

                await tcs.Task.ConfigureAwait(false);
            }
        }
    }
}

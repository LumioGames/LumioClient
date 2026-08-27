using System;
using System.Threading;
using System.Threading.Channels;

namespace Lumio.Client.Observability
{
    internal sealed class BoundedEventWriter : IClientEventWriter, IDisposable
    {
        private readonly object _writeGate = new object();
        private readonly Channel<ClientEventRecord>? _channel;
        private readonly EventDispatcherWorker? _worker;
        private ReadOnlyMemory<byte> _lastFailureEvidence;
        private ulong _lastProducerSequence;
        private int _dropCount;
        private int _highWatermark;
        private bool _closed;

        private BoundedEventWriter(
            Channel<ClientEventRecord>? channel,
            EventDispatcherWorker? worker,
            bool closed)
        {
            _channel = channel;
            _worker = worker;
            _closed = closed;
        }

        internal ReadOnlyMemory<byte> LastFailureEvidence
        {
            get
            {
                if (_worker != null)
                {
                    ReadOnlyMemory<byte> fromWorker = _worker.LastFailureEvidence;
                    if (!fromWorker.IsEmpty)
                    {
                        return fromWorker;
                    }
                }

                lock (_writeGate)
                {
                    return _lastFailureEvidence;
                }
            }
        }

        public static BoundedEventWriter Start(in ClientEventPipelineOptions options, IClientEventSink sink)
        {
            var channel = Channel.CreateBounded<ClientEventRecord>(new BoundedChannelOptions(options.Capacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
                AllowSynchronousContinuations = false
            });

            var worker = new EventDispatcherWorker(channel.Reader, sink, options.BatchSize, options.SinkTimeout);
            return new BoundedEventWriter(channel, worker, false);
        }

        public static BoundedEventWriter CreateRejected()
        {
            return new BoundedEventWriter(null, null, true);
        }

        public ClientEventWriteResult TryWrite(in ClientEventRecord record)
        {
            if (!EventDropPolicy.IsEnqueueAllowed(record.SchemaClass))
            {
                return new ClientEventWriteResult(ClientEventWriteOutcome.Rejected);
            }

            lock (_writeGate)
            {
                if (_closed || _channel is null)
                {
                    return new ClientEventWriteResult(ClientEventWriteOutcome.Rejected);
                }

                ulong sequence = _lastProducerSequence + 1UL;
                var stamped = new ClientEventRecord(record.SchemaClass, CopyPayload(record.Payload), sequence);
                if (_channel.Writer.TryWrite(stamped))
                {
                    _lastProducerSequence = sequence;
                    int depth = _channel.Reader.Count;
                    if (_worker != null)
                    {
                        depth += _worker.PendingCount;
                    }

                    if (depth > _highWatermark)
                    {
                        _highWatermark = depth;
                    }

                    return new ClientEventWriteResult(ClientEventWriteOutcome.Accepted);
                }

                if (EventDropPolicy.CanDropOnQueueFull(record.SchemaClass))
                {
                    _dropCount++;
                    _lastFailureEvidence = FailureEvidenceEncoder.EncodeDropped(record.SchemaClass, _dropCount);
                    return new ClientEventWriteResult(ClientEventWriteOutcome.Dropped);
                }

                _lastFailureEvidence = FailureEvidenceEncoder.EncodeQueueFull(
                    record.SchemaClass,
                    _channel.Reader.Count);
                return new ClientEventWriteResult(ClientEventWriteOutcome.QueueFull);
            }
        }

        public ClientEventPipelineSnapshot GetSnapshot()
        {
            int dropCount;
            int highWatermark;
            ulong lastProducerSequence;
            bool closed;
            lock (_writeGate)
            {
                dropCount = _dropCount;
                highWatermark = _highWatermark;
                lastProducerSequence = _lastProducerSequence;
                closed = _closed;
            }

            int queueDepth = 0;
            if (_channel != null)
            {
                queueDepth = _channel.Reader.Count;
            }

            if (_worker != null)
            {
                queueDepth += _worker.PendingCount;
            }

            bool sinkFaulted = _worker != null && _worker.SinkFaulted;
            return new ClientEventPipelineSnapshot(
                queueDepth,
                dropCount,
                highWatermark,
                lastProducerSequence,
                sinkFaulted,
                closed);
        }

        public void Close()
        {
            lock (_writeGate)
            {
                if (!_closed)
                {
                    _closed = true;
                    if (_channel != null)
                    {
                        _channel.Writer.TryComplete();
                    }
                }
            }

            if (_worker != null)
            {
                _worker.Dispose();
            }
        }

        public void Dispose()
        {
            Close();
        }

        private static ReadOnlyMemory<byte> CopyPayload(ReadOnlyMemory<byte> payload)
        {
            if (payload.Length == 0)
            {
                return ReadOnlyMemory<byte>.Empty;
            }

            byte[] copy = new byte[payload.Length];
            payload.Span.CopyTo(copy);
            return copy;
        }
    }
}

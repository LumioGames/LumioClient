using System;

namespace Lumio.Client.Observability
{
    internal sealed class InMemoryEventBuffer
    {
        private readonly ClientEventRecord[] _slots;
        private readonly object _gate = new object();
        private int _count;
        private int _droppedCount;
        private bool _closed;

        public InMemoryEventBuffer(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _slots = new ClientEventRecord[capacity];
        }

        public int Capacity
        {
            get { return _slots.Length; }
        }

        public void Close()
        {
            lock (_gate)
            {
                _closed = true;
            }
        }

        // Drop incoming Droppable when full; Critical/Durable never overwrite and fail the remaining batch.
        public ClientEventSinkResult TryAppend(ReadOnlySpan<ClientEventRecord> records)
        {
            lock (_gate)
            {
                if (_closed)
                {
                    return new ClientEventSinkResult(false, 0, false);
                }

                int written = 0;
                for (int i = 0; i < records.Length; i++)
                {
                    ClientEventRecord record = records[i];
                    if (_count < _slots.Length)
                    {
                        Append(Clone(in record));
                        written++;
                        continue;
                    }

                    if (record.SchemaClass == EventSchemaClass.Droppable)
                    {
                        _droppedCount++;
                        continue;
                    }

                    return new ClientEventSinkResult(false, written, true);
                }

                return new ClientEventSinkResult(true, written, false);
            }
        }

        public void CopySnapshot(out ClientEventRecord[] records, out int droppedCount, out bool closed)
        {
            lock (_gate)
            {
                records = new ClientEventRecord[_count];
                for (int i = 0; i < _count; i++)
                {
                    records[i] = Clone(in _slots[i]);
                }

                droppedCount = _droppedCount;
                closed = _closed;
            }
        }

        private void Append(in ClientEventRecord record)
        {
            _slots[_count] = record;
            _count++;
        }

        private static ClientEventRecord Clone(in ClientEventRecord record)
        {
            ReadOnlyMemory<byte> payload = record.Payload;
            if (payload.Length == 0)
            {
                return new ClientEventRecord(record.SchemaClass, ReadOnlyMemory<byte>.Empty, record.ProducerSequence);
            }

            byte[] copy = new byte[payload.Length];
            payload.Span.CopyTo(copy);
            return new ClientEventRecord(record.SchemaClass, copy, record.ProducerSequence);
        }
    }
}

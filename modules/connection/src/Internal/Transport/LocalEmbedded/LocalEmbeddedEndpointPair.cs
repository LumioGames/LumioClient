using System;
using System.Threading.Channels;

namespace Lumio.Client.Connection
{
    internal sealed class LocalEmbeddedEndpointPair
    {
        private readonly Channel<ReadOnlyMemory<byte>> _aToB;
        private readonly Channel<ReadOnlyMemory<byte>> _bToA;

        public LocalEmbeddedEndpointPair(int capacity)
        {
            var options = new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            };
            _aToB = Channel.CreateBounded<ReadOnlyMemory<byte>>(options);
            _bToA = Channel.CreateBounded<ReadOnlyMemory<byte>>(options);
            Client = new LocalEmbeddedEndpoint(_aToB.Writer, _bToA.Reader);
            Server = new LocalEmbeddedEndpoint(_bToA.Writer, _aToB.Reader);
        }

        public LocalEmbeddedEndpoint Client { get; }

        public LocalEmbeddedEndpoint Server { get; }
    }

    internal sealed class LocalEmbeddedEndpoint
    {
        private readonly ChannelWriter<ReadOnlyMemory<byte>> _writer;
        private readonly ChannelReader<ReadOnlyMemory<byte>> _reader;

        public LocalEmbeddedEndpoint(ChannelWriter<ReadOnlyMemory<byte>> writer, ChannelReader<ReadOnlyMemory<byte>> reader)
        {
            _writer = writer;
            _reader = reader;
        }

        public bool TrySend(ReadOnlyMemory<byte> bytes)
        {
            return _writer.TryWrite(bytes);
        }

        public bool TryReceive(out ReadOnlyMemory<byte> bytes)
        {
            return _reader.TryRead(out bytes);
        }
    }
}

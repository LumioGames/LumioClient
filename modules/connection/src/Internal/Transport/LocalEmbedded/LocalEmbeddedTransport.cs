using System;

namespace Lumio.Client.Connection
{
    internal sealed class LocalEmbeddedTransport
    {
        private readonly LocalEmbeddedEndpointPair _pair;
        private readonly GeneratedEnvelopeCodecAdapter _codec = new GeneratedEnvelopeCodecAdapter();

        public LocalEmbeddedTransport(int capacity)
        {
            _pair = new LocalEmbeddedEndpointPair(capacity);
        }

        public LocalEmbeddedEndpointPair Pair
        {
            get { return _pair; }
        }

        public bool TrySendClient(in EncodedFrame frame)
        {
            if (!_codec.TryEncode(in frame, out ReadOnlyMemory<byte> bytes))
            {
                return false;
            }

            return _pair.Client.TrySend(bytes);
        }

        public bool TryReceiveServer(out EncodedFrame frame)
        {
            if (!_pair.Server.TryReceive(out ReadOnlyMemory<byte> bytes))
            {
                frame = default;
                return false;
            }

            return _codec.TryDecode(bytes, out frame);
        }
    }
}

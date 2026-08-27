using System;

namespace Lumio.Client.Connection
{
    internal sealed class GeneratedEnvelopeCodecAdapter
    {
        private readonly bool _requireBytes = true;

        public int EncodeCalls { get; private set; }

        public int DecodeCalls { get; private set; }

        public bool TryEncode(in EncodedFrame frame, out ReadOnlyMemory<byte> bytes)
        {
            bytes = default;
            if (!_requireBytes || frame.Bytes.IsEmpty)
            {
                return false;
            }

            byte[] copy = new byte[frame.Bytes.Length];
            frame.Bytes.Span.CopyTo(copy);
            bytes = copy;
            EncodeCalls++;
            return true;
        }

        public bool TryDecode(ReadOnlyMemory<byte> bytes, out EncodedFrame frame)
        {
            frame = default;
            if (!_requireBytes || bytes.IsEmpty)
            {
                return false;
            }

            byte[] copy = new byte[bytes.Length];
            bytes.Span.CopyTo(copy);
            frame = new EncodedFrame(copy);
            DecodeCalls++;
            return true;
        }
    }
}

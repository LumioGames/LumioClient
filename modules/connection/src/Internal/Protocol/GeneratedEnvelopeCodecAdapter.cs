using System;

namespace Lumio.Client.Connection
{
    internal sealed class GeneratedEnvelopeCodecAdapter
    {
        private readonly bool _requireBytes = true;

        public bool TryEncode(in EncodedFrame frame, out ReadOnlyMemory<byte> bytes)
        {
            if (!_requireBytes)
            {
                bytes = default;
                return false;
            }

            if (frame.Bytes.IsEmpty)
            {
                bytes = default;
                return false;
            }

            bytes = frame.Bytes;
            return true;
        }

        public bool TryDecode(ReadOnlyMemory<byte> bytes, out EncodedFrame frame)
        {
            if (!_requireBytes)
            {
                frame = default;
                return false;
            }

            if (bytes.IsEmpty)
            {
                frame = default;
                return false;
            }

            frame = new EncodedFrame(bytes);
            return true;
        }
    }
}

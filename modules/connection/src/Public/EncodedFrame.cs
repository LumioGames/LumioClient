using System;

namespace Lumio.Client.Connection
{
    public readonly struct EncodedFrame
    {
        public EncodedFrame(ReadOnlyMemory<byte> bytes)
        {
            Bytes = bytes;
        }

        public ReadOnlyMemory<byte> Bytes { get; }
    }
}

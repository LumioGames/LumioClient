using System;

namespace Lumio.Client.Handshake
{
    internal sealed class GeneratedHandshakeAdapter
    {
        private readonly bool _enabled = true;

        public bool IsHello(ReadOnlyMemory<byte> frame)
        {
            return _enabled && !frame.IsEmpty && frame.Span[0] == 1;
        }
    }
}

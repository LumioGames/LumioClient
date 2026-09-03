using System;
using System.Text;

namespace Lumio.Client.Session
{
    public sealed class JsonSessionMessageKindMap : ISessionMessageKindMap
    {
        private static readonly byte[] GapMagic = { 0x91, 0xA9, 0xB0, 0xC3 };

        public SessionMessageKind Map(ReadOnlyMemory<byte> frame)
        {
            ReadOnlySpan<byte> span = frame.Span;
            if (span.SequenceEqual(GapMagic))
            {
                return SessionMessageKind.Gap;
            }

            if (!TryReadMessageType(span, out string messageType))
            {
                return SessionMessageKind.Unknown;
            }

            if (string.Equals(messageType, "FullSnapshot", StringComparison.Ordinal))
            {
                return SessionMessageKind.FullSnapshot;
            }

            if (string.Equals(messageType, "Delta", StringComparison.Ordinal))
            {
                return SessionMessageKind.Delta;
            }

            if (string.Equals(messageType, "ConnectionSuperseded", StringComparison.Ordinal))
            {
                return SessionMessageKind.ConnectionSuperseded;
            }

            return SessionMessageKind.Unknown;
        }

        private static bool TryReadMessageType(ReadOnlySpan<byte> utf8, out string messageType)
        {
            messageType = string.Empty;
            string text = Encoding.UTF8.GetString(utf8.ToArray());
            const string marker = "\"messageType\"";
            int at = text.IndexOf(marker, StringComparison.Ordinal);
            if (at < 0)
            {
                return false;
            }

            int colon = text.IndexOf(':', at + marker.Length);
            if (colon < 0)
            {
                return false;
            }

            int first = text.IndexOf('"', colon + 1);
            if (first < 0)
            {
                return false;
            }

            int last = text.IndexOf('"', first + 1);
            if (last < 0)
            {
                return false;
            }

            messageType = text.Substring(first + 1, last - first - 1);
            return messageType.Length > 0;
        }
    }
}

using System;
using System.Text;

namespace Lumio.Client.Observability
{
    internal static class FailureEvidenceEncoder
    {
        public const byte Version = 1;
        public const byte KindQueueFull = 1;
        public const byte KindDropped = 2;
        public const byte KindSinkFault = 3;

        public static ReadOnlyMemory<byte> EncodeQueueFull(EventSchemaClass schemaClass, int queueDepth)
        {
            byte[] payload = new byte[6];
            payload[0] = Version;
            payload[1] = KindQueueFull;
            payload[2] = (byte)schemaClass;
            WriteUInt16(payload, 3, queueDepth);
            return payload;
        }

        public static ReadOnlyMemory<byte> EncodeDropped(EventSchemaClass schemaClass, int dropCount)
        {
            byte[] payload = new byte[6];
            payload[0] = Version;
            payload[1] = KindDropped;
            payload[2] = (byte)schemaClass;
            WriteUInt16(payload, 3, dropCount);
            return payload;
        }

        public static ReadOnlyMemory<byte> EncodeSinkFault(string exceptionName, int batchCount)
        {
            if (exceptionName is null)
            {
                exceptionName = string.Empty;
            }

            byte[] nameBytes = Encoding.UTF8.GetBytes(exceptionName);
            int nameLength = Math.Min(nameBytes.Length, 255);
            byte[] payload = new byte[4 + nameLength];
            payload[0] = Version;
            payload[1] = KindSinkFault;
            payload[2] = (byte)Math.Min(Math.Max(batchCount, 0), 255);
            payload[3] = (byte)nameLength;
            if (nameLength > 0)
            {
                Array.Copy(nameBytes, 0, payload, 4, nameLength);
            }

            return payload;
        }

        private static void WriteUInt16(byte[] payload, int index, int value)
        {
            int clamped = value;
            if (clamped < 0)
            {
                clamped = 0;
            }
            else if (clamped > ushort.MaxValue)
            {
                clamped = ushort.MaxValue;
            }

            payload[index] = (byte)clamped;
            payload[index + 1] = (byte)(clamped >> 8);
        }
    }
}

using System;

namespace Lumio.Client.Prediction
{
    internal static class RuntimePredictionPlanAdapter
    {
        internal const byte LocalKind = 1;
        internal const byte ConfirmationKind = 2;
        internal const byte CorrectionKind = 3;
        internal const byte ComposedKind = 0xA0;

        public static LocalPredictionPlan CreateLocal(ulong stageId, ulong generation, ReadOnlyMemory<byte> payload)
        {
            byte[] bytes = new byte[17 + payload.Length];
            bytes[0] = LocalKind;
            WriteUInt64(bytes, 1, stageId);
            WriteUInt64(bytes, 9, generation);
            payload.Span.CopyTo(bytes.AsSpan(17));
            return new LocalPredictionPlan(bytes);
        }

        public static PredictionReconcilePlan CreateReconcile(
            PredictionUpdateKind kind,
            ulong stageId,
            ulong generation,
            ulong confirmedThrough,
            int replayCount,
            ReadOnlyMemory<byte> payload)
        {
            byte[] bytes = new byte[29 + payload.Length];
            bytes[0] = kind == PredictionUpdateKind.Correction ? CorrectionKind : ConfirmationKind;
            WriteUInt64(bytes, 1, stageId);
            WriteUInt64(bytes, 9, generation);
            WriteUInt64(bytes, 17, confirmedThrough);
            WriteUInt32(bytes, 25, (uint)replayCount);
            payload.Span.CopyTo(bytes.AsSpan(29));
            return new PredictionReconcilePlan(bytes);
        }

        public static ReadOnlyMemory<byte> Compose(ReadOnlyMemory<byte> leftPlan, ReadOnlyMemory<byte> rightPlan)
        {
            byte[] bytes = new byte[5 + leftPlan.Length + rightPlan.Length];
            bytes[0] = ComposedKind;
            WriteUInt32(bytes, 1, (uint)leftPlan.Length);
            leftPlan.Span.CopyTo(bytes.AsSpan(5));
            rightPlan.Span.CopyTo(bytes.AsSpan(5 + leftPlan.Length));
            return bytes;
        }

        private static void WriteUInt32(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteUInt64(byte[] buffer, int offset, ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                buffer[offset + i] = (byte)(value >> (8 * i));
            }
        }
    }
}

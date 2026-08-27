using System;

namespace Lumio.Client.Prediction
{
    internal enum PredictionUpdateKind : byte
    {
        Invalid = 0,
        Confirmation = 1,
        Correction = 2
    }

    internal static class GeneratedPredictionAdapter
    {
        public static bool TryClassify(ReadOnlyMemory<byte> payload, out PredictionUpdateKind kind)
        {
            kind = PredictionUpdateKind.Invalid;
            if (payload.IsEmpty)
            {
                return false;
            }

            byte marker = payload.Span[0];
            if (marker == (byte)PredictionUpdateKind.Correction)
            {
                kind = PredictionUpdateKind.Correction;
                return true;
            }

            kind = PredictionUpdateKind.Confirmation;
            return true;
        }
    }
}

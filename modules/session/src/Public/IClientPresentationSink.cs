using System;

namespace Lumio.Client.Session
{
    public readonly struct PresentationWriteResult
    {
        public PresentationWriteResult(bool accepted)
        {
            Accepted = accepted;
        }

        public bool Accepted { get; }
    }

    public interface IClientPresentationSink
    {
        PresentationWriteResult TryWrite(ReadOnlyMemory<byte> committedDiff, ulong sessionGeneration);
    }

    public sealed class NullPresentationSink : IClientPresentationSink
    {
        public int WriteCalls { get; private set; }

        public PresentationWriteResult TryWrite(ReadOnlyMemory<byte> committedDiff, ulong sessionGeneration)
        {
            _ = committedDiff;
            _ = sessionGeneration;
            WriteCalls++;
            return new PresentationWriteResult(true);
        }
    }
}

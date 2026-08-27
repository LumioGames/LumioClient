using System;

namespace Lumio.Client.Handshake
{
    public enum HandshakeOpaqueFrameRole
    {
        Unclassified = 0,
        ServerHello = 1,
        HandshakeReject = 2
    }

    public interface IHandshakeFrameClassifier
    {
        HandshakeOpaqueFrameRole Classify(ReadOnlyMemory<byte> frame);
    }

    public sealed class UnpublishedHandshakeFrameClassifier : IHandshakeFrameClassifier
    {
        public HandshakeOpaqueFrameRole Classify(ReadOnlyMemory<byte> frame)
        {
            _ = frame;
            return HandshakeOpaqueFrameRole.Unclassified;
        }
    }
}

using System;

namespace Lumio.Client.Handshake
{
    internal sealed class GeneratedHandshakeAdapter
    {
        private readonly IHandshakeFrameClassifier _classifier;

        public GeneratedHandshakeAdapter(IHandshakeFrameClassifier classifier)
        {
            _classifier = classifier ?? new UnpublishedHandshakeFrameClassifier();
        }

        public HandshakeOpaqueFrameRole Classify(ReadOnlyMemory<byte> frame)
        {
            if (frame.IsEmpty)
            {
                return HandshakeOpaqueFrameRole.Unclassified;
            }

            return _classifier.Classify(frame);
        }
    }
}

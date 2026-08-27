using System;

namespace Lumio.Client.Handshake.Tests;

internal static class HandshakeTestFixtures
{
    public static readonly byte[] ServerHello = { 0xA5, 0x3C, 0x91, 0x07, 0xD2, 0x4E, 0xB8, 0x11 };

    public static readonly byte[] HandshakeReject = { 0x5A, 0xC3, 0x0E, 0xF4 };

    public static IHandshakeFrameClassifier Classifier { get; } = new FixtureClassifier();

    private sealed class FixtureClassifier : IHandshakeFrameClassifier
    {
        public HandshakeOpaqueFrameRole Classify(ReadOnlyMemory<byte> frame)
        {
            if (frame.Span.SequenceEqual(ServerHello))
            {
                return HandshakeOpaqueFrameRole.ServerHello;
            }

            if (frame.Span.SequenceEqual(HandshakeReject))
            {
                return HandshakeOpaqueFrameRole.HandshakeReject;
            }

            return HandshakeOpaqueFrameRole.Unclassified;
        }
    }
}

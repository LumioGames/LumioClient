using System;
using Lumio.Client.Handshake;

namespace Lumio.Client.Session
{
    internal sealed class HandshakeOrchestrator
    {
        public int BeginCount { get; private set; }

        public IClientHandshake Handshake { get; private set; } = default!;

        public void Begin(IClientHandshake handshake, HandshakeAttemptId attempt, ulong generation)
        {
            Handshake = handshake;
            BeginCount++;
            handshake.Begin(new HandshakeBeginRequest(attempt, generation));
        }

        public HandshakeOutcome HandleOpaqueFrame(ReadOnlyMemory<byte> frame)
        {
            if (Handshake == null)
            {
                return default(HandshakeOutcome);
            }

            Handshake.HandleFrame(frame);
            return Handshake.Poll();
        }
    }
}

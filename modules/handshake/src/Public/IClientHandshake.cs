using System;

namespace Lumio.Client.Handshake
{
    public interface IClientHandshake
    {
        HandshakeCommandResult Begin(in HandshakeBeginRequest request);

        HandshakeCommandResult HandleFrame(ReadOnlyMemory<byte> frame);

        HandshakeOutcome Poll();

        HandshakeCommandResult Cancel();

        HandshakeOutcome GetSnapshot();
    }

    public readonly struct HandshakeBeginRequest
    {
        public HandshakeBeginRequest(HandshakeAttemptId attempt, ulong generation)
        {
            Attempt = attempt;
            Generation = generation;
        }

        public HandshakeAttemptId Attempt { get; }

        public ulong Generation { get; }
    }

    public readonly struct HandshakeCommandResult
    {
        public HandshakeCommandResult(bool succeeded)
        {
            Succeeded = succeeded;
        }

        public bool Succeeded { get; }
    }
}

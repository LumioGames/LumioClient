using Lumio.Client.Handshake;

namespace Lumio.Client.Handshake.Tests.Fault;

public sealed class HandshakeRaceTests
{
    [Fact]
    public void CancelDisconnectAccepted_PriorityIsDeterministic()
    {
        var handshake = new ClientHandshakeFactory().Create(new ImmediateCapability());
        handshake.Begin(new HandshakeBeginRequest(new HandshakeAttemptId(1), 1));
        handshake.Cancel();
        handshake.HandleFrame(new byte[] { 1 });
        var outcome = handshake.Poll();
        Assert.Equal(HandshakePhase.Cancelled, outcome.Phase);
        Assert.False(outcome.Accepted);
    }

    private sealed class ImmediateCapability : IPlatformCapabilityProvider
    {
        public ValueTask<PlatformCapabilityResult> QueryAsync(in PlatformCapabilityQuery query, CancellationToken cancellationToken)
        {
            return new ValueTask<PlatformCapabilityResult>(new PlatformCapabilityResult(query.Attempt, query.Generation, true));
        }
    }
}

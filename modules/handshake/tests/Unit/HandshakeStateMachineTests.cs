using Lumio.Client.Handshake;

namespace Lumio.Client.Handshake.Tests.Unit;

public sealed class HandshakeStateMachineTests
{
    [Fact]
    public void Accepted_RequiresCapabilityAndValidServerHello()
    {
        var handshake = new ClientHandshakeFactory().Create(new FixedCapability(true));
        handshake.Begin(new HandshakeBeginRequest(new HandshakeAttemptId(1), 1));
        handshake.HandleFrame(new byte[] { 1 });
        var outcome = handshake.Poll();
        Assert.True(outcome.Accepted);
        Assert.Equal(HandshakePhase.Accepted, outcome.Phase);
    }
}

public sealed class HandshakeAttemptGenerationTests
{
    [Fact]
    public void LateCapabilityCompletion_Dropped()
    {
        var handshake = new HandshakeSession(new FixedCapability(true));
        handshake.Begin(new HandshakeBeginRequest(new HandshakeAttemptId(1), 1));
        handshake.HandleFrame(new byte[] { 1 });
        handshake.Poll();
        handshake.Begin(new HandshakeBeginRequest(new HandshakeAttemptId(2), 2));
        var snapshot = handshake.GetSnapshot();
        Assert.Equal(HandshakePhase.AwaitingHello, snapshot.Phase);
        Assert.False(snapshot.Accepted);
    }
}

internal sealed class FixedCapability : IPlatformCapabilityProvider
{
    private readonly bool _ok;

    public FixedCapability(bool ok)
    {
        _ok = ok;
    }

    public ValueTask<PlatformCapabilityResult> QueryAsync(in PlatformCapabilityQuery query, CancellationToken cancellationToken)
    {
        return new ValueTask<PlatformCapabilityResult>(new PlatformCapabilityResult(query.Attempt, query.Generation, _ok));
    }
}

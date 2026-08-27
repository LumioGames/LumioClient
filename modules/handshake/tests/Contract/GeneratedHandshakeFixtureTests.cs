using Lumio.Client.Handshake;

namespace Lumio.Client.Handshake.Tests.Contract;

public sealed class GeneratedHandshakeFixtureTests
{
    [Fact]
    public void ValidAndInvalidVectors()
    {
        var adapter = new GeneratedHandshakeAdapter();
        Assert.True(adapter.IsHello(new byte[] { 1 }));
        Assert.False(adapter.IsHello(ReadOnlyMemory<byte>.Empty));
        Assert.False(adapter.IsHello(new byte[] { 9 }));
    }
}

public sealed class HandshakePhaseFixtureTests
{
    [Fact]
    public void OutOfPhaseMessageHasZeroStateAdvance()
    {
        var gate = new GeneratedHandshakeMessageGate();
        Assert.False(gate.Allows(HandshakePhase.Accepted, 1));
        var handshake = new ClientHandshakeFactory().Create(new OkCap());
        handshake.Begin(new HandshakeBeginRequest(new HandshakeAttemptId(1), 1));
        handshake.HandleFrame(new byte[] { 1 });
        handshake.Poll();
        var before = handshake.GetSnapshot();
        handshake.HandleFrame(new byte[] { 1 });
        var after = handshake.GetSnapshot();
        Assert.Equal(before.Phase, after.Phase);
    }

    private sealed class OkCap : IPlatformCapabilityProvider
    {
        public ValueTask<PlatformCapabilityResult> QueryAsync(in PlatformCapabilityQuery query, CancellationToken cancellationToken)
        {
            return new ValueTask<PlatformCapabilityResult>(new PlatformCapabilityResult(query.Attempt, query.Generation, true));
        }
    }
}

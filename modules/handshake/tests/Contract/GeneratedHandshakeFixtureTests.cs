using Lumio.Client.Handshake;

namespace Lumio.Client.Handshake.Tests.Contract;

public sealed class GeneratedHandshakeFixtureTests
{
    [Fact]
    public void ValidAndInvalidVectors()
    {
        var adapter = new GeneratedHandshakeAdapter(HandshakeTestFixtures.Classifier);
        Assert.Equal(HandshakeOpaqueFrameRole.ServerHello, adapter.Classify(HandshakeTestFixtures.ServerHello));
        Assert.Equal(HandshakeOpaqueFrameRole.Unclassified, adapter.Classify(ReadOnlyMemory<byte>.Empty));
        Assert.Equal(HandshakeOpaqueFrameRole.Unclassified, adapter.Classify(new byte[] { 9 }));
        Assert.Equal(HandshakeOpaqueFrameRole.Unclassified, new GeneratedHandshakeAdapter(new UnpublishedHandshakeFrameClassifier()).Classify(HandshakeTestFixtures.ServerHello));
    }
}

public sealed class HandshakePhaseFixtureTests
{
    [Fact]
    public void OutOfPhaseMessageHasZeroStateAdvance()
    {
        var gate = new GeneratedHandshakeMessageGate();
        Assert.False(gate.Allows(HandshakePhase.Accepted, 1));
        var handshake = new ClientHandshakeFactory().Create(new OkCap(), HandshakeTestFixtures.Classifier);
        handshake.Begin(new HandshakeBeginRequest(new HandshakeAttemptId(1), 1));
        handshake.HandleFrame(HandshakeTestFixtures.ServerHello);
        handshake.Poll();
        var before = handshake.GetSnapshot();
        handshake.HandleFrame(HandshakeTestFixtures.ServerHello);
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

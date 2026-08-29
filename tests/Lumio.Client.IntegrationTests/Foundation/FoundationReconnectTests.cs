using Lumio.Client.IntegrationTests.Support;
using Lumio.Client.Session;

namespace Lumio.Client.IntegrationTests.Foundation;

public sealed class FoundationReconnectTests
{
    [Fact]
    [Trait("Category", "Foundation")]
    public void Disconnect_StartsNewGenerationAndHandshake()
    {
        var harness = new FoundationHarness(true);
        harness.HappyPathToActive();
        int begins = harness.Session.GetSnapshot().HandshakeBeginCount;
        ulong g1 = harness.Session.GetSnapshot().Generation;
        harness.Connections.Loopback.TryDisconnectClient();
        harness.Tick();
        ClientSessionSnapshot snap = harness.Session.GetSnapshot();
        Assert.True(snap.Generation > g1);
        Assert.True(snap.HandshakeBeginCount > begins);
        Assert.NotEqual(ClientSessionState.Active, snap.State);
    }
}

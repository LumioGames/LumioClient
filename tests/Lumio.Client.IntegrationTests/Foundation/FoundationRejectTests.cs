using Lumio.Client.Session;
using Lumio.Client.IntegrationTests.Support;

namespace Lumio.Client.IntegrationTests.Foundation;

public sealed class FoundationRejectTests
{
    [Fact]
    [Trait("Category", "Foundation")]
    public void HandshakeReject_ClosesWithoutActivatingScope()
    {
        var harness = new FoundationHarness(true);
        harness.Connect();
        harness.Tick();
        harness.Deliver(FoundationTestBytes.Reject);
        harness.Tick();
        ClientSessionSnapshot snap = harness.Session.GetSnapshot();
        Assert.Equal(ClientSessionState.Closed, snap.State);
        Assert.Equal(0, harness.Scope.ActivateCalls);
        Assert.Equal(0, snap.EcsHandles);
        Assert.False(snap.RuntimeCommitted);
    }
}

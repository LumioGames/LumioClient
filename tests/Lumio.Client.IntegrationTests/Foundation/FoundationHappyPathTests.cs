using Lumio.Client.Session;
using Lumio.Client.IntegrationTests.Support;

namespace Lumio.Client.IntegrationTests.Foundation;

public sealed class FoundationHappyPathTests
{
    [Fact]
    [Trait("Category", "Foundation")]
    public void ConnectHandshakeSnapshotAckActiveGapResyncClose()
    {
        var harness = new FoundationHarness(true);
        harness.HappyPathToActive();
        ClientSessionSnapshot active = harness.Session.GetSnapshot();
        Assert.Equal(ClientSessionState.Active, active.State);
        Assert.True(active.RuntimeCommitted);
        Assert.True(active.BaselineAckSent);
        Assert.True(active.PresentationWritten);
        Assert.True(active.ScopeActivated);
        Assert.True(harness.Runtime.AuthorityCalls >= 1);

        harness.Deliver(FoundationTestBytes.Gap);
        harness.Tick();
        Assert.Equal(ClientSessionState.Resyncing, harness.Session.GetSnapshot().State);

        harness.Deliver(FoundationTestBytes.Snapshot);
        harness.Tick();
        Assert.Equal(ClientSessionState.Active, harness.Session.GetSnapshot().State);

        harness.Session.RequestClose(new SessionCloseRequest(false));
        Assert.Equal(ClientSessionState.Closed, harness.Session.GetSnapshot().State);
    }
}

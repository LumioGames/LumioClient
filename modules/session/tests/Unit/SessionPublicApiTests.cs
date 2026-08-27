using Lumio.Client.Session;
using Lumio.Client.Session.Tests.Support;

namespace Lumio.Client.Session.Tests.Unit;

public sealed class SessionPublicApiTests
{
    [Fact]
    public void RuntimeHandleLedger_AndFirstConnectHappyPath()
    {
        var harness = new SessionHarness(true);
        harness.HappyPathToActive();
        ClientSessionSnapshot snapshot = harness.Session.GetSnapshot();
        Assert.Equal(ClientSessionState.Active, snapshot.State);
        Assert.True(snapshot.RuntimeCommitted);
        Assert.True(snapshot.LedgerCount > 0);
        harness.Session.RequestClose(new SessionCloseRequest(false));
        Assert.Equal(ClientSessionState.Closed, harness.Session.GetSnapshot().State);
    }

    [Fact]
    public void AuthorityTransactionFault_DoesNotCommit()
    {
        var harness = new SessionHarness(false);
        harness.Connect();
        harness.Tick();
        harness.Deliver(SessionTestBytes.Hello);
        harness.Tick();
        harness.Deliver(SessionTestBytes.Snapshot);
        harness.Tick();
        Assert.NotEqual(ClientSessionState.Active, harness.Session.GetSnapshot().State);
        Assert.False(harness.Session.GetSnapshot().RuntimeCommitted);
    }
}

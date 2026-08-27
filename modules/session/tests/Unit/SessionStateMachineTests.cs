using Lumio.Client.Session;
using Lumio.Client.Session.Tests.Support;

namespace Lumio.Client.Session.Tests.Unit;

public sealed class SessionStateMachineTests
{
    [Fact]
    public void HappyPath_ConnectToActive()
    {
        var harness = new SessionHarness(runtimeCommitted: true);
        harness.HappyPathToActive();
        Assert.Equal(ClientSessionState.Active, harness.Session.GetSnapshot().State);
        Assert.True(harness.Session.GetSnapshot().RuntimeCommitted);
        Assert.True(harness.Session.GetSnapshot().ScopeActivated);
        Assert.True(harness.Session.GetSnapshot().BaselineAckSent);
        Assert.True(harness.Runtime.AuthorityCalls >= 1);
        Assert.True(harness.Session.GetSnapshot().ReplicaStageCalls >= 1);
    }

    [Fact]
    public void ScopeMustActivateBeforeWorldHandles()
    {
        var gate = new GameplayScopeActivationGate();
        Assert.False(gate.CanCreateWorldHandles());
        gate.TryPrepare();
        Assert.False(gate.CanCreateWorldHandles());
        gate.TryActivate();
        Assert.True(gate.CanCreateWorldHandles());
    }
}

using Lumio.Client.Session;
using Lumio.Client.Session.Tests.Support;

namespace Lumio.Client.Session.Tests.Fault;

public sealed class SessionRaceTests
{
    [Fact]
    public void LateG1Success_CannotActivateG2()
    {
        var harness = new SessionHarness(true);
        harness.HappyPathToActive();
        ulong g1 = harness.Session.GetSnapshot().Generation;
        harness.Connections.Loopback.TryDisconnectClient();
        harness.Tick();
        ulong g2 = harness.Session.GetSnapshot().Generation;
        Assert.True(g2 > g1);
        harness.Deliver(SessionTestBytes.Hello);
        harness.Tick();
        Assert.NotEqual(ClientSessionState.Active, harness.Session.GetSnapshot().State);
    }

    [Fact]
    public void QueueFullCloseFault_TerminalIsDeterministic()
    {
        var harness = new SessionHarness(true);
        harness.Connect();
        harness.Session.RequestClose(new SessionCloseRequest(true));
        Assert.Equal(ClientSessionState.Faulted, harness.Session.GetSnapshot().State);
        harness.Session.RequestClose(new SessionCloseRequest(false));
        Assert.Equal(ClientSessionState.Faulted, harness.Session.GetSnapshot().State);
    }

    [Fact]
    public void FaultBeatsCloseAndCommitted()
    {
        var harness = new SessionHarness(true);
        harness.HappyPathToActive();
        harness.Session.RequestClose(new SessionCloseRequest(true));
        Assert.Equal(ClientSessionState.Faulted, harness.Session.GetSnapshot().State);
    }

    [Fact]
    public void LateCompletionCannotRecreateReleasedResource()
    {
        var harness = new SessionHarness(true);
        harness.HappyPathToActive();
        harness.Session.RequestClose(new SessionCloseRequest(false));
        harness.Deliver(SessionTestBytes.Snapshot);
        harness.Tick();
        Assert.Equal(ClientSessionState.Closed, harness.Session.GetSnapshot().State);
        Assert.Equal(0, harness.Session.GetSnapshot().EcsHandles);
    }
}

using Lumio.Client.Session;
using Lumio.Client.Session.Tests.Support;

namespace Lumio.Client.Session.Tests.Fault;

public sealed class AuthorityTransactionFaultMatrixTests
{
    [Fact]
    public void EveryFaultPoint_PreservesContract()
    {
        var abort = new SessionHarness(false);
        abort.Connect();
        abort.Tick();
        abort.Deliver(SessionTestBytes.Hello);
        abort.Tick();
        abort.Deliver(SessionTestBytes.Snapshot);
        abort.Tick();
        Assert.False(abort.Session.GetSnapshot().RuntimeCommitted);
        Assert.False(abort.Session.GetSnapshot().PresentationWritten);
    }

    [Fact]
    public void IndeterminateNeverAcksOrPresents()
    {
        var harness = new SessionHarness(true, true);
        harness.Connect();
        harness.Tick();
        harness.Deliver(SessionTestBytes.Hello);
        harness.Tick();
        harness.Deliver(SessionTestBytes.Snapshot);
        harness.Tick();
        ClientSessionSnapshot snap = harness.Session.GetSnapshot();
        Assert.Equal(ClientSessionState.Faulted, snap.State);
        Assert.False(snap.RuntimeCommitted);
        Assert.False(snap.BaselineAckSent);
        Assert.False(snap.PresentationWritten);
        Assert.False(harness.Connections.Loopback.TryReceiveFromClient(out _));
    }

    [Fact]
    public void AbortDiscardsBothStages()
    {
        var abort = new SessionHarness(false);
        abort.HappyPathToActive();
        Assert.False(abort.Session.GetSnapshot().RuntimeCommitted);
        Assert.NotEqual(ClientSessionState.Active, abort.Session.GetSnapshot().State);
    }

    [Fact]
    public void CommitAdvancesExactlyOnce()
    {
        var harness = new SessionHarness(true);
        harness.HappyPathToActive();
        int calls = harness.Runtime.AuthorityCalls;
        harness.Tick();
        Assert.Equal(calls, harness.Runtime.AuthorityCalls);
        Assert.True(harness.Session.GetSnapshot().RuntimeCommitted);
    }
}

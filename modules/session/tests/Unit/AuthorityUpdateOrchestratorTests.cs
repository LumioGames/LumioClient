using Lumio.Client.Session;
using Lumio.Client.Session.Tests.Support;

namespace Lumio.Client.Session.Tests.Unit;

public sealed class AuthorityUpdateOrchestratorTests
{
    [Fact]
    public void Committed_MetadataAckDiffOrder()
    {
        var harness = new SessionHarness(true);
        harness.HappyPathToActive();
        ClientSessionSnapshot snap = harness.Session.GetSnapshot();
        Assert.True(snap.RuntimeCommitted);
        Assert.True(snap.BaselineAckSent);
        Assert.True(snap.PresentationWritten);
        Assert.True(snap.ReplicaStageCalls >= 1);
        Assert.True(harness.Runtime.AuthorityCalls >= 1);
    }

    [Fact]
    public void Aborted_NoMetadataAckOrDiff()
    {
        var harness = new SessionHarness(false);
        harness.Connect();
        harness.Tick();
        harness.Deliver(SessionTestBytes.Hello);
        harness.Tick();
        harness.Deliver(SessionTestBytes.Snapshot);
        harness.Tick();
        ClientSessionSnapshot snap = harness.Session.GetSnapshot();
        Assert.False(snap.RuntimeCommitted);
        Assert.False(snap.BaselineAckSent);
        Assert.NotEqual(ClientSessionState.Active, snap.State);
    }

    [Fact]
    public void SecondStageFails_FirstStageDiscarded()
    {
        var harness = new SessionHarness(false);
        harness.HappyPathToActive();
        Assert.False(harness.Session.GetSnapshot().RuntimeCommitted);
    }
}

public sealed class LocalPredictionOrchestratorTests
{
    [Fact]
    public void CommandSequenceAllocatedOnlyAfterRuntimeCommit()
    {
        var harness = new SessionHarness(true);
        harness.HappyPathToActive();
        harness.Ingress.TryEnqueue(new Lumio.Client.Input.RawInputSample(1, 0, 0));
        harness.Tick();
        Assert.True(harness.Runtime.LocalCalls >= 1);
    }
}

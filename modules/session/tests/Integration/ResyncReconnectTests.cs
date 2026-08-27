using Lumio.Client.Input;
using Lumio.Client.Session;
using Lumio.Client.Session.Tests.Support;

namespace Lumio.Client.Session.Tests.Integration;

public sealed class ResyncReconnectTests
{
    [Fact]
    public void Resync_DoesNotHandshake()
    {
        var harness = new SessionHarness(true);
        harness.HappyPathToActive();
        int begins = harness.Session.GetSnapshot().HandshakeBeginCount;
        harness.Deliver(SessionTestBytes.Gap);
        harness.Tick();
        Assert.Equal(ClientSessionState.Resyncing, harness.Session.GetSnapshot().State);
        Assert.Equal(begins, harness.Session.GetSnapshot().HandshakeBeginCount);
        harness.Deliver(SessionTestBytes.Snapshot);
        harness.Tick();
        Assert.Equal(ClientSessionState.Active, harness.Session.GetSnapshot().State);
        Assert.Equal(begins, harness.Session.GetSnapshot().HandshakeBeginCount);
    }

    [Fact]
    public void Reconnect_NewGenerationReauthAndHandshake_NoResume()
    {
        var harness = new SessionHarness(true);
        harness.HappyPathToActive();
        int begins = harness.Session.GetSnapshot().HandshakeBeginCount;
        ulong g1 = harness.Session.GetSnapshot().Generation;
        harness.Connections.Loopback.TryDisconnectClient();
        harness.Tick();
        Assert.True(harness.Session.GetSnapshot().Generation > g1);
        Assert.True(harness.Session.GetSnapshot().HandshakeBeginCount > begins);
    }

    [Fact]
    public void ResyncInputPolicyIsGenerationScoped()
    {
        var harness = new SessionHarness(true);
        harness.HappyPathToActive();
        harness.Deliver(SessionTestBytes.Gap);
        harness.Tick();
        Assert.Equal(InputBufferPolicyKind.Resync, harness.Commands.GetSnapshotPolicy().Kind);
        Assert.Equal(harness.Session.GetSnapshot().Generation, harness.Commands.GetSnapshotPolicy().Generation);
    }
}

using Lumio.Client.Connection;
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
        Assert.True(harness.Connections.Loopback.EncodeCalls >= 1);
        Assert.True(harness.Connections.Loopback.DecodeCalls >= 1);
        Assert.True(harness.Connections.Loopback.TryReceiveFromClient(out EncodedFrame ack));
        Assert.False(ack.Bytes.IsEmpty);

        harness.Deliver(FoundationTestBytes.Gap);
        harness.Tick();
        Assert.Equal(ClientSessionState.Resyncing, harness.Session.GetSnapshot().State);

        harness.Deliver(FoundationTestBytes.Snapshot);
        harness.Tick();
        Assert.Equal(ClientSessionState.Active, harness.Session.GetSnapshot().State);

        harness.Session.RequestClose(new SessionCloseRequest(false));
        Assert.Equal(ClientSessionState.Closed, harness.Session.GetSnapshot().State);
    }

    [Fact]
    [Trait("Category", "Foundation")]
    public async Task HeadlessLocalEmbeddedConnectHandshakeSnapshotAckActiveGapResyncClose()
    {
        int code = await Lumio.Client.Bot.Host.FoundationHostCommand.RunAsync(
            new[] { "foundation", "--transport", "local-embedded", "--fixture", "foundation-happy-path" },
            CancellationToken.None);
        Assert.Equal(0, code);
    }
}

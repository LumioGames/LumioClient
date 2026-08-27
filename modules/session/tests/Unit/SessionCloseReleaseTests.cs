using Lumio.Client.Session;
using Lumio.Client.Session.Tests.Support;

namespace Lumio.Client.Session.Tests.Unit;

public sealed class SessionCloseReleaseTests
{
    [Fact]
    public void OrderIsInputPredictionReplicaVoxelEcsScopeHandshakeConnection()
    {
        var harness = new SessionHarness(true);
        harness.HappyPathToActive();
        harness.Session.RequestClose(new SessionCloseRequest(false));
        string[] order = harness.Session.GetSnapshot().ReleaseOrder;
        Assert.Contains("input", order);
        Assert.Contains("prediction", order);
        Assert.Contains("replica", order);
        Assert.Contains("connection", order);
        Assert.True(Array.IndexOf(order, "input") < Array.IndexOf(order, "connection"));
        Assert.True(Array.IndexOf(order, "prediction") < Array.IndexOf(order, "handshake"));
    }

    [Fact]
    public void RepeatedCloseIsIdempotent()
    {
        var harness = new SessionHarness(true);
        harness.HappyPathToActive();
        harness.Session.RequestClose(new SessionCloseRequest(false));
        string[] first = harness.Session.GetSnapshot().ReleaseOrder;
        harness.Session.RequestClose(new SessionCloseRequest(false));
        Assert.Equal(ClientSessionState.Closed, harness.Session.GetSnapshot().State);
        Assert.Equal(first, harness.Session.GetSnapshot().ReleaseOrder);
    }
}

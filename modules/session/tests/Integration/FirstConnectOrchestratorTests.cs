using Lumio.Client.Session;
using Lumio.Client.Session.Tests.Support;

namespace Lumio.Client.Session.Tests.Integration;

public sealed class FirstConnectOrchestratorTests
{
    [Fact]
    public void RejectHasZeroScopeAndHandleCalls()
    {
        var harness = new SessionHarness(true);
        harness.Connect();
        harness.Tick();
        harness.Deliver(SessionTestBytes.Reject);
        harness.Tick();
        Assert.Equal(0, harness.Scope.ActivateCalls);
        Assert.Equal(0, harness.Session.GetSnapshot().EcsHandles);
        Assert.NotEqual(ClientSessionState.Active, harness.Session.GetSnapshot().State);
    }

    [Fact]
    public void VoxelHandleFailureRollsBackEcsThenScope()
    {
        var ledger = new RuntimeHandleLedger();
        ledger.TryCreateEcs();
        ledger.RollbackEcsOnVoxelFailure();
        Assert.Equal(0, ledger.EcsCount);
        Assert.Equal(new[] { "ecs" }, ledger.DestroyOrder);
    }
}

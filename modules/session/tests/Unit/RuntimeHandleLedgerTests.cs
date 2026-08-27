namespace Lumio.Client.Session.Tests.Unit;

public sealed class RuntimeHandleLedgerTests
{
    [Fact]
    public void CreateEcsThenVoxel_DestroyVoxelThenEcs()
    {
        var ledger = new RuntimeHandleLedger();
        Assert.True(ledger.TryCreateEcs());
        Assert.True(ledger.TryCreateVoxel());
        ledger.DestroyVoxelThenEcs();
        Assert.Equal(new[] { "voxel", "ecs" }, ledger.DestroyOrder);
        Assert.Equal(0, ledger.EcsCount);
        Assert.Equal(0, ledger.VoxelCount);
    }

    [Fact]
    public void VoxelCreateFailureDestroysEcs()
    {
        var ledger = new RuntimeHandleLedger();
        Assert.True(ledger.TryCreateEcs());
        ledger.RollbackEcsOnVoxelFailure();
        Assert.Equal(0, ledger.EcsCount);
        Assert.Equal(new[] { "ecs" }, ledger.DestroyOrder);
    }
}

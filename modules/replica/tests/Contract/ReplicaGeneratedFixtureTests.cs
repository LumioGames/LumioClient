using Lumio.Client.Replica;

namespace Lumio.Client.Replica.Tests.Contract;

public sealed class ReplicaGeneratedFixtureTests
{
    [Fact]
    public void FullSnapshotDeltaInvalidVectors()
    {
        var adapter = new GeneratedReplicaAdapter();
        Assert.True(adapter.TryValidate(ReplicaUpdateKind.FullSnapshot, new byte[] { 1 }));
        Assert.True(adapter.TryValidate(ReplicaUpdateKind.Delta, new byte[] { 2 }));
        Assert.False(adapter.TryValidate(ReplicaUpdateKind.FullSnapshot, ReadOnlyMemory<byte>.Empty));
        Assert.False(adapter.TryValidate(ReplicaUpdateKind.Delta, ReadOnlyMemory<byte>.Empty));
        Assert.True(adapter.TryValidate(ReplicaUpdateKind.FullSnapshot, new byte[] { 0 }));
        Assert.True(adapter.TryValidate(ReplicaUpdateKind.Delta, new byte[] { 0 }));
        Assert.True(adapter.TryValidate(ReplicaUpdateKind.FullSnapshot, new byte[] { 2 }));
        Assert.True(adapter.TryValidate(ReplicaUpdateKind.Delta, new byte[] { 1 }));
        Assert.True(adapter.TryValidate(ReplicaUpdateKind.FullSnapshot, new byte[] { 9 }));
        Assert.True(adapter.TryValidate(ReplicaUpdateKind.Delta, new byte[] { 9 }));
    }
}

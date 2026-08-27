using Lumio.Client.Replica;
using Lumio.Client.Replica.Tests.Unit;

namespace Lumio.Client.Replica.Tests.Properties;

public sealed class ReplicaSequenceProperties
{
    [Fact]
    public void CommittedWatermarkNeverRegresses()
    {
        var replica = new ClientReplicaFactory().Create();
        replica.ResetForNewSession(new ReplicaResetRequest(2));
        ReplicaStageRequest snapshot = ReplicaRequests.FullSnapshot(2, 40, 1, 1);
        replica.StageAuthority(in snapshot, out ReplicaStageHandle snapshotHandle, out _);
        replica.ObserveRuntimeOutcome(snapshotHandle, ReplicaRuntimeOutcome.CommittedOutcome(), out ReplicaCommittedMetadata watermark);

        for (int i = 0; i < 32; i++)
        {
            ulong from = watermark.Revision;
            ulong to = from + 1;
            ulong sequence = watermark.Sequence + 1;
            ReplicaStageRequest delta = ReplicaRequests.Delta(2, 40, from, to, sequence);
            ReplicaStageResult staged = replica.StageAuthority(in delta, out ReplicaStageHandle handle, out _);
            Assert.Equal(ReplicaStageStatus.Staged, staged.Status);
            replica.ObserveRuntimeOutcome(handle, ReplicaRuntimeOutcome.CommittedOutcome(), out ReplicaCommittedMetadata next);
            Assert.True(next.Revision >= watermark.Revision);
            Assert.True(next.Sequence >= watermark.Sequence);
            Assert.Equal(to, next.Revision);
            Assert.Equal(sequence, next.Sequence);
            watermark = next;
        }

        ReplicaStageResult duplicate = replica.StageAuthority(
            ReplicaRequests.Delta(2, 40, watermark.Revision - 1, watermark.Revision, watermark.Sequence),
            out ReplicaStageHandle duplicateHandle,
            out ReadOnlyMemory<byte> duplicatePlan);
        Assert.Equal(ReplicaStageStatus.DuplicateIgnored, duplicate.Status);
        Assert.True(duplicateHandle.IsEmpty);
        Assert.True(duplicatePlan.IsEmpty);
        Assert.Equal(watermark.Revision, replica.GetSnapshot().Committed.Revision);
        Assert.Equal(watermark.Sequence, replica.GetSnapshot().Committed.Sequence);
    }
}

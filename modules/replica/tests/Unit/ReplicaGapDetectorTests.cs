using Lumio.Client.Replica;

namespace Lumio.Client.Replica.Tests.Unit;

public sealed class ReplicaGapDetectorTests
{
    [Fact]
    public void DuplicateGapTombstone_MatchFixture()
    {
        var metadata = new ReplicaMetadataState();
        metadata.Reset(1);
        ReplicaStageRequest snapshot = ReplicaRequests.FullSnapshot(1, 10, 3, 3);
        metadata.ApplyCommitted(in snapshot);
        var tombstones = new TombstoneEvidence();
        tombstones.Replace(new ulong[] { 7 });
        var detector = new ReplicaGapDetector();

        ReplicaGapClassification duplicate = detector.Classify(
            ReplicaRequests.Delta(1, 10, 2, 3, 3),
            metadata,
            tombstones);
        ReplicaGapClassification gap = detector.Classify(
            ReplicaRequests.Delta(1, 10, 3, 5, 5),
            metadata,
            tombstones);
        ReplicaGapClassification tombstone = detector.Classify(
            ReplicaRequests.Delta(1, 10, 3, 4, 4, touched: new ulong[] { 7 }),
            metadata,
            tombstones);
        ReplicaGapClassification accepted = detector.Classify(
            ReplicaRequests.Delta(1, 10, 3, 4, 4, tombstones: new ulong[] { 8 }, touched: new ulong[] { 8 }),
            metadata,
            tombstones);
        ReplicaGapClassification unknownBaseline = detector.Classify(
            ReplicaRequests.Delta(1, 99, 3, 4, 4),
            metadata,
            tombstones);

        Assert.Equal(ReplicaGapClassification.Duplicate, duplicate);
        Assert.Equal(ReplicaGapClassification.Gap, gap);
        Assert.Equal(ReplicaGapClassification.TombstoneConflict, tombstone);
        Assert.Equal(ReplicaGapClassification.Accept, accepted);
        Assert.Equal(ReplicaGapClassification.BaselineMismatch, unknownBaseline);
    }
}

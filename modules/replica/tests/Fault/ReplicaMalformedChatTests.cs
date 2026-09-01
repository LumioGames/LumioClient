using Lumio.Client.Replica;
using Lumio.Client.Replica.Tests.Support;

namespace Lumio.Client.Replica.Tests.Fault;

public sealed class ReplicaMalformedChatTests
{
    [Fact]
    public void MalformedOrUnauthorizedEventsDoNotMutateReplicaWorldOrChatWindow()
    {
        ReplicaChatConsumer consumer = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Browser);
        ReplicaVisibleEntity bot = GameplayWireFixtures.Entity("101", "bot", "room-01", 1, 1, 0);
        Assert.True(GameplayWireFixtures.AdmitRoom(consumer.World, extras: new[] { bot }).Accepted);
        Assert.True(GameplayWireFixtures.CommitEmptySnapshot(consumer.Replica));
        ReplicaBindingLookup beforeBinding = consumer.World.SelfLookup();
        int beforeEntities = consumer.World.VisibleEntityCount;
        ReplicaCommittedMetadata beforeMeta = consumer.Replica.GetSnapshot().Committed;

        AssertRejected(consumer, ReplicaUpdateKind.FullSnapshot, GameplayWireFixtures.SnapshotWithChatEvent(), 2, 10, 0, 1);
        AssertRejected(consumer, ReplicaUpdateKind.Delta, GameplayWireFixtures.DeltaWithComponent(), 2, 10, 0, 1);
        AssertRejected(consumer, ReplicaUpdateKind.Delta, GameplayWireFixtures.BadHashDelta(), 2, 10, 0, 1);
        AssertRejected(consumer, ReplicaUpdateKind.Delta, GameplayWireFixtures.InputCommand(), 2, 10, 0, 1);
        AssertRejected(consumer, ReplicaUpdateKind.Delta, "{\"messageType\":\"Nope\"}", 2, 10, 0, 1);

        ReplicaBindingLookup afterBinding = consumer.World.SelfLookup();
        Assert.Equal(beforeBinding.Binding.NetEntityId, afterBinding.Binding.NetEntityId);
        Assert.Equal(beforeEntities, consumer.World.VisibleEntityCount);
        Assert.Empty(consumer.ChatWindow);
        ReplicaCommittedMetadata afterMeta = consumer.Replica.GetSnapshot().Committed;
        Assert.Equal(beforeMeta.Revision, afterMeta.Revision);
        Assert.Equal(beforeMeta.Sequence, afterMeta.Sequence);
        Assert.Equal(beforeMeta.HasBaseline, afterMeta.HasBaseline);
    }

    [Fact]
    public void RoomSequenceRegressionDoesNotAppend()
    {
        ReplicaChatConsumer consumer = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Bot);
        Assert.True(GameplayWireFixtures.AdmitRoom(
            consumer.World,
            extras: new[] { GameplayWireFixtures.Entity("101", "bot", "room-01", 1, 1, 0) }).Accepted);
        Assert.True(GameplayWireFixtures.CommitEmptySnapshot(consumer.Replica));
        Assert.True(GameplayWireFixtures.CommitJson(
            consumer.Replica,
            ReplicaUpdateKind.Delta,
            GameplayWireFixtures.ContractChatDelta(),
            2,
            10,
            0,
            1));
        Assert.Single(consumer.ChatWindow);

        (string payload, string sha) = GameplayWireFixtures.EncodeChatEvent(1, 1, 101, "dup", 8);
        ReplicaStageStatus staged = GameplayWireFixtures.StageJson(
            consumer.Replica,
            ReplicaUpdateKind.Delta,
            GameplayWireFixtures.ChatDelta(payload, sha, 8, 2),
            3,
            10,
            1,
            2,
            out _);
        Assert.Equal(ReplicaStageStatus.Rejected, staged);
        Assert.Single(consumer.ChatWindow);
        Assert.Equal("gg", consumer.ChatWindow[0].Text);
        Assert.Equal("bad_envelope", consumer.World.LastRejectCode);
    }

    [Fact]
    public void TombstonedSenderEventDoesNotAppend()
    {
        ReplicaChatConsumer consumer = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Browser);
        Assert.True(GameplayWireFixtures.AdmitRoom(
            consumer.World,
            extras: new[] { GameplayWireFixtures.Entity("101", "bot", "room-01", 1, 1, 0, tombstoned: true) }).Accepted);
        Assert.True(GameplayWireFixtures.CommitEmptySnapshot(consumer.Replica));
        ReplicaStageStatus staged = GameplayWireFixtures.StageJson(
            consumer.Replica,
            ReplicaUpdateKind.Delta,
            GameplayWireFixtures.ContractChatDelta(),
            2,
            10,
            0,
            1,
            out _);
        Assert.Equal(ReplicaStageStatus.Rejected, staged);
        Assert.Empty(consumer.ChatWindow);
        Assert.Equal("tombstoned", consumer.World.LastRejectCode);
        Assert.Equal(
            ReplicaQueryStatus.Tombstoned,
            consumer.World.QueryAttribute(new ReplicaAttributeQuery("client-replica", "room-01", "101", "EntityIdentity.entityType")).Status);
    }

    [Fact]
    public void InvisibleSenderEventDoesNotAppend()
    {
        ReplicaChatConsumer consumer = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Browser);
        Assert.True(GameplayWireFixtures.AdmitRoom(
            consumer.World,
            extras: new[] { GameplayWireFixtures.Entity("101", "bot", "room-01", 1, 1, 0, inAoi: false) }).Accepted);
        Assert.True(GameplayWireFixtures.CommitEmptySnapshot(consumer.Replica));
        ReplicaStageStatus staged = GameplayWireFixtures.StageJson(
            consumer.Replica,
            ReplicaUpdateKind.Delta,
            GameplayWireFixtures.ContractChatDelta(),
            2,
            10,
            0,
            1,
            out _);
        Assert.Equal(ReplicaStageStatus.Rejected, staged);
        Assert.Empty(consumer.ChatWindow);
        Assert.Equal("unauthorized", consumer.World.LastRejectCode);
    }

    private static void AssertRejected(
        ReplicaChatConsumer consumer,
        ReplicaUpdateKind kind,
        string json,
        ulong sequence,
        ulong baseline,
        ulong fromRevision,
        ulong toRevision)
    {
        ReplicaStageStatus status = GameplayWireFixtures.StageJson(
            consumer.Replica,
            kind,
            json,
            sequence,
            baseline,
            fromRevision,
            toRevision,
            out ReplicaStageHandle handle);
        Assert.Equal(ReplicaStageStatus.Rejected, status);
        Assert.True(handle.IsEmpty);
        Assert.Empty(consumer.ChatWindow);
    }
}

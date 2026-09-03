using Lumio.Client.Replica;
using Lumio.Client.Replica.Tests.Support;

namespace Lumio.Client.Replica.Tests.Unit;

public sealed class ReplicaFullSnapshotRebuildTests
{
    [Fact]
    public void ContractIdentityEncoderMatchesC1TwoLiveExample()
    {
        (string payload, string sha) = GameplayWireFixtures.EncodeIdentity(
            (101, "player", "a"),
            (102, "bot", "b"));
        Assert.Equal(GameplayWireFixtures.IdentityTwoLivePayload, payload);
        Assert.Equal(GameplayWireFixtures.IdentityTwoLiveSha256, sha);
    }

    [Fact]
    public void FullSnapshotRebuildsEntitySetFromIdentityRecordsNotAdmissionOrEmptyBlocks()
    {
        ReplicaChatConsumer consumer = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Browser);
        Assert.True(GameplayWireFixtures.AdmitRoom(
            consumer.World,
            extras: new[]
            {
                GameplayWireFixtures.Entity("999", "player", "room-01", 1, 1, 0),
                GameplayWireFixtures.Entity("101", "bot", "room-01", 1, 1, 0)
            }).Accepted);
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

        Assert.True(GameplayWireFixtures.CommitJson(
            consumer.Replica,
            ReplicaUpdateKind.FullSnapshot,
            GameplayWireFixtures.ContractIdentitySnapshot(),
            3,
            11,
            0,
            2));

        IReadOnlyList<ReplicaIdentityRecord> census = consumer.World.CopyIdentityRecords();
        Assert.Equal(2, census.Count);
        Assert.Equal(new[] { "101", "102" }, census.Select(r => r.NetEntityId).ToArray());
        Assert.Equal(new[] { "player", "bot" }, census.Select(r => r.EntityType).ToArray());
        Assert.Equal(new[] { string.Empty, string.Empty }, census.Select(r => r.UnmappedMark).ToArray());
        Assert.Equal(2, consumer.World.VisibleEntityCount);
        Assert.Empty(consumer.ChatWindow);
        Assert.True(consumer.World.InputEnabled);
        Assert.Equal(
            ReplicaQueryStatus.NonExistent,
            consumer.World.QueryAttribute(
                new ReplicaAttributeQuery("client-replica", "room-01", "999", "EntityIdentity.entityType")).Status);
        Assert.Equal(
            "player",
            consumer.World.QueryAttribute(
                new ReplicaAttributeQuery("client-replica", "room-01", "101", "EntityIdentity.entityType")).Value);
        Assert.Equal(
            string.Empty,
            consumer.World.QueryAttribute(
                new ReplicaAttributeQuery("client-replica", "room-01", "101", "EntityIdentity.unmappedMark")).Value);
        Assert.Equal(
            "bot",
            consumer.World.QueryAttribute(
                new ReplicaAttributeQuery("client-replica", "room-01", "102", "EntityIdentity.entityType")).Value);
    }

    [Fact]
    public void FullSnapshotOnNewGenerationDropsOldGenerationEntities()
    {
        ReplicaChatConsumer consumer = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Bot);
        Assert.True(GameplayWireFixtures.AdmitRoom(
            consumer.World,
            extras: new[] { GameplayWireFixtures.Entity("101", "player", "room-01", 1, 1, 0) }).Accepted);
        Assert.True(GameplayWireFixtures.CommitJson(
            consumer.Replica,
            ReplicaUpdateKind.FullSnapshot,
            GameplayWireFixtures.ContractIdentitySnapshot(),
            1,
            10,
            0,
            1));
        Assert.Equal(2, consumer.World.CopyIdentityRecords().Count);

        Assert.True(consumer.Replica.ResetForNewSession(new ReplicaResetRequest(2)).Succeeded);
        Assert.Equal(0, consumer.World.VisibleEntityCount);
        Assert.False(consumer.World.SelfLookup().Found);
        Assert.False(consumer.World.InputEnabled);

        Assert.True(GameplayWireFixtures.AdmitRoom(consumer.World, "101", "player").Accepted);
        Assert.True(GameplayWireFixtures.CommitJson(
            consumer.Replica,
            ReplicaUpdateKind.FullSnapshot,
            GameplayWireFixtures.ContractIdentitySnapshot(),
            1,
            20,
            0,
            1,
            2));

        Assert.Equal(2, consumer.World.CopyIdentityRecords().Count);
        Assert.Equal(
            ReplicaQueryStatus.StaleGeneration,
            consumer.World.QueryAttribute(
                new ReplicaAttributeQuery(
                    "client-replica",
                    "room-01",
                    "101",
                    "EntityIdentity.entityType",
                    1,
                    true,
                    string.Empty,
                    false)).Status);
        Assert.Equal(
            ReplicaQueryStatus.Ok,
            consumer.World.QueryAttribute(
                new ReplicaAttributeQuery("client-replica", "room-01", "101", "EntityIdentity.entityType")).Status);
        Assert.Empty(consumer.ChatWindow);
        Assert.True(consumer.World.InputEnabled);
    }

    [Fact]
    public void UnsortedIdentityRecordsAreRejected()
    {
        ReplicaChatConsumer consumer = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Browser);
        Assert.True(GameplayWireFixtures.AdmitRoom(consumer.World).Accepted);
        (string payload, string sha) = GameplayWireFixtures.EncodeIdentity(
            (102, "bot", "b"),
            (101, "player", "a"));
        ReplicaStageStatus staged = GameplayWireFixtures.StageJson(
            consumer.Replica,
            ReplicaUpdateKind.FullSnapshot,
            GameplayWireFixtures.IdentitySnapshot(payload, sha),
            1,
            10,
            0,
            0,
            out _);
        Assert.Equal(ReplicaStageStatus.Rejected, staged);
        Assert.Equal("block_order_violation", consumer.World.LastRejectCode);
    }

    [Fact]
    public void IllegalIdentityEntityTypeIsRejected()
    {
        ReplicaChatConsumer consumer = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Bot);
        Assert.True(GameplayWireFixtures.AdmitRoom(consumer.World).Accepted);
        (string payload, string sha) = GameplayWireFixtures.EncodeIdentity((101, "npc", "a"));
        ReplicaStageStatus staged = GameplayWireFixtures.StageJson(
            consumer.Replica,
            ReplicaUpdateKind.FullSnapshot,
            GameplayWireFixtures.IdentitySnapshot(payload, sha),
            1,
            10,
            0,
            0,
            out _);
        Assert.Equal(ReplicaStageStatus.Rejected, staged);
        Assert.Equal("undecodable_payload", consumer.World.LastRejectCode);
    }

    [Fact]
    public void EmptyStateBlocksAreZeroLiveCensusNotAPlaceholder()
    {
        ReplicaChatConsumer consumer = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Browser);
        Assert.True(GameplayWireFixtures.AdmitRoom(
            consumer.World,
            extras: new[] { GameplayWireFixtures.Entity("101", "bot", "room-01", 1, 1, 0) }).Accepted);
        Assert.True(GameplayWireFixtures.CommitEmptySnapshot(consumer.Replica));
        Assert.Empty(consumer.World.CopyIdentityRecords());
        Assert.Equal(0, consumer.World.VisibleEntityCount);
        Assert.True(consumer.World.SelfLookup().Found);
        Assert.True(consumer.World.InputEnabled);
    }
}

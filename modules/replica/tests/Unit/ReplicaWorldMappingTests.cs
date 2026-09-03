using Lumio.Client.Replica;
using Lumio.Client.Replica.Tests.Support;

namespace Lumio.Client.Replica.Tests.Unit;

public sealed class ReplicaWorldMappingTests
{
    [Fact]
    public void EachClientOwnsAnIndependentReplicaWorld()
    {
        ReplicaChatConsumer browser = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Browser);
        ReplicaChatConsumer bot = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Bot);
        Assert.NotSame(browser.Replica, bot.Replica);
        Assert.NotSame(browser.World, bot.World);
        Assert.True(GameplayWireFixtures.AdmitRoom(browser.World).Accepted);
        Assert.True(GameplayWireFixtures.AdmitRoom(bot.World, "2", "bot").Accepted);
        Assert.Equal("1", browser.World.SelfLookup().Binding.NetEntityId);
        Assert.Equal("2", bot.World.SelfLookup().Binding.NetEntityId);
        Assert.Equal("bot", bot.World.SelfLookup().Binding.EntityType);
    }

    [Fact]
    public void ClientQueryReadsOnlyReplicatedVisibleAttributes()
    {
        ReplicaChatConsumer browser = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Browser);
        Assert.True(GameplayWireFixtures.AdmitRoom(
            browser.World,
            extras: new[] { GameplayWireFixtures.Entity("101", "bot", "room-01", 1, 4, 7) }).Accepted);

        ReplicaAttributeQueryResult type = browser.World.QueryAttribute(
            new ReplicaAttributeQuery("client-replica", "room-01", "101", "EntityIdentity.entityType"));
        Assert.Equal(ReplicaQueryStatus.Ok, type.Status);
        Assert.Equal("bot", type.Value);

        ReplicaAttributeQueryResult persistOnly = browser.World.QueryAttribute(
            new ReplicaAttributeQuery("client-replica", "room-01", "101", "ChatComponent.lastMessagePersistOnly"));
        Assert.Equal(ReplicaQueryStatus.Invisible, persistOnly.Status);
        Assert.Equal(string.Empty, persistOnly.Value);
    }

    [Fact]
    public void ForbiddenBindingShapeDoesNotMutateWorld()
    {
        ReplicaChatConsumer consumer = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Browser);
        var admission = new ReplicaAdmission(
            new ReplicaBinding("acct-07", "room-01", "1", "player", 1),
            new[] { GameplayWireFixtures.Entity("1", "player", "room-01", 1, 1, 0) },
            hasClaim: false,
            hasForbiddenAccountEntityRef: true);
        ReplicaAdmissionResult result = consumer.World.InstallAdmission(in admission);
        Assert.False(result.Accepted);
        Assert.Equal("invalid_binding_shape", result.RejectCode);
        Assert.False(consumer.World.SelfLookup().Found);
        Assert.Equal(0, consumer.World.VisibleEntityCount);
    }

    [Fact]
    public void EmptyFullSnapshotCommitsThroughAuthorityTransaction()
    {
        ReplicaChatConsumer consumer = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Browser);
        Assert.True(GameplayWireFixtures.AdmitRoom(consumer.World).Accepted);
        Assert.True(GameplayWireFixtures.CommitEmptySnapshot(consumer.Replica));
        ReplicaCommittedMetadata committed = consumer.Replica.GetSnapshot().Committed;
        Assert.True(committed.HasBaseline);
        Assert.Equal(10UL, committed.Baseline);
        Assert.Equal(1UL, committed.Sequence);
        Assert.True(consumer.World.SelfLookup().Found);
    }
}

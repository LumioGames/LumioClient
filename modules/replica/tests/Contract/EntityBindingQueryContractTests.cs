using System.Text.Json;
using Lumio.Client.Replica;
using Lumio.Client.Replica.Tests.Support;

namespace Lumio.Client.Replica.Tests.Contract;

public sealed class EntityBindingQueryContractTests
{
    [Fact]
    public void FrozenC2ContractDeclaresBindingQuintupleAndFiveOutcomes()
    {
        using JsonDocument document = LoadBinding();
        JsonElement root = document.RootElement;
        Assert.Equal("lumio.entity-binding-query.v1", root.GetProperty("contractId").GetString());
        JsonElement required = root.GetProperty("binding").GetProperty("record").GetProperty("required");
        Assert.Equal(new[] { "accountId", "roomId", "netEntityId", "entityType", "connectionGeneration" }, required.EnumerateObject().Select(p => p.Name).ToArray());
        string[] outcomes = root.GetProperty("errorCodes").GetProperty("outcomeCodes").EnumerateArray().Select(e => e.GetString()!).ToArray();
        Assert.Equal(new[] { "non_existent", "stale_generation", "invisible", "unauthorized", "tombstoned" }, outcomes);
        Assert.Equal("const:client-replica", root.GetProperty("binding").GetProperty("operations").GetProperty("selfLookup").GetProperty("request").GetProperty("required").GetProperty("callerScope").GetString());
    }

    [Fact]
    public void ClientReplicaQueryMatchesC2InvalidCaseMatrix()
    {
        ReplicaChatConsumer consumer = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Browser);
        ReplicaVisibleEntity bot = GameplayWireFixtures.Entity("101", "bot", "room-01", 1, 1, 0);
        ReplicaVisibleEntity otherRoom = GameplayWireFixtures.Entity("N7", "player", "room-02", 1, 1, 0);
        ReplicaVisibleEntity outOfAoi = GameplayWireFixtures.Entity("N5", "player", "room-01", 1, 1, 0, inAoi: false);
        ReplicaVisibleEntity tombstoned = GameplayWireFixtures.Entity("201", "player", "room-01", 1, 1, 0, tombstoned: true);
        Assert.True(consumer.World.InstallAdmission(
            new ReplicaAdmission(
                new ReplicaBinding("acct-07", "room-01", "1", "player", 1),
                new[]
                {
                    GameplayWireFixtures.Entity("1", "player", "room-01", 1, 1, 0),
                    bot,
                    otherRoom,
                    outOfAoi,
                    tombstoned,
                    new ReplicaVisibleEntity(
                        "301",
                        "player",
                        "room-01",
                        1,
                        1,
                        0,
                        new[] { new ReplicaAttributeValue("EntityIdentity.claimedMark", "secret") },
                        true,
                        false)
                })).Accepted);

        AssertRequestError(
            consumer.World.QueryAttribute(new ReplicaAttributeQuery("client-replica", "room-01", "1", "last message text")),
            "invalid_attribute_id");
        AssertRequestError(
            consumer.World.QueryAttribute(new ReplicaAttributeQuery("client-replica", "room-01", "1", "SELECT * FROM entities")),
            "invalid_attribute_id");
        AssertRequestError(
            consumer.World.QueryAttribute(new ReplicaAttributeQuery("client-replica", "room-01", "1", "Storage.tables.entity_row(42)")),
            "storage_access_forbidden");
        AssertRequestError(
            consumer.World.QueryAttribute(new ReplicaAttributeQuery("client-replica", "room-01", "1", "ecs/tables/entity_row")),
            "storage_access_forbidden");
        AssertRequestError(
            consumer.World.QueryAttribute(new ReplicaAttributeQuery("client-replica", "room-01", "1", "ChatComponent.notDeclared")),
            "undeclared_attribute");
        AssertRequestError(
            consumer.World.QueryAttribute(new ReplicaAttributeQuery("client-replica", "room-01", "N7", "EntityIdentity.entityType")),
            "cross_room_reference");
        Assert.Equal(
            ReplicaQueryStatus.Invisible,
            consumer.World.QueryAttribute(new ReplicaAttributeQuery("client-replica", "room-01", "1", "ChatComponent.lastMessagePersistOnly")).Status);
        AssertRequestError(
            consumer.World.QueryAttribute(new ReplicaAttributeQuery(
                "server-authoritative",
                "room-01",
                "1",
                "EntityIdentity.entityType",
                0,
                false,
                "client-connection",
                false)),
            "scope_violation");
        AssertRequestError(
            consumer.World.QueryAttribute(new ReplicaAttributeQuery(
                "server-authoritative",
                "room-01",
                "1",
                "Storage.tables.entity_row(42)",
                0,
                false,
                "client-connection",
                true)),
            "invalid_binding_shape");
        Assert.Equal(
            ReplicaQueryStatus.NonExistent,
            consumer.World.QueryAttribute(new ReplicaAttributeQuery("client-replica", "room-01", "N5", "EntityIdentity.entityType")).Status);
        Assert.Equal(
            ReplicaQueryStatus.Tombstoned,
            consumer.World.QueryAttribute(new ReplicaAttributeQuery("client-replica", "room-01", "201", "EntityIdentity.entityType")).Status);
        Assert.Equal(
            ReplicaQueryStatus.Unauthorized,
            consumer.World.QueryAttribute(new ReplicaAttributeQuery("client-replica", "room-01", "301", "EntityIdentity.claimedMark")).Status);
        Assert.Equal(
            ReplicaQueryStatus.NonExistent,
            consumer.World.QueryAttribute(new ReplicaAttributeQuery("client-replica", "room-01", "N9", "EntityIdentity.entityType")).Status);
        Assert.Equal(
            ReplicaQueryStatus.StaleGeneration,
            consumer.World.QueryAttribute(new ReplicaAttributeQuery("client-replica", "room-01", "1", "EntityIdentity.entityType", 0, true, string.Empty, false)).Status);
    }

    [Fact]
    public void SelfLookupReturnsAdmittedQuintuple()
    {
        ReplicaChatConsumer consumer = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Bot);
        Assert.True(GameplayWireFixtures.AdmitRoom(consumer.World, "1", "player").Accepted);
        ReplicaBindingLookup lookup = consumer.World.SelfLookup();
        Assert.True(lookup.Found);
        Assert.Equal("1", lookup.Binding.NetEntityId);
        Assert.Equal("player", lookup.Binding.EntityType);
        Assert.Equal(1UL, lookup.Binding.ConnectionGeneration);
        Assert.Equal("room-01", lookup.Binding.RoomId);
        Assert.Equal("acct-07", lookup.Binding.AccountId);
    }

    private static void AssertRequestError(ReplicaAttributeQueryResult result, string code)
    {
        Assert.Equal(ReplicaQueryStatus.RequestError, result.Status);
        Assert.Equal(code, result.Code);
        Assert.Equal(string.Empty, result.Value);
    }

    private static JsonDocument LoadBinding()
    {
        string? path = WireContractLocator.LocateEntityBinding();
        if (path is null)
        {
            throw Xunit.Sdk.SkipException.ForSkip(
                "entity-binding-and-query-v1.json not found; need architecture origin/main 2b7e321 or LUMIO_ENTITY_BINDING_CONTRACT.");
        }

        return JsonDocument.Parse(File.ReadAllText(path));
    }
}

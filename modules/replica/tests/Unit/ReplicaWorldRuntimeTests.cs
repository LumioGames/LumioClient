using Lumio.Client.Replica;
using Lumio.Client.Replica.Tests.Support;
using Lumio.GameRuntime.Ecs;
using Lumio.GameRuntime.Samples.Username.Components.Identity;
using Lumio.GameRuntime.Samples.Username.EntityTypes;

namespace Lumio.Client.Replica.Tests.Unit;

public sealed class ReplicaWorldRuntimeTests
{
    [Fact]
    public void CreateRecordRunsAwakePostAttributeStart()
    {
        ReplicaChatConsumer consumer = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Browser);
        WorldManager manager = consumer.World.Manager;
        ulong instance = manager.World.InstanceId;
        var worldEntity = new NetEntityId(instance, 1);
        var player = new NetEntityId(instance, 2);
        manager.Enqueue(new WelcomeMessage(instance, player, "self"));
        manager.Enqueue(new WorldChangeMessage(
            1UL,
            new[]
            {
                new CreateRecord("WorldEntity", worldEntity, Array.Empty<FieldValue>()),
                new CreateRecord("PlayerEntity", player, Array.Empty<FieldValue>())
            },
            Array.Empty<FieldChange>(),
            Array.Empty<NetEntityId>(),
            Array.Empty<ClientRpcRecord>()));
        manager.Tick();

        Assert.Equal(new[] { "Awake", "PostAttribute", "Start" }, manager.World.LifecycleOf(player).ToArray());
        Assert.True(manager.World.TypeOf(player).Is<PlayerEntity>());
        Assert.Equal(player, manager.World.Self.Id);
    }

    [Fact]
    public void NonC1FullSnapshotIsBadEnvelopeAndDoesNotEnableInput()
    {
        ReplicaChatConsumer consumer = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Browser);
        Assert.True(GameplayWireFixtures.AdmitRoom(consumer.World).Accepted);
        ReplicaStageStatus staged = GameplayWireFixtures.StageJson(
            consumer.Replica,
            ReplicaUpdateKind.FullSnapshot,
            "not-json",
            1,
            10,
            0,
            0,
            out _);
        Assert.Equal(ReplicaStageStatus.Rejected, staged);
        Assert.Equal("bad_envelope", consumer.World.LastRejectCode);
        Assert.False(consumer.World.InputEnabled);

        staged = consumer.Replica.StageAuthority(
            new ReplicaStageRequest(
                1,
                ReplicaUpdateKind.FullSnapshot,
                10,
                0,
                0,
                1,
                new byte[] { 0x10, 0x32, 0x54, 0x76, 0x98, 0xBA, 0xDC, 0xFE },
                Array.Empty<ulong>(),
                Array.Empty<ulong>()),
            out _,
            out _).Status;
        Assert.Equal(ReplicaStageStatus.Rejected, staged);
        Assert.Equal("bad_envelope", consumer.World.LastRejectCode);
        Assert.False(consumer.World.InputEnabled);
    }

    [Fact]
    public void MissingStateBlocksIsBadEnvelope()
    {
        ReplicaChatConsumer consumer = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Browser);
        ReplicaStageStatus staged = GameplayWireFixtures.StageJson(
            consumer.Replica,
            ReplicaUpdateKind.FullSnapshot,
            "{\"messageType\":\"FullSnapshot\",\"tickId\":0,\"revision\":0}",
            1,
            10,
            0,
            0,
            out _);
        Assert.Equal(ReplicaStageStatus.Rejected, staged);
        Assert.Equal("bad_envelope", consumer.World.LastRejectCode);
    }

    [Fact]
    public void OwnerNameWriteProducesFieldWriteOutbound()
    {
        ReplicaChatConsumer consumer = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Browser);
        Assert.True(GameplayWireFixtures.AdmitRoom(consumer.World).Accepted);
        World world = consumer.World.Manager.World;
        world.Self.Get<IdentityComponent>().Name.Value = "ABCD";
        IReadOnlyList<WorldMessage> outbound = consumer.World.DrainOutbound();
        Assert.Contains(outbound, message => message is InputCommandMessage input
            && string.Equals(input.MappingId, "field.write", StringComparison.Ordinal));
    }

    [Fact]
    public void ProductionSourcesHaveNoAttributeBag()
    {
        string repo = RepoRoot();
        string[] roots =
        {
            Path.Combine(repo, "modules", "replica", "src"),
            Path.Combine(repo, "modules", "bot", "src")
        };
        var hits = new List<string>();
        foreach (string root in roots)
        {
            foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                if (text.Contains("Dictionary<string, string> Attributes", StringComparison.Ordinal)
                    || text.Contains("class AttributeDeclarationTable", StringComparison.Ordinal)
                    || text.Contains("RebuildFromIdentity", StringComparison.Ordinal))
                {
                    hits.Add(file);
                }
            }
        }

        Assert.Empty(hits);
    }

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LumioClient.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("repo root not found");
    }
}

using System.Text;
using Lumio.Client.Replica;
using Lumio.Client.Replica.Tests.Support;

namespace Lumio.Client.Replica.Tests.Unit;

public sealed class ReplicaConnectionSupersededTests
{
    [Fact]
    public void ConnectionSupersededStopsInputRecordsReasonAndDoesNotReconnect()
    {
        ReplicaChatConsumer consumer = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Bot);
        Assert.True(GameplayWireFixtures.AdmitRoom(consumer.World).Accepted);
        Assert.True(GameplayWireFixtures.CommitJson(
            consumer.Replica,
            ReplicaUpdateKind.FullSnapshot,
            GameplayWireFixtures.ContractIdentitySnapshot(),
            1,
            10,
            0,
            1));
        Assert.True(consumer.World.InputEnabled);
        int census = consumer.World.CopyIdentityRecords().Count;

        byte[] utf8 = Encoding.UTF8.GetBytes(GameplayWireFixtures.ConnectionSupersededNotice(101, 2));
        Assert.True(consumer.Replica.TryObserveConnectionSuperseded(utf8, out ReplicaConnectionSuperseded notice));
        Assert.True(notice.Received);
        Assert.Equal("connection_superseded", notice.ReasonCode);
        Assert.Equal("101", notice.NetEntityId);
        Assert.Equal(2UL, notice.NewConnectionGeneration);
        Assert.False(consumer.World.InputEnabled);
        Assert.Equal("connection_superseded", consumer.World.LastConnectionSuperseded.ReasonCode);
        Assert.Equal(census, consumer.World.CopyIdentityRecords().Count);
        Assert.True(consumer.World.SelfLookup().Found);
        Assert.False(consumer.Replica.GetSnapshot().Committed.Frozen);

        Assert.True(consumer.Replica.TryObserveConnectionSuperseded(utf8, out _));
        Assert.False(consumer.World.InputEnabled);
        Assert.Equal(1UL, consumer.Replica.GetSnapshot().Committed.Generation);
    }

    [Fact]
    public void MalformedConnectionSupersededDoesNotStopInput()
    {
        ReplicaChatConsumer consumer = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Browser);
        Assert.True(GameplayWireFixtures.AdmitRoom(consumer.World).Accepted);
        Assert.True(GameplayWireFixtures.CommitEmptySnapshot(consumer.Replica));
        Assert.True(consumer.World.InputEnabled);

        byte[] utf8 = Encoding.UTF8.GetBytes("{\"messageType\":\"ConnectionSuperseded\",\"reasonCode\":\"other\"}");
        Assert.False(consumer.Replica.TryObserveConnectionSuperseded(utf8, out ReplicaConnectionSuperseded notice));
        Assert.False(notice.Received);
        Assert.True(consumer.World.InputEnabled);
    }
}

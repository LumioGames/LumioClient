using Lumio.Client.Replica;
using Lumio.Client.Replica.Tests.Support;

namespace Lumio.Client.Replica.Tests.Unit;

public sealed class ReplicaChatPresentationTests
{
    [Fact]
    public void BrowserDisplaysBotEntitySenderNetEntityIdAndText()
    {
        ReplicaChatConsumer browser = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Browser);
        Assert.Equal(ReplicaClientKind.Browser, browser.Kind);
        Assert.True(GameplayWireFixtures.AdmitRoom(
            browser.World,
            extras: new[] { GameplayWireFixtures.Entity("101", "bot", "room-01", 1, 1, 0) }).Accepted);
        Assert.True(GameplayWireFixtures.CommitEmptySnapshot(browser.Replica));
        Assert.True(GameplayWireFixtures.CommitJson(
            browser.Replica,
            ReplicaUpdateKind.Delta,
            GameplayWireFixtures.ContractChatDelta(),
            2,
            10,
            0,
            1));

        IReadOnlyList<ReplicaChatLine> window = browser.ChatWindow;
        Assert.Single(window);
        Assert.Equal(1UL, window[0].MessageId);
        Assert.Equal(1UL, window[0].RoomSequence);
        Assert.Equal("101", window[0].SenderNetEntityId);
        Assert.Equal("gg", window[0].Text);
        Assert.Equal(7UL, window[0].AppliedTick);
        ReplicaAttributeQueryResult senderType = browser.World.QueryAttribute(
            new ReplicaAttributeQuery("client-replica", "room-01", window[0].SenderNetEntityId, "EntityIdentity.entityType"));
        Assert.Equal(ReplicaQueryStatus.Ok, senderType.Status);
        Assert.Equal("bot", senderType.Value);
    }

    [Fact]
    public void TwoClientsReceiveIdenticalMessageIdAndRoomSequenceWithoutSharedReferences()
    {
        ReplicaChatConsumer browser = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Browser);
        ReplicaChatConsumer bot = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Bot);
        ReplicaVisibleEntity sender = GameplayWireFixtures.Entity("101", "bot", "room-01", 1, 1, 0);
        Assert.True(GameplayWireFixtures.AdmitRoom(browser.World, extras: new[] { sender }).Accepted);
        Assert.True(GameplayWireFixtures.AdmitRoom(bot.World, "2", "player", extras: new[] { sender }).Accepted);
        Assert.True(GameplayWireFixtures.CommitEmptySnapshot(browser.Replica));
        Assert.True(GameplayWireFixtures.CommitEmptySnapshot(bot.Replica));

        (string payload2, string sha2) = GameplayWireFixtures.EncodeChatEvent(2, 2, 101, "hi", 8);
        string first = GameplayWireFixtures.ContractChatDelta();
        string second = GameplayWireFixtures.ChatDelta(payload2, sha2, 8, 2);

        Assert.True(GameplayWireFixtures.CommitJson(browser.Replica, ReplicaUpdateKind.Delta, first, 2, 10, 0, 1));
        Assert.True(GameplayWireFixtures.CommitJson(bot.Replica, ReplicaUpdateKind.Delta, first, 2, 10, 0, 1));
        Assert.True(GameplayWireFixtures.CommitJson(browser.Replica, ReplicaUpdateKind.Delta, second, 3, 10, 1, 2));
        Assert.True(GameplayWireFixtures.CommitJson(bot.Replica, ReplicaUpdateKind.Delta, second, 3, 10, 1, 2));

        IReadOnlyList<ReplicaChatLine> browserWindow = browser.ChatWindow;
        IReadOnlyList<ReplicaChatLine> botWindow = bot.ChatWindow;
        Assert.NotSame(browser.Replica, bot.Replica);
        Assert.NotSame(browser.World, bot.World);
        Assert.NotSame(browserWindow, botWindow);
        Assert.Equal(2, browserWindow.Count);
        Assert.Equal(2, botWindow.Count);
        Assert.Equal(
            browserWindow.Select(line => (line.MessageId, line.RoomSequence)).ToArray(),
            botWindow.Select(line => (line.MessageId, line.RoomSequence)).ToArray());
        Assert.Equal("gg", browserWindow[0].Text);
        Assert.Equal("hi", botWindow[1].Text);
        Assert.Equal("101", browserWindow[0].SenderNetEntityId);
        Assert.Equal("101", botWindow[1].SenderNetEntityId);
    }

    [Fact]
    public void TwoClientsReceiveIdenticalChatStreamWhenSenderAdmissionAndAoiDiverge()
    {
        ReplicaChatConsumer browser = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Browser);
        ReplicaChatConsumer botOutOfAoi = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Bot);
        ReplicaChatConsumer botWithoutSender = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Bot);
        ReplicaVisibleEntity inAoiSender = GameplayWireFixtures.Entity("101", "bot", "room-01", 1, 1, 0);
        ReplicaVisibleEntity outOfAoiSender = GameplayWireFixtures.Entity("101", "bot", "room-01", 1, 1, 0, inAoi: false);
        Assert.True(GameplayWireFixtures.AdmitRoom(browser.World, extras: new[] { inAoiSender }).Accepted);
        Assert.True(GameplayWireFixtures.AdmitRoom(botOutOfAoi.World, "2", "player", extras: new[] { outOfAoiSender }).Accepted);
        Assert.True(GameplayWireFixtures.AdmitRoom(botWithoutSender.World, "3", "player").Accepted);
        Assert.True(GameplayWireFixtures.CommitEmptySnapshot(browser.Replica));
        Assert.True(GameplayWireFixtures.CommitEmptySnapshot(botOutOfAoi.Replica));
        Assert.True(GameplayWireFixtures.CommitEmptySnapshot(botWithoutSender.Replica));

        (string payload2, string sha2) = GameplayWireFixtures.EncodeChatEvent(2, 2, 101, "hi", 8);
        string first = GameplayWireFixtures.ContractChatDelta();
        string second = GameplayWireFixtures.ChatDelta(payload2, sha2, 8, 2);

        Assert.True(GameplayWireFixtures.CommitJson(browser.Replica, ReplicaUpdateKind.Delta, first, 2, 10, 0, 1));
        Assert.True(GameplayWireFixtures.CommitJson(botOutOfAoi.Replica, ReplicaUpdateKind.Delta, first, 2, 10, 0, 1));
        Assert.True(GameplayWireFixtures.CommitJson(botWithoutSender.Replica, ReplicaUpdateKind.Delta, first, 2, 10, 0, 1));
        Assert.True(GameplayWireFixtures.CommitJson(browser.Replica, ReplicaUpdateKind.Delta, second, 3, 10, 1, 2));
        Assert.True(GameplayWireFixtures.CommitJson(botOutOfAoi.Replica, ReplicaUpdateKind.Delta, second, 3, 10, 1, 2));
        Assert.True(GameplayWireFixtures.CommitJson(botWithoutSender.Replica, ReplicaUpdateKind.Delta, second, 3, 10, 1, 2));

        IReadOnlyList<ReplicaChatLine> browserWindow = browser.ChatWindow;
        IReadOnlyList<ReplicaChatLine> outOfAoiWindow = botOutOfAoi.ChatWindow;
        IReadOnlyList<ReplicaChatLine> missingSenderWindow = botWithoutSender.ChatWindow;
        Assert.NotSame(browser.Replica, botOutOfAoi.Replica);
        Assert.NotSame(browser.World, botWithoutSender.World);
        Assert.Equal(2, browserWindow.Count);
        Assert.Equal(
            browserWindow.Select(line => (line.MessageId, line.RoomSequence)).ToArray(),
            outOfAoiWindow.Select(line => (line.MessageId, line.RoomSequence)).ToArray());
        Assert.Equal(
            browserWindow.Select(line => (line.MessageId, line.RoomSequence)).ToArray(),
            missingSenderWindow.Select(line => (line.MessageId, line.RoomSequence)).ToArray());
        Assert.Equal("101", outOfAoiWindow[0].SenderNetEntityId);
        Assert.Equal("101", missingSenderWindow[1].SenderNetEntityId);
        Assert.Equal(
            ReplicaQueryStatus.Ok,
            browser.World.QueryAttribute(
                new ReplicaAttributeQuery("client-replica", "room-01", "101", "EntityIdentity.entityType")).Status);
        Assert.Equal(
            ReplicaQueryStatus.Invisible,
            botOutOfAoi.World.QueryAttribute(
                new ReplicaAttributeQuery("client-replica", "room-01", "101", "EntityIdentity.entityType")).Status);
        Assert.Equal(
            ReplicaQueryStatus.NonExistent,
            botWithoutSender.World.QueryAttribute(
                new ReplicaAttributeQuery("client-replica", "room-01", "101", "EntityIdentity.entityType")).Status);
    }

    [Fact]
    public void FullSnapshotClearsChatWindowAndDoesNotRestoreHistory()
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

        Assert.True(GameplayWireFixtures.CommitJson(
            consumer.Replica,
            ReplicaUpdateKind.FullSnapshot,
            GameplayWireFixtures.EmptySnapshot(),
            3,
            11,
            0,
            2));
        Assert.Empty(consumer.ChatWindow);
        Assert.True(consumer.World.SelfLookup().Found);
    }

    [Fact]
    public void AbortedDeltaDoesNotAppendChat()
    {
        ReplicaChatConsumer consumer = GameplayWireFixtures.CreateConsumer(ReplicaClientKind.Browser);
        Assert.True(GameplayWireFixtures.AdmitRoom(
            consumer.World,
            extras: new[] { GameplayWireFixtures.Entity("101", "bot", "room-01", 1, 1, 0) }).Accepted);
        Assert.True(GameplayWireFixtures.CommitEmptySnapshot(consumer.Replica));
        ReplicaStageStatus staged = GameplayWireFixtures.StageJson(
            consumer.Replica,
            ReplicaUpdateKind.Delta,
            GameplayWireFixtures.ContractChatDelta(),
            2,
            10,
            0,
            1,
            out ReplicaStageHandle handle);
        Assert.Equal(ReplicaStageStatus.Staged, staged);
        Assert.Equal(
            ReplicaOutcomeStatus.Aborted,
            consumer.Replica.ObserveRuntimeOutcome(handle, ReplicaRuntimeOutcome.AbortedOutcome(), out _));
        Assert.Empty(consumer.ChatWindow);
    }
}

using Lumio.Client.Bot;
using Lumio.Client.Bot.Host;
using Lumio.Client.Bot.Tests.Support;
using Lumio.Client.Connection;
using Lumio.GameRuntime.Ecs;
using Lumio.Client.Handshake;
using Lumio.Client.Input;
using Lumio.Client.Observability;
using Lumio.Client.Persistence;
using Lumio.Client.Prediction;
using Lumio.Client.Replica;
using Lumio.Client.Session;

namespace Lumio.Client.Bot.Tests.Unit;

public sealed class BotCadenceTests
{
    [Fact]
    public void ClientTimerManagerFiresFiveTenFifteenOnTickFrameAdvance()
    {
        var abi = new C4TickFrameAbi();
        using var timer = new ClientTimerManager(abi);
        Assert.True(timer.ScheduleBotChatCadence());
        IReadOnlyList<ulong> dues = timer.Advance(15);
        Assert.Equal(new ulong[] { 5, 10, 15 }, dues.ToArray());
        Assert.Equal(new ulong[] { 5, 10, 15 }, timer.Trace.UtteranceTicks.ToArray());
    }

    [Fact]
    public async Task HeadlessBotHostSubmitsChatInputOnCadenceTicksOnly()
    {
        var abi = new C4TickFrameAbi();
        var timer = new ClientTimerManager(abi);
        var order = new List<string>();
        var host = new HeadlessBotHost(
            new RecordingSession(order),
            new RecordingDriver(order),
            new RecordingIngress(order),
            new NullHook(),
            timer);
        int code = await host.RunAsync(new BotRunRequest(15, 0), CancellationToken.None);
        Assert.Equal(0, code);
        Assert.Equal(new ulong[] { 5, 10, 15 }, timer.Trace.UtteranceTicks.ToArray());
        Assert.Equal(3, order.Count(item => item == "fill"));
        Assert.Equal(host.SubmittedTicks.ToArray(), new ulong[] { 5, 10, 15 });
    }

    [Fact]
    public async Task ConnectionSupersededStopsChatInputWithoutReconnect()
    {
        var abi = new C4TickFrameAbi();
        var timer = new ClientTimerManager(abi);
        var host = new HeadlessBotHost(
            new RecordingSession(new List<string>()),
            new RecordingDriver(new List<string>()),
            new RecordingIngress(new List<string>()),
            new NullHook(),
            timer);
        host.StopInput("connection_superseded");
        int code = await host.RunAsync(new BotRunRequest(15, 0), CancellationToken.None);
        Assert.Equal(0, code);
        Assert.Equal("connection_superseded", host.InputStopReason);
        Assert.Empty(host.SubmittedTicks);
        Assert.False(host.Reconnected);
    }

    [Fact]
    public async Task HeadlessBotHostSendsChatOnCadenceAfterCreateRecord()
    {
        IClientReplica replica = new ClientReplicaFactory().Create();
        replica.ResetForNewSession(new ReplicaResetRequest(1));
        IReplicaWorld world = replica.World;
        var admission = new ReplicaAdmission(
            new ReplicaBinding("acct-07", "room-01", "1", "player", 1),
            new[]
            {
                new ReplicaVisibleEntity("1", "player", "room-01", 1, 1, 0, Array.Empty<ReplicaAttributeValue>(), true, false)
            });
        Assert.True(world.InstallAdmission(in admission).Accepted);
        Assert.True(CommitEmptySnapshot(replica));
        NetEntityId self = world.Manager.World.Self.Id;
        world.Manager.Enqueue(new WorldChangeMessage(
            1UL,
            new[] { new CreateRecord("player", self, Array.Empty<FieldValue>()) },
            Array.Empty<FieldChange>(),
            Array.Empty<NetEntityId>(),
            Array.Empty<ClientRpcRecord>()));
        world.Manager.Tick();
        Assert.True(world.InputEnabled);
        Assert.True(world.Manager.World.IsLive(self));

        var abi = new C4TickFrameAbi();
        var timer = new ClientTimerManager(abi);
        var host = new HeadlessBotHost(
            new RecordingSession(new List<string>()),
            new RecordingDriver(new List<string>()),
            new RecordingIngress(new List<string>()),
            new NullHook(),
            timer,
            world);
        int code = await host.RunAsync(new BotRunRequest(15, 0), CancellationToken.None);
        Assert.Equal(0, code);
        Assert.Equal(new ulong[] { 5, 10, 15 }, host.SubmittedTicks.ToArray());
        IReadOnlyList<WorldMessage> outbound = world.DrainOutbound();
        Assert.Equal(3, outbound.Count(message => message is InputCommandMessage input
            && string.Equals(input.MappingId, "chat.input", StringComparison.Ordinal)));
    }

    [Fact]
    public void ProductionResidentLoopTicksAndLogsChatInputAfterAwait()
    {
        IClientReplica replica = new ClientReplicaFactory().Create();
        replica.ResetForNewSession(new ReplicaResetRequest(1));
        IReplicaWorld world = replica.World;
        var admission = new ReplicaAdmission(
            new ReplicaBinding("acct-07", "room-01", "1", "player", 1),
            new[]
            {
                new ReplicaVisibleEntity("1", "player", "room-01", 1, 1, 0, Array.Empty<ReplicaAttributeValue>(), true, false)
            });
        Assert.True(world.InstallAdmission(in admission).Accepted);
        Assert.True(CommitEmptySnapshot(replica));
        NetEntityId self = world.Manager.World.Self.Id;
        world.Manager.Enqueue(new WorldChangeMessage(
            1UL,
            new[] { new CreateRecord("player", self, Array.Empty<FieldValue>()) },
            Array.Empty<FieldChange>(),
            Array.Empty<NetEntityId>(),
            Array.Empty<ClientRpcRecord>()));
        world.Manager.Tick();
        Assert.True(world.InputEnabled);

        string logDir = Path.Combine(Path.GetTempPath(), "lumio-bot-owner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(logDir);
        string logPath = Path.Combine(logDir, "bot-host.ndjson");
        string releaseFlag = Path.Combine(logDir, "release.flag");
        int owner = Environment.CurrentManagedThreadId;
        var threadIds = new List<int>();
        var abi = new C4TickFrameAbi();
        using var timer = new ClientTimerManager(abi);
        Assert.True(timer.ScheduleBotChatCadence());
        var session = new WorldBackedSession(world);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        int code = BotHostOwnerPump.Run(async () =>
        {
            await BotHostResidentLoop.RunAsync(
                new[] { new ResidentBot("Bot01", session) },
                timer,
                logPath,
                releaseFlag,
                async cancellationToken =>
                {
                    threadIds.Add(Environment.CurrentManagedThreadId);
                    await Task.Delay(5, cancellationToken);
                    threadIds.Add(Environment.CurrentManagedThreadId);
                    if (File.Exists(logPath)
                        && File.ReadAllText(logPath).Contains("\"kind\":\"chat.input\"", StringComparison.Ordinal))
                    {
                        File.WriteAllText(releaseFlag, "1");
                    }
                },
                timeout.Token);
            return 0;
        });

        try
        {
            Assert.Equal(0, code);
            Assert.NotEmpty(threadIds);
            Assert.All(threadIds, id => Assert.Equal(owner, id));
            Assert.True(File.Exists(logPath));
            string log = File.ReadAllText(logPath);
            Assert.Contains("\"kind\":\"chat.input\"", log, StringComparison.Ordinal);
            Assert.Contains("\"tickSource\":\"native-kernel/tickFrame\"", log, StringComparison.Ordinal);
            Assert.Contains("\"accountId\":\"Bot01\"", log, StringComparison.Ordinal);
            IReadOnlyList<WorldMessage> outbound = world.DrainOutbound();
            Assert.Contains(outbound, message => message is InputCommandMessage input
                && string.Equals(input.MappingId, "chat.input", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(logDir, true);
        }
    }

    [Fact]
    public async Task MissingEngineNativeIsBlocked()
    {
        string logDir = Path.Combine(Path.GetTempPath(), "lumio-bot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(logDir);
        try
        {
            int code = await FoundationHostCommand.RunAsync(
                new[]
                {
                    "--server", "ws://127.0.0.1:1/session",
                    "--account-from", "Bot01",
                    "--account-to", "Bot01",
                    "--log-dir", logDir
                },
                CancellationToken.None);
            Assert.Equal(FoundationHostCommand.BlockedExitCode, code);
        }
        finally
        {
            Directory.Delete(logDir, true);
        }
    }

    [Fact]
    public void ProductionSourcesHaveNoSecondTimerOrBindingTable()
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
                if (text.Contains("System.Timers.Timer", StringComparison.Ordinal)
                    || text.Contains("System.Threading.Timer", StringComparison.Ordinal)
                    || text.Contains("class BindingTable", StringComparison.Ordinal)
                    || text.Contains("new BindingTable", StringComparison.Ordinal))
                {
                    hits.Add(file);
                }
            }
        }

        Assert.Empty(hits);
    }

    private static bool CommitEmptySnapshot(IClientReplica replica)
    {
        var request = new ReplicaStageRequest(
            1,
            ReplicaUpdateKind.FullSnapshot,
            10,
            0,
            0,
            1,
            ReplicaC1Frames.EmptyFullSnapshot,
            Array.Empty<ulong>(),
            Array.Empty<ulong>());
        if (replica.StageAuthority(in request, out ReplicaStageHandle handle, out _).Status != ReplicaStageStatus.Staged)
        {
            return false;
        }

        return replica.ObserveRuntimeOutcome(handle, ReplicaRuntimeOutcome.CommittedOutcome(), out _) == ReplicaOutcomeStatus.Observed;
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

    private sealed class NullHook : IBotTickHook
    {
        public void BeforeTick(int tick)
        {
            _ = tick;
        }
    }

    private sealed class RecordingDriver : IBotScenarioDriver
    {
        private readonly List<string> _order;

        public RecordingDriver(List<string> order)
        {
            _order = order;
        }

        public int FillSamples(in BotDriverContext context, Span<RawInputSample> destination)
        {
            _ = context;
            _order.Add("fill");
            if (destination.Length == 0)
            {
                return 0;
            }

            destination[0] = new RawInputSample(1, 0, 0);
            return 1;
        }
    }

    private sealed class RecordingIngress : IInputSampleIngress
    {
        private readonly List<string> _order;

        public RecordingIngress(List<string> order)
        {
            _order = order;
        }

        public InputEnqueueReceipt TryEnqueue(in RawInputSample sample)
        {
            _ = sample;
            _order.Add("enqueue");
            return new InputEnqueueReceipt(true, default, default);
        }

        public SequencedInputSample[] DrainAccepted()
        {
            return Array.Empty<SequencedInputSample>();
        }
    }

    private sealed class WorldBackedSession : IClientSession
    {
        private readonly IReplicaWorld _world;

        public WorldBackedSession(IReplicaWorld world)
        {
            _world = world;
        }

        public SessionCommandResult RequestConnect(in SessionConnectRequest request, CancellationToken cancellationToken)
        {
            _ = request;
            cancellationToken.ThrowIfCancellationRequested();
            return new SessionCommandResult(true);
        }

        public SessionTickResult Tick(in ClientOwnerTick tick)
        {
            _ = tick;
            return new SessionTickResult(ClientSessionState.Active);
        }

        public SessionCommandResult RequestClose(in SessionCloseRequest request)
        {
            _ = request;
            return new SessionCommandResult(true);
        }

        public SessionCommandResult Login(in SessionConnectRequest request, CancellationToken cancellationToken)
        {
            return RequestConnect(in request, cancellationToken);
        }

        public bool TryDequeueSuperseded(out SessionSupersededNotice notice)
        {
            notice = default;
            return false;
        }

        public bool TryGetReplicaWorld(out IReplicaWorld world)
        {
            world = _world;
            return true;
        }

        public ClientSessionSnapshot GetSnapshot()
        {
            return new ClientSessionSnapshot(
                ClientSessionState.Active,
                1,
                true,
                0,
                true,
                true,
                false,
                0,
                0,
                0,
                0,
                0,
                0,
                Array.Empty<string>());
        }
    }

    private sealed class RecordingSession : IClientSession
    {
        private readonly List<string> _order;
        private ClientSessionState _state = ClientSessionState.Disconnected;

        public RecordingSession(List<string> order)
        {
            _order = order;
        }

        public SessionCommandResult RequestConnect(in SessionConnectRequest request, CancellationToken cancellationToken)
        {
            _ = request;
            cancellationToken.ThrowIfCancellationRequested();
            _state = ClientSessionState.Negotiating;
            return new SessionCommandResult(true);
        }

        public SessionTickResult Tick(in ClientOwnerTick tick)
        {
            _ = tick;
            _order.Add("tick");
            return new SessionTickResult(_state);
        }

        public SessionCommandResult RequestClose(in SessionCloseRequest request)
        {
            _ = request;
            _state = ClientSessionState.Closed;
            return new SessionCommandResult(true);
        }

        public SessionCommandResult Login(in SessionConnectRequest request, CancellationToken cancellationToken)
        {
            return RequestConnect(in request, cancellationToken);
        }

        public bool TryDequeueSuperseded(out SessionSupersededNotice notice)
        {
            notice = default;
            return false;
        }

        public bool TryGetReplicaWorld(out IReplicaWorld world)
        {
            world = default!;
            return false;
        }

        public ClientSessionSnapshot GetSnapshot()
        {
            _order.Add("observe");
            return new ClientSessionSnapshot(
                _state,
                1,
                false,
                0,
                false,
                false,
                false,
                0,
                0,
                0,
                0,
                0,
                0,
                Array.Empty<string>());
        }
    }
}

using Lumio.Client.Bot;
using Lumio.Client.Bot.Tests.Support;
using Lumio.Client.Connection;
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

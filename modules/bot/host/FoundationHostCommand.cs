using Lumio.Client.Bot;
using Lumio.Client.Connection;
using Lumio.Client.Handshake;
using Lumio.Client.Input;
using Lumio.Client.Observability;
using Lumio.Client.Persistence;
using Lumio.Client.Prediction;
using Lumio.Client.Replica;
using Lumio.Client.Session;
using Lumio.GameRuntime.Samples.Username.Components.Chat;
#if LUMIO_ENGINE_SDK
using Lumio.Engine.SDK;
#endif

namespace Lumio.Client.Bot.Host;

public static class FoundationHostCommand
{
    public const int BlockedExitCode = 8;

    public static readonly byte[] Hello = { 0xA5, 0x3C, 0x91, 0x07, 0xD2, 0x4E, 0xB8, 0x11 };

    public static readonly byte[] Snapshot = ReplicaC1Frames.EmptyFullSnapshot;

    public static readonly byte[] Gap = { 0x91, 0xA9, 0xB0, 0xC3 };

    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        HostArgs parsed = HostArgs.Parse(args);
        if (parsed.Production)
        {
            return await RunProductionAsync(parsed, cancellationToken).ConfigureAwait(false);
        }

        if (!parsed.Foundation)
        {
            return 2;
        }

        if (!string.Equals(parsed.Transport, "local-embedded", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (!string.Equals(parsed.Fixture, "foundation-happy-path", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

#if LUMIO_ENGINE_SDK
        using var engine = parsed.EngineNativePath is null ? null : LumioEngineSdk.LoadNative(parsed.EngineNativePath);
        if (engine is not null)
        {
            engine.Ping();
            Console.WriteLine($"ENGINE_NATIVE path={engine.NativePath} buildId={engine.BuildId} abiHash={engine.AbiHash} binarySha256={engine.BinarySha256}");
        }
#endif

        var connections = new CapturingConnectionFactory();
        var ingress = new InputSampleIngress(16);
        var options = new ClientEventPipelineOptions(8, 4, TimeSpan.FromSeconds(1));
        new ClientEventPipelineFactory().Create(in options, new InMemoryClientEventSink(8), out var writer);
        var deps = new ClientSessionDependencies(
            connections,
            new ClientHandshakeFactory(),
            new HostCapability(),
            new HelloClassifier(),
            ingress,
            new InputCommandSource(ingress, new HostInputMapper()),
            IClientPersistenceFactory.CreateMemory().CreateVerifiedSessionArtifactSource(),
            writer,
            new HostRuntime(),
            new ClientReplicaFactory(),
            new ClientPredictionFactory(),
            new ImmediateGameplayScopeActivator(),
            new NullPresentationSink(),
            new FixtureMessageMap());
        new ClientSessionFactory().Create(in deps, out IClientSession session);
        var hook = new FoundationPeer(connections);
        var host = new HeadlessBotHost(session, new DeterministicBotDriver(), ingress, hook);
        int code = await host.RunAsync(new BotRunRequest(5, 0), cancellationToken);
        ClientSessionSnapshot snap = session.GetSnapshot();
        if (snap.State == ClientSessionState.Faulted)
        {
            return 1;
        }

        if (snap.State != ClientSessionState.Closed)
        {
            return 5;
        }

        if (connections.Loopback.EncodeCalls < 1 || connections.Loopback.DecodeCalls < 1)
        {
            return 6;
        }

        return code;
    }

    private static async Task<int> RunProductionAsync(HostArgs parsed, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(parsed.EngineNativePath))
        {
            Console.Error.WriteLine("BLOCKED: --engine-native is required (set LumioEngineNative or LUMIO_ENGINE_NATIVE).");
            return BlockedExitCode;
        }

        if (string.IsNullOrWhiteSpace(parsed.LogDir))
        {
            Console.Error.WriteLine("BLOCKED: --log-dir is required (set LumioBotLogDir).");
            return BlockedExitCode;
        }

        if (string.IsNullOrWhiteSpace(parsed.Server))
        {
            Console.Error.WriteLine("BLOCKED: --server is required (set LumioBotServer).");
            return BlockedExitCode;
        }

        if (!File.Exists(parsed.EngineNativePath))
        {
            Console.Error.WriteLine("BLOCKED: --engine-native path does not exist.");
            return BlockedExitCode;
        }

#if !LUMIO_NATIVE_LOADER
        Console.Error.WriteLine("BLOCKED: Lumio.Engine.NativeLoader project was not found.");
        return BlockedExitCode;
#else
        Directory.CreateDirectory(parsed.LogDir);
        string logPath = Path.Combine(parsed.LogDir, "bot-host.ndjson");
        string releaseFlag = Path.Combine(parsed.LogDir, "release.flag");

        using NativeLoaderTimerAbi abi = NativeLoaderTimerAbi.Load(parsed.EngineNativePath);
        using var timer = new ClientTimerManager(abi);
        if (!timer.ScheduleBotChatCadence())
        {
            Console.Error.WriteLine("BLOCKED: ClientTimerManager failed to schedule bot chat cadence.");
            return BlockedExitCode;
        }

        var bots = new List<ProductionBot>();
        foreach (string account in EnumerateAccounts(parsed.AccountFrom, parsed.AccountTo))
        {
            bots.Add(CreateProductionBot(parsed.Server, account));
        }

        ulong tick = 0;
        while (!cancellationToken.IsCancellationRequested && !File.Exists(releaseFlag))
        {
            tick++;
            for (int i = 0; i < bots.Count; i++)
            {
                bots[i].Session.Tick(new ClientOwnerTick(tick));
            }

            IReadOnlyList<ulong> dues = timer.Advance(tick);
            for (int d = 0; d < dues.Count; d++)
            {
                for (int i = 0; i < bots.Count; i++)
                {
                    ProductionBot bot = bots[i];
                    if (!bot.Session.TryGetReplicaWorld(out IReplicaWorld world) || !world.InputEnabled)
                    {
                        continue;
                    }

                    try
                    {
                        world.Manager.World.Self.Get<ChatComponent>().SendMessage("bot-" + dues[d].ToString(System.Globalization.CultureInfo.InvariantCulture));
                        world.Manager.Tick();
                        AppendChatInputLog(logPath, dues[d], bot.AccountId);
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
            }

            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        }

        for (int i = 0; i < bots.Count; i++)
        {
            bots[i].Session.RequestClose(new SessionCloseRequest(false));
        }

        return 0;
#endif
    }

    private static ProductionBot CreateProductionBot(string server, string account)
    {
        var ingress = new InputSampleIngress(16);
        var options = new ClientEventPipelineOptions(8, 4, TimeSpan.FromSeconds(1));
        new ClientEventPipelineFactory().Create(in options, new InMemoryClientEventSink(8), out var writer);
        var endpoint = new ClientEndpoint(
            server,
            new byte[] { 0x01, 0x02, 0x03, 0x04 },
            new byte[] { 0x05, 0x06, 0x07, 0x08 },
            TimeSpan.FromSeconds(10));
        var deps = new ClientSessionDependencies(
            new WebSocketClientConnectionFactory(),
            new ClientHandshakeFactory(),
            new HostCapability(),
            new HelloClassifier(),
            ingress,
            new InputCommandSource(ingress, new HostInputMapper()),
            IClientPersistenceFactory.CreateMemory().CreateVerifiedSessionArtifactSource(),
            writer,
            new HostRuntime(),
            new ClientReplicaFactory(),
            new ClientPredictionFactory(),
            new ImmediateGameplayScopeActivator(),
            new NullPresentationSink(),
            new JsonSessionMessageKindMap());
        new ClientSessionFactory().Create(in deps, out IClientSession session);
        session.Login(new SessionConnectRequest(1, endpoint), CancellationToken.None);
        _ = account;
        return new ProductionBot(account, session);
    }

    private static void AppendChatInputLog(string path, ulong tick, string accountId)
    {
        string line = "{\"ts\":\"" + DateTime.UtcNow.ToString("o") +
                      "\",\"kind\":\"chat.input\",\"tick\":" + tick.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                      ",\"tickSource\":\"native-kernel/tickFrame\",\"pid\":" +
                      Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                      ",\"accountId\":\"" + accountId + "\"}\n";
        File.AppendAllText(path, line);
    }

    private static IEnumerable<string> EnumerateAccounts(string from, string to)
    {
        SplitAccount(from, out string prefix, out int start);
        SplitAccount(to, out _, out int end);
        if (end < start)
        {
            end = start;
        }

        for (int i = start; i <= end; i++)
        {
            string number = i < 100
                ? i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)
                : i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            yield return prefix + number;
        }
    }

    private static void SplitAccount(string account, out string prefix, out int number)
    {
        prefix = "Bot";
        number = 1;
        if (string.IsNullOrEmpty(account))
        {
            return;
        }

        int i = account.Length - 1;
        while (i >= 0 && char.IsDigit(account[i]))
        {
            i--;
        }

        prefix = i >= 0 ? account[..(i + 1)] : string.Empty;
        if (!int.TryParse(account.AsSpan(i + 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out number))
        {
            number = 1;
        }
    }

    private readonly struct ProductionBot
    {
        public ProductionBot(string accountId, IClientSession session)
        {
            AccountId = accountId;
            Session = session;
        }

        public string AccountId { get; }

        public IClientSession Session { get; }
    }

    internal readonly struct HostArgs
    {
        public bool Foundation { get; init; }
        public bool Production { get; init; }
        public string Transport { get; init; }
        public string Fixture { get; init; }
        public string? Server { get; init; }
        public string AccountFrom { get; init; }
        public string AccountTo { get; init; }
        public string? EngineNativePath { get; init; }
        public string? LogDir { get; init; }

        public static HostArgs Parse(string[]? args)
        {
            bool foundation = false;
            string transport = "local-embedded";
            string fixture = "foundation-happy-path";
            string? server = FirstEnv("LumioBotServer");
            string accountFrom = FirstEnv("LumioBotAccountFrom") ?? "Bot01";
            string accountTo = FirstEnv("LumioBotAccountTo") ?? "Bot01";
            string? engineNative = FirstEnv("LumioEngineNative", "LUMIO_ENGINE_NATIVE");
            string? logDir = FirstEnv("LumioBotLogDir");

            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (string.Equals(args[i], "foundation", StringComparison.OrdinalIgnoreCase))
                    {
                        foundation = true;
                    }
                    else if (Match(args, i, "--transport", out string transportValue))
                    {
                        i++;
                        transport = transportValue;
                    }
                    else if (Match(args, i, "--fixture", out string fixtureValue))
                    {
                        i++;
                        fixture = fixtureValue;
                    }
                    else if (Match(args, i, "--server", out string serverValue))
                    {
                        i++;
                        server = serverValue;
                    }
                    else if (Match(args, i, "--account-from", out string fromValue))
                    {
                        i++;
                        accountFrom = fromValue;
                    }
                    else if (Match(args, i, "--account-to", out string toValue))
                    {
                        i++;
                        accountTo = toValue;
                    }
                    else if (Match(args, i, "--engine-native", out string nativeValue))
                    {
                        i++;
                        engineNative = nativeValue;
                    }
                    else if (Match(args, i, "--log-dir", out string logValue))
                    {
                        i++;
                        logDir = logValue;
                    }
                }
            }

            if (args == null || args.Length == 0)
            {
                foundation = true;
            }

            bool production = !string.IsNullOrEmpty(server) && !foundation;
            if (!production && !foundation && args != null && args.Length == 0)
            {
                foundation = true;
            }

            return new HostArgs
            {
                Foundation = foundation,
                Production = production,
                Transport = transport,
                Fixture = fixture,
                Server = server,
                AccountFrom = accountFrom,
                AccountTo = accountTo,
                EngineNativePath = engineNative,
                LogDir = logDir
            };
        }

        private static bool Match(string[] args, int i, string name, out string value)
        {
            value = string.Empty;
            if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) || i + 1 >= args.Length)
            {
                return false;
            }

            value = args[i + 1];
            return true;
        }

        private static string? FirstEnv(params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string? value = Environment.GetEnvironmentVariable(names[i]);
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return null;
        }
    }

    private sealed class FoundationPeer : IBotTickHook
    {
        private readonly CapturingConnectionFactory _connections;

        public FoundationPeer(CapturingConnectionFactory connections)
        {
            _connections = connections;
        }

        public void BeforeTick(int tick)
        {
            if (_connections.Loopback == null)
            {
                return;
            }

            if (tick == 0)
            {
                _connections.Loopback.TryDeliverToClient(new EncodedFrame(Hello));
            }
            else if (tick == 1)
            {
                _connections.Loopback.TryDeliverToClient(new EncodedFrame(Snapshot));
            }
            else if (tick == 2)
            {
                _connections.Loopback.TryDeliverToClient(new EncodedFrame(Gap));
            }
            else if (tick == 3)
            {
                _connections.Loopback.TryDeliverToClient(new EncodedFrame(Snapshot));
            }
        }
    }

    internal sealed class CapturingConnectionFactory : IClientConnectionFactory
    {
        public LocalEmbeddedLoopback Loopback { get; private set; } = default!;

        public ClientConnectionCreateResult Create(in ClientConnectionCreateRequest request, out IClientConnection connection)
        {
            ClientConnectionCreateResult result = new ClientConnectionFactory().Create(in request, out connection);
            Loopback = result.Loopback;
            return result;
        }
    }

    private sealed class HelloClassifier : IHandshakeFrameClassifier
    {
        public HandshakeOpaqueFrameRole Classify(ReadOnlyMemory<byte> frame)
        {
            if (frame.Span.SequenceEqual(Hello))
            {
                return HandshakeOpaqueFrameRole.ServerHello;
            }

            return HandshakeOpaqueFrameRole.Unclassified;
        }
    }

    private sealed class FixtureMessageMap : ISessionMessageKindMap
    {
        public SessionMessageKind Map(ReadOnlyMemory<byte> frame)
        {
            if (frame.Span.SequenceEqual(Snapshot))
            {
                return SessionMessageKind.FullSnapshot;
            }

            if (frame.Span.SequenceEqual(Gap))
            {
                return SessionMessageKind.Gap;
            }

            return SessionMessageKind.Unknown;
        }
    }

    private sealed class HostCapability : IPlatformCapabilityProvider
    {
        public ValueTask<PlatformCapabilityResult> QueryAsync(in PlatformCapabilityQuery query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<PlatformCapabilityResult>(new PlatformCapabilityResult(query.Attempt, query.Generation, true));
        }
    }

    private sealed class HostInputMapper : IGameInputMapper
    {
        public bool TryMap(in SequencedInputSample sample, in InputDrainContext context, out GameplayCommandCandidate candidate)
        {
            _ = context;
            candidate = new GameplayCommandCandidate(sample.Sequence, new byte[] { 0x42 });
            return true;
        }
    }

    private sealed class HostRuntime : IClientRuntimePort
    {
        public ValueTask<RuntimeTransactionOutcome> ApplyAuthoritativeTransaction(in RuntimeTransactionRequest request, CancellationToken cancellationToken)
        {
            _ = request;
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<RuntimeTransactionOutcome>(new RuntimeTransactionOutcome(true));
        }

        public ValueTask<RuntimeTransactionOutcome> ApplyLocalPrediction(in RuntimeTransactionRequest request, CancellationToken cancellationToken)
        {
            _ = request;
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<RuntimeTransactionOutcome>(new RuntimeTransactionOutcome(true));
        }
    }
}

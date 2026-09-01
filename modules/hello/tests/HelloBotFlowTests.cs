using System.Text.Json;
using Lumio.Client.Hello;

namespace Lumio.Client.Hello.Tests;

public sealed class HelloBotFlowTests
{
    private const string HelloSha = "a591a6d40bf420404a011733cfb7b190d62c65bf0bcda32b57b277d9ad9f146e";

    private static string? LocateContract()
    {
        string? path = HelloContractLocator.Locate();
        if (path is null)
        {
            throw Xunit.Sdk.SkipException.ForSkip("hello-wire-v1.json 未找到:需要兄弟 LumioGameEngineArchitecture 检出或 LUMIO_HELLO_WIRE_CONTRACT 环境变量;本仓不内嵌契约副本。");
        }

        return path;
    }

    // ---------- 成功路径 ----------

    [Fact]
    public async Task FullHappyPathReturnsZeroAndWritesEvidence()
    {
        string contractPath = LocateContract()!;
        using var contract = HelloContract.Load(contractPath);
        await using var server = HelloLoopbackServer.Start(new HelloServerScript(), contract);

        using RunOutcome run = await RunBotAsync(server, contractPath);

        Assert.True(HelloBotCli.ExitOk == run.Code, "stderr: " + run.Stderr + " | server: " + server.LastServerError);

        using JsonDocument result = JsonDocument.Parse(File.ReadAllText(run.ResultPath));
        JsonElement root = result.RootElement;
        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal("bot", root.GetProperty("role").GetString());
        Assert.Equal("srv-session-1", root.GetProperty("sessionId").GetString());
        JsonElement received = root.GetProperty("received");
        Assert.Equal(1, received.GetArrayLength());
        Assert.Equal("browser", received[0].GetProperty("sender").GetString());
        Assert.Equal(1L, received[0].GetProperty("sequence").GetInt64());
        Assert.Equal(1L, received[0].GetProperty("revision").GetInt64());
        Assert.Equal(HelloSha, received[0].GetProperty("payloadSha256").GetString());
        Assert.True(received[0].GetProperty("latencyMs").GetInt64() is >= 0 and < 1000);
        JsonElement sent = root.GetProperty("sent");
        Assert.Equal(1L, sent.GetProperty("sequence").GetInt64());
        Assert.Equal(HelloSha, sent.GetProperty("payloadSha256").GetString());
        Assert.True(sent.GetProperty("sentAtMs").GetInt64() > 0);
        Assert.True(root.GetProperty("maxLatencyMs").GetInt64() >= 0);

        List<JsonElement> trace = ParseTrace(run.TracePath);
        AssertTraceSatisfiesContract(trace, contract);
        Assert.Equal("bot_started", trace[0].GetProperty("kind").GetString());
        Assert.True(trace[0].GetProperty("pid").GetInt64() > 0);
        Assert.Equal("bot", trace[0].GetProperty("role").GetString());
        Assert.Equal(server.Uri, trace[0].GetProperty("serverUrl").GetString());
        Assert.Equal("bot_finished", trace[^1].GetProperty("kind").GetString());
        Assert.True(trace[^1].GetProperty("exitOk").GetBoolean());
        Assert.Equal(1L, trace[^1].GetProperty("receivedBySender").GetProperty("browser").GetInt64());

        List<string> kinds = trace.Select(l => l.GetProperty("kind").GetString()!).ToList();
        foreach (string expected in new[] { "connected", "handshake_ack", "baseline_received", "baseline_ack_sent", "delta_received", "command_sent", "command_result" })
        {
            Assert.Contains(expected, kinds);
        }

        Assert.DoesNotContain("error_received", kinds);

        // 顺序断言:bot 等 browser Delta 之后才发送;握手/子协议也在服务器侧核对。
        Assert.True(server.OrderingHeld);
        Assert.True(server.ProtocolHeaderValid);
        Assert.Equal(new[] { "Handshake", "BaselineAck", "InputCommand" }, server.ReceivedTypes);
        JsonElement command = server.ReceivedMessages[^1];
        Assert.Equal("bot", command.GetProperty("sender").GetString());
        Assert.Equal("hello", command.GetProperty("kind").GetString());
        Assert.Equal("Hello World", command.GetProperty("payload").GetString());
        Assert.Equal(HelloSha, command.GetProperty("payloadSha256").GetString());
        Assert.Equal(1L, command.GetProperty("sequence").GetInt64());
        JsonElement handshake = server.ReceivedMessages[0];
        Assert.Equal("lumio-bot", handshake.GetProperty("clientName").GetString());
        Assert.Equal("lumio.hello-wire.v1", handshake.GetProperty("contractId").GetString());
        Assert.Equal("bot", handshake.GetProperty("role").GetString());
    }

    [Fact]
    public async Task BotSendsNothingWhenBrowserDeltaNeverArrives()
    {
        string contractPath = LocateContract()!;
        using var contract = HelloContract.Load(contractPath);
        var script = new HelloServerScript
        {
            SendBrowserDelta = false,
            QuietWindowMs = 300,
        };
        await using var server = HelloLoopbackServer.Start(script, contract);

        using RunOutcome run = await RunBotAsync(server, contractPath);

        Assert.Equal(HelloBotCli.ExitFailure, run.Code);
        Assert.DoesNotContain("InputCommand", server.ReceivedTypes);

        using JsonDocument result = JsonDocument.Parse(File.ReadAllText(run.ResultPath));
        Assert.False(result.RootElement.GetProperty("ok").GetBoolean());
    }

    // ---------- 失败矩阵 ----------

    [Fact]
    public async Task BadDeltaHashFailsTheBot()
    {
        string contractPath = LocateContract()!;
        using var contract = HelloContract.Load(contractPath);
        var script = new HelloServerScript
        {
            BrowserDeltaPayloadSha256 = new string('f', 64),
        };
        await using var server = HelloLoopbackServer.Start(script, contract);

        using RunOutcome run = await RunBotAsync(server, contractPath);

        Assert.Equal(HelloBotCli.ExitFailure, run.Code);
        using JsonDocument result = JsonDocument.Parse(File.ReadAllText(run.ResultPath));
        Assert.False(result.RootElement.GetProperty("ok").GetBoolean());

        List<JsonElement> trace = ParseTrace(run.TracePath);
        AssertTraceSatisfiesContract(trace, contract);
        Assert.False(trace[^1].GetProperty("exitOk").GetBoolean());
    }

    [Fact]
    public async Task UnknownMessageTypeFailsTheBot()
    {
        string contractPath = LocateContract()!;
        using var contract = HelloContract.Load(contractPath);
        var script = new HelloServerScript { SendUnknownMessage = true };
        await using var server = HelloLoopbackServer.Start(script, contract);

        using RunOutcome run = await RunBotAsync(server, contractPath);

        Assert.Equal(HelloBotCli.ExitFailure, run.Code);
        using JsonDocument result = JsonDocument.Parse(File.ReadAllText(run.ResultPath));
        Assert.False(result.RootElement.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task ServerErrorMessageFailsTheBotAndIsTraced()
    {
        string contractPath = LocateContract()!;
        using var contract = HelloContract.Load(contractPath);
        var script = new HelloServerScript { SendError = true, CloseAfterCommand = false };
        await using var server = HelloLoopbackServer.Start(script, contract);

        using RunOutcome run = await RunBotAsync(server, contractPath);

        Assert.Equal(HelloBotCli.ExitFailure, run.Code);
        List<JsonElement> trace = ParseTrace(run.TracePath);
        AssertTraceSatisfiesContract(trace, contract);
        JsonElement errorEvent = trace.Single(l => l.GetProperty("kind").GetString() == "error_received");
        Assert.Equal("runtime_failure", errorEvent.GetProperty("code").GetString());
        Assert.Equal("scripted runtime failure", errorEvent.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task BaselineTimeoutFailsWithExitCodeOne()
    {
        string contractPath = LocateContract()!;
        using var contract = HelloContract.Load(contractPath);
        var script = new HelloServerScript { SendBaseline = false };
        await using var server = HelloLoopbackServer.Start(script, contract);

        using RunOutcome run = await RunBotAsync(server, contractPath, "--baseline-timeout-ms", "300");

        Assert.Equal(HelloBotCli.ExitFailure, run.Code);
        using JsonDocument result = JsonDocument.Parse(File.ReadAllText(run.ResultPath));
        Assert.False(result.RootElement.GetProperty("ok").GetBoolean());
        Assert.Contains("baseline", result.RootElement.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.Equal(new[] { "Handshake" }, server.ReceivedTypes);
    }

    [Fact]
    public async Task MidRunDisconnectFailsWithCompleteTrace()
    {
        string contractPath = LocateContract()!;
        using var contract = HelloContract.Load(contractPath);
        var script = new HelloServerScript { AbortTcpAfterHandshakeAck = true };
        await using var server = HelloLoopbackServer.Start(script, contract);

        using RunOutcome run = await RunBotAsync(server, contractPath);

        Assert.Equal(HelloBotCli.ExitFailure, run.Code);
        using JsonDocument result = JsonDocument.Parse(File.ReadAllText(run.ResultPath));
        Assert.False(result.RootElement.GetProperty("ok").GetBoolean());

        List<JsonElement> trace = ParseTrace(run.TracePath);
        AssertTraceSatisfiesContract(trace, contract);
        Assert.Equal("bot_started", trace[0].GetProperty("kind").GetString());
        Assert.Equal("bot_finished", trace[^1].GetProperty("kind").GetString());
        Assert.False(trace[^1].GetProperty("exitOk").GetBoolean());
    }

    [Fact]
    public async Task ContractIdMismatchFailsAtStartup()
    {
        string contractPath = LocateContract()!;
        using var contract = HelloContract.Load(contractPath);
        var script = new HelloServerScript { HandshakeAckContractId = "lumio.hello-wire.v999" };
        await using var server = HelloLoopbackServer.Start(script, contract);

        using RunOutcome run = await RunBotAsync(server, contractPath);

        Assert.Equal(HelloBotCli.ExitFailure, run.Code);
        using JsonDocument result = JsonDocument.Parse(File.ReadAllText(run.ResultPath));
        Assert.False(result.RootElement.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task MissingContractFileFailsWithExitCodeOne()
    {
        string contractPath = LocateContract()!;
        using var contract = HelloContract.Load(contractPath);
        await using var server = HelloLoopbackServer.Start(new HelloServerScript(), contract);

        string dir = Directory.CreateTempSubdirectory("lumio-hello-tests-").FullName;
        try
        {
            string trace = Path.Combine(dir, "trace.ndjson");
            string result = Path.Combine(dir, "result.json");
            int code = await HelloBotCli.RunAsync(new[]
            {
                "--url", server.Uri,
                "--role", "bot",
                "--contract", Path.Combine(dir, "missing.json"),
                "--trace", trace,
                "--result", result,
            }, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(HelloBotCli.ExitFailure, code);
            Assert.True(File.Exists(result));
            using JsonDocument parsed = JsonDocument.Parse(File.ReadAllText(result));
            Assert.False(parsed.RootElement.GetProperty("ok").GetBoolean());
        }
        finally
        {
            HelloContractTests.TryDelete(dir);
        }
    }

    // ---------- CLI 参数面 ----------

    [Fact]
    public async Task UsageErrorsExitWithCodeThree()
    {
        string contractPath = LocateContract()!;
        using var contract = HelloContract.Load(contractPath);
        await using var server = HelloLoopbackServer.Start(new HelloServerScript(), contract);
        string dir = Directory.CreateTempSubdirectory("lumio-hello-tests-").FullName;
        try
        {
            string trace = Path.Combine(dir, "t.ndjson");
            string result = Path.Combine(dir, "r.json");

            Assert.Equal(HelloBotCli.ExitUsage, await HelloBotCli.RunAsync(Array.Empty<string>(), cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(HelloBotCli.ExitUsage, await HelloBotCli.RunAsync(new[] { "--url", server.Uri }, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(HelloBotCli.ExitUsage, await HelloBotCli.RunAsync(new[] { "--wat", "1" }, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(HelloBotCli.ExitUsage, await HelloBotCli.RunAsync(new[]
            {
                "--url", server.Uri, "--role", "bot", "--contract", contractPath, "--trace", trace, "--result", result,
                "--baseline-timeout-ms", "zero",
            }, cancellationToken: TestContext.Current.CancellationToken));
            Assert.False(File.Exists(result));
        }
        finally
        {
            HelloContractTests.TryDelete(dir);
        }
    }

    // ---------- 夹具 ----------

    private static async Task<RunOutcome> RunBotAsync(HelloLoopbackServer server, string contractPath, params string[] extra)
    {
        string dir = Directory.CreateTempSubdirectory("lumio-hello-tests-").FullName;
        try
        {
            string trace = Path.Combine(dir, "trace.ndjson");
            string result = Path.Combine(dir, "result.json");
            var args = new List<string>
            {
                "--url", server.Uri,
                "--role", "bot",
                "--contract", contractPath,
                "--trace", trace,
                "--result", result,
            };
            args.AddRange(extra);
            var stderr = new StringWriter();
            int code = await HelloBotCli.RunAsync(
                args.ToArray(),
                stdout: new StringWriter(),
                stderr: stderr,
                cancellationToken: TestContext.Current.CancellationToken);
            return new RunOutcome(code, trace, result, dir, stderr.ToString());
        }
        catch
        {
            HelloContractTests.TryDelete(dir);
            throw;
        }
    }

    private static List<JsonElement> ParseTrace(string path)
    {
        return File.ReadAllLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line =>
            {
                using JsonDocument document = JsonDocument.Parse(line);
                return document.RootElement.Clone();
            })
            .ToList();
    }

    private static void AssertTraceSatisfiesContract(List<JsonElement> lines, HelloContract contract)
    {
        Assert.NotEmpty(lines);
        foreach (JsonElement line in lines)
        {
            string kind = line.GetProperty("kind").GetString()!;
            Assert.Contains(kind, contract.BotTraceKinds);
            foreach (string field in contract.BotTraceRequiredFields(kind))
            {
                Assert.True(line.TryGetProperty(field, out _), $"trace 事件 {kind} 缺必填字段 {field}");
            }
        }
    }

    private sealed class RunOutcome : IDisposable
    {
        public RunOutcome(int code, string tracePath, string resultPath, string dir, string stderr)
        {
            Code = code;
            TracePath = tracePath;
            ResultPath = resultPath;
            Dir = dir;
            Stderr = stderr;
        }

        public int Code { get; }

        public string TracePath { get; }

        public string ResultPath { get; }

        public string Stderr { get; }

        private string Dir { get; }

        public void Dispose()
        {
            HelloContractTests.TryDelete(Dir);
        }
    }
}

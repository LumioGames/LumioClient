using System.Text.Json;
using Lumio.Client.Hello;

namespace Lumio.Client.Hello.Tests;

public sealed class HelloContractTests
{
    private static string? LocateContract()
    {
        string? path = HelloContractLocator.Locate();
        if (path is null)
        {
            throw Xunit.Sdk.SkipException.ForSkip("hello-wire-v1.json 未找到:需要兄弟 LumioGameEngineArchitecture 检出或 LUMIO_HELLO_WIRE_CONTRACT 环境变量;本仓不内嵌契约副本。");
        }

        return path;
    }

    [Fact]
    public void LoadsRealContractWithExpectedIdentity()
    {
        string path = LocateContract()!;
        using var contract = HelloContract.Load(path);

        Assert.Equal("lumio.hello-wire.v1", contract.ContractId);
        Assert.Equal("lumio-hello-v1", contract.Subprotocol);
        Assert.Contains("browser", contract.Roles);
        Assert.Contains("bot", contract.Roles);
        Assert.Contains("bad_payload_hash", contract.ErrorCodes);
        Assert.Contains("unknown_mapping", contract.ErrorCodes);
    }

    [Fact]
    public void RequiredFieldsAreDrivenFromTheContractFile()
    {
        using var contract = HelloContract.Load(LocateContract()!);

        IReadOnlyDictionary<string, string> delta = contract.RequiredFields("Delta");
        Assert.Equal(
            new[] { "messageType", "tickId", "revision", "sender", "sequence", "kind", "payload", "payloadSha256", "originSentAtMs", "committedAtMs", "commandSequence" }.OrderBy(f => f, StringComparer.Ordinal),
            delta.Keys.OrderBy(f => f, StringComparer.Ordinal));
        Assert.Equal("const:Delta", delta["messageType"]);
        Assert.Equal("sha256-hex", delta["payloadSha256"]);

        IReadOnlyDictionary<string, string> handshake = contract.RequiredFields("Handshake");
        Assert.Equal(
            new[] { "messageType", "role", "clientName", "contractId" }.OrderBy(f => f, StringComparer.Ordinal),
            handshake.Keys.OrderBy(f => f, StringComparer.Ordinal));
    }

    [Fact]
    public void LimitsComeFromTheContractFile()
    {
        using var contract = HelloContract.Load(LocateContract()!);

        Assert.Equal(4096L, contract.Limits.MaxPayloadBytes);
        Assert.Equal(2L, contract.Limits.MaxSessions);
        Assert.Equal(5000L, contract.Limits.BaselineTimeoutMs);
        Assert.Equal(5000L, contract.Limits.HandshakeTimeoutMs);
        Assert.Equal(30000L, contract.Limits.ScenarioTimeoutMs);
        Assert.Equal(32L, contract.Limits.HelloLogCapacity);
    }

    [Fact]
    public void ValidateMessageAcceptsAWellFormedDelta()
    {
        using var contract = HelloContract.Load(LocateContract()!);
        using var document = JsonDocument.Parse(DeltaJson(sha: "a591a6d40bf420404a011733cfb7b190d62c65bf0bcda32b57b277d9ad9f146e"));

        Assert.Empty(contract.ValidateMessage(document.RootElement));
    }

    [Fact]
    public void ValidateMessageFlagsMissingRequiredField()
    {
        using var contract = HelloContract.Load(LocateContract()!);
        string json = DeltaJson(sha: "a591a6d40bf420404a011733cfb7b190d62c65bf0bcda32b57b277d9ad9f146e")
            .Replace("\"payloadSha256\":\"a591a6d40bf420404a011733cfb7b190d62c65bf0bcda32b57b277d9ad9f146e\",", string.Empty);
        using var document = JsonDocument.Parse(json);

        IReadOnlyList<ContractFieldError> errors = contract.ValidateMessage(document.RootElement);

        Assert.Single(errors);
        Assert.Equal("payloadSha256", errors[0].Field);
    }

    [Fact]
    public void ValidateMessageFlagsMalformedHash()
    {
        using var contract = HelloContract.Load(LocateContract()!);
        using var uppercase = JsonDocument.Parse(DeltaJson(sha: "A591A6D40BF420404A011733CFB7B190D62C65BF0BCDA32B57B277D9AD9F146E"));
        using var shortHash = JsonDocument.Parse(DeltaJson(sha: "deadbeef"));

        Assert.Single(contract.ValidateMessage(uppercase.RootElement));
        Assert.Single(contract.ValidateMessage(shortHash.RootElement));
    }

    [Fact]
    public void ValidateMessageRejectsUnknownMessageType()
    {
        using var contract = HelloContract.Load(LocateContract()!);
        using var document = JsonDocument.Parse("{\"messageType\":\"Nope\",\"foo\":1}");

        IReadOnlyList<ContractFieldError> errors = contract.ValidateMessage(document.RootElement);

        Assert.Single(errors);
        Assert.Equal("messageType", errors[0].Field);
        Assert.Contains("unknown_mapping", errors[0].Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateMessageEnforcesConstAndEnumConstraints()
    {
        using var contract = HelloContract.Load(LocateContract()!);
        using var wrongKind = JsonDocument.Parse(DeltaJson(sha: "a591a6d40bf420404a011733cfb7b190d62c65bf0bcda32b57b277d9ad9f146e").Replace("\"kind\":\"hello\"", "\"kind\":\"bye\""));
        using var wrongSender = JsonDocument.Parse(DeltaJson(sha: "a591a6d40bf420404a011733cfb7b190d62c65bf0bcda32b57b277d9ad9f146e").Replace("\"sender\":\"browser\"", "\"sender\":\"server\""));

        Assert.Contains(contract.ValidateMessage(wrongKind.RootElement), e => e.Field == "kind");
        Assert.Contains(contract.ValidateMessage(wrongSender.RootElement), e => e.Field == "sender");
    }

    [Fact]
    public void ValidateMessageChecksSharedTypeRecordsInsideArrays()
    {
        using var contract = HelloContract.Load(LocateContract()!);
        string snapshot = "{\"messageType\":\"FullSnapshot\",\"sessionId\":\"s\",\"tickId\":0,\"revision\":0,"
            + "\"helloLog\":[{\"sender\":\"bot\",\"sequence\":1,\"kind\":\"hello\",\"payload\":\"Hello World\","
            + "\"payloadSha256\":\"a591a6d40bf420404a011733cfb7b190d62c65bf0bcda32b57b277d9ad9f146e\",\"originSentAtMs\":1,\"committedAtMs\":2}]}";
        using var missingTick = JsonDocument.Parse(snapshot);
        using var document = JsonDocument.Parse(snapshot.Replace("\"originSentAtMs\":1,\"committedAtMs\":2", "\"originSentAtMs\":1,\"committedAtMs\":2,\"tickId\":0,\"revision\":1"));

        Assert.NotEmpty(contract.ValidateMessage(missingTick.RootElement));
        Assert.Empty(contract.ValidateMessage(document.RootElement));
    }

    [Fact]
    public void BotTraceRequiredFieldsComeFromTheContractFile()
    {
        using var contract = HelloContract.Load(LocateContract()!);

        Assert.Contains("bot_started", contract.BotTraceKinds);
        Assert.Contains("bot_finished", contract.BotTraceKinds);
        Assert.Equal(new[] { "pid", "role", "serverUrl" }, contract.BotTraceRequiredFields("bot_started"));
        Assert.Equal(new[] { "sender", "sequence", "payloadSha256", "sentAtMs" }, contract.BotTraceRequiredFields("command_sent"));
        Assert.Equal(new[] { "sender", "sequence", "tickId", "revision", "payloadSha256", "latencyMs" }, contract.BotTraceRequiredFields("delta_received"));
        Assert.Equal(new[] { "exitOk", "receivedBySender" }, contract.BotTraceRequiredFields("bot_finished"));
    }

    [Fact]
    public void LoadHonorsEnvironmentVariableOverride()
    {
        string source = LocateContract()!;
        string tempDir = Directory.CreateTempSubdirectory("lumio-hello-contract-").FullName;
        try
        {
            string copy = Path.Combine(tempDir, "hello-wire-v1.json");
            File.WriteAllText(copy, File.ReadAllText(source).Replace("lumio.hello-wire.v1", "lumio.hello-wire.test"));
            string? previous = Environment.GetEnvironmentVariable(HelloContractLocator.EnvironmentVariable);
            Environment.SetEnvironmentVariable(HelloContractLocator.EnvironmentVariable, copy);
            try
            {
                string? located = HelloContractLocator.Locate();
                Assert.Equal(copy, located);
                using var contract = HelloContract.Load(located!);
                Assert.Equal("lumio.hello-wire.test", contract.ContractId);
            }
            finally
            {
                Environment.SetEnvironmentVariable(HelloContractLocator.EnvironmentVariable, previous);
            }
        }
        finally
        {
            TryDelete(tempDir);
        }
    }

    internal static void TryDelete(string dir)
    {
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string DeltaJson(string sha)
    {
        return "{\"messageType\":\"Delta\",\"tickId\":1,\"revision\":1,\"sender\":\"browser\",\"sequence\":1,"
            + "\"kind\":\"hello\",\"payload\":\"Hello World\",\"payloadSha256\":\"" + sha
            + "\",\"originSentAtMs\":10,\"committedAtMs\":20,\"commandSequence\":1}";
    }
}

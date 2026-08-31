using System.Text.Json;
using Lumio.Client.Hello;

namespace Lumio.Client.Hello.Tests;

public sealed class HelloWireClientTests
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

    [Fact]
    public async Task SubprotocolIsNegotiatedFromTheContract()
    {
        string contractPath = LocateContract()!;
        using var contract = HelloContract.Load(contractPath);
        await using var server = HelloLoopbackServer.Start(new HelloServerScript(), contract);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await using var client = new HelloWireClient(new HelloWireClientOptions
        {
            ServerUrl = server.Uri,
            Role = "bot",
            ClientName = "lumio-bot",
            Contract = contract,
        });

        await client.ConnectAsync(cancellation.Token);
        await client.HandshakeAsync(cancellation.Token);

        Assert.Equal("lumio-hello-v1", client.NegotiatedSubProtocol);
        Assert.True(server.ProtocolHeaderValid);
        Assert.Equal("srv-session-1", client.SessionId);
        await client.AbortAsync("test complete");
    }

    [Fact]
    public async Task SendCommandIncrementsSequenceAndHashesPayload()
    {
        string contractPath = LocateContract()!;
        using var contract = HelloContract.Load(contractPath);
        var script = new HelloServerScript { MaxCommands = 2, CloseAfterCommand = false };
        await using var server = HelloLoopbackServer.Start(script, contract);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await using var client = new HelloWireClient(new HelloWireClientOptions
        {
            ServerUrl = server.Uri,
            Role = "bot",
            ClientName = "lumio-bot",
            Contract = contract,
        });
        await client.ConnectAsync(cancellation.Token);
        await client.HandshakeAsync(cancellation.Token);
        await client.WaitForBaselineAsync(cancellation.Token);
        DeltaRecord delta = await client.WaitForDeltaAsync("browser", cancellation.Token);
        Assert.Equal("browser", delta.Sender);
        Assert.Equal(1L, delta.Revision);

        CommandRecord first = await client.SendCommandAsync("Hello World", cancellation.Token);
        CommandRecord second = await client.SendCommandAsync("Hello World", cancellation.Token);

        Assert.Equal(1L, first.Sequence);
        Assert.Equal(2L, second.Sequence);
        Assert.Equal(HelloSha, first.PayloadSha256);
        Assert.Equal(HelloSha, second.PayloadSha256);
        Assert.True(first.SentAtMs > 0);
        Assert.True(second.SentAtMs >= first.SentAtMs);

        // 服务器侧独立重算核对。
        for (int i = 0; i < 20 && server.ReceivedTypes.Count(t => t == "InputCommand") < 2; i++)
        {
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        JsonElement[] commands = server.ReceivedMessages
            .Where(m => m.GetProperty("messageType").GetString() == "InputCommand")
            .ToArray();
        Assert.Equal(2, commands.Length);
        Assert.Equal(1L, commands[0].GetProperty("sequence").GetInt64());
        Assert.Equal(2L, commands[1].GetProperty("sequence").GetInt64());
        Assert.Equal(HelloSha, commands[0].GetProperty("payloadSha256").GetString());
        Assert.Equal(HelloSha, commands[1].GetProperty("payloadSha256").GetString());
        Assert.Equal("bot", commands[0].GetProperty("sender").GetString());

        Assert.True(server.OrderingHeld);
        await client.AbortAsync("test complete");
    }

    [Fact]
    public async Task NonIncreasingRevisionDeltaFailsSubsequentWaits()
    {
        string contractPath = LocateContract()!;
        using var contract = HelloContract.Load(contractPath);
        var script = new HelloServerScript { SendDuplicateRevisionDelta = true, CloseAfterCommand = false };
        await using var server = HelloLoopbackServer.Start(script, contract);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await using var client = new HelloWireClient(new HelloWireClientOptions
        {
            ServerUrl = server.Uri,
            Role = "bot",
            ClientName = "lumio-bot",
            Contract = contract,
        });
        await client.ConnectAsync(cancellation.Token);
        await client.HandshakeAsync(cancellation.Token);
        await client.WaitForBaselineAsync(cancellation.Token);
        DeltaRecord delta = await client.WaitForDeltaAsync("browser", cancellation.Token);
        Assert.Equal(1L, delta.Revision);

        HelloWireException failure = await Assert.ThrowsAsync<HelloWireException>(
            () => client.WaitForDeltaAsync("browser", cancellation.Token));

        Assert.Equal("stale_revision", failure.Code);
    }
}

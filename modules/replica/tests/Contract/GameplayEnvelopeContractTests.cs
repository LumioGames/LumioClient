using System.Text.Json;
using Lumio.Client.Replica.Tests.Support;

namespace Lumio.Client.Replica.Tests.Contract;

public sealed class GameplayEnvelopeContractTests
{
    [Fact]
    public void FrozenC1ContractIsGameplayEnvelopeNotHelloWire()
    {
        using JsonDocument document = LoadGameplay();
        JsonElement root = document.RootElement;
        Assert.Equal("lumio.gameplay-envelope.v1", root.GetProperty("contractId").GetString());
        Assert.NotEqual("lumio.hello-wire.v1", root.GetProperty("contractId").GetString());
        Assert.Equal("utf8-json-text-frame", root.GetProperty("transport").GetProperty("encoding").GetString());
        Assert.True(root.GetProperty("mappings").TryGetProperty("chat.input", out _));
        Assert.True(root.GetProperty("mappings").TryGetProperty("chat.event", out _));
        Assert.True(root.GetProperty("mappings").TryGetProperty("chat.component", out _));
        Assert.Equal("event", root.GetProperty("mappings").GetProperty("chat.event").GetProperty("kind").GetString());
        Assert.Equal("delta-live-only", root.GetProperty("mappings").GetProperty("chat.event").GetProperty("delivery").GetString());
        Assert.Equal("persist-only", root.GetProperty("mappings").GetProperty("chat.component").GetProperty("dimensions").GetProperty("persistence").GetString());
        Assert.Contains("chat_text_too_long", root.GetProperty("errorCodes").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(512, root.GetProperty("boundedInput").GetProperty("rules").GetProperty("chatTextMaxUtf8Bytes").GetInt32());
    }

    [Fact]
    public void FrozenC1HashExampleMatchesLocalLumioBinV1Encoder()
    {
        using JsonDocument document = LoadGameplay();
        (string payload, string sha) = GameplayWireFixtures.EncodeChatEvent(1, 1, 101, "gg", 7);
        Assert.Equal(GameplayWireFixtures.ChatEventPayload, payload);
        Assert.Equal(GameplayWireFixtures.ChatEventSha256, sha);

        JsonElement example = document.RootElement.GetProperty("hash").GetProperty("examples")
            .EnumerateArray()
            .First(e => e.GetProperty("mappingId").GetString() == "chat.event");
        Assert.Equal(payload, example.GetProperty("payload").GetString());
        Assert.Equal(sha, example.GetProperty("payloadSha256").GetString());
    }

    [Fact]
    public void FrozenC1ChatEventFieldOrderIsStable()
    {
        using JsonDocument document = LoadGameplay();
        string[] order = document.RootElement
            .GetProperty("mappings")
            .GetProperty("chat.event")
            .GetProperty("fieldOrder")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToArray();
        Assert.Equal(new[] { "messageId", "roomSequence", "senderNetEntityId", "text", "appliedTick" }, order);
    }

    private static JsonDocument LoadGameplay()
    {
        string? path = WireContractLocator.LocateGameplayEnvelope();
        if (path is null)
        {
            throw Xunit.Sdk.SkipException.ForSkip(
                "gameplay-command-envelope-v1.json not found; need architecture origin/main 2b7e321 or LUMIO_GAMEPLAY_ENVELOPE_CONTRACT.");
        }

        return JsonDocument.Parse(File.ReadAllText(path));
    }
}

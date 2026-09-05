// CL-1 探针：C-1 wire 的解包 / 编包全部经 Runtime 的 C# codec（Lumio.GameRuntime.Replication.Chat）。
// JS 只把 WebSocket 文本帧原样交进来、把返回的 JSON 原样发出去，不解析任何字段。
using System;
using System.Diagnostics;
using System.Security.Cryptography;
using Lumio.GameRuntime.Ecs;
using Lumio.GameRuntime.Replication.Chat;
using Lumio.GameRuntime.Samples.Username.Components.Chat;

namespace Lumio.Client.Spike.RuntimeWasm;

public readonly record struct DecodedFrame(
    bool Succeeded,
    string? Code,
    string? Detail,
    ChatMessageEvent? Event,
    double DecodeMs);

public sealed class WireBridge
{
    private readonly ChatTypedMapping _mapping = new();
    private readonly string _connectionId;

    public WireBridge(string connectionId) => _connectionId = connectionId;

    /// <summary>Runtime codec：校验信封形状、hash、块序、LumioBinV1 载荷，并把 chat.event 解成字段。</summary>
    public DecodedFrame Decode(string frameJson)
    {
        long start = Stopwatch.GetTimestamp();
        ChatMappingResult result = _mapping.ApplyDownstream(_connectionId, frameJson);
        double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        return new DecodedFrame(result.Succeeded, result.Code, result.Detail, result.Event, ms);
    }

    /// <summary>
    /// 上行 InputCommand：LumioBinV1 载荷字节由 Runtime 的 Say() → ServerRpc → outbox 产出（Ecs 内部 WireCodec），
    /// 探针只把这些字节装进 C-1 三字段信封（hex + SHA-256），随后交 Runtime 的 ChatEnvelope.Validate 复核。
    /// Runtime HEAD 没有公开的「InputCommand 信封编码」API（只有 Validate / TryParseInputCommand），这是本卡记录的缺口之一。
    /// </summary>
    public static string EncodeChatInput(WorldManager client, string text, out int payloadBytes)
    {
        client.World.Self.Get<ChatComponent>().Say(text);
        byte[]? payload = null;
        foreach (WorldMessage message in client.DrainOutbox())
        {
            if (message is InputCommandMessage input && string.Equals(input.MappingId, ChatMapping.InputMappingId, StringComparison.Ordinal))
                payload = input.Payload.ToArray();
        }

        if (payload is null)
            throw new InvalidOperationException("Say() produced no chat.input outbox message.");
        payloadBytes = payload.Length;
        string hex = Convert.ToHexStringLower(payload);
        string digest = Convert.ToHexStringLower(SHA256.HashData(payload));
        string json = "{\"messageType\":\"InputCommand\",\"commands\":[{\"mappingId\":\"" + ChatMapping.InputMappingId +
                      "\",\"payload\":\"" + hex + "\",\"payloadSha256\":\"" + digest + "\"}]}";
        ChatMappingResult check = ChatEnvelope.Validate(json);
        if (!check.Succeeded)
            throw new InvalidOperationException("Runtime codec rejected the envelope: " + check.Code + " " + check.Detail);
        return json;
    }
}

// CL-1 探针：浏览器侧入口与 [JSExport] 面。JS 只搬字节 / 字符串，所有解包、编包、世界重建都在这里的 C#（Runtime 程序集）完成。
using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using Lumio.GameRuntime.Ecs;
using Lumio.GameRuntime.Replication.Chat;
using Lumio.GameRuntime.Samples.Username.Components.Identity;

namespace Lumio.Client.Spike.RuntimeWasm;

internal static class Program
{
    private static int Main()
    {
        Console.WriteLine("[spike] Main managedThreadId=" + Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture) +
                          " isBrowser=" + OperatingSystem.IsBrowser());
        return 0;
    }
}

public static partial class SpikeExports
{
    private static WorldManager? s_client;
    private static WireBridge? s_bridge;
    private static NetEntityId s_self;

    [JSExport]
    public static string Probe() => RuntimeProbe.Describe();

    [JSExport]
    public static string Gc() => RuntimeProbe.GcInfo();

    /// <summary>拉起客户端世界（WorldManager.Create 客户端路径）并绑定 Self；返回 JSON。</summary>
    [JSExport]
    public static string Boot(string connectionId, string selfName)
    {
        long start = Stopwatch.GetTimestamp();
        s_client?.Dispose();
        s_client = ClientWorldBootstrap.CreateWithSelf(selfName, out s_self);
        s_bridge = new WireBridge(connectionId);
        double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        int live = 0;
        foreach (NetEntityId id in s_client.World.IssuedIds) if (s_client.World.IsLive(id)) live++;
        return "{\"ok\":true,\"self\":\"" + s_self.ToHex() + "\",\"selfName\":\"" + Escape(s_client.World.Self.Get<IdentityComponent>().Name.Value) +
               "\",\"tick\":" + s_client.World.Tick + ",\"live\":" + live + ",\"bootMs\":" + F(ms) +
               ",\"ownerThread\":" + (s_client.OwnerThread?.ManagedThreadId ?? -1) + "}";
    }

    /// <summary>一条下行 WebSocket 文本帧 → Runtime codec 解包。返回 JSON（succeeded / code / event 字段 / 解包耗时）。</summary>
    [JSExport]
    public static string OnFrame(string frameJson)
    {
        if (s_bridge is null) return "{\"ok\":false,\"code\":\"not_booted\"}";
        DecodedFrame decoded = s_bridge.Decode(frameJson);
        var sb = new StringBuilder(256);
        sb.Append("{\"ok\":").Append(decoded.Succeeded ? "true" : "false");
        sb.Append(",\"code\":").Append(decoded.Code is null ? "null" : "\"" + Escape(decoded.Code) + "\"");
        sb.Append(",\"detail\":").Append(decoded.Detail is null ? "null" : "\"" + Escape(decoded.Detail) + "\"");
        sb.Append(",\"decodeMs\":").Append(F(decoded.DecodeMs));
        sb.Append(",\"bytes\":").Append(Encoding.UTF8.GetByteCount(frameJson));
        if (decoded.Event.HasValue)
        {
            ChatMessageEvent e = decoded.Event.Value;
            sb.Append(",\"event\":{\"messageId\":").Append(e.MessageId).Append(",\"roomSequence\":").Append(e.RoomSequence)
              .Append(",\"sender\":\"").Append(e.SenderNetEntityId).Append("\",\"text\":\"").Append(Escape(e.Text))
              .Append("\",\"appliedTick\":").Append(e.AppliedTick).Append('}');
        }
        else
        {
            sb.Append(",\"event\":null");
        }

        sb.Append('}');
        return sb.ToString();
    }

    /// <summary>上行：Runtime 的 Say() 产出 LumioBinV1 字节 → C-1 信封 → Runtime Validate。返回信封 JSON 或 {"error":..}。</summary>
    [JSExport]
    public static string EncodeChat(string text)
    {
        if (s_client is null) return "{\"error\":\"not_booted\"}";
        try
        {
            long start = Stopwatch.GetTimestamp();
            string json = WireBridge.EncodeChatInput(s_client, text, out int payloadBytes);
            double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            return "{\"envelope\":" + json + ",\"payloadBytes\":" + payloadBytes + ",\"encodeMs\":" + F(ms) + "}";
        }
        catch (Exception ex)
        {
            return "{\"error\":\"" + Escape(ex.GetType().Name + ": " + ex.Message) + "\"}";
        }
    }

    /// <summary>「按键 → 预测世界更新」的近似：owner 字段本地写（走 Sync 本地生效 + 自动上行路径）。返回耗时 ms。</summary>
    [JSExport]
    public static double LocalWrite(string value)
    {
        if (s_client is null) return -1;
        long start = Stopwatch.GetTimestamp();
        s_client.World.Self.Get<IdentityComponent>().Name.Value = value;
        int outbound = s_client.DrainOutbox().Count;
        double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        return outbound > 0 ? ms : -ms;
    }

    /// <summary>重建基准：CreateFromSnapshot + inputs 条近似输入 + 一次 Tick，重复 repeats 次。返回 JSON 数组。</summary>
    [JSExport]
    public static string Rebuild(byte[] snapshot, int inputs, int repeats)
    {
        var sb = new StringBuilder();
        sb.Append('[');
        for (int i = 0; i < repeats; i++)
        {
            RebuildSample s = RebuildBench.Run(snapshot, inputs);
            if (i > 0) sb.Append(',');
            sb.Append("{\"createMs\":").Append(F(s.CreateMs)).Append(",\"applyMs\":").Append(F(s.ApplyMs))
              .Append(",\"totalMs\":").Append(F(s.TotalMs)).Append(",\"livePlayers\":").Append(s.LivePlayers)
              .Append(",\"hash\":\"").Append(s.Hash.ToString("x16", CultureInfo.InvariantCulture)).Append("\"}");
        }

        sb.Append(']');
        // 基准结束后把当前客户端世界重新登记为 EcsRegistry.Current 的宿主（CreateFromSnapshot 会新建 Manager）。
        return sb.ToString();
    }

    [JSExport]
    public static string DiffJson(int entities, int frame) => PresentationDiff.BuildJson(entities, frame);

    [JSExport]
    public static int[] DiffPacked(int entities, int frame) => PresentationDiff.BuildPacked(entities, frame);

    [JSExport]
    public static int Ping(int value) => value + 1;

    [JSExport]
    [return: JSMarshalAs<JSType.BigInt>]
    public static long EchoBigInt([JSMarshalAs<JSType.BigInt>] long value) => value + 1;

    [JSExport]
    [return: JSMarshalAs<JSType.Number>]
    public static long EchoNumber([JSMarshalAs<JSType.Number>] long value) => value + 1;

    [JSExport]
    public static double EchoDouble(double value) => value + 1.0;

    [JSExport]
    public static string EchoString(string value) => value;

    private static string F(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)
             .Replace("\n", "\\n", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal);
}

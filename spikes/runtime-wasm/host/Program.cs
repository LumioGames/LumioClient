// CL-1 探针宿主。wire 语义照 LumioServer modules/process/src/entity_chat/{wire,host}.rs：
//   · 纯 WebSocket（无子协议），首帧 {"connectionId":"..."}；之后每帧文本当 C-1 InputCommand 解析（解不出即忽略）
//   · 连接绑定后先发 FullSnapshot（entity.identity 块），之后每 tick 广播 Delta 帧（chat.event 每条一帧；无事件也发一帧空 Delta）
//   · 准入在 Rust 宿主里由 suite 驱动（Account Server 凭证经 verify_admission）；本替身按 connectionId 直接准入，不复现 ed25519 验签
// 所有 JSON 帧由 Runtime 的 ChatEnvelope（C# codec）产出 / 解析；本文件不写字段级协议。
// 用法：dotnet run -c Release --project host -- [--port N] [--bots N] [--bot-chat-per-tick K] [--tick-hz 20] [--delay-ms D] [--verbose]
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Lumio.GameRuntime.Ecs;
using Lumio.GameRuntime.Replication.Binding;
using Lumio.GameRuntime.Replication.Chat;
using Lumio.GameRuntime.Samples.Username;
using Lumio.GameRuntime.Samples.Username.Components.Identity;

namespace Lumio.Client.Spike.RuntimeWasm.Host;

internal sealed class Options
{
    public int Port;
    public int Bots = 100;
    public int BotChatPerTick = 1;
    public int TickHz = 20;
    public int DelayMs;
    public bool Verbose;
    public string Room = "room-main";

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException("missing value for " + args[i]);
            switch (args[i])
            {
                case "--port": o.Port = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--bots": o.Bots = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--bot-chat-per-tick": o.BotChatPerTick = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--tick-hz": o.TickHz = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--delay-ms": o.DelayMs = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--room": o.Room = Next(); break;
                case "--verbose": o.Verbose = true; break;
                default: throw new ArgumentException("unknown argument " + args[i]);
            }
        }

        return o;
    }
}

internal sealed class Session
{
    public required string ConnectionId;
    public required ConnectionBinding Binding;
    public readonly List<Channel<(string Text, long DueTicks)>> Egress = new();
}

internal sealed class OwnerState
{
    public required EntityBindingQuery Bindings;
    public required ChatCommandRuntime Chat;
    public readonly Dictionary<string, Session> Sessions = new(StringComparer.Ordinal);
    public readonly Dictionary<string, List<Channel<(string Text, long DueTicks)>>> PendingEgress = new(StringComparer.Ordinal);
    public readonly List<string> BotConnections = new();
    public ulong TickId;
    public long EventsBroadcast;
    public long FramesBroadcast;
    public long InputsAdmitted;
    public long InputsRejected;
    public int BotRoundRobin;
}

internal static class Program
{
    private static readonly ConcurrentQueue<Action<OwnerState>> Work = new();
    private static Options s_options = new();
    private static TextWriter s_log = Console.Error;

    private static async Task<int> Main(string[] args)
    {
        s_options = Options.Parse(args);
        int port = s_options.Port != 0 ? s_options.Port : FreePort();
        var ready = new TaskCompletionSource<bool>();
        var owner = new Thread(() => OwnerLoop(ready)) { IsBackground = true, Name = "spike-host-owner" };
        owner.Start();
        await ready.Task.ConfigureAwait(false);

        var listener = new HttpListener();
        listener.Prefixes.Add("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/");
        listener.Start();
        Console.Out.WriteLine("HOST_READY {\"wsUri\":\"ws://127.0.0.1:" + port + "/\",\"port\":" + port + ",\"pid\":" + Environment.ProcessId +
                              ",\"bots\":" + s_options.Bots + ",\"botChatPerTick\":" + s_options.BotChatPerTick + ",\"tickHz\":" + s_options.TickHz +
                              ",\"delayMs\":" + s_options.DelayMs + ",\"room\":\"" + s_options.Room + "\"}");
        Console.Out.Flush();
        if (!s_options.Verbose) Console.SetOut(TextWriter.Null); // Runtime 样板组件用 Console.WriteLine 打 log（[server] X says），默认静音
        while (true)
        {
            HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = 426;
                context.Response.Close();
                continue;
            }

            _ = Task.Run(() => ServeAsync(context));
        }
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static async Task ServeAsync(HttpListenerContext context)
    {
        WebSocket socket;
        try
        {
            HttpListenerWebSocketContext ws = await context.AcceptWebSocketAsync(subProtocol: null).ConfigureAwait(false);
            socket = ws.WebSocket;
        }
        catch (Exception ex)
        {
            Log("accept failed: " + ex.Message);
            return;
        }

        string? connectionId = null;
        var egress = Channel.CreateUnbounded<(string Text, long DueTicks)>();
        Task sender = SendLoopAsync(socket, egress.Reader);
        var buffer = new byte[64 * 1024];
        var frame = new MemoryStream();
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                frame.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    frame.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);
                if (result.MessageType == WebSocketMessageType.Close) break;
                if (result.MessageType != WebSocketMessageType.Text) continue;
                string text = Encoding.UTF8.GetString(frame.GetBuffer(), 0, (int)frame.Length);
                if (connectionId is null)
                {
                    connectionId = ParseConnectionId(text);
                    if (connectionId is null) { Log("first frame is not {connectionId}; closing"); break; }
                    string id = connectionId;
                    Work.Enqueue(state => Attach(state, id, egress));
                    continue;
                }

                // 单向人工延迟（上行）：到期后再入 owner 队列；不阻塞接收循环，避免把延迟串行叠加成积压。
                string inputText = text;
                string conn = connectionId;
                if (s_options.DelayMs > 0)
                    _ = Task.Delay(s_options.DelayMs).ContinueWith(_ => Work.Enqueue(state => Input(state, conn, inputText)), TaskScheduler.Default);
                else
                    Work.Enqueue(state => Input(state, conn, inputText));
            }
        }
        catch (Exception ex)
        {
            Log("receive loop ended: " + ex.GetType().Name + " " + ex.Message);
        }

        egress.Writer.TryComplete();
        await sender.ConfigureAwait(false);
        if (connectionId is not null) Log("closed " + connectionId);
        try { socket.Dispose(); } catch (Exception) { /* socket already gone */ }
    }

    /// <summary>入队时盖到期时间戳：到期 = 现在 + 单向延迟；发送循环按到期时间发，帧率不受延迟影响（模拟链路时延，不是串行排队）。</summary>
    private static (string Text, long DueTicks) Stamp(string text) =>
        (text, Stopwatch.GetTimestamp() + (long)(s_options.DelayMs * (Stopwatch.Frequency / 1000.0)));

    private static async Task SendLoopAsync(WebSocket socket, ChannelReader<(string Text, long DueTicks)> reader)
    {
        try
        {
            await foreach ((string text, long due) in reader.ReadAllAsync().ConfigureAwait(false))
            {
                long remaining = due - Stopwatch.GetTimestamp();
                // Stopwatch 刻度是 ns（Frequency = 1e9），不能用 TimeSpan.TicksPerSecond / Frequency 的整数除法（会得 0 → 零延迟）。
                if (remaining > 0) await Task.Delay(TimeSpan.FromSeconds(remaining / (double)Stopwatch.Frequency)).ConfigureAwait(false);
                if (socket.State != WebSocketState.Open) break;
                await socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Log("send loop ended: " + ex.GetType().Name + " " + ex.Message);
        }
    }

    private static string? ParseConnectionId(string text)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(text);
            return doc.RootElement.TryGetProperty("connectionId", out System.Text.Json.JsonElement id) && id.ValueKind == System.Text.Json.JsonValueKind.String
                ? id.GetString()
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    // ---------------- owner thread: all Runtime calls happen here (WorldManager.Start binds it) ----------------

    private static void OwnerLoop(TaskCompletionSource<bool> ready)
    {
        EcsRegistry.Current = GeneratedRegistry.Instance;
        EntityBindingQuery bindings = EntityBindingQuery.Create();
        ChatCommandRuntime chat = ChatCommandRuntime.Create(bindings);
        var state = new OwnerState { Bindings = bindings, Chat = chat };
        for (int i = 1; i <= s_options.Bots; i++)
        {
            string conn = "c-bot" + i.ToString("D2", CultureInfo.InvariantCulture);
            BindingQueryResult admitted = bindings.Admit(conn, "acct-bot" + i.ToString("D2", CultureInfo.InvariantCulture), s_options.Room, "bot");
            if (admitted.Outcome != "ok" || !admitted.Binding.HasValue) { Log("bot admit failed " + conn + " " + admitted.Code); continue; }
            SetName(bindings, admitted.Binding.Value, "Bot" + i.ToString("D2", CultureInfo.InvariantCulture));
            chat.AttachMember(s_options.Room, conn);
            state.BotConnections.Add(conn);
        }

        Log("owner ready: bots=" + state.BotConnections.Count + " registrySide=" + GeneratedRegistry.Instance.Side + " snapshotMode=" + SnapshotMode);
        ready.SetResult(true);

        var clock = Stopwatch.StartNew();
        double period = 1000.0 / s_options.TickHz;
        double next = period;
        double lastReport = 0;
        while (true)
        {
            while (Work.TryDequeue(out Action<OwnerState>? work)) work(state);
            double now = clock.Elapsed.TotalMilliseconds;
            if (now >= next)
            {
                next += period;
                if (now - next > period * 4) next = now + period;
                RunTick(state);
            }
            else
            {
                Thread.Sleep(1);
            }

            if (now - lastReport >= 5000)
            {
                lastReport = now;
                Log("tick=" + state.TickId + " sessions=" + state.Sessions.Count + " events=" + state.EventsBroadcast + " frames=" + state.FramesBroadcast +
                    " inputsAdmitted=" + state.InputsAdmitted + " inputsRejected=" + state.InputsRejected);
            }
        }
    }

    private static void SetName(EntityBindingQuery bindings, ConnectionBinding binding, string name)
    {
        NetEntityId id = NetEntityId.Parse(binding.NetEntityId);
        bindings.Manager.World.Get<IdentityComponent>(id).Name.Value = name;
    }

    private static void Attach(OwnerState state, string connectionId, Channel<(string Text, long DueTicks)> egress)
    {
        if (!state.Sessions.TryGetValue(connectionId, out Session? session))
        {
            string entityType = IsBotName(connectionId) ? "bot" : "player";
            BindingQueryResult admitted = state.Bindings.Admit(connectionId, "acct-" + connectionId, s_options.Room, entityType);
            if (admitted.Outcome != "ok" || !admitted.Binding.HasValue)
            {
                Log("attach " + connectionId + " admit rejected: " + admitted.Outcome + " " + admitted.Code + " " + admitted.Detail);
                if (!state.PendingEgress.TryGetValue(connectionId, out List<Channel<(string Text, long DueTicks)>>? pending)) state.PendingEgress[connectionId] = pending = new List<Channel<(string Text, long DueTicks)>>();
                pending.Add(egress);
                return;
            }

            ConnectionBinding binding = admitted.Binding.Value;
            SetName(state.Bindings, binding, connectionId);
            ChatMappingResult attached = state.Chat.AttachMember(s_options.Room, connectionId);
            session = new Session { ConnectionId = connectionId, Binding = binding };
            state.Sessions[connectionId] = session;
            Log("attach " + connectionId + " admitted netEntityId=" + binding.NetEntityId + " entityType=" + binding.EntityType +
                " generation=" + binding.ConnectionGeneration + " attachMember=" + attached.Succeeded);
        }

        session.Egress.Add(egress);
        string snapshot = BuildFullSnapshot(state);
        egress.Writer.TryWrite(Stamp(snapshot));
        state.FramesBroadcast++;
        Log("fullsnapshot -> " + connectionId + " bytes=" + Encoding.UTF8.GetByteCount(snapshot) + " tick=" + state.TickId);
    }

    private static void Input(OwnerState state, string connectionId, string text)
    {
        if (!state.Sessions.TryGetValue(connectionId, out Session? session)) { Log("input from unbound " + connectionId); return; }
        if (!ChatEnvelope.TryParseInputCommand(text, out string chatText, out ChatMappingResult failure))
        {
            state.InputsRejected++;
            Log("input " + connectionId + " rejected by Runtime codec: " + failure.Code + " " + failure.Detail);
            return;
        }

        ChatMappingResult admitted = state.Chat.AdmitInput(s_options.Room, connectionId, session.Binding.ConnectionGeneration, new ChatInput(chatText));
        if (admitted.Succeeded) state.InputsAdmitted++; else state.InputsRejected++;
        Log("input " + connectionId + " text=\"" + chatText + "\" admitted=" + admitted.Succeeded + (admitted.Code is null ? string.Empty : " code=" + admitted.Code) +
            " frameBytes=" + Encoding.UTF8.GetByteCount(text));
    }

    private static void RunTick(OwnerState state)
    {
        for (int k = 0; k < s_options.BotChatPerTick && state.BotConnections.Count > 0; k++)
        {
            string bot = state.BotConnections[state.BotRoundRobin++ % state.BotConnections.Count];
            state.Chat.AdmitInput(s_options.Room, bot, 1UL, new ChatInput("tick " + state.TickId.ToString(CultureInfo.InvariantCulture)));
        }

        state.TickId++;
        ChatTickResult result = state.Chat.RunTick(state.TickId);
        // RunTick 把 outbox 里每个会话一份的 WorldChange 都摊平，同一事件会按会话数重复；按 messageId 去重后再打包。
        var events = new List<ChatMessageEvent>();
        var seen = new HashSet<ulong>();
        for (int i = 0; i < result.Events.Count; i++)
        {
            if (seen.Add(result.Events[i].MessageId)) events.Add(result.Events[i]);
        }

        IReadOnlyList<string> frames = ChatEnvelope.DeltaFrames(result.AppliedTick, result.Revision, events);
        state.EventsBroadcast += events.Count;
        foreach (Session session in state.Sessions.Values)
        {
            for (int e = session.Egress.Count - 1; e >= 0; e--)
            {
                Channel<(string Text, long DueTicks)> channel = session.Egress[e];
                bool alive = true;
                for (int f = 0; f < frames.Count && alive; f++) alive = channel.Writer.TryWrite(Stamp(frames[f]));
                if (!alive) session.Egress.RemoveAt(e); else state.FramesBroadcast += frames.Count;
            }
        }
    }

    private static bool IsBotName(string connectionId)
    {
        string name = connectionId.StartsWith("c-", StringComparison.Ordinal) ? connectionId.Substring(2) : connectionId;
        if (!name.StartsWith("bot", StringComparison.OrdinalIgnoreCase) || name.Length == 3) return false;
        for (int i = 3; i < name.Length; i++) if (!char.IsAsciiDigit(name[i])) return false;
        return true;
    }

    // ---------------- FullSnapshot with entity.identity block ----------------
    // Runtime HEAD 只公开无实体的 ChatEnvelope.FullSnapshot(tick, revision)；带 identity 记录的重载是 internal
    //（原先经 ChatCommandRuntime.BuildFullSnapshot 暴露给 HostEntry，该方法在 Runtime 7f198e5 之后不存在）。
    // 替身宿主经反射调用 internal 重载以便 FullSnapshot 携带 101 条 identity 记录（真实包体积）；反射失败则退回公开的空快照并在日志标明。
    private static string SnapshotMode = "identity-via-internal-overload";

    private static string BuildFullSnapshot(OwnerState state)
    {
        ulong revision = state.Chat.Revision;
        try
        {
            Type? recordType = typeof(ChatEnvelope).Assembly.GetType("Lumio.GameRuntime.Replication.Chat.EntityIdentityRecord");
            ConstructorInfo? ctor = recordType?.GetConstructor(new[] { typeof(ulong), typeof(string), typeof(string) });
            MethodInfo? builder = recordType is null ? null : typeof(ChatEnvelope).GetMethod(
                "FullSnapshot", BindingFlags.NonPublic | BindingFlags.Static, new[] { typeof(ulong), typeof(ulong), typeof(IReadOnlyList<>).MakeGenericType(recordType) });
            if (recordType is null || ctor is null || builder is null) throw new MissingMethodException("internal FullSnapshot overload not found");
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(recordType))!;
            BindingQueryResult bindings = state.Bindings.ListBindings(s_options.Room);
            if (bindings.Bindings is not null)
            {
                foreach (ConnectionBinding binding in bindings.Bindings)
                    list.Add(ctor.Invoke(new object[] { NetEntityId.Parse(binding.NetEntityId).Counter, binding.EntityType, string.Empty }));
            }

            return (string)builder.Invoke(null, new object[] { state.TickId, revision, list })!;
        }
        catch (Exception ex)
        {
            SnapshotMode = "public-empty-fallback: " + ex.GetType().Name;
            Log("identity FullSnapshot unavailable (" + ex.GetType().Name + " " + ex.Message + "); falling back to ChatEnvelope.FullSnapshot(tick, revision)");
            return ChatEnvelope.FullSnapshot(state.TickId, revision);
        }
    }

    private static void Log(string line) =>
        s_log.WriteLine("[host " + DateTime.UtcNow.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "] " + line);
}

// CL-1 探针：预测世界重建的近似基准。
// 正式形态（ADR-064 第 8 条：确认世界整体克隆 + 重放未确认输入）归 RT-3，尚未实现；本卡按卡面用现有 API 近似：
//   CreateFromSnapshot(bytes) 重建世界 → 把 N 条「未确认输入的效果」当作 N 个 WorldChange 包入队 → 一次 Tick 批量应用。
// 每条近似输入 = 对一个玩家实体的一条字段变化（IdentityComponent.name），走 ApplyClientBatch → ApplyWorldChange 路径。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Lumio.GameRuntime.Ecs;
using Lumio.GameRuntime.Samples.Username;
using Lumio.GameRuntime.Samples.Username.EntityTypes;

namespace Lumio.Client.Spike.RuntimeWasm;

public readonly record struct RebuildSample(double CreateMs, double ApplyMs, double TotalMs, int LivePlayers, ulong Hash);

public static class RebuildBench
{
    public static RebuildSample Run(ReadOnlyMemory<byte> snapshot, int inputs)
    {
        EcsRegistry.Current = GeneratedRegistry.Instance;
        long t0 = Stopwatch.GetTimestamp();
        WorldManager manager = WorldManager.CreateFromSnapshot(snapshot);
        long t1 = Stopwatch.GetTimestamp();
        manager.Start(Thread.CurrentThread);
        World world = manager.World;
        var players = new List<NetEntityId>();
        foreach (NetEntityId id in world.IssuedIds)
        {
            if (world.IsLive(id) && world.TypeOf(id).Is<PlayerEntity>()) players.Add(id);
        }

        ulong tick = world.Tick + 1;
        for (int i = 0; i < inputs && players.Count > 0; i++)
        {
            NetEntityId target = players[i % players.Count];
            manager.Enqueue(new WorldChangeMessage(
                tick,
                Array.Empty<CreateRecord>(),
                new[] { new FieldChange(target, "IdentityComponent", "name", "replay-" + i, ChangeReason.Sync) },
                Array.Empty<NetEntityId>(),
                Array.Empty<ClientRpcRecord>()));
        }

        manager.Tick();
        long t2 = Stopwatch.GetTimestamp();
        ulong hash = WorldHash.Compute(world);
        manager.Dispose();
        return new RebuildSample(
            Stopwatch.GetElapsedTime(t0, t1).TotalMilliseconds,
            Stopwatch.GetElapsedTime(t1, t2).TotalMilliseconds,
            Stopwatch.GetElapsedTime(t0, t2).TotalMilliseconds,
            players.Count,
            hash);
    }
}

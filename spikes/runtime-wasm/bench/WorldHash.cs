// CL-1 探针：客户端世界的确定性哈希（FNV-1a 64），用于 wasm 与桌面 net10.0 逐位比对。
// 覆盖：实例号、tick、revision、每个活实体的计数器 + 类型 wire 名 + 生成组件经 CaptureSync 暴露的全部同步字段。
// 组件名单取样板的三种组件（探针范围内已知集合；正式做法应由注册表枚举）。
using System;
using System.Text;
using Lumio.GameRuntime.Ecs;

namespace Lumio.Client.Spike.RuntimeWasm;

public static class WorldHash
{
    private const ulong Offset = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;
    private static readonly string[] ComponentNames = { "IdentityComponent", "ChatComponent", "WorldSaveComponent" };

    public static ulong Compute(World world)
    {
        var sink = new Sink { Hash = Offset };
        sink.Mix(world.InstanceId);
        sink.Mix(world.Tick);
        sink.Mix(world.Revision);
        foreach (NetEntityId id in world.IssuedIds)
        {
            if (!world.IsLive(id)) continue;
            sink.Mix(id.Counter);
            sink.Mix(world.Registry.WireName(world.TypeOf(id).ClrType));
            for (int i = 0; i < ComponentNames.Length; i++)
            {
                Component? component = world.NamedComponent(id, ComponentNames[i]);
                if (component is null) continue;
                EcsRegistry.Generated(component)?.CaptureSync(sink);
            }
        }

        return sink.Hash;
    }

    private sealed class Sink : IPersistWriter
    {
        public ulong Hash;

        public void Mix(ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                Hash ^= (byte)(value >> (i * 8));
                Hash *= Prime;
            }
        }

        public void Mix(string? value)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(value ?? string.Empty);
            Mix((ulong)utf8.Length);
            for (int i = 0; i < utf8.Length; i++)
            {
                Hash ^= utf8[i];
                Hash *= Prime;
            }
        }

        public void WriteString(string attributeId, string? value)
        {
            Mix(attributeId);
            Mix(value);
        }

        public void WriteUInt64(string attributeId, ulong value)
        {
            Mix(attributeId);
            Mix(value);
        }

        public void WriteBoolean(string attributeId, bool value)
        {
            Mix(attributeId);
            Mix(value ? 1UL : 0UL);
        }
    }
}

// CL-1 探针：用现有 Runtime API 把客户端世界拉起并绑定 Self。
// 近似说明：正式的欢迎消息 / 创建记录由 R5-01 的 WorldChange wire 提供；当前 origin/main 的 C-1 wire 只有
// FullSnapshot / Delta（entity.identity / chat.event），不承载 ECS 创建记录，所以这里自造同形的
// WelcomeMessage + WorldChangeMessage 喂给 WorldManager，让 World.Self 可用（Say() 需要它）。
using System;
using System.Threading;
using Lumio.GameRuntime.Ecs;
using Lumio.GameRuntime.Samples.Username;

namespace Lumio.Client.Spike.RuntimeWasm;

public static class ClientWorldBootstrap
{
    /// <summary>与 EntityBindingQuery.Create 使用的服务器实例号一致，便于对照日志。</summary>
    public const ulong InstanceId = 0x1000000000000001UL;

    public static WorldManager CreateWithSelf(string selfName, out NetEntityId self)
    {
        EcsRegistry.Current = GeneratedRegistry.Instance;
        WorldManager manager = WorldManager.Create(GeneratedRegistry.Instance);
        manager.Start(Thread.CurrentThread);
        var worldEntity = new NetEntityId(InstanceId, 1);
        self = new NetEntityId(InstanceId, 2);
        manager.Enqueue(new WelcomeMessage(InstanceId, self));
        manager.Enqueue(new WorldChangeMessage(
            tick: 1,
            creates: new[]
            {
                new CreateRecord("world", worldEntity, Array.Empty<FieldValue>()),
                new CreateRecord("player", self, new[] { new FieldValue("IdentityComponent", "name", selfName) }),
            },
            fields: Array.Empty<FieldChange>(),
            destroys: Array.Empty<NetEntityId>(),
            rpcs: Array.Empty<ClientRpcRecord>()));
        manager.Tick();
        return manager;
    }
}

# LumioClient

> 通用客户端连接、Replica/Prediction 运行时与 Host Adapter。

## 定位

`LumioClient` 是可复用的客户端基础设施，不是具体游戏产品。它维护客户端自己的 ECS `ReplicaWorld` 和 VoxelReplicaWorld，消费 Server Snapshot/Delta，执行预测、校正和回滚，并把输入与表现边界提供给 `LumioGame`。

Server 和 Client 始终各自创建本地 Entity；跨端关联使用 `NetEntityId`，每个 ECS World 内部使用独立的 `LocalEntityId`。两端 Component 可以完全不同。

总架构基线见 [`docs/architecture/LumioGameEngine_Architecture_v0.3.md`](docs/architecture/LumioGameEngine_Architecture_v0.3.md)。

客户端 Host/平台实现可按目标平台选择技术，但 Client Gameplay、Replica/Prediction Processor 和热更程序集统一使用 C#；Native 高性能能力只通过 `LumioCoreEngine` Rust 产物接入。

## 拥有的状态与生命周期

- Connection、握手、断线、重连、Ack、缺口检测、Resync 和 Endpoint 状态。
- Client `ReplicaWorld` 的 Local Entity/Component、Snapshot Revision、预测命令缓冲、确认、校正和回滚状态。
- Client VoxelReplicaWorld 的 Chunk、Revision、Streaming 和预测视图（权威修改仍来自 Server）。
- 输入采样、表现输出、Headless Bot 控制和渲染/平台 Adapter 生命周期。

## 职责

- 连接公共 DS、玩家 DS、本机 DS，或在 LocalEmbedded 中连接 InMemoryTransport。
- 解析并校验 RPC Envelope，应用 Snapshot/Delta、Revision、Ack、缺口和 Resync。
- 提供 Prediction、Authority Confirmation、Correction、Rollback 和可回放 Command Stream。
- 只加载 `LumioCoreEngine` 的统一 Native 平台包，并通过 Runtime Adapter 使用 Voxel 能力。
- 创建 Client Role 的 ECS World，加载 Client Gameplay Assembly，驱动 Replica/Prediction Processor。
- 为 Unity、自研 Renderer、移动端和 Headless Bot 提供输入、表现、平台和诊断边界。

## 明确不负责什么

- 不成为 Server 权威状态源，不信任客户端预测作为最终结果。
- 不包含具体 UI、角色、关卡、技能、内容资产或商业配置。
- 不保存服务器完整 ECS/Gameplay 权威副本，也不强制对端 Component 对称。
- 不重新定义 Native ABI、Voxel ABI、RPC Envelope 或 Game Gameplay Schema。
- 不把 Renderer、Unity 类型、DOM、平台 UI 或 Socket 实现反向污染 `LumioGameRuntime`。
- 不依赖 `LumioGame` 源码；只加载其发布的 Client Gameplay Assembly 和内容包。

## 对外产物与契约

- `lumio-client` Headless/平台 Host、连接与 Replica API、Prediction/Correction API。
- Client Host Adapter、Input/Presentation Channel、Bot Driver 和诊断接口。
- `ClientHostManifest`：Core Engine、Runtime、网络协议、GameRelease、平台和 Artifact Hash。

## Source / Compile-Time Dependencies

- `LumioGameRuntime` 稳定 ECS/Replica/GAS 接口。
- `LumioCoreEngine` 统一 Native 平台包和生成 Managed Contract；不直接引用 NativeCore/VoxelEngine 源码。
- Server 公开的 RPC Envelope/Endpoint Contract；不引用 `LumioServer` 实现。
- 平台 SDK、Headless Host 和经审核的客户端基础包。

## Generated Contract Dependencies

消费 RPC Envelope、MessageId、Snapshot/Delta、NetEntity 映射、Component Schema、Voxel Port 和 Game Gameplay Contract 生成物。Client Component 由 `LumioGame` 提供，Replica Apply 只按 Mapping 写入本地 Component。

## Runtime Loading Relationships

```text
LumioClient Host / LocalEmbedded ClientRoleHost
  -> LumioCoreEngine (one unified native package)
  -> LumioGameRuntime stable host
  -> ClientGameplay.dll + generated contracts + Content
  -> Client ReplicaWorld + VoxelReplicaWorld
```

LocalEmbedded 的 Client Role 与 Server Role 同进程但使用不同 ECS/实体/体素世界，通过 InMemoryTransport 经过完整消息边界。

## Release Composition Relationships

客户端发行包由 `LumioGame` 组装：Client Host、CoreEngine 平台包、Runtime、Client Gameplay Assembly、生成契约、配置、内容和 Manifest。Client 必须与 DS 声明相同 `GameReleaseId`；不匹配时在握手阶段拒绝加入。

## Room Modes / Host Profiles

| RoomMode | Host Profile | 客户端关系 |
| --- | --- | --- |
| `Online` | `PublicDedicatedServer` | 连接公共 DS。 |
| `Online` | `PlayerHostedDedicatedServer` | 连接玩家启动的独立 DS。 |
| `Online` | `LocalhostDedicatedServer` | 连接本机独立 DS。 |
| `Singleplayer` | `LocalEmbedded` | 同进程 Client Role + Server Role + InMemoryTransport。 |

移动端第一阶段支持 LocalEmbedded 和加入远程 DS；不负责启动 Player-hosted DS。Gameplay 不读取模式布尔值，只使用 Role/Capability/Port。

## Headless Test Surface

- Replica Apply、Snapshot/Delta、Revision、Ack、缺口、Resync 和断线重连。
- Prediction/Correction/Rollback、输入延迟、丢包/乱序/重复包和 State Hash。
- Client VoxelReplicaWorld Streaming、Chunk Diff、表现输出和 Native Headless Smoke Test。
- Bot Driver 在 `PureHeadless`、`LocalEmbedded`、`LocalSplitProcess`、`RemoteDS`、`MobileLocal` 运行同一 Scenario。
- 记录 Command Stream、Snapshot、Metrics、网络 p95/p99、帧时间和内存。

## Version / Manifest

- Client Host、网络协议、Runtime API、Core Engine ABI 和 Game Release 分别记录版本与 Hash。
- Manifest 必须声明平台、Renderer Adapter、Generated Contract、GameReleaseId、内容 Hash 和能力矩阵。
- 握手校验 Release/Schema/ABI/Capability；只允许明确兼容的 Server/Client 组合。

## 开发规范

- `NetEntityId` 用于跨端稳定关联，`LocalEntityId` 只在当前 ECS World 有效；禁止把网络 ID 当作数组索引。
- Snapshot 只通过生成 Mapping 应用；预测修改必须可被权威 Revision 回滚。
- 网络线程只写入队列，Replica/Gameplay Processor 在固定 Tick 阶段消费。
- Headless 与渲染 Host 使用同一输入、Replica、Prediction API；表现层不可成为状态真相。
- Core Engine 只通过统一包加载一次；平台差异封装在 Client Adapter。

## 当前阶段任务

- 建立 Headless Client、InMemoryTransport、ReplicaWorld/预测回滚最小闭环。
- 实现 CoreEngine 单包加载、Client Gameplay Assembly 校验和 DS 握手拒绝路径。
- 建立 Bot/Replay/网络故障场景与移动端 Local/Remote DS 测试矩阵。

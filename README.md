# LumioClient

> 通用客户端连接、Replica/Prediction Host、Unity/HybridCLR 适配和 Headless Bot 基础设施。

## 架构基线

- Baseline：`LGE-V1.2-2026-08-27`
- 唯一架构源：`LumioGameEngineArchitecture`
- 本地镜像：[`docs/architecture/LumioGameEngine_Architecture_v1.2.md`](docs/architecture/LumioGameEngine_Architecture_v1.2.md)

`LumioClient` 是客户端基础设施，不是具体游戏产品。它拥有连接、握手、ClientReplicaSession、ReplicaWorld、输入和平台适配；Runtime 提供复制/回滚机制，Game 提供具体 Component/Mapping/表现内容。Server 与 Client 永远拥有独立的本地状态。

## Architecture Gate

Handshake、Replication/Prediction、Mapping、Entity、Capability 和 Failure Bundle Schema 只维护在 `LumioGameEngineArchitecture`。连接或 Replica 行为变更必须补齐正向/失败 Fixture，并在架构源执行 `python3 tools/lumio_contract.py validate`；客户端不得通过本地快捷路径绕过 Envelope、权限或 Baseline 校验。

## 拥有的状态与生命周期

- Connection、Handshake、Endpoint、断线、重连、Transport ACK、Baseline ACK、Gap 和 Resync。
- Client `ReplicaWorld`、`VoxelReplicaWorld`、LocalEntityId、Snapshot/Revision 和预测历史。
- Input Sample、ClientCommandSeq、PredictionKey、Confirmation、Correction 和 Presentation 输出。
- Unity Host、HybridCLR Capability、Renderer/Input Adapter 和 Headless Bot 生命周期。

Client 不拥有 Server 权威状态、Server Wall Clock、Release Pool 或 Voxel 内部数据；每个 `ClientReplicaSession` 通过 `SessionId + ProductId + GameReleaseId` 与服务器逻辑关联。

## 子模块

每个模块的当前责任、明确非责任、依赖方向、失败语义和验证面以其目录内 README 为入口；模块化设计与统一 README 契约见 [`docs/specs/2026-08-27-client-module-architecture-design.md`](docs/specs/2026-08-27-client-module-architecture-design.md)。

| 子模块 | 责任 | 优先级 |
| --- | --- | --- |
| [`session`](modules/session/README.md) | ClientReplicaSession 状态机与跨模块编排 | P0 |
| [`connection`](modules/connection/README.md) | Transport Adapter、Endpoint、有界队列、超时和断线检测 | P0 |
| [`handshake`](modules/handshake/README.md) | Release/Manifest/Schema/ABI/Capability 准入校验 | P0 |
| [`replica`](modules/replica/README.md) | Snapshot/Delta/Mapping Apply、Tombstone、Gap 和 Resync | P0 |
| [`prediction`](modules/prediction/README.md) | PredictionFrame、确认、校正、回滚与命令重放驱动 | P0 |
| [`input`](modules/input/README.md) | 平台无关输入归一化、采样序列与有界缓冲 | P1 |
| [`persistence`](modules/persistence/README.md) | 客户端设置、Config/Content 缓存和可移植 Save Adapter | P1 |
| [`observability`](modules/observability/README.md) | Client Log、Metrics、Trace、Replay 与 Failure Bundle 出口 | P1 |
| [`unity-adapter`](modules/unity-adapter/README.md) | Unity Host、Renderer 和平台 Input 边界 | P1 |
| [`hybridclr-adapter`](modules/hybridclr-adapter/README.md) | Unity Client C# 热更加载与 Capability Provider | P1 |
| [`bot`](modules/bot/README.md) | Headless Host、Input/Presentation Adapter 和 Bot Driver | P1 |

## 职责

- 连接公共 DS、Player DS、Localhost DS 或 LocalEmbedded Transport。
- 校验 Envelope、Schema、Release、权限和长度，应用 FullSnapshot/Delta/Ack/Resync。
- 驱动 Runtime 的 PredictionFrame、Correction、Rollback 和未确认命令重放；不重新实现 Runtime 状态机制。
- 创建 Client Role 的 ECS/Voxel Replica World，加载 Client Gameplay Assembly 和生成 Mapping。
- 为 Unity、Desktop、iOS/Android、Headless Bot 提供 Input/Presentation/诊断适配。
- 产出可回放 Command Stream、Client State Hash、网络指标和 Failure Bundle。

## 明确不负责什么

- 不成为 Server 权威状态源，不把预测结果当最终结果。
- 不定义 Native ABI、Voxel Schema、RPC Envelope 或 Game Gameplay Schema 的唯一来源。
- 不保存服务器完整 ECS/Gameplay/Voxel 权威副本，不强制 Component 对称。
- 不把 Unity 类型、DOM、平台 UI 或 Renderer 细节下沉到 GameRuntime。
- 不直接加载第二套 NativeCore/VoxelEngine；只使用 CoreEngine 统一包。

## Client Session 状态机

```text
Disconnected -> Connecting -> Negotiating -> Synchronizing -> Active
Active -> Resyncing -> Active
Active/Resyncing -> Reconnecting -> Synchronizing
Any state -> Closed / Faulted
```

进入 `Active` 前必须完成 Release/Manifest/Schema/ABI/Capability 校验、精确 Gameplay Scope 激活和 FullSnapshot。Resync 期间继续采样输入的策略必须由 Host Profile 指定：默认缓冲并限制长度，超过窗口丢弃并产生诊断事件。

## Replication 与 Prediction

Transport ACK 与 Baseline ACK 分离。Delta 必须带 BaseSnapshot、From/To Revision、Sequence 和 Mapping Hash；未知 Baseline、Gap、旧 Revision、Tombstone 冲突或历史窗口不足直接请求 Full Resync。

权威更新顺序由 Runtime 统一：验证 Baseline/Revision → 恢复最近 Confirmed PredictionFrame → 原子应用 ECS/GAS/Voxel 权威结果 → 删除已确认命令 → 原序重放未确认命令 → 生成表现差异。该链条构成单一 Runtime 事务提交，任一步失败不推进 Baseline、Confirmed Point 或 Ack。Client 只负责何时预测、何时请求校正和如何呈现。

`NetEntityId` 为 128 位不透明逻辑身份；`LocalEntityId` 只在 Client World 有效。预测生成实体使用独立临时命名空间，确认包提供重映射；Destroy Tombstone 防止迟到 Delta 复活实体。

## Transport 与 LocalEmbedded

LocalEmbedded 使用与 DS 相同的 Schema、Serializer、Envelope、权限校验、大小限制、有界队列和 Tick 交付；可以绕过 Socket/TLS/OS 网络栈，但不能绕过业务协议。Fault Decorator 支持延迟、抖动、丢包、乱序、重复、断线、重连和 QueueFull。

## Unity 与 HybridCLR

所有 Unity Client（Desktop、iOS、Android）可将 HybridCLR 作为 Platform Capability 使用。稳定 Runtime/Host 与 Client Gameplay 的加载边界必须清晰，热更包需要签名、Hash、GameRelease、Schema、权限和资源预算校验。Native ABI、稳定 Runtime 或存档 Schema 的破坏性变化不能通过普通热更掩盖，必须走完整 Release/重启路径。

Server 默认 CoreCLR；Server HybridCLR 仅作为后续可行性 Spike，不是 Client 的前置依赖。

## 持久化、序列化与配置

- Replica/Replay 使用生成的 Canonical Serializer；不以对象引用、内存地址或渲染状态作为真相。
- 客户端缓存和本地 Save 采用版本化 Snapshot/Hash/Checksum；与 DS 同 Release 时使用可移植格式，跨版本走 Game Migrator。
- 配置源在构建期编译为 typed table；每个 Tick 读取不可变配置快照，开发可热载，生产显式版本切换。

## 日志与观测

使用成熟 C# 日志框架和有界异步队列，输出 Diagnostic/Audit/Replay/Metric/Trace 事件；Error/Fatal 有应急落盘。事件至少带 `ProductId、GameReleaseId、SessionId、WorldId、TickId、SnapshotId、PredictionKey、TraceId`。网络队列和表现状态只进入诊断数据，不进入权威 Simulation Hash。

## Source / Compile-Time Dependencies

- `LumioGameRuntime` 稳定 ECS/Replica/GAS/Prediction 机制。
- `LumioCoreEngine` 统一 Native 包和生成 Managed Contract；不直接引用 NativeCore/VoxelEngine 源码。
- Server 公开的 Envelope/Endpoint/Handshake Contract；不引用 Server 实现。
- Unity/HybridCLR/平台 SDK 和经过供应链审查的托管包，通过 Adapter 隔离。

## Generated Contract Dependencies

消费 RPC Envelope、MessageId、Snapshot/Delta、Entity Mapping、Component Schema、Voxel Port 和 Game Gameplay Contract。Replica Apply 只能按生成 Mapping 写入本地 Component，不手写布局或 ID。

## Runtime Loading Relationships

```text
LumioClient Host / Unity Host / Headless Bot
  -> CoreEngine Loader (one package)
  -> stable GameRuntime
  -> ClientGameplay.dll / HybridCLR module
  -> ReplicaWorld + VoxelReplicaWorld
```

## Release Composition Relationships

`LumioGame` 组装 Client Host、CoreEngine、Runtime、Client Gameplay、Mapping、Config/Content 和 Manifest。客户端通过 Release Catalog/Handshake 路由到对应 Release Pool；版本不匹配时拒绝加入并显示稳定错误。

## Room Modes / Host Profiles

支持 `PublicDedicatedServer`、`PlayerHostedDedicatedServer`、`LocalhostDedicatedServer`、`LocalEmbedded`、`PureHeadless`、`NativeHeadless`、`LocalSplitProcess`、`RemoteDS` 和 `MobileLocal`。Gameplay 只使用 Role/Capability/Port，不读取 Offline/Local 布尔值。

## Headless Test Surface

- Snapshot/Delta/Mapping、Tombstone、Revision、Ack、Gap、Resync、断线重连和 Release 拒绝。
- Prediction/Correction/Rollback、输入延迟、丢包/乱序/重复和 Client State Hash。
- LocalEmbedded 同 Codec/同权限/有界队列保真度；LocalSplitProcess 端口与进程隔离。
- Unity/HybridCLR 设备 Smoke、AOT/包体/内存/启动时长和热更回滚。
- Headless Bot 复用同一连接/Replica/Prediction API，替换 Input/Presentation Adapter。
- Client 日志背压、缓存损坏、Save/Load、Failure Bundle 和 Replay 首差异。

## Version / Manifest

`ClientHostManifest` 至少包含 Product/GameRelease、Platform、Renderer/HybridCLR Capability、Runtime API、Core ABI、Network/Replication Protocol、Generated Contract、Config/Content Hash、Signature 和 SBOM。握手精确校验，不做未经声明的跨 Release 推断。

## 开源优先与供应链

优先复用成熟连接、序列化、日志、指标、Unity/HybridCLR 和测试方案；所有依赖锁定版本/Commit、许可证、SBOM、漏洞、AOT、确定性和性能检查。默认优先宽松许可证，第三方类型不得穿过稳定接口。

## 开发规范

- 网络线程只入队；Replica/Prediction Processor 在 Runtime 固定 Phase 消费。
- 表现层不能成为状态真相；所有预测必须有可回滚边界。
- 不把 Server/Client World 合并以“优化”移动端资源。
- 连接、重连、维护、更新和错误都必须写入可诊断事件。

## 当前阶段与开发节奏

1. **Architecture Gate**：冻结 Replication/Prediction/Resync、Client Session、Handshake 和 Platform Capability。
2. **Foundation**：实现 Headless Connection、Replica Apply、Input Buffer、Local Transport 和 CoreEngine 单加载。P1 模块只进最小切片：`observability` 的内存 Sink、结构化事件与 QueueFull 证据，`bot` 的最小 Headless Host，`input` 的有界 Sample Queue 与无玩法依赖的测试命令入口；`persistence` 可后置。
3. **Vertical Slice**：接入不对称 Mapping、PredictionFrame、Replay、Save/Load、配置快照和日志证据。
4. **Production Hardening**：LocalSplitProcess、RemoteDS、Unity/HybridCLR、断线重连、滚动 Release 和资源基线。
5. **P2**：更深移动端优化、Server HybridCLR、Mod Client 能力和跨 Release Session 迁移。

# replica

> 校验并应用权威 Snapshot/Delta/Mapping，将 Server 状态投影到独立的客户端 Replica World。

## 状态

- 阶段：R-00349 ReplicaWorld 映射与 Room 聊天呈现已落地；Snapshot/Delta 事务路径沿用既有 Stage/Observe
- 优先级：P0
- 架构基线：`LGE-V1.2-2026-08-27`
- 公共契约来源：[`World、ECS 与 Entity`](../../docs/architecture/LumioGameEngine_Architecture_v1.2.md#5-worldecs-与-entity)、[`Replication、Prediction 与网络`](../../docs/architecture/LumioGameEngine_Architecture_v1.2.md#7-replicationprediction-与网络)、架构仓 `engine/wire/gameplay-command-envelope-v1.json`（C-1）、`engine/wire/entity-binding-and-query-v1.json`（C-2）
- 内部设计：[`LumioClient 模块化架构`](../../docs/specs/2026-08-27-client-module-architecture-design.md)

## 责任

- 校验 FullSnapshot、Delta 和合法 ResyncPatch，构造 Staged Authority Plan 交由单一 Runtime 权威更新事务提交。
- 维护客户端 Baseline、SnapshotId、ReplicationRevision、Sequence 和 Mapping Hash 视图。
- 使用生成 Mapping 将 Server Component 投影到允许的 Client Component/Field。
- 维护 `NetEntityId -> LocalEntityId` 映射、Destroy Tombstone 和 provisional ID 确认重映射。
- 为每个连接维护独立客户端 World，现行 `ReplicaWorld` API 是其薄门面：持有 Runtime `WorldManager`（`ClientBootstrap.Boot()`，不传 instanceId）。FullSnapshot/Delta 解码为创建/字段变化/销毁 + ClientRpc 后 `Enqueue`；Attribute Query 读 Runtime 声明表与世界字段。`ConnectionSuperseded` 停止输入且不自动重连。
- 检测 Gap、未知 Baseline、旧 Revision、重复/迟到 Delta、Mapping 不匹配和 Tombstone 冲突。
- 输出 Apply Result、BaselineAck/DeltaAck 或明确的 ResyncRequest 原因。

## 明确不负责什么

- 不保存或修改 Server 权威 World，不要求 Server/Client Component 对称。
- 不定义 Snapshot、Delta、Entity、Mapping 或 Component Schema。
- 不维护 Prediction History，不决定如何回滚或重放未确认命令。
- 不直接操作 GameObject、Renderer、UI、DOM 或其他表现对象。聊天窗是客户端呈现副本，不是 ReplicaWorld 权威，也不从 FullSnapshot 恢复历史。
- 不手写 Component 布局、字段 ID、MessageId 或 Canonical Serializer。
- 不扩展 hello-wire-v1；玩法信封与绑定/查询以 C-1 / C-2 JSON 为字段真值。
- 不直连服务端数据库、不共享跨连接 World/Entity 存储、不对服务端 Entity 行使客户端权威。

## 公共入口与出口

**入口：** 已通过 `connection` 帧/通道校验与 `session` Active 消息门的生成 Snapshot、Delta、ResyncPatch，当前 Runtime Replica Handle 和不可变 Negotiation Result。

**出口：** Staged Authority Plan、事务提交后的 Apply Result 与 Snapshot/Revision 更新、Entity Mapping 变化、Ack、Gap/Resync 分类。Presentation Diff 由 Runtime 事务生成，本模块不生产表现真相。

输出不得暴露 Runtime Storage 内部引用；LocalEntityId 只能在当前 Client World 和 Generation 内使用。

## 数据与控制流

1. `session` 在正确 Runtime Phase 提交一批已解码的权威更新。
2. 本模块先验证 Release/Schema、Baseline、From/To Revision、Sequence、Mapping Hash 和 Tombstone。
3. 验证全部成功后，调用 Runtime/生成 Mapping 构造 Staged Authority Plan；本模块不提交核心状态。
4. `session` 将 Staged Plan 与 Prediction 的确认/重放集合一起交给单一 Runtime 权威更新事务；ECS/GAS/Voxel 的共同原子顺序由该事务保证。
5. 共同事务（含未确认命令重放）提交成功后才推进本地 Baseline/Revision 并输出 Ack；任一步失败不产生部分可见状态，不允许本模块先提交、Prediction 后补偿。
6. 无法增量恢复的情况输出 ResyncRequest，由 `session` 迁移状态。

## 依赖

- 允许依赖：[`observability`](../observability/README.md)。
- 外部依赖：`LumioGameRuntime` Replica API、生成的 Snapshot/Delta/Mapping/Entity Contracts 和 Canonical Serializer。
- 禁止依赖：`session` 具体实现、[`prediction`](../prediction/README.md)、Unity/HybridCLR、Server Storage 或 Gameplay 源码。
- `session` 负责协调本模块与 `prediction`；两者之间不建立工程引用。

## 生命周期与线程模型

- Replica Context 随 `ClientReplicaSession` 创建，在 FullSnapshot 成功后建立有效 Baseline，在 Session Close 时销毁。
- 只有 Client Owner Thread 在 Runtime 指定 Phase 修改 Replica 状态。
- 网络线程只产生不可变消息/Buffer；Decode 与 Apply 之间必须有明确大小和分配上限。
- Resync 使用新的 Baseline Generation；旧 Generation 的迟到 Delta 必须拒绝。
- FullSnapshot 丢弃旧实体投影后按 `stateBlocks` 重建；`ConnectionSuperseded` 使本代次输入关闭。

## 失败与恢复

- 可忽略但需记录：已确认的完全重复消息，前提是幂等身份匹配。
- 需 Resync：未知 Baseline、Gap、历史窗口不足、Mapping Hash 不匹配、Revision/Tombstone 冲突。
- 可拒绝：截断、超长、未知必需字段、Schema/Release 不匹配、非法 Entity 身份。
- 可致命：共同事务提交后出现部分可见状态、Runtime/Mapping 契约不一致或 Replica Storage 损坏。
- 失败不得推进 Ack 或 Revision，不得让迟到 Delta 复活已 Tombstone 的 Entity。

## 可观测性

- 记录 Snapshot/Delta 数量和字节、Apply 时长、Mapping 数量、Entity Spawn/Despawn、Gap 和 Resync 原因。
- 关联字段来自生成的 Lumio Event Schema；本模块额外提供 Baseline Generation、ReplicationRevision、Mapping Hash 和 Apply 分类，不在 README 复制共享字段清单。
- Client State Hash 与首差异信息进入 Replay/Failure Bundle；表现状态和网络时间戳不进入权威 Simulation Hash。

## 验证

- Golden：FullSnapshot、Delta、Mapping 和 Canonical Decode/Encode Fixture。
- Property：Revision 单调、重复 Apply 幂等、Entity 映射代次隔离、Tombstone 不复活。
- 失败 Fixture：未知 Baseline、Gap、旧/跳跃 Revision、重复/乱序、未知字段、Mapping Hash 冲突。
- 集成测试：ECS/GAS/Voxel 作为同一权威更新单元，失败时无部分提交。
- Fuzz：Envelope 通过后的 Snapshot/Delta/Mapping 输入仍受长度、分配和结构限制。

## 目录

- `src/Public`：`IClientReplica`、客户端 World 门面（现行类型名为 `ReplicaWorld` / `IReplicaWorld`）、`ReplicaChatConsumer`。
- `src/Internal`：权威 Stage/Observe、C-1 信封解码。C-2 声明表来自 Runtime 生成注册表，本模块不手写。
- 生成 Contract 和 Mapping 是外部构建产物，不复制到本模块维护第二套源文件。C-1/C-2 living wire JSON 由测试定位架构仓 `origin/main`，本仓不内嵌协议副本。

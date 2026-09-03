# session

> 唯一拥有 `ClientReplicaSession` 生命周期，并编排连接、握手、同步、复制、预测和关闭流程。

## 状态

- 阶段：未实现
- 优先级：P0
- 架构基线：`LGE-V1.2-2026-08-27`
- 公共契约来源：[`Session、World 与生命周期`](../../docs/architecture/LumioGameEngine_Architecture_v1.2.md#3-sessionworld-与生命周期)、[`Replication、Prediction 与网络`](../../docs/architecture/LumioGameEngine_Architecture_v1.2.md#7-replicationprediction-与网络)
- 内部设计：[`LumioClient 模块化架构`](../../docs/specs/2026-08-27-client-module-architecture-design.md)

## 责任

- 唯一拥有 `ClientReplicaSession` 状态机、Session 级取消范围和资源释放顺序。
- 编排连接建立、Handshake、Gameplay Scope 激活、FullSnapshot、BaselineAck、进入 `Active`、Resync、Reconnect、Close 和 Fault。
- 声明平台无关的 Gameplay Scope 激活端口并在 Handshake 接受后调用；预编译路径的默认实现直接返回已激活，HybridCLR 实现由 Release Composition 注入。
- 执行 Active 消息门：调用生成的 Protocol/Permission Validator 校验 SessionId、GameReleaseId、MessageId、Role、Claims 和 Connection Generation；未通过的消息不得进入 Replica/Prediction（校验矩阵见设计文档第 13 节）。
- 编排权威更新事务：收集 Replica/Prediction 的 Staged Plan、发起单一 Runtime 事务，提交成功后才允许推进元数据并发送 Ack。
- 唯一拥有 Session 级 Runtime Handle（客户端 World 与 VoxelReplicaWorld 两个独立 Handle）的创建与逆序销毁（先 Voxel 后 ECS）；在 Tick 边界唯一请求 Config staging/activation。
- 保存 `SessionId + ProductId + GameReleaseId` 关联及一次 Session 内不可变的 Capability 结果。
- 接收 Host 的 Tick 驱动，在正确的 Runtime Phase 调用 Replica/Prediction 能力。
- 决定 Resync/Reconnect 期间输入是继续有界缓冲、丢弃还是拒绝，并把策略交给 `input` 执行。
- 将 Runtime 产生的 Presentation Diff 交给 Host Adapter，不把表现状态保存为真相。

## 明确不负责什么

- 不拥有 Wall Clock、Socket、Endpoint 解析、网络线程或 Transport 重传。
- 不定义 Handshake、Envelope、Snapshot、Delta、Mapping 或 Gameplay Command Schema。
- 不保存 Replica Storage、Prediction History 内部布局或 Server 权威状态。
- 不重新实现 GameRuntime 的 ECS、GAS、PredictionFrame、Rollback 或原子权威更新机制。
- 不定义 Validator 规则内容，也不实现 Gameplay Scope 的具体校验与加载；只拥有调用时机与顺序。
- 不引用 Unity、HybridCLR、Renderer、平台 UI 或 Bot 的具体类型。

## 公共入口与出口

**入口：**

- Host 提供的连接请求、目标 Endpoint、Host Profile、取消信号和 Tick 驱动。
- `connection` 上报的已连接、已断开、入站消息和 QueueFull 事实。
- `handshake` 返回的准入结果，`replica` 返回的 Apply/Gap 结果，`prediction` 返回的确认/校正结果。
- Host 发起的正常关闭、强制关闭和进程退出通知。

**出口：**

- 可观察的 Session 状态迁移及稳定的关闭/故障结果。
- 发给 `connection` 的连接、重连、发送和断开命令。
- 发给 `handshake`、`replica`、`prediction` 和 `input` 的有序调用。
- 交给 Unity 或 Bot Host 的 Presentation Diff 和生命周期通知。

公共入口与出口只使用本仓稳定值类型、已发布 Runtime API 和生成契约；不得暴露下游实现对象。

## 数据与控制流

1. Host 创建 Session 并提供 Release、Endpoint、Profile 与 Cancellation Scope。
2. `session` 进入 `Connecting`，命令 `connection` 建立通道。
3. 通道可用后进入 `Negotiating`，由 `handshake` 完成发布与能力校验。
4. 准入成功后先经激活端口激活精确 Gameplay Scope、绑定生成 Contract/Mapping、请求 Config staging 并创建 Runtime Replica Handle；激活失败不得进入 `Synchronizing`。
5. 进入 `Synchronizing` 后由 `replica` 校验 FullSnapshot 并构造 Staged Plan，经单一 Runtime 事务原子提交并生成 BaselineAck。
6. 只有前置校验、Gameplay Scope 激活和 FullSnapshot 均成功后才进入 `Active`。
7. Active Tick 中，Session 先执行 Active 消息门，再依次转交输入、权威更新事务、预测确认/校正和表现输出，不改变 Runtime 固定 Phase；Ack 只在事务提交成功后发送。
8. Gap、未知 Baseline 或历史不足进入 `Resyncing`；传输断开进入 `Reconnecting`，重连必须重新完成通道认证与 Handshake；任一不可恢复错误进入 `Faulted`。

## 依赖

- 允许依赖：[`connection`](../connection/README.md)、[`handshake`](../handshake/README.md)、[`replica`](../replica/README.md)、[`prediction`](../prediction/README.md)、[`input`](../input/README.md)、[`persistence`](../persistence/README.md)（窄化为已验证 Artifact/Checkpoint 读取端口）、[`observability`](../observability/README.md)。
- 外部依赖：已发布的 `LumioGameRuntime` API、纯生成 Contract Artifact、Host 提供的 Clock/Profile Port，以及 Composition 注入的 Gameplay Scope 激活端口实现。
- 禁止依赖：Unity/HybridCLR SDK、Renderer/GameObject、Bot Driver、Server 实现、NativeCore/VoxelEngine 源码。
- 叶子模块不得反向依赖 `session` 的具体实现；Host Adapter 才能依赖 Session 公共入口。

## 生命周期与线程模型

- 状态集合与允许迁移以公共架构第 3.2 节为唯一来源；本模块只实现并校验该状态机，不维护本地扩展版本。
- Host 决定何时驱动 Tick；`session` 不拥有 Wall Clock。
- Session 状态只在指定 Client Owner Thread 修改。网络、IO 和平台回调必须先进入有界队列。
- Close/Fault 必须停止新输入、取消在途操作、导出诊断证据，再按 Prediction、Replica、Handshake、Connection 的逆序释放。
- 状态迁移必须幂等；重复关闭和取消不得重复释放资源。

## 失败与恢复

- 可重试：暂时性连接失败、允许重试的 Transport 错误、Host Profile 允许的超时。
- 可拒绝：Release/Schema/ABI/Capability 不匹配、权限拒绝、资源预算不满足。
- Gameplay Scope 激活失败不得进入 `Synchronizing`，按激活结果分类决定重试、稳定拒绝或 Fault。
- 需 Resync：Gap、未知 Baseline、Revision 冲突、Tombstone 冲突、Prediction 历史窗口不足。
- 可致命：状态机不变量破坏、生成契约不可信、事务结果 `Indeterminate` 且 `FaultClass` 为 `SlotStateUnproven`（进入 `Faulted`，经 Full Resync 或重启会话恢复）、资源释放失败导致状态未知。
- 事务任一步失败不得推进 Baseline、Revision、Confirmed Point，不得发送 Ack；`Aborted` 零可见副作用，由本模块按原因分类决定重试或 Resync。
- Session 必须给出唯一终态；不能停留在半初始化或“调用方自行判断”的状态。

## 可观测性

- 记录每次状态迁移、原因、持续时间、重试次数和最终结果。
- 关联字段来自生成的 Lumio Event Schema；本模块额外提供迁移前后状态、原因、持续时间和重试代次，不在 README 复制共享字段清单。
- Resync、Reconnect、拒绝和 Fault 必须可形成 Failure Bundle；状态迁移日志不能替代 Replay 或权威状态。

## 验证

- 状态机属性测试：非法迁移被拒绝，任意路径最终可进入 `Closed` 或 `Faulted`。
- 正向 Fixture：首次连接、FullSnapshot 后 Active、Gap 后 Resync、断线后 Reconnect。
- 失败 Fixture：握手拒绝、激活失败、Active 消息门拒绝、事务各步失败注入、同步超时、重复关闭、QueueFull、取消与 Fault 同时发生。
- 集成测试：LocalEmbedded 与 Remote Transport 走相同 Session 流程；Bot 与 Unity 只替换 Host Adapter。
- 资源测试：每个终态均释放队列、订阅、Cancellation Scope 和 Runtime Session Handle。

## 目录

- 当前仅包含本 README；尚未创建实现工程。
- 后续源码、工程文件和模块级测试资产必须与本 README 同地，或在此给出唯一链接。

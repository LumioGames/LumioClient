# prediction

> 驱动 Runtime 的客户端预测、确认、校正、回滚和未确认命令重放，不自行实现状态机制。

## 状态

- 阶段：未实现
- 优先级：P0
- 架构基线：`LGE-V1.0-2026-08-27`
- 公共契约来源：[`PredictionFrame`](../../docs/architecture/LumioGameEngine_Architecture_v1.0.md)、[`GAS Framework`](../../docs/architecture/LumioGameEngine_Architecture_v1.0.md)
- 内部设计：[`LumioClient 模块化架构`](../../docs/specs/2026-08-27-client-module-architecture-design.md)

## 责任

- 分配和跟踪 `ClientCommandSeq`、`PredictionKey`、未确认命令历史和 PredictionFrame 生命周期。
- 在 Host Profile 允许时，请求 Runtime 对生成的 Gameplay Command 执行本地预测。
- 消费权威 Confirmation/Correction 结果，删除已确认命令并按原序重放未确认命令。
- 管理预测历史窗口、内存预算、过期、拒绝和重基策略。
- 保证 ECS、GAS 与 Voxel Overlay 属于同一确认/回滚单元。
- 输出可发送命令、预测结果、校正结果和 Presentation Diff，不把预测结果标记为权威。

## 明确不负责什么

- 不采集 Unity/平台输入，不执行输入归一化，也不定义具体游戏操作映射。
- 不解码 Snapshot/Delta，不维护 Baseline、Entity Mapping 或 Tombstone。
- 不实现 Runtime 的 Snapshot/Restore、ECS/GAS/Voxel 状态算法。
- 不修改 Server 权威状态，不把本地 PredictionFrame 当作最终结果。
- 不直接操作 Renderer、GameObject、UI 或音频对象。

## 公共入口与出口

**入口：** 由 `session` 提交的生成 Gameplay Command、当前 Tick/Frame 上下文、Runtime Prediction Port、权威 Confirmation/Correction 和历史预算。

**出口：** 带 `ClientCommandSeq/PredictionKey` 的出站命令、Prediction Result、Correction Result、重放统计和平台无关 Presentation Diff。

本模块消费的是生成契约中的命令值，不依赖 [`input`](../input/README.md) 的采集或归一化实现。

## 数据与控制流

1. `input` 产生生成契约定义的命令值，`session` 在正确 Phase 将其提交给本模块。
2. 本模块记录命令顺序和前置 PredictionFrame，并调用 Runtime Prediction API。
3. 命令通过 `session/connection` 发送，Prediction History 保持有界。
4. 权威更新到达时，`session` 先协调 Baseline/Revision 验证，再请求 Runtime 恢复最近 Confirmed Frame。
5. Runtime 原子应用 ECS/GAS/Voxel 权威结果；本模块删除已确认命令并原序重放剩余命令。
6. 结果以平台无关差异交给 Host Adapter，历史推进到新的确认点。

## 依赖

- 允许依赖：[`observability`](../observability/README.md)。
- 外部依赖：`LumioGameRuntime` Prediction/GAS/Replica API 和生成的 Gameplay Command/Confirmation Contracts。
- 禁止依赖：`session` 具体实现、[`input`](../input/README.md)、[`replica`](../replica/README.md)、Unity/HybridCLR 或 Server 实现。
- `session` 通过生成命令值连接 `input` 与本模块，并协调本模块与 `replica`。

## 生命周期与线程模型

- Prediction Context 随 Active Session 建立，在 Resync/Reconnect 时按 Host Profile 暂停、重基或清空，在 Session Close 时销毁。
- 预测、确认、回滚和重放只在 Runtime 规定的 Client Owner Thread/Phase 执行。
- 平台回调和网络消息不能直接修改 Prediction History。
- 历史条数、字节、最大未确认 Tick 和单 Tick 命令数均必须有界。

## 失败与恢复

- 可拒绝：命令不满足生成 Schema、当前 Capability 不允许预测、命令超出预算或 Session 非 Active。
- 需校正：服务器拒绝 PredictionKey、结果偏差、确认顺序变化或权威状态更新。
- 需 Resync：最近 Confirmed Frame 已被淘汰、历史损坏或无法覆盖目标 Revision。
- 可致命：回滚后状态不可恢复、重放顺序不稳定、ECS/GAS/Voxel 只部分应用。
- 失败必须区分“命令未发送”“已发送未确认”“服务器已拒绝”，不得伪造提交成功。

## 可观测性

- 记录预测命令数、确认延迟、Correction 次数、回滚深度、重放命令数、历史水位和丢弃原因。
- 关联字段来自生成的 Lumio Event Schema；本模块额外提供 ClientCommandSeq、回滚深度、历史水位和校正分类，不在 README 复制共享字段清单。
- Replay 必须能定位首个预测差异 Tick；表现平滑参数不进入 Simulation Hash。

## 验证

- 单元测试：命令排序、确认删除、历史窗口、重复 Confirmation 和 PredictionKey 映射。
- 正向 Fixture：本地预测与服务器确认一致，无需可见校正。
- 失败 Fixture：拒绝、Correction、迟到确认、乱序确认、历史不足和重放中断。
- Property：未确认命令始终按原顺序重放；确认命令不会二次执行。
- 集成测试：ECS/GAS/Voxel Overlay 原子回滚，Replica 更新与 Prediction 校正由 Session 正确排序。

## 目录

- 当前仅包含本 README；尚未创建实现工程。
- Prediction History 的具体存储布局是内部实现，不进入公共契约或模块 README。

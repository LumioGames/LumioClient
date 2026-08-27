# input

> 将 Host 提供的平台输入样本归一化为有序、可回放、平台无关的客户端命令流。

## 状态

- 阶段：未实现
- 优先级：P1
- 架构基线：`LGE-V1.1-2026-08-27`
- 公共契约来源：[`队列与迟到输入`](../../docs/architecture/LumioGameEngine_Architecture_v1.1.md#43-队列与迟到输入)、[`Host Profile、平台与能力`](../../docs/architecture/LumioGameEngine_Architecture_v1.1.md#10-host-profile平台与能力)
- 内部设计：[`LumioClient 模块化架构`](../../docs/specs/2026-08-27-client-module-architecture-design.md)

## 责任

- 定义平台无关的 Input Sample Port，接收 Unity、Desktop、Mobile 或 Bot Host 的采样值。
- 归一化采样时间、设备值域、排序、去抖和同 Tick 合并规则。
- 唯一分配 `InputSampleSeq` 采样序列，并通过 Game 提供的生成 Mapping 产生候选 Gameplay Command 值。
- 管理 Active、Resync 和 Reconnect 期间的有界输入缓冲及明确丢弃策略。
- 输出可记录、可回放的 Command Stream，并向上层报告采样或映射拒绝。
- 保证 Host Profile 只通过 Capability/Port 驱动行为，不引入 `IsOffline` 或 `IsLocal` 分支。

## 明确不负责什么

- 不引用 Unity Input System、iOS/Android、桌面窗口系统或 Bot 脚本的具体类型。
- 不拥有具体游戏操作、按键绑定内容或 Gameplay Command Schema；这些由 Game/生成契约提供。
- 不执行 Prediction、Rollback、Replica Apply 或网络发送。
- 不分配 `ClientCommandSeq`；最终命令序号由 `prediction` 在命令被接纳时唯一分配，本模块只消费其返回的不可变结果。
- 不决定 Session 状态迁移；缓冲策略由 `session` 和 Host Profile 提供。
- 不把原始平台时间戳作为 Simulation 真相或跨平台确定性依据。

## 公共入口与出口

**入口：** 平台无关 Input Sample、采样来源身份、Host Tick 上下文、生成的 Game Input Mapping、缓冲预算和 Session 输入策略。

**出口：** 带 `InputSampleSeq` 的有序候选 Gameplay Command 值、Command Stream 记录、丢弃/拒绝结果和输入统计；不输出 `ClientCommandSeq`。

具体平台 Adapter 必须在边界外把 SDK 类型转换为稳定 Sample；本模块输出生成契约值，不输出设备对象。

## 数据与控制流

1. Unity 或 Bot Host 在自己的回调边界采样，并写入有界 Input Sample Queue。
2. Client Owner Thread 在 `ApplyInputs` 前批量读取样本，规范化时间和顺序。
3. 本模块按稳定规则合并同 Tick 样本，并调用 Game 提供的生成 Mapping。
4. 合法命令写入有界 Command Buffer；`session` 在正确 Phase 将命令交给 `prediction`。
5. Resync/Reconnect 期间按策略继续缓冲或拒绝；超过窗口时按稳定顺序丢弃并产生诊断。
6. Command Stream 以生成契约和稳定顺序进入 Replay，不保存平台 SDK 对象。

## 依赖

- 允许依赖：[`observability`](../observability/README.md)。
- 外部依赖：生成的 Game Input/Gameplay Command Mapping、Host Tick/Profile Port。
- 被依赖方：[`session`](../session/README.md)、[`unity-adapter`](../unity-adapter/README.md)、[`bot`](../bot/README.md)。
- 禁止依赖：Prediction/Replica 实现、Unity/HybridCLR SDK、Renderer、Server 实现。

## 生命周期与线程模型

- Input Context 随 Host/Session 创建；Session 未 Active 时是否接收样本由显式策略决定。
- 平台回调只写入有界队列；归一化、映射和命令排序只在 Client Owner Thread 执行。
- 每个采样源使用 Generation 隔离；设备重连或 Host 重建后的迟到样本不能进入新代次。
- 队列条数、总字节、单 Tick 样本和最大缓冲 Tick 均必须有界。

## 失败与恢复

- 可拒绝：未知采样来源、值域非法、Mapping 不支持、Session 策略禁止输入。
- 可丢弃：超过声明窗口的旧样本、允许合并的重复样本和低优先级 QueueFull 项；必须记录原因。
- 需 Fault：稳定排序不可能、生成 Mapping 与 Negotiated Schema 不一致或缓冲状态损坏。
- 任何拒绝/丢弃都不消耗 `ClientCommandSeq`（该序号由 `prediction` 分配），也不能改变已生成候选命令的相对顺序。

## 可观测性

- 记录样本数、命令数、队列水位、采样到 Tick 延迟、合并、拒绝和丢弃原因。
- 关联字段来自生成的 Lumio Event Schema；本模块额外提供采样 SourceId、队列水位、`InputSampleSeq` 和丢弃分类，不在 README 复制共享字段清单；如需关联最终命令序号，只消费 `prediction` 返回的不可变分配结果。
- 原始输入内容按产品隐私策略处理；文本、语音或敏感设备数据不得默认写日志。

## 验证

- 单元测试：值域归一化、稳定排序、同 Tick 合并、去抖和代次隔离。
- Property：相同 Sample Stream 与 Mapping 产生相同 Command Stream。
- 失败测试：QueueFull、迟到样本、未知来源、非法值、Mapping 拒绝和 Session 非 Active。
- Replay 测试：记录后的 Command Stream 无需平台 SDK 即可重放。
- 集成测试：Unity 与 Bot 输入通过同一平台无关端口，只有采样 Adapter 不同。

## 目录

- 当前仅包含本 README；尚未创建实现工程。
- 具体游戏绑定和 Mapping 由 `LumioGame` 提供，不在本模块建立默认玩法内容。

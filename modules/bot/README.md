# bot

> 提供复用生产 Client Session 主链的 Headless Host、脚本化输入和无渲染表现适配。

## 状态

- 阶段：未实现
- 优先级：P1
- 架构基线：`LGE-V1.0-2026-08-27`
- 公共契约来源：[`Host Profile、平台与能力`](../../docs/architecture/LumioGameEngine_Architecture_v1.0.md)、[`工具链、测试与可观测性`](../../docs/architecture/LumioGameEngine_Architecture_v1.0.md)
- 内部设计：[`LumioClient 模块化架构`](../../docs/specs/2026-08-27-client-module-architecture-design.md)

## 责任

- 提供 PureHeadless/NativeHeadless Client Host 和 Bot 生命周期。
- 消费 Game 提供的版本化 Scenario，生成确定性或脚本化平台无关 Input Sample。
- 提供无渲染 Presentation Adapter，消费 Presentation Diff 并提取业务断言/统计。
- 批量创建和关闭独立 `ClientReplicaSession`，执行并发资源预算和速率限制。
- 驱动连接、握手、复制、预测、Resync、Reconnect、Replay 和 Failure Bundle 测试链路。
- 输出结构化 Bot Result、Command Stream、Client State Hash 和首差异证据。

## 明确不负责什么

- 不实现简化版 Connection、Handshake、Replica、Prediction 或 LocalEmbedded 协议。
- 不拥有 Server Bot 权威逻辑、Gameplay Scenario 内容或具体游戏断言定义。
- 不依赖 Unity、Renderer、GameObject、HybridCLR 或平台 UI。
- 不共享不同 Bot/Session 的 World、Entity、Prediction History 或可变状态。
- 不把测试成功等同于 Unity 设备、Native Streaming 或生产网络验证通过。

## 公共入口与出口

**入口：** Host Profile、版本化 Scenario、Endpoint/Release 身份、Bot Seed、并发/时长预算和停止条件。

**出口：** Bot/Session 生命周期结果、结构化 Scenario Assertion、Command Stream、State Hash、网络/预测指标和 Artifact 引用。

Scenario 只声明 RequiredCapabilities 和业务步骤；Bot Host 使用与生产客户端相同的稳定 Session/Input 接口。

## 数据与控制流

1. Bot Host 校验 Scenario RequiredCapabilities 与 ProvidedCapabilities，并拒绝不匹配组合。
2. 为每个 Bot 创建独立 Host Clock、Input Driver、Presentation Adapter 和 Session。
3. Bot Driver 按 Seed/Scenario 生成 Input Sample，交给 `input` 形成与生产一致的命令流。
4. `session` 复用真实 Connection/Handshake/Replica/Prediction 主链；Bot 不插入业务协议快捷入口。
5. Presentation Adapter 消费平台无关差异并执行 Game 提供的断言，不修改 Replica 状态。
6. 达到成功、失败、超时或取消条件后有界关闭 Session，汇集 Result 与 Failure Bundle。

## 依赖

- 允许依赖：[`session`](../session/README.md)、[`input`](../input/README.md)、[`observability`](../observability/README.md)。
- 外部依赖：Game 提供的 Scenario/Assertion Contract、Host Clock 和命令行/测试运行器。
- 禁止依赖：Connection/Replica/Prediction 内部实现、Unity/HybridCLR、Server 实现或 Gameplay 源码反向引用。
- Bot 通过 `session` 公共 API 使用核心能力，不能为测试暴露生产模块内部方法。

## 生命周期与线程模型

- Bot Host 状态为 `Created -> Starting -> Running -> Stopping -> Stopped/Faulted`；每个 Session 维持自己的状态机。
- Host Clock 决定 Tick 驱动；批量 Bot 可以共享调度器，但不能共享可变 Session/World 状态。
- Scenario Driver、网络回调和 Artifact IO 通过有界队列交接，禁止为追求吞吐使用无界任务创建。
- 取消、超时和进程退出必须传播到全部子 Session，并在截止时间内导出最小证据。

## 失败与恢复

- 可拒绝：Scenario Capability 不匹配、Release/Endpoint 非法、资源预算不足。
- 可重试：Scenario 明确允许的连接暂时失败或服务不可用；重试仍走 Session 状态机。
- 测试失败：业务断言不满足、State Hash 首差异、超时、QueueFull 或预期故障未出现。
- 可致命：Bot 绕过生产主链、Session 状态互相污染、结果缺少可定位证据。
- 批量运行中单 Bot 失败按 Scenario 策略隔离，不能默认终止或污染其他 Bot。

## 可观测性

- 记录 ScenarioId、BotId、Seed、Host Profile、Session 状态、命令数、网络/预测指标、断言和最终结果。
- 关联字段来自生成的 Lumio Event Schema；本模块额外提供 ScenarioId、BotId、Seed、Host Profile 和 Artifact Hash，不在 README 复制共享字段清单。
- 大规模运行采用采样和聚合，但失败 Bot 必须保留可重放的最小 Command Stream 与 Failure Bundle。

## 验证

- 单元测试：Scenario 能力匹配、Seed 稳定性、停止条件、批量隔离和结果分类。
- 正向场景：连接、同步、输入、预测、确认、表现断言和正常关闭完整通过。
- Fault 场景：延迟、丢包、乱序、重复、断线、QueueFull、Correction、Resync 和 Release 拒绝。
- Differential：相同 Command Stream 的 Server/Replay/Client State Hash 可定位首差异 Tick。
- Stress/Soak：固定 Bot 数量和时长下记录 CPU、RSS、GC、队列、网络字节和失败率。

## 目录

- 当前仅包含本 README；尚未创建 Headless 可执行工程或 Scenario Runner。
- 具体 Scenario 和业务断言由 `LumioGame` 提供，本模块只实现通用 Host/Driver/Result 基础设施。

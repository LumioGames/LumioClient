# observability

> 统一客户端 Diagnostic、Metric、Trace、Replay 和 Failure Bundle 的事件出口与背压策略。

## 状态

- 阶段：未实现
- 优先级：P1
- 架构基线：`LGE-V1.2-2026-08-27`
- 公共契约来源：[`日志、Metrics、Trace 与审计`](../../docs/architecture/LumioGameEngine_Architecture_v1.2.md#12-日志metricstrace-与审计)、[`工具链、测试与可观测性`](../../docs/architecture/LumioGameEngine_Architecture_v1.2.md#15-工具链测试与可观测性)
- 内部设计：[`LumioClient 模块化架构`](../../docs/specs/2026-08-27-client-module-architecture-design.md)

## 责任

- 提供平台和供应商无关的 Client Event Context、事件类别和 Sink Port。
- 通过成熟日志/指标/Trace 框架的 Adapter 输出控制台、文件和可选外部 Sink。
- 管理有界异步队列、批量写入、采样、轮转、保留、脱敏和权限策略。
- 汇集 Client State Hash、Command Stream、首差异信息和可下载 Failure Bundle 元数据。
- 为 Error/Fatal 提供应急落盘路径，为可丢 Diagnostic 与不可静默丢失类别采用不同策略。
- 保留每个 Producer 的 EventSeq 和 Tick 关联，不承诺跨线程实时全局顺序。

## 明确不负责什么

- 不成为 Replica、Prediction、Session、Save、Txn Journal 或 Gameplay 状态真相。
- 不自研底层日志、Metrics 或 Trace 框架。
- 不把具体供应商 SDK 类型写入稳定公共接口。
- 不用 Diagnostic Log 替代 Replay、Command Log、Audit 或持久化恢复证据。
- 不把网络时间戳、队列状态或表现状态加入权威 Simulation Hash。

## 公共入口与出口

**入口：** 不可变 Client Event、关联 Context、Metric Sample、Trace Span、Replay/Failure Bundle 片段和 Sink 配置。

**出口：** 有界入队结果、Sink 写入结果、聚合指标、轮转文件、Trace/Replay/Failure Bundle Artifact 引用和背压诊断。

业务模块只依赖稳定 Event/Sink Port；日志实现、文件格式和外部服务客户端保持在本模块 Adapter 内。

## 数据与控制流

1. 业务模块构造带稳定 EventId、Category、Severity 和 Context 的不可变事件。
2. 入口按类别、级别、大小和脱敏规则校验，再进入对应有界队列。
3. 专用 Sink Worker 批量编码并写入控制台、文件或可选外部 Sink。
4. Diagnostic/Metrics/Trace 可按策略采样；需要保留的 Audit/Replay/Failure 证据使用独立受保护路径。
5. QueueFull 按类别丢弃、降级或阻止新接入，不能静默忽略。
6. Error/Fatal 在异步路径不可用时执行有界的同步应急落盘。

## 依赖

- 本仓模块依赖：无；本模块是稳定叶子依赖。
- 外部依赖：Lumio Event Schema、成熟的 C# 日志/Metrics/Trace 库、平台文件与可选 Sink SDK Adapter。
- 被依赖方：所有需要产生客户端诊断证据的模块。
- 禁止依赖：Session/Connection/Replica/Prediction/Input/Persistence 或平台 Adapter 的业务实现。

## 生命周期与线程模型

- Observability Host 在其他业务模块前启动，在其他模块完成证据导出后最后关闭。
- 多 Producer 只执行有界、非阻塞入队；专用 Worker 批量编码和写 Sink。
- 每个 Producer 单调递增 EventSeq；跨 Producer 的重建依赖 Tick/Trace 关联而非采样时间排序。
- Shutdown 必须声明排空期限和各类别未写完时的处置，不能无限等待。

## 失败与恢复

- 可降级：Diagnostic/Metrics/Trace Sink 暂不可用，按级别采样或切换本地文件。
- 必须阻止或进入维护：声明不可静默丢失的证据队列耗尽且无可靠落盘路径。
- 可致命：事件编码器破坏边界、递归日志导致失控、Error/Fatal 应急路径不可用。
- 外部 Sink 失败不得阻塞 Client Owner Thread，也不得触发不受控的无限重试。

## 可观测性

本模块必须观测自身：队列水位、丢弃/采样数、Sink 延迟、批量大小、轮转、磁盘使用、失败和应急落盘结果。自身诊断需要防递归，并保留最小独立计数器。

共享 Context 字段以生成的 Lumio Event Schema 为唯一来源；本 README 不维护字段副本。每类事件只填适用字段，并保留每个 Producer 的稳定 EventSeq 和可重建 Tick/Trace 关联。

## 验证

- 单元测试：分类路由、采样、脱敏、EventSeq、批量和 QueueFull 策略。
- Fault 测试：Sink 超时、磁盘满、文件权限、外部服务断开和 Shutdown 截止时间。
- Stress/Soak：高频网络与预测事件下队列有界，Client Owner Thread 不等待 Diagnostic Sink。
- Replay 测试：Command Stream、State Hash 和首差异信息可关联到同一 Session/Tick。
- 安全测试：Secret、Token、签名材料和用户敏感字段被拒绝或脱敏。

## 目录

- 当前仅包含本 README；尚未选择具体日志、Metrics 或 Trace 框架。
- 第三方 Adapter、Sink 和编码实现必须留在本模块内部，不改变稳定 Event Port。

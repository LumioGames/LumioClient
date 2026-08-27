# handshake

> 在 Client Session 激活前完成发布身份、协议、ABI 和平台能力准入校验。

## 状态

- 阶段：未实现
- 优先级：P0
- 架构基线：`LGE-V1.2-2026-08-27`
- 公共契约来源：[`Replication、Prediction 与网络`](../../docs/architecture/LumioGameEngine_Architecture_v1.2.md#7-replicationprediction-与网络)、[`Release、版本共存与更新`](../../docs/architecture/LumioGameEngine_Architecture_v1.2.md#13-release版本共存与更新)
- 内部设计：[`LumioClient 模块化架构`](../../docs/specs/2026-08-27-client-module-architecture-design.md)

## 责任

- 驱动 Handshake 请求/响应流程，并精确校验 Product、GameRelease、Manifest、Schema 和协议身份。
- 校验 Runtime API、Core ABI、Network/Replication Protocol、生成契约 Hash 和 Host Capability。
- 聚合平台 Capability Provider 的声明；HybridCLR 只是可选 Provider，不是 Unity Client 的强制依赖。
- 输出不可变的 Negotiation Result 与准入 Claims，供 `session` 先完成 Gameplay Scope 激活、再决定进入 `Synchronizing` 或稳定拒绝。
- 分类版本不匹配、能力不足、权限拒绝、签名/Hash 失败和资源预算不足。

## 明确不负责什么

- 不定义 Handshake/Manifest/Capability Schema，不推断未声明的跨 Release 兼容性。
- 不校验 Active 阶段每条消息的权限；准入只证明 Session 级资格，Active 消息门由 `session` 调用生成 Validator 执行。
- 不建立或重连 Transport；只使用 `connection` 提供的消息通道。
- 不应用 FullSnapshot、生成 BaselineAck 或进入 `Active`。
- 不实际加载 HybridCLR Assembly、CoreEngine 包或 Gameplay 内容。
- 不拥有 Release Catalog、Release Pool 路由或 Server 认证实现。

## 公共入口与出口

**入口：** 连接消息通道、期望的 Product/GameRelease、ClientHostManifest、平台 Capability Provider 集合、资源预算和取消信号。

**出口：** Accepted Negotiation Result，或带稳定类别、可诊断原因和可重试属性的 Rejection Result。

Negotiation Result 必须保留握手所依据的 Manifest/Contract Hash，后续 Session 不得在不重握手的情况下替换这些值。

## 数据与控制流

1. `session` 在 Transport Open 后启动一次 Handshake Attempt。
2. 本模块收集本地 Manifest、Runtime/Core 能力和已注册平台 Capability。
3. 通过 `connection` 发送生成契约定义的请求并接收响应。
4. 按固定顺序验证 Envelope 身份、Release、Manifest、Schema、ABI、协议、权限、能力和预算。
5. 全部成功后冻结 Negotiation Result；任一步失败立即产生明确 Rejection，不进入部分接受状态。
6. `session` 消费结果并决定同步、重试、关闭或 Fault。

## 依赖

- 允许依赖：[`connection`](../connection/README.md)、[`observability`](../observability/README.md)。
- 外部依赖：生成的 Handshake/Manifest/Capability Contract、已发布 Runtime/Core ABI 描述。
- 可选实现方：[`hybridclr-adapter`](../hybridclr-adapter/README.md) 实现本模块声明的 Capability Provider Port。
- 禁止依赖：`session` 具体实现、Replica/Prediction、Unity SDK、HybridCLR SDK、Release Catalog 实现。

## 生命周期与线程模型

- 单次尝试状态为 `Created -> Requesting -> Verifying -> Accepted/Rejected/Cancelled/Faulted`。
- 一个连接代次最多有一个生效的 Handshake Attempt；重连使用新的 AttemptId。
- 网络消息由 Client Owner Thread 从 `connection` 队列交给本模块；Capability 收集不能阻塞 Simulation Tick。
- Accepted Result 不可变，并在 Session Close 或重新 Handshake 时失效。

## 失败与恢复

- 可重试：明确标记为暂时性的服务不可用、响应超时或连接中断。
- 可拒绝：Release/Schema/ABI/Protocol 不匹配、未知必需能力、权限不足、签名/Hash 失败。
- 可致命：响应违反生成契约、校验顺序被绕过、Accepted Result 内部不一致。
- 所有拒绝必须稳定分类；UI 文案由上层产品提供，本模块只输出稳定错误身份和诊断参数。

## 可观测性

- 记录 AttemptId、阶段时长、期望与实际版本身份、Capability 集和最终分类，不记录密钥或签名私钥。
- 关联字段来自生成的 Lumio Event Schema；本模块额外提供 AttemptId、Manifest/Contract Hash 和校验阶段，不在 README 复制共享字段清单。
- 拒绝结果可进入 Failure Bundle，并能区分客户端本地校验失败与服务器明确拒绝。

## 验证

- 正向 Fixture：完全匹配的 Release/Manifest/Schema/ABI/Capability 被接受。
- 失败 Fixture：每个必需字段不匹配、未知必需 Capability、签名错误、Hash 错误、权限拒绝和预算不足。
- 状态测试：重复响应、迟到响应、取消与响应竞态、重连代次隔离。
- 契约测试：所有正向/失败 Fixture 来自架构源，客户端不得维护第二套手写布局。
- 集成测试：HybridCLR Provider 缺失时非 HybridCLR Unity Client 仍可完成合法握手。

## 目录

- 当前仅包含本 README；尚未创建实现工程。
- 将来所有 Capability Provider Port 必须保持平台无关，具体 SDK 实现在相应 Adapter 模块。

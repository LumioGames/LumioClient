# connection

> 提供可替换的客户端传输通道、Endpoint 连接机制和有界网络队列，不解释握手或游戏消息语义。

## 状态

- 阶段：未实现
- 优先级：P0
- 架构基线：`LGE-V1.2-2026-08-27`
- 公共契约来源：[`Wire 与 Transport`](../../docs/architecture/LumioGameEngine_Architecture_v1.2.md#73-wire-与-transport)、[`Host Profile、平台与能力`](../../docs/architecture/LumioGameEngine_Architecture_v1.2.md#10-host-profile平台与能力)
- 内部设计：[`LumioClient 模块化架构`](../../docs/specs/2026-08-27-client-module-architecture-design.md)

## 责任

- 解析和校验 Endpoint，建立、监测并关闭 Remote、LocalSplitProcess 或 LocalEmbedded 通道。
- 提供统一的 Transport Adapter，使 Session 不依赖具体 Socket、IPC 或内存通道 API。
- 管理网络线程与 Client Owner Thread 之间的有界 ingress/egress 队列。
- 承载 Envelope 长度、序号、完整性、分片、重传、连接级反重放和 Transport ACK 机制；通道认证与 Channel Binding 属于本层。
- 提供超时、断开检测、退避机制及 Fault Decorator 的延迟、抖动、丢包、乱序、重复和 QueueFull 注入点。
- 把连接事实和分类后的 Transport 错误上报给 `session`，不自行决定 Session 状态。

## 明确不负责什么

- 不判断 Release、Manifest、Schema、ABI、Capability 或权限是否允许进入 Session。
- 不执行 Active 消息的业务权限校验；该门由 `session` 调用生成的 Protocol/Permission Validator 完成，校验矩阵见设计文档第 13 节。
- 不生成或消费 BaselineAck，不验证 Snapshot/Delta Revision，也不维护 Prediction History。
- 不定义 Envelope、MessageId 或认证协议的公共 Schema。
- 不拥有自动重连策略；它只执行 `session` 发出的连接或重连命令。
- LocalEmbedded 不得提供绕过 Codec、Envelope、权限、大小限制或 Tick 交付的快捷入口。

## 公共入口与出口

**入口：** 已验证格式的 Endpoint、Transport Profile、连接/发送/关闭命令、生成的 Envelope 值和取消信号。

**出口：** 通道状态、入站 Envelope、Transport ACK、断开事实、QueueFull、统计快照和分类后的 Transport 错误。

公共接口以稳定 Buffer、Envelope 值和不透明 Connection Handle 表达；第三方 Socket/IPC 类型不得穿过模块边界。

## 数据与控制流

1. `session` 提交通道类型、Endpoint、资源预算和 Cancellation Scope。
2. Adapter 建立物理或进程内通道，并把连接结果写入有界事件队列。
3. 入站数据先校验基础 Frame/Envelope 边界，再写入 ingress 队列；网络回调不调用 Hot Gameplay。
4. Client Owner Thread 在固定入口批量取走消息，并交给握手或 Active Session 的正确消费者。
5. 出站 Envelope 写入有界 egress 队列，由 Transport 发送并报告 ACK/错误。
6. 断开、取消或关闭时停止接收新数据、排空或丢弃已声明类别，最后释放通道和队列。

## 依赖

- 允许依赖：[`observability`](../observability/README.md)。
- 外部依赖：生成的 Wire/Envelope Contract、经过供应链审查的 Transport 库、平台 Socket/IPC API。
- 禁止依赖：`session` 具体实现、`handshake`、`replica`、`prediction`、Unity/HybridCLR、Server 实现。
- `handshake` 可以使用本模块的消息通道；本模块不得反向理解 Handshake Payload。

## 生命周期与线程模型

- 单次连接尝试具有明确的 `Created -> Connecting -> Open -> Closing -> Closed/Faulted` 生命周期。
- 网络或平台回调只能生产队列项；Client Owner Thread 才能把消息交给 Runtime 相关流程。
- ingress/egress 容量、单消息上限和批量上限必须来自 Host Profile/Manifest，禁止无界增长。
- 重复关闭、迟到回调和连接代次切换必须使用 Generation/Token 隔离，旧连接事件不能污染新连接。

## 失败与恢复

- 可重试：DNS/拨号暂时失败、远端暂不可达、允许重试的超时。
- 可拒绝：Endpoint 格式不合法、Frame 超限、认证结果拒绝、反重放校验失败。
- 可致命：Envelope 完整性被破坏、队列或通道状态不一致、平台 API 返回不可恢复错误。
- QueueFull 必须按消息类别执行明确的丢弃、降级或断开策略并产生诊断，禁止静默覆盖。
- Transport 只报告断开事实；是否重试、退避多久和 Session 如何迁移由 `session` 决定。

## 可观测性

- 记录连接阶段时长、Endpoint 类别、发送/接收字节、消息数、重传、RTT、队列水位、丢弃和断开原因。
- 日志必须脱敏 Endpoint 凭据和认证材料，不得记录 Payload 中的密钥或用户隐私数据。
- 关联字段来自生成的 Lumio Event Schema；本模块额外提供 Connection Generation、Endpoint 类别和 Transport Profile，不在 README 复制共享字段清单。

## 验证

- 单元测试：Endpoint 校验、队列容量、代次隔离、重复关闭和取消竞态。
- 正向 Fixture：Remote、LocalSplitProcess、LocalEmbedded 传输相同 Envelope。
- 失败 Fixture：截断、超长、重复、乱序、重放、QueueFull、半连接断开和迟到回调。
- Fault 测试：延迟、抖动、丢包、乱序、重复、断线和重连组合可复现。
- 保真测试：LocalEmbedded 与 Remote 经过相同 Codec、权限、大小限制和 Tick 交付边界。

## 目录

- 当前仅包含本 README；尚未创建实现工程或选择 Transport 库。
- 具体第三方 Adapter 必须位于本模块内部边界，不能把其类型暴露给其他模块。

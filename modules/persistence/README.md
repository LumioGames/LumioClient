# persistence

> 提供客户端本地设置、Config/Content 缓存和可移植 Save 的版本化存储适配及损坏恢复边界。

## 状态

- 阶段：未实现
- 优先级：P1
- 架构基线：`LGE-V1.1-2026-08-27`
- 公共契约来源：[`持久化、序列化与配置`](../../docs/architecture/LumioGameEngine_Architecture_v1.1.md#11-持久化序列化与配置)、[`Release、版本共存与更新`](../../docs/architecture/LumioGameEngine_Architecture_v1.1.md#13-release版本共存与更新)
- 内部设计：[`LumioClient 模块化架构`](../../docs/specs/2026-08-27-client-module-architecture-design.md)

## 责任

- 保存和读取版本化客户端设置、缓存索引、已验证 Config/Content Artifact 及可移植 Save 数据。
- 通过 Adapter 隔离文件系统、平台 Key/Value Store 和 Host 提供的 Storage Port。
- 执行公共存储契约规定的 Header、完整性、压缩和资源上限校验。
- 使用临时文件、完整校验和原子替换，保留最近有效 Checkpoint 与损坏证据。
- 维护缓存容量、淘汰和 Release/Content Hash 隔离，防止跨 Release 误用。
- 向 `session` 提供已验证 Config/Content Artifact 与 Checkpoint 的窄读取端口，供其在 Tick 边界请求 Runtime Config staging。
- 将需要业务迁移的 Save 明确交给 Game Migrator，不自行猜测兼容性。

## 明确不负责什么

- 不拥有 Server WAL、权威 Snapshot、Txn Journal、Command Log 的服务器恢复语义。
- 不定义 Game Save Schema、迁移规则、配置表 Schema、配置优先级或 Canonical Serializer。
- 不决定 Config Snapshot 在哪个 Tick 激活；staging/activation 请求时机归 `session`，typed materialization 与 Tick Barrier 原子切换归 GameRuntime Config Port。
- 不把 Unity PlayerPrefs、平台文件句柄或第三方存储类型暴露给稳定模块。
- 不保存明文 Secret、认证 Token 或签名私钥；Secret 与普通配置必须分离。
- 不把缓存命中结果当作已通过 Handshake 的 Release/Manifest 准入结果。

## 公共入口与出口

**入口：** 版本化设置/缓存/Save 值、Storage Key、Release/Schema/Content 身份、生成 Serializer 和资源预算。

**出口：** 已完整校验的 typed 值、NotFound、Incompatible、Corrupted、CapacityExceeded、IOFailure 等分类结果，以及损坏恢复证据。

读取必须先完成边界校验再 materialize typed 状态；调用方不能获得未经验证的部分对象。

## 数据与控制流

1. 调用方提供值类别、Release/Schema 身份和 Storage Port。
2. 写入先生成版本化 Header 与 Canonical Payload，再写临时位置并校验长度/Hash/Checksum。
3. 成功后原子切换有效指针，最后按保留策略清理旧版本。
4. 读取先按公共存储契约校验 Header、大小、压缩和完整性，再通过生成 Serializer 解码。
5. 损坏时保留证据并回退最近有效 Checkpoint；无有效版本时返回明确失败。
6. 跨版本 Save 交给声明的 Game Migrator，迁移失败不得覆盖原数据。

## 依赖

- 允许依赖：[`observability`](../observability/README.md)。
- 外部依赖：生成的 Canonical Serializer、Game Migrator Port、平台文件/存储 API。
- 被依赖方：[`session`](../session/README.md) 只通过窄化的已验证 Artifact/Checkpoint 读取端口使用本模块；应用设置与 Content 下载缓存由 Host/Composition 在 Session 之外使用。
- 禁止依赖：Server Persistence 实现、Unity PlayerPrefs 公共类型、具体数据库/对象存储 SDK 的公共类型。

## 生命周期与线程模型

- Storage Context 由 Host 创建并在 Session/应用关闭前排空；不同 Product/Release 使用隔离命名空间。
- IO 在受控 Worker/异步队列执行，完成结果进入有界队列；Simulation/Client Owner Thread 不同步等待慢存储。
- 写入取消必须留下旧有效版本或完整新版本，不能留下被标记为有效的半成品。
- 同一 Storage Key 的并发写入必须序列化或使用显式 Revision/Compare-And-Swap 语义。

## 失败与恢复

- 可恢复：当前文件损坏但存在有效 Checkpoint、缓存项缺失或容量可通过淘汰释放。
- 可拒绝：Schema/Release 不兼容、长度/Hash/Checksum 错误、解压比或分配上限超限。
- 可致命：原子替换语义不可用且无法保证旧数据、重复失败导致存储状态未知。
- 磁盘满、权限错误、取消和进程崩溃必须有明确失败结果，不得返回伪成功。

## 可观测性

- 记录读写类别、字节、耗时、缓存命中、淘汰、损坏、回退和迁移结果；Storage Key 必须按隐私策略脱敏。
- 关联字段来自生成的 Lumio Event Schema；本模块额外提供 Storage 类别、Schema/Release 身份、字节和恢复分类，不在 README 复制共享字段清单。
- 损坏输入及元数据进入 Failure Bundle；用户内容和 Secret 不得默认附带。

## 验证

- Golden：当前版本 round-trip、旧版本读取和声明 Migrator 输入输出。
- 失败 Fixture：截断、未知必需字段、错误 Hash/Checksum、压缩炸弹、超大长度和错误 Release。
- 崩溃测试：临时写入、校验、原子替换各阶段中断后至少保留一个有效版本。
- Fault 测试：磁盘满、只读目录、权限变化、取消、并发写和存储超时。
- 平台测试：Desktop、iOS/Android Storage Port 的路径、原子性和容量语义满足声明。

## 目录

- 当前仅包含本 README；尚未创建实现工程或选择存储库。
- Adapter 可以分平台组织，但 Schema、Serializer 和 Migrator 仍来自外部权威源。

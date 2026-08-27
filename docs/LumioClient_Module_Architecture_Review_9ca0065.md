# LumioClient 模块化架构审查报告

## 审查对象与证据边界

审查对象为远端仓库目标提交 `9ca0065598edfcaf404e5bf7881b4f8ec345de7f`，其直接父提交是对比基线 `a02724c0eb475d0fa626a587aea7670c38de32b6`。GitHub 记录显示该比较范围只有 1 个提交、17 个文件变更；目标提交统计为约 `+1,510/-13` 行。

- 仓库：<https://github.com/LumioGames/LumioClient>
- 目标分支：`main`
- 审查提交：`9ca0065598edfcaf404e5bf7881b4f8ec345de7f`
- 对比基线：`a02724c0eb475d0fa626a587aea7670c38de32b6`
- 目标提交：<https://github.com/LumioGames/LumioClient/commit/9ca0065598edfcaf404e5bf7881b4f8ec345de7f>

执行环境无法访问用户本机 `/Users/cui/LumioGames/LumioClient`，也无法完成本地工作区检查，因此：

- 无法验证本机工作区的 `git status --short --branch`。
- 无法发现未提交或未推送的本地文件。
- 本报告只审查 GitHub 上目标提交的精确文件内容，不审查当前工作区漂移。

本报告中的 `文件路径:行号` 均按目标提交文件的 **1-based 行号** 标注。

会话中附带的 `LumioGameEngine_Architecture_v0.3.md` 明确只是弃用兼容指针，规范基线仍是 `LGE-V1.0-2026-08-27`；本报告未将该文件视为第二架构源。

---

# 1. 结论

## **有条件通过**

更准确地说：

| 评审面 | 裁决 |
|---|---|
| 11 个模块的总体集合 | **通过**。没有证据支持现在新增 `common`、`presentation`、`config`、`host` 或第二套 `contracts` 模块。 |
| 模块职责方向 | **基本通过**。Connection/Handshake、Replica/Prediction、Unity/HybridCLR、生产 Client/Bot 的拆分总体合理。 |
| 声明的源码依赖 DAG | **纸面无环**，但生成契约层和运行时启动顺序尚未封口，暂时不能直接映射为安全的 csproj 图。 |
| 可变状态唯一所有权 | **未完全通过**。`ClientCommandSeq`、Replica World 生命周期、Config Snapshot 激活等仍有空洞或歧义。 |
| 原子权威更新 | **未通过实现门禁**。文档依赖一个尚未被明确描述的 Runtime 原子事务边界。 |
| 安全与权限边界 | **未通过实现门禁**。Active 消息的权限校验所有者不明确。 |
| Unity + HybridCLR 启动 | **未通过实现门禁**。Handshake 之后、FullSnapshot 之前的 Gameplay Scope 激活门缺少所有者。 |
| Repository Policy | **文档阶段够用，工程阶段不够用**。 |

**未发现有充分证据可直接定性的 P0。**

但存在 5 个 P1。它们不会推翻 11 模块体系，却会使 C# 实现人员被迫自行发明关键 API、状态所有权和失败顺序。因此当前状态：

> 可以进入一轮明确的 **Architecture Closure / Implementation Planning Gate 0**，但不应直接创建完整业务实现任务并开始编码。

---

# 2. Findings

## P0

### 未发现可被现有证据直接证明的 P0

以下 P1 涉及安全和状态所有权，但因为当前没有源码，且部分语义可能已由尚未提供的 Runtime/Generated Contract 覆盖，所以不将推测升级为 P0。

---

## P1

## P1-1：权威更新的原子提交所有者没有真正闭合

### 问题

公共基线要求以下操作构成一个有序的权威校正单元：

1. 验证 Baseline/Revision。
2. 恢复最近 Confirmed PredictionFrame。
3. 原子应用 ECS/GAS/Voxel 权威结果。
4. 删除已确认命令。
5. 原序重放未确认命令。
6. 生成表现差异。

但是内部文档把这些可变状态操作分别交给了：

- `replica`：验证并“原子 Apply”、推进 Baseline/Revision、生成 Ack。
- `prediction`：删除已确认命令、重放未确认命令、推进历史。
- `session`：按顺序调用。
- Runtime：被假设能够保证整个共同原子边界。

当前文档没有说明：

- 是不是存在一个已发布的 Runtime 单调用事务 API。
- `replica` 和 `prediction` 是只构造计划，还是会分别提交内部状态。
- 如果 Replica 已成功而 Prediction 重放失败，如何回滚。
- Ack、Baseline、Confirmed Point 和 Presentation Diff 在何时变为可见。
- FullSnapshot/Resync 时是否使用同一个事务边界。

公共基线见 `docs/architecture/LumioGameEngine_Architecture_v1.0.md:281-287`；内部流程见 `docs/specs/2026-08-27-client-module-architecture-design.md:388-396`；两个模块分别声明状态修改责任，见 `modules/replica/README.md:14-18,36-40` 和 `modules/prediction/README.md:14-18,36-40`。

### 影响

最危险的错误实现是：

```text
Restore confirmed frame
-> Replica Apply 已提交
-> Prediction 删除部分命令
-> Replay 失败
-> Replica Baseline 已推进但 Prediction History 未完成
```

此时既不能安全 Ack，也不能简单重试，可能形成第二套状态真相。

### 最小修正

不新增模块。冻结以下二选一契约：

**优先方案：Runtime 单事务操作**

```text
Runtime.ApplyAuthoritativeUpdate(
    validatedAuthorityBatch,
    confirmationSet,
    pendingCommands,
    currentConfirmedFrame)
    -> AtomicUpdateResult
```

该操作内部统一完成 Restore、Apply、Confirm、Replay 和 Diff。`replica`、`prediction` 只负责校验、状态视图及事务前后元数据，不分别提交核心状态。

**备选方案：显式 Stage/Commit/Rollback**

```text
BeginClientUpdate
-> Replica.Stage
-> Prediction.Stage
-> Runtime.Commit
-> Replica.CommitMetadata
-> Prediction.CommitMetadata
-> Ack
```

必须明确任何失败都不能推进 Baseline、Revision、Confirmed Point 或 Ack。

---

## P1-2：HybridCLR 的“握手后加载、同步前可用”门没有编排所有者

### 问题

首次连接流程当前直接定义为：

```text
Handshake
-> Replica FullSnapshot
-> Active
```

但 `hybridclr-adapter` 明确要求：

```text
握手前：只报告静态平台/AOT Capability
握手成功后：取得精确 Release Artifact
-> 校验
-> 加载
-> 验证导出 Contract
-> 激活 Gameplay Scope
```

这意味着 HybridCLR Client 在 Handshake 成功后，至少还需要完成：

- 精确 Release 的 Gameplay Assembly 加载。
- 生成 Mapping/Contract 绑定。
- Gameplay Scope 激活。
- 必要 Config/Content 准备。
- ReplicaWorld 所需客户端 Component/Mapping 可用。

当前 `session` 不依赖 `hybridclr-adapter`，`unity-adapter` 也明确不直接依赖它；这是正确的源码 DAG，但没有文档说明谁在运行时把“握手完成”暂停下来，等待 Gameplay Scope Ready 后才进入 `Synchronizing`。首次连接流程见 `docs/specs/2026-08-27-client-module-architecture-design.md:374-383`；加载时序见 `modules/hybridclr-adapter/README.md:36-40`；Unity 的启动流程和可选组合见 `modules/unity-adapter/README.md:36-46`；`session` 当前依赖中没有该激活能力，见 `modules/session/README.md:54-56`。

### 影响

可能出现两种错误设计：

1. `session` 在 Gameplay Assembly/Mapping 未加载时就应用 FullSnapshot。
2. Unity/LumioGame Composition 在 `session` 外部偷偷暂停和推进状态机，破坏 `session` 的唯一编排所有权。

这不是源码循环，而是**运行时启动门缺失**。

### 最小修正

由 `session` 编排一个平台无关的、注入式的 Release Scope 激活端口，例如概念上的：

```text
IClientGameplayScopeActivator
```

正确顺序应冻结为：

```text
Transport Open
-> Handshake Accepted
-> Activate exact Gameplay Scope
-> Bind generated contracts/mapping/config
-> Create or finalize Runtime Replica handles
-> FullSnapshot
-> BaselineAck
-> Active
```

- 预编译 Gameplay Assembly 路径返回“已激活”。
- HybridCLR 路径由 adapter 实现该端口。
- `session` 不源码依赖 HybridCLR。
- 激活失败时不能进入 `Synchronizing`。
- 一个 Session 的 Gameplay Scope 应当固定，除非公共基线未来显式支持 Session 内跨 Release 迁移。

---

## P1-3：Connection、Handshake 与 Active 消息权限校验之间存在安全责任空洞

### 问题

公共基线要求 Wire/Transport 明确处理长度、序号、完整性、认证、反重放以及稳定错误分类；LocalEmbedded 也不能绕过权限检查。

当前文档定义：

- `connection` 负责 Envelope 基础边界、序号、完整性、反重放和 Transport ACK。
- `connection` 明确“不判断权限是否允许进入 Session”。
- `handshake` 校验权限、Release、Schema、ABI 等准入信息。
- `replica` 的输入却被描述为“已经通过 Envelope/权限/长度校验”。

Handshake 能证明 Session 准入，但不能自动证明每一条 Active 消息的：

- Session/Release 绑定。
- MessageId 是否允许当前 Role 接收。
- 当前权限 Claims 是否允许该消息。
- Connection Generation/Nonce 是否有效。
- Reconnect 后旧通道消息是否仍可接受。

证据分别位于 `docs/architecture/LumioGameEngine_Architecture_v1.0.md:285-287`、`modules/connection/README.md:14-18,22-25,29-31`、`modules/handshake/README.md:14-18,35-39` 和 `modules/replica/README.md:29-30`。

### 影响

Remote 与 LocalEmbedded 都可能被实现成：

```text
Connection 验证 frame
-> Session 根据 MessageId 路由
-> Replica 认为权限已检查
```

但实际上没有任何模块执行 Active 消息级授权。

### 最小修正

新增一张明确的验证所有权矩阵，不需要增加 `auth` 模块：

| 验证类型 | 唯一所有者 |
|---|---|
| Endpoint 格式、Socket/IPC 建立 | `connection` |
| TLS/IPC 对端身份、Channel Binding | `connection` |
| Frame 长度、完整性、分片、连接序号、连接级反重放窗口 | `connection` |
| 登录/Session 准入、权限 Claims、Release/Capability 协商 | `handshake` |
| Active Envelope 的 SessionId、GameReleaseId、MessageId、Role、Claims、Generation 校验 | `session` 调用生成的 Protocol/Permission Validator |
| Snapshot/Delta 的 Baseline、Revision、Mapping、Tombstone | `replica` |
| Confirmation/Correction 的预测语义 | `prediction` |

若架构源已经生成 Active Message Permission Validator，应在 README 中明确名称和调用位置；若没有，则必须先在公共架构源补契约。

---

## P1-4：可变状态所有权表仍有未闭合项

当前“每类可变状态只有一个创建、修改、快照、销毁所有者”的原则是正确的，但模块文档尚未完全达到该标准。

### 4.1 `ClientCommandSeq`

`prediction` 明确声明分配和跟踪 `ClientCommandSeq`；`input` 同时声明产生命令序列、输出 `ClientCommand` 候选序列，并在可观测字段中直接使用 `ClientCommandSeq`。如果不区分采样序列和最终网络命令序列，两边都可能分配序号。证据见：

- `docs/specs/2026-08-27-client-module-architecture-design.md:213-225`
- `modules/prediction/README.md:14-18`
- `modules/input/README.md:14-17,29-30,63-64`

建议冻结为：

- `input` 唯一拥有 `InputSampleSeq`、Sample Queue 和确定性 Mapping 输出顺序。
- `prediction` 在命令被 Session 接纳并进入预测/发送历史时，唯一分配 `ClientCommandSeq` 和 `PredictionKey`。
- 被拒绝或被 Input 丢弃的样本不得消耗 `ClientCommandSeq`。
- Input 若需要记录最终命令序号，只能消费 Prediction 返回的不可变结果。

### 4.2 `ReplicaWorld` / `VoxelReplicaWorld` 生命周期

公共基线只写了 `Client + GameRuntime` 共同负责 ReplicaWorld。内部文档中：

- `session` 不拥有 Replica Storage，但负责逆序释放和 Runtime Session Handle。
- `replica` 说 Replica Context 随 Session 创建和销毁，却没有明确谁调用 Runtime 创建、快照和销毁 World Handle。

证据见 `docs/architecture/LumioGameEngine_Architecture_v1.0.md:84-92,108-114`、`modules/session/README.md:23-24,62-63,83-84` 和 `modules/replica/README.md:49-52`。

建议冻结为：

- `session` 拥有 Session 范围 Runtime Handle 的创建和销毁顺序。
- GameRuntime 拥有实际 Storage 和 Snapshot/Restore 机制。
- `replica` 是唯一可通过该 Handle 修改权威客户端投影元数据和映射的 Client 模块。
- `prediction` 只能通过 Runtime 的预测事务修改预测 Overlay。

### 4.3 Config Snapshot

公共基线要求 Config 在激活前完成 Schema、范围、引用和 Hash/签名校验，并只在 Tick 边界原子切换不可变快照。

`persistence` 只负责缓存、存储完整性和读取，并明确“不决定 Config Snapshot 在哪个 Tick 激活”；但 `session`、`unity-adapter` 和其他模块均未承担 staging/activation 编排。证据见 `docs/architecture/LumioGameEngine_Architecture_v1.0.md:330-333` 和 `modules/persistence/README.md:14-18,22-24,36-41`。

建议冻结为：

| 状态 | 所有者 |
|---|---|
| Config/Content 原始 Artifact、缓存、Hash/Checksum、原子文件替换 | `persistence` |
| Config Schema、默认值、层级和业务引用 | `LumioGame` / 生成契约 |
| typed materialization、不可变 Snapshot、ConfigRevision | GameRuntime Config Port |
| 在哪个 Client Tick 请求 staging/activation | `session` 或顶层 Host |
| Tick Barrier 上实际原子切换 | GameRuntime |

当前不需要新增客户端 `config` 模块。

---

## P1-5：Generated Game Contract / Mapping 的工程层级未冻结，可能形成未来引用环

### 问题

公共架构的源码依赖图写的是：

```text
LumioClient -> LumioGameRuntime + Server/Runtime Contracts
LumioGame   -> LumioGameRuntime + Server/Client Host Contracts
```

并强调 Gameplay Assembly、Config/Content 和生成契约作为版本化构建产物输入 Host，不形成反向源码依赖。

内部设计先定义 `A -> B` 为源码依赖，随后又声明所有适用模块可以依赖“generated Server/Runtime/Game contracts”。与此同时：

- `input` 调用 Game 提供的生成 Input Mapping。
- `replica` 调用生成 Mapping 写入 Client Component。
- `LumioGame` 又需要依赖 Client Host Contracts。

如果“generated Game contracts”实际上编译在 `LumioGame.ClientGameplay` 中，那么容易形成：

```text
LumioClient.Input -> LumioGame.ClientGameplay
LumioGame.ClientGameplay -> LumioClient.Input/Host Contracts
```

公共源码 DAG 见 `docs/architecture/LumioGameEngine_Architecture_v1.0.md:50-60`；内部 DAG 见 `docs/specs/2026-08-27-client-module-architecture-design.md:128-155`；Mapping 消费位置见 `modules/input/README.md:14-16,36-40` 和 `modules/replica/README.md:14-16,36-38`。

### 影响

在建立 csproj 时，团队可能只能选择以下错误之一：

- 让通用 LumioClient 按每个游戏重新编译。
- 让 LumioClient 直接引用 LumioGame 源码。
- 通过反射或 Service Locator 隐藏循环。
- 在客户端再创建一套手写 contracts。

### 最小修正

明确区分三种依赖：

1. **稳定 Host/Runtime Port**  
   定义在对应 LumioClient 模块或已发布 Runtime Contract 中。

2. **工具链生成的纯 Contract Artifact**  
   不依赖 LumioClient 实现，也不依赖 Gameplay 实现；可被双方引用。

3. **Game-specific Mapper/Binding 实现**  
   位于 LumioGame Release Artifact 中，实现 `input`/`replica` 提供的稳定端口，由 Release Composition 注入。

明确禁止：

```text
LumioClient core project
    -> LumioGame.ClientGameplay implementation project
```

这不要求新增全局 `contracts` 模块；需要的是构建产物层级和端口方向，而不是新的“公共杂物程序集”。

---

## P1 小结

这 5 项都可以在不改变 11 模块集合的情况下修复：

1. 冻结 Runtime 原子更新事务。
2. 增加 Handshake 后的 Gameplay Scope 激活门。
3. 指定 Active 消息权限校验所有者。
4. 补齐可变状态所有权表。
5. 冻结 Generated Contract/Mapping 的工程层级。

---

## P2

## P2-1：`session -> persistence` 的直接依赖过宽，可能推动 God Module 演化

`session` 的声明职责是状态机和跨模块编排，但 DAG 直接允许其依赖整个 `persistence`。Session 主流程没有说明设置、缓存、Save 中哪一种是 Session 生命周期的必要依赖；`persistence` 也只笼统写 Session“可读取设置或持久化本地状态”。证据见 `modules/session/README.md:45-56` 和 `modules/persistence/README.md:43-47`。

建议：

- 应用设置、Content 下载缓存由 Host/Composition 在 Session 外处理。
- Session 只依赖明确、窄化的 Artifact/Checkpoint Port。
- 若当前没有明确的 Session 内存储用例，先从 DAG 移除 `session -> persistence`。
- 不需要拆出 `persistence-abstractions`；模块内部端口与实现可以先同程序集。

---

## P2-2：Observability 方向正确，但必须防止成为新的 shared/common 或通用 Event Bus

ADR 将其定义为无业务反向依赖的叶子，模块 README 也明确使用生成的 Lumio Event Schema，并隔离具体 SDK，这些方向是正确的。

风险在于它同时声明拥有：

- Event Context 和类别。
- Sink Port。
- Diagnostic、Metric、Trace。
- Replay、Command Stream、State Hash。
- Failure Bundle 聚合。

如果 Event Port 允许任意对象、回调或业务查询，它会变成隐式 shared/common；如果日志框架类型通过公共 API 泄露，则所有核心程序集都会产生供应商耦合。证据见 `.spec/decisions/0001-capability-modules-and-session-orchestration.md:15-16` 和 `modules/observability/README.md:13-18,29-48`。

建议保持模块，但冻结：

- EventId、Context 和共享字段来自生成 Event Schema。
- 业务模块拥有“事实”，Observability 只拥有队列、编码、背压和 Sink。
- Sink 不允许回调业务状态。
- 禁止通用对象 Payload 和 Service Locator。
- 第三方日志包使用私有依赖；若未来出现公共 API 或传递包污染，再拆“Port project / Implementation project”，不是现在新增 `common`。

---

## P2-3：Presentation Diff 和 Replay 证据的生产者需要进一步单一化

当前文档分别写道：

- `session` 转发 Runtime 产生的 Presentation Diff。
- `prediction` 输出 Presentation Diff。
- `replica` 输出供 Runtime 生成表现差异的状态变更集。
- `input` 输出 Command Stream。
- `observability` 汇集 Command Stream、State Hash 和首差异。
- `bot` 也输出这些 Artifact。

这些不一定都是冲突，因为大部分是不可变数据流，但需要明确：

```text
Runtime/Generated Presentation Projection -> 生产平台无关 Diff
session -> 只排序、关联 Session 生命周期并转发
unity-adapter/bot -> 消费
observability -> 记录 Artifact，不拥有内容真相
```

不建议创建 `presentation` 模块。只有未来出现独立缓存、插值状态、生命周期和多 Renderer 可替换实现时，才重新评估。

---

## P2-4：Session 失败事件缺少竞态优先级和终态决策表

各模块均有“可重试、可拒绝、需 Resync、可致命”等分类，这是优点；但 Session 没有给出以下竞态的统一裁决：

- Cancel 与 Handshake Accepted 同时到达。
- Disconnect 与 Release Rejection 同时到达。
- QueueFull 与正常 Close 同时到达。
- Resync 期间新 Gap 到达。
- Runtime Correction 失败后又发生 Transport Disconnect。
- Shutdown Deadline 与 Failure Bundle 导出冲突。

`session` 虽要求唯一终态和幂等迁移，但未定义事件优先级。见 `modules/session/README.md:59-70,79-83`。

建议增加状态迁移表或 reducer 规则：

```text
CurrentState + Event + Generation -> NextState + Actions
```

并冻结 `Fault > ForcedClose > Cancel > Disconnect > RetryableFailure > Success` 等优先级，具体顺序由设计确认。

---

## P2-5：模块优先级和 Foundation/Vertical Slice 退出条件没有完全对齐

模块表把 `input`、`observability` 和 `bot` 列为 P1；但 Foundation 又要求 Input Buffer、Local Transport 和 Headless 路径，P0 的失败验收还需要最小诊断证据与无 Unity 测试 Host。根 README 的模块优先级与阶段定义见 `README.md:27-37,136-140`；公共阶段退出条件见 `docs/architecture/LumioGameEngine_Architecture_v1.0.md:433-437`。

建议不要简单把整个模块改成 P0，而是定义最小切片：

### Foundation 必需切片

- `observability`：内存 Sink、结构化事件、QueueFull 证据。
- `bot`：最小 Headless Host。
- `input`：有界 Sample Queue 和无玩法依赖的测试命令入口。
- `persistence`：可以暂不进入 Foundation。

### Vertical Slice

- Game-generated Input Mapping。
- Prediction/Correction/Replay。
- Config Snapshot。
- Save/Load。
- 完整 Failure Bundle。

---

## P2-6：Repository Policy 在源码出现后不足以验证架构语义

当前工作流检查：

- README 和基线文件存在。
- 模块目录数量。
- README 标题和章节。
- 根 README 链接。
- 架构镜像 Hash。

这些适合纯文档阶段，但完全不检查项目引用、平台类型渗透、生成契约层级和原子流程。见 `.github/workflows/repository-policy.yml:39-80`。

首次建立工程时至少需要新增：

1. csproj 引用图和循环检测。
2. 每个模块允许引用边的精确 allowlist。
3. Core 模块禁止引用 UnityEngine、HybridCLR、平台 SDK。
4. Bot/Unity 只能通过 Session 公共 API 使用核心链。
5. LumioClient core 禁止引用 LumioGame implementation。
6. Public API 第三方类型泄漏检查。
7. `InternalsVisibleTo` allowlist。
8. 生成契约 Baseline/Hash 校验。
9. LocalEmbedded/Remote 合同保真测试。
10. 原子 Update 的每个故障点注入测试。
11. nullable、analyzer、warnings-as-errors、formatter。
12. locked restore、SBOM、许可证和漏洞检查。
13. Unity asmdef / AOT / HybridCLR 兼容构建矩阵。

---

## P3

### P3-1：公共契约链接没有定位到具体章节

模块 README 的“公共契约来源”均链接到架构文件根部，而不是精确 section anchor。对 445 行架构文件而言，可追溯性不足。

建议把链接改为对应标题 anchor，并在设计文档维护“模块 → 公共章节”表。

### P3-2：`persistence` 命名仍容易被误认为权威持久化

设计文档已经识别这一风险并通过“明确不负责 Server WAL/权威 Snapshot”缓解。见 `docs/specs/2026-08-27-client-module-architecture-design.md:52-64`。

当前不要求改名。若首次创建程序集时仍频繁产生误解，可使用：

```text
client-storage
```

但这是清晰度优化，不是架构阻断项。

---

# 3. 模块审查矩阵

| module | 结论 | 所有权 | 重叠/空洞 | 依赖风险 | 入口/出口、线程、失败和验证 | 必须修改 |
|---|---|---|---|---|---|---|
| `session` | **保留，不拆分** | Session 状态机、取消范围、Release/Capability 快照、跨模块顺序 | 缺少 Gameplay Scope 激活门；原子 Update 契约不闭合；直接 persistence 过宽 | 高扇出但纸面无环；必须严禁平台类型和业务逻辑进入 | Owner Thread、逆序释放和状态机测试方向正确；缺少竞态优先级和事务故障测试 | 增加内部 Update Orchestrator、激活端口、状态事件表；窄化 persistence 依赖。 |
| `connection` | **保留** | Channel、Attempt、Generation、Ingress/Egress Queue、Transport ACK、连接级重放窗口 | 认证、权限和 Envelope 语义边界仍混杂 | 只依赖 observability，方向正确 | 稳定 Buffer/Handle、回调入队、代次隔离和 Fault 测试充分 | 冻结 frame/channel auth 与 application auth/permission 的分界；不得承担 Active 业务权限。 |
| `handshake` | **保留** | Handshake Attempt、不可变 Negotiation Result、准入 Claims | 只定义准入，不负责后续 Gameplay Scope；Active 权限不能只靠握手 | `handshake -> connection` 合理；Provider Port 可由 HybridCLR 实现，无源码环 | Attempt 生命周期、拒绝分类和 Fixture 较完整 | 明确应用认证/授权结果、Claims 生命周期和 post-handshake activation continuation。 |
| `replica` | **保留** | Baseline、Revision、Sequence、Mapping、Entity Map、Tombstone | 与 prediction 的共同事务未闭合；World Handle 生命周期不明确 | 不直接依赖 prediction 是正确方向，但依赖 Runtime 原子 API 成立 | Apply/Gap/Resync 输入输出清楚，Owner Thread 和测试面较强 | 改成“验证/Stage + 事务提交元数据”；Ack 只能在共同事务成功后输出。 |
| `prediction` | **保留** | `ClientCommandSeq`、`PredictionKey`、History、Frame、Confirmed Point | 与 input 的序号边界模糊；Presentation Diff 生产者不唯一 | 不依赖 input/replica 的方向合理 | 历史预算、校正分类、重放 Property 测试充分 | 冻结最终 CommandSeq 所有权；所有 Restore/Replay 必须处于 Runtime 原子事务。 |
| `input` | **保留** | Sample Queue、InputSampleSeq、归一化、排序、映射输入、缓冲策略执行 | Game-generated Mapping 的注入/程序集边界未冻结；不能分配最终 ClientCommandSeq | 核心仅依赖 observability 是正确的；Host Adapter 可依赖它 | 平台回调入队、Owner Thread 归一化和 Replay 测试较完整 | 显式区分 InputSampleSeq 与 ClientCommandSeq；定义纯 Mapping Port 和版本绑定。 |
| `persistence` | **保留；可选改名** | 设置、缓存字节、Checkpoint、Storage Key、完整性和原子替换 | Config typed Snapshot 和 Tick 激活不属于它，但后续所有者未写清；职责略宽 | 仅依赖 observability 合理；session 直接依赖需窄化 | IO Worker、CAS、崩溃恢复和损坏 Fixture 很完整 | 增加 Artifact 生命周期与 Runtime Config handoff；限制 Session 只使用窄端口。 |
| `observability` | **保留，不合并** | Event Queue、EventSeq、Sink、轮转、采样、背压、应急落盘 | Replay/Command Stream 的内容真相属于生产模块；不得成为通用 Event Bus | 作为无内部依赖叶子正确；需防第三方包传递污染 | 自观测、背压、Fault/Soak/Security 测试充分 | 冻结生成 Event Schema 和单向 Sink Port；禁止业务回调、任意对象 Payload。 |
| `unity-adapter` | **保留** | Unity Host 生命周期、主线程、输入采样、Presentation Binding | HybridCLR/预编译 Gameplay Scope 的启动编排未进入流程 | 依赖 session/input/observability 正确；不直接依赖 HybridCLR 正确 | 主线程、Generation、设备 Smoke 和性能面完整 | 在 Composition/Session 流程中增加 Gameplay Scope Ready 门；保持 Unity 类型不外泄。 |
| `hybridclr-adapter` | **保留** | Artifact 校验、Gameplay Scope、Generation、加载/回滚/卸载 | 握手后加载、同步前激活的所有者缺失 | `hybridclr -> handshake` 用于实现 Provider Port，不构成源码环 | 安全边界、线程、回滚和设备测试较完整 | 定义两阶段 Capability/Activation 契约；Session 只能绑定已激活且 Release 固定的 Scope。 |
| `bot` | **保留** | Bot Host、Scenario Driver、Seed、资源预算、结果和批量生命周期 | 文档没有明显责任空洞；需要机器保证不能引用内部协议捷径 | 仅依赖 session/input/observability，方向正确 | 明确复用生产链，隔离、Fault、Differential、Soak 面充分 | 增加项目引用和 API 表面检查；同一合同 Fixture 必须同时跑 Bot/Unity/Remote/LocalEmbedded。 |

---

# 4. 依赖 DAG 审查

声明的内部依赖图为：

```text
unity-adapter      -> session + input + observability
bot                -> session + input + observability

session            -> connection + handshake + replica + prediction
                      + input + persistence + observability

handshake          -> connection + observability
hybridclr-adapter  -> handshake + observability

connection         -> observability
replica            -> observability
prediction         -> observability
input              -> observability
persistence        -> observability
```

该图在文档层面是无环的，ADR 也明确禁止叶子反向引用 `session`、禁止平台类型进入核心模块。

可形成如下拓扑层：

```text
Layer 0:
  observability
  published Runtime / generated stable contracts

Layer 1:
  connection
  replica
  prediction
  input
  persistence

Layer 2:
  handshake

Layer 3:
  session
  hybridclr-adapter

Layer 4:
  unity-adapter
  bot
```

## DAG 的两个条件风险

### 1. Runtime 流程边不是源码边

HybridCLR 运行时需要：

```text
handshake accepted
-> hybrid gameplay activation
-> session synchronization
```

但不能把它实现为：

```text
session -> hybridclr-adapter
```

正确做法是 Session 依赖平台无关激活端口，由顶层 Composition 注入实现。

### 2. Generated Game Contract 不应成为 LumioGame implementation 引用

未来项目图必须明确：

```text
LumioClient.* -> stable Runtime/Contract artifacts
LumioGame.ClientGameplay -> LumioClient host/module ports
Release Composition -> both
```

而不是：

```text
LumioClient.* <-> LumioGame.ClientGameplay
```

## csproj 映射裁决

11 个模块可以映射为 11 个项目，但文档目前没有强制“一模块一 csproj”。为了让依赖图可机械验证，首次工程计划应明确：

- 核心 11 模块是否各自独立程序集。
- Host 可执行项目和测试项目是否属于模块目录。
- 生成 Contract Artifact 的项目/包所有者。
- Unity asmdef 与 .NET csproj 的映射方式。
- 哪些模块允许 `InternalsVisibleTo` 测试项目。

在这些决策完成前，当前 DAG **逻辑无环，但尚未达到可执行依赖图标准**。

---

# 5. 缺失模块与疑似多余模块

| 候选模块 | 裁决 | 理由 |
|---|---|---|
| `composition` | **现在不新增模块** | Release Composition 已由 `LumioGame` 拥有。需要补一节 Composition Root 启动合同，而不是新增通用 Client 模块。 |
| `host` | **不新增** | `unity-adapter` 和 `bot` 已是两个顶层 Host Adapter；通用 Session 编排由 `session` 承担。 |
| `presentation` | **不新增** | 当前只有不可变 Presentation Diff 和 Host Binding，没有独立可变状态、发布和恢复边界。 |
| `config` | **不新增客户端模块** | Config Schema/内容属于 Game，typed Snapshot 和 Tick 激活属于 Runtime；Client persistence 只缓存 Artifact。 |
| `contracts` / `abstractions` | **不新增全局模块** | 模块端口先与实现同模块；公共 Generated Contract 必须是外部工具链 Artifact，不应在 Client 再建第二套。 |
| `client-update-coordinator` | **不新增独立模块** | 需要的是 `session` 内部的 Update Orchestrator，以及 Runtime 原子事务，不是第二个编排所有者。 |
| `auth` | **暂不新增** | Channel Auth 属于 connection，应用准入/Claims 属于 handshake，Active Message Permission Gate 由 session 调生成 Validator。职责扩大后再评估。 |
| `replay` | **暂不新增** | Input/Prediction 生产证据，Observability 存储和导出，Bot/Tooling 消费。只有出现独立 Playback 生命周期后才拆。 |
| `persistence` | **不是多余模块** | 设置、缓存、Save、损坏恢复具有共同存储适配和失败语义。当前问题是边界需细化，不是模块太薄。 |
| `observability` | **不是多余模块** | 有独立队列、Sink、背压、关闭和故障语义；不能并入各业务模块。 |

公共基线最初只列出 connection、handshake、replica、prediction、input、unity、hybridclr 和 bot；内部设计增加 session、persistence、observability，并明确说明后三者分别承接 Session 所有权和公共架构第 11、12 节职责。这种内部细分不改变跨仓职责，因此是允许的。

---

# 6. 跨模块关键流程审查

## 6.1 首次连接

### 当前裁决

Connection、Handshake、FullSnapshot、BaselineAck、Active 的基本顺序正确，但缺少 Gameplay Scope、生成 Mapping、Config 和 Runtime Handle 的 Ready 门。

### 建议冻结的完整顺序

```text
LumioGame / Host Composition
  -> 启动 observability
  -> 创建 storage/artifact ports
  -> 加载唯一 CoreEngine + stable GameRuntime
  -> 构造 ClientHostManifest 与静态 Capability Providers
  -> 创建 session

session.Connect
  -> connection Open
  -> channel/frame authentication
  -> handshake:
       Product/GameRelease
       Manifest/Schema/ABI/Protocol
       application auth/permission claims
       platform capability
  -> Negotiation Accepted

session:
  -> Activate exact Gameplay Scope
       precompiled OR HybridCLR
  -> bind generated Contract/Mapping
  -> materialize/stage required Config Snapshot
  -> create/finalize ReplicaWorld handles
  -> replica FullSnapshot atomic commit
  -> send BaselineAck
  -> create/enable Prediction Context
  -> Active
```

任何一步失败都必须由 `session` 决定状态迁移；外部 Composition 不能自行把 Session 推进到下一状态。

---

## 6.2 权威更新与预测校正

### 当前裁决

设计目标正确，但现有文档不足以证明原子性。

### 必须冻结的处理链

```text
connection:
  structural frame + channel replay checks
  -> immutable Envelope

session:
  Active message identity/permission/generation gate
  -> canonical payload decode/route

replica:
  Validate Baseline/Revision/Sequence/Mapping/Tombstone
  -> Staged Authority Plan

session -> Runtime atomic client update:
  Restore confirmed frame
  Apply ECS/GAS/Voxel authority batch
  Apply confirmation/rejection
  Remove confirmed commands
  Replay pending commands in original order
  Generate platform-neutral Presentation Diff
  Commit or expose nothing

after commit:
  replica advances Baseline/Revision
  prediction advances Confirmed Point/History
  session sends Ack
  session forwards Presentation Diff
```

不允许 Replica 先提交、Prediction 后补偿。

---

## 6.3 Gap / Resync / Reconnect

### 当前文档中正确的部分

- Gap、未知 Baseline、Revision/Tombstone 冲突会进入 Resync。
- Input 在 Resync/Reconnect 时使用有界缓冲。
- Connection 只报告断线事实，Session 决定重试。
- Connection Generation 隔离迟到事件。
- Resync 使用新 Baseline Generation。

这些设计是合理的。

### 仍需补齐

Resync 的以下状态必须作为同一切换门：

```text
New FullSnapshot / ResyncPatch
+ New Baseline Generation
+ Prediction History reset/rebase
+ Confirmed Point
+ buffered input disposition
```

建议：

```text
Resyncing
  -> stage new snapshot
  -> stage prediction reset/rebase
  -> atomic generation swap
  -> discard all old-generation messages
  -> only then Active
```

若重连建立了新 Channel Generation，应重新完成 Channel Auth 和 Handshake Attempt，或由公共契约明确支持安全的 Session Resume Token；不能默认复用旧连接的认证状态。

---

## 6.4 Unity + HybridCLR 启动

### 源码依赖裁决

- `unity-adapter` 不直接引用 HybridCLR：正确。
- `hybridclr-adapter` 实现 handshake 所声明的 Provider Port：正确。
- 二者由 Release Composition 选择性组装：正确。
- 没有源码循环。

### 运行时裁决

存在缺失的两阶段协议：

```text
Phase A: Pre-handshake
  HybridCLR provider reports platform/AOT capability only

Phase B: Post-handshake
  exact release artifact verify/load/activate
  -> gameplay scope ready
  -> synchronization allowed
```

Handshake 中声明“平台支持 HybridCLR”不等于“当前 Release Gameplay Scope 已成功激活”。这两个结果必须使用不同状态和值类型。

---

## 6.5 Headless Bot

### 裁决：通过，需机器约束

文档明确禁止 Bot 实现简化协议，并规定：

```text
Bot -> session
session -> production Connection/Handshake/Replica/Prediction
```

Bot 只替换 Input Driver、Host Clock 和 Presentation Adapter。这足以表达正确架构。

工程建立后必须自动检查：

- Bot 项目不能直接引用 connection/replica/prediction 实现项目。
- Bot 不得访问内部 Codec、Storage 或测试专用 Apply 入口。
- Bot 的 LocalEmbedded、Remote、Gap、Correction 和 Release Reject Fixture 使用与生产客户端相同的公共 API。
- 允许使用 fake Transport Adapter，但不能 fake Session/Handshake/Replica 语义来声称生产链通过。

---

## 6.6 LocalEmbedded

### 文档裁决：基本通过

根 README、仓库规范、Connection、Session 和 Bot 均明确：

- 同 Schema。
- 同 Serializer/Codec。
- 同 Envelope。
- 同权限。
- 同大小限制。
- 同有界队列。
- 同 Tick 交付。
- 可绕过 Socket/TLS/OS 网络栈，但不能绕过业务协议。
- 支持 Fault Decorator。

证据见 `README.md:70-72`、`.spec/knowledge/standards/repository-architecture.md:17-20,28-31` 和 `modules/connection/README.md:25,68-72`。

### 实现阶段额外门禁

LocalEmbedded 合同测试必须验证：

1. 仍执行真实 Encode/Decode，不直接传 typed message 对象。
2. 不共享 Server/Client World、Entity、Buffer 所有权或对象引用。
3. 不允许发送方同步重入接收方。
4. 仍通过 ingress/egress 有界队列。
5. 仍执行 Connection Generation、反重放和 Active 权限 Gate。
6. 只在规定 Tick/Phase 消费。
7. QueueFull、乱序、重复、断线、迟到回调与 Remote 行为同分类。
8. 同一测试向量在 Remote 和 LocalEmbedded 产生相同业务结果及可比较的证据。

---

# 7. 开始 C# 实现前必须解决的决策

按优先级排序：

## D1 — Runtime 原子 Client Update Contract

必须回答：

- 是否已有单调用原子 API。
- 谁 Stage，谁 Commit。
- Replica/Prediction 元数据何时推进。
- Ack 何时允许发送。
- FullSnapshot、Delta、Resync 是否使用同一事务机制。
- 任一步失败如何恢复或 Fault。

## D2 — Active Message Security Gate

冻结 Channel Auth、Handshake Auth、Claims、Message Permission、Connection Generation、Anti-replay 的唯一所有者和调用顺序。

## D3 — Post-Handshake Gameplay Scope Activation

冻结预编译与 HybridCLR 共用的激活端口及顺序：

```text
Handshake Accepted
-> Gameplay Scope Ready
-> Mapping/Config/Runtime handles Ready
-> Synchronizing
```

## D4 — 可变状态所有权表

至少覆盖：

- ClientReplicaSession。
- Connection Generation/Queues。
- Negotiation Result/Claims。
- ReplicaWorld Handle。
- Baseline/Revision/Mapping/Tombstone。
- InputSampleSeq。
- ClientCommandSeq/PredictionKey。
- Prediction History/Confirmed Point。
- Config Artifact/Staged Snapshot/Active Snapshot。
- Gameplay Scope。
- Presentation Diff。
- Command Stream/Replay Artifact。
- Observability EventSeq/Sink Queue。

每行必须回答创建、修改、快照、销毁和故障恢复所有者。

## D5 — Generated Contract / Mapping 工程层级

明确纯生成 Contract Artifact、LumioClient module ports 和 LumioGame implementation 之间的项目引用方向。

## D6 — 模块与 csproj/asmdef 的映射

冻结：

- 是否一模块一程序集。
- Host executable 项目位置。
- Test project 位置。
- Unity asmdef 关系。
- Public API 和 Internal API 策略。
- `InternalsVisibleTo` 规则。

## D7 — Session 事件与失败优先级

形成可执行状态转移表，覆盖取消、断线、拒绝、Resync、Fault、Close 和并发事件。

## D8 — Foundation 与 Vertical Slice 验收闭环

### Foundation 退出条件

至少应独立跑通：

```text
Headless Host
-> LocalEmbedded production protocol
-> Connection
-> Handshake
-> FullSnapshot
-> BaselineAck
-> Active
-> Gap/Resync
-> Close
```

并具备 QueueFull、Release Reject、权限拒绝和迟到 Generation Fixture。

### Vertical Slice 退出条件

至少应跑通：

```text
Game Input Mapping
-> ClientCommandSeq
-> Prediction
-> Server Confirmation/Correction
-> Atomic Rollback/Replay
-> Presentation Diff
-> Config Snapshot
-> Save/Load
-> Replay/Failure Bundle
```

---

# 8. 建议修改位置

本报告不直接修改文件。建议的最小文档修正位置如下。

| 问题 | 建议文件与章节 |
|---|---|
| 原子权威更新 | 新增 `.spec/decisions/0002-...md`；更新 `docs/specs/2026-08-27-client-module-architecture-design.md` 第 7、8.1、8.4、8.5、10.2、10.3、12 节 |
| Session Update Orchestrator | `modules/session/README.md` 的“责任”“数据与控制流”“失败与恢复”“验证” |
| Replica/Prediction 事务角色 | `modules/replica/README.md` 和 `modules/prediction/README.md` 的“责任”“公共入口与出口”“数据与控制流” |
| Active 消息权限 Gate | `modules/connection/README.md`、`modules/handshake/README.md`、`modules/session/README.md`、`modules/replica/README.md` |
| HybridCLR 启动门 | 设计文档第 10.1 节；`modules/session/README.md`、`modules/unity-adapter/README.md`、`modules/hybridclr-adapter/README.md` |
| ClientCommandSeq 所有权 | 设计文档第 8.5、8.6、10.2 节；`modules/input/README.md`、`modules/prediction/README.md` |
| ReplicaWorld 生命周期 | 设计文档第 8.1、8.4 节；`modules/session/README.md`、`modules/replica/README.md` |
| Config Snapshot handoff | 设计文档第 8.7、10.1 节；`modules/persistence/README.md`、`modules/session/README.md` |
| Generated Contract 层级 | 设计文档第 5.4、7、10.1 节；首次实现计划中的 project graph |
| session → persistence 窄化 | 设计文档第 7 节；`modules/session/README.md`“依赖”；`modules/persistence/README.md`“被依赖方” |
| Presentation/Replay 所有权 | 设计文档第 8.5、8.8、8.9、8.11、10.2 节 |
| 阶段验收 | 根 `README.md`“当前阶段与开发节奏”；设计文档第 12 节 |
| 工程语义 CI | `.github/workflows/repository-policy.yml`，在首次 csproj/asmdef 同一提交中加入 |

ADR 0001 已明确“改变本决策时新增 ADR，不改写本记录”，因此不建议直接重写 ADR 0001；应新增后续 ADR，并让设计文档反映新生效状态。

如果 D1、D2 或 D5 所需契约并不存在，修改位置应是唯一公共架构源 `LumioGameEngineArchitecture`；本仓 `docs/architecture` 镜像不得直接产生新公共语义。

---

# 9. Open Questions

以下问题依赖未提供的外部契约，不能仅凭 LumioClient 文档定论：

1. **LumioGameRuntime 是否已经发布单一的客户端权威更新事务 API？**  
   若只有独立 Snapshot Restore、Replica Apply 和 Prediction Replay API，当前“不直接依赖”设计不足以自动保证原子性。

2. **Generated Game Contract 和 Mapping 具体由哪个仓库、哪个工具链、哪个程序集发布？**  
   该 Artifact 是否完全不依赖 LumioClient 和 LumioGame implementation？

3. **Active Message 的权限 Validator 是否已经由架构源生成？**  
   是否同时校验 Session、Release、Role、Claims、MessageId、Connection Generation 和反重放状态？

4. **GameRuntime Config Port 是否已经定义 typed materialization、staging、ConfigRevision 和 Tick-boundary activation？**

5. **HybridCLR Gameplay Scope 是否允许在 Active Session 内替换？**  
   按当前公共基线推断应当固定 Release，普通热更不能掩盖 Runtime/ABI/Save Schema 变化；但具体 Scope 切换约束尚未提供。

6. **Replay/Command Stream 的规范格式和持久化责任由 Runtime、Architecture Tooling 还是 Client Observability 发布？**

7. **ClientReplicaSession 的 Runtime Handle 是否同时拥有 ReplicaWorld 与 VoxelReplicaWorld，还是两个独立 Handle？**  
   这会直接影响销毁、Snapshot、Resync 和原子事务边界。

8. **应用认证与权限 Claims 是 Handshake Payload 的一部分，还是独立 Auth Contract？**  
   当前模块可以容纳两种方案，但实现前必须选择唯一方式。

---

# 10. 最终裁决

## 是否足以进入实现计划阶段

**有条件足够。**

它足以支持制定一个以以下内容为第一阶段的实现计划：

```text
Gate 0:
  冻结原子 Update Contract
  冻结安全验证所有权
  冻结 Gameplay Scope 启动门
  完成状态所有权表
  冻结 Generated Contract 项目层级
  建立 csproj/asmdef DAG 与 CI 检查
```

它**不够**支持直接进入下列工作：

- 按现有文档自行设计 Replica/Prediction 调用 API。
- 创建可绕过 Runtime 原子事务的独立 Apply/Replay 服务。
- 在 Session 外部由 Unity Host 推进启动状态。
- 让 LocalEmbedded 直接传 typed 对象。
- 让 Input 和 Prediction 各自分配命令序号。
- 让 Persistence 或 Host 自行决定 Config Tick 激活。
- 让通用 LumioClient 直接引用 LumioGame Gameplay 实现。

## 最终评价

11 模块体系本身没有需要推翻的证据：

- `session` 是必要补充，不是多余编排层。
- `connection` 与 `handshake` 的拆分成立。
- `replica` 与 `prediction` 不直接依赖是可行方向。
- `unity-adapter` 与 `hybridclr-adapter` 分离正确。
- `bot` 的生产链复用要求明确。
- `persistence` 与 `observability` 有独立状态、生命周期和失败语义。
- 当前没有必要引入 `common/shared/utils/presentation/config/contracts`。

真正的问题不是模块数量，而是**若干跨模块提交点、启动门、安全 Gate 和构建产物边界尚未被写成唯一、可失败、可测试的契约**。

因此本次裁决为：

> **有条件通过模块架构；C# 实现门禁暂不放行。完成 5 个 P1 的最小文档封口后，可进入正式实现计划与工程骨架阶段。**

# LumioClient 模块化架构审阅与文档设计

## 1. 文档定位

本文审阅 `LumioClient` 设计框架，并定义进入实现前采用的模块边界与模块文档规范。它只定义本仓内部的组织方式，不新增公共协议、Schema、Envelope、ABI、错误码或跨仓依赖。

- 架构基线：`LGE-V1.2-2026-08-27`
- 公共架构唯一来源：`LumioGameEngineArchitecture`
- 本仓只读镜像：[`../architecture/LumioGameEngine_Architecture_v1.2.md`](../architecture/LumioGameEngine_Architecture_v1.2.md)
- 本仓边界入口：[`../../README.md`](../../README.md)
- 本文状态：已确认并完成模块文档落地；实现工程与业务代码尚未创建

## 2. 审阅结论

当前框架的仓库级边界是成立的：Client 与 Server 状态分离，Client 不拥有权威状态，LocalEmbedded 不绕过协议，Replica/Prediction 复用 Runtime 语义，Unity/HybridCLR 被限制在适配边界。这些原则足以作为实现的上层约束。

本仓内部结构补齐三项原框架未冻结的设计：

1. `session` 是 `ClientReplicaSession`、客户端状态机及跨模块调用顺序的唯一所有者。
2. 第 7 节冻结模块源码依赖方向，避免 `connection`、`handshake`、`replica`、`prediction` 与平台适配形成双向依赖。
3. 第 9 节冻结模块 README 契约，模块目录内的 README 负责表达当前所有权、输入输出、失败和验证面。

本仓在原有 10 个候选模块上增加 `session`，形成 11 个首批内部模块。首批不增加 `common`、`shared`、`utils`、`presentation` 或第二套 `contracts` 模块。

## 3. 当前框架中应保留的设计

### 3.1 仓库所有权

本仓拥有：

- Connection、Handshake、Endpoint、断线、重连及有界网络队列。
- `ClientReplicaSession` 生命周期。
- `ReplicaWorld`、`VoxelReplicaWorld` 的客户端投影和本地身份映射。
- 输入采样、命令序列、预测历史、确认、校正及表现输出。
- Unity Host、HybridCLR Capability 和 Headless Bot 适配。
- 客户端配置、缓存、可移植 Save Adapter 和诊断证据。

本仓不拥有：

- Server 权威状态、Server Wall Clock、Release Pool 或 Server Host 生命周期。
- RPC/Replication/Mapping/Serializer/Manifest Schema 的唯一来源。
- Game 的具体 Component、Gameplay、Mapping、UI 或内容。
- Voxel 内部存储、第二套 NativeCore/VoxelEngine 或稳定 Runtime 的替代实现。

### 3.2 强制运行时边界

- Server 与 Client 永远使用独立的 World、Storage、Entity 和状态真相。
- LocalEmbedded 可以绕过 Socket、TLS 和 OS 网络栈，不能绕过 Schema、Serializer、Envelope、权限、大小限制、有界队列和 Tick 交付。
- 网络、IO、Native Job 和平台回调只能进入有界队列；Runtime 固定 Phase 才能消费并改变 Replica/Prediction 状态。
- 表现层只消费结果，不能成为状态真相。
- 未完成 Release、Manifest、Schema、ABI、Capability 校验、精确 Gameplay Scope 激活与 FullSnapshot 时，Session 不能进入 `Active`。

### 3.3 公共契约边界

模块 README 只能引用公共契约，不得复制或重新定义公共字段。任何需要改变公共状态、字段、错误、时序、ID、版本或跨仓依赖图的需求，都必须先回到 `LumioGameEngineArchitecture` 形成新 Baseline，再同步本仓镜像。

## 4. 审阅发现

| 级别 | 发现 | 影响 | 处理结论 |
| --- | --- | --- | --- |
| 阻断 | `ClientReplicaSession` 无模块所有者 | 状态迁移、重连、同步、Resync 和关闭逻辑会散落在多个模块 | 增加 `session`，唯一拥有状态机与跨模块编排 |
| 阻断 | 未定义模块依赖 DAG | 容易形成 `connection <-> handshake`、`replica <-> prediction`、核心模块依赖 Unity 等环 | 冻结本文第 7 节依赖方向；工程建立后由 CI 校验项目引用图 |
| 阻断 | 没有模块 README 契约 | README 无法稳定表达所有权、输入输出和失败语义 | 所有模块使用本文第 9 节模板 |
| 重要 | `input` 与 `unity-adapter` 的输入职责重叠 | 平台事件、归一化和命令生成可能混在同一程序集 | `unity-adapter` 只采集平台事件；`input` 负责平台无关归一化、排序和命令序列 |
| 重要 | `replica` 与 `prediction` 的校正顺序容易互相侵入 | 两边各自恢复或应用状态会破坏原子回滚单元 | 两者不直接编排对方；`session` 调用 Runtime 提供的统一恢复/应用/重放流程 |
| 重要 | `observability` 是横切能力 | 直接依赖具体日志 SDK 会污染稳定接口并产生供应商耦合 | 只暴露稳定事件出口和上下文字段；具体 Sink 位于适配边界 |
| 重要 | `persistence` 的名称可能被误解为 Server 权威持久化 | 客户端模块可能承担 WAL、权威 Snapshot 或迁移编排 | 明确只负责客户端设置、缓存、可移植 Save Adapter 与损坏恢复 |
| 重要 | `unity-adapter` 与 `hybridclr-adapter` 的装载职责重叠 | Unity Host 可能自行实现热更校验，形成两条加载路径 | `hybridclr-adapter` 唯一拥有热更包校验和加载；`unity-adapter` 只提供 Host 集成 |
| 重要 | Headless Bot 容易成为简化协议实现 | 测试结果不能代表真实客户端链路 | `bot` 必须复用同一 `session`、连接、握手、复制和预测 API，只替换输入与表现端口 |
| 改进 | 测试面没有映射到模块 | Fixture 可能无人维护，失败语义无法归属 | 每个模块 README 必须列出正向、失败、边界和故障测试 |

根 README 新增的 `persistence` 与 `observability` 虽未出现在公共架构第 16 节 Client 首批模块表中，但它们分别承接公共架构第 11、12 节已分配给 Client 的职责，属于本仓内部细分，不构成基线冲突。

## 5. 模块化原则

### 5.1 一个模块只有一个主要变化原因

模块应拥有一组共同演进的状态、生命周期和失败语义。出现下列任一情况时应拆分：

- 两部分由不同 Host 或平台独立替换。
- 两部分具有不同的生命周期或线程模型。
- 两部分可以分别发布、测试或失败恢复。
- 修改一部分时，大多数情况下不应重新验证另一部分。

仅仅文件数量增多不是拆分理由；仅仅被多处使用也不是建立 `common` 的理由。

### 5.2 依赖只能单向

- Host/Adapter 依赖稳定 Client 模块，稳定 Client 模块不得依赖 Unity、HybridCLR、Renderer、平台 UI 或 Bot 类型。
- `session` 可以编排叶子能力；叶子能力不得回调或引用 `session` 的具体实现。
- `replica` 与 `prediction` 不直接相互拥有状态；需要原子协调时通过 Runtime 契约并由 `session` 编排。
- 第三方库必须位于 Adapter 后，第三方类型不得穿过模块公共接口。
- 不允许循环工程引用，也不允许通过反射、Service Locator 或静态全局状态隐藏依赖。

### 5.3 状态所有权唯一

每类可变状态只能有一个模块负责创建、修改、快照和销毁。其他模块通过不可变值、命令或显式端口交互，不共享内部对象引用。

### 5.4 契约与实现分离但不滥建项目

模块的公共端口与默认实现可以先位于同一模块中，通过命名空间和可见性隔离。只有出现独立发布、独立替换或依赖倒置的真实需要时，才拆成单独的 `abstractions` 工程。首批不建立全局 `common`、`shared` 或 `utils` 模块。

## 6. 目录模型

仓库采用能力优先的目录，而不是按 `interfaces`、`services`、`models` 等技术层横切：

```text
modules/
  session/
    README.md
  connection/
    README.md
  handshake/
    README.md
  replica/
    README.md
  prediction/
    README.md
  input/
    README.md
  persistence/
    README.md
  observability/
    README.md
  unity-adapter/
    README.md
  hybridclr-adapter/
    README.md
  bot/
    README.md
```

约束：

- `modules/<name>/` 是模块边界；未来该模块的源文件、工程文件和模块级测试资产应与 README 同地或由 README 给出唯一链接。
- 每个模块目录在引入任何源码前必须先存在非空 `README.md`。
- 子目录只有在具有独立公共边界时才称为子模块；一旦称为子模块，也必须有自己的 `README.md`。
- 根 README 只保留仓库边界、模块索引和全局关系，不复制各模块的详细设计。
- 模块 README 描述当前有效设计，不记录变更历史；历史由 Git 和 ADR 承担。

## 7. 依赖图

`A -> B` 表示 A 可以源码依赖 B 的公开接口：

```text
unity-adapter      -> session + input + observability
bot                -> session + input + observability

session            -> connection + handshake + replica + prediction + input
                    + persistence + observability
handshake          -> connection + observability
prediction         -> observability
connection         -> observability
replica            -> observability
input              -> observability
persistence        -> observability
hybridclr-adapter  -> handshake + observability

all applicable modules -> published GameRuntime API
all applicable modules -> 纯生成 Contract Artifact（层级见下方补充规则）
```

补充规则：

- `session` 不复制 Runtime 的预测、回滚、ECS 或 Replica 机制，只组织调用顺序与 Session 状态迁移。
- `handshake` 使用 `connection` 提供的消息通道，但 `connection` 不理解 Release、Schema、ABI 或 Capability 的业务含义。
- `replica` 和 `prediction` 之间不建立工程引用；它们的共同原子边界由已发布 Runtime API 表达。
- `input` 产生生成契约定义的候选命令值（只带 `InputSampleSeq`），`session` 将其交给 `prediction`，由 `prediction` 在接纳时分配 `ClientCommandSeq`；`prediction` 不依赖输入采集或归一化实现。
- `observability` 只能提供事件、上下文和 Sink 端口，不能反向依赖业务模块。
- `unity-adapter` 与 `bot` 是顶层宿主适配，不能被核心模块引用。
- `handshake` 声明平台 Capability Provider 端口，`hybridclr-adapter` 选择性实现该端口；`unity-adapter` 不直接依赖 HybridCLR。没有该能力时，Unity Host 仍可使用稳定 Runtime 与预编译 Gameplay Assembly 启动。
- 契约依赖分三层（ADR 0002）：已发布 Host/Runtime Port 定义于本仓模块或已发布 Runtime Contract；工具链发布的纯生成 Contract Artifact 不依赖 LumioClient 与 LumioGame 的任何实现，双方均可引用；Game 专属 Mapper/Binding 实现位于 LumioGame Release Artifact，实现本仓模块声明的端口并由 Release Composition 注入。禁止 LumioClient 核心工程直接或传递引用 `LumioGame.ClientGameplay` 实现工程，也不得用反射或 Service Locator 隐藏该引用。
- `session -> persistence` 窄化为已验证 Artifact/Checkpoint 读取端口，供 Config staging 与本地 Checkpoint 使用；应用设置与 Content 下载缓存由 Host/Composition 在 Session 之外处理。
- `session` 声明平台无关的 Gameplay Scope 激活端口：预编译路径的默认实现直接返回已激活；HybridCLR 路径由 `LumioGame` Release Composition 用 `hybridclr-adapter` 的公开能力实现并注入，不新增模块间源码依赖边。

## 8. 模块目录

首批实现优先级保持与根 README 一致：`session`、`connection`、`handshake`、`replica`、`prediction` 为 P0；其余模块为 P1。`session` 是本次审阅补出的 P0 编排模块，不改变公共架构基线。

### 8.1 `session`

**目的：** 唯一拥有 `ClientReplicaSession` 生命周期和跨模块编排。

**拥有：**

- 公共架构第 3.2 节定义的连接、协商、同步、Active、Resync、Reconnect、Close 和 Fault 状态迁移。
- Session 级取消、超时、资源释放，以及供 Host 调用的 Tick 驱动入口。
- Handshake 完成、Gameplay Scope 激活、FullSnapshot 应用、进入 Active、Gap/Resync、断线重连的编排顺序。
- Gameplay Scope 激活端口的声明与调用时机；预编译路径的默认实现直接返回已激活。
- Active 消息门：调用生成的 Protocol/Permission Validator 校验 SessionId、GameReleaseId、MessageId、Role、Claims 和 Connection Generation。
- 权威更新事务的编排：提交 Staged Plan、发起单一 Runtime 事务、成功后的元数据推进与 Ack 发送顺序。
- Session 级 Runtime Handle（ReplicaWorld/VoxelReplicaWorld）的创建与逆序销毁；Config staging/activation 的请求时机。
- `SessionId + ProductId + GameReleaseId` 关联和 Session 级 Capability 快照。
- 输入缓冲、Replica 更新、Prediction 校正和 Presentation 输出之间的调用顺序。

**不拥有：** Wall Clock、Socket、协议字段、Replica Storage、Prediction History 内部结构、Validator 规则内容、Runtime 事务机制、Unity 生命周期或 Server Session。

### 8.2 `connection`

**目的：** 提供可替换的客户端传输通道及连接级故障语义。

**拥有：**

- Endpoint 解析、连接尝试、断开检测、超时、退避和 Transport ACK。
- 网络线程到 Runtime 消费边界之间的有界 ingress/egress 队列。
- Remote、LocalSplitProcess、LocalEmbedded Transport Adapter 和 Fault Decorator 接口。
- 大小限制、分片、重传、反重放、认证结果的传输层承载。

**不拥有：** Handshake 判定、Baseline ACK、Session 状态机、消息 Schema 或游戏命令含义。

### 8.3 `handshake`

**目的：** 在 Session 激活前完成发布与能力协商。

**拥有：**

- Release、Manifest、Schema、Runtime API、Core ABI、Network/Replication Protocol 和 Capability 校验流程。
- 握手拒绝分类、稳定诊断信息及校验结果。
- FullSnapshot 同步前的准入结果。

**不拥有：** Manifest/Envelope Schema 定义、Release Catalog、Transport 生命周期或热更包实际加载。

### 8.4 `replica`

**目的：** 将经过校验的权威 Snapshot/Delta 投影到客户端 Replica World。

**拥有：**

- FullSnapshot、Delta、Mapping Apply、Baseline ACK、Revision、Sequence、Gap 和 Resync 信号。
- `NetEntityId` 到 `LocalEntityId` 的客户端映射、Tombstone 和 provisional ID 重映射。
- 未知 Baseline、旧 Revision、Mapping Hash 不符和迟到 Delta 的拒绝语义。
- 权威更新的校验与 Staged Authority Plan 构造；共同事务提交成功后的 Baseline/Revision 推进与 Ack 生成。

**不拥有：** Server 权威状态、Game Component Schema、Runtime ECS Storage 实现、Prediction History、事务提交本身（由 Runtime 事务完成）或表现对象。

### 8.5 `prediction`

**目的：** 驱动 Runtime 的预测、确认、校正、回滚与未确认命令重放。

**拥有：**

- 在命令被 `session` 接纳进入预测/发送流程时唯一分配的 `ClientCommandSeq` 与 `PredictionKey`，以及命令历史和 PredictionFrame 生命周期。
- Confirmation/Correction 的接收与校正请求；恢复与重放只在 Runtime 权威更新事务内执行，Confirmed Point 在事务提交成功后推进。
- 历史窗口、过期策略和重放顺序；Presentation Diff 由 Runtime 事务生成，本模块只转发。
- ECS、GAS、Voxel Overlay 作为同一确认/回滚单元的客户端协调入口。

**不拥有：** Runtime 的状态机制、Replica Delta 解码、平台输入事件或最终权威结果。

### 8.6 `input`

**目的：** 将平台输入采样转换为平台无关、可排序、可回放的客户端命令流。

**拥有：**

- 输入采样时间语义、归一化、排序、去抖、`InputSampleSeq` 采样序列和有界缓冲。
- 输出未编号的候选命令；`ClientCommandSeq` 由 `prediction` 在接纳时分配，被拒绝或丢弃的样本不消耗序号。
- Resync/Reconnect 期间的默认缓冲上限与丢弃诊断。
- Host 输入端口和可回放 Command Stream 出口。

**不拥有：** Unity Input 类型、具体游戏操作映射、Gameplay Command Schema、最终命令序号 `ClientCommandSeq` 或预测状态。

### 8.7 `persistence`

**目的：** 提供客户端本地设置、缓存和可移植 Save 的存储适配。

**拥有：**

- 版本化客户端设置、缓存索引、原子替换、Hash/Checksum 和损坏回退。
- 可移植 Save Adapter、最近有效 Checkpoint 和本地容量策略。
- 向 `session` 提供已验证 Config/Content Artifact 与 Checkpoint 的窄读取端口；staging/activation 时机归 `session`，Tick Barrier 切换归 Runtime。
- 文件系统、平台 Key/Value Store 或宿主提供的 Storage Port 适配。

**不拥有：** Server WAL、权威 Snapshot、跨 World Txn Journal、Game Migrator 业务规则或 Secret 明文存储。

### 8.8 `observability`

**目的：** 统一客户端 Diagnostic、Metric、Trace、Replay 和 Failure Bundle 出口。

**拥有：**

- Client 事件上下文、分类、采样、脱敏、背压和应急落盘端口。
- 有界异步 Sink、控制台/文件/外部 Sink Adapter 接口。
- Replay 证据、Client State Hash 和首差异诊断的汇集。

**不拥有：** 业务状态、Txn Journal 真相、供应商 SDK 公共类型或跨线程全局实时顺序承诺。

### 8.9 `unity-adapter`

**目的：** 将 Unity 生命周期、渲染和平台输入接到稳定 Client API。

**拥有：**

- Unity Host 启停、帧循环到 Client Tick 入口的适配。
- Unity Input 采集和 Presentation 输出到 Renderer/GameObject 的桥接。
- Desktop、iOS、Android 的 Unity 平台能力声明和设备级诊断。

**不拥有：** Replica/Prediction 真相、具体游戏 UI/内容、Handshake 规则或 HybridCLR 包校验逻辑。

### 8.10 `hybridclr-adapter`

**目的：** 将 HybridCLR 作为可选 Unity Platform Capability 安全接入。

**拥有：**

- Client Gameplay Assembly 的签名、Hash、GameRelease、Schema、权限和资源预算校验。
- 加载、激活、拒绝、回滚和卸载边界。
- AOT 元数据、平台限制和 Capability 结果的适配。

**不拥有：** 稳定 Runtime/ABI 的热替换、Server HybridCLR、Release Catalog 或游戏代码内容。

### 8.11 `bot`

**目的：** 提供复用生产客户端链路的 Headless Host、输入和表现适配。

**拥有：**

- Bot Driver、确定性/脚本化 Input Adapter 和无渲染 Presentation Adapter。
- Bot 生命周期、并发资源预算、Scenario 驱动和结果采集。
- 连接、复制、预测、重连和 Replay 测试入口。

**不拥有：** 简化版协议、第二套 Session/Replica/Prediction 实现、Server Bot 权威逻辑或 Unity 依赖。

## 9. 模块 README 契约

每个 `modules/<name>/README.md` 必须使用以下结构。内容应描述当前有效边界；没有实现的能力应明确写为“未实现”，不能用 `TODO`、`TBD` 或模糊占位语句。

```markdown
# <module-name>

> 一句话说明该模块解决什么问题。

## 状态

- 阶段：未实现 | 实施中 | 已交付
- 架构基线：`LGE-V1.2-2026-08-27`
- 公共契约来源：链接到架构镜像对应章节，不复制 Schema

## 责任

- 本模块唯一拥有的状态、生命周期和行为。

## 明确不负责什么

- 容易混入但必须由其他模块或其他仓库拥有的内容。

## 公共入口与出口

- 调用方如何进入本模块。
- 本模块产生什么值、事件、命令或错误。
- 第三方类型不得出现在稳定入口与出口中。

## 数据与控制流

1. 输入如何进入。
2. 在哪个线程、队列或 Tick Phase 消费。
3. 状态由谁修改。
4. 输出如何交给下游。

## 依赖

- 允许的本仓模块依赖。
- 外部已发布 API、生成契约或平台 SDK。
- 明确禁止的反向依赖。

## 生命周期与线程模型

- 创建、启动、暂停、恢复、关闭和故障状态。
- 所属线程、队列容量原则和取消边界。

## 失败与恢复

- 可重试、可拒绝、可致命错误。
- 超时、取消、资源释放、Resync 或回退行为。

## 可观测性

- 必须产生的 Diagnostic、Metric、Trace、Replay 或 Failure Bundle 证据。
- 必须携带的关联标识。

## 验证

- 单元测试边界。
- 正向与失败 Fixture。
- 集成、Fault、Stress、设备或平台验证。

## 目录

- 列出模块内一级目录及其单一职责。
```

### 9.1 README 质量门槛

一个模块 README 只有同时回答以下问题才算合格：

1. 这个模块为什么存在，删除后缺失什么能力？
2. 它唯一拥有哪类状态，谁创建、修改、快照和销毁该状态？
3. 它接收什么、输出什么，调用发生在哪个线程或 Tick 边界？
4. 它允许依赖谁，谁可以依赖它，哪些依赖明确禁止？
5. 失败如何分类、恢复、记录和重放？
6. 如何在不启动完整游戏或 Unity 的情况下验证核心行为？
7. 哪些公共语义来自架构源，README 是否只链接而未复制？

README 不应包含：

- 实现历史、提交日志、临时任务列表或个人工作笔记。
- 复制粘贴的公共 Schema、Envelope 字段或跨仓规则。
- 与源码不一致的计划性 API 名称。
- “以后处理”“适当校验”“类似其他模块”等不可验收表述。
- 教程式代码细节；详细 API 文档应由源码注释和生成文档承担。

## 10. 模块间关键流程

### 10.1 首次连接

```text
Unity Adapter / Bot
  -> session.Connect
  -> connection 建立传输并启动有界队列
  -> handshake 校验 Release/Manifest/Schema/ABI/Capability 并产出准入 Claims
  -> session 通过注入的激活端口激活精确 Gameplay Scope
     （预编译路径直接返回已激活；HybridCLR 路径校验、加载并激活精确 Release Artifact）
  -> session 绑定生成 Contract/Mapping，经 persistence 窄端口取已验证 Artifact 并请求 Config staging
  -> session 请求 Runtime 创建 Session 级 Replica Handle
  -> replica 校验 FullSnapshot 并构造 Staged Plan，经 Runtime 事务原子提交
  -> session 发送 BaselineAck 并进入 Active
  -> input/prediction 开始提交可预测命令
```

任何一步失败都由拥有该判断的模块产生分类结果，由 `session` 决定 Session 状态迁移；模块不得直接修改其他模块状态。激活失败不得进入 `Synchronizing`；外部 Composition 不得在 `session` 之外推进启动状态。一个 Session 的 Gameplay Scope 固定，Session 内不得跨 Release 替换。

### 10.2 权威更新与预测校正

```text
connection 完成帧/通道校验并产出不可变 Envelope
  -> session 执行 Active 消息门
     （SessionId/GameReleaseId/MessageId/Role/Claims/Connection Generation）
  -> replica 校验 Baseline/Revision/Sequence/Mapping/Tombstone 并构造 Staged Authority Plan
  -> session 发起单一 Runtime 权威更新事务：
       恢复最近 Confirmed PredictionFrame
       原子应用 ECS/GAS/Voxel 权威结果
       应用确认/拒绝并删除已确认命令
       原序重放未确认命令
       生成平台无关 Presentation Diff
       全部成功才提交；任一步失败不产生可见状态
  -> 提交成功后：replica 推进 Baseline/Revision，prediction 推进 Confirmed Point/History
  -> session 发送 Ack 并转发 Presentation Diff
  -> unity-adapter 或 bot 消费表现结果
```

`replica` 不直接操作表现对象，`prediction` 不自行实现 Runtime 回滚，Host Adapter 不读取内部 Replica Storage。不允许 `replica` 先提交、`prediction` 后补偿；FullSnapshot、Delta 与 Resync 使用同一事务边界。Presentation Diff 由 Runtime 事务生成，`session` 只排序、关联 Session 生命周期并转发，`replica`、`prediction` 和 Host Adapter 不生产第二套表现真相。事务结果为 `Aborted` 时零可见副作用，由 `session` 按原因分类决定重试或 Resync；`Indeterminate` 时按 Runtime 见证的 `FaultClass`（公共契约 ADR-021）处置，`SlotStateUnproven` 使 Session 进入 `Faulted`，经 Full Resync 或重启会话恢复。

### 10.3 Gap、Resync 与重连

```text
replica 检测 Gap/未知 Baseline/Revision 冲突
  -> session 进入 Resyncing（同一连接与准入内，不重新 handshake）
  -> input 按 Host Profile 有界缓冲
  -> replica 校验新 FullSnapshot 或合法 ResyncPatch 并构造 Staged Plan（新 Baseline Generation）
  -> prediction 暂存历史重基或清空计划并给出诊断
  -> 经同一 Runtime 事务原子生效：切换 Baseline Generation，丢弃旧代次消息
  -> session 返回 Active
```

传输断开时由 `connection` 报告事实，是否重试以及 Session 如何迁移到 `Reconnecting` 由 `session` 决定。重连建立新连接代次后，必须重新完成通道认证与 Handshake Attempt；公共契约显式支持 Session Resume Token 前，不得复用旧连接的认证状态。

## 11. 文档与实现同步规则

- 新增模块：先补模块 README，再添加工程和源码；根 README 同步增加模块链接。
- 修改模块所有权、公共入口、依赖方向、状态机或失败语义：同一改动必须更新模块 README。
- 改变本仓内部的重要边界：在 `.spec/decisions/` 新增 ADR；功能文档只保留生效后的设计现状。
- 改变公共契约：先在架构源完成 ADR、Schema、正向/失败 Fixture 和新 Baseline，本仓只同步只读镜像及引用。
- 删除模块：先迁移所有权和依赖，移除根索引与悬空链接，再删除目录。
- Repository Policy 检查每个已登记模块存在非空 README、标题和必要章节，并检查根 README 链接；工程建立后继续增加项目引用图无环校验。

## 12. 落地状态与实现门槛

当前已经落地：

- ADR 0001 记录 11 个能力模块、`session` 所有权和依赖原则。
- ADR 0002 冻结权威更新事务边界、Gameplay Scope 激活门、消息校验所有权、可变状态所有权和生成契约工程层级；校验矩阵与状态表见第 13 节。
- 上游以 `LGE-V1.2-2026-08-27` 关闭全部 8 项上游契约确认（ADR-021/022/023 与既有章节），ADR 0002 无需被取代；裁决结果见第 14 节。
- `modules/<name>/README.md` 已覆盖全部 11 个首批模块。
- 根 README 已作为模块索引，Repository Policy 已建立模块文档检查。
- 每个模块 README 已写明责任、非责任、输入输出、依赖、线程、失败、可观测性和验证面。

开始建立实现工程或写源码前，仍必须固定 .NET SDK/语言版本、formatter、analyzer、Unity/HybridCLR 兼容矩阵和可复现验证命令，并把第 7 节依赖 DAG 映射为可由 CI 校验的项目引用图。第 14 节的上游契约确认已全部关闭，不再阻塞实现。首次实现应另写实施计划，不在本文中混入任务状态或源码占位。

## 13. 校验与可变状态所有权

本节是 ADR 0002 第 3、4 条的展开，是模块 README 相关表述的唯一汇总处。公共契约中的所有者术语是 `ClientReplicaSession`、Connection 层与 GameRuntime；本仓模块名不进入公共契约，本节矩阵是公共所有者到本仓模块的内部角色映射。

### 13.1 消息校验所有权矩阵

| 验证类型 | 唯一所有者 |
| --- | --- |
| Endpoint 格式、Socket/IPC 建立、TLS/IPC 对端身份、Channel Binding | `connection` |
| Frame 长度、完整性、分片、连接序号、连接级反重放窗口 | `connection` |
| 登录/Session 准入、权限 Claims、Release/Schema/ABI/Protocol/Capability 协商 | `handshake` |
| Active Envelope 的 SessionId、GameReleaseId、MessageId、Role、Claims、Connection Generation 与会话级反重放 | `session`（调用生成的 Protocol/Permission Validator） |
| Snapshot/Delta 的 Baseline、Revision、Sequence、Mapping、Tombstone | `replica` |
| Confirmation/Correction 的预测语义 | `prediction` |

LocalEmbedded 与 Remote 走完全相同的矩阵。重连（新连接代次）必须重做通道认证与 Handshake（V1 不提供 Session Resume Token）；Resync 在同一连接与准入内进行，不重新握手。

生成 Validator 的字段集由上游 ADR-022 冻结；V1 中 `MessageId` 使用 `MessageType` 命名空间，不实现 D-009 RPC 分发。消息门拒绝使用公共 ErrorCode `MessagePermissionDenied`，连接代次过期使用 `StaleConnectionGeneration`。

### 13.2 可变状态所有权表

每类可变状态回答创建、修改、快照/证据、销毁与失败恢复所有者：

| 状态 | 创建 | 修改 | 快照/证据 | 销毁 | 失败恢复 |
| --- | --- | --- | --- | --- | --- |
| ClientReplicaSession 状态机 | `session` | `session`（Owner Thread） | 状态迁移事件 | `session` | 唯一终态、幂等迁移 |
| Connection Generation 与 ingress/egress 队列 | `connection` | `connection` | 统计快照 | `connection`（执行 `session` 命令） | 分类上报，`session` 决定迁移 |
| Negotiation Result 与准入 Claims | `handshake` | 不可变 | Failure Bundle | Session Close 或重握手时失效 | 重新 Handshake |
| ReplicaWorld/VoxelReplicaWorld Runtime Handle | `session` 请求 Runtime 创建 | Storage 归 Runtime | Runtime Snapshot 机制 | `session` 逆序销毁（先 Voxel 后 ECS） | 销毁失败进入 Faulted |
| Baseline/Revision/Mapping/Entity Map/Tombstone | `replica` | `replica`（仅事务提交后推进） | Apply 证据、State Hash | 随 Replica Context | Resync 换新 Baseline Generation |
| InputSampleSeq 与 Sample Queue | `input` | `input` | Command Stream 记录 | `input` | 采样源 Generation 隔离 |
| ClientCommandSeq/PredictionKey | `prediction`（接纳时分配） | `prediction` | Replay 证据 | 随 Prediction Context | 拒绝/丢弃不消耗序号 |
| Prediction History/Confirmed Point | `prediction` | `prediction`（仅事务提交后推进） | Replay、回滚统计 | 随 Session | Resync 重基或清空 |
| Config/Content Artifact 与缓存 | `persistence` | `persistence` | Hash/Checksum、损坏证据 | 容量策略淘汰 | 回退最近有效 Checkpoint |
| Staged/Active Config Snapshot | Runtime Config Port | 不可变，Tick Barrier 原子切换 | ConfigRevision | Runtime | 激活失败保留旧 Snapshot；staging 请求时机归 `session` |
| Gameplay Scope | `hybridclr-adapter` 或预编译默认实现 | 不可变 Handle，按 Generation 切换 | 校验/加载证据 | Adapter 卸载 | 激活失败回滚旧 Scope 或要求重启；Session 内固定 |
| Presentation Diff | Runtime 事务生成 | 不可变 | Replay 关联 | 消费后丢弃 | 不回写任何状态 |
| Command Stream/Replay Artifact | `input`/`prediction` 生产 | 追加式 | `observability` 存储与导出 | 轮转/保留策略 | 不作为状态真相 |
| Observability EventSeq 与 Sink Queue | `observability` | `observability` | 自观测计数器 | `observability` | 按类别背压策略 |

## 14. 上游契约确认（已全部关闭）

原「待上游契约确认」8 项已由上游 `LumioGameEngineArchitecture` 以基线 `LGE-V1.2-2026-08-27` 全部裁决（同步说明见上游 `docs/architecture/LGE-V1.2-lumio-client-contract-ruling.md`）。裁决与 ADR 0002 冻结的内部角色约束一致，0002 无需被取代。本次上游新增公共契约：Schema `client-authority-update`、`protocol-permission-gate`、`generated-contract-artifact`；ErrorCode `MessagePermissionDenied` (1031)、`StaleConnectionGeneration` (1032)。

| # | 请求 | 裁决 | 公共落点（§ 指本仓只读镜像 `LumioGameEngine_Architecture_v1.2.md` 的章节） |
| --- | --- | --- | --- |
| 1 | 单一客户端权威更新事务 API | 新增 | ADR-021、§7.2：固定步骤序，`Committed`/`Aborted`/`Indeterminate` 提交语义与 Runtime 见证的 `FaultClass`；独立 Restore/Apply/Replay API 不能替代 |
| 2 | 生成 Protocol/Permission Validator | 新增 | ADR-022、§7.3：字段集冻结为 SessionId/Release/MessageId/Role/Claims/Connection Generation；会话级反重放归 `ClientReplicaSession` 所有者；V1 `MessageId` 使用 `MessageType` 命名空间，不冻结 D-009 RPC |
| 3 | 生成 Contract Artifact 发布方 | 新增 | ADR-023、§11.2：上游工具链唯一发布纯生成物，零依赖 LumioClient/LumioGame 实现工程，四仓可引用同一包 |
| 4 | GameRuntime Config Port | 已存在 | ADR-010、§11.3：typed materialization、Staged/Active 不可变 `ConfigSnapshot`、`ConfigRevision`、Tick Barrier 原子激活；staging 请求时机属宿主编排，不进公共契约 |
| 5 | Runtime Handle 形态 | 已存在 | ADR-001、§3.1/§3.3：`ReplicaWorld` 与 `VoxelReplicaWorld` 两个独立 Handle，逆序销毁先 Voxel 后 ECS，权威更新事务跨越二者 |
| 6 | Session Resume Token | 拒绝 | §7.3、D-012：V1 不提供；新连接代次一律重做通道认证与完整 Handshake |
| 7 | Active Session 内跨 Release 替换 Scope | 拒绝 | §13.1、D-007、ADR-014：Session 精确绑定 `GameReleaseId`，禁止跨 Release 替换；同 Release 热更失败回滚不构成 Release 切换 |
| 8 | Replay/Command Stream 归属 | 已存在 | ADR-010/011、§11.2/§12.2：规范格式归上游 Canonical Serializer，权威 Command Log 归 Host 持久化；Client Observability 只导出同格式证据，不作为状态真相 |

C# 开工门禁中的上游依赖（原 D1/D2/D5）已解除；剩余实现前置见第 12 节。

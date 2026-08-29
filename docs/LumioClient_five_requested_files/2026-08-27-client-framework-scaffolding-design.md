# LumioClient 框架实现级设架设计

- 日期：2026-08-27
- 架构基线：`LGE-V1.2-2026-08-27`
- 文档性质：实现级设计；不包含生产源码
- 适用仓库：`LumioClient`
- 冻结模块：`session`、`connection`、`handshake`、`replica`、`prediction`、`input`、`persistence`、`observability`、`unity-adapter`、`hybridclr-adapter`、`bot`

> 本文把已冻结的客户端框架拆到可派工精度。签名中的 `GeneratedContract.*` 与 `RuntimeContract.*` 是对“已发布生成 Contract Artifact / LumioGameRuntime Port”的设计别名，不定义字段、不建立第二套公共协议。执行 Wave 0 时必须把别名机械映射到上游真实类型名；映射缺失时相应任务保持阻塞。

## 0. 冻结前提与非目标

1. `session` 是唯一跨能力编排者，11 个模块集合与依赖方向不变。
2. `replica` 和 `prediction` 只负责 Stage/Discard/Observe；两者都没有独立 Runtime Commit。
3. 权威更新只允许由 `session` 调用一次 Runtime Authority Transaction。
4. `input` 只分配 `InputSampleSeq`；`prediction` 只在本地 Runtime 事务提交后分配 `ClientCommandSeq` 与 `PredictionKey`。
5. LocalEmbedded 与 Remote 使用相同生成 Codec、Envelope、权限、Tick、队列与 Fixture；禁止 typed-object shortcut。
6. 核心程序集不引用 Unity、HybridCLR、Server 实现或 `LumioGame.ClientGameplay` 实现工程。
7. 本文不创建 `.cs`、`.csproj`、`.asmdef`；所有路径均是实现者将创建的目标。
## 1. 工程工具链冻结

| 项目 | 冻结值 | 理由与验证 |
| --- | --- | --- |
| SDK | .NET SDK `10.0.400`；`global.json` 设置 `rollForward: disable`，`allowPrerelease: false` | Host/测试采用当前 LTS 工具链；固定编译器与 restore 行为 |
| 核心 TFM | `netstandard2.1` | Unity 与纯 .NET Host 的共同最低稳定面；上游 Runtime 发布包若不支持则由 Wave 0 阻塞项处理 |
| Host/测试 TFM | `net10.0` | Headless、测试工具与 Bot Host 使用完整 BCL |
| C# 语言版本 | 核心 `9.0`；Host/测试 `14.0` | 核心受 Unity 编译器/AOT 约束；Host 允许现代测试辅助代码 |
| Nullable | `enable` | 所有 production/test project 一致 |
| 警告 | `TreatWarningsAsErrors=true`；`AnalysisLevel=latest-recommended` | 不允许警告债务进入起步骨架 |
| 格式化 | `dotnet format --verify-no-changes --no-restore` + 根 `.editorconfig` | 可复现、CI 可执行 |
| Analyzer | .NET analyzers + `Microsoft.CodeAnalysis.BannedApiAnalyzers` 5.6.0 | 禁止核心引用 Unity/Socket/Serilog/HybridCLR 等类型 |
| 包管理 | Central Package Management + lock file + locked restore | 供应链与版本可重现 |
| 预览能力 | 全部关闭 | 避免 SDK/Unity 组合漂移 |

### 1.1 根级工程文件

| 路径 | 单一职责 |
|---|---|
| `global.json` | 锁定 SDK 10.0.400 与 roll-forward 策略 |
| `Directory.Build.props` | nullable、warning、analyzer、deterministic build、IVT 约束 |
| `Directory.Packages.props` | NuGet 中央版本 |
| `NuGet.Config` | 源白名单、签名/缓存策略 |
| `.editorconfig` | 格式与命名规则 |
| `eng/dependency-baseline.md` | 许可证、版本、AOT 与退出路径证据 |
| `eng/project-reference-allowlist.json` | 第 4 节 DAG 的机器可读来源 |
| `eng/verify-toolchain.sh` / `.ps1` | SDK、locked restore、format/build/test 的统一入口 |

## 2. 程序集与测试映射

- 每个能力模块一个 production `.csproj`，路径固定为 `modules/<name>/src/Lumio.Client.<PascalName>.csproj`。
- `bot` 另外有一个可执行 Composition Root：`modules/bot/host/Lumio.Client.Bot.Host.csproj`；它不是第十二个能力模块。
- 每个模块一个测试项目：`modules/<name>/tests/Lumio.Client.<PascalName>.Tests.csproj`。
- 跨模块 Fixture 位于 `tests/Lumio.Client.IntegrationTests`；依赖图、第三方泄漏、asmdef 图位于 `tests/Lumio.Client.ArchitectureTests`。
- `InternalsVisibleTo` 只允许模块自身的测试程序集；IntegrationTests 只能走 public ports。禁止 friend 到其他 production module 或产品工程。
- Unity asmdef 与 production assembly 一一对应；asmdef 引用边必须是第 4 节 allowlist 的子集。

| 模块目录 | 程序集 | 命名空间 | 测试程序集 |
| --- | --- | --- | --- |
| modules/session | Lumio.Client.Session | Lumio.Client.Session | Lumio.Client.Session.Tests |
| modules/connection | Lumio.Client.Connection | Lumio.Client.Connection | Lumio.Client.Connection.Tests |
| modules/handshake | Lumio.Client.Handshake | Lumio.Client.Handshake | Lumio.Client.Handshake.Tests |
| modules/replica | Lumio.Client.Replica | Lumio.Client.Replica | Lumio.Client.Replica.Tests |
| modules/prediction | Lumio.Client.Prediction | Lumio.Client.Prediction | Lumio.Client.Prediction.Tests |
| modules/input | Lumio.Client.Input | Lumio.Client.Input | Lumio.Client.Input.Tests |
| modules/persistence | Lumio.Client.Persistence | Lumio.Client.Persistence | Lumio.Client.Persistence.Tests |
| modules/observability | Lumio.Client.Observability | Lumio.Client.Observability | Lumio.Client.Observability.Tests |
| modules/unity-adapter | Lumio.Client.UnityAdapter | Lumio.Client.UnityAdapter | Lumio.Client.UnityAdapter.Tests |
| modules/hybridclr-adapter | Lumio.Client.HybridClrAdapter | Lumio.Client.HybridClrAdapter | Lumio.Client.HybridClrAdapter.Tests |
| modules/bot | Lumio.Client.Bot | Lumio.Client.Bot | Lumio.Client.Bot.Tests |

## 3. 目录、命名与文件边界

统一布局：

```text
modules/<name>/
├─ README.md                       # 已有契约，不写任务状态
├─ src/
│  ├─ Lumio.Client.<Module>.csproj
│  ├─ Public/                      # 稳定端口、值类型、工厂
│  └─ Internal/                    # 默认实现、状态机、队列、Adapter
└─ tests/
   ├─ Lumio.Client.<Module>.Tests.csproj
   ├─ Unit/
   ├─ Contract/
   ├─ Property/
   ├─ Fault/
   └─ Performance/                # 仅有预算要求的模块
```

硬约束：

- 不创建 `common`、`shared`、`utils`、`contracts`、`composition`、`presentation`、`config`、`auth`、`replay` 模块。
- 跨模块稳定值放在语义拥有者模块；生成协议值由上游 package 提供。
- 一个文件只放一个主要 public type 或一个内部职责；不得出现 `ClientManager.cs`、`Everything.cs`、全局 service locator。
- 核心 public API 只出现 BCL primitive、模块自有不可变值、上游生成 Contract 或 Runtime Port 类型。

## 4. ProjectReference / asmdef allowlist

| 消费者程序集 | 唯一允许的能力模块引用 | 附加禁止 |
| --- | --- | --- |
| Lumio.Client.Observability | 无 | 禁止引用其他能力模块、UnityEngine、HybridCLR、平台 SDK |
| Lumio.Client.Connection | Observability | 只消费事件端口；不得引用 Handshake/Session/Replica/Prediction |
| Lumio.Client.Handshake | Connection, Observability | 通过连接公共端口交换生成协议帧 |
| Lumio.Client.Replica | Observability | 不得引用 Prediction 或 Session；不直接 Commit Runtime |
| Lumio.Client.Prediction | Observability | 不得引用 Replica 或 Session；不直接 Commit Runtime |
| Lumio.Client.Input | Observability | 不引用 Prediction；只产生 Sample/Candidate |
| Lumio.Client.Persistence | Observability | 不引用 Session；只实现已验证 Artifact/Checkpoint 窄端口 |
| Lumio.Client.Session | Connection, Handshake, Replica, Prediction, Input, Persistence, Observability | 唯一跨能力编排者；不得引用 Unity/HybridCLR/Bot/Game 实现 |
| Lumio.Client.UnityAdapter | Session, Input, Observability | Unity 类型只留在该程序集；不让核心反向引用 |
| Lumio.Client.HybridClrAdapter | Handshake, Observability | 不引用 Session/UnityAdapter；通过 Scope Loader 端口注入 Session |
| Lumio.Client.Bot | Session, Input, Observability | 不得引用 Connection/Handshake/Replica/Prediction 的内部实现或简化协议 |
| Lumio.Client.Bot.Host | 作为 Composition Root 可引用装配所需公共程序集 | 没有生产模块可反向引用 Host |

### 4.1 机器校验

`eng/project-reference-allowlist.json` 为唯一机器规则来源。ArchitectureTests 必须读取所有 `.csproj`、编译后的 assembly reference 与 Unity `.asmdef`，同时验证：

1. 实际边均在 allowlist 内；allowlist 中的 production DAG 无环。
2. 核心模块不引用 `UnityEngine*`、`Unity.InputSystem*`、`HybridCLR*`、`System.Net.Sockets`、Serilog/OpenTelemetry supplier API。
3. `replica` 与 `prediction` 互不引用。
4. `bot` 只通过 Session/Input/Observability 公共 API。
5. 没有 `LumioGame.ClientGameplay` 或 Server 实现引用。
6. `InternalsVisibleTo` 仅指向本模块测试程序集。

## 5. 生成契约三层接入

| 层 | 内容 | 依赖方向 | 禁止 |
|---|---|---|---|
| Host/Runtime Port | LumioGameRuntime 发布的 Handle、Transaction Request/Outcome、Mapper/Presentation Port | Client module → package | 复制 Runtime storage、直接 ECS/World 实现引用 |
| 纯生成 Contract Artifact | Envelope、Schema、ErrorCode、Ack、Config/Artifact、Event、Fixture corpus | module → generated package | 在 LumioClient 新建字段、手写第二 Codec |
| Game/Platform Adapter | Game Mapper、Capability Provider、Scope Activator、Presentation Binding | 产品 Composition Root → Client public ports | Client core → Gameplay 实现工程 |

Wave 0 生成 `eng/upstream-api-map.md`，每一行记录“设计别名、真实 package、真实全名、版本、fixture 路径、消费者”。映射无法完成时只允许 Fake/contract compilation，不允许猜字段。

## 6. 可复现验证命令

```bash
dotnet --version
dotnet restore LumioClient.slnx --locked-mode
dotnet format LumioClient.slnx --verify-no-changes --no-restore
dotnet build LumioClient.slnx -c Release --no-restore
dotnet test tests/Lumio.Client.ArchitectureTests/Lumio.Client.ArchitectureTests.csproj -c Release --no-build
dotnet test LumioClient.slnx -c Release --no-build
node .spec/tools/spec-lint.mjs
node --test .spec/tools/spec-lint.test.mjs
```

Foundation 额外命令：

```bash
dotnet test tests/Lumio.Client.IntegrationTests/Lumio.Client.IntegrationTests.csproj   -c Release --no-build --filter "Category=Foundation"
dotnet run --project modules/bot/host/Lumio.Client.Bot.Host.csproj   -c Release --no-build -- foundation   --transport local-embedded --fixture foundation-happy-path
```

## 7. Composition Root 合同

Composition Root 只存在于：

1. `modules/bot/host`：Headless/Bot 可执行装配。
2. `LumioGame` 的 Unity Bootstrap：装配 Unity Input、Presentation、可选 HybridCLR。
3. `tests/Lumio.Client.IntegrationTests`：装配 Fake Runtime、生成 Fixture Server、Fault Decorator。

装配顺序：Observability → Persistence/Capability/Mapper/Sinks → Connection Factory → Handshake → Replica/Prediction/Input → Session → Bot 或 Unity Host。关闭顺序由 Session/Host 的资源 ledger 负责，不由通用 DI 容器猜测。

## 8. 线程、取消、队列与借用规则

- **Client Owner Thread**：唯一允许调用 `IClientSession.Tick`、修改 Session/Replica/Prediction 可变状态、激活 Config/Scope、观察 Runtime outcome 的线程。
- **Transport/Storage/Sink Worker**：只能产出带 generation/attempt 的不可变 completion 到有界队列；不得回调 Session。
- **Unity Main Thread**：只存在于 unity-adapter/hybridclr-adapter 的 UnitySurface/Official Adapter。
- **借用数据**：`Span<T>` 仅在方法调用期间有效；任何跨异步/跨 Tick 数据必须复制到模块拥有的缓冲或不可变值。
- **取消**：外部 `CancellationToken` 被转换为带 generation 的内部事件；token callback 不直接改状态。
- **QueueFull**：所有队列满载均是显式结果。Critical queue 不丢数据；普通输入是否拒绝由模块策略定义；禁止无限等待。
- **终态**：每个 generation 只能有一个 terminal outcome。Fault > ForcedClose > Cancel > StableReject > Disconnect > CriticalQueueFull > Gap/Resync > Retryable/Timeout > Success > Normal。

## 9. 成熟方案选型总表

> **2026-08-29 经 T-00006 修订。** 本节依赖表原先在 5 处写 `10.0.11`：其中 `System.Threading.Channels` 与仓库锁定值不符（`Directory.Packages.props` = `10.0.0`），另四行的包全仓未被引用却写死补丁号（虚假精度，随 servicing 腐烂）。本次改为实际锁定值或不含数字的表述。同目录 `LumioClient_framework_scaffolding_manifest.txt` 与 `LumioClient_framework_scaffolding_audit.json` 记录的本文件 SHA-256 对应**修订前**版本，需随 R-00065 / R-00067 评审重算。

| 能力 | 候选（至少 2 个，含自研） | 选用 | 许可证 | 锁定版本策略 | AOT/Unity/确定性 | 为何不自研 | Adapter 隔离点 | 第三方类型穿过公共接口 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 有界异步队列 | `System.Threading.Channels`；`TPL Dataflow`；自研环形队列 | `System.Threading.Channels` 10.0.0 | MIT | 中央包版本 + lock file；BCL 同 SDK 锁定 | 核心可用；不依赖 Unity；队列顺序可测试；不把调度时序计入权威 Hash | 成熟背压、取消、完成语义；避免重写竞态复杂的队列 | 各模块 `Internal/Queues/*` | 否 |
| Socket 与字节管道 | BCL `Socket`/`SslStream` + `System.IO.Pipelines`；第三方网络框架；自研 TCP/KCP | BCL Socket/SslStream + Pipelines（随 SDK，未引入 NuGet 包） | MIT | 随 .NET SDK；Remote Adapter 单独 spike | Headless 可用；Unity/AOT 需目标矩阵；确定性只约束解码后顺序 | 平台栈已经处理 TCP/TLS/缓冲；本仓不拥有可靠 UDP 协议 | `connection/Internal/Transport/Remote` | 否 |
| 缓冲池 | `ArrayPool<T>`/`MemoryPool<T>`；RecyclableMemoryStream；自研池 | BCL ArrayPool/MemoryPool | MIT | 随 SDK | AOT 兼容；借用期由 Adapter 内部封闭；不得跨 Tick 泄漏 | 减少自研生命周期与泄漏风险 | Codec/Transport 内部 | 否 |
| 不可变集合 | `System.Collections.Immutable`；普通集合复制；自研持久化集合 | `System.Collections.Immutable`（当前未引入，引入时以中央包版本为准） | MIT | 中央包锁定 | 核心可用；权威排序必须显式，不依赖哈希迭代顺序 | 成熟快照语义；避免隐藏共享可变状态 | Snapshot/证据值内部 | 否 |
| 结构化日志门面 | `Microsoft.Extensions.Logging.Abstractions`；Serilog API；自研 Logger | `Microsoft.Extensions.Logging.Abstractions`（当前未引入，引入时以中央包版本为准），仅 Adapter | MIT | 中央包锁定 | 核心事件先进入生成 Event Schema；Unity/IL2CPP 需 Sink 验证 | 不发明日志级别、scope 与 provider 生命周期 | `observability/Internal/Adapters/Logging` | 否 |
| 日志落盘 | Serilog；NLog；自研滚动文件 | Serilog 4.4.0 + Serilog.Sinks.File 7.0.0 | Apache-2.0 | 中央包精确版本 + lock file | 仅 Production Sink；不进入确定性逻辑；IL2CPP 由 spike 验证 | 成熟 rolling/buffering；Failure Bundle 仍使用本仓生成事件编码 | `observability/Internal/Adapters/Serilog` | 否 |
| Metrics/Trace | OpenTelemetry .NET；Application Insights SDK；自研 Metrics/Trace | OpenTelemetry 1.17.0 | Apache-2.0 | 中央包精确版本 + exporter 白名单 | Headless 优先；IL2CPP/AOT 由 `SPIKE-OTEL-IL2CPP` 关闭 | 遵循标准语义与 exporter 生态；避免自研 trace context | `observability/Internal/Adapters/OpenTelemetry` | 否 |
| 配置/JSON | `System.Text.Json` source generation；Newtonsoft.Json；自研解析器 | `System.Text.Json` source generation | MIT | 随 SDK；生成 Context 纳入编译 | AOT 友好；禁止反射 fallback；不用于替代生成协议 Codec | BCL 提供成熟解析与源生成 | Host 配置、manifest Adapter | 否 |
| 哈希/签名 | BCL `System.Security.Cryptography`；libsodium wrapper；自研 | BCL Cryptography | MIT | 随 SDK | 平台支持矩阵内使用；验证结果不暴露算法实现类型 | 密码学不能由项目自行实现 | Artifact 验证 Adapter | 否 |
| 压缩 | 上游生成 Contract 指定算法；BCL 压缩；自研 | 仅调用生成 Contract/上游发布实现；BCL 仅用于非协议 Bundle | MIT/以上游为准 | 跟随 Contract Artifact 锁定 | 协议确定性由上游向量验证；本仓不选择新的 wire 算法 | 避免产生第二协议与跨端不一致 | Codec 或 Failure Bundle Adapter | 否 |
| 单元测试 | xUnit v3；NUnit；自研 runner | xunit.v3 3.2.2 + Microsoft.NET.Test.Sdk 18.8.1 | Apache-2.0/MIT | 中央包精确版本 | Headless；Unity 专用测试继续用 Unity Test Framework | 成熟 discovery、filter、并发配置 | tests 项目内部 | 否 |
| 属性测试 | FsCheck.Xunit；Hedgehog；自研随机器 | FsCheck.Xunit 3.3.4 | BSD-3-Clause | 中央包精确版本；seed 固化进失败证据 | 不进入生产；确定性复现 seed | 成熟 shrink 与随机序列模型 | tests/Property | 否 |
| 依赖图测试 | ArchUnitNET；自写 MSBuild binlog parser；纯人工 | ArchUnitNET.xUnitV3 0.13.3 + 小型 allowlist 校验器 | Apache-2.0 | 中央包锁定；allowlist 数据版本化 | 只在测试工具链；Unity asmdef 另做 JSON 图检查 | 成熟程序集规则表达；自研仅限仓库特定 allowlist 读取 | `tests/Lumio.Client.ArchitectureTests` | 否 |
| Unity 输入 | Unity Input System；旧 Input Manager；自研输入系统 | Unity Input System 1.17.0 | Unity Companion License | UPM lock + Unity 版本锁定 | 只在 unity-adapter；核心仅接收平台无关 Sample | 官方设备/Action/重绑定能力成熟 | `unity-adapter/UnitySurface/Input` | 否 |
| HybridCLR | 官方 HybridCLR；原生 AssemblyLoadContext；自研 IL VM | 官方 HybridCLR 8.12.0 候选，须通过版本/许可/AOT spike | 以官方发行许可审查为准 | Unity Package lock；仅在 spike 关闭后固化 | 仅 Unity 支持矩阵；核心接口不携带 HybridCLR 类型 | 不维护第二 IL 运行时或自制 loader | `hybridclr-adapter/Internal/Official` | 否 |
| 持久化文件 | BCL FileStream + 原子替换；SQLite；自研数据库 | Foundation 只定义窄端口；Production 默认 BCL 文件原子替换，SQLite 仅在 spike 证明需要后采用 | MIT/Public Domain（视 SQLite 包） | 平台矩阵与 lock file | 异步 I/O 不在 Owner Tick 内阻塞；格式使用生成 Artifact/manifest | 避免在第一阶段引入数据库运维面 | `persistence/Internal/FileSystem` | 否 |
| Host 生命周期 | Generic Host；自建 service locator；显式构造 | Bot Host 用 Generic Host（随 SDK，当前未引入 `Microsoft.Extensions.Hosting` 包）；核心模块显式构造 | MIT | 中央包锁定 | Bot 为 net10.0；Unity 不依赖 Generic Host | 核心保持透明生命周期；Host 采用成熟启动/停止模型 | `bot/host` | 否 |
| CLI | `System.CommandLine`；Spectre.Console.Cli；手写参数解析 | `System.CommandLine` 稳定版，锁定于中央包 | MIT | 中央包精确版本 | 仅 Bot Host；不进入 Unity/AOT | 成熟校验和 help；避免命令行边角错误 | `bot/host/CommandLine` | 否 |

## 10. 跨模块端口总表

| 端口 | 定义模块 | 消费模块 | 签名摘要 | 线程 |
| --- | --- | --- | --- | --- |
| `IClientEventWriter` | observability | 全部模块 | `TryWrite(in GeneratedContract.ClientEventRecord) -> ClientEventWriteResult`; `GetSnapshot()` | 任意生产者线程；必须非阻塞 |
| `IClientEventSink` | observability | Composition Root/observability | `WriteBatchAsync(ReadOnlyMemory<ClientEventRecord>, CancellationToken)` | Dispatcher Worker；不得在 Owner Tick 同步调用外部 I/O |
| `IClientConnectionFactory` | connection | session/Host | `Create(in ClientConnectionCreateRequest, out IClientConnection?)` | Composition Root 或 Client Owner Thread |
| `IClientConnection` | connection | handshake/session | `Start`; `TrySend`; `DrainEvents`; `RequestClose`; `GetSnapshot` | 命令与 Drain 仅 Client Owner Thread；底层回调入有界队列 |
| `ITransportFaultPolicy` | connection | connection Fixture | 对 drop/duplicate/delay/disconnect 做确定性决策 | 仅测试/故障装饰器线程；seed 固化 |
| `IClientHandshake` | handshake | session | `Begin`; `HandleFrame`; `Poll`; `Cancel`; `GetSnapshot` | Client Owner Thread |
| `IPlatformCapabilityProvider` | handshake | handshake/Host | `QueryAsync(in PlatformCapabilityQuery, CancellationToken)` | 异步 Adapter；完成事件带 Attempt/Generation 回 Owner Thread |
| `IInputSampleIngress` | input | Unity/Bot | `TryEnqueue(in RawInputSample, out InputEnqueueReceipt)` | 平台输入线程或 Bot loop；非阻塞 |
| `IInputCommandSource` | input | session | `DrainCandidates(Span<CandidateGameplayCommand>, in InputDrainContext)`; `SetBufferPolicy`; `GetSnapshot` | Client Owner Thread |
| `IGameInputMapper` | input | input | `Map(in SequencedInputSample, in InputMappingContext, out CandidateGameplayCommand)` | Client Owner Thread；确定性；无 I/O |
| `IClientReplica` | replica | session | `StageAuthority`; `DiscardStage`; `ObserveRuntimeOutcome`; `ResetForNewSession`; `GetSnapshot` | Client Owner Thread |
| `IReplicaMapper` | replica | replica | 生成 Contract 到 Runtime Apply Plan 的纯映射 | Client Owner Thread；无副作用 |
| `IClientPrediction` | prediction | session | `AcceptCandidate`; `DiscardCandidateStage`; `ObserveLocalPredictionOutcome`; `StageAuthority`; `DiscardAuthorityStage`; `ObserveRuntimeOutcome`; `ResetForNewSession` | Client Owner Thread |
| `IVerifiedSessionArtifactSource` | persistence | session/handshake | `ReadAsync(in VerifiedArtifactReadRequest, CancellationToken)` | 后台 I/O；结果带 Generation |
| `IClientCheckpointStore` | persistence | session | `ReadLatestAsync`; `WriteAsync`; `GetSnapshot` | 后台 I/O；Session 只传已提交 Checkpoint |
| `IClientSessionFactory` | session | Bot/Unity Composition Root | `Create(in ClientSessionDependencies, out IClientSession?)` | Composition Root |
| `IClientSession` | session | Bot/Unity Host | `RequestConnect`; `Tick`; `RequestClose`; `GetSnapshot` | 只允许 Client Owner Thread |
| `IClientGameplayScopeActivator` | session | 产品 Composition Root/HybridCLR Adapter | 准备、在 Tick Barrier 激活、释放 Gameplay Scope | 准备可异步；激活与释放回 Owner Thread |
| `IClientPresentationSink` | session | Unity/Headless | 接收已提交且 generation 匹配的不可变 Presentation Diff | Session 在 Owner Tick 产出；Adapter 可入自身有界队列 |
| `IUnityClientHost` | unity-adapter | LumioGame Unity Bootstrap | `Start`; `Update`; `Stop`; `GetSnapshot` | Unity Main Thread |
| `IHybridClrScopeLoader` | hybridclr-adapter | 产品 Scope Activator | `PrepareActivateAsync`; `PumpMainThread`; `ReleaseAsync`; `GetSnapshot` | 准备异步，官方 API 步骤在 Unity Main Thread |
| `IBotScenarioDriver` | bot | bot | `FillSamples(in BotDriverContext, Span<RawInputSample>)` | Bot Owner Loop；确定性 |
| `IHeadlessBotHost` | bot | Bot.Host | `RunAsync(in BotRunRequest, CancellationToken)` | 单 Bot Owner Loop；外部取消只入事件 |

## 11. Session 事件优先级与状态迁移

同一 Owner Tick 内先按 generation 丢弃迟到事件，再按以下优先级归并：

```text
Fault
> ForcedClose
> Cancel
> StableReject
> Disconnect
> CriticalQueueFull
> Gap / ResyncRequired
> Retryable / Timeout
> Success
> Normal message / input
```

同优先级按 `(producer_sequence, enqueue_sequence)` 稳定排序；排序只用于本地控制，不进入服务器权威 Hash。

| CurrentState | Event | Generation 条件 | NextState | Actions | 必须为零的副作用 |
| --- | --- | --- | --- | --- | --- |
| Disconnected | RequestConnect | 生成 G+1 | Connecting | 创建 Attempt；启动 Connection；记录 Generation | 不得创建 Scope/Runtime Handle |
| Connecting | ConnectionStarted | 等于当前 G | Negotiating | 启动认证/握手；只传生成契约 | 不得激活 Gameplay Scope |
| Connecting | Cancel | 等于当前 G | Closed | 取消 Attempt；关闭 Connection；释放已创建资源 | 不得接受随后到达的成功事件 |
| Connecting | Disconnect | 等于当前 G | Reconnecting 或 Closed | 按策略创建新 Generation 或终止 | 不得沿用旧 Attempt/Resume Token |
| Negotiating | HandshakeAccepted | 等于当前 G | Synchronizing | 验证已完成；准备 Scope；Config 进入 staging | 不得先创建 World Handle |
| Negotiating | StableReject | 等于当前 G | Closed | 输出稳定 Reject；关闭连接；保留证据 | Scope/Handle 副作用必须为零 |
| Negotiating | Cancel | 等于当前 G | Closed | Cancel 优先于 Reject/Success；释放 Attempt | 不得进入 Synchronizing |
| Negotiating | CriticalQueueFull | 等于当前 G | Faulted 或 Closed | 冻结接收；采集证据；按错误矩阵终止 | 不得覆盖已验证帧 |
| Synchronizing | ScopePrepared | 等于当前 G | Synchronizing | 在 Owner Tick Barrier 激活 Scope | 不得从后台线程发布激活 |
| Synchronizing | ScopeActivated | 等于当前 G | Synchronizing | 按 ECS→Voxel 顺序创建双 Handle；请求 FullSnapshot | 失败时逆序回滚 |
| Synchronizing | FullSnapshotValidated | 等于当前 G | Synchronizing | Replica Stage；Prediction Stage；调用单一 Runtime Authority Transaction | Stage 本身不得推进元数据 |
| Synchronizing | AuthorityCommitted | 等于当前 G | Active | Replica/Prediction Observe；推进 baseline；发送 BaselineAck；转发 Diff | Ack/Diff 必须晚于 Commit 与元数据推进 |
| Synchronizing | AuthorityAborted | 等于当前 G | Resyncing 或 Closed | Discard 两侧 Stage；按分类请求 Resync/终止 | 不得推进 baseline、Ack、Presentation |
| Active | InputCandidate | 等于当前 G | Active | Prediction Stage local；单一 Local Runtime Transaction；成功后分配 CommandSeq/PredictionKey | 失败不得消耗序号或进入历史 |
| Active | AuthorityUpdate | 等于当前 G | Active | 消息门→Replica Stage→Prediction Stage→单一 Runtime Transaction→元数据→Ack→Diff | Replica/Prediction 不得自行 Commit |
| Active | Gap | 等于当前 G | Resyncing | 停止普通输入发送；保留连接；请求 FullSnapshot | 不得重新握手 |
| Active | Disconnect | 等于当前 G | Reconnecting | 关闭旧连接；Generation+1；重新认证与握手 | 不得复用 Resume Token 或旧 Scope |
| Active | RequestClose | 等于当前 G | Closed | 按输入→Prediction→Replica→Voxel→ECS→Scope→Handshake→Connection 释放 | 不得让迟到回调复活资源 |
| Resyncing | FullSnapshotValidated | 等于当前 G | Active | 同权威事务路径重建 baseline；恢复输入策略 | 不得执行重新握手 |
| Resyncing | Disconnect | 等于当前 G | Reconnecting | 新 Generation；完整认证与握手 | 旧 resync 结果不得应用 |
| Reconnecting | ConnectionStarted | 等于新 G | Negotiating | 新 Attempt；重新认证/握手 | 不得沿用旧代次 capability/scope/handle |
| 任意非终态 | Fault | 等于当前 G | Faulted | 最高优先级；冻结状态；采集 Failure Bundle；逆序释放 | 不得被 Close/Success 覆盖 |
| 任意非终态 | ForcedClose | 等于当前 G | Closed | 优先于 Cancel/Reject/Disconnect；逆序释放 | 不得再接受业务事件 |
| 任意状态 | Event | 小于当前 G | 不变 | 计数并丢弃为 LateGeneration | 不得修改任何可变状态 |
| Closed | 任意事件 | 任意 | Closed | 只记录迟到证据 | 不得重新分配资源 |
| Faulted | 任意事件 | 任意 | Faulted | 只允许幂等释放/导出证据 | 不得自行重连 |

### 11.1 权威更新唯一事务顺序

```text
ConnectionEvent
→ Session ActiveMessageGate
→ Replica.StageAuthority
→ Prediction.StageAuthority
→ IClientRuntimePort.ApplyAuthoritativeTransaction（唯一调用）
→ Replica.ObserveRuntimeOutcome
→ Prediction.ObserveRuntimeOutcome
→ Config/Metadata barrier
→ BaselineAck
→ IClientPresentationSink.TryWrite(committed diff)
```

任一 Stage 失败：已创建的另一个 Stage 必须 Discard；Runtime 不调用。Runtime Aborted：两侧 Stage 均 Discard。Runtime outcome 无法确定：Session Faulted，禁止 Ack、Diff、metadata 推进。

## 12. 11 个模块实现级设架

## 12.1 `session`

### 1. 一句话职责

作为唯一客户端 Owner Thread 编排器，把 connection、handshake、scope、双 Runtime Handle、replica、prediction、input、persistence 与 presentation 串成冻结的状态机和单一事务。

### 2. 唯一拥有的可变状态

| 状态 | 创建者 | 唯一修改者 | 快照/证据 | 销毁者 | 失败恢复 |
| --- | --- | --- | --- | --- | --- |
| ClientSessionState | Factory | Session Tick only | ClientSessionSnapshot | Close/Fault | terminal immutable |
| SessionGeneration/Attempt | RequestConnect | event arbiter | snapshot/event | new connect/close | late events dropped |
| Event priority inbox | Session | producers enqueue; Owner drain | queue evidence | terminal drain | critical full faults |
| Scope lease | Handshake accepted | activation gate/close | resource ledger | reverse release | never before accepted |
| ECS/Voxel Handles | Runtime ledger | Session only via Runtime Port | opaque handles/receipt | Voxel then ECS | create ECS then Voxel |
| Config staging/active | handshake/update | Tick barrier only | snapshot | scope close | staged config not visible |
| authority transaction stage bundle | message orchestration | Session only | transaction receipt | commit/abort | single commit point |
| presentation diff queue | committed outcome | Session produces | sink result | generation close | only committed diff |

### 3. 公共端口（精确 C# 签名）

```csharp
namespace Lumio.Client.Session;

public interface IClientSessionFactory
{
    ClientSessionCreateResult Create(
        in ClientSessionDependencies dependencies,
        out IClientSession? session);
}

public interface IClientSession
{
    SessionCommandResult RequestConnect(
        in SessionConnectRequest request,
        CancellationToken cancellationToken);
    SessionTickResult Tick(in ClientOwnerTick tick);
    SessionCommandResult RequestClose(in SessionCloseRequest request);
    ClientSessionSnapshot GetSnapshot();
}

public interface IClientGameplayScopeActivator
{
    ValueTask<GameplayScopePrepareResult> PrepareAsync(
        in GameplayScopePrepareRequest request,
        CancellationToken cancellationToken);
    GameplayScopeActivationResult ActivateAtTickBarrier(
        in GameplayScopeActivationRequest request);
    ValueTask<GameplayScopeReleaseResult> ReleaseAsync(
        GameplayScopeLease lease,
        CancellationToken cancellationToken);
}

public interface IClientPresentationSink
{
    PresentationWriteResult TryWrite(
        in RuntimeContract.CommittedPresentationDiff diff,
        ulong sessionGeneration);
}
```

**禁止出现在签名中的类型：**

- UnityEngine/HybridCLR/Socket/Serilog 类型
- LumioGame.ClientGameplay 实现工程类型
- Server 实现/Resume Token/第二协议

### 4. 内部类型与文件树

| 目标文件 | 单一职责 |
| --- | --- |
| modules/session/src/Public/IClientSession.cs | stable owner API |
| modules/session/src/Public/IClientSessionFactory.cs | composition entry |
| modules/session/src/Public/IClientGameplayScopeActivator.cs | scope gate port |
| modules/session/src/Public/IClientPresentationSink.cs | committed diff port |
| modules/session/src/Public/ClientSessionDependencies.cs | leaf ports + Runtime port bundle |
| modules/session/src/Public/ClientSessionState.cs | frozen state enum |
| modules/session/src/Public/SessionGeneration.cs | opaque generation |
| modules/session/src/Public/SessionConnectRequest.cs | connect value |
| modules/session/src/Public/ClientSessionSnapshot.cs | evidence |
| modules/session/src/Internal/State/SessionStateMachine.cs | transition table implementation |
| modules/session/src/Internal/Events/SessionEvent.cs | internal discriminated event |
| modules/session/src/Internal/Events/SessionEventInbox.cs | bounded inbox |
| modules/session/src/Internal/Events/SessionEventArbiter.cs | priority and generation filter |
| modules/session/src/Internal/Lifecycle/RuntimeHandleLedger.cs | ECS/Voxel order |
| modules/session/src/Internal/Lifecycle/SessionResourceLedger.cs | full reverse release |
| modules/session/src/Internal/Gates/GameplayScopeActivationGate.cs | activation barrier |
| modules/session/src/Internal/Gates/ActiveMessageGate.cs | phase/generation/schema/permission matrix |
| modules/session/src/Internal/Config/ClientConfigStagingArea.cs | staging→barrier activation |
| modules/session/src/Internal/Orchestration/FirstConnectOrchestrator.cs | connect pipeline |
| modules/session/src/Internal/Orchestration/AuthorityUpdateOrchestrator.cs | single authority transaction |
| modules/session/src/Internal/Orchestration/LocalPredictionOrchestrator.cs | local transaction |
| modules/session/src/Internal/Orchestration/ResyncOrchestrator.cs | same-connection resync |
| modules/session/src/Internal/Orchestration/ReconnectOrchestrator.cs | new generation full handshake |
| modules/session/src/Internal/Orchestration/CloseOrchestrator.cs | reverse release |
| modules/session/tests/Unit/SessionStateMachineTests.cs | state table |
| modules/session/tests/Unit/SessionEventArbiterTests.cs | priority |
| modules/session/tests/Unit/SessionMessageGateTests.cs | matrix |
| modules/session/tests/Unit/RuntimeHandleLedgerTests.cs | order |
| modules/session/tests/Fault/AuthorityTransactionFaultMatrixTests.cs | every fault point |
| modules/session/tests/Fault/SessionRaceTests.cs | late generation/races |

### 5. 成熟依赖

| 能力 | 候选 | 选用 | 许可证/版本 | AOT/确定性 | 为何不自研 | Adapter 隔离点 | 第三方类型穿过公共接口 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 编排 | 显式 state machine / Stateless / 自研 event bus | 显式纯 transition table + 专用 orchestrators | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Internal/State/Orchestration | 否 |
| 队列 | Channels / callback direct / 自研 | Channels bounded inbox | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Internal/Events | 否 |
| Runtime | LumioGameRuntime published Port / direct ECS / second runtime | published Runtime Port | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | ClientSessionDependencies | 否 |

### 6. 控制流（实现顺序）

1. 首次连接：`RequestConnect` 分配新 generation，Connection.Start；事件进入 Session inbox。
2. Connection ready 后启动认证/Handshake；Accepted 前 Scope、Config active、ECS/Voxel Handles 均不存在。
3. Handshake Accepted 后读取/验证 Artifact，Config 只 staging；调用 Scope Prepare，并在 Owner Tick Barrier Activate。
4. Scope 激活成功后通过 Runtime Port 先创建 ECS Handle，再创建 Voxel Handle；任一步失败逆序释放。
5. FullSnapshot 经过 Active/Sync 消息门，Replica Stage 与 Prediction Authority Stage 均不提交。
6. Session 组合两侧 plan、Config context、ECS/Voxel opaque handles，调用一次 `ApplyAuthoritativeTransaction`。
7. Committed 后严格按 Replica Observe→Prediction Observe→Config barrier metadata→Baseline/Ack→Presentation Diff 顺序；Aborted 则 Discard 两侧且副作用为零。
8. Active 输入：Input Drain Candidate→Prediction local stage→一次 Runtime local transaction；Committed 后才分配 ClientCommandSeq/Key 并发送。
9. Gap：进入 Resyncing，保留连接与已验证握手，停止普通输入，申请 FullSnapshot；不重握手。
10. Disconnect：进入 Reconnecting，新 generation，重新认证+握手+Scope+Handles；不使用 Resume Token。
11. Close/Fault：停止输入/发送/presentation，再释放 Prediction→Replica→Voxel→ECS→Scope→Handshake→Connection。

### 7. 与 Runtime / 生成契约的接缝

- `IClientRuntimePort`、双 Handle、Local/Authority Transaction Request/Outcome 来自 LumioGameRuntime 发布 API。
- Envelope/message matrix/ErrorCode/Ack/Config Artifact 来自生成 Contract。
- Game Mapper、Capability Provider、Gameplay Scope Activator 与 Presentation Sink 由外部 Composition Root 注入；核心不引用实现。

### 8. 明确不实现

| 实现者最容易误做 | 正确归属/做法 |
| --- | --- |
| Session 内复制 Envelope/事务字段 | 只绑定上游发布类型并按冻结顺序编排 |
| Resync 重新握手或 Reconnect 使用 Resume Token | Resync 保持连接；Reconnect 新代次完整重认证/握手且无 Resume Token |
| 让 leaf module 或 callback 直接改 Session 状态 | 所有异步事实入 bounded inbox，Owner Tick + arbiter 单点修改 |

### 9. 失败分类如何变成代码

| 分类 | 结果类型/码 | 谁通知 Session | 副作用约束 |
| --- | --- | --- | --- |
| 可重试 | ConnectTimeout/TransientIo/ArtifactBusy | Session event 按优先级决定 retry | 未提交阶段无副作用 |
| 可拒绝 | Handshake/permission/message stable reject | Closed/保持 Active 依矩阵 | 被拒绝路径零 Runtime 调用 |
| 需 Resync | Gap/BaselineMismatch/HistoryUnavailable | 进入 Resyncing | 不重握手、不推进 baseline |
| 可致命 | CriticalQueueFull/IndeterminateTransaction/ledger fault | Faulted + Failure Bundle + reverse release | 不允许 Ack/Presentation/重连掩盖 |

### 10. 可观测性埋点

| 稳定事件名/生成 EventId | 产生位置 | 进入 Failure Bundle |
| --- | --- | --- |
| SessionStateChanged | state machine accepted transition | 是 |
| SessionEventSuppressed | priority/generation drop | 是 |
| GameplayScopeActivated | barrier activation | 是 |
| RuntimeHandlesCreated | ECS+Voxel success | 是 |
| AuthorityTransactionOutcome | single runtime call | 是 |
| SessionResyncStarted | gap path | 是 |
| SessionReconnectGeneration | new generation | 是 |
| SessionReleaseStep | reverse release | 是 |

### 11. 测试面（先于实现）

| 测试 | 输入 | 期望 | 需要 Unity | LocalEmbedded/Remote Fixture |
| --- | --- | --- | --- | --- |
| `SessionStateMachineTests.HappyPath_ConnectToActive` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `SessionStateMachineTests.ScopeMustActivateBeforeWorldHandles` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `SessionEventArbiterTests.CancelBeatsRejectDisconnectAndSuccess` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `SessionEventArbiterTests.FaultBeatsCloseAndCommitted` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `SessionMessageGateTests.InvalidMatrix_HasZeroLeafCalls` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `RuntimeHandleLedgerTests.CreateEcsThenVoxel_DestroyVoxelThenEcs` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `AuthorityUpdateOrchestratorTests.Committed_MetadataAckDiffOrder` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `AuthorityUpdateOrchestratorTests.Aborted_NoMetadataAckOrDiff` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `AuthorityUpdateOrchestratorTests.SecondStageFails_FirstStageDiscarded` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `AuthorityTransactionFaultMatrixTests.EveryFaultPoint_PreservesContract` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `ResyncReconnectTests.Resync_DoesNotHandshake` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `ResyncReconnectTests.Reconnect_NewGenerationReauthAndHandshake_NoResume` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `SessionRaceTests.LateG1Success_CannotActivateG2` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `SessionRaceTests.QueueFullCloseFault_TerminalIsDeterministic` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |

### 12. Foundation 最小切片 vs 完整模块

| 阶段 | 必须实现的范围 |
| --- | --- |
| Foundation | LocalEmbedded happy path、state/priority/gates、scope+handles、FullSnapshot→Active、Gap/Resync、Close、Bot |
| Vertical Slice | 真实 Input→Prediction→Correction、Config、Persistence、Presentation Diff、Remote |
| Production | 容量/时限、外部 Sink、Unity/HybridCLR 设备矩阵、故障证据预算 |

### 13. 任务拆分

1. `w5-session-contract-and-resource-ledger` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
2. `w5-session-event-arbiter` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
3. `w5-session-active-message-gate` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
4. `w5-session-first-connect-orchestration` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
5. `w5-session-authority-transaction-orchestration` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
6. `w5-session-resync-reconnect` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
7. `w5-session-close-fault-release` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
8. `w5-authority-transaction-fault-matrix` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。

### 14. 开放阻塞

- `UPSTREAM-RUNTIME-CONTRACT-API-MAP`：Runtime port/handle/transaction/outcome 真名；阻塞真实 Session transaction。
- `UPSTREAM-GENERATED-CONTRACT-API-MAP`：消息矩阵/Ack/Config/ErrorCode；阻塞 protocol fixtures。

## 12.2 `connection`

### 1. 一句话职责

拥有客户端传输连接、代次、有界收发队列、生产 Codec 路径和 Transport Fault 装饰；删除后 Session 无法以统一语义连接 LocalEmbedded 或 Remote。

### 2. 唯一拥有的可变状态

| 状态 | 创建者 | 唯一修改者 | 快照/证据 | 销毁者 | 失败恢复 |
| --- | --- | --- | --- | --- | --- |
| ConnectionGeneration | Factory/Create | Connection Owner | Snapshot/Event | Close | 新连接必须新代次 |
| 连接状态 | Connection | Client Owner Thread 通过命令；底层回调只入队 | ConnectionEvent | RequestClose/Dispose | 终态只能一次 |
| Ingress/Egress 队列 | Transport Adapter | Producer/Owner Drain | 深度/QueueFull | Close drain | 满载不覆盖 |
| Replay window/decoder buffer | Connection | Owner Drain | 诊断快照 | Generation 结束 | Decode 错误不污染下一帧 |

### 3. 公共端口（精确 C# 签名）

```csharp
namespace Lumio.Client.Connection;

public interface IClientConnectionFactory
{
    ClientConnectionCreateResult Create(
        in ClientConnectionCreateRequest request,
        out IClientConnection? connection);
}

public interface IClientConnection
{
    ConnectionCommandResult Start();
    ConnectionSendResult TrySend(in GeneratedContract.EncodedEnvelope envelope);
    int DrainEvents(Span<ConnectionEvent> destination);
    ConnectionCommandResult RequestClose(in GeneratedContract.ConnectionCloseReason reason);
    ClientConnectionSnapshot GetSnapshot();
}

public interface ITransportFaultPolicy
{
    TransportFaultDecision Decide(in TransportFaultContext context);
}
```

**禁止出现在签名中的类型：**

- `Socket`、`SslStream`、`PipeReader`/`PipeWriter`
- `Channel<T>`/第三方网络框架类型
- Handshake/Session/Unity 类型

### 4. 内部类型与文件树

| 目标文件 | 单一职责 |
| --- | --- |
| modules/connection/src/Public/IClientConnectionFactory.cs | 连接工厂 |
| modules/connection/src/Public/IClientConnection.cs | 稳定连接端口 |
| modules/connection/src/Public/ITransportFaultPolicy.cs | 确定性故障装饰端口 |
| modules/connection/src/Public/ConnectionGeneration.cs | 不透明代次值 |
| modules/connection/src/Public/ClientConnectionCreateRequest.cs | 模式、容量、生成协议依赖 |
| modules/connection/src/Public/ConnectionEvent.cs | 不可变连接事件 |
| modules/connection/src/Public/ClientConnectionSnapshot.cs | 只读证据 |
| modules/connection/src/Internal/State/ConnectionStateMachine.cs | 连接状态机 |
| modules/connection/src/Internal/Queues/ConnectionEventQueue.cs | 有界 ingress |
| modules/connection/src/Internal/Queues/ConnectionSendQueue.cs | 有界 egress |
| modules/connection/src/Internal/Protocol/GeneratedEnvelopeCodecAdapter.cs | 唯一生产 Codec |
| modules/connection/src/Internal/Protocol/ReplayWindow.cs | 重复/乱序窗口 |
| modules/connection/src/Internal/Transport/LocalEmbedded/LocalEmbeddedTransport.cs | 进程内字节传输 |
| modules/connection/src/Internal/Transport/LocalEmbedded/LocalEmbeddedEndpointPair.cs | 双向有界端点 |
| modules/connection/src/Internal/Transport/Remote/SocketPipelineTransport.cs | Remote BCL 管道 |
| modules/connection/src/Internal/Transport/Remote/SslChannelAuthenticator.cs | TLS/通道认证 |
| modules/connection/src/Internal/Faults/FaultDecoratingTransport.cs | drop/duplicate/delay/disconnect |
| modules/connection/tests/Contract/LocalEmbeddedTransportTests.cs | 保真合同 |
| modules/connection/tests/Contract/RemoteTransportContractTests.cs | Remote 同合同 |
| modules/connection/tests/Fault/ConnectionCloseRaceTests.cs | 终态竞态 |
| modules/connection/tests/Performance/RemoteTransportBudgetTests.cs | 预算 |

### 5. 成熟依赖

| 能力 | 候选 | 选用 | 许可证/版本 | AOT/确定性 | 为何不自研 | Adapter 隔离点 | 第三方类型穿过公共接口 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 队列 | Channels / Dataflow / 自研 | Channels | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Internal/Queues | 否 |
| 远程传输 | BCL Socket/Pipelines / Netty / 自研 | BCL Socket/Pipelines | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Internal/Transport/Remote | 否 |
| Codec | 生成 Contract / MessagePack 自定 / 自研 | 生成 Contract | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Internal/Protocol | 否 |

### 6. 控制流（实现顺序）

1. `Create` 固化 mode、capacity、generation、Codec 和 fault policy，返回尚未启动的连接。
2. `Start` 只在 Owner Thread 触发；Adapter 异步建立 LocalEmbedded 字节端点或 Remote Socket/TLS。
3. 底层接收字节进入 buffer，调用生成 Codec 解码为 Envelope；验证失败产生稳定 ConnectionEvent。
4. 有效事件写入 ingress 有界队列；Owner Thread 每 Tick 调用 `DrainEvents`。
5. `TrySend` 接受已经生成编码的 Envelope，写入 egress 队列；队列满立即返回。
6. LocalEmbedded 仍执行 Encode→字节队列→Decode，不传 typed object，不同步重入。
7. Remote 使用相同 Codec、队列、replay window、generation guard 与 fault decorator。
8. Close/Disconnect/Cancel 经过状态机合并为单一 generation 终态，迟到回调只计数。

### 7. 与 Runtime / 生成契约的接缝

- Envelope、EncodedEnvelope、ConnectionCloseReason、Codec/验证向量来自生成 Contract。
- 本模块不定义认证、握手、权限或 Tick 语义；只承载协议帧和连接事实。

### 8. 明确不实现

| 实现者最容易误做 | 正确归属/做法 |
| --- | --- |
| LocalEmbedded 直接传 typed object | 强制生产 Encode/Decode 和有界字节队列 |
| 实现自有 TCP/KCP/重传协议 | Remote 只用平台 Socket/TLS/Pipelines，游戏协议来自上游 |
| 底层回调直接调用 Session | 回调只写队列，Session 在 Owner Tick Drain |

### 9. 失败分类如何变成代码

| 分类 | 结果类型/码 | 谁通知 Session | 副作用约束 |
| --- | --- | --- | --- |
| 可重试 | ConnectTimeout/TransientIo | ConnectionEvent 通知 Session | 不产生 Handshake accepted |
| 可拒绝 | ChannelAuthReject/InvalidEnvelope | 稳定 Reject event | 不向 Handshake 转发无效帧 |
| 需 Resync | ReplayGap 由上层消息门分类 | 只上报 frame/replay 事实 | 不自行 Resync |
| 可致命 | CriticalIngressFull/DecoderCorrupt | Fault event | 不得覆盖或跳过 Critical frame |

### 10. 可观测性埋点

| 稳定事件名/生成 EventId | 产生位置 | 进入 Failure Bundle |
| --- | --- | --- |
| ConnectionStart | Start | 否 |
| ConnectionEstablished | transport ready | 否 |
| ConnectionQueueFull | ingress/egress full | 是 |
| ConnectionDecodeRejected | codec reject | 是 |
| ConnectionLateGeneration | late callback | 是 |
| ConnectionTerminal | single terminal emission | 是 |

### 11. 测试面（先于实现）

| 测试 | 输入 | 期望 | 需要 Unity | LocalEmbedded/Remote Fixture |
| --- | --- | --- | --- | --- |
| `LocalEmbeddedTransportTests.TypedShortcut_IsImpossibleAndCodecRuns` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 共用 |
| `LocalEmbeddedTransportTests.Send_DoesNotSynchronouslyReenterReceiver` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 共用 |
| `ConnectionReplayWindowTests.DuplicateAndOutOfOrder_FollowContract` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `GeneratedEnvelopeCodecAdapterFixtureTests.ValidInvalidVectors` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `ConnectionQueueFullTests.IngressFull_NeverOverwritesValidatedFrame` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `ConnectionQueueFullTests.EgressFull_ReturnsBeforeBlocking` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `ConnectionCloseRaceTests.CloseDisconnectSuccess_EmitsOneTerminal` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `TransportFaultDecoratorTests.DropDuplicateDelayDisconnect_AreDeterministic` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `LateGenerationTests.G1Callback_CannotReachG2` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `RemoteTransportContractTests.LoopbackAndLocalEmbeddedProduceEquivalentProtocolTrace` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 共用 |
| `RemoteTransportFaultTests.CancelCloseAndLateCallbackEmitOneTerminalGeneration` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 共用 |
| `RemoteTransportFaultTests.ChannelAuthRejectsBeforeHandshake` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 共用 |
| `RemoteTransportBudgetTests.QueueFullAndCancellationCompleteWithinBudget` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 共用 |

### 12. Foundation 最小切片 vs 完整模块

| 阶段 | 必须实现的范围 |
| --- | --- |
| Foundation | LocalEmbedded、生成 Codec、有界队列、Generation、Fault Decorator |
| Vertical Slice | Remote Socket/TLS/Pipelines Adapter |
| Production | 目标平台性能/证书/代理/MTU 证据与容量调优 |

### 13. 任务拆分

1. `w2-connection-contract-and-generation` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
2. `w2-connection-localembedded-transport` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
3. `w2-connection-bounded-queues-and-faults` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
4. `w7-connection-remote-transport` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。

### 14. 开放阻塞

- `UPSTREAM-GENERATED-CONTRACT-API-MAP`：Envelope/Codec/CloseReason 真实类型；阻塞生产 Codec 绑定。
- `SPIKE-REMOTE-AOT`：2 个工作日验证 Unity/IL2CPP Socket/Pipelines/TLS；阻塞 Remote 默认支持矩阵。

## 12.3 `handshake`

### 1. 一句话职责

在已建立 connection 上执行认证后握手、能力查询、Release/Schema/ABI/Role/Claim 拒绝矩阵，并输出不可变结果；删除后 Session 无法安全进入 Scope 激活。

### 2. 唯一拥有的可变状态

| 状态 | 创建者 | 唯一修改者 | 快照/证据 | 销毁者 | 失败恢复 |
| --- | --- | --- | --- | --- | --- |
| HandshakeAttemptId | Begin | Handshake Owner | Snapshot/outcome | terminal/cancel | new generation new attempt |
| phase state | Handshake | Owner Thread HandleFrame/Poll | snapshot | terminal | 单向迁移 |
| capability query | Begin | Provider async completion | attempt-tagged result | cancel/terminal | late result dropped |
| validated negotiation result | Handshake | terminal only | Outcome | session releases | reject side effects zero |

### 3. 公共端口（精确 C# 签名）

```csharp
namespace Lumio.Client.Handshake;

public interface IClientHandshake
{
    HandshakeCommandStatus Begin(
        IClientConnection connection,
        in HandshakeBeginRequest request,
        out HandshakeAttemptId attemptId);
    HandshakeCommandStatus HandleFrame(in ConnectionEvent connectionEvent);
    HandshakeOutcomeKind Poll(out HandshakeOutcome outcome);
    HandshakeCommandStatus Cancel(
        HandshakeAttemptId attemptId,
        in GeneratedContract.HandshakeCancelReason reason);
    HandshakeSnapshot GetSnapshot();
}

public interface IPlatformCapabilityProvider
{
    ValueTask<PlatformCapabilityResult> QueryAsync(
        in PlatformCapabilityQuery query,
        CancellationToken cancellationToken);
}
```

**禁止出现在签名中的类型：**

- Socket/Transport 实现类型
- Runtime World Handle/Gameplay Scope 实现
- Unity/HybridCLR 类型（只通过 capability value）

### 4. 内部类型与文件树

| 目标文件 | 单一职责 |
| --- | --- |
| modules/handshake/src/Public/IClientHandshake.cs | 握手端口 |
| modules/handshake/src/Public/IPlatformCapabilityProvider.cs | 平台能力端口 |
| modules/handshake/src/Public/IClientHandshakeFactory.cs | 构造入口 |
| modules/handshake/src/Public/HandshakeAttemptId.cs | 不透明 attempt |
| modules/handshake/src/Public/HandshakeBeginRequest.cs | 生成契约与 generation |
| modules/handshake/src/Public/HandshakeOutcome.cs | accepted/rejected outcome |
| modules/handshake/src/Public/HandshakeSnapshot.cs | 只读证据 |
| modules/handshake/src/Internal/HandshakeStateMachine.cs | phase transition |
| modules/handshake/src/Internal/HandshakeAttempt.cs | attempt-owned state |
| modules/handshake/src/Internal/GeneratedHandshakeAdapter.cs | 生成 Contract 映射 |
| modules/handshake/src/Internal/HandshakeRejectClassifier.cs | 拒绝矩阵 |
| modules/handshake/src/Internal/CapabilityCompletionQueue.cs | 异步结果回 Owner |
| modules/handshake/tests/Unit/HandshakeStateMachineTests.cs | 主流程 |
| modules/handshake/tests/Contract/GeneratedHandshakeFixtureTests.cs | 向量 |
| modules/handshake/tests/Fault/HandshakeRaceTests.cs | 竞态 |

### 5. 成熟依赖

| 能力 | 候选 | 选用 | 许可证/版本 | AOT/确定性 | 为何不自研 | Adapter 隔离点 | 第三方类型穿过公共接口 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 异步 completion | Channels / callback 直调 / 自研 | Channels queue | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Internal/CapabilityCompletionQueue | 否 |
| 协议 | 生成 Contract / 自定义 DTO / 自研 | 生成 Contract | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Internal/GeneratedHandshakeAdapter | 否 |
| 能力查询 | 平台 Adapter / 反射探测 / hardcode | IPlatformCapabilityProvider Adapter | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Public port | 否 |

### 6. 控制流（实现顺序）

1. Session 在 ConnectionStarted 后调用 `Begin`，传入当前 generation 与认证后的上下文。
2. Handshake 发送生成 ClientHello，并并行向 capability provider 查询平台能力。
3. ConnectionEvent 由 Session/Owner Thread 调 `HandleFrame`；无效 schema/phase 立即稳定拒绝。
4. Capability completion 进入 attempt-tagged queue；旧 attempt 结果丢弃。
5. Reject classifier 按上游矩阵检查 Release/Schema/ABI/Role/Claims/Capability。
6. 只有全部通过才 `Poll` 出 Accepted；Session 随后才能准备 Gameplay Scope。
7. Cancel、Disconnect、QueueFull、Accepted 由优先级仲裁为一个终态。

### 7. 与 Runtime / 生成契约的接缝

- ClientHello/ServerHello/Reject/ErrorCode/Capability Manifest 来自生成 Contract。
- 认证票据字段、Release 支持范围与 Claim 语义不得在本仓补写。

### 8. 明确不实现

| 实现者最容易误做 | 正确归属/做法 |
| --- | --- |
| 握手成功前创建 Runtime Handle | Accepted 后由 Session 先激活 Scope，再创建 Handles |
| 把 HybridCLR 探测写进 Handshake 核心 | 注入 IPlatformCapabilityProvider |
| 对未知 ErrorCode 猜测兼容 | 按生成矩阵稳定拒绝并保留原码 |

### 9. 失败分类如何变成代码

| 分类 | 结果类型/码 | 谁通知 Session | 副作用约束 |
| --- | --- | --- | --- |
| 可重试 | Timeout/CapabilityTransient | HandshakeOutcome Retryable | 不创建 Scope/Handle |
| 可拒绝 | Release/Schema/ABI/Role/Claim Reject | StableRejected | 副作用为零 |
| 需 Resync | 不在握手内产生 | 无 | 无 |
| 可致命 | InvalidState/CriticalQueueFull | Fault outcome 通知 Session | 不能产生 Accepted |

### 10. 可观测性埋点

| 稳定事件名/生成 EventId | 产生位置 | 进入 Failure Bundle |
| --- | --- | --- |
| HandshakeBegin | Begin | 否 |
| HandshakeCapabilityResult | completion consumed | 否 |
| HandshakeRejected | classifier reject | 是 |
| HandshakeAccepted | terminal accepted | 是 |
| HandshakeLateAttempt | late completion/frame | 是 |

### 11. 测试面（先于实现）

| 测试 | 输入 | 期望 | 需要 Unity | LocalEmbedded/Remote Fixture |
| --- | --- | --- | --- | --- |
| `HandshakeStateMachineTests.Accepted_RequiresCapabilityAndValidServerHello` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `HandshakeStateMachineTests.Reject_HasZeroScopeAndWorldCalls` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `HandshakeAttemptGenerationTests.LateCapabilityCompletion_Dropped` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `GeneratedHandshakeFixtureTests.ValidAndInvalidVectors` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `CapabilityProviderTests.Unavailable_ReturnsStableCapabilityReject` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `HandshakeRejectTests.ReleaseSchemaAbiRoleClaims_MatchMatrix` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `HandshakeRaceTests.CancelDisconnectAccepted_PriorityIsDeterministic` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `HandshakeRaceTests.QueueFull_DoesNotAdvanceSentPhase` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |

### 12. Foundation 最小切片 vs 完整模块

| 阶段 | 必须实现的范围 |
| --- | --- |
| Foundation | LocalEmbedded 上完整握手、Fake Capability、生成 Fixture、拒绝矩阵 |
| Vertical Slice | Remote 通道认证衔接、真实平台 Capability Adapter |
| Production | N/N-1 仅在公共架构明确后启用；证书/设备能力矩阵 |

### 13. 任务拆分

1. `w3-handshake-contract-and-attempt` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
2. `w3-handshake-generated-contract-adapter` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
3. `w3-handshake-capability-and-rejects` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
4. `w7-hybridclr-capability-provider` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。

### 14. 开放阻塞

- `UPSTREAM-GENERATED-CONTRACT-API-MAP`：Handshake 消息/Reject/ErrorCode/Capability 类型；阻塞真实向量。

## 12.4 `replica`

### 1. 一句话职责

验证并暂存 FullSnapshot/Delta/Tombstone 的客户端 Replica 变更计划，追踪已提交 baseline/revision；删除后 Session 无法把权威更新安全映射进单一 Runtime 事务。

### 2. 唯一拥有的可变状态

| 状态 | 创建者 | 唯一修改者 | 快照/证据 | 销毁者 | 失败恢复 |
| --- | --- | --- | --- | --- | --- |
| Committed baseline/revision metadata | Reset/Authority commit | ObserveRuntimeOutcome only | ReplicaSnapshot | session close/reset | Abort 不推进 |
| Stage ledger | StageAuthority | Stage/Discard/Observe | stage handle/evidence | terminal outcome | stale stage rejected |
| Gap detector | Reset | Stage validation | snapshot/event | new generation | gap freezes normal apply |
| Tombstone evidence | Stage/Commit | Observe commit | snapshot | baseline replace | stable ordering |

### 3. 公共端口（精确 C# 签名）

```csharp
namespace Lumio.Client.Replica;

public interface IClientReplica
{
    ReplicaStageResult StageAuthority(
        in ReplicaStageRequest request,
        out ReplicaStageHandle stageHandle,
        out RuntimeContract.ReplicaApplyPlan applyPlan);
    ReplicaOutcomeStatus DiscardStage(
        ReplicaStageHandle stageHandle,
        ReplicaStageDiscardReason reason);
    ReplicaOutcomeStatus ObserveRuntimeOutcome(
        ReplicaStageHandle stageHandle,
        in RuntimeContract.AuthorityTransactionOutcome outcome,
        out ReplicaCommittedMetadata committedMetadata);
    ReplicaResetResult ResetForNewSession(in ReplicaResetRequest request);
    ReplicaSnapshot GetSnapshot();
}

public interface IReplicaMapper
{
    ReplicaMappingResult Map(
        in GeneratedContract.AuthorityReplicaUpdate update,
        in ReplicaMappingContext context,
        out RuntimeContract.ReplicaApplyPlan applyPlan);
}
```

**禁止出现在签名中的类型：**

- Runtime `Commit`/World 实现对象
- Prediction/Session 类型
- 自研 Replica Storage/ECS

### 4. 内部类型与文件树

| 目标文件 | 单一职责 |
| --- | --- |
| modules/replica/src/Public/IClientReplica.cs | Stage-only port |
| modules/replica/src/Public/IReplicaMapper.cs | Contract→Runtime plan mapper |
| modules/replica/src/Public/IClientReplicaFactory.cs | 构造入口 |
| modules/replica/src/Public/ReplicaStageHandle.cs | 不透明 stage |
| modules/replica/src/Public/ReplicaStageRequest.cs | generation/baseline/update |
| modules/replica/src/Public/ReplicaCommittedMetadata.cs | commit 后元数据 |
| modules/replica/src/Public/ReplicaSnapshot.cs | 证据 |
| modules/replica/src/Internal/ReplicaStageLedger.cs | stage 生命周期 |
| modules/replica/src/Internal/ReplicaMetadataState.cs | committed-only 元数据 |
| modules/replica/src/Internal/ReplicaGapDetector.cs | duplicate/gap/tombstone |
| modules/replica/src/Internal/GeneratedReplicaAdapter.cs | 生成契约验证 |
| modules/replica/src/Internal/RuntimeReplicaPlanAdapter.cs | 发布 Runtime plan |
| modules/replica/tests/Unit/ReplicaStageTests.cs | 无副作用 stage |
| modules/replica/tests/Unit/ReplicaMetadataTests.cs | commit/abort |
| modules/replica/tests/Contract/ReplicaGeneratedFixtureTests.cs | full/delta vectors |
| modules/replica/tests/Property/ReplicaSequenceProperties.cs | watermark 不回退 |
| modules/replica/tests/Fault/ReplicaOutcomeFaultTests.cs | stale/indeterminate |

### 5. 成熟依赖

| 能力 | 候选 | 选用 | 许可证/版本 | AOT/确定性 | 为何不自研 | Adapter 隔离点 | 第三方类型穿过公共接口 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 存储 | LumioGameRuntime Port / 自研 ECS / immutable mirror | LumioGameRuntime Port | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | 只输出 plan | 否 |
| 协议校验 | 生成 Contract / 手写 schema / JSON | 生成 Contract | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Internal/GeneratedReplicaAdapter | 否 |
| stage ledger | Dictionary+immutable handles / workflow library / 自研通用事务 | BCL 集合的最小专用 ledger | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Internal/ReplicaStageLedger | 否 |

### 6. 控制流（实现顺序）

1. Session 消息门验证 envelope/phase/generation 后调用 `StageAuthority`。
2. Replica 校验 baseline/revision/duplicate/gap/tombstone，并调用 mapper 生成 Runtime ReplicaApplyPlan。
3. Stage ledger 保存最小暂存证据；对外返回 handle+plan，Committed metadata 不变。
4. Session 再调用 Prediction `StageAuthority`，组合两侧 plan 后调用单一 Runtime Authority Transaction。
5. Runtime Aborted 时 Session 调 `DiscardStage`；所有元数据保持不变。
6. Runtime Committed 时 Session 调 `ObserveRuntimeOutcome`，验证 receipt 对应 stage 后推进 baseline/revision。
7. 只有随后 Session 才发送 Ack 与 Presentation Diff；Replica 自己不发送。

### 7. 与 Runtime / 生成契约的接缝

- AuthorityReplicaUpdate、revision/baseline/tombstone 语义来自生成 Contract。
- ReplicaApplyPlan 与 AuthorityTransactionOutcome 来自已发布 LumioGameRuntime Port。

### 8. 明确不实现

| 实现者最容易误做 | 正确归属/做法 |
| --- | --- |
| Replica 自己调用 Runtime Commit | 只 Stage/Observe，由 Session 合并单一事务 |
| 保存第二份 ECS/Replica Storage | Runtime 是状态存储；本模块只持元数据与 stage ledger |
| Gap 时尝试猜测补丁 | 返回 RequiresResync，Session 保持同连接请求 FullSnapshot |

### 9. 失败分类如何变成代码

| 分类 | 结果类型/码 | 谁通知 Session | 副作用约束 |
| --- | --- | --- | --- |
| 可重试 | MapperTransient（仅 Runtime Port 明示） | StageResult Retryable | stage 不可见 |
| 可拒绝 | Duplicate/InvalidUpdate | StableReject/Ignore | 不创建 plan |
| 需 Resync | Gap/BaselineMismatch | StageResult RequiresResync 通知 Session | 不推进 metadata |
| 可致命 | IndeterminateRuntimeOutcome/ledger corruption | Freeze + Fault Session | 不得 Ack/Diff |

### 10. 可观测性埋点

| 稳定事件名/生成 EventId | 产生位置 | 进入 Failure Bundle |
| --- | --- | --- |
| ReplicaStageCreated | Stage success | 否 |
| ReplicaGapDetected | gap detector | 是 |
| ReplicaStageDiscarded | Abort/second-stage failure | 是 |
| ReplicaMetadataCommitted | Observe committed | 是 |
| ReplicaIndeterminate | receipt mismatch | 是 |

### 11. 测试面（先于实现）

| 测试 | 输入 | 期望 | 需要 Unity | LocalEmbedded/Remote Fixture |
| --- | --- | --- | --- | --- |
| `ReplicaStageTests.Stage_HasNoVisibleMetadataMutation` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `ReplicaStageTests.Gap_ReturnsRequiresResyncAndNeverCallsMapper` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `ReplicaMetadataTests.CommittedAdvances_AbortedDoesNot` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `ReplicaMetadataTests.IndeterminateFreezesAndRetainsEvidence` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `ReplicaGapDetectorTests.DuplicateGapTombstone_MatchFixture` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `ReplicaGeneratedFixtureTests.FullSnapshotDeltaInvalidVectors` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `ReplicaSequenceProperties.CommittedWatermarkNeverRegresses` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `ReplicaOutcomeFaultTests.StaleStage_CannotAdvanceMetadata` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |

### 12. Foundation 最小切片 vs 完整模块

| 阶段 | 必须实现的范围 |
| --- | --- |
| Foundation | FullSnapshot/Delta Stage、gap、baseline metadata、Fake Runtime plans |
| Vertical Slice | 与真实 Game Mapper/Config/Prediction correction 组合 |
| Production | 大快照预算、分配/吞吐 benchmark、损坏证据 |

### 13. 任务拆分

1. `w4-replica-stage-contract` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
2. `w4-replica-baseline-gap-metadata` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
3. `w4-replica-fullsnapshot-fixtures` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。

### 14. 开放阻塞

- `UPSTREAM-RUNTIME-CONTRACT-API-MAP`：ReplicaApplyPlan/AuthorityTransactionOutcome 真实类型；阻塞 production adapter。
- `UPSTREAM-GENERATED-CONTRACT-API-MAP`：Authority update/revision 类型；阻塞 Fixture。

## 12.5 `prediction`

### 1. 一句话职责

拥有客户端命令序号、PredictionKey、已提交预测历史和 correction/replay 暂存计划；删除后客户端无法确定性预测、确认与原子回滚重放。

### 2. 唯一拥有的可变状态

| 状态 | 创建者 | 唯一修改者 | 快照/证据 | 销毁者 | 失败恢复 |
| --- | --- | --- | --- | --- | --- |
| ClientCommandSeq allocator | Prediction factory/reset | ObserveLocalPredictionOutcome committed | Snapshot/history | new generation | aborted candidate 不消耗 |
| PredictionKey allocator | 同上 | committed local outcome | accepted command | new generation | 与 CommandSeq 同时提交 |
| Committed prediction history | local commit | local commit/authority commit prune | Snapshot | reset | stage 不可见 |
| Candidate/authority stage ledger | AcceptCandidate/StageAuthority | Discard/Observe | stage handle | terminal outcome | indeterminate freezes |
| window policy | factory/reset | commit/prune | depth/high-watermark | reset | full 返回明确背压 |

### 3. 公共端口（精确 C# 签名）

```csharp
namespace Lumio.Client.Prediction;

public interface IClientPrediction
{
    PredictionCandidateResult AcceptCandidate(
        in GeneratedContract.CandidateGameplayCommand candidate,
        in PredictionCandidateContext context,
        out PredictionCandidateStage stage,
        out RuntimeContract.LocalPredictionPlan localPlan);
    PredictionLocalOutcomeResult DiscardCandidateStage(
        PredictionCandidateStage stage,
        PredictionStageDiscardReason reason);
    PredictionLocalOutcomeResult ObserveLocalPredictionOutcome(
        PredictionCandidateStage stage,
        in RuntimeContract.LocalPredictionOutcome outcome,
        out AcceptedPredictionCommand acceptedCommand);
    PredictionAuthorityResult StageAuthority(
        in GeneratedContract.AuthorityPredictionUpdate update,
        in PredictionAuthorityContext context,
        out PredictionAuthorityStage stage,
        out RuntimeContract.PredictionReconcilePlan reconcilePlan);
    PredictionAuthorityOutcomeResult DiscardAuthorityStage(
        PredictionAuthorityStage stage,
        PredictionStageDiscardReason reason);
    PredictionAuthorityOutcomeResult ObserveRuntimeOutcome(
        PredictionAuthorityStage stage,
        in RuntimeContract.AuthorityTransactionOutcome outcome);
    PredictionResetResult ResetForNewSession(in PredictionResetRequest request);
    PredictionSnapshot GetSnapshot();
}
```

**禁止出现在签名中的类型：**

- Replica/Session 类型
- Runtime Commit/World 实现对象
- 自研 ECS/PredictionFrame/Rollback 引擎

### 4. 内部类型与文件树

| 目标文件 | 单一职责 |
| --- | --- |
| modules/prediction/src/Public/IClientPrediction.cs | stable stage port |
| modules/prediction/src/Public/IClientPredictionFactory.cs | factory |
| modules/prediction/src/Public/ClientCommandSeq.cs | prediction-owned seq |
| modules/prediction/src/Public/PredictionKey.cs | opaque key |
| modules/prediction/src/Public/PredictionCandidateStage.cs | local stage |
| modules/prediction/src/Public/PredictionAuthorityStage.cs | authority stage |
| modules/prediction/src/Public/AcceptedPredictionCommand.cs | committed local command |
| modules/prediction/src/Public/PredictionSnapshot.cs | evidence |
| modules/prediction/src/Internal/PredictionSequenceAllocator.cs | commit-only allocators |
| modules/prediction/src/Internal/PredictionHistory.cs | bounded committed history |
| modules/prediction/src/Internal/PredictionStageLedger.cs | stage lifecycle |
| modules/prediction/src/Internal/PredictionWindowPolicy.cs | capacity/backpressure |
| modules/prediction/src/Internal/GeneratedPredictionAdapter.cs | contract validation |
| modules/prediction/src/Internal/RuntimePredictionPlanAdapter.cs | Runtime plans |
| modules/prediction/tests/Unit/PredictionCandidateTests.cs | local commit semantics |
| modules/prediction/tests/Unit/PredictionAuthorityStageTests.cs | authority correction |
| modules/prediction/tests/Property/PredictionSequenceProperties.cs | seq monotonic |
| modules/prediction/tests/Fault/PredictionOutcomeFaultTests.cs | indeterminate |

### 5. 成熟依赖

| 能力 | 候选 | 选用 | 许可证/版本 | AOT/确定性 | 为何不自研 | Adapter 隔离点 | 第三方类型穿过公共接口 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 预测执行 | LumioGameRuntime Port / 自研 rollback / Unity ECS | LumioGameRuntime Port | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | 只输出 plan | 否 |
| 历史 | ImmutableArray/Queue / event sourcing library / 自研框架 | BCL/Immutable 的有界专用 history | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Internal/PredictionHistory | 否 |
| 协议 | 生成 Contract / hand DTO / JSON | 生成 Contract | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Internal/GeneratedPredictionAdapter | 否 |

### 6. 控制流（实现顺序）

1. Session 从 Input 得到无命令序号 Candidate，调用 `AcceptCandidate`。
2. Prediction 检查 window/generation，生成 local stage 和 Runtime LocalPredictionPlan；尚不分配 CommandSeq/Key。
3. Session 调用 Runtime 单一 Local Prediction Transaction。
4. Aborted 时 Discard stage，不消耗序号、不入历史。
5. Committed 时 `ObserveLocalPredictionOutcome` 原子分配 ClientCommandSeq+PredictionKey，并把 accepted command 放入有界历史；Session 才发送网络命令。
6. 权威 Confirmation/Correction 到达时，Prediction `StageAuthority` 产出 reconcile plan，不改历史。
7. Session 把 reconcile plan 与 Replica plan 合入同一 Authority Transaction。
8. Commit 后 `ObserveRuntimeOutcome` 才 prune/advance；Abort 保持历史。

### 7. 与 Runtime / 生成契约的接缝

- Candidate/AuthorityPredictionUpdate 来自生成 Game Contract。
- LocalPredictionPlan、PredictionReconcilePlan、Local/Authority Outcome 来自 LumioGameRuntime Port。

### 8. 明确不实现

| 实现者最容易误做 | 正确归属/做法 |
| --- | --- |
| Input 或 Session 分配 ClientCommandSeq | 只有 Prediction 在本地 Runtime committed 后分配 |
| 实现自己的 rollback frame/ECS | 调用 Runtime 发布的 prediction/reconcile transaction port |
| authority correction 单独 Commit | 与 Replica update 组成 Session 单一权威事务 |

### 9. 失败分类如何变成代码

| 分类 | 结果类型/码 | 谁通知 Session | 副作用约束 |
| --- | --- | --- | --- |
| 可重试 | PredictionWindowBusy | CandidateResult Retryable/QueueFull | 不消耗序号 |
| 可拒绝 | InvalidCandidate/UnknownConfirmation | StableReject | 不改历史 |
| 需 Resync | HistoryUnavailable/CorrectionBaseMissing | AuthorityResult RequiresResync | 不 prune |
| 可致命 | IndeterminateRuntimeOutcome/history corruption | Freeze + Session Fault | 不发 Ack/命令 |

### 10. 可观测性埋点

| 稳定事件名/生成 EventId | 产生位置 | 进入 Failure Bundle |
| --- | --- | --- |
| PredictionCandidateStaged | AcceptCandidate | 否 |
| PredictionLocalCommitted | local observe committed | 是 |
| PredictionWindowFull | capacity reject | 是 |
| PredictionCorrectionStaged | authority stage | 否 |
| PredictionHistoryPruned | authority committed | 是 |
| PredictionIndeterminate | receipt mismatch | 是 |

### 11. 测试面（先于实现）

| 测试 | 输入 | 期望 | 需要 Unity | LocalEmbedded/Remote Fixture |
| --- | --- | --- | --- | --- |
| `PredictionCandidateTests.RejectedCandidate_DoesNotConsumeClientCommandSeq` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `PredictionCandidateTests.LocalAborted_DoesNotConsumeOrEnterHistory` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `PredictionCandidateTests.LocalCommitted_AssignsSeqAndKeyOnce` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `PredictionHistoryTests.ConfirmationPrunesOnlyAfterAuthorityCommit` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `PredictionAuthorityStageTests.Stage_HasNoHistoryMutation` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `PredictionAuthorityStageTests.CorrectionPlan_ComposesWithReplicaPlan` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `PredictionGeneratedFixtureTests.ConfirmationCorrectionVectors` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `PredictionSequenceProperties.AcceptedSequencesStrictlyIncrease` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `PredictionOutcomeFaultTests.IndeterminateFreezesHistory` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |

### 12. Foundation 最小切片 vs 完整模块

| 阶段 | 必须实现的范围 |
| --- | --- |
| Foundation | 序号/Key、bounded history、Stage/Observe、Fake Runtime |
| Vertical Slice | 真实 command mapper、confirmation/correction、原子 rollback/replay |
| Production | 长 RTT/window 压测、内存预算、replay 性能证据 |

### 13. 任务拆分

1. `w4-prediction-sequence-history` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
2. `w4-prediction-authority-stage` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
3. `w4-prediction-window-faults` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。

### 14. 开放阻塞

- `UPSTREAM-RUNTIME-CONTRACT-API-MAP`：LocalPredictionPlan/ReconcilePlan/outcome 类型；阻塞 production adapter。
- `UPSTREAM-GENERATED-CONTRACT-API-MAP`：Candidate/Confirmation/Correction 类型；阻塞真实 Fixture。

## 12.6 `input`

### 1. 一句话职责

把平台输入或 Bot 测试命令转成有界、带 `InputSampleSeq` 的平台无关 Sample，再经 Game Mapper 产出 Candidate；删除后输入没有确定性入口。

### 2. 唯一拥有的可变状态

| 状态 | 创建者 | 唯一修改者 | 快照/证据 | 销毁者 | 失败恢复 |
| --- | --- | --- | --- | --- | --- |
| InputSampleSeq allocator | Ingress Factory | Ingress accepted path | Snapshot/receipt | Session generation reset | QueueFull 不推进 |
| Sample queue | Input pipeline | platform producer/Owner drain | depth/high-watermark | generation end | 不覆盖旧样本 |
| Buffer policy | Session | Owner Thread | Snapshot | new generation | Resync policy 带 generation |
| Mapper fault evidence | CommandSource | Owner Thread | Event | drain completes | 单样本失败按稳定策略拒绝 |

### 3. 公共端口（精确 C# 签名）

```csharp
namespace Lumio.Client.Input;

public interface IInputSampleIngress
{
    InputEnqueueStatus TryEnqueue(
        in RawInputSample sample,
        out InputEnqueueReceipt receipt);
    InputPipelineSnapshot GetSnapshot();
}

public interface IInputCommandSource
{
    int DrainCandidates(
        Span<GeneratedContract.CandidateGameplayCommand> destination,
        in InputDrainContext context);
    InputBufferControlResult SetBufferPolicy(
        InputBufferPolicy policy,
        ulong sessionGeneration);
    InputPipelineSnapshot GetSnapshot();
}

public interface IGameInputMapper
{
    InputMappingResult Map(
        in SequencedInputSample sample,
        in InputMappingContext context,
        out GeneratedContract.CandidateGameplayCommand command);
}
```

**禁止出现在签名中的类型：**

- Unity Input System/UnityEngine 类型
- `ClientCommandSeq`/`PredictionKey` 分配器
- 具体 Gameplay 实现类型

### 4. 内部类型与文件树

| 目标文件 | 单一职责 |
| --- | --- |
| modules/input/src/Public/IInputSampleIngress.cs | Sample producer port |
| modules/input/src/Public/IInputCommandSource.cs | Owner drain port |
| modules/input/src/Public/IGameInputMapper.cs | 产品 mapper port |
| modules/input/src/Public/InputSampleSeq.cs | 仅本模块分配的序号 |
| modules/input/src/Public/RawInputSample.cs | 平台无关 sample |
| modules/input/src/Public/SequencedInputSample.cs | accepted sample |
| modules/input/src/Public/InputBufferPolicy.cs | Active/Resync/Closed 策略 |
| modules/input/src/Internal/InputSampleQueue.cs | 有界 queue |
| modules/input/src/Internal/InputSampleSequenceAllocator.cs | accepted-only allocator |
| modules/input/src/Internal/InputCommandSource.cs | 按序 mapper |
| modules/input/src/Internal/InputBufferPolicyState.cs | generation-scoped policy |
| modules/input/tests/Unit/InputSampleIngressTests.cs | 序号/满载 |
| modules/input/tests/Property/InputSequenceProperties.cs | 单调不变量 |
| modules/input/tests/Contract/InputMappingFixtureTests.cs | 生成向量 |
| modules/input/tests/Fault/InputQueueFaultTests.cs | mapper fault |

### 5. 成熟依赖

| 能力 | 候选 | 选用 | 许可证/版本 | AOT/确定性 | 为何不自研 | Adapter 隔离点 | 第三方类型穿过公共接口 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 队列 | Channels / ConcurrentQueue / 自研 | Channels | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Internal/InputSampleQueue | 否 |
| 平台输入 | Unity Input System / legacy / 自研 | 由 unity-adapter 使用 Input System | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | 不进入核心 | 否 |
| 映射 | 产品 Game Mapper / 核心硬编码 / JSON DSL 自研 | 注入 Game Mapper | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Public/IGameInputMapper | 否 |

### 6. 控制流（实现顺序）

1. Unity/Bot 构造 `RawInputSample` 调 `TryEnqueue`。
2. 队列有容量时才分配 `InputSampleSeq` 并返回 receipt；满载不消耗序号。
3. Session Owner Tick 调 `DrainCandidates`，按 SampleSeq 稳定顺序取样。
4. 每个样本调用注入的 `IGameInputMapper`，产出生成 Contract Candidate。
5. Candidate 尚无 ClientCommandSeq；Session 把它交 Prediction。
6. Resync 时 Session 设置 generation-scoped buffer policy；旧 generation 策略与样本被丢弃。

### 7. 与 Runtime / 生成契约的接缝

- CandidateGameplayCommand 与映射上下文契约来自 Game Contract Artifact。
- Input 不知道 ECS/Runtime/Predict history；只交付 Candidate。

### 8. 明确不实现

| 实现者最容易误做 | 正确归属/做法 |
| --- | --- |
| 在 Input 分配 ClientCommandSeq | 只分配 InputSampleSeq；Prediction 在本地事务成功后分配命令序号 |
| 核心引用 Unity Input System | Unity Adapter 转换为 RawInputSample |
| QueueFull 丢最旧输入 | 显式返回 QueueFull，不覆盖已接受样本 |

### 9. 失败分类如何变成代码

| 分类 | 结果类型/码 | 谁通知 Session | 副作用约束 |
| --- | --- | --- | --- |
| 可重试 | TransientMapperUnavailable（仅产品明确定义时） | 返回 MappingResult | 不得分配命令序号 |
| 可拒绝 | InvalidSample/MappingRejected | 跳过当前 Sample 并发事件 | 不产生 Candidate |
| 需 Resync | BufferPolicy=Resync | 通知 Session snapshot | 不自行发网络请求 |
| 可致命 | CriticalSampleQueueContractViolation | 通知 Session Fault | 不覆盖/重排 |

### 10. 可观测性埋点

| 稳定事件名/生成 EventId | 产生位置 | 进入 Failure Bundle |
| --- | --- | --- |
| InputSampleAccepted | TryEnqueue success | 否 |
| InputSampleQueueFull | TryEnqueue full | 是 |
| InputSampleMapped | Map success | 否 |
| InputSampleRejected | Map reject/throw | 是 |
| InputLateGeneration | old generation sample | 是 |

### 11. 测试面（先于实现）

| 测试 | 输入 | 期望 | 需要 Unity | LocalEmbedded/Remote Fixture |
| --- | --- | --- | --- | --- |
| `InputSampleIngressTests.AcceptedSamples_GetStrictlyIncreasingSeq` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `InputSampleIngressTests.QueueFull_DoesNotAdvanceSequence` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `InputCommandSourceTests.MapperInvokedInSampleOrder` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `InputCommandSourceTests.Candidate_DoesNotAllocateClientCommandSeq` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `InputBufferPolicyTests.ResyncPolicy_IsGenerationScoped` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `InputSequenceProperties.AcceptedSequenceNeverRepeats` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `InputMappingFixtureTests.GeneratedVectors_AreDeterministic` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `InputQueueFaultTests.MapperThrows_CurrentSampleRejectedByPolicy` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |

### 12. Foundation 最小切片 vs 完整模块

| 阶段 | 必须实现的范围 |
| --- | --- |
| Foundation | 有界 Sample Queue、InputSampleSeq、无玩法测试命令入口、Fake Mapper |
| Vertical Slice | 真实 Game Mapper + Unity Input System Adapter |
| Production | 设备热插拔/重绑定/采样预算与平台矩阵 |

### 13. 任务拆分

1. `w2-input-sample-ingress` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
2. `w2-input-deterministic-mapping` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
3. `w7-unity-input-system-adapter` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。

### 14. 开放阻塞

- `UPSTREAM-GENERATED-CONTRACT-API-MAP`：CandidateGameplayCommand 真实类型；阻塞 mapper adapter。

## 12.7 `persistence`

### 1. 一句话职责

提供已验证 Session Artifact 与已提交 Checkpoint 的窄异步存取端口；删除后客户端无法安全缓存 Config/Scope Artifact 或恢复已提交本地证据。

### 2. 唯一拥有的可变状态

| 状态 | 创建者 | 唯一修改者 | 快照/证据 | 销毁者 | 失败恢复 |
| --- | --- | --- | --- | --- | --- |
| per-key operation gate | Factory | async adapter | snapshot/inflight count | store close | 同 key 串行 |
| artifact index/cache metadata | File adapter | successful atomic replace | manifest snapshot | cleanup | 损坏项隔离 |
| checkpoint generation metadata | Store | committed writes | read result | retention cleanup | late result tagged |
| failure/corruption evidence | Adapter | validation failures | event/bundle | bounded retention | 不返回未验证 bytes |

### 3. 公共端口（精确 C# 签名）

```csharp
namespace Lumio.Client.Persistence;

public interface IVerifiedSessionArtifactSource
{
    ValueTask<VerifiedArtifactReadResult> ReadAsync(
        in VerifiedArtifactReadRequest request,
        CancellationToken cancellationToken);
}

public interface IClientCheckpointStore
{
    ValueTask<CheckpointReadResult> ReadLatestAsync(
        in CheckpointReadRequest request,
        CancellationToken cancellationToken);
    ValueTask<CheckpointWriteResult> WriteAsync(
        in CheckpointWriteRequest request,
        CancellationToken cancellationToken);
    PersistenceSnapshot GetSnapshot();
}
```

**禁止出现在签名中的类型：**

- 文件路径、`Stream`、SQLite Connection
- 未验证的 raw Artifact 跨端口
- Session/Runtime World 实现对象

### 4. 内部类型与文件树

| 目标文件 | 单一职责 |
| --- | --- |
| modules/persistence/src/Public/IVerifiedSessionArtifactSource.cs | verified artifact read |
| modules/persistence/src/Public/IClientCheckpointStore.cs | committed checkpoint store |
| modules/persistence/src/Public/IClientPersistenceFactory.cs | factory |
| modules/persistence/src/Public/VerifiedArtifactReadRequest.cs | release/hash/generation query |
| modules/persistence/src/Public/VerifiedArtifactReadResult.cs | verified result |
| modules/persistence/src/Public/CheckpointWriteRequest.cs | committed checkpoint value |
| modules/persistence/src/Public/PersistenceSnapshot.cs | health evidence |
| modules/persistence/src/Internal/Concurrency/PerKeyOperationGate.cs | same-key serialization |
| modules/persistence/src/Internal/FileSystem/FileVerifiedSessionArtifactSource.cs | file adapter |
| modules/persistence/src/Internal/FileSystem/FileClientCheckpointStore.cs | checkpoint adapter |
| modules/persistence/src/Internal/FileSystem/AtomicFileReplacer.cs | temp→flush→atomic replace |
| modules/persistence/src/Internal/Validation/ArtifactCryptographicVerifier.cs | BCL hash/signature |
| modules/persistence/src/Internal/Serialization/ArtifactManifestJsonContext.cs | STJ source generation |
| modules/persistence/tests/Unit/VerifiedSessionArtifactSourceTests.cs | verified-only |
| modules/persistence/tests/Fault/PersistenceCorruptionTests.cs | bitflip/truncate |
| modules/persistence/tests/Property/ArtifactRoundTripProperties.cs | roundtrip |

### 5. 成熟依赖

| 能力 | 候选 | 选用 | 许可证/版本 | AOT/确定性 | 为何不自研 | Adapter 隔离点 | 第三方类型穿过公共接口 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| I/O | BCL FileStream / SQLite / 自研 DB | BCL atomic file adapter first | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Internal/FileSystem | 否 |
| JSON | STJ source gen / Newtonsoft / 自研 | STJ source gen | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Internal/Serialization | 否 |
| 密码学 | BCL / libsodium wrapper / 自研 | BCL | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Internal/Validation | 否 |

### 6. 控制流（实现顺序）

1. Session/Handshake 以 release/hash/generation 请求 Artifact；不传文件路径。
2. Adapter 读取 manifest 和 bytes，使用 BCL hash/signature 与生成 Contract 校验。
3. 只有完全验证成功才返回 `VerifiedArtifact`；损坏/版本错返回稳定结果。
4. 异步完成携带发起 generation，Session Owner Thread 丢弃迟到结果。
5. Checkpoint 只接受 Runtime 已提交事务产出的不可变值。
6. 写入执行 temp file→flush→atomic replace；成功结果只在替换后返回。
7. 同 key 操作串行，不同 key 可并行；关闭时取消并输出未完成证据。

### 7. 与 Runtime / 生成契约的接缝

- Artifact manifest/hash/signature/release 与 Checkpoint 格式来自生成 Contract/Runtime Port。
- 本模块不创建业务 checkpoint，也不解释 Config 内容。

### 8. 明确不实现

| 实现者最容易误做 | 正确归属/做法 |
| --- | --- |
| 把路径/Stream 暴露给 Session | 只暴露 request/result value |
| 先返回 bytes 再让 Session 验证 | Adapter 内完成全部验证后才返回 |
| 保存未提交预测/Replica 状态 | 只写 Runtime committed checkpoint/artifact |

### 9. 失败分类如何变成代码

| 分类 | 结果类型/码 | 谁通知 Session | 副作用约束 |
| --- | --- | --- | --- |
| 可重试 | IoTransient/Busy | 异步 Retryable result | 不改旧文件 |
| 可拒绝 | Missing/WrongRelease/HashMismatch | StableReject | 不返回 bytes |
| 需 Resync | Checkpoint incompatible | 通知 Session 走完整同步 | 不部分加载 |
| 可致命 | AtomicityViolation/VerifierFailure | Fault + evidence | 保留旧 committed artifact |

### 10. 可观测性埋点

| 稳定事件名/生成 EventId | 产生位置 | 进入 Failure Bundle |
| --- | --- | --- |
| ArtifactReadStarted | ReadAsync | 否 |
| ArtifactVerified | validation success | 否 |
| ArtifactRejected | validation failure | 是 |
| CheckpointWriteCommitted | atomic replace success | 是 |
| PersistenceLateGeneration | late completion | 是 |

### 11. 测试面（先于实现）

| 测试 | 输入 | 期望 | 需要 Unity | LocalEmbedded/Remote Fixture |
| --- | --- | --- | --- | --- |
| `VerifiedSessionArtifactSourceTests.Read_ReturnsOnlyVerifiedArtifact` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `VerifiedSessionArtifactSourceTests.LateGeneration_ResultCarriesOriginalGeneration` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `AtomicFileReplacerTests.CrashBeforeReplace_OldArtifactRemains` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `AtomicFileReplacerTests.Success_ReturnsAfterReplaceAndFlush` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `PerKeyOperationGateTests.SameKeySerialDifferentKeysParallel` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `ArtifactContractFixtureTests.ValidInvalidManifestVectors` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `ArtifactRoundTripProperties.CommittedBlobVerifiesOrIsRejected` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `PersistenceCorruptionTests.BitFlipTruncateWrongRelease_AreRejected` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |

### 12. Foundation 最小切片 vs 完整模块

| 阶段 | 必须实现的范围 |
| --- | --- |
| Foundation | 只定义窄端口和 in-memory fake；不阻塞 Foundation exit |
| Vertical Slice | BCL filesystem、验证、Config artifact、checkpoint save/load |
| Production | retention/quota/跨平台 crash evidence；SQLite 仅在 spike 支持时 |

### 13. 任务拆分

1. `w1-persistence-contract-surface` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
2. `w7-persistence-artifact-ports` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
3. `w7-persistence-filesystem-adapter` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
4. `w7-persistence-corruption-recovery` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。

### 14. 开放阻塞

- `UPSTREAM-GENERATED-CONTRACT-API-MAP`：Artifact/manifest/checkpoint 真实类型；阻塞 file adapter。

## 12.8 `observability`

### 1. 一句话职责

提供非阻塞、结构化、可限流的客户端事件管线及 Sink Adapter；删除后所有模块失去统一 Event/Metric/Trace 与 Failure Evidence 出口。

### 2. 唯一拥有的可变状态

| 状态 | 创建者 | 唯一修改者 | 快照/证据 | 销毁者 | 失败恢复 |
| --- | --- | --- | --- | --- | --- |
| ProducerSequence | Pipeline Factory | Writer 原子递增 | PipelineSnapshot/批次证据 | Pipeline Close | 重启新 pipeline generation；不伪造连续性 |
| 有界事件队列 | DefaultClientEventPipeline | Writer/Dispatcher | 深度、drop、high-watermark | Dispatcher Drain 后释放 | Sink 失败保留可重试批次或按生成 schema 分类降级 |
| Sink 健康/重试状态 | Dispatcher | Dispatcher Worker | Failure Evidence | Pipeline Close | 外部 Sink 故障不阻塞 Owner Tick |
| 内存证据环 | InMemory Sink | Dispatcher | 稳定顺序快照 | Sink Dispose | 满载只按 schema 允许策略处理 |

### 3. 公共端口（精确 C# 签名）

```csharp
namespace Lumio.Client.Observability;

public interface IClientEventWriter
{
    ClientEventWriteResult TryWrite(in GeneratedContract.ClientEventRecord record);
    ClientEventPipelineSnapshot GetSnapshot();
}

public interface IClientEventSink
{
    ValueTask<ClientEventSinkResult> WriteBatchAsync(
        ReadOnlyMemory<GeneratedContract.ClientEventRecord> records,
        CancellationToken cancellationToken);
}

public interface IClientEventPipelineFactory
{
    ClientEventPipelineCreateResult Create(
        in ClientEventPipelineOptions options,
        IClientEventSink sink,
        out IClientEventWriter? writer);
}

public interface IClientEventMemorySnapshotSource
{
    ClientEventMemorySnapshot Capture();
}
```

**禁止出现在签名中的类型：**

- `ILogger`、Serilog、OpenTelemetry 类型
- `Channel<T>`/`Activity`
- UnityEngine、Socket、HybridCLR 类型

### 4. 内部类型与文件树

| 目标文件 | 单一职责 |
| --- | --- |
| modules/observability/src/Public/IClientEventWriter.cs | 稳定写入端口 |
| modules/observability/src/Public/IClientEventSink.cs | 稳定 Sink 端口 |
| modules/observability/src/Public/IClientEventPipelineFactory.cs | 显式工厂 |
| modules/observability/src/Public/IClientEventMemorySnapshotSource.cs | Failure Bundle 快照端口 |
| modules/observability/src/Public/ClientEventPipelineOptions.cs | 容量、批次、超时等不可变选项 |
| modules/observability/src/Public/ClientEventPipelineSnapshot.cs | 健康证据值 |
| modules/observability/src/Public/InMemoryClientEventSink.cs | Foundation 内存 Sink 的公开构造入口 |
| modules/observability/src/Public/DefaultClientEventPipelineFactory.cs | 默认实现构造入口 |
| modules/observability/src/Internal/Pipeline/BoundedEventWriter.cs | 非阻塞 TryWrite |
| modules/observability/src/Internal/Pipeline/EventDispatcherWorker.cs | 批次 drain 与 Sink 生命周期 |
| modules/observability/src/Internal/Pipeline/EventDropPolicy.cs | 只按生成 schema 分类丢弃 |
| modules/observability/src/Internal/Pipeline/FailureEvidenceEncoder.cs | 稳定 Failure Evidence 编码 |
| modules/observability/src/Internal/Adapters/Serilog/SerilogClientEventSink.cs | Production 文件日志 Adapter |
| modules/observability/src/Internal/Adapters/OpenTelemetry/OpenTelemetryClientEventSink.cs | Metrics/Trace Adapter |
| modules/observability/tests/Unit/ClientEventWriterTests.cs | 写入语义 |
| modules/observability/tests/Property/EventDropPolicyProperties.cs | drop 不变量 |
| modules/observability/tests/Fault/ObservabilityFaultTests.cs | Sink/Close 竞态 |
| modules/observability/tests/Contract/FailureEvidenceEncoderFixtureTests.cs | 生成向量 |

### 5. 成熟依赖

| 能力 | 候选 | 选用 | 许可证/版本 | AOT/确定性 | 为何不自研 | Adapter 隔离点 | 第三方类型穿过公共接口 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 队列 | Channels / TPL Dataflow / 自研 | Channels | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Internal/Pipeline | 否 |
| 日志 | Serilog / NLog / 自研 | Serilog Adapter | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Internal/Adapters/Serilog | 否 |
| Trace | OpenTelemetry / vendor SDK / 自研 | OpenTelemetry Adapter | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Internal/Adapters/OpenTelemetry | 否 |

### 6. 控制流（实现顺序）

1. 业务模块构造 `GeneratedContract.ClientEventRecord` 并调用 `IClientEventWriter.TryWrite`。
2. Writer 在调用线程分配单调 ProducerSequence，执行 schema drop-class 校验并尝试写入有界队列；绝不等待。
3. QueueFull 对 Critical/Durable 返回显式失败；对 schema 允许的 Droppable 类增加 drop 证据。
4. Dispatcher Worker 按稳定入队顺序组成不可变批次，调用 Sink。
5. Sink 成功后推进最后输出序号；失败时记录 sink fault 并按有限重试/降级矩阵处理。
6. Failure Bundle 从内存快照端口抓取最近事件、队列指标和缺失 provider 说明，不阻塞 Session Tick。

### 7. 与 Runtime / 生成契约的接缝

- EventId、字段 Schema、drop class 与 Failure Bundle 记录格式来自生成 Contract Artifact。
- 模块不判断“业务是否成功”；只记录调用方提供的业务事实与自己的队列/Sink 事实。

### 8. 明确不实现

| 实现者最容易误做 | 正确归属/做法 |
| --- | --- |
| 直接在核心使用 Serilog `ILogger` | 只依赖 `IClientEventWriter`，Serilog 留在 Sink Adapter |
| QueueFull 时同步写磁盘 | 返回显式结果，由 Session 依据优先级决定故障 |
| 将日志时间/线程顺序纳入权威 Hash | 确定性业务证据使用 Tick/Sequence；墙钟仅诊断 |

### 9. 失败分类如何变成代码

| 分类 | 结果类型/码 | 谁通知 Session | 副作用约束 |
| --- | --- | --- | --- |
| 可重试 | SinkTransientFailure | Dispatcher 有限重试；不通知业务成功/失败 | 不回滚业务 |
| 可拒绝 | InvalidEventClass | 调用者收到拒绝 | 不入队 |
| 需 Resync | 无业务 Resync 分类 | 不由 observability 发起 | 零业务副作用 |
| 可致命 | CriticalQueueFull / EvidenceCorrupt | 通知 Session/Host Fault | 不得静默丢失 Critical 事件 |

### 10. 可观测性埋点

| 稳定事件名/生成 EventId | 产生位置 | 进入 Failure Bundle |
| --- | --- | --- |
| ClientEventQueued | TryWrite accepted | 否 |
| ClientEventQueueFull | TryWrite full | 是 |
| ClientEventDropped | schema-allowed drop | 是 |
| ClientEventSinkFault | Sink exception/result failure | 是 |
| FailureBundlePartial | provider 超预算/缺失 | Bundle 自身 |

### 11. 测试面（先于实现）

| 测试 | 输入 | 期望 | 需要 Unity | LocalEmbedded/Remote Fixture |
| --- | --- | --- | --- | --- |
| `ClientEventWriterTests.Accepted_AssignsMonotonicProducerSequence` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `BoundedEventDispatcherTests.CriticalQueueFull_ReturnsWithoutBlocking` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `BoundedEventDispatcherTests.DroppableQueueFull_DropsOnlySchemaAllowedClass` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `InMemoryEventSinkTests.BatchOrder_IsStable` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `EventDropPolicyProperties.NeverDropsDurableClass` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `FailureEvidenceEncoderFixtureTests.ValidInvalidGeneratedFixtures` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `ObservabilityFaultTests.SinkThrows_BatchRetainedAndExceptionContained` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `ObservabilityFaultTests.CloseWriteRace_NoSilentLoss` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |

### 12. Foundation 最小切片 vs 完整模块

| 阶段 | 必须实现的范围 |
| --- | --- |
| Foundation | 公共事件端口、内存 Sink、有界队列、结构化事件、Critical QueueFull |
| Vertical Slice | Serilog Sink、基础 OTel Sink、Failure Bundle 导出 |
| Production | Exporter 白名单、磁盘配额、IL2CPP 验证、采样/脱敏策略 |

### 13. 任务拆分

1. `w1-observability-contract` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
2. `w1-observability-memory-sink` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
3. `w1-observability-bounded-dispatch` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
4. `w7-observability-serilog-sink` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
5. `w7-observability-opentelemetry-sink` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
6. `w7-observability-failure-bundle-export` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。

### 14. 开放阻塞

- `UPSTREAM-GENERATED-CONTRACT-API-MAP`：真实 EventId/Event Record/Failure Bundle 类型名；阻塞 contract adapter。
- `SPIKE-OTEL-IL2CPP`：2 个工作日验证 exporter 与 IL2CPP；阻塞 Production OTel 默认启用。

## 12.9 `unity-adapter`

### 1. 一句话职责

把 Unity Main Thread、Input System、生命周期和表现对象适配到 Session/Input/Presentation 公共端口；删除后核心仍可 Headless，但不能安全接入 Unity。

### 2. 唯一拥有的可变状态

| 状态 | 创建者 | 唯一修改者 | 快照/证据 | 销毁者 | 失败恢复 |
| --- | --- | --- | --- | --- | --- |
| Unity host generation | Start | Main Thread | snapshot | Stop/Destroy | late frame ignored |
| Input subscriptions | Start/binding resolve | Main Thread | binding snapshot | Stop/Disable | exactly-once unsubscribe |
| Presentation queue | Host | Session producer/Main Thread consumer | depth/high-watermark | Stop drain/drop by generation | bounded no overwrite |
| binding manifest/cache | Resolver | Main Thread activation | snapshot | scope release | manifest mismatch reject |

### 3. 公共端口（精确 C# 签名）

```csharp
namespace Lumio.Client.UnityAdapter;

public interface IUnityClientHost
{
    UnityClientHostStatus Start(in UnityClientHostStartRequest request);
    UnityClientHostStatus Update(in UnityFrameContext frame);
    UnityClientHostStatus Stop(ulong hostGeneration);
    UnityClientHostSnapshot GetSnapshot();
}
```

**禁止出现在签名中的类型：**

- 公共签名中的 UnityEngine/InputSystem 类型
- Session 内部类型
- 直接 ECS/Runtime storage 访问

### 4. 内部类型与文件树

| 目标文件 | 单一职责 |
| --- | --- |
| modules/unity-adapter/src/Public/IUnityClientHost.cs | Unity-neutral host port |
| modules/unity-adapter/src/Public/IUnityClientHostFactory.cs | factory |
| modules/unity-adapter/src/Public/UnityFrameContext.cs | primitive frame value |
| modules/unity-adapter/src/Public/UnityClientHostSnapshot.cs | evidence |
| modules/unity-adapter/src/Internal/UnityClientHost.cs | main-thread orchestration |
| modules/unity-adapter/src/Internal/UnityMainThreadGuard.cs | thread assertion |
| modules/unity-adapter/src/Internal/UnityPresentationQueue.cs | bounded diff queue |
| modules/unity-adapter/src/UnitySurface/LumioClientBootstrap.cs | MonoBehaviour composition hook |
| modules/unity-adapter/src/UnitySurface/Input/UnityInputSystemAdapter.cs | Action→RawInputSample |
| modules/unity-adapter/src/UnitySurface/Presentation/PresentationBindingResolver.cs | manifest binding |
| modules/unity-adapter/src/UnitySurface/Presentation/UnityPresentationApplier.cs | committed diff apply |
| modules/unity-adapter/tests/EditMode/UnityInputSystemAdapterEditModeTests.cs | input |
| modules/unity-adapter/tests/PlayMode/UnitySessionPumpPlayModeTests.cs | order |
| modules/unity-adapter/tests/Fault/UnityAdapterLifecycleRaceTests.cs | lifecycle |

### 5. 成熟依赖

| 能力 | 候选 | 选用 | 许可证/版本 | AOT/确定性 | 为何不自研 | Adapter 隔离点 | 第三方类型穿过公共接口 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 输入 | Unity Input System / legacy / 自研 | Input System 1.17.0 | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | UnitySurface/Input | 否 |
| 测试 | Unity Test Framework / xUnit player / 手写 | Unity Test Framework | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | tests | 否 |
| 表现队列 | Channels / UnityEvent / 自研 | Channels or bounded BCL abstraction isolated | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Internal/UnityPresentationQueue | 否 |

### 6. 控制流（实现顺序）

1. LumioGame Bootstrap 在 Main Thread 创建模块公共依赖与 `IUnityClientHost`。
2. Start 解析 action/binding manifest，订阅 Unity Input System；回调只构造 RawInputSample 并 TryEnqueue。
3. 每帧 Update 固定顺序：收集输入→调用 Session.Tick→drain presentation queue→应用到 Unity 对象。
4. Session 输出的 committed diff 通过 Unity-neutral sink 入 bounded queue，携带 generation。
5. Main Thread resolver 验证 manifest/scope generation，再调用 UnityPresentationApplier。
6. Disable/Destroy/Stop 只执行一次退订与 Session close；迟到 diff 被 generation guard 丢弃。

### 7. 与 Runtime / 生成契约的接缝

- Unity 侧只消费 Session/Input 公共值；Presentation schema/binding manifest 来自 Game Contract/Mapper。
- 核心 never references UnityEngine；asmdef DAG 镜像 ProjectReference DAG。

### 8. 明确不实现

| 实现者最容易误做 | 正确归属/做法 |
| --- | --- |
| 把 Vector3/GameObject 写入核心公共接口 | 在 UnitySurface 内转换为平台无关值/绑定 ID |
| Input callback 直接 Tick Session | callback 只入 Input queue，Update 单点 Tick |
| 表现层读取 Runtime mutable world | 只消费 committed immutable Presentation Diff |

### 9. 失败分类如何变成代码

| 分类 | 结果类型/码 | 谁通知 Session | 副作用约束 |
| --- | --- | --- | --- |
| 可重试 | PresentationQueueBusy | TryWrite QueueFull | Session 决定降级/故障 |
| 可拒绝 | BindingManifestMismatch | StableReject activation | 不改 Unity object |
| 需 Resync | StaleGenerationDiff | 丢弃并计数；必要时 Session 已负责 resync | 不自行请求协议 |
| 可致命 | MainThreadViolation/critical queue contract | Host Fault | 不从错误线程调用 Unity API |

### 10. 可观测性埋点

| 稳定事件名/生成 EventId | 产生位置 | 进入 Failure Bundle |
| --- | --- | --- |
| UnityHostStarted | Start | 否 |
| UnityInputSample | callback accepted/rejected | 否 |
| UnityPresentationQueued | sink accepted | 否 |
| UnityPresentationRejected | binding/generation failure | 是 |
| UnityLifecycleRace | duplicate/late stop | 是 |

### 11. 测试面（先于实现）

| 测试 | 输入 | 期望 | 需要 Unity | LocalEmbedded/Remote Fixture |
| --- | --- | --- | --- | --- |
| `UnityClientHostContractTests.PublicPortContainsNoUnityTypes` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 是 | 不适用或模块内 |
| `UnityMainThreadGuardTests.UpdateFromWrongThread_IsRejected` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 是 | 不适用或模块内 |
| `UnityPresentationQueueTests.QueueFull_IsExplicitAndDoesNotDropOldest` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 是 | 不适用或模块内 |
| `UnityInputSystemAdapterEditModeTests.ActionCallback_ProducesGeneratedSample` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 是 | 不适用或模块内 |
| `PresentationBindingResolverEditModeTests.ManifestMismatch_StableReject` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `UnitySessionPumpPlayModeTests.Update_OrderIsInputThenSessionThenPresentation` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 是 | 不适用或模块内 |
| `UnityPresentationApplierPlayModeTests.StaleGenerationDiff_IsIgnored` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 是 | 不适用或模块内 |
| `UnityAdapterLifecycleRaceTests.DisableDestroyStop_UnsubscribesExactlyOnce` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 是 | 不适用或模块内 |

### 12. Foundation 最小切片 vs 完整模块

| 阶段 | 必须实现的范围 |
| --- | --- |
| Foundation | 不要求 Unity；只冻结 asmdef 与公共 API 泄漏测试 |
| Vertical Slice | Host loop、Input System、Presentation Adapter、Edit/PlayMode tests |
| Production | IL2CPP/AOT/device matrix、域重载/Scene 生命周期、性能预算 |

### 13. 任务拆分

1. `w7-unity-host-loop` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
2. `w7-unity-input-system-adapter` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
3. `w7-unity-presentation-adapter` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
4. `w7-unity-aot-device-matrix` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。

### 14. 开放阻塞

- `SPIKE-UNITY-63-AOT-MATRIX`：3 个工作日锁定 Unity 6.3 LTS、Input System、目标设备与 AOT 兼容；阻塞设备矩阵。
- `UPSTREAM-GENERATED-CONTRACT-API-MAP`：Presentation Diff/Binding Manifest 真名；阻塞真实表现 adapter。

## 12.10 `hybridclr-adapter`

### 1. 一句话职责

封装官方 HybridCLR 的 Artifact 校验、AOT metadata、程序集加载、入口激活、回滚与释放，向上只暴露 Gameplay Scope Loader；删除后仍可使用静态 Gameplay，但不能热加载。

### 2. 唯一拥有的可变状态

| 状态 | 创建者 | 唯一修改者 | 快照/证据 | 销毁者 | 失败恢复 |
| --- | --- | --- | --- | --- | --- |
| load operation/lease | Prepare | operation queue/Main Thread pump | snapshot/result | release/rollback | generation-scoped |
| validated artifact set | validator | immutable after verify | evidence | operation terminal | invalid never loaded |
| rollback ledger | prepare steps | append on success/reverse on failure | evidence | terminal | idempotent reverse |
| capability state | provider init | platform probe | capability result | host stop | no optimistic advertise |

### 3. 公共端口（精确 C# 签名）

```csharp
namespace Lumio.Client.HybridClrAdapter;

public interface IHybridClrScopeLoader
{
    ValueTask<HybridClrScopeLoadResult> PrepareActivateAsync(
        in HybridClrScopeLoadRequest request,
        CancellationToken cancellationToken);
    HybridClrScopeLoadStatus PumpMainThread(in HybridClrMainThreadTick tick);
    ValueTask<HybridClrScopeLoadResult> ReleaseAsync(
        HybridClrScopeLeaseId leaseId,
        CancellationToken cancellationToken);
    HybridClrScopeLoaderSnapshot GetSnapshot();
}
```

**禁止出现在签名中的类型：**

- HybridCLR 官方类型穿过接口
- UnityEngine 类型穿过接口
- Session/Runtime mutable storage/第二 IL VM

### 4. 内部类型与文件树

| 目标文件 | 单一职责 |
| --- | --- |
| modules/hybridclr-adapter/src/Public/IHybridClrScopeLoader.cs | stable loader port |
| modules/hybridclr-adapter/src/Public/IHybridClrScopeLoaderFactory.cs | factory |
| modules/hybridclr-adapter/src/Public/HybridClrScopeLeaseId.cs | opaque lease |
| modules/hybridclr-adapter/src/Public/HybridClrScopeLoadRequest.cs | verified artifact refs |
| modules/hybridclr-adapter/src/Public/HybridClrScopeLoadResult.cs | stable outcome |
| modules/hybridclr-adapter/src/Internal/HybridClrScopeLoader.cs | operation state machine |
| modules/hybridclr-adapter/src/Internal/HybridClrOperationQueue.cs | bounded queue |
| modules/hybridclr-adapter/src/Internal/HybridClrRollbackLedger.cs | reverse cleanup |
| modules/hybridclr-adapter/src/Internal/Validation/GameplayScopeArtifactValidator.cs | hash/release/dependency validation |
| modules/hybridclr-adapter/src/Internal/Official/OfficialHybridClrMetadataAdapter.cs | AOT metadata API |
| modules/hybridclr-adapter/src/Internal/Official/OfficialHybridClrAssemblyAdapter.cs | assembly load |
| modules/hybridclr-adapter/src/Internal/Official/OfficialHybridClrEntrypointAdapter.cs | entry activation |
| modules/hybridclr-adapter/tests/Unit/HybridClrOperationQueueTests.cs | bounded queue |
| modules/hybridclr-adapter/tests/Fault/HybridClrPartialLoadFaultTests.cs | every step fault |
| modules/hybridclr-adapter/tests/PlayMode/HybridClrActivationPlayModeTests.cs | valid activation |

### 5. 成熟依赖

| 能力 | 候选 | 选用 | 许可证/版本 | AOT/确定性 | 为何不自研 | Adapter 隔离点 | 第三方类型穿过公共接口 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| IL runtime | 官方 HybridCLR / ALC / 自研 VM | 官方 HybridCLR 8.12.0 候选 | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Internal/Official | 否 |
| Artifact verify | BCL crypto / package API / 自研 | BCL + generated manifest | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Internal/Validation | 否 |
| operation queue | Channels / Unity coroutine only / 自研 | Channels + MainThread pump | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Internal | 否 |

### 6. 控制流（实现顺序）

1. Capability provider 只有在平台、官方 runtime、AOT metadata 与版本全部可用时才 advertise。
2. Session 的产品 Scope Activator 先从 persistence 获得 verified artifact，再调用 Prepare。
3. Validator 在任何官方加载前检查 release/hash/dependency/capability。
4. 后台可准备 bytes，但所有要求 Main Thread 的官方步骤通过 `PumpMainThread` 顺序执行。
5. 每成功一步登记 rollback action；全部步骤成功且入口验证后才返回 lease。
6. Cancel/QueueFull/任何步骤失败按 ledger 逆序回滚，入口从未暴露。
7. Release 按 lease 幂等执行；迟到 operation 由 generation/lease guard 拒绝。

### 7. 与 Runtime / 生成契约的接缝

- Artifact manifest、Scope entry contract、capability IDs 来自上游生成 Contract。
- Session 只看 IClientGameplayScopeActivator；具体产品 Composition Root 可用本 loader 实现它。

### 8. 明确不实现

| 实现者最容易误做 | 正确归属/做法 |
| --- | --- |
| 自研 HybridCLR loader/第二 IL VM | 只包装官方 API 并校验/激活 |
| 部分 load 后先暴露 entrypoint | 所有步骤成功后原子发布 lease |
| 让 Session 引用 HybridCLR 类型 | 通过 IClientGameplayScopeActivator/IHybridClrScopeLoader value port |

### 9. 失败分类如何变成代码

| 分类 | 结果类型/码 | 谁通知 Session | 副作用约束 |
| --- | --- | --- | --- |
| 可重试 | OperationQueueBusy/temporary file read | Retryable result | 无可见 lease |
| 可拒绝 | Unsupported/HashMismatch/DependencyMismatch | StableReject | 不调用官方 load |
| 需 Resync | Artifact release mismatch | Session 获取正确 Artifact/完整同步 | 不加载旧 scope |
| 可致命 | RollbackFailure/official runtime invariant | Fault + partial bundle | 不得发布 entrypoint |

### 10. 可观测性埋点

| 稳定事件名/生成 EventId | 产生位置 | 进入 Failure Bundle |
| --- | --- | --- |
| HybridClrCapability | provider query | 否 |
| HybridClrArtifactRejected | validator | 是 |
| HybridClrLoadStep | each official step | 是 |
| HybridClrRollbackStep | reverse ledger | 是 |
| HybridClrScopeActivated | lease published | 是 |

### 11. 测试面（先于实现）

| 测试 | 输入 | 期望 | 需要 Unity | LocalEmbedded/Remote Fixture |
| --- | --- | --- | --- | --- |
| `HybridClrCapabilityProviderTests.UnavailablePlatform_DoesNotAdvertiseSupport` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `GameplayScopeArtifactValidatorTests.HashOrReleaseMismatch_RejectsBeforeLoad` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `HybridClrOperationQueueTests.QueueFull_IsExplicitAndBounded` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `HybridClrRollbackLedgerTests.ReverseOrder_IsStableAndIdempotent` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `HybridClrLoadRaceTests.CancelBeatsLateActivation` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `HybridClrPartialLoadFaultTests.FailureAtEveryOfficialStep_NeverExposesEntrypoint` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `HybridClrDependencyBoundaryTests.NoSessionOrUnityTypesInStableApi` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 是 | 不适用或模块内 |
| `HybridClrActivationPlayModeTests.ValidArtifact_ActivatesOnlyAfterAllSteps` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 是 | 不适用或模块内 |
| `HybridClrAotPlayerTests.TargetMatrix_LoadsMetadataAndScope` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 是 | 不适用或模块内 |

### 12. Foundation 最小切片 vs 完整模块

| 阶段 | 必须实现的范围 |
| --- | --- |
| Foundation | 只冻结接口/asmdef/No-op capability；不引入官方 runtime |
| Vertical Slice | Capability provider、verified load、rollback/unload、PlayMode |
| Production | AOT player/device matrix、许可/发行审查、内存泄漏/100 次 reload |

### 13. 任务拆分

1. `w7-hybridclr-capability-provider` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
2. `w7-hybridclr-scope-loader` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
3. `w7-hybridclr-rollback-unload` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。

### 14. 开放阻塞

- `SPIKE-HYBRIDCLR-63`：3 个工作日关闭官方版本、许可、Unity/AOT metadata 与发行路径；阻塞生产 loader。
- `UPSTREAM-GENERATED-CONTRACT-API-MAP`：Scope artifact/entry/capability contract；阻塞真实激活。

## 12.11 `bot`

### 1. 一句话职责

提供最小 Headless Host 与确定性 Scenario Driver，只通过 Session/Input/Observability 公共 API 跑完整生产协议；删除后 Foundation 无自动端到端退出证明。

### 2. 唯一拥有的可变状态

| 状态 | 创建者 | 唯一修改者 | 快照/证据 | 销毁者 | 失败恢复 |
| --- | --- | --- | --- | --- | --- |
| bot run generation | RunAsync | single owner loop | BotRunResult/snapshot | terminal/cancel | one terminal |
| scenario cursor/seed | driver create | owner loop | fixture evidence | run end | same seed deterministic |
| owner tick clock | host | owner loop | tick trace | run end | no wallclock authority |
| exit evidence | host | terminal reducer | result/bundle | caller owns | queue full categories distinct |

### 3. 公共端口（精确 C# 签名）

```csharp
namespace Lumio.Client.Bot;

public interface IBotScenarioDriver
{
    BotDriverResult FillSamples(
        in BotDriverContext context,
        Span<Lumio.Client.Input.RawInputSample> destination);
}

public interface IHeadlessBotHost
{
    ValueTask<BotRunResult> RunAsync(
        in BotRunRequest request,
        CancellationToken cancellationToken);
}
```

**禁止出现在签名中的类型：**

- Connection/Handshake/Replica/Prediction 内部类型
- 简化 Envelope/typed LocalEmbedded shortcut
- Unity/HybridCLR 类型

### 4. 内部类型与文件树

| 目标文件 | 单一职责 |
| --- | --- |
| modules/bot/src/Public/IHeadlessBotHost.cs | host port |
| modules/bot/src/Public/IBotScenarioDriver.cs | scenario port |
| modules/bot/src/Public/IHeadlessBotHostFactory.cs | factory |
| modules/bot/src/Public/BotRunRequest.cs | fixture/seed/tick budget |
| modules/bot/src/Public/BotRunResult.cs | terminal evidence |
| modules/bot/src/Internal/HeadlessBotHost.cs | owner loop |
| modules/bot/src/Internal/BotRunLoop.cs | fill→enqueue→session tick→observe |
| modules/bot/src/Internal/FixtureBotScenarioDriver.cs | generated fixture driver |
| modules/bot/src/Internal/BotTerminalReducer.cs | one terminal |
| modules/bot/host/Lumio.Client.Bot.Host.csproj | executable composition root |
| modules/bot/host/Program.cs | Generic Host entry |
| modules/bot/host/BotHostComposition.cs | explicit module construction |
| modules/bot/host/CommandLine/BotCommandLine.cs | CLI |
| modules/bot/tests/Unit/BotRunLoopTests.cs | order |
| modules/bot/tests/Contract/BotSameProtocolFixtureTests.cs | same protocol |
| modules/bot/tests/Architecture/BotPublicApiTests.cs | reference policy |

### 5. 成熟依赖

| 能力 | 候选 | 选用 | 许可证/版本 | AOT/确定性 | 为何不自研 | Adapter 隔离点 | 第三方类型穿过公共接口 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Host | Generic Host / custom loop / Unity | Generic Host + explicit owner loop | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | bot/host | 否 |
| CLI | System.CommandLine / hand parse / 自研 | System.CommandLine | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | host/CommandLine | 否 |
| Fixtures | 生成 corpus / custom JSON / hardcoded | 上游生成 fixture corpus | 见第 9 节对应许可证/锁定策略 | 公共 API 不泄漏 | 成熟语义、减少维护面 | Internal/FixtureBotScenarioDriver | 否 |

### 6. 控制流（实现顺序）

1. CLI 选择 generated fixture、transport mode、seed 与 tick budget。
2. Composition Root 创建 observability、connection、handshake、leaf modules、Session 与 Bot。
3. Run loop 固定顺序调用 Driver.FillSamples→Input.TryEnqueue→Session.Tick→观察 snapshot/terminal。
4. LocalEmbedded/Remote 由相同 Session API 与 fixture 驱动；Bot 不拿连接内部引用。
5. Cancel、Session terminal、QueueFull、budget exhaustion 进入 terminal reducer，输出一次 BotRunResult。
6. 失败时附上内存事件/Fault decorator trace，退出码稳定。

### 7. 与 Runtime / 生成契约的接缝

- Fixture corpus/expected protocol trace 来自生成 Contract。
- Bot 仅消费公共 API；Architecture test 检查 csproj allowlist 与程序集引用。

### 8. 明确不实现

| 实现者最容易误做 | 正确归属/做法 |
| --- | --- |
| Bot 直接调用 Handshake/Replica 内部实现 | 只调用 Session/Input/Observability public ports |
| 用 typed object 跳过 Codec | transport 仍使用 production LocalEmbedded/Remote |
| 以 wallclock sleep 驱动权威 Tick | 显式 ClientOwnerTick/fixture clock |

### 9. 失败分类如何变成代码

| 分类 | 结果类型/码 | 谁通知 Session | 副作用约束 |
| --- | --- | --- | --- |
| 可重试 | scenario-defined transient | host 根据 fixture policy 继续 | 不绕过 Session |
| 可拒绝 | stable protocol reject | BotRunResult Rejected | 保留完整 trace |
| 需 Resync | fixture gap | 由 Session 执行 resync | Bot 不直接干预 |
| 可致命 | critical queue full/session fault/budget invariant | nonzero stable exit + bundle | one terminal |

### 10. 可观测性埋点

| 稳定事件名/生成 EventId | 产生位置 | 进入 Failure Bundle |
| --- | --- | --- |
| BotRunStarted | RunAsync | 否 |
| BotTick | each owner tick summary | 否 |
| BotScenarioStep | driver cursor | 否 |
| BotRunTerminal | terminal reducer | 是 |
| BotShortcutPolicyViolation | architecture test | 测试证据 |

### 11. 测试面（先于实现）

| 测试 | 输入 | 期望 | 需要 Unity | LocalEmbedded/Remote Fixture |
| --- | --- | --- | --- | --- |
| `FixtureBotScenarioDriverTests.SameSeedAndFixture_ProducesSameSamples` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `BotRunLoopTests.OrderIsFillEnqueueSessionTickObserve` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `BotRunLoopTests.DriverCannotCreateClientCommandSequence` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `BotCancellationRaceTests.CancelAndSessionSuccess_OneTerminalResult` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `BotQueueFullTests.InputAndCriticalQueueFull_AreDistinguished` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |
| `BotSameProtocolFixtureTests.LocalAndRemote_UseSameGeneratedVectors` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 共用 |
| `BotSameProtocolFixtureTests.BotCannotActivateWithoutHandshakeAndScopeGate` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 共用 |
| `BotPublicApiTests.ProjectReferencesMatchAllowlist` | 生成 fixture/fake/fault 注入（由测试名限定） | 状态、结果、调用顺序或零副作用断言 | 否 | 不适用或模块内 |

### 12. Foundation 最小切片 vs 完整模块

| 阶段 | 必须实现的范围 |
| --- | --- |
| Foundation | 最小 Headless Host、LocalEmbedded full protocol、Foundation exit command |
| Vertical Slice | Remote transport、完整 Input/Prediction/Config/Persistence scenario |
| Production | 并发 Bot/长稳/性能基线，但仍不走捷径 |

### 13. 任务拆分

1. `w6-bot-headless-host` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
2. `w6-bot-deterministic-adapters` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。
3. `w6-bot-no-shortcut-policy` — 见对应计划/任务卡；单卡文件集与同 wave 邻卡不重叠。

### 14. 开放阻塞

- `UPSTREAM-GENERATED-CONTRACT-API-MAP`：Foundation/Vertical fixture corpus 真名；阻塞真实 end-to-end vectors。


## 13. Wave 与退出条件映射

| Wave | 范围 | 关键退出证据 |
|---|---|---|
| 0 | SDK、目录、项目图、上游 API map、测试/formatter/analyzer、allowlist | locked restore；ArchitectureTests 可失败/通过；无生产行为 |
| 1 | observability 内存 Sink + 有界 dispatch；persistence 窄端口 | Critical QueueFull 可观察；事件稳定有序 |
| 2 | connection LocalEmbedded/Codec/有界队列/Fault + input sample queue | 真 Encode/Decode；无同步重入；InputSampleSeq accepted-only |
| 3 | handshake | 生成 Fixture Accepted/Reject；零副作用拒绝 |
| 4 | replica stage + prediction history/stage | 两者互不引用、无独立 Commit |
| 5 | session state/priority/gates/scope/双 Handle/单一事务/resync/reconnect/close | 完整故障矩阵与释放顺序 |
| 6 | Bot Headless Host + Foundation Gate | Headless→LocalEmbedded→Active→Gap/Resync→Close |
| 7 | Remote、Persistence、Serilog/OTel/Bundle、Unity、HybridCLR、Vertical Slice | Input→Prediction→Correction/Rollback→Diff→Config→Save/Load |

### 13.1 Foundation Exit

必须由任务 `w6-foundation-exit-scenario`、`w6-localembedded-fidelity-suite`、`w6-bot-no-shortcut-policy` 联合证明：

```text
Headless Bot
→ LocalEmbedded production Encode/Decode
→ Connect
→ Authentication + Handshake
→ Gameplay Scope activation gate
→ ECS Handle → Voxel Handle
→ FullSnapshot
→ single Runtime authority transaction
→ BaselineAck
→ Active
→ Gap
→ same-connection Resync（不重握手）
→ Close（逆序释放）
```

同时注入 Release Reject、权限拒绝、Critical QueueFull、Input QueueFull、迟到 generation、Disconnect/Reconnect。Reconnect 必须新 generation、重认证、重握手且无 Resume Token。

### 13.2 Vertical Slice Exit

```text
Unity/Bot Input Mapping
→ InputSampleSeq
→ Candidate（无 ClientCommandSeq）
→ local Runtime prediction transaction
→ ClientCommandSeq + PredictionKey
→ authority confirmation/correction
→ Replica + Prediction single authority transaction
→ atomic rollback/replay
→ committed Presentation Diff
→ Config staging/activation
→ verified Artifact + Checkpoint Save/Load
→ Replay/Failure Bundle evidence
```

## 14. 阻塞项总表

| ID | 类型 | 需要的结论 | 阻塞任务 |
|---|---|---|---|
| `UPSTREAM-RUNTIME-CONTRACT-API-MAP` | 上游发布 API | Handle、local/authority transaction、plan/outcome、presentation diff 真实全名与版本 | replica、prediction、session production adapter |
| `UPSTREAM-GENERATED-CONTRACT-API-MAP` | 上游生成 Contract | Envelope/Codec/ErrorCode/Event/Ack/Config/Artifact/Fixture 真实全名与 corpus 路径 | connection、handshake、所有 contract fixture、exit gate |
| `SPIKE-REMOTE-AOT` | 2 工作日 spike | Socket/Pipelines/TLS 在目标 Unity/IL2CPP 的行为、取消与关闭预算 | Remote transport |
| `SPIKE-HYBRIDCLR-63` | 3 工作日 spike | 官方版本、许可、Unity 6.3、AOT metadata、发行路径 | HybridCLR loader |
| `SPIKE-OTEL-IL2CPP` | 2 工作日 spike | OpenTelemetry exporter 在 IL2CPP 的链接、分配与降级 | Production OTel |
| `SPIKE-UNITY-63-AOT-MATRIX` | 3 工作日 spike | Unity/Input System/设备矩阵与 asmdef/AOT 约束 | Unity device matrix |

## 15. 建议同步清单（等待确认后执行）

1. 模块 README 若仍使用与本文不同的工厂名，只同步实现级命名，不改变公共协议语义。
2. 原设计文档第 7 节 DAG 增加对 `eng/project-reference-allowlist.json` 的链接。
3. 审查报告 D7/D8 的状态迁移与退出条件以本文第 11、13 节为实现入口。
4. 上游真实 API map 完成后，将本文设计别名替换为真实全名；不复制字段表。

## 16. 本回合不产生的文件

本文和两份计划是设计交付。实际 `.cs`、`.csproj`、`.asmdef`、CI 配置与 `.spec/tasks/*.md` 由后续实现任务创建；当前恢复包不含生产源码。

## 17. 恢复说明

本文件依据上一轮已经输出的设架范围、签名、任务顺序与审计摘要重新恢复。先前会话附件的临时下载句柄已失效，因此本副本不承诺与失效附件逐字节相同；其目标是恢复同一实现设计语义与可执行结构。

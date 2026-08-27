# LumioClient Foundation Implementation Plan

> **执行纪律：** 按依赖顺序逐卡执行；每张卡先写失败测试，再写最小实现。本文包含端口/测试草图，但它们是设计草图，不是本回合提交的生产代码。

**Goal:** 以 Wave 0–6 建立可复现工程骨架，并由 Headless Bot 使用 production LocalEmbedded 协议跑通 Connect、Handshake、Scope、FullSnapshot、单一 Runtime 事务、Active、Gap/Resync、Close。

**Architecture:** 按依赖从 observability → connection/input → handshake → replica/prediction → session → bot 推进；leaf module 只暴露自己的 port，Session 独占编排与 commit。

**Tech Stack:** .NET SDK 10.0.400；核心 netstandard2.1/C#9；Host/tests net10.0/C#14；Channels；xUnit v3；FsCheck；ArchUnitNET；生成 Contract/Runtime Port。

## 1. 执行总则

- 一张任务只修改“涉及范围”列出的文件；发现邻卡需要同一路径时先串行化依赖，不并行写同文件。
- 每个 production API 先有 compilation/contract test；每个状态机先有 table-driven test；每个异步入口先有 cancel/late generation/QueueFull test。
- 上游真实类型无法解析时停止在 compile-only map，不创建本仓替代 Envelope/Transaction/ErrorCode。
- 任务完成证据包括：红灯命令、绿灯命令、架构检查、fixture/hash、必要时 failure trace。

## 2. Wave 概览

| Wave | 任务 | 并行纪律 |
| --- | --- | --- |
| 0A | `w0-freeze-dotnet-toolchain-and-packages`, `w0-map-upstream-runtime-contract-apis`, `w0-create-test-fixture-layout` | 同一子 wave 的文件集在本计划中不重叠 |
| 0B | `w0-create-module-project-graph` | 同一子 wave 的文件集在本计划中不重叠 |
| 0C | `w0-create-unity-asmdef-graph`, `w0-create-architecture-policy-harness` | 同一子 wave 的文件集在本计划中不重叠 |
| 1A | `w1-observability-contract`, `w1-persistence-contract-surface` | 同一子 wave 的文件集在本计划中不重叠 |
| 1B | `w1-observability-memory-sink`, `w1-observability-bounded-dispatch` | 同一子 wave 的文件集在本计划中不重叠 |
| 2A | `w2-connection-contract-and-generation`, `w2-input-sample-ingress` | 同一子 wave 的文件集在本计划中不重叠 |
| 2B | `w2-connection-localembedded-transport`, `w2-input-deterministic-mapping` | 同一子 wave 的文件集在本计划中不重叠 |
| 2C | `w2-connection-bounded-queues-and-faults` | 同一子 wave 的文件集在本计划中不重叠 |
| 3A | `w3-handshake-contract-and-attempt` | 同一子 wave 的文件集在本计划中不重叠 |
| 3B | `w3-handshake-generated-contract-adapter`, `w3-handshake-capability-and-rejects` | 同一子 wave 的文件集在本计划中不重叠 |
| 4A | `w4-replica-stage-contract`, `w4-prediction-sequence-history` | 同一子 wave 的文件集在本计划中不重叠 |
| 4B | `w4-replica-baseline-gap-metadata`, `w4-replica-fullsnapshot-fixtures`, `w4-prediction-authority-stage`, `w4-prediction-window-faults` | 同一子 wave 的文件集在本计划中不重叠 |
| 5A | `w5-session-contract-and-resource-ledger` | 同一子 wave 的文件集在本计划中不重叠 |
| 5B | `w5-session-event-arbiter`, `w5-session-active-message-gate` | 同一子 wave 的文件集在本计划中不重叠 |
| 5C | `w5-session-first-connect-orchestration`, `w5-session-authority-transaction-orchestration` | 同一子 wave 的文件集在本计划中不重叠 |
| 5D | `w5-session-resync-reconnect` | 同一子 wave 的文件集在本计划中不重叠 |
| 5E | `w5-session-close-fault-release` | 同一子 wave 的文件集在本计划中不重叠 |
| 5F | `w5-authority-transaction-fault-matrix` | 同一子 wave 的文件集在本计划中不重叠 |
| 6A | `w6-bot-headless-host`, `w6-bot-deterministic-adapters` | 同一子 wave 的文件集在本计划中不重叠 |
| 6B | `w6-foundation-exit-scenario`, `w6-localembedded-fidelity-suite`, `w6-bot-no-shortcut-policy` | 同一子 wave 的文件集在本计划中不重叠 |

## 3. 任务明细

### Task 1: 冻结 .NET SDK、语言、分析器、格式与包版本

- **Task card:** `.spec/tasks/w0-freeze-dotnet-toolchain-and-packages.md`
- **Wave:** `0A`
- **依赖:** `无`

#### 涉及范围

- `global.json`
- `Directory.Build.props`
- `Directory.Packages.props`
- `NuGet.Config`
- `.editorconfig`
- `eng/dependency-baseline.md`
- `eng/verify-toolchain.sh`
- `eng/verify-toolchain.ps1`

#### 接口

**Consumes**

- 仓库现有规范文件

**Produces**

- SDK 10.0.400 policy
- central package versions
- deterministic build/format contract

#### Step 1：先写失败测试

- `ToolchainPolicyTests.GlobalJsonPinsSdkAndDisablesRollForward`
- `ToolchainPolicyTests.AllProjectsEnableNullableAndWarningsAsErrors`
- `DependencyBaselineTests.AllDirectPackagesHaveLicenseAndLockStrategy`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先创建 ArchitectureTests 中的工具链失败测试。
2. 创建根级策略文件；核心 TFM/C#9 与 Host/tests net10/C#14 分离。
3. 加入 locked restore、format、build 命令；记录每个包许可证/AOT 隔离。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test tests/Lumio.Client.ArchitectureTests/Lumio.Client.ArchitectureTests.csproj --filter ToolchainPolicy
```

### Task 2: 把设计别名映射到已发布 Runtime/生成 Contract 真名

- **Task card:** `.spec/tasks/w0-map-upstream-runtime-contract-apis.md`
- **Wave:** `0A`
- **依赖:** `w0-freeze-dotnet-toolchain-and-packages`

#### 涉及范围

- `eng/upstream-api-map.md`
- `eng/upstream-contract-smoke/Lumio.Client.UpstreamContractSmoke.csproj`
- `eng/upstream-contract-smoke/Program.cs`
- `tests/Lumio.Client.ArchitectureTests/Upstream/UpstreamApiMapTests.cs`

#### 接口

**Consumes**

- 架构源 package/feed 与 fixture corpus

**Produces**

- `RuntimeContract.*` 真名表
- `GeneratedContract.*` 真名表
- 阻塞项证据

#### Step 1：先写失败测试

- `UpstreamApiMapTests.EveryDesignAliasMapsToOnePublishedType`
- `UpstreamApiMapTests.GeneratedFixtureCorpusIsVersionPinned`
- `UpstreamApiMapTests.NoClientDefinedEnvelopeOrTransactionContract`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 编写映射完整性测试，缺一条即失败。
2. 用 compile-only smoke 引用真实类型，不反射猜字段。
3. 无法读取的 API 记录为阻塞并标出受影响任务，不创建替代 DTO。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet build eng/upstream-contract-smoke/Lumio.Client.UpstreamContractSmoke.csproj -c Release
```

### Task 3: 建立生成 Fixture、Fault 与证据目录合同

- **Task card:** `.spec/tasks/w0-create-test-fixture-layout.md`
- **Wave:** `0A`
- **依赖:** `w0-map-upstream-runtime-contract-apis`

#### 涉及范围

- `tests/Fixtures/README.md`
- `tests/Fixtures/index.json`
- `tests/Lumio.Client.IntegrationTests/Fixtures/GeneratedFixtureCatalog.cs`
- `tests/Lumio.Client.IntegrationTests/Fixtures/FixtureHashTests.cs`
- `tests/Lumio.Client.IntegrationTests/Fakes/FakeClientRuntimePort.cs`
- `tests/Lumio.Client.IntegrationTests/Fakes/FakeGameplayScopeActivator.cs`

#### 接口

**Consumes**

- w0-map-upstream-runtime-contract-apis

**Produces**

- 统一 fixture catalog
- Fake Runtime/Scope evidence hooks

#### Step 1：先写失败测试

- `FixtureHashTests.CatalogMatchesPinnedUpstreamHashes`
- `FixtureCatalogTests.NoSchemaFieldsAreReplicatedLocally`
- `FakeRuntimePortTests.RecordsSingleTransactionCalls`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写 catalog/hash 失败测试。
2. 目录只保存上游 fixture 引用、哈希与本地 fault script；不复制 schema。
3. Fake Runtime 暴露调用记录与可注入 outcome，不发明 production semantics。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test tests/Lumio.Client.IntegrationTests/Lumio.Client.IntegrationTests.csproj --filter Fixture
```

### Task 4: 按一模块一程序集创建空项目图与测试项目图

- **Task card:** `.spec/tasks/w0-create-module-project-graph.md`
- **Wave:** `0B`
- **依赖:** `w0-freeze-dotnet-toolchain-and-packages`

#### 涉及范围

- `LumioClient.slnx`
- `modules/session/src/Lumio.Client.Session.csproj`
- `modules/connection/src/Lumio.Client.Connection.csproj`
- `modules/handshake/src/Lumio.Client.Handshake.csproj`
- `modules/replica/src/Lumio.Client.Replica.csproj`
- `modules/prediction/src/Lumio.Client.Prediction.csproj`
- `modules/input/src/Lumio.Client.Input.csproj`
- `modules/persistence/src/Lumio.Client.Persistence.csproj`
- `modules/observability/src/Lumio.Client.Observability.csproj`
- `modules/unity-adapter/src/Lumio.Client.UnityAdapter.csproj`
- `modules/hybridclr-adapter/src/Lumio.Client.HybridClrAdapter.csproj`
- `modules/bot/src/Lumio.Client.Bot.csproj`
- `modules/bot/host/Lumio.Client.Bot.Host.csproj`
- `tests/Lumio.Client.ArchitectureTests/Lumio.Client.ArchitectureTests.csproj`
- `tests/Lumio.Client.IntegrationTests/Lumio.Client.IntegrationTests.csproj`

#### 接口

**Consumes**

- w0-freeze-dotnet-toolchain-and-packages

**Produces**

- 空项目图
- test project graph
- Bot composition root

#### Step 1：先写失败测试

- `ProjectGraphTests.AllElevenModuleAssembliesExist`
- `ProjectGraphTests.ProjectReferencesAreAllowlisted`
- `ProjectGraphTests.ProductionDagIsAcyclic`
- `InternalsVisibleToTests.OnlyOwnTestAssemblyIsFriend`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写读取 csproj 的图测试。
2. 创建空项目与准确 ProjectReference；不创建生产行为。
3. 所有 production 项目开启 lock file、nullable、warning-as-error；IVT 仅本模块测试。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test tests/Lumio.Client.ArchitectureTests/Lumio.Client.ArchitectureTests.csproj --filter ProjectGraph
```

### Task 5: 建立与 ProjectReference 同构的 Unity asmdef 图

- **Task card:** `.spec/tasks/w0-create-unity-asmdef-graph.md`
- **Wave:** `0C`
- **依赖:** `w0-create-module-project-graph`

#### 涉及范围

- `packages/com.lumio.client/package.json`
- `packages/com.lumio.client/Runtime/Session/Lumio.Client.Session.asmdef`
- `packages/com.lumio.client/Runtime/Connection/Lumio.Client.Connection.asmdef`
- `packages/com.lumio.client/Runtime/Handshake/Lumio.Client.Handshake.asmdef`
- `packages/com.lumio.client/Runtime/Replica/Lumio.Client.Replica.asmdef`
- `packages/com.lumio.client/Runtime/Prediction/Lumio.Client.Prediction.asmdef`
- `packages/com.lumio.client/Runtime/Input/Lumio.Client.Input.asmdef`
- `packages/com.lumio.client/Runtime/Persistence/Lumio.Client.Persistence.asmdef`
- `packages/com.lumio.client/Runtime/Observability/Lumio.Client.Observability.asmdef`
- `packages/com.lumio.client/Runtime/UnityAdapter/Lumio.Client.UnityAdapter.asmdef`
- `packages/com.lumio.client/Runtime/HybridClrAdapter/Lumio.Client.HybridClrAdapter.asmdef`
- `tests/Lumio.Client.ArchitectureTests/Unity/AsmdefGraphTests.cs`

#### 接口

**Consumes**

- w0-create-module-project-graph

**Produces**

- UPM package skeleton
- asmdef DAG

#### Step 1：先写失败测试

- `AsmdefGraphTests.UnityReferencesAreSubsetOfProjectAllowlist`
- `AsmdefGraphTests.CoreAsmdefsDoNotReferenceUnityEngine`
- `AsmdefGraphTests.UnityAndHybridClrStayAtLeaves`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写 asmdef JSON 图测试。
2. 创建 package/asmdef 元数据；核心 asmdef 不引用 UnityEngine。
3. UnityAdapter 与 HybridClrAdapter 作为叶子；测试 asmdef 单独。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test tests/Lumio.Client.ArchitectureTests/Lumio.Client.ArchitectureTests.csproj --filter Asmdef
```

### Task 6: 实现引用 allowlist、第三方泄漏与禁止模块检查器

- **Task card:** `.spec/tasks/w0-create-architecture-policy-harness.md`
- **Wave:** `0C`
- **依赖:** `w0-create-module-project-graph`

#### 涉及范围

- `eng/project-reference-allowlist.json`
- `eng/banned-public-api.txt`
- `tests/Lumio.Client.ArchitectureTests/References/ProjectReferenceAllowlistTests.cs`
- `tests/Lumio.Client.ArchitectureTests/References/AssemblyReferenceLeakTests.cs`
- `tests/Lumio.Client.ArchitectureTests/Api/PublicApiSupplierLeakTests.cs`
- `tests/Lumio.Client.ArchitectureTests/Layout/ForbiddenModuleTests.cs`

#### 接口

**Consumes**

- w0-create-module-project-graph

**Produces**

- machine-readable allowlist
- banned API surface rules

#### Step 1：先写失败测试

- `ProjectReferenceAllowlistTests.ActualEdgesEqualAllowedSubset`
- `AssemblyReferenceLeakTests.CoreHasNoUnityHybridClrSocketSupplierReferences`
- `PublicApiSupplierLeakTests.NoThirdPartyTypeCrossesStablePorts`
- `ForbiddenModuleTests.NoCommonSharedUtilsOrSecondContractsModule`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写对故意违规 fixture 的失败断言。
2. 读取 csproj、assembly metadata 与 public API；报告精确消费者/边。
3. 加入 replica↔prediction、bot shortcut、Gameplay reverse reference 专项规则。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test tests/Lumio.Client.ArchitectureTests/Lumio.Client.ArchitectureTests.csproj -c Release
```

### Task 7: 定义结构化事件写入、Sink 与快照端口

- **Task card:** `.spec/tasks/w1-observability-contract.md`
- **Wave:** `1A`
- **依赖:** `w0-create-architecture-policy-harness`

#### 涉及范围

- `modules/observability/src/Public/IClientEventWriter.cs`
- `modules/observability/src/Public/IClientEventSink.cs`
- `modules/observability/src/Public/IClientEventPipelineFactory.cs`
- `modules/observability/src/Public/IClientEventMemorySnapshotSource.cs`
- `modules/observability/src/Public/ClientEventPipelineOptions.cs`
- `modules/observability/tests/Unit/ClientEventWriterTests.cs`

#### 接口

**Consumes**

- 生成 ClientEventRecord/EventId

**Produces**

- IClientEventWriter
- IClientEventSink
- IClientEventPipelineFactory
- IClientEventMemorySnapshotSource

#### Step 1：先写失败测试

- `ClientEventWriterTests.PublicPortUsesOnlyGeneratedAndModuleTypes`
- `ClientEventWriterTests.InvalidSchemaClassIsRejectedWithoutQueueMutation`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先提交 public API compilation/failure tests。
2. 创建不可变 options/result/snapshot 与接口；supplier 类型不得出现。
3. 提供默认工厂构造入口但不实现外部 Sink。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/observability/tests/Lumio.Client.Observability.Tests.csproj --filter ClientEventWriterTests
```

### Task 8: 定义 Session Artifact 与 Checkpoint 窄端口

- **Task card:** `.spec/tasks/w1-persistence-contract-surface.md`
- **Wave:** `1A`
- **依赖:** `w0-create-architecture-policy-harness`

#### 涉及范围

- `modules/persistence/src/Public/IVerifiedSessionArtifactSource.cs`
- `modules/persistence/src/Public/IClientCheckpointStore.cs`
- `modules/persistence/src/Public/IClientPersistenceFactory.cs`
- `modules/persistence/src/Public/VerifiedArtifactReadRequest.cs`
- `modules/persistence/src/Public/CheckpointWriteRequest.cs`
- `modules/persistence/tests/Unit/PersistencePublicContractTests.cs`

#### 接口

**Consumes**

- 生成 Artifact/Checkpoint values

**Produces**

- IVerifiedSessionArtifactSource
- IClientCheckpointStore
- IClientPersistenceFactory

#### Step 1：先写失败测试

- `PersistencePublicContractTests.NoPathStreamOrDatabaseTypeCrossesPort`
- `PersistencePublicContractTests.OnlyVerifiedArtifactCanBeReturned`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写 API 泄漏与 verified-only 失败测试。
2. 定义 async result 与 generation tagging；不实现文件 I/O。
3. 创建 integration fake 所需最小 in-memory factory。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/persistence/tests/Lumio.Client.Persistence.Tests.csproj --filter PersistencePublicContractTests
```

### Task 9: 实现 Foundation 内存 Sink 与稳定批次顺序

- **Task card:** `.spec/tasks/w1-observability-memory-sink.md`
- **Wave:** `1B`
- **依赖:** `w1-observability-contract`

#### 涉及范围

- `modules/observability/src/Public/InMemoryClientEventSink.cs`
- `modules/observability/src/Internal/Sinks/InMemoryEventBuffer.cs`
- `modules/observability/src/Internal/Sinks/InMemorySnapshotBuilder.cs`
- `modules/observability/tests/Unit/InMemoryEventSinkTests.cs`

#### 接口

**Consumes**

- w1-observability-contract

**Produces**

- in-memory sink
- immutable Failure evidence snapshot

#### Step 1：先写失败测试

- `InMemoryEventSinkTests.BatchOrder_IsStable`
- `InMemoryEventSinkTests.SnapshotIsImmutableAndBounded`
- `InMemoryEventSinkTests.CloseIsIdempotent`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写顺序、容量与关闭测试。
2. 使用有界 BCL 存储；只在 dispatcher 线程修改。
3. snapshot 复制成不可变值，调用方不能持有内部 buffer。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/observability/tests/Lumio.Client.Observability.Tests.csproj --filter InMemoryEventSinkTests
```

### Task 10: 实现非阻塞 Writer、有界 dispatcher 与 QueueFull 语义

- **Task card:** `.spec/tasks/w1-observability-bounded-dispatch.md`
- **Wave:** `1B`
- **依赖:** `w1-observability-memory-sink`

#### 涉及范围

- `modules/observability/src/Internal/Pipeline/BoundedEventWriter.cs`
- `modules/observability/src/Internal/Pipeline/EventDispatcherWorker.cs`
- `modules/observability/src/Internal/Pipeline/EventDropPolicy.cs`
- `modules/observability/src/Internal/Pipeline/FailureEvidenceEncoder.cs`
- `modules/observability/src/Public/DefaultClientEventPipelineFactory.cs`
- `modules/observability/tests/Unit/BoundedEventDispatcherTests.cs`
- `modules/observability/tests/Property/EventDropPolicyProperties.cs`
- `modules/observability/tests/Fault/ObservabilityFaultTests.cs`

#### 接口

**Consumes**

- w1-observability-memory-sink

**Produces**

- nonblocking event pipeline
- CriticalQueueFull evidence

#### Step 1：先写失败测试

- `BoundedEventDispatcherTests.CriticalQueueFull_ReturnsWithoutBlocking`
- `BoundedEventDispatcherTests.DroppableQueueFull_DropsOnlySchemaAllowedClass`
- `EventDropPolicyProperties.NeverDropsDurableClass`
- `ObservabilityFaultTests.SinkThrows_BatchRetainedAndExceptionContained`
- `ObservabilityFaultTests.CloseWriteRace_NoSilentLoss`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先用容量 1 的测试制造红灯。
2. 用 Channels 实现 writer/dispatcher；ProducerSequence 只在 accepted path 推进。
3. Sink throw 被捕获为 pipeline fault；关闭/写入竞态输出一个稳定结果。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/observability/tests/Lumio.Client.Observability.Tests.csproj
```

### Task 11: 定义 Connection 公共端口、Generation 与单终态状态机

- **Task card:** `.spec/tasks/w2-connection-contract-and-generation.md`
- **Wave:** `2A`
- **依赖:** `w1-observability-bounded-dispatch`

#### 涉及范围

- `modules/connection/src/Public/IClientConnectionFactory.cs`
- `modules/connection/src/Public/IClientConnection.cs`
- `modules/connection/src/Public/ITransportFaultPolicy.cs`
- `modules/connection/src/Public/ConnectionGeneration.cs`
- `modules/connection/src/Public/ConnectionEvent.cs`
- `modules/connection/src/Internal/State/ConnectionStateMachine.cs`
- `modules/connection/tests/Unit/ConnectionStateMachineTests.cs`
- `modules/connection/tests/Fault/ConnectionCloseRaceTests.cs`

#### 接口

**Consumes**

- observability writer
- 生成 EncodedEnvelope/CloseReason

**Produces**

- IClientConnectionFactory
- IClientConnection
- ConnectionGeneration/event state machine

#### Step 1：先写失败测试

- `ConnectionStateMachineTests.GenerationIsImmutableAfterCreate`
- `ConnectionCloseRaceTests.CloseDisconnectSuccess_EmitsOneTerminal`
- `LateGenerationTests.G1Callback_CannotReachG2`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写 generation/终态竞态测试。
2. 创建接口和值类型；状态只由 Owner command/queued completion 推进。
3. 所有底层 completion 必须带 generation；迟到只计数。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/connection/tests/Lumio.Client.Connection.Tests.csproj --filter Connection
```

### Task 12: 实现有界 RawInputSample ingress 与 accepted-only InputSampleSeq

- **Task card:** `.spec/tasks/w2-input-sample-ingress.md`
- **Wave:** `2A`
- **依赖:** `w1-observability-bounded-dispatch`

#### 涉及范围

- `modules/input/src/Public/IInputSampleIngress.cs`
- `modules/input/src/Public/InputSampleSeq.cs`
- `modules/input/src/Public/RawInputSample.cs`
- `modules/input/src/Public/SequencedInputSample.cs`
- `modules/input/src/Public/InputEnqueueReceipt.cs`
- `modules/input/src/Internal/InputSampleQueue.cs`
- `modules/input/src/Internal/InputSampleSequenceAllocator.cs`
- `modules/input/tests/Unit/InputSampleIngressTests.cs`
- `modules/input/tests/Property/InputSequenceProperties.cs`

#### 接口

**Consumes**

- observability writer

**Produces**

- IInputSampleIngress
- InputSampleSeq semantics
- bounded sample queue

#### Step 1：先写失败测试

- `InputSampleIngressTests.AcceptedSamples_GetStrictlyIncreasingSeq`
- `InputSampleIngressTests.QueueFull_DoesNotAdvanceSequence`
- `InputSequenceProperties.AcceptedSequenceNeverRepeats`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先用容量 1/并发 producer 测试红灯。
2. Channels TryWrite 成功后提交 seq；失败路径恢复/不消耗。
3. 快照暴露深度和 high watermark，不暴露 Channel。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/input/tests/Lumio.Client.Input.Tests.csproj --filter InputSampleIngress
```

### Task 13: 实现生产 Codec 路径的 LocalEmbedded 双向字节端点

- **Task card:** `.spec/tasks/w2-connection-localembedded-transport.md`
- **Wave:** `2B`
- **依赖:** `w2-connection-contract-and-generation,w0-create-test-fixture-layout`

#### 涉及范围

- `modules/connection/src/Internal/Transport/LocalEmbedded/LocalEmbeddedTransport.cs`
- `modules/connection/src/Internal/Transport/LocalEmbedded/LocalEmbeddedEndpointPair.cs`
- `modules/connection/src/Internal/Protocol/GeneratedEnvelopeCodecAdapter.cs`
- `modules/connection/src/Internal/Protocol/ReplayWindow.cs`
- `modules/connection/tests/Contract/LocalEmbeddedTransportTests.cs`
- `modules/connection/tests/Contract/GeneratedEnvelopeCodecAdapterFixtureTests.cs`

#### 接口

**Consumes**

- w2-connection-contract-and-generation
- 上游 generated codec/fixtures

**Produces**

- LocalEmbedded transport
- generated codec adapter
- replay window

#### Step 1：先写失败测试

- `LocalEmbeddedTransportTests.TypedShortcut_IsImpossibleAndCodecRuns`
- `LocalEmbeddedTransportTests.Send_DoesNotSynchronouslyReenterReceiver`
- `GeneratedEnvelopeCodecAdapterFixtureTests.ValidInvalidVectors`
- `ConnectionReplayWindowTests.DuplicateAndOutOfOrder_FollowContract`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先用 spy codec 证明 Encode 与 Decode 都被调用。
2. 两个方向各自使用有界 byte queue；发送只入队，不同步调用对端。
3. decode/replay 结果转换为 ConnectionEvent；无共享 World/typed object。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/connection/tests/Lumio.Client.Connection.Tests.csproj --filter LocalEmbedded
```

### Task 14: 实现按序 drain、Game Mapper 与 generation-scoped buffer policy

- **Task card:** `.spec/tasks/w2-input-deterministic-mapping.md`
- **Wave:** `2B`
- **依赖:** `w2-input-sample-ingress,w0-map-upstream-runtime-contract-apis`

#### 涉及范围

- `modules/input/src/Public/IInputCommandSource.cs`
- `modules/input/src/Public/IGameInputMapper.cs`
- `modules/input/src/Public/InputDrainContext.cs`
- `modules/input/src/Public/InputBufferPolicy.cs`
- `modules/input/src/Internal/InputCommandSource.cs`
- `modules/input/src/Internal/InputBufferPolicyState.cs`
- `modules/input/tests/Unit/InputCommandSourceTests.cs`
- `modules/input/tests/Unit/InputBufferPolicyTests.cs`
- `modules/input/tests/Contract/InputMappingFixtureTests.cs`
- `modules/input/tests/Fault/InputQueueFaultTests.cs`

#### 接口

**Consumes**

- w2-input-sample-ingress
- 生成 CandidateGameplayCommand

**Produces**

- IInputCommandSource
- IGameInputMapper
- Candidate stream without ClientCommandSeq

#### Step 1：先写失败测试

- `InputCommandSourceTests.MapperInvokedInSampleOrder`
- `InputCommandSourceTests.Candidate_DoesNotAllocateClientCommandSeq`
- `InputBufferPolicyTests.ResyncPolicy_IsGenerationScoped`
- `InputMappingFixtureTests.GeneratedVectors_AreDeterministic`
- `InputQueueFaultTests.MapperThrows_CurrentSampleRejectedByPolicy`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写 mapper 顺序与禁止命令序号测试。
2. Owner Thread drain Span；每个 accepted sample 调 mapper 一次。
3. 旧 generation policy/sample 明确丢弃并发事件；异常不越过端口。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/input/tests/Lumio.Client.Input.Tests.csproj --filter "InputCommandSource|InputBufferPolicy|InputMapping"
```

### Task 15: 补齐收发有界队列、Fault Decorator 与关闭/满载矩阵

- **Task card:** `.spec/tasks/w2-connection-bounded-queues-and-faults.md`
- **Wave:** `2C`
- **依赖:** `w2-connection-localembedded-transport`

#### 涉及范围

- `modules/connection/src/Internal/Queues/ConnectionEventQueue.cs`
- `modules/connection/src/Internal/Queues/ConnectionSendQueue.cs`
- `modules/connection/src/Internal/Faults/FaultDecoratingTransport.cs`
- `modules/connection/src/Internal/Faults/DeterministicDelayQueue.cs`
- `modules/connection/tests/Fault/ConnectionQueueFullTests.cs`
- `modules/connection/tests/Fault/TransportFaultDecoratorTests.cs`
- `modules/connection/tests/Fault/ConnectionCloseRaceTests.cs`

#### 接口

**Consumes**

- w2-connection-localembedded-transport

**Produces**

- bounded ingress/egress
- deterministic fault decorator

#### Step 1：先写失败测试

- `ConnectionQueueFullTests.IngressFull_NeverOverwritesValidatedFrame`
- `ConnectionQueueFullTests.EgressFull_ReturnsBeforeBlocking`
- `TransportFaultDecoratorTests.DropDuplicateDelayDisconnect_AreDeterministic`
- `ConnectionCloseRaceTests.CloseDisconnectSuccess_EmitsOneTerminal`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写 capacity=1 的 ingress/egress 测试。
2. Fault policy seed/sequence 固化；装饰字节/事件路径，不改协议。
3. Close 完成队列并生成一次 terminal；late callback 只记录。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/connection/tests/Lumio.Client.Connection.Tests.csproj --filter "QueueFull|Fault|CloseRace"
```

### Task 16: 定义 Handshake 端口、Attempt、Phase 与异步 completion 回流

- **Task card:** `.spec/tasks/w3-handshake-contract-and-attempt.md`
- **Wave:** `3A`
- **依赖:** `w2-connection-bounded-queues-and-faults`

#### 涉及范围

- `modules/handshake/src/Public/IClientHandshake.cs`
- `modules/handshake/src/Public/IClientHandshakeFactory.cs`
- `modules/handshake/src/Public/IPlatformCapabilityProvider.cs`
- `modules/handshake/src/Public/HandshakeAttemptId.cs`
- `modules/handshake/src/Public/HandshakeOutcome.cs`
- `modules/handshake/src/Internal/HandshakeStateMachine.cs`
- `modules/handshake/src/Internal/CapabilityCompletionQueue.cs`
- `modules/handshake/tests/Unit/HandshakeStateMachineTests.cs`
- `modules/handshake/tests/Fault/HandshakeRaceTests.cs`

#### 接口

**Consumes**

- IClientConnection
- observability

**Produces**

- IClientHandshake
- IPlatformCapabilityProvider
- attempt state machine

#### Step 1：先写失败测试

- `HandshakeStateMachineTests.Accepted_RequiresCapabilityAndValidServerHello`
- `HandshakeAttemptGenerationTests.LateCapabilityCompletion_Dropped`
- `HandshakeRaceTests.CancelDisconnectAccepted_PriorityIsDeterministic`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写 accepted gate/late attempt/cancel race 测试。
2. Begin/HandleFrame/Poll/Cancel 都只在 Owner Thread 修改状态。
3. capability async completion 入有界 queue，带 attempt+generation。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/handshake/tests/Lumio.Client.Handshake.Tests.csproj --filter Handshake
```

### Task 17: 绑定生成 Handshake 消息、Codec Fixture 与阶段验证

- **Task card:** `.spec/tasks/w3-handshake-generated-contract-adapter.md`
- **Wave:** `3B`
- **依赖:** `w3-handshake-contract-and-attempt,w0-create-test-fixture-layout`

#### 涉及范围

- `modules/handshake/src/Internal/GeneratedHandshakeAdapter.cs`
- `modules/handshake/src/Internal/GeneratedHandshakeMessageGate.cs`
- `modules/handshake/tests/Contract/GeneratedHandshakeFixtureTests.cs`
- `modules/handshake/tests/Contract/HandshakePhaseFixtureTests.cs`

#### 接口

**Consumes**

- w3-handshake-contract-and-attempt
- 上游 hello/reject fixtures

**Produces**

- generated handshake adapter
- phase gate

#### Step 1：先写失败测试

- `GeneratedHandshakeFixtureTests.ValidAndInvalidVectors`
- `HandshakePhaseFixtureTests.OutOfPhaseMessageHasZeroStateAdvance`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先载入 valid/invalid corpus 建立失败测试。
2. 只调用生成 validator/serializer；不复制字段。
3. phase/generation/schema 不通过时不调用 capability/scope/runtime。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/handshake/tests/Lumio.Client.Handshake.Tests.csproj --filter Fixture
```

### Task 18: 实现平台 Capability 汇合与稳定 Reject 矩阵

- **Task card:** `.spec/tasks/w3-handshake-capability-and-rejects.md`
- **Wave:** `3B`
- **依赖:** `w3-handshake-generated-contract-adapter`

#### 涉及范围

- `modules/handshake/src/Internal/HandshakeRejectClassifier.cs`
- `modules/handshake/src/Internal/CapabilityNegotiationCoordinator.cs`
- `modules/handshake/src/Public/DefaultClientHandshakeFactory.cs`
- `modules/handshake/tests/Unit/CapabilityProviderTests.cs`
- `modules/handshake/tests/Contract/HandshakeRejectTests.cs`
- `modules/handshake/tests/Fault/HandshakeRaceTests.cs`

#### 接口

**Consumes**

- w3-handshake-generated-contract-adapter

**Produces**

- accepted/rejected HandshakeOutcome
- zero-side-effect reject

#### Step 1：先写失败测试

- `CapabilityProviderTests.Unavailable_ReturnsStableCapabilityReject`
- `HandshakeRejectTests.ReleaseSchemaAbiRoleClaims_MatchMatrix`
- `HandshakeStateMachineTests.Reject_HasZeroScopeAndWorldCalls`
- `HandshakeRaceTests.QueueFull_DoesNotAdvanceSentPhase`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写每个矩阵行与零副作用测试。
2. classifier 只映射生成 ErrorCode；未知值按上游规则拒绝。
3. Accepted 需 protocol + capability 两边完成；QueueFull/Cancel 优先级固定。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/handshake/tests/Lumio.Client.Handshake.Tests.csproj
```

### Task 19: 定义 Replica Stage/Discard/Observe 公共端口与无 Commit 约束

- **Task card:** `.spec/tasks/w4-replica-stage-contract.md`
- **Wave:** `4A`
- **依赖:** `w1-observability-bounded-dispatch,w0-map-upstream-runtime-contract-apis`

#### 涉及范围

- `modules/replica/src/Public/IClientReplica.cs`
- `modules/replica/src/Public/IReplicaMapper.cs`
- `modules/replica/src/Public/IClientReplicaFactory.cs`
- `modules/replica/src/Public/ReplicaStageHandle.cs`
- `modules/replica/src/Public/ReplicaStageRequest.cs`
- `modules/replica/src/Internal/ReplicaStageLedger.cs`
- `modules/replica/tests/Unit/ReplicaStageTests.cs`
- `modules/replica/tests/Architecture/ReplicaPublicApiTests.cs`

#### 接口

**Consumes**

- Runtime ReplicaApplyPlan alias
- generated authority update

**Produces**

- IClientReplica
- IReplicaMapper
- stage ledger

#### Step 1：先写失败测试

- `ReplicaStageTests.Stage_HasNoVisibleMetadataMutation`
- `ReplicaPublicApiTests.NoCommitMethodAndNoPredictionReference`
- `ReplicaStageTests.SecondStageCanBeDiscardedWithoutRuntimeCall`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写 API 形状和 stage 无副作用测试。
2. 实现 opaque handle/generation ledger；Stage 只保存最小证据。
3. Architecture test 拒绝 Commit 方法、Runtime implementation、Prediction reference。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/replica/tests/Lumio.Client.Replica.Tests.csproj --filter "ReplicaStage|PublicApi"
```

### Task 20: 实现 Prediction local stage、commit-only 序号与有界历史

- **Task card:** `.spec/tasks/w4-prediction-sequence-history.md`
- **Wave:** `4A`
- **依赖:** `w1-observability-bounded-dispatch,w0-map-upstream-runtime-contract-apis`

#### 涉及范围

- `modules/prediction/src/Public/IClientPrediction.cs`
- `modules/prediction/src/Public/ClientCommandSeq.cs`
- `modules/prediction/src/Public/PredictionKey.cs`
- `modules/prediction/src/Public/PredictionCandidateStage.cs`
- `modules/prediction/src/Internal/PredictionSequenceAllocator.cs`
- `modules/prediction/src/Internal/PredictionHistory.cs`
- `modules/prediction/src/Internal/PredictionStageLedger.cs`
- `modules/prediction/tests/Unit/PredictionCandidateTests.cs`
- `modules/prediction/tests/Property/PredictionSequenceProperties.cs`

#### 接口

**Consumes**

- CandidateGameplayCommand
- Runtime LocalPredictionPlan/outcome

**Produces**

- IClientPrediction local path
- ClientCommandSeq/PredictionKey ownership
- bounded history

#### Step 1：先写失败测试

- `PredictionCandidateTests.RejectedCandidate_DoesNotConsumeClientCommandSeq`
- `PredictionCandidateTests.LocalAborted_DoesNotConsumeOrEnterHistory`
- `PredictionCandidateTests.LocalCommitted_AssignsSeqAndKeyOnce`
- `PredictionSequenceProperties.AcceptedSequencesStrictlyIncrease`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写三条序号边界测试。
2. AcceptCandidate 创建 stage/plan；不分配 seq/key。
3. Observe committed 原子分配并入历史；aborted/invalid 不修改。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/prediction/tests/Lumio.Client.Prediction.Tests.csproj --filter "PredictionCandidate|PredictionSequence"
```

### Task 21: 实现 baseline/revision、duplicate/gap/tombstone 与 committed-only 推进

- **Task card:** `.spec/tasks/w4-replica-baseline-gap-metadata.md`
- **Wave:** `4B`
- **依赖:** `w4-replica-stage-contract`

#### 涉及范围

- `modules/replica/src/Internal/ReplicaMetadataState.cs`
- `modules/replica/src/Internal/ReplicaGapDetector.cs`
- `modules/replica/src/Internal/TombstoneEvidence.cs`
- `modules/replica/src/Public/ReplicaCommittedMetadata.cs`
- `modules/replica/tests/Unit/ReplicaMetadataTests.cs`
- `modules/replica/tests/Unit/ReplicaGapDetectorTests.cs`
- `modules/replica/tests/Property/ReplicaSequenceProperties.cs`
- `modules/replica/tests/Fault/ReplicaOutcomeFaultTests.cs`

#### 接口

**Consumes**

- w4-replica-stage-contract

**Produces**

- gap/result classification
- committed metadata state

#### Step 1：先写失败测试

- `ReplicaStageTests.Gap_ReturnsRequiresResyncAndNeverCallsMapper`
- `ReplicaMetadataTests.CommittedAdvances_AbortedDoesNot`
- `ReplicaMetadataTests.IndeterminateFreezesAndRetainsEvidence`
- `ReplicaGapDetectorTests.DuplicateGapTombstone_MatchFixture`
- `ReplicaSequenceProperties.CommittedWatermarkNeverRegresses`
- `ReplicaOutcomeFaultTests.StaleStage_CannotAdvanceMetadata`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先构造 duplicate/gap/stale/indeterminate matrix。
2. gap detector 在 mapper 前执行；RequiresResync 无 plan。
3. 只有 matching stage + committed receipt 推进；indeterminate 冻结。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/replica/tests/Lumio.Client.Replica.Tests.csproj --filter "Metadata|Gap|Sequence|Outcome"
```

### Task 22: 绑定 FullSnapshot/Delta 生成向量与 Runtime Replica plan mapper

- **Task card:** `.spec/tasks/w4-replica-fullsnapshot-fixtures.md`
- **Wave:** `4B`
- **依赖:** `w4-replica-baseline-gap-metadata,w0-create-test-fixture-layout`

#### 涉及范围

- `modules/replica/src/Internal/GeneratedReplicaAdapter.cs`
- `modules/replica/src/Internal/RuntimeReplicaPlanAdapter.cs`
- `modules/replica/tests/Contract/ReplicaGeneratedFixtureTests.cs`
- `modules/replica/tests/Contract/ReplicaMapperContractTests.cs`

#### 接口

**Consumes**

- w4-replica-baseline-gap-metadata
- upstream fixtures/runtime API map

**Produces**

- contract→Runtime plan adapter

#### Step 1：先写失败测试

- `ReplicaGeneratedFixtureTests.FullSnapshotDeltaInvalidVectors`
- `ReplicaMapperContractTests.ValidUpdateProducesOneImmutableRuntimePlan`
- `ReplicaMapperContractTests.InvalidFixtureHasZeroRuntimePlan`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先加载 FullSnapshot/Delta valid/invalid corpus。
2. 生成 validator 先行，Game/Runtime mapper 后行。
3. 不复制 snapshot schema，不保存 ECS/Replica storage。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/replica/tests/Lumio.Client.Replica.Tests.csproj --filter "Fixture|MapperContract"
```

### Task 23: 实现 Confirmation/Correction authority stage 与 Replica plan 可组合合同

- **Task card:** `.spec/tasks/w4-prediction-authority-stage.md`
- **Wave:** `4B`
- **依赖:** `w4-prediction-sequence-history,w0-create-test-fixture-layout`

#### 涉及范围

- `modules/prediction/src/Public/PredictionAuthorityStage.cs`
- `modules/prediction/src/Internal/GeneratedPredictionAdapter.cs`
- `modules/prediction/src/Internal/RuntimePredictionPlanAdapter.cs`
- `modules/prediction/tests/Unit/PredictionAuthorityStageTests.cs`
- `modules/prediction/tests/Contract/PredictionGeneratedFixtureTests.cs`
- `modules/prediction/tests/Unit/PredictionHistoryTests.cs`

#### 接口

**Consumes**

- w4-prediction-sequence-history
- Runtime authority outcome/generated correction

**Produces**

- PredictionReconcilePlan stage
- commit-only history prune

#### Step 1：先写失败测试

- `PredictionAuthorityStageTests.Stage_HasNoHistoryMutation`
- `PredictionAuthorityStageTests.CorrectionPlan_ComposesWithReplicaPlan`
- `PredictionGeneratedFixtureTests.ConfirmationCorrectionVectors`
- `PredictionHistoryTests.ConfirmationPrunesOnlyAfterAuthorityCommit`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写 stage 无历史副作用与 plan 组合测试。
2. StageAuthority 只验证并生成 reconcile plan。
3. Observe committed 才 prune；aborted 保留 history。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/prediction/tests/Lumio.Client.Prediction.Tests.csproj --filter "AuthorityStage|GeneratedFixture|History"
```

### Task 24: 补齐 Prediction window 背压、stale stage 与 indeterminate fault

- **Task card:** `.spec/tasks/w4-prediction-window-faults.md`
- **Wave:** `4B`
- **依赖:** `w4-prediction-authority-stage`

#### 涉及范围

- `modules/prediction/src/Internal/PredictionWindowPolicy.cs`
- `modules/prediction/src/Public/PredictionSnapshot.cs`
- `modules/prediction/tests/Unit/PredictionWindowTests.cs`
- `modules/prediction/tests/Fault/PredictionOutcomeFaultTests.cs`
- `modules/prediction/tests/Architecture/PredictionPublicApiTests.cs`

#### 接口

**Consumes**

- w4-prediction-authority-stage

**Produces**

- bounded prediction window
- fault evidence

#### Step 1：先写失败测试

- `PredictionWindowTests.Full_ReturnsExplicitBackpressureWithoutSequenceUse`
- `PredictionOutcomeFaultTests.IndeterminateFreezesHistory`
- `PredictionPublicApiTests.NoCommitMethodAndNoReplicaReference`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先用最小 window 注入 full/stale/indeterminate。
2. full 只返回显式结果；不丢已提交 history。
3. indeterminate 冻结新 candidate/authority stage 并通知 Session fault。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/prediction/tests/Lumio.Client.Prediction.Tests.csproj --filter "Window|OutcomeFault|PublicApi"
```

### Task 25: 定义 Session 公共端口、依赖包与 Scope/双 Handle 资源 ledger

- **Task card:** `.spec/tasks/w5-session-contract-and-resource-ledger.md`
- **Wave:** `5A`
- **依赖:** `w4-replica-fullsnapshot-fixtures,w4-prediction-window-faults,w3-handshake-capability-and-rejects,w2-input-deterministic-mapping,w1-persistence-contract-surface`

#### 涉及范围

- `modules/session/src/Public/IClientSession.cs`
- `modules/session/src/Public/IClientSessionFactory.cs`
- `modules/session/src/Public/IClientGameplayScopeActivator.cs`
- `modules/session/src/Public/IClientPresentationSink.cs`
- `modules/session/src/Public/ClientSessionDependencies.cs`
- `modules/session/src/Public/ClientSessionState.cs`
- `modules/session/src/Internal/Lifecycle/RuntimeHandleLedger.cs`
- `modules/session/src/Internal/Lifecycle/SessionResourceLedger.cs`
- `modules/session/tests/Unit/RuntimeHandleLedgerTests.cs`
- `modules/session/tests/Architecture/SessionPublicApiTests.cs`

#### 接口

**Consumes**

- all leaf public ports
- Runtime handle API map

**Produces**

- IClientSession
- IClientGameplayScopeActivator
- IClientPresentationSink
- resource ledgers

#### Step 1：先写失败测试

- `RuntimeHandleLedgerTests.CreateEcsThenVoxel_DestroyVoxelThenEcs`
- `RuntimeHandleLedgerTests.VoxelCreateFailureDestroysEcs`
- `SessionPublicApiTests.NoUnityHybridClrGameImplementationTypes`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写 handle order/rollback 与 API leakage 测试。
2. 依赖通过 `ClientSessionDependencies` 显式注入。
3. ledger 登记每一步，关闭按冻结顺序幂等释放。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/session/tests/Lumio.Client.Session.Tests.csproj --filter "RuntimeHandleLedger|SessionPublicApi"
```

### Task 26: 实现 bounded Session inbox、Generation 过滤与事件优先级

- **Task card:** `.spec/tasks/w5-session-event-arbiter.md`
- **Wave:** `5B`
- **依赖:** `w5-session-contract-and-resource-ledger`

#### 涉及范围

- `modules/session/src/Internal/Events/SessionEvent.cs`
- `modules/session/src/Internal/Events/SessionEventInbox.cs`
- `modules/session/src/Internal/Events/SessionEventArbiter.cs`
- `modules/session/src/Internal/State/SessionStateMachine.cs`
- `modules/session/tests/Unit/SessionEventArbiterTests.cs`
- `modules/session/tests/Unit/SessionStateMachineTests.cs`
- `modules/session/tests/Fault/SessionRaceTests.cs`

#### 接口

**Consumes**

- w5-session-contract-and-resource-ledger
- observability critical queue semantics

**Produces**

- priority arbiter
- frozen transition table

#### Step 1：先写失败测试

- `SessionEventArbiterTests.CancelBeatsRejectDisconnectAndSuccess`
- `SessionEventArbiterTests.FaultBeatsCloseAndCommitted`
- `SessionRaceTests.LateG1Success_CannotActivateG2`
- `SessionRaceTests.QueueFullCloseFault_TerminalIsDeterministic`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先把第 11 节每个关键 transition 变成 parameterized test。
2. 生产者只 enqueue immutable event；Owner Tick 统一 drain。
3. 先丢 late generation，再按 priority+sequence 归并，终态一次。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/session/tests/Lumio.Client.Session.Tests.csproj --filter "EventArbiter|StateMachine|SessionRace"
```

### Task 27: 实现 phase/generation/schema/permission 消息矩阵与零副作用拒绝

- **Task card:** `.spec/tasks/w5-session-active-message-gate.md`
- **Wave:** `5B`
- **依赖:** `w5-session-contract-and-resource-ledger,w0-create-test-fixture-layout`

#### 涉及范围

- `modules/session/src/Internal/Gates/ActiveMessageGate.cs`
- `modules/session/src/Internal/Gates/GameplayScopeActivationGate.cs`
- `modules/session/src/Internal/Config/ClientConfigStagingArea.cs`
- `modules/session/tests/Unit/SessionMessageGateTests.cs`
- `modules/session/tests/Unit/GameplayScopeActivationGateTests.cs`
- `modules/session/tests/Contract/SessionMessageMatrixFixtureTests.cs`

#### 接口

**Consumes**

- w5-session-contract-and-resource-ledger
- generated message matrix

**Produces**

- ActiveMessageGate
- Scope activation gate
- Config staging barrier

#### Step 1：先写失败测试

- `SessionMessageGateTests.InvalidMatrix_HasZeroLeafCalls`
- `GameplayScopeActivationGateTests.ScopeMustActivateBeforeWorldHandles`
- `SessionMessageMatrixFixtureTests.GeneratedValidInvalidRowsMatch`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先对每个 invalid matrix 行断言 leaf call count=0。
2. 验证顺序固定：generation→phase→schema→permission→payload。
3. Config 只进入 staging，Scope 只在 Owner Tick barrier 激活。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/session/tests/Lumio.Client.Session.Tests.csproj --filter "MessageGate|ScopeActivation|MessageMatrix"
```

### Task 28: 实现首次连接：Connection→Handshake→Scope→Config→双 Handle→FullSnapshot

- **Task card:** `.spec/tasks/w5-session-first-connect-orchestration.md`
- **Wave:** `5C`
- **依赖:** `w5-session-event-arbiter,w5-session-active-message-gate`

#### 涉及范围

- `modules/session/src/Internal/Orchestration/FirstConnectOrchestrator.cs`
- `modules/session/src/Internal/Orchestration/HandshakeOrchestrator.cs`
- `modules/session/src/Internal/Orchestration/ScopeAndRuntimeActivationOrchestrator.cs`
- `modules/session/tests/Unit/SessionStateMachineTests.cs`
- `modules/session/tests/Integration/FirstConnectOrchestratorTests.cs`

#### 接口

**Consumes**

- w5-session-event-arbiter
- w5-session-active-message-gate
- connection/handshake/runtime fakes

**Produces**

- first-connect orchestration through Synchronizing

#### Step 1：先写失败测试

- `SessionStateMachineTests.HappyPath_ConnectToActive`
- `SessionStateMachineTests.ScopeMustActivateBeforeWorldHandles`
- `FirstConnectOrchestratorTests.RejectHasZeroScopeAndHandleCalls`
- `FirstConnectOrchestratorTests.VoxelHandleFailureRollsBackEcsThenScope`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写 happy/reject/每个资源步骤失败测试。
2. Connection/Handshake 事件只经 inbox；Accepted 后才准备 Scope。
3. Config staging→barrier activation→ECS→Voxel→FullSnapshot，失败逆序。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/session/tests/Lumio.Client.Session.Tests.csproj --filter "FirstConnect|HappyPath|ScopeMust"
```

### Task 29: 实现 Replica+Prediction+Config 的单一权威 Runtime 事务与 Ack/Diff 顺序

- **Task card:** `.spec/tasks/w5-session-authority-transaction-orchestration.md`
- **Wave:** `5C`
- **依赖:** `w5-session-active-message-gate,w4-replica-fullsnapshot-fixtures,w4-prediction-window-faults`

#### 涉及范围

- `modules/session/src/Internal/Orchestration/AuthorityUpdateOrchestrator.cs`
- `modules/session/src/Internal/Orchestration/LocalPredictionOrchestrator.cs`
- `modules/session/src/Internal/Orchestration/AuthorityStageBundle.cs`
- `modules/session/tests/Unit/AuthorityUpdateOrchestratorTests.cs`
- `modules/session/tests/Unit/LocalPredictionOrchestratorTests.cs`

#### 接口

**Consumes**

- w5-session-active-message-gate
- replica/prediction stage ports
- Runtime transaction port

**Produces**

- single authority transaction pipeline
- single local prediction transaction pipeline

#### Step 1：先写失败测试

- `AuthorityUpdateOrchestratorTests.Committed_MetadataAckDiffOrder`
- `AuthorityUpdateOrchestratorTests.Aborted_NoMetadataAckOrDiff`
- `AuthorityUpdateOrchestratorTests.SecondStageFails_FirstStageDiscarded`
- `LocalPredictionOrchestratorTests.CommandSequenceAllocatedOnlyAfterRuntimeCommit`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写调用序列 spy tests。
2. Replica Stage 成功后 Prediction Stage；任一失败 discard 已有 stage。
3. Runtime 只调用一次；committed 后 observe→metadata→Ack→Diff，aborted 无后续。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/session/tests/Lumio.Client.Session.Tests.csproj --filter "AuthorityUpdate|LocalPrediction"
```

### Task 30: 实现 Gap/Resync 与 Disconnect/Reconnect 的不同路径

- **Task card:** `.spec/tasks/w5-session-resync-reconnect.md`
- **Wave:** `5D`
- **依赖:** `w5-session-first-connect-orchestration,w5-session-authority-transaction-orchestration`

#### 涉及范围

- `modules/session/src/Internal/Orchestration/ResyncOrchestrator.cs`
- `modules/session/src/Internal/Orchestration/ReconnectOrchestrator.cs`
- `modules/session/src/Internal/State/SessionGenerationAllocator.cs`
- `modules/session/tests/Integration/ResyncReconnectTests.cs`
- `modules/session/tests/Fault/SessionRaceTests.cs`

#### 接口

**Consumes**

- w5-session-first-connect-orchestration
- w5-session-authority-transaction-orchestration

**Produces**

- same-connection Resync
- new-generation Reconnect

#### Step 1：先写失败测试

- `ResyncReconnectTests.Resync_DoesNotHandshake`
- `ResyncReconnectTests.Reconnect_NewGenerationReauthAndHandshake_NoResume`
- `SessionRaceTests.LateG1Success_CannotActivateG2`
- `ResyncReconnectTests.ResyncInputPolicyIsGenerationScoped`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写 handshake-call-count 与 no-resume assertions。
2. Gap 保持 connection/handshake/scope，暂停普通 input，申请 FullSnapshot。
3. Disconnect 销毁旧代次资源，G+1 完整认证/握手/scope/handles；迟到 G 丢弃。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/session/tests/Lumio.Client.Session.Tests.csproj --filter ResyncReconnect
```

### Task 31: 实现 Close/Fault 逆序释放、幂等终态与迟到结果封锁

- **Task card:** `.spec/tasks/w5-session-close-fault-release.md`
- **Wave:** `5E`
- **依赖:** `w5-session-event-arbiter,w5-session-first-connect-orchestration`

#### 涉及范围

- `modules/session/src/Internal/Orchestration/CloseOrchestrator.cs`
- `modules/session/src/Internal/Lifecycle/SessionResourceLedger.cs`
- `modules/session/src/Internal/State/TerminalSessionState.cs`
- `modules/session/tests/Unit/SessionCloseReleaseTests.cs`
- `modules/session/tests/Fault/SessionRaceTests.cs`

#### 接口

**Consumes**

- w5-session-event-arbiter
- w5-session-first-connect-orchestration

**Produces**

- deterministic reverse release
- terminal evidence

#### Step 1：先写失败测试

- `SessionCloseReleaseTests.OrderIsInputPredictionReplicaVoxelEcsScopeHandshakeConnection`
- `SessionCloseReleaseTests.RepeatedCloseIsIdempotent`
- `SessionRaceTests.FaultBeatsCloseAndCommitted`
- `SessionRaceTests.LateCompletionCannotRecreateReleasedResource`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先用记录型 fake 断言完整释放序列。
2. Close 和 Fault 共享 ledger，但 Fault 先冻结并采集 evidence。
3. 每一步失败继续释放余项并记 partial failure；终态不复活。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/session/tests/Lumio.Client.Session.Tests.csproj --filter "CloseRelease|SessionRace"
```

### Task 32: 对权威事务每个故障点做零副作用/冻结证明

- **Task card:** `.spec/tasks/w5-authority-transaction-fault-matrix.md`
- **Wave:** `5F`
- **依赖:** `w5-session-authority-transaction-orchestration,w5-session-close-fault-release`

#### 涉及范围

- `modules/session/tests/Fault/AuthorityTransactionFaultMatrixTests.cs`
- `modules/session/tests/Fault/AuthorityTransactionFaultCases.cs`
- `tests/Lumio.Client.IntegrationTests/Faults/AuthorityTransactionFaultFixture.cs`
- `tests/Lumio.Client.IntegrationTests/Fakes/FakeClientRuntimePort.cs`

#### 接口

**Consumes**

- w5-session-authority-transaction-orchestration
- w5-session-close-fault-release

**Produces**

- fault matrix evidence

#### Step 1：先写失败测试

- `AuthorityTransactionFaultMatrixTests.EveryFaultPoint_PreservesContract`
- `AuthorityTransactionFaultMatrixTests.IndeterminateNeverAcksOrPresents`
- `AuthorityTransactionFaultMatrixTests.AbortDiscardsBothStages`
- `AuthorityTransactionFaultMatrixTests.CommitAdvancesExactlyOnce`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 枚举 message gate、replica stage、prediction stage、runtime before/after apply、observe、Ack、presentation 注入点。
2. 每个 case 断言 Runtime call count、metadata、history、Ack、Diff、state。
3. indeterminate 必须 Faulted 并形成 Failure Bundle 输入。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/session/tests/Lumio.Client.Session.Tests.csproj --filter AuthorityTransactionFaultMatrix
```

### Task 33: 实现最小 Headless Bot Host、Owner Tick 与稳定退出码

- **Task card:** `.spec/tasks/w6-bot-headless-host.md`
- **Wave:** `6A`
- **依赖:** `w5-authority-transaction-fault-matrix,w5-session-resync-reconnect`

#### 涉及范围

- `modules/bot/src/Public/IHeadlessBotHost.cs`
- `modules/bot/src/Public/IHeadlessBotHostFactory.cs`
- `modules/bot/src/Public/IBotScenarioDriver.cs`
- `modules/bot/src/Public/BotRunRequest.cs`
- `modules/bot/src/Public/BotRunResult.cs`
- `modules/bot/src/Internal/HeadlessBotHost.cs`
- `modules/bot/src/Internal/BotRunLoop.cs`
- `modules/bot/src/Internal/BotTerminalReducer.cs`
- `modules/bot/host/Program.cs`
- `modules/bot/host/BotHostComposition.cs`
- `modules/bot/tests/Unit/BotRunLoopTests.cs`
- `modules/bot/tests/Fault/BotCancellationRaceTests.cs`

#### 接口

**Consumes**

- IClientSession
- IInputSampleIngress
- IClientEventWriter

**Produces**

- IHeadlessBotHost
- Bot executable composition root

#### Step 1：先写失败测试

- `BotRunLoopTests.OrderIsFillEnqueueSessionTickObserve`
- `BotCancellationRaceTests.CancelAndSessionSuccess_OneTerminalResult`
- `BotQueueFullTests.InputAndCriticalQueueFull_AreDistinguished`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写 run loop 顺序与 terminal reducer 测试。
2. Generic Host 只负责进程生命周期；每个 Bot 有单 Owner loop。
3. CLI/取消转换为事件；退出码与 BotRunResult 稳定。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/bot/tests/Lumio.Client.Bot.Tests.csproj --filter "BotRunLoop|BotCancellation|BotQueueFull"
```

### Task 34: 实现 generated Fixture Driver 与可复现输入/故障脚本

- **Task card:** `.spec/tasks/w6-bot-deterministic-adapters.md`
- **Wave:** `6A`
- **依赖:** `w6-bot-headless-host,w0-create-test-fixture-layout`

#### 涉及范围

- `modules/bot/src/Internal/FixtureBotScenarioDriver.cs`
- `modules/bot/src/Internal/FixtureTickClock.cs`
- `modules/bot/src/Internal/FixtureFaultScript.cs`
- `modules/bot/host/CommandLine/BotCommandLine.cs`
- `modules/bot/tests/Unit/FixtureBotScenarioDriverTests.cs`
- `modules/bot/tests/Contract/BotSameProtocolFixtureTests.cs`

#### 接口

**Consumes**

- w6-bot-headless-host
- upstream fixture catalog

**Produces**

- deterministic scenario driver
- fixture CLI

#### Step 1：先写失败测试

- `FixtureBotScenarioDriverTests.SameSeedAndFixture_ProducesSameSamples`
- `BotRunLoopTests.DriverCannotCreateClientCommandSequence`
- `BotSameProtocolFixtureTests.BotCannotActivateWithoutHandshakeAndScopeGate`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写 same seed/same trace 与禁止命令序号测试。
2. Driver 只填 RawInputSample 与 fault schedule；不调用 leaf internals。
3. Fixture clock 显式产生 ClientOwnerTick；不使用 wallclock 作为权威。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/bot/tests/Lumio.Client.Bot.Tests.csproj --filter "FixtureBot|SameProtocol"
```

### Task 35: 跑通 Foundation 主路径与拒绝/重连证据

- **Task card:** `.spec/tasks/w6-foundation-exit-scenario.md`
- **Wave:** `6B`
- **依赖:** `w6-bot-deterministic-adapters`

#### 涉及范围

- `tests/Lumio.Client.IntegrationTests/Foundation/FoundationHappyPathTests.cs`
- `tests/Lumio.Client.IntegrationTests/Foundation/FoundationRejectTests.cs`
- `tests/Lumio.Client.IntegrationTests/Foundation/FoundationReconnectTests.cs`
- `tests/Lumio.Client.IntegrationTests/Foundation/FoundationFixtureComposition.cs`
- `tests/Lumio.Client.IntegrationTests/Foundation/FoundationTraceAssertions.cs`

#### 接口

**Consumes**

- w6-bot-deterministic-adapters
- all Wave 0-5 outputs

**Produces**

- Foundation exit evidence
- bot command `foundation`

#### Step 1：先写失败测试

- `FoundationHappyPathTests.HeadlessLocalEmbeddedConnectHandshakeSnapshotAckActiveGapResyncClose`
- `FoundationRejectTests.ReleaseRejectAndPermissionRejectHaveZeroRuntimeSideEffects`
- `FoundationReconnectTests.DisconnectCreatesNewGenerationAndFullHandshakeWithoutResume`
- `FoundationHappyPathTests.CriticalAndInputQueueFullAreDistinct`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先把冻结路径写成单一 trace assertion。
2. 用 production LocalEmbedded/Codec/queues 与 Fake Runtime；不手调 leaf internals。
3. 验证状态、调用顺序、generation、Ack、release、events 与退出码。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test tests/Lumio.Client.IntegrationTests/Lumio.Client.IntegrationTests.csproj -c Release --filter Category=Foundation
```

### Task 36: 证明 LocalEmbedded 真 Encode/Decode、无共享 World/重入且与 Remote 向量同源

- **Task card:** `.spec/tasks/w6-localembedded-fidelity-suite.md`
- **Wave:** `6B`
- **依赖:** `w6-foundation-exit-scenario`

#### 涉及范围

- `tests/Lumio.Client.IntegrationTests/Transport/LocalEmbeddedProtocolFidelityTests.cs`
- `tests/Lumio.Client.IntegrationTests/Transport/LocalEmbeddedIsolationTests.cs`
- `tests/Lumio.Client.IntegrationTests/Transport/ProtocolTraceRecorder.cs`

#### 接口

**Consumes**

- w6-foundation-exit-scenario
- w2-connection-localembedded-transport

**Produces**

- LocalEmbedded fidelity gate

#### Step 1：先写失败测试

- `LocalEmbeddedProtocolFidelityTests.EveryFrameRunsProductionEncodeAndDecode`
- `LocalEmbeddedIsolationTests.ClientAndServerDoNotShareWorldOrMutableBuffer`
- `LocalEmbeddedProtocolFidelityTests.SendNeverSynchronouslyReentersReceiver`
- `LocalEmbeddedProtocolFidelityTests.QueuePermissionAndTickRulesMatchFixture`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先为 codec call count、buffer identity、reentrancy depth 建立断言。
2. 记录 production trace，不读 typed payload shortcut。
3. fixture catalog 记录未来 Remote 使用同向量的合同。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test tests/Lumio.Client.IntegrationTests/Lumio.Client.IntegrationTests.csproj --filter LocalEmbeddedProtocolFidelity
```

### Task 37: 用项目引用与运行 Fixture 证明 Bot 不走简化协议

- **Task card:** `.spec/tasks/w6-bot-no-shortcut-policy.md`
- **Wave:** `6B`
- **依赖:** `w6-foundation-exit-scenario,w6-localembedded-fidelity-suite`

#### 涉及范围

- `modules/bot/tests/Architecture/BotPublicApiTests.cs`
- `tests/Lumio.Client.ArchitectureTests/References/BotReferencePolicyTests.cs`
- `tests/Lumio.Client.IntegrationTests/Foundation/BotPublicApiParityTests.cs`

#### 接口

**Consumes**

- w6-foundation-exit-scenario
- w6-localembedded-fidelity-suite

**Produces**

- Bot no-shortcut gate

#### Step 1：先写失败测试

- `BotPublicApiTests.ProjectReferencesMatchAllowlist`
- `BotReferencePolicyTests.BotHasNoLeafInternalOrGeneratedShortcutReference`
- `BotPublicApiParityTests.BotUsesSameSessionAndInputPortsAsUnityFixture`
- `BotSameProtocolFixtureTests.BotCannotActivateWithoutHandshakeAndScopeGate`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先在 test fixture 中制造 forbidden reference 并证明 checker 会失败。
2. 检查 csproj、assembly references 与 public construction path。
3. 运行时 trace 必须包含 Connect/Handshake/Scope/FullSnapshot，不能直接置 Active。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test tests/Lumio.Client.ArchitectureTests/Lumio.Client.ArchitectureTests.csproj --filter Bot && dotnet test tests/Lumio.Client.IntegrationTests/Lumio.Client.IntegrationTests.csproj --filter BotPublicApiParity
```

## 4. 退出条件

完成 Wave 0–6 后，以下命令必须全部通过：

```bash
dotnet restore LumioClient.slnx --locked-mode
dotnet format LumioClient.slnx --verify-no-changes --no-restore
dotnet build LumioClient.slnx -c Release --no-restore
dotnet test tests/Lumio.Client.ArchitectureTests/Lumio.Client.ArchitectureTests.csproj -c Release --no-build
dotnet test tests/Lumio.Client.IntegrationTests/Lumio.Client.IntegrationTests.csproj -c Release --no-build --filter "Category=Foundation"
dotnet run --project modules/bot/host/Lumio.Client.Bot.Host.csproj -c Release --no-build -- foundation --transport local-embedded --fixture foundation-happy-path
node .spec/tools/spec-lint.mjs
node --test .spec/tools/spec-lint.test.mjs
```

Foundation trace 必须覆盖：Headless → production LocalEmbedded Encode/Decode → Connect → Authentication/Handshake → Gameplay Scope gate → ECS/Voxel Handle → FullSnapshot → single Runtime authority transaction → BaselineAck → Active → Gap/same-connection Resync → Close。附加 case：Release Reject、permission reject、Input/Critical QueueFull、late generation、Disconnect→new-generation full reconnect、无 Resume Token、Bot 无捷径。

## 5. 恢复说明

本计划依据上一轮 Foundation 任务顺序、测试名、接口与退出 Gate 重新恢复。先前附件临时句柄失效，因此本副本不承诺逐字节一致，但保留相同 Wave 0–6 执行语义。

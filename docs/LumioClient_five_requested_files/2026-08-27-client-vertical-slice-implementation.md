# LumioClient Vertical Slice Implementation Plan

> **执行纪律：** 按依赖顺序逐卡执行；每张卡先写失败测试，再写最小实现。本文包含端口/测试草图，但它们是设计草图，不是本回合提交的生产代码。

**Goal:** 在 Foundation 通过后完成 Remote、verified persistence、Production observability、Unity Input/Presentation、可选 HybridCLR，并跑通完整客户端垂直切片。

**Architecture:** Wave 7 不改变 11 模块 DAG；所有平台与供应商能力留在 Adapter，Session 仍是唯一事务编排者，LocalEmbedded/Remote/Bot/Unity 共享公共端口与生成向量。

**Tech Stack:** Foundation 技术栈 + BCL File/Cryptography/System.Text.Json source generation、Socket/SslStream/Pipelines、Serilog、OpenTelemetry、Unity 6.3 LTS/Input System、官方 HybridCLR（通过 spike 后）。

## 1. 执行总则

- 一张任务只修改“涉及范围”列出的文件；发现邻卡需要同一路径时先串行化依赖，不并行写同文件。
- 每个 production API 先有 compilation/contract test；每个状态机先有 table-driven test；每个异步入口先有 cancel/late generation/QueueFull test。
- 上游真实类型无法解析时停止在 compile-only map，不创建本仓替代 Envelope/Transaction/ErrorCode。
- 任务完成证据包括：红灯命令、绿灯命令、架构检查、fixture/hash、必要时 failure trace。

## 2. Wave 概览

| Wave | 任务 | 并行纪律 |
| --- | --- | --- |
| 7A | `w7-persistence-artifact-ports`, `w7-connection-remote-transport`, `w7-observability-serilog-sink`, `w7-observability-opentelemetry-sink`, `w7-observability-failure-bundle-export`, `w7-unity-host-loop`, `w7-hybridclr-capability-provider` | 同一子 wave 的文件集在本计划中不重叠 |
| 7B | `w7-persistence-filesystem-adapter`, `w7-unity-input-system-adapter`, `w7-unity-presentation-adapter`, `w7-hybridclr-scope-loader` | 同一子 wave 的文件集在本计划中不重叠 |
| 7C | `w7-persistence-corruption-recovery`, `w7-hybridclr-rollback-unload`, `w7-unity-aot-device-matrix` | 同一子 wave 的文件集在本计划中不重叠 |
| 7D | `w7-vertical-slice-exit-scenario` | 同一子 wave 的文件集在本计划中不重叠 |

## 3. 任务明细

### Task 1: 补齐 verified Artifact/Checkpoint 结果、generation 与 integration fake

- **Task card:** `.spec/tasks/w7-persistence-artifact-ports.md`
- **Wave:** `7A`
- **依赖:** `w6-bot-no-shortcut-policy`

#### 涉及范围

- `modules/persistence/src/Public/VerifiedArtifactReadRequest.cs`
- `modules/persistence/src/Public/VerifiedArtifactReadResult.cs`
- `modules/persistence/src/Public/CheckpointReadRequest.cs`
- `modules/persistence/src/Public/CheckpointReadResult.cs`
- `modules/persistence/src/Public/CheckpointWriteRequest.cs`
- `modules/persistence/src/Public/CheckpointWriteResult.cs`
- `modules/persistence/src/Public/PersistenceSnapshot.cs`
- `modules/persistence/tests/Unit/VerifiedSessionArtifactSourceTests.cs`

#### 接口

**Consumes**

- w1-persistence-contract-surface
- upstream artifact/checkpoint types

**Produces**

- complete persistence port values

#### Step 1：先写失败测试

- `VerifiedSessionArtifactSourceTests.Read_ReturnsOnlyVerifiedArtifact`
- `VerifiedSessionArtifactSourceTests.LateGeneration_ResultCarriesOriginalGeneration`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写 verified-only 与 late-generation 结果测试。
2. 值类型不携带路径/Stream/DB connection。
3. Session integration fake 与 production adapter 使用同一 interface。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/persistence/tests/Lumio.Client.Persistence.Tests.csproj --filter VerifiedSessionArtifactSource
```

### Task 2: 实现 BCL Socket/SslStream/Pipelines Remote Adapter

- **Task card:** `.spec/tasks/w7-connection-remote-transport.md`
- **Wave:** `7A`
- **依赖:** `w6-bot-no-shortcut-policy,SPIKE-REMOTE-AOT`

#### 涉及范围

- `modules/connection/src/Internal/Transport/Remote/SocketPipelineTransport.cs`
- `modules/connection/src/Internal/Transport/Remote/SslChannelAuthenticator.cs`
- `modules/connection/src/Internal/Transport/Remote/RemoteEndpointParser.cs`
- `modules/connection/src/Internal/Transport/Remote/RemoteTransportOptions.cs`
- `modules/connection/tests/Contract/RemoteTransportContractTests.cs`
- `modules/connection/tests/Fault/RemoteTransportFaultTests.cs`
- `modules/connection/tests/Performance/RemoteTransportBudgetTests.cs`

#### 接口

**Consumes**

- w2-connection-bounded-queues-and-faults
- SPIKE-REMOTE-AOT

**Produces**

- Remote transport factory mode

#### Step 1：先写失败测试

- `RemoteTransportContractTests.LoopbackAndLocalEmbeddedProduceEquivalentProtocolTrace`
- `RemoteTransportFaultTests.CancelCloseAndLateCallbackEmitOneTerminalGeneration`
- `RemoteTransportFaultTests.ChannelAuthRejectsBeforeHandshake`
- `RemoteTransportBudgetTests.QueueFullAndCancellationCompleteWithinBudget`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先用 loopback server 重放与 LocalEmbedded 相同 fixtures。
2. 复用 generated codec、bounded queues、replay window、generation guard、fault decorator。
3. 通道认证在 Handshake 前完成；所有 connect/read/write/close 有有限取消预算。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/connection/tests/Lumio.Client.Connection.Tests.csproj --filter "RemoteTransport|RemoteTransportBudget"
```

### Task 3: 实现 Serilog 文件 Sink Adapter，不泄漏 supplier 类型

- **Task card:** `.spec/tasks/w7-observability-serilog-sink.md`
- **Wave:** `7A`
- **依赖:** `w6-bot-no-shortcut-policy`

#### 涉及范围

- `modules/observability/src/Internal/Adapters/Serilog/SerilogClientEventSink.cs`
- `modules/observability/src/Internal/Adapters/Serilog/SerilogSinkOptions.cs`
- `modules/observability/tests/Contract/SerilogClientEventSinkTests.cs`
- `modules/observability/tests/Fault/SerilogSinkFaultTests.cs`

#### 接口

**Consumes**

- w1-observability-bounded-dispatch

**Produces**

- Serilog IClientEventSink

#### Step 1：先写失败测试

- `SerilogClientEventSinkTests.GeneratedEventFieldsArePreserved`
- `SerilogClientEventSinkTests.PublicApiHasNoSerilogTypes`
- `SerilogSinkFaultTests.DiskFailureReturnsStableSinkResult`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写字段保真、供应商泄漏与磁盘失败测试。
2. 只在 Adapter 内构造 Serilog pipeline；事件 schema 不改名。
3. 外部异常转成 ClientEventSinkResult，由 dispatcher 处理。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/observability/tests/Lumio.Client.Observability.Tests.csproj --filter Serilog
```

### Task 4: 实现 OpenTelemetry Metrics/Trace Adapter 与降级

- **Task card:** `.spec/tasks/w7-observability-opentelemetry-sink.md`
- **Wave:** `7A`
- **依赖:** `w6-bot-no-shortcut-policy,SPIKE-OTEL-IL2CPP`

#### 涉及范围

- `modules/observability/src/Internal/Adapters/OpenTelemetry/OpenTelemetryClientEventSink.cs`
- `modules/observability/src/Internal/Adapters/OpenTelemetry/OpenTelemetryProjection.cs`
- `modules/observability/tests/Contract/OpenTelemetryProjectionTests.cs`
- `modules/observability/tests/Fault/OpenTelemetrySinkFaultTests.cs`

#### 接口

**Consumes**

- w1-observability-bounded-dispatch
- SPIKE-OTEL-IL2CPP

**Produces**

- OTel IClientEventSink

#### Step 1：先写失败测试

- `OpenTelemetryProjectionTests.EventIdAndCorrelationFieldsMapStably`
- `OpenTelemetryProjectionTests.PublicApiHasNoActivityMeterTypes`
- `OpenTelemetrySinkFaultTests.ExporterFailureDoesNotBlockOwnerThread`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写 projection 与非阻塞 exporter fault 测试。
2. Activity/Meter 只在 Adapter 内；生成 EventId/Correlation 为输入。
3. IL2CPP 不支持时 Composition Root 选择内存/Serilog，能力不虚报。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/observability/tests/Lumio.Client.Observability.Tests.csproj --filter OpenTelemetry
```

### Task 5: 聚合有界事件、Session/leaf snapshot 与缺失 provider 证据

- **Task card:** `.spec/tasks/w7-observability-failure-bundle-export.md`
- **Wave:** `7A`
- **依赖:** `w6-bot-no-shortcut-policy`

#### 涉及范围

- `modules/observability/src/Internal/FailureBundle/FailureBundleCoordinator.cs`
- `modules/observability/src/Internal/FailureBundle/FailureBundleProviderRegistry.cs`
- `modules/observability/src/Internal/FailureBundle/FailureBundleArchiveWriter.cs`
- `modules/observability/tests/Unit/FailureBundleCoordinatorTests.cs`
- `modules/observability/tests/Fault/FailureBundlePartialTests.cs`

#### 接口

**Consumes**

- w1-observability-memory-sink
- module snapshot ports

**Produces**

- Failure Bundle exporter

#### Step 1：先写失败测试

- `FailureBundleCoordinatorTests.ProviderOrderAndBudgetAreStable`
- `FailureBundlePartialTests.TimeoutOrThrowProducesPartialBundleWithMissingEntry`
- `FailureBundleCoordinatorTests.DoesNotBlockSessionOwnerThread`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写 provider 200ms 预算/throw/timeout partial bundle 测试。
2. coordinator 在后台抓不可变 snapshot，Owner Thread 只提供非阻塞快照。
3. archive 使用生成 Bundle schema/BCL 压缩；不把日志当业务权威。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/observability/tests/Lumio.Client.Observability.Tests.csproj --filter FailureBundle
```

### Task 6: 实现 Unity Main Thread Host、固定 Update 顺序与生命周期门

- **Task card:** `.spec/tasks/w7-unity-host-loop.md`
- **Wave:** `7A`
- **依赖:** `w6-bot-no-shortcut-policy,SPIKE-UNITY-63-AOT-MATRIX`

#### 涉及范围

- `modules/unity-adapter/src/Public/IUnityClientHost.cs`
- `modules/unity-adapter/src/Public/IUnityClientHostFactory.cs`
- `modules/unity-adapter/src/Public/UnityFrameContext.cs`
- `modules/unity-adapter/src/Internal/UnityClientHost.cs`
- `modules/unity-adapter/src/Internal/UnityMainThreadGuard.cs`
- `modules/unity-adapter/src/UnitySurface/LumioClientBootstrap.cs`
- `modules/unity-adapter/tests/Unit/UnityClientHostContractTests.cs`
- `modules/unity-adapter/tests/PlayMode/UnitySessionPumpPlayModeTests.cs`

#### 接口

**Consumes**

- session/input/observability public ports
- SPIKE-UNITY-63-AOT-MATRIX

**Produces**

- IUnityClientHost
- Unity bootstrap hook

#### Step 1：先写失败测试

- `UnityClientHostContractTests.PublicPortContainsNoUnityTypes`
- `UnityMainThreadGuardTests.UpdateFromWrongThread_IsRejected`
- `UnitySessionPumpPlayModeTests.Update_OrderIsInputThenSessionThenPresentation`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写公共 API 泄漏、wrong thread、Update order 测试。
2. Start/Update/Stop 只在 Main Thread；callback 不 Tick Session。
3. Disable/Destroy/Stop 归并为一次 close/unsubscribe。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
Unity -runTests -testPlatform PlayMode -testFilter UnitySessionPumpPlayModeTests
```

### Task 7: 实现 HybridCLR 平台能力探测并避免乐观 advertise

- **Task card:** `.spec/tasks/w7-hybridclr-capability-provider.md`
- **Wave:** `7A`
- **依赖:** `w6-bot-no-shortcut-policy,SPIKE-HYBRIDCLR-63`

#### 涉及范围

- `modules/hybridclr-adapter/src/Public/IHybridClrScopeLoader.cs`
- `modules/hybridclr-adapter/src/Public/IHybridClrScopeLoaderFactory.cs`
- `modules/hybridclr-adapter/src/Internal/HybridClrCapabilityProvider.cs`
- `modules/hybridclr-adapter/src/Internal/Official/OfficialHybridClrCapabilityProbe.cs`
- `modules/hybridclr-adapter/tests/Unit/HybridClrCapabilityProviderTests.cs`
- `modules/hybridclr-adapter/tests/Architecture/HybridClrDependencyBoundaryTests.cs`

#### 接口

**Consumes**

- handshake capability port
- SPIKE-HYBRIDCLR-63

**Produces**

- IPlatformCapabilityProvider implementation
- IHybridClrScopeLoader surface

#### Step 1：先写失败测试

- `HybridClrCapabilityProviderTests.UnavailablePlatform_DoesNotAdvertiseSupport`
- `HybridClrCapabilityProviderTests.AllRequiredOfficialPartsMustBePresent`
- `HybridClrDependencyBoundaryTests.NoSessionOrUnityTypesInStableApi`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写 unavailable/partial install/public API boundary tests。
2. probe 结果映射为生成 capability value；不暴露官方类型。
3. spike 未关闭或任一组件缺失时明确 unsupported。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/hybridclr-adapter/tests/Lumio.Client.HybridClrAdapter.Tests.csproj --filter "Capability|DependencyBoundary"
```

### Task 8: 实现 verified Artifact 与 Checkpoint 的 BCL 原子文件 Adapter

- **Task card:** `.spec/tasks/w7-persistence-filesystem-adapter.md`
- **Wave:** `7B`
- **依赖:** `w7-persistence-artifact-ports`

#### 涉及范围

- `modules/persistence/src/Internal/FileSystem/FileVerifiedSessionArtifactSource.cs`
- `modules/persistence/src/Internal/FileSystem/FileClientCheckpointStore.cs`
- `modules/persistence/src/Internal/FileSystem/AtomicFileReplacer.cs`
- `modules/persistence/src/Internal/Concurrency/PerKeyOperationGate.cs`
- `modules/persistence/src/Internal/Validation/ArtifactCryptographicVerifier.cs`
- `modules/persistence/src/Internal/Serialization/ArtifactManifestJsonContext.cs`
- `modules/persistence/tests/Unit/AtomicFileReplacerTests.cs`
- `modules/persistence/tests/Unit/PerKeyOperationGateTests.cs`
- `modules/persistence/tests/Contract/ArtifactContractFixtureTests.cs`

#### 接口

**Consumes**

- w7-persistence-artifact-ports
- generated artifact manifest

**Produces**

- filesystem persistence adapters

#### Step 1：先写失败测试

- `AtomicFileReplacerTests.CrashBeforeReplace_OldArtifactRemains`
- `AtomicFileReplacerTests.Success_ReturnsAfterReplaceAndFlush`
- `PerKeyOperationGateTests.SameKeySerialDifferentKeysParallel`
- `ArtifactContractFixtureTests.ValidInvalidManifestVectors`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写 temp/flush/replace crash points 与 per-key concurrency。
2. STJ source-gen 读取 manifest，BCL crypto 校验后返回 verified value。
3. write 只接受 committed checkpoint；成功只在 atomic replace 后返回。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/persistence/tests/Lumio.Client.Persistence.Tests.csproj --filter "AtomicFile|PerKey|ArtifactContract"
```

### Task 9: 把 Unity Input System Action 回调映射为 RawInputSample

- **Task card:** `.spec/tasks/w7-unity-input-system-adapter.md`
- **Wave:** `7B`
- **依赖:** `w7-unity-host-loop`

#### 涉及范围

- `modules/unity-adapter/src/UnitySurface/Input/UnityInputSystemAdapter.cs`
- `modules/unity-adapter/src/UnitySurface/Input/UnityInputActionBinding.cs`
- `modules/unity-adapter/src/UnitySurface/Input/UnityInputSubscriptionLedger.cs`
- `modules/unity-adapter/tests/EditMode/UnityInputSystemAdapterEditModeTests.cs`
- `modules/unity-adapter/tests/Fault/UnityAdapterLifecycleRaceTests.cs`

#### 接口

**Consumes**

- w7-unity-host-loop
- w2-input-sample-ingress

**Produces**

- Unity Input System→IInputSampleIngress adapter

#### Step 1：先写失败测试

- `UnityInputSystemAdapterEditModeTests.ActionCallback_ProducesGeneratedSample`
- `UnityInputSystemAdapterEditModeTests.CallbackNeverAllocatesClientCommandSequence`
- `UnityAdapterLifecycleRaceTests.DisableDestroyStop_UnsubscribesExactlyOnce`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写 action callback、seq ownership 与 unsubscribe tests。
2. Unity 类型只在 UnitySurface；转换为 RawInputSample 后立即 TryEnqueue。
3. QueueFull 记录结果，不在 callback 阻塞或 Tick Session。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
Unity -runTests -testPlatform EditMode -testFilter UnityInputSystemAdapterEditModeTests
```

### Task 10: 实现 committed Presentation Diff 有界队列、绑定解析与 Main Thread apply

- **Task card:** `.spec/tasks/w7-unity-presentation-adapter.md`
- **Wave:** `7B`
- **依赖:** `w7-unity-host-loop`

#### 涉及范围

- `modules/unity-adapter/src/Internal/UnityPresentationQueue.cs`
- `modules/unity-adapter/src/UnitySurface/Presentation/PresentationBindingResolver.cs`
- `modules/unity-adapter/src/UnitySurface/Presentation/UnityPresentationApplier.cs`
- `modules/unity-adapter/src/UnitySurface/Presentation/UnityPresentationSink.cs`
- `modules/unity-adapter/tests/Unit/UnityPresentationQueueTests.cs`
- `modules/unity-adapter/tests/EditMode/PresentationBindingResolverEditModeTests.cs`
- `modules/unity-adapter/tests/PlayMode/UnityPresentationApplierPlayModeTests.cs`

#### 接口

**Consumes**

- w7-unity-host-loop
- generated presentation diff/binding manifest

**Produces**

- IClientPresentationSink implementation

#### Step 1：先写失败测试

- `UnityPresentationQueueTests.QueueFull_IsExplicitAndDoesNotDropOldest`
- `PresentationBindingResolverEditModeTests.ManifestMismatch_StableReject`
- `UnityPresentationApplierPlayModeTests.StaleGenerationDiff_IsIgnored`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写 queue full、manifest mismatch、stale generation tests。
2. Session producer 只写 immutable committed diff；queue bounded。
3. Main Thread resolver/apply 不读取 Runtime mutable world；错误不部分 apply。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
Unity -runTests -testPlatform PlayMode -testFilter UnityPresentationApplierPlayModeTests
```

### Task 11: 实现 verified Artifact→官方 HybridCLR 步骤→原子 Scope Lease

- **Task card:** `.spec/tasks/w7-hybridclr-scope-loader.md`
- **Wave:** `7B`
- **依赖:** `w7-hybridclr-capability-provider,w7-persistence-filesystem-adapter`

#### 涉及范围

- `modules/hybridclr-adapter/src/Public/HybridClrScopeLeaseId.cs`
- `modules/hybridclr-adapter/src/Public/HybridClrScopeLoadRequest.cs`
- `modules/hybridclr-adapter/src/Public/HybridClrScopeLoadResult.cs`
- `modules/hybridclr-adapter/src/Internal/HybridClrScopeLoader.cs`
- `modules/hybridclr-adapter/src/Internal/HybridClrOperationQueue.cs`
- `modules/hybridclr-adapter/src/Internal/Validation/GameplayScopeArtifactValidator.cs`
- `modules/hybridclr-adapter/src/Internal/Official/OfficialHybridClrMetadataAdapter.cs`
- `modules/hybridclr-adapter/src/Internal/Official/OfficialHybridClrAssemblyAdapter.cs`
- `modules/hybridclr-adapter/src/Internal/Official/OfficialHybridClrEntrypointAdapter.cs`
- `modules/hybridclr-adapter/tests/Unit/HybridClrOperationQueueTests.cs`
- `modules/hybridclr-adapter/tests/PlayMode/HybridClrActivationPlayModeTests.cs`

#### 接口

**Consumes**

- w7-hybridclr-capability-provider
- w7-persistence-filesystem-adapter

**Produces**

- IHybridClrScopeLoader production implementation

#### Step 1：先写失败测试

- `GameplayScopeArtifactValidatorTests.HashOrReleaseMismatch_RejectsBeforeLoad`
- `HybridClrOperationQueueTests.QueueFull_IsExplicitAndBounded`
- `HybridClrActivationPlayModeTests.ValidArtifact_ActivatesOnlyAfterAllSteps`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先写 pre-load rejection、queue full 与 all-steps-before-lease tests。
2. 后台只准备 verified bytes；官方 Main Thread 步骤由 PumpMainThread 执行。
3. 全部成功后原子发布 lease；中途不暴露 entrypoint。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
Unity -runTests -testPlatform PlayMode -testFilter HybridClrActivationPlayModeTests
```

### Task 12: 验证 bitflip/truncate/wrong release/crash 与旧 committed 数据保留

- **Task card:** `.spec/tasks/w7-persistence-corruption-recovery.md`
- **Wave:** `7C`
- **依赖:** `w7-persistence-filesystem-adapter`

#### 涉及范围

- `modules/persistence/src/Internal/Recovery/ArtifactRecoveryScanner.cs`
- `modules/persistence/src/Internal/Recovery/CheckpointRetentionPolicy.cs`
- `modules/persistence/tests/Fault/PersistenceCorruptionTests.cs`
- `modules/persistence/tests/Property/ArtifactRoundTripProperties.cs`
- `modules/persistence/tests/Fault/PersistenceCrashMatrixTests.cs`

#### 接口

**Consumes**

- w7-persistence-filesystem-adapter

**Produces**

- corruption/recovery evidence

#### Step 1：先写失败测试

- `PersistenceCorruptionTests.BitFlipTruncateWrongRelease_AreRejected`
- `ArtifactRoundTripProperties.CommittedBlobVerifiesOrIsRejected`
- `PersistenceCrashMatrixTests.EveryPreReplaceCrashLeavesPreviousCommittedValue`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先生成 mutation/crash matrix 与 fixed seeds。
2. scanner 隔离损坏临时项，不把它们返回为 latest。
3. retention 只删除已确认过期项；旧 committed 文件优先保留。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/persistence/tests/Lumio.Client.Persistence.Tests.csproj --filter "Corruption|RoundTrip|CrashMatrix"
```

### Task 13: 实现每个官方步骤故障的逆序回滚、取消与幂等 Release

- **Task card:** `.spec/tasks/w7-hybridclr-rollback-unload.md`
- **Wave:** `7C`
- **依赖:** `w7-hybridclr-scope-loader`

#### 涉及范围

- `modules/hybridclr-adapter/src/Internal/HybridClrRollbackLedger.cs`
- `modules/hybridclr-adapter/src/Internal/HybridClrLoadAttempt.cs`
- `modules/hybridclr-adapter/tests/Unit/HybridClrRollbackLedgerTests.cs`
- `modules/hybridclr-adapter/tests/Fault/HybridClrLoadRaceTests.cs`
- `modules/hybridclr-adapter/tests/Fault/HybridClrPartialLoadFaultTests.cs`

#### 接口

**Consumes**

- w7-hybridclr-scope-loader

**Produces**

- rollback/unload contract

#### Step 1：先写失败测试

- `HybridClrRollbackLedgerTests.ReverseOrder_IsStableAndIdempotent`
- `HybridClrLoadRaceTests.CancelBeatsLateActivation`
- `HybridClrPartialLoadFaultTests.FailureAtEveryOfficialStep_NeverExposesEntrypoint`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先枚举 metadata、assembly、dependency、entry、activate 每个故障点。
2. 每成功一步登记对应 rollback；失败从 ledger 逆序。
3. Release/Cancel 幂等；late main-thread completion 不发布 lease。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test modules/hybridclr-adapter/tests/Lumio.Client.HybridClrAdapter.Tests.csproj --filter "Rollback|LoadRace|PartialLoad"
```

### Task 14: 在目标 Unity/IL2CPP 设备矩阵验证核心、Input、Presentation 与可选 HybridCLR

- **Task card:** `.spec/tasks/w7-unity-aot-device-matrix.md`
- **Wave:** `7C`
- **依赖:** `w7-unity-input-system-adapter,w7-unity-presentation-adapter,w7-hybridclr-rollback-unload`

#### 涉及范围

- `eng/unity/device-matrix.json`
- `eng/unity/run-editmode-tests.sh`
- `eng/unity/run-playmode-tests.sh`
- `eng/unity/run-il2cpp-smoke.sh`
- `modules/unity-adapter/tests/Aot/UnityAdapterAotPlayerTests.cs`
- `modules/hybridclr-adapter/tests/Aot/HybridClrAotPlayerTests.cs`
- `docs/evidence/unity-aot-matrix.md`

#### 接口

**Consumes**

- w7-unity-input-system-adapter
- w7-unity-presentation-adapter
- w7-hybridclr-rollback-unload
- SPIKE-UNITY-63-AOT-MATRIX

**Produces**

- AOT/device evidence matrix

#### Step 1：先写失败测试

- `UnityAdapterAotPlayerTests.CorePortsLinkWithoutReflectionFallback`
- `UnityAdapterAotPlayerTests.InputAndPresentationRoundTripOnTarget`
- `HybridClrAotPlayerTests.TargetMatrix_LoadsMetadataAndScope`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先锁定 editor/player/platform/package versions。
2. 运行 EditMode、PlayMode、IL2CPP player smoke，记录构建哈希与设备。
3. 不支持 HybridCLR 的平台必须 capability reject，而不是测试跳过后宣称支持。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
bash eng/unity/run-editmode-tests.sh && bash eng/unity/run-playmode-tests.sh && bash eng/unity/run-il2cpp-smoke.sh
```

### Task 15: 跑通 Input→Prediction→Correction/Rollback→Diff→Config→Save/Load→Bundle

- **Task card:** `.spec/tasks/w7-vertical-slice-exit-scenario.md`
- **Wave:** `7D`
- **依赖:** `w7-persistence-corruption-recovery,w7-unity-aot-device-matrix,w7-observability-failure-bundle-export,w7-connection-remote-transport`

#### 涉及范围

- `tests/Lumio.Client.IntegrationTests/VerticalSlice/VerticalSliceHappyPathTests.cs`
- `tests/Lumio.Client.IntegrationTests/VerticalSlice/VerticalSliceCorrectionTests.cs`
- `tests/Lumio.Client.IntegrationTests/VerticalSlice/VerticalSlicePersistenceTests.cs`
- `tests/Lumio.Client.IntegrationTests/VerticalSlice/VerticalSliceFailureBundleTests.cs`
- `tests/Lumio.Client.IntegrationTests/VerticalSlice/VerticalSliceFixtureComposition.cs`
- `tests/Lumio.Client.IntegrationTests/VerticalSlice/VerticalSliceTraceAssertions.cs`

#### 接口

**Consumes**

- all Wave 7A-7C tasks

**Produces**

- Vertical Slice exit evidence
- bot command `vertical-slice`

#### Step 1：先写失败测试

- `VerticalSliceHappyPathTests.InputMappingPredictionConfirmationPresentationDiff`
- `VerticalSliceCorrectionTests.CorrectionUsesSingleAuthorityTransactionAndAtomicRollbackReplay`
- `VerticalSlicePersistenceTests.ConfigStagingSaveLoadAndRestartPreserveCommittedState`
- `VerticalSliceFailureBundleTests.FaultExportsCorrelatedReplayEvidence`
- `VerticalSliceHappyPathTests.RemoteAndLocalEmbeddedUseSameVectors`

#### Step 2：证明红灯

运行该卡末尾命令；失败原因必须是目标类型/行为尚不存在，而不是 restore、编译器版本或无关测试损坏。

#### Step 3：按边界实现最小行为

1. 先把完整 trace、调用次数与 committed-only assertions 写成失败测试。
2. 同一 fixture 分别运行 LocalEmbedded 与 Remote；Unity path 使用同 public Session/Input APIs。
3. 注入 correction、queue full、artifact corruption、sink failure，验证 bundle/recovery 与零旁路。

#### Step 4：验收

- [ ] 上述测试全部通过；未削弱断言。
- [ ] 实际文件只在“涉及范围”内，或新增路径已先更新任务卡和依赖。
- [ ] ArchitectureTests 仍通过，public API 无第三方类型泄漏。
- [ ] QueueFull、取消、迟到 generation、关闭路径具备稳定结果。
- [ ] 没有新增公共协议字段、第二 Runtime/Codec/Storage。

#### 验证命令

```bash
dotnet test tests/Lumio.Client.IntegrationTests/Lumio.Client.IntegrationTests.csproj -c Release --filter Category=VerticalSlice
```

## 4. 退出条件

执行前置条件：Foundation 全部命令通过，且上游 API map 与需要的 spike 均有可审计结论。

完成 Wave 7 后必须证明：

```text
Input Mapping → InputSampleSeq → Candidate
→ local prediction transaction
→ ClientCommandSeq/PredictionKey
→ Confirmation/Correction
→ Replica + Prediction single authority transaction
→ atomic Rollback/Replay
→ committed Presentation Diff
→ Config staging/activation
→ verified Artifact + Checkpoint Save/Load
→ correlated Failure Bundle
```

验证命令至少包括：

```bash
dotnet restore LumioClient.slnx --locked-mode
dotnet format LumioClient.slnx --verify-no-changes --no-restore
dotnet build LumioClient.slnx -c Release --no-restore
dotnet test tests/Lumio.Client.ArchitectureTests/Lumio.Client.ArchitectureTests.csproj -c Release --no-build
dotnet test modules/connection/tests/Lumio.Client.Connection.Tests.csproj -c Release --no-build --filter RemoteTransport
dotnet test tests/Lumio.Client.IntegrationTests/Lumio.Client.IntegrationTests.csproj -c Release --no-build --filter "Category=VerticalSlice"
bash eng/unity/run-editmode-tests.sh
bash eng/unity/run-playmode-tests.sh
bash eng/unity/run-il2cpp-smoke.sh
node .spec/tools/spec-lint.mjs
```

LocalEmbedded 与 Remote 必须使用同一生成向量并产生等价协议 trace；Unity 与 Bot 必须使用相同 Session/Input 公共 API。

## 5. 恢复说明

本计划依据上一轮 Wave 7 任务、Remote/Unity/HybridCLR/Persistence/Failure Bundle 设计重新恢复。先前附件临时句柄失效，因此本副本不承诺逐字节一致。

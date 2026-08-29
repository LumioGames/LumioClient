# A1 客户端接入设计（WSS 传输 · 三项落点裁决）

- 日期：2026-08-29
- 卡片：T-00001（RM-00007 / R-00055）
- 决策记录：[`ADR 0003`](../../.spec/decisions/0003-a1-client-wss-access-landing-sites.md)
- 架构基线：室级 `LGE-V1.4-2026-08-27`；本仓模块 README 基线 `LGE-V1.2-2026-08-27`

本文只做一件事：为 T-00002 / T-00003 / T-00004 / T-00005 四张实现卡把三项落点裁决写到无歧义，并记录做裁决时所依据的实测事实。**不写实现代码，不改任何 csproj，不在本仓定义任何公共 wire。**

## 0. 本轮范围与两条硬边界

**本轮只做 A1 的协议与生命周期闭环**：拨号、通道认证、握手推进、断线与重连、可诊断事件、trace 落盘。

以下两项**双前置 BLOCKED，不在本轮范围**，本文不对世界状态做任何断言：

| 缺件 | 前置 | 阻塞理由（架构源原文） |
| --- | --- | --- |
| **CC-8** 上行 gameplay 命令（「挖哪个方块」） | **D-009** 未冻结 | 「The V1 wire surface is limited to the replication envelope MessageTypes; the server `protocol-dispatch` boundary stays blocked and **no repository may invent a dispatch wire format**.」上行 8 个冻结 messageType 无一能承载 client→server gameplay 命令。 |
| **CC-9** 下行公共状态载荷解码 | **ADR-028** | Alternatives 明文否决 free-form payload：「Keeping a free-form payload was rejected because two implementations can pass the gate and disagree on Snapshot identity.」下行 typed body 无状态载荷字段。 |

D-1 已定方向（`FullSnapshot` 增 `stateBlocks`、`Delta` 增 `changedBlocks`、上行新增 MessageType `InputCommand`），但归 **V1.5 跃迁批**；CC-8 / CC-9 在 V1.5 之前解不开。

## 1. 三项落点裁决

### 裁决一 · 凭据入参落点 → **扩 `ClientConnectionCreateRequest` 公共面**

在「给 `ClientConnectionCreateRequest` 增 endpoint / 不透明凭据 / nonce / 超时字段」与「由 `IClientConnectionFactory` 实现类在组装根构造时持有、公共面不变」之间，**取前者**。

凭据与 nonce 在公共面上一律是**不透明字节**（`ReadOnlyMemory<byte>` 或等价形态）。本仓不定义其内部格式、算法、轮换或 nonce 派生规则。

**逐条回应卡面要求回应的三项现状：**

1. **`modules/session/src/Internal/ClientSession.cs:180` 硬编码 `new ClientConnectionCreateRequest(generation, 32)`。**
   这条恰恰是选前者的理由，不是反对理由：该构造点已经是**每代次一次**的参数载体。D-012 冻结「V1 无 Resume Token，新连接代次必须重做通道认证 + 完整 Handshake」，而**连接代次由 session 拥有**（`StartGeneration`）。若把 endpoint 与凭据挪到工厂构造时持有，工厂就必须自己为每次重连铸造新 nonce——那等于把「重连尝试语义」搬进 connection 层。
2. **`modules/connection/README.md`「责任」写明「通道认证与 Channel Binding 属于本层」。**
   不冲突。本裁决搬的是**每次尝试的输入参数**，不是认证行为本身：握手期发送子协议位序、校验协商结果、失败映射为本地关闭原因，全部仍在 connection 的传输适配器内完成。参数显式化反而让「本层拥有认证」这件事可测——测试能对同一层注入不同凭据观察行为差异。
3. **同文件「明确不负责什么」写明本层不拥有自动重连策略。**
   这是决定性的一条。重连策略归 session；每个新代次需要**新 nonce**（D-012）。工厂持有式方案要么持有一个固定 nonce（违反反重放语义），要么自行生成（把重连语义拉进 connection，直接违反这条 README 断言）。参数随创建请求逐次传入，是唯一不越权的形态。

**同时落地的 CC-7 三项**（与本裁决同一入口，避免二次改公共面）：事件队列容量、每轮 drain 上限、`ITransportFaultPolicy` 注入。默认值保持现状（容量 32、drain 16），以免改变既有行为。

**凭据不外泄的机器判据**（T-00002 / T-00003 验收）：凭据与 nonce 不得出现在 `ConnectionEvent`、`ClientConnectionSnapshot`、`EncodedFrame` 的任何字段，相关类型的 `ToString()` 不得回显凭据字节。对齐 `modules/connection/README.md`「可观测性」节「日志必须脱敏 Endpoint 凭据和认证材料」，以及 `.spec/rules/system.md`「密钥 / 凭据不得入库、不得进 prompt、不得进日志」。

### 裁决二 · 上行 Envelope 构造落点 → **落 `modules/bot/host/**` 组装根，生产库继续只传不透明字节**

在「落 `modules/session/src/Internal/**` + `modules/handshake/src/Internal/**`（R-00260 CC-3 建议）」与「落组装根」之间，**取后者**。

**逐条回应卡面要求回应的四项现状：**

1. **`eng/upstream-api-map.md` 开头「This repo must not define a second Envelope, Transaction, ErrorCode, Schema, Codec, or Storage」。**
   合法的构造路径只有一条：消费架构源的生成产物。把这条依赖收敛到**一个非生产工程**，比让它穿进两个生产模块，爆炸半径小一个数量级。
2. **13 条 `GeneratedContract.*` / `RuntimeContract.*` 别名全部 `status: blocked-unpublished`，`publishedType` / `packageId` / `packageVersion` 均为 `null`。**
   这是本仓当前**没有任何可用编解码器**的机器可读证据。见 §3「本仓消费通道缺失」——该缺口在 R-00268 落地后**依然存在**。
3. **`modules/session/src/Internal/SessionWireBytes.cs` 现为硬编码魔数 `BaselineAck = { 0xAC, 0x4B, 0x01 }`。**
   实测本仓生产库的出站构造点**只有两处**：`ClientSession.cs:321` 的 `new EncodedFrame(SessionWireBytes.BaselineAck)`，与 `LocalPredictionOrchestrator.cs:52` 的 `new EncodedFrame(plan.OpaqueBytes)`。**后者已经是不透明字节**。也就是说生产库的出站面已经有一半是字节透明的，剩下那一处魔数服务的是 LocalEmbedded 夹具链路，**不是公共 wire**。保持不动，`tests/Lumio.Client.IntegrationTests/Foundation/**` 四个既有测试才能保持绿。
4. **`modules/bot/host/Lumio.Client.Bot.Host.csproj` 是 `LumioProduction=false` / `net10.0` / `LangVersion 14.0`，生产库是 `netstandard2.1` / `LangVersion 9.0`。**
   组装根是**唯一**能吸收一个生成依赖而不触动 Unity 侧 TFM 与语言版本下限的工程；且它已在 `eng/project-reference-allowlist.json` 的 `compositionRoot` 段被允许引用 connection / handshake / replica / prediction / persistence。

**旁证（跨仓）**：LumioServer 的 MVP 宿主设计 §8.1「LumioClient 侧硬约束」原文——「其生产库是 `netstandard2.1` / `LangVersion 9.0`，**无法引用架构源生成包**（实测 NU1201）——**本设计不要求它改 TFM**（那属对方 Wave7 范围）；跨边界只传 Envelope 字节与已注册错误码字符串。」服务端一侧独立实测到同一堵墙，并已按「客户端生产库保持字节透明」来设计。

**本裁决的直接后果，必须显式记录**：CC-3「上行帧改为 Envelope 形状」**本轮不交付**。它随 T-00004 一起等 §3 的消费通道缺口收口。`SessionWireBytes.BaselineAck` 的魔数在那之前保留原样。这是一处**已知缺口，不是已完成项**。

### 裁决三 · WSS adapter 工程落点 → **进既有 `modules/connection/src/Lumio.Client.Connection.csproj`**

在「进既有 csproj」与「在 `modules/connection/` 下新建独立 csproj」之间，**取前者**。实现落 `modules/connection/src/Internal/Transport/WebSocket/**`。

**逐条回应卡面要求回应的三项闸门：**

1. **`.github/workflows/repository-policy.yml` 断言 `modules/` 恰 11 个子目录，`tests/.../Layout/ForbiddenModuleTests.cs` 断言模块目录名集合恰为该 11 项。**
   两个方案都不触发这条——新建的是 `modules/connection/` **之下**的 csproj，不是新的顶层模块目录。这条闸门**不构成区分依据**，卡面把它列进来是过虑。
2. **`tests/.../GraphHelpers.cs` 的 `CsprojGraph.ProductionEdges()` 扫描 `modules/**/*.csproj`（排除路径含 `/tests/` 与 `/host/`），`ProjectReferenceAllowlistTests` 要求每个节点都在 `eng/project-reference-allowlist.json` 的 `production` 映射里且双向包含。**
   这条**是**决定性的：新建 csproj 会**新增一个生产图节点**，连带要求改 `eng/project-reference-allowlist.json`（双向）、`LumioClient.slnx`、新增一份 `packages.lock.json`。而 T-00002 与 T-00003 的边界都明写「不改 `eng/project-reference-allowlist.json`」。
3. **`tests/.../Graph/InternalsVisibleToTests.cs` 要求路径含 `/src/` 的每个 csproj 的 `InternalsVisibleTo` 集合恰为 `{<AssemblyName>.Tests}`。**
   新建 csproj 还要连带新建一个配套测试工程才能满足这条，否则闸门红。进既有工程零成本满足。

**收益侧为零**：`ClientWebSocket` 是 BCL 类型，`netstandard2.1` 直接可解析，**零新增 NuGet**；独立程序集带不来任何额外隔离——真正的隔离由 `modules/connection/README.md`「公共入口与出口」的「第三方 Socket/IPC 类型不得穿过模块边界」加**构建期 banned-api 闸门**共同保证，二者都不以程序集为粒度。

**收口 R-00055 的实现合同冲突：remote transport 走 `ClientWebSocket`，不走 BCL Socket / SslStream / Pipelines。**

R-00055「详细要求」第 1 条写的是「实现 BCL **Socket/SslStream/Pipelines** Remote Adapter」。该合同在本仓生产工程中**不可执行**：

```
# 生产工程内放一处 new System.Net.Sockets.Socket(SocketType.Stream, ProtocolType.Tcp)
dotnet build modules/observability/src/Lumio.Client.Observability.csproj -c Release
→ error RS0030: The symbol 'Socket' is banned in this project:
              sockets stay behind remote transport adapters
  Build FAILED.  1 Error(s)
# 移除探针后
→ Build succeeded.  0 Warning(s)  0 Error(s)
```

> **该证据的成立前提，请务必连带读**：上面这条 `RS0030` 是在 **T-00007 修好闸门之后**测得的。在此之前，`Directory.Build.targets` 注入的 `AdditionalFiles` 名为 `eng/banned-public-api.txt`，而 `Microsoft.CodeAnalysis.BannedApiAnalyzers` 只识别 `BannedSymbols*.txt`，**整份禁令从未生效**（同一探针当时输出 `Build succeeded. 0 Error(s)`）。该缺陷早在 `docs/spikes/2026-08-28-spike-hybridclr-63.md` §4.7 就以对照实验记录在案，行动项 P0-1。T-00007 已将文件改名为 `eng/BannedSymbols.txt` 并同步 `Directory.Build.targets`。
> **凡是引用「banned-api 在构建期强制」作为验收证据的卡面（T-00003 尤其），其证据只在修复之后成立。**

`SslStream` / `PipeReader` / `PipeWriter` 亦已随 T-00007 一并进入禁表（依据设计文档 connection 节「禁止出现在签名中的类型」），因此 R-00055 那条实现合同的三个组件在本仓生产工程内均不可用。

## 2. 与 LumioServer 必须双向确认后同时落地的项

以下**全部不是公共契约**。依据 `TRANSPORT-WEBSOCKET-PROFILE-REGISTRATION.md` §3 开头原文：「以下是……在 A1 联调中**可以直接依赖**的公共面。**清单外的一切都不是公共契约。**」该文点名 WS 子协议名、端点路径、close code 映射归 Server / Client 自行约定。

| 项 | 值 | 备注 |
| --- | --- | --- |
| `productId` | `"A"` | 三者必须与 Server 同时落地 |
| `gameReleaseId` | `"A-1.1.0"` | 三段式；Admission 做 ExactRelease 精确匹配（D-007） |
| `protocolVersion` | `1` | |
| WS 子协议名 | `lumio.mvp.v0` | 协商成功后 `ClientWebSocket.SubProtocol` 必须恰为此值 |
| 子协议三段位序 | `lumio.mvp.v0, <opaqueTokenB64Url>, <opaqueNonceB64Url>` | 第 2 段 base64url 解码后为凭据，第 3 段**原样**作为 nonce |
| 通道认证失败 | WebSocket close `1008`，且此前**零字节**应用数据 | 不是公共错误码 |

**close 1008 的语义来源**（Server 设计 §6.2 原文）：ID Registry 的 ErrorCode 中**没有任何一个表示「凭据无效」**，因此 MVP **不为通道认证失败发任何 Envelope `Error`**，改在 HTTP 升级阶段以 close `1008` 拒绝。

**退场纪律**：源码常量处必须注释写明——该位序是 LumioServer / LumioClient 双端私有约定；架构源冻结凭据承载方式（**D-011**）后即改用公共形态并删除本约定；`lumio.mvp.v0` 里的 `mvp` 与 `v0` 是退场标记，**不得去掉**。Server 侧已把它登记为 `mvp-host/absences.json` 的 `ABS-AUTH-CREDENTIAL-CARRIAGE`。

## 3. 阻塞与缺口（机器可判，不得当作已完成）

### 3.1 本仓消费通道缺失 —— R-00268 之后**依然**阻塞 T-00004 / T-00005

架构仓 `origin/main` 的 `0338c86`（ADR-048）已落地：`packages/csharp/*` 六个工程**全部** `<TargetFrameworks>netstandard2.1;net8.0</TargetFrameworks>`，八类契约类型本体在 `packages/csharp/Lumio.Gen.ContractTypes/ContractBodies.cs`，可执行 gate 在 `packages/csharp/Lumio.Gen.ProtocolPermissionValidator/ProtocolGate.cs`。**TFM 墙到此为止。**

但**本仓拿不到这些产物**：

- 架构仓**不打包、不发 NuGet**（全仓 `nupkg` / `nuget push` / `PackageId` / `GeneratePackageOnBuild` 零命中；CI 里名为 "Publish generated artifacts…" 的步骤实为 `generate --out /tmp` 后比对 `outputHash`）。产物是**提交进仓的生成源码**。
- 消费模型是**字节级只读镜像 + sha256 锁**。LumioServer 为此有专门一张卡（`vendor-architecture-contracts-and-fixture-mirror`），其卡面原话：「靠环境变量指向兄弟仓会让 **CI 上等于没有这道门**」。
- 本仓无 `contract-mirror/` / `schemas/` / `fixtures/` / `ids/`；`NuGet.Config` 只有 nuget.org；`tests/Fixtures/index.json` 的 `upstreamCorpusPin` 仍是 `{"status":"unpublished","packageId":null,"hashes":[]}`。

**解冻前置**：一张 LumioClient 侧的 vendor / mirror 卡（镜像目录 + sha256 锁 + sync/verify 双脚本 + 接进 `repository-policy.yml`）。

**⚠️ 解冻时的绊线**：`eng/upstream-contract-smoke/Program.cs` 遍历 `upstream-api-map.md` 每条别名，**遇到任何非 `blocked-unpublished` 的 status 直接 `return 3`**。把 map 改成「已发布」的那一刻这个 smoke 就红——谁解冻谁必须在同一改动里同步它。

### 3.2 validator 只校验「已注册」，不校验角色权限

ADR-048 §2 原文：「the id must be a registered `MessageType`. ADR-022 also says "permitted for the admitted Role", and no role-to-message permission table exists anywhere in the architecture source. Deriving one here would invent a public contract; it belongs with the D-009 dispatch surface that remains blocked. **The gate therefore checks registration and stops.**」

两条对本仓直接相关：

- gate 的拒绝优先级是**已发布的数据**：`StaleConnectionGeneration → SessionMismatch → ReleaseMismatch → MessagePermissionDenied → RoleMismatch → ClaimNotGranted`。
- **`SessionAntiReplay` 不在该优先级里**——ADR-048 明写它「owned by `ClientReplicaSession`, invisible to the gate」，**那是本仓的所有权**。且 gate 要求「它能跑的每项检查都必须过」，否则一个可推导的失败会藏在无人能验证的原因后面。

### 3.3 CC-6 本轮不修

`HandshakeSession.HandleFrame` 收到 ServerHello 后调用 `_capabilities.QueryAsync(...)`，但只有 `pending.IsCompleted` 为真时才把结果入队，**异步完成的 `ValueTask` 结果被静默丢弃**，握手会永久停在 `AwaitingCapability`。本轮按**同步能力提供者**规避。R-00260 §12.2 明确标注 CC-6「非 Server 卡前置」。

### 3.4 三处既有闸门缺陷（开工前必知，均非本轮引入）

| 缺陷 | 实测 | 影响 |
| --- | --- | --- |
| 3 项架构测试非 Windows 恒红 | `dotnet test tests/Lumio.Client.ArchitectureTests` → `Failed: 3, Passed: 15`。根因 `GraphHelpers.cs` 用 `Path.GetFileNameWithoutExtension` 解析 csproj 里 `..\..\x\src\Y.csproj` 的反斜杠路径，在 macOS / Linux 不切分目录分隔符 | T-00002 / T-00003 / T-00004 验收要求「架构闸门全绿」，在非 Windows 宿主上**开箱即红**。已授权并入 T-00002 |
| `dotnet format` 开箱即红 | `dotnet format --verify-no-changes --no-restore` → exit 2，3 处 `error IMPORTS: Fix imports ordering`，全在 `tests/Lumio.Client.IntegrationTests/Foundation/` | T-00002 / T-00003 / T-00005 验收要求该命令无改动 |
| CI 无 dotnet test job | `.github/workflows/` 只有 `repository-policy.yml`，无任何 job 跑 `dotnet build` / `dotnet test` | 上面两条从未在 CI 暴露。已立 R-00287 |

## 4. 两个计数器不映射

服务端 `ConnectionEpoch`（绑定计数）与客户端 `ConnectionGeneration`（重连计数）**相互独立**，实现中**不得建立任何映射或换算关系**。客户端 `ConnectionGeneration` 严格递增；重连按 D-012 重做通道认证（新 nonce）加完整握手，无 Resume Token。

## 5. 方向纠正：`ResyncRequest` 是客户端**出站**

架构源 v1.4 §7.1 链路：

```
HandshakeAccepted
 -> FullSnapshot(SnapshotId, TickId, RevisionVector)
 -> BaselineAck
 -> Delta(BaseSnapshotId, FromRevision, ToRevision, Sequence)
 -> DeltaAck / GapDetected
 -> ResyncRequest
 -> FullSnapshot or ResyncPatch
```

- **客户端合法出站 4 种**：`Handshake{role:"Client"}` / `BaselineAck` / `DeltaAck` / `ResyncRequest`
- **服务端出站 5 种**：`Handshake` / `FullSnapshot` / `Delta` / `MaintenanceKick` / `Error`

`ResyncRequest` 由**检测到 gap 的副本方**发出，服务端从不下发。§7.1 本身只给链路顺序，方向认定来自「`GapDetected` 的主体是副本方」这一推导 + LumioServer 设计 §8.1 的显式表述，本文记录该推导链而非伪称 §7.1 有直述。

## 6. 收下的两条口径修正

1. **`TRANSPORT-WEBSOCKET-PROFILE-REGISTRATION.md` §4 是「观察项」，不是 errorClass / reasonCode 词表。** 真词表在 §3.2（errorClass 三值 `Retryable` / `Rejectable` / `Fatal`）与 §3.4（A1 会用到的 10 个稳定 ErrorCode）。
2. **`BudgetExceeded`（1035）在 A1 期是多义码。** §4 的临时口径把「超出 `maxMessageBytes` / `maxFragmentBytes`」也归到 `errorClass = Rejectable` + `reasonCode = BudgetExceeded`，与队列背压预算共用一个码。**客户端可以按此上报，但不得反向断言**「收到 `BudgetExceeded` ⇒ 消息超长」。

## 7. 引用纪律

- **跨仓核实只读已提交对象**（`git -C <repo> show origin/main:<path>` / `git ls-tree`），不读其它仓库的工作区。
- **一律用符号锚，不用行号锚。** 实测三处行号锚全部失效：`_REPLICATION_BODY_REQUIRED` 卡面写 `tools/lumio_contract.py:355-364`，实际 **485–494**（消费点 681）；FullSnapshot 必须 Reliable 卡面写 `:802-803`，实际 **1162–1163**；`_CHUNK_KEY` 卡面写 `:401`，实际 **531**。三处内容全部正确、行号全部错，漂移 130–360 行不等。
- **引用提交号用内容提交，不用合并提交**（分支 SHA 可能因 rebase 被重写）。例：契约面裁决的内容提交是 `8ab4ec4`，`c350ec6` 是其合并提交。
- **ErrorCode 计数不得硬编码**：`ids/index.json` 已由 V1.4 基线的 43 个增至 **53**（`5c222c4`，ADR-046 native kernel status band）。新增 10 个全是内核状态码、不含凭据类语义，因此「无『凭据无效』码 ⇒ close 1008」的裁决不受影响。断言口径按「SchemaId 在册 + BaselineId 相等」，不硬编码任何计数。

## 8. A1-α 的范围更正

LumioServer 的 A1-α 验收卡明写：「本卡验收的**全部 17 步**只依赖本仓的 `lumio-mvp-host` 与 `lumio-mvp-smoke-client` 两个进程，**今天就能独立跑绿**，不依赖 LumioClient 与 LumioGameRuntime 的任何产物」。

即 **A1-α 是 LumioServer 自闭环的，其绿灯不代表 LumioClient 接通**。客户端真机对接需要 CC-1/2/3/4/5/7，是 A1-α 之后的独立跨仓卡。

两条连带事实：

- **A1-α 全程跑明文 `ws://127.0.0.1`**（Server 设计 §7.4，由 `transport.allowInsecureLoopback` 显式开关控制、**默认 false**，非 `LocalSplitProcess` / `LocalEmbedded` Profile 时拒绝启用）；`wss://` + 真实证书是**独立后续卡，不进 CI 必经路径**。Server 设计 §6.1 里 TLS 版本 / 客户端证书 / ALPN / 会话恢复**一项都没有**。
  → **R-00253（SPIKE-REMOTE-AOT）不是 T-00003 的 A1 前置**，只是 Wave7 生产化前置。T-00003 本轮不为 TLS 规划参数面；但 endpoint 值类型仍须同时容纳 `ws://` 与 `wss://`（CC-5 的 CLI 是 `--transport ws|wss`）。
- **`lumio-mvp-host` 尚不存在**：截至 2026-08-29，LumioServer `origin/main` 的 `mvp-host/` 下只有构建根、门禁脚本与 Platform 原语（`Lumio.Server.MvpHost.Platform` 及其测试工程，落地提交 `8cb2fc5`：时钟 / Timer / 有界端口 / 受监督线程）。**没有 host 可执行工程，也没有 `lumio-mvp-smoke-client`**（`git ls-tree -r origin/main mvp-host/` 对 `App` / `SmokeClient` 零命中）。客户端侧本轮以自建 loopback WS 测试端收口，**不得把 loopback 结果声称为跨仓联调结果**。

## 9. 下游卡的落点结论

| 卡 | 依据本文的落点 | 本轮可否交付 |
| --- | --- | --- |
| T-00002 | 裁决一：扩 `ClientConnectionCreateRequest` 公共面；CC-7 三项同批 | 可（需先修 §3.4 的两处既有红灯） |
| T-00003 | 裁决三：进既有 `Lumio.Client.Connection.csproj`，落 `Internal/Transport/WebSocket/**` | 可（TLS 面按 §8 缩小；Envelope 形状的畸形拒绝断言随 §3.1 顺延） |
| T-00004 | 裁决二：落 `modules/bot/host/**`，生产库不动 | **否** —— §3.1 消费通道缺失 |
| T-00005 | 随 T-00004 | **否** —— 传导阻塞 |

# 0003 · A1 客户端接入的三项落点：凭据随创建请求、Envelope 构造留在组装根、WSS 进既有 connection 工程

- 日期:2026-08-29
- 状态:生效

## 背景

A1（跨进程 WSS 复制与重连闭环）的客户端一端缺件。R-00260 §12.2 列出 CC-1..CC-9,其中 CC-1/2/3/4/5/7 属本轮协议与生命周期闭环,CC-8（上行 gameplay 命令）与 CC-9（下行公共状态载荷解码）因架构源 D-009 与 ADR-028 双前置 BLOCKED,不在本轮。

三项落点必须在动手前裁决,否则 T-00002..T-00005 四张实现卡会撞上本仓既有硬约束:`modules/connection` 只有 LocalEmbedded 环回、无任何远程传输;`eng/upstream-api-map.md` 的 13 条生成契约别名全部 `blocked-unpublished`;生产库锁 `netstandard2.1` / `LangVersion 9.0` 而组装根是 `net10.0` / `LumioProduction=false`;`repository-policy.yml` 与三项架构测试对模块数、工程引用图、`InternalsVisibleTo` 集合有硬断言。

R-00055「详细要求」第 1 条要求实现 BCL Socket/SslStream/Pipelines Remote Adapter,该合同与本仓构建期闸门冲突,也需在此收口。

## 决策

1. **凭据入参落点:扩 `ClientConnectionCreateRequest` 公共面。** endpoint、不透明凭据、不透明 nonce、连接超时随每次创建请求传入;凭据与 nonce 在公共面上一律是不透明字节,本仓不定义其格式、算法、轮换或派生规则。理由:D-012 冻结「V1 无 Resume Token,新连接代次必须重做通道认证 + 完整 Handshake」,而连接代次由 `session` 拥有;工厂持有式方案要么持有固定 nonce（违反反重放语义）、要么自行铸造 nonce（把重连尝试语义拉进 connection,直接违反 `modules/connection/README.md`「本层不拥有自动重连策略」）。CC-7 的事件队列容量、每轮 drain 上限、`ITransportFaultPolicy` 注入随同一入口落地,默认值保持现状（容量 32、drain 16）以免改变既有行为。

2. **上行 Envelope 构造落点:`modules/bot/host/**` 组装根,生产库继续只传不透明字节。** 理由:合法构造路径只有「消费架构源生成产物」一条,而组装根是唯一能吸收该依赖、又不触动 Unity 侧 TFM 与语言版本下限的工程,且已在 `eng/project-reference-allowlist.json` 的 `compositionRoot` 段被允许引用相关模块;把这条依赖收敛到一个非生产工程,爆炸半径小于让它穿进两个生产模块。生产库出站面实测只有两处构造点（`ClientSession.cs:321` 的 `SessionWireBytes.BaselineAck` 魔数、`LocalPredictionOrchestrator.cs:52` 的 `plan.OpaqueBytes`）,后者已是字节透明,前者服务 LocalEmbedded 夹具链路而非公共 wire,保持不动才能让四个既有 Foundation 集成测试保持绿。LumioServer 设计 §8.1 独立实测到同一堵 NU1201 墙,并已按「客户端生产库保持字节透明、跨边界只传 Envelope 字节与已注册错误码字符串」设计,不要求本仓改 TFM。

3. **WSS adapter 工程落点:进既有 `modules/connection/src/Lumio.Client.Connection.csproj`,实现落 `Internal/Transport/WebSocket/**`。** 理由:新建 csproj 会新增一个生产图节点,连带要求改 `eng/project-reference-allowlist.json`（双向包含）、`LumioClient.slnx`、新增 `packages.lock.json`,并需配套新建测试工程才能满足 `InternalsVisibleTo` 恰为 `{<AssemblyName>.Tests}` 的断言;而 T-00002 / T-00003 的边界都明写不改 allowlist。收益侧为零:`ClientWebSocket` 是 BCL 类型、`netstandard2.1` 直接可解析、零新增 NuGet,隔离由模块 README 的边界约定加构建期 banned-api 闸门保证,二者都不以程序集为粒度。「`modules/` 恰 11 个子目录」的断言对两个方案都不触发,不构成区分依据。

4. **收口 R-00055 的实现合同冲突:remote transport 走 `ClientWebSocket`,不走 BCL Socket / SslStream / Pipelines。** 生产工程内引入 `System.Net.Sockets.Socket` 触发 `error RS0030 ... Build FAILED`;`SslStream` / `PipeReader` / `PipeWriter` 亦已随 T-00007 进入禁表。该证据的成立前提是 T-00007 已修好闸门文件名——在此之前 `Directory.Build.targets` 指向的 `eng/banned-public-api.txt` 不被 `BannedApiAnalyzers` 识别,整份禁令从未生效（详见 `docs/spikes/2026-08-28-spike-hybridclr-63.md` §4.7 与其行动项 P0-1）。凡引用「banned-api 在构建期强制」作为验收证据的卡面,其证据只在该修复之后成立。

## 后果

- 决策 1 让 `modules/connection` 的公共面承载每次尝试的 endpoint 与凭据参数,代价是公共面变宽;换来的是「本层拥有通道认证」可被机器验证（对同一层注入不同凭据即可观察行为差异）,以及重连策略留在 `session` 不外溢。凭据不入 `ConnectionEvent` / `ClientConnectionSnapshot` / `EncodedFrame`、相关类型 `ToString()` 不回显凭据字节,由测试断言。
- 决策 2 的直接代价:**CC-3「上行帧改为 Envelope 形状」本轮不交付**,`SessionWireBytes.BaselineAck` 的魔数保留原样。原因不是选型,而是本仓当前**没有任何可用编解码器**:架构源 ADR-048（`0338c86`）虽已让 `packages/csharp/*` 多目标 `netstandard2.1;net8.0`、解除 TFM 墙,但那些产物是提交进仓的生成源码、不上任何 feed,消费模型是字节级只读镜像加 sha256 锁,而本仓无镜像目录、无 sync/verify 脚本、`tests/Fixtures/index.json` 的 `upstreamCorpusPin` 仍为 `unpublished`。解冻需先有一张本仓侧 vendor / mirror 卡;解冻时必须在同一改动内同步 `eng/upstream-contract-smoke/Program.cs`——它遇到任何非 `blocked-unpublished` 的别名状态直接 `return 3`。
- 决策 2 是**落点裁决,不是永久形态**。消费通道落地后若要把上行构造移进生产库,新增 ADR 取代本条第 2 项,不改写本记录。
- 决策 3 把远程传输与 LocalEmbedded 放进同一程序集,二者共享 `IClientConnection` 上层语义;代价是 `modules/connection` 的内部文件数增长,收益是零闸门改动、零新增 NuGet、零 allowlist 变更。
- 三项裁决均不触碰架构源 D-009（RPC/Message dispatch）与 D-011（Auth wire）冻结面,不改 Server 侧所有权。与 LumioServer 双向确认的常量（`productId` / `gameReleaseId` / `protocolVersion` / WS 子协议名与三段位序 / close 1008 语义）**都不是公共契约**,D-011 冻结凭据承载方式后即改用公共形态并删除私有约定。
- 详细设计、逐条现状回应、阻塞清单与引用纪律在 [`docs/specs/2026-08-28-client-a1-wss-design.md`](../../docs/specs/2026-08-28-client-a1-wss-design.md) 维护;本记录只留决策与理由。

## 修订记录

### 2026-09-03 · ADR-058 §17 开放 Client 对 Runtime ECS 的工程引用

按 ADR-058 第 17 条，Client 生产模块 `Lumio.Client.Replica` 与 `Lumio.Client.Bot` 直接引用 Runtime `Lumio.GameRuntime.Ecs`、`Lumio.GameRuntime.Replication` 与 `Lumio.GameRuntime.Samples.Username.Client`（均 netstandard2.1）。`eng/project-reference-allowlist.json` 同步放行这些边。本修订不改写上文 A1 三项落点裁决。

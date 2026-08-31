# hello

> MS-00002 Hello World 里程碑的独立 Headless Bot:按 hello-wire-v1 契约直连 server 的最小客户端与 CLI 进程。

## 状态

- 阶段:Wave 4 交付(浏览器端到端由集成阶段验收)
- 优先级:P1
- 架构基线:`LGE-V1.2-2026-08-27`
- 公共契约来源:架构仓 `engine/wire/hello-wire-v1.json`(唯一 wire 真值,本模块零副本:字段清单、limits、botTrace 词表全部运行时从契约文件解析)
- 内部设计:[`LumioClient 模块化架构`](../../docs/specs/2026-08-27-client-module-architecture-design.md)

## 责任

- 封装 ClientWebSocket 流程:子协议连接、Handshake/HandshakeAck、FullSnapshot/BaselineAck、InputCommand 发送与 Delta 校验。
- 按契约 required 字段动态核对收发消息:缺字段、坏 payloadSha256、revision 非严格递增、未知 messageType 均判失败。
- 提供 Headless Bot CLI 进程:等待 browser Delta 作为发送前提,发送一条 Hello 命令,写 result JSON 与 NDJSON trace。
- trace 事件种类与必缺字段校验按契约 `process.botTraceEventKinds` 执行,残缺审计行直接抛出而非落盘。

## 明确不负责什么

- 不复制或派生第二份 wire 契约;不实现简化版握手、基线或增量协议。
- 不复用 `session`/`connection` 等生产模块(Hello World 里它们尚未存在对应通道),也不被其他模块依赖。
- 不拥有 Server 权威状态、tick 调度或 Delta 路由;断线重连、Resync 不在本里程碑范围。
- 不做浏览器渲染;浏览器侧见 [`web`](../web/README.md)。

## 公共入口与出口

**入口:** `HelloBotCli.RunAsync(args)`(CLI 参数见 `--url/--role/--contract/--trace/--result/--client-name/--baseline-timeout-ms/--command-delay-ms`,退出码 0/1/3)、`HelloWireClient` + `HelloWireClientOptions`、`HelloContract.Load(path)`、`BotTrace(path, contract)`。

**出口:** result JSON(`{ok, role, sessionId, received, sent, maxLatencyMs}` 或 `{ok:false, reason, ...}`)、NDJSON trace(`bot_started/connected/handshake_ack/baseline_received/baseline_ack_sent/delta_received/command_sent/command_result/error_received/bot_finished`)。

## 数据与控制流

1. 解析参数(错则退出码 3)并从 `--contract` 路径加载 hello-wire-v1.json。
2. 写 `bot_started`,ClientWebSocket 以契约 subprotocol 连接。
3. 发 Handshake{role, clientName, contractId},核对 HandshakeAck 的 accepted 与 contractId。
4. 等 FullSnapshot,回 BaselineAck{revision}(超时 = `--baseline-timeout-ms`,默认取契约 limits)。
5. **等待 sender=browser 的 Delta**——这是发送自己命令的前提。
6. 发 InputCommand{sender=role, sequence=1, kind=hello, payload="Hello World", payloadSha256, sentAtMs}。
7. 等服务器正常关闭,写 result JSON,退出码 0;任一步失败则写失败 result 并退出码 1。

## 依赖

- 允许依赖:仅 BCL(net10.0,含 System.Text.Json);零 NuGet 包、零仓库内 ProjectReference。
- 外部依赖:架构仓 hello-wire-v1.json 文件(路径由调用方/测试给出,不内嵌副本)。
- 禁止依赖:本仓其他模块、架构仓工程、Unity/HybridCLR、Server 实现。

## 生命周期与线程模型

- 单连接单流程:`Connecting -> Handshaking -> Baseline -> WaitingBrowserDelta -> CommandSent -> Closed`,任一步失败即终态 Failed。
- 一个后台接收循环驱动全部入站消息;等待方用 TaskCompletionSource 交接,不轮询。
- 发送侧经信号量串行化;进程退出前 abort 连接并 flush trace。

## 失败与恢复

- 参数错误 → 退出码 3;连接/握手/超时/断线/校验失败 → 失败 result + 退出码 1。
- 坏 payloadSha256、revision 非严格递增、未知 messageType、Error 消息 → 立即失败,不静默丢弃(fieldSemantics 要求)。
- 服务器中途断线 → 失败,但 trace 必须完整(含 `bot_finished{exitOk:false}`)。
- 本里程碑无重试:Hello World 场景一轮即终态。

## 可观测性

- NDJSON trace 每行 `{kind, receivedAtMs, ...契约必填字段}`,逐行 flush,供集成验收对账。
- result JSON 供 Playwright/集成启动器断言;stderr 只写人类可读失败说明。

## 验证

- `dotnet build modules/hello/src`、`dotnet build modules/hello/host`、`dotnet test modules/hello/tests`。
- 成功路径:完整流程 + 「等 browser Delta 后才发送」的顺序断言。
- 失败矩阵:坏 hash Delta、未知 messageType、baseline 超时、中途断线(trace 完整)、契约 contractId 不符、Error 消息、参数错误退出码 3。
- HelloContract 用例直接读架构仓真身文件(缺省兄弟仓相对定位,LUMIO_HELLO_WIRE_CONTRACT 可覆盖;找不到按 Skip 语义跳过)。

## 目录

- `src/`:HelloContract、HelloWireClient、BotTrace、HelloBotCli(net10.0 类库)。
- `host/`:Lumio.Client.HelloBot 控制台进程(仅组合 CLI 入口)。
- `tests/`:契约 speaking 的 loopback server + 成功路径与失败矩阵(xUnit v3)。

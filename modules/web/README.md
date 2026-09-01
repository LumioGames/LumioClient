# web

> MS-00002 Hello World 的浏览器客户端:纯静态、无构建步骤、无框架、无外部资源的 ES module 页面。

## 状态

- 阶段:Wave 4 交付(浏览器逻辑的端到端验收由集成阶段 Playwright 执行)
- 优先级:P1
- 架构基线:`LGE-V1.2-2026-08-27`
- 公共契约来源:架构仓 `engine/wire/hello-v1.json` 同源的 `hello-wire-v1.json`;页面运行时 `fetch('./contract.json')` 读取,集成方负责把契约文件复制到页面旁边

## 责任

- 以 `browser` 角色完成 hello-wire-v1 客户端流程:Handshake → HandshakeAck → FullSnapshot → BaselineAck → InputCommand → Delta 渲染。
- 全程把状态写 `window.__lumioResult`,形状严格按契约 `process.evidence.browserResult`。
- 消息字段核对由 fetch 到的契约 `messages.*.required` 动态驱动,页面不硬编码字段清单(浏览器不另写协议真值)。

## 明确不负责什么

- 不复制契约内容进页面/脚本;不定义第二份字段清单、错误码或事件词表。
- 不做构建、打包、CDN 或外部资源引入;不依赖任何框架。
- 不拥有 Server 或 Bot 侧行为;不产出 result 文件(证据在 `window.__lumioResult`)。

## 公共入口与出口

**入口:** URL query `ws`(server ws 地址)与 `role`(默认 browser);同目录 `contract.json`。

**出口:** `window.__lumioResult = {status: running|ok|error, role, sessionId, baselineRevision, sent:{sequence,payloadSha256,sentAtMs}, received:[{sender,sequence,tickId,revision,payloadSha256,latencyMs}], errors:[{code,detail}]}`;页面上的状态与 Delta 列表渲染。

## 数据与控制流

1. 读 query 参数;fetch 契约并校验 `contractId` 存在。
2. `new WebSocket(url, contract.transport.subprotocol)` 连接。
3. HandshakeAck(accepted) → FullSnapshot → 发 BaselineAck{revision}。
4. WebCrypto 计算 payload SHA-256(小写 hex)后发一次 InputCommand。
5. 收 Delta:按契约 required 核对字段、重算 hash、revision 严格递增、latencyMs=Date.now()-originSentAtMs;渲染并记录。
6. 收 Error → errors 入列、status="error";连接关闭时 status 保持(已收到反向 Delta 则为 "ok")。

## 依赖

- 允许依赖:浏览器原生 API(ES module、WebSocket、WebCrypto、fetch)。
- 外部依赖:运行时同目录的 `contract.json` 文件。
- 禁止依赖:构建工具、npm 包、CDN/外部资源、本仓 .NET 工程。

## 生命周期与线程模型

- 页面加载即启动(async IIFE,全路径 try/catch,无未捕获 rejection)。
- 无 server/无契约时呈现 waiting 状态且不产生 console 错误噪音。
- 单 WebSocket、事件驱动;close 后不再重连(Hello World 一轮即终态)。

## 失败与恢复

- 契约缺失/损坏 → waiting 状态,不连接。
- 消息缺字段、坏 hash、revision 回退、未知 messageType → errors 记录(取契约 errorCodes 词表内的码)+ status="error"。
- 服务器 Error → errors 记录(码取自契约 errorCodes 词表) + status="error"。
- HandshakeAck accepted=false → errors 记录 + status="error";此路径无服务器 Error 消息,页面以本页合成码 handshake_rejected 标注(页面本地状态码,有意不进 wire 词表,不外发)。
- 本里程碑无重试与恢复路径。

## 可观测性

- `window.__lumioResult` 是唯一机器可读证据出口(Playwright 断言目标)。
- 页面渲染 status/session/baseline/received/errors;Error 级事件 console.error。

## 验证

- `node --check modules/web/hello/hello-client.js`(语法级)。
- 行为验证由集成阶段 Playwright 实测(真实 server + 契约文件),本仓不做浏览器自动化测试。

## 目录

- `hello/`:`index.html`、`hello-client.js`、`style.css`。纯静态,任意静态文件服务器可直接托管。

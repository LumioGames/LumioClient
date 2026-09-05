# spikes/runtime-wasm — CL-1 调研探针（Runtime 客户端模块跑在浏览器 WASM）

> 调研卡 `bomber-engine/CL-1`。报告：[`docs/spikes/2026-09-05-spike-runtime-wasm.md`](../../docs/spikes/2026-09-05-spike-runtime-wasm.md)。
> 本目录**不是生产代码**：不进 `modules/`、不进 `eng/project-reference-allowlist.json`、不进 CI、不进 `LumioClient.slnx`。
> 本目录自带 `Directory.Build.props / .targets / Directory.Packages.props`，不 import 仓根的（仓根强加 netstandard2.1 / LangVersion 9.0）。

## 布局

| 路径 | 是什么 |
|---|---|
| `bench/` | 共享代码（net10.0 类库）：客户端世界拉起、C-1 解包 / 编包桥、`CreateFromSnapshot` 重建基准、世界哈希、表现差集样例、运行环境探针。桌面基准与 wasm 应用同一份代码。 |
| `app/` | `.NET 10 browser-wasm` 应用（`dotnet new wasmbrowser` 模板形状）。`Program.cs` 是 `[JSExport]` 面；`wwwroot/main.js` 只搬字节、画 Canvas、记时间；`wwwroot/index.html` 页面。 |
| `host/` | **C# 探针宿主（macOS 替身）**。现行 Rust 宿主 `LumioServer lumio-entity-chat-replay` 在 macOS 编不过（见报告 §2），本工程用 Runtime 公开 API（`EntityBindingQuery` + `ChatCommandRuntime` + `ChatEnvelope`）复现同一条 C-1 wire。 |
| `tools/snapshot-gen/` | 用样板服务器注册表生成 `world-{N}.lwm1` 快照（浏览器重建基准的输入）。 |
| `tools/desktop-bench/` | 桌面 net10.0（CoreCLR）跑同一份重建基准与哈希，作对照与逐位比对。 |
| `tools/static-server.mjs` | 零依赖静态文件服务（`--no-store` 测空缓存，`--coop-coep` 发多线程所需响应头）。 |
| `tools/measure.mjs` | Playwright（Chromium headless）测量脚本：startup / rebuild / wire / interop / timers / draw。 |
| `tools/sizes.mjs` | 产物体积（未压缩 / gzip -9 / brotli -q 11，按类别）。 |
| `tools/measure-js-baseline.mjs` | 纯 JS 聊天页对照：本仓 `modules/web/chat` 的体积 / 冷启动，LumioGame `integration/entity-chat/web` 连宿主到首帧。 |
| `tools/persistent-startup.mjs` | 对照实验：Playwright 持久化 profile 连载同一页 N 次（磁盘 + V8 代码缓存是否改变启动时间）。 |
| `tools/summarize.mjs` | 把 `results/*.jsonl` 汇总成 Markdown 表。 |
| `results/` | 本次调研的原始输出（JSON 行），报告只引用不重抄。 |

## 环境（本次实测）

- .NET SDK 10.0.400（仓根 `global.json` 钉死；`node eng/verify-sdk-pin.mjs` 校验），RID `osx-x64`（Apple M5 上的 Rosetta）。
- 工作负载：`dotnet workload install wasm-tools wasm-experimental`（10.0.111 / manifests 10.0.100；本机安装耗时见报告）。
- Node 26.x；Playwright 1.63.0（`tools/package.json`，`npm install`）；Chromium 由 Playwright 自带。
- 姊妹仓：`../LumioGameRuntime`（或 `LUMIO_RUNTIME_ROOT`）；本目录经 `ProjectReference` 引用其 `Ecs / Replication / Samples.Username.{Client,Server}` 工程，不改 Runtime 任何文件。

## 复现

所有命令在 `spikes/runtime-wasm/` 下执行。`LumioGameRuntime` 的 `samples/username` 目录同时放着 Server / Client 两个 csproj（共用 `obj/`），**不要并行跑两个 dotnet 构建**。

```bash
# 0. 一次性
dotnet workload install wasm-tools wasm-experimental
(cd tools && npm install)

# 1. 快照 fixture（浏览器重建基准输入；生成物不入库）
dotnet build tools/snapshot-gen -c Release
dotnet run --no-build -c Release --project tools/snapshot-gen -- app/wwwroot/fixtures 100 300 1000

# 2. 桌面对照（同一份基准代码；输出 REBUILD 行，含哈希）
dotnet build tools/desktop-bench -c Release
dotnet run --no-build -c Release --project tools/desktop-bench -- app/wwwroot/fixtures 5 7

# 3. 探针宿主（C-1 wire；HOST_READY 行给出 ws 地址；--delay-ms 150 模拟单向 150 ms）
dotnet build host -c Release
dotnet run --no-build -c Release --project host -- --port 47311 --bots 100 --bot-chat-per-tick 1 --tick-hz 20
dotnet run --no-build -c Release --project host -- --port 47312 --bots 100 --bot-chat-per-tick 1 --tick-hz 20 --delay-ms 150

# 4. wasm 应用：三个变体（裁剪开 = 默认；ILLinkTreatWarningsAsErrors=false 是因为 Runtime Ecs 有 4 处 IL2075，见报告 §4 A1-4）
dotnet publish app -c Release -o publish/default -p:ILLinkTreatWarningsAsErrors=false
dotnet publish app -c Release -o publish/notrim  -p:PublishTrimmed=false   # 能发布，但浏览器里起不来（TypeLoadException，见报告 §4 A2-1），只用来量体积
dotnet publish app -c Release -o publish/aot     -p:RunAOTCompilation=true -p:ILLinkTreatWarningsAsErrors=false
node tools/sizes.mjs publish/default/wwwroot

# 5. 起静态服务（两个：正常缓存 / no-store）
node tools/static-server.mjs --root publish/default/wwwroot --port 47380
node tools/static-server.mjs --root publish/default/wwwroot --port 47381 --no-store

# 6. 测量（RESULT 行即证据；results/ 里是本次的原始输出）
node tools/measure.mjs startup --page http://127.0.0.1:47381/ --runs 5            # 空缓存
node tools/measure.mjs startup --page http://127.0.0.1:47380/ --runs 5 --warm     # 热缓存
node tools/measure.mjs rebuild --page http://127.0.0.1:47380/ --fixtures 100,300,1000 --inputs 5 --repeats 7
node tools/measure.mjs wire    --page http://127.0.0.1:47380/ --ws ws://127.0.0.1:47311/ --seconds 300 --chats 5
node tools/measure.mjs wire    --page http://127.0.0.1:47380/ --ws ws://127.0.0.1:47312/ --seconds 30  --chats 5
node tools/measure.mjs interop --page http://127.0.0.1:47380/ --runs 5
node tools/measure.mjs timers  --page http://127.0.0.1:47380/
node tools/measure.mjs draw    --page http://127.0.0.1:47380/ --runs 5
node tools/measure.mjs worker  --page http://127.0.0.1:47380/worker.html --runs 3          # 整个运行时挪进 Web Worker
node tools/measure.mjs timers  --page http://127.0.0.1:47380/ --background --ws ws://127.0.0.1:47311/ --headed   # 后台标签页节流（需要有界面的 Chrome）
node tools/measure-js-baseline.mjs --chat-page http://127.0.0.1:47382/index.html --game-page http://127.0.0.1:47383/index.html --ws ws://127.0.0.1:47311/ --runs 5 --channel chrome
node tools/persistent-startup.mjs http://127.0.0.1:47380/ 4
node tools/summarize.mjs results                                                            # 汇总成 Markdown 表
```

`measure.mjs` 默认用 Playwright 自带 Chromium；本次全部数字用 `--channel chrome`（本机 Google Chrome 152.0.7977.82，headless new）。所有命令都可加 `--channel chrome`。

手动看页面：浏览器打开 `http://127.0.0.1:47380/?ws=ws://127.0.0.1:47311/&connectionId=c-browser`，控制台 `window.__spike` 是全部机器可读状态（`frames` / `sent` / `roundtrips` / `timings`），`await __spike.sendChat("hi")` 发一条真实 InputCommand。

## 边界（照卡面）

- 不写正式客户端、不实现预测世界（RT-3）、不改 Runtime、不改 `modules/web`、不在 JS 里写任何 C-1 / LumioBinV1 解析。
- 重建基准是**近似**：`CreateFromSnapshot` + N 个 WorldChange 包一次 Tick 批量应用，正式的整体克隆 + 重放归 RT-3。
- 探针宿主替身按 `connectionId` 直接准入，不复现 Account Server 凭证验签（Rust 侧 `verify_admission`）。

## 踩过的坑（复现时别再踩）

- `TreatWarningsAsErrors` 下 `[JSExport]` 报 CA1416：加 `Properties/AssemblyInfo.cs` 的 `[assembly: SupportedOSPlatform("browser")]`（与官方 wasmbrowser 模板一致）。
- 默认 publish（裁剪开）失败：Runtime Ecs 4 处 IL2075 被当成错误 → 传 `-p:ILLinkTreatWarningsAsErrors=false`（不改 Runtime）。
- `[JSExport]` 在 JS 里按命名空间挂：`exports.Lumio.Client.Spike.RuntimeWasm.SpikeExports.X()`，不是 `exports.SpikeExports.X()`。
- 在 Web Worker 里起运行时：**不要**在 `import('./_framework/dotnet.js')` 之前给 `self.onmessage` 赋值（dotnet.js 据此把自己当 pthread worker 等主线程，`dotnet.create()` 永不返回）；用 `addEventListener('message', …)`。
- `PublishTrimmed=false` 的产物在浏览器里起不来（`TypeLoadException … RuntimeInformation`），只能拿来量体积。
- Playwright 1.63 自带的 headless shell 未安装时，用 `--channel chrome` 走本机 Chrome。

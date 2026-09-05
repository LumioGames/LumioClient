# SPIKE-RUNTIME-WASM 调研记录 — 浏览器能不能、以什么代价跑 Runtime 的客户端模块

- 卡：`bomber-engine/CL-1`（LumioGameEngine `plans/2026-09-05-bomber-engine-runtime-cards.md`）
- 日期：2026-09-05
- 结论：**桌面 Chrome 能跑，但不便宜；手机全部未执行（无真机）；Owner 定路线**
- 各仓 SHA：见 §2.2；本仓 HEAD `18020a117b8856fd74369c760d9b161ef083174f`
- 探针：[`spikes/runtime-wasm/`](../../spikes/runtime-wasm/README.md)（复现命令在其 README）；原始输出 [`spikes/runtime-wasm/results/`](../../spikes/runtime-wasm/results/)

> **文档事实性声明**：所有「官方事实」来自 2026-09-05 抓取的官方页面，附 URL 与原文片段；所有「实测」附命令、原始输出位置、方法与次数。**未执行的项一律标注「未执行」**，桌面 Chrome 的数据不冒充手机，本机 iOS Simulator 的补充数据单独标注且不计入真机结论。本卡不替 Owner 定路线，也不改 LumioGame ADR 0013 的产品承诺。

## 0. 给 Owner 的大白话结论

1. **能编、能跑、能对话。** Runtime 的三个客户端程序集（Ecs、Replication、样板 Username.Client）一行不改就编成了 .NET 10 browser-wasm；在 Chrome 152 里起了客户端世界，用 Runtime 自己的 C# codec 解出了服务器发来的真实 FullSnapshot（4 KB，101 个实体的身份块）和每秒 20 帧的 Delta，也把 Runtime `Say()` 产出的一条真实 InputCommand 发到宿主并被接受、回声回来。JS 一个字段都没解析。
2. **同一份规则代码在浏览器和桌面算出的世界哈希逐位相同**（100 / 300 / 1000 实体三档都对上），一处维护这条路在正确性上成立。
3. **代价一：启动时间在 0.6–5.6 s 之间，取决于谁来起浏览器。** Playwright 起的 Chrome 152（有窗口 / 无窗口 / 持久 profile 都一样）：空缓存到「WorldManager 可用」5.2–5.6 s，热缓存 4.1–4.4 s，AOT 版 4.9–5.9 s / 4.6 s；Claude 桌面应用内嵌 Chromium 148 是 1.05 s、热重载 0.13 s；iOS 模拟器 Safari 0.62 s。后两个是「用户口径」旁证，前者是「CI 口径」；差异原因本卡没裁决（真实 Chrome 152 无自动化的测法因扩展未连接没做）。现有纯 JS 聊天页 0.44 s。实际下载量（按语言只取一份 ICU）：默认版未压缩 5.3 MB、brotli 后 1.6 MB；AOT 版 10.2 MB / 2.5 MB；再去 ICU 还能省 0.14 MB（brotli）。
4. **代价二：不开 AOT 重建抖，开了就够用。** 100 / 300 / 1000 实体的世界从快照重建 + 应用 5 条输入：解释模式跑 30 次，稳定后中位 4.5 ms（300 实体）/ 8.6 ms（1000 实体）——占 50 ms 帧预算 9 % / 17 %，但预热期单次能到 0.35–1.1 s，且每档都有超 100 ms 的毛刺；**AOT 后中位 0.5–1.7 ms（1000 实体 3.4 % 预算），最差 11–154 ms**（首次）。桌面 CoreCLR 同一代码 0.24–2.4 ms。AOT 发布只多花 57 s、包大 0.9 MB（brotli）。
5. **代价三：真实 Rust 宿主今天连不上——它是 Windows-only。** 本机 macOS 编不过（`kernel32`、Windows-only loader），且它反射调用的 Runtime 方法在 Runtime HEAD 已被删。本卡用 Runtime 公开 API 写了个 C# 替身宿主走同一条 wire，所有帧仍由 Runtime codec 产出。
6. **裁剪器对 Runtime Ecs 报了 4 处反射警告（IL2075）**，默认设置下 publish 直接失败；关掉「警告即错误」后能过，运行时功能与哈希未受影响（本卡范围内）。不裁剪的产物反而在浏览器里起不来（BCL 类型转发缺失）。这是 Runtime 侧要修的坑，不是浏览器的锅。
7. **单线程模型能成立**：`WorldManager.Start(Thread.CurrentThread)` 的归属检查在 wasm 主线程通过，`new Thread` 直接抛 PlatformNotSupportedException（单线程运行时）；整个运行时挪进 Web Worker：成立——运行时与 WorldManager 整体在 Worker 里跑通、哈希一致；多线程 wasm 需要站点发 COOP / COEP，本卡未开。
8. **手机：一台真机都没有，轨道 B 全部未执行**（设备清单在 §4 B）。官方文档明说 iOS Safari 对大 wasm 模块与内存更苛刻，别拿桌面数据当手机结论。
9. **按键到画面**：本地预测路径（按键 → C# 更新预测世界 → Canvas 画出）中位 1–3 ms、最差 35–41 ms（撞上一帧刷新）；走服务器往返 + 一个 tick 的路径在 0 ms 链路上中位 21–40 ms，单向 150 ms 链路上 中位 333.3 ms（318.0–400.8）——这就是路线 B「只画不预测」每次按键要等的时间。
10. **一句话推荐在 §5**：数据支持「桌面浏览器走路线 A（.NET WASM）」作为一处维护的方向，但要先把启动 / AOT / 裁剪三件事的预算定下来；手机不下结论。

**一个例子：玩家在浏览器里按右键，字节走了哪几步、每步多少毫秒（桌面 Chrome 152 实测中位；手机未测）**

1. 按键事件到达页面 JS：≈ 0 ms（浏览器事件）。
2. JS 调 C# `[JSExport]`：一次调用 ≈ §4 A3-1 的微秒级（int 参数）；带字符串参数也在微秒级。
3. C# 在预测世界里应用这条输入（近似：owner 字段本地写 + 自动上行入 outbox）：0.1–0.3 ms。
4. C# 把差集交给 JS、Canvas 画 100 个实体：0.2 ms / 帧（打包 int[]）或 0.4 ms / 帧（JSON）。
5. 画面出现：从按键算起中位 1–3 ms、最差 35–41 ms（等下一次 requestAnimationFrame，最差落在一帧之外）。——**这一步玩家已经看到人动了。**
6. 同一条输入经 C# 编成 InputCommand（21 字节载荷、208 字节 JSON 帧）：首次 7–592 ms（解释器预热，AOT 版 24–50 ms），之后 0.3–13 ms。
7. WebSocket 到宿主、等下一个 20 Hz tick、宿主用 Runtime 编 Delta 发回：0 ms 链路上往返中位 40 ms（解释版，25–71 ms）/ 21 ms（AOT 版，17–32 ms）；单向 150 ms 链路上 中位 333.3 ms（318.0–400.8）。
8. C# 解包回来的 Delta：稳定后 0.3–0.7 ms（第一帧 110–285 ms 预热）。
9. 慢在哪：**不是 wire，是「起来」和「预热」**——运行时启动 3.4–4.4 s，每条新代码路径第一次跑要几十到几百毫秒；稳定后每包解包与每帧绘制都在 1 ms 内，重建世界解释模式抖、AOT 后进预算（§4 A2-3）。
## 1. 卡面验收逐条对照

| 验收项（卡面 §9 原文） | 结论 | 证据位置 |
|---|---|---|
| 轨道 A 的 A1–A4 每条有实测数据与复现步骤 | **达成**（A1-1..5、A2-1..5、A3-1..3、A4-1..3 各有数字或原文；复现在 `spikes/runtime-wasm/README.md`） | §4.A |
| WASM 页面加载 Runtime 程序集、连上现行宿主 | **部分达成**：程序集加载达成；「现行宿主」= LumioServer Rust 宿主在 macOS 编不过且与 Runtime HEAD API 漂移，改连 C# 替身宿主（同一份 Runtime codec 产帧） | §2.4、§4.A A1-2 |
| C# codec 在浏览器里解出一条真实权威包 | **达成**（FullSnapshot 4320 B + Delta chat.event，字段与帧头在 A1-2） | §4.A A1-2 |
| 编出一条被宿主接受的输入（控制台输出 + 抓包） | **达成**（208 B InputCommand 帧原文、宿主 `admitted=True` 日志、回声事件） | §4.A A1-3 |
| 轨道 B 至少两台真机的 B1–B4 数据 | **未达成——全部「未执行」**，附设备清单与执行方法 | §4.B |
| 没有真机的项标「未执行」并附设备清单；桌面仿真单独标注 | **达成**（未做桌面仿真；iOS Simulator 补充数据单独标注） | §4.B |
| 三条路线对照表齐全 | **达成** | §5.1 |
| 路线 B 的延迟是实测 | **部分达成**：往返 + tick 是同一 wire 上的实测；JS 侧解包 / 上行无现成实现（且禁写 JS codec），按键到画面为「实测往返 − 实测 C# 编解码」的推导，已标注 | §4.A A3-3、§5.1 |
| 推荐一句话 + 理由；ADR 草案建议 | **达成** | §5.2、§5.3 |
| modules/**、tests/**、CI、global.json、allowlist、Runtime 零改动（git status 证据） | **达成**（`git status --short` 只有 `spikes/` 与 `docs/spikes/2026-09-05-spike-runtime-wasm.md`；Runtime / Server / Engine / Game 四仓工作树干净） | §7 |
| verify-sdk-pin 与 ArchitectureTests 全绿 | **达成**（输出见 §7） | §7 |
| 探针可复现 | **达成**（README 命令 + results/ 原始输出） | `spikes/runtime-wasm/README.md` |
## 2. 环境与可测性

### 2.1 宿主架构声明（Rosetta）

本机 **Apple M5（arm64，10 核 = 4 性能 + 6 能效，16 GB）**，.NET SDK 跑在 **`RID: osx-x64`（Rosetta 2 下的 x86_64）**；wasm 工作负载装的也是 `osx-x64` 包（Emscripten 3.1.56 `osx-x64`、AOT 交叉编译器 `Microsoft.NETCore.App.Runtime.AOT.osx-x64.Cross.browser-wasm`），全部经 Rosetta 运行。

```
$ uname -m
arm64
$ sysctl -n machdep.cpu.brand_string
Apple M5
$ dotnet --info | head -12
.NET SDK:
 Version:           10.0.400
 Commit:            14fbf8d527
 Workload version:  10.0.400-manifests.330ea142
 MSBuild version:   18.9.6+14fbf8d52
Runtime Environment:
 OS Name:     Mac OS X
 OS Version:  26.5
 OS Platform: Darwin
 RID:         osx-x64
 Base Path:   /usr/local/Cellar/dotnet/10.0.400/libexec/sdk/10.0.400/
```

**对数字的影响：**
- **构建耗时**（工作负载安装、`dotnet publish`、Emscripten 重链接、AOT 编译）全部在 Rosetta 下测得，只作量级，不作 CI 预算。
- **浏览器内的数字不受 Rosetta 影响**：Chrome 是原生 arm64 进程（Playwright 起的 `Google Chrome 152.0.7977.82` 与 Playwright 自带 Chromium 均为 `chrome-mac-arm64`），wasm 由 Chrome 的 V8 编译执行，与 SDK 是 x64 无关。桌面对照基准（`tools/desktop-bench`，CoreCLR）跑在 Rosetta x64 下，**只能作序关系**（它比 wasm 快多少倍这个比值会偏小，因为 CoreCLR 一侧被 Rosetta 拖慢）。
- **本机不是目标设备**：桌面 Chrome 数据 ≠ 手机浏览器数据，见 §4 轨道 B。

### 2.2 各仓 origin/main SHA（开工时钉定，全部工作树干净且 == origin/main）

| 仓 | SHA | 说明 |
|---|---|---|
| LumioClient（本仓） | `18020a117b8856fd74369c760d9b161ef083174f` | 探针与报告落在其上的新增文件 |
| LumioGameRuntime | `010ae46f87eb9aa6ad0c6075ffa86054f9f6f335` | 被 `ProjectReference` 的 Ecs / Replication / Samples.Username.{Client,Server}；**零改动** |
| LumioServer | `4c7688b7aacdd037f08ef22f053a3d9e6af7e5a7` | 现行 Rust 宿主来源；**零改动**（在 macOS 编不过，见 §2.4） |
| LumioGameEngine | `d23d671c549109aa5f52531a0073cd4bb0d56a67` | 真值文档与 `engine/wire/*.json`（工作树 `engine/` 干净） |
| LumioGame | `5bc5afc3c29b607945b70b98751621a4301ef771` | ADR 0013；`integration/entity-chat/web` 作纯 JS 对照页 |

C-1 形态：`engine/wire/gameplay-command-envelope-v1.json` 在上述 SHA 的消息集仍是 **InputCommand / FullSnapshot / Delta / Error / ConnectionSuperseded**（R5-01 的 Welcome / WorldChange / `sequence` / `appliedInputSequence` 尚未合入），本卡全部数字按此形态。

### 2.3 工具链版本

| 项 | 版本 / 来源 |
|---|---|
| .NET SDK | 10.0.400（`global.json` 钉死，`rollForward: disable`；`node eng/verify-sdk-pin.mjs` 通过） |
| 工作负载 | `wasm-tools 10.0.111/10.0.100`、`wasm-experimental 10.0.111/10.0.100`（`dotnet workload list`）；包落在 `~/.dotnet/packs/`（Homebrew SDK 目录只读时的用户级落点） |
| Emscripten | 3.1.56（`Microsoft.NET.Runtime.Emscripten.3.1.56.Sdk.osx-x64/10.0.11`） |
| 模板 | `dotnet new list wasm` → `wasmbrowser`（WebAssembly Browser App）、`wasmconsole`、`blazorwasm` |
| Node / npm | 26.4.0 / 11.17.0 |
| Playwright | 1.63.0（`spikes/runtime-wasm/tools/package.json`）；驱动的浏览器 = 本机 **Google Chrome 152.0.7977.82**（`channel: chrome`，headless new）；未用 Playwright 自带 Chromium（其 headless shell 1243 未安装） |
| Rust | LumioServer 钉 1.98.0；LumioGameEngine native 钉 1.89.0（rustup 均已装） |

### 2.4 本机具备 / 不具备

| 能力 | 状态 | 证据 |
|---|---|---|
| .NET 10 browser-wasm 编译 / 发布 / AOT | 具备 | §4 A1-1、A2-1 |
| Chrome 152 桌面（headless 与 headed） | 具备 | §4 轨道 A |
| **现行 Rust 宿主 `lumio-entity-chat-replay` / `lumio-server`** | **不具备（macOS 编不过）** | 下方原文 |
| Account Server（C#）、entity-chat HostEntry（C#） | 可编（`dotnet build` 全绿）但无宿主可装载 | §2.4 |
| **真机 iPhone / Android** | **不具备** | §4 轨道 B 全部「未执行」 |
| iOS Simulator（本机 Xcode，iPhone 17 / iOS 26.5 runtime） | 具备，**不算真机** | §4.B 末尾补充数据（单独标注）；Claude Code 模拟器面板因 `xcode-select` 未指向 Xcode（需 sudo）不可用，用 `xcrun simctl` 驱动与截图 |
| 微信内置浏览器 | 不具备 | 未执行 |

**现行 Rust 宿主在 macOS 编不过（原文）。** `cargo build -p lumio-server-process --release`（toolchain 1.98.0-x86_64-apple-darwin，按 `rust-toolchain.toml`）：

```
error: constant `ENTRY_SYMBOL` is never used
  --> modules/host-runtime/src/native_timer.rs:10:7
   = note: `-D dead-code` implied by `-D warnings`
error: type alias `GetApiV1` is never used
  --> modules/host-runtime/src/native_timer.rs:87:6
error: could not compile `lumio-host-runtime` (lib) due to 2 previous errors
```

加 `RUSTFLAGS=--cap-lints=warn` 绕过 lint 后，两个 bin 都在链接期失败：

```
error: linking with `cc` failed: exit status: 1
  = note: ld: library 'kernel32' not found
error: could not compile `lumio-server-process` (bin "lumio-server") due to 1 previous error
error: could not compile `lumio-server-process` (bin "lumio-entity-chat-replay") due to 1 previous error
```

根因在源码里写明是 Windows-only：`modules/process/src/sdk_loader.rs:116` 无条件 `#[link(name = "kernel32")]` + `LoadLibraryW/GetProcAddress`；`modules/host-runtime/src/native_timer.rs:363` `#[cfg(not(windows))] return Err("BLOCKED: NativeCore timer ABI loader is Windows-only")`；架构仓 `engine/native/modules/clr-host/src/sys.rs:3` 注释「MS-00002 Wave 2 只实现 Windows … Unix 的 dlopen 路径未接入」；`eng/dev-build.ps1` 只认 `.dll` / `.so`。**另一处漂移**：LumioServer `entity-chat-host/HostEntry.cs`（1c1dce1，2026-09-03）经反射调用 `ChatCommandRuntime.BuildFullSnapshot / BuildDelta / AdmitInputCommand`，而 Runtime HEAD 的 `ChatCommandRuntime`（7f198e5 之后）只有 `AttachMember / AdmitInput / RunTick`——即便在 Windows，现行宿主也要先对齐 Runtime 才能跑。

**处置（卡面 §11「宿主连不上 → 桌面轨道先用本机宿主」）：** 写了一个 C# 探针宿主 `spikes/runtime-wasm/host/`，用 Runtime **公开** API `EntityBindingQuery.Admit / ChatCommandRuntime.{AttachMember, AdmitInput, RunTick} / ChatEnvelope.{TryParseInputCommand, DeltaFrames}` 复现 Rust 宿主 `entity_chat/{wire,host}.rs` 的 wire 语义（纯 WebSocket、首帧 `{"connectionId"}`、绑定后先发 FullSnapshot、每 tick 广播 Delta、每帧文本当 InputCommand）。**帧字节全部由 Runtime 的 C# codec 产出**，探针宿主不写字段级协议；偏离两处并已标注：① 准入按 connectionId 直接放行，不复现 Account Server 凭证 ed25519 验签（那在 Rust 侧）；② 带 `entity.identity` 记录的 FullSnapshot 在 Runtime HEAD 只有 internal 重载（原经 `ChatCommandRuntime.BuildFullSnapshot` 暴露，已被删），替身经反射调用它以便 FullSnapshot 携带 101 条身份记录（真实包体积）。

### 2.5 可测性判定

- **可测**：A1（编过 / 起世界 / 解真实包 / 编真实上行 / 裁剪与 AOT / 线程）、A2（体积 / 冷启动 / 重建 / 20 包每秒 / 5 分钟 / 定时器）、A3（互操作 / 差集 / 按键→画面）、A4（构建耗时 / headless / 调试）——全部实测，见 §4。
- **部分可测**：路线 B 的「按键→画面」——现有纯 JS 页（本仓 `modules/web/chat`、LumioGame `integration/entity-chat/web`）都**没有上行输入路径**（前者是纯表现模块，后者的 Playwright 场景不注入聊天），且卡面禁止在 JS 写 C-1 codec；所以路线 B 的往返用同一条 wire 上 wasm 页实测的往返减去 C# 编解码耗时给出，标注为推导。
- **不可测**：轨道 B 真机全部；WSS / 落地站点；微信内置浏览器；Safari 桌面 / Firefox（未在范围）。

## 3. 官方事实（抓取日期均为 2026-09-05，UTC）

| # | 事实 | 来源 | 原文片段 |
|---|---|---|---|
| 1 | 非 Blazor 的浏览器 .NET 应用用 `dotnet workload install wasm-tools`；模板在 `wasm-experimental` 工作负载（或 `Microsoft.NET.Runtime.WebAssembly.Templates` 包） | https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop/wasm-browser-app?view=aspnetcore-10.0 | "Install the wasm-tools workload … `dotnet workload install wasm-tools`" / "Optionally, install the wasm-experimental workload, which adds the following experimental project templates: WebAssembly Browser App …" |
| 2 | JS 互操作需 `AllowUnsafeBlocks`；工程 Sdk 为 `Microsoft.NET.Sdk.WebAssembly` | 同上 | "Enable the AllowUnsafeBlocks property, which permits the code generator in the Roslyn compiler to use pointers for JS interop" / `<Project Sdk="Microsoft.NET.Sdk.WebAssembly">` |
| 3 | AOT 只在 publish 时做，开发期不用；用 `RunAOTCompilation=true` | https://learn.microsoft.com/en-us/aspnet/core/blazor/webassembly-build-tools-and-aot?view=aspnetcore-10.0 | "WebAssembly AOT compilation is only performed when the project is published. AOT compilation isn't used when the project is run during development … because AOT compilation usually takes several minutes on small projects and potentially much longer for larger projects." |
| 4 | 多线程是实验特性，默认关；需要站点发 COOP / COEP；**JS 互操作只在主线程**；主线程阻塞不受支持 | https://raw.githubusercontent.com/dotnet/runtime/main/src/mono/wasm/features.md | "Multi-threading experiment is enabled by `<WasmEnableThreads>true</WasmEnableThreads>`, and is currently disabled by default." / "Your HTTPS server and/or proxy must be configured to send HTTP headers similar to `Cross-Origin-Embedder-Policy:require-corp` and `Cross-Origin-Opener-Policy:same-origin`" / "JavaScript interop with managed code via `[JSExport]`/`[JSImport]` is currently limited to the main thread even if multi-threading support is enabled." / "Blocking on the main thread with operations like `Task.Wait` or `Monitor.Enter` are not supported by browsers" |
| 5 | SIMD、异常处理默认开 | 同上 | "WebAssembly SIMD … It is currently enabled by default." / "WebAssembly exception handling … It is currently enabled by default and can be disabled via `<WasmEnableExceptionHandling>false</WasmEnableExceptionHandling>`." |
| 6 | 最大内存默认 2 GiB，iOS Safari 可能拒绝；桌面推荐 256–512 MB，>1 GB 要实测，>2 GB 实验 | 同上 | "The default value is `2,147,483,648 bytes`, which may be too large and result in the app failing to start, because the browser refuses to grant it." / "Recommended size of the memory used by dotnet applications in the desktop browsers is between 256MB and 512MB. If you are using more than 1GB, please make sure that you test it properly. Using more than 2GB is experimental." |
| 7 | iOS / iPadOS 上所有浏览器都是 Safari 引擎；手机浪览器内存限制严、网速慢，桌面能跑的可能在手机下载几分钟或起不来 | 同上 | "all browsers on iOS and iPadOS are required to use the Safari browser engine" / "A WebAssembly application that works well on desktop PCs browser may take minutes to download or run out of memory before it is able to start on a mobile device, and the same is true for .NET." |
| 8 | 裁剪（`PublishTrimmed` / `TrimMode=full`）减小体积、启动与内存；`InvariantGlobalization=true` 可去 ICU | 同上 | "Trimming will remove unused code from your application, which reduces application startup time and memory usage." / "you can make your application smaller by enabling Invariant Globalization via the `<InvariantGlobalization>true</InvariantGlobalization>` msbuild property" |
| 9 | publish 产物默认带 Brotli（最高级）与 Gzip 预压缩，靠宿主协商发出 | https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/webassembly/?view=aspnetcore-10.0 | "compressed during publish to reduce the app's size and remove the overhead for runtime compression. The following compression algorithms are used: Brotli (highest level) Gzip" |
| 10 | Safari 18.2 起支持 Wasm GC（与 .NET 无关，但说明 WebKit wasm 面在演进） | https://webkit.org/blog/16301/webkit-features-in-safari-18-2/ | "WebKit for Safari 18.2 adds support for WASM Garbage Collection." |
| 11 | 浏览器客户端产品承诺（不改）：桌面浏览器优先，触屏浏览器不承诺 | LumioGame `.spec/decisions/0013-logic-first-browser-client-no-engine.md` | "后续客户端暂定浏览器 … 桌面浏览器优先，触屏浏览器不承诺" |
## 4. 实测记录

> 方法总则：桌面轨道全部由 Playwright 1.63 驱动本机 Google Chrome 152.0.7977.82（headless new，`channel: chrome`），页面即 `spikes/runtime-wasm/app`，宿主即 `spikes/runtime-wasm/host`（C# 替身，§2.4）。每条命令在 [`spikes/runtime-wasm/README.md`](../../spikes/runtime-wasm/README.md) 可复现；原始 `RESULT` 行在 [`spikes/runtime-wasm/results/`](../../spikes/runtime-wasm/results/)，本节只摘中位 / 最差与关键字段。「首次」指该代码路径在页面生命周期内第一次执行（解释器预热），「稳定」指其后。

### 4.A 轨道 A：Chrome 桌面浏览器

#### A1-1 能不能编过

**结论：能。** `Lumio.GameRuntime.Ecs` + `Lumio.GameRuntime.Replication`（连带 Command / Config / Coordination / Gas / Observability / GeneratedContracts）+ `Lumio.GameRuntime.Samples.Username.Client` 经 `ProjectReference` 进 `Microsoft.NET.Sdk.WebAssembly` 工程，`dotnet build -c Debug` 通过；Runtime 零改动，TFM 用其 `net10.0` 目标（Runtime 双目标 `net10.0;netstandard2.1`），LangVersion 14 / TreatWarningsAsErrors / AnalysisLevel latest-recommended 全部保持。

```
$ dotnet build app/Lumio.Client.Spike.RuntimeWasm.csproj -c Debug
  Lumio.GameRuntime.Samples.Username.Client -> .../samples/username/bin/Debug/net10.0/Lumio.GameRuntime.Samples.Username.Client.dll
  Lumio.Client.Spike.RuntimeWasm -> .../spikes/runtime-wasm/app/bin/Debug/net10.0/Lumio.Client.Spike.RuntimeWasm.dll
Build succeeded.  0 Warning(s)  0 Error(s)
Time Elapsed 00:00:06.09
```

唯一改在探针自己身上的编译问题：探针工程开着 `TreatWarningsAsErrors`，`[JSExport]` / `[JSMarshalAs]` 触发 **CA1416**（"This call site is reachable on all platforms. 'JSExportAttribute' is only supported on: 'browser'"）；与官方 `wasmbrowser` 模板同法解决——`Properties/AssemblyInfo.cs` 加 `[assembly: SupportedOSPlatform("browser")]`。与 Runtime 无关。

#### A1-2 浏览器里起客户端世界、连宿主、C# codec 解出真实权威包

**结论：成立。** 证据（`results/wire-soak300.jsonl` 首两条 RESULT；对应页面控制台 `[spike] boot …`）：

- `WorldManager.Create(GeneratedRegistry.Instance)`（客户端注册表，`Side=Client`）+ `Start(Thread.CurrentThread)` + 欢迎 / 创建包一次 Tick：`{"ok":true,"self":"10000000000000010000000000000002","selfName":"Browser01","tick":2,"live":2,"bootMs":33.5,"ownerThread":1}`。
- 连上 `ws://127.0.0.1:47311/`（宿主 100 个 Bot + 1 个浏览器玩家）后第一帧 **FullSnapshot 4320 字节**，Runtime `ChatTypedMapping.ApplyDownstream` 解包 `ok=true`（形状、`payloadSha256`、块序、`entity.identity` LumioBinV1 载荷全部校验），首帧解包 151 ms（预热），帧头：`{"messageType":"FullSnapshot","tickId":18107,"revision":108,"stateBlocks":[{"mappingId":"entity.identity","payload":"6b000000020000000000000003000000626f7400000…`（`6b000000` = 107 条身份记录，小端 u32）。
- 之后每 tick 一帧 **Delta**（chat.event），解出的字段例：`{"messageId":18132,"roomSequence":18132,"sender":"10000000000000010000000000000067","text":"c-browser: hello-from-wasm-0","appliedTick":18234}`——`sender` 是 128 位 NetEntityId 的 32-hex（instanceId `1000000000000001` + counter `0x67` = 103 号实体，即浏览器自己）。
- JS 侧代码只做 `socket.addEventListener('message', ev => api.OnFrame(String(ev.data)))`，无任何字段解析（`app/wwwroot/main.js`）。

#### A1-3 上行一条真实 InputCommand 被宿主接受

**结论：成立。** 路径：`World.Self.Get<ChatComponent>().Say(text)`（Runtime 样板）→ 生成的 `[ServerRpc]` 桩 → `WorldManager.EnqueueServerRpc` → outbox 里的 `InputCommandMessage`（`Payload` = Runtime Ecs 内部 `WireCodec.EncodeUtf8` 的 LumioBinV1 字节，21 B）→ 探针把字节装进 C-1 三字段信封（hex + SHA-256）→ Runtime `ChatEnvelope.Validate` 复核 → WebSocket 发出。宿主用 Runtime `ChatEnvelope.TryParseInputCommand` 解出文本，`ChatCommandRuntime.AdmitInput` 接受，下一 tick 的 Delta 带回 `chat.event`。

发出的帧（208 字节）：

```json
{"messageType":"InputCommand","commands":[{"mappingId":"chat.input","payload":"1100000068656c6c6f2d66726f6d2d7761736d2d30","payloadSha256":"6f79e98652d4ea4d1abad385568e15a2cc87a29b975b2b975272aab94452a613"}]}
```

`11000000` = 17（u32 LE 长度前缀）+ `68656c6c6f…` = "hello-from-wasm-0"。宿主日志（`host-47311.err`）：`input c-browser text="hello-from-wasm-0" admitted=True frameBytes=208`。回声：见 A1-2 的 `chat.event`；5 条往返（0 ms 链路）24.9 / 34.7 / 39.6 / 45.5 / 71.1 ms（含等下一个 50 ms tick），编包耗时首条 7.2 ms、之后 3.3–13.3 ms。

**缺口（记录，不绕）**：Runtime HEAD 没有公开的「InputCommand 信封编码」API（只有 `Validate` / `TryParseInputCommand`；LumioServer Rust 侧有 `InputCommand::from_chat_text`）；探针里那 3 行信封拼装是本卡范围内的最小替代，归 R5-02 codec 正式化。

#### A1-4 裁剪与 AOT 下生成代码 / 嵌入资源 / 确定性

**裁剪器对 Runtime Ecs 报 4 处 IL2075（原文）：**

```
/_/modules/ecs/src/Lumio.GameRuntime.Ecs/World/World.cs(236,9): Trim analysis error IL2075: Lumio.GameRuntime.Ecs.World.TryReadAccountId(Component): 'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicFields' in call to 'System.Type.GetField(String)'. The return value of method 'System.Object.GetType()' does not have matching annotations. ...
/_/modules/ecs/src/Lumio.GameRuntime.Ecs/World/WorldManager.cs(673,9): Trim analysis error IL2075: Lumio.GameRuntime.Ecs.WorldManager.FindSyncField(Component, String): 'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicFields' in call to 'System.Type.GetFields()'. ...
/_/modules/ecs/src/Lumio.GameRuntime.Ecs/World/WorldManager.cs(683,17): Trim analysis error IL2075: Lumio.GameRuntime.Ecs.WorldManager.FindSyncField(Component, String): 'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.NonPublicProperties' in call to 'System.Type.GetProperty(String, BindingFlags)'. ...
/_/modules/ecs/src/Lumio.GameRuntime.Ecs/World/WorldManager.cs(693,9): Trim analysis error IL2075: Lumio.GameRuntime.Ecs.WorldManager.TryDispatchSendMessage(Component, String): 'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicMethods' in call to 'System.Type.GetMethod(String, Type[])'. ...
error NETSDK1144: Optimizing assemblies for size failed.
```

探针工程开着 `TreatWarningsAsErrors`，所以 **默认 publish 直接失败**；这 4 处在 Runtime 自己的桌面构建里不报（桌面不裁剪）。处置：publish 时传 `-p:ILLinkTreatWarningsAsErrors=false`（只放过 ILLink 警告，编译警告仍为错），不改 Runtime。

**裁剪后运行时验证（默认变体，`PublishTrimmed` 开、`TrimMode` 默认）：**
- 生成的注册表与组件 glue 没被裁掉：浏览器内 `GeneratedRegistry.Instance.Side=Client`、`AttributeDeclarations=5`、`CreateComponents(PlayerEntity).Length=2`；声明表嵌入资源 `Lumio.GameRuntime.Ecs.generated.attribute-declarations.json` 在浏览器内 `GetManifestResourceStream` 长度 **2103 字节**（与桌面一致）。
- 反射路径实际生效：重建基准里对 100 / 300 / 1000 个实体的 `IdentityComponent.name` 做 WorldChange 字段写（走 `FindSyncField` → `WriteField`），世界哈希与桌面**逐位一致**（见下），说明被反射查找的字段 / 属性 / 方法在裁剪后仍在（它们同时被生成代码直接引用）。**注意**：这只证明本卡覆盖的路径；IL2075 的含义是「裁剪器不能保证」，任何只经反射触达的成员都可能被裁——Runtime 侧应按 §5.3 修。
- **Deterministic 双轮哈希（wasm vs 桌面 net10.0，同一份 `RebuildBench` 代码、同一批快照字节、5 条输入）：**

| 快照 | 桌面 CoreCLR（Rosetta x64） | Chrome 152 wasm（解释） | Chrome 152 wasm（AOT） | 逐位一致 |
|---|---|---|---|---|
| world-100（101 实体） | `d619dccfe2eaac0b` | `d619dccfe2eaac0b` | `d619dccfe2eaac0b` | 是 |
| world-300（301 实体） | `bb5db92fc3792f69` | `bb5db92fc3792f69` | `bb5db92fc3792f69` | 是 |
| world-1000（1001 实体） | `34adf9a5335d2683` | `34adf9a5335d2683` | `34adf9a5335d2683` | 是 |

哈希覆盖：实例号、tick、revision、每个活实体的计数器 + 类型 wire 名 + 生成组件 `CaptureSync` 暴露的全部同步字段（FNV-1a 64，`bench/WorldHash.cs`）。卡面写的「整数路径」在样板里只有 tick / 计数器 / revision（样板没有整数业务字段），字符串字段（name）一并纳入。

#### A1-5 单线程 wasm 主线程下 Thread.CurrentThread / Interlocked / OwnerThreadGuard；挪进 Web Worker

浏览器内 `RuntimeProbe`（`results/startup-*.jsonl` 每个样本的 `probe`）：

```json
{"frameworkDescription":".NET 10.0.11","osDescription":"Browser","processArchitecture":"Wasm","isBrowser":true,"processorCount":1,"managedThreadId":1,"currentThreadManagedId":1,"threadCurrentThreadStable":true,"newThreadStart":"PlatformNotSupportedException: Arg_PlatformNotSupported","interlockedIncrement":1000,"registrySide":"Client","attributeDeclarations":5,"playerComponents":2,"embeddedDeclarationsBytes":2103,"stopwatchHighResolution":true,"stopwatchFrequency":1000000000}
```

- `WorldManager.Start(Thread.CurrentThread)` 后 `Tick()` / `DrainOutbox()` 的 `EnsureOwner`（比较 `Thread.CurrentThread` 引用并回退比较 `ManagedThreadId`）在主线程**全部通过**（Boot / 每帧 OnFrame 走 ApplyClientBatch 的世界都是同一个 Manager；`ownerThread=1`）。
- `Interlocked.Increment` 正常（1000 次 = 1000）；`OwnerThreadGuard`（`Volatile` + `Interlocked.CompareExchange` 绑 `Environment.CurrentManagedThreadId`）用的原语在单线程 wasm 都可用，token 恒为 1。
- `new Thread(...).Start()` → **`PlatformNotSupportedException`**：单线程运行时（默认）不能起托管线程；`Environment.ProcessorCount = 1`。任何 Runtime 代码若假设能开线程（如 Observability 的 `Channel<T>` 消费线程）在浏览器不可用——本卡客户端模块路径没触到。
- **挪进 Web Worker**（`app/wwwroot/worker.html`：整个 .NET 运行时在 module Worker 里 `dotnet.create()`，主线程只收 `postMessage`）：**成立。** 运行时与 WorldManager 整体在 module Worker 里跑，`Start(Thread.CurrentThread)` 绑的是 Worker 的主线程（`managedThreadId=1`，Worker 自己的运行时实例），Tick / DrainOutbox / 解包 / 重建全部通过，哈希与主线程 / 桌面一致：
- worker-default run 0: isWorker=True ownerThread=1 managedThreadId=1 newThreadStart="PlatformNotSupportedException: Arg_PlatformNotSupported" dotnet.create 5703.8 ms · WorldManager 可用 6930.8 ms · Delta 解包 ok=True · LocalWrite 190.6 ms · rebuild(100)×7 中位 25.50 ms 哈希 ['d619dccfe2eaac0b']
- worker-default run 1: isWorker=True ownerThread=1 managedThreadId=1 newThreadStart="PlatformNotSupportedException: Arg_PlatformNotSupported" dotnet.create 5900.5 ms · WorldManager 可用 7045.7 ms · Delta 解包 ok=True · LocalWrite 185.7999 ms · rebuild(100)×7 中位 34.90 ms 哈希 ['d619dccfe2eaac0b']
- worker-default run 2: isWorker=True ownerThread=1 managedThreadId=1 newThreadStart="PlatformNotSupportedException: Arg_PlatformNotSupported" dotnet.create 5390.6 ms · WorldManager 可用 6658.2 ms · Delta 解包 ok=True · LocalWrite 140.3 ms · rebuild(100)×7 中位 25.00 ms 哈希 ['d619dccfe2eaac0b']
- worker-aot run 0: isWorker=True ownerThread=1 managedThreadId=1 newThreadStart="PlatformNotSupportedException: Arg_PlatformNotSupported" dotnet.create 4808.1 ms · WorldManager 可用 5980.5 ms · Delta 解包 ok=True · LocalWrite 7 ms · rebuild(100)×7 中位 2.30 ms 哈希 ['d619dccfe2eaac0b']
- worker-aot run 1: isWorker=True ownerThread=1 managedThreadId=1 newThreadStart="PlatformNotSupportedException: Arg_PlatformNotSupported" dotnet.create 4415.8 ms · WorldManager 可用 5532.4 ms · Delta 解包 ok=True · LocalWrite 6.5 ms · rebuild(100)×7 中位 2.00 ms 哈希 ['d619dccfe2eaac0b']
- worker-aot run 2: isWorker=True ownerThread=1 managedThreadId=1 newThreadStart="PlatformNotSupportedException: Arg_PlatformNotSupported" dotnet.create 4380.9 ms · WorldManager 可用 5545.1 ms · Delta 解包 ok=True · LocalWrite 6.3 ms · rebuild(100)×7 中位 1.60 ms 哈希 ['d619dccfe2eaac0b']

#### A4 开发体验与 CI 可行性

- **构建耗时（Rosetta 下，单独标注）**：工作负载安装 `wasm-tools + wasm-experimental` **246 s**（12 个 pack，落 `~/.dotnet/packs/`）；探针 `dotnet build -c Debug` 首次（含 Runtime 9 个工程重编）10.4 s、增量 4.1 s（touch Program.cs）；干净构建（删 app/ bench 的 bin/obj）5.5 s（只删探针工程的 bin/obj，Runtime 各工程增量）；`dotnet publish -c Release`（裁剪开，含 Emscripten 重链接 `emcc -Oz`）首次 **65 s**、增量 6.4–6.7 s；裁剪关 15 s；AOT **56.5 s**（13 个程序集 AOT + `-O2` 位码编译 + IL stripping；13.5 s 花在 aot-instances；wwwroot 只改 JS 的增量 AOT publish 101 s）。
- **headless Chrome 跑 wasm 探针并断言输出**：可行——本卡全部桌面数字就是 `tools/measure.mjs`（Playwright 1.63 + Chrome 152 headless）跑出来的，页面把状态放在 `window.__spike`，脚本 `waitForFunction(() => window.__spike.ready)` 后 `evaluate` 取 JSON；`.NET` 的 `Console.WriteLine` 落浏览器 console 可被 `page.on('console')` 捕获。未改本仓 CI。
- **调试：异常栈**（Release publish，无 pdb）：C# 抛出的 `InvalidOperationException("Snapshot magic is not LWM1.")` 经 `[JSExport]` 到 JS 是 `Error`，`message` 原文保留，`stack` 前半是**可读的托管栈**（`WorldSnapshotCodec.Read → WorldManager.CreateFromSnapshot → RebuildBench.Run → SpikeExports.Rebuild → 生成的 __Wrapper_Rebuild_… stub`，方法名与参数类型齐全、**无行号**），后半是 `dotnet.runtime.*.js` 的压缩 JS 帧。Debug 构建产物里带 `.pdb.gz`（`_framework/*.pdb`），C# 源码级断点需 .NET 的浏览器调试代理（`dotnet run` 开发服务器提供），Release 静态托管下不可用——本卡未进一步验证。
#### A2-1 产物体积（三变体各一组；`node tools/sizes.mjs publish/<变体>/wwwroot`，`results/sizes-*.json`）

「全部文件」= `_framework/` 下不含 `.gz/.br/.pdb` 的全部；「实际下载」= 浏览器真正取的文件（`performance.getEntriesByType('resource')`）——三份 ICU 只按语言取一份（本机 en → `icudt_EFIGS`），所以实际下载比全部文件少两份 ICU。

| 变体 | 文件数 | 全部文件 未压缩 / gzip -9 / brotli -11 | 实际下载 未压缩 / gzip / brotli（推算：全部 − 两份未用 ICU） | 其中 dotnet.native.wasm 未压缩 / br | BCL 程序集 未压缩 / br | Runtime 3 程序集 未压缩 / br |
|---|---|---|---|---|---|---|
| 默认（裁剪开，AOT 关） | 19 | 7,360,120 / 2,632,352 / 2,104,042 | **5,296,536 / 1,986,247 / 1,632,952** | 2,880,883 / 925,138 | 1,289,107 / 416,325 | 80,703 / 30,602 |
| 裁剪关（AOT 关） | 190 | 21,814,058 / 8,359,026 / 6,614,127 | 19,750,474 / 7,712,921 / 6,143,037 | 3,002,094 / 976,866 | 15,169,056 / 4,720,822 | 561,085 / 187,441（9 个：含 Command / Config / Coordination / Gas / Observability / GeneratedContracts） |
| AOT 开（裁剪开） | 19 | 12,275,242 / 3,980,214 / 2,978,753 | **10,211,658 / 3,334,109 / 2,507,663** | **7,792,222 / 1,918,823** | 1,289,107 / 306,078 | 80,703 / 23,119 |

- 实际下载核对：默认变体 Chrome 报 `decodedBodySize` 合计 **5,296,536 B / 17 个资源**（与推算列逐字节相等；`transferSize` 5,301,636 B 是未压缩传输——探针静态服务不发预压缩文件，见 A2-2 说明）。
- 裁剪把 BCL 从 172 个程序集 15.2 MB 裁到 7 个 1.29 MB，Runtime 从 9 个程序集裁到 3 个（Command / Config / Coordination / Gas / Observability / GeneratedContracts 在客户端路径上没有活代码，整个被裁掉）。
- AOT 把 `dotnet.native.wasm` 从 2.88 MB 涨到 7.79 MB（brotli 后 0.93 → 1.92 MB），IL 程序集不变但 brotli 更小（IL stripping）。
- `InvariantGlobalization=true` 可去掉 ICU（本机实际下载的 `icudt_EFIGS` 550,832 / brotli 143,983 B）——未实测启动收益。

#### A2-2 冷启动：导航到「WorldManager 可用」（`results/startup-*.jsonl`，每组 5 次，`tools/measure.mjs startup`）

方法：Playwright 每次 **新建浏览器 context**（空缓存）访问 `--no-store` 静态服务 = 空缓存；同一 context 连续 5 次访问普通缓存服务 = 热缓存（首访不计）。时间基准 = 页面 `performance.now()`（导航起点），打点：`dotnet.create()` 返回 / `getAssemblyExports` 返回 / `runMain` 返回 / `Boot()`（WorldManager.Create + Start + 欢迎包 Tick）返回。静态服务在环回，**未发预压缩文件**（每次下载 5.3 MB 未压缩，环回带宽下 ≈ 1 s 内），所以下面的数字里「下载」偶然性很小、大头是运行时初始化；真实网络下还要加下载时间（体积见 A2-1）。

| 变体 | 缓存 | WorldManager 可用 中位 / 最差 / 最好 ms | dotnet.create 中位 / 最差 ms | exports 就位 中位 ms | Boot()（C# 内）中位 ms |
|---|---|---|---|---|---|
| 默认 | 空 | **5581.7 / 5776.5 / 5440.6** | 4369.2 / 4747.6 | 4750 | 27.9 |
| 默认 | 热 | **4374.1 / 4587.5 / 4246.9** | 3410.4 / 3570.7 | 3704 | 27.4 |
| 默认（第二轮复测） | 空 | **5315.5 / 5993.0 / 5080.8** | 4244.6 / 5017.6 | 4537.4 | 27.8 |
| 裁剪关 | 空 | 未执行 |
| 裁剪关 | 热 | 未执行 |
| AOT | 空 | **4911.2 / 8113.0 / 4579.3** | 3967.8 / 6626.1 | 4149.7 | 97.8 |
| AOT | 热 | **4568.5 / 4611.8 / 3663.2** | 3661.6 / 3693.5 | 3842.0 | 93.2 |
| **默认（headed Chrome 152，有窗口）** | 空 | **5234.5 / 5701.3 / 5115.4** | 4245.3 / 4716.6 | 4526.2 | 26.5 |
| **默认（headed）** | 热 | **4388.3 / 5659.8 / 4225.2** | 3401.7 / 4616.6 | 3691.9 | 27.0 |
| **AOT（headed）** | 空 | **5930.8 / 6157.1 / 5735.5** | 4927.4 / 5121.0 | 5117.1 | 105.7 |

对照：纯 JS 聊天页（`modules/web/chat/index.html`，3,184 B、2 个资源）同法 5 次 **就绪中位 436 ms**（DOMContentLoaded 392 ms）；Game 聊天页连上宿主到首帧 **153 ms**（中位，最差 220 ms）。

读法：热缓存 4.4 s 里 3.4 s 是 `dotnet.create()`（wasm 编译 + 运行时起来 + 加载 19 个程序集 + ICU），`getAssemblyExports` 再 0.3 s，`Boot()` 只要 28 ms。**启动瓶颈是运行时初始化，不是我们的程序集。**

**但「谁来起浏览器」影响很大，本卡没能完全解释：**

| 浏览器 | 起法 | 空缓存 → WorldManager 可用 | 热缓存 |
|---|---|---|---|
| Google Chrome 152.0.7977.82 | Playwright 1.63 `channel: chrome`，headless（new） | 5.2–5.6 s（中位，两轮） | 4.4 s |
| 同上 | Playwright，headed（有窗口） | 5.2 s | 4.4 s |
| 同上 | Playwright `launchPersistentContext`（持久 profile，磁盘缓存 + V8 代码缓存可用），同页连载 4 次 | 5.5 s（第 1 次） | 4.4 / 4.1 / 4.1 s（第 2–4 次） |
| Claude 桌面应用内嵌 Chromium 148.0.7778.280（`results/inapp-hidden-tab.json`） | 应用自己的面板，无自动化参数 | **1.05 s**（首次） | **0.13 s**（`location.reload()`，`dotnet.create` 100 ms） |
| iOS Simulator Safari（iOS 26.5，跑在 M5 上；§4.B） | `simctl openurl`，无自动化 | **0.62 s**（`dotnet.create` 249 ms） | 未测 |

同一份 `publish/default` 产物、同一台机器、同一条环回链路，Playwright 起的 Chrome 152 比另两个「用户口径」的浏览器慢 5–8 倍，热缓存差 30 倍——差异出在 Playwright 的启动方式（自动化参数 / 临时 profile）还是 Chrome 152 自身的 wasm 分层编译策略，本卡**没有裁决**：想用本机真实 Chrome 152（无 Playwright）做第三方裁决，但 Claude in Chrome 扩展未连接，未执行。**报告口径**：表格里的 headless 数字保留为「CI / Playwright 口径」，1.05 s / 0.62 s 作为「用户口径」旁证；给 Owner 的结论按区间写（0.6–5.6 s），不取单值。

#### A2-3 重建耗时：CreateFromSnapshot + 5 条近似输入一次 Tick（`results/rebuild*.jsonl`；`tools/measure.mjs rebuild`）

近似口径见 `bench/RebuildBench.cs` 注释；快照由 `tools/snapshot-gen` 生成（100 / 300 / 1000 个 PlayerEntity + 1 个 WorldEntity；每实体 2 组件 Identity + Chat——样板只有这两个组件，比卡面「3 组件」少一个）；`world-100.lwm1` 18,977 B、`world-300.lwm1` 56,777 B、`world-1000.lwm1` 189,077 B。

**第一轮（每档 7 次，页面刚加载）——预热效应明显：**

| 实体 | 7 次 totalMs 原始样本 | 中位 / 最差 / 最好 | 占 50 ms 帧预算（中位） |
|---|---|---|---|
| 100 | 32.4, 378.1, 3.8, 3.1, 98.5, 115.0, 21.3 | 32.4 / 378.1 / 3.1 | 65 % |
| 300 | 576.3, 451.7, 252.4, 174.5, 80.1, 16.3, 37.7 | 174.5 / 576.3 / 16.3 | 349 % |
| 1000 | 233.0, 122.3, 188.3, 241.8, 189.0, 41.6, 13.1 | 188.3 / 241.8 / 13.1 | 377 % |

第一轮里 GC gen0 从 0 涨到 4 次、wasm 线性内存从 32 MiB 长到 46 MiB（`gcBefore/gcAfter`、`wasmMemoryBytes`）；样本忽高忽低对应解释器分层编译（JITerpreter 在主线程同步编译热方法）与堆增长。

**第二轮（每档 30 次，页面各只加载一次、三档顺序 100 → 300 → 1000；默认 / AOT 两变体，裁剪关变体起不来见 A2-1）：**

| 变体 | 实体 | 30 次 中位 / P90 / 最差 / 最好 ms | Create 中位 ms | Apply 中位 ms | 占 50 ms 帧预算 中位（P90） | 哈希 | GC gen0 增量 / wasm 内存 |
|---|---|---|---|---|---|---|---|
| 默认（解释 + JITerpreter） | 100 | **49.70** / 350.30 / 1092.90 / 1.10 | 37.90 | 0.70 | 99.4 %（700.6 %） | d619dccfe2eaac0b | +1 / 33,554,432 B |
|  |  | 原始 30 样本：26.8, 442.9, 4.7, 2.2, 93.4, 167.5, 20.3, 103.2, 137.6, 224.9, 51.7, 350.3, 92.0, 6.6, 223.4, 31.4, 26.0, 29.1, 53.1, 67.7, 1092.9, 1.1, 88.9, 1.2, 1.4, 1.1, 38.2, 49.7, 10.7, 46.7 |  |  |  |  |  |
| 默认（解释 + JITerpreter） | 300 | **4.50** / 107.70 / 133.40 / 2.20 | 4.20 | 0.30 | 9.0 %（215.4 %） | bb5db92fc3792f69 | +4 / 33,554,432 B |
|  |  | 原始 30 样本：7.5, 133.4, 6.2, 51.7, 23.3, 6.6, 3.2, 114.6, 15.9, 107.7, 82.1, 39.8, 3.9, 11.2, 4.5, 8.9, 16.3, 2.4, 2.9, 2.3, 4.1, 2.2, 2.3, 3.7, 2.5, 2.5, 3.7, 3.0, 2.4, 2.2 |  |  |  |  |  |
| 默认（解释 + JITerpreter） | 1000 | **8.60** / 69.00 / 347.00 / 6.90 | 7.40 | 0.80 | 17.2 %（138.0 %） | 34adf9a5335d2683 | +12 / 69,730,304 B |
|  |  | 原始 30 样本：8.1, 46.4, 7.4, 15.7, 7.1, 7.3, 9.4, 8.2, 127.4, 9.6, 347.0, 7.7, 7.4, 11.7, 8.9, 14.7, 7.5, 6.9, 6.9, 8.6, 12.1, 7.6, 9.2, 8.6, 7.4, 12.3, 8.1, 7.5, 69.0, 7.0 |  |  |  |  |  |
| AOT | 100 | **0.80** / 42.00 / 153.80 / 0.20 | 0.50 | 0.10 | 1.6 %（84.0 %） | d619dccfe2eaac0b | +1 / 33,554,432 B |
| AOT | 300 | **0.50** / 9.30 / 10.70 / 0.30 | 0.40 | 0.10 | 1.0 %（18.6 %） | bb5db92fc3792f69 | +4 / 40,304,640 B |
| AOT | 1000 | **1.70** / 21.30 / 34.60 / 1.00 | 1.30 | 0.30 | 3.4 %（42.6 %） | 34adf9a5335d2683 | +12 / 69,730,304 B |

读法：解释模式 100 实体那一档中位 49.7 ms 是**预热**（它排在页面里第一个跑，30 次里前十几次在分层编译）；到 300 / 1000 实体时代码已热，中位 4.5 / 8.6 ms，但每档仍有 100–350 ms 级毛刺（GC gen0 +1 / +4 / +12，wasm 线性内存 32 → 66 MiB）。**AOT 把中位压到 0.5–1.7 ms、最差压到 11–154 ms**（首次仍有），1000 实体 = 帧预算 3.4 %。与桌面 CoreCLR（Rosetta）的 0.24 / 0.57 / 2.38 ms 同一量级。

桌面对照（同一份代码，CoreCLR Rosetta x64，每档 7 次）：100 实体中位 0.243 ms、300 实体 0.569 ms、1000 实体 2.380 ms（最差分别 39.1 / 0.99 / 21.4 ms，首次含 JIT）。

#### A2-4 20 包 / 秒下每包解包占帧预算；连续 5 分钟（`results/wire-soak300.jsonl`）

宿主 20 Hz tick，每 tick 1 条 Bot 聊天 → 每秒 20 帧 Delta（各含 1 条 chat.event，≈ 310 B），浏览器逐帧 `ChatTypedMapping.ApplyDownstream`：

| 指标 | 值 |
|---|---|
| 5 分钟收到 / 解包成功 / 含事件 | 6074 / 6074 / 6073 帧（20.2 帧 / s） |
| 每帧解包耗时 中位 / P95 / 最大 | **0.7 / 1.3 / 668.9 ms**（最大值那一帧与到达间隔最大值 670 ms 同一处，页面级停顿一次） |
| 占 50 ms 帧预算（中位 / P95） | **1.4 % / 2.6 %** |
| 帧到达间隔 中位 / P95 / 最大 | 50.0 / 51.9 / 670.2 ms |
| 5 分钟收字节 | 1,923,618 B |
| wasm 线性内存（`WebAssembly.Memory.buffer.byteLength`）t=30s → t=300s | 33,554,432 → 33,554,432 B（不增长） |
| `GC.GetTotalMemory(false)` t=30s → t=300s | 4,308,344 → 4,316,336 B（+8 KB） |
| GC 次数（gen0 / 1 / 2） | 0 / 0 / 0（`GC.CollectionCount` 在这 5 分钟内未增） |
| `GC.GetGCMemoryInfo().HeapSizeBytes` | 0（Mono wasm 不填此字段，不可用作仪表） |

注：这一项是「解包占预算」不是「重建占预算」——现行 wire 的 Delta 只有 chat.event，没有 ECS 字段变化可重建；预测世界重建占预算见 A2-3。

#### A2-5 帧驱动：20 Hz 固定步长的定时器抖动；标签页后台（`results/timers*.jsonl`）

| 驱动方式（前台，200 样本，目标 50 ms） | 间隔 中位 / 最小 / 最大 ms | 抖动（|间隔 − 50|） 中位 / P95 / 最大 ms |
|---|---|---|
| `setTimeout(50)` 链 | 51.2 / 50.2 / 61.9 | 1.2 / 1.5 / 11.9 |
| `requestAnimationFrame` 每 3 帧（60 Hz 显示） | 50.0 / 49.9 / 50.1 | 0.0 / 0.1 / 0.1（被显示刷新量化，不是独立时钟） |
| Worker 内 `setInterval(50)` + postMessage | 49.7 / 0.0 / 379.1 | 1.4 / 40.1 / 329.1（首样本含 Worker 启动；后续偶发 40 ms 级抖动） |

后台标签页：Playwright 驱动的 Chrome 152（headed，同 context 新标签 + bringToFront）两次都没让页面进入 `document.hidden=true`（读数 hidden=false，setTimeout 间隔中位 51.3 ms、WS 1086 帧 / 45 s），不能当后台数据；改用 Claude 桌面应用内嵌 Chromium 148.0.7778.280 的隐藏面板（`document.hidden=true`，`results/inapp-hidden-tab.json`）：**`setTimeout(50)` 链被对齐到每 1000 ms 一次**（前 6 个间隔 1000, 1000, 999, 1001, 999 ms），页面隐藏约 5 分钟后进入密集节流，**间隔跳到 12001 ms**；12 个 50 ms 链式定时器 45 s 内跑不完。同一时段 **WebSocket 帧照常到达并被 C# 逐帧解包**：3213 帧 / 161 s = 20.0 帧/s，帧间最大 62.3 ms，解包中位 0.3 ms。现象读法：积压不在 socket 层（消息事件不节流），而在「靠定时器推进的本地 tick」——若预测世界重建 / 20 Hz 本地步进挂在 setTimeout 上，后台期间不推进，回前台要一次性追帧；输入序号怎么续归协议（本卡只记现象）。

#### A3-1 [JSExport] 调用开销、每帧差集数据量、long 过互操作（`results/interop-*.jsonl`，每项 10,000 次 × 5 轮）

| 调用 | 每次调用 µs（5 轮中位） | 最差轮 µs |
|---|---|---|
| **默认（解释）** | | |
| Ping(int) | 0.58 | 52.81 |
| EchoDouble(double) | 1.77 | 18.61 |
| EchoNumber(long as Number) | 0.42 | 15.69 |
| EchoBigInt(long as BigInt) | 2.36 | 20.45 |
| EchoString(100 chars) | 2.94 | 140.25 |
| DiffJson(100) | 70.37 | 386.75 |
| DiffPacked(100) int[] | 3.89 | 111.50 |
| DiffJson(100)+JSON.parse | 66.38 | 100.49 |
| long > 2^53：BigInt 路径精确 = True；Number 路径 = throws: Assert failed: Value is not an integer: 9007199254740992 (number) | | |
| **AOT** | | |
| Ping(int) | 0.31 | 29.96 |
| EchoDouble(double) | 0.80 | 1.34 |
| EchoNumber(long as Number) | 0.49 | 3.22 |
| EchoBigInt(long as BigInt) | 0.97 | 7.42 |
| EchoString(100 chars) | 2.46 | 11.80 |
| DiffJson(100) | 26.98 | 248.07 |
| DiffPacked(100) int[] | 3.55 | 72.09 |
| DiffJson(100)+JSON.parse | 33.89 | 62.37 |
| long > 2^53：BigInt 路径精确 = True；Number 路径 = throws: Assert failed: Value is not an integer: 9007199254740992 (number) | | |

`IPresentationDiff` 形状样例（Started / Continued / Ended，键 = 实体类型 + fx_key + 稳定参数）100 实体一帧：JSON 形态 3901 字节、打包 `int[]`（kind, keyHash, x, y）400 个 int = 1,600 字节；Canvas 画 100 个实体每帧 中位 0.2 ms（打包）/ 0.4 ms（JSON + `JSON.parse`），P95 3.1 / 5.6 ms，最大 171 / 413 ms（首帧预热）。

#### A3-2 是否需要 SharedArrayBuffer / 多线程

**不需要。** 本卡全部路径在单线程运行时完成（`WasmEnableThreads` 关，默认）；官方事实 4：多线程是实验特性、要站点发 `Cross-Origin-Opener-Policy: same-origin` + `Cross-Origin-Embedder-Policy: require-corp`，且 **JS 互操作只在主线程**——开了线程也不能把 WorldManager 挪出主线程再从 JS 调它。对照 decisions/0003 的落地站点：静态托管加两个响应头技术上可配，但 COEP `require-corp` 会要求页面引用的所有跨源资源（含账号站、CDN）都带 CORP / CORS 头，这是站点级约束，本卡建议**不开**。

#### A3-3 输入到画面的延迟（`results/draw*.jsonl`、`results/wire-delay150b.jsonl`）

**本地预测路径**（按键事件 → C# `LocalWrite`（owner 字段本地写 + 自动上行入 outbox）→ Canvas 画 → 下一次 `requestAnimationFrame` 回调；5 轮 × 20 次）：各轮中位 **3.4 / 1.2 / 3.2 / 1.0 / 1.1 ms**，各轮最差 34.9 / 36.2 / 40.7 / 18.4 / 4.7 ms（最差落在等下一帧刷新）；`LocalWrite` 本身 0.1–0.3 ms（首次 4.8 ms）。**这条路不等服务器，与链路延迟无关。**

**权威回声路径**（按键 → C# 编 InputCommand → WS → 宿主等下一 tick → Runtime 编 Delta → WS → C# 解包）：

| 链路 | 往返 中位 / 最差 / 最好 ms（5 条） | 说明 |
|---|---|---|
| 0 ms（环回） | 39.6 / 71.1 / 24.9 | 含 0–50 ms 等 tick |
| 单向 150 ms（宿主 `--delay-ms 150`，上下行各 150） | **333.3 / 400.8 / 318.0**（AOT 变体同链路：333.2 / 345.1 / 308.5） | 期望 ≈ 300 + 0–50 + 编解码 |

路线 B 的按键到画面 = 上表往返 − C# 编解码（编 1–13 ms、解 0.4–0.7 ms，首次更高）+ JS 画（≈ 1 ms）：0 ms 链路约 **35–70 ms**，150 ms 单向链路约 **313.0–400.8 ms（中位 ≈ 330.3 ms）**——按键必等一个来回；路线 A 的本地预测路径 1–3 ms。
### 4.B 轨道 B：手机浏览器

**全部「未执行」——本机没有任何真机。** 卡面 §6 要求最少两台真机（一台 iPhone iOS Safari 最近两代、一台中端 Android Chrome），可选微信内置浏览器；以下为需要的设备清单与每项的执行方法，交主 loop 安排。

| 项 | 状态 | 需要的设备 / 条件 | 执行方法（已就绪） |
|---|---|---|---|
| B1-1 加载并连宿主解出真实包 | **未执行** | iPhone（iOS 18 / 26 两代 Safari）、中端 Android（Chrome 稍新版）；同一局域网 | 手机浏览器打开 `http://<Mac 局域网 IP>:47380/?ws=ws://<Mac IP>:47311/&connectionId=c-phone`，页面状态行给出 dotnet.create / WorldManager 时间；Safari 用 Mac 的 Web Inspector 读 `window.__spike`（`frames` / `roundtrips`） |
| B1-2 WASM 特性面（SIMD / 线程 / EH / 最大内存） | **未执行** | 同上 | 特性探测页未写（卡面要求「逐步增大到失败的那个数」——需要一个只做 `WebAssembly.Memory({initial, maximum})` 逐步增大的独立探测页，非 .NET） |
| B1-3 wss 连接 | **未执行** | 落地站点或局域网自签证书 | 见 decisions/0003；本卡全程 `ws://127.0.0.1` |
| B2-1 下载时间（Wi-Fi / 蜂窝） | **未执行** | 真机 + 蜂窝网 | 静态服务要开 brotli 协商（本卡的 `static-server.mjs` **不**发预压缩文件，只用于环回；真机测下载时间要换能协商 `.br` 的服务） |
| B2-2 冷启动到 WorldManager 可用 | **未执行** | 真机 | 同 B1-1，`window.__spike.timings.bootDone` |
| B2-3 重建耗时 100 / 300 / 1000 | **未执行** | 真机 | `await __spike.rebuild('fixtures/world-100.lwm1', 5, 30)` |
| B2-4 5 分钟连续跑（发热 / 内存 / 页面被杀） | **未执行** | 真机 | `node tools/measure.mjs wire … --seconds 300`（Playwright 连真机需 WebDriver / 远程调试；或手动跑页面后读 `__spike.frameStats()`） |
| B2-5 电量 | **未执行** | 真机 | 系统电量读数前后差 |
| B3-1 切后台 / 锁屏 / 来电 / 切 App | **未执行** | 真机 | 观察 `__spike.wire.closed`、`frames` 间隔、回前台后帧到达数 |
| B3-2 触摸 → 预测世界 → 画面 | **未执行** | 真机 | `await __spike.inputToPaint(20)` |
| B3-3 视口 / 安全区 / 横竖屏 | **未执行** | 真机 | `await __spike.drawFrames(100, 120, 'packed')` 横竖各一次 |
| B4 纯 JS 聊天页对照 | **未执行**（桌面对照见 4.A 路线 B 基线） | 真机 | 同一手机打开 `modules/web/chat/index.html` 与 Game 聊天页 |

**桌面设备模拟未做**（卡面明说不算真机，只作补充且需单独标注；本卡没有用 DevTools 节流 / 移动仿真冒充）。

**补充数据（不算真机，单独标注）：iOS Simulator** —— **iOS Simulator（iPhone 17 模拟器，iOS 26.5 runtime 23F77，Safari；跑在本机 Apple M5 上，不是真机、不代表手机 CPU / 内存 / 网络）**：`xcrun simctl openurl` 打开 `http://127.0.0.1:47380/?ws=ws://127.0.0.1:47311/&connectionId=c-iossim`（默认变体，裁剪开 AOT 关），截图 `spikes/runtime-wasm/results/iossim-iphone17-ios26.5-screenshot.png`（`xcrun simctl io screenshot`；Claude Code 的模拟器面板因本机 `xcode-select` 未指向 Xcode、需 sudo 而不可用，故用 simctl 截图替代）。页面状态行：**`dotnet.create 249.0 ms · WorldManager ready @ 623.0 ms`，`wire: connected ws://127.0.0.1:47311/ as c-iossim`**——Safari/JavaScriptCore 在 M5 上加载 5.3 MB 未压缩产物并起好世界只用 0.62 s，且连上了宿主（宿主日志可见 `attach c-iossim admitted`）。这说明 .NET 10 wasm 在 WebKit 引擎上能起、能连；**手机真机的 CPU、内存上限、蜂窝网下载全部另当别论，未执行。**
## 5. 三条路线成本对照、推荐与 ADR 草案建议

### 5.1 对照表（桌面 Chrome 152 实测；手机列全部「未执行」）

| 列 | A：.NET WASM 跑 Runtime 客户端模块 | B：浏览器只画不预测（预测只在 C# 客户端 Bot.Host / 桌面壳） | C：生成第二份 JS / TS 预测实现（只作对照，不做） |
|---|---|---|---|
| 一处维护？ | **是**——Ecs / Replication / 样板客户端程序集零改动进浏览器；codec、ECS、世界推进都是同一份 C# | **是**（浏览器侧没有规则代码）；但浏览器要有一份 C-1 codec 才能上行 / 解 Delta——今天 JS 侧没有，要么写第二份 codec（违反一处维护），要么把 codec 单独编成 wasm（那就是路线 A 的子集） | **否**——要复制 `Lumio.GameRuntime.Ecs`（WorldManager 客户端路径、WireCodec、Sync 字段模型、生成组件 glue）、`Replication.Chat`（ChatEnvelope / ChatPayload / ChatTypedMapping）、样板 `Username.Client` 生成物（Registry.g / 组件 .g）、未来的 GAS 准入五步与预测重建（RT-3 / RT-4 / RT-5）；每次 Runtime 改动都要同步两份，哈希对账要跨语言 |
| 桌面可行 | **可行**（实测：编过、起世界、解真实包、上行被接受、哈希逐位一致） | 可行（现有 Game 聊天页已连上宿主收帧；上行无实现） | 未做 |
| 手机可行 | **未执行**（无真机） | **未执行** | 未做 |
| 包体积 | 见 §4 A2-1：默认（裁剪开）未压缩 7.36 MB / gzip 2.63 MB / brotli 2.10 MB；实际下载按浏览器语言只取一份 ICU；`InvariantGlobalization` 可再去 ~0.6 MB（未实测） | 纯 JS 聊天页 3.2 KB（2 个资源） | 未做（估算无依据，不给数） |
| 冷启动到「可用」 | 空缓存中位 5.6 s，热缓存 4.4 s（§4 A2-2） | 页面就绪 0.44–0.46 s，连上宿主到首帧 0.15 s | 未做 |
| 每包重建占 50 ms 预算 | §4 A2-3（解释模式中位 100 实体 ≈ 65 %，300 / 1000 实体 > 300 %；AOT 见同节） | 不适用（浏览器不重建） | 未做 |
| 150 ms 单向延迟下按键到画面 | 本地预测路径 1–3 ms（不等服务器，§4 A3-3）；权威回声路径见 §4 A3-3 | 只能走权威路径：往返 ≥ 300 ms + 一个 tick（0–50 ms）+ JS 解包与绘制；§4 A3-3 用同一 wire 实测推导 | 未做 |
| 需要的站点配置 | 静态托管 + 正确 MIME（`application/wasm`）+ 预压缩协商（brotli / gzip）；**不需要** COOP / COEP（单线程）；wss 与落地站点同 decisions/0003；若开 `WasmEnableThreads` 才需 COOP / COEP | 静态托管 + wss | 同 B |
| 主要风险 | 启动 3–6 s；解释模式重建超预算、抖动大，AOT 拉长构建；Runtime Ecs 4 处 IL2075 反射需修；手机内存 / 体积未知；现行 Rust 宿主 Windows-only 且与 Runtime HEAD 漂移 | 按键要等一个来回才动（体验差，数字见上）；浏览器仍要一份 codec | 两处维护、两处出错；跨语言哈希对账 |

### 5.2 推荐（一句话 + 理由）

**推荐：桌面浏览器按路线 A（.NET WASM 跑 Runtime 客户端模块）立项验证，附三个先决预算（启动时间上限——先把 0.6 s 与 5 s 的分歧在真实 Chrome 上裁决、AOT 构建时间与体积上限、Runtime 反射裁剪修复）；手机不下结论，等真机数据。**
理由：路线 A 是三条里唯一同时满足「一处维护」与「按键立刻动」的；它的代价（启动秒级、重建要 AOT）都是可量化、可工程化的预算问题，而路线 B 的体验差是结构性的（按键必等往返），路线 C 违反第一性原理。

### 5.3 ADR 草案建议（不落 ADR 文件，归架构仓）

- **标题**：`ADR-0xx：浏览器客户端预测走 .NET WebAssembly 装载 Runtime 客户端模块（桌面浏览器）；手机浏览器待真机数据`
- **决策条目（建议）**：
  1. 浏览器客户端的规则代码 = Runtime 客户端程序集经 .NET browser-wasm 装载，不得另写 JS 版 ECS / codec / 预测；JS 只做 WebSocket 搬字节、Canvas / DOM 表现、输入采集。
  2. 预测世界重建（RT-3）在浏览器与 C# 客户端是同一份代码；双端对账哈希（ADR-064 第 10 条）在浏览器同样成立，验收用本卡的「桌面 vs wasm 逐位一致」作回归。
  3. 交付形态：publish 必须开裁剪与 AOT（AOT 在 CI 的构建时间与体积预算另定）；`InvariantGlobalization` 默认开（Runtime 不依赖 ICU 语义——待 Runtime 确认）；单线程运行时，不开 `WasmEnableThreads`（JS 互操作只在主线程；开了也不能把 WorldManager 挪出主线程），所以落地站点不需要 COOP / COEP。
  4. 站点：静态托管 + `application/wasm` MIME + brotli 预压缩协商；wss 与落地站点沿用 decisions/0003。
  5. Runtime 义务：修掉 Ecs 里 4 处 IL2075 反射（`World.TryReadAccountId`、`WorldManager.FindSyncField` ×2、`WorldManager.TryDispatchSendMessage`），或给生成器加 `DynamicallyAccessedMembers` 标注，使 `PublishTrimmed` 零警告；公开 InputCommand 信封编码 API（今天只有 Validate / TryParseInputCommand）。
  6. 宿主义务：现行 Rust 宿主补 Unix 装载路径（或明确只在 Windows CI 验收），并与 Runtime HEAD 的 `ChatCommandRuntime` API 对齐。
- **替代方案**：B（只画不预测：按键等往返，结构性体验差；且浏览器仍需一份 codec）；C（第二份 JS / TS 实现：违反 ADR-056 第一性原理与 ADR-058 一处维护）；Blazor（同样是 .NET WASM，多一层 UI 框架，本卡未评估，不推荐进入）。
- **失败语义 / 回图触发**：① 桌面 Chrome 空缓存到「WorldManager 可用」超过预算（建议 ≤ 3 s，AOT + 去 ICU 后复测）→ 回图；② AOT 后 100 实体重建中位仍 > 50 ms 帧预算的 20 % → 先按 gas.md kill criterion 2 收窄克隆域，再回图；③ 真机 iOS Safari 装不下（内存拒绝 / 下载超 30 s）→ 手机走路线 B 或不承诺（ADR 0013 已不承诺触屏浏览器）；④ Runtime 出现第二份浏览器专用 codec → 审查退回。

## 6. Known gaps（没测到的，逐条）

1. **轨道 B 真机全部未执行**：iPhone（iOS Safari，最近两代）、中端 Android（Chrome）、微信内置浏览器（iOS / Android）——B1–B4 无一项有真机数据；本机 iOS Simulator 的补充数据（§4 B 末尾，若有）不算真机。
2. **现行 Rust 宿主未连上**（macOS 编不过 + 与 Runtime HEAD API 漂移）；所有 wire 数字来自 C# 替身宿主（同一份 Runtime codec 产帧），准入验签（Account Server ed25519）路径未过。
3. **wss / 落地站点 / 证书未测**：全部在 `ws://127.0.0.1` 环回；decisions/0003 的 WSS 约束未在浏览器端复现。
4. **Safari 桌面、Firefox 未测**（卡面只要 Chrome 桌面）；Playwright 自带 Chromium 未用，主数字是本机 Google Chrome 152 headless（new headless），启动一项另有 headed 复测（差异很大，见 §4 A2-2）；其余项（重建 / 互操作 / wire）未做 headed 复测。
5. **多线程 wasm（`WasmEnableThreads` + COOP / COEP）未开**：只测了单线程主线程与 Web Worker 两种线程归属。
6. **断网重连、弱网丢包**未测；后台标签页节流只在 Claude 桌面应用内嵌 Chromium 148 的隐藏面板里量到（1 s 对齐 → 约 5 分钟后 12 s），Playwright 控制的 Chrome 152 没能进入隐藏态；「回前台后输入序号怎么续」只记现象。
7. **`InvariantGlobalization` 去 ICU 的体积 / 启动收益未实测**（只有官方事实与 ICU 文件体积）。
8. **R5-01 后的 WorldChange / `sequence` / `appliedInputSequence` 形态未测**（尚未合入）；本卡的「预测世界」是 `CreateFromSnapshot` + WorldChange 包批量应用的近似，正式克隆 + 重放归 RT-3。
9. **样板实体只有 2 个组件**（Identity + Chat；卡面写「每实体 3 组件」）——用的是 Runtime 现有样板，没有为拿数字改基准；组件数比卡面少一个，重建耗时会偏乐观。
10. **路线 B 的 JS 解包耗时未测**：现有 JS 页没有上行路径且卡面禁止在 JS 写 codec，路线 B 的按键到画面是同一 wire 上 wasm 页往返减去 C# 编解码的推导值。
11. **电量、发热降频**：桌面不适用，手机未执行。
12. **调试体验**只验了源码映射 / 异常栈的最小样本（§4 A4-3）。
13. **启动时间的 5–8 倍分歧未裁决**（Playwright 起的 Chrome 152 vs 应用内嵌 Chromium 148 / 模拟器 Safari）：本机真实 Chrome 152 无自动化的对照未执行（Claude in Chrome 扩展未连接）；建议后续用户手动打开页面读状态行即可补上。
## 7. 收口证据（本仓）

### 7.1 `node eng/verify-sdk-pin.mjs`

```
不被 restore / lock 覆盖的版本号出现点:
  - global.json [sdk.version] = 10.0.400
  - eng/verify-toolchain.sh [SDK pin 副本] = 10.0.400
  - eng/verify-toolchain.ps1 [SDK pin 副本] = 10.0.400
  - tests/Lumio.Client.ArchitectureTests/Toolchain/ToolchainPolicyTests.cs [SDK pin 副本] = 10.0.400
  - .github/workflows/dotnet-test.yml [dotnet-version] = (未固定)
  - .github/workflows/repository-policy.yml [dotnet-version] = (未固定)

verify-sdk-pin: OK
```

### 7.2 `dotnet test tests/Lumio.Client.ArchitectureTests`

```
# 主工作树（含本仓既有的 3 个 .claude/worktrees/* 残留，2026-08-29 建，非本卡产物）
$ dotnet test tests/Lumio.Client.ArchitectureTests
  Failed Lumio.Client.ArchitectureTests.Graph.ProjectGraphTests.AllElevenModuleAssembliesExist [213 ms]
  Error Message:  Lumio.Client.Session production csproj
Failed!  - Failed:     1, Passed:    37, Skipped:     0, Total:    38, Duration: 469 ms - Lumio.Client.ArchitectureTests.dll (net10.0)

# 失败原因：该测试按程序集名在整个仓目录树里找 csproj 并断言恰好 1 个；本机有 3 个游离的 git worktree 各带一份副本
$ find . -name "Lumio.Client.Session.csproj" -not -path "*/bin/*" -not -path "*/obj/*"
./modules/session/src/Lumio.Client.Session.csproj
./.claude/worktrees/wonderful-stonebraker-f29af4/modules/session/src/Lumio.Client.Session.csproj
./.claude/worktrees/elated-neumann-1ca4c2/modules/session/src/Lumio.Client.Session.csproj
./.claude/worktrees/charming-dijkstra-fb6d5a/modules/session/src/Lumio.Client.Session.csproj
$ git worktree list
/Users/cui/LumioGames/LumioClient                                                 18020a1 [main]
/Users/cui/LumioGames/LumioClient/.claude/worktrees/charming-dijkstra-fb6d5a      45d804b (detached HEAD)
/Users/cui/LumioGames/LumioClient/.claude/worktrees/elated-neumann-1ca4c2         219e1a4 (detached HEAD)
/Users/cui/LumioGames/LumioClient/.claude/worktrees/wonderful-stonebraker-f29af4  219e1a4 (detached HEAD)
# spikes/ 下没有任何 Lumio.Client.*.csproj；本卡不删别人的 worktree。

# 干净环境证明：HEAD 18020a1 的独立 worktree + 拷入 spikes/（不含 bin/obj/publish/node_modules）→ 全绿
$ git worktree add --detach <tmp>/clean-check HEAD && rsync -a --exclude bin --exclude obj --exclude publish --exclude node_modules spikes <tmp>/clean-check/
$ cd <tmp>/clean-check && LumioRuntimeRoot=../LumioGameRuntime dotnet test tests/Lumio.Client.ArchitectureTests
Passed!  - Failed:     0, Passed:    38, Skipped:     0, Total:    38, Duration: 262 ms - Lumio.Client.ArchitectureTests.dll (net10.0)
$ git worktree remove --force <tmp>/clean-check   # 用后即删，git worktree list 不再有它
```

### 7.3 `git status --short`（只应有报告 + 探针目录）

```
?? spikes/
```

### 7.4 姊妹仓零改动

```
LumioGameRuntime 010ae46 0 changed
LumioServer 4c7688b 0 changed
LumioGame 5bc5afc 0 changed
LumioGameEngine engine/+eng/: 0 changed
```

### 7.5 `.spec` 结构校验（本仓收口门槛）

```
✔ 任务卡缺 frontmatter 被抓 (117.334875ms)
✔ 任务卡多余 frontmatter 字段被抓 (118.430416ms)
✔ 合法任务卡通过,子目录与 README 不校验 (120.153875ms)
✔ 软链接缺失被抓 (116.412542ms)
ℹ tests 13
ℹ suites 0
ℹ pass 13
ℹ fail 0
ℹ cancelled 0
ℹ skipped 0
ℹ todo 0
ℹ duration_ms 1719.750125
```

## 附：本卡产出与证据落点

- 报告：`docs/spikes/2026-09-05-spike-runtime-wasm.md`（本文）。
- 探针工程：`spikes/runtime-wasm/`（`README.md` 复现命令；`bench/` `app/` `host/` `tools/`）。
- 原始输出：`spikes/runtime-wasm/results/*.jsonl`（Playwright `RESULT` 行）、`results/sizes-*.json`（体积）。
- 不入库的生成物：`spikes/runtime-wasm/app/wwwroot/fixtures/*.lwm1`（`tools/snapshot-gen` 可再生，SHA-256 见 §4 A2-3）、`publish/`、`bin/` `obj/`、`tools/node_modules/`。
- 知识沉淀建议（由主 loop 定）：本仓 `.spec/knowledge/README.md` 是否新增一行指向本报告；架构仓 ADR 草案见 §5.3；Runtime 侧两条缺口（IL2075 ×4、InputCommand 信封编码 API）建议由主 loop 经 Workflow 向 LumioGameRuntime 提需求；LumioServer 侧「Rust 宿主 Windows-only + 与 Runtime HEAD API 漂移」同上。

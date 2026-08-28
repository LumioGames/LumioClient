# SPIKE-HYBRIDCLR-63 验证记录 — 官方版本 / 许可 / Unity 6.3 兼容 / AOT metadata 与发行路径

- 卡：R-00256 `SPIKE-HYBRIDCLR-63` / `01a045d8-9751-7406-b3cb-c5749385319b`
- 日期：2026-08-28
- 结论：**明确阻塞**
- 基线：LGE-V1.2-2026-08-27（镜像）/ 室基线 LGE-V1.4-2026-08-27
- 仓库 HEAD：证据采集于 `c571ff12fb86b201a5a4dea52f3aa5383530ec07`；
  收口复核于 **`6b7e8349afc634d076207efef2f8c9c168ad5747`**（本卡执行期间 main 前进了 2 个提交，见下）

> **HEAD 变动声明**：本卡执行中 main 从 `c571ff1` 前进到 `6b7e834`，仅两个文件变更：
> ```
> $ git diff --name-only c571ff1 HEAD
> .github/workflows/repository-policy.yml
> .spec/decisions/0002-cross-module-gates-and-state-ownership.md
> ```
> 且**与本卡结论相关的文件一个都没动**：
> ```
> $ git diff --stat c571ff1 HEAD -- eng/ Directory.Build.targets Directory.Build.props \
>     modules/hybridclr-adapter packages/com.lumio.client tests/Lumio.Client.ArchitectureTests global.json
> （无输出 —— 零变更）
> ```
> 因此 §4.2–§4.7 的全部实测结论在新 HEAD 上继续成立，未重跑。
> **仅 §4.8 的门禁状态发生变化，已按新 HEAD 更新（`spec-lint` 由红转绿）。**

> **文档事实性声明**：本文所有「官方事实」均来自 2026-08-28 抓取的官方页面与 Git 远端 ref，附 URL 与原文片段。
> 所有「实测」均附完整命令与真实输出。**未执行的项一律标注「未执行」**，不以计划冒充结论。
> 本卡不产出任何时延数字（见 §2 宿主架构声明）。

---

## 1. 卡面验收逐条对照

| 验收项（原文） | 结论 | 证据位置 |
|---|---|---|
| ① 记录官方版本 | **达成** | §3 事实 1–3；§4 实测 4.3、4.4 |
| ① 记录许可结论 | **达成** | §3 事实 4–6；§4 实测 4.2 |
| ① 记录 Unity 6.3 兼容 | **达成（结论：官方支持 6000.3.x）** | §3 事实 7–10；§4 实测 4.3 |
| ① 记录 AOT metadata | **达成** | §3 事实 11–15；§4 实测 4.5 |
| ① 记录发行路径 | **达成（且发行路径本身构成阻塞源）** | §3 事实 16–19 |
| ② 结论锁定或明确阻塞生产 loader | **明确阻塞** | §2、§5、§6 |
| ② 平台不可用时不得乐观 advertise | **达成（锁定为「默认不可用」纪律）** | §5 锁定项 L6 |
| ③ 稳定 API 不泄漏 Session/Unity 类型 | **当前真空成立；强制手段部分失效** | §4 实测 4.6、4.7、4.8 |

**逐条说明**

- ① 的四项资料性验收全部达成，且**推翻了设计文档钉的候选版本**（8.12.0 已过期三个发行）。
- ② 判定为阻塞：官方兼容矩阵**支持** Unity 6.3，但「生产 loader 可用」不是文档问题，而是必须经由
  Unity Editor + Installer + 本地 il2cpp 构建 + 设备 AOT 出包才能证明的问题，本机**完全不具备**该能力（§2）。
  资料齐备 ≠ loader 可放行；把前者当后者就是卡面禁止的「乐观 advertise」。
- ③ 当前 `Lumio.Client.HybridClrAdapter` **无任何 public 类型**（实测 4.6），故「不泄漏」真空成立；
  但其编译期强制手段 `eng/banned-public-api.txt` 经实测**完全未生效**（实测 4.7），这是必须在写 loader 之前修掉的前置条件。

---

## 2. 环境与可测性

### 2.1 宿主架构声明（Rosetta）

本机为 **Apple M5（arm64）**，但 .NET SDK 运行在 **`RID: osx-x64`（Rosetta 2 下的 x86_64）**。

```
$ uname -m
arm64
$ sysctl -n hw.optional.arm64
1
$ sysctl -n machdep.cpu.brand_string
Apple M5
$ dotnet --info | head -20
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

.NET workloads installed:
There are no installed workloads to display.
...
Host:
  Version:      10.0.11
```

**因此适用的跨仓纪律（LumioCoreEngine 通报、LumioServer 设计 G14、LumioGameRuntime 确认）：**
**本机任何时延数字只能作为「相对序关系 / 量级」，不得升格为设备预算。**
目标设备（iOS / Android IL2CPP）是 **arm64**；本机是 **x86_64-under-Rosetta**，二者指令集、JIT/AOT 代码生成、内存模型均不同。

**本卡的实际处置：本卡不产出任何时延数字。** 唯一执行的构建（§4.7 分析器探针）用于判定「诊断是否触发」这一布尔事实，
与架构和计时无关，Rosetta 不影响其结论。补充元数据的「~6 倍 dll 内存」是**官方文档陈述**（§3 事实 13），不是本机实测。

### 2.2 本机具备 / 不具备

| 能力 | 状态 | 证据 |
|---|---|---|
| .NET SDK 10.0.400 | 具备 | 上方 `dotnet --info` |
| `dotnet build` / `dotnet test` | 具备 | §4.6、§4.7、§4.8 |
| 公网只读检索（GitHub / hybridclr.cn） | 具备 | §4.3、§4.4 |
| **Unity Editor（任意版本）** | **不具备** | 下方 `ls` |
| Unity 6.3（6000.3.x）Editor | 不具备 | 同上 |
| IL2CPP Build Support 模块 | 不具备 | 同上（Editor 都不存在，模块无从谈起） |
| HybridCLR `Installer`（需 Editor 安装目录） | 不可执行 | §3 事实 17 |
| PlayMode 测试 / AOT player 出包 / 设备 smoke | **不可执行** | 同上 |

```
$ ls -la /Applications/Unity/Hub/Editor/
ls: /Applications/Unity/Hub/Editor/: No such file or directory
exit=1

$ ls -la /Applications/Unity/
ls: /Applications/Unity/: No such file or directory
```

> 注：共享上下文记为「`/Applications/Unity/Hub/Editor/` 为空」。**实测更强：`/Applications/Unity/` 整个目录不存在。**
> 结论方向一致（无任何 Unity Editor），此处以实测原文为准。

### 2.3 可测性判定

- **可实测**：仓库侧结构与门禁（asmdef、allowlist、架构测试、分析器是否生效、程序集公开面）。
- **可现查**：HybridCLR 官方版本、许可、兼容矩阵、AOT metadata API 与发行路径。
- **不可测（本机硬缺失）**：HybridCLR 在 Unity 6.3 上的 Installer 成功与否、il2cpp_plus 本地构建、
  AOT 补充元数据在真机的加载、`LoadMetadataForAOTAssembly` 的真实返回码、包体/内存/启动影响、卸载与回滚。
- **无可用代理**：HybridCLR 的核心是**定制 IL2CPP（il2cpp_plus）+ 自研寄存器解释器**。
  .NET NativeAOT / trim 分析**不构成**其代理证据（不同 AOT 实现、不同元数据模型、不同泛型实例化策略）。
  本卡因此**未执行**任何 NativeAOT / trim 实验，也不引用任何此类结论。

---

## 3. 现查的官方事实

抓取日期统一为 **2026-08-28**。HTML 页面为当日本地快照（`docs_*.html`，见 §4.1）；Git ref 与 raw 文件为当日实时复核（§4.3、§4.4）。

| # | 事实 | 值 | 来源 URL | 抓取日期 | 原文片段 |
|---|---|---|---|---|---|
| 1 | hybridclr_unity 最新 tag | **v8.14.1** | `https://github.com/focus-creative-games/hybridclr_unity.git`（`git ls-remote --tags`） | 2026-08-28 | `refs/tags/v8.14.1` |
| 2 | hybridclr（core）主线最新 tag | v8.13.0 | `https://github.com/focus-creative-games/hybridclr.git` | 2026-08-28 | `refs/tags/v8.13.0` |
| 3 | UPM 包名 / 版本字段 | `com.code-philosophy.hybridclr` @ `8.14.1` | `https://raw.githubusercontent.com/focus-creative-games/hybridclr_unity/v8.14.1/package.json` | 2026-08-28 | `"name": "com.code-philosophy.hybridclr", "version": "8.14.1"` |
| 4 | core 仓许可 | **MIT** | `LICENSE`（hybridclr core，本地快照 `LICENSE_core.txt`） | 2026-08-28 | `MIT License` / `Copyright (c) 2023 Code Philosophy Technology Ltd.` |
| 5 | unity 包仓许可 | **MIT** | `LICENSE`（hybridclr_unity，本地快照 `LICENSE_unity.txt`） | 2026-08-28 | `MIT License` / `Copyright (c) 2025 Code Philosophy Technology Ltd.` |
| 6 | 社区版商业条款 | **0 元、无限期使用** | `https://www.hybridclr.cn/docs/business/intro` | 2026-08-28 | 表格行：`社区版 ｜ 0 ｜ 无限期使用` |
| 7 | 官方兼容 Unity 版本列表 | 2019.4.x / 2020.3.x / 2021.3.x / 2022.3.x / 2023.2.x / **6000.x.y** | `https://www.hybridclr.cn/docs/basic/supportedplatformanduniyversion` | 2026-08-28 | `HybridCLR已经稳定支持了2019.4.x、2020.3.x、2021.3.x、2022.3.x 系列LTS版本及2023.2.x、6000.x.y等测试版本，并且支持所有il2cpp支持的平台。` |
| 8 | **Unity 6.3 = 6000.3.x 有专属支持行** | `hybridclr@v6000.3.x-8.13.0` + `il2cpp_plus@v6000.3.x-8.14.0` | `https://raw.githubusercontent.com/focus-creative-games/hybridclr_unity/v8.14.1/Data~/hybridclr_version.json` | 2026-08-28 | `{ "unity_version":"6000.3.x", "hybridclr" : { "branch":"v6000.3.x-8.13.0"}, "il2cpp_plus": { "branch":"v6000.3.x-8.14.0"} }` |
| 9 | 该行引用的两个 ref **真实存在** | core tag + il2cpp_plus tag 均可解析 | `git ls-remote --tags`（两仓） | 2026-08-28 | `f7b414d72115e818f2e7799bc78502ab05fc4051	refs/tags/v6000.3.x-8.13.0`；`0439eae2c6e6d0fe2fe89bdcb90ac98aec496ac0	refs/tags/v6000.3.x-8.14.0` |
| 10 | 支持平台含目标设备 | iOS arm64、Android armv7/armv8(arm64) | 同事实 7 页面 | 2026-08-28 | `Android armv7、armv8(arm64)` / `iOS arm64` |
| 11 | AOT metadata 入口 API | `HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly(byte[], HomologousImageMode)` | `https://raw.githubusercontent.com/focus-creative-games/hybridclr_unity/v8.14.1/Runtime/RuntimeApi.cs` | 2026-08-28 | `public static extern LoadImageErrorCode LoadMetadataForAOTAssembly(byte[] dllBytes, HomologousImageMode mode);` |
| 12 | **社区版必须用补充元数据** | 完全泛型共享为商业版专属 | `https://www.hybridclr.cn/docs/basic/aotgeneric` | 2026-08-28 | `基于full generic sharing完全泛型共享技术。该技术目前只在商业化版本提供。` |
| 13 | 补充元数据内存代价 | **约 6 倍 dll 大小，且不可回收** | 同上 | 2026-08-28 | `补充元数据加载后，大约会占用6倍dll大小的内存，而且这些内存无法回收。` |
| 14 | 元数据模式 | `Consistent`（须用裁剪后 dll）/ `SuperSet`（可用原始 dll） | 同上 | 2026-08-28 | `HomologousImageMode::SuperSet模式，即补充的dll是打包时裁剪后的dll的超集。` |
| 15 | 调用时机与线程 | 任意时机、只需一次、可后台线程 | 同上 | 2026-08-28 | `LoadMetadataForAOTAssembly函数可以在任何时机调用` / `你可以在其他线程异步加载。` / `补充元数据没有加载顺序的要求` |
| 16 | 发行路径（包） | UPM **git URL**（github / gitee），`#{tag}` 选版；无 registry / OpenUPM | `https://www.hybridclr.cn/docs/basic/install` | 2026-08-28 | `其他tag版本地址为 https://gitee.com/focus-creative-games/hybridclr_unity.git#{tag}` |
| 17 | **发行路径（关键约束）** | 装包后**必须**跑 `HybridCLR/Installer`，从 **Unity Editor 安装目录**复制文件 | 同上 | 2026-08-28 | `为了减少package自身大小，有一些文件需要从Unity Editor的安装目录复制。因此安装完插件后，还需要一个额外的初始化过程。` |
| 18 | Installer 版本不可自定义 | 只装 `Data~/hybridclr_version.json` 指定的分支 | 同上 | 2026-08-28 | `Installer会安装配置中指定的版本，不再支持自定义待安装的版本。` |
| 19 | macOS 侧工具链前置 | macOS ≥ 12、Xcode ≥ 13、git、cmake | 同上 | 2026-08-28 | `要求MacOS版本 >= 12，xcode版本 >= 13` / `安装 git` / `安装cmake` |
| 20 | **CoreCLR backend 表态** | **未找到官方来源** | roadmap / faq / changelog（见 §4.9） | 2026-08-28 | roadmap 全文正文仅一行：`支持extern函数` |

### 3.1 版本对照表（本卡核心资料产出）

`Data~/hybridclr_version.json` 是 Installer 的唯一版本真值（事实 18）。逐版读出的 `6000` 系列行：

| hybridclr_unity 包版本 | `6000` 行 core / il2cpp_plus | **`6000.3.x` 行 core / il2cpp_plus** | `6000.5.x` 行 |
|---|---|---|---|
| **v8.12.0**（设计文档 pin） | `v8.12.0` / `v6000-8.11.0` | `v6000.3.x-8.12.0` / `v6000.3.x-8.11.0` | 无 |
| v8.13.0 | `v8.13.0` / `v6000-8.13.0` | `v6000.3.x-8.13.0` / `v6000.3.x-8.13.0` | 无 |
| **v8.14.1**（当前最新） | `v8.13.0` / `v6000-8.14.0` | **`v6000.3.x-8.13.0` / `v6000.3.x-8.14.0`** | `v6000.5.x-8.14.0` / `v6000.5.x-8.14.0` |

**读出的三条结论：**

1. **设计文档的 pin 已过期。** `docs/LumioClient_five_requested_files/2026-08-27-client-framework-scaffolding-design.md:194`
   写「官方 HybridCLR 8.12.0 候选」，同文件 `:1793` 重复一次。实际最新为 **v8.14.1**，落后 3 个发行（8.13.0 / 8.14.0 / 8.14.1）。
2. **Unity 6.3 早在 8.12.0 就有专属支持行**，不是新增能力；8.14.1 在其上又补了 `6000.5.x`。
3. **UPM `package.json` 全版本均无 `unity` / `unityRelease` / `dependencies` 字段**（实测 4.4）。
   即：**UPM 元数据不施加任何 Unity 版本约束**，版本约束只存在于 `Data~/hybridclr_version.json`，
   由 Installer 在**编辑器内**消费。→ 本仓无法用 UPM 清单静态校验 Unity 版本匹配，只能靠 Installer 运行时。

---

## 4. 实测记录

### 4.1 复用上一轮已采集证据（不重跑）

**目的：** 先读回 15:43–15:48 采集的证据，避免重复劳动。

```
$ ls -la /private/tmp/.../scratchpad/harness/hybridclr-63
（36 项，含 LICENSE_core.txt / LICENSE_unity.txt / RuntimeApi.cs / HomologousImageMode.cs /
  pkg_v8.1{2,3,4}.*.json / ver_v8.1{2,3,4}.*.json / tags_{core,unity,il2cppplus}.txt /
  heads_*.txt / docs_*.html / sitemap.xml / fetch.sh）
```

**判读：** 版本、许可、兼容矩阵、AOT、发行路径五条线的原始素材齐备，全部复用。

**但发现两处上一轮证据缺陷，本轮已纠正：**

1. `docs_basic_supplementarymetadata.html` **是一个 404 页面**，不是文档内容：
   ```
   $ python3 <extract> docs_basic_supplementarymetadata.html
   找不到页面 | HybridCLR
   我们找不到您要找的页面。
   ```
   经 sitemap 核对，`www.hybridclr.cn/docs/basic/supplementarymetadata` **不存在**；补充元数据内容在
   `docs/basic/aotgeneric`。本文事实 12–15 因此改引 `aotgeneric`（§4.5）。
   **不得把该 404 当成「已核实补充元数据」。**
2. `releases_unity.json` 内容为空数组 `[]`（该仓不用 GitHub Releases，只用 tag）。故版本真值取 tag，不取 releases。

### 4.2 许可结论

**目的：** 给出明确许可结论（类型 + 有无附加义务 + 商业条款）。

```
$ cat LICENSE_core.txt
MIT License

Copyright (c) 2023 Code Philosophy Technology Ltd.
...
$ diff LICENSE_core.txt LICENSE_unity.txt
3c3
< Copyright (c) 2023 Code Philosophy Technology Ltd.
---
> Copyright (c) 2025 Code Philosophy Technology Ltd.
```

商业页原文（`docs/business/intro`）关键行：

```
社区版      0        无限期使用
专业版      邮件咨询商务   买断一个项目的使用权，同时包含2年技术支持，提供2年代码更新
```
社区版特性表包含：`解释执行 ✔`、`MonoBehaviour ✔`、`补充元数据 ✔`、`增量式GC ✔`、`Unity 2019-6000 LTS ✔`、`DOTS ✔`；
**不含** `完全泛型共享`、`元数据优化`、`global-metadata.dat加密`、`热重载`、`DHE技术`。

**判读（许可结论，锁定）：**
- 类型：**MIT**，core 与 unity 包**两仓一致**，仅版权年份不同（2023 / 2025）。
- 附加义务：**仅 MIT 标准义务**——保留版权声明与许可声明。**无 copyleft、无 royalty、无署名展示要求、无使用范围限制。**
- 商业条款：社区版 **0 元、无限期**，商用无需付费。专业版/旗舰版/热重载版是**可选增值**，不是使用前提。
- 对本仓影响：可安全以 MIT 记入 `eng/dependency-baseline.md`（建议行见 §5.2）。
- **注意（非许可问题、是工程问题）**：社区版无「完全泛型共享」，**因此补充元数据是强制路径**，
  连带承担事实 13 的「6 倍 dll、不可回收」内存代价。这必须进 `hybridclr-adapter` 的资源预算设计。

### 4.3 Unity 6.3 兼容判定（本卡核心）

**目的：** 明确回答「是否支持 Unity 6.3（6000.3.x）」，官方矩阵未覆盖则判阻塞。

命令与真实输出：

```
$ curl -sSL "https://raw.githubusercontent.com/focus-creative-games/hybridclr_unity/v8.14.1/Data~/hybridclr_version.json"
{
    "versions": [
    ...
    {
        "unity_version":"6000",
        "hybridclr" : { "branch":"v8.13.0"},
        "il2cpp_plus": { "branch":"v6000-8.14.0"}
    },
    {
        "unity_version":"6000.3.x",
        "hybridclr" : { "branch":"v6000.3.x-8.13.0"},
        "il2cpp_plus": { "branch":"v6000.3.x-8.14.0"}
    },
    {
        "unity_version":"6000.5.x",
        "hybridclr" : { "branch":"v6000.5.x-8.14.0"},
        "il2cpp_plus": { "branch":"v6000.5.x-8.14.0"}
    }
    ]
}
HTTP:200

$ diff <(sed '/^HTTP:/d' ver_v8.14.1.json) <(sed '/^HTTP:/d' verify_hybridclr_version_v8.14.1.json)
IDENTICAL          ← 与上一轮 15:46 采集完全一致，非本轮偶发
```

两个被引用 ref 的存在性复核：

```
$ git ls-remote --tags https://github.com/focus-creative-games/hybridclr.git | grep 'v6000\.3\.x-8\.13\.0'
f7b414d72115e818f2e7799bc78502ab05fc4051	refs/tags/v6000.3.x-8.13.0

$ git ls-remote --tags https://github.com/focus-creative-games/il2cpp_plus.git | grep 'v6000\.3\.x-8\.14\.0'
0439eae2c6e6d0fe2fe89bdcb90ac98aec496ac0	refs/tags/v6000.3.x-8.14.0

$ grep '6000.3.x-main' heads_il2cppplus.txt
0439eae2c6e6d0fe2fe89bdcb90ac98aec496ac0	refs/heads/6000.3.x-main
```

**判读：Unity 6.3（6000.3.x）官方支持 = 是。** 三重独立佐证：
(a) Installer 版本真值文件有**专属 `6000.3.x` 行**；(b) 该行引用的两个 ref **都真实存在**；
(c) il2cpp_plus 有**长期维护分支** `6000.3.x-main`，且 6000.3.x 系列 tag 从 `-8.8.0` 连续到 `-8.14.0`（7 个），说明是持续维护线而非一次性提交。

**必须同时记下的三条限定（不得乐观外推）：**

1. **文档矩阵页从未出现字符串「6000.3」。** 全文只写 `6000.x.y`：
   ```
   $ grep -oiE '.{80}6000\.3.{80}' docs_*.html | head
   （无输出）
   ```
   即 6.3 的支持**由版本清单与分支承载，不由兼容矩阵文档承载**。文档与代码在此处不同步。
2. **同页写着「只支持 LTS」**：原文 `出于维护成本考虑，HybridCLR只支持LTS系列版本。`
   且把 `6000.x.y` 与 `2023.2.x` 并列称作**「测试版本」**（原文：`及2023.2.x、6000.x.y等测试版本`）。
   Unity 6.3 是否属于 LTS 由 `SPIKE-UNITY-63-AOT-MATRIX` 判定；**若 6.3 非 LTS，本条与专属分支存在张力，须以商务确认收口。**
3. **changelog 最新条目停在 `2024.6.11 发布v6.0.0版本，正式支持2023和6000`**，完全没有 6000.3.x 记录。
   → changelog **不可**用作版本能力真值，只能用 `Data~/hybridclr_version.json` + git ref。

### 4.4 版本与 API 形态复核

```
$ git ls-remote --tags https://github.com/focus-creative-games/hybridclr_unity.git | grep -oE 'refs/tags/v8\.[0-9.]+$' | sort -V | tail -6
refs/tags/v8.10.0
refs/tags/v8.11.0
refs/tags/v8.12.0
refs/tags/v8.13.0
refs/tags/v8.14.0
refs/tags/v8.14.1

$ for f in pkg_v8.12.0.json pkg_v8.13.0.json pkg_v8.14.0.json pkg_v8.14.1.json; do ... done
（四个文件均只含 name/version/displayName/description/category/documentationUrl/
  changelogUrl/licensesUrl/keywords/author —— 无 "unity"、无 "unityRelease"、无 "dependencies"）

$ diff <(sed '/^HTTP:/d' RuntimeApi.cs) verify_RuntimeApi.cs
IDENTICAL to captured RuntimeApi.cs
```

`HybridCLR.Runtime` 程序集的**完整公开类型面**（v8.14.1 `Runtime/` 目录实测）：

```
$ curl -sSL "https://api.github.com/repos/focus-creative-games/hybridclr_unity/git/trees/v8.14.1?recursive=1" | <filter Runtime/>
  Runtime/HomologousImageMode.cs
  Runtime/LoadImageErrorCode.cs
  Runtime/ReversePInvokeWrapperGenerationAttribute.cs
  Runtime/RuntimeApi.cs
  Runtime/RuntimeOptionId.cs
  Runtime/HybridCLR.Runtime.asmdef

$ curl -sSL ".../v8.14.1/Runtime/HybridCLR.Runtime.asmdef"
{ "name": "HybridCLR.Runtime", ..., "autoReferenced": true, "noEngineReferences": false }
```

**判读：** `HybridCLR` 命名空间共 **5 个公开类型**：
`RuntimeApi`、`HomologousImageMode`、`LoadImageErrorCode`、`RuntimeOptionId`、`ReversePInvokeWrapperGenerationAttribute`。
**这直接回答卡面追问「只禁了 `HybridCLR.RuntimeApi` 一个类型名——是否足够？」→ 不够，见 §4.7。**

另：`Runtime/RuntimeApi.cs` 第 9 行 `using UnityEditor;` **无 `#if UNITY_EDITOR` 包裹**，而其 asmdef
`includePlatforms: []`（非 Editor-only）。该写法在 v8.12.0 / v8.13.0 / v8.14.0 / v8.14.1 **四个版本完全一致**。
**本机无 Unity Editor，无法判定 Unity 的 player 编译管线是否接受该 using**（考虑到该包广泛用于线上项目，
最可能是实践中可编译）。**本条列为待验证项，不作为缺陷主张**，收口清单见 §6 第 4 项。

### 4.5 AOT metadata 路径

**目的：** 确定补充元数据的 API、模式、代价与调用约束。

```
$ python3 <extract> docs_basic_aotgeneric.html     # https://www.hybridclr.cn/docs/basic/aotgeneric
使用 com.code-philosophy.hybridclr package中的 HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly 函数为AOT的assembly补充对应的元数据。
LoadMetadataForAOTAssembly函数可以在任何时机调用，另外既可以在AOT中调用，也可以在热更新中调用，你只要在使用AOT泛型前调用即可（只需要调用一次）。
补充元数据没有加载顺序的要求。
补充元数据加载后，大约会占用6倍dll大小的内存，而且这些内存无法回收。对内存有较高的要求，请使用商业版本的完全泛型共享技术...
...
HomologousImageMode::Consistent 模式，即补充的dll与打包时裁剪后的dll精确一致。因此必须使用build过程中生成的裁剪后的dll，则不能直接复制原始dll。
HomologousImageMode::SuperSet 模式，即补充的dll是打包时裁剪后的dll的超集。这个模式放松对了AOT dll的要求，你既可以用裁剪后的AOT dll，也可以用原始AOT dll。
...
执行 HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly 时会在内部将传入的dllBytes复制一份，调用完该接口后请不要保存dllBytes，否则会造成内存浪费。
如果RuntimeApi.LoadMetadataForAOTAssembly花费太多时间，造成卡顿，你可以在其他线程异步加载。
```

签名（`RuntimeApi.cs:24-31`，实测原文）：

```csharp
#if UNITY_EDITOR
        public static unsafe LoadImageErrorCode LoadMetadataForAOTAssembly(byte[] dllBytes, HomologousImageMode mode)
        { return LoadImageErrorCode.OK; }
#else
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern LoadImageErrorCode LoadMetadataForAOTAssembly(byte[] dllBytes, HomologousImageMode mode);
#endif
```

`HomologousImageMode.cs` 全文：
```csharp
namespace HybridCLR
{
    public enum HomologousImageMode { Consistent, SuperSet, }
}
```

**判读（可锁定的设计约束，供 `w7-hybridclr-scope-loader` 直接采用）：**
- Editor 下 `LoadMetadataForAOTAssembly` 是**桩实现，恒返回 `OK`**。
  → **Editor/PlayMode 的绿灯对补充元数据零证明力**；`hybridclr-adapter` 的失败 fixture
    「AOT 元数据缺失」**只能在真机/AOT player 上验证**，这是 §5 阻塞的一部分，不是可绕过项。
- 模式选 `SuperSet` 可免去「必须留存构建期裁剪 dll」的发行耦合；选 `Consistent` 则 Release 产物必须携带裁剪后 dll。
  **该选择影响 Release Manifest 内容，须在 loader 设计中显式定死。**
- `dllBytes` 调用后即可释放（内部已拷贝）→ adapter 不得持有原 buffer。
- 可后台线程加载 → 与 `modules/hybridclr-adapter/README.md:58` 的「后台校验结果通过有界队列交回」相容。
- 内存：**每个补充元数据 dll ≈ 6 倍自身大小且不可回收**（社区版无法规避）→ 必须计入 README 的「资源预算」校验项。

### 4.6 本仓 `hybridclr-adapter` 现状

**目的：** 核对模块现状与验收 ③ 的当前成立性。

```
$ find modules/hybridclr-adapter -type f | grep -vE '/(bin|obj)/'
modules/hybridclr-adapter/README.md
modules/hybridclr-adapter/src/Lumio.Client.HybridClrAdapter.csproj
modules/hybridclr-adapter/src/packages.lock.json
modules/hybridclr-adapter/tests/GlobalUsings.cs
modules/hybridclr-adapter/tests/Lumio.Client.HybridClrAdapter.Tests.csproj
modules/hybridclr-adapter/tests/packages.lock.json

$ find modules/hybridclr-adapter/src -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' | wc -l
0
```

程序集实际公开面（用 scratchpad 内的一次性 `System.Reflection.Metadata` 宿主读 PE，**未在仓库内建任何工程**）：

```
$ dotnet run --project <scratchpad>/reflect/reflect.csproj -- \
    modules/hybridclr-adapter/src/bin/Release/netstandard2.1/Lumio.Client.HybridClrAdapter.dll
total typedefs=1 public=0
--- AssemblyRefs ---
  netstandard 2.1.0.0
```

`packages/com.lumio.client/Runtime/HybridClrAdapter/Lumio.Client.HybridClrAdapter.asmdef`（全文，行号即文件行号）：
```
 2  "name": "Lumio.Client.HybridClrAdapter",
 4  "references": [
 5    "Lumio.Client.Handshake",
 6    "Lumio.Client.Observability"
 7  ],
13  "autoReferenced": false,
16  "noEngineReferences": false
```

**判读：**
- **验收 ③ 当前真空成立**：0 个 public 类型、仅引用 `netstandard 2.1.0.0`，既无 HybridCLR 也无 Unity 引用。
  **「不泄漏」目前不是因为设计有效，而是因为没有代码。** 写 loader 时该保证会被真正考验。
- asmdef `references` 与 `eng/project-reference-allowlist.json:38-41` 的
  `Lumio.Client.HybridClrAdapter: [Handshake, Observability]` **一致**；**未引用 `Lumio.Client.Session`** ——
  这正是「稳定 API 不泄漏 Session 类型」的实际强制手段（引用图，而非 API 分析器）。
- `noEngineReferences: false` 是**正确**的（该模块必须能引 UnityEngine），且
  `tests/Lumio.Client.ArchitectureTests/Unity/AsmdefGraphTests.cs:28-33` 已把它与 `UnityAdapter` 一并豁免该断言，
  `:42-48` 又禁止任何模块反向引用它（叶子约束）。**此处设计自洽，无需改动。**
- **文档漂移**：`modules/hybridclr-adapter/README.md:85` 写「当前仅包含本 README；尚未创建 Unity Package、Assembly Definition」，
  但 asmdef 与 csproj 均已存在。属既有小缺陷，本卡**不修**（不夹带），记入 §7。

### 4.7 `eng/banned-public-api.txt` 是否足够 —— 实测

**目的：** 卡面追问「只禁了 `HybridCLR.RuntimeApi` 一个类型名——是否足够？」

现状（`eng/banned-public-api.txt` 全文 9 行）：
```
6  T:HybridCLR.RuntimeApi; HybridCLR types stay in hybridclr-adapter
```
接线（`Directory.Build.targets:5-12`）：
```
 5  <ItemGroup Condition="'$(LumioProduction)' != 'false'">
 6    <PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers">
11    <AdditionalFiles Include="$(MSBuildThisFileDirectory)eng\banned-public-api.txt" />
```

**实测（对照实验，两个工程只差 `AdditionalFiles` 的文件名，其余完全相同；均建在 scratchpad，不进仓库）：**

```
$ dotnet build <scratchpad>/bannedapi-probe/probeA/probeA.csproj -v:m --nologo
   # probeA: AdditionalFiles 名为 banned-public-api.txt（与本仓完全相同的命名）
   # 内容 T:System.Text.StringBuilder; probe marker ，代码里确实用了 StringBuilder
  probeA -> .../probeA.dll
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet build <scratchpad>/bannedapi-probe/probeB/probeB.csproj -v:m --nologo
   # probeB: AdditionalFiles 名为 BannedSymbols.txt，内容与代码完全相同
.../probeB/Probe.cs(4,34): warning RS0030: The symbol 'StringBuilder' is banned in this project: probe marker
Build succeeded.
    3 Warning(s)
    0 Error(s)
```

**判读（本卡最重要的仓库侧发现）：**

**`eng/banned-public-api.txt` 完全没有生效——它是一个 no-op。**
`Microsoft.CodeAnalysis.BannedApiAnalyzers` 5.6.0 只读取**文件名为 `BannedSymbols.txt`** 的 `AdditionalFiles`；
名为 `banned-public-api.txt` 的附加文件被静默忽略，**不报错、不警告**，因此该缺陷至今无人发现。

连带后果：
1. `eng/banned-public-api.txt` 全部 9 条禁令（Unity / InputSystem / **HybridCLR** / Serilog / OpenTelemetry / Socket）
   **在编译期一条都没有强制力**。这不止影响本卡，**同样影响 `SPIKE-UNITY-63-AOT-MATRIX` 与 `SPIKE-OTEL-IL2CPP`。**
2. 当前唯一真正生效的 API 泄漏门是 `tests/.../Api/PublicApiSupplierLeakTests.cs`（运行期反射）。
   它用**前缀**匹配（`:7-15` `BannedPrefixes` 含 `"HybridCLR"`），**比 `banned-public-api.txt` 的精确类型名更宽**，
   能覆盖全部 5 个 `HybridCLR.*` 公开类型。
3. **因此，「只禁一个类型名是否足够」的答案是分层的：**
   - 对**分析器**（`eng/banned-public-api.txt:6`）：**不足够，而且根本没运行**。即便修好文件名，
     `T:HybridCLR.RuntimeApi` 也只挡 5 个公开类型中的 1 个，漏掉
     `HomologousImageMode` / `LoadImageErrorCode` / `RuntimeOptionId` / `ReversePInvokeWrapperGenerationAttribute`。
   - 对**架构测试**（`PublicApiSupplierLeakTests.cs:11`）：前缀 `"HybridCLR"` **足够覆盖类型名维度**。

`PublicApiSupplierLeakTests` 自身的**覆盖边界**（写 loader 前必须知道）——它只检查
「导出类型名」+「public 方法的返回类型与参数类型」（`:38-48`），**不检查**：
构造函数参数（`GetMethods` 不含 ctor）、公开字段、事件、基类型、实现的接口、泛型约束。
→ 例如 `public sealed class ScopeHandle { public ScopeHandle(HybridCLR.HomologousImageMode m) {...} }` **能通过现有全部门禁**。

### 4.8 门禁现状实跑

```
$ dotnet test tests/Lumio.Client.ArchitectureTests/Lumio.Client.ArchitectureTests.csproj \
    --filter "FullyQualifiedName~PublicApiSupplierLeakTests|FullyQualifiedName~AsmdefGraphTests" -v:q --nologo
Passed!  - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 213 ms

# 在旧 HEAD c571ff1 上：
$ node .spec/tools/spec-lint.mjs
spec-lint: 1 处不一致
  ✗ .spec/decisions/0002-cross-module-gates-and-state-ownership.md: 悬空链接:../../docs/LumioClient_Module_Architecture_Review_9ca0065.md
$ echo $?
1

# 在收口 HEAD 6b7e834 上（main 期间由他人修复，非本卡改动）：
$ node .spec/tools/spec-lint.mjs
spec-lint: OK
$ echo $?
0
```

**判读：** 4 个架构测试通过，但如 §4.6 所述其中 API 泄漏项**真空通过**。

`spec-lint` 在旧 HEAD 为红（`0002` 悬空链接），**在收口 HEAD 已转绿**——
修复由 `6b7e834` 带入（把悬空链接改成纯文本并注明该文件已于 `d818e33` 删除），**不是本卡的改动**。

**但仍不得声称「收口门槛通过」**，理由与 spec-lint 无关：
`AGENTS.md` 的收口门槛还要求 `node --test .spec/tools/spec-lint.test.mjs`（本卡**未执行**，与本卡无关），
且本卡的实质结论依赖 Unity/设备验证，那部分**不可执行**（§2.2）。本卡是预研记录，不是可放行交付。

补充：CI 侧无任何 .NET 门禁——
```
$ ls .github/workflows/
repository-policy.yml
$ grep -n -iE 'dotnet-version|setup-dotnet' .github/workflows/*.yml
（无匹配）
```
`repository-policy.yml` 只跑 `node .spec/tools/spec-lint.mjs` 与文档 `grep` 断言，
**从不执行 `dotnet build` / `dotnet test`**。`global.json` 固定 `10.0.400`（`rollForward: disable`），与本机已装 SDK 一致，
但**该固定值没有任何 CI 校验**。

### 4.9 CoreCLR 风险检索

**目的：** Unity 若转向 CoreCLR backend，HybridCLR 依赖的定制 IL2CPP（il2cpp_plus）整条热更路径可能失效。查官方有无表态。

```
$ grep -ril 'coreclr' /private/tmp/.../harness/hybridclr-63/
（无输出，exit 1）
```
检索覆盖的官方页面：`docs/other/roadmap`、`docs/help/faq`、`docs/other/changelog`、
`docs/basic/supportedplatformanduniyversion`、`docs/basic/install`、`docs/basic/buildpipeline`、
`docs/basic/aotgeneric`、`docs/business/intro`、`docs/intro`、`docs/beginner/quickstart`、`sitemap.xml`。

roadmap 页正文全文（去导航后）：
```
后续开发规划
支持extern函数
```

**判读：结论为「未找到官方来源」。** HybridCLR 官方**至今没有对 Unity CoreCLR backend 作任何公开表态**——
既没说支持，也没说不支持，roadmap 上没有任何相关条目。
**这不是「无风险」，而是「风险未被官方承认，且无缓解时间表」。** 参见 §7 R-1。

---

## 5. 结论

### 5.1 结论行

**明确阻塞。** 资料性验收（①）全部达成并锁定；**生产 loader（②）不可放行**，
因为其可用性只能由 Unity 6.3 Editor + Installer + 本地 il2cpp 构建 + 设备 AOT 出包证明，本机**无任何 Unity Editor**（§2.2）。

### 5.2 锁定项（可直接被下游采用，无需再验）

| ID | 锁定内容 |
|---|---|
| **L1** | **版本**：采用 `com.code-philosophy.hybridclr` **v8.14.1**（最新 tag）。**废弃设计文档的 8.12.0 pin**（已落后 3 个发行）。 |
| **L2** | **许可**：**MIT**（core + unity 包一致），无附加义务；社区版 0 元无限期，商用无需付费。可直接入基线。 |
| **L3** | **Unity 6.3 兼容**：**官方支持 6000.3.x**，v8.14.1 映射 `hybridclr@v6000.3.x-8.13.0` + `il2cpp_plus@v6000.3.x-8.14.0`，两 ref 均存在，且 `6000.3.x-main` 为持续维护分支。 |
| **L4** | **AOT metadata**：社区版**必须**用补充元数据（完全泛型共享是商业版专属）；入口 `RuntimeApi.LoadMetadataForAOTAssembly(byte[], HomologousImageMode)`；建议 `SuperSet` 模式以解除「必须留存裁剪 dll」的发行耦合；每 dll ≈ 6× 自身大小且**不可回收**，必须计入资源预算；调用后立即释放 buffer；可后台线程调用。 |
| **L5** | **发行路径**：UPM **git URL** + `#{tag}`（无 registry）；装包后**强制** `HybridCLR/Installer` 从 **Unity Editor 安装目录**取文件并构建本地 il2cpp；版本由 `Data~/hybridclr_version.json` 单方面决定，**不可自定义**；macOS 侧需 macOS ≥ 12 / Xcode ≥ 13 / git / cmake。 |
| **L6** | **Advertise 纪律（卡面②强制）**：`hybridclr-adapter` 实现的平台 Capability Provider **默认必须报告「热更能力不可用」**。只有在 §6 清单**全部**勾选、且目标平台**实机**验证通过后，才允许对该平台 advertise 为可用。**禁止**以「官方文档说支持」为由 advertise；禁止用 Editor 绿灯替代设备证据（§4.5：Editor 下该 API 是恒返回 `OK` 的桩）。 |
| **L7** | **Editor 证据无效性**：`LoadMetadataForAOTAssembly` 在 `UNITY_EDITOR` 下是桩实现。README 的失败 fixture「AOT 元数据缺失」**不得**用 Editor/PlayMode 验证，只能用 AOT player。 |

### 5.3 阻塞项与对下游任务的确切影响

| 阻塞 | 影响的任务 slug | 具体影响 |
|---|---|---|
| **B1 无 Unity Editor**（`/Applications/Unity/` 不存在） | `w7-hybridclr-scope-loader`、`w7-hybridclr-rollback-unload` | Installer 不可跑 → 本地 il2cpp 不可构建 → 无 AOT player → 加载 / 激活 / 回滚 / 卸载**全部无法验证**。这两卡**不得开工**（写出的代码无任何可执行验收路径）。 |
| **B2 无设备出包链路** | `w7-hybridclr-scope-loader`、`w7-unity-aot-device-matrix` | README:80 的「设备 Smoke：Desktop、iOS、Android 的 AOT、包体、内存、启动时长、卸载和重启路径」不可执行。 |
| **B3 `banned-public-api.txt` 为 no-op**（§4.7 实测） | `w7-hybridclr-capability-provider`、`w7-hybridclr-scope-loader`，**并外溢** `SPIKE-UNITY-63-AOT-MATRIX`、`SPIKE-OTEL-IL2CPP` | 验收 ③ 的编译期强制手段不存在。在修复前写 loader，`HybridCLR.*` 类型可无声穿过公共 API。**必须先修门，再写 loader。** |
| **B4 `UPSTREAM-GENERATED-CONTRACT-API-MAP` 未解**（既有阻塞，非本卡引入） | `w7-hybridclr-capability-provider`、`w7-hybridclr-scope-loader` | Scope artifact / entry / capability contract 真名不可得（`eng/upstream-api-map.md` 全部 `blocked-unpublished`），真实激活无法实现。 |
| **B5 Unity 6.3 的 LTS 属性未定** | 本卡 ← `SPIKE-UNITY-63-AOT-MATRIX` | 官方写「只支持 LTS 系列版本」，却把 `6000.x.y` 称作「测试版本」。若 6.3 非 LTS，L3 需降级为「有分支但非官方 LTS 承诺」，须商务确认。**本卡不代 R-00255 判定。** |

**可在阻塞下推进的部分**：`w7-hybridclr-capability-provider` 的**纯静态能力声明**部分
（不触碰 HybridCLR API、按 L6 默认报不可用、只依赖 `handshake` 端口）可在 **B3 修复后**先行实现并用现有架构测试验收；
但其「HybridCLR 可用」分支必须留空并显式返回不可用，直到 §6 清单勾满。

### 5.4 建议写入 `eng/dependency-baseline.md` 的行

> **仅为文本建议，本卡未修改该文件**（不夹带纪律）。待 §6 清单勾满、真正引入依赖时再落。

```markdown
| HybridCLR (com.code-philosophy.hybridclr) | 8.14.1 | MIT | UPM git URL + #tag（非 NuGet，不入 packages.lock.json）；Installer 版本由包内 Data~/hybridclr_version.json 单方决定 | 仅 unity-adapter/hybridclr-adapter；Unity 6000.3.x 走 hybridclr@v6000.3.x-8.13.0 + il2cpp_plus@v6000.3.x-8.14.0；社区版必须用补充元数据（≈6× dll 且不可回收），HybridCLR.* 类型不得穿过公共端口 |
```

行内各列对应现有表头 `Package | Version | License | Lock strategy | AOT / isolation`。
注意该表首句写「All direct packages must appear here … Versions live in `Directory.Packages.props`」——
HybridCLR **不是 NuGet 包**，落行时需同步在表前加一句说明「UPM 依赖不进 `Directory.Packages.props`，版本以 UPM tag 为准」，否则表头口径与该行矛盾。

---

## 6. 解阻塞前置条件（机器可判清单）

按顺序执行；每条给出**通过判据**。全部勾满前，L6 的「默认不可用」不得放开。

**第 0 组 — 仓库侧，现在就能做，不依赖 Unity**

- [ ] **P0-1 修复 banned-API 门失效。** 将 `Directory.Build.targets:11` 的 `AdditionalFiles` 指向一个**文件名为 `BannedSymbols.txt`** 的文件
      （重命名 `eng/banned-public-api.txt`，或新增同内容的 `eng/BannedSymbols.txt`）。
      **通过判据**：在任一生产工程临时写入一处 `HybridCLR.RuntimeApi` 引用后，`dotnet build` 输出包含 `warning RS0030`；
      移除后 `dotnet build` 回到 0 warning。（复现方法见 §4.7 对照实验。）
- [ ] **P0-2 补齐 HybridCLR 类型禁令。** 在该文件加入其余 4 个公开类型：
      `T:HybridCLR.HomologousImageMode`、`T:HybridCLR.LoadImageErrorCode`、`T:HybridCLR.RuntimeOptionId`、
      `T:HybridCLR.ReversePInvokeWrapperGenerationAttribute`。
      **通过判据**：`grep -c '^T:HybridCLR\.' <该文件>` 输出 `5`。
- [ ] **P0-3 收紧 `PublicApiSupplierLeakTests` 覆盖面。** 增加对构造函数参数、公开字段、事件、基类型与实现接口的检查。
      **通过判据**：新增一个「public 构造函数参数为 `HybridCLR.HomologousImageMode`」的红测能失败，加固后转绿。
- [ ] **P0-4 更新设计文档版本 pin。** 把 `docs/LumioClient_five_requested_files/2026-08-27-client-framework-scaffolding-design.md:194` 与 `:1793`
      的 `8.12.0 候选` 改为 `8.14.1`。**通过判据**：`grep -c '8\.12\.0' <该文件>` 输出 `0`。

**第 1 组 — 需要一台装了 Unity 的机器（本机不可能满足）**

- [ ] **P1-1 安装 Unity 6.3（6000.3.x）Editor**，含 **iOS Build Support (IL2CPP)** 与 **Android Build Support (IL2CPP + NDK)**。
      **通过判据**：`ls /Applications/Unity/Hub/Editor/` 输出含 `6000.3.` 前缀的目录；
      该目录下存在 `PlaybackEngines/iOSSupport` 与 `PlaybackEngines/AndroidPlayer`。
- [ ] **P1-2 装包并跑 Installer。** UPM `Add package from git URL`：
      `https://github.com/focus-creative-games/hybridclr_unity.git#v8.14.1`，菜单 `HybridCLR/Installer...` 执行完成。
      **通过判据**：工程下存在 `HybridCLRData/LocalIl2CppData-{platform}/il2cpp/`，且 Editor Console **0 error**。
- [ ] **P1-3 确认 Installer 实际拉取的分支**与 §3 事实 8 一致。
      **通过判据**：`HybridCLRData` 内 hybridclr 与 il2cpp_plus 两个 checkout 的
      `git rev-parse HEAD` 分别等于 `f7b414d72115e818f2e7799bc78502ab05fc4051`（`v6000.3.x-8.13.0`）
      与 `0439eae2c6e6d0fe2fe89bdcb90ac98aec496ac0`（`v6000.3.x-8.14.0`）。
- [ ] **P1-4 解决 §4.4 的 `using UnityEditor;` 悬念。**
      **通过判据**：一个仅包含 `HybridCLR.Runtime` asmdef 的 **player**（非 Editor）构建编译成功，无 `CS0246`。
      失败则须向官方报 issue 并在本仓记录规避方案。

**第 2 组 — 设备实证（B1/B2 真正解除的地方）**

- [ ] **P2-1 iOS arm64 与 Android arm64 各出一个 IL2CPP Release 包**，`Managed Stripping Level` 与生产一致。
      **通过判据**：两个包均构建成功并可在真机启动到首帧。
- [ ] **P2-2 补充元数据真机加载。** 真机调用 `RuntimeApi.LoadMetadataForAOTAssembly`（`SuperSet` 模式）。
      **通过判据**：对每个 AOT dll 返回值 `== LoadImageErrorCode.OK`（**不接受 Editor 结果**，理由见 L7）。
- [ ] **P2-3 AOT 泛型负向用例。** 构造一个「未在 AOT 实例化、且未补充元数据」的泛型调用。
      **通过判据**：真机上稳定抛出可分类异常，且 `hybridclr-adapter` 将其映射为稳定失败分类，进程不崩。
- [ ] **P2-4 资源预算实测。** 记录每个补充元数据 dll 的常驻内存增量。
      **通过判据**：实测增量 ≤ 6.5 × dll 字节数（官方称 ≈6×），且写入 README「资源预算」阈值。
- [ ] **P2-5 回滚与卸载。** 覆盖 README:78-79 的失败 fixture（签名/Hash/Release/Schema/权限/预算/元数据缺失）与三阶段回滚。
      **通过判据**：每个失败 fixture 下旧 Scope 仍可用，或返回明确「需重启」，无中间态泄漏。

**第 3 组 — 跨卡与商务**

- [ ] **P3-1** `SPIKE-UNITY-63-AOT-MATRIX`（R-00255）给出 Unity 6.3 是否 LTS 的结论（解 B5）。
- [ ] **P3-2** 若 6.3 非 LTS：邮件 `business@code-philosophy.com` 取得书面支持承诺，或改钉一个官方 LTS 版本。
      **通过判据**：仓库内留存书面答复摘要 + 日期。
- [ ] **P3-3** `UPSTREAM-GENERATED-CONTRACT-API-MAP` 解除（解 B4）。

---

## 7. 已知缺口与风险

**缺口（本卡未能覆盖，且诚实标注为未执行）**

- **G1** 所有 Unity / IL2CPP / 设备侧验证**未执行**——本机无 Unity Editor（§2.2 实测）。本卡对「HybridCLR 在 Unity 6.3 上真的能跑」**没有任何一手证据**，只有官方声明 + ref 存在性。
- **G2** 本卡**未执行**任何 NativeAOT / trim 实验。理由：HybridCLR 走定制 IL2CPP + 自研解释器，**NativeAOT ≠ IL2CPP**，此类结果对本卡无证明力，做了也只能当噪声。**本文因此不含任何 [代理] 证据。**
- **G3** 未验证 HybridCLR 与本仓 `netstandard2.1` / `LangVersion 9.0` 冻结值的相容性。官方 FAQ 明确：`主工程打包用.net standard，而热更新dll打包必须用.net 4.x` —— **热更 dll 的 TFM 与本仓核心冻结值不同**，该差异对 `hybridclr-adapter` 的构建配置有影响，**未展开**。
- **G4** 未读 `docs/basic/buildpipeline`（已下载但本卡未展开）与 `docs/basic/notsupportedfeatures`（未下载）。后者对「热更代码限制」是必读项，留给 `w7-hybridclr-scope-loader`。
- **G5** 未核实 Unity 6.3 自身的 IL2CPP 是否与 `il2cpp_plus@v6000.3.x-8.14.0` 的合并基线对齐。官方原话：`尽管有时候我们还未合并最新的il2cpp代码，但绝大多数情况下也是能正常工作的` —— **存在版本漂移窗口，只能靠 P1-2 实测**。

**风险**

- **R-1（高，跨卡）CoreCLR 断链风险。** Unity 若转向 CoreCLR backend，HybridCLR 的 il2cpp_plus 路径可能整条失效。
  **官方无任何表态**（§4.9 实测：全部官方页面 0 处 `coreclr`，roadmap 仅「支持extern函数」一行）。
  **与 `SPIKE-UNITY-63-AOT-MATRIX` 联动**：R-00255 必须查明 Unity 6.3 及其后续路线图对 CoreCLR 的计划；
  若 Unity 已宣布迁移时间表，本卡 L1/L3 需整体重估，`hybridclr-adapter` 的长期可行性存疑。
  **缓解**：README:19「隔离 HybridCLR/Unity API，向上层只输出稳定 Capability」的边界必须严格守住，
  使热更后端可整体替换（这也是 P0-1/P0-2/P0-3 必须先修的原因）。
- **R-2（高）门禁虚假安全感。** §4.7 证明 `banned-public-api.txt` 9 条禁令**全部未生效**且**静默失败**。
  在修复前，任何「架构测试通过」的绿灯都不能理解为「供应商类型未泄漏」。**外溢至 R-00255 与 SPIKE-OTEL-IL2CPP，应作为跨卡通报。**
- **R-3（中）文档与代码不同步。** 官方 changelog 停在 2024.6.11，兼容矩阵页只写 `6000.x.y` 且仍称其为「测试版本」，
  而 `Data~/hybridclr_version.json` 已有 6000.3.x / 6000.5.x 专属分支。**版本能力真值只能取版本清单 + git ref，不能取文档。**
- **R-4（中）社区版内存代价。** 补充元数据每 dll ≈ 6× 且不可回收（事实 13）。
  若热更 dll 总量达 10 MB，仅元数据即约 60 MB 常驻。移动端预算须提前建模，否则可能被迫采购商业版（改变 L2 的成本结论）。
- **R-5（低）`autoReferenced` 语义。** `HybridCLR.Runtime.asmdef` 为 `autoReferenced: true`，会被 Unity 的**预定义程序集**（`Assembly-CSharp` 等松散脚本）自动引用。
  本仓 10 个 asmdef 均通过显式 `references` 数组约束，**不受影响**；但 `AsmdefGraphTests` 只扫描 `packages/com.lumio.client`（`:54`），
  **不覆盖消费方 Unity 工程的松散脚本**。集成侧需另行约束。
- **R-6（低）`AssemblyReferenceLeakTests` 的禁用名已核实正确。** `:12` 的 `"HybridCLR.Runtime"` 与官方 asmdef `"name": "HybridCLR.Runtime"` **精确匹配**（§4.4 实测）。
  未禁 `HybridCLR.Editor`，但其 `includePlatforms: ["Editor"]` 使运行时程序集无法引用，风险可接受。
- **R-7（低，既有）文档漂移。** `modules/hybridclr-adapter/README.md:85` 声称尚未创建 asmdef，实际已存在。本卡不修（不夹带）。
- **R-8（已于本卡执行期间由他人解决）** `spec-lint` 在旧 HEAD `c571ff1` 为红（`.spec/decisions/0002` 悬空链接），
  在收口 HEAD `6b7e834` **已转绿**（§4.8 实测）。**本卡未修此项**，仅如实记录状态迁移。
- **R-9（中，跨卡）架构基线在本卡执行期间由 v1.2 迁到 v1.4。** `6b7e834` 把 `repository-policy.yml` 的断言目标
  从 `LumioGameEngine_Architecture_v1.2.md` / `LGE-V1.2-2026-08-27` 改为 `v1.4` / `LGE-V1.4-2026-08-27`，
  README 现为 `Baseline：LGE-V1.4-2026-08-27`。
  但模块侧仍整体停在 v1.2，实测：
  ```
  $ grep -n 'LGE-V1' .github/workflows/repository-policy.yml
  34:          grep -q 'LGE-V1.4-2026-08-27' README.md
  35:          grep -q 'LGE-V1.4-2026-08-27' docs/architecture/LumioGameEngine_Architecture_v1.4.md
  70:            grep -q 'LGE-V1.2-2026-08-27' "$readme"      ← 模块 README 仍断言 v1.2

  $ grep -n 'LGE-V1' modules/hybridclr-adapter/README.md
  9:- 架构基线：`LGE-V1.2-2026-08-27`

  $ grep -h -o 'LGE-V1\.[0-9]-2026-08-27' modules/*/README.md | sort | uniq -c
    11 LGE-V1.2-2026-08-27
  ```
  即**根 README 与 11 个模块 README 目前处于两个不同基线，且 CI 同时把两者都断言为「正确」**——
  这是一致的（CI 无矛盾），但语义上基线迁移只做了一半。本卡**不修**（不夹带），
  但下游 `w7-hybridclr-*` 三卡开工前应确认 `modules/hybridclr-adapter/README.md:10` 引用的公共契约章节
  （Host Profile、平台与能力 / Release、版本共存与更新）在 v1.4 下是否仍然成立。

---

## 附：本卡产出与证据落点

- 证据目录（只读复用 + 本轮新增，**均在仓库外**）：
  `/private/tmp/claude-501/-Users-cui-LumioGames-LumioClient/6cdd4d3c-3f76-4e58-8ee5-9d9db5f173a3/scratchpad/harness/hybridclr-63/`
  - 复用：`LICENSE_{core,unity}.txt`、`RuntimeApi.cs`、`HomologousImageMode.cs`、`pkg_v8.1*.json`、
    `ver_v8.1*.json`、`tags_{core,unity,il2cppplus}.txt`、`heads_*.txt`、`docs_*.html`、`sitemap.xml`
  - 本轮新增：`verify_hybridclr_version_v8.14.1.json`、`verify_RuntimeApi.cs`、
    `verify_HybridCLR.Runtime.asmdef`、`verify_HybridCLR.Editor.asmdef`、
    `bannedapi-probe/`（probeA / probeB 对照实验 + 构建日志）、`reflect/`（PE 公开面读取宿主）
- **本卡未修改仓库任何既有文件**，未在仓库内新建任何 `.csproj` / `.cs`。唯一新增文件即本文档。

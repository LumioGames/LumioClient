# hybridclr-adapter

> 将 HybridCLR 作为可选 Unity Platform Capability 接入，并负责客户端热更包的校验、加载和回滚边界。

## 状态

- 阶段：未实现
- 优先级：P1
- 架构基线：`LGE-V1.0-2026-08-27`
- 公共契约来源：[`Host Profile、平台与能力`](../../docs/architecture/LumioGameEngine_Architecture_v1.0.md)、[`Release、版本共存与更新`](../../docs/architecture/LumioGameEngine_Architecture_v1.0.md)
- 内部设计：[`LumioClient 模块化架构`](../../docs/specs/2026-08-27-client-module-architecture-design.md)

## 责任

- 实现 `handshake` 声明的平台 Capability Provider Port，报告实际可用的 HybridCLR/AOT 能力。
- 校验 Client Gameplay Assembly、补充元数据和相关 Artifact 的签名、Hash、GameRelease、Schema、权限和资源预算。
- 按 Manifest 固定顺序执行准备、加载、验证、激活、拒绝、回滚和卸载。
- 隔离 HybridCLR/Unity API，向上层只输出稳定 Capability 和加载结果。
- 检测 AOT 元数据缺失、Assembly 身份冲突、重复加载、资源超限和不允许的 API/权限。
- 保留最近有效 Gameplay Scope，以便新包激活失败时恢复或要求完整重启。

## 明确不负责什么

- 不热替换稳定 Runtime、Native ABI、CoreEngine、存档 Schema 或其他破坏性 Release 内容。
- 不实现 Server HybridCLR，不把 Server HybridCLR 作为 Client 的前置依赖。
- 不拥有 Release Catalog、Handshake 接受决策或 Gameplay Assembly 的业务内容。
- 不绕过签名、Hash、Release、Schema、权限或资源预算以支持开发快捷路径。
- 不允许第三方或热更 Assembly 获得裸指针、任意 Socket、未声明文件系统或稳定接口外权限。

## 公共入口与出口

**入口：** 已验证来源的 Release Manifest、Artifact/Assembly Buffer、签名与 Hash 元数据、平台/AOT 能力、资源预算和 Cancellation Scope。

**出口：** 平台 Capability 声明、Verified Artifact、不可变 Gameplay Scope Handle、激活/回滚/卸载结果和稳定失败分类。

HybridCLR、Unity Assembly 或补充元数据对象不得穿过稳定接口；上层只持有不透明且带 Generation 的 Scope Handle。

## 数据与控制流

1. Host 在握手前查询本模块的静态平台/AOT Capability，并交给 `handshake`。
2. Handshake/Manifest 准入后，Host 提供与精确 GameRelease 对应的 Artifact。
3. 本模块先校验长度、签名、Hash、身份、Schema、权限和预算，再准备加载环境。
4. Assembly 在隔离 Scope 中加载，验证导出 Contract 与 Manifest 后才允许激活。
5. 激活失败时销毁新 Scope，并保留旧有效 Scope 或返回必须重启的稳定结果。
6. Session/Host 关闭时停止新调用、排空受控工作并卸载 Scope；泄漏必须可诊断。

## 依赖

- 允许依赖：[`handshake`](../handshake/README.md)、[`observability`](../observability/README.md)。
- 外部依赖：Unity、HybridCLR、平台 AOT API、签名/Hash 库和生成的 Manifest/Gameplay Contract。
- 组合关系：[`unity-adapter`](../unity-adapter/README.md) 与本模块由 `LumioGame` Release Composition 选择性组装，二者无强制源码依赖。
- 禁止依赖：`session` 具体实现、Replica/Prediction、Server HybridCLR、NativeCore/VoxelEngine 源码。

## 生命周期与线程模型

- Gameplay Scope 状态为 `Absent -> Verifying -> Loading -> Ready -> Active -> Draining -> Unloaded`，任一准备阶段可进入 `Rejected/Faulted`。
- Unity/HybridCLR 要求主线程的 API 只在声明线程调用；后台校验结果通过有界队列交回。
- Scope Handle 带 Generation；旧 Scope 的异步结果在切换后必须丢弃。
- 同一进程的稳定 Runtime/CoreEngine 组合唯一，加载热更 Assembly 不得引入第二套 Native 组合。

## 失败与恢复

- 可拒绝：签名/Hash/Release/Schema/权限/预算不匹配、AOT 元数据缺失或平台不支持。
- 可回滚：新 Scope 尚未对 Session 生效且旧 Scope 仍完整可用。
- 需重启：稳定 Runtime/ABI 变化、无法安全卸载、旧 Scope 不可恢复或 Manifest 要求完整 Release。
- 可致命：绕过校验后执行、Scope 交叉引用导致状态未知或加载第二套 Native 组合。

## 可观测性

- 记录校验与加载阶段时长、Artifact 身份/大小、Capability、内存预算、激活、回滚、卸载和泄漏结果。
- 关联字段来自生成的 Lumio Event Schema；本模块额外提供 Manifest/Assembly Hash、Platform、Scope Generation 和加载阶段，不在 README 复制共享字段清单。
- 不记录 Assembly 内容、签名私钥或用户数据；失败包只包含允许外发的 Hash、Manifest 和诊断元数据。

## 验证

- 正向 Fixture：合法签名/Hash/Release/Schema 的 Assembly 在支持平台加载并激活。
- 失败 Fixture：签名错误、Hash 错误、Release/Schema 不匹配、权限超限、AOT 元数据缺失和资源超限。
- 回滚测试：加载、验证、激活各阶段失败时旧 Scope 保持可用或明确要求重启。
- 设备 Smoke：Desktop、iOS、Android 的 AOT、包体、内存、启动时长、卸载和重启路径。
- 安全测试：热更 Assembly 不能绕过 Capability/权限访问稳定接口外资源。

## 目录

- 当前仅包含本 README；尚未创建 Unity Package、Assembly Definition 或选择 HybridCLR 版本。
- SDK/平台版本矩阵必须在首次实现工程时固定，并同步仓库代码风格与测试规范。

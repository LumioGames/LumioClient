# unity-adapter

> 将 Unity 生命周期、帧循环、平台输入和 Renderer 接到稳定的 LumioClient 模块接口。

## 状态

- 阶段：未实现
- 优先级：P1
- 架构基线：`LGE-V1.2-2026-08-27`
- 公共契约来源：[`Host Profile、平台与能力`](../../docs/architecture/LumioGameEngine_Architecture_v1.2.md#10-host-profile平台与能力)、[`Native、Managed 与 CoreEngine`](../../docs/architecture/LumioGameEngine_Architecture_v1.2.md#8-nativemanaged-与-coreengine)
- 内部设计：[`LumioClient 模块化架构`](../../docs/specs/2026-08-27-client-module-architecture-design.md)

## 责任

- 将 Unity 应用启动、暂停、恢复、焦点、退出和场景生命周期转换为稳定 Host 命令。
- 将 Unity 帧循环和平台时钟转换为 `session` 消费的 Host Tick 驱动，不定义 Runtime Phase。
- 将 Unity Input System 或平台输入事件转换为 [`input`](../input/README.md) 的平台无关 Sample。
- 将 Session/Runtime 产生的 Presentation Diff 应用到 Renderer、GameObject、UI、音频或其他表现 Adapter。
- 声明 Desktop、iOS、Android 的 Render/Input/Platform Capability 与资源预算。
- 承担 Unity 主线程调度、域/场景重载、设备诊断和退出阶段资源释放。

## 明确不负责什么

- 不保存 Replica/Prediction 权威或预测真相，不直接读取内部 ECS/Replica Storage。
- 不定义 Gameplay UI、具体内容、Input Mapping 或 GameObject 组织方式；这些由 Game 提供。
- 不执行 Handshake 判定、Snapshot/Delta Apply 或 Prediction Rollback。
- 不校验或加载 HybridCLR Gameplay Assembly；该能力由独立 Adapter 提供。
- 不推进 Session 启动状态，也不绕过 Gameplay Scope 激活门；激活由 `session` 通过 Composition 注入的端口编排，本模块只消费 Session 状态与结果。
- 不让 UnityEngine、Input System、Renderer 或平台 SDK 类型穿过稳定 Client 模块接口。

## 公共入口与出口

**入口：** Unity 生命周期回调、帧时机、平台输入事件、设备/Renderer 能力，以及 `session` 输出的状态和 Presentation Diff。

**出口：** Host Tick、平台无关 Input Sample、Host 生命周期命令、Capability 声明、表现应用结果和设备诊断事件。

Unity 类型只存在于本模块内；进入核心模块前必须转换为稳定值，核心输出也必须经 Adapter 才能操作 Unity 对象。

## 数据与控制流

1. Unity Host 启动 Observability 与稳定 Runtime，再创建 Session 和平台 Adapter；Gameplay Scope 激活实现（预编译或 HybridCLR）由 Release Composition 选择并注入 `session`。
2. 每帧按 Host Profile 计算 Tick 驱动，并调用 `session` 的稳定入口。
3. 平台输入回调写入有界采样队列，由 `input` 在 Client Owner Thread 归一化。
4. Session 输出 Presentation Diff，本模块在允许的 Unity 主线程阶段应用到 Game 提供的 Presentation Binding。
5. 暂停/失焦时按 Profile 停止或调整 Tick/Input；恢复时不绕过 Session 重连/同步规则。
6. 退出时停止新输入和表现更新，等待 Session 有界关闭，再释放 Unity 资源。

## 依赖

- 允许依赖：[`session`](../session/README.md)、[`input`](../input/README.md)、[`observability`](../observability/README.md)。
- 外部依赖：Unity、Unity Input System、平台 SDK 和 Game 提供的 Presentation Binding。
- 可选组合：[`hybridclr-adapter`](../hybridclr-adapter/README.md) 由 Release Composition 选择，不形成强制源码依赖。
- 禁止依赖：Replica/Prediction 内部实现、Server 实现、NativeCore/VoxelEngine 源码。

## 生命周期与线程模型

- Unity Host 生命周期映射为 `Created -> Starting -> Running <-> Paused -> Stopping -> Stopped/Faulted`。
- Unity 对象和 Renderer 只在 Unity 主线程访问；网络和 IO 回调不得直接操作 Unity 对象。
- Client Owner Thread 可以与 Unity 主线程相同或由 Host Profile 指定，但边界必须显式且通过队列/批次交接。
- 场景切换、Domain Reload 和应用退出必须使用 Generation 隔离迟到回调。

## 失败与恢复

- 可降级：非必需 Renderer/设备能力缺失、低优先级表现资源不足。
- 可拒绝：必需 Platform/Render/Input Capability 缺失、资源预算不满足或平台版本不支持。
- 可致命：Unity 主线程边界被破坏、稳定 Runtime 初始化失败、退出后仍访问 Unity 对象。
- 表现应用失败只能影响 Presentation 并产生诊断，不得回写 Replica/Prediction 状态作为修复。

## 可观测性

- 记录启动阶段、首帧/首 Active 时长、帧/Tick 差异、输入队列、表现应用耗时、内存、暂停/恢复和退出结果。
- 关联字段来自生成的 Lumio Event Schema；本模块额外提供 Platform、RenderProfile、设备能力和 Unity 生命周期阶段，不在 README 复制共享字段清单。
- 设备型号与用户数据按隐私策略采集；不得默认记录平台账号、输入文本或认证材料。

## 验证

- Edit/Play Mode 测试：生命周期映射、暂停/恢复、场景切换、输入转换和表现绑定。
- 设备 Smoke：Desktop、iOS、Android 启动、握手、Active、退出和故障提示。
- 线程测试：非主线程 Unity API 访问被结构性阻止，迟到回调不能访问已销毁 Generation。
- 性能测试：启动时长、包体、RSS/GC、帧/Tick 抖动、输入延迟和表现批量成本。
- 集成测试：不启用 HybridCLR 时仍能通过预编译 Gameplay Assembly 启动合法 Unity Client。

## 目录

- 当前仅包含本 README；尚未创建 Unity Package 或 Assembly Definition。
- Game 的具体 Scene、Prefab、UI 和 Presentation Binding 不进入通用客户端模块。

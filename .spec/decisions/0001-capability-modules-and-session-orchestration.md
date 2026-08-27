# 0001 · 按能力模块组织客户端并由 session 统一编排

- 日期:2026-08-27
- 状态:生效

## 背景

公共架构基线已经为 `LumioClient` 划定连接、握手、复制、预测、输入、Unity/HybridCLR 和 Headless Bot 等职责，但原有模块表没有明确 `ClientReplicaSession` 的内部所有者，也没有冻结本仓模块之间的源码依赖方向。若直接开始实现，Session 状态机可能散落在 `connection`、`handshake`、`replica` 和 `prediction` 中，平台适配也可能反向渗入稳定模块。

仓库同时要求按能力模块组织内容，并让每个模块在源码出现前具备一份可以独立说明边界的 README。

## 决策

1. 本仓内部能力统一放在 `modules/<name>/`，每个模块必须先有非空 `README.md`，再引入工程或源码。
2. 首批固定 11 个模块：`session`、`connection`、`handshake`、`replica`、`prediction`、`input`、`persistence`、`observability`、`unity-adapter`、`hybridclr-adapter`、`bot`。
3. `session` 是 `ClientReplicaSession`、客户端状态机和跨模块调用顺序的唯一所有者；它只编排已发布的 Runtime 能力，不重新实现复制、回滚、ECS 或 GAS 语义。
4. 核心能力和 Host/Adapter 之间使用单向依赖。`replica` 与 `prediction` 不直接依赖彼此；二者通过已发布 Runtime 契约形成原子更新边界，并由 `session` 编排。
5. `observability` 是只提供事件上下文和 Sink 端口的叶子依赖。Unity、HybridCLR、Renderer、平台 UI 和 Bot 类型不得进入稳定核心模块的公共接口。
6. 首批不建立全局 `common`、`shared`、`utils`、`presentation` 或第二套 `contracts` 模块。共享内容只有在具备独立所有权、生命周期和验证边界后才能升级为模块。
7. 根 README 只承担仓库边界和模块索引；各模块 README 是模块当前职责、非职责、依赖、失败和验证面的入口。详细依赖图与 README 契约见 [`模块化架构设计`](../../docs/specs/2026-08-27-client-module-architecture-design.md)。

## 后果

- `session` 会成为依赖多个叶子能力的顶层编排模块，这是为了集中状态机所有权而接受的依赖扇入；叶子模块不得反向引用它。
- 每次改变模块所有权、公开入口、依赖方向、状态机或失败语义，都必须同步模块 README；改变本决策时新增 ADR，不改写本记录。
- 每个模块可以独立理解和测试，但不承诺独立发布。是否拆分单独的 abstractions 工程由真实的替换或发布需求驱动。
- 模块文档会增加维护成本，Repository Policy 因此校验登记模块的 README、标题、必要章节和根索引链接。
- 本决策只涉及 `LumioClient` 内部结构，不改变公共 Schema、Envelope、ABI、错误码、时序或跨仓依赖图。

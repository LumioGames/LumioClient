---
name: repository-architecture
description: 仓库边界与架构契约——客户端状态所有权、复制预测和 Architecture Gate;改连接、Replica 或平台边界前查
metadata:
  type: doc
  status: 已交付
---

# 仓库边界与架构契约

## 规范来源与优先级

- Agent 的开发流程、测试政策和交付规则以 `.spec/` 为权威。
- 模块边界以根 [`README.md`](../../../README.md) 为本仓入口；共享架构以 `LumioGameEngineArchitecture` 的 `LGE-V1.2-2026-08-27` 为唯一来源，本仓 [`架构镜像`](../../../docs/architecture/LumioGameEngine_Architecture_v1.2.md) 只读。
- 冲突时不得在本仓自行扩展公共 Envelope、Schema 或依赖方向；先在架构源完成 ADR、Fixture 和新 Baseline。

## 所有权边界

- 本仓拥有 Connection/Handshake、ClientReplicaSession、客户端 World / VoxelReplicaWorld、Input/Prediction 历史、Unity/HybridCLR Host 与 Headless Bot 生命周期。
- Runtime 提供复制、回滚和状态语义，Game 提供 Component/Mapping/表现内容；Client 只负责连接、调用和呈现。
- 本仓不拥有 Server 权威状态、Server Wall Clock、Release Pool、Voxel 内部存储或第二套 NativeCore/VoxelEngine。
- Server 与 Client 永远拥有独立本地状态；LocalEmbedded 也必须走完整 Schema、Envelope、权限、大小限制、有界队列和 Tick 交付路径。

## 内部模块文档

- 根 [`README.md`](../../../README.md) 是模块索引；每个 `modules/<name>/README.md` 是对应模块责任、依赖、失败和验证面的入口。
- 内部模块结构与依赖规则见 [`模块化架构设计`](../../../docs/specs/2026-08-27-client-module-architecture-design.md)，决策依据见 [`ADR 0001`](../../decisions/0001-capability-modules-and-session-orchestration.md)。
- 模块目录必须先有 README 再引入源码；模块所有权或依赖方向变化时，同一改动同步对应 README。

## 架构源发布物的消费通道

- 架构源的 generated 产物**不上任何 NuGet feed**;公共消费模型是字节级只读镜像加 sha256 锁。镜像在 [`contract-mirror/`](../../../contract-mirror/MIRROR.md),pin 是 commit sha(不是分支名),范围是整目录规则而非文件清单,决策见 [`ADR 0004`](../../decisions/0004-architecture-source-readonly-mirror.md)。
- **镜像只读**:`contract-mirror/upstream/` 下任何手改都是缺陷,不是修复。上游有错就在架构源改——本仓不拥有公共 schema / fixture / ids / 生成物。
- **两条检查不可合并**:`bash eng/verify-contract-mirror.sh` 是硬门禁(只读本仓,已进 CI,篡改退 `33` 并点名);`bash eng/sync-contract-mirror.sh --source <path> --check` 是漂移报告(需要架构源检出,恒退 `0`)。需要兄弟仓路径的检查放进 CI 等于没有门,`--source` 因此是必填参数、不从环境变量取。
- 对镜像内容断言一律用「存在性 + 身份」(具名 SchemaId 在册、BaselineId 相等),**不硬编码任何计数**——上游 additive 增补是被鼓励的,计数断言必然腐烂。
- 镜像目录不进 `LumioClient.slnx`、不放 `Directory.Build.*`:产物走源码拷贝,不作为工程引用。

## Architecture Gate

- 进入 `Active` 前必须完成 Release/Manifest/Schema/ABI/Capability 校验和 FullSnapshot；Gap、未知 Baseline 或 Revision 冲突触发明确 Resync。
- 网络线程只入有界队列；Replica/Prediction Processor 在 Runtime 固定 Phase 消费，表现层不得成为状态真相，所有预测必须有回滚边界。
- 客户端快捷路径不得绕过 Codec、Envelope、权限或 Baseline；不能通过合并 Server/Client World 优化移动端资源。
- 连接、重连、维护、更新与错误都必须形成可诊断事件；协议变化必须在架构源补齐正向/失败 Fixture。
- Unity/HybridCLR 热更包必须校验签名、Hash、Release、Schema、权限与资源预算；破坏性 Runtime/ABI/存档变化只能走完整 Release。

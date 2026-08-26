# LumioClient

> LumioGameEngine v0.2 架构中的通用客户端运行时与宿主适配层。

## 定位

`LumioClient` 是可被具体游戏复用的客户端基础设施，不是最终游戏产品。它连接服务器、维护 Replica/Prediction 状态，并把 Rust Core Engine 能力适配给 C# 上层。正式 UI、场景表现、内容与游戏资产属于 `LumioGame`。

首个实现阶段采用 .NET 10 Headless Test Host；渲染引擎接入通过后续 Adapter 和 ADR 完成。

## 职责

- 客户端连接状态、握手、断线重连、消息收发和传输适配。
- Replica World Apply、Snapshot/Delta、Revision、Ack、缺口检测和 Resync。
- 客户端预测记录、确认、校正和回滚框架。
- 加载 `LumioNativeCore` 与 `LumioVoxelEngine` 平台产物。
- 实现 `LumioGameRuntime` 的客户端 Core Engine Adapter。
- 为未来 Unity、自研 Renderer 或其他 Host 提供输入、表现和平台边界。

## 依赖关系

### 上游依赖

- [`LumioNativeCore`](https://github.com/LumioGames/LumioNativeCore)：客户端原生高性能能力。
- [`LumioVoxelEngine`](https://github.com/LumioGames/LumioVoxelEngine)：客户端 Chunk、本地查询、Mesh 数据和预测体素视图。
- [`LumioGameRuntime`](https://github.com/LumioGames/LumioGameRuntime)：共享 ECS、Replica、稳定抽象与 Gameplay 契约。
- [`LumioServer`](https://github.com/LumioGames/LumioServer)：只消费其发布的网络传输信封契约。

### 下游使用者

- [`LumioGame`](https://github.com/LumioGames/LumioGame)：在本运行时之上实现客户端游戏、UI、表现、输入和内容。

```text
LumioNativeCore + LumioVoxelEngine + LumioGameRuntime
                         └─> LumioClient
                             └─> LumioGame client
```

## 契约所有权

本仓库拥有客户端连接状态机、Replica Apply、预测记录和宿主 Adapter 契约；不重新定义 Native ABI、Voxel ABI、服务器信封或具体 Gameplay Schema。

## 禁止事项

- 禁止包含具体游戏 UI、角色、关卡、技能内容、美术资产或商业配置。
- 禁止成为服务器权威状态来源或信任客户端预测结果作为最终结果。
- 禁止复制服务器完整 ECS/Gameplay 权威状态之外的无界数据。
- 禁止重新定义 `LumioNativeCore`、`LumioVoxelEngine` 或 `LumioServer` 已拥有的协议。
- 禁止让 Renderer、Unity 类型、DOM 或平台 UI 反向污染 `LumioGameRuntime`。
- 禁止依赖 `LumioGame` 源码。

## 当前状态

`v0.1.0` 仅冻结仓库职责与依赖边界；C# 基线为 .NET 10 LTS，尚未发布代码或软件包。


---
name: replica-world-chat
description: 客户端 World 绑定/查询与聊天呈现——改 Client World、Attribute Query 或 UI 聊天窗时查
metadata:
  type: doc
  status: 实施中
---

# 客户端 World 与聊天呈现

每个客户端连接拥有一份客户端 World，由同一 `WorldManager` 类创建（ADR-058 §17），不再是字符串属性袋。设计真值在架构仓 `ecs.md` M1a / M4 / §4.5；本仓只记 Client 消费口径。

## 客户端建世界

- 写法与服务器相同：`WorldManager.Create(GeneratedRegistry.Instance)`，**不传 `instanceId`**（客户端不发号；生成注册表自带端别）。
- 连上后前两条消息：欢迎消息（世界实例 ID + 自己的 NetEntityId）经 `Enqueue(WorldMessage)` 在提交相绑 `World.Self`；第一条创建记录是游戏声明的 WorldEntity。
- FullSnapshot / Delta 解码为创建 / 字段变化 / 销毁记录，进客户端提交相；创建优先。客户端建实体：同一 EntityType 模板 → Awake → PostAttribute → Start。
- 字段上行只按 `Authority.Owner`。Bot.Host 用同一客户端 World。

## 同进程双端

单机 / 本地联调 = 两个 Manager（服务器程序集一个、客户端程序集一个）+ 内存环回（`server.outbox → client.Enqueue`）。回调、同步、权限、校验与联网零差异。不共用一个 World。

## 变化钩子

每个 `Sync` 字段一对可选 partial：`OnXChanging` / `OnXChanged(old, new, reason)`（容器 `ListChange` / `DictChange`）。默认 `Notify.Remote` 只收对端（`Sync` / `Correction`）；自己写自己不收。`Notify.All` 才收 `Local`。首次填值不触发；整包先写入再统一触发 Changed。WhenAll 组合器后置（架构仓 ecs.md §6）。

## 绑定、查询与聊天

- **绑定与查询**：准入写入 C-2 五元组（`entityType` 由 `TypeOf` 派生，无 `IdentityComponent.Kind`）；`Self` 与 `QueryAttribute` 只读本连接这份世界。
- **ConnectionSuperseded**：旧连接收到后停止输入，不自动重连，回登录界面。
- **聊天**：`OnChatMessage(string line)` 到达即交给 UI。ECS 组件上不留窗口字段；不从 FullSnapshot 回放历史。服务器把「名字: 内容」拼进 `line`（C-1 不加字段，按 UTF-8 字节卡 512）。
- **契约**：字段真值是架构仓 C-1 / C-2 JSON。本仓不内嵌协议副本。

## 相关

- [`0005`](../../../decisions/0005-chat-event-netentityid-string-bridge.md)
- [`0006`](../../../decisions/0006-room-chat-event-not-gated-by-receiver-aoi.md)
- [`replica` 模块 README](../../../../modules/replica/README.md)

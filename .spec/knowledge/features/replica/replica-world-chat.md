---
name: replica-world-chat
description: ReplicaWorld 绑定/查询与 Room 聊天呈现——改 Replica 实体映射、Attribute Query 或 Browser/Bot 聊天窗时查
metadata:
  type: doc
  status: 已交付
---

# ReplicaWorld 实体映射与聊天呈现

每个客户端连接拥有独立客户端 World，现行 `ReplicaWorld` API 是其薄门面，内部是 Runtime `WorldManager.Create(GeneratedRegistry.Instance)`（不传 instanceId）。FullSnapshot/Delta 只经既有 `StageAuthority` → Runtime 事务 → `ObserveRuntimeOutcome` 提交后才 `Enqueue` 到客户端 World。

## 设计

- **绑定与查询**：准入写入 C-2 五元组并投影到客户端 World 的创建记录；`SelfLookup` 读本连接绑定，`QueryAttribute` 委托 Runtime 生成声明表与世界字段。client-replica 仅可读 `replication=replicated` 且当前可见的 AttributeId；persist-only / server-only 返回 `invisible`。
- **FullSnapshot 重建**：重连/首入按 C-1 `stateBlocks` 解码为创建记录，经 `WorldManager.Enqueue` 按 EntityType 建实体（Awake → PostAttribute → Start）。空 `stateBlocks` 表示零活体。旧代次实体不残留。聊天窗清空且不回放历史。重建成功后再启用输入。非 C-1 形状（非 JSON / 缺 stateBlocks）为 `bad_envelope`。
- **ConnectionSuperseded**：旧连接收到该通知后停止输入、会话进入 `Superseded` 终态并抛 `SessionSupersededNotice`，不自动重连；显式 `Login()` 才开新代次。
- **聊天呈现**：`chat.event` 仅经 Delta 追加到客户端聊天窗（MessageId、Room sequence、sender NetEntityId、text）。C-1 `visibility=room` 只校验信封/摘要/序号/text 上限；接收方是否已 Admit 发送者、发送者是否 InAoi / tombstoned 只影响 Attribute Query，不决定是否入窗。畸形信封在 Stage 拒绝，零可见突变。
- **消费者**：`ReplicaChatConsumer` 区分 Browser 与 Bot；二者不得共享 World/Entity 引用。浏览器静态页 `modules/web/chat/` 只渲染已接受事件，不扩展 hello-wire-v1。Bot 发言节奏由 Client Timer Manager 适配层消费 NativeCore `tickFrame`（N=5），每次触发提交一条 `chat.input`；Client 不自建定时器或绑定表。
- **契约**：字段真值是架构仓 C-1 / C-2 JSON。测试定位架构仓检出，本仓不内嵌协议副本。C-1 `entity.identity` 金样在 replica 测试夹具中按 pin `997bcf3` 校验编码器。

## 相关

- [`0005`](../../../decisions/0005-chat-event-netentityid-string-bridge.md)
- [`0006`](../../../decisions/0006-room-chat-event-not-gated-by-receiver-aoi.md)
- [`replica` 模块 README](../../../../modules/replica/README.md)

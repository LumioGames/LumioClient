---
name: replica-world-chat
description: ReplicaWorld 绑定/查询与 Room 聊天呈现——改 Replica 实体映射、Attribute Query 或 Browser/Bot 聊天窗时查
metadata:
  type: doc
  status: 已交付
---

# ReplicaWorld 实体映射与聊天呈现

每个客户端连接拥有独立 `ReplicaWorld`。FullSnapshot/Delta 只经既有 `StageAuthority` → Runtime 事务 → `ObserveRuntimeOutcome` 提交后才写入实体或聊天窗。

## 设计

- **绑定与查询**：准入写入 C-2 五元组；`SelfLookup` 与 `QueryAttribute` 只读本连接副本。client-replica 仅可读 `replication=replicated` 且当前可见的 AttributeId；persist-only / server-only 返回 `invisible`。
- **聊天呈现**：`chat.event` 仅经 Delta 追加到客户端聊天窗（MessageId、Room sequence、sender NetEntityId、text）。FullSnapshot 清空窗口，不回放历史。畸形/未授权事件在 Stage 拒绝，零可见突变。
- **消费者**：`ReplicaChatConsumer` 区分 Browser 与 Bot；二者不得共享 World/Entity 引用。浏览器静态页 `modules/web/chat/` 只渲染已接受事件，不扩展 hello-wire-v1。
- **契约**：字段真值是架构仓 C-1 / C-2 JSON。测试定位 `origin/main` 文件，本仓不内嵌协议副本。

## 相关

- [`0005`](../../../decisions/0005-chat-event-netentityid-string-bridge.md)
- [`replica` 模块 README](../../../../modules/replica/README.md)

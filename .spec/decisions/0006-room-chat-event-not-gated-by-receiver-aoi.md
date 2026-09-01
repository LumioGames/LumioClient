# 0006 · Room 范围 chat.event 不以接收方 sender 投影 / AOI 为投递闸

- 日期:2026-09-01
- 状态:生效

## 背景

C-1 `chat.event` 的 `dimensions.visibility` 为 `room`，由服务端盖章后经 Delta 可靠有序广播到本 Room。C-2 的实体存在性、`InAoi` 与 tombstone 是 Attribute Query 结局，不是聊天投递闸。若 Stage 用接收方 ReplicaWorld 的 sender 记录拒帧，同一 Delta 会在两端产生不同 `(MessageId, roomSequence)` 流。

## 决策

已解码的 Room `chat.event` 只强制 C-1 接收方规则（payload hash、block kind、序号单调、text 上限）。接收方是否已 Admit 发送者、发送者是否 `InAoi` 或 tombstoned，只影响本连接 Query，不决定是否入窗。

## 后果

聊天窗可以显示尚未投影、当前 AOI 外或已 tombstone 的发送者 NetEntityId。两端可见集不一致时，独立 ReplicaWorld 仍对同一 Delta 得到相同的 `(MessageId, roomSequence)` 序列。

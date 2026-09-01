# 0005 · Chat event sender NetEntityId 以十进制字符串桥接到 C-2 不透明身份

- 日期:2026-09-01
- 状态:生效

## 背景

C-1 `gameplay-command-envelope-v1.json` 将 `chat.event.senderNetEntityId` 编码为 LumioBinV1 `u64`；C-2 `entity-binding-and-query-v1.json` 将 `netEntityId` 冻为不透明字符串。客户端 ReplicaWorld 必须同时消费两份契约，不能另写第三套身份。

## 决策

ReplicaWorld 与 Attribute Query 一律以 C-2 不透明字符串寻址实体。从 C-1 `chat.event` 解码出的 `u64` 发送者按不变文化十进制转成字符串（例：`101` → `"101"`）后再查找、呈现。不引入第二个身份名空间。

## 后果

数值型 wire 发送者与字符串绑定记录仅在十进制形式上对齐。C-2 夹具中的非数字 id（如 `N1`）不会作为 `chat.event` 发送者出现，直到架构仓统一编码。

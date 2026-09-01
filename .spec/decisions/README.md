# Decisions(决策记录 · ADR)

用 ADR(Architecture Decision Record)记录决策:为什么这样调度、为什么定这种结构、为什么划这条边界。**本目录是全仓决策记录的唯一落点**——功能内决策与框架级决策都记这里,feature 文档只描述设计现状,不留决策记录。

跨仓公共语义的决策只在 `LumioGameEngineArchitecture` 维护；本目录仅记录 Client 内部实现决策。

## 怎么写一条 ADR

- 一个决策 = 一个文件 `NNNN-<slug>.md`,编号从 `0001` 递增;写完在下方索引加一行。
- **一旦记录不改写**:被推翻就新增一条,把旧的状态标成「被 NNNN 取代」,历史留痕。
- 无 frontmatter。格式照抄:

      # NNNN · <一句话决策>

      - 日期:YYYY-MM-DD
      - 状态:生效 | 被 NNNN 取代

      ## 背景
      面对什么问题。

      ## 决策
      定了什么。

      ## 后果
      接受了什么代价。

## 索引

| 编号 | 决策 | 状态 |
|------|------|------|
| [`0001`](0001-capability-modules-and-session-orchestration.md) | 按能力模块组织客户端并由 session 统一编排 | 生效 |
| [`0002`](0002-cross-module-gates-and-state-ownership.md) | 冻结跨模块提交点、启动门与可变状态所有权 | 生效 |
| [`0003`](0003-a1-client-wss-access-landing-sites.md) | A1 客户端接入的三项落点:凭据随创建请求、Envelope 构造留在组装根、WSS 进既有 connection 工程 | 生效 |
| [`0004`](0004-architecture-source-readonly-mirror.md) | 架构源发布物以整目录只读镜像消费,硬校验与上游同步拆成两条独立检查 | 生效 |
| [`0005`](0005-chat-event-netentityid-string-bridge.md) | Chat event sender NetEntityId 以十进制字符串桥接到 C-2 不透明身份 | 生效 |
| [`0006`](0006-room-chat-event-not-gated-by-receiver-aoi.md) | Room 范围 chat.event 不以接收方 sender 投影 / AOI 为投递闸 | 生效 |

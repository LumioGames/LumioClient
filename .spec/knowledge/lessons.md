---
name: lessons
description: 经验教训——reviewer 反复退回的同类问题与 Agent 常犯坑;开工前与复盘沉淀时查
metadata:
  type: doc
  status: 已交付
---

# 经验教训（Lessons Learned）

复发问题的暂存区：记录 reviewer 反复退回的同类问题与 Agent 常犯的坑，让同一个坑不踩第三次。本文档是规范的**候选池**——条目在这里验证价值，稳定后升格，不在这里长住。

## 收录准入

- **同类问题第二次出现才收录**——单次偶发不收，防噪音。
- 来源：reviewer 退回报告、交回物的 known gaps、用户纠偏。
- 不收待办（走任务卡）；不收项目常识（进 `standards/` 或 feature 文档）。

## 条目格式

一条 lesson 一个小节，新条目加在「条目」节最上方（倒序）：

    ### <一句话规避规则>
    - 日期：YYYY-MM-DD
    - 现象：踩了什么坑、复发几次
    - 根因：为什么会发生
    - 规避：怎么做能不再犯（可验证的行为，不是口号）
    - 来源：reviewer 报告 / known gaps / 用户纠偏（附提交或任务标识）

## 升级路径

某条 lesson 被稳定复用（约第三次引用起）→ 升格为 `knowledge/standards/` 规则或 `rules/` 红线，原条目标注「已升格 → <落点>」，保留不删。

## 条目

### 审计记录（bytes/SHA-256/计数）必须有机器闸门校验，否则只会静默变成谎言

- 日期：2026-08-29
- 现象：`docs/LumioClient_five_requested_files/` 的 manifest 与 audit 记录三个规划文件的 bytes/SHA-256，但全仓没有任何检查引用这些值（`repository-policy.yml` 的 `sha256sum -c` 只覆盖 `docs/architecture/.baseline.sha256`）。T-00006 一次合法修订让记录失真，CI 全绿，直到 QA 逐字节重算才被发现；同族问题在架构仓已记为「Golden 会腐烂成谎言」「K[28] 无 KAT」。
- 根因：把「写进文档的哈希」当成守护。它只是**声称**；没有比对它的可执行检查，被记录对象一改就失真，而且失真方向永远是「记录说通过」。
- 规避：任何文档里的 bytes/SHA-256/计数，落笔时同轮补上重算并比对它的脚本，并接进 CI；脚本必须带反向证明（造一个字节差 → 红 → 还原 → 绿）。本仓落点：`eng/verify-five-file-package.mjs` + 其 `node --test` 自测，已接进 `repository-policy.yml`。
- 来源：R-00065 / R-00067 的 QA 核销与总调度裁决（提交见 PR「五文件包哈希重算 + 机器守护」）。

### 测试只许断言被测语义，不许断言宿主调度速度

- 日期：2026-08-29
- 现象：`BoundedEventDispatcherTests` 用 `Task.WhenAny(Task.Run(...), Task.Delay(250))` + `Assert.Same` 判「不阻塞」，另一条用 `Stopwatch.Elapsed < 1s`，`ObservabilityFaultTests` 用 2 s 等待墙——同一形态三处。宿主一忙就红：高负载对照跑 30 次挂 5 次。
- 根因：把「快」当成「对」。时间阈值断的是线程池排队延迟，不是被测语义；被测状态（如 capacity-1 管道的 QueueFull）在后台消费者在场时本身还不稳定，断言与后台抽取赛跑（TOCTOU）。
- 规避：等**状态量**不等墙钟（事件/条件 + 测试取消令牌，超时交给 runner）；先把后台消费者停泊到确定位置，让被测状态稳定下来再断言；加大超时或重试一律不算修复。真退化为阻塞时应表现为挂起被取消，而不是由快慢决定红绿。
- 来源：R-00031 的 QA 核销与 R-00287 交付方诊断（提交 `782ef050b2b6eb05ebfcdf476f5668b89b589ff0`）。

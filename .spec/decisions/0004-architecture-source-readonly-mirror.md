# 0004 · 架构源发布物以整目录只读镜像消费，硬校验与上游同步拆成两条独立检查

- 日期:2026-08-29
- 状态:生效

## 背景

[`0003`](0003-a1-client-wss-access-landing-sites.md) 的「后果」把 CC-3 挡在一句实测结论上:架构源 `packages/csharp/*` 是提交进仓的生成源码,**不上任何 feed**,消费模型只能是字节级只读镜像加 sha256 锁;而本仓无镜像目录、无 sync/verify 脚本、`tests/Fixtures/index.json` 的 `upstreamCorpusPin` 仍是 `unpublished`。R-00291 就是那张被点名的 vendor / mirror 卡。

落地前需要先裁三件事,否则镜像会长成一个「看起来有门、其实没门」的形态:

- 镜像收哪些文件——枚举清单会随上游 additive 增补腐烂,而 additive 增补正是被鼓励的。
- 校验靠什么——需要兄弟仓路径的检查在 CI 上没有兄弟仓,等于没有这道门,这正是卡面「不靠环境变量指向兄弟仓」要堵的洞。
- 镜像到位后 13 条设计别名怎么记——`0003` 当时的措辞预期镜像会带来「解冻」。

## 决策

1. **镜像范围是规则,不是文件清单,且零例外。** `schemas/`、`fixtures/`、`ids/`、`packages/` 四个上游目录整目录镜像进 `contract-mirror/upstream/`,不在任何地方记录文件数。所有断言一律「存在性 + 身份」——沿用需求室对 ErrorCode 43→53 的既有裁决(计数断言必然随上游 additive 增补腐烂)。
   曾考虑只收 `packages/csharp/` 与 `packages/canonical/`、排除 `packages/rust/`,理由是「C# 客户端引用不了 crate」。该理由**不成立**:镜像不是引用,镜像的是作为字节读取的契约真值。而且这种子选择会绕开 `packages/index.json`——记录每件产物 `compilerHash` / `outputHash` / `baselineId` 的登记表,正是应当据以实测取值的地方。一条零例外的规则既更短,也更难写错。

2. **pin 是 commit sha,不是分支名。** 落卡期间架构源 `origin/main` 实测前进两次(`e354611` → `11f6bfc` → `3287bba`),分支相对的 pin 不可复现。

3. **「产物未被手改」与「与上游同步」是两条独立检查,不合并。**
   - `eng/verify-contract-mirror.sh|ps1` —— **硬门禁**,只读本仓,不读架构源,因此在没有兄弟仓的 CI 上是一道真门;镜像被改、被删、被塞私货一律退出码 `33` 并点名路径。已接进 `.github/workflows/repository-policy.yml`。
   - `eng/sync-contract-mirror.sh|ps1 --check` —— **报告项**,需要架构源检出,恒退 `0`。它的 `--source` 是必填参数、**不从环境变量取**:一个变量没设就静默通过的检查不是检查。

4. **镜像到位不等于别名解冻。** 13 条 `GeneratedContract.*` / `RuntimeContract.*` 别名的类型名在镜像全文搜索**零命中**,状态由 `blocked-unpublished` 改为 `blocked-absent-from-published-surface`:前者说「本仓读不到已发布面」,后者说「读得到,而这些类型不在里面」。两者必须可区分,否则一次 vendor 会被误读成解冻事件。

5. **镜像只拷贝、不作为工程引用。** 上游 `.csproj` 随目录镜像进来但从不进 `LumioClient.slnx`;镜像目录不放任何 `Directory.Build.*`,由测试断言。

## 后果

- CC-3 的前置解除的是「拿不到产物」,**不是「别名有类型可映射」**。`0003` 后果段「解冻」一词就此收窄:T-00004 需要的是镜像里的 `schemas/` 与 `fixtures/` 契约真值(以及 `tools/lumio_contract.py` 的符号锚口径),不是这 13 条别名对应的 C# 类型——那些类型上游从未发布。
- 整目录规则的代价是镜像体量(当前 4 个目录、约 700 KB)大于按需枚举,换来的是新增一个下游消费点不必重开镜像卡,以及守护逻辑无需维护例外清单。
- `contract-mirror/.gitattributes` 把镜像标成 `-text`。仓库根是 `* text=auto`,会让 git 在检出时重规范化行尾并静默破坏与上游的字节相等;当前 pin 范围内零 CRLF,该标记现在不改变任何字节,存在意义是让未来带 CRLF 的上游文件也能原样往返。
- 硬门禁进的是 `repository-policy.yml` 的 node/shell 作业。该 workflow 至今没有任何 dotnet 步骤,镜像守护因此必须是纯 shell 才能真跑;补 dotnet job 是独立的 R-00287,本决策不代办。
- 镜像携带的 descriptor 全部是 `baselineId = LGE-V1.4-2026-08-27`,而 `eng/upstream-api-map.md` 仍声明 `architectureBaseline = LGE-V1.2-2026-08-27`、模块 README 也被 `repository-policy.yml` 钉在 `LGE-V1.2-2026-08-27`。该基线落差**先于本镜像存在**,本决策不顺手改:移动全仓声明基线是跨切面裁决,不是 vendor 的副作用。
- 退场条件:架构源发布出本仓可直接引用的包时,整个 `contract-mirror/` 随之删除。

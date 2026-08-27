# 0002 · 冻结跨模块提交点、启动门与可变状态所有权

- 日期:2026-08-27
- 状态:生效

## 背景

对提交 `9ca0065` 的模块架构审查(报告存档于 [`docs/LumioClient_Module_Architecture_Review_9ca0065.md`](../../docs/LumioClient_Module_Architecture_Review_9ca0065.md))核实了 5 个 P1:权威更新的原子提交所有者未闭合、Handshake 之后的 Gameplay Scope 激活门无所有者、Active 消息权限校验存在责任空洞、`ClientCommandSeq` 与 Config/Runtime Handle 等可变状态所有权有歧义、生成契约的工程层级未冻结。这些跨模块接缝不封口,实现者将被迫自行发明关键 API、状态所有权和失败顺序。ADR 0001 的 11 模块集合与依赖方向不受影响,本决策只在其上补齐接缝所有权。

## 决策

1. **权威更新单一事务边界。** 恢复最近 Confirmed PredictionFrame、应用 ECS/GAS/Voxel 权威结果、删除已确认命令、原序重放未确认命令、生成 Presentation Diff 必须经由单一 Runtime 客户端权威更新事务提交;FullSnapshot、Delta 与 Resync 使用同一事务边界。`replica` 只做校验与 Staged Authority Plan 构造,`prediction` 只做命令历史整理与事务提交后的 Confirmed Point 推进;两者都不独立提交核心状态。任一步失败不得推进 Baseline、Revision、Confirmed Point,不得发送 Ack。不允许 `replica` 先提交、`prediction` 后补偿。若 Runtime 尚未发布该事务 API,先回架构源补契约,本仓不得以多次独立提交拼装替代。
2. **Gameplay Scope 激活门。** `session` 在 Handshake Accepted 之后、进入 `Synchronizing` 之前,通过自己声明的平台无关激活端口完成精确 Release 的 Gameplay Scope 激活与生成 Contract/Mapping/Config 绑定。预编译 Gameplay Assembly 路径的默认实现直接返回已激活;HybridCLR 路径由 `LumioGame` Release Composition 用 `hybridclr-adapter` 的公开能力实现该端口并注入,不新增模块间源码依赖边。激活失败不得进入 `Synchronizing`;一个 Session 的 Gameplay Scope 固定,Session 内不得跨 Release 替换。Host/Composition 不得在 `session` 之外推进启动状态。
3. **消息校验所有权矩阵。** `connection` 唯一拥有帧/通道层校验(Endpoint 格式、TLS/IPC 对端身份、长度、完整性、分片、连接序号、连接级反重放);`handshake` 唯一拥有 Session 准入(Release/Schema/ABI/Protocol/Capability 与权限 Claims);`session` 唯一拥有 Active 消息门,调用生成的 Protocol/Permission Validator 校验 SessionId、GameReleaseId、MessageId、Role、Claims 与 Connection Generation;`replica`/`prediction` 只做各自的复制/预测语义校验。重连(新连接代次)必须重新完成通道认证与 Handshake Attempt;公共契约显式支持 Session Resume Token 前,不得复用旧连接的认证状态。Resync 在同一连接与准入内进行,不重新握手。
4. **可变状态所有权。** `input` 唯一拥有 `InputSampleSeq`、Sample Queue 与确定性映射输出顺序,输出未编号的候选命令;`prediction` 在 `session` 接纳命令进入预测/发送流程时唯一分配 `ClientCommandSeq` 与 `PredictionKey`,被拒绝或被丢弃的样本不消耗序号,`input` 只能消费 `prediction` 返回的不可变分配结果。`session` 唯一拥有 Session 级 Runtime Handle(ReplicaWorld/VoxelReplicaWorld)的创建与逆序销毁顺序,Runtime 拥有实际 Storage 与 Snapshot/Restore 机制。Config staging/activation 由 `session` 在 Tick 边界唯一请求,`persistence` 只提供已验证的 Config/Content Artifact,Runtime Config Port 负责 typed materialization 与 Tick Barrier 上的原子切换。`session -> persistence` 依赖窄化为已验证 Artifact/Checkpoint 读取端口;应用设置与 Content 下载缓存由 Host/Composition 在 Session 之外处理。完整校验矩阵与状态所有权表在设计文档维护。
5. **生成契约工程层级。** 契约依赖分三层:已发布 Host/Runtime Port(定义于本仓模块或已发布 Runtime Contract);工具链发布的纯生成 Contract Artifact(不依赖 LumioClient 与 LumioGame 的任何实现,双方均可引用);Game 专属 Mapper/Binding 实现(位于 LumioGame Release Artifact,实现本仓模块声明的端口,由 Release Composition 注入)。禁止 LumioClient 核心工程直接或传递引用 `LumioGame.ClientGameplay` 实现工程,也不得用反射或 Service Locator 隐藏该引用;首个工程提交起由 CI 校验。

## 后果

- `session` 的编排职责进一步集中(更新事务顺序、激活门、Active 消息门、Config 请求时机、Runtime Handle 生命周期),是 ADR 0001 集中状态机所有权的延续代价;它仍只组织顺序,不实现 Runtime 机制。
- 事务 API(D1)、生成 Validator(D2)、Contract Artifact 发布方(D5)依赖上游契约确认;确认前本仓只冻结内部角色约束,不发明公共语义。待确认清单在设计文档「待上游契约确认」一节维护,上游答案与本决策冲突时新增 ADR 取代对应条款。
- 设计文档与受影响模块 README 随本决策同步;此后改变这些接缝的所有权须新增 ADR,不改写本记录。
- C# 实现门禁的解除条件:本决策落文档、上游 D1/D2/D5 契约确认、依赖 DAG 映射为 CI 可校验的工程引用图,三者齐备。

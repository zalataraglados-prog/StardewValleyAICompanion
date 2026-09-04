# StardewAI 文档权威层级与维护状态（2026-09-05 r34）

本文解决仓库文档随着 EVD/rN 快速推进而产生的“历史段落仍存在，但容易被搜索引擎或 Agent 当作当前状态”问题。当前权威层是 **r29 有序高层队列边界 + issue #89 运行证据新鲜度治理 + r34 透明候选缓存与连续有界原生跨日训练事务**。

## 1. 当前权威状态

当前唯一顶层口径：

- 当前 release/run：`formal-r34-10b7722-20260905` / `train.server.20260905.r34.plan02`
- registered / semantic / compiler-bound：228 / 230 / 227
- Harness / Product Executor：145 / 145
- five-gate 历史动作口径 / training allowlist：151 / 62；其中 runtime evidence freshness 正在按 #89 逐域强绑定迁移，不能把历史 151 自动解释成全部已完成新鲜度强绑定
- pending：2（`tailoring.dye_item`、`minigame.play_junimo_kart`，均后置，不阻塞当前自主策略训练）
- policy trajectory：`policy_decision_trajectory.v2`
- feature schema：`policy_features.v2`
- compiler：`action_queue.v1`
- executor：`product_executor.v1`
- 当前策略边界：**一次模型排序选择一条有序高层候选队列；fresh snapshot 只在候选边界和 continuation 中做本地确定性校验/重编译，不自动重新调用模型**
- r29 决策边界实证：1 次策略排序 / 4 个高层候选 / 9 个机械动作 / 3 次候选边界刷新 / 5 次 continuation 刷新 / 后 8 次刷新均 `policy_model_invoked=false` / 4 条高层策略轨迹
- r34 连续事务实证：round10 将 Summer 4 提交到 Summer 5；`10b7722` 在不裁剪透明字段的前提下缓存同一快照的 route connector 候选；round01/02 随后连续提交到 Summer 6/7，原生保存、事务提升、失效队列重规划和控制面/训练面隔离均成立
- 当前 canonical：215 accepted / 0 rejected，157 / 5 / 53，4920 pairs；checkpoint / manifest SHA-256 为 `bc5369df5a47bfdf27d9a49b99cc4498b54a4cd4dc27bba1b02de907419c15a4` / `24b18a5bf0317e36f36398609b9e65c79a69f42bef73cee35b57191ae56ec653`
- 当前结论：正式 Product 训练、队列级决策边界、原生跨日事务、canonical 连续提交与透明候选性能优化均有真实服务器证据；跨季/跨年/Grandpa 21 长训尚未完成
- 当前运行边界：服务器正式训练进程已停止；磁盘只允许有退出条件的有界批次，不允许无界长训

## 2. 文档读取优先级

后续 ChatGPT / Codex / 人工审计按下列顺序读取：

1. `README.md` —— 对外当前状态、产品定位、r29 决策边界、evidence freshness 和 r34 连续事务；
2. `docs/CURRENT_WORK_CN.md` 最新日期章节 —— 当前 r34 状态与直接下一步；
3. `docs/SHORT_HANDOFF_20260901_FORMAL_TRAINING_EVD326_CN.md` —— 当前已经接续到 r34，文件名保留历史 EVD326 仅为路径兼容；
4. `docs/FORMAL_FULL_TRAINING_READINESS_CN.md` —— 当前训练准入、r34 原生存档/canonical 事务与 #89 新鲜度门；
5. `docs/FORMAL_TRAINING_BUG_FORECAST_20260901_CN.md` —— 主动静态审计、bug 预测与已闭合训练控制面问题；
6. `docs/FULL_SYSTEM_COMPLETION_PLAN_CN.md` —— 完全体工程路线；顶部最新章节优先，旧 EVD 章节仅作历史；
7. `docs/FUTURE_COMPANION_ARCHITECTURE_CN.md` —— 完全体产品不可返工约束；
8. `catalogs/vanilla-1.6.15/action-progress-dashboard.json`、`action-implementation-reconciliation.json` —— 机器生成式动作状态；
9. `src/StardewAI.Contracts/Capabilities/PendingSemanticActionCatalog.cs` —— pending 的机器权威来源；
10. 历史 EVD short handoff / audit —— 仅用于追溯当时证据，不能作为“当前下一步”。

## 3. r29 后必须统一的决策语义

任何当前文档、Agent context 或训练说明都必须遵守以下口径：

```text
fresh snapshot != 新策略决策
```

规范过程是：

```text
模型/策略排序器
→ 一次选出有序高层候选队列
→ SelectedQueueDecisionLease 持有排序、顺序与游标
→ 候选边界 fresh snapshot
→ 本地检查时间/能量/资源/身份/前后关系
→ C# 编译并执行该候选
→ 候选内部 continuation 继续本地 fresh + 确定性重编译
→ 候选完成后推进游标
→ 队列完成或失效才重新调用模型
```

不得再沿用 r24/r25 的旧解释，把“每次 fresh replan”“每个机械 continuation”写成一次新的策略选择。

## 4. 决策状态与执行状态

r29 后必须区分：

- `ranking/decision state`：模型真正排序队列时看到的状态；
- `execution state`：后续候选/continuation 在执行前重新读取的 fresh 状态。

保留队列中的第 2、3、4…候选可以在新的 execution state 上重新校验和重编译，但不得被记录成“模型在这个 fresh state 下又选择了一次”。后续 trajectory schema / trainer 若增加 queue provenance，必须保留原始排序身份、queue position 与执行态哈希之间的区别。

## 5. 运行证据新鲜度权威口径

issue #89 后，`runtime_verified` 不能再仅由历史 evidence id 推导。

首批 `native_object_execution.v2` 强绑定范围要求：

- runtime path revision 匹配；
- 32 个相关源文件规范化 SHA-256 匹配；
- artifact / source / build identity 完整；
- RuntimeTestHarness / Contracts / TransparentBridge 三份 DLL SHA-256 匹配。

当前只对 #88 实际重跑的六个动作完成强绑定；其他历史动作暂为 `LegacyUnbound`。因此历史 five-gate 数仍可用于动作工程历程，但不能被外部 Agent 误读成“当前全部运行证据均已完成新鲜度迁移”。

## 6. 历史段落解释规则

仓库保留历史检查点是为了审计，不会批量删除。例如文档中仍可能出现：

- `Product Executor = 0`
- `6/9/16/... catalogued blocked`
- `policy_decision_trajectory.v1`
- “正式训练尚未开始”
- “一个 iteration 只能有一个高层目标”
- “每次 fresh replan 都形成策略轨迹”
- “下一动作是 movie/story/tailoring”等旧下一步

这些文字如果位于明确日期/EVD/rN 历史章节中，只能解释为**当时事实或当时设计**。其中 r24/r25 关于决策边界的解释已经被 r29 明确 supersede。

任何工具、搜索引擎或 Agent 若回答“现在是什么状态”，必须先读取 README、CURRENT_WORK 顶部、当前短交接和机器目录，不能从全文旧命中反推当前状态。

## 7. 本轮已维护文档

### `README.md`

已从 r25 更新到 r29 / #89：

- 增加有序高层候选队列决策边界；
- 明确 fresh snapshot 不自动触发模型；
- 增加 1→4→9 的 r29 服务器证据；
- 增加 decision state / execution state 分离；
- 增加 runtime evidence freshness 与 LegacyUnbound 边界。

### `docs/CURRENT_WORK_CN.md`

由 r29 提交与 #89 提交维护：顶部现以 #89 为最新静态治理状态，下接 r29 训练运行实证；r24/r25 保留为历史。

### `docs/SHORT_HANDOFF_20260901_FORMAL_TRAINING_EVD326_CN.md`

内容已更新为 r29 队列级正式闭环短交接。文件名不再表示当前 EVD 编号，仅为历史路径；读取标题和正文，而不是从文件名推断当前阶段。

### `docs/FORMAL_FULL_TRAINING_READINESS_CN.md`

已加入 r29 队列级决策边界及 #89 runtime evidence 强绑定准入规则。

### `docs/FORMAL_TRAINING_BUG_FORECAST_20260901_CN.md`

继续作为训练风险主表；需要把 r29 的“fresh snapshot 误接策略排序入口”登记为已闭合控制面缺陷，而不是继续当潜在风险。

### `docs/FUTURE_COMPANION_ARCHITECTURE_CN.md`

完全体终点仍保持：共同在场 + 连续记忆 + 低审判反馈。r29 只是策略/执行边界优化，不改变 body / memory / persona / player language / multiplayer 等产品后续路线。

## 8. 仍保留为历史日志的大文件

`CURRENT_WORK_CN.md`、`FORMAL_FULL_TRAINING_READINESS_CN.md`、`FULL_SYSTEM_COMPLETION_PLAN_CN.md` 都包含大量已经完成切片的原始审计记录。**不应为了“看起来干净”删除这些证据历史。**

维护策略是：

- 新状态只在顶部/当前短交接/README 给唯一解释；
- 历史章节继续保留原数字、原措辞和当时下一步，保持审计可追溯；
- 对已经被新架构明确 supersede 的全局断言，在顶部写明确替代关系；
- 不批量把历史 `Product Executor=0` 改成 145，否则会篡改当时事实。

## 9. 当前已知文档债

### DOC-001：历史 completion plan 中仍存在 `policy_decision_trajectory.v1`

部分 2026-08-31 历史段落使用 `.v1`。当前 schema 已明确为 `.v2`。如果段落是在描述当时真实产物则保留；如果仍被写成当前/未来条件，应在对应顶部新章节覆盖。

### DOC-002：大型历史日志中的“下一步”容易被全文搜索误读

继续通过本文权威层级和 README 缓解。Agent-facing context 应只截取大型文档顶部 current checkpoint，不要把数百个旧“下一步”一起塞给模型。

### DOC-003：EVD 编号与 rN 运行编号不是同一时间轴

EVD 表示证据/工程切片，rN 表示正式训练运行迭代。文件名中的旧 EVD 也不等于当前状态编号。

### DOC-004：历史 r24/r25 决策单位已经被 r29 替代

旧“一个外层 iteration = 一个高层目标”与“fresh replan 产生新策略轨迹”只保留为 bug 演进史，当前训练实现不得据此回退。

## 10. 当前维护原则

1. 当前状态必须由机器目录 + 当前短交接 + README 三方一致；
2. 历史证据不删除、不改写成今天的数字；
3. 预测与静态审计不得冒充运行实证；
4. `runtime_verified` 必须满足当前 evidence freshness 规则；
5. 正式训练只接受 Product Executor 轨迹，Harness 继续 calibration/evidence-only；
6. fresh snapshot 与 policy invocation 严格解耦；
7. decision state 与 execution state 严格分离；
8. “模型会做什么”与“产品最终能接受玩家哪些命令”保持两条独立完成线；
9. stable 1.0 必须回到 Companion 产品目标，不能以动作数、模型调用减少或 checkpoint 数替代完成定义。

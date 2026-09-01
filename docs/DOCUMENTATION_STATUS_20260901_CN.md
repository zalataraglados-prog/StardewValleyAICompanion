# StardewAI 文档权威层级与维护状态（2026-09-01 r25）

本文解决仓库文档随着 EVD/r8-r25 快速推进而产生的“历史段落仍存在，但容易被搜索引擎或 Agent 当作当前状态”问题。

## 1. 当前权威状态

当前唯一顶层口径：

- release/run：`formal-r25-20260901` / `train.server.20260901.r25`
- registered / semantic / compiler-bound：228 / 230 / 227
- Harness / Product Executor：145 / 145
- five-gate / training allowlist：151 / 62
- pending：2（`tailoring.dye_item`、`minigame.play_junimo_kart`，均后置，不阻塞当前自主策略训练）
- policy trajectory：`policy_decision_trajectory.v2`
- feature schema：`policy_features.v2`
- compiler：`action_queue.v1`
- executor：`product_executor.v1`
- dataset：accepted 186 / rejected 0 / train 128 / validation 5 / test 53
- train pairs：3415
- checkpoint：`structured-policy-52c9f785cc6dcc46c02f94e7`
- 当前结论：正式 Product 训练闭环已有真实数据与 checkpoint 更新；连续多日/跨季/跨年/Grandpa 21 长训尚未完成。

## 2. 文档读取优先级

后续 ChatGPT / Codex / 人工审计按下列顺序读取：

1. `README.md` —— 对外当前状态、产品定位、架构边界；
2. `docs/SHORT_HANDOFF_20260901_FORMAL_TRAINING_EVD326_CN.md` —— 当前 r25 技术接续；
3. `docs/FORMAL_TRAINING_BUG_FORECAST_20260901_CN.md` —— 主动静态审计、bug 预测与下一批风险；
4. `docs/FORMAL_FULL_TRAINING_READINESS_CN.md` —— 训练准入和 r20-r25 运行证据历史；
5. `docs/CURRENT_WORK_CN.md` —— 大型时序工作日志；顶部最新节优先，下方全部是历史检查点；
6. `docs/FULL_SYSTEM_COMPLETION_PLAN_CN.md` —— 完全体工程路线与历史切片；读取时必须以本文/r25 状态覆盖旧数字；
7. `docs/FUTURE_COMPANION_ARCHITECTURE_CN.md` —— 完全体产品不可返工约束；
8. `catalogs/vanilla-1.6.15/action-progress-dashboard.json`、`action-implementation-reconciliation.json` —— 机器生成式动作状态；
9. `src/StardewAI.Contracts/Capabilities/PendingSemanticActionCatalog.cs` —— pending 的机器权威来源；
10. 历史 EVD short handoff / audit —— 仅用于追溯当时证据，不能作为“当前下一步”。

## 3. 历史段落解释规则

仓库保留历史检查点是为了审计，不会批量删除。例如文档中仍可能出现：

- `Product Executor = 0`
- `6/9/16/... catalogued blocked`
- `policy_decision_trajectory.v1`
- “正式训练尚未开始”
- “下一动作是 movie/story/tailoring”等旧下一步

这些文字如果位于明确日期/EVD/rN 历史章节中，只能解释为**当时事实**。

任何工具、搜索引擎或 Agent 若要回答“现在是什么状态”，必须先读取文档顶部最新 r25 段、README 和机器目录，不得从全文命中的旧段落反推出当前状态。

## 4. 本轮已维护文档

### `README.md`

已从 EVD-320 旧状态更新为 r25：

- 不再声称 Product Executor 未完成；
- 不再声称剩余 6 pending；
- 加入当前真实 Product training loop、dataset/checkpoint、磁盘长训门；
- 补入 Companion 最终产品定义和 PlayerCommandOnly vs autonomous policy 分层；
- 链接主动 bug 审计。

### `docs/SHORT_HANDOFF_20260901_FORMAL_TRAINING_EVD326_CN.md`

已补：

- r25 当前权威数字；
- pending 两项后置解释；
- r20-r25 已知训练 bug 类别；
- 主动静态风险；
- 60-120 meaningful bugs 的后续容量预测；
- 扩盘后必须验证真实 mount 容量的准入门。

### `docs/FUTURE_COMPANION_ARCHITECTURE_CN.md`

已补：

- 完全体产品终点“共同在场 + 连续记忆 + 低审判反馈”；
- 策略、执行技能、人格、记忆的分层；
- PlayerCommandOnly / delegated Minigame Skill 后置边界；
- 当前 r25 训练阶段不能替代 body/memory/persona/interaction；
- stable 1.0 的产品完成定义。

### `docs/FORMAL_TRAINING_BUG_FORECAST_20260901_CN.md`

本轮新增。包含：

- 训练 bug 数量预测；
- 7 个主动源码审计风险 FTB-001..007；
- P1/P2 优先级；
- bug 数据污染/重训记账规则。

## 5. 仍保留为历史日志的大文件

`CURRENT_WORK_CN.md`、`FORMAL_FULL_TRAINING_READINESS_CN.md`、`FULL_SYSTEM_COMPLETION_PLAN_CN.md` 都包含大量已经完成切片的原始审计记录。**不应为了“看起来干净”删除这些证据历史。**

维护策略是：

- 新状态只在顶部/当前短交接/README 给唯一解释；
- 历史章节继续保留原数字、原措辞和当时下一步，保持审计可追溯；
- 发现历史章节内的无日期全局断言、错误 schema 或会被当前工具直接消费的旧值时才原地修正；
- 不批量把历史 `Product Executor=0` 改成 145，否则会篡改当时事实。

## 6. 当前已知文档债

### DOC-001：历史 completion plan 中仍存在 `policy_decision_trajectory.v1`

部分 2026-08-31 历史段落使用 `.v1`。当前 schema 已明确为 `.v2`。如果段落是在描述当时尚未运行的“未来启动条件”，应逐步改为 `.v2`；如果是在记录当时真实产物则保留原值并标历史。

### DOC-002：大型历史日志中的“下一步”容易被全文搜索误读

已通过本权威层级文件和 README 缓解。后续生成 Agent-facing context 时应只截取每个大型文档的最新 current checkpoint，不要把数百个旧“下一步”一起塞给模型。

### DOC-003：EVD 编号与 rN 运行编号不是同一时间轴

EVD 表示证据/工程切片，rN 表示正式训练运行迭代。文档必须同时写 release/run 和 EVD 时明确两者，不得用“r25=EVD-325”一类错误等价。

## 7. 当前维护原则

1. 当前状态必须由机器目录 + 最新短交接 + README 三方一致；
2. 历史证据不删除、不改写成今天的数字；
3. 预测与静态审计不得冒充运行实证；
4. `runtime_verified` 只给 E3+；
5. 正式训练只接受 Product Executor 轨迹，Harness 继续 calibration/evidence-only；
6. “模型会做什么”与“产品最终能接受玩家哪些命令”保持两条独立完成线；
7. 文档更新不能把 Product Executor 误写成第一轮离线训练的历史前置条件；
8. stable 1.0 必须回到 Companion 产品目标，不能以动作数或 checkpoint 数替代完成定义。

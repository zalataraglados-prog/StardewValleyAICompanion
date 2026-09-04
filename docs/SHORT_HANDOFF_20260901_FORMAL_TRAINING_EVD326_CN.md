# StardewAI r34 有界正式训练短交接

## 当前结论

队列级决策边界已经修正，并已在 119 服务器连续跨日提交到 Summer 6。现在不是“每个机械动作调用一次模型”，而是模型一次选择一条有序高层候选队列，C# 编译器和执行器逐候选完成全部机械动作。r34 又消除了同一快照重复构建跨图候选的主要性能浪费；跨季、跨年与 Grandpa 21 全量长训尚未完成。

## 每次模型决策

1. 透明桥生成 fresh 游戏快照、状态哈希和当前可用信息。
2. 上游按时间、资源、安全、目标和准入证据排除不可能候选；策略模型只对剩余高层候选排序，并一次选择有序队列。
3. `SelectedQueueDecisionLease` 持有原始排序、编译队列、候选顺序和当前游标，不因机械 continuation 或 fresh snapshot 被替换。
4. 每到一个高层候选边界，运行时从 fresh snapshot 重新物化同一候选，检查队列先后关系、累计时间/能量和身份一致性，再由 DailyPlanCompiler 确定性编译。
5. 候选内部的寻路、移动、交互、等待、菜单和其他 continuation 只进行本地确定性重编译，`policy_model_invoked=false`。
6. 一个高层候选只有在 Product Executor 回执 `applied + verified + fresh` 且完成条件成立后，才写入一条 `policy_decision_trajectory.v2`。机械原语不单独写策略轨迹。
7. 队列全部完成或任一候选使队列失效后，才允许重新调用模型。失败候选不计完成；已完成候选的有效轨迹可保留。

## r29 实证

- 发布/运行：`formal-r29-20260901` / `train.server.20260901.r29`。
- 隔离槽位：`StardewAIDebug_16564609768130219756`。
- 一次策略排序选择 4 个高层候选：处理邮件和 3 个装箱候选。
- 共执行 9 个机械动作，全部 `applied/verified/fresh`。
- 发生 3 次候选边界刷新、5 次候选内部 continuation 刷新；这些刷新都没有调用策略模型。
- 形成 4 条高层候选策略轨迹，`policy_trajectory_skips=0`，最终 `selected_queue_decision_complete=true`。
- checkpoint：`structured-policy-184b08e75e9672a4cd8a74b1`；本轮报告追加 203 行。
- r27/r28 只用于发现并修正站位身份和 continuation 跨候选问题，不算成功训练证据。

## 迁移与服务器状态

修正前训练根已迁移至 `I:\StardewAITrainingArchive\119.91.139.160\formal-training-r18-pre-queue-boundary-fix-20260901`。远端 910 个文件、本地 910 个文件，SHA-256 验证 910/910；远端源目录保留。2026-09-02 已部署 `formal-r30-20260902`，停止状态的 Compose 容器已重建并绑定 r30；游戏、Backend、Product Executor 和 LiveTrainingLoop 均未运行。

## 实机暴露边界与长训前静态审计

r20-r29 已把跨图后旧队列、暂不可输入菜单、continuation 越候选、候选来源缺失、制品无限增长、站位身份混入高层语义，以及 fresh recompile 误触发策略模型等问题前移为显式门禁。它们证明 rollout 能承担压力测试，但不表示跨日、跨季、跨年和 Companion 产品层不再有未知缺陷。

完整静态条目以 `docs/FORMAL_TRAINING_BUG_FORECAST_20260901_CN.md` 为准，当前重点仍是：训练根迁移后空间门必须检查真实挂载盘；负长期回报不能被最低正权重掩盖；特征缺失不能伪装成真实零值；JSONL 崩溃尾行必须可恢复；全量重建成本需要跨季前 profiling；爷爷 21 分 terminal 的重评语义必须冻结；新增迭代制品必须自动纳入滚动保留。未经过运行复现的条目只能标记为 risk/static finding，不能冒充动态证据。

从当前状态到 stable 1.0 的容量估计仍采用远端审计口径：连续 3-7 个游戏日约暴露 8-18 项，跨季再增加 10-25 项，跨年与爷爷 21 分再增加 12-30 项，Companion 长期记忆/语言/多人层约 20-50 项，RC 到 stable 约 15-35 项。区间存在重叠，中位总量约 60-120 个有意义缺陷；该数字用于容量规划，不是已确认 bug 数。

## 下一步与退出条件

先在扩容或迁移正式训练根后做一次有界多外层迭代恢复验证，确认队列 lease 能跨外层循环续跑且每条队列只产生一次模型排序；随后启动可恢复的多日正式批次。每批必须同时满足：计划游戏日或 attempt 上限到达；无孤立 Product pending；所有入库高层候选有 verified/fresh 回执；dataset/checkpoint/manifest 哈希一致；策略调用计数不超过队列决策次数；UPS 和剩余磁盘不越线。达到跨季、跨年、第三年爷爷 21 分独立长跑前，不得宣称全量训练完成。

## 文档口径

当前代码与新文档统一使用 `policy_decision_trajectory.v2 / policy_features.v2 / action_queue.v1 / product_executor.v1`。r25 是四轮制品保留和首个非空 validation 的历史证据；r29 是当前队列级决策边界权威证据；r30 是停止状态的最新部署。旧 r8-r28 数字只能解释各自历史检查点，不得覆盖顶层 README、当前工作、训练 readiness、完全体路线和本短交接的最新结论。

## 2026-09-05 接续更新：r32 round07

r32 已越过原生存档事务硬阻塞。部署时 `e0a6221` 要求真实 `newDaySync.hasSaved` 后才允许结束睡眠，`af432ed` 修正 stale `lastQuestionKey=Sleep` 导致隔夜系统对话被误判为睡眠确认框，并通过原生输入推进 Summer 2 地震对话；远端整合后的公开 `main` 等价提交分别为 `bcc0bc0`、`d881555`。最终回归为 Core `2273/2273`、Backend `171/171`、Release 0 warnings / 0 errors。

119 有界运行 `train.server.20260905.r32.plan07` 用 3 次迭代完成 1 个 verified 主动作和 2 次存档边界处理，Summer 2 → Summer 3；存档树 SHA-256 实际变化，事务状态为 `committed_after_native_save_boundary`，canonical checkpoint 更新为 `4f937ec73f2a0f58bdac00ff9345fd4fbcc201010d627b53939a132357a2181f`。正式数据集为 200 accepted / 0 rejected，142 / 5 / 53 split，4367 train pairs。

本机归档位于 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r32-round07-20260905-011000`；远端与本机均为 146 个文件，缺失、额外和 SHA-256 不一致均为 0。正式训练、游戏与执行器进程已停止；既有观察隧道仍独立运行。round05/round06 是未提交的失败诊断轮次，不得计入训练成果。

直接下一步：以 Summer 3 存档和 r32 round07 canonical 哈希生成下一份有界计划，继续真实 Product rollout。不得回到动作逐条调用模型；不得跳过原生存档边界；不得在未解决 Product pending、事务未提交或归档哈希未核对时推进 canonical。当前是“第一轮完整事务成功”，不是“全量训练完成”。

## 2026-09-05 接续更新：r33 round09

round08 在 prepare 阶段发现 round07 的 canonical checkpoint 仍绑定 staging manifest 路径，因 `formal_checkpoint_dataset_binding_mismatch` 失败关闭；没有执行动作或推进 canonical。`7b1da8d` 已把 manifest 全部 digest 路径、manifest hash、checkpoint ID/绑定和最终报告身份的 canonical 重绑定纳入正式事务提交，并增加“提升后可直接进入下一轮”的回归。

同版本工具对历史 canonical 做一次性重建后，200 条样本、0 rejection、142/5/53 与 4367 pairs 均保持不变。round09 随即通过 prepare，完成邮件与两个装箱高层轨迹；睡眠控制步没有调用模型。存档 Summer 3 → Summer 4，事务再次提交，canonical 变为 203 accepted、145/5/53、4547 pairs；checkpoint / manifest SHA-256 为 `4247b9feed96fbb40fbe263dd6f260c006d8f2db96c28b4a21bc0b5ffc717eeb` / `b35080e21a10ae61109df1577a4e6534fa2e60150917e3d75671d8d8734c45d5`。

round09 完整制品已归档到 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r33-round09-20260905-015829`，远端/本机 142 / 142 且三类差异均为 0；正式训练已停止。当前直接下一步是从 Summer 4 做连续有界批次，保持同一队列决策语义和每轮退出门。连续多日、跨季、跨年、Grandpa 21 与 Companion 产品层仍未完成。

## 2026-09-05 接续更新：r34 round01

r33 round10 已将存档从 Summer 4 提交到 Summer 5。`10b7722` 随后缓存同一快照的完整 route connector 候选，并统一默认自主候选入口；没有删减透明字段，也没有把 `exploration.visit_location` 等显式目标错误纳入空请求。`recovery.stabilize_day` 继续只作为控制面边界。

r34 round01 从 Summer 5 推进到 Summer 6，完成 2 个主决策、2 个原生存档边界和 4 条 applied Product 策略轨迹。当前 canonical 为 211 accepted / 0 rejected、153/5/53、4783 pairs；checkpoint / manifest SHA-256 为 `b33d54f66fdcbe304e5207043751a0a153dcd45d24064b299903b978d28bd010` / `103770377bdca5b4979bd196ae4756c93e080f8924bc1de136a9bd35cc07c738`。室外首次排序降至 43.329 秒，随后同快照边界为 145.6/9.7 毫秒。

归档位于 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r34-round01-20260905-041036`，远端/本机 189 / 189，缺失、额外和摘要不一致均为 0。正式容器已正常停止。直接下一步是从 Summer 6 和当前 canonical 创建 r34 round02；每轮仍必须有退出条件、原生保存、事务提交、0 unresolved pending 与本机哈希归档。

## 2026-09-05 接续更新：r34 round02

round02 已从 Summer 6 连续提交到 Summer 7。2 个主决策产生完整处理两封邮件、照料宠物、收取树产品 4 条 applied 策略轨迹；邮件 route/open 只是进入阶段身份，同一候选的机械 continuation 完整执行并聚合为一个 outcome。fresh 后失效的社交尾项只记 skip 并触发重新规划，控制面睡眠仍没有训练轨迹。canonical 为 215 accepted / 0 rejected、157/5/53、4920 pairs；checkpoint / manifest SHA-256 为 `bc5369df5a47bfdf27d9a49b99cc4498b54a4cd4dc27bba1b02de907419c15a4` / `24b18a5bf0317e36f36398609b9e65c79a69f42bef73cee35b57191ae56ec653`。

归档位于 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r34-round02-20260905-043913`，远端/本机 170 / 170 且三类差异为 0。正式容器已正常停止，只保留观察隧道。当前唯一直接下一步是从 Summer 7 和上述 canonical 创建下一份有界 Product rollout；不得跳回旧存档或旧 checkpoint。

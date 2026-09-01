# StardewAI r29 有序高层队列正式闭环短交接

## 当前结论

队列级决策边界已经修正并在 119 服务器通过有界正式证明。现在不是“每个机械动作调用一次模型”，而是模型一次选择一条有序高层候选队列，C# 编译器和执行器逐候选完成全部机械动作。多日全量长训尚未开始。

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

修正前训练根已迁移至 `I:\StardewAITrainingArchive\119.91.139.160\formal-training-r18-pre-queue-boundary-fix-20260901`。远端 910 个文件、本地 910 个文件，SHA-256 验证 910/910；远端源目录保留。服务器当前保留单个 SMAPI 游戏进程及 r29 Backend/Product 服务，没有运行 LiveTrainingLoop 长训。

## 下一步与退出条件

先在扩容或迁移正式训练根后做一次有界多外层迭代恢复验证，确认队列 lease 能跨外层循环续跑且每条队列只产生一次模型排序；随后启动可恢复的多日正式批次。每批必须同时满足：计划游戏日或 attempt 上限到达；无孤立 Product pending；所有入库高层候选有 verified/fresh 回执；dataset/checkpoint/manifest 哈希一致；策略调用计数不超过队列决策次数；UPS 和剩余磁盘不越线。达到跨季、跨年、第三年爷爷 21 分独立长跑前，不得宣称全量训练完成。

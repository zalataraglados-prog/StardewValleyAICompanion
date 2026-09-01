# StardewAI r25 正式训练短交接

## 当前结论

正式 Product 训练闭环已在 119 服务器产生真实数据和新 checkpoint；这不是 bootstrap，也不是仅有进程。
但只完成了一个有界高层目标，尚未开始连续多日的全量长训，更不能称为完整陪玩模型训练完成。

当前自主训练所需语义分母已经冻结。剩余两个 `catalogued_blocked` 是：

- `tailoring.dye_item`：后置 `PlayerCommandOnly` 外观能力；
- `minigame.play_junimo_kart`：后置玩家委托的真实原生完美代打能力。

两者都不得重新插回首轮自主策略训练的关键路径。AI actor 的 Junimo Kart 自主游玩继续遵守既定 `timed_equivalent` 边界。

## 每轮训练

1. 透明桥生成 fresh 全量游戏快照、状态哈希和当前可用信息。
2. 上游按时间、资源、安全、目标和准入证据排除不可能候选；C#
   `return_weighted_pairwise_linear_ranker.v1` 只排序“现在做什么”。
3. DailyPlanCompiler 将被选中的高层目标展开为动作队列；坐标、路线、菜单、工具、按键和安全时序属于
   编译器/执行器，不交给小模型猜测。
4. Product Executor 通过现有原生执行端执行，每个需要重规划的动作后读取 fresh snapshot，并核对
   `applied + verified + fresh`。失败、状态漂移或回执不确定时失败关闭并重规划。
5. 只有有候选来源、准入且验证成功的显式决策进入 `policy_decision_trajectory.v2`；随后确定性重建数据集、
   训练结构化 checkpoint，并原子更新 manifest/hash。
6. 一个外层 iteration 对应一个高层目标 episode，可包含多个机械动作和多次 fresh replan；目标完成立即结束
   iteration，不能顺带执行下一个全局目标。

## r25 实证

- 发布/运行：`formal-r25-20260901` / `train.server.20260901.r25`。
- 隔离槽位：`StardewAIDebug_16564609768130219756`。
- 单目标：处理 `landslideDone` 邮件。
- 原生动作：跨图、走到信箱、交互、等待 LetterViewer 可输入、关闭信件，共 `5/5` applied/verified/fresh。
- 策略轨迹：5 条成功，五个候选 ID 均存在，`effective_candidate_id_missing=0`。
- manifest：`max_attempts=1 / max_persisted_iterations=4 / min_free_space_mb=4800`。
- 数据集：accepted 186、rejected 0、train/validation/test 128/5/53、train pairs 3415。
- checkpoint：`structured-policy-52c9f785cc6dcc46c02f94e7`，SHA-256
  `b97bbdc1b64ba77b38097fc691581d4397c32807246f312b00dd883249e23b67`。
- 性能：训练后约 54.124 UPS；12 次 full snapshot 平均 1406.835 ms。
- 进程：游戏、Backend、Product Executor 运行；LiveTrainingLoop 已正常退出，当前没有长训。

## 已经由实机训练暴露的边界问题

r20-r25 证明正式 rollout 正在承担全系统压力测试职责，至少已经把以下类别前移为显式修复/门禁：

- 跨地图后旧动作队列不能继续执行，必须 fresh replan；
- 暂时不可输入的 `LetterViewerMenu` 需要绑定身份的有界等待；
- 高层 continuation 完成后必须结束当前 episode，不能顺带执行第二个全局目标；
- continuation 策略轨迹必须保留有效 `candidate_id` provenance；
- 正式运行制品必须有界保留，不能让诊断快照无限吃满训练盘。

这些不表示当前只剩这五类 bug；跨日、跨季、跨年和 Companion 产品层会进入新的状态空间。

## 主动静态审计：长训前的新风险

完整条目见 `docs/FORMAL_TRAINING_BUG_FORECAST_20260901_CN.md`。当前优先关注：

1. **训练盘挂载后的空间检查**：当前 Linux 容量门从 `Path.GetPathRoot(SnapshotDir)` 得到 `/`；若 `/state` 迁移到独立挂载盘，可能检查错文件系统。扩盘/挂载后必须实测。
2. **负长期回报的训练语义**：当前 pairwise trainer 始终把“已选候选”作为正方向；负 return 只会被截成最低权重，不会反向监督。必须在把 checkpoint 称为 return-aware policy 前冻结 signed-return 语义。
3. **训练特征缺失值**：LiveTrainingLoop 当前有若干 completeness 特征被硬编码为 true/1/false，数值读取失败又会回退到 0。要防止“缺失”被误标成真实 0。
4. **horizon JSONL 崩溃尾行**：horizon writer 使用普通 append；若进程在一行中途退出，dataset builder 当前会因损坏 JSON 直接失败。多日无人值守前需要恢复测试。
5. **全量重建伸缩性**：每轮结构化训练会重新读取历史轨迹/horizon、重写全部数据分区并从头训练；长数据集下累计成本近似 O(N²)，需要在跨季前设 profiling 门。
6. **Grandpa terminal 唯一性**：当前同一 save 只允许一个 `grandpa_21` terminal observation。进入 Year 3 前必须明确是否允许重评以及相应数据合同。
7. **rolling artifact family 漏登记**：保留策略依赖硬编码文件名族；以后新增 per-iteration artifact 时必须有自动覆盖测试，避免静默泄漏。

以上静态审计不能冒充 E3。没有运行复现的条目必须继续标为 risk / static finding。

## 缺陷数量预测

从 r25 到 stable 1.0，按“值得单独修复、回归或记录的工程问题”估计：

- 连续 3-7 个游戏日：8-18；
- 跨季：再增加 10-25；
- 跨年 / Year 3 Grandpa 21：再增加 12-30；
- 独立 AI body、长期记忆、语言交互、多人 Companion 层：20-50；
- RC 到 stable：15-35。

区间互有重叠，不能简单机械相加；**中位总预期约 60-120 个有意义缺陷**。若把轻微性能、断言、文档镜像、发布脚本和环境兼容也计入，修复条目可能达到 100-220。该数字用于容量规划，不是当前已发现 bug 数。

## 下一步与退出条件

正式 run 已将诊断快照限制为最近四个迭代家族，但服务器仍仅约 4999 MiB 可用、使用率 92%，且没有
第二块数据盘。先扩容或迁移正式训练根并验证数据、manifest、checkpoint 全部哈希，再启动有界多日批次。

扩容/迁移时必须额外验证“空间门实际读取的是训练数据所在文件系统”，不能只验证根文件系统 `/`。

每批退出条件为：完成计划游戏日数或达到显式 attempt 上限；所有入库动作有 Product verified/fresh 回执；无孤立 pending；dataset/checkpoint/manifest 哈希一致；游戏 UPS 和剩余空间不越线；停止后可由 ready/recovery probe 恢复。

达到跨季、跨年、第三年爷爷 21 分独立长跑与完整动作覆盖前，禁止写“全量训练完成”。当前线性 ranker 不需要 RTX 5070 8GB；GPU 只在长期轨迹足够后评估神经模型或 QLoRA。

## 文档口径

当前所有新文档统一使用：

- `policy_decision_trajectory.v2`
- `policy_features.v2`
- `action_queue.v1`
- `product_executor.v1`
- r25 / accepted 186 / 128-5-53 / 3415 pairs

历史章节中出现的旧 r8-r24、`.v1`、`Product Executor = 0` 等数字只代表当时检查点，不能再被解释为当前状态。顶层 README、当前工作、训练 readiness、完全体路线和本短交接必须以 r25 为当前入口。

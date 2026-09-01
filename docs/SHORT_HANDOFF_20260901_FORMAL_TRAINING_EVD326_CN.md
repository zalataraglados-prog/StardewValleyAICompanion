# StardewAI r25 正式训练短交接

## 当前结论

正式 Product 训练闭环已在 119 服务器产生真实数据和新 checkpoint；这不是 bootstrap，也不是仅有进程。
但只完成了一个有界高层目标，尚未开始连续多日的全量长训，更不能称为完整陪玩模型训练完成。

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

## 下一步与退出条件

正式 run 已将诊断快照限制为最近四个迭代家族，但服务器仍仅约 4999 MiB 可用、使用率 92%，且没有
第二块数据盘。先扩容或迁移正式训练根并验证数据、manifest、checkpoint 全部
哈希，再启动有界多日批次。每批退出条件为：完成计划游戏日数或达到显式 attempt 上限；所有入库动作有
Product verified/fresh 回执；无孤立 pending；dataset/checkpoint/manifest 哈希一致；游戏 UPS 和剩余空间不越线；
停止后可由 ready/recovery probe 恢复。达到跨季、跨年、第三年爷爷 21 分独立长跑与完整动作覆盖前，禁止写
“全量训练完成”。当前线性 ranker 不需要 RTX 5070 8GB；GPU 只在长期轨迹足够后评估神经模型或 QLoRA。

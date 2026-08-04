# 受支持机器容量生命周期短交接

状态：2026-08-04，EVD-215 已闭合并按有界范围进入训练准入。

- 新增高层语义 `farm.establish_supported_machine_capacity`，仅服务 `goal.economy.earn_money`。
- 无活动意图时，只选择一个当前有界正净收益且证据完整的机器制作候选。
- `craft_selected` 只继续同一意图的既有机器摆放链。
- `placement_bound` 优先继续同一精确机器的既有首次投料链；若摆放尚未落地，重试原精确目标。
- 非赚钱目标、无效活动意图、随机/附加耗材、材料预留冲突及账本/预测/路线漂移全部失败关闭。
- 没有新增机器执行器：制作、摆放、投料分别复用现有原生编译和运行链。
- 隐藏静默隔离运行 `runtime-supported-machine-capacity-20260804-120211` 已由同一高层 option 完成原生制作、规划器精确摆放、首次投料、处理开始、意图完成和三条训练行。
- 实际绑定目标为 `Farm:61,15`；队列、执行结果和持久意图三者一致。
- 运行中发现并修复执行器原料重算缺少售价字段造成的透明契约漂移。
- 全量回归 Core 1510/1510、Backend 108/108；Release 解决方案构建 0 错误、5 个既有警告。
- 当前权威对账：100 registered / 168 semantic / 88 compiler-bound / 19 five-gate / 18 allowlist / 0 Product Executor；585/585 exports，blocking 0，冻结分母指纹不变。

下一步：进入“任务/收集需求机器处理”独立切片。不得把 EVD-215 外推为随机机器、附加耗材、远程摆放、任务/收集需求或整个 `farm.process_machines` 已完成。正式全量训练仍等待生产长 rollout、正式 manifest/checkpoint、独立存档评测和第三年 21 分长跑。

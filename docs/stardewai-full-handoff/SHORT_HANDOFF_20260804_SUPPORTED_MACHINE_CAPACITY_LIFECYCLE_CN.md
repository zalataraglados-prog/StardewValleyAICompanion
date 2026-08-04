# 受支持机器容量生命周期短交接

状态：2026-08-04，编排实现完成，EVD-215 运行证据待补。

- 新增高层语义 `farm.establish_supported_machine_capacity`，仅服务 `goal.economy.earn_money`。
- 无活动意图时，只选择一个当前有界正净收益且证据完整的机器制作候选。
- `craft_selected` 只继续同一意图的既有机器摆放链。
- `placement_bound` 优先继续同一精确机器的既有首次投料链；若摆放尚未落地，重试原精确目标。
- 非赚钱目标、无效活动意图、随机/附加耗材、材料预留冲突及账本/预测/路线漂移全部失败关闭。
- 没有新增机器执行器：制作、摆放、投料分别复用现有原生编译和运行链。
- 分阶段候选、目标过滤、DailyPlan 和 ActionQueue 测试已通过。
- 当前权威对账：100 registered / 168 semantic / 88 compiler-bound / 18 five-gate / 17 allowlist / 0 Product Executor；585/585 exports，blocking 0。
- 新语义尚未进入训练白名单，因为还没有一条由它驱动的跨快照隐藏隔离整链证据。

下一步退出条件：在同一隔离运行中由该语义依次完成原生制作、摆放、首次投料、处理开始、支持意图完成和训练行落盘；通过后登记 EVD-215 并更新训练准入。随后进入“任务/收集需求机器处理”独立切片。

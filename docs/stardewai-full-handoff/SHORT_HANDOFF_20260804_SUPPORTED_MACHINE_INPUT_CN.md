# 机器承诺投料准入短交接

状态：2026-08-04，EVD-214 已闭合。

- 新增高层训练语义 `farm.load_supported_machine_input`。
- 只允许当前地图、精确 placement-bound 机器支持意图、当前确定性正净值、零附加耗材、精确输入槽未被其他目标预留的单次投料。
- 唯一执行链仍是 `MachineServiceCandidates -> load_machine_input_tile -> load_machine_input -> executor.load_machine_input`，没有第二套候选、编译器或执行器。
- 候选、DailyPlan、ActionQueue 和派发都携带并复核支持意图、策略账本、材料预留账本、机器、输入槽、数量和预测；任一漂移失败关闭。
- 主线程缓存的当前输入预测新增 `additional_consumed_item_count`，用于证明零附加耗材；没有恢复高频完整机器目录扫描。
- `training_machine` 桥配置此前会被 Backend 拒绝为未知 profile，现已增加精确必需域验证和缺域回归测试。
- 隐藏静默运行 `artifacts/runtime-machine-daily-plan-smoke/runtime-machine-daily-plan-smoke-20260804-104556/summary.json` PASS：原生 Keg 投料 applied/verified，机器开始处理，写入 1 条训练行，支持意图变为 `completed`。
- Core 1506/1506，Backend 106/106，Release 全解 0 error / 5 个既有 warning。
- 权威对账：99 registered / 167 semantic / 87 compiler-bound / 18 five-gate / 17 allowlist / 0 Product Executor；585/585 exports，blocking 0。

仍未覆盖：无支持意图的任意投料、非正收益、随机输出、附加耗材、预留冲突、远端未刷新路线、自定义执行、任务/收集需求，以及完整制作-摆放-投料生命周期。`farm.process_machines` 继续只作校准入口。

下一步：审计 `farm.process_machines` 剩余分支，优先选择能够形成独立全量闭环的“完整制作-摆放-首次投料支持生命周期”或“任务/收集需求机器处理”之一；先证明只有一条实现链，再补足运行时五门证据，不能直接放行广义机器处理。

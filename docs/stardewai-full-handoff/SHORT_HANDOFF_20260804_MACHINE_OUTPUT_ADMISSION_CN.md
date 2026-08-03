# 机器产物收取准入短交接

状态：2026-08-04，EVD-213 切片。

- 新增训练高层语义 `farm.collect_machine_outputs`，只暴露当前加载地图中精确已完成、非孵化器的机器产物。
- 唯一执行链仍是 `MachineServiceCandidates -> collect_machine_output_tile -> collect_machine_output -> executor.collect_machine_output`，没有第二套候选、编译或运行时。
- 上游要求机器就绪、精确持有物、背包可接收、可达站位和完整结构化技能/精通经验投影；任一漂移均失败关闭。
- `farm.process_machines` 继续校准专用。投料、制作、机器/箱子摆放、搬迁、存储和孵化器均未借此准入。
- EVD-180 的普通任务/特别订单绑定已通过编译重绑，但本切片不把它写成任务附着原生运行证据。
- 复用隐藏静默制品 `runtime-machine-output-smoke-20260729-234047`：4/4 PASS，覆盖背包入账、机器清空、结构化技能经验和精通经验。
- 对账：98 registered / 166 semantic / 86 compiler-bound / 17 five-gate / 16 allowlist / 0 Product Executor；585/585 exports，blocking 0。

下一步：继续从 `farm.process_machines` 做 MECE 审计。优先审查“当前就值得投料”的正收益、任务/收集需求、材料保留和时间窗口是否能形成一个独立、全量、失败关闭的高层入口；在统一策略闭合前，不得直接准入广义机器处理。

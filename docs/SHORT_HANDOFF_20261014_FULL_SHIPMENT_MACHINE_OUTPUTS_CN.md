# StardewAI 短交接：Full Shipment 共享机器产出族

## 当前状态

- 分支：`codex/unify-current-machine-output-evidence`。
- 严格运行时证据：`25/26`；共享 shipping/settlement 有效，正式训练仍未授权。
- 已闭合四层：`machine_output`、`native_machine_flavored_output`、`native_machine_item_query_output`、`native_solar_panel_output`。
- 唯一动作链：`farm.collect_machine_outputs -> executor.collect_machine_output`。
- 严格索引根：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v36-20261014`。

## 本轮闭合内容

- 当前机器产出贯穿日历、位置、设施、资源、加工、随机性、日内时间/能量和 dispatch 轴；太阳能板仅在当前产出物化且身份完整时可执行。
- `AcquisitionOutputProof.CanSatisfy` 统一数量/品质充分性，`MatchesAuthoritativeRoute` 允许同一对象保留多个权威来源描述，并按精确来源三元组匹配。
- 同一机器产出的等价来源只选择一个 canonical Teacher 候选，避免相同动作生成重复等价标签；透明桥来源没有被删除。
- 执行器接受普通机器与树液采集器由原生代码立即排入下一周期，但必须验证本轮物品已入包、机器不再可收获、计时为正且产物对象已更换。

## 原生证明

- 普通机器：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-machine-output-v5-20261014\summary.json`。
- 风味产物：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-machine-flavored-output-v2-20261014\summary.json`。
- ItemQuery：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-machine-item-query-output-v1-20261014\summary.json`。
- 太阳能板：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-solar-panel-output-v1-20261014\summary.json`。
- 四份证明均通过原生获取、入包、发货、睡眠结算与独立 rollout proof；冻结源存档未变。

## 下一固定任务

闭合唯一剩余层 `tree_moss_harvest_sample / full_shipment:item:Moss / (O)Moss / native_tree_moss_harvest`，复用现有 `foraging.harvest_tree_moss` 与共享移动/自然拾取执行链。通过后重建严格 `26/26` 索引，再单独判断正式训练准入；不得触碰或提交 `local-data/`。

# StardewAI 短交接：Full Shipment 原生水果树产物

## 当时状态（历史快照）

- 分支：`codex/admit-native-fruit-tree-produce`。
- 严格运行时证据：`18/26`，剩余 `8` 层；共享 shipping/settlement 有效，正式训练仍未授权。
- 本轮路线：`full_shipment:item:638 / (O)638 / native_fruit_tree_produce / fruit_tree:628:0`。
- 唯一动作链：`foraging.harvest_fruit_tree -> harvest_fruit_tree -> executor.harvest_fruit_tree`。
- 正式原生 proof 根：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-fruit-tree-v3-20261014\runtime-full-shipment-evidence-fruit-tree-v3-20261014`。
- 严格索引根：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v30-20261014`。

## 已闭合内容

- 当前水果树候选把精确来源、地点、目标格、站位格和真实路线距离贯穿到日历、位置、随机性、设施、加工与日内时间/能量轴；同路线族的非当前来源不会冒充当前可执行来源。
- v1 暴露设施与加工轴缺口；v2 证明原生摇树成功，但产物仅落在 debris，未进入背包。执行器现先验真原生掉落，再复用共享 BFS 和正常移动输入自然拾取，不直接写 debris 或背包。
- 正式 v3 在 `93` tick 内得到 `(O)638 x1`，验证水果数量清空、产量/品质、背包自然拾取、零觅食经验增量以及完整 fresh terminal/settlement/rollout proof。manifest SHA-256：`3c599efe1e7510c2ffa37a5e3e8e95bbcb0070a8fe135f0dd643c3ef279481ba`；receipt SHA-256：`1ce50884e8f31c9a614867f17be28dbb66e73758db931aafed7dcca0a4a44e55`。
- 既有 17 层全部重新验真通过，加入本层后为 `18/26`；Core `2679/2679`、Backend `229/229`、Bootstrap 与 RuntimeTestHarness Release `0 warning / 0 error`。

## 当时后续计划（已作废，仅历史）

已先消除功能分支 push 与 PR 的重复 `quality` 运行，并允许同一 PR 的新提交取消陈旧 CI。单 requirement 路线裁剪与跨 proof 复用继续遵守 #200/#201/#202 的 26 层启动门；执行器-only 重试则使用现有、受 StateHash/规划输入约束的 `ReplayAcquisitionQueue`。下一开发切片为 `farm_animal_product_sample / full_shipment:item:184 / (O)184 / native_farm_animal_produce`；不得触碰或提交 `local-data/`。

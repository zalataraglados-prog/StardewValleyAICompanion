# StardewAI 短交接：Full Shipment 原生野树种子

## 当时状态（历史快照）

- 分支：`codex/admit-native-wild-tree-seed`。
- 严格运行时证据：`15/26`，剩余 `11` 层；共享 shipping/settlement 有效，正式训练仍未授权。
- 本轮路线：`full_shipment:full_shipment:item:88:0:1 / full_shipment:item:88 / (O)88 / native_wild_tree_seed / wild_tree:6`。
- 唯一动作链：`foraging.harvest_tree_product -> executor.harvest_tree_product`。
- 正式原生 proof 根：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-wild-tree-seed-v2-20261013\runtime-full-shipment-evidence-wild-tree-seed-v2-20261013`。
- 严格索引根：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v26-20261013`。

## 已闭合内容

- 当前候选精确绑定 `Data/WildTrees payload.6.SeedItemId / wild_tree:6 / (O)88`。同物品的 `wild_tree:9` 只获得权威日历来源解析，位置轴因没有精确实时候选而判为不适用，不会与 `wild_tree:6` 混同。
- 原生 v2 在 `70` tick 内取得 `(O)88 x1`，验证摇树状态、完整输出域、自然碎片拾取、背包增量、觅食经验 `0` 和安全槽位恢复，回执为 `applied/verified`。
- 最终规则下既有 14 层全部重建通过；加入本层后的索引为 `15/26`，`formal_product_training_authorized=false`。
- v1 是 `wild_tree:9` 日历占位的失败诊断，不得作为正式 proof 引用。

## 当时后续计划（已作废，仅历史）

执行 `spring_onion_harvest_sample / full_shipment:item:399 / (O)399 / native_spring_onion_harvest`。复用既有春葱透明读、`foraging.harvest_spring_onions` 计划编译和原生 `executor.harvest_crop`，只闭合严格 Full Shipment evidence；不得重跑已通过的 15 层，不得触碰或提交 `local-data/`。

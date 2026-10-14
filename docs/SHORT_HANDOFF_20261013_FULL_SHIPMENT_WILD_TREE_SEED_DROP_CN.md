# StardewAI 短交接：Full Shipment 原生野树种子掉落

## 当时状态（历史快照）

- 分支：`codex/admit-native-wild-tree-seed-drop`。
- 严格运行时证据：`14/26`，剩余 `12` 层；共享 shipping/settlement 有效，正式训练仍未授权。
- 本轮路线：`full_shipment:full_shipment:item:408:0:4 / full_shipment:item:408 / (O)408 / native_wild_tree_seed_drop / wild_tree:2:0`。
- 唯一动作链：`foraging.harvest_tree_product -> executor.harvest_tree_product`。
- 正式原生 proof 根：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-wild-tree-seed-drop-v4-20261013\runtime-full-shipment-evidence-wild-tree-seed-drop-v4-20261013`。
- 严格索引根：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v24-20261013`。

## 已闭合内容

- 日历、地点、随机性和日内时间/能量轴均绑定精确当前候选与 `Data/WildTrees payload.2.SeedDropItems[0].ItemId`。`(O)408` 的 4 条未物化地点觅食替代路线只在同物品已有当前确定性来源时判为即时不适用，否则继续因概率证据缺失而失败关闭。
- 原生 v4 在 `56` tick 内取得 `(O)408 x1`，验证摇树状态、完整输出域、自然碎片拾取、背包增量、觅食经验 `0` 和安全槽位恢复，回执为 `applied/verified`。
- 历史 13 层已在最终代码下全部重建验证，无 proof 漂移。最终索引为 `14/26`，`formal_product_training_authorized=false`。
- v2 是 Teacher 分母缺口诊断；v3 是收窄作用域前的动作诊断。两者都不属于最终 evidence index，不得作为正式 proof 引用。

## 当时后续计划（已作废，仅历史）

执行 `wild_tree_seed_sample / full_shipment:item:88 / (O)88 / native_wild_tree_seed`。复用同一野树候选、编译、执行和自然拾取链，只闭合 `Data/WildTrees payload.6.SeedItemId` 的独立严格证据层；不得重跑已通过的 14 层，不得触碰或提交 `local-data/`。

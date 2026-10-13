# StardewAI 短交接：Full Shipment 原生地点觅食拾取

## 当前状态

- 分支：`codex/admit-native-location-forage-spawn`。
- 严格运行时证据：`17/26`，剩余 `9` 层；共享 shipping/settlement 有效，正式训练仍未授权。
- 本轮路线：`full_shipment:item:16 / (O)16 / native_location_forage_spawn / location:Forest:0`。
- 唯一动作链：`foraging.collect_spawned_objects -> collect_spawned_object -> executor.collect_spawned_object`。
- 正式原生 proof 根：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-location-forage-v2-20261014\runtime-full-shipment-evidence-location-forage-v2-20261014`。
- 严格索引根：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v29-20261014`。

## 已闭合内容

- 当前 spawned-object 候选现在把精确来源、地点、目标格、站位格和真实路线距离贯穿到位置轴与日内时间/能量轴；同物品、同路线族的非当前来源在存在精确当前替代来源时判为不适用。
- v1 因日内终端预算未实现和同路线替代来源未排除而在 Teacher preference 前失败关闭，只作诊断，不得作为正式 proof。v2 由原生 `GameLocation.checkAction` 移除 `Forest (1,6)` 的 `(O)16`，验证背包增加 `1`、觅食经验增加 `7`、耕种经验不变以及完整 fresh terminal/settlement/rollout proof。
- rollout manifest SHA-256：`6aad341921b5b081064ecb0865556e38582b2e1d55e219c7ab0da50858e5068b`；receipt SHA-256：`31d5347d06e9d607532af4e1c80150f170eb2b40b089bd33077024bede49383b`。
- 既有 16 层全部重新验真通过，加入本层后为 `17/26`；完整 Core `2679/2679`、Backend `229/229`、Bootstrap Release `0 warning / 0 error`、路线分发自测均通过。

## 下一固定任务

执行 `fruit_tree_harvest_sample / full_shipment:item:638 / (O)638 / native_fruit_tree_produce`。优先复用现有 `foraging.harvest_fruit_tree`、移动、原生摇树与自然拾取链，只补严格来源轴和 Full Shipment proof；不得重跑已通过的 17 层，不得触碰或提交 `local-data/`。

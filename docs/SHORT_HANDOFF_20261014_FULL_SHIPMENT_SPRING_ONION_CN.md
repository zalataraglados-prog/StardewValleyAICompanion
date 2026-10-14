# StardewAI 短交接：Full Shipment 原生春葱采收

## 当时状态（历史快照）

- 分支：`codex/admit-native-spring-onion-harvest`。
- 严格运行时证据：`16/26`，剩余 `10` 层；共享 shipping/settlement 有效，正式训练仍未授权。
- 本轮路线：`full_shipment:item:399 / (O)399 / native_spring_onion_harvest / Crop.harvest`。
- 唯一动作链：`foraging.harvest_spring_onions -> harvest_crop_tile -> executor.harvest_crop`。
- 正式原生 proof 根：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-spring-onion-v2-20261014\runtime-full-shipment-evidence-spring-onion-v2-20261014`。
- 严格索引根：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v28-20261014`。

## 已闭合内容

- 透明桥只为精确原版春葱作物发布 `native_spring_onion_harvest / Crop.harvest / (O)399` 来源；候选、日历、地点、日内预算、Teacher 与队列共用同一身份和当前快照。
- 原生动作验证 ready 作物移除、背包 `(O)399` 增加 `1`、觅食经验增加 `3`、耕种经验不变以及完整 fresh terminal/settlement/rollout proof；没有直接写背包或经验。
- rollout manifest SHA-256：`7976da974cbec8c931e1b52d137f09f2891336864e31e82fe4c72d9787335677`；receipt SHA-256：`da94fcddbd89a96e11395c0142b2852075cbf1728250ce22b077c8e6ce750057`。
- 既有 15 层先完成兼容重建，再加入本层形成 `16/26`；缓存 schema 已升至 `v8`，避免同进程误复用旧候选索引。

## 当时后续计划（已作废，仅历史）

执行 `location_forage_spawn_sample / native_location_forage_spawn`。优先复用现有 `foraging.collect_spawned_objects`、BFS 移动和自然拾取链，只补精确来源轴、当前候选贯穿与 Full Shipment proof；不得重跑已通过的 16 层，不得触碰或提交 `local-data/`。

# StardewAI 短交接：Full Shipment 原生茶树采收

## 当时状态（历史快照）

- 分支：`codex/admit-native-tea-bush-harvest`。
- 严格运行时证据：`13/26`，剩余 `13` 层；共享 shipping/settlement 有效，正式训练仍未授权。
- 本轮路线：`full_shipment:full_shipment:item:815:0:0 / full_shipment:item:815 / (O)815 / native_tea_bush_harvest / Bush.GetShakeOffItem`。
- 唯一动作链：`foraging.harvest_bushes -> executor.harvest_bush`。
- 原生证明根：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-tea-bush-v2-20261013\runtime-full-shipment-evidence-tea-bush-v2-20261013`。
- 严格索引根：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v19-20261013`。
- 冻结源存档 SHA-256：`5af603dbb99bafadbb14580952b6e4b8c0d9e257963174759f53fbc8b2d671cc`。

## 本轮闭合内容

- 没有新增业务代码。茶叶完整复用浆果已验证的透明候选、严格路线轴、调度编译、原生灌木执行器、BFS 移动与自然拾取链。
- 原生回执在 `39` tick 内取得 `(O)815 x1`，验证灌木偏移复位、输出增量、背包自然拾取和茶叶原生觅食经验 `0`，最终为 `applied/verified`。
- 完整 rollout proof 为 `verified_complete_rollout_proof_chain`。proof manifest SHA-256 为 `032e337bb3ab487249465d289c8a8f2145cc1934a46f2ff4add75349176dea13`，receipt SHA-256 为 `3fb71cbd20411e567577a9d0d0217e608c0070d043df8e13448e747406bb896f`。
- 首次 `-SkipBuild` 尝试被源码/DLL 新鲜度门禁拒绝，未启动游戏或形成证据。重建后只运行一次正式样本和一次最终 evidence index；既有 12 层没有重跑。

## 当时后续计划（已作废，仅历史）

执行 `wild_tree_seed_drop_sample / full_shipment:item:408 / (O)408 / native_wild_tree_seed_drop`。先审查并复用现有野树原生交互、移动和自然拾取链，只补当前严格路线轴与正式 Full Shipment proof。

不得复制执行器，不得重跑已通过的 13 层，不得触碰或提交 `local-data/`。性能优化继续遵守 #195/#200/#201/#202/#211/#223 的依赖哈希、失效与单次计算边界。

# StardewAI 短交接：Full Shipment 原生浆果灌木

## 当时状态（历史快照）

- 分支：`codex/admit-native-berry-bush-harvest`。
- 严格运行时证据：`11/26`，剩余 `15` 层；共享 shipping/settlement 有效，正式训练仍未授权。
- 本轮路线：`full_shipment:full_shipment:item:296:0:0 / full_shipment:item:296 / (O)296 / native_bush_shake / Bush.GetShakeOffItem`。
- 唯一动作链：`foraging.harvest_bushes -> executor.harvest_bush`。
- 原生证明根：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-berry-bush-v16-20261013\runtime-full-shipment-evidence-berry-bush-v16-20261013`。
- 严格索引根：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v17-20261013`。
- 冻结源存档哈希：`5af603dbb99bafadbb14580952b6e4b8c0d9e257963174759f53fbc8b2d671cc`。

## 本轮闭合内容

- 透明桥为浆果和茶叶提供 `Bush.GetShakeOffItem` 权威来源，候选、日历、地点、时间/能量、Teacher、调度和执行绑定使用同一精确候选。
- 夹具站位改为灌木真实包围盒外围，修复两格宽灌木内部站位。
- 执行器不再把落地碎片误判为已获得物品。原生摇树后验证状态与掉落守恒，再沿 BFS 靠近目标碎片并由游戏自然磁吸入库存；没有直接写背包。
- 隔离重放在 `89` tick 内取得 `(O)296 x3`；最终完整 v16 在 `52` tick 内完成同一原生动作，并通过装箱、睡眠、次日结算、fresh terminal、settlement、checkpoint 与 rollout proof。候选使用保守 `180` tick 的原生交互与收集预算。
- `-SkipBuild` 增加 Release 组件新鲜度检查，避免旧 Backend 在线排名与新 Bootstrap 离线验证发生版本分裂。

## 已验证

- RuntimeTestHarness Debug：0 warning / 0 error。
- 浆果与 Full Shipment 定向测试：`38/38`。
- 隔离动作重放：`passed`，库存 `(O)296 x3`，源存档未变。
- 完整原生样本：`passed`，rollout proof 为完整验证链。
- production evidence index：`11/26`，`native_bush_shake` 为 `verified_exact_native_sample`。

## 当时后续计划（已作废，仅历史）

执行 `ginger_harvest_sample / full_shipment:item:829 / (O)829 / native_ginger_harvest`。先审查并复用现有 `foraging.harvest_ginger -> executor.harvest_ginger` 候选、编译、执行与回执链，只补严格路线轴和 Full Shipment proof 缺口。不要复制执行器，不要重跑已通过的 11 层，不要触碰或提交 `local-data/`。

性能优化与下一样本分开提交。当前测得的固定开销来自里程碑前置索引和 1599 路线目标日期轴跨进程重算；按 #195/#200/#201/#202/#211 的依赖哈希与 proof 失效规则做复用，不能用缓存绕过权威输入哈希、fresh snapshot、原生动作或终态验证。

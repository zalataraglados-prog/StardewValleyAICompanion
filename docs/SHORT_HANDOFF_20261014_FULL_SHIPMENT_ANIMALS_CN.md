# StardewAI 短交接：Full Shipment 原生动物产物

## 当时状态（历史快照）

- 分支：`codex/admit-native-farm-animal-produce`。
- 严格运行时证据：`20/26`，剩余 `6` 层；共享 shipping/settlement 有效，正式训练仍未授权。
- 本轮路线：普通牛奶 `(O)184 / native_farm_animal_produce` 与大壶牛奶 `(O)186 / native_farm_animal_deluxe_produce`。
- 唯一动作链：`farm.collect_animal_products -> collect_animal_product -> executor.collect_animal_product`。
- 普通牛奶 proof 根：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-farm-animal-product-v1-20261014\runtime-full-shipment-evidence-farm-animal-product-v1-20261014`。
- 大壶牛奶 proof 根：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-farm-animal-deluxe-product-v1-20261014\runtime-full-shipment-evidence-farm-animal-deluxe-product-v1-20261014`。
- 严格索引根：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v32-20261014`。

## 已闭合内容

- 当前动物候选新增真实 `route_distance_tiles`，并将权威来源、动物身份、位置/目标/站位、工具种类/槽位、预计输出、质量、能量、经验、友好度与统计变化绑定到同一候选。
- 普通与高级动物产物共同支持精确 `Data/FarmAnimals` 来源身份校验、当前候选日历、当前位置目标、现有动物容量、非消耗工具门禁、已物化产物、确定性当前输出与日内时间/能量预算。缺工具、未就绪、来源或物品漂移继续在上游失败关闭。
- 普通牛奶原生执行 `97` tick，回执为 `applied/verified`；大壶牛奶复用同一原生 Milk Pail 生命周期并通过完整 rollout proof。没有直接写动物、背包、经验、能量、友好度或统计。
- `Invoke-RuntimeFullShipmentEvidenceMilestone.ps1` 新增 `-CompactOutput`，只压缩控制器输出，不跳过证明重建。Core `2680/2680`、Backend `229/229`、Bootstrap 与 RuntimeTestHarness Release 全部通过。

## 当时后续计划（已作废，仅历史）

进入 `fish_pond_output_sample / full_shipment:item:812 / (O)812 / native_fish_pond_output`。先审计并复用现有 `fishing.service_fish_ponds` 当前鱼塘输出链，只补严格 Full Shipment 规划轴与证据；不得新建第二套鱼塘收取执行器，不得触碰或提交 `local-data/`。机器三类路线应在共享机器规划改造后集中采证并刷新一次严格索引。

# StardewAI 短交接：Full Shipment 原生鱼塘产物

## 当前状态

- 分支：`codex/admit-native-fish-pond-output`。
- 严格运行时证据：`21/26`；共享 shipping/settlement 有效，正式训练仍未授权。
- 本轮路线：`full_shipment:item:812 / (O)812 / native_fish_pond_output / fish_pond:Sturgeon:0`。
- 唯一动作链：`fishing.service_fish_ponds -> collect_fish_pond_output -> executor.collect_fish_pond_output`。
- proof 根：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-fish-pond-output-v5-20261014\runtime-full-shipment-evidence-fish-pond-output-v5-20261014`。
- 严格索引根：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v35-20261014`。

## 已闭合内容

- 透明桥保留 `Sturgeon:0..5` 六条权威来源；规划、Teacher 和派发对同一实时候选只绑定 canonical 的 `Sturgeon:0`，不删除其余可读来源，也不让它们重复进入前沿。
- 当前鱼塘候选把来源、物品、位置、目标/站位格、路线距离、输出数量/质量、原生回调与技能经验贯穿日历、位置、设施、资源、加工、随机性和日预算轴。鱼塘不存在、输出未就绪或任一身份漂移均在上游失败关闭。
- v5 原生收取与完整 proof 已通过；离线重放两次复用原执行回执成功，没有再次启动游戏。候选索引缓存 schema 为 `v12`。
- 重放脚本已支持唯一透明当前候选的 option-only 排名恢复，避免重放自行改变 ranking hash 和 queue id；历史候选缺失诊断契约保持不变，旧 Sap 位置轴重算 `CHANGED=0`。

## 下一固定任务

进入共享机器产出族，统一审计并补齐 `machine_output`、`native_machine_flavored_output`、`native_machine_item_query_output` 和 `native_solar_panel_output`。必须复用现有当前机器候选与 `farm.collect_machine_outputs` 编译/执行链，只做一套来源分类和共用规划轴，再集中运行严格样本；不得触碰或提交 `local-data/`。机器族完成后处理最后一层 `native_tree_moss_harvest`。

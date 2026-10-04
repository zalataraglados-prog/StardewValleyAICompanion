# StardewAI 短交接：Full Shipment 普通作物样本

## 2026-10-04 接续状态：浆果灌木样本

- 下一层固定为 `full_shipment.runtime_sample.04.native_bush_shake`，代表路线 `full_shipment:full_shipment:item:296:0:0`，复用 `foraging.harvest_bushes -> executor.harvest_bush`。
- 运行脚本和通用断点 proof 已适配；旧防风草证据完整重放通过，Core game-free `124/124`。
- 当前无交互桌面会话，浆果原生运行未启动，不能计入生产证据。严格状态仍为 `2/26`、剩余 24 层。
- 下一次有交互会话时直接运行 `berry_bush_harvest_sample`；成功后登记 proof manifest/receipt 并重建索引。不要复制动作链，也不要删除当前失败/准备产物或 `local-data/`。

## 当前结论

- 分支：`feat/full-shipment-sap-native-prefix`。
- Full Shipment 静态覆盖：`154/154` requirements、`641` routes、`26` runtime strata。
- 严格运行时抽样：`2/26`，已覆盖 `harvests_as` 与 `native_wild_tree_chop_drop`；剩余 24 层。
- 共享 shipping/deposit/sleep recurrence 已严格验证。
- `runtime_sample_evidence_complete=false`、`formal_product_training_authorized=false`，尚不可正式全量训练。

## 本轮实现

- 反编译确认普通作物来源字段为 `Crop.netSeedIndex`，野生种子仍使用 `whichForageCrop`；透明桥已修复并有源码守卫。
- 隔离样本 `runtime-full-shipment-parsnip-sample-20261004-135111` 原生收获防风草成功：路线 `full_shipment:full_shipment:item:24:0:0`，作物移除，背包增加 `(O)24 x1`，Farming XP `0 -> 8`。
- 新增 verified-artifact settlement/checkpoint 快路径及严格/快路径等价回归。快路径逐项核验所有输入哈希；最终 proof 仍完整重建一次。
- `scripts/Resume-RuntimeFullShipmentAcquisitionProof.ps1` 可从已有执行回执续接证明，不启动游戏。它使用运行目录内隔离账本副本，结束时关闭 Backend。

## 已验证

- GoalConditionedBootstrap Release：0 warning / 0 error。
- Backend Release：0 warning / 0 error。
- `self-test-acquisition-route-dispatch`：通过。
- `CropHarvestSourceIdentitySourceGuardTests`：1/1 通过。
- 防风草最终 rollout proof：`verified_complete_rollout_proof_chain`。
- 严格生产索引：`verified_runtime_sample_stratum_count=2`、`missing_runtime_sample_stratum_count=24`、`shared_shipping_evidence_verified=true`。

## 本地证据

运行产物和生产证据 manifest/index 位于 `artifacts/`，按仓库规则忽略，不应提交。不要删除失败运行或 `local-data/`。本轮结束时应确认端口 `8765/8767/8768/8798` 无监听，且无遗留 StardewAI 计划任务。

## 下一固定任务

读取严格索引的 `remaining_stratum_ids`，盘点每层已有原生 smoke 是否包含 fresh before/after、精确来源身份、编译队列和原生执行回执。能复用的证据升级为当前唯一 acquisition rollout 合同；缺少关键材料的层才新增隔离运行。不得复制候选、编译器、执行器或 Teacher 链，也不得回退为逐物品执行 154 次。

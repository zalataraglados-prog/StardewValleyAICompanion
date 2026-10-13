# StardewAI 短交接：Full Shipment 社区中心奖励路线

## 当前状态

- 分支：`codex/admit-native-community-center-reward`。
- 代表目标：`full_shipment:item:336 / (O)336 / creates_reward_item / bundle:Bulletin Board/33:reward`。
- 路线 occurrence：`full_shipment:full_shipment:item:336:0:0`。
- 严格 Full Shipment 运行时证据：`9/26`，剩余 `17` 层；七个 high-risk 样本已全部完成。
- 共享 shipping/settlement 证明有效；`formal_product_training_authorized=false`。

## 本轮实现

- `AcquisitionCurrentRouteCandidateIndex` 从 `community_center.donate_bundle_items` 读取当前奖励候选，并要求 `world_progress.community_center` 的 bundle 行数、投影行数、不可用行数和逐行 `projection_status=exact` 共同证明分母完整。
- `creates_reward_item` 只有在当前快照存在精确奖励候选时才进入支持集合；其他快照维持旧占位结果，避免让无关历史证明发生全局日历漂移。
- 日历解析严格核对 `Data/Bundles`、`bundle:<key>:reward` 与 `payload.<key>[reward]`，生成当前候选约束的 CommunityCenter 全日窗口。地点轴、无材料消耗轴以及零能量/候选估时轴均复用同一精确候选。
- Teacher 选择后继续复用现有 `community_center.donate_bundle_items`、DailyPlan、动作编译器和 `executor.claim_community_center_bundle_reward`；没有新增第二套规划器、编译器或执行器。
- `Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1` 对非 Sap acquisition replay 在成功后直接写样本摘要，不再进入 Sap 专属投递、睡眠与结算路径。
- `Invoke-RuntimeFullShipmentEvidenceMilestone.ps1` 在高风险样本齐备时报告 `high_risk_evidence_verified`，空待办输出为 `[]`。

## 原生证据

- 证明根：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-community-center-v2-20261013\runtime-full-shipment-evidence-community-center-v2-20261013`。
- 奖励可领取：`true -> false`。
- Gold Bar `(O)336`：`0 -> 5`。
- 原生 item receipt：`applied`。
- rollout receipt：`verified_complete_rollout_proof_chain`，portfolio completion 与 proof chain 均为 `true`。
- 严格索引：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v5-20261013`，状态 `9/26`，共享 shipping/settlement 有效。

## 下一步

standard 批次首项 `parsnip_harvest_sample` 已完成并把严格索引推进到 `10/26`。下一固定项为 `berry_bush_harvest_sample`：`full_shipment:item:296 / (O)296 / native_bush_shake / foraging.harvest_bushes`。不要重复运行已通过的十个分层，不要提交或删除 `local-data/`。

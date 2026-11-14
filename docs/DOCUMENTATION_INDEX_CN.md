# StardewAI 文档总目录

按用途找现行入口，按日期追历史。这里登记仓库内的文档和 `docs/` 中的随附证据。修改、移动、新增或交接时见 [文档编辑与交接规范](DOCUMENTATION_MAINTENANCE_CN.md)。

## 现行入口

| 要查什么 | 先看这里 |
| --- | --- |
| 今天的状态、当前在制项和接手位置 | [当前状态](CURRENT_STATUS_CN.md) · [当前工作](CURRENT_WORK_CN.md)，以有时间戳且有效的接续条目为准 |
| 近期开发怎样走到第一批正式数据 | [R2 开训前执行顺序](FORMAL_FULL_TRAINING_READINESS_CN.md#r2-开训前执行顺序) · [R2 四层验收](PRETRAINING_ACCEPTANCE_SCOPE_CORRECTION_CN.md#3-修正后的四层验收合同) |
| 远期产品阶段、架构边界与系统建设 | [产品路线图](COMPANION_PRODUCT_ROADMAP_CN.md) · [陪玩架构约束](FUTURE_COMPANION_ARCHITECTURE_CN.md) · [系统完成路线](FULL_SYSTEM_COMPLETION_PLAN_CN.md) |
| 游戏状态、字段与制品证据 | [权威知识字典](authoritative-knowledge-dictionary.md) · [字段字典](stardewai-full-handoff/field-dictionary.md) · [制品布局](knowledge-artifact-layout.md) · [版本锁](../knowledge-artifacts.lock.json) · [原生证据账本](stardewai-full-handoff/evidence.md) |
| 教师监督、数据和模型 | [Teacher／Student 合同](TEACHER_STUDENT_CONVERGENCE_CONTRACT_CN.md) · [独立成对监督](independent-teacher-pairwise-admission.md) · [Provider 要求](STUDENT_TEACHER_PROVIDER_INTERFACE_REQUIREMENTS_CN.md) |
| 动作能力的当前机器口径 | [版本目录说明](../catalogs/vanilla-1.6.15/README.md) · [动作实现对账](../catalogs/vanilla-1.6.15/action-implementation-reconciliation.json) · [能力注册表](../src/StardewAI.Contracts/Capabilities/OptionCapabilityRegistrySource.cs) |
| 怎么修改文档或移交工作 | [文档编辑与交接规范](DOCUMENTATION_MAINTENANCE_CN.md) |

事实和流程分别按来源查。代码、同版本机器报告和原生工件用于核实实现与结果。当前状态和当前工作用于找运行事实和施工接续。R2、正式准入及产品路线合同用于确定各自范围内的规则。旧 `SHORT_HANDOFF`、审计、根目录证据账本及实验室初始说明用于历史追溯。总目录中的日期只用于导航。遇到冲突时，先核对原文件的适用范围和失效注释。

## 关键时间关系

- **2026-07。** 透明读取、任务／社交／矿井基础与权威字典逐步成形。早期审计和交接记录可用于回溯字段来源及各动作当时的验证范围。 [7 月架构审计](FULL_SYSTEM_AUDIT_20260719_CN.md) · [7 月权威字典交接](stardewai-full-handoff/SHORT_HANDOFF_20260723_AUTHORITATIVE_DICTIONARY_CN.md)
- **2026-08 至 09。** Product Executor、策略轨迹和原生跨日事务接续到 r35。旧文档治理页和 EVD326 交接记录的是当时的训练和数据状态。 [Product Executor 交接](SHORT_HANDOFF_20260901_PRODUCT_EXECUTOR_EVD325_CN.md) · [训练交接](SHORT_HANDOFF_20260901_FORMAL_TRAINING_EVD326_CN.md) · [r35 历史治理页](DOCUMENTATION_STATUS_20260901_CN.md)
- **2026-10。** Full Shipment 补采记录与运行边界按专题归档。姜岛核桃来源日程及独立重放也在这个阶段形成专门的证明合同。 [Full Shipment 交接](stardewai-full-handoff/SHORT_HANDOFF_20261005_FULL_SHIPMENT_SAMPLES_CN.md) · [核桃日程证明](GOLDEN_WALNUT_SOURCE_SCHEDULE_VERIFICATION.md)
- **2026-11-03。** R2 取代 R1 在单根验收、固定施工顺序及 Gourmand 前置方面的旧条款。局部验收、共享前置、全局组合和新档评测分层。独立成对监督与正式启动准入分别处理。 [R2 纠偏](PRETRAINING_ACCEPTANCE_SCOPE_CORRECTION_CN.md) · [独立成对监督](independent-teacher-pairwise-admission.md)
- **2026-11-07 至 11。** L1 局部覆盖冻结后，接续转向作物托管和 L3 全局集成。#551 把从当前代码到正式数据的施工顺序、退出条件和启动硬锁集中到正式准入文档的 R2 小节。 [作物证据交接](current-production-snapshot-handoff.md) · [R2 开训前执行顺序](FORMAL_FULL_TRAINING_READINESS_CN.md#r2-开训前执行顺序)
- **2026-11-14。** 按能力块持续负责调试与集成，长期无进展先核对目标、合同归属及旧实现意图；小错误不反复新建交接或验收层。十七点后原API协作者接生命周期消费，控制端推进不重叠的checkpoint接线，原八步及正式训练锁不变。 [持续阻塞规则](DOCUMENTATION_MAINTENANCE_CN.md#能力块与持续阻塞) · [当前分工](CURRENT_WORK_CN.md#2026-11-14-十七点协作接续与能力块规则)

## 全量时间目录

覆盖 Git 中 `docs/` 下的全部文件和仓库其余位置的 Markdown／纯文本文档。日期依次取首个一级标题中明确写出的日期、文件名日期、该路径在 Git 历史中的首次入库日期。Git 日期换算为北京时间。索引和编辑规范用本次编制日期。按月分组，由近到远，同日按路径排序。日期只用于定位，后续正文编辑不会改变该文档的目录时点。

编制于 2026-11-11T01:06:05+08:00（Asia/Shanghai），本版共 318 项。

### 2026-11

| 日期 | 文档 | 日期依据 |
| --- | --- | --- |
| 2026-11-11 | [docs/DOCUMENTATION_INDEX_CN.md](DOCUMENTATION_INDEX_CN.md) | 编制 |
| 2026-11-11 | [docs/DOCUMENTATION_MAINTENANCE_CN.md](DOCUMENTATION_MAINTENANCE_CN.md) | 编制 |
| 2026-11-09 | [docs/native-debris-state-observation.md](native-debris-state-observation.md) | Git 首录 |
| 2026-11-09 | [docs/native-inventory-graph-observation.md](native-inventory-graph-observation.md) | Git 首录 |
| 2026-11-09 | [docs/native-inventory-object-transients.md](native-inventory-object-transients.md) | Git 首录 |
| 2026-11-09 | [docs/native-inventory-payload-observation.md](native-inventory-payload-observation.md) | Git 首录 |
| 2026-11-09 | [docs/native-inventory-transient-observation.md](native-inventory-transient-observation.md) | Git 首录 |
| 2026-11-08 | [docs/action-origin-required-output.md](action-origin-required-output.md) | Git 首录 |
| 2026-11-08 | [docs/canonical-hay-storage-resource.md](canonical-hay-storage-resource.md) | Git 首录 |
| 2026-11-08 | [docs/canonical-object-unit-purity.md](canonical-object-unit-purity.md) | Git 首录 |
| 2026-11-08 | [docs/money-crop-controller-stop-recovery.md](money-crop-controller-stop-recovery.md) | Git 首录 |
| 2026-11-08 | [docs/money-crop-lifecycle-recovery.md](money-crop-lifecycle-recovery.md) | Git 首录 |
| 2026-11-08 | [docs/money-crop-managed-maintenance.md](money-crop-managed-maintenance.md) | Git 首录 |
| 2026-11-08 | [docs/money-crop-managed-phase-owner.md](money-crop-managed-phase-owner.md) | Git 首录 |
| 2026-11-08 | [docs/money-crop-managed-runtime-recovery.md](money-crop-managed-runtime-recovery.md) | Git 首录 |
| 2026-11-08 | [docs/money-crop-managed-seed-scopes.md](money-crop-managed-seed-scopes.md) | Git 首录 |
| 2026-11-08 | [docs/money-crop-owned-maintenance-execution.md](money-crop-owned-maintenance-execution.md) | Git 首录 |
| 2026-11-08 | [docs/money-crop-owned-planting-binding.md](money-crop-owned-planting-binding.md) | Git 首录 |
| 2026-11-08 | [docs/money-crop-owned-planting-runtime.md](money-crop-owned-planting-runtime.md) | Git 首录 |
| 2026-11-08 | [docs/money-crop-owned-planting-settlement.md](money-crop-owned-planting-settlement.md) | Git 首录 |
| 2026-11-08 | [docs/money-crop-owned-route-execution.md](money-crop-owned-route-execution.md) | Git 首录 |
| 2026-11-08 | [docs/money-crop-owner-lifecycle.md](money-crop-owner-lifecycle.md) | Git 首录 |
| 2026-11-08 | [docs/money-crop-partial-preparation-continuation.md](money-crop-partial-preparation-continuation.md) | Git 首录 |
| 2026-11-08 | [docs/money-crop-prepared-runtime-restore.md](money-crop-prepared-runtime-restore.md) | Git 首录 |
| 2026-11-08 | [docs/money-crop-refill-approach-compilation.md](money-crop-refill-approach-compilation.md) | Git 首录 |
| 2026-11-08 | [docs/money-crop-refill-control-outcome.md](money-crop-refill-control-outcome.md) | Git 首录 |
| 2026-11-08 | [docs/money-crop-seed-lifecycle-release.md](money-crop-seed-lifecycle-release.md) | Git 首录 |
| 2026-11-08 | [docs/money-crop-seed-lifecycle-runtime.md](money-crop-seed-lifecycle-runtime.md) | Git 首录 |
| 2026-11-08 | [docs/money-crop-seed-native-settlement.md](money-crop-seed-native-settlement.md) | Git 首录 |
| 2026-11-08 | [docs/money-crop-seed-preparation-ownership.md](money-crop-seed-preparation-ownership.md) | Git 首录 |
| 2026-11-08 | [docs/money-crop-seed-shared-runtime.md](money-crop-seed-shared-runtime.md) | Git 首录 |
| 2026-11-08 | [docs/money-crop-supporting-route-continuity.md](money-crop-supporting-route-continuity.md) | Git 首录 |
| 2026-11-08 | [docs/money-crop-watering-maintenance.md](money-crop-watering-maintenance.md) | Git 首录 |
| 2026-11-08 | [docs/native-clearance-resource-continuity.md](native-clearance-resource-continuity.md) | Git 首录 |
| 2026-11-08 | [docs/native-clearance-tool-observation.md](native-clearance-tool-observation.md) | Git 首录 |
| 2026-11-08 | [docs/native-output-observation.md](native-output-observation.md) | Git 首录 |
| 2026-11-08 | [docs/native-output-snapshot-anchor.md](native-output-snapshot-anchor.md) | Git 首录 |
| 2026-11-08 | [docs/ordinary-drop-projection-purity.md](ordinary-drop-projection-purity.md) | Git 首录 |
| 2026-11-08 | [docs/refill-approach-native-trace.md](refill-approach-native-trace.md) | Git 首录 |
| 2026-11-07 | [docs/current-production-snapshot-handoff.md](current-production-snapshot-handoff.md) | Git 首录 |
| 2026-11-07 | [docs/marriage-criterion-corpus.md](marriage-criterion-corpus.md) | Git 首录 |
| 2026-11-07 | [docs/marriage-local-acceptance.md](marriage-local-acceptance.md) | Git 首录 |
| 2026-11-07 | [docs/marriage-local-method-view.md](marriage-local-method-view.md) | Git 首录 |
| 2026-11-07 | [docs/marriage-native-joint-receipts.md](marriage-native-joint-receipts.md) | Git 首录 |
| 2026-11-07 | [docs/marriage-policy-teacher-rows.md](marriage-policy-teacher-rows.md) | Git 首录 |
| 2026-11-07 | [docs/money-counterfactual-source-binding.md](money-counterfactual-source-binding.md) | Git 首录 |
| 2026-11-07 | [docs/money-crop-owned-seed-supply.md](money-crop-owned-seed-supply.md) | Git 首录 |
| 2026-11-07 | [docs/money-crop-resource-reservation.md](money-crop-resource-reservation.md) | Git 首录 |
| 2026-11-07 | [docs/money-crop-runtime-installation.md](money-crop-runtime-installation.md) | Git 首录 |
| 2026-11-07 | [docs/money-crop-seed-purchase-settlement.md](money-crop-seed-purchase-settlement.md) | Git 首录 |
| 2026-11-07 | [docs/money-crop-seed-runtime-transaction.md](money-crop-seed-runtime-transaction.md) | Git 首录 |
| 2026-11-07 | [docs/money-crop-seed-transfer-preparation.md](money-crop-seed-transfer-preparation.md) | Git 首录 |
| 2026-11-07 | [docs/money-local-method-view.md](money-local-method-view.md) | Git 首录 |
| 2026-11-07 | [docs/money-local-native-collection.md](money-local-native-collection.md) | Git 首录 |
| 2026-11-07 | [docs/money-shop-replay-comparison.md](money-shop-replay-comparison.md) | Git 首录 |
| 2026-11-07 | [docs/money-shop-replay-corpus.md](money-shop-replay-corpus.md) | Git 首录 |
| 2026-11-07 | [docs/museum-collection-local-intake.md](museum-collection-local-intake.md) | Git 首录 |
| 2026-11-07 | [docs/museum-criterion-corpus.md](museum-criterion-corpus.md) | Git 首录 |
| 2026-11-07 | [docs/museum-local-acceptance.md](museum-local-acceptance.md) | Git 首录 |
| 2026-11-07 | [docs/museum-local-method-view.md](museum-local-method-view.md) | Git 首录 |
| 2026-11-07 | [docs/museum-policy-input-admission.md](museum-policy-input-admission.md) | Git 首录 |
| 2026-11-07 | [docs/museum-terminal-native-join.md](museum-terminal-native-join.md) | Git 首录 |
| 2026-11-07 | [docs/museum-terminal-teacher-comparison.md](museum-terminal-teacher-comparison.md) | Git 首录 |
| 2026-11-06 | [docs/friendship-complete-intake.md](friendship-complete-intake.md) | Git 首录 |
| 2026-11-06 | [docs/friendship-criterion-corpus.md](friendship-criterion-corpus.md) | Git 首录 |
| 2026-11-06 | [docs/friendship-independent-comparison.md](friendship-independent-comparison.md) | Git 首录 |
| 2026-11-06 | [docs/friendship-local-acceptance.md](friendship-local-acceptance.md) | Git 首录 |
| 2026-11-06 | [docs/friendship-local-intake.md](friendship-local-intake.md) | Git 首录 |
| 2026-11-06 | [docs/friendship-local-method-view.md](friendship-local-method-view.md) | Git 首录 |
| 2026-11-06 | [docs/friendship-native-receipts.md](friendship-native-receipts.md) | Git 首录 |
| 2026-11-06 | [docs/friendship-native-thresholds.md](friendship-native-thresholds.md) | Git 首录 |
| 2026-11-06 | [docs/friendship-pairwise-admission.md](friendship-pairwise-admission.md) | Git 首录 |
| 2026-11-06 | [docs/friendship-policy-inputs.md](friendship-policy-inputs.md) | Git 首录 |
| 2026-11-06 | [docs/friendship-real-calibration.md](friendship-real-calibration.md) | Git 首录 |
| 2026-11-06 | [docs/friendship-route-contact-budget.md](friendship-route-contact-budget.md) | Git 首录 |
| 2026-11-06 | [docs/marriage-current-comparison.md](marriage-current-comparison.md) | Git 首录 |
| 2026-11-06 | [docs/marriage-house-local-intake.md](marriage-house-local-intake.md) | Git 首录 |
| 2026-11-06 | [docs/policy-independent-decisions.md](policy-independent-decisions.md) | Git 首录 |
| 2026-11-06 | [docs/skills-local-acceptance.md](skills-local-acceptance.md) | Git 首录 |
| 2026-11-05 | [docs/master-angler-local-acceptance.md](master-angler-local-acceptance.md) | Git 首录 |
| 2026-11-05 | [docs/master-angler-local-native-join.md](master-angler-local-native-join.md) | Git 首录 |
| 2026-11-05 | [docs/rusty-key-donation-teacher-comparison.md](rusty-key-donation-teacher-comparison.md) | Git 首录 |
| 2026-11-05 | [docs/rusty-key-local-acceptance.md](rusty-key-local-acceptance.md) | Git 首录 |
| 2026-11-05 | [docs/rusty-key-teacher-corpus.md](rusty-key-teacher-corpus.md) | Git 首录 |
| 2026-11-05 | [docs/rusty-key-teacher-native-join.md](rusty-key-teacher-native-join.md) | Git 首录 |
| 2026-11-05 | [docs/rusty-key-terminal-evidence.md](rusty-key-terminal-evidence.md) | Git 首录 |
| 2026-11-05 | [docs/shared-executor-compatibility-audit-20261105.md](shared-executor-compatibility-audit-20261105.md) | 标题 |
| 2026-11-05 | [docs/skills-local-intake.md](skills-local-intake.md) | Git 首录 |
| 2026-11-04 | [docs/control-execution-admission.md](control-execution-admission.md) | Git 首录 |
| 2026-11-04 | [docs/executor-owned-idle-pause.md](executor-owned-idle-pause.md) | Git 首录 |
| 2026-11-04 | [docs/geode-dialogue-lifecycle.md](geode-dialogue-lifecycle.md) | Git 首录 |
| 2026-11-04 | [docs/material-transfer-slot-lineage.md](material-transfer-slot-lineage.md) | Git 首录 |
| 2026-11-04 | [docs/portfolio-mutation-reply-recovery.md](portfolio-mutation-reply-recovery.md) | Git 首录 |
| 2026-11-04 | [docs/product-receipt-recovery.md](product-receipt-recovery.md) | Git 首录 |
| 2026-11-04 | [docs/rusty-key-runtime-rolling.md](rusty-key-runtime-rolling.md) | Git 首录 |
| 2026-11-04 | [docs/rusty-key-step-preparation.md](rusty-key-step-preparation.md) | Git 首录 |
| 2026-11-04 | [docs/rusty-key-time-budget.md](rusty-key-time-budget.md) | Git 首录 |
| 2026-11-03 | [docs/independent-teacher-pairwise-admission.md](independent-teacher-pairwise-admission.md) | Git 首录 |
| 2026-11-03 | [docs/museum-donation-material-admission.md](museum-donation-material-admission.md) | Git 首录 |
| 2026-11-03 | [docs/museum-output-custody.md](museum-output-custody.md) | Git 首录 |
| 2026-11-03 | [docs/PRETRAINING_ACCEPTANCE_SCOPE_CORRECTION_CN.md](PRETRAINING_ACCEPTANCE_SCOPE_CORRECTION_CN.md) | Git 首录 |
| 2026-11-03 | [docs/rusty-key-acquisition-portfolio.md](rusty-key-acquisition-portfolio.md) | Git 首录 |
| 2026-11-03 | [docs/rusty-key-native-threshold-evidence.md](rusty-key-native-threshold-evidence.md) | Git 首录 |
| 2026-11-03 | [docs/rusty-key-rolling-supply.md](rusty-key-rolling-supply.md) | Git 首录 |
| 2026-11-02 | [docs/end-of-night-transaction-runtime-contract.md](end-of-night-transaction-runtime-contract.md) | Git 首录 |

### 2026-10

| 日期 | 文档 | 日期依据 |
| --- | --- | --- |
| 2026-10-30 | [docs/GOLDEN_WALNUT_BOOTSTRAP_CORRECTION.md](GOLDEN_WALNUT_BOOTSTRAP_CORRECTION.md) | Git 首录 |
| 2026-10-30 | [docs/GOLDEN_WALNUT_BOOTSTRAP_CORRECTION_EVIDENCE.md](GOLDEN_WALNUT_BOOTSTRAP_CORRECTION_EVIDENCE.md) | Git 首录 |
| 2026-10-30 | [docs/GOLDEN_WALNUT_BOOTSTRAP_CORRECTION_RISK.md](GOLDEN_WALNUT_BOOTSTRAP_CORRECTION_RISK.md) | Git 首录 |
| 2026-10-30 | [docs/GOLDEN_WALNUT_SOURCE_SCHEDULE_VERIFICATION.md](GOLDEN_WALNUT_SOURCE_SCHEDULE_VERIFICATION.md) | Git 首录 |
| 2026-10-30 | [docs/master-angler-source-stability-review.md](master-angler-source-stability-review.md) | Git 首录 |
| 2026-10-28 | [docs/COMPANION_PRODUCT_ROADMAP_CN.md](COMPANION_PRODUCT_ROADMAP_CN.md) | Git 首录 |
| 2026-10-28 | [docs/STUDENT_TEACHER_PROVIDER_INTERFACE_REQUIREMENTS_CN.md](STUDENT_TEACHER_PROVIDER_INTERFACE_REQUIREMENTS_CN.md) | Git 首录 |
| 2026-10-15 | [docs/money-threshold-production-evidence.md](money-threshold-production-evidence.md) | Git 首录 |
| 2026-10-15 | [docs/validation-trace-issue-274-20261015.json](validation-trace-issue-274-20261015.json) | 文件名 |
| 2026-10-14 | [docs/CURRENT_STATUS_CN.md](CURRENT_STATUS_CN.md) | Git 首录 |
| 2026-10-14 | [docs/SHORT_HANDOFF_20261014_FULL_SHIPMENT_ANIMALS_CN.md](SHORT_HANDOFF_20261014_FULL_SHIPMENT_ANIMALS_CN.md) | 文件名 |
| 2026-10-14 | [docs/SHORT_HANDOFF_20261014_FULL_SHIPMENT_FISH_POND_CN.md](SHORT_HANDOFF_20261014_FULL_SHIPMENT_FISH_POND_CN.md) | 文件名 |
| 2026-10-14 | [docs/SHORT_HANDOFF_20261014_FULL_SHIPMENT_LOCATION_FORAGE_CN.md](SHORT_HANDOFF_20261014_FULL_SHIPMENT_LOCATION_FORAGE_CN.md) | 文件名 |
| 2026-10-14 | [docs/SHORT_HANDOFF_20261014_FULL_SHIPMENT_MACHINE_OUTPUTS_CN.md](SHORT_HANDOFF_20261014_FULL_SHIPMENT_MACHINE_OUTPUTS_CN.md) | 文件名 |
| 2026-10-14 | [docs/SHORT_HANDOFF_20261014_FULL_SHIPMENT_SPRING_ONION_CN.md](SHORT_HANDOFF_20261014_FULL_SHIPMENT_SPRING_ONION_CN.md) | 文件名 |
| 2026-10-14 | [docs/SHORT_HANDOFF_20261014_FULL_SHIPMENT_TREE_MOSS_CN.md](SHORT_HANDOFF_20261014_FULL_SHIPMENT_TREE_MOSS_CN.md) | 文件名 |
| 2026-10-13 | [docs/SHORT_HANDOFF_20261013_FULL_SHIPMENT_BERRY_BUSH_CN.md](SHORT_HANDOFF_20261013_FULL_SHIPMENT_BERRY_BUSH_CN.md) | 文件名 |
| 2026-10-13 | [docs/SHORT_HANDOFF_20261013_FULL_SHIPMENT_COMMUNITY_CENTER_REWARD_CN.md](SHORT_HANDOFF_20261013_FULL_SHIPMENT_COMMUNITY_CENTER_REWARD_CN.md) | 文件名 |
| 2026-10-13 | [docs/SHORT_HANDOFF_20261013_FULL_SHIPMENT_GINGER_CN.md](SHORT_HANDOFF_20261013_FULL_SHIPMENT_GINGER_CN.md) | 文件名 |
| 2026-10-13 | [docs/SHORT_HANDOFF_20261013_FULL_SHIPMENT_TEA_BUSH_CN.md](SHORT_HANDOFF_20261013_FULL_SHIPMENT_TEA_BUSH_CN.md) | 文件名 |
| 2026-10-13 | [docs/SHORT_HANDOFF_20261013_FULL_SHIPMENT_WILD_TREE_SEED_CN.md](SHORT_HANDOFF_20261013_FULL_SHIPMENT_WILD_TREE_SEED_CN.md) | 文件名 |
| 2026-10-13 | [docs/SHORT_HANDOFF_20261013_FULL_SHIPMENT_WILD_TREE_SEED_DROP_CN.md](SHORT_HANDOFF_20261013_FULL_SHIPMENT_WILD_TREE_SEED_DROP_CN.md) | 文件名 |
| 2026-10-12 | [docs/SHORT_HANDOFF_20261012_FULL_SHIPMENT_SHOP_PURCHASE_CN.md](SHORT_HANDOFF_20261012_FULL_SHIPMENT_SHOP_PURCHASE_CN.md) | 文件名 |
| 2026-10-11 | [docs/invocation-read-session-sample.json](invocation-read-session-sample.json) | Git 首录 |
| 2026-10-11 | [docs/invocation-read-session.md](invocation-read-session.md) | Git 首录 |
| 2026-10-11 | [docs/validation-dependency-dag-phase-a.md](validation-dependency-dag-phase-a.md) | Git 首录 |
| 2026-10-11 | [docs/validation-impact-advisory.md](validation-impact-advisory.md) | Git 首录 |
| 2026-10-11 | [docs/validation-trace-phase0-baseline.json](validation-trace-phase0-baseline.json) | Git 首录 |
| 2026-10-11 | [docs/validation-trace-phase0.md](validation-trace-phase0.md) | Git 首录 |
| 2026-10-05 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20261005_FULL_SHIPMENT_SAMPLES_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20261005_FULL_SHIPMENT_SAMPLES_CN.md) | 标题 |
| 2026-10-04 | [docs/SHORT_HANDOFF_20261004_FULL_SHIPMENT_HARVEST_SAMPLE_CN.md](SHORT_HANDOFF_20261004_FULL_SHIPMENT_HARVEST_SAMPLE_CN.md) | 文件名 |

### 2026-09

| 日期 | 文档 | 日期依据 |
| --- | --- | --- |
| 2026-09-08 | [docs/TEACHER_STUDENT_CONVERGENCE_CONTRACT_CN.md](TEACHER_STUDENT_CONVERGENCE_CONTRACT_CN.md) | Git 首录 |
| 2026-09-08 | [experiments/README.local.md](../experiments/README.local.md) | Git 首录 |
| 2026-09-06 | [docs/goal-conditioned-no-human-teacher-plan.md](goal-conditioned-no-human-teacher-plan.md) | Git 首录 |
| 2026-09-05 | [docs/DOCUMENTATION_STATUS_20260901_CN.md](DOCUMENTATION_STATUS_20260901_CN.md) | 标题 |
| 2026-09-02 | [docs/FORMAL_TRAINING_BUG_FORECAST_20260901_CN.md](FORMAL_TRAINING_BUG_FORECAST_20260901_CN.md) | 标题 |
| 2026-09-01 | [docs/SHORT_HANDOFF_20260901_FORMAL_TRAINING_EVD326_CN.md](SHORT_HANDOFF_20260901_FORMAL_TRAINING_EVD326_CN.md) | 文件名 |
| 2026-09-01 | [docs/SHORT_HANDOFF_20260901_PRODUCT_EXECUTOR_EVD325_CN.md](SHORT_HANDOFF_20260901_PRODUCT_EXECUTOR_EVD325_CN.md) | 文件名 |

### 2026-08

| 日期 | 文档 | 日期依据 |
| --- | --- | --- |
| 2026-08-31 | [docs/SHORT_HANDOFF_20260831_GEODE_PROCESSING_EVD315_CN.md](SHORT_HANDOFF_20260831_GEODE_PROCESSING_EVD315_CN.md) | 文件名 |
| 2026-08-31 | [docs/SHORT_HANDOFF_20260831_QUEST_CANCELLATION_EVD316_CN.md](SHORT_HANDOFF_20260831_QUEST_CANCELLATION_EVD316_CN.md) | 文件名 |
| 2026-08-31 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260831_ADVENTURE_GUILD_REWARD_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260831_ADVENTURE_GUILD_REWARD_CN.md) | 文件名 |
| 2026-08-31 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260831_MASTERY_CLAIM_EVD319_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260831_MASTERY_CLAIM_EVD319_CN.md) | 文件名 |
| 2026-08-31 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260831_PLAYER_EMOTE_EVD320_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260831_PLAYER_EMOTE_EVD320_CN.md) | 文件名 |
| 2026-08-31 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260831_PRIZE_TICKET_REWARD_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260831_PRIZE_TICKET_REWARD_CN.md) | 文件名 |
| 2026-08-31 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260831_TAILORING_EVD324_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260831_TAILORING_EVD324_CN.md) | 文件名 |
| 2026-08-30 | [docs/SHORT_HANDOFF_20260830_BOBBER_SELECTION_EVD312_CN.md](SHORT_HANDOFF_20260830_BOBBER_SELECTION_EVD312_CN.md) | 文件名 |
| 2026-08-30 | [docs/SHORT_HANDOFF_20260830_CALICO_JACK_EVD304_CN.md](SHORT_HANDOFF_20260830_CALICO_JACK_EVD304_CN.md) | 文件名 |
| 2026-08-30 | [docs/SHORT_HANDOFF_20260830_CALICO_STATUE_EVD309_CN.md](SHORT_HANDOFF_20260830_CALICO_STATUE_EVD309_CN.md) | 文件名 |
| 2026-08-30 | [docs/SHORT_HANDOFF_20260830_DARTS_EVD306_CN.md](SHORT_HANDOFF_20260830_DARTS_EVD306_CN.md) | 文件名 |
| 2026-08-30 | [docs/SHORT_HANDOFF_20260830_FIELD_OFFICE_DONATION_EVD302_CN.md](SHORT_HANDOFF_20260830_FIELD_OFFICE_DONATION_EVD302_CN.md) | 文件名 |
| 2026-08-30 | [docs/SHORT_HANDOFF_20260830_FIELD_OFFICE_SURVEY_EVD303_CN.md](SHORT_HANDOFF_20260830_FIELD_OFFICE_SURVEY_EVD303_CN.md) | 文件名 |
| 2026-08-30 | [docs/SHORT_HANDOFF_20260830_GARBAGE_RUMMAGE_EVD300_CN.md](SHORT_HANDOFF_20260830_GARBAGE_RUMMAGE_EVD300_CN.md) | 文件名 |
| 2026-08-30 | [docs/SHORT_HANDOFF_20260830_HOME_RENOVATION_EVD301_CN.md](SHORT_HANDOFF_20260830_HOME_RENOVATION_EVD301_CN.md) | 文件名 |
| 2026-08-30 | [docs/SHORT_HANDOFF_20260830_JUKEBOX_SELECTION_EVD313_CN.md](SHORT_HANDOFF_20260830_JUKEBOX_SELECTION_EVD313_CN.md) | 文件名 |
| 2026-08-30 | [docs/SHORT_HANDOFF_20260830_MULTIPLAYER_CHAT_EVD311_CN.md](SHORT_HANDOFF_20260830_MULTIPLAYER_CHAT_EVD311_CN.md) | 文件名 |
| 2026-08-30 | [docs/SHORT_HANDOFF_20260830_MULTIPLAYER_WALLET_EVD310_CN.md](SHORT_HANDOFF_20260830_MULTIPLAYER_WALLET_EVD310_CN.md) | 文件名 |
| 2026-08-30 | [docs/SHORT_HANDOFF_20260830_PLAYER_CUSTOMIZATION_EVD314_CN.md](SHORT_HANDOFF_20260830_PLAYER_CUSTOMIZATION_EVD314_CN.md) | 文件名 |
| 2026-08-30 | [docs/SHORT_HANDOFF_20260830_SLOTS_EVD308_CN.md](SHORT_HANDOFF_20260830_SLOTS_EVD308_CN.md) | 文件名 |
| 2026-08-30 | [docs/SHORT_HANDOFF_20260830_WILD_TREE_PRODUCT_EVD299_CN.md](SHORT_HANDOFF_20260830_WILD_TREE_PRODUCT_EVD299_CN.md) | 文件名 |
| 2026-08-29 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260829_NATIVE_OBJECT_ARCHITECTURE_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260829_NATIVE_OBJECT_ARCHITECTURE_CN.md) | 标题 |
| 2026-08-14 | [docs/AUDIT_STATUS_20260814_CN.md](AUDIT_STATUS_20260814_CN.md) | 标题 |
| 2026-08-12 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260812_BUILDING_CONSTRUCTION_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260812_BUILDING_CONSTRUCTION_CN.md) | 文件名 |
| 2026-08-12 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260812_BUILDING_SKIN_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260812_BUILDING_SKIN_CN.md) | 文件名 |
| 2026-08-12 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260812_MINE_ELEVATOR_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260812_MINE_ELEVATOR_CN.md) | 标题 |
| 2026-08-11 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260811_JUNIMO_EQUIVALENT_QUEST_REWARD_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260811_JUNIMO_EQUIVALENT_QUEST_REWARD_CN.md) | 文件名 |
| 2026-08-11 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260811_MAIL_PROCESSING_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260811_MAIL_PROCESSING_CN.md) | 文件名 |
| 2026-08-11 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260811_PROFESSION_CHOICE_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260811_PROFESSION_CHOICE_CN.md) | 文件名 |
| 2026-08-10 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260810_CRAFTING_QUEST_EVD235_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260810_CRAFTING_QUEST_EVD235_CN.md) | 文件名 |
| 2026-08-10 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260810_JUNIMO_KART_EVD239_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260810_JUNIMO_KART_EVD239_CN.md) | 文件名 |
| 2026-08-10 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260810_SECRET_LOST_ITEM_EVD237_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260810_SECRET_LOST_ITEM_EVD237_CN.md) | 文件名 |
| 2026-08-10 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260810_TYPE11_EVD238_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260810_TYPE11_EVD238_CN.md) | 文件名 |
| 2026-08-09 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260809_CROP_MAINTENANCE_EVD226_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260809_CROP_MAINTENANCE_EVD226_CN.md) | 文件名 |
| 2026-08-07 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260807_ANIMAL_PRODUCTS_EVD222.md](stardewai-full-handoff/SHORT_HANDOFF_20260807_ANIMAL_PRODUCTS_EVD222.md) | 文件名 |
| 2026-08-07 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260807_COMMUNITY_CENTER_EVD225.md](stardewai-full-handoff/SHORT_HANDOFF_20260807_COMMUNITY_CENTER_EVD225.md) | 文件名 |
| 2026-08-07 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260807_MUSEUM_EVD224.md](stardewai-full-handoff/SHORT_HANDOFF_20260807_MUSEUM_EVD224.md) | 文件名 |
| 2026-08-07 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260807_PET_CARE_EVD223.md](stardewai-full-handoff/SHORT_HANDOFF_20260807_PET_CARE_EVD223.md) | 文件名 |
| 2026-08-07 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260807_SHIP_ITEMS_EVD221_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260807_SHIP_ITEMS_EVD221_CN.md) | 文件名 |
| 2026-08-05 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260805_SELL_ITEMS_EVD220_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260805_SELL_ITEMS_EVD220_CN.md) | 文件名 |
| 2026-08-05 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260805_TASK_MACHINE_CAPACITY_STATIC_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260805_TASK_MACHINE_CAPACITY_STATIC_CN.md) | 文件名 |
| 2026-08-05 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260805_VISIT_LOCATION_EVD218_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260805_VISIT_LOCATION_EVD218_CN.md) | 文件名 |
| 2026-08-04 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260804_MACHINE_OUTPUT_ADMISSION_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260804_MACHINE_OUTPUT_ADMISSION_CN.md) | 文件名 |
| 2026-08-04 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260804_SUPPORTED_MACHINE_CAPACITY_LIFECYCLE_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260804_SUPPORTED_MACHINE_CAPACITY_LIFECYCLE_CN.md) | 文件名 |
| 2026-08-04 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260804_SUPPORTED_MACHINE_INPUT_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260804_SUPPORTED_MACHINE_INPUT_CN.md) | 文件名 |
| 2026-08-03 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260803_FISH_POND_ADMISSION_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260803_FISH_POND_ADMISSION_CN.md) | 文件名 |
| 2026-08-03 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260803_GREEN_RAIN_ADMISSION_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260803_GREEN_RAIN_ADMISSION_CN.md) | 文件名 |
| 2026-08-03 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260803_SPAWNED_OBJECT_ADMISSION_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260803_SPAWNED_OBJECT_ADMISSION_CN.md) | 文件名 |
| 2026-08-02 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260802_BOOK_ADMISSION_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260802_BOOK_ADMISSION_CN.md) | 文件名 |
| 2026-08-02 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260802_BUSH_ADMISSION_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260802_BUSH_ADMISSION_CN.md) | 文件名 |
| 2026-08-02 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260802_CRAB_POT_ADMISSION_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260802_CRAB_POT_ADMISSION_CN.md) | 文件名 |
| 2026-08-02 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260802_GINGER_ADMISSION_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260802_GINGER_ADMISSION_CN.md) | 文件名 |
| 2026-08-02 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260802_MINE_REWARD_CHEST_ADMISSION_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260802_MINE_REWARD_CHEST_ADMISSION_CN.md) | 文件名 |
| 2026-08-02 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260802_PANNING_ADMISSION_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260802_PANNING_ADMISSION_CN.md) | 文件名 |
| 2026-08-02 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260802_SKULL_KEY_ADMISSION_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260802_SKULL_KEY_ADMISSION_CN.md) | 文件名 |
| 2026-08-02 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260802_STRUCTURED_POLICY_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260802_STRUCTURED_POLICY_CN.md) | 标题 |
| 2026-08-02 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260802_VOLCANO_ADMISSION_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260802_VOLCANO_ADMISSION_CN.md) | 文件名 |
| 2026-08-01 | [docs/EXECUTOR_SINGLE_PATH_AUDIT_20260801_CN.md](EXECUTOR_SINGLE_PATH_AUDIT_20260801_CN.md) | 标题 |
| 2026-08-01 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260801_POLICY_DATASET_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260801_POLICY_DATASET_CN.md) | 标题 |

### 2026-07

| 日期 | 文档 | 日期依据 |
| --- | --- | --- |
| 2026-07-31 | [catalogs/vanilla-1.6.15/README.md](../catalogs/vanilla-1.6.15/README.md) | Git 首录 |
| 2026-07-31 | [docs/CURRENT_WORK_CN.md](CURRENT_WORK_CN.md) | Git 首录 |
| 2026-07-31 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260731_EXECUTOR_SUBSTRATE_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260731_EXECUTOR_SUBSTRATE_CN.md) | 文件名 |
| 2026-07-30 | [docs/stardewai-full-handoff/mining-transparent-planning-foundation/route-clearance-attribution-audit.md](stardewai-full-handoff/mining-transparent-planning-foundation/route-clearance-attribution-audit.md) | Git 首录 |
| 2026-07-27 | [docs/FORMAL_FULL_TRAINING_READINESS_CN.md](FORMAL_FULL_TRAINING_READINESS_CN.md) | Git 首录 |
| 2026-07-27 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260727_FORMAL_TRAINING_READINESS_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260727_FORMAL_TRAINING_READINESS_CN.md) | 标题 |
| 2026-07-26 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260726_MULTI_CONNECTOR_MACHINE_RELOCATION_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260726_MULTI_CONNECTOR_MACHINE_RELOCATION_CN.md) | 文件名 |
| 2026-07-26 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260726_WORKBENCH_MACHINE_LIFECYCLE_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260726_WORKBENCH_MACHINE_LIFECYCLE_CN.md) | 文件名 |
| 2026-07-25 | [docs/authoritative-knowledge-dictionary.md](authoritative-knowledge-dictionary.md) | Git 首录 |
| 2026-07-25 | [docs/knowledge-artifact-layout.md](knowledge-artifact-layout.md) | Git 首录 |
| 2026-07-23 | [docs/quest-objective-execution-coverage.md](quest-objective-execution-coverage.md) | Git 首录 |
| 2026-07-23 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260723_AUTHORITATIVE_DICTIONARY_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260723_AUTHORITATIVE_DICTIONARY_CN.md) | 标题 |
| 2026-07-23 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260723_QUEST_DROPBOX_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260723_QUEST_DROPBOX_CN.md) | 标题 |
| 2026-07-23 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260723_QUEST_TYPED_EXECUTION_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260723_QUEST_TYPED_EXECUTION_CN.md) | 标题 |
| 2026-07-19 | [docs/FULL_SYSTEM_AUDIT_20260719_CN.md](FULL_SYSTEM_AUDIT_20260719_CN.md) | 标题 |
| 2026-07-19 | [docs/FUTURE_COMPANION_ARCHITECTURE_CN.md](FUTURE_COMPANION_ARCHITECTURE_CN.md) | Git 首录 |
| 2026-07-19 | [docs/machine-demand-window.md](machine-demand-window.md) | Git 首录 |
| 2026-07-19 | [docs/material-inventory-graph.md](material-inventory-graph.md) | Git 首录 |
| 2026-07-19 | [docs/strategy-commitment-ledger.md](strategy-commitment-ledger.md) | Git 首录 |
| 2026-07-18 | [docs/skill-experience-source-audit.md](skill-experience-source-audit.md) | Git 首录 |
| 2026-07-18 | [docs/stardewai-full-handoff/mining-transparent-planning-foundation/mine-reward-chest-slice.md](stardewai-full-handoff/mining-transparent-planning-foundation/mine-reward-chest-slice.md) | Git 首录 |
| 2026-07-17 | [docs/cleanup-review-20260717.txt](cleanup-review-20260717.txt) | 文件名 |
| 2026-07-17 | [docs/cleanup-review-addendum-20260717.txt](cleanup-review-addendum-20260717.txt) | 文件名 |
| 2026-07-17 | [docs/code-structure-audit-20260717.md](code-structure-audit-20260717.md) | 文件名 |
| 2026-07-17 | [docs/stardewai-full-handoff/grandpa-skull-key-acquisition/AUDIT.md](stardewai-full-handoff/grandpa-skull-key-acquisition/AUDIT.md) | Git 首录 |
| 2026-07-16 | [docs/stardewai-full-handoff/mining-transparent-planning-foundation/quarry-golden-scythe-slice.md](stardewai-full-handoff/mining-transparent-planning-foundation/quarry-golden-scythe-slice.md) | Git 首录 |
| 2026-07-16 | [docs/stardewai-full-handoff/mining-transparent-planning-foundation/volcano-dungeon-slice.md](stardewai-full-handoff/mining-transparent-planning-foundation/volcano-dungeon-slice.md) | Git 首录 |
| 2026-07-15 | [docs/candidate-availability-gating-policy.md](candidate-availability-gating-policy.md) | Git 首录 |
| 2026-07-15 | [docs/fishing-transparency-readiness-slice.md](fishing-transparency-readiness-slice.md) | Git 首录 |
| 2026-07-15 | [docs/full-chain-task-planning-roadmap.md](full-chain-task-planning-roadmap.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/evidence.md](stardewai-full-handoff/evidence.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/field-dictionary.md](stardewai-full-handoff/field-dictionary.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/grandpa-direction-compile-chain/evidence.md](stardewai-full-handoff/grandpa-direction-compile-chain/evidence.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/grandpa-direction-compile-chain/risk.md](stardewai-full-handoff/grandpa-direction-compile-chain/risk.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/grandpa-direction-compile-chain/test-results.txt](stardewai-full-handoff/grandpa-direction-compile-chain/test-results.txt) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/grandpa-direction-compile-chain/transparency-coverage.md](stardewai-full-handoff/grandpa-direction-compile-chain/transparency-coverage.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/grandpa-direction-compile-chain/WORKER_NOTES.md](stardewai-full-handoff/grandpa-direction-compile-chain/WORKER_NOTES.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/grandpa-direction-daily-candidate-binding/AUDIT.md](stardewai-full-handoff/grandpa-direction-daily-candidate-binding/AUDIT.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/grandpa-direction-daily-candidate-binding/evidence.md](stardewai-full-handoff/grandpa-direction-daily-candidate-binding/evidence.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/grandpa-direction-daily-candidate-binding/risk.md](stardewai-full-handoff/grandpa-direction-daily-candidate-binding/risk.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/grandpa-direction-daily-candidate-binding/test-results.txt](stardewai-full-handoff/grandpa-direction-daily-candidate-binding/test-results.txt) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/grandpa-direction-daily-candidate-binding/transparency-coverage.md](stardewai-full-handoff/grandpa-direction-daily-candidate-binding/transparency-coverage.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/grandpa-direction-daily-candidate-binding/WORKER_NOTES.md](stardewai-full-handoff/grandpa-direction-daily-candidate-binding/WORKER_NOTES.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/grandpa-full-shipment-contribution/AUDIT.md](stardewai-full-handoff/grandpa-full-shipment-contribution/AUDIT.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/grandpa-full-shipment-contribution/evidence.md](stardewai-full-handoff/grandpa-full-shipment-contribution/evidence.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/grandpa-full-shipment-contribution/risk.md](stardewai-full-handoff/grandpa-full-shipment-contribution/risk.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/grandpa-full-shipment-contribution/test-results.txt](stardewai-full-handoff/grandpa-full-shipment-contribution/test-results.txt) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/grandpa-full-shipment-contribution/transparency-coverage.md](stardewai-full-handoff/grandpa-full-shipment-contribution/transparency-coverage.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/grandpa-full-shipment-contribution/WORKER_NOTES.md](stardewai-full-handoff/grandpa-full-shipment-contribution/WORKER_NOTES.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/hard-rule-transparency-audit.md](stardewai-full-handoff/hard-rule-transparency-audit.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/mining-transparent-planning-foundation/evidence.md](stardewai-full-handoff/mining-transparent-planning-foundation/evidence.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/mining-transparent-planning-foundation/risk.md](stardewai-full-handoff/mining-transparent-planning-foundation/risk.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/mining-transparent-planning-foundation/test-results.txt](stardewai-full-handoff/mining-transparent-planning-foundation/test-results.txt) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/mining-transparent-planning-foundation/transparency-coverage.md](stardewai-full-handoff/mining-transparent-planning-foundation/transparency-coverage.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/mining-transparent-planning-foundation/WORKER_NOTES.md](stardewai-full-handoff/mining-transparent-planning-foundation/WORKER_NOTES.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/quest-transparent-planning-foundation/ACCEPTANCE.md](stardewai-full-handoff/quest-transparent-planning-foundation/ACCEPTANCE.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/quest-transparent-planning-foundation/APPROVED_QUEST_AUDIT.md](stardewai-full-handoff/quest-transparent-planning-foundation/APPROVED_QUEST_AUDIT.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/quest-transparent-planning-foundation/AUDIT.md](stardewai-full-handoff/quest-transparent-planning-foundation/AUDIT.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/quest-transparent-planning-foundation/CONTROLLER_REVIEW.md](stardewai-full-handoff/quest-transparent-planning-foundation/CONTROLLER_REVIEW.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/quest-transparent-planning-foundation/evidence.md](stardewai-full-handoff/quest-transparent-planning-foundation/evidence.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/quest-transparent-planning-foundation/MERGE_NOTES.md](stardewai-full-handoff/quest-transparent-planning-foundation/MERGE_NOTES.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/quest-transparent-planning-foundation/risk.md](stardewai-full-handoff/quest-transparent-planning-foundation/risk.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/quest-transparent-planning-foundation/TASK.md](stardewai-full-handoff/quest-transparent-planning-foundation/TASK.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/quest-transparent-planning-foundation/test-results.txt](stardewai-full-handoff/quest-transparent-planning-foundation/test-results.txt) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/quest-transparent-planning-foundation/transparency-coverage.md](stardewai-full-handoff/quest-transparent-planning-foundation/transparency-coverage.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/quest-transparent-planning-foundation/WORKER_NOTES.md](stardewai-full-handoff/quest-transparent-planning-foundation/WORKER_NOTES.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/risk.md](stardewai-full-handoff/risk.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/social-transparent-planning-foundation/ACCEPTANCE.md](stardewai-full-handoff/social-transparent-planning-foundation/ACCEPTANCE.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/social-transparent-planning-foundation/APPROVED_SOCIAL_AUDIT.md](stardewai-full-handoff/social-transparent-planning-foundation/APPROVED_SOCIAL_AUDIT.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/social-transparent-planning-foundation/AUDIT.md](stardewai-full-handoff/social-transparent-planning-foundation/AUDIT.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/social-transparent-planning-foundation/CONTEXT.md](stardewai-full-handoff/social-transparent-planning-foundation/CONTEXT.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/social-transparent-planning-foundation/CONTROLLER_REVIEW.md](stardewai-full-handoff/social-transparent-planning-foundation/CONTROLLER_REVIEW.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/social-transparent-planning-foundation/evidence.md](stardewai-full-handoff/social-transparent-planning-foundation/evidence.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/social-transparent-planning-foundation/risk.md](stardewai-full-handoff/social-transparent-planning-foundation/risk.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/social-transparent-planning-foundation/TASK.md](stardewai-full-handoff/social-transparent-planning-foundation/TASK.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/social-transparent-planning-foundation/test-results.txt](stardewai-full-handoff/social-transparent-planning-foundation/test-results.txt) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/social-transparent-planning-foundation/transparency-coverage.md](stardewai-full-handoff/social-transparent-planning-foundation/transparency-coverage.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/social-transparent-planning-foundation/WORKER_NOTES.md](stardewai-full-handoff/social-transparent-planning-foundation/WORKER_NOTES.md) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/test-results.txt](stardewai-full-handoff/test-results.txt) | Git 首录 |
| 2026-07-15 | [docs/stardewai-full-handoff/transparency-coverage.md](stardewai-full-handoff/transparency-coverage.md) | Git 首录 |
| 2026-07-15 | [docs/STARDewAI_OWLAI_HANDOFF_CN.md](STARDewAI_OWLAI_HANDOFF_CN.md) | Git 首录 |
| 2026-07-15 | [evidence.md](../evidence.md) | Git 首录 |
| 2026-07-15 | [risk.md](../risk.md) | Git 首录 |
| 2026-07-15 | [transparency-coverage.md](../transparency-coverage.md) | Git 首录 |
| 2026-07-15 | [WORKER_NOTES.md](../WORKER_NOTES.md) | Git 首录 |
| 2026-07-14 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260714_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260714_CN.md) | 标题 |
| 2026-07-14 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260714_SOCIAL_RUNTIME_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260714_SOCIAL_RUNTIME_CN.md) | 标题 |
| 2026-07-13 | [docs/stardewai-full-handoff/SHORT_HANDOFF_20260713_CN.md](stardewai-full-handoff/SHORT_HANDOFF_20260713_CN.md) | 标题 |
| 2026-07-06 | [docs/FULL_SYSTEM_COMPLETION_PLAN_CN.md](FULL_SYSTEM_COMPLETION_PLAN_CN.md) | Git 首录 |
| 2026-07-06 | [docs/perfect-executor-gameplay-audit.md](perfect-executor-gameplay-audit.md) | Git 首录 |
| 2026-07-06 | [docs/real-game-training-session-gate.md](real-game-training-session-gate.md) | Git 首录 |
| 2026-07-05 | [docs/grandpa-evaluation-goal.md](grandpa-evaluation-goal.md) | Git 首录 |
| 2026-07-05 | [docs/grandpa-training-sample-adapter.md](grandpa-training-sample-adapter.md) | Git 首录 |
| 2026-07-05 | [docs/mock-model-output-loop.md](mock-model-output-loop.md) | Git 首录 |
| 2026-07-05 | [docs/phase-1h-world-progress-completion.md](phase-1h-world-progress-completion.md) | Git 首录 |
| 2026-07-05 | [docs/phase-1i-npc-friendship-schedule.md](phase-1i-npc-friendship-schedule.md) | Git 首录 |
| 2026-07-05 | [docs/phase-1j-menu-specific-read.md](phase-1j-menu-specific-read.md) | Git 首录 |
| 2026-07-05 | [docs/phase-2-world-model-input.md](phase-2-world-model-input.md) | Git 首录 |
| 2026-07-05 | [docs/runtime-test-harness.md](runtime-test-harness.md) | Git 首录 |
| 2026-07-05 | [docs/small-model-action-queue-executor.md](small-model-action-queue-executor.md) | Git 首录 |
| 2026-07-05 | [docs/training-hardware-assessment.md](training-hardware-assessment.md) | Git 首录 |
| 2026-07-04 | [docs/full-transparent-runtime-target.md](full-transparent-runtime-target.md) | Git 首录 |
| 2026-07-04 | [docs/phase-1a-correction.md](phase-1a-correction.md) | Git 首录 |
| 2026-07-04 | [docs/phase-1b-farm-read-slice.md](phase-1b-farm-read-slice.md) | Git 首录 |
| 2026-07-04 | [docs/phase-1c-current-location-read-slice.md](phase-1c-current-location-read-slice.md) | Git 首录 |
| 2026-07-04 | [docs/phase-1d-npc-read-slice.md](phase-1d-npc-read-slice.md) | Git 首录 |
| 2026-07-04 | [docs/phase-1e-progress-read-slice.md](phase-1e-progress-read-slice.md) | Git 首录 |
| 2026-07-04 | [docs/phase-1f-menu-read-slice.md](phase-1f-menu-read-slice.md) | Git 首录 |
| 2026-07-04 | [docs/phase-1g-modded-state-read-slice.md](phase-1g-modded-state-read-slice.md) | Git 首录 |
| 2026-07-04 | [docs/smapi-runtime-acceptance.md](smapi-runtime-acceptance.md) | Git 首录 |
| 2026-07-04 | [docs/worker-transparent-runtime-loop.md](worker-transparent-runtime-loop.md) | Git 首录 |
| 2026-07-03 | [docs/math-model.md](math-model.md) | Git 首录 |
| 2026-07-03 | [docs/Stardew_Valley_AI_Companion_Blueprint_AI_Only_CN_v0.2.txt](Stardew_Valley_AI_Companion_Blueprint_AI_Only_CN_v0.2.txt) | Git 首录 |
| 2026-07-03 | [docs/transparent-read-plan.md](transparent-read-plan.md) | Git 首录 |
| 2026-07-03 | [docs/transparent-read-slicing-plan.md](transparent-read-slicing-plan.md) | Git 首录 |
| 2026-07-03 | [README.md](../README.md) | Git 首录 |

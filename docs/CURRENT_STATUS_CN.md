# StardewAI 当前状态与施工顺序（Canonical）

> 本文件是**当前人类可读状态入口**。机器可读 report / self-test / evidence index 仍是事实源；本文件负责把不同分母分开显示并规定开发顺序。
>
> 下方其他带日期的 handoff、readiness、plan 章节均保留作历史证据。除非其数字在本文件中再次出现，否则**不得把历史数字当作当前状态**。

## 1. 当前状态面板

基于当前主线状态：

| 指标 | 当前值 | 含义 |
| --- | ---: | --- |
| Goal Method executable criteria | **4/19** | root method 已达到 `executable_frontier` 的 criterion |
| Split-complete Teacher/native evidence | **4/19** | train/validation/test 两条证据通道完整的 criterion |
| Promotion-grade / CoverageGateReady intersection | **2/19** | 同时满足 executable + 正式 Teacher/native 三分区证据 |
| Coverage-ready root methods | **1/11** | 11 个战略 root method 中正式 coverage-ready 的数量 |
| Full Shipment runtime strata | **当前权威 26/26** | 固定 26 层均由当前源码原生重采并通过严格索引，**不是全局 /19** |
| Money production evidence cells | **0/18** | 6 个 Money criterion × 3 partitions；hermetic fixture 不计 production |
| Formal product training | **false** | 未通过完整正式训练准入 |

当前两个 `CoverageGateReady` criterion 固定为：

- `community_center_access_or_completion`
- `community_center_accessible_bonus`

### 不允许再使用的模糊短写

禁止单独写：

- “当前 coverage 是 4/19”
- “当前进度是 2/19”
- “Full Shipment 26/26，所以全局完成”
- “Money 已完成”

必须写完整指标名，例如：

```text
Executable criteria:       4/19
Split-complete evidence:   4/19
CoverageGateReady:         2/19
Root methods ready:        1/11
Full Shipment runtime:    current authority 26/26
Money production cells:    0/18
Formal training:           false
```

## 2. 已完成并冻结的施工单位

### Full Shipment runtime evidence

固定 `26/26` runtime strata 已由 2026-10-15 至 2026-10-16 的当前源码全部原生重采，分母不得扩张。每层均绑定当前 requirement/lowering、Master Angler 窗口、路线计时、精确 route occurrence、原生动作、fresh 终态、reservation settlement 与独立 rollout proof；共享运输箱和睡眠结算前缀也通过重建。最终 `full_shipment_runtime_sample_evidence_index.v1` 为 `verified=26 / missing=0 / shared_shipping=true`，不再引用漂移的历史执行收据。

PR #298 随后补回 deferred pickup 的精确需求数量绑定。该共享 correctness 变更没有触发无差别重跑：wild-tree Sap 路线重新原生执行并证明只拾取需求量 `1`、而非保证产量 `6`；其余四条受影响的 `1:1` 路线从当前源码重建 proof。新的严格 26 层索引仍为 `26/26`、缺失 `0`，SHA-256 为 `e30edb0e6aca07deca484c8b8f1a4437eef68cb01120f13074329d7e2e0830b1`。

收口过程中发现并修复了一个跨路径的可重放性硬伤：PowerShell 将后端原始 ledger 中的历史 `DateTimeOffset` 从 `+00:00` 重写为本机 `+08:00`，虽然时刻相同，却破坏字节级重放。标准采证、断点恢复、规划重建和商店支持链共七个 ledger 提取点已统一从原始响应提取 JSON，不再经过对象往返序列化。严格守卫因此保持不放宽，并在真实防风草提交/结算及后续全部标准层中通过。

`26/26` 只关闭 Full Shipment 运行签名层，不会把全局 Goal Method / formal coverage 提升到 `19/19`。正式训练仍因其他 criterion 和生产分区缺口失败关闭。

除非出现以下情况，否则**不得主动重跑全部 26 层**：

1. 权威 route/source contract 真实变化；
2. game/mod profile 或版本变化；
3. 共享 correctness 修复明确影响某个既有 stratum；
4. 正式 promotion review 要求 force-full revalidation。

正常共享代码修复只重验**受影响的代表 strata**，再由 production evidence index 重建确认。

## 3. 当前唯一 Coverage 主施工单位：Money / Revenue

2026-10-21 更新：`money-crop-native-20261021-pricefix-v10` 已形成第一条完整原生作物生产段。它从不可变源存档执行 `21` 个购种过渡、精确购买 `5` 份 `(O)474`，随后完成场地准备、播种、条件浇水、收获、`5` 次单件投箱、回家和原生过夜结算。整条 shared lifecycle 为 `48` 条连续 transition、`12` 个实际作物批次。checkpoint v12 为 `verified=6 / pending=0`；终局 recurrence 从 total day `18` 推进到 `19`（春 20 日 06:00），`money 50 -> 925`、`totalMoneyEarned 0 -> 875`。源存档与隔离 Mod 哈希均未漂移，最终透明状态哈希与 recurrence 完全一致。

这份成功结果仍不增加 `6 x 3` 分子。它只获得 `875g`，未跨越最低 `50,000g` Money criterion；生产段尚需用统一结果收据绑定已有 Teacher 决策、完整 checkpoint、终端 recurrence、最终快照与最终隔离存档树，然后建立 outcome-backed Teacher comparison。之后从同一最终隔离存档继续下一段 Teacher 决策，直到累计真实原生结果跨越阈值，才进入既有 criterion-local join。因此状态面板继续严格保持 `Money=0/18`、`formal_product_training_authorized=false`。

同日终局运输箱契约已经升级为单波 `money_revenue_crop_terminal_settlement_proof.v3` 与多波 `money_revenue_crop_shared_shipping_settlement_proof.v2`。两者都要求投箱前路线、逐件原生投递、投箱后资源中性回家路线和唯一原生睡眠四段连续证据；不能在运输箱旁直接构造睡眠。`v10` 已用单波分支真实通过该边界，但这仍只是生产段 outcome，不是阈值 cell。

`money_revenue_crop_production_teacher_request.v1 -> money_revenue_crop_production_decision_request.v2 -> money_revenue_crop_production_decision_receipt.v2` 是每个连续生产段的正式 Teacher 起点。Teacher 从同源透明状态生成完整候选分母并确定性选择；运行层继续只复用既有 ledger、obligation plan、run plan、编译器和执行器。下一施工单位不得复制这些系统，只补它们之间缺失的结果身份和连续段接线。

当前 coverage 主线固定为：

```text
earn_money
  -> Revenue authoritative denominator
  -> terminal/rebound identity
  -> native execution proof
  -> reservation/ledger continuity
  -> exact Teacher comparison admission
  -> criterion-local native outcome join
  -> 6 criteria × 3 partitions production evidence
```

Money 的基础设施、hermetic fixtures、comparison/native channel separation 已建立，但 production 18 格尚未闭合，因此**不得提前把 Money 六项计入 CoverageGateReady**。

Money 原生结果表示现已从 sale-only 扩展为统一双入口：隔离的 ShopMenu sale episode，或完整的 Revenue trajectory receipt。后者复用权威 request、可选 acquisition/processing rollout、terminal proof 与 sale/shipping recurrence，并由 corpus v4 统一投影；criterion join 必须匹配 selected concrete alternative、完整 Revenue prefix、terminal evidence 和原生 artifacts。该结构关闭的是 shipping/rebound 的表示缺口，不产生真实生产证据，故 `Money production cells` 仍为 `0/18`。

Money batch 更新链可在调用方显式提供现有 production coverage request 模板时，通过独立 manifest 重放 corpus 并原子替换其中全部旧 Money source；其他 source 原样保留，source ID 冲突失败关闭。默认不提供模板时不会改写 coverage request。该接线仍不改变 `0/18`，但避免 18 格闭合后再手工拼 JSON。

同一轮 batch 更新现已收敛为单次生产包调用：原生证据只在 canonical corpus 构建阶段深验一次，work plan 与可选 coverage attachment 复用同一进程内已验证对象，并重新绑定落盘路径、SHA-256 与行级不变量。独立调用和下游 coverage/reconciliation 的完整重放边界不变；这减少确定存在的重复回放，但不构成跨运行缓存，也不改变 `0/18` 或正式训练状态。

当前 work plan 还会把同一 `18` 格分母投影为最少误导的采集波次。生产 corpus 为空时是 `3` 个条件性原生波次，而不是 18 次必需启动：train、validation、test 各一波；单波能否同时跨越六个阈值必须由真实 source checkpoint 和 Revenue candidate 证明，不能由计划器假定。已有 native row 的比较工作按 row 聚合，但每个 criterion 的 exact comparison admission 仍独立，且不同优选不得合并到同一个 seed。该变化只减少调度重复，不增加任何生产分子。

波次执行不会死守计划中的最高阈值。`build-money-threshold-native-outcome-wave-selection` 会在实时、完整的 Revenue denominator 上选择当前能够跨越的最高缺失阈值，再复用原单阈值选择器取得最小充分售卖；真实结果跨过多少格就只关闭多少格，其余格由刷新后的 work plan 保留。`Invoke-MoneyThresholdNativeCollectionWave.ps1` 只负责校验波次、调用既有隔离 runner、重建 batch 并确认至少一格真实关闭，不新增第二套执行器或证据捷径。

首轮空生产状态可由 `Initialize-MoneyThresholdProductionWorkPlan.ps1` 直接生成固定 `6 × 3 = 18` 格计划；无 corpus 时必须得到 `0` 个完整格、`3` 个原生波次和 `0` 个 comparison 波次。该入口只调用既有 work-plan builder，不创建空 corpus、证据或训练授权。为防止空初始化把真实进度降回 `0/18`，目标目录一旦存在 canonical corpus、不可读/非规范计划或任一非零完成格，初始化器会在首次写入前失败关闭；后续只有 batch updater 能刷新 canonical production work plan。

2026-10-16 已在 E 盘本地证据区初始化上述正式计划，机器结果为 `0/18 + 3 native waves + 0 comparison waves`。首次 train 诊断取得真实 fresh full snapshot，但权威 Revenue denominator 按设计拒绝生产采集：`earn_money` dependency expansion 仍有 fresh-save 百万收入下界日程、机会成本 Teacher 标签和未来日期作物生产承诺三个 blocker。因此没有任何 Money cell 被伪计完成。`farm.maintain_crops` 的五门证据早已闭合，但它属于编译器所有的确定性机械支持，不得为了消除阻塞而进入 Student 策略标签；Money 图现在以 `deterministic_transition` 复用它，只有原生收获回执可以进入待出售库存。runner 现会在复制存档或启动进程前检查静态准入；未完成时约一秒失败关闭。真正运行时只加载 TransparentBridge 与 RuntimeTestHarness，并对首次世界快照强制刷新，避免标题界面空缓存和无关模组污染。

comparison 侧现由 `Initialize-MoneyRevenueComparisonWave.ps1` 以 `partition + source_row_id` 从 work plan 精确定位 corpus row 和 native run summary，并生成带全路径哈希、source identity 与缺失 criterion 集的 handoff。completion 可只处理 handoff 列出的缺口，并把新 admission 与已有 index 按 `run_id + criterion_id` 累积合并，防止局部补证据时丢掉旧分子。该接线仍要求后续 acquisition rollout、strategy ledger 与 terminal proof 全部真实存在，不会凭 handoff 生成 Teacher 标签。

counterfactual catalog 之后现使用 `money_revenue_terminal_proof_collection_plan.v2`。它重建并逐字节核对 comparison plan、concrete denominator 与 request catalog，显式绑定请求期望的 run/partition、骨架分母路径与双哈希、具体分母路径与双哈希，并以 concrete request 数量而非 skeleton 数量建立工作分母。每个权威 concrete request 恰好映射为一个 terminal-proof 工作项；只有与 source state、candidate、物品槽、数量、单价和商店都完全一致的既有原生 sale episode 可以进入离线复用通道，其余 shop/shipping alternative 必须原生采集。该计划明确列出账本边界、资源中性终端路线、原生收入动作、recurrence 和终端证明阶段，但自身不是 terminal proof index、Teacher 标签或 production evidence；Money 仍为 `0/18`。

`Initialize-MoneyRevenueTerminalProofCollection.ps1` 现可无游戏物化上述唯一离线复用项：after boundary 继续来自已验证 sale episode，before boundary 则严格使用 request/verifier 规定的 terminal snapshot，并同时核对 request、work item 与实物 SHA-256。所有 fragment 进入 `money_revenue_terminal_proof_collection_checkpoint.v1` 前都由既有 terminal execution verifier 完整重放；checkpoint 允许保存真实部分进度，但只有 verified request ID 集与完整 denominator 相等时才输出 completion 可消费的 `money_revenue_terminal_proof_index.v1`。其余反事实分支仍为原生采集 blocker，未增加 Money production 分子。

未来作物承诺现在具备独立的精确采证计划和可恢复 checkpoint。正式 Teacher 决策器从实时透明数据建立作物候选分母和 bounded Farm 布局；玩家还在 `FarmHouse` 时，`FarmReadAdapter` 也通过唯一共享 `FarmSitePreparationProjector` 对实际 Farm 做目的受限读取，不复制场地分类逻辑。既有种植账本从透明原生作物目录记录种子身份、逐日浇水日期、全部收获波次、保守最小产量和零品质毛收入；`money_revenue_crop_production_obligation_plan.v2` 会重新核验这些值，拒绝已经过去的种植日，把同承诺已有预留与当前未预留供给分开审计，并只将未预留余量按承诺顺序唯一分配。种子预留 proof v2 必须从计划基准 ledger 原子重放 commit request/result，手写最终账本、倒退日期或同日倒退 tick 均失败关闭。布局 proof 仍必须在精确 planting-day 快照上逐格核对 Farm 室外空 HoeDirt、原生种植许可和不晚于承诺上界的调整后生长周期。购种 shortfall 由 `money_revenue_crop_seed_acquisition_proof.v2` 对既有 `economy.buy_supplies` 的连续单份原生执行切片进行严格适配；逐日容量由 `money_revenue_crop_daily_capacity_proof.v1` 在整份计划上聚合全部承诺，逐日重放 shared BFS、编译器时序、时间、体力与精确水壶存量，需补水但没有建模路线时失败关闭。`money_revenue_crop_native_lifecycle_proof.v3` 进一步复用既有播种/浇水/收获/睡眠队列与统一回执验证器，但所有承诺现在必须共享一条连续世界状态轨迹和每夜唯一睡眠；睡醒后的 `FarmHouse -> Farm` crop-ingress 和睡前的 `Farm -> home` sleep-ingress 都必须作为同轨迹中的显式 supporting route，由既有移动/连接器 primitive 和统一回执完成，并证明同日、资源中性且确实抵达声明地点。雨天不会生成冗余浇水；若 aggregate daily-capacity 要求当天 Farm 边界快照，允许一条资源中性的 route-only ingress。每个作物动作队列绑定唯一承诺布局，`action_commitment_id` 保留逐承诺归因，并从 `farm.crops` 同时重放全部活跃地块的反编译 `Crop.newDay` 阶段机和再生倒计时。`money_revenue_crop_output_retention_proof.v1` 再按该归因与波次选择连续收获队列，以透明玩家库存中精确物品的净增核对保守最小产量；地面 debris 不算留存，随机超产也不抬高保守收入下界。`money_revenue_crop_terminal_settlement_proof.v2` 允许 ShopMenu sale 或 shipping settlement 二选一：商店分支继续复用共享资源中性路线；运输箱分支要求每个保守单位各有一次既有原生单件投递、连续累计待结算值并以唯一原生睡眠完成 Revenue recurrence。`money_revenue_crop_settlement_capacity_proof.v1` 又把全计划中同一收获日的全部承诺/波次合并，以共享维护 BFS 终点、运输箱站位、投递时序和 recovery cutoff 证明尾程加全部单件投递仍可在当日完成。`money_revenue_crop_shared_shipping_settlement_proof.v1` 进一步独立重建整套支持，在同一 fresh 终端库存上按容量顺序串起多个波次的全部单件原生投递，并只允许一次共享原生睡眠结算；旧单波与新共享模式复用同一投箱批次及 recurrence 校验器。单独留存和容量支持均不关闭 `output-retention-terminal:*`，只有单波完整 proof 或共享 proof 覆盖的精确 request ID 才进入分子。fragment index 已为 v13、checkpoint 已为 v12。`money_revenue_crop_native_production_run_plan.v2` 从权威源快照当天开始，把承诺和不重叠布局意图一次展开成连续日期；种植日前只允许唯一原生睡眠，之后按固定动作顺序、条件浇水、有限批次和唯一睡眠推进，运行时无需为机械动作反复调用模型。每个承诺布局现在还必须落在单个 `32×32` bounded Farm 投影中；播种日会先复用既有 site-preparation plan，把清障、资源块破坏和锄地按一次一候选执行并在每次状态变化后 fresh rebind，全部目标格透明确认为 HoeDirt 后才捕获布局/容量边界和播种。`Invoke-RuntimeMoneyCropNativeProduction.ps1` 已把该 plan 接入隔离源存档、精确双 Mod allowlist、现有 action/plan compiler、RuntimeTestHarness 原生执行与统一回执落盘，并可物化 layout、daily-capacity、shared lifecycle 和 retention fragment；跨图编译现在保留候选身份。运行器只从 `money_crop_native_production_input_manifest.v1` 取得源存档、Teacher lineage、obligation plan、layout intents 和 run plan，并在任何构建或进程启动前验证全部输入及血缘；运行前的 `money_crop_native_source_preflight.v1` 还要求首次 fresh full snapshot 的 state hash 和总日数同时匹配计划源边界。尚未物化首份 production Teacher v2 artifact 或执行首次真实原生采集；terminal settlement 未接入此运行壳层。future-proof、fresh-save schedule 和 formal training 三个 gate 固定为 false，因此 Money 仍是 `0/18`。下一切片先捕获 fresh full + bounded Farm snapshots、生成首份权威生产输入并完成首次隔离运行，再在同一轨迹接入 shipping/shop terminal settlement，不得手写生产 JSON，也不得另写第二套作物候选、规划器、动作执行器。

上段末尾的“尚未首次采集”是 2026-10-18 的历史描述，已由本节顶部 2026-10-21 `v10` 完整结果取代。当前 crop checkpoint 已无 pending request；真实剩余项是生产段结果收据、outcome-backed Teacher comparison、连续段推进和阈值跨越后的 criterion-local join。

上段中的 native run plan v1 已由 `money_revenue_crop_native_production_run_plan.v2` 取代。v2 把每个非零种子 shortfall 列为显式前置义务；运行器不直接改库存或货币，而是复用 `economy.buy_supplies -> DailyPlanCompiler -> ActionQueueCompiler -> RuntimeTestHarness`，以一次一份、每次 fresh snapshot 的有界切片精确补齐。候选身份在一次 continuation 内锁定，超买、错误增量、无透明状态进展或越过 deadline 均失败关闭。`money_revenue_crop_seed_acquisition_proof.v2` 已把这组原生切片适配为正式 fragment：逐回执验证连续世界状态、资源中性路线、种子/商店/stock row/输出 stack/quality/价格/单份数量绑定，以及精确库存和 money 净变化；累计增量必须恰好等于计划 shortfall。运行器已将 proof 路径写入 checkpoint，v22 已产生正式原生 fragment；Money 分子仍因 terminal/Teacher/join 未完成而保持不变。

场地准备组合现已落地：透明桥的 `farm_layout` profile 仅投影调用方给定的最多 `32×32` Farm 区域；计划器把精确布局逐格绑定到现有清障、资源块破坏、BFS 站位与锄地候选。任何清障后续都必须重新取得 fresh snapshot 才能绑定锄地，计划不跨状态边界臆测第二步。局部读取现使用 overflow-safe 边界与不会回绕的遍历，资源团块站位复用共享 BFS，锄地在体力缺失或 `<=0` 时失败关闭。该计划本身不是 planting-layout proof，也没有增加 Money production 分子。

crop-production checkpoint v12 只认可可从精确请求重建的 fragment。种子预留继续要求 fresh snapshot 上由共享原子提交服务重放完整 reservation；购种 shortfall proof 要求现有购买编译执行链在截止日前以连续的资源中性支持阶段和单份购买切片精确补足，逐片回执及累计净增都必须成立；逐日容量 proof 要求每项承诺有精确布局，并对整份计划的全部活动日逐日绑定同存档 fresh snapshot；生命周期 proof 要求整计划共享唯一连续状态链、显式资源中性睡醒路线、逐队列唯一承诺归因、逐日不重不漏的地块动作和精确原生跨日阶段转换；输出留存 proof 要求逐波完整收获动作与精确玩家库存净增，但只作为 terminal settlement 的支持片段。原生商店组合 proof 要求资源中性路线、精确物品与保守数量全部售出、原生收入增量及 recurrence 完整一致；单波原生运输箱 proof 要求单件投递数等于保守产量、每次库存/箱内/待结算值精确递增，并以唯一原生睡眠完整结算。同日多波共享 proof 则要求所有波次与聚合容量顺序一致、fresh 终端库存覆盖总保守数量、待结算值跨波次从零连续累积，并只执行一次共享睡眠结算。运输箱 fragment 仍必须由唯一整计划结算容量 proof 支持；该 proof 自身不增加 request numerator。少投或少卖、错误物品、错误计划边界、缺日、重复地块、跨承诺混合队列、复制世界轨迹、隐藏传送、路线夹带资源变化、不可达、超时、体力不足、缺水、阶段跳变、仅有地面 debris、预载结算值、混合终端模式、缺失聚合容量、重复/乱序波次或额外 continuation 均失败关闭。正式购种/容量/生命周期/留存/结算 fragment 尚未采集，因此 Money 仍为 `0/18`。

### production 前 correctness debt 已清零（2026-10-18 审计）

#307、#308、#309、#310 与 #311 均已按各自验收边界收口；当前没有已知 P0/P1 review issue 阻止 Money 主线继续。该结论不增加证据分子：`CoverageGateReady=2/19`、`Money=0/18`、`formal_product_training_authorized=false` 仍保持不变。购种 shortfall、逐日容量、原生生命周期、逐波输出留存、原生商店/运输箱结算、跨承诺/波次的同日聚合容量及共享过夜结算证明适配器已实现；下一施工单位是真实 native collection runner，随后进行 terminal proof completion、Teacher comparison 和 criterion-local join。

#307 已关闭：共享 raw JSON helper 现在在 Windows PowerShell 5.1 与 PowerShell 7 上通过同一严格词法扫描原样截取根属性，不再依赖 `System.Text.Json`，也不对 ledger 值做对象往返序列化。真实双壳层测试覆盖嵌套对象、转义字符串、时区小数以及缺失/重复属性失败关闭；七条 ledger 调用路径仍统一复用该 helper。

#308 已关闭：`money_threshold_native_outcome_run_summary.v2` 持久化隔离 Mods 根目录、运行前后根内容哈希，以及 TransparentBridge / RuntimeTestHarness 各自的目录名、UniqueID、版本、manifest 哈希和整目录哈希。回放会重新枚举真实目录并复核全部身份；缺失、额外、漂移、错配和历史 v1 summary 均失败关闭。PowerShell 5.1 与 C# 的目录哈希实现由 hermetic 跨进程测试锁定一致。

#309 已关闭：terminal-proof collection request/plan 升级为 v2，明确绑定期望 run/partition、骨架与 concrete denominator 的路径和哈希，并以 concrete catalog 校验 request 数量。离线复用 sale episode 的 before capture 改用 request/verifier 的 terminal snapshot；错误 catalog、run、partition、boundary 和部分 fragment 均失败关闭，且不会创建 Teacher label 或 production cell。

## 4. 单项闭环施工原则（WIP = 1）

从本文件生效后，Goal Method / formal-coverage 主线实行：

```text
同一时间只允许 1 个 primary root method / progression family 作为 coverage 施工单位。
当前：Money / Revenue
```

在当前项达到 Exit Gate 前，不启动 Friendship、Skill、Museum/Rusty、Marriage、Master Angler 等下一项的新的 coverage expansion。

### 允许的中断

仅以下情况可以临时中断当前项：

- P0 / P1 跨域 correctness 缺陷；
- 当前项被一个共享基础设施硬 blocker 阻塞；
- 已测量确认的性能热点使当前项无法在可接受反馈时间内继续。

中断处理完成后必须**回到同一 primary item**，不得顺势切换到另一个 Goal Method。

### 不算“并行开新项”的工作

以下工作可与当前项并行，但只能服务当前项或全局安全：

- review finding 修复；
- CI / source guard / deterministic-build correctness；
- 共享 Planner / Compiler / Executor / Verifier 的 P0/P1 修复；
- 已测量性能热点的优化；
- 文档/状态面板同步。

## 5. Money Exit Gate

Money 完成并允许切换下一项，至少要同时满足：

1. 当前 Revenue denominator / transition / ledger / reservation continuity 的 P1 review debt 清零；
2. 6×3 production work plan 的每个 cell 都具备：
   - independent native outcome；
   - exact Teacher comparison；
   - criterion-local join；
3. train / validation / test 分区身份独立且可重放；
4. production coverage request 实际接入这些证据；
5. Goal Method self-test / reconciliation 的 Money 六个 criterion 真实进入目标状态；
6. formal-training gate 仍按全局规则 fail-closed，不因 Money 单项完成自动授权；
7. 相关文档和本状态面板同步；
8. 高成本全量验证仅在该项 Exit Gate 时统一跑一次，不在每个小 PR 重复执行。

只有上述标准完成后，才选择下一个 primary item。

## 6. 下一项如何选择

Money 完成后，不预先固定 Friendship / Skill / Museum 的顺序。先重新生成：

- Goal Method reconciliation；
- blocker 分类；
- 当前 evidence reuse 情况；
- 预计新增工作量 / 共享基础设施收益。

然后只选**一个**下一施工单位。

## 7. 两组 `4/19` 的差集与恢复队列

当前两组 `4/19` 不是同一组，也不能相加：

| 状态 | criterion | 后续缺口 |
| --- | --- | --- |
| executable 且 split-complete | Community Center 两项 | 已进入 `CoverageGateReady` |
| 仅 executable | Skull Key、Pet Love | 当前 authority request 未重新绑定历史三分区 corpus |
| 仅 split-complete | Full Shipment、Master Angler | 顶层 Goal Method 仍为 `pending_dependency_expansion` |
| 两侧均未闭合 | 其余 13 项 | Method 与生产证据都要闭合 |

Skull Key/Pet Love 的旧生产工件仍是待重放证据，不是当前分子。它们需要按当前字典、日历、输入契约和哈希重新验证；
完全一致时直接重绑，只有失效分区才重采。Full Shipment/Master Angler 已有两通道三分区证据，后续只允许补顶层依赖展开，
不得另写第二套动作、候选或执行器。

这些修复不打断当前 Money `WIP=1`。Money Exit Gate 完成后，重新生成 reconciliation，并把上述四项作为高复用候选优先
评估；仍然一次只选择一个 root method。这样既避免丢弃历史成果，也不重新引入多个半成品并行推进。

## 8. 最近的深度优化状态

已进入主线并作为后续施工基础的改进：

1. action-origin 输出归因与通用 acquisition output sufficiency，避免执行成功但产物归属错误或重复写专用校验；
2. recovery 到 settlement 的版本化持久状态边界，未知持久字段默认失败关闭；
3. 同一次 Goal Method 调用内确定性复用 frontier，避免相同 authority 输入重复构建；
4. Planner、候选与 Executor 共用 `RouteGridPathfinder`、到达站位和终端计时语义，同一快照只构建一份当前地图距离场；
5. strategy-ledger 前后边界改为内容寻址 capture，绑定阶段、请求、候选、原生运行、快照与账本身份，不能再靠裸路径声明连续性。
6. validation trace v2 已记录 managed allocation、GC、private memory、read-session retention 与 deterministic-cache producer 成本；这些指标只负责解释开销，不改变 admission。
7. acquisition rollout proof 已改为 invocation 内的唯一 heavy verifier：缓存键绑定 manifest 及其全部直接/传递文件身份，缓存值仅保留轻量 receipt/checkpoint certificate；Full Shipment evidence index 先按精确 proof 内容身份去重，再映射回各 stratum。

这些优化减少重复 BFS、重复 frontier 构建、伪输出和伪账本连续性，但它们是 correctness/feedback-time 基础设施，**不直接增加**
`CoverageGateReady 2/19`。

#274 的代码优化阶段已完成：canonical artifact equality 改为流式哈希，read-session 与 deterministic-cache 均有显式总预算、单项预算、逐出与遥测，优先级最高的 DTO/DOM 双表示已收敛为 typed/read-only projection，并完成真实 26-proof 前缀的 A/B/C 测量。最终 C 组墙钟 `7588.7 ms`、CPU `6859.4 ms`、峰值工作集 `525.4 MiB`；相对旧默认实现分别降低约 `55.8%`、`40.2%` 和 `2.9%`。

#274 的 current-authority force-full correctness 门已关闭：固定 26 层全部由当前源码重新执行，最终严格索引为 `26/26`、缺失 `0`，且共享运输/结算证据有效。性能优化没有通过复用旧收据、减少分母或放宽 proof 相等性获得；相反，ledger 时间偏移导致的精确重放差异被守卫发现并修复。合并本次收口后可关闭 #274，并按 WIP=1 返回 Money / Revenue 主线。

## 9. 剩余规模

- 战略分母：`19` 个 criterion、`11` 个 root method。
- 细粒度证据分母：`19 x 2` 个独立通道（Teacher comparison/native outcome）`x 3` 个分区，共 `114` 个 channel-partition cell；也可视为 `57` 个必须同时具备双通道的 criterion-partition join cell。
- 当前机器报告实际闭合 `24/114` 个 channel cell，即 `12/57` 个 join cell；仍缺 `90` 个 channel cell。
- Skull Key/Pet Love 对应的 `12` 个 channel cell 有历史工件待 current-authority 重放；Full Shipment/Master Angler 的 `12` 个 channel cell 已闭合，但缺 Method executable。
- 其余 `13` 个 criterion 分属 Money、Museum、Friendship、Marriage/House、Skills、Rusty Key 六个根方法，对应 `78` 个尚未闭合的 channel cell。Money 当前以双通道 join 计为 `0/18` production cell。

这些逻辑格不等于同等数量的完整三年运行。相同根方法允许复用权威前缀、checkpoint、原生结果和批量分区编排，但每个逻辑格
仍必须保留独立的 save/player/partition/criterion 身份与可重放证据，不能为了提速合并监督标签。

## 10. 时间估计

假定不调用 worker、复用现有执行器和生产工件、游戏版本/权威字典不发生大漂移，并保持 `WIP=1`：

| 阶段 | 预计主动工程时间 | 说明 |
| --- | ---: | --- |
| Money / Revenue Exit Gate | `5-9` 天 | 关闭 review debt、18 个生产 join cell、接入 coverage request 并统一做一次高成本验收 |
| Skull/Pet 重放与 Full/Angler Method 修复队列 | `5-10` 天 | 每次一个根方法；若旧 corpus 哈希完全一致，耗时接近下界 |
| Museum、Rusty Key、Skills、Friendship、Marriage/House | `12-20` 天 | 长日历与社交/婚姻状态是主要不确定性 |
| 全局 reconciliation、回退检查与 promotion review | `3-5` 天 | 达到 `19/19` 前的统一收口，不在每个小 PR 重跑 |

从当前状态到 `19/19` 正式训练准入，合理估计为 `25-40` 个主动工作日；生产采证中的等待和原生重试可与部分开发重叠。
首轮全量训练、holdout 评估和首个第三年爷爷 `21` 分模型另需约 `3-7` 天，因此持续投入的墙钟总量约 `6-10` 个日历周；
间歇开发按 `10-14` 周准备。RTX 5070 8GB 主要用于 LoRA/小批量正式训练，不会明显缩短游戏原生采证与 Method 闭合。

首个 21 分模型之后，达到“除声音略机械外，长期表现接近人类玩家”的陪玩还需约 `4-8` 周的人类适应、联机协作、节奏与
长线回归。稳定 `100%` 完美存档是第二产品阶段，保守按额外 `8-12` 周估计，不应反向阻塞 21 分首训。

### 估计适用边界

上述时间不是承诺日期。以下事件会触发重新估算：游戏或 SMAPI 版本变化、权威字典/日历指纹变化、历史 corpus 无法重放、
新增 P0/P1 跨域 correctness 缺陷，或真实性能 baseline 证明当前反馈回路仍不可接受。

## 11. 防回退与文档治理规则

1. 本文件只保留当前状态，不堆叠历史 changelog。
2. `FORMAL_FULL_TRAINING_READINESS_CN.md`、`goal-conditioned-no-human-teacher-plan.md`、`FULL_SYSTEM_COMPLETION_PLAN_CN.md`、`CURRENT_WORK_CN.md` 中的日期章节均视为历史/证据记录。
3. 任何历史章节中的数字若与本文件冲突，以当前机器报告 + 本文件最新同步值为准。
4. 新 PR 若改变上述七个状态量之一，必须在同一 PR 或紧随其后的状态同步 PR 更新本文件。
5. 不再通过新增 handoff 文档来表达一个简单数字变化；优先更新本面板和机器 report。
6. 每次 authority rebuild 都必须输出 criterion 级四态矩阵：Method executable、Teacher comparison 三分区、native outcome 三分区及其交集。
7. 曾经 `CoverageGateReady` 的 criterion 在新报告中回退时，合并门默认失败；只有显式 invalidation artifact 记录失效原因、受影响分区和恢复计划后才允许接受。
8. 局部动作注册、runner、Full Shipment strata、hermetic fixture 或性能测试通过，均不得直接改变正式 coverage 分子。

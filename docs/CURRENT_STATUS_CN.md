# StardewAI 当前状态与施工顺序（Canonical）

> 本文件是**当前人类可读状态入口**。机器可读 report / self-test / evidence index 仍是事实源；本文件负责把不同分母分开显示并规定开发顺序。
>
> 下方其他带日期的 handoff、readiness、plan 章节均保留作历史证据。除非其数字在本文件中再次出现，否则**不得把历史数字当作当前状态**。

## 1. 当前状态面板

基于当前主线状态：

| 指标 | 当前值 | 含义 |
| --- | ---: | --- |
| Goal Method executable criteria | **5/19** | root method 已达到 `executable_frontier` 的 criterion |
| Split-complete Teacher/native evidence | **6/19** | train/validation/test 两条证据通道完整的 criterion |
| Promotion-grade / CoverageGateReady intersection | **5/19** | 同时满足 executable + 正式 Teacher/native 三分区证据 |
| Coverage-ready root methods | **4/11** | 11 个战略 root method 中正式 coverage-ready 的数量 |
| Full Shipment runtime strata | **当前权威 26/26** | 固定 26 层均由当前源码原生重采并通过严格索引，**不是全局 /19** |
| Money production evidence cells | **0/18** | 6 个 Money criterion × 3 partitions；hermetic fixture 不计 production |
| Formal product training | **false** | 未通过完整正式训练准入 |

新档开局礼盒已由真实 Spring 1 原生运行闭合五门证据。`runtime-starter-gift-20261024-172156` 在只加载 TransparentBridge 与 RuntimeTestHarness 的隔离存档中，从 fresh full snapshot 读取 owned `FarmHouse` 的精确 starter gift Chest，经共享 BFS 到 `(3,8)`，只调用原生 `GameLocation.checkAction`，405 tick 后得到 `(O)472 x15`、任务 `6`、礼盒移除和新状态哈希；`EVD-342` 因此将 `rewards.claim_starter_gift` 提升为 five-gate / training-eligible。运行时同时增加淡入、暂停、传送与控制冻结门，等待时不累积假卡死；失败回执改为记录实际礼盒/任务后状态。该局部准入不改变上表 19 criterion、11 root method 或 `formal_product_training=false`。

领取后真实快照 `eed1329a...bb71` 的离线后端回放已经完成。它暴露并关闭两项入口硬伤：普通可种种子现在由透明桥与候选层双重标记 `plantable_seed_requires_explicit_surplus_authorization`，不可被通用售卖/出货链误清空；Grandpa 解析器在 `earn_money` 为最高优先级、持有精确可种库存且农场尚无作物时，不再静默回退到 28 条社交候选，而是返回类型化 `planning_handoff_required / crop_production_teacher / farm_layout`。LiveTrainingLoop 对该状态编译空候选集，失败关闭普通队列。交接触发后，LiveTrainingLoop 先完整采集并校验分块 `farm_layout`，再由唯一 `MoneyRevenueCropTeacherDispatchBuilder` 顺序调用现有 Teacher decision、commitment ledger、obligation plan 和 `money_revenue_crop_native_production_run_plan.v3`。首日状态机根据每轮 fresh full snapshot 只执行购种、前往 Farm、场地准备、播种、浇水、回家、原生睡眠或完成中的唯一下一阶段；场地准备另用精确 bounded `farm_layout` 前后快照，各阶段只调用现有 DailyPlan、site-preparation builder、action/plan compiler 和 Runtime Harness/Product Executor。隐藏静音运行 `runtime-crop-teacher-first-day-20261024-183952` 已取得首日原生闭环证据：80×65 Farm 被完整采集为 9 个分块，Teacher 生成 84 个跨日动作，首日以 49 次控制尝试完成 47 次已验证执行，其中 6 格播种和 6 格浇水均有逐项原生回执；日期从 `1:spring:1` 推进到 `1:spring:2`，存档哈希从 `edfc6278...` 变为 `993d7906...` 并通过 durable save boundary。次日 fresh 快照保留 5 株作物，说明首日后的随机损失必须由多日 fresh 重规划处理。当前可称“首日原生闭环已取证”，下一步是把同一状态机扩展到 run plan 全日期及收获转移账本，仍不能称完整作物周期或正式训练已准入。

Master Angler 当前子切片已把 Golden Walnut 新档来源日程接到可跨 rolling retention 的真实 episode 归档。显式捕获模式只接受稳定 run ID、最大 Grandpa 分数目标、`full` 执行快照与 `content_addressed_gzip`；首条 episode 还必须绑定 day 0、同一 save/run/state/tick、未推进的姜岛/鹦鹉状态及逐行完整且全部为零的 `69 keys / 130 walnuts` 初始目录。每个 success + fresh episode 原子保留前后快照清单和重写引用的 episode，正常退出在训练事务提交前生成 manifest，异常退出可从耐久别名重建。`golden-walnut-fresh-save-smoke-20261024-161207` 已以隐藏静音原生运行生成首条 verified episode、initial boundary 和 episode manifest；该运行同时暴露并修复顺序队列聚合回执的起点 tick/path 不一致。它只证明采集入口端到端成立，尚未形成首次登岛、51 来源和三次鹦鹉跃迁的连续新档日程，因此不改变 `5/19`、`6/19`、`5/19`、`4/11` 或正式训练禁令。下一真实产物是延长同一新档 episode 链并导出 Golden Walnut source schedule，然后重建解锁时间线与 Master Angler prerequisite audit。

同一真实 Spring 1 快照的离线回放又关闭了两个会让长跑静默偏航的入口缺口。Grandpa 21 分目标的权威 ID、战略别名和 Golden Walnut 证据别名现由 Contracts 单点识别；运行脚本固定传权威 `goal.grandpa_max_score_year3`。状态解析为 `resolved` 时，LiveTrainingLoop 只把 `bound_candidate_ids` 送入日计划编译，完整排序仍保留供审计和结构化模型在绑定集合内比较；真实快照因此从 62 个同分候选按字符串误选 AnimalShop，改为 `raise_friendships` 的 28 个精确绑定 NPC 候选。另将 `initial-snapshot.json` 改为原始 HTTP JSON 保真落盘，禁止 `Invoke-RestMethod -> ConvertTo-Json` 改写值后沿用旧 state hash。定向回归 `164/164` 通过；没有再次启动游戏，正式准入指标不变。

Golden Walnut 现有采集入口还移除了“`MaxAttempts=8` 但 `required_verified_actions=1`”造成的首条成功即退出。工作流默认要求 `4` 条 verified 主动作、最多 `8` 次主尝试；达到目标后必须进入现有 native save boundary，在最多 `16` 次边界尝试内完成原生跨日和存档文件变化，才允许摘要标记 passed。报告会同时核对目标数、实际 verified 数、耐久 episode 数以及 boundary required/verified，不能用单条动作或旧报告冒充连续链。这里只完成脚本与离线守卫，尚未因 E 盘空间余量过低而再次启动游戏；下一真实产物仍是同一新档的多 episode、可落盘边界运行。

当前五个 `CoverageGateReady` criterion 固定为：

- `community_center_access_or_completion`
- `community_center_accessible_bonus`
- `pet_love`
- `skull_key`
- `achievement_full_shipment`

### 不允许再使用的模糊短写

禁止单独写：

- “当前 coverage 是 4/19”
- “当前进度是 2/19”
- “Full Shipment 26/26，所以全局完成”
- “Money 已完成”

必须写完整指标名，例如：

```text
Executable criteria:       5/19
Split-complete evidence:   6/19
CoverageGateReady:         5/19
Root methods ready:        4/11
Full Shipment runtime:    current authority 26/26
Money production cells:    0/18
Formal training:           false
```

### 2026-10-21 current-authority 复核与主施工项切换

Money 的第二条有界连续生产段 `money-crop-native-20261021-v10-continuation-v15-full-chain` 已通过并完成本轮退出条件。该运行从 v10 不可变终态继续，执行 `25` 个购种过渡、精确取得 `8` 份 `(O)475`，完成 `4` 份场地准备计划中的 `3` 个实际动作、`8` 个作物批次、一次补水、`8` 次单件投箱、回家和唯一原生睡眠；总计 `26` 条连续 transition，checkpoint 为 `verified=6 / pending=0`。源存档哈希前后均为 `9429c6d01d327111af8c81d0c36f2e318ccc4f1b0ea3094d84e19f25308b22e4`，最终状态哈希为 `bb70acbd0b477b7e14d7fe1b4450ee85292fa29e56de854924e4ee67f1216849`。该段仍未形成 Money threshold cell，`Money production cells=0/18`、正式训练关闭。

随后按当前源码、`game-1.6.15` 原始字典、当前 goal dependency 与 authoritative dependency graph 重建 requirement inventory、acquisition lowering 和三分区 current-source corpus。直接复用旧 corpus 被确定性拒绝，原因是 target-date time/energy budget 已随当前编译结果漂移；因此旧工件没有被错误冻结。新 reconciliation 位于 `E:\StardewAI-TestLab\artifacts\community-center-current-source-rebind-20261021\coverage\goal-method-coverage-reconciliation.json`，SHA-256 为 `24dae83e5164cc984486ae9ae008d6899250938da7ff8d695b82a488d3981456`，结果仍为 `4/19 executable、4/19 split-complete、2/19 CoverageGateReady、1/11 root ready`。

`complete_community_center` 的两个 criterion 均为 `coverage_ready`，train/validation/test 的 Teacher 与 native 三分区完整，method blocker 和 next action 均为空，故已按 current authority 复核并冻结。

Pet Love 的历史三分区原生工件已经在当前源码下严格重放并重绑，无需启动游戏或重采。`pet_love_teacher_corpus.v1` 为 `3 rows / 3 partitions`，Teacher comparison 与 native terminal outcome 均为 train/validation/test 完整；corpus SHA-256 为 `99f885037a21a9ae82865cdeec612c61d5bb8018e4701f1d149f8a4738cfb546`。当前 coverage report SHA-256 为 `4159b217df5e0457f9788d5df408e042b8bd2b66d64a2c2b1676da14bb30ba67`，reconciliation 位于 `E:\StardewAI-TestLab\artifacts\pet-love-current-source-rebind-20261021\goal-method-coverage-reconciliation.json`，SHA-256 为 `71781e188410a96eaab7d00ad61b703b4ec1981fdb2a0841c0727e611764b383`。结果为 `4/19 executable、5/19 split-complete、3/19 CoverageGateReady、2/11 root ready`；Pet Love blocker/next action 为空，正式训练仍为 false。

历史 `snapshot.v1` 的 state hash 包含现已判定为非语义的 `machine_probe_cache_tick`。Pet Love 与 Skull Key 适配器现统一调用共享 `SnapshotHash.MatchesStateHash`，允许当前或严格 legacy hash，但不跳过 schema、版本、身份、Teacher comparison、native outcome 或篡改检查。完整 Goal Method coverage 自测已通过，包括 legacy 正例、来源替换/碰撞和篡改负例。

Skull Key 的三条真实普通矿井终端 episode 已在当前源码下严格重放：train 为第 209 天 EVD-106，validation/test 为两个独立第 223 天运行；分别精确绑定第 6、31、4 步的 `claim_skull_key`，未混入骷髅洞、火山或 Quarry Mine。canonical corpus 为 `3 rows / 3 partitions`，SHA-256 `ff2afcb27b6e011c3e9261ad1f086f3934cc7710a59adf8f9de021a721df275a`；coverage report SHA-256 `3f33b1b14b78b991c155107106899270e474b8d19f6c59ee149cc2f9de2e3e77`；reconciliation 位于 `E:\StardewAI-TestLab\artifacts\skull-key-current-source-rebind-20261021\goal-method-coverage-reconciliation.json`，SHA-256 `a6d1ecb14ff9c6a1c8be5d079a69ea95d556760493e7f0d3807cbe8236cea841`。结果为 `4/19 executable、6/19 split-complete、4/19 CoverageGateReady、3/11 root ready`，Skull Key blocker/next action 为空，正式训练仍为 false。

Full Shipment dependency graph 的节点早已声明 `full_recurrence_required_for_training=false`，但顶层仍残留“完整 fresh-save 154 项 recurrence 是训练前置”的过时 blocker。当前机器证据已重新核实：`154/154` 权威静态需求、`641` 条来源、`26` 个执行签名层、静态 blocker `0`；严格原生索引为 `26/26 verified、0 missing`，共享 shipping/settlement 通过，完整 154 项 recurrence 继续保留给正常游玩与最终可选验收。索引位于 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-required-amount-current-authority-26-20261016\runtime-sample-evidence-index.json`，SHA-256 `e30edb0e6aca07deca484c8b8f1a4437eef68cb01120f13074329d7e2e0830b1`。

过时 blocker 移除后，current-source coverage report SHA-256 为 `b2cc9dd87212c9e86324708a38f2545723325b04ed7ea3ce19dc01473bab9743`；reconciliation 位于 `E:\StardewAI-TestLab\artifacts\full-shipment-topology-current-source-20261021\goal-method-coverage-reconciliation.json`，SHA-256 `71f0e4ed0e7928c1e63f39641f4b90153674f154a575ec331342a3d5241adf43`。Full Shipment 现为 `executable_frontier + coverage_ready`，blocker/next action 为空；全局达到 `5/19 executable、6/19 split-complete、5/19 CoverageGateReady、4/11 root ready`，正式训练仍为 false。Community Center、Pet Love、Skull Key 与 Full Shipment 均冻结；**当前唯一 coverage 主施工项切换为 `complete_master_angler`**，只补其顶层依赖、日历窗口与 coverage 接线。

2026-10-21 复核确认 Master Angler 的目标日期机会生成链已经是现行实现，而非待建占位：`MasterAnglerTargetDateIntentBuilder` 消费 72/72 Stage 1 窗口索引，随后由现有候选构建器、`CandidateOptionAvailabilityEvaluator.FishingCandidates` 与 `MasterAnglerCurrentCandidateMatcher` 共同核对动态查询、地点可达性、完整路线与计时、可钓 tile、鱼竿/装备以及完整原生 spawn outcome。蟹笼生命周期也已在同一个 `fishing.collect_crab_pots` 高层出口下闭合，制作、放置、装饵、跨日和收取均为编译器拥有的既有原生动作；其中 `executor.craft_crab_pot` 已由 EVD-339 补齐真实 `CraftingPage` 回执，而非只依赖单测。新 full snapshot 和 option matrix 已验证 `235/235` 注册、`159` 个 runtime verified、`66` 个 training eligible，Master Angler `option_blockers=[]`、状态为 `pending_dependency_expansion`。新增 `MasterAnglerFreshSavePrerequisiteAuditBuilder` 严格哈希绑定当前 catalog、Stage 1 窗口和依赖图，结果为 `72/72` 物种至少有两个静态日历日、`4` 种必须雨/风暴、最高要求钓鱼 `10` 级、分区仍为 `60/2/10`；但状态固定为 `partial_fresh_save_prerequisite_inventory`、`training_label_eligible=false`。钓鱼等级已通过显式 recurrence 复用 `raise_skill_levels` 的精确 Fishing XP 生产者；普通矿井地点链也已通过唯一 `mining.reach_depth` 生产者接入原生 `Mine` 大厅动作，并在每条路线/入口 primitive 后 fresh 重算。它精确支持当前鱼种所需的普通矿井 floor `20/60/100`，且继续拒绝把 Skull Cavern、Volcano Dungeon 或 Quarry Mine 当作普通矿井。鱼竿生产者则复用 `economy.buy_supplies`，精确绑定 FishShop 的 `(T)BambooPole x1` 与 `500g` 上限；共享购买策略只放行这个已审计的非 Object 商品，升级鱼竿、鱼饵和浮漂仍是可选效率优化而非硬门。删除关键依赖边、篡改矿井入口目标或把所需鱼竿替换为其他型号都会失败关闭。最新审计剩余 `8` 个未绑定族，SHA-256 为 `3c4016ecdadbc5964147d434578dd86c5ce8498adb39ed1cad48938430508e09`。真实 blocker 现为蟹笼配方/材料/饵料、沙漠巴士、姜岛、Mutant Bug Lair、秘密森林或森林农场、Rusty Key/下水道、天气保留或 Rain Totem、女巫沼泽，最终再组合 seed-robust 随机重试和 Year 3 截止证明。全局指标保持 `5/19 executable、6/19 split-complete、5/19 CoverageGateReady、4/11 root ready`。

2026-10-23，Rain Totem 天气支援完成材料消费 reservation 与原生制作回执。计划编译边界原子提交精确材料节点/槽位/物品/数量，dispatch 前要求同一 ledger revision 和完整 reservation ID；原生制作 applied/verified 且 after snapshot fresh 后，LiveTrainingLoop 通过现有 supporting-transition API 完成三项材料结算，失败回执不会进入训练。EVD-340 隐藏静音 E 盘运行验证 `(O)681 0 -> 1`、`timesCrafted 0 -> 1` 和原生材料/产物/任务/成就回调。该 primitive 保持 calibration-only；正式训练仍为 false。下一固定步骤是用当前依赖图重建 Master Angler fresh-save prerequisite audit，再完成其实际残余依赖和 Year 3 截止证明。

2026-10-23，Master Angler 审计已重建并补上旧版遗漏的 Rain Totem 觅食 9 级前置。未学配方时复用 `foraging.chop_wild_tree` 的精确正向 XP primitive，Hardwood reserve、三材料 reservation/settlement 与 EVD-340 原生制作回执均由独立依赖节点校验。天气族现为 `implemented_and_bound`；审计 SHA-256 为 `473be51d47822b9576327c5c6979b2c3b302c5c1828bdd50f7f5cb787e99862d`，未绑定族为 `4`：姜岛、Mutant Bug Lair、秘密森林或森林农场、女巫沼泽。`formal_product_training_authorized=false` 不变，下一切片为秘密森林替代分支。

2026-10-23，秘密森林/森林农场替代分支已经机器化闭合。`Farm_Forest` 窗口只在实时农场类型确为 Forest 时成立，并映射到运行时 `Farm`；其他布局由透明桥核对 Forest→Woods 两个原生 warp、Forest `(1,6)` 的精确 `ResourceClump(602,2,2)`、当前 Axe 与钢斧等级 `2`。高层仍为 `fishing.catch_fish`，工具升级复用共享 submit/claim 生命周期，原木清除复用 `executor.break_current_location_resource_clump`，每个原生 primitive 后 fresh 重算。定向编译测试和钢斧门槛篡改负例通过；没有启动游戏，也没有把既有绿雨 EVD-212 扩张为 602 的运行时证据。最新 fresh-save prerequisite 审计 SHA-256 为 `78f868edd55d4b5807ad211afc10289811c86473df056df9992996b7cd4f618e`，剩余 `3` 个未绑定族：姜岛、Mutant Bug Lair、女巫沼泽。全局指标不变，`formal_product_training_authorized=false`。

2026-10-23，Mutant Bug Lair 与 Witch Swamp 的共享 Dark Talisman 路线已机器化闭合。透明桥、候选编译与 Runtime Harness 锁定 Railroad `529952/C -> quest 28 -> Krobus/krobusUnseal -> BugLand SpecialItem(6) -> Railroad tile 287/witchStatueGone -> MagicWarp WitchSwamp 20 42`；BugLand 目标在 `krobusUnseal` 后停止，WitchSwamp 目标继续完整后缀。宝箱复用 Skull Key 的唯一原生两阶段 SpecialItem 状态机，Krobus/雕像调用原生 `checkAction`，没有直接进度写入。定向回归 `5/5`、相关 Master Angler/route 回归 `16/16`、依赖篡改负例和零警告构建通过；尚无本切片真实游戏回执。fresh-save prerequisite 审计 SHA-256 为 `74460f8612d92ab09ead68aa2c5aeedd7eaa5aaed8dc1a6ced923f84b5578892`，仅剩 `ginger_island_access` 未绑定；之后仍需 Year 3 截止组合与 seed-robust 随机重试证明。`formal_product_training_authorized=false` 与全局指标不变。

2026-10-23，姜岛访问已完成“原生终端 + 当前快照即时供给”切片。Willy 邀请、三项船修、船票、出航、BoatJourney 和四个鹦鹉升级均调用原生交互；即时 Battery Pack/Hardwood/Iridium Bar/票钱缺口复用既有机器、清障、投箱及睡眠结算。金核桃缺口现会复用当前 fresh snapshot 中已严格投影的灌木、特殊钓鱼、地面掉落、确定性挖掘、金椰子及田野办公室调查候选，并在每个 primitive 后回到原 `fishing.catch_fish` 意图。鹦鹉链的累计支出是 `1 + 10 + 20 + 20 = 51`，不是 21。定向回归 `7/7`、构建零警告零错误，未启动游戏。当前仍不能将 `ginger_island_access` 标记完成：跨地图/未加载地点的全 51 核桃生产者目录、路线和随机重试预算尚未形成权威 fresh-save 闭环；无当前精确来源时实现会明确失败关闭。所以下一步是补全 51 核桃跨地图来源投影后重建 prerequisite audit，正式训练仍为 `false`。

2026-10-23，金核桃来源目录与既有动作链已形成新的机器边界。目录锁定原生 `69 keys / 130 walnuts` 分母并实时读取 tracker、limited-drop、world flag、master mail；固定埋藏点由当前世界 `IsBuriedNutLocation` 复核，西部两处日记点精确锁定 `(18,42)/note 1004` 与 `(104,74)/note 1006`。不购买可选桥且排除 Resort 后置来源时，已接通来源容量精确为 `51`，全部复用现有灌木、钓鱼、清障、作物收获和 `executor.till_soil`，没有新增平行动作系统。随机来源按“一次原生尝试 -> fresh limited-drop 计数”滚动，固定来源按 fresh tracker/计数收据滚动；跨地图每次只走一个 connector。相关回归 `42/42` 通过，未启动游戏。该数字只证明来源能力分母足够，不证明 `1+10+20+20` 钱包顺序、阶段可达性和随机来源能在 Year 3 截止内完成；`ginger_island_access`、全局 coverage 分子与 `formal_product_training_authorized=false` 均不变。下一步固定为构建该顺序/截止证明并重建 Master Angler prerequisite audit。

同日后续切片已完成四阶段钱包、随机尝试预算、部分截止容量结构和 Resort 资金路线比较。首选 `deterministic_gourmand_requests` 以 House 后 `IslandGourmand1/2/3` 各 `5` 个确定性核桃替代随机路线的 `15` 个限量核桃；透明桥、候选、编译、跨图 continuation 与原生执行器均已接入。E 盘隐藏隔离运行 `runtime-island-gourmand-smoke-20261023-110034` 已通过引介和首单交付，EVD-341 验证原生 `Gourmand_Yes`、作物事件、洞穴返回、五碎片生成/拾取、request index `0 -> 1`、`GoldenWalnutsFound 0 -> 5`、tracker 与锁释放；目录因此从 `160` 增至 `161 five-gate`。`island_gourmand_supply_schedule.v1` 进一步锁定 SeedShop 的大蒜第 2 年春季窗口、甜瓜夏季窗口、小麦夏秋窗口，按实时商店报价预留各一包种子，并复用现有购买、跨图、耕地、播种、浇水和睡眠链在 `IslandWest` 并行养成；甜瓜的 `12` 个原生生长夜决定总工期，三株成熟后必须留在地里供 Gourmand 观察，不能收获。标准利润率价格 `40+80+10=130g` 只作参考，实际编译仍要求 fresh quote。Resort 目标候选同时携带 Turtle `211` 与 House `223` 的最晚边界，未完成 Turtle 而超过第 `211` 天会在上游直接排除；非 Resort 岛屿目标不受此限。路线证明现可接收实际 Turtle/House 日期并机器组合截止：`211/223` 可选路、`212/223` 失败；即便日期门完成，第二、三单连续原生回执仍独立阻止训练准入。当前实际解锁日尚未由 fresh-save 日程绑定，所以默认 `route_selection_complete=false`，正式训练仍为 `false`。相关回归 `26/26` 与 Bootstrap Release 构建通过；全量 hermetic 自检仍在既有 Money Revenue seed supporting-step fixture 的缺失 snapshot path 上失败，未冒充全量通过。

日期绑定入口现进一步收紧为 `golden_walnut_fresh_save_unlock_timeline_proof.v1`。它消费同一新档计划导出的、SHA-256 绑定的确定性核桃来源与鹦鹉交互日程，而不是直接信任 Turtle/House 两个整数；按 `1/11/31` 累计钱包阈值计算各阶段就绪日，并验证计划中的 First Parrot、Western Turtle、Island House 交互日不早于钱包就绪日，来源属于已有唯一动作链、数量不超原生分母且不早于前置解锁。Master Angler CLI 新增可选 `--golden-walnut-source-schedule`，只有该文件完整时才把经验证的实际计划日期交给 Resort 路线。代码与定向回归已完成，Bootstrap 构建零警告；真实新档日程文件尚未生成，因此默认状态与正式训练禁令没有变化。

新档日程的生产入口现已接到既有闭环回执，而不是候选参数。`build-golden-walnut-fresh-save-source-schedule` 消费显式 episode manifest，验证同一 run/save、连续状态哈希、单调 tick/日期、成功状态与 fresh after snapshot；逐来源只在透明 `golden_walnut_sources` 数量真实增加时记录，三次鹦鹉支出及首次登岛必须有原生状态跃迁。产物携带 manifest SHA-256、episode/快照有序证据根、run/save 身份和条目数，时间线证明拒绝缺少该来源归属的旧式手填日程。代码和 `12/12` 聚焦回归已通过，尚缺的不是另一个计划器，而是真实连续新档清单及其后续 Master Angler 审计重建。

2026-10-21 后续切片已关闭上述清单中的沙漠巴士前置。唯一共享 connector 链新增 `native_bus_transport`，透明桥实时读取票机、`ccVault`、Pam/自驾、动态票价、余额、坐骑及返回触发状态；编译器仍只产生 `executor.traverse_connector`，运行时调用原生确认框和巴士过场并校验地点/tile/钱款回执。Master Angler 依赖图严格组合现有 Community Center 完成事实、`fishing.catch_fish` 高层目标和单段 fresh 重绑定，篡改 connector 或关键边会失败关闭。当前证据级别是锁定源码核对、编译与离线测试通过，尚无本切片的真实游戏运行回执。最新审计剩余 `7` 个未绑定族，SHA-256 为 `40e3df4d946f9dbf09cf70c399f994b94e7c3038b619f76e7108e05fccd73890`；剩余为蟹笼配方/材料/饵料、姜岛、Mutant Bug Lair、秘密森林或森林农场、Rusty Key/下水道、天气保留或 Rain Totem、女巫沼泽。`training_label_eligible=false`，全局指标仍为 `5/19 executable、6/19 split-complete、5/19 CoverageGateReady、4/11 root ready`。

2026-10-21 下水道切片继续把未绑定族降为 `6`。透明门现在把 `OpenedSewer || hasRustyKey` 视为可执行；Runtime Harness 按锁定 1.6.15 `EnterSewer` 控制流支持“钥匙开锁文本 -> mail 回执 -> 第二次原生交互 -> Sewer(16,11)”，无钥匙在动作入队前排除。Master Angler 节点严格引用 `obtain_rusty_key` 终态和关键 recurrence/event 边，并继续通过唯一 `fishing.catch_fish` 路由出口执行；这只完成跨 root 组合，不等于 Rusty Key root 的 60 件捐赠、Teacher/native 证据已经完成。最新审计 SHA-256 为 `00042d57ed53321e9b7510aa6dc1d8c5502aa11d16496513ced7146bb4e3f04c`；剩余族为蟹笼供应、姜岛、Mutant Bug Lair、秘密森林或森林农场、天气保留或 Rain Totem、女巫沼泽。当前仍只有锁定源码、编译和离线测试，没有本切片真实游戏回执；`training_label_eligible=false` 与全局指标不变。

2026-10-21 蟹笼供应切片把未绑定族进一步降为 `5`。隔离 E 盘存档副本的实时 FishShop 投影证明钓鱼 `2` 级已有 `(O)685` 鱼饵 `5g` 而无蟹笼，钓鱼 `3` 级出现 `(O)710` 蟹笼 `1500g`；原用户存档未被加载或改写。唯一高层出口仍为 `fishing.collect_crab_pots`：缺饵的远程服务先复用 FishShop 单件购买链；精确缺失蟹笼鱼种的随机容量不足时优先制作，只有不能制作才买一个蟹笼，随后关闭菜单、保留 continuation 并 fresh 重算。没有新增平行商店、蟹笼容量或收取系统。定向回归 `48/48` 和防篡改自测通过；实际购买仍沿用既有通用执行器证据，本切片只新增实时库存权威投影，不声称新增原生购买运行回执。最新审计 SHA-256 为 `8cd925d8ede0a0c5d3e5b51817095958ffdd7f5ff100d588400f8dda2f46204b`；剩余族为姜岛、Mutant Bug Lair、秘密森林或森林农场、天气保留或 Rain Totem、女巫沼泽。`training_label_eligible=false`，全局指标不变。

2026-10-22 天气族已机器化登记并严格审计“已有 Rain Totem 的消费者”这一叶。它直接复用 EVD-288 的 `executor.use_rain_totem` 原生路径，要求精确 `(O)681`、可生效的 location-context、明日非默认节日、最终天气 Rain 和 fresh 原生回执；篡改最终天气会使绑定失效。该进展没有把 primitive 当成新的模型动作，也没有声称图腾会凭空出现。权威依赖图已锁定 `Data/CraftingRecipes` 的 `709 x1 + 432 x1 + 726 x5`、Foraging `9` 和 `(O)681 x1`，但配方材料生产及“今晚用图腾 -> 明日 `fishing.catch_fish`”调度仍待实现，所以该阶段天气族状态为 `runtime_consumer_bound_supply_and_scheduler_missing`，审计 SHA-256 为 `7dd897e3cfb2a52a5016c4f342eef003b6e6848f5d12d7d44f46aae2f84468a9`。

2026-10-22 后续只读切片补齐 `player.rain_totem_crafting`：full profile 从实时已学配方与锁定 `Data/CraftingRecipes` 精确筛选 `(O)681`，输出原始配方、制作次数、材料槽、可制作数量、产物容量和 Workbench 来源。它与 `player.crab_pot_crafting` 共用唯一精确单产物扫描器，避免两套制作语义。依赖图新增严格只读节点并通过 schema 篡改负例，天气族状态更新为 `runtime_consumer_and_crafting_read_bound_executor_supply_scheduler_missing`。该切片仅通过 TransparentBridge 与 Bootstrap Release 编译及离线专项自测，未启动游戏，也没有原生 Rain Totem 制作回执；材料生产与明日钓鱼调度仍缺失，因此未绑定族仍为 `5`、最新审计 SHA-256 为 `09d9053c264208f11e95b1080da7b5d4db0958a790aa18d1f2640d76cd4d4748`，所有正式准入分子不变。

2026-10-23 明日雨窗调度已接通且继续只暴露 `fishing.catch_fish`：权威生产器从 fresh snapshot 和哈希锁定窗口索引筛出明日仅雨、无动态条件、装备与 location-context 均满足的精确缺鱼集合；候选按“无图腾则制作 -> fresh 重算 -> 有图腾则原生使用 -> fresh 重算 -> 次日捕鱼”滚动展开。Backend 新增与默认候选相加的 `additional_candidates`，当前日与天气意图均在排名前注入。定向回归 `125/125`、防篡改审计和 Release 构建通过，未启动游戏。天气族状态为 `runtime_consumer_crafting_read_and_scheduler_bound_material_supply_and_native_crafting_receipt_missing`，审计 SHA-256 `ee97bff20a4991a8f4db70b6b7cf7e6ad9b1a3749ec89b9a048362c281eb9572`；三种材料生产和原生制作回执仍缺，正式准入分子不变。

2026-10-23 即时材料供应切片已加入同一明日雨窗循环。精确配方缺料时，编译器只复用现有 `clear_obstacle_tile`、`collect_machine_output_tile`、`load_machine_input_tile` 和 `collect_spawned_object`，分别覆盖保证掉落 Hardwood 的树、已完成的 Pine Tar/Truffle Oil、Truffle 投入 Oil Maker 及当前地面 Truffle；材料行不完整、数量漂移或没有可立即执行的原生生产者都会失败关闭。该切片不代表新档已建立长期供应：Pine/Tapper 五日周期、Deluxe Barn/Pig 十日成熟、Oil Maker 容量和 Hardwood 储备仍未绑定，Rain Totem 原生制作回执也未采集。依赖图状态更新为 `runtime_consumer_crafting_read_scheduler_and_immediate_material_supply_bound_long_horizon_reserve_and_native_crafting_receipt_missing`，图 SHA-256 `c437060b231ca4649c96798dfb521fa1bc7767eb5cf5ee2e7dbbabb28c7ccb17`；未绑定族保持 `5`、正式训练仍禁止。离线验证为 Rain Totem `22/22`、Bootstrap Release `0 warning / 0 error`；现存审计夹具早于当前 60/2/10 权威目录，未生成新的正式审计报告哈希。

2026-10-23 Pine Tar 长线产能已在既有天气调度和机器链内闭合，没有新增第二套机器执行器。未来雨窗意图会从权威窗口索引、节日日历、fresh 缺鱼、当前库存和精确 Pine Tapper 到期产出计算缺口，再按 5 天基础周期求最少新增 Tapper 数；每次只通过通用 `craft_machine_item` / `place_machine_item` 补一台并 fresh 重算。透明放置同时锁定 `Object.canBePlacedHere` 与 `Object.placementAction` 的成熟可 Tap 树分支，候选测试确认会越过更近的橡树选择松树；季节和树状态已进入缓存指纹。专项回归 `46/46`，Bootstrap Release `0 warning / 0 error`，未启动游戏。依赖图 SHA-256 为 `e6a4637b5501e12bc4f6e45deacd1b551376e0daad838f84ff1b15787ba4dccc`。天气族的新精确状态为 `runtime_consumer_crafting_read_scheduler_immediate_supply_and_pine_tar_capacity_bound_pig_truffle_hardwood_reservation_and_native_crafting_receipt_missing`；Pig/Truffle Oil、Hardwood、材料消费 reservation 和原生 Rain Totem 制作回执仍缺，故未绑定族仍为 `5`、正式训练仍为 `false`，所有全局分子不变。现存审计夹具缺当前 60/2/10 字段，本切片未生成或宣称新的正式 audit hash。

2026-10-23 Pig/Truffle Oil reserve 的必要建筑升级底座已补齐。此前通用建筑目录显式跳过 `BuildingToUpgrade`，导致 fresh save 无法从 Barn 走到 Deluxe Barn；现在透明目录为每座精确来源建筑生成升级行，候选、日计划和动作队列在唯一 `buildings.construct -> executor.construct_building` 链内以 `project_kind=upgrade` 复用原生 CarpenterMenu。编译与运行均核验来源建筑类型和坐标，成功必须观察到原生 `upgradeName`、`daysUntilUpgrade`、金钱及材料收据，不允许直接写建筑状态。专项测试 `11/11`、Runtime Harness Release 零警告零错误，未启动游戏。这里只修复 Pig reserve 的前置执行硬伤；猪购买/成熟/天气生产、Truffle 与 Oil Maker 容量调度仍是下一固定切片，因此全局准入指标、依赖图哈希、天气族未完成状态和正式训练禁令不变。

2026-10-23 Pig/Truffle Oil reserve 已完成代码级闭环，但未被夸大为整个天气族完成。未来雨窗投影从精确建筑、升级进度、动物购买目录、Pig 年龄、动物门、Farm 地面 Truffle、Oil Maker 和库存状态选择唯一当前阶段；Barn -> Big Barn -> Deluxe Barn、Pig 购买、10 日成熟、非冬季产出机会及 Oil Maker 工期进入同一 lead-time。执行继续复用通用建筑、购畜、机器和采集链；唯一新增原生 primitive 是透明绑定的 Barn 动物门右键，使用 `Building.doAction` 并验证 `animalDoorOpen false -> true`，不直接改状态。已有按期 Truffle Oil 产出会抑制重复 reserve，库存已有 Truffle 时不会重复建设 Pig 产能，远程 Farm Truffle 会先形成去 Farm 的采集连接，冬季无产出机会会跳到更晚的合法雨窗。定向 `ActionQueueCompilerTests` 为 `159/159`，四个 Release 工程与 Master Angler 防篡改自测通过，未启动游戏。依赖图 SHA-256 为 `2178040959b142f04b3d5be0795945d0286a05ccfad405fe6c84ba63c54a5fd3`，天气状态为 `runtime_consumer_crafting_read_scheduler_immediate_supply_pine_tar_and_pig_truffle_capacity_bound_hardwood_reservation_and_native_crafting_receipt_missing`；未绑定族仍为 `5`，`long_horizon_reserve_bound=false`、训练准入分子与正式训练禁令均不变。下一步只做 Hardwood reserve。

2026-10-23 Hardwood reserve 已完成“现有精确来源”切片，但工具升级生命周期仍未完成。未来雨窗在 Pine Tar 与 Truffle Oil 已满足后继续投影 `(O)709 x1`，按 fresh 状态依次选择当前 Farm 的原版树桩/倒木、当前地图保证掉落 Hardwood 的成熟野树、或先路由到 Farm；来源身份锁定地点、格点、ResourceClump parent index 和保守最低产量，参数漂移会失败关闭。即时材料链也会复用同一 `executor.break_farm_resource_clump`，没有第二套清障执行器。原版树桩 `600 -> Hardwood x2`、倒木 `602 -> x8` 已绑定；斧头等级不足时只产生 `blocked_required_axe_upgrade_missing`，不会伪装成可执行。因此依赖图精确记录 `ready_hardwood_source_reserve_bound=true`、`hardwood_tool_upgrade_lifecycle_bound=false`、`hardwood_reserve_bound=false`，SHA-256 为 `87f9dacd6ed7f85bd8088514da0d4e21308df9d25f4579490740302b866b0802`。Rain Totem 专项回归 `22/22`、Hardwood 专项 `7/7`、依赖图联合回归 `24/24`，均未启动游戏；未绑定族、正式准入分子与训练禁令不变。下一固定切片是原生工具升级 lifecycle，随后才是材料消费 reservation 和 Rain Totem 原生制作回执。

2026-10-23 原生 Axe 升级 lifecycle 已完成代码级接线，未冒充真实运行证据。透明桥实时投影下一等级 Axe、来源槽、Clint/Blacksmith 状态、原生价格与 5 根对应金属锭、两日等待和待领取工具；Hardwood reserve 据此滚动生成提交、等待、领取或显式失败阶段。编译链 fresh 锁定投影指纹、端点、站位、工具身份和成本；Runtime Harness 只调用原生 Blacksmith 对话、`ClintUpgrade` ShopMenu 与原生领取，不直接改写生产状态。E 盘隔离存档的隐藏静音 full snapshot 已通过 `177 required / 160 readable / 17 contextual / 0 blocking`，新增链路和目录测试 `12/12` 通过，完整 Core 为 `2791/2805`；剩余 14 个均为此前其他模块的已知失败。`executor.submit_tool_upgrade` 与 `executor.claim_tool_upgrade` 仍是 `RegisteredOnly`，runtime/output gate 为空，因此 `hardwood_tool_upgrade_lifecycle_bound` 当前只表示离线实现完成、原生回执待采，不改变任何训练分子。下一步固定为材料消费 reservation，再做原生 Rain Totem 制作回执。

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

## 3. 已完成的有界施工单位：Money / Revenue

2026-10-21 更新：`money-crop-native-20261021-pricefix-v10` 已形成第一条完整原生作物生产段。它从不可变源存档执行 `21` 个购种过渡、精确购买 `5` 份 `(O)474`，随后完成场地准备、播种、条件浇水、收获、`5` 次单件投箱、回家和原生过夜结算。整条 shared lifecycle 为 `48` 条连续 transition、`12` 个实际作物批次。checkpoint v12 为 `verified=6 / pending=0`；终局 recurrence 从 total day `18` 推进到 `19`（春 20 日 06:00），`money 50 -> 925`、`totalMoneyEarned 0 -> 875`。源存档与隔离 Mod 哈希均未漂移，最终透明状态哈希与 recurrence 完全一致。

这份成功结果仍不增加 `6 x 3` 分子。`money_revenue_crop_native_production_segment_receipt.v1` 已从 v10 摘要深回放并绑定已有 Teacher 决策、完整 checkpoint、终端 recurrence、最终快照与最终隔离存档树；它确认 `50` 个候选、`400g` 实际种子成本、`875g` 毛收入和 `475g` 净现金收入。`money_revenue_crop_production_teacher_comparison_receipt.v1` 又把唯一原生选中结果接回完整 `50` 项分母，保留 `5` 个 admitted、`45` 个 blocked 和 `4` 条 Teacher 偏好对；未执行候选没有伪装成 native outcome。由于没有跨越最低 `50,000g` criterion，该 comparison 仍明确保持 `criterion_local_join_ready=false`。v15 已满足“再闭合一条连续 Money 段”的有界退出条件；Money 现暂停在第 11 顺位，等待前十个 root method 收口后再完成 criterion-local join。因此状态面板继续严格保持 `Money=0/18`、`formal_product_training_authorized=false`。

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

上段末尾的“尚未首次采集”是 2026-10-18 的历史描述，已由本节顶部 2026-10-21 `v10` 完整结果取代。当前 crop checkpoint、生产段结果收据和 segment-level outcome-backed Teacher comparison 均已完成；真实剩余项是连续段推进和阈值跨越后的 criterion-local join。

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
已完成恢复点：v15 已闭合 v10 之后的下一条 Money 连续生产段。
当前恢复点：`complete_master_angler` 顶层 dependency、日历窗口与 coverage 接线。
```

Money 的有界段已验收并停止继续深挖。Community Center、Pet Love、Skull Key 与 Full Shipment 已在 current authority 下复核冻结，因此当前从第 6 节下一项 `complete_master_angler` 开始。任何时刻仍只允许一个 primary item；不得以“复杂度排序”为由重新并行展开多个半成品。

### 允许的中断

仅以下情况可以临时中断当前项：

- P0 / P1 跨域 correctness 缺陷；
- 当前项被一个共享基础设施硬 blocker 阻塞；
- 已测量确认的性能热点使当前项无法在可接受反馈时间内继续。

中断处理完成后必须**回到同一 primary item**，直到它达到本轮显式退出条件；不得借中断顺势跳过第 6 节队列。

### 不算“并行开新项”的工作

以下工作可与当前项并行，但只能服务当前项或全局安全：

- review finding 修复；
- CI / source guard / deterministic-build correctness；
- 共享 Planner / Compiler / Executor / Verifier 的 P0/P1 修复；
- 已测量性能热点的优化；
- 文档/状态面板同步。

## 5. Money Exit Gate

Money 自身最终完成，至少要同时满足：

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

上述标准仍定义 Money 自身的最终 Exit Gate，但自 2026-10-21 的复杂度排序指令生效后，不再作为开始其他较小 root method 的前置条件。Money 回到队列时仍必须完整满足本 Gate，不能因被排到后面而降低标准。

## 6. 下一项如何选择

### 2026-10-21 固定的 11 核心复杂度队列

这里的复杂度是**从当前仓库状态继续完成所需的增量工程成本**，不是玩法表面难度、criterion 数量或代码行数。排序依据包括当前 authority 状态、可复用原生证据、剩余依赖展开、跨日长度、候选分母规模和不可逆结算风险。当前 Money 有界连续段完成后，按下表从小到大施工：

| 顺序 | root method | 当前增量判断 | 本轮处理方式 |
| ---: | --- | --- | --- |
| 1 | `complete_community_center` | current-authority 重绑已通过，coverage-ready root | 已冻结；除 authority 漂移外不再施工 |
| 2 | `earn_pet_love` | current-authority 三分区重放、重绑和篡改回归已通过 | 已冻结；除 authority 漂移外不再施工 |
| 3 | `obtain_skull_key` | current-authority 三分区重放、重绑和篡改回归已通过 | 已冻结；除 authority 漂移外不再施工 |
| 4 | `complete_full_shipment` | current-authority `154/154` 静态 + `26/26` 原生分层和顶层接线已通过 | 已冻结；完整 recurrence 保留给正常游玩/最终验收 |
| 5 | `complete_master_angler` | split-complete，已有钓鱼与蟹笼执行基础 | **当前活动项**；补顶层依赖、日历窗口与正式接线 |
| 6 | `obtain_rusty_key` | 复用博物馆前 60 件与奖励事件，分母小于全博物馆 | 先闭合阈值子集，再给全博物馆复用 |
| 7 | `raise_friendships` | 社交执行器可复用，但有多人、多日程与礼物约束 | 建立共享社交 recurrence 后闭合分区 |
| 8 | `complete_museum_collection` | 大型异构获取分母与捐赠 recurrence | 复用 Rusty Key 的 acquisition/reservation/donation 链 |
| 9 | `raise_skill_levels` | 五套 XP 来源、资源与机会成本共同参与 | 先统一 XP deficit/Teacher 分母，再做长轨迹 |
| 10 | `marriage_and_house_upgrade` | 组合社交、采购、房屋、日历、求婚和婚礼结算 | 复用 Friendship 与 Money 支持链后施工 |
| 11 | `earn_money` | 六阈值 × 三分区，且要求连续经济 recurrence | 回到现有生产段链，完成 `18/18` 与 Money Exit Gate |

此表是新的默认开发顺序。每项开工前仍要重新生成 Goal Method reconciliation、blocker 分类和 evidence reuse 报告，但它们用于确认“重绑、补证或重采”的范围，不得静默改变顺序。只有出现可验证的硬依赖倒置或 P0/P1 correctness blocker 时，才允许先编辑本表及其余当前态文档，再调整尚未开始的项；已开工项仍遵守 WIP=1。

## 7. executable / split-complete 差集与恢复队列

当前 `5/19 executable` 与 `6/19 split-complete` 不是同一组，也不能相加：

| 状态 | criterion | 后续缺口 |
| --- | --- | --- |
| executable 且 split-complete | Community Center 两项、Pet Love、Skull Key、Full Shipment | 已进入 `CoverageGateReady` |
| 仅 executable | 无 | 当前五个 executable criterion 均已完成正式三分区覆盖 |
| 仅 split-complete | Master Angler | 顶层 Goal Method 仍为 `pending_dependency_expansion` |
| 两侧均未闭合 | 其余 13 项 | Method 与生产证据都要闭合 |

Skull Key 与 Pet Love 的旧生产工件均已按当前字典、日历、输入契约和哈希严格重放并冻结。Full Shipment 的顶层接线也已完成；Master Angler 已有两通道三分区证据，后续只允许补顶层依赖展开，不得另写第二套动作、候选或执行器。

这些既有成果解释了新队列为何把 Community Center、Pet Love、Skull Key、Full Shipment 和 Master Angler 放在前部。Money 有界段已经验收，前四个 root method 已按 current source 冻结；现在严格进入 Master Angler 顶层接线，仍然一次只选择一个 root method。这样既避免丢弃历史成果，也不重新引入多个半成品并行推进。

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
`CoverageGateReady 5/19`。

#274 的代码优化阶段已完成：canonical artifact equality 改为流式哈希，read-session 与 deterministic-cache 均有显式总预算、单项预算、逐出与遥测，优先级最高的 DTO/DOM 双表示已收敛为 typed/read-only projection，并完成真实 26-proof 前缀的 A/B/C 测量。最终 C 组墙钟 `7588.7 ms`、CPU `6859.4 ms`、峰值工作集 `525.4 MiB`；相对旧默认实现分别降低约 `55.8%`、`40.2%` 和 `2.9%`。

#274 的 current-authority force-full correctness 门已关闭：固定 26 层全部由当前源码重新执行，最终严格索引为 `26/26`、缺失 `0`，且共享运输/结算证据有效。性能优化没有通过复用旧收据、减少分母或放宽 proof 相等性获得；相反，ledger 时间偏移导致的精确重放差异被守卫发现并修复。合并本次收口后可关闭 #274，并按 WIP=1 返回 Money / Revenue 主线。

## 9. 剩余规模

- 战略分母：`19` 个 criterion、`11` 个 root method。
- 细粒度证据分母：`19 x 2` 个独立通道（Teacher comparison/native outcome）`x 3` 个分区，共 `114` 个 channel-partition cell；也可视为 `57` 个必须同时具备双通道的 criterion-partition join cell。
- 当前机器报告实际闭合 `36/114` 个 channel cell，即 `18/57` 个 join cell；仍缺 `78` 个 channel cell。
- Master Angler 的 `6` 个 channel cell 已闭合但缺 Method executable；Full Shipment 的同类缺口已关闭，不再有仅 executable、等待三分区重绑的 criterion。
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

# StardewAI 正式全量训练准入与实施路线

> **当前状态入口：** [CURRENT_STATUS_CN.md](CURRENT_STATUS_CN.md)。本文件下方带日期的状态、计数和“下一步”均保留为历史证据；若与当前机器报告或 CURRENT_STATUS_CN.md 冲突，不得按历史数字规划新工作。
>
> **施工顺序：** Goal Method / formal-coverage 主线实行 **WIP=1**。v15 已闭合从 v10 最终隔离存档开始的一条 Money 连续生产段，Community Center、Pet Love、Skull Key 与 Full Shipment 已通过 current-authority 复核并冻结；当前唯一施工项是 `complete_master_angler` 顶层依赖、日历窗口与 coverage 接线。Money Exit Gate 标准不降低，但 Money 已回到复杂度队列第 11 位。

## 当前增量 · 新档礼盒动作已局部准入

`rewards.claim_starter_gift` 已完成透明投影、候选、日计划、fresh 编译和原生 `GameLocation.checkAction` 执行路径，并作为 `earn_money` 的 fresh-save 支撑转移。实现锁定 owned FarmHouse、唯一 starter gift Chest、精确 contents、背包容量、相邻站位和农场类型任务；运行时不得直接写 Chest、库存或任务。

`runtime-starter-gift-20261024-172156` 已从更新后的桥采集同一真实隔离新档的领取前/后 full snapshot 与原生回执：共享 BFS 到精确相邻格，405 tick 内由原生交互生成 `(O)472 x15`、任务 `6` 并移除空礼盒，状态哈希同步变化，只加载 TransparentBridge 与 RuntimeTestHarness。`EVD-342` 因此关闭 read/candidate/compiler/runtime/output 五门，本项进入 training allowlist。该局部动作准入不改变全局 `CoverageGateReady 5/19`、Money production `0/18` 或 `formal_product_training=false`。

领取后 fresh 状态已完成真实快照离线后端回放：普通种子在 bridge 与 planner 两层均受显式 surplus authorization 保护，不能作为通用售卖/出货候选；Grandpa 不再越过最高优先级 `earn_money` 去执行社交，而是发布 `planning_goal_handoff.v1`，请求现有 `crop_production_teacher` 和按需 `farm_layout`。LiveTrainingLoop 已能完整采集并校验分块 Farm 布局，再复用唯一 Teacher、ledger、obligation 与 native run-plan builder 生成绑定哈希的调度清单。`runtime-crop-teacher-first-day-20261024-183952` 已让该 continuation 在真实隔离新档通过：9 个 bounded 布局分块、6 单位购种、场地准备、6 格播种、6 格浇水、回家、原生睡眠和 `1:spring:1 -> 1:spring:2` 的存档哈希变化均有 fresh 原生回执。之后代码层已把首日 planner 收敛为唯一全日期状态机，并加入按 run-plan SHA-256、承诺、日期和地块绑定的持久化收获 transition ledger；只有原生 `applied/verified` 结果可结算，部分队列成功可恢复。非终端日还新增独立 save-boundary 硬门：睡前指纹、下一日 fresh 日期和变化后的存档哈希必须共同写入 ledger，缺少任一回执不得执行后续日期。明确的作物战略漂移会重新采集 fresh full/Farm layout，并通过同一个 Teacher dispatch helper 生成后继 plan；旧 plan 哈希写入 lineage，最多重派发 8 次，基础设施证据缺失不得重派发。`runtime-crop-teacher-full-cycle-ledger-fix-20261024-211500` 已关闭该局部缺口：Teacher v2 在水壶 `40` 次容量、无补水路线的真实边界只承诺 `3` 格花椰菜和 `36` 次浇水；原生循环跨 `12` 个唯一日期/存档边界运行到收获，写入 `3` 条逐地块 verified receipt，计划完成且未重派发。该结果证明作物生产交接、收获守恒和 commitment settlement 的运行入口成立，但不自动填充 Money 的正式 train/validation/test production cells，也不改变 `formal_product_training=false`。

## 当前准入状态 · 2026-10-21

正式训练准入没有变化：

```text
CoverageGateReady:       5/19
Money production cells:  0/18
Formal product training: false
```

Golden Walnut 新档日程的证据保存硬伤已修复，但准入结论不变。LiveTrainingLoop 只有在显式启用捕获、提供稳定 run ID、选择最大 Grandpa 分数目标并使用 `full + content_addressed_gzip` 时，才会归档 success + fresh episode；首条记录还必须证明 day 0、同一 save/run/state/tick、姜岛/鹦鹉均未推进，并逐行核对完整且零进度的 `69 keys / 130 walnuts` 初始目录。前后快照清单和 episode 使用 rolling retention 不会删除的耐久别名。真实隐藏静音运行 `golden-walnut-starter-fix-v6-20261024-2140` 已从 Spring 1 连续运行到 Spring 13，产出 `82` 条 verified episode、`12` 条原生日期/存档边界和 `3` 条逐格收获回执；manifest 接纳 `82/82`，作物 Teacher 零重派发完成。连续性只允许最多 `4` tick、同 save/player/day/time 且姜岛访问与完整核桃来源目录不变的被动观察间隙；有界 `farm_layout` 也显式携带该来源域，没有放宽证据门。正式 schedule 导出仍准确拒绝该链，因为其中没有首次登岛原生跃迁。因此当前只关闭多 episode 归档与连续性基础设施，不关闭 51 来源、三次鹦鹉跃迁或 Master Angler 审计，也不能提升 coverage 分子或解除 `formal_product_training_authorized=false`。

该首轮的排序产物还证明最大分数目标别名和编译边界存在断链：`grandpa.maximum_21` 被旧解析器当成无关目标，62 个候选同分后按 ID 误选商店。现在三种最大分数 ID 由 Contracts 单一契约识别，运行脚本传权威 ID；解析成功后，完整候选仍留在响应中供审计，但日计划只编译 `bound_candidate_ids`。同一真实 Spring 1 快照的离线回放已解析到 `raise_friendships` 并绑定 28 个 NPC 候选。`initial-snapshot.json` 同时改为保存桥返回的原始 JSON，避免 PowerShell 反序列化/重序列化造成 state hash 不自洽。聚焦回归 `164/164`；这只恢复 Teacher 目标到候选的正确入口，不等于模型训练、连续新档证据或正式准入。

采集脚本此前虽然声明最多 8 次尝试，却把退出目标固定为 1 个 verified 动作；因此首轮得到 1 条 episode 是入口配置的必然结果。现默认目标改为 `4 verified / 8 attempts`，成功后还必须完成现有原生跨日存档边界，并同时验证 verified、episode 与 boundary 报告字段。该变更只修复下一次证据采集的退出条件；尚未产生新的真实运行，所以不改变任何 coverage 分子或 `formal_product_training_authorized=false`。

`money-crop-native-20261021-v10-continuation-v15-full-chain` 已完成本轮 Money 有界退出条件，包含 `26` 条连续 transition、`8` 个作物批次、`8` 次单件投箱和 `6/6` checkpoint，源存档保持不变，最终状态哈希为 `bb70acbd0b477b7e14d7fe1b4450ee85292fa29e56de854924e4ee67f1216849`。它没有跨越并接入 Money criterion-local cell，因此 `0/18` 不变。

Community Center、Pet Love、Skull Key 与 Full Shipment 已通过 current-authority 复核。Full Shipment 由 `154/154` 静态来源/编译完整性、`26/26` 原生执行签名分层和既有三分区 Teacher/native 证据共同关闭训练前置；完整 154 项 recurrence 仍是正常游玩与最终验收能力。新 reconciliation SHA-256 为 `71f0e4ed0e7928c1e63f39641f4b90153674f154a575ec331342a3d5241adf43`。当前准入指标为 `5/19 executable、6/19 split-complete、5/19 CoverageGateReady、4/11 root ready`；下一项是 Master Angler 顶层接线，而不是开始正式训练。

Master Angler 的当前日机会生成器和蟹笼高层生命周期均已由当前源码闭合：前者把 Stage 1 全窗口索引与当前动态查询、路线计时、可钓 tile、鱼竿装备和完整原生 spawn outcome 绑定到既有 `fishing.catch_fish`，后者只通过 `fishing.collect_crab_pots` 一个模型出口复用既有制作、放置、装饵、跨日和收取动作。EVD-339 进一步用原生个人 `CraftingPage` 回执补齐 `executor.craft_crab_pot` 的 runtime evidence，未把测试 fixture 材料误当作新档供应证明。新增 fresh-save prerequisite audit 证明 72/72 均有至少两个静态日历日，但也证明“当前可执行”不等于“新档可达”。钓鱼等级现通过显式 recurrence 复用 `raise_skill_levels` 的精确 XP 生产者，并要求原生阈值、同一截止预算和逐次 fresh 回执，未增加第二个模型出口。普通矿井地点生产者同样复用唯一 `mining.reach_depth`：从矿外只派发一条透明路线或原生大厅入口 primitive，fresh snapshot 后继续既有普通矿井下行链；floor `20/60/100` 覆盖当前全部普通矿井鱼种需求，且与 Skull Cavern、Volcano Dungeon、Quarry Mine 严格隔离。鱼竿生产者复用 `economy.buy_supplies` 的完整 FishShop 路线与单件购买链，锁定 `(T)BambooPole x1`、价格上限 `500g` 和 fresh 回执；竹鱼竿足以形成全部 72 项合法捕获，其他鱼竿、鱼饵与浮漂只影响效率。最新审计剩余 `8` 个未绑定族，SHA-256 为 `3c4016ecdadbc5964147d434578dd86c5ce8498adb39ed1cad48938430508e09`。训练前仍须绑定天气保留或 Rain Totem、蟹笼供应、沙漠巴士、姜岛、Mutant Bug Lair、秘密森林或森林农场、Rusty Key/下水道与女巫沼泽，再把它们与随机重试及 Year 3 截止证明组合。因此正式训练继续禁止，准入分子不增加。

沙漠巴士已从上述 blocker 中移除，但这不改变正式训练结论。实现把 BusStop→Desert 与 Desert→BusStop 投影成共享 `native_bus_transport` connector，继续复用 `executor.traverse_connector` 和 `fishing.catch_fish`，并在编译前检查 `ccVault`、司机/自驾、动态票价、余额、坐骑和返回触发状态。原生对话、过场与地点/tile/钱款回执均在同一运行时路径定义；当前只取得锁定 `game-1.6.15` 源码核对、编译、定向单测和防篡改依赖审计，未取得新的真实游戏回执，故不增加 runtime 或训练分子。fresh-save prerequisite 审计 SHA-256 为 `40e3df4d946f9dbf09cf70c399f994b94e7c3038b619f76e7108e05fccd73890`，剩余 `7` 个未绑定族：天气保留或 Rain Totem、蟹笼供应、姜岛、Mutant Bug Lair、秘密森林或森林农场、Rusty Key/下水道、女巫沼泽。`formal_product_training_authorized=false` 保持不变。

Master Angler 的 Rusty Key/下水道组合也已闭合到现有上游根：`EnterSewer` 在 `OpenedSewer` 已有时一次进入，只有 Rusty Key 时必须先完成一次原生开锁文本并观察 mail，再执行第二次原生交互。透明门、候选、共享 connector 和运行回执均按该锁定源码顺序失败关闭；依赖审计同时要求 `obtain_rusty_key` 的 terminal、recurrence、native event 与 Master Angler 三条组合边存在。该组合不提升 `obtain_rusty_key` root 的正式状态，其 60 件安全捐赠与三分区证据仍待固定队列第 6 项完成。fresh-save prerequisite 审计 SHA-256 为 `00042d57ed53321e9b7510aa6dc1d8c5502aa11d16496513ced7146bb4e3f04c`，剩余 `6` 个未绑定族：天气保留或 Rain Totem、蟹笼供应、姜岛、Mutant Bug Lair、秘密森林或森林农场、女巫沼泽。尚无本切片真实游戏回执，正式训练和所有准入分子继续不变。

蟹笼供应不再是训练前未绑定族。实时隔离快照锁定 FishShop 的等级门和价格：钓鱼 `2` 级出售 `(O)685` 鱼饵 `5g`，钓鱼 `3` 级出售 `(O)710` 蟹笼 `1500g`。实现继续只暴露 `fishing.collect_crab_pots`：远程服务缺饵时先买一个鱼饵；精确随机容量不足时先尝试既有原生制作，制作不可用才购买一个蟹笼，随后保留 continuation 并 fresh replan。该接线复用共享路线、商店交互、购买、关菜单、制作、放置、装饵和收取实现，未创建第二套执行系统。定向回归与防篡改自测通过，但新增运行时材料仅是实时商店投影，不冒充实际购买回执。fresh-save prerequisite 审计 SHA-256 为 `8cd925d8ede0a0c5d3e5b51817095958ffdd7f5ff100d588400f8dda2f46204b`，剩余 `5` 个未绑定族：天气保留或 Rain Totem、姜岛、Mutant Bug Lair、秘密森林或森林农场、女巫沼泽。正式训练和全部准入分子仍保持关闭。

天气控制先完成已有 Rain Totem 的原生消费叶。`dependency.complete_master_angler.consume_owned_rain_totem` 复用 EVD-288 的 `executor.use_rain_totem`，严格绑定 `(O)681 x1`、context 路由、默认节日排除、明日有效天气 Rain 和 fresh 回执；防篡改自测通过。该阶段审计 SHA-256 为 `7dd897e3cfb2a52a5016c4f342eef003b6e6848f5d12d7d44f46aae2f84468a9`。

透明桥已进一步补上 `player.rain_totem_crafting` 的只读生产前态：实时发布配方是否已学、精确材料可用量、可制作数量、产物容量和 Workbench 材料来源，并与 Crab Pot 共用同一精确单产物扫描器。依赖图严格锁定 `rain_totem_crafting.v1`、Foraging `9`、`709 x1 + 432 x1 + 726 x5 -> (O)681 x1` 及只读边界；篡改 schema 会使天气状态退回。它不等于 `executor.craft_rain_totem`，也不证明材料来源或跨日调度已经闭合；天气族当前为 `runtime_consumer_and_crafting_read_bound_executor_supply_scheduler_missing`，最新审计 SHA-256 `09d9053c264208f11e95b1080da7b5d4db0958a790aa18d1f2640d76cd4d4748`，剩余 `5` 个未绑定族与正式训练门均不变。

明日天气支援现由权威生产器和共享候选链闭合：它只接受哈希锁定的 72 项窗口索引、精确缺鱼集合、明日仅雨静态窗口、当前装备、location-context 与最终天气投影，并把意图在排名前作为默认候选的附加项注入。唯一高层出口仍是 `fishing.catch_fish`；制作与使用均为编译器拥有的机械 primitive，每步后强制 fresh replan。当前天气族状态为 `runtime_consumer_crafting_read_and_scheduler_bound_material_supply_and_native_crafting_receipt_missing`，审计 SHA-256 `ee97bff20a4991a8f4db70b6b7cf7e6ad9b1a3749ec89b9a048362c281eb9572`。尚缺 `(O)709`、`(O)432`、`(O)726` 的 fresh-save 生产者和一条真实原生 Rain Totem 制作回执，因此训练禁令不变。

截至 2026-10-23，Pine Tar、Pig/Truffle Oil 与现有 Hardwood 来源的长期 reserve 已接入，Axe 升级也已具备源码锁定的透明投影、候选、编译和原生 Runtime Harness 路径。该升级路径尚无真实提交与两日后领取回执，两个 executor 的 runtime/output gate 仍为空；不得因为代码存在而计入训练准入。天气族接下来的固定顺序是材料消费 reservation、原生 Rain Totem 制作回执，然后才重建 Master Angler fresh-save 审计与 Year 3 截止证明。

材料消费 reservation 与原生制作回执现已按上述顺序完成。日计划编译前会为目标日和 location-context 生成稳定 portfolio/source-decision 身份，把个人背包或 Workbench 的精确节点、槽位、物品和数量作为一个原子组合提交；竞争目标的预留仍会阻塞，只有本次 Rain Totem 的完全匹配预留可被制作使用。运行时只有在 `executor.craft_rain_totem` 原生回执为 applied/verified 且 after snapshot fresh 时才提交三项材料结算，结算失败会把该回执降为不可训练并保留重试标记。EVD-340 的 E 盘隐藏静音运行证明 `(O)681 0 -> 1`、`timesCrafted 0 -> 1` 和精确材料/回调验证；它不证明新档材料生产与截止日前可达。下一固定步骤因此是重建 Master Angler fresh-save prerequisite audit，再据实际残余 blocker 完成 Year 3 截止证明；`formal_product_training_authorized=false` 和全部准入分子保持不变。

上述审计现已重建，并补上“配方要求觅食 9 级”这一旧版未枚举前置。Master Angler 天气续程在配方未学会时复用已有 `foraging.chop_wild_tree` 原生动作，只接受完整正向 XP 投影并在每棵树后 fresh 重算；依赖图分别校验该 progression、Hardwood/工具升级 reserve、三材料原子 reservation/settlement 与 EVD-340 制作回执，防止用单一布尔值冒充闭环。天气族因此可记为 `implemented_and_bound`，但整个目标仍有 `4` 个未绑定族：姜岛、Mutant Bug Lair、秘密森林或森林农场、女巫沼泽；Year 3 全局截止组合和 seed-robust 随机重试也仍未完成。正式产品训练继续禁止。

秘密森林/森林农场替代分支随后通过严格依赖节点关闭：实时农场类型决定 `Farm_Forest -> Farm` 替代是否成立；非森林农场必须依次满足透明 Secret Woods 门、共享 Axe 升级生命周期、Forest `(1,6)` 的精确 vanilla 602 hollow log 清除和原生 Woods connector。该实现没有新增模型动作或平行执行器，且任何钢斧等级、原木身份、共享执行器或 recurrence 边漂移都会失败关闭。当前只有源码锁定、编译、定向执行链测试和图篡改负例，尚无 602 原木真实运行回执，故该用途仍为 calibration-only，不进入正式训练。最新审计剩余 `3` 个未绑定族：姜岛、Mutant Bug Lair、女巫沼泽；完成它们后仍须组合 Year 3 截止与 seed-robust 随机重试证明。`formal_product_training_authorized=false` 不变。

共享 Dark Talisman 依赖节点现已关闭 Mutant Bug Lair 和 Witch Swamp 两族。它精确组合既有 Community Center/Joja 完成事实、Railroad `529952/C`、quest `28`、Krobus 原生交互、BugLand `(31,5)` 的唯一 `SpecialItem(6)`、Railroad tile `287` 与 WitchWarpCave `MagicWarp`；BugLand 与 WitchSwamp 按各自最短充分终态停止，不要求 BugLand 目标无意义地完成后缀。SpecialItem 宝箱与 Skull Key 共用唯一原生两阶段状态机，Krobus 与雕像只调用原生 `checkAction` 并验证 fresh 后置条件，代码不直接写进度。当前证据是锁定源码、透明投影、编译、`5/5 + 16/16` 定向测试与依赖防篡改自测，不是原生运行回执。fresh-save prerequisite 审计 SHA-256 为 `74460f8612d92ab09ead68aa2c5aeedd7eaa5aaed8dc1a6ced923f84b5578892`，静态未绑定族只剩 `ginger_island_access`；姜岛完成后仍必须建立 Year 3 截止组合和 seed-robust 随机重试证明，故正式训练继续禁止。

姜岛当前已达到“原生访问终端和当前快照即时生产者可编译”，但尚未达到 fresh-save 依赖完成。锁定 1.6.15 的顺序要求为 Willy 邀请、Battery Pack `5`、Hardwood `200`、Iridium Bar `5`、船票 `1000g`、出航事件/BoatJourney，再依次支付第一只鹦鹉 `1`、Western Turtle `10`、Island House `20`、Resort `20`；因此到 Master Angler 所需 IslandSouthEast/IslandSouthEastCave 的累计核桃依赖为 **51**。当前代码复用既有机器、清障、出货结算、灌木、钓鱼、掉落拾取、确定性挖掘、金椰子和田野办公室调查动作，每步 fresh 重算且不直接写进度；定向测试 `7/7`、构建零警告零错误。该结果不允许把审计中的 `ginger_island_access` 标为 bound：现行透明来源候选仍受 current-location/loaded-state 限制，尚缺跨地图未加载的 51 核桃完整来源目录、可达路线、一次性 tracker 去重与随机来源的 seed-robust 重试预算。正式准入顺序固定为：先补全并防篡改验证这套 51 核桃权威投影，再重建 Master Angler fresh-save prerequisite audit，最后组合 Year 3 截止和随机重试证明；在此之前 `formal_product_training_authorized=false`。

其后的来源目录切片已经把“51 是否存在”从文档假设变成机器分母：原生总分母为 `69 keys / 130 walnuts`，在排除 Resort 后置来源与未购买的可选 Field Office Bridge 后，当前已绑定既有执行器的来源容量精确为 `51`。透明桥逐源发布当前 tracker/limited-drop/world/mail 收据；固定埋藏点实时调用 `IsBuriedNutLocation`，日记点同时核对坐标、日记 ID 与 `Diggable`；随机来源只允许一次原生尝试后 fresh 重算。普通埋藏与日记埋藏均复用同一 `executor.till_soil`，队列编译时再次校验来源键、坐标、剩余量和实时状态，漂移即拒绝。跨地图候选只编译一段已解析 connector。相关回归 `42/42` 通过，但没有本切片真实游戏回执。训练门仍关闭：还必须机器证明 `1+10+20+20` 支出过程中每个阶段有足够可达来源，并为钓鱼、蚌结石、岛屿作物等随机来源建立 Year 3 截止内的 seed-robust 重试预算，然后重建 Master Angler fresh-save prerequisite audit。容量 `51` 不得直接记作 `ginger_island_access=bound`。

四阶段容量和独立重试预算随后已进入同一权威审计。`golden_walnut_unlock_schedule_proof.v2` 证明当前已接线来源累计容量 `7/25/51/51`；随机路线在 Resort 前缺少的确定性 `15` 可由 House 后三个 Gourmand 请求各 `5` 个核桃精确替代。`island_gourmand_progress.v1` 与唯一动作链已覆盖请求序号、254/262/248 作物、成熟地块、洞穴 Action、原生事件、碎片拾取和 fresh 回执。EVD-341 现以隐藏 E 盘隔离运行证明引介与第一单原生交付：request index `0 -> 1`、`GoldenWalnutsFound 0 -> 5`、`IslandGourmand1` tracker、五碎片生成/拾取和锁释放全部 verified，且没有直接进度或奖励写入。因此原生交互回执不再是缺口，`executor.advance_gourmand_request` 已为 `RuntimeVerified`。`island_gourmand_supply_schedule.v1` 又以精确种子/作物 ID、SeedShop 季节与年份条件、原生 phase 数组、IslandWest 全年播种规则及现有购买/农作/跨日动作闭合三株供给：大蒜须在第 2 年春季预留，夏 1 起备齐甜瓜和小麦，三株并行生长 `12` 夜后保持未收获状态供三次原生事件依次观察。实际价格必须在购买时 fresh 读取，`130g` 只代表标准价格。Resort 相关候选已把 Turtle `211`、House `223` 两个边界绑定到回执，并在 Turtle 未完成且当前日超过 `211` 时上游失败关闭；路线证明也能接收实际日期并验证 `211/223` 成功、`212/223` 失败。该接口不会把“公式可算”冒充“日期已产生”，也不会用选路完成代替第二、三请求的连续原生回执。默认仍须由 fresh-save 日程给出实际日期，因此 `RouteSelectionComplete=false`、`TrainingAdmissionEligible=false`，正式训练禁令不变。回归 `26/26` 与 Bootstrap Release 构建通过；全量 hermetic 自检仍被既有 Money Revenue seed supporting-step fixture 的缺失 snapshot path 阻塞。

正式日期证据现在必须经过 `golden_walnut_fresh_save_unlock_timeline_proof.v1`。该投影要求上游新档计划输出首次登岛日、每个确定性核桃来源的计划完成日，以及 First Parrot/Turtle/House 三次原生交互的计划执行日，并用原文件 SHA-256 绑定；它从来源日程计算 `1/11/31` 三个钱包就绪日，再验证实际计划交互日不早于就绪日。任何未知、重复、超原生数量、随机、未绑定、前置阶段尚未开放的来源，或钱包未就绪就安排支出的计划都会失败关闭。Master Angler 审计只有在传入 `--golden-walnut-source-schedule` 且投影完整时才关闭 Gourmand 日期组合；目前仓库没有这份真实新档产物，所以训练准入分子不变。下一准入顺序固定为：上游 Teacher/日计划导出真实来源与交互日程并重建审计，再在同一连续原生存档采集 Gourmand 第 2、3 单回执；两者都完成前不得开始正式全量训练。

日程导出不读取“候选带了哪个 source key”来推断完成，因为路线步骤也会携带该参数。正式入口 `build-golden-walnut-fresh-save-source-schedule` 只接受显式、同 run/save、连续性可证明的成功 fresh episode 清单，并从每对前后透明快照中的来源数量增量与原生解锁状态跃迁生成 schedule；schedule 还必须携带 manifest SHA-256、全部 episode/快照的有序证据根及 run/save 身份，时间线证明拒绝无来源归属的旧格式。真实 `82` episode manifest 已证明归档器和连续性门可工作，但导出器因缺 `native first-island-arrival transition` 正确失败。

Golden Walnut 专用长期里程碑控制面现已实现，但尚未形成新原生日程。长期模式必须显式启用证据捕获、日计划和原生存档边界；每个 fresh 前后快照都核对锁定版本、可读存档、完整 `69/130` 目录身份、逐行数量与真实来源增量，并要求首次登岛、First Parrot、Western Turtle、Island House 当前终态齐备。作物 Teacher 第一日完成不会再触发短跑退出，最后一个预算内动作的 after snapshot 也会参与判定。`total_days >= 224`、attempt、磁盘余量或连续错误上限均会失败关闭。即使当前快照达到里程碑，正式成功仍须用同一 manifest 的 existing schedule builder 重放全部 episode，证明四个原生 `false -> true` 转变并写出 source schedule；当前态不能替代历史证据。下一准入顺序是运行一次受限长期新档、导出并绑定 schedule、重建 Master Angler 审计，再采集 Gourmand 第 2、3 单连续回执。`formal_product_training_authorized=false` 与全部 coverage 分子保持不变。

Money 的第一条完整原生作物生产段 `money-crop-native-20261021-pricefix-v10` 已通过：checkpoint v12 为 `6/6 verified、0 pending`，终局含 `5` 次单件原生投箱和唯一睡眠结算，实际收入 `875g`，最终为春 20 日 06:00、`money=925`、`totalMoneyEarned=875`。源存档和隔离 Mod 哈希均未漂移。这证明 native runner、购种、场地准备、作物生命周期、留存、容量和运输箱终局可以在同一连续真实轨迹中闭合。

`money_revenue_crop_native_production_segment_receipt.v1` 已完成并对 v10 深回放：Teacher 决策、commit receipt、obligation/run plan、checkpoint、terminal recurrence、最终透明快照和最终隔离存档树均从路径与哈希重新核验。回执确认 `50` 个 Teacher 候选、选中 `5` 格 `(O)474`、实际购种成本 `400g`、毛收入 `875g`、净现金收入 `475g`，并正确输出空阈值集合；它不创建 Teacher comparison、Money cell 或训练授权。

`money_revenue_crop_production_teacher_comparison_receipt.v1` 已关闭 segment-level comparison：完整保留 `50` 候选、`5` admitted、`45` blocked 及 `4` 条选中候选偏好对，并只把真实成本/收入绑定到唯一执行候选。它不把 counterfactual 推演冒充原生结果，也明确保持 `criterion_local_join_ready=false`。

**准入规则没有降低：**v10 生产段尚未跨越最低 `50,000g` Money criterion，v15 也没有形成 criterion-local cell，因此不能增加 `0/18` 分子。Full Shipment 的提升来自机器可验的静态全分母、原生分层全覆盖、顶层依赖一致性和三分区双通道共同通过，不是删除游戏能力或缩小目标；当前按 canonical 复杂度队列进入 Master Angler。Money 最终返回时，仍只有累计原生轨迹真实跨越阈值，并且相应 train/validation/test 分区的 exact comparison 和 native outcome 均闭合，Money cell 才能入账。

## 已收口待后续证据 · 2026-10-18 Money 种子预留 proof

crop-production checkpoint 已演进到 v12，并在 `v10` 中真实重建 planting-layout、seed-reservation、seed-acquisition、daily-capacity、native-lifecycle 与完整 terminal-settlement 六个请求。output retention 与 settlement capacity 仍是支持片段，不能单独关闭 terminal request；本次单波运输箱 proof 已重建资源中性路线、五次原生投箱与 Revenue recurrence。该层已无 pending request，但阈值、Teacher comparison、partition join 和正式训练门仍保持关闭。
## 已收口待后续证据 · 2026-10-18 Money 作物场地准备

作物场地准备已通过有界 `farm_layout` 透明快照和既有清障/资源块/BFS/锄地候选形成执行计划；清障后必须 fresh snapshot 重绑。#311 已关闭 bounds overflow、BFS reachability、stamina gate 及相关作物分母 fail-closed 缺口。该层仍只是基础设施，不产生 production cell；Money 保持 `0/18` 且 `formal_product_training_authorized=false`。
## 历史记录 · 2026-10-16 Money 作物布局 proof checkpoint

Money 未来作物证据分母新增逐承诺 planting-layout 请求，并用 planting-day fresh snapshot 验证精确 Farm 地块、原生种植许可和基础生长时长。`money_revenue_crop_production_proof_checkpoint.v1` 只统计能够从自身 request 重新构建且仍在原计划分母内的 proof；当前 schema 仅支持布局片段，所以即使布局已验证，种子、容量、生命周期、输出保留与终端结算仍全部独立 pending。该 checkpoint 不产生生产格，不授权 fresh-save 百万日程或正式训练，Money 保持 `0/18`。


## 历史记录 · 2026-10-15 Money 采证波次与 18 格分母分离

`money_threshold_evidence_work_plan.v1` 新增原生采集波次和比较采集波次，但 `6 x 3 = 18` 的正式分母不变。原生波次按分区聚合：一条独立轨迹可以同时跨越多个阈值，所以空 corpus 的理论首轮为 train/validation/test 各一个条件性波次；只有真实源状态低于最低缺失阈值、且真实结果达到最高缺失阈值时才成立。无法一次跨越时按真实候选拆波，不能降低分母。

比较波次按 verified native row 聚合一次 denominator 初始化，再为该 row 跨越且仍缺 comparison 的 criterion 分别建立 exact admission。不同 criterion 选择不同 native outcome 时不得共用 seed。波次是施工调度，不是 evidence、Teacher 标签或训练授权；当前生产仍为 `0/18`，`formal_product_training_authorized=false`。

实际执行时，计划中的最高缺失阈值只是条件性上界。实时 wave selector 必须从完整 Revenue denominator 选择当前可达的最高缺失阈值，并继续使用原单阈值 selector 取得最小充分动作；只接纳原生结果真实跨越的 criterion，剩余格在 batch 重建后继续调度。没有可推进候选时在动作入队前失败关闭。该自适应拆波减少无效启动和人工转录，但不放宽 `18` 格分母、partition 隔离、comparison admission 或 formal gate。

comparison 波次必须通过版本化 handoff 连接 work plan、corpus row、native run summary 与 comparison collection plan；completion 只允许处理 handoff 中仍缺的 criterion，并把新 admission 与既有 index 累积合并。handoff 的每个源文件在使用前按绝对路径和 SHA-256 重验，但它本身不是 comparison evidence；真实 acquisition rollout、strategy-ledger boundary、terminal proof、Pareto admission 与 criterion-local join 仍逐项必需。

首轮 `0/18` 状态由只读初始化脚本生成无 corpus 的固定 18-cell work plan；它必须显示三个条件原生波次且所有 evidence 计数为零。这个空计划只给采集器提供调度分母，不构成 corpus、native outcome、Teacher comparison 或任何训练准入分子。首条真实 native run 之后，计划必须由 replayed batch/corpus 重建。


## 历史记录 · 2026-10-14 Money 6x3 evidence work plan

新增只读 `money_threshold_evidence_work_plan.v1`，把六个权威 Grandpa Money 阈值与 train/validation/test 做笛卡尔积，固定形成 18 个 criterion-local 工作格。每格从重新验证的 Money corpus v3 独立计算 native outcome 和 Teacher comparison 是否存在，并列出实际 row ID；method 聚合、调用方计数和 self-test 目录名都不能改变该分母。

该计划解决“知道 Money 未完成但不知道该采哪一格”的调度问题，不是新的 coverage gate。缺 outcome 时必须先取得原生结果再做 exact comparison；已有 outcome 时只补 comparison；两者齐全才关闭单格。计划本身始终 `formal_product_training_authorized=false`。当时只有 hermetic `1/18 native、0/18 comparison、0/18 complete`，生产计数仍为 `2/19`、`1/11`。

## 历史记录 · 2026-10-14 Money criterion comparison join gate

Money threshold corpus v3 在同一个 source kind 内合并两条相互独立的证据通道：原生 sale episode 负责 native outcome，Revenue comparison admission 负责 Teacher preference。每个 criterion 独立携带 admission manifest/receipt 的路径与 SHA、比较 source state、完整候选数、唯一 preferred、全部 rejected、选中 terminal episode SHA 和 selection policy；多阈值跨越不能广播一份 criterion 不匹配的比较。

comparison receipt 已升级为 v2 并公开从 terminal execution proof 重建过的 native terminal artifacts。准入时会再次重建 admission 全链，并要求选中替代项的 sale episode、before/after snapshot、execution result、首条 Revenue transition、save/player、权威 threshold 和完整 pairwise 分母与 raw row 精确相同。缺文件、路径漂移、SHA 漂移、错 criterion、非唯一 Pareto 或 episode 不一致均失败关闭。

Goal Method coverage 仅在上述 join 成功时增加对应 partition 的 Teacher comparison；native partition 始终由原生结果独立记账。当时只有 hermetic 正负例，没有把 fixture 计入 production train/validation/test 分子，因此总体准入仍为 `2/19`、`1/11`，正式训练授权保持 `false`。当时后续计划是采集或重建 Money 六阈值的真实三分区 exact-denominator comparison/outcome 对，而不是放宽门槛。

## 历史记录 · 2026-10-14 Money threshold supervision-channel isolation

Money threshold raw corpus 已升级为 v2，并从此只代表 native outcome channel。成功出售、fresh snapshot 与阈值跨越不能单独证明该动作优于其他候选；
因此 raw row/criterion 不再携带 preferred/rejected action，也不再设置 Teacher comparison verified。Goal Method coverage 可记录其 train/validation/test
native outcome partition，但 comparison partition 必须来自独立的 exact-ID Revenue comparison admission。

这是一项防止伪标签的纠错，不增加 coverage。下一门是建立 criterion/partition join artifact：每个 raw outcome 必须绑定同 source state、同 criterion context、
同选中 terminal episode 的 comparison receipt，之后才可计入 Money-family Teacher comparison。

## 历史记录 · 2026-10-14 Revenue exact-ID comparison admission gate

`money_revenue_comparison_admission_receipt.v2` 只接受完整 terminal execution receipt，并从其源 request catalog 取得唯一候选分母。
request ID、terminal proof ID 与 terminal evidence ID 必须逐集合完全相等；concrete alternative ID 只能由 request 映射获得。direct 候选使用 terminal cost，
rebound 候选通过既有 artifact verifier 重建 acquisition cost/reservation 后与 terminal cost 聚合。Revenue prefix 还必须与 terminal transition、source save/player
和 source `total_money_earned` 连续。

完整分母进入现有 reserve-aware strict-Pareto comparer。只有唯一前沿才产生 production Teacher preference eligibility；不可比较前沿是一个合法但无标签的结果，
不得任意 tie-break。即使唯一偏好成立，receipt 仍强制 `formal_product_training_authorized=false`，因为 Money criterion 的独立 train/validation/test 原生 outcome view
尚未消费该偏好。当时正式覆盖与根方法保持 `2/19`、`1/11`。

## 历史记录 · 2026-10-14 Revenue terminal execution proof gate

`money_revenue_terminal_execution_proof_receipt.v2` 现在消费完整 counterfactual request catalog，并要求每个 request ID
恰好对应一条可重放终端证明。它从 request 已锁定的 terminal snapshot/ledger 出发，允许零条或多条仅含共享资源中性动作的 route receipt，
随后验证现有原生 shop sale 或 shipping deposit，并把结果连接到现有 Revenue recurrence receipt。路由前后由版本化 persistent-state boundary
排除库存、货币、精力和未知持久状态漂移；真实 elapsed ticks 统一换算为 terminal opportunity cost。

外部 strategy ledger 不再以两个裸路径作为边界证据。动作前后必须分别提供 `strategy_ledger_boundary_capture.v1`，其内容寻址 ID 绑定 before/after 阶段、request、concrete alternative、原生执行 run、快照 SHA/state/tick 与账本 SHA/ID/revision/source-state/actor；证明器从实际文件重新构造制品并精确比较。动作前账本仍必须等于 request 推导出的 path/SHA/ID/revision/source-state 锚点，动作后必须以不同路径保存完全相同的字节与 SHA，两个 capture 文件和 ID 也必须独立。
目标库存槽位同时受透明 material graph、actor ownership 和共享 `MaterialSupplyProjection` 约束；active reservation 占用后的可用数量不足即失败关闭。
终端 after snapshot 还必须证明同一槽位按精确数量减少，避免仅凭收益变化或执行器声明推断物品已被消费。

该 receipt 明确保持 `production_teacher_preference_label_eligible=false`：它尚未在全 request ID 分母上执行严格 Pareto comparison admission。
因此正式覆盖、根方法和授权仍为 `2/19`、`1/11`、`false`。下一门固定为只消费本 receipt 的 exact-ID comparison；在那之前不得把终端执行证明直接写成正标签。

## 历史记录 · 2026-10-14 Revenue counterfactual proof request denominator

`money_revenue_counterfactual_proof_request_catalog.v1` 从 authoritative concrete catalog 一次性生成完整采证分母，并
绑定同一 source state 的 strategy ledger。每个 concrete alternative 恰好对应一个 request；direct alternative 的
acquisition proof 必须为空，rebound alternative 必须保留原 rollout manifest/receipt。shop 与 shipping 分别声明
原生 sale 或 deposit+settlement proof，且全部要求可重放 Revenue recurrence。

每个 request 还固定 terminal strategy-ledger 起点。direct 路径只能复用 source ledger；rebound 路径必须重建 rollout
并从最后一笔已验证 route settlement 取得 ledger path、文件哈希、ledger ID、revision 与 source state。该锚点解决“后续
证明应从哪个 ledger 版本开始”的歧义，但不冒充执行期间的 before/after ledger 连续性或 reservation 安全结论。

该切片同时关闭一个规划层遗漏：acquisition opportunity cost 不等于整条 Revenue alternative 的总成本。最终去商店、
去出货箱和终端交互同样消耗时间，不能被 comparison 当作零。因此所有请求现在都要求独立 terminal opportunity-cost
proof。外部 strategy ledger 的 material/currency reservations 也必须在终端消费前单独验真，不能仅依赖游戏内自动出售
保护标记；因此全部请求同时要求 terminal reservation claim proof。现有仅含 acquisition 成本与 claim 的 comparison
receipt 继续是非权威诊断，不能用于生产偏好。

密封测试证明 `8 concrete -> 8 requests`，其中 `3` 条带 acquisition rollout、`5` 条 direct 不伪造 rollout、
`5` 条 shop sale、`3` 条 shipping deposit+settlement；陈旧 strategy ledger 会失败关闭。当前只达到
`counterfactual_proof_collection_ready=true`，尚未获得执行结果。下一准入门是从该终端账本锚点出发，证明共享 terminal timing/cost、执行前后 ledger 连续性与 reservation
证明、逐请求 native recurrence receipt 和严格 ID 对账；正式覆盖、根方法与授权保持 `2/19`、`1/11`、`false`。

## 历史记录 · 2026-10-14 Revenue concrete alternative denominator

`money_revenue_concrete_alternative_denominator_catalog.v1` 现将完整路径骨架分母分成两类后重新合并：direct
sale/shipping 直接绑定源快照候选；需要 acquisition/processing 的骨架消费已重放验证的 terminal-rebind 证据，并可
一对多展开全部合法商店或出货终端。每个 concrete alternative 保存完整 source/terminal candidate、路径、proof shape、
terminal state，以及需要时的 rollout manifest/receipt 路径与哈希。

构建器不接受调用方填写 concrete ID。alternative catalog、rebind manifest 和 rebind receipt 都必须重新构建且互相绑定
同一 source state/catalog；每个骨架至少贡献一个 concrete alternative，direct 必须恰好一个，ID 和内容哈希不得重复。
密封 fixture 的 `7 skeleton -> 5 direct + 3 rebound -> 8 concrete` 通过，漏掉任一 rebound 证据会拒绝。

`current_concrete_denominator_authoritative=true` 只证明当时分母不重不漏，不证明这些候选已有隔离执行结果。下一准入门
是按该目录生成 counterfactual proof requests，并让 comparison receipt 的候选 ID 集合与 catalog 完全相等；direct
候选不能被迫伪造 acquisition rollout，rebound 候选也不能省略已验证 rollout。完成每项原生 Revenue recurrence 前，
production label、正式覆盖和训练授权均不变：`2/19`、根方法 `1/11`、
`formal_product_training_authorized=false`。

## 历史记录 · 2026-10-14 Revenue fresh terminal candidate rebind

`money_revenue_terminal_candidate_rebind_receipt.v1` 现在把所有需要结果态重绑定的 Revenue 路径骨架，与各自完整
acquisition/processing rollout 建立可重放关系。每份 rollout manifest、rollout receipt 和 alternative denominator
都会从叶子工件重建并逐 JSON 核对；骨架源候选必须在 rollout 中精确命中一个已执行 transition。

实际产物身份来自该 transition 的已验证 fresh terminal receipt。构建器随后对前后快照按物品、最低品质和槽位计算
净增量，只把真正增加的槽位交给现有 Revenue candidate evaluator。旧库存中的同物品不会进入本路径；同一新槽位若
存在多个合法商店候选，则全部保留，避免用“唯一商店”假设漏掉价格与路线分支。出货候选同样保留原生 bin、槽位、
实时单价和 continuation 参数。

此层闭合的是 terminal rebind 子分母，不是最终 alternative 分母。direct sale/shipping 骨架与 rebound concrete
候选的联合目录仍待建立，comparison manifest 也尚未改成由该联合目录生成。故
`current_alternative_denominator_authoritative=false`、production label eligibility 为 `false`，正式覆盖仍为
`2/19`、根方法 `1/11`，正式产品训练未授权。下一门固定为 concrete union denominator，再接现有 native
sale/deposit/settlement 与 comparison proof。

## 历史记录 · 2026-10-14 原生出货入箱证据边界

原生 `executor.ship_inventory_item_to_bin` 现由单一共享验证器核验：队列末项与执行回执必须绑定精确库存槽、
物品 ID、品质、数量、实时单价、Farm 物理出货箱端点和 fresh after snapshot；库存总量、选中槽、所选端点内容、
待结算金额和 changed-facts 必须同时形成同一个一件物品转移。共享钱包下多个物理出货箱展示同一原生库存，
验证器只读取队列选中的端点，旧 Stage-One 汇总读取也改为要求各同 scope 视图一致并只计一次，不再重复求和。

入箱阶段必须保持玩家现金、累计收入和 total day 不变，待结算金额只能增加 `quantity * expected_unit_price`；
错槽位、错单价、错端点、入箱即到账、回执与透明快照不一致均失败关闭。隔夜到账继续由既有原生 shipping
settlement 验证器负责，两段证据不得混合。

该切片只闭合 Revenue 路径的 deposit proof shape，不代表获取或加工后的具体终端候选已经枚举完成。当时后续计划固定为：
从每个已验证 fresh terminal snapshot 重新构建候选目录，按路径骨架要求的 shop sale / shipping 终端和实际产物身份
做唯一重绑定，再进入比较证明。正式覆盖保持 `2/19`，根方法保持 `1/11`，正式产品训练仍未授权。

## 历史记录 · 2026-10-14 Revenue alternative skeleton denominator

- `build-money-revenue-alternative-denominator` rebuilds the current candidate catalog, then expands every eligible policy candidate through every simple authoritative dependency path to the unique settled-money milestone. Acquisition candidates branch into sale and shipping paths, machine capacity retains load/collect dependencies, and direct terminal candidates keep only their own settlement family.
- This is a complete path-skeleton denominator, not a concrete counterfactual denominator. Any acquisition, processing or stochastic action requires a fresh post-action terminal-candidate rebind; direct sale and direct shipping retain separate proof shapes, and shipping still requires both deposit and later native settlement evidence.
- Consequently `alternative_skeleton_denominator_complete` may be true while `current_alternative_denominator_authoritative`, long-horizon authority, production labels and formal training remain false. The next gate closes fresh terminal identity and shipping-deposit proof before comparison-proof binding.

## 历史记录 · 2026-10-14 Revenue current-snapshot candidate denominator

- `build-money-revenue-candidate-denominator` now derives its actionable-node inventory from the authoritative `earn_money` dependency expansion and delegates all live candidate semantics to the existing `CandidateOptionAvailabilityEvaluator`. It records every event/economic row, blocked exclusion, rolling continuation parameter and content digest without introducing a second planner or pricing implementation.
- Current-snapshot enumeration authority is deliberately separate from long-horizon authority. The current set fails closed on missing option evaluations, missing transparent fields, duplicate identities or candidate-kind drift. Long-horizon authority remains false while the dependency expansion still carries the fresh-save schedule, counterfactual opportunity-cost and crop-route blockers.
- The catalog alone cannot emit a production preference: it has no verified counterfactual rollout receipts. `production_teacher_preference_label_eligible=false` and `formal_product_training_authorized=false` remain mandatory. Its candidate IDs first enter authoritative dependency-path expansion; acquisition outcomes then require a fresh terminal-candidate rebind before any comparison alternative can be concrete.

## 历史记录 · 2026-10-14 Revenue comparison artifact binding

- `money_revenue_alternative_comparison_proof_receipt.v1` 现能深度重放每个候选的完整 acquisition rollout 与 Revenue recurrence，并逐 transition 核对确定性 opportunity cost、reservation claim、portfolio commit、起始 snapshot/ledger 以及 rollout 终点到收益起点的完整 Money checkpoint 连续性。
- 成本聚合已成为 acquisition portfolio 与 Revenue comparison 共用的唯一实现。比较仍只使用权威 Grandpa Money 阈值和严格 Pareto，不读 learner score；持久化 receipt 或任一叶子工件漂移会在重新构建时失败关闭。
- 该工件关闭了“真实路线/commit/rollout 没有绑定”的结构缺口，但没有凭空解决候选全集枚举：manifest 自报的 alternatives 不是权威 denominator。故其 production label、正式训练授权均强制为 `false`，当时准入继续是 `2/19`、`1/11`。下一准入门是建立由透明状态、权威 acquisition/processing route 与 Revenue terminal options 确定性生成的完整候选目录；之后才能把唯一 Pareto preference 计入生产分子。
- 本地仅完成 Release `0 warning / 0 error` 与快速 hermetic 回归。完整 Stage-One 成功链已挂接新增测试，但因已知 183 GiB 读取成本不在本机执行，且轻量 PR CI 不包含该项；故该成功链保持“待下一次计划内完整验证”，不能按已通过记录。

## 历史记录 · 2026-10-14 Recovery -> settlement 持久状态边界

- Full Shipment 中间 recovery 不再只守 Full Shipment 进度与待结算物品。`full_shipment_recovery_settlement_boundary.v1` 将 actor/save/game/day、inventory、Money/星星币/赌场币/Qi 宝石视图、累计收入、shipping bin、world progress、quests 和未显式声明的状态字段纳入稳定指纹。
- 只有移动位置、菜单、动画、当前场景候选投影等版本化瞬态字段可以变化；未知字段默认参与比较。合法 `traverse_connector` fixture 通过，Money 与 inventory 注入负例失败关闭。
- 此项不增加 criterion coverage，也不替代外部 strategy ledger 的工件连续性证明。当时仍为 `2/19`、`1/11`，`formal_product_training_authorized=false`；Revenue comparison 的真实 acquisition/ledger/counterfactual 绑定仍是主线下一块。

## 历史记录 · 2026-10-14 Revenue recurrence 可重放工件

- `money_revenue_recurrence_proof_manifest.v1` 现在显式列出 shop-sale episode 或 shipping queue/snapshots/receipt，不接受调用方直接填写收益金额。`MoneyRevenueRecurrenceProofBuilder` 逐边复用唯一原生验证器，随后重建同 actor/save、checkpoint 连续、Year 3 截止内的 prefix。
- `money_revenue_recurrence_proof_receipt.v1` 固化 manifest 及所有叶子工件哈希。双类型 hermetic 证明累计 320g，并拒绝伪造 queue selected-candidate 绑定；因此历史工件后续可确定性重放，而不是信任序列化 summary。
- 该 receipt 仍来自合成 fixture，不能计入生产分子。下一固定边界是 comparison manifest：必须把每个候选的 recurrence receipt 绑定到确定性 opportunity-cost report、strategy ledger/commit 与隔离 counterfactual rollout 后才可输出生产 Teacher preference。

## 历史记录 · 2026-10-14 Revenue 储备感知候选比较

- `MoneyRevenueAlternativeComparer` 从权威 Money criterion 内部取得阈值，要求完整候选分母、同一收益起点和同一 ledger revision。每个候选重新验真 Revenue 前缀，非标量机会成本必须与原子 reservation claim set 的材料/货币数量一致，且不允许替换既有储备。
- 选择策略为 `authoritative_threshold_then_reserve_aware_strict_pareto.v1`：先区分是否满足权威终态；终态状态相同时，才按阈值内收益进度、完成日、时间、能量、逐物品材料和逐币种成本做严格 Pareto。只有唯一权威偏好才输出 pairwise label；trade-off 与平局保持不可比，learner score 永不参与。
- 当前是 hermetic 类型边界，还未绑定真实 acquisition route 的确定性重建、ledger commit 证明和隔离 counterfactual rollout。它不能进入 production 分子，也不授权正式训练；下一开发块补齐这些 artifact binding 与 acquisition/processing 支持转移连续性。

## 历史记录 · 2026-10-14 Revenue 连续前缀

- `MoneyRevenueRecurrenceVerifier` 现将即时商店销售与原生隔夜发货结算组合为同 save/player、状态哈希连续且严格早于 Year 3 截止的类型化前缀。每个公开 transition record 都会重验金额、日期、shipping scope 与 kind 不变量，不能靠调用方声明绕过原生边界。
- 前缀输出保留首尾状态、首尾日数、现金与累计收入、总收益、两类边计数及完整 transition 序列。断链、伪造增量和截止日外记录 fail closed。
- 这仍只是连续收益证据结构，不是 fresh-save 百万元 schedule。后续比较层已经消费 reservation claim 与非标量机会成本，但真实路线重建和隔离 counterfactual 尚未绑定；正式训练准入不变。

## 历史记录 · 2026-10-14 原生发货结算证据复用

- 新增共享 `NativeShippingSettlementEvidenceVerifier`，把单一 sleep queue、fresh queue receipt 和精确 shipping Money progression 合并为一个 fail-closed 证明边界。
- Full Shipment 普通/终局 settlement 已复用该边界。只有 `player.money` 与 `player.total_money_earned` 都按前夜唯一 shipping scope 的 pending value 精确增长，才可继续进入各自的 collection/achievement 终态验证。
- 该层与 `NativeShopSaleEpisodeVerifier` 共同覆盖 Revenue recurrence 的两种结算方式；后续类型化连续前缀已能组合两类边，但仍缺少 fresh-save schedule 与 reserve-aware Teacher counterfactual，因此训练准入状态不变。

## 历史记录 · 2026-10-14 原生销售证据复用

- `NativeShopSaleEpisodeVerifier` 现为所有即时商店收益的共享原生证据入口，统一绑定 effective queue item、结构化商品/商店/槽位/数量/单价/总价、execution result 以及 fresh Money progression。
- Money threshold corpus 只从该证据派生 criterion-local crossed-threshold views。普通未跨阈值销售不会进入 threshold coverage，但可供当时后续计划连续收益 recurrence 使用，避免为了覆盖每一笔销售复制 corpus schema 或执行器。

## 历史记录 · 2026-10-14 Revenue recurrence 状态边界

- 新增共享 `MoneyRevenueProgressionVerifier`，作为 shop immediate settlement 与 native shipping day settlement 的唯一金额 progression 投影。Money 阈值语料已复用该层，后续 recurrence 不再重新实现 actor/day/money/totalMoneyEarned 读取。
- shipping-bin 以 `bin_scope` 去重；同一 scoped inventory 的多个物理端点必须报告相同 pending value 与 contents signature，结算收益只计算一次。即时销售要求 shipping scope 保持不变，隔夜结算要求金额增量精确等于前夜 pending 值。
- 该变更关闭了“物理端点重复计价”和“sale 与 shipping 收益混合归因”两类假证据，但仍不构成 fresh-save 百万元 schedule，也不改变 `formal_product_training_authorized=false`。

## 历史记录 · 2026-10-14 Money method-family 试点

- 覆盖门已从“method 证据广播到全部 criterion”改为“共享 method progression、独立 criterion evidence view”。Money 六档仍共享候选分母、执行器、原生 progression 和分区管线，但任一档缺失都会使 method 汇总保持未完成。
- `money_threshold_progression_corpus` 只接收原生 `executor.sell_shop_item` episode。它重算快照哈希、actor/save/day、执行结果绑定、原生 sale verification reasons、当前金额与累计收入的精确同增量，并从权威阈值表内部推导 crossed criterion；调用方不能自报阈值、criterion 或 partition。
- 每个 crossed criterion 均生成独立 `grandpa.criterion:<criterion_id>` Teacher 上下文和终态断言。隔离三分区测试只覆盖 50,000 档并确认其余五档不会被连带记账；这消除了共享方法导致的准入假阳性。
- 当时没有把 fixture 计入 production 分子，准入仍为 `2/19`、`1/11` 且 `formal_product_training_authorized=false`。Money 下一门是完成 fresh-save 收益 recurrence、储备约束与 counterfactual opportunity-cost Teacher，再接入真实三分区 episode。

## 历史记录 · 2026-10-14 当时准入基线

- 当时代码已在全新 `E:` 隔离根重建 acquisition corpus、checkpoint 与支持转移证明，并重新执行正式准入总门。机器报告为 `blocked_open_formal_training_evidence`：promotion-grade coverage `2/19`，coverage-ready root method `1/11`，`formal_product_training_authorized=false`。
- evidence-integrity 六门全部通过：19 项/21 分权威分母、三来源九行语料重放、非空 train/validation/test、checkpoint/corpus 绑定、holdout 重算和支持终端谱系 `6/6`。这批小语料只验证管线与证据约束，不能解释为正式 Product 数据规模充分。
- Teacher 阻塞归并为 `dependency_graph_incomplete:15` 和 `evidence_not_connected:2`。三个清障 executor 已将既有 `EVD-112/189/212` 接入唯一 capability source；重新生成 option matrix 与 acquisition lowering 后，透明读取、原生运行和 five-gate 三类告警均已消失，同时保持 executor calibration 的 `EvaluationOnly` 边界。
- 三个社交高层 option 已补齐显式 `Option -> candidate kinds -> DailyPlanCompiler -> executor.social_interact` 所有权登记，没有新增第二套运行时。直接高层执行继续被拒绝，只有日计划可以下沉到既有机械原语；关系升级继续保留玩家确认。重算结果为 `referenced_option_execution_inventory=true`、`runtime_product_promotion_ready=true`、下游 blocker 空。最新报告是 `E:\StardewAI-TestLab\artifacts\formal-training-admission-current-source-v1-20261014\formal-training-admission-reconciliation-social-pipeline.json`。
- 后续按 method family 批量闭合，不再按 19 个 criterion 逐项重复施工。第一优先是通用单调阈值 source/progression contract 和 Money 六阈值试点；每个 criterion 保留独立 target、Teacher goal context、终态断言与 coverage row，runtime progression、Planner/Compiler/Executor、partition plumbing 和 proof substrate 只构建一次。
- 正式报告：`E:\StardewAI-TestLab\artifacts\formal-training-admission-current-source-v1-20261014\formal-training-admission-reconciliation.json`。旧 2026-09-28 语料在当前回放器下出现确定性日历漂移，已失败关闭，不得拿旧汇总覆盖本基线。

## 历史记录 · 2026-10-14 Full Shipment 运行时证据门已闭合

- 权威静态分母保持 `154` 个需求、`641` 条来源、`26` 个执行签名层。当时严格索引为 `26/26`，`runtime_sample_evidence_complete=true`、`shared_shipping_evidence_verified=true`、缺失层 `0`；机器可读索引位于 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v38-20261014\runtime-sample-evidence-index.json`。
- 最后一层树苔藓已通过 `foraging.harvest_tree_moss -> executor.clear_obstacle -> executor.pickup_debris` 完成原生证据。第一段只发出一次原生镰刀挥击，第二段复用共享 debris BFS/自然拾取；没有直接写树状态、世界碎片或背包，也没有新增第二套执行器。
- 历史 `native_machine_item_query_output` 层已在当前代码下重新生成 v3 proof。具有完整但不同权威来源的同物品机器产出是确定性路线不匹配，不再被误报为来源缺失；真正缺失或不完整的来源继续失败关闭。
- Full Shipment 证据完成不等于正式训练授权。`formal_product_training_authorized=false` 保持不变，完整三年 recurrence 仍只作为最终验收与玩法保留，不是训练准入的重复采证要求。
- 当时后续计划必须使用现有只读 `build-goal-method-formal-training-admission-reconciliation` 重建独立准入报告。该报告要求 19 项权威爷爷评分分母、可重放 acquisition corpus、非空 train/validation/test、与语料绑定的 checkpoint、重算 holdout、五类支持转移终端谱系，以及全部引用 option 的透明读取、原生运行时、五门和内部执行链均通过。报告最多进入独立 promotion review，不能自行把训练标记改为 true。

## 历史记录 · 2026-10-05 Full Shipment 采证计划准入门

- 当时权威静态清单已重建为 `154` 个需求、`641` 条来源、`26` 个执行签名层，静态可编译完整且 blocker 为 `0`；机器对账后的采证计划为 `26/26`，其中高风险首批为 `7` 层。
- 该结果只证明“每层都有可运行入口”，不增加生产证据。Sap 与放射性矿石的已有产物仍须由 milestone runner 导入并按当前清单复核；随后每个新层都必须产生 fresh 原生回执和独立重建 rollout proof。
- 批处理失败后立即停在对应场景，不自动重试、不跳过失败层；checkpoint 允许修复后继续。只有最终 evidence index 为 `26/26` 且其余训练门也通过时，才能重新评估训练授权。

## 历史记录 · 2026-10-05 当时准入结论与证据边界

- `formal_product_training_authorized=false` 保持不变。Sap 与放射性矿石已有当时版本 fresh 原生证明，但防风草因停止低收益的全量重放，没有形成可接纳的新版本证明，因此本轮不得宣称严格索引已升至 `3/26`。
- Sap 证明：`F:\StardewAI-TestLab\artifacts\runtime-full-shipment-sap-refresh\runtime-full-shipment-sap-refresh-20261005-183733`。原生睡眠 `applied/verified`，新日、存档提交、菜单关闭和世界稳定均被验证，recurrence prefix checkpoint 通过。
- 放射性矿石证明：`F:\StardewAI-TestLab\artifacts\runtime-full-shipment-radioactive-node\runtime-full-shipment-radioactive-node-20261005-134024`，来源固定为 `(O)95 -> GameLocation.breakStone -> (O)909`，route occurrence 为 `full_shipment:full_shipment:item:909:0:0`。
- 生产索引只在证据集合稳定后统一重建。日常开发采用定向原生回放加 game-free 全套，不再为与改动无关的层重复生成全部日历轴。任何被中断或日历指纹漂移的旧证明都只能保留为诊断材料，不能进入正式训练准入。
- 部署准入新增硬门：`-NoBuild` 只能使用不早于源码的构建产物，部署后源/目标 SHA-256 必须一致。本轮守卫 `3/3`、Core game-free `212/212`。

## 历史记录 · 2026-10-05 Full Shipment 树苔藓样本准入状态

苔藓 `(O)Moss` 已按 `native_tree_moss_harvest` 接入现有 `foraging.harvest_tree_moss -> executor.clear_obstacle` 原生镰刀链，严格 resume tuple 为 `full_shipment:item:Moss|(O)Moss|native_tree_moss_harvest`。`LZT` 样本守卫 `17/17`、Core game-free `138/138`、Bootstrap Release `0 warning / 0 error`。尚无 fresh Full Shipment acquisition receipt，因此该层仍为 pending；已装配待运行 14 层、未装配 10 层、严格索引 `2/26`，训练授权不变。

## 历史记录 · 2026-10-05 Full Shipment acquisition 路线类型前置校验

所有已配置运行时样本均在执行前绑定并核对精确 `requirement_id|qualified_item_id|route_kind`。只匹配 requirement、不匹配来源层的 Teacher 选择现在失败关闭；resume 端仍再次按同一三元组验证。`LZT` 样本守卫 `16/16`、Core game-free `137/137`、Bootstrap Release `0 warning / 0 error`。本项没有新增 fresh receipt 或 rollout proof，严格索引保持 `2/26`，`formal_product_training_authorized=false`。

## 历史记录 · 2026-10-05 Full Shipment 太阳能板产物样本准入状态

电池组 `(O)787` 已按 `native_solar_panel_output` 接入现有 `farm.collect_machine_outputs -> executor.collect_machine_output` 链，严格 resume tuple 为 `full_shipment:item:787|(O)787|native_solar_panel_output`。`LZT` 样本守卫 `15/15`、Core game-free `136/136`、Bootstrap Release `0 warning / 0 error`。没有 fresh Full Shipment acquisition receipt，因此该层仍为 pending，严格索引保持 `2/26`，训练授权不变。

## 历史记录 · 2026-10-05 Full Shipment 机器直接产物样本准入状态

蘑菇树桩 `(BC)128` 的 `(O)257` 已按 `machine_output` 接入现有 `farm.collect_machine_outputs -> executor.collect_machine_output` 链，严格 resume tuple 为 `full_shipment:item:257|(O)257|machine_output`。`LZT` 样本守卫 `14/14`、Core game-free `135/135`、Bootstrap Release `0 warning / 0 error`。尚无 fresh Full Shipment acquisition receipt，因此该层仍为 pending，严格索引保持 `2/26`，训练授权不变。

## 历史记录 · 2026-10-05 Full Shipment 鱼塘产物样本准入状态

鱼籽 `(O)812` 已按 `native_fish_pond_output` 接入现有 `fishing.service_fish_ponds -> executor.collect_fish_pond_output` 链，严格 resume tuple 为 `full_shipment:item:812|(O)812|native_fish_pond_output`。`LZT` 样本守卫 `13/13`、Core game-free `134/134`、Bootstrap Release `0 warning / 0 error`。没有 fresh Full Shipment acquisition receipt，因此该层仍为 pending，严格索引保持 `2/26`，训练授权不变。

## 历史记录 · 2026-10-05 Full Shipment 高级动物产物样本准入状态

大瓶牛奶 `(O)186` 已按 `native_farm_animal_deluxe_produce` 接入现有动物产物链，严格 resume tuple 为 `full_shipment:item:186|(O)186|native_farm_animal_deluxe_produce`。`LZT` 样本守卫 `12/12`、Core game-free `133/133`、Bootstrap Release `0 warning / 0 error`。尚无本物品 fresh Full Shipment acquisition receipt，因此该层仍为 pending，严格索引保持 `2/26`，训练授权不变。

## 历史记录 · 2026-10-05 Full Shipment 基础动物产物样本准入状态

牛奶 `(O)184` 已按 `native_farm_animal_produce` 接入现有 `farm.collect_animal_products -> executor.collect_animal_product` 链，严格 resume tuple 为 `full_shipment:item:184|(O)184|native_farm_animal_produce`。`LZT` 样本守卫 `11/11`、Core game-free `132/132`、Bootstrap Release `0 warning / 0 error`。EVD-222 不能替代本层 fresh Full Shipment acquisition receipt，因此该层仍为 pending，严格索引保持 `2/26`，训练授权不变。

## 历史记录 · 2026-10-05 Full Shipment 果树产物样本准入状态

樱桃 `(O)638` 已按 `native_fruit_tree_produce` 接入现有 `foraging.harvest_fruit_tree` 原生链，严格 resume tuple 为 `full_shipment:item:638|(O)638|native_fruit_tree_produce`。`LZT` 样本守卫 `10/10`、Core game-free `131/131`、Bootstrap Release `0 warning / 0 error`。历史果树 action smoke 不能替代 fresh Full Shipment acquisition receipt，因此该层仍为 pending，严格索引保持 `2/26`，训练授权不变。

## 历史记录 · 2026-10-05 Full Shipment 地点觅食物样本准入状态

野山葵 `(O)16` 已按 `native_location_forage_spawn` 接入 EVD-211 的现有 spawned-object 采集链，精确 resume tuple 已锁定。异机样本守卫 `9/9`、Core game-free `130/130` 和 Bootstrap Release 已通过。尚无 fresh Full Shipment acquisition receipt，因此该层仍为 pending，严格索引与训练授权不变。

## 历史记录 · 2026-10-05 Full Shipment 春葱样本准入状态

春葱 `(O)399` 已通过共享 crop fixture 接入既有 `foraging.harvest_spring_onions -> executor.harvest_crop` 链，严格 resume tuple 已锁定。异机样本守卫 `8/8`、Core game-free `129/129` 和 Bootstrap Release 已通过。尚无新的 Full Shipment acquisition receipt，因此该层仍为 pending，严格索引与训练授权不变。

## 历史记录 · 2026-10-05 Full Shipment 岛屿棕榈样本准入状态

椰子 `(O)88` 的 `native_wild_tree_seed` 样本已接入现有树产品链，并以精确 `IslandSouth 20,20` 根区别于 Farm 样本。异机样本守卫 `7/7`、Core game-free `128/128` 和 Bootstrap Release 已通过。没有 fresh Full Shipment acquisition receipt，因此该层仍为 pending，严格索引和训练授权均不变。

## 历史记录 · 2026-10-05 Full Shipment 野树落种样本准入状态

榛子 `(O)408` 已按 `native_wild_tree_seed_drop` 接入共享 forage 场景与既有 `foraging.harvest_tree_product` 链，严格 resume tuple 已锁定。异机样本守卫 `6/6`、Core game-free `127/127` 和 Bootstrap Release 均通过。历史原生树产品 action smoke 只证明执行链可复用；新的 Full Shipment receipt 与 rollout proof 尚未产生，因此该层仍为 pending，严格索引继续为 `2/26`。

## 历史记录 · 2026-10-05 Full Shipment 茶树样本准入状态

茶叶 `(O)815` 已按 `native_tea_bush_harvest` 接入共享 forage fixture 配置与既有 `foraging.harvest_bushes` 链，精确 resume tuple 为 `full_shipment:item:815|(O)815|native_tea_bush_harvest`。异机专项守卫 `5/5`、Core game-free `126/126` 和 Bootstrap Release 已通过。既有茶树 action smoke 不是本次 acquisition rollout proof；茶树仍为 pending，严格索引保持 `2/26`，训练授权不变。

## 历史记录 · 2026-10-05 Full Shipment 姜样本准入状态

`native_ginger_harvest` 已按 `full_shipment:item:829 / (O)829 / foraging.harvest_ginger` 接入现有 Full Shipment 样本与断点 proof 流程，复用现有原生姜 fixture 和执行链。异机 Bootstrap Release、路由分派、静态可编译性以及 Core game-free `125/125` 已通过。该状态只表示代码可进入原生采证，不表示已取得新回执；浆果与姜均仍为 pending，严格索引保持 `2/26`，`runtime_sample_evidence_complete=false`、`formal_product_training_authorized=false`。

## 历史记录 · 2026-10-04 Full Shipment 灌木样本准入状态

`native_bush_shake` 的权威 requirement、lowering、既有候选/原语复用、样本运行入口和通用断点 proof 已闭合到代码与离线回归。Core game-free 为 `124/124`，旧防风草 proof 重新构建后仍严格通过。当时缺少的唯一材料是交互桌面会话中的新原生浆果采集执行证据；因此 `runtime_sample_evidence_complete=false`、`formal_product_training_authorized=false`，严格索引仍为 `2/26`。只有新回执通过独立 rollout 重建并被生产 manifest 接纳后，才允许提升为 `3/26`。

## 历史记录 · 2026-10-04 Full Shipment 作物抽样证据

Full Shipment 静态权威覆盖保持 `154/154`、641 条 route、26 个运行时抽样层。普通作物候选的来源身份已按 1.6.15 原生 `Crop.netSeedIndex` 修复，防风草 `(O)24` 的 `harvests_as` 原生执行、fresh 终态、reservation settlement、rollout checkpoint 和最终严格 proof 均通过。加上既有 Sap 样本，严格索引现为 `2/26`，共享 shipping recurrence 已验证，剩余 24 层。

本结果只解除普通作物来源缺失这一硬阻塞，不解除训练门。`runtime_sample_evidence_complete=false`、`formal_product_training_authorized=false` 保持不变。后续必须按剩余层复用或采集精确原生样本，并继续绑定唯一权威 route、源身份、队列、执行回执和 fresh 快照；不得用只读 smoke 名称、人工声明或同层重复物品计数。

## 历史记录 · 2026-10-04 当时准入口径：Full Shipment 分层原生证据

- Full Shipment 的游戏目标与权威静态分母仍是完整 `154/154`；这一点没有降低。当时生产静态清单继续要求全部需求、来源身份、endpoint/supporting option、运行时绑定与 supporting-transition 谱系精确闭合。
- 由于无法为开发验证稳定生成覆盖 154 个物品、跨季节/年份/特殊条件的完整原生存档集合，正式训练准入不再要求机械执行 154 次 fresh-save recurrence。运行时证据改为按 `route_kind + source_evidence_mode + endpoint options + supporting options + inline support transitions` 分层抽样；当时生产静态清单生成 26 个运行时层，每层要求一条精确原生 acquisition proof。
- 公共的出货、回家、原生睡眠与次日结算尾链由一条独立重算的 Full Shipment recurrence prefix 证明。Sap 已取得第一条真实 fresh-save `1/154`：原生砍树、debris 拾取、出货、恢复与睡眠结算均通过，prefix 为 `verified_iteration_count=1 / remaining_item_count=153 / ready_for_next_iteration=true`。该 `1/154` 是公共递归链诊断证据，不代表完整 Full Shipment 已完成。
- `full_shipment_runtime_sample_evidence_index.v1` 会重新构建 supplied acquisition rollout proof，绑定唯一 route occurrence、权威 requirement/lowering 哈希和静态分层签名，并单独重算共享 shipping prefix。只读 smoke 名称、人工声明、重复 layer 或陈旧 receipt 不计覆盖。
- 完整 `154/154` fresh-save recurrence 构建器继续保留，用于 AI 正常游戏能力和可选最终验收；不再作为 Full Shipment Teacher coverage 或 formal product training 的前置条件。
- 当时 Goal Method 生产覆盖仍为 `4/19`，`formal_product_training_authorized=false`。分层证据索引完成前不得把 Full Shipment 提升为 coverage-ready；即使 Full Shipment 单项完成，也仍不能绕过其余 criterion、数据分区、checkpoint、Product Executor、版本锁与正式 promotion review。
- 历史章节中出现的“生产 recurrence 仍为 `0/154`”或“必须完整 154-requirement recurrence 才能训练准入”均只描述当时状态；不得再作为当时口径引用。

## 历史记录 · 2026-09-28 Full Shipment 原生恢复链准入修复

Full Shipment recurrence 的结算证据现在允许在投入出货箱后，先按既有 `recovery.stabilize_day` 完成最多 8 段“一个连接点一次重规划”的原生恢复，再接原有专用睡眠结算回执。每段必须通过共享队列/原生执行回执校验，并保持同一存档与玩家、同一天、相同 Full Shipment 进度和精确的待结算箱内物品；中间睡眠、断链、乱序、重复证据或状态漂移全部拒绝。prefix 回执会记录每段四类证据文件的 SHA-256。该变更修复了真实角色从农场出货箱走回床会被旧证明合同错误拒绝的问题，不新增执行器，也不解除正式训练门；生产 recurrence 仍为 `0/154`。

## 历史记录 · 2026-09-27 机器触发器条件修复边界

机器输出路线现会保留原生触发器条件的 OR 身份。单触发器的目标地点季节条件必须由同一目标日期快照中的逐地点 `effective_season` 证明，`RANDOM` 概率由随机重试轴与输出项概率相乘；缺少证据、未知 GSQ 谓词或尚未支持的多触发器跨轴组合均失败关闭。物品输入标签与可食用性条件仍由资源轴独立证明。该修复消除了错误正例，但不新增训练授权，正式训练门槛保持关闭。

## 历史记录 · 2026-09-27 机器输入采购递归准入边界

机器路线只在资源轴证明一个确定性输入缺口、当前原生商店预览存在可购买且库存足够的精确金币报价时进入采购递归。reservation 同时锁定当前真实材料和全部采购资金，但不把未来物品写成材料 claim；processing 明确停在 `upstream_machine_input_purchase`，因此采购完成前不能提前形成机器加工日程。

执行复用既有 `economy.buy_supplies` 滚动链。跨图和开店交互每次只证明无资源变化并重绑活动 claims；单件购买以受限的“可选菜单等待、购买、关菜单”宏执行，fresh 回执必须同时证明精确货币减少与精确玩家背包物品增加。共享 ledger 每次只消费一个单价，可保留货币余量，也能在最后一次购买时完成该 claim；已完成 claim 禁止继续重绑。每步之后必须完整重规划，材料满足后才回到既有暂存/投料协议。

该闭环修复了“材料不足但明明可买”无法递归的问题，但仍不解除正式训练禁入。缺少已摆放机器时对既有容量生命周期的接线、Teacher 19 项完成度、独立 Teacher 监督、正式 Product Executor 轨迹与第三年 21 分长跑验收仍是独立门槛。

## 历史记录 · 2026-09-27 机器箱存原料暂存边界

机器路线现在可以把当前地图普通箱中的已预留原料识别为“可通过支持动作取得”，但不能把箱中数量当作机器可直接消费的背包数量。控制面复用既有 `inventory.transfer_item` 两步原生宏，要求完整 claim 只落入一个玩家背包槽；fresh 回执确认精确箱减/包增后，共享 ledger 只搬迁 claim 地址并保持其活动状态，再以新快照和新 revision 完整重规划进入既有投料链。

该闭环拒绝跨槽拆分、非当时地图或特殊/锁定箱、部分 claim 搬迁、消费/搬迁混用和未观测到目标物品的结算。静态 dispatch、hermetic、Core game-free `91/91`、Backend `213/213` 已通过，但这仍是非终端执行证据，`formal_training_authorized=false`。材料缺失时的权威采购递归、缺少机器时对既有容量生命周期的接线、Teacher 19 项完成度以及独立 Teacher 监督门仍必须继续闭合，不能据此恢复正式全量训练。

## 历史记录 · 2026-09-08 EVD-335 春葱高层准入

`foraging.harvest_spring_onions` 已完成五门闭环。锁定的 1.6.15 程序集确认 `Crop.forageCrop_springOnionID == "1"`，原生 `Crop.harvest` 在该分支动态创建 `(O)399` 并增加 3 点 Foraging XP；它不依赖普通作物的 `indexOfHarvest`。透明桥据此只对精确原版基类春葱发布产物身份及精确投影状态，未知觅食 ID、自定义作物、姜和普通作物均不借用本证据。

隐藏静音隔离运行 `runtime-spring-onion-daily-plan-20260908-233958` 从高层候选经既有 `harvest_crop_tile -> executor.harvest_crop` 单链完成原生收获，验证 `(O)399` 库存增加、Foraging XP 精确 `+3`、作物移除和 fresh changed state hash。运行使用 `--skip-training`，只构成 EVD-335 五门证据，不是正式策略样本。

权威对账现为 `230 registered / 232 semantic / 229 compiler-bound / 153 five-gate / 64 training allowlist / 145 Product Executor / 2 catalogued blocked`。1599 条获取来源仍完整，路由为 `30/33 admitted / 3 blocked`；剩余树苔和共享的野树砍伐获取缺口仍需闭合。Teacher 总体仍为 `2/19 executable`，正式全量训练继续禁用。下一切片固定为 `native_tree_moss_harvest`。

## 历史记录 · 2026-09-08 EVD-334 古物点高层准入

`foraging.excavate_artifact_spots` 已闭合读、候选、编译、原生运行和输出五门。隐藏静音隔离运行选择唯一原版 `(O)590`，经 DailyPlan 和既有 `clear_obstacle_tile -> executor.clear_obstacle` 单链执行原生 Hoe；输出 unit-state 集合、Foraging XP、`ArtifactSpotsDug`、地形、邮件状态、对象移除与 fresh state hash 全部吻合。`(O)SeedSpot`、其他清障对象、自定义地点重载和未透明的全局 RNG 分支继续失败关闭。

权威对账现为 `229 registered / 228 compiler-bound / 152 five-gate / 63 training allowlist / 145 Product Executor / 2 catalogued blocked`。四个收集分母的 1599 条获取来源仍完整，路由种类从 `27/33` 提升到 `29/33`；剩余缺口固定为春葱、树苔、砍野树产物及砍野树种子掉落。Teacher criterion 总体仍为 `2/19 executable`，所以该准入不等于可以恢复正式全量训练。下一切片为 `native_spring_onion_harvest` 的高层选项与既有 `executor.harvest_crop` 单链闭环。

## 历史记录 · 2026-09-08 当时有效覆盖：Teacher / Student 监督来源门

本文下方 r24-r35 内容保留真实历史运行事实，但不得再据此宣称正式策略监督有效。当前规范见 `TEACHER_STUDENT_CONVERGENCE_CONTRACT_CN.md`：`teacher_preference`、`native_outcome`、`student_observation` 必须类型化分离；`candidate.Selected` 只能描述 Student 行为，不能自动成为正例。

源码审计确认当前 `StructuredPolicyTrainer.BuildPairs` 仍以 selected candidate 构造正向 pair。因此启动、Product 回执、跨日事务、数据 manifest 和 checkpoint 即使全部成功，也只证明管线与执行闭环，不解除策略训练禁入。恢复正式全量训练前必须完成独立 Teacher 标签、learner-visited state 重标、候选集/decision-state 身份和三类来源的 dataset/trainer 回归。

当时 Teacher 反向图为 2/19 executable、17/19 expansion pending。Master Angler 当时日期 route/time 叶已闭合；未来日期、主动蟹笼容量、概率重试与原生跨日回执仍是 blocker。系统可以按工程门槛收敛，但当时尚未收敛。

## 历史记录 · 2026-09-05 r35 round01 软障碍修复与连续批次结果

r34 round05 从 Summer 9 精确起点运行时，摸宠物成功，但后续跨出 FarmHouse 的通用移动在宠物占据下一格时只反复尝试 soft-obstacle BFS，未发送原生移动输入。6 次主尝试后仍只有 1/2 verified，事务保持 `staged_not_committed`，没有进入存档边界，也没有提升 canonical。失败轮次和 6 份执行器诊断已按 130/130 文件完成双端 SHA-256 归档。

`20fff604434584f966824e043360b1802647027d` 将动态角色阻挡统一为“先绕行、无路则原生推让、180 tick 后失败关闭”，没有新增第二套移动器，也没有同步修改角色坐标。RuntimeTestHarness Release 构建和 game-free governance 20/20 通过；发布 `formal-r35-20fff60-20260905` 的 367 项清单通过校验。r35 round01 的实机日志证明角色从 FarmHouse `(10,9)` 启动移动并原生切换到 Farm，旧超时未复现。

`train.server.20260905.r35.plan01` 以并发 1、5 次迭代完成 2 个 verified 主决策和 3 个原生保存边界尝试，Summer 9 推进至 Summer 10。每日摸宠物、完整处理 `passedOut3_Billed 8` 与 `spring_23` 两封邮件形成 3 条 applied 高层策略轨迹；控制面 return-home/sleep 没有进入策略数据。事务为 `committed_after_native_save_boundary`，pending 0、resolved 13，容器 exit 0 / OOM false。

canonical 为 accepted 221 / rejected 0、163 / 5 / 53、5094 pairs，执行特征 230 行、horizon 观测 8 行；checkpoint / manifest SHA-256 为 `dcb6ff2bc8dd28a223fcd0fe5a27c4a58215ff23084b6013a30f9299daac774b` / `dc7628a1add5a9db7247980bc7e19e4f671c93c3b0f51bcf12e4917e3085904b`。完整成功制品位于 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r35-round01-20260905-120836`，远端/本机 165 / 165 且摘要不一致为 0。下一批只能从 Summer 10、存档摘要 `19de382994da925d5a47707eb143a89f0710a38e89dec7374b4d217faaaf62d7` 和上述 canonical 起步。

## 历史记录 · 2026-09-05 r34 round04 连续批次结果

round04 从 Summer 8 和 round03 canonical 精确哈希通过 prepare，以并发 1 完成 2 个主决策和 3 个原生保存边界迭代。完整处理 `fertilizers` 邮件、每日摸宠物和完整处理 `spring_19_1` 邮件形成 3 条 applied 策略轨迹；未闭合队列尾项只记 skip，控制面 return-home/sleep 没有进入策略数据。执行特征从 224 增至 227，horizon 观测保持 8。

原生存档推进到 Summer 9，事务为 `committed_after_native_save_boundary`，未解决 pending 为 0，正式容器 exit 0 / OOM false。canonical 为 accepted 218 / rejected 0、160 / 5 / 53、5011 pairs；checkpoint / manifest SHA-256 为 `0b31d8084c6fcc728defd4ba916d8542b2e1efb13cbf97695f1db94a910b1035` / `0af71721588d0da1bd78cb72cfca6e3cd74e7545f2edb2726390d3d9b08cf96b`。train/validation/test pair accuracy 为 0.992217 / 1.0 / 0.965602；两次主排序为 2.228/82.095 秒，后两个边界排序为 138.9/9.5 毫秒。

完整制品位于 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r34-round04-20260905-111352`，远端/本机 172 / 172，缺失、额外和摘要不一致为 0，存档树聚合摘要也独立复算一致。下一批只能从 Summer 9、存档摘要 `c542c7c9ed8192c2e26b19cc714c94e9589dad27ea586efe800e6208a6fa7756` 和上述 canonical 起步。

## 历史记录 · 2026-09-05 r34 round03 连续批次结果

round03 从 Summer 7 和 round02 canonical 精确哈希通过 prepare，以并发 1 完成 2 个主决策和 2 个原生保存边界迭代。出货与邮件 approach 的机械回执均为 applied/verified，但 selected queue candidate 均未完整闭合，因此正确地产生 0 条新策略轨迹；控制面 return-home/sleep 也没有进入训练策略数据。执行特征从 220 增至 224，horizon 观测从 7 增至 8。

原生存档推进到 Summer 8，事务为 `committed_after_native_save_boundary`，未解决 pending 为 0，正式容器 exit 0 / OOM false。canonical 仍为 accepted 215 / rejected 0、157 / 5 / 53、4920 pairs；checkpoint / manifest SHA-256 更新为 `64283a58cb8e48eed06baffb1c8116a241033c26b00e792ec01807db537c345c` / `47ac7aaff2798f3546eea1e0a5772a524dc3d9ee959e4f0506136d7e3fb18a78`，checkpoint 精确绑定新 horizon 清单。train/validation/test pair accuracy 为 0.992276 / 1.0 / 0.96683；两次主排序为 2.427/45.624 秒，两个边界排序为 203.5/12.4 毫秒。

完整制品位于 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r34-round03-20260905-085309`，远端/本机 119 / 119，缺失、额外和摘要不一致为 0，存档树聚合摘要也独立复算一致。下一批只能从 Summer 8、存档摘要 `4bb3420051c550a5f8c371bec8867e3101ca155ad0c6a69a60ed9ae36ad7096f` 和上述 canonical 起步。

## 历史记录 · 2026-09-05 r34 round02 连续批次结果

round02 从 Summer 6 和 round01 canonical 精确哈希直接通过 prepare，2 个主决策生成 4 条 applied Product 策略轨迹。fresh 后不可用的社交队列尾项被失败关闭并触发重新规划，只留下 skip 诊断；控制面保存边界没有写策略轨迹。原生存档推进到 Summer 7，事务为 `committed_after_native_save_boundary`，未解决 pending 为 0，正式容器 exit 0 / OOM false。

canonical 更新为 accepted 215 / rejected 0、157 / 5 / 53、4920 pairs；checkpoint / manifest SHA-256 为 `bc5369df5a47bfdf27d9a49b99cc4498b54a4cd4dc27bba1b02de907419c15a4` / `24b18a5bf0317e36f36398609b9e65c79a69f42bef73cee35b57191ae56ec653`。清单六摘要与实际文件一致；validation/test pair accuracy 为 1.0 / 0.96683。两次主排序为 2.412/43.828 秒，控制面后续排序为 154.6/22.2 毫秒。

完整制品位于 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r34-round02-20260905-043913`，远端/本机 170 / 170，缺失、额外和摘要不一致为 0。下一批以 Summer 7 为唯一合法起点，继续遵守并发 1、有界退出、失败不提升、原生保存和停机归档门。

## 历史记录 · 2026-09-05 r34 候选性能门与连续批次结果

`10b7722` 只复用由同一不可变快照派生出的 route connector 候选，不缓存跨快照事实，也不裁剪透明字段。默认自主候选由单一权威集合提供，显式目标、校准和玩家指令候选继续失败关闭；`recovery.stabilize_day` 只在控制面显式补入，不参与策略训练。game-free governance 18/18、Backend 172/172 和针对性缓存/候选边界测试通过；真实 119 运行继续承担游戏程序集相关验证。

冷启动 Farm full snapshot 默认排序从约 227.9 秒降至 28.266 秒，119 室外首次排序为 43.329 秒，后续同快照边界排序为 145.6/9.7 毫秒。该结果解除“每次排序重复构建全部跨图连接候选”的 I/O/CPU 瓶颈，但不改变策略调用边界，也不代表可无界运行。

`train.server.20260905.r34.plan01` 从 Summer 5 推进至 Summer 6，以 4 次迭代完成 2 个主决策、2 个原生存档边界和 4 条 applied Product 策略轨迹。事务为 `committed_after_native_save_boundary`，规范数据为 accepted 211 / rejected 0、153 / 5 / 53、4783 pairs；checkpoint / manifest SHA-256 为 `b33d54f66fdcbe304e5207043751a0a153dcd45d24064b299903b978d28bd010` / `103770377bdca5b4979bd196ae4756c93e080f8924bc1de136a9bd35cc07c738`。原生保存完成、存档哈希变化、六个数据摘要、0 unresolved Product pending 与正式容器正常退出均已复核。

完整制品已归档到 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r34-round01-20260905-041036`，远端/本机 189 / 189 且三类差异为 0。下一批继续沿用并发 1、静音后台、计划退出、失败不提升和逐轮归档；跨季、跨年、Grandpa 21 与 Companion 产品层仍是未完成准入。

## 历史记录 · 2026-09-02 运行证据强绑定准入

正式训练不得把“存在 evidence id”视为 Runtime/Output 已验证。`native_object_execution.v2` 的当时准入同时要求：证据 ID 已登记、运行路径修订完全匹配、32 个源文件的规范化 SHA-256 未漂移、artifact/source/build identity 完整，以及 RuntimeTestHarness、Contracts、TransparentBridge 三份运行 DLL 的 SHA-256 与登记值一致。任一项未知、缺失或变化均 fail closed，必须重新构建并取得新的原生运行证据后才能恢复准入。

首批强绑定范围是 #88 重跑的六个动作：`world.rotate_house_plant`、`world.play_singing_stone`、`farming.collect_slime_ball`、`animals.withdraw_feed_hopper_hay`、`animals.collect_auto_grabber_contents` 和 `movement.use_mini_obelisk`。其余历史域暂标记 `LegacyUnbound`，不能据此宣称全目录已完成强证据迁移；应按真实运行产物逐域补齐。CI 使用不依赖私有游戏 DLL 的 game-free governance profile 持续检查登记表、源码指纹、序列化追溯字段和原生对象机械约束，完整本地 Core/Backend/Release 回归仍作为合入门槛。

## 历史记录 · 2026-09-01 r29 队列级决策边界修正

正式训练的规范单位不是机械动作，也不是每次 fresh snapshot。策略模型一次排序并选择一条有序高层候选队列；该选择、原始排序、编译队列和候选顺序由 `SelectedQueueDecisionLease` 持有。运行时逐候选执行，并在每个候选边界从 fresh snapshot 重新物化候选、校验队列顺序以及累计时间/能量，再由 C# 编译器确定性展开。候选内部的寻路、移动、交互、等待和菜单 continuation 同样只进行确定性重编译。

只有队列全部完成，或当前候选不可用、阻塞、身份漂移、回执不确定以及时间/能量预算失效导致整条队列失效时，才能重新调用策略模型。每个成功完成并验证的高层候选写入一条 `policy_decision_trajectory.v2`；机械原语不单独生成策略轨迹。已完成候选的轨迹可以在后续候选失效时保留，但失败候选不得计为完成。

119 服务器 `formal-r29-20260901 / train.server.20260901.r29` 已证明该边界：1 个排序文件选择 4 个高层候选，随后执行 9 个机械动作；3 次候选边界刷新和 5 次候选内部 continuation 刷新均为本地确定性编译，`policy_model_invoked=false`；最终 4 条高层候选轨迹入库且 `selected_queue_decision_complete=true`。r24/r25 关于“一次 fresh replan 对应一条策略轨迹”或“一个外层 iteration 只能容纳一个高层目标”的历史解释由本节明确取代。

迁移证据位于 `I:\StardewAITrainingArchive\119.91.139.160\formal-training-r18-pre-queue-boundary-fix-20260901`，远端与本地 910/910 文件 SHA-256 一致，远端源训练根未删除。当时仍不是多日全量训练完成；服务器约剩 4.2 GB，扩容或迁移训练根仍是长训硬门槛。

## 历史记录 · 2026-09-01 119 服务器 r25 有界证据保留

r25 将正式运行的诊断快照窗口从 LiveTrainingLoop 默认 64 轮改为 manifest 冻结的 4 轮。请求、
`training_run_manifest.v2`、launcher CLI、Linux attached 脚本、Windows 启动器和 Compose 使用同一
`max_persisted_iterations`；范围限制为 1-64。滚动清理只处理当前 run 的 `live-snapshots` 中旧迭代
家族，保留最近四轮；正式策略轨迹、Product pending/final 回执、数据集、checkpoint、manifest 和日志不受影响。

发布 `formal-r25-20260901`、运行 `train.server.20260901.r25` 再次完成邮件目标五步，全部为
`applied/verified/fresh`，5 条候选轨迹全部入库且来源遗漏为 0。manifest 实际冻结
`max_attempts=1 / max_persisted_iterations=4 / min_free_space_mb=4800`。正式数据集现为 accepted 186、
rejected 0、train/validation/test 128/5/53，首次得到非空 validation 分区；checkpoint 更新为
`structured-policy-52c9f785cc6dcc46c02f94e7`，SHA-256 为
`b97bbdc1b64ba77b38097fc691581d4397c32807246f312b00dd883249e23b67`。训练结束后约 54.124 UPS，
12 次 full snapshot 平均 1406.835 ms。服务器只有一块 60GB 系统盘，当前约 4999 MiB 可用、使用率
92%；四轮滚动可限制后续单 run 工作集，但不能替代扩盘，也不得据此直接开启无界长训。

## 历史记录 · 2026-09-01 119 服务器 r24 正式闭环实证

正式 Product 训练控制闭环已经开始产生真实更新，但全量长训尚未启动。服务器使用隔离槽位
`StardewAIDebug_16564609768130219756`、发布 `formal-r24-20260901` 和运行
`train.server.20260901.r24`，以 `max_attempts=1` 完成一个有界高层目标。这里的一轮是一个模型级目标
episode，不是一个按键：模型选择“处理 landslideDone 邮件”，编译/执行层依次完成跨图、走到信箱、
原生交互、LetterViewer 输入稳定等待和原生关信五个机械动作；每次 fresh snapshot 后只对同一类型化
continuation 重规划，目标完成后以 `objective_continuation_completed_iteration_boundary` 结束本轮，
不得在同一 episode 中误选第二个全局目标。

r20-r24 的服务器证据把该边界逐层闭合：r20 暴露跨地图后沿用旧队列，r21 改为 fresh snapshot
重规划；r22 为暂时不可输入的 `LetterViewerMenu` 增加严格绑定菜单身份的 30 tick 有界等待；r23 在
目标 continuation 完成时结束外层 episode；r24 为打开信箱、菜单等待和关信阶段补齐 `candidate_id`
来源。r24 最终为 `5/5 applied + verified + fresh`，形成 5 条互异且成功的 Product 策略轨迹，
`effective_candidate_id_missing=0`。正式数据集由 176 增至 181 条接收、0 条拒收，分区为
train 128 / validation 0 / test 53，训练比较对 3415；新 checkpoint 为
`structured-policy-35eb3439e19036a75b9c628b`，SHA-256 为
`11f38de448370bb401278bd8cd32ca4faea1e42e67df3b5299e20394c53ef0f7`。

本轮后游戏保持运行，Backend 与 Product Executor 保持空闲，LiveTrainingLoop 正常退出。低频观察模式
为 `STARDEWAI_OBSERVER_RENDER_INTERVAL_MS=1000`，实测约 56.346 UPS；12 次 full snapshot 平均
1430.682 ms，没有形成持续卡顿。服务器当前仅约 5072 MiB 可用、磁盘使用率 92%，因此不得在现盘上
启动无界长训。下一准入动作是扩容或迁移正式训练根并复核哈希，然后按可恢复的小批游戏日运行；每批必须
同时验收 Product 原生回执、轨迹增量、dataset/checkpoint 哈希、磁盘门、游戏 UPS 和恢复探针。当前模型仍是
`return_weighted_pairwise_linear_ranker.v1`，RTX 5070 8GB 对这一阶段不是必需资源；神经策略模型或
QLoRA 替换属于积累足量、多分区长期轨迹后的后续阶段。

## 历史记录 · 2026-09-01 EVD-327 Product bootstrap 与隔离存档绑定

E 盘隔离副本 `StardewAIDebug_16564609768130219756` 已完成隐藏静音 Product bootstrap 校准。运行
`product-bootstrap-20260901-013441` 形成 3 条 `policy_decision_trajectory.v2 / product_executor.v1`
真实轨迹，其中 2 条包含至少两个准入候选；首两组分别保留 107 与 143 个可训练候选。数据集构建为
3/3 接收、0 拒绝、0 重复，并形成 248 个训练比较对。初始 checkpoint 为
`E:\StardewAITraining\checkpoints\structured-policy-bootstrap-20260901.json`，模型类型是
`return_weighted_pairwise_linear_ranker.v1`。当前三条数据全部位于 train 分区，validation/test 均为 0，
因此该 checkpoint 只作为正式循环冷启动，不构成泛化评测或全量训练完成证据。

正式 launcher 现把 `save_slot` 冻结进 `training_run_manifest.v2`，并在 prepare 阶段验证槽位名、
隔离根和主存档文件。游戏进程显式接收 `STARDEWAI_TEST_SAVES`、`STARDEWAI_TEST_SLOT` 与
`STARDEWAI_TEST_AUTO_LOAD=true`；缺失或越界槽位失败关闭，不能再以停在主菜单的进程冒充运行中的训练。
下一准入门是把 bootstrap 轨迹按哈希种入正式训练根，prepare/launch 同一份 manifest，并由新存档完整运行
至少一个真实游戏日，使 Product 轨迹、正式数据 manifest 和 checkpoint 哈希发生受控更新。

## 历史记录 · 2026-09-01 EVD-326 正式训练编排与模型更新准入

正式版本绑定已固定为 `policy_decision_trajectory.v2 / policy_features.v2 / action_queue.v1 / product_executor.v1`。轨迹写入端会按实际执行入口区分 Product 与 RuntimeTestHarness；清洗层允许保留 Harness 校准数据，但正式结构化训练器和 checkpoint store 只接受 Product 数据，且一个 manifest 只能包含一个不可混合的版本集。

`training_run_manifest.v2` 是启动与运行中的唯一控制记录。prepare 冻结数据 manifest/checkpoint hash、路径、run-id、隔离存档、执行端和工具二进制；launch 必须加载同一份 prepared manifest，并拉起 Product Executor、SMAPI 与 LiveTrainingLoop。`training_ready_probe.v2` 逐轮核对三进程、Product health、透明快照、run-id、收据 pending、dataset manifest、cleaned/三分区文件及 checkpoint hash。任何崩溃中间态都不得继续发动作。

正式循环已经改为真实结构化更新：Product applied/verified/fresh 轨迹落盘后，重建 policy dataset，训练结构化 ranker，并原子刷新 checkpoint 和 run manifest hash；旧 baseline 训练只保留非正式兼容路径。正式模式缺 Product、feedback、prepared manifest 或结构化 checkpoint 时启动即失败。

EVD-327 已完成 Product bootstrap 与初始 checkpoint，控制面不再受“无真实初始数据”阻塞。当前仍未满足
“全量训练已开始”：必须由正式 prepared manifest 拉起 Product Executor、SMAPI 和 LiveTrainingLoop，
完整运行一个真实游戏日，并证明 checkpoint 随新增 Product 数据更新。bootstrap 校准运行本身不得计入正式训练时长。

## 历史记录 · 2026-08-31 Product Executor 准入（EVD-325）

独立 `StardewAI.ProductExecutor` 已把 145 个现有原生 Harness dispatch 状态机装配到产品入口，但不复制动作实现。产品授权锁定 loopback、非 debug 产品能力、run-id、执行模式/actor、精确隔离存档根、nonce 和时间戳；正式 `LiveTrainingLoop` 未选择产品入口或关闭 executor feedback 时直接失败。62 项策略 allowlist 仍独立决定哪些高层候选可进入训练，Product Executor 数量不能反向扩大训练准入。

产品层在原生动作前原子写 pending，随后采集并记录实际执行前/后快照、校验原生回执身份与 verified 状态并写 final。全量世界状态在运行中会自然漂移，因此请求决策哈希与实际分发哈希同时保留，动作安全由游戏线程中的动作级 fresh 前置条件负责；漂移会强制后续重规划。相同 final 收据可长期幂等返回；nonce 冲突拒绝；孤立 pending 永不重发并转为结果不确定的阻断回执。隐藏静音 E 盘 `3/3` 产品冒烟位于 `artifacts/product-executor-smoke/product-executor-20260831-235239/summary.json`，服务测试 `10/10`，全量 Core `2191/2191`、Backend `162/162`、Release `0 warnings / 0 errors`。

当前训练阻塞已从“无产品执行器”转为“尚未重建并冻结正式轨迹 manifest/checkpoint，服务器启动与恢复探针尚未完成”。当时后续计划必须使用独立新存档和 62 项 allowlist 生成真实 `policy_decision_trajectory.v1`，验证 Product Executor 回执、数据集/checkpoint 哈希与断点恢复后，才启动服务器全量训练。

## 历史记录 · 2026-08-30 联机钱包执行准入（EVD-310）

`multiplayer.manage_wallet` 已通过 read / candidate / compile / native runtime / output receipt 五门，但治理为 `PlayerCommandOnly`，不进入策略训练 allowlist。玩家命令层给出五类操作之一；转账还必须给出精确收款人、金额和第二级确认。模式、房主权限、参与者、余额、原生菜单响应键、LedgerBook 站位、路线、即时回执和次日结算均由 fresh snapshot 与机械执行层绑定，模型不能自主决定分钱、合并或转账。

隐藏静音 E 盘 `7/7` 原生矩阵覆盖五项即时命令及共享转独立、独立转共享的次日结算；生产执行仅使用原生 `ManorHouse.checkAction`、对话和数字输入，不直接写钱包状态。最新 schema 为 `157/140/17/0`；对账为 `200 registered / 216 semantic / 199 compiler-bound / 123 five-gate / 54 allowlist / 16 catalogued blocked / 0 Product Executor`。正式全量训练仍受剩余 16 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；Junimo Kart 完美代打维持后置，下一实际目录切片为 `multiplayer.send_chat`。

## 历史记录 · 2026-08-30 卡利科雕像训练准入（EVD-309）

`mining.activate_calico_statue` 已通过 read / candidate / compile / native runtime / output receipt 五门并进入策略训练 allowlist。小模型只决定是否接受 fresh snapshot 投影的精确下一效果；沙漠节/骷髅洞/房主权限、日存档随机种子、效果栈、站位、移动和原生交互均由透明桥与机械执行层绑定。每次激活后必须以 live feedback 重规划，模型不得自行指定不同效果。

隐藏静音 E 盘矩阵 `18/18` 覆盖全部效果 ID、四档 Calico Egg 奖励、速度、完全恢复、正负效果栈、评分和单次地块转换；生产执行不写评分、效果、奖励、生命、耐力、Buff、地块或 RNG。最新 schema 为 `156/139/17/0`；对账为 `198 registered / 215 semantic / 197 compiler-bound / 121 five-gate / 54 allowlist / 17 catalogued blocked / 0 Product Executor`。正式全量训练仍受剩余 17 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一实际目录切片为 `multiplayer.manage_wallet`。

## 历史记录 · 2026-08-30 赌场老虎机训练准入（EVD-308）

`minigame.play_slots` 已通过 read / candidate / compile / native runtime / output receipt 五门并进入策略训练 allowlist。小模型只决定是否在缺少 `(BC)126` 且 Deluxe Scarecrow 依赖开放时安排一次齐币下注；赌场权限、机器、路线、10/100 下注、Luck 概率分布、转轴、倍率、原生币差和退出均由 fresh snapshot 与机械执行层绑定。共享 `Game1.random` 的未来结果不作确定性伪预测，每次原生结算后以 live feedback 重规划。

隐藏静音 E 盘矩阵 `2/2` 验证 10 币无奖和 100 币单七 `x2`，图案、倍率、`ClubCoins` 差、`timesPlayedSlots +1` 与 Done 清理完全一致；生产执行不写 RNG、转轴、倍率、齐币或统计。最新 schema 为 `155/139/16/0`；对账为 `196 registered / 214 semantic / 195 compiler-bound / 119 five-gate / 53 allowlist / 18 catalogued blocked / 0 Product Executor`。正式全量训练仍受剩余 18 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一实际目录切片为 `mining.activate_calico_statue`。

## 历史记录 · 2026-08-30 草原大王等价训练准入（EVD-307）

`minigame.play_prairie_king` 已通过 read / candidate / compile / equivalent runtime / native output receipt 五门并进入策略训练 allowlist。训练的标签是高层“是否、何时安排无伤通关”，不是逐帧射击动作；`executor.play_prairie_king` 仍为机械执行层。训练、联机陪玩和专用房主中的 AI actor 均使用相同的不可见 108000-tick 定时等价会话，禁止将其标成原生完美操作。

隐藏静音 E 盘冒烟验证原生 `Arcade_Prairie -> AbigailGame -> usePowerup(-3)` 结算，通关与无伤统计均精确 `+1`，`Beat_PK` 邮件由游戏自身产生。最新 schema 为 `154/138/16/0`；对账为 `194 registered / 213 semantic / 193 compiler-bound / 117 five-gate / 52 allowlist / 19 catalogued blocked / 0 Product Executor`。正式全量训练仍受剩余 19 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡。Junimo Kart 原生完美代打后置为训练完成后的 `PlayerCommandOnly`；下一实际目录切片为 `minigame.play_slots`。

## 历史记录 · 2026-08-30 飞镖训练准入（EVD-306）

`minigame.play_darts` 已通过 read / candidate / compile / native runtime / output receipt 五门并进入策略训练 allowlist。小模型只决定是否取得下一枚尚未发放的海盗湾飞镖核桃；海盗夜、目标地图天气、路线、20/15/10 支限额、301 分六投方案、鼠标瞄准/充能/释放、结果对话和限量奖励都由 fresh snapshot 与机械执行层绑定。非海盗夜、3 枚奖励已完成、端点/路线/菜单忙碌或投影漂移会在上游或 fresh 编译阶段关闭。

隐藏静音 E 盘 `3/3` 原生矩阵覆盖全部三个限量核桃阶段，每轮均以 6 投和 `60,60,60,60,51,10` 完成。生产执行不直接写分数、投掷数、计时器、RNG、奖励或进度。最新 schema 为 `153/137/16/0`；对账为 `192 registered / 212 semantic / 191 compiler-bound / 115 five-gate / 51 allowlist / 20 catalogued blocked / 0 Product Executor`。正式全量训练仍受剩余 20 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一目录切片为 `minigame.play_junimo_kart`。

## 历史记录 · 2026-08-30 抓娃娃机执行准入（EVD-305）

`minigame.play_crane_game` 已通过 read / candidate / compile / native runtime / output receipt 五门，但治理为 `PlayerCommandOnly`，只形成可验证的玩家命令执行能力，不进入策略训练 allowlist。玩家只授权一次 500g 会话；机器占用、路线、三次机会、实时奖品选择、横向/纵向释放时机、原生随机物理和奖励转移均由 fresh snapshot 与机械执行层处理。费用不足、少于 3 个空槽、机器占用、忙碌、路线或投影漂移会在上游或 fresh 编译阶段关闭。

隐藏静音 E 盘 `1/1` 原生冒烟完成 3 次机会并通过原生奖励菜单转移 2 件奖品，Money 精确 `-500`；生产执行仅使用 `MovieTheater.checkAction`、原生 Yes 对话、`UpdateTicking` D/S 输入和 `ItemGrabMenu`，不写结果状态。最新 schema 为 `152/136/16/0`；对账为 `190 registered / 211 semantic / 189 compiler-bound / 113 five-gate / 50 allowlist / 21 catalogued blocked / 0 Product Executor`。正式全量训练仍受剩余 21 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一目录切片为 `minigame.play_darts`。

## 历史记录 · 2026-08-30 Calico Jack 训练准入（EVD-304）

`minigame.play_calico_jack` 已通过 read / candidate / compile / native runtime / output receipt 五门并进入策略训练 allowlist。小模型只决定是否执行一局由稀有稻草人依赖驱动的牌局；牌桌、路线、下注、初始牌、隐藏牌、未来抽牌、要牌/停牌序列、齐币结算和单局退出都由 fresh snapshot 与共享确定性模型机械绑定。缺少赌场权限、无 Rarecrow 需求、菜单/路线/牌局漂移、余额不足或仅剩 100 且下一局投影损失会在上游排除。

隐藏静音 E 盘 `3/3` 原生矩阵覆盖高注获胜、低注失败和首次要牌获胜；生产执行只使用赌场原生 `checkAction`、`DialogueBox` 和 `CalicoJack.receiveLeftClick`，不写 RNG、牌、齐币或结果。最新 schema 为 `151/135/16/0`；对账为 `188 registered / 210 semantic / 187 compiler-bound / 111 five-gate / 50 allowlist / 22 catalogued blocked / 0 Product Executor`。正式全量训练仍受剩余 22 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一目录切片为 `minigame.play_crane_game`。

## 历史记录 · 2026-08-30 Field Office 调查训练准入（EVD-303）

`island.field_office_survey` 已通过 read / candidate / compile / native runtime / output receipt 五门并进入策略训练 allowlist。小模型只决定是否完成当前唯一未完成调查；问题身份、固定答案、端点、站位、路线、当日失败锁、核桃上限、瞬时 debris 生成、共享拾取和 finale 结算均由 fresh snapshot 与编译执行层机械绑定。已有核桃 debris、答错锁日、完成态、锁/菜单/教授/路线漂移会在上游排除。

隐藏静音 E 盘矩阵 `9/9` 覆盖 22/18、同日连续两题、错误答案锁日、原生 DayUpdate 重置、130 核桃无掉落和 finale。生产执行只经 `checkAction(FieldOfficeSurvey)` 与两个原生 `answerDialogue` 响应，不直接写游戏结果。最新 schema 为 `148/132/16/0`；对账为 `186 registered / 209 semantic / 185 compiler-bound / 109 five-gate / 49 allowlist / 23 catalogued blocked / 0 Product Executor`。正式全量训练仍受剩余 23 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一目录切片为 `minigame.play_calico_jack`。

## 历史记录 · 2026-08-30 Field Office 化石捐赠闭环（EVD-302）

`island.field_office_donate` 已通过 read / candidate / compile / native runtime / output receipt 五门，但当前要求显式确认且未进入策略训练 allowlist。模型或玩家只选择“捐赠当前已拥有且原生可接受的一件化石”；具体背包槽、原生重复物品槽位顺序、Desk 端点、站位、路线、菜单点击、集合奖励、核桃标记和 finale readiness 均由 fresh snapshot 与编译执行层机械绑定。远程候选每次只走一个已解析连接器，并锁定同一背包物品和目标槽位直到原生捐赠成功。

隐藏静音 E 盘最终矩阵 `15/15` 覆盖 11 个显示槽、中心/蛇集合完成、蝙蝠/青蛙普通奖励及 130 核桃替代奖励。生产执行只经 `FieldOfficeDesk` 互斥锁、`Safari_Donate` 回答、`FieldOfficeMenu` 背包/精确 holder/OK 输入完成，不直接修改持久状态。最新 schema 为 `148/132/16/0`；对账为 `184 registered / 208 semantic / 183 compiler-bound / 107 five-gate / 48 allowlist / 24 catalogued blocked / 0 Product Executor`。正式全量训练仍受剩余 24 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡。下一语义切片为独立 `island.field_office_survey`；透明读已存在，但不得用 EVD-302 捐赠回执替代调查执行证据。

## 历史记录 · 2026-08-30 住宅装修玩家命令闭环（EVD-301）

`housing.renovate` 已通过 read / candidate / compile / native runtime / output receipt 五门，但严格保持 `PlayerCommandOnly`，不进入正式策略训练 allowlist。模型或玩家命令层只给出精确装修 ID、区域、原因和确认；目录、要求、原生商店顺序、价格、首次购买退款语义、区域几何、阻挡、柜台站位和菜单输入均由 fresh snapshot 与编译执行层机械绑定。破坏动画另需独立破坏性确认。

隐藏静音 E 盘矩阵 `19/19` 覆盖实时 `Data/HomeRenovations` 的 18 个原版条目，以及一个负价、无 `FirstPurchase` 标记、不退款分支。生产执行只通过 Robin 原生对话、`ShopMenu("HouseRenovations")` 和 `RenovateMenu` 输入完成，不直接写钱、邮件、`NetInt`、地图、家具或事件。跨地图 continuation 以装修 ID、区域、原因和确认锁定同一目标，并在每次新快照排名时恢复 `PlayerCommand` 来源，终端原生执行成功后才结束。最新 schema 为 `146/130/16/0`；对账为 `182 registered / 206 semantic / 181 compiler-bound / 105 five-gate / 48 allowlist / 24 catalogued blocked / 0 Product Executor`，回归为 Core `2045/2045`、Backend `148/148`、Release `0 warnings / 0 errors`。正式全量训练仍受剩余 24 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `island.field_office_donate`。

## 历史记录 · 2026-08-30 垃圾桶翻找训练准入（EVD-300）

`foraging.rummage_garbage` 已通过 read / candidate / compile / native runtime / output receipt 五门并进入 `StrategyValue` allowlist。小模型只决定是否翻找某个当前可达且安全的未检查垃圾桶；桶身份、地图位置、站位、路径、运气/书籍状态、确定输出、交付方式、安全槽、NPC 目击和任务回执都由编译执行层从 fresh snapshot 机械绑定。已检查、负友谊目击、数据/预测漂移、无安全槽和不可达目标在上游排除。

隐藏静音 E 盘矩阵 `9/9` 覆盖空结果、普通 debris、直接入包垃圾帽、Desert Festival 多 debris、两类排除、Linus 正向反应以及普通/特别收集任务。生产执行只调用一次原生 `GameLocation.checkAction`，不写 CheckedGarbage、统计、好感、库存、debris 或 RNG。最新 schema 为 `146/130/16/0`；对账为 `180 registered / 205 semantic / 179 compiler-bound / 103 five-gate / 48 allowlist / 25 catalogued blocked / 0 Product Executor`，回归为 Core `2039/2039`、Backend `145/145`、Release `0 warnings / 0 errors`。正式全量训练仍受剩余 25 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `housing.renovate`。

## 历史记录 · 2026-08-30 普通树产品训练准入（EVD-299）

`foraging.harvest_tree_product` 已通过 read / candidate / compile / native runtime / output receipt 五门并进入 `StrategyValue` allowlist。小模型只决定是否收取当前可用普通树产品；树种、地点、站位、路径、安全空槽、确定掉落和随机附加掉落域全部由编译执行层机械生成并在 fresh snapshot 重绑。自定义树、数据漂移、未成熟、树桩、tapped、无种子、原生等级门不满足、摇动中、输出域不完整及不可达目标在上游直接排除。

隐藏静音 E 盘矩阵覆盖普通种子、秋季榛子、岛屿棕榈和三类排除分支。生产执行只调用一次原生 `GameLocation.checkAction`，以背包加 debris 守恒验收确定输出及至多一个有界可选输出，不读取或改写 RNG。最新 schema 仍为 `145/129/16/0`；对账为 `178 registered / 204 semantic / 177 compiler-bound / 102 five-gate / 47 allowlist / 26 catalogued blocked / 0 Product Executor`，回归为 Core `2035/2035`、Backend `144/144`。正式全量训练仍受剩余 26 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `foraging.rummage_garbage`。

## 历史记录 · 2026-08-30 果树收获训练准入（EVD-298）

`foraging.harvest_fruit_tree` 已通过 read / candidate / compile / native runtime / output receipt 五门并进入 `StrategyValue` allowlist。小模型只决定是否在日计划中收获已就绪果树；位置、站位、路径、交互、live fruit、品质、雷击替换、数量与零经验回执全部由编译执行层机械生成并在 fresh snapshot 重绑。空树、未成熟、树桩、摇动中、自定义类型和不可达目标在上游直接排除，不产生无意义训练阻塞。

隐藏静音 E 盘矩阵覆盖单果普通、三果金星、雷击三煤炭、空树排除和摇动中排除；生产执行只调用一次原生 `GameLocation.checkAction`，不写任何结果状态。最新 schema 仍为 `145/129/16/0`；对账为 `176 registered / 203 semantic / 175 compiler-bound / 101 five-gate / 46 allowlist / 27 catalogued blocked / 0 Product Executor`，回归为 Core `2031/2031`、Backend `143/143`。正式全量训练仍受剩余 27 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `foraging.harvest_tree_product`。

## 历史记录 · 2026-08-30 鱼塘管理玩家命令闭环（EVD-297）

`fishing.manage_fish_pond` 已完成五道证据闭环，但严格保持 `PlayerCommandOnly`，不进入正式训练 allowlist。玩家必须提供精确鱼塘、操作和原因，`empty_pond` 另需操作级确认；自动日计划继续只使用既有 `fishing.service_fish_ponds` 处理产出与请求，不会把换网装饰或清塘破坏性重置混入收益训练。

透明桥发布鱼塘管理状态、四种网样式、空手安全槽、精确站位、清塘前状态及反编译锁定的 reset/preserve 收据，菜单桥发布绑定鱼塘、确认状态和全部公共按钮。运行层复用共享 BFS，经作用域右键边沿和真实 `GameLocation.checkAction -> FishPond.doAction -> PondQueryMenu.receiveLeftClick` 执行；不构造菜单、不调用 `ClearPond`、不直接写鱼塘状态。隐藏静音运行 `runtime-fish-pond-management-20260830-013602` 覆盖换网和确认清塘两支。最新 schema 为 `145/129/16/0`，对账为 `174 registered / 202 semantic / 173 compiler-bound / 100 five-gate / 45 allowlist / 28 catalogued blocked / 0 Product Executor`，回归为 Core `2027/2027`、Backend `142/142`。正式全量训练仍受剩余 28 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `foraging.harvest_fruit_tree`。

## 历史记录 · 2026-08-30 展览会转盘策略与原生随机执行闭环（EVD-296）

`festival.spin_wheel` 已完成五道证据闭环并进入 `StrategyValue` allowlist。模型只决定是否用绿方转盘补齐未获得 Fair Stardrop 的至少两枚星币缺口；编译器从 fresh snapshot 绑定节日、Buildings `308/309`、站位、节庆币、需求、零幸运 `22/30` 分布、有效 `LuckLevel`、数字菜单和 native contract。下注严格为 `min(remainingDemand, floor(festivalScore * 7 / 15))`，即等赔率零幸运 Kelly 比例，不把 50% 误标为 Kelly；`executor.spin_fair_wheel` 保持 `ExecutorCalibration` 与 policy confirmation。

运行层复用共享 BFS 和原生菜单输入，只经 `Event.checkAction -> DialogueBox(Green) -> NumberSelectionMenu -> WheelSpinGame` 启动，接受原版随机胜负并核对精确 `+/- wager`、结果文字与退出。最终隐藏静音运行 `runtime-fair-wheel-spin-20260830-005054` 用两次 `466` 星币下注覆盖胜负两支，festivalScore 分别 `1000->1466` 与 `1000->534`；生产代码不写 RNG、旋转、结算或结果。最新 schema 为 `145 required / 129 readable / 16 contextual / 0 blocking`，对账为 `173 registered / 202 semantic / 172 compiler-bound / 99 five-gate / 45 allowlist / 29 catalogued blocked / 0 Product Executor`，回归为 Core `2023/2023`、Backend `138/138`、Release `0 warnings / 0 errors`。正式全量训练仍受剩余 29 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `fishing.manage_fish_pond`。

## 历史记录 · 2026-08-29 展览会力量小游戏策略与原生执行闭环（EVD-295）

`festival.play_strength_game` 已完成五道证据闭环并进入 `StrategyValue` allowlist。模型只在扣除未领取陈列奖励后，未获得 Fair Stardrop 的缺口恰好为 `1` 星币时决定是否进行这次免费尝试；其他缺口不会把单次固定一币的小游戏误当作高效循环。编译器从 fresh snapshot 绑定节日实例、Buildings `540`、站立 X=`29`、力量/速度/方向、动画和计时合同、商店需求及 native contract；`executor.play_fair_strength_game` 严格为 `ExecutorCalibration`。

运行层复用共享 BFS，等待移动输入结算后通过真实 `Event.checkAction` 打开 `StrengthGame`，以点击后恰好 `9` 次原生更新预测满力量窗口并只调用一次原生点击。隐藏静音样本覆盖两种初始速度：`64/+4 -> 100` 与 `72/+3 -> 99`，均由原版把 festivalScore `1999->2000` 并完成结果对话/退出；生产代码不直接写力量、得分、计时器、菜单或位置。最新 schema 为 `143 required / 127 readable / 16 contextual / 0 blocking`，对账为 `171 registered / 201 semantic / 170 compiler-bound / 97 five-gate / 44 allowlist / 30 catalogued blocked / 0 Product Executor`，回归为 Core `2018/2018`、Backend `138/138`、Release `0 warnings / 0 errors`。正式全量训练仍受剩余 30 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `festival.spin_wheel`。

## 历史记录 · 2026-08-29 展览会靶场策略与原生执行闭环（EVD-294）

`festival.play_slingshot_game` 已完成五道证据闭环并进入 `StrategyValue` allowlist。模型只决定是否投入 50g 和原版 50 秒会话补齐未获得 Fair Stardrop 的星币缺口；尚未领取的展览陈列奖励会先从缺口扣除。编译器从 fresh snapshot 绑定节日实例、交互/站立图块、金额、星币、四段时序、79 个目标、Dialogue key 和 native contract；`executor.play_fair_slingshot_game` 严格为 `ExecutorCalibration`。

运行层复用共享移动与普通矿井弹弓唯一的瞄准补丁，在原生 TargetGame 物理更新前预测拦截点并发送按下/蓄力/释放输入。验收精确核对 50g、临时弹弓和弹药、shots/success、原版 accuracy 分母、75/85/90/95/100% 倍率、得分、40 分奖励门、280 分封顶、节庆返回和临时物品清理，不直接写任何结果。隐藏静音样本为 `48/48` 命中、raw `95`、accuracy `102`、final `380`、`500` 星币。最新 schema 为 `142 required / 126 readable / 16 contextual / 0 blocking`，对账为 `169 registered / 200 semantic / 168 compiler-bound / 95 five-gate / 43 allowlist / 31 catalogued blocked / 0 Product Executor`，回归为 Core `2013/2013`、Backend `138/138`、Release `0 warnings / 0 errors`。正式全量训练仍受剩余 31 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `festival.play_strength_game`。

## 历史记录 · 2026-08-29 展览会钓鱼小游戏策略与原生执行闭环（EVD-293）

`festival.play_fishing_game` 已完成五道证据闭环并进入 `StrategyValue` allowlist。模型只决定是否投入 50g 和原版 100 秒来补齐未获得 Fair Stardrop 的星币缺口；上游会扣除尚未领取的展览陈列奖励，不会为其他装饰商店行无限重复。编译器从 fresh snapshot 绑定节日实例、交互/站立图块、金额、星币、缺口、时长、Dialogue key 和 native contract；`executor.play_fair_fishing_game` 严格为 `ExecutorCalibration`。

运行层复用共享移动和普通钓鱼预测输入，在游戏物理更新前控制原生 BobberBar。运行验收不把随机完美率误作执行器稳定性：必须精确验证 50g、真实 FishingGame、原版 raw score + perfection bonus + triple-perfect multiplier、星币公式、节日返回和临时钓具清理，完美数/有效鱼数则作为收益反馈。最终隐藏静音样本为 `5/5` 完美、`364` 分、`432` 星币。最新 schema 为 `141 required / 125 readable / 16 contextual / 0 blocking`，对账为 `167 registered / 199 semantic / 166 compiler-bound / 93 five-gate / 42 allowlist / 32 catalogued blocked / 0 Product Executor`。Core `2008/2008`、Backend `138/138`、Release `0 warnings / 0 errors`。正式全量训练仍受剩余 32 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `festival.play_slingshot_game`。

## 历史记录 · 2026-08-29 展览会陈列策略与原生执行闭环（EVD-292）

`festival.manage_grange_display` 已完成五道证据闭环并进入 `StrategyValue` allowlist。模型只决定是否在展览会准备一等奖陈列或在评审后取回；透明桥与编译器从 fresh snapshot 绑定共享展台、库存单位、实际售价、品质、八类多样性、九件数量分、Mayor 短裤、评审状态、交互图块、互斥锁和下一次唯一机械操作。`executor.manage_grange_display` 严格为 `ExecutorCalibration`，每个快照只允许一次原生放入/取回，不进入策略训练，也不得启动评审。

隐藏静音隔离运行 `10/10` 通过：九次原生菜单放入达到 `124` 分，超过一等奖阈值 `90`，评审后一次原生取回；生产链复用共享 BFS/连续移动与 `Event.checkAction -> StorageContainer -> grangeMutex`，不直接写展台、库存、评分或评审状态。最新 schema 为 `140 required / 124 readable / 16 contextual / 0 blocking`，对账为 `165 registered / 198 semantic / 164 compiler-bound / 91 five-gate / 41 allowlist / 33 catalogued blocked / 0 Product Executor`。Core `2003/2003`、Backend `138/138`、Release `0 warnings / 0 errors`。正式全量训练仍受剩余 33 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `festival.play_fishing_game`。

## 历史记录 · 2026-08-29 传送图腾执行器校准（EVD-291）

`executor.use_warp_totem` 已完成五道执行证据闭环，训练角色严格为 `ExecutorCalibration`。上游策略只决定目的地和“是否值得消耗”；机械链从 fresh snapshot 重绑五种精确库存、Farm 地图属性/农场回退、固定目的地、主动与被动节日路由、地图边缘修正、2000ms 动画和 1000ms 原生回调。会消耗但不传送的节日前分支、联机 ReadyCheck、精确目的地重复使用和基础物品门失败均在消费前排除。

隐藏静音隔离运行五变体 `5/5` 验证原生单物品消费、68 个即时效果精灵、五个精确落点和最终角色状态恢复。最新 schema 为 `139 required / 123 readable / 16 contextual / 0 blocking`，对账为 `163 registered / 197 semantic / 162 compiler-bound / 89 five-gate / 40 allowlist / 34 catalogued blocked / 0 Product Executor`。该切片不进入训练 allowlist，也不解除 Product Executor、剩余 34 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等阻挡。下一语义切片为 `festival.manage_grange_display`。

## 历史记录 · 2026-08-29 宝藏图腾执行器校准（EVD-290）

`executor.use_treasure_totem` 已完成五道执行证据闭环，训练角色严格为 `ExecutorCalibration`。上游策略只决定是否值得在当前位置消耗图腾；机械链从 fresh snapshot 重绑精确库存、室内门、中心周围 16 格原生候选环、每格可生成原因、`TreasureTotemsUsed` 和时序合同。公共物品门失败、室内或零可生成格会在消费前排除，不能依赖下游失败补救。

隐藏静音隔离运行验证原生 `16/16` 宝藏点生成、图腾 `2->1`、世界计数 `0->1` 和地点宝藏点 `5->21`。生成结果由既有宝藏点读取与挖掘链继续处理，不训练也不复制机械挖掘。最新 schema 为 `138 required / 122 readable / 16 contextual / 0 blocking`，对账为 `162 registered / 197 semantic / 161 compiler-bound / 88 five-gate / 40 allowlist / 35 catalogued blocked / 0 Product Executor`。该切片不进入训练 allowlist，也不解除 Product Executor、剩余 35 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等阻挡。下一语义切片为 `executor.use_warp_totem`。

## 历史记录 · 2026-08-29 回城魔杖执行器校准（EVD-289）

`executor.use_return_scepter` 已完成五道执行证据闭环，训练角色严格为 `ExecutorCalibration`。上游只决定“现在是否值得立即回家”；机械链从 fresh snapshot 重绑精确 `Wand`、当前角色自己的 `FarmHouse`/`Cabin`、门前格、稳定输入门、即时工具调用、1000ms 原生回调和最终状态。已在落点、住宅不可解析、浴衣、桥上或执行瞬态不稳定时均在候选/编译阶段排除，不能等到原生回调后再补救。

隐藏静音隔离运行验证房主分支的原生落点、29 精灵即时状态、最终显示/无敌/移动恢复和可复用库存。最新 schema 为 `137 required / 121 readable / 16 contextual / 0 blocking`，对账为 `161 registered / 197 semantic / 160 compiler-bound / 87 five-gate / 40 allowlist / 36 catalogued blocked / 0 Product Executor`。该切片不进入训练 allowlist，也不解除 Product Executor、剩余 36 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等阻挡。下一语义切片为 `executor.use_treasure_totem`。

## 历史记录 · 2026-08-29 雨水图腾执行器校准（EVD-288）

`executor.use_rain_totem` 已完成五道执行证据闭环，但训练角色严格为 `ExecutorCalibration`。上游只决定未来天气是否值得消耗图腾；机械链从 fresh snapshot 绑定精确库存、上下文许可/重定向、天气状态归属、明日日期、换日最终天气、动画和对话结算。官方 Wiki 关于季节首日的可见规则已用锁定 1.6.15 的 `Game1.getWeatherModificationsForDate` 复核并扩大为完整默认上下文覆盖门，不能只凭即时 `WeatherForTomorrow=Rain` 认定有效。

隐藏静音隔离运行覆盖 Default、Desert->Default、Island 和默认节日前拒绝四条分支。最新 schema 为 `136 required / 120 readable / 16 contextual / 0 blocking`，对账为 `160 registered / 197 semantic / 159 compiler-bound / 86 five-gate / 40 allowlist / 37 catalogued blocked / 0 Product Executor`。该切片不进入训练 allowlist，也不解除 Product Executor、剩余 37 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等阻挡。下一语义切片为 `executor.use_return_scepter`。

## 历史记录 · 2026-08-29 怪兽香水执行器校准（EVD-287）

`executor.use_monster_musk` 已完成五道执行证据闭环，但训练角色严格为 `ExecutorCalibration`。小模型或上游战斗目标只决定是否需要提高怪物密度；精确库存槽、Buff 24 当前状态、消费、朝向、动画时序和刷新回执全部由机械编译/执行链负责。普通矿井与火山地牢都按在线玩家 Buff 24 将怪物生成率乘以 2，普通矿井与驱怪 Buff 23 的组合继续服从原生优先级。

隐藏静音隔离运行覆盖“无 Buff 首次施加”和“已有 Buff 替换刷新”两条分支。最新 schema 为 `135 required / 119 readable / 16 contextual / 0 blocking`，对账为 `159 registered / 197 semantic / 158 compiler-bound / 85 five-gate / 40 allowlist / 38 catalogued blocked / 0 Product Executor`。该切片没有把机械原语直接加入策略训练，也不解除 Product Executor、剩余语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等阻挡。下一语义切片为 `executor.use_rain_totem`。

## 历史记录 · 2026-08-29 马笛执行器校准（EVD-286）

`executor.use_horse_flute` 已完成五道执行证据闭环，但训练角色严格为 `ExecutorCalibration`：策略只需决定是否用马笛，机械层从 fresh snapshot 重绑库存、限制掩码、拥有马匹身份、邻近状态、朝向和延迟结果。模型不得预测或复制原生 team event、mutex 与传送副作用。

隐藏静音隔离运行覆盖远程 1500ms 召回与邻近成功无传送两条分支，并验证同一马匹 GUID、精确落点、朝向规则和可复用库存。最新 schema 为 `134 required / 118 readable / 16 contextual / 0 blocking`，对账为 `158 registered / 197 semantic / 157 compiler-bound / 84 five-gate / 40 allowlist / 39 catalogued blocked / 0 Product Executor`。因此该切片不解除 Product Executor、剩余 39 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等全量训练阻挡。下一语义切片为 `executor.use_monster_musk`。

## 历史记录 · 2026-08-29 烟花玩家命令边界（EVD-285）

`executor.use_firework` 已闭合五道执行证据，但训练 allowlist 保持 `40`。烟花是显式玩家表达命令，不是第三年爷爷 21 分路线、日循环或资源规划的自主欲望；相关运行样本只属于 `player_command_only_executor_evidence`。模型不能学习或猜测共享 RNG 的精确下一值。

隐藏静音隔离运行已验证 `(O)893/(O)894/(O)895` 三个分支、原生 5 精灵图、目标格冲突、随机域和精确单件消耗。最新 schema 为 `133 required / 117 readable / 16 contextual / 0 blocking`，对账为 `157 registered / 197 semantic / 156 compiler-bound / 83 five-gate / 40 allowlist / 40 catalogued blocked / 0 Product Executor`。因此该切片不解除 Product Executor、剩余 40 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等全量训练阻挡。下一语义切片为 `executor.use_horse_flute`。

## 历史记录 · 2026-08-29 秘密纸条执行器校准（EVD-284）

`executor.read_secret_note` 已完成五道执行证据闭环，但训练角色仍为 `ExecutorCalibration`：它校准透明桥、编译器和执行器能否忠实完成一个已由上游选择的纸条读取，不让策略模型学习原生随机数、菜单构造或库存扣减。普通纸条与日记残页的选择均在 fresh snapshot 中机械计算，小模型不能伪造 note id 或任务副作用。

隐藏静音隔离运行覆盖多未读种子抽取、任务 30、任务 29 和普通日记残页四条分支。最新 schema 为 `132 required / 116 readable / 16 contextual / 0 blocking`，对账为 `156 registered / 197 semantic / 155 compiler-bound / 82 five-gate / 40 allowlist / 41 catalogued blocked / 0 Product Executor`。因此该切片不解除 Product Executor、剩余 41 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等全量训练阻挡。下一语义切片为 `executor.use_firework`。

## 历史记录 · 2026-08-29 草种放置执行器校准（EVD-283）

`executor.plant_grass` 已完成五道执行证据闭环，但训练角色严格为 `ExecutorCalibration`：它证明动作编译和执行器能忠实完成上游给定的普通/蓝草精确布局，不为策略模型生成“应该在哪里种草”的价值标签。上游必须给出用途、精确地块和时间预算；编译器从 fresh snapshot 重绑所有机械字段。

隐藏静音隔离运行已验证 `(O)297 -> Grass(1,4)` 与 `(O)BlueGrassStarter -> Grass(7,4)`，库存精确减一且透明后状态可读。最新 schema 为 `131 required / 115 readable / 16 contextual / 0 blocking`，对账为 `155 registered / 197 semantic / 154 compiler-bound / 81 five-gate / 40 allowlist / 42 catalogued blocked / 0 Product Executor`。因此该切片不解除 Product Executor、剩余 42 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等全量训练阻挡。下一语义切片为 `executor.read_secret_note`。

## 历史记录 · 2026-08-29 Drum Block 玩家命令边界（EVD-282）

`world.tune_drum_block` 已闭合五门执行证据，但训练 allowlist 保持 `40`。调音属于显式玩家表达/谜题布置，不是自主日计划欲望；运行数据只属于 `player_command_only_executor_evidence`。路过自动播放是独立对象邻接回调，不生成第二个训练动作。

当前权威状态为 `154 registered / 197 semantic / 153 compiler-bound / 80 five-gate / 43 catalogued blocked / 0 Product Executor`。该切片不解除 Product Executor、剩余 43 个动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等全量训练阻挡。下一语义切片为 `executor.plant_grass`。

## 历史记录 · 2026-08-29 Flute Block 玩家命令边界（EVD-281）

`world.tune_flute_block` 已闭合五门执行证据，但训练 allowlist 保持 `40`。调音是玩家表达/谜题布置命令，不是自主日计划欲望；运行数据只属于 `player_command_only_executor_evidence`。路过自动播放是对象邻接回调，不生成第二个训练动作。

当前权威状态为 `153 registered / 197 semantic / 152 compiler-bound / 79 five-gate / 44 catalogued blocked / 0 Product Executor`。该切片不解除 Product Executor、剩余 44 个动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等全量训练阻塞。

## 历史记录 · 2026-08-29 Farm Computer 透明信息与训练边界（EVD-280）

`farming.read_farm_computer_report` 已闭合五门执行证据，但训练 allowlist 保持 `40`。透明桥已经按原生根地点语义直接发布报告的全部结构化来源和精确本地化摘要，因此策略模型可直接使用这些状态；打开 Farm Computer 只服务显式玩家查看，不得制造“先读菜单才能决策”的训练依赖。

运行时只复用共享移动器并调用一次原生地点交互，验证 500ms 延迟 `DialogueBox`、报告摘要、对象身份及槽位恢复。当前权威状态为 `152 registered / 197 semantic / 151 compiler-bound / 78 five-gate / 45 catalogued blocked / 0 Product Executor`。该切片减少一个执行缺口，但不解除 Product Executor、剩余 45 个动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等全量训练阻塞。

## 历史记录 · 2026-08-29 Mini-Obelisk 校准边界（EVD-279）

`movement.use_mini_obelisk` 已闭合五门执行证据，但不增加训练 allowlist。它的作用是校准“机械路由原语能否严格复刻原生配对、目标和落点”，而不是让策略模型学习是否想传送。默认策略候选排除该动作；只有显式启用执行器校准候选时才发布，运行结果标记为 `executor_calibration_only_not_strategy_desire`，数据清洗不得把成功、耗时或落点当作策略偏好标签。

当时准入计数仍为 `40`，权威状态为 `151 registered / 197 semantic / 150 compiler-bound / 77 five-gate / 46 catalogued blocked / 0 Product Executor`。因此这次闭环减少了一个原版动作缺口，但没有解除 Product Executor、剩余 46 个动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等正式全量训练阻塞。

## 历史记录 · 2026-08-28 Auto-Grabber 训练准入与兼容占位边界（EVD-277 / EVD-278）

`animals.collect_auto_grabber_contents` 已进入训练 allowlist。模型只决定是否在当前日计划中收取；精确对象、站位、安全槽、held Chest 全部堆栈身份、累计背包容量、转移集合和留存集合均由新鲜快照与编译器决定。空容器、全部物品不可接纳或无安全站位不会生成候选。生产执行只调用原生对象交互并向 `ItemGrabMenu` 发送原生点击，验收要求源集合严格等于“已转移 + 留存”、背包回执匹配、对象/Chest 身份不变且菜单关闭。

Lantern/Raft 是分母外兼容占位，不是训练缺口，也不得生成负样本。当时准入计数为 `40`，完整权威状态为 `150 registered / 197 semantic / 149 compiler-bound / 76 five-gate / 47 catalogued blocked / 0 Product Executor`。这只增加一个经过原生运行验证的策略动作，不改变正式全量训练仍受 Product Executor、剩余 47 个动作、正式轨迹 manifest/checkpoint、独立存档评测及第三年 21 分长跑验收阻塞的结论。

## 历史记录 · 2026-08-28 喂食斗训练准入与木筏分母修正（EVD-275 / EVD-276）

`animals.withdraw_feed_hopper_hay` 已通过五门证据并进入训练 allowlist。训练样本只允许来自“当前动物屋至少有一只未喂动物、原生精确取草量为正、背包可接纳、菜单与站位安全”的候选；编译器必须从同一新鲜快照重绑根料仓、动物数、容量、已摆干草、取草量、安全槽和站位。生产运行只调用一次原生 `GameLocation.checkAction`，并以料仓 `-N`、背包 `(O)178 +N` 的守恒回执验收。E 盘隐藏静音运行 `runtime-feed-hopper-20260828-130723` 验证 `N=8`。

不可达的 `Raft` 遗留类型不属于训练缺口。锁定 1.6.15 没有原版获取或调用入口，因此该历史检查点把语义分母校正为 `198`；当前由 EVD-277 以分母外兼容占位保留原生表面。该检查点状态为 `149 registered / 198 semantic / 148 compiler-bound / 75 five-gate / 39 allowlist / 49 catalogued blocked / 0 Product Executor`；这仍不解除 Product Executor、正式数据 manifest/checkpoint、独立存档评测和第三年 21 分长跑阻塞。

## 历史记录 · 2026-08-28 PlayerCommandOnly 原生声音交互边界

`world.play_singing_stone` 已闭合五门执行证据，但正式训练准入保持关闭。锁定 1.6.15 原生语义只允许透明发布共享 RNG 的 24 项均匀音高分布，不允许预读或猜测下一音高。显式玩家命令经确认后，编译器重绑精确 `(BC)94`、站位和安全槽，运行时通过共享移动器调用一次原生地点交互并验证 `shakeTimer=100`、对象身份与槽位恢复。其证据范围为 `player_command_only_executor_evidence`，默认候选、策略请求和训练清洗分别通过生成、授权与类型化排除三层阻断。当时 allowlist 仍为 `38`，没有因为该执行器验证而增长。

状态日期：2026-08-01

## 1. 当前结论

项目已经具备可运行的工程闭环：

`透明快照 -> 候选生成 -> 日计划 -> 动作编译 -> 原生执行 -> 前后状态 -> 训练记录`

但这不等于已经具备正式全量训练条件。当前的主要阻塞不是“再跑一次短训”，而是训练准入证据、全量候选覆盖、正式轨迹数据和真实模型提供器尚未同时闭合。

短训只允许用于验证数据管线、显存、检查点和推理接口，不能作为训练成果汇报。

### 调用来源硬边界

`capability_registry.v3` 将“具备执行能力”和“允许策略模型选择”分开。`PlayerCommandOnly` 动作可以拥有完整透明读取、编译器、运行时和回执，但必须同时满足：默认候选不发布、策略来源请求由安全门阻断、训练清洗以类型化原因排除、仅 `InvocationSource=PlayerCommand` 的显式玩家请求可继续原有授权链。装饰轮转、建筑外观、家具放置、标牌展示物与文字编辑属于该类；它们的运行证据不得被解释成策略训练准入。

机械但具有正常日循环价值的动作不因此被排除。例如 `farming.collect_slime_ball` 只让模型选择当前合法目标，随机种子、产量、站位、空槽、原生交互和后续 debris 拾取均由透明桥与编译器绑定，因此在 EVD-272 五门证据闭合后可以进入有界策略训练。

2026-08-02 进展：`capability_registry.v2` 已实现五门证据、证据 ID、范围和类型化排除原因，空 allowlist 会使注册表初始化失败。该阶段准入项为 `mining.reach_depth`、`mining.obtain_skull_key`、`inventory.transfer_item`、`social.talk_npc` 和 `social.gift_npc`，每项都仅限其登记的 EVD 范围；这不是任意深度、任意库存、全矿洞或全社交完成声明。EVD-202 仅把 EVD-106 已验证的普通矿井 119 -> 120 层、原生骷髅钥匙宝箱领取和退出链登记为 `mining.obtain_skull_key` 五门证据，不覆盖沙漠矿洞、采石场矿洞金镰刀或火山矿洞。EVD-195 已闭合 `recovery.stabilize_day` 的滚动跨图回家和原生终端睡眠运行/输出门，但该项按策略仍是校准型高层动作，不进入训练白名单。EVD-196 已闭合当前已加载原版 NPC 的滚动远端送礼和普通单件礼物栈归零。EVD-197 又使旧聚合训练器、默认/显式排序和预测 API 统一经过生成式 allowlist。EVD-198/EVD-199 已建立正式策略轨迹契约并接入 LiveTrainingLoop 的有效排序/队列/源哈希绑定。EVD-200 已完成确定性清洗、冲突去重、按存档/日切分、SHA-256 清单、不可变版本锁和日/季/年/爷爷 21 分回报回填；EVD-201 已完成 C# 结构化排序器和检查点往返。未闭合跨度保留为 `null/pending`，不得猜测标签。当前 E 盘还没有真实策略轨迹和跨度观测，因此这是数据治理与模型基础设施就绪，不是正式数据集或训练完成。下一步继续按权威字典依赖顺序扩大五门准入；在真实长期 rollout 和其余正式准入条件闭合前仍不能启动正式全量训练。

2026-08-02 EVD-203 当前增量：`volcano.reach_caldera` 已独立绑定 EVD-190/EVD-191 的火山
0..9 滚动原生动作、目的化战斗与 Caldera 终态。它不借用普通矿井、Skull Cavern 或采石场
矿洞金镰刀证据，也不让模型控制机械原语。当前准入为 `mining.reach_depth`、
`mining.obtain_skull_key`、`volcano.reach_caldera`、`inventory.transfer_item`、
`social.talk_npc`、`social.gift_npc` 六个有界范围；生产轨迹、跨度观测、正式 manifest 和
checkpoint 仍不存在，因此正式全量训练仍然阻塞。

2026-08-02 EVD-204 当前增量：`skills.read_books` 已绑定 EVD-124 的六类原版基础书籍分支和七用例
原生矩阵。治理目录现在同时识别日计划 option 展开与动作队列直接编译，仍只保留唯一
`read_inventory_book -> executor.read_book -> wait_ticks` 链。该切片完成时训练 allowlist 为七个有界范围；
自定义书籍覆盖和畸形模组标签不在准入内。

2026-08-02 EVD-205 当前增量：`foraging.harvest_ginger` 已绑定 EVD-119 的原版当前地图精确姜收获
矩阵，复用唯一 `harvest_ginger -> executor.harvest_ginger` 链。覆盖干燥普通锄、雨天 Efficient 且
背包满后的 debris 输出，以及体力不足上游排除；自定义 Hoe/Crop/HoeDirt 和其他采集族不在准入内。
该切片完成时训练 allowlist 为八个有界范围，正式生产轨迹、manifest、checkpoint 与 Product Executor 仍不存在。

2026-08-02 EVD-206 当前增量：`foraging.harvest_bushes` 已绑定 EVD-120 的原版当前地图精确 Bush
六用例矩阵，复用唯一 `harvest_bush -> executor.harvest_bush` 链。普通浆果、Botanist 浆果、茶叶和
金核桃原生成功，已领取金核桃与摇动冷却在上游排除；自定义 Bush 和 town bush 不在准入内。
该切片完成时训练 allowlist 为九个有界范围，正式全量训练的其他阻塞不变。

2026-08-02 EVD-207 当前增量：`mining.claim_reward_chests` 已绑定 EVD-122 的已加载原版 MineShaft
精确奖励箱范围，复用唯一领取链。固定奖励、星之果实与强制随机奖励完成原生领取和清箱；骷髅钥匙
特殊箱、金镰刀祭坛与未知箱体不在准入内。金镰刀的完整运行证据不覆盖其显式玩家确认门，因此仍
不得进入策略训练。该切片完成时训练 allowlist 为十个有界范围。

2026-08-02 EVD-208 当前增量：`foraging.pan_ore_spot` 已绑定当前地图精确活动矿点和实时 Pan 奖励
投影。铜盘、钢盘两次原生生命周期验证了精确奖励、收货统计、TimesPanned、采矿/采集 XP 与矿点
消费；所有候选仍必须通过当前 Pan 状态与隔离 RNG 精确投影，不使用固定奖励表。当前训练 allowlist
在该切片完成时为十一个有界范围。

2026-08-02 EVD-209 当前增量：`fishing.collect_crab_pots` 已绑定当前地图已就绪原版基础 `CrabPot`
的精确原生收取生命周期，复用唯一收取链。实时产物、Book of Crabbing 确定性翻倍、背包入账、
Fishing XP、`caughtFish` 统计以及 bait/ready/tile-index 复位均纳入读、候选、编译、运行和输出证据。
未就绪、背包拒收、投影不完整与自定义子类失败关闭；放置和补饵不在该范围。当前训练 allowlist
在该切片完成时为十二个有界范围。

2026-08-03 EVD-210 当前增量：`fishing.service_fish_ponds` 已绑定已完成原版基础 `FishPond` 的
产物收取与人口请求双分支。产物保持原生优先；请求逐件消耗透明绑定物品，并核验人口上限、解锁
门槛、刷新计时和 Fishing XP。请求的 `PolicyAuthorizationRequired` 表示模型只能在授权策略内决策，
不同于会阻断训练准入的 `ExplicitUserConfirmationRequired`；运行时约束没有放宽。当前训练 allowlist
为十三个有界范围。

2026-08-03 EVD-211 当前增量：`foraging.collect_spawned_objects` 已绑定当前加载地图中精确原版基础
`StardewValley.Object` 的原生拾取链。隔离运行矩阵覆盖普通、Botanist、确定性 Gatherer 双倍、特殊
`724519` 和动物屋内部五类，并逐项核验数量、品质、Foraging/Farming XP。训练请求现完整运输这些
上游投影，运行层不再用自行重算掩盖字段断点。Lewis 地下室 `(O)789` 还有生成 Bat 和音画状态副作用，
在透明建模前于读层和运行层失败关闭。当前训练 allowlist 为十四个有界范围。

## 2. “正式全量训练”的定义

正式全量训练只训练模型应当决定的内容：

- 从当前合法候选中选择高层目标、目标对象和策略参数；
- 安排必要任务、附加任务、资源预算和时间预算；
- 处理经济、剧情、收集、关系与长期目标之间的权衡；
- 在不确定地图或随机事件中选择目标与退出条件。

以下内容不进入策略模型的自由输出空间：

- WASD、转向、挥刀、工具使用、拾取、开门、柜台交互等机械输入；
- BFS、动态避障、可清除障碍估时、安全窗口、补血和战斗微操；
- 已确定布局后的农场维护、固定机器收取与补料；
- 动作合法性、时间许可、资源许可和不可逆操作授权。

这些由动作编译器和执行器确定性完成，并通过校准数据持续验证。模型只能输出受类型约束的候选 ID 和必要参数，不能直接生成任意按键脚本。

“全量”是指训练所有已经通过准入证据的模型级候选，并覆盖从新存档到第三年爷爷评分 21 分目标所需的长期轨迹；不是训练所有登记名称，更不是把执行器原语混入策略训练。

## 3. 正式训练前的四个工程包

### 3.1 训练准入与证据注册表

为每个模型级候选建立五道独立门：

1. `read`：透明桥能实时读取决策所需字段；
2. `candidate`：上游能在正确时间、地点和资源条件下生成或排除候选；
3. `compile`：动作编译器能生成有界、可审计的动作队列；
4. `runtime`：原生执行器完成过真实运行验证；
5. `output`：结果、失败原因、耗时和状态变化能完整回写。

必须区分：

- 已实现但证据尚未登记；
- 已登记但仅有模拟证据；
- 真实运行已通过；
- 确有实现缺口；
- 因不可逆授权或环境限制而暂不准入。

退出条件：

- 训练 allowlist 非空；
- allowlist 内每项五门全通过；
- allowlist 外每项都有类型化排除原因；
- 候选生成器只向训练暴露 allowlist；
- 空 allowlist 不得让测试以“空集合成立”方式误通过。

### 3.2 能力缺口闭合与候选扩展

先用证据注册表找出真实缺口，再补代码，避免把“未登记”误判为“未实现”。重点包括：

- 普通任务、特别订单与候选/结果的完整绑定；
- Joja、房屋升级等不可逆路径的隔离和授权；
- 钓鱼、矿井、骷髅洞、火山等长链随机环境；
- 工作台、远端库存、箱子、机器和建筑布局；
- 特殊、随机、条件性机器的剩余矩阵；
- 社交、剧情、收集和跨地图目标；
- 新存档到第三年 21 分目标的完整日循环。

#### 3.2.1 共享原生执行底座硬门

在继续扩展目标族前，先闭合所有动作共同依赖的执行底座。该硬门不增加模型自由度，只保证机械展开不会制造错误训练反馈：

- 行走使用持续移动租约，方向切换原子完成；快照、模型和外部编排等待不得造成掉键；
- 工具、武器、交互和菜单使用统一输入仲裁与原生动作生命周期；
- 每个动作只在原生动画和状态允许时进入下一阶段，禁止直接世界状态修改和人为延时伪装；
- 最近数秒的输入、位置、朝向、`UsingTool`、`CanMove`、动画、碰撞和原语状态进入有界环形缓冲，只在异常时落盘；
- 底座由确定性夹具验证，各目标族只组合并补充领域终态，不复制底层输入状态机。

五门证据回答“该候选是否能透明读取、生成、编译、执行和回写”，原生可见符合性回答“执行过程是否真的像原版玩家输入”。正式训练要求两者同时通过。服务器/后台运行不能替代本地可见短测：前者负责长周期逻辑、恢复、死锁和资源边界，后者负责动画、步态、按键和交互节奏。

退出条件：

- 每个目标族至少有一条真实闭环证据；
- 编译失败和运行失败可分类恢复，不产生无动作污染；
- 上游能排除已知不可能候选，不依赖下游反复阻塞；
- 不可逆动作必须经过显式策略授权。
- 共享执行底座契约测试通过，且生产执行路径不存在直接世界状态修改；
- 每个准入目标族同时具有领域终态证据和对应的原生可见符合性证据。

### 3.3 正式策略轨迹数据

数据必须分层保存：

- `policy`：模型级候选、选择、预算、结果与长期回报；
- `mixed`：用于回放分析但不能直接进入策略训练的数据；
- `calibration`：动作编译器和执行器校准数据。

每个策略样本至少记录：

- 决策时刻的版本化特征；
- 当时全部候选，而不只是最终选择；
- 每个候选的排除原因、时间预算和资源预算；
- 选择结果、实际耗时、状态增减和失败类型；
- 与日、季节、年度及第三年 21 分目标关联的长期回报；
- 字段字典、候选词表、编译器和执行器版本。

清洗要求：

- 删除旧 schema、模拟结果、已知 bug、重复样本和无动作污染；
- 按存档与游戏日切分训练/验证/测试集，禁止随机拆散同一轨迹；
- 保留负例和未选候选，避免只学习成功动作；
- 原始快照可归档压缩，训练数据使用流式、去重后的结构化表示。

退出条件：

- schema 审计通过；
- 数据来源和版本可追溯；
- 三类数据不会互相倒灌；
- 训练、验证、测试之间没有同轨迹泄漏；
- 数据集哈希和清洗报告进入检查点清单。

2026-08-01 工程进展：`policy_decision_trajectory.v1` 已建立强类型候选全集、选择、版本、
存档/日切分键、执行结果和长回报槽位，并通过源哈希一致性与准入选择校验。LiveTrainingLoop
现把每个有效决策的模型计划、完整排序响应、编译队列和源状态哈希绑定到实际 verified/fresh
执行；派发前重规划立即替换绑定，动作后重规划只对下一动作生效。同一决策的重复编译原语、
源哈希漂移、候选 ID 缺失和非准入选择均失败关闭。`StardewAI.PolicyDataset` 现执行严格
schema/版本/准入/结果校验、语义冲突去重、`SHA-256(save_id:year:season:day)` 的
80/10/10 确定性切分、逐文件 SHA-256 清单和拒绝报告。LiveTrainingLoop 在原生日期跨越时写入
日/季/年闭合观测，只在第三年首次评价边界且透明 `farm.grandpa_score` 可读时写入唯一 21 分
终点；回填只使用已闭合跨度，终点之后的决策不反向获得标签。当前标准生产路径尚无真实轨迹
文件，因此本节的工程实现已闭合，但“真实数据来源与清单进入检查点”退出条件仍需长期 rollout
产物验证。

### 3.4 正式模型提供器、检查点与评估

正式基线采用 C# 结构化排序模型。模型面对有限候选排序问题，不需要先用语言模型生成动作。优先评估 ML.NET 的 LightGBM/FastTree 排序或等价结构化模型，并保持：

- C# 特征契约、候选词表和推理接口为权威；
- 确定性规则继续负责硬约束；
- 模型只替换候选评分，不替换候选生成、编译和执行；
- 训练与推理检查点可以完整往返加载。

ML.NET 的 LightGBM/FastTree 排序器目前不支持直接导出 ONNX；若必须跨运行时部署，应选择可导出的替代任务形式，或把原生 ML.NET 模型作为正式检查点格式。

每个检查点清单必须绑定：

- 权重、超参数和随机种子；
- 特征 schema 与候选词表；
- 权威字典版本；
- 编译器与执行器版本；
- 数据集哈希和切分信息；
- 离线评估结果。

硬性评估门：

- schema 合法率 100%；
- 输出 allowlist 外候选为 0；
- 检查点往返推理结果一致；
- 无动作和过期 schema 不进入训练；
- 不可逆动作授权规则不可被模型绕过；
- 长运行无日志失控、存储失控或死锁。

## 4. 执行顺序

1. 实现证据注册表和生成式准入清单；
2. 回填已有真实运行证据，分离“未登记”和“未实现”；
3. 闭合持续移动、输入仲裁、原生动作生命周期和异常诊断组成的共享执行底座；
4. 按权威字典的真实缺口逐个闭合目标族，每个稳定纵向切片及时合并；
5. 用本地可见短测验证动画/按键，用后台或服务器长测验证逻辑/恢复，最后做全系统回归；
6. 重建并审计正式策略轨迹；
7. 接入 C# 结构化排序模型并完成检查点往返；
8. 做离线回放和独立存档验证；
9. 从新存档进行长期完整 rollout，目标为第三年爷爷评分 21 分；
10. 冻结“最强完美 AI”基线；
11. 再开发声音、节奏、失误容忍和玩家适应性，不得提前污染完美基线。

当前第 1、3、6、7 步的数据治理与结构化模型基础设施已经闭合，第 2、4、5 步仍按证据范围持续推进；现有十三项 allowlist 不能代表全量目标覆盖。直接下一步是继续按权威字典扩大第 4 步准入项。只有真实长期 rollout 产生轨迹和闭合跨度观测后，才运行第 6 步工具形成可供检查点引用的正式数据清单；不得用合成测试数据冒充正式训练集。

2026-08-09 EVD-226 已将 `farm.maintain_crops` 收敛为当前地点透明候选到五类类型化机械原语的唯一链路，并通过浇水、播种、普通收获、普通地块施肥、花盆施肥和巨型作物后台隔离验证。该项五门已闭合，但按 `CalibrationOnlyHighLevelIds` 仍为评估/执行器校准用途，不增加策略训练 allowlist。当前生成看板为 103 registered / 170 semantic / 98 compiler-bound / 67 runtime dispatch / 29 five-gate / 25 allowlist，KnowledgeCompiler 为 585/585、blocking 0。下一步仍是继续闭合未准入高层目标并采集真实长期 rollout，不是提前启动全量训练。

2026-08-09 EVD-228 已将 `fishing.catch_fish` 收敛为透明候选经 DailyPlan 到 `executor.catch_fish` 的唯一链路，并以普通海滩 3/3、鱼塘无 BobberBar 1/1、矿井 100 层 12/12（含两次岩浆鳗鱼）后台隔离运行闭合读、候选、编译、运行和输出五门。高层动作进入策略训练 allowlist；机械原语仅作执行器校准。运行时只发送原生等价输入，不改写鱼、绿条、进度、结果或背包；低技能/低装备下的真实失败继续作为阻塞反馈，上游必须结合实时技能、鱼竿、浮标和鱼难度评估。当前生成看板为 103 registered / 170 semantic / 99 compiler-bound / 67 runtime dispatch / 32 five-gate / 26 allowlist，KnowledgeCompiler 为 585/585、blocking 0。正式全量训练仍等待其余高层目标闭合和生产长 rollout，不得把本次有界钓鱼验证外推为所有传奇鱼、宝箱优化或模组覆盖。

## 5. 新训练笔记本与模型路线

目标训练节点为用户报告的新笔记本：

- CPU：AMD Ryzen 9 9955HX，16 核 32 线程；
- 内存：32 GB；
- GPU：GeForce RTX 5070 Laptop GPU，8 GB GDDR7；
- 状态：配置来自用户报告，尚未在目标机器上完成驱动、功耗、散热和存储验收。

### 5.1 资源判断

- CPU 适合结构化数据预处理、回放、树模型训练、压缩和多环境评估；
- 32 GB 内存足够当前结构化路线，但必须使用流式数据、限制并行环境和避免同时常驻完整快照；
- 8 GB 显存是本地神经模型训练的硬边界，不能按桌面版 RTX 5070 或更大显存估算；
- 机器型号的 GPU TGP、散热和内存扩展上限需以整机厂商规格和实测为准。

存储建议：

- 结构化训练最低预留 150 GB 快速 SSD 空间；
- 保留长期原始快照和多轮 rollout 时建议 300 GB 以上；
- 冷数据归档与训练热数据分离，按哈希去重和压缩；
- 不再把 500 GB 写成所有训练路线的统一最低门槛。

### 5.2 模型分级

1. **V0 确定性基线**：现有规则和执行链，作为回归参照；
2. **V1 正式结构化排序器**：全量训练的首个必需模型，优先在 C#/ML.NET 内完成；
3. **V2 可选 0.6B 级受约束模型**：只输出候选 ID 和参数，用 4-bit QLoRA、短上下文、batch 1、梯度累积和检查点技术做比较实验；
4. **1.7B 级模型**：8 GB 显存上的边界实验，必须先通过显存烟测，不作为默认正式路线；
5. **3B 及以上训练**：不作为该笔记本的本地目标，应使用更大显存设备或远端资源。

可选神经模型不得直接读取未经裁剪的全量快照文本，也不得拥有动作执行权。其输出仍经过 C# schema、候选 allowlist、编译器和执行器。

### 5.3 实机验收门

开始正式训练前在目标笔记本完成：

- `nvidia-smi` 正确识别 RTX 5070 Laptop GPU 和 8 GB 显存；
- 驱动与选定 CUDA/训练工具链版本锁定；
- 接通电源并启用稳定性能模式；
- 记录持续负载下的显存峰值、内存峰值、温度、功耗和吞吐；
- 验证至少 150 GB 可用快速存储及数据落盘速度；
- C# 结构化模型完成训练、保存、加载和推理烟测；
- 若启用 QLoRA，先完成 0.6B 级最小批次显存烟测，失败即回退，不挤占正式 V1 路线。

## 6. 权威参考

- [AMD Ryzen 9 9955HX 官方规格](https://www.amd.com/en/products/processors/laptop/ryzen/9000-series/amd-ryzen-9-9955hx.html)
- [NVIDIA GeForce RTX 50 系列笔记本 GPU 官方规格](https://www.nvidia.com/en-gb/geforce/laptops/50-series/)
- [NVIDIA RTX 50 系列发布规格](https://www.nvidia.com/en-us/geforce/news/rtx-50-series-graphics-cards-gpu-laptop-announcements/)
- [Microsoft ML.NET 算法选择说明](https://learn.microsoft.com/zh-cn/dotnet/machine-learning/how-to-choose-an-ml-net-algorithm)
- [Hugging Face bitsandbytes 安装与平台支持](https://huggingface.co/docs/bitsandbytes/installation)
- [Hugging Face Transformers 4-bit/QLoRA 说明](https://huggingface.co/docs/transformers/main/quantization/bitsandbytes)
- [Qwen3 官方仓库](https://github.com/QwenLM/Qwen3)

## 7. 2026-08-02 实现状态与下一门

首个正式 C# 模型提供器已经实现为
`return_weighted_pairwise_linear_ranker.v1`。它消费
`policy_decision_trajectory.v2` / `policy_features.v2`，使用同一套投影代码完成采集和推理，
保留完整源候选字段，并只在生成式 allowlist 内对当前可用候选排序。候选生成、上游排除、时间与
资源许可、日计划、动作编译和原生执行仍是确定性权威。

检查点 `structured_policy_checkpoint.v1` 必须绑定正式 manifest、cleaned/train/validation/test
SHA-256、超参数、特征 schema、候选/能力词表、权威字典、编译器和执行器版本。训练器会重新校验
每个分区的哈希、行数、确定性 split 与轨迹 schema；推理会拒绝过期或损坏的检查点。Backend 的
结构化训练端点和现有 rank-options 单路径重排已接通；LiveTrainingLoop 可用
`--policy-checkpoint-path` 和 `--require-structured-policy` 显式启用，缺失时不得静默回退。

这仍未解除正式全量训练阻塞：标准 E 盘生产轨迹、跨度观测、manifest 和 checkpoint 均不存在，
当前训练 allowlist 也只有十六个有界范围。2026-08-04 EVD-213 已把当前加载地图中精确、已完成、
非孵化器的原版机器产物收取通过既有单链纳入五门准入；投料、制作、摆放、搬迁、存储和孵化器
流程仍留在 `farm.process_machines` 校准范围。2026-08-03 EVD-212 已把精确原版绿雨灌木索引 44/46
通过既有单链纳入五门准入；确定性核心掉落、采集经验和任务收取已验证，秘密纸条仍按概率边界与
执行后观测处理。下一门仍是扩大五门准入并采集真实长期 v2 rollout，不是
继续用合成数据调模型。形成正式 manifest 后执行 `StardewAI.PolicyModel`，再通过独立存档评测和
第三年 21 分长跑；未通过前不得冻结完美策略或开始拟人适配。
## 历史记录 · 2026-08-04 EVD-214 更新

训练白名单现为 17 个有界范围。`farm.load_supported_machine_input` 仅在当前地图的精确已摆放机器支持
意图下准入，并要求实时确定性正净值、零附加耗材、精确玩家槽数量未被其他目标预留，以及账本、
预测和路线在编译/派发时没有漂移。隐藏静默 E 盘运行已完成原生投料、处理开始、训练行写入和意图
完成对账。该结果没有解除正式全量训练阻塞：Product Executor 仍为 0，生产轨迹、跨度观测、正式
manifest/checkpoint 和第三年 21 分独立评估仍未完成；广义机器策略和完整制作-摆放-投料生命周期也
仍在准入范围之外。

## 历史记录 · 2026-08-04 机器容量生命周期编排更新

`farm.establish_supported_machine_capacity` 已登记并接入单一滚动编排链。它只服务
`goal.economy.earn_money` 的有界正收益容量缺口，并按持久 `MachineSupportIntent` 在制作、精确摆放、
首次投料三个既有执行分支之间推进；每个新快照最多选择一个当前阶段，摆放执行失败时保持原目标重试，
无效意图则失败关闭。阶段测试已证明它不会在非赚钱目标下排名，也不会复制执行器。

EVD-215 已通过隐藏静默隔离运行 `runtime-supported-machine-capacity-20260804-120211`。同一高层选项
连续驱动原生制作、规划器精确摆放、确定性首次投料、处理开始、意图完成和三条训练记录；五门闭合数
现为 19，allowlist 为 18。该准入只覆盖当时地图、有界正收益、零附加耗材且无预留冲突的单机器容量
生命周期，不覆盖任务/收集需求、远程摆放、随机机器或广义 `farm.process_machines`。

正式全量训练仍未准入：Product Executor 仍为 0，生产长 rollout、闭合跨度观测、正式 manifest、
生产 checkpoint、独立存档评测和第三年 21 分长跑尚未完成。下一开发切片是任务/收集需求机器处理，
之后继续按权威字典扩大五门范围并采集真实长期轨迹。

## 历史记录 · 2026-08-11 每日委托接受链更新

`quest.accept_daily` 与 `executor.accept_daily_quest` 已注册并接通透明桥、上游候选排除、滚动跨地图接近、
DailyPlan、动作队列和原生 Billboard 点击。隔离 E 盘运行已验证同一原生 offer 进入任务日志且保留两天期限；
安装后的真实 full 快照为 required 103、blocking 0。两项仍为 RegisteredOnly，五门闭合数和训练白名单仍分别为
39 与 26；需要重复跨日、无任务、过期及联机归属证据后才能改变准入状态。该闭环不解除正式全量训练阻塞。

## 历史记录 · 2026-08-11 特别订单接受链更新

`quest.accept_special_order` 与 `executor.accept_special_order` 已按单一实现覆盖 Town、Qi 和沙漠节庆三种原生入口，
接通实时左右 offer、上游许可排除、滚动接近、原生开板/对话以及精确选择。Town 隐藏静默隔离运行已验证原生
互斥锁延迟和 key、generation seed、fingerprint、accepted type 的一致回执；安装后的真实 full 快照为
required 104、blocking 0。Qi 与沙漠节庆只有锁定版本反编译和结构覆盖，必须分别完成运行校准后才能形成对应证据。

动作对账现为 111 registered / 176 semantic / 110 compiler-bound；five-gate 仍为 39，训练白名单仍为 26。
特别订单高层项和原语继续保持 RegisteredOnly，一次 Town 通过不构成正式训练准入，也不解除 Product Executor、
长期 rollout、独立存档评测和第三年 21 分长跑等全量训练阻塞项。

## 历史记录 · 2026-08-27 彩虹尽头奖励准入（EVD-268）

`rewards.claim_pot_of_gold` 已按锁定 1.6.15 原生规则闭合五门并进入训练白名单。模型不输出坐标、站位、数量或领取细节；编译器从最新 `current_location.pot_of_gold_reward` 绑定 Forest、`52,98`、可用相邻格、春 17 日、`min(100, 7 + year)` 金币和帽子契约。隐藏静音满背包运行验证原生 `GameLocation.checkAction` 产生第二年 9 个金币 debris 与 1 顶帽子，随后由既有 debris 拾取链处理，不建立第二套奖励转移系统。

最新 full 快照为 `128 required / 112 readable / 16 contextual / 0 blocking`；权威对账为 `143 registered / 199 semantic / 142 compiler-bound / 69 five-gate / 37 allowlist / 56 catalogued blocked / 0 Product Executor`。该单项准入不解除正式全量训练对 Product Executor、生产长 rollout、冻结正式数据 manifest、独立存档评测和第三年 21 分长跑的总体阻塞。

## 历史记录 · 2026-08-27 矮人王雕像每日能力准入（EVD-269）

`mining.choose_dwarf_statue_power` 已按锁定 1.6.15 原生规则闭合五门并进入训练白名单。透明桥实时发布采矿精通门、当前地图全部精确基础雕像、可达站位、由 `DaysPlayed*77 + uniqueID` 生成的两个不同选项、五种原生效果分支和已有 buff 锁。模型必须选择一个当天真实提供的 `power_id`；编译器拒绝缺失或伪造的 ID，并从新快照重绑菜单索引、buff、雕像和站位。

隐藏静音 E 盘运行对当天两个选项 `0/3` 分别验证了原生 `Object.checkForAction -> ChooseFromIconsMenu.receiveLeftClick`、唯一选中 buff 和菜单关闭回执。最新 full 快照为 `129 required / 113 readable / 16 contextual / 0 blocking`；权威对账为 `144 registered / 199 semantic / 143 compiler-bound / 70 five-gate / 38 allowlist / 55 catalogued blocked / 0 Product Executor`。该单项准入不解除正式全量训练对 Product Executor、生产长 rollout、冻结正式数据 manifest、独立存档评测和第三年 21 分长跑的总体阻塞。

## 历史记录 · 2026-08-27 祝福雕像每日奖励准入（EVD-270）

`rewards.claim_statue_blessing` 已按锁定 1.6.15 原生规则闭合五门并进入训练白名单。它是无参数领取目标，不是策略选择菜单：透明桥预测当天唯一祝福，并发布农业精通、日锁、天气/节日分母、七种效果、当前地图精确基础雕像和相邻站位；编译器覆盖模型伪造的所有机械字段。

## 历史记录 · 2026-08-27 House Plant 原生轮转准入（EVD-271）

`world.rotate_house_plant` 已按锁定 1.6.15 原生规则闭合五门。它是显式装饰目标，不是自主维护候选：模型选择当前地图中的一盆精确基础 House Plant，编译器从最新 `current_location.objects[]` 重绑永久物品身份、当前/预期 `ParentSheetIndex`、相邻站位、真正空工具栏槽位、恢复槽位和原生契约。工具槽不能替代空槽，因为起始帧 7 在空手 `GameLocation.checkAction` 下会触发地点层第二次对象调用，一次交互的真实结果为 `7→1`。

生产执行只复用共享 BFS 和一次地点级原生交互，不直接写贴图帧或调用对象级方法。透明桥、候选和运行时同时防守原生四向不可通行对象包围时会触发的 `performToolAction(null)` 破坏性前导分支。隐藏静音 E 盘矩阵对 0..7 全部通过，并验证永久 ID 与槽位不变。该项进入证据白名单但 `AutonomousCandidateEnabled=false`，普通日计划不会擅自改变玩家装饰。当前对账为 `146 registered / 199 semantic / 145 compiler-bound / 72 five-gate / 40 allowlist / 53 catalogued blocked / 0 Product Executor`；下一切片是 `farming.collect_slime_ball`。

生产执行只复用共享 BFS 和原生 `GameLocation.checkAction`，不直接施加 buff 或写日锁。隐藏静音 E 盘运行验证当天 `statue_of_blessings_1` 唯一回执和 `hasBeenBlessedByStatueToday=true`。最新 full 快照为 `130 required / 114 readable / 16 contextual / 0 blocking`；权威对账为 `145 registered / 199 semantic / 144 compiler-bound / 71 five-gate / 39 allowlist / 54 catalogued blocked / 0 Product Executor`。该单项准入仍不解除 Product Executor、生产长 rollout、正式 manifest/checkpoint、独立存档评测和第三年 21 分长跑阻塞。

## 8. 2026-09-05 r32 原生存档事务准入结果

`train.server.20260905.r32.plan07` 是首个同时满足真实 Product 动作、原生跨日、原生存档完成、训练事务提交和 canonical 制品更新的 r32 有界轮次。发布固定为 `formal-r32-af432ed-20260905`，并发为 1；运行从 Summer 2 推进到 Summer 3，存档树 SHA-256 从 `7822c135afa09a355fbed3ce1462784d1551fdf8cfdf81ae4efebd95fcba31a3` 变为 `a4af6a79e6138085b07e7c63c7977fdc1c12e1bf34df28955c6a7614816af27b`。睡眠回执包含 `sleep_yes_confirmed`、`new_day_observed`、`native_save_committed`、`post_sleep_menu_closed`、`native_new_day_world_stable` 和 `post_sleep_dialogue_advanced_natively`。

事务最终状态为 `committed_after_native_save_boundary`。正式数据集 accepted 200 / rejected 0，train / validation / test = 142 / 5 / 53，train pairs = 4367；canonical checkpoint SHA-256 为 `4f937ec73f2a0f58bdac00ff9345fd4fbcc201010d627b53939a132357a2181f`，dataset manifest SHA-256 为 `bfbeb9e5f943726a9e830e31bb6926724566e74e42505f1613159cef706d7a16`。运行、事务副本、canonical 数据、执行器诊断、精确存档和四项控制文件已经归档，本机与远端 146 个文件逐一校验一致。

round05 和 round06 分别因保存完成判定过早、隔夜地震系统对话未推进而失败，二者均保持 `staged_not_committed`，不能并入成功训练证据。现有结果证明单轮正式事务可以可靠提交，但不证明策略已经完成全量训练，也不覆盖连续多日、跨季、跨年、第三年 21 分和 Companion 人类适应层。

下一准入阶段固定为可恢复的连续有界批次：从 Summer 3 的已提交存档和 canonical 哈希开始，每批只允许一个训练实例；达到计划日数或 attempt 上限即退出。成功退出必须同时满足主动作均有 fresh/verified Product 回执、无未解决 `*.pending.json`、原生存档哈希发生预期变化、事务已提交、canonical dataset/checkpoint/manifest 相互一致、全部制品归档并通过远端到本机 SHA-256 核对。任一门失败则保留 staged 诊断且不得推进基线。

## 9. 2026-09-05 r33 连续事务准入结果

round08 首次用 round07 的 canonical 产物准备下一轮时，被 `formal_checkpoint_dataset_binding_mismatch` 在动作执行前拒绝。动态证据证明提升逻辑只复制文件，却没有把 manifest/checkpoint 内的 staging 绝对路径改为 canonical 路径。该失败没有启动 Product 执行与训练循环，canonical 未被该轮推进。

`7b1da8d` 将重绑定纳入事务提交本身：manifest 的 input、horizon、cleaned 和三分区路径必须位于受控根内并统一重绑定；canonical manifest SHA-256、checkpoint dataset binding、checkpoint ID 和最终报告身份一起更新。回归为 Core 2274/2274、Backend 171/171、Release 0 warnings / 0 errors。历史 canonical 经同一版本的 PolicyDataset/PolicyModel 工具重建，样本数、切分、pairs 与模型指标均未漂移，6 个 digest 全部通过。

round09 随后证明下一轮 prepare 与提交均成立：3 个高层候选轨迹全部 fresh/success；原生睡眠控制步不调用策略模型；Summer 3 → Summer 4 且存档树 SHA-256 改变；事务为 `committed_after_native_save_boundary`。最终为 accepted 203 / rejected 0、145 / 5 / 53、4547 pairs，checkpoint / manifest SHA-256 为 `4247b9feed96fbb40fbe263dd6f260c006d8f2db96c28b4a21bc0b5ffc717eeb` / `b35080e21a10ae61109df1577a4e6534fa2e60150917e3d75671d8d8734c45d5`，未解决 Product pending 为 0。

该结果把“单轮可提交”提升为“提交结果可直接供下一轮使用”，允许进入连续有界批次；仍不等于跨季/跨年/Grandpa 21 全量长训通过。下一批必须继续维持并发 1、静音后台、失败不提升、原生保存、canonical 内部绑定验证和本机逐文件归档。

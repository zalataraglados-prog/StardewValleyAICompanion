# StardewAI 完全体完成路线图

## 计划权威与当前执行顺序（2026-08-01 修正）

本文件是唯一的人类可读总计划。`docs/CURRENT_WORK_CN.md` 只记录当前切片；
`action-progress-dashboard.json`、`action-implementation-reconciliation.json`、
`native-action-surface-inventory.json`、`native-action-branch-inventory.json`、
`native-map-interaction-coverage.json` 和 `semantic-action-catalog.json` 是机器生成
状态。旧 handoff、测试报告和 GitHub issue 只保留历史或验收信息，不得覆盖这些事实源。

当前必须先完成动作全集对账，再继续逐动作纵向闭环：

1. 从锁定的 1.6.15 反编译源枚举工具、物品使用、地图交互、菜单、事件、任务、节日、
   小游戏、多人和可选内容的原生动作表面；
2. 将原生表面归并为稳定的语义动作全集，未实现项也必须注册为显式 blocked，冻结
   `registered/total` 的真实分母；
3. 将全部现有 Candidate、Compiler、Harness/Product Executor、Verifier 和 E3 证据
   归属到一个语义动作及一个主执行引擎，禁止孤儿代码和第二套机械执行循环；
4. 之后才按优先级逐项完成
   `read -> candidate -> compile -> product runtime -> before/after verifier -> E3 -> training record`。

Issue #85（Harness Handler 状态所有权）和 #86（`TrainingExecutionRequest.v2`）均为
触及领域时的渐进迁移，不是动作注册和执行器开发的前置项目。不得因架构整理暂停动作
覆盖主线；相同机械机制第二次出现时检查复用边界，第三次出现前必须抽取共享引擎。

动作数字只允许由机器看板生成。历史文档中的 89、95、96、190 或 210 不得再单独作为
完成度口径；当前 97 是注册数，不是全游戏动作总数。

当前阶段退出条件：

- 锁定反编译版本的原生动作表面零未分类；
- 所有宽入口均有按完整方法签名生成的分支证据，缺失和待语义审查均为零；
- 所有有效地图 Action/TouchAction token 均映射到证实的原生分支和语义动作，或有
  原生默认返回证据证明它是失效/遗留 token；
- 语义动作全集分母冻结，所有动作均已注册（允许显式 blocked）；
- 每个注册动作恰好归属一个主执行引擎，编译和运行 ID 零孤儿；
- 看板同时报告 `registered/total`、`five_gate_closed/total`、
  `product_executable/total` 和 `E3/total`，不再混用不同阶段数字；
- 完成以上条件前，不开始正式训练，也不以 Harness 成功宣称 Product Executor 完成。

当前锁定扫描切片的机器基线为：320 个原生输入表面、60 个宽入口、428 条分支证据、
150 个地图交互 token、165 个语义动作（97 个已有 `OptionSpec`，68 个显式 blocked）。
源表面、分支、地图 token 和语义注册均为零未分类/零缺注册，状态为
`native_action_denominator_frozen`。冻结指纹只包含原生身份、映射身份和语义 ID，不包含
注册/覆盖进度；因此实现推进不会改变动作分母。这表示此前实现没有作废，而是获得
了可审计分母；它不表示 165 个动作已经具备 Product Executor 或五门证据。

## 目标架构

最终目标是一个星露谷 AI 陪玩系统：透明读取全游戏状态，小模型只输出高层目标和策略方向，动作编译器负责机械展开、路径/时间/资源校验，执行器在训练单人或未来联机陪玩角色上执行，不抢玩家键鼠。

## 行为分类

1. 机械动作
   - 例：浇水、收机器、回家睡觉。
   - 小模型只发 option，动作编译器全权展开步骤。
   - 训练角色：executor_calibration。

2. 参数化机械动作
   - 例：去矿洞第 N 层、去某地点钓鱼。
   - 小模型给目标参数，动作编译器做路径、风险、时间、资源校验并生成动作队列。
   - 训练角色：mixed，策略层只学选择目标，不学低层操作。

3. 策略动作
   - 例：爷爷四蜡烛目标方向、采购、社交、全出货、社区中心、技能成长。
   - 小模型输出策略方向和约束。
   - 动作编译器校验时间、预算、地点、前置条件，必要时拆成可执行子任务。
   - 训练角色：strategy_value。

## 完全体切片

### A. 透明读取闭环
- 保持 TransparentBridge 实时读取，不依赖文档举例。
- 所有目标评估只能使用透明字段或显式 unavailable。
- 缺失字段分级：评分事实缺失会阻塞策略训练；展示/重评上下文字段缺失只记录，不阻塞方向训练。

### B. 策略出口层
- `strategy.grandpa_progress` 是长期目标 option。
- 输出层增加 `direction_id`，候选包括：
  - `complete_community_center`
  - `raise_friendships`
  - `complete_full_shipment`
  - `raise_skill_levels`
  - `marriage_and_house_upgrade`
  - `complete_master_angler`
  - `complete_museum_collection`
  - `obtain_rusty_key`
  - `obtain_skull_key`
  - `earn_money`
  - `earn_pet_love`
- Joja 发展保留为完整游戏路线，不进入爷爷 21 分候选，因为原生 `Utility.getGrandpaScore()` 没有该得分项。
- 每个方向必须带：domain、potential_points、priority_score、feedback_key、required_minutes、hard_preconditions。
- `unlock community center` 只能作为前置条件，不作为爷爷得分方向。
- 社区中心和 Joja 路线互斥，完成任一路线都能满足对应进度收益，但不能同时推荐两个互斥方向。
- 博物馆/锈钥匙、社交/婚房、技能/钓鱼允许依赖重叠，但不得重复累计同一评分收益。
- 金钱、技能、好感人数必须按下一档阈值建模，优先推荐最小可达下一分的方向。

### C. 动作编译器
- 机械 option 必须生成 `CompiledActionStep[]`。
- 策略 option 必须生成 `StrategyPlanStep[]` 或等价结构：
  - direction selection
  - hard preconditions
  - estimated required/optional minutes
  - resource budget
  - executor handoff option
- 所有计划必须过 TimeBudgetValidator。

### D. 执行器
- 训练单人模式用 RuntimeTestHarness。
- 联机陪玩模式未来用 companion actor，不抢玩家键鼠。
- 执行失败不进入策略惩罚，进入 executor_calibration。

### D1. 共享原生执行底座与双层验证门（当前优先级）

各目标族不得各自复制一套移动、按键、工具或动画轮询。普通矿井、骷髅洞、采石场矿洞、火山、农场、商店和社交等切片必须共同使用以下执行底座：

- **持续移动租约**：普通行走期间持续持有唯一方向输入；转向时原子切换方向，不插入无意义的全松开帧，也不允许对向键同时按下。只有原生工具/武器动画锁、菜单、地图连接器、碰撞重规划、明确的安全中断或玩家接管可以暂停或释放移动。
- **原生动作生命周期**：工具、武器和交互动作必须按“按下 -> 原生状态开始 -> 在原生允许时释放 -> 等待原生动画和状态结束 -> 校验结果”执行。不得通过直接删除对象、直接改生命/地形/位置或用任意墙钟延时伪造原生过程。
- **统一输入仲裁**：移动、工具、武器、菜单和玩家输入必须有唯一所有者及可审计的抢占原因；快照刷新、模型等待和外部编排间隙不得导致执行中的机械动作掉键。
- **低开销诊断环形缓冲**：只在内存保留最近数秒的按键、像素/格坐标、朝向、`UsingTool`、`CanMove`、动画阶段、碰撞/重规划和当前原语；正常运行只写摘要，异常或人工触发时才落盘，禁止恢复逐 tick 的无界日志。
- **确定性夹具与回放**：共享底座先在隔离存档和固定入口验证，再被各目标族复用；目标族不得用自己的一次成功运行替代底座契约测试。

验证职责严格分开：

- 本地可见运行是步行连续性、按键生命周期、原生动画、工具节奏和菜单交互的权威证据。
- 后台/服务器长运行只证明状态变化、日循环、目标推进、恢复、无死锁及日志/存储边界；无渲染服务器不得声称动画或视觉节奏正确。
- 联机观察只作为抽查，不作为持续跟踪的主要证据。
- 权威字典负责语义、前置条件和能力覆盖，不负责证明输入帧序列或动画符合原版；两类证据缺一不可。

每个执行器纵向切片除 `read/candidate/compile/runtime/output` 五门外，还必须交付：前置条件、动作生命周期与时间、打断/恢复、终态验证、原生可见符合性。共享底座未通过时，策略训练保持阻塞，执行器故障不得写入 `strategy_value`。

退出条件：

- 普通行走不会因快照、模型或外部编排间隙无故释放方向输入，转向无对向键冲突；
- 每次清障挥击对应且只对应一个完整原生输入/动画周期，高级工具一次击碎也必须看见完整原生挥击；
- 所有直接世界状态修改的生产执行路径被移除或明确限制为隔离夹具，且不会进入训练证据；
- 环形诊断能在异常时还原输入与原生状态因果，同时正常长运行日志量有界；
- 本地可见短测与服务器长测分别通过各自负责的门，最终才允许全系统回归。

### E. 训练闭环
- mechanical rows：calibration_only，排除 policy。
- strategy rows：policy_ranker，纳入 baseline/后续小模型训练。
- 爷爷目标用透明评分方向生成正样本。
- 随机地图类任务必须用完美执行器，避免策略层把低层危险误判成目标不可取。

### E1. 正式全量训练准入（当前阶段）

- 正式训练只覆盖通过 `read/candidate/compile/runtime/output` 五门的模型级候选；注册名称、静态编译或单次烟测都不能单独构成训练资格。
- 当前闭环能写训练记录，但现有基线聚合器不是真实学习模型，专用服务器的五类候选和 `--skip-training` 也不等于全量训练。
- 机械动作、路径、战斗微操和工具输入继续作为执行器校准，不进入策略模型自由输出。
- 正式首选模型是 C# 结构化候选排序器；0.6B 级受约束神经模型仅作为新笔记本上的可选对照。
- 完整工程包、退出条件、硬件边界和执行顺序以 [`FORMAL_FULL_TRAINING_READINESS_CN.md`](FORMAL_FULL_TRAINING_READINESS_CN.md) 为当前事实源。

### F. 演示验收
- 可展示：
  - 当前透明读数
  - 小模型输出
  - 编译后队列
  - 时间校验
  - 训练行分流
  - policy 排序
- 完整演示最低要求：
  - 读出爷爷评分当前分与缺口。
  - 选择一个策略方向。
  - 编译出可校验计划。
  - 写入 policy 样本。
  - policy 排名反映该方向。

### G. 最强完美策略训练与基线冻结
- 先使用完全透明状态、完整输出记录和机械层完美执行器训练并评测最强策略。
- 冻结最优 checkpoint、特征 schema、option 词表、编译器/执行器版本及评测语料，形成可复现的“完美策略基线”。
- 执行器校准失败不得污染策略奖励；随机地图或低层操作失败不得让策略层错误降低对应目标欲望。
- 冻结后的完美策略必须始终可以单独选择、回归和对照评测。

### H. 完美训练后的人类适应性改造（细节待定）
- 本阶段是完全体陪玩上线前的必经阶段，但具体体验参数由用户后续决定；当前只固定架构边界和退出条件。
- 待设计事项包括：合作分工、主动性、节奏与打断偏好、玩家资源和目标保留、有限的人类式非最优行为、交流/人格/声音、玩家习惯学习与隐私边界。
- 拟人适配必须作为可配置的策略输出包装层、独立 profile 或独立 checkpoint 实现，不得覆盖、重标或污染完美策略训练数据。
- 透明读取、合法性、安全、动作校验、结果记录和执行器正确性不得为了“像人”而降低。
- 最优性、拟人度、帮助程度和干扰程度必须使用互相分离的指标与数据评测。
- 联机陪玩适配不得抢玩家键鼠焦点，不得擅自占用玩家保留资源或目标。

退出条件：
- 完美策略基线仍可复现并通过原始 benchmark。
- 所有适配 profile 均版本化、可关闭、可回滚、可独立评测。
- 人类试玩证明适配后的陪玩有帮助且不干扰玩家，同时透明与执行器不变量仍全部成立。
- 用户尚未决定的适应性细节全部形成明确规格后，本阶段才允许标记完成。

### I. 长期承诺账本与陪玩接口约束

- 增加跨日、跨季、可修订的类型化策略承诺账本；种植面积、播种日、首收、再收波次、机器建设最晚点、建筑扩建和资源预留不能只存在于单日日计划。
- 当前机器时间窗只允许消费实时存货、原生机器周期和实时作物首收；在承诺账本完成前，不得把持有种子猜成第二年确定种植队列。
- 联机加入模式、独立角色输入、人格/熟练度/知识轴、可插拔引擎、扩展 MOD 接口和作弊测试 profile 服从 `docs/FUTURE_COMPANION_ARCHITECTURE_CN.md`。
- 训练记录必须能区分完美策略、拟人适配、执行校准、玩家打断和多人资源竞争。

## 当前下一步

训练证据注册表和由证据生成的 allowlist 已于 2026-07-28 完成首版，权威字典的当前锁定版本为 `game-1.6.15-20260723T093543Z-linux-v24`。火山 0 到 Caldera 的隔离运行已经证明一条状态闭环，但不等于移动连续性、工具动画和全部生成种子均已通过：

1. `capability_registry.v2` 独立记录五门、证据 ID、证据范围和类型化排除原因；
2. 空 allowlist 会使注册表初始化失败；
3. 该阶段准入项为 EVD-095 范围内的 `mining.reach_depth`、EVD-106 普通矿井 119 -> 120 层范围内的 `mining.obtain_skull_key`、EVD-192 明确意图范围内的 `inventory.transfer_item`、EVD-076/EVD-105 当前已加载原版 NPC 对话范围内的 `social.talk_npc`，以及 EVD-196 当前已加载原版 NPC 滚动追踪和普通单件送礼范围内的 `social.gift_npc`；
4. 执行器原语和校准型高层 option 不进入策略训练；
5. 字典副本与锁文件只能证明语义来源和版本，不能代替原生可见执行证据。

2026-07-31 已完成 D1 的首个工程切片：“持续移动 + 通用清障/农场工具/火山石头共享原生工具周期 + 共享转向路径游标 + 600 帧异常环形诊断”。通用清障生产路径不再直接 `performToolAction` 或移除对象；第一次后台回归以 11/11 步进入 Caldera。解锁后的可见回归先后暴露“火山领域复制的简化路径推进器未继承转弯中心修复”和“外层超时未触发诊断”两个缺口；收敛火山冷却/清障到共享路径推进器并补齐异常落盘后，最终可见 level 9 回归以 14/14 步进入 Caldera，其中含 4 次石头清障、3 次移动、3 次熔岩冷却、3 次等待和 1 次出口穿越，全部 after snapshot fresh 且 state hash 改变。

2026-08-01 首个分母冻结后的切片 `inventory.transfer_item` 已完成强类型明确意图、透明库存图投影、候选、路径站位、日计划和既有 `executor.transfer_material` 机械原语复用；没有新增第二套箱子运行时。EVD-192 在隐藏、静音的 E 盘隔离存档中完成双向原生运行。随后完成 current live snapshot schema 工作流，新实时快照覆盖 94/94 required factors，完整 KnowledgeCompiler 达到 585/585 exports、blocking 0。复核确认 `recovery.stabilize_day` 和普通社交原生执行链早已存在，禁止复制第二套。EVD-195/EVD-196 闭合恢复睡眠与当前原版 NPC 社交窄范围，训练准入增至 4 项。EVD-197 统一旧聚合训练器与生成式 allowlist，但它不是正式模型。EVD-198/EVD-199 建立强类型策略轨迹并把初始、派发前和动作后决策绑定到各自有效制品。EVD-200 现完成严格清洗、语义冲突去重、按存档/日的确定性 80/10/10 切分、逐文件 SHA-256 清单、不可变版本锁，以及由真实日期边界和第三年可读评分驱动的长回报回填；未闭合跨度保持 `null/pending`。E 盘标准生产路径当前没有真实策略轨迹，因此不能声称已有正式训练集。下一步接入 C# 结构化排序器和检查点往返，同时继续扩大五门准入覆盖；真实长期 rollout 后再生成并冻结正式数据清单。短训只能做基础设施烟测，不得替代正式全量训练。

架构防扩张门同步生效：`ModEntry` 不再新增领域 active state，新领域必须由独立 Handler 持有状态与 cleanup；含 304 个 public instance 属性、横跨 8 个 partial 声明文件的 `TrainingExecutionRequest.v1` 停止新增字段。v2 采用强类型 payload envelope，并按领域逐族兼容迁移，禁止一次性重写现有 compiler/runtime/verifier 链。

2026-08-02 EVD-201 已闭合第 7 步的首个可执行版本：C# 返回加权成对线性排序器、完整候选字段与
状态特征投影、逐分区哈希/版本绑定检查点、原子保存/严格加载，以及 Backend/LiveTrainingLoop
单路径推理接入均已完成。该 V1 是透明、确定、可审计的行为克隆基线，不是最终最优策略声明；
模型不能生成候选、绕过硬约束或直接控制执行器。标准 E 盘没有真实 v2 轨迹、闭合跨度、正式
manifest 或生产 checkpoint，现阶段禁止把合成训练测试称为正式训练。

2026-08-02 EVD-202 又将已有 EVD-106 的普通矿井 119 -> 120 层骷髅钥匙原生宝箱领取和退出证据
登记到五门准入，未新建候选、编译器或运行时。当前五门闭环为 6，训练白名单为 5：
`inventory.transfer_item`、`mining.obtain_skull_key`、`mining.reach_depth`、
`social.gift_npc`、`social.talk_npc`。普通矿井、沙漠矿洞、采石场矿洞金镰刀和火山矿洞继续
保持独立边界；一族证据不得为另一族放行。Product Executor 仍为 0，正式生产轨迹仍为空。

2026-08-02 EVD-203 再将 `volcano.reach_caldera` 独立登记为第六项训练准入。EVD-190/EVD-191
证明现有透明火山读取、滚动候选、编译器、原生机械动作、目的化战斗、安全重规划和 Caldera 终态；
它没有复制矿井系统，也不把火山证据外推给普通矿井、沙漠矿洞或采石场矿洞。模型输出仍止于
高层目标，逐帧动作保持确定性。当前五门闭环为 7、训练白名单为 6、Product Executor 仍为 0。

2026-08-02 EVD-204 登记 `skills.read_books` 的全六类原版基础分支。治理修正让
`OptionImplementationCatalog` 与 `OptionGovernanceCatalog` 共同识别既有 DailyPlan option 展开，
而不是伪造高层 ActionQueue 直接编译器；唯一执行路径仍是 `read_inventory_book ->
executor.read_book -> wait_ticks`。当前 compiler-bound 为 77、五门闭环为 8、训练白名单为 7，
Product Executor 仍为 0。

2026-08-02 EVD-205 又登记 `foraging.harvest_ginger` 的原版当前地图精确姜收获范围。它复用唯一
`harvest_ginger -> executor.harvest_ginger` 链；EVD-119 覆盖干燥普通锄、雨天 Efficient 且背包满后
落为 debris，以及体力不足上游排除。自定义 Hoe/Crop/HoeDirt、任意采集和灌木不在该准入范围。
当前 compiler-bound 为 78、五门闭环为 9、训练白名单为 8，Product Executor 仍为 0。

2026-08-02 EVD-206 登记 `foraging.harvest_bushes` 的原版当前地图精确 Bush 范围。它复用唯一
`harvest_bush -> executor.harvest_bush` 链；EVD-120 覆盖普通浆果、Botanist 浆果、茶叶、金核桃，
以及已领取金核桃和摇动冷却的上游排除。自定义 Bush、town bush 特殊交互和其他采集族不在准入内。
当前 compiler-bound 为 79、五门闭环为 10、训练白名单为 9，Product Executor 仍为 0。

2026-08-02 EVD-207 登记 `mining.claim_reward_chests` 的已加载原版 MineShaft 精确奖励箱范围，
复用唯一 `claim_mine_reward_chest -> executor.claim_mine_reward_chest` 链。EVD-122 覆盖固定奖励、
星之果实和强制随机奖励的原生领取与清箱；骷髅钥匙特殊箱、金镰刀祭坛和未知箱体不在范围内。
金镰刀虽已有 59/59 运行证据，仍因显式玩家确认策略保持训练排除。当前 compiler-bound 为 80、
五门闭环为 11、训练白名单为 10，Product Executor 仍为 0。

2026-08-02 EVD-208 登记 `foraging.pan_ore_spot` 的当前地图精确活动矿点范围，复用唯一
`pan_ore_spot -> executor.pan_ore_spot` 链。隔离运行验证铜盘与钢盘的原生工具周期、实时奖励多集、
收货统计、TimesPanned、采矿/采集 XP 及矿点消费/重生观察；奖励继续由当前 Pan 和实时 RNG 输入
精确投影，不使用固定表。当前 compiler-bound 为 81、五门闭环为 12、训练白名单为 11，
 Product Executor 仍为 0。

2026-08-02 EVD-209 登记 `fishing.collect_crab_pots` 的当前地图已就绪原版基础 `CrabPot` 精确收取范围，
复用唯一 `collect_crab_pot -> executor.collect_crab_pot` 链。锁定版反编译和既有隔离制品共同覆盖
实时产物、Book of Crabbing 确定性翻倍、背包入账、Fishing XP、`caughtFish` 统计及 bait/ready/tile-index
复位。未就绪、背包拒收、投影不完整和自定义子类均失败关闭；放置与补饵不属于本目标。当前
compiler-bound 为 82、五门闭环为 13、训练白名单为 12，Product Executor 仍为 0。

2026-08-03 EVD-210 登记 `fishing.service_fish_ponds` 的已完成原版基础 `FishPond` 双分支范围，
复用唯一 `collect_fish_pond_output` / `complete_fish_pond_request` 到对应 executor 的链。原生制品验证
产物入账与价格派生 Fishing XP，以及逐件提交请求物品、人口上限/解锁门槛/刷新计时和请求 XP。
产物分支保持原生优先；请求分支仍要求策略授权，不等于绕过显式用户确认。未完工/自定义鱼塘、
投影漂移、背包或工具栏不足、sign/cracker 截获均失败关闭。当前 compiler-bound 为 83、五门闭环
为 14、训练白名单为 13，Product Executor 仍为 0。

2026-08-03 EVD-211 登记 `foraging.collect_spawned_objects` 的当前地图精确原版基础拾取范围，复用唯一
`collect_spawned_object -> executor.collect_spawned_object` 链。五类隐藏隔离矩阵已验证普通、Botanist、
确定性 Gatherer 双倍、特殊 `724519` 与动物屋内部的原生数量、品质和双技能经验。质量与两类经验值
现在从透明候选完整运输到运行请求并在执行前重绑；Lewis 地下室 `(O)789` 的额外 Bat/音画副作用、
自定义子类和错误身份继续失败关闭。当前 compiler-bound 为 84、五门闭环为 15、训练白名单为 14，
Product Executor 仍为 0。

2026-08-03 EVD-212 登记 `foraging.clear_green_rain_bushes` 的当前加载地图精确原版基础
`ResourceClump` 索引 44/46 范围，复用唯一
`clear_green_rain_resource_clump -> executor.break_current_location_resource_clump` 链。两种索引均通过
隐藏隔离原生斧头验证，精确匹配日/存档/锚点 RNG 核心掉落和 `+15` 采集经验；既有普通任务与特别
订单证据继续证明原生收取进度。秘密纸条只携带可知身份与概率并在执行后观测，不伪造确定结果。
当前 compiler-bound 为 85、五门闭环为 16、训练白名单为 15，Product Executor 仍为 0。

2026-08-04 EVD-213 登记 `farm.collect_machine_outputs` 的当前加载地图精确已完成原版非孵化器
机器产物收取范围。它只过滤并复用既有 `MachineServiceCandidates -> collect_machine_output_tile ->
collect_machine_output -> executor.collect_machine_output` 单链，没有新增第二套候选、编译器或执行器。
背包入账、机器清空、结构化技能经验和精通经验已有隐藏隔离原生矩阵；远端路径头、孵化器、投料、
制作、摆放、搬迁和存储不在本准入范围。`farm.process_machines` 因广义需求、保留量和时间价值策略
尚未全闭合，继续保持校准专用。EVD-180 的任务绑定是编译证据，不在本次冒充任务附着运行证据。
当前 registered 为 98、semantic 为 166、compiler-bound 为 86、五门闭环为 17、训练白名单为 16，
Product Executor 仍为 0；权威分母重新冻结后 blocking 为 0。

直接执行顺序调整为：继续第 2/4/5 步，按权威字典依赖树扩大五门准入并补原生运行证据；随后
用真实长期 rollout 采集 `policy_decision_trajectory.v2`，闭合日/季/年/爷爷 21 分标签，生成并审计
manifest；再运行 V1 全量训练、独立存档离线/在线评测和第三年 21 分长跑。通过后才进入第 10 步
冻结完美策略，第 11 步拟人化仍必须保持独立 profile/checkpoint。

直接执行任务见 [`SHORT_HANDOFF_20260802_STRUCTURED_POLICY_CN.md`](stardewai-full-handoff/SHORT_HANDOFF_20260802_STRUCTURED_POLICY_CN.md)。
## 2026-08-04 EVD-214 机器承诺投料切片

新增 `farm.load_supported_machine_input`，但只覆盖当前地图、精确绑定到已摆放机器的有效支持意图、
当前确定性正净值、零附加耗材、输入槽未被其他目标预留的单次投料。它过滤并复用既有
`MachineServiceCandidates -> load_machine_input_tile -> load_machine_input -> executor.load_machine_input`
单链，不新增第二套执行器。候选、计划、编译和派发都绑定支持账本与材料预留账本版本，漂移即阻塞。

隐藏静默运行 `runtime-machine-daily-plan-smoke-20260804-104556` 已验证原生投料、处理开始、训练行写入和
支持意图完成。权威对账为 99 registered / 167 semantic / 87 compiler-bound / 18 five-gate /
17 allowlist / 0 Product Executor，585/585 exports，blocking 0。广义 `farm.process_machines`、随机输出、
附加耗材、任务/收集需求及完整制作-摆放-投料生命周期仍未因此准入。下一切片必须继续从这些边界中
选择一个可完整闭合的高层语义，不能把 EVD-214 外推为机器模块全部完成。

## 2026-08-04 受支持机器容量生命周期编排（EVD-215 已闭合）

新增 `farm.establish_supported_machine_capacity`，把已有机器支持意图推进成严格的滚动状态机：无活动
意图时只选择一个 `goal.economy.earn_money` 下证据完整、当前有界正净收益的制作候选；
`craft_selected` 阶段只暴露绑定同一意图的既有 `place_machine_item`；`placement_bound` 阶段优先暴露
同一精确机器的既有 `load_machine_input_tile`，若机器尚未实际出现则重放原精确目标的摆放候选。
任意无效活动意图、目标不一致或证据漂移都失败关闭，且不会趁机创建第二个意图。

这不是第二套机器实现。它只过滤并组合已有
`craft_machine_item -> executor.craft_machine_item`、
`place_machine_item -> executor.place_machine` 和
`load_machine_input_tile -> executor.load_machine_input` 三条原生链，账本绑定与派发校验仍由既有组件负责。
隐藏静默隔离运行 `runtime-supported-machine-capacity-20260804-120211` 已由同一高层语义跨三次新快照
完成 Keg 原生制作、规划器选择并绑定 `Farm:61,15`、Wheat 原生投料、处理开始、精确意图完成和三条
训练行落盘。运行还发现并修复了透明桥与执行器原料重算之间缺少售价字段的契约漂移。当前权威对账为
100 registered / 168 semantic / 88 compiler-bound / 19 five-gate / 18 allowlist / 0 Product Executor，
585/585 exports，blocking 0，冻结分母指纹不变。EVD-215 只放行上述当前地图、有界正收益、确定性零附加
耗材生命周期；任务/收集需求机器处理仍是下一独立切片，不能借本状态机放行。

## 2026-08-05 任务/收集需求机器处理（EVD-216 已闭合）

新增 `farm.fulfill_machine_task_demand`，但只准入已有、当前地图、可达机器的一条确定性任务链。普通
`ResourceCollectionQuest` 按精确物品身份匹配，特别订单 `CollectObjective` 按原生上下文标签语法匹配；
投料必须来自实时原生探针、产物确定、附加耗材计数为零，且不存在尚未精确投影的活动材料保留。
投料只是生产源步骤，任务进度不得增加；机器自然完成后，既有原生收取链才是入账步骤。

隐藏静默隔离矩阵 `runtime-machine-task-demand-20260805-001630` 已让 Charcoal Kiln `(BC)114` 原生消耗
Wood `(O)388` 并自然产出 Coal `(O)382`。普通任务和特别订单均在投料后保持 `0/1`，原生收取后变为
`1/1`，没有直接写任务计数。该切片不新增第二套机器候选、编译器或执行器，也不放行广义
`quest.advance` / `farm.process_machines`。为任务新制作或摆放机器、远程机器、随机产物、附加耗材、
浣熊包需求和精确的任务优先材料保留仍是后续独立切片。

## 2026-08-05 任务驱动机器容量组合（EVD-217 已闭合）

既有 `farm.establish_supported_machine_capacity` 现在增加一个严格有界的需求类别：实时普通
`ResourceCollectionQuest` 的精确物品，或特别订单 `CollectObjective` 的上下文标签集合，必须能由当前快照中
确定性、零附加耗材的机器产物满足。已摆放机器或背包机器会抵消重复建设；背包已有机器时直接进入既有摆放链，
否则进入既有制作链。相同 `MachineSupportIntent` 随后负责摆放和 EVD-216 的精确任务投料。没有新增制作、摆放、
投料、收取或运行时执行器。

任务证据必须规范化，并且只能包含精确普通收集任务或特别订单来源。机器本体身份不能冒充预测产物需求。
浣熊包、`CraftingQuest`、混合来源、随机产物、附加耗材、远程机器、任务漂移和精确槽位预留冲突均失败关闭。
无关预留只有在既有材料图证明精确输入槽余量充足时才可通过。

E 盘隐藏静默矩阵已通过三条正例：普通任务的制作路径
`runtime-supported-machine-capacity-evd217-final2-ordinary-20260805`、特别订单的制作路径
`runtime-supported-machine-capacity-evd217-final-special-20260805`，以及背包已有机器直接摆放路径
`runtime-supported-machine-capacity-evd217-final2-inventory-20260805`。三条路径均完成原生摆放、精确投料、自然处理和原生收取；投料后
任务进度保持 `0/1`，收取后变为 `1/1`。背包直摆首次运行暴露远程放置候选抢占当前地图原生放置的问题，候选入口已
收紧为当前地图 `place_machine_item`，修复后通过。任务漂移、混合来源、附加耗材和精确槽位冲突由静态编译/账本测试
失败关闭，未冒充运行时负例。EVD-217 扩大同一选项的训练证据范围，不增加五门闭环或 allowlist 的选项数量。
最终回归 Core 1528/1528、Backend 112/112 通过，整套 Release 构建 0 错误、5 个既有警告。

## 2026-08-05 单步透明跨图访问（EVD-218 已闭合）

`exploration.visit_location` 已按滚动时域准入：每次只选择当前地图上一个实时可达、已解析、目标地图不同的
精确连接器，编译为既有 `executor.traverse_connector`，原生穿越后必须读取新快照再规划。若当前路径被一个
可移除对象阻挡，同一高层选项可以先复用既有 `executor.clear_obstacle` 清除一个精确障碍，然后刷新快照；
不得把一次证据外推为任意多段路线、关闭门禁、未知 Action、未加载地图碰撞或自定义连接器支持。

候选 ID 现包含源地图、源格、连接器种类、目标地图和可用时的到达格，避免多个出口去重碰撞。直连分支只读取
位置、碰撞、连接器和动作分支所需字段；对象、地形、背包与工具明细只在清障分支读取和校验。路线修复候选保留
原清障参数，并携带原路线候选、预期目标和刷新策略，不存在第二套移动或清障执行系统。

隐藏静默 E 盘运行 `runtime-route-connector-smoke-evd218-pass-20260805` 从完整透明快照产生 3 个候选，日计划仅取
`FarmHouse:27,31 -> Farm:64,15`，形成唯一 `traverse_connector` 计划步和唯一
`executor.traverse_connector` 队列项。原生结果为 `applied/verified`，到达 `Farm`，after snapshot 新鲜、状态哈希
变化，并写入恰好 1 条训练特征。EVD-189 支持清障原语本身；清障后继续跨图仍必须由下一张新快照重新候选，
本轮没有伪造组合运行负例。
最终回归 Core 1530/1530、Backend 114/114 通过；整套 Release 构建 0 错误、5 个既有警告。

## 2026-08-10 `quest.advance` 权威目录与 DailyPlan 编译链对齐

`quest.advance` 的已绑定任务阶段现在由 `QuestActionCoverageCatalog` 直接提供 DailyPlan 候选 kind
白名单，不再维护另一份容易漂移的手写注册表。锁定的 1.6.15 反编译扫描仍得到 12 种普通任务类型、
9 种特别订单目标类型、28 个阶段，其中 21 个已绑定、5 个明确阻塞、2 个仅原生观察，未发现未登记类型。
本轮修正了矿层目标和出货目标的真实候选名称，并补回已实现但旧目录遗漏的巨型作物、绿雨资源团块、
钓鱼、机器投料和怪物掉落等收集来源。DailyPlan 入口会拒绝不属于 `quest.advance` 目录的候选 kind，
防止跨领域动作被任务选项误编译。

当前权威对账为 104 registered / 171 semantic / 103 compiler-bound / 39 five-gate / 26 allowlist /
0 Product Executor，585/585 exports，blocking 0。`quest.advance` 只从 `Unbound` 提升为
`StepCompilerDeclared`；候选状态仍为 `PartiallyBlocked`，运行状态仍为 `RegisteredOnly`，没有进入训练
白名单。Core 1601/1601、Backend 119/119 通过。本切片未启动游戏，下一步是分别用隔离存档闭合
`executor.quest_npc_interact` 与 `executor.quest_drop_box_donate` 的原生运行、结果校验和训练行证据；
之后才处理 5 个明确阻塞阶段，不能把部分任务链外推为完整任务系统。

## 2026-08-10 任务原生终端运行闭环（EVD-233 已闭合）

`quest.advance` 的普通物品交付与特别订单投递箱两类终端现已通过隐藏、静音、E 盘隔离存档矩阵。普通任务由
精确任务候选编译为既有 `executor.quest_npc_interact`，通过原生 `checkAction` 给 Robin 交付物品并观察任务消失；
Gunther 特别订单由精确 `DonateObjective` 候选编译为既有 `executor.quest_drop_box_donate`，通过地图原生
`DropBox GuntherBox`、订单互斥锁、`QuestContainerMenu` 背包点击与确认生命周期把进度从 `0` 增至 `1`。
两条链均完成 DailyPlan、动作队列、原生执行、结果回读和各一条训练特征落盘，没有直接写任务计数。

运行矩阵同时修复三项真实闭环缺陷：交付任务错误继承普通交谈的“手持物品阻塞”；投递物品后原生菜单自动关闭
却被状态机误报失败；投递箱、博物馆与社区中心三个原生菜单执行器使用 `verified_native_*` 非标准状态，导致
LiveTrainingLoop 丢弃实际成功样本。现统一使用标准 `verified`，原生生命周期细节保留在验证原因中。

当前任务目录仍为 28 阶段、21 已绑定、5 明确阻塞、2 仅观察，`quest.advance` 仍保持 PartiallyBlocked、
RegisteredOnly 且不进入训练白名单。下一步依次关闭 5 个明确阻塞阶段：普通制作、建造、秘密物品取得、type-11
除草和 Junimo Kart 分数；同时补齐 preserved `ColoredObject` 父物品颜色标签透明投影。只有目录声明范围、编译、
运行和输出反馈全部闭合后，才能重新评估 `quest.advance` 的训练准入。

## 2026-08-10 Donate 父物品颜色透明闭环（EVD-234 已闭合）

锁定版 1.6.15 反编译确认：只有 `DonateObjective.IsValidItem` 在标签组以 `color` 开头且物品为带
`preservedParentSheetIndex` 的 `ColoredObject` 时，改用父物品 `GetBaseContextTags`；`CollectObjective`、
`DeliverObjective`、`GiftObjective` 和 `FishObjective` 只读取当前物品 `GetContextTags`。透明桥现逐背包槽位保留
`donate_color_context`，不把父物品标签错误并入普通 `context_tags`。核心匹配器按上述两个原生入口分流；父项
投影缺失时失败关闭，父项颜色不匹配时不得回退到成品自身颜色。

E 盘隐藏静音矩阵现为 `3/3`。新增案例使用原生 `QiChallenge12`、`QiChallengeBox`、运行时确认带
`color_red` 的父物品和原生 `DonateObjective.IsValidItem`，再经候选、DailyPlan、动作队列和既有
`executor.quest_drop_box_donate` 完成原生菜单点击与确认，目标进度增加并写入 verified 训练行。运行中暴露的
未见 Mr. Qi 地点事件只在隔离测试夹具中按当前原生前置条件标记已见，生产执行器未放宽。下一步处理普通制作，
其余明确阻塞阶段顺序不变。

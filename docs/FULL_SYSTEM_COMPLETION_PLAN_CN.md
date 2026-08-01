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
3. 当前准入项为 EVD-095 范围内的 `mining.reach_depth`、EVD-192 明确意图范围内的 `inventory.transfer_item`、EVD-076/EVD-105 当前已加载原版 NPC 对话范围内的 `social.talk_npc`，以及 EVD-196 当前已加载原版 NPC 滚动追踪和普通单件送礼范围内的 `social.gift_npc`；
4. 执行器原语和校准型高层 option 不进入策略训练；
5. 字典副本与锁文件只能证明语义来源和版本，不能代替原生可见执行证据。

2026-07-31 已完成 D1 的首个工程切片：“持续移动 + 通用清障/农场工具/火山石头共享原生工具周期 + 共享转向路径游标 + 600 帧异常环形诊断”。通用清障生产路径不再直接 `performToolAction` 或移除对象；第一次后台回归以 11/11 步进入 Caldera。解锁后的可见回归先后暴露“火山领域复制的简化路径推进器未继承转弯中心修复”和“外层超时未触发诊断”两个缺口；收敛火山冷却/清障到共享路径推进器并补齐异常落盘后，最终可见 level 9 回归以 14/14 步进入 Caldera，其中含 4 次石头清障、3 次移动、3 次熔岩冷却、3 次等待和 1 次出口穿越，全部 after snapshot fresh 且 state hash 改变。

2026-08-01 首个分母冻结后的切片 `inventory.transfer_item` 已完成强类型明确意图、透明库存图投影、候选、路径站位、日计划和既有 `executor.transfer_material` 机械原语复用；没有新增第二套箱子运行时。EVD-192 在隐藏、静音的 E 盘隔离存档中完成双向原生运行：`Chest.ShowMenu -> ItemGrabMenu.receiveRightClick -> Chest.grabItemFromChest/grabItemFromInventory` 两支均通过，菜单与互斥锁生命周期、逐单位数量、before/after 快照、episode 和 dataset 均闭合，往返恢复初值；过期源栈投影在输入前失败关闭。该高层项五门证据已登记，但 Product Executor 仍为未集成，不能据此声称产品陪玩闭环。随后完成 current live snapshot schema 工作流：旧快照稳定复现 `player.storage_crafting` 和 `player.storage_placement` 两个缺失，新实时快照覆盖 94/94 required factors（77 可读、17 场景性不可用、0 阻塞），完整 KnowledgeCompiler 达到 585/585 exports、blocking 0。复核又确认 `recovery.stabilize_day` 的全部当前候选编译链和普通社交原生 Harness 执行器早已存在；旧“先补 recovery、再做社交执行器”的表述作废，禁止复制第二套。EVD-076/EVD-105 现将 `social.talk_npc` 的窄范围五门证据回填；EVD-195 又闭合 `recovery.stabilize_day` 的跨图回家、原生睡眠和新日输出，但该高层动作仍按策略保持校准专用。EVD-196 随后复用同一社交链闭合当前已加载原版 NPC 的滚动远端追踪和普通单件礼物 `1 -> null`，训练准入增至 4 项。EVD-197 再把旧聚合训练、默认/显式排序和预测 API 锁定到同一生成式 allowlist，并使校准排除和准入排除可审计；它不是正式模型。下一步建立强类型策略轨迹、接入 C# 结构化排序器并开始长期完整 rollout；短训只能做基础设施烟测，不得替代正式全量训练。

架构防扩张门同步生效：`ModEntry` 不再新增领域 active state，新领域必须由独立 Handler 持有状态与 cleanup；含 304 个 public instance 属性、横跨 8 个 partial 声明文件的 `TrainingExecutionRequest.v1` 停止新增字段。v2 采用强类型 payload envelope，并按领域逐族兼容迁移，禁止一次性重写现有 compiler/runtime/verifier 链。

明日直接执行任务见 [`SHORT_HANDOFF_20260731_EXECUTOR_SUBSTRATE_CN.md`](stardewai-full-handoff/SHORT_HANDOFF_20260731_EXECUTOR_SUBSTRATE_CN.md)。

# StardewAI 当前工作

更新时间：2026-08-11

## 当前权威检查点（优先于下方历史记录）

- 锁定版本仍为 Stardew Valley 1.6.15；KnowledgeCompiler 当前为 `585/585` exports、blocking `0`。
- 动作对账当前为 `114 registered / 177 semantic / 113 compiler-bound / 40 five-gate / 27 training allowlist / 0 Product Executor`；
  原生分母仍为 `320 surfaces / 428 branches / 150 map tokens`，三类 blocking 均为 `0`。
- `quest.advance` 的 28 个目录阶段为 `24 bound / 0 blocked / 3 observation-only / 1 native-unreachable`；反编译扫描为
  `12` 种普通任务类型和 `9` 种特别订单目标类型，未发现未登记类型。
- EVD-235 已把任务终端矩阵扩展为 `4/4`：新增普通 `CraftingQuest`，从目的限定的
  `player.quest_crafting` 经 `quest.advance`、DailyPlan、动作队列和既有原生 `CraftingPage` 执行器完成，
  并写入精确任务身份、前后存在/完成状态及 terminal 事实。EVD-236 又闭合普通 `HaveBuildingQuest`：
  `player.quest_building_construction`、候选、DailyPlan 和唯一 `executor.construct_building` 通过原生
  Robin/`CarpenterMenu` 放置建筑，原生扣除钱和材料并生成三天施工倒计时；后续天数复用既有恢复睡眠链。
- `quest.advance` 已因目录零阻塞提升为 `Declared / StepCompilerDeclared / RegisteredOnly`，但没有进入训练白名单；
  目录绑定完成不等于原生运行证据闭环。
- EVD-237 已确认秘密物品取得不是独立任务动作：原版只有任务 128/129，`Railroad.getFish` 在同一
  原生钓鱼事务中返回 `(O)191` 并创建两条任务；透明桥与现有 `fishing.catch_fish` 已覆盖该特殊收获，
  `itemFound=false` 任务行只是事务中的瞬态观察，不得再建第二套任务钓鱼执行器。候选与运行时均复用
  `player.inventory_capacity` 要求至少一个空格；满包必须先走既有存储转移链，避免唯一项链进入未接管的
  `ItemGrabMenu` 后无法重新取得。
- EVD-238 已关闭 type-11 假缺口：锁定 `Data/Quests` 的 66 行没有 Weeding 类型，`Quest.getQuestFromId`
  没有对应工厂分支，原生任务源码没有任何 `questType=11` 写入点；它只是保留的兼容常量。目录使用独立
  `native_unreachable` 状态，KnowledgeCompiler 每次对账复核常量、工厂分支和写入点；旧存档或模组强塞
  type-11 时明确失败关闭，不生成除草执行器。
- EVD-239 已闭合 Junimo Kart 分数的静态主链：真实 full 快照验证
  `current_location.arcade_action_tiles` 可读且带来源；`JKScoreObjective` 绑定 Saloon 街机，复用移动、地图交互和
  `MinecartGame/Endless` 对话原语和唯一 `executor.play_junimo_kart`。训练默认策略现为 `timed_equivalent`：按既有
  15 分钟平均预算计时 54,000 tick，运行时可加速墙钟，但只通过原生 `MineCart.submitHighScore()` 提交并核对
  `JKScoreObjective`，结果必须标记 `simulated_equivalent`，不得伪装成原生完美游玩证据。
- 原有只发送跳跃输入的 `native_perfect` 控制器完整保留且与等价分数写入隔离；它后续用于帮玩家完成完美存档。
  只有该模式真实达到 Endless 50,000 分并自然提交，才能登记 Junimo Kart 原生五门证据。训练等价模式不得增加
  five-gate 或 allowlist 计数。
- 2026-08-11 复核发现 `30,190` 历史运行的 smoke 脚本没有设置 `SMAPI_MODS_PATH`，实际还加载了
  `JunimoTestClient`，因此该制品只保留为受污染诊断样本，不再作为运行验收或回退基线。脚本现使用每次运行独立的
  两模组白名单，并把白名单写入汇总。首个干净矩阵为
  `artifacts/runtime-junimo-kart/runtime-junimo-kart-20260811-002951/`，峰值 `10,940/50,000`，仍为 blocked。
- 当前唯一控制器已加入 `Bubble` 和 `FallingBoulder`/下一次 spawner 的只读轨迹预测，复刻落石原生加速度、
  速度上限和逐轨道反弹顺序；干净矩阵证明这些实体进入运行轨迹。连续跳跃模拟现复刻原生释放时重力归零、
  位移前 `x/x+4/x-4` 落地检测、坡道/冰面/黏液速度倍率和落地帧水平位移。运行
  `runtime-junimo-kart-20260811-011601` 观察 57 次落地，预测与实际 X 的最大绝对误差为 `0px`。
- `native_perfect` 当前干净最高分仍为 `10,940/50,000`；精确落点矩阵峰值为 `9,320`，其中 7/8 次为 theme 0，不能直接作为
  算法回退判断。剩余主缺口是 8 次 planner fallback：需要从原生轨道求下一段可行落地区间，替代固定
  `gap + 18px` 目标，并按主题进行可重复校准。不得用直接改分、改轨道或改任务目标替代控制问题。
- EVD-240 已完成 `quest.accept_daily`：透明桥读取实时 `questOfTheDay`、接受许可、原生任务身份和从 Town
  地图发现的 `Billboard 3` 入口；候选层先做上游许可排除，再按新快照逐连接器接近柜台，终端编译为原生
  Billboard 交互与接受。隐藏静默隔离运行 `runtime-daily-quest-acceptance-20260811-125209` 已验证任务进入
  quest log、`acceptedDailyQuest=true`、原生接受字段和 `daysLeft=2`。新快照基线为 required 103、blocking 0。
  该高层项及原语暂保持 RegisteredOnly，不因一次通过直接进入训练白名单。
- EVD-241 已完成统一的 `quest.accept_special_order` 链：Town、Qi 和沙漠节庆入口共享透明读取、滚动寻路、
  原生开板和精确左右选择，不另建移动或菜单系统。Town 隐藏静默隔离运行
  `runtime-special-order-acceptance-20260811-172636` 已通过，验证原生互斥锁延迟、`Robin2` 的 key/seed/指纹和
  accepted type；新快照基线为 required 104、blocking 0。Qi 与沙漠节庆目前只有反编译和结构覆盖，待独立运行
  校准，因此两项仍保持 RegisteredOnly，five-gate 与 allowlist 不变。
- EVD-242 已完成 `quest.claim_reward`：透明桥实时枚举普通任务日志中的可领取金钱奖励，并用任务 ID、运行时类型、
  标题、奖励、接受日和 daily 标记生成稳定指纹；候选层在菜单非空、身份或金额漂移时上游排除。唯一
  `executor.claim_quest_reward` 构造原生 `QuestLog`、选择精确任务行、点击 `rewardBox`，验证原生
  `OnMoneyRewardClaimed`/`OnLeaveQuestPage` 收据；生产代码没有直接写钱、`moneyReward`、`destroy` 或任务日志。
  隐藏静默隔离运行 `runtime-quest-reward-claim-20260811-195512` 验证 `144755 -> 145505`、奖励 750g 和任务移除。
  最新 full 快照基线为 required 105、blocking 0。
- 最新运行证据：
  `artifacts/runtime-quest-reward-claim/runtime-quest-reward-claim-20260811-195512/summary.json` 和
  `artifacts/runtime-junimo-kart/runtime-junimo-kart-20260811-194648/summary.json`。

## 当前阶段

锁定 Stardew Valley 1.6.15 的动作全集对账和独立分母冻结已经完成，现已转入逐动作纵向
闭环。当前 114 个注册项是可复用的实现基线，不是被废弃的旧代码；63 个已编目未注册语义项
用于记录已证实但尚未实现的能力。正式训练保持阻塞。

## 已完成

- 现有注册表、治理表、编译器注册和 Harness 能力表已做集合一致性校验；
- 取消 `OptionRegistry` 中手写的 31/65 固定计数，改为逐 ID 对账；
- 每个现有动作已归属唯一主执行引擎；
- KnowledgeCompiler 开始生成：
  - `native-action-surface-inventory.json`
  - `native-action-branch-inventory.json`
  - `native-map-interaction-coverage.json`
  - `semantic-action-catalog.json`
  - `action-implementation-reconciliation.json`
  - `action-progress-dashboard.json`
  - `native-action-denominator-fingerprint.json`
- 独立冻结文件 `native-action-denominator-freeze.json` 已与当前指纹核对一致；覆盖状态和
  实现进度不进入分母身份哈希，因此后续把占位动作提升为实现不会伪造“分母变化”；
- 原生方法扫描已改为 Roslyn 语法解析，方法重载按完整签名独立建档；
- 60 个宽入口已展开为 428 条带源码行号和哈希的分支证据；
- 1,102 个有效地图交互实例已归并为 150 个 Action/TouchAction token，并逐项连接
  到原生处理分支。

## 当前任务

当前生成基线位于 `catalogs/vanilla-1.6.15/`：

- 320 个原生输入表面，表面级未分类 0；
- 60 个宽入口全部生成分支目录，428 条分支中待语义审查 0、缺注册 0；
- 150 个地图交互 token 中 142 个映射到语义动作，8 个经原生分支证实为无玩家语义、
  失效/遗留静态 token，待审查 0；
- 语义动作目录共 177 项：114 项已有 `OptionSpec`，63 项为
  `catalogued_blocked`，确认存在但尚未登记的动作数为 0。

机器状态为 `native_action_denominator_frozen`，当前锁定扫描范围已闭合并通过独立审批文件
核对。不能把“已登记”解释为“已实现”：现有代码的编译器孤儿 0、运行 ID 孤儿 0；
Product Executor 仍为 0；EVD-204 回填后，五门证据闭环为 8，训练准入为 7。

## 退出条件

- 原生动作表面未分类数为 0；
- 宽入口分支和有效地图交互 token 未审查数为 0；
- 锁定扫描范围的语义动作分母可确定性重生成并完成治理冻结；
- 所有已证实语义动作均已注册，未实现项必须保持显式 blocked；
- 所有现有代码零孤儿，每个动作只有一个主执行引擎；
- 固定口径看板可重复生成。

## 紧接任务

首个缺口 `inventory.transfer_item` 已完成纵向闭环：强类型明确意图、透明库存图投影、
上游候选、路径站位、日计划展开和既有 `executor.transfer_material` 原语复用均已接通。
EVD-192 在 E 盘隔离存档中验证了“箱子到玩家”和“玩家到箱子”两个方向，均经原生
`Chest`/`ItemGrabMenu`、逐单位右键、互斥锁释放、before/after 数量差分和训练记录；往返后
箱子数量恢复，过期源栈投影在菜单打开前失败关闭且零点击。该项五门证据已登记，可进入其
明确意图范围内的训练；Product Executor 仍未集成，不得把 Harness 闭环称为产品陪玩闭环。
`player.storage_crafting` 与 `player.storage_placement` 的透明性 join 已修复：旧快照稳定复现
94 项 required state factor 中 2 项缺失，当前实时快照为 77 项带完整来源可读、17 项场景性
不可用、0 项阻塞。新安装器会先校验全部 required factors、哈希与版本，再原子更新外部权威
字典的 current 指针；完整 KnowledgeCompiler 已以该指针达到 585/585 exports、blocking 0。
复核确认 `recovery.stabilize_day` 的全部当前候选到日计划/队列编译链早已完成，普通社交对话和
送礼也共用唯一 `executor.social_interact` 原生 Harness 执行器，不得重复实现第二套。EVD-195 已在
隐藏、静音的 E 盘隔离存档中闭合 `Farm@2200 -> 单连接器回家 -> 新鲜快照重规划 -> 原生睡眠 -> 新日`，
因此 recovery 五门已通过，但它仍是校准型高层动作，不进入策略训练。EVD-196 又通过现有社交链
完成 Abigail 的实时远端滚动追踪：35 轮中 31 个连接器动作和一次原生送礼均验证通过，普通礼物
`(O)388` 严格由栈 1 变为 `null`，且未读取未来日程。`social.gift_npc` 现仅在“当前已加载原版 NPC、
同图或滚动连接器追踪、普通单件礼物”范围内五门闭合并进入训练准入；不得外推为模组 NPC、特殊
物品或全部社交完成。下一切片按既定路线重建准入策略轨迹并接入 C# 结构化排序器，不重做候选、
编译器或社交执行器。

正式轨迹与数据治理硬闸已经完成：`PolicyTrainingAdmissionFilter` 直接消费生成式 allowlist，
校准行与未准入行分开计数；每条 v2 轨迹绑定 effective ranking、完整源候选、版本化状态特征、
编译队列、执行结果、fresh after-state 和观察型长回报。清洗器按存档/游戏日确定性切分，拒绝
冲突标签并生成逐文件 SHA-256 manifest。旧 `BaselineFeatureRowTrainer` 仍只作聚合烟测；正式路径
使用下节的 C# 结构化提供器。

## 2026-08-02 正式模型链状态

EVD-201 已完成首个真实 C# 结构化策略提供器。轨迹 schema 升级为
`policy_decision_trajectory.v2` / `policy_features.v2`，每条轨迹除版本化状态特征外，还保留
完整源候选对象，商店、位置、物品、价格、开放时窗、排程、原因、参数与结构化效果字段不会在
训练前丢失。训练器只对“已准入且当前可用”的候选建立成对比较，检查点绑定数据清单及三个分区
SHA-256、特征/候选/能力/字典/编译器/执行器版本；推理只重排既有候选，不复制候选生成、日计划、
编译器或执行器。`--require-structured-policy` 在检查点缺失时失败关闭。

当前标准 E 盘生产轨迹、跨度观测、正式 manifest 和检查点四个路径均不存在。因此完成的是模型
基础设施与合成契约验收，不是生产训练。直接下一步仍是按权威字典依赖顺序扩大五门准入范围，
再用真实、verified/fresh 的长期 rollout 生成 v2 轨迹和闭合跨度标签；形成正式 manifest 后才运行
`StardewAI.PolicyModel`。只有独立存档评测与第三年 21 分长跑通过后，才冻结“最强完美 AI”基线。

EVD-202 没有新增第二套矿洞候选、编译器或执行器，而是把已有 EVD-106 运行证据登记到
`mining.obtain_skull_key` 的五门：范围严格限定为普通矿井 119 -> 120 层、原生骷髅钥匙宝箱领取、
`has_skull_key false -> true` 与原生退出。该目标现进入训练白名单；沙漠矿洞、采石场矿洞金镰刀和
火山矿洞仍是独立族，未被本证据放行。当前五项准入为 `inventory.transfer_item`、
`mining.obtain_skull_key`、`mining.reach_depth`、`social.gift_npc`、`social.talk_npc`。

EVD-203 随后独立登记 `volcano.reach_caldera`：EVD-190 的火山 0..9 -> Caldera 完整原生滚动链
记录 106 步、82 次 applied/verified 和 24 次安全重规划；EVD-191 的战斗目的链记录 66 步、
27 次验证动作和 39 次安全脱离/重规划。两份制品均无非新鲜快照或未变化状态。模型只决定是否选择
“到达 Caldera”这个高层目标；浇岩浆、清石、战斗、移动、门和连接器仍由确定性候选/编译/执行链
完成。普通矿井、沙漠矿洞、采石场矿洞金镰刀与火山矿洞证据继续互不借用。当前六项准入为
`inventory.transfer_item`、`mining.obtain_skull_key`、`mining.reach_depth`、`social.gift_npc`、
`social.talk_npc`、`volcano.reach_caldera`。

EVD-204 复核并登记 `skills.read_books`。能力目录此前只识别动作队列直接编译器，漏记了现有
`DailyPlanCompiler` 的 `read_inventory_book -> executor.read_book -> wait_ticks` 展开，因此错误显示
`compiler=unbound`；现已用同一治理入口登记日计划 option compiler，没有新增读书执行器。EVD-124
七用例矩阵覆盖六类原版基础书籍分支，全部 applied/verified。当前 compiler-bound 为 77，五门闭环
为 8，训练准入为 7；新增准入项是 `skills.read_books`。自定义 `performUseAction`、畸形模组标签和
原版证据范围外分支继续失败关闭。

## 2026-08-11 职业选择闭环（EVD-244）

`skills.choose_profession` 已从恢复链中的隐式自动处理提升为正式语义动作。透明桥同时公开当前
`LevelUpMenu` 的两个原生职业选项、精确 ID、标题、描述，以及玩家持久职业列表和待处理升级列表；候选层只在
两个选项均完整可读时开放，并把同一选择界面的两个候选登记为互斥决策。DailyPlan 只把所选职业编译到既有
`close_menu -> executor.close_menu`，没有新增第二套职业或菜单执行器。

锁定版反编译确认 5 级分支为每技能 `skill * 6 + 0/1`，10 级分支按已有 5 级职业选择 `+2/+3` 或 `+4/+5`；
运行回执记录选择前后的职业、待处理升级、生命、体力等即时变化。隐藏静默 E 盘全矩阵
`runtime-profession-choice-20260811-203159` 覆盖原版 30 个职业 ID，修正前置即时 perk 的战斗复核矩阵
`runtime-profession-choice-20260811-203610` 为 6/6 通过，并验证 Fighter `+15` 与 Defender `+25` 最大生命。
最新 full snapshot schema 为 105 required、88 实时带来源可读、17 场景性、blocking 0；动作对账为
114 registered / 177 semantic / 113 compiler-bound / 63 catalogued-blocked，five-gate 为 40，训练准入为 27。

直接下一主切片是 `mail.process_letter`。`mining.use_elevator` 必须先按普通矿井既有移动、菜单和楼层切换链做
复用对账，只有证明存在未覆盖的原生分支才允许新增实现。

## 禁止事项

- 不把 97 当作总动作数；
- 不把 Harness dispatch 当作 Product Executor；
- 不因独立架构重构暂停动作主线；
- 不开始短训或正式训练；
- 不启动游戏，除非当前动作已完成静态和单元测试且明确进入运行验收。

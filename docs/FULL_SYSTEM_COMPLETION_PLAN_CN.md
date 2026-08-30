# StardewAI 完全体完成路线图

## 2026-08-30 联机聊天玩家命令闭环（EVD-311）

`multiplayer.send_chat` 已按完整纵向切片闭合并严格保持 `PlayerCommandOnly`。透明桥发布当前发送者、语言、默认颜色、网络角色、消息队列边界、ChatTextBox 宽度和在线收件人的原生枚举/匹配信息。上游只接受带原因和确认的普通全局消息或精确私聊，拒绝任意斜杠命令、控制字符、模糊目标及原生输入宽度无法容纳的文本；fresh 编译器不信任模型提供的机械字段，全部从当前投影重新绑定。

生产运行层只模拟原生聊天框生命周期：激活 ChatBox、逐字符输入 ChatTextBox，再调用 `textBoxEnter`。脏词过滤、全局 AllPlayers/私聊 exact recipient、type-10 网络分发和发送者本地 kind 0/kind 3 回执都由 1.6.15 原版处理；执行器禁止直接调用 `sendChatMessage`/`receiveChatMessage`，且只声明发送路径与本地回执，不伪造远端送达。隐藏静音矩阵 `2/2` 通过。当前对账为 `202 registered / 217 semantic / 201 compiler-bound / 125 five-gate / 54 allowlist / 15 catalogued blocked / 0 Product Executor`，full snapshot `158/141/17/0`、KnowledgeCompiler `585/585` blocking 0、Core `2105/2105`、Backend `151/151`、Release `0 warnings / 0 errors`。该切片不增加策略训练 allowlist；下一实际纵向切片为 `player.choose_bobber`。

## 2026-08-30 联机钱包玩家命令闭环（EVD-310）

`multiplayer.manage_wallet` 已按完整纵向切片闭合，但严格保持 `PlayerCommandOnly`。透明桥发布 ManorHouse LedgerBook 端点、当前共享/独立模式、今晚待切换状态、已认领参与者、原生收款人顺序与响应键、共享及个人余额、赠款统计、五项命令门控和两种次日结算投影。上游仅接收带原因和确认的显式命令；fresh 编译器从实时投影重绑全部机械字段，跨地图 continuation 锁定同一操作、收款人和金额。

运行层复用共享 BFS，只通过原生 LedgerBook `checkAction`、`DialogueBox` 和 `DigitEntryMenu` 输入执行。隐藏静音矩阵 `7/7` 覆盖五项即时命令与两种原生次日结算，生产路径不存在钱包或余额直写。当前对账为 `200 registered / 216 semantic / 199 compiler-bound / 123 five-gate / 54 allowlist / 16 catalogued blocked / 0 Product Executor`，full snapshot `157/140/17/0`、KnowledgeCompiler `585/585` blocking 0、Core `2096/2096`、Backend `150/150`、Release `0 warnings / 0 errors`。该切片不增加策略训练 allowlist；Junimo Kart 维持既定后置边界，下一实际纵向切片为 `multiplayer.send_chat`。

## 2026-08-30 卡利科雕像原生闭环（EVD-309）

`mining.activate_calico_statue` 已按完整纵向切片闭合并进入策略训练范围。透明桥以房主权威的日存档随机域发布下一次精确效果、完整 18 效果目录、当前效果栈、雕像地块、可达站位、评分和所有效果回执；上游只在沙漠节骷髅洞内、当前层雕像未激活且存在安全站位时生成候选。fresh 编译器重新计算效果并锁定种子输入、投影指纹、效果身份、地块和前后状态。

运行层复用共享 BFS，只调用一次原生 `MineShaft.checkAction`，并以 `284 -> 285`、激活计数、评分、效果栈、蛋奖励、速度或完全恢复状态验收。隐藏静音矩阵 `18/18` 覆盖效果 ID `0..17`，不存在第二套随机、奖励或状态写入器。当前对账为 `198 registered / 215 semantic / 197 compiler-bound / 121 five-gate / 54 allowlist / 17 catalogued blocked / 0 Product Executor`，full snapshot `156/139/17/0`、KnowledgeCompiler `585/585` blocking 0、Core `2087/2087`、Backend `149/149`、Release `0 warnings / 0 errors`。下一实际纵向切片为 `multiplayer.manage_wallet`。

## 2026-08-30 赌场老虎机原生闭环（EVD-308）

`minigame.play_slots` 已按完整纵向切片闭合并进入策略训练范围。透明桥实时发布共享 Rarecrow 齐币需求、ClubSlots 机器、10/100 下注、Luck 倍率、完整概率/图案/倍率分布、期望净收益、活动转轴和原生退出状态；fresh 编译器锁定全部机械字段。运行层复用共享路线，只经原生 `checkAction` 和 `Slots.receiveLeftClick` 输入执行一次旋转，原版共享 RNG 独占结果生成，执行器只验收图案、倍率、齐币差、`timesPlayedSlots` 和 Done 清理。

隐藏静音矩阵 `2/2` 覆盖 10 币无奖和 100 币单七 `x2` 分支，不存在第二套 RNG、转轴或齐币写入器。当前对账为 `196 registered / 214 semantic / 195 compiler-bound / 119 five-gate / 53 allowlist / 18 catalogued blocked / 0 Product Executor`，full snapshot `155/139/16/0`、KnowledgeCompiler `585/585` blocking 0、Core `2080/2080`、Backend `148/148`、Release `0 warnings / 0 errors`。下一实际纵向切片为 `mining.activate_calico_statue`。

## 2026-08-30 草原大王正式 AI 等价闭环（EVD-307）

`minigame.play_prairie_king` 已按完整纵向切片闭合并进入策略训练范围。透明桥发布原生完成统计、无伤统计、JOTPK 存档、Saloon 端点、活动小游戏状态、108000-tick 等价预算和策略边界；fresh 编译器绑定全部机械字段。运行层复用共享路线，经原生入口创建 `AbigailGame`，计时期间暂停开始界面，结束后调用 `usePowerup(-3)` 并只接受原生统计、邮件和成就检查回执。

此处的“闭合”明确等于 AI actor 的最终正式行为，不是原生完美代打。高层选择可训练，底层结果永久标记 `simulated_equivalent`；逐帧代理控制仅作为核心能力训练完成后的玩家指令扩展。隐藏静音冒烟通过统计 `0->1`、无伤 `0->1`、`Beat_PK=True`。当前对账为 `194 registered / 213 semantic / 193 compiler-bound / 117 five-gate / 52 allowlist / 19 catalogued blocked / 0 Product Executor`，full snapshot `154/138/16/0`、KnowledgeCompiler `585/585` blocking 0。Junimo Kart AI 继续复用既有等价执行器；下一实际纵向切片为 `minigame.play_slots`。

## 2026-08-30 飞镖原生自主闭环（EVD-306）

`minigame.play_darts` 已按完整纵向切片闭合并进入策略训练范围。透明桥发布目标洞穴天气上下文、海盗夜、活动状态、地图端点、三阶段飞镖限额、分数/投掷/瞄准/充能状态和限量核桃计数。高层只决定是否取得下一枚核桃；fresh 编译器绑定全部机械字段，运行层复用共享路线并只经原生 `checkAction`、Yes 对话和鼠标输入完成。

隐藏静音矩阵 `3/3` 覆盖 `20/15/10` 支飞镖和全部三枚核桃，六投方案全部原生结算；不存在第二套分数或奖励写入器。当前对账为 `192 registered / 212 semantic / 191 compiler-bound / 115 five-gate / 51 allowlist / 20 catalogued blocked / 0 Product Executor`，原生 `322/448/150` 分母已冻结且 full snapshot `153/137/16/0` blocking 0，回归为 Core `2071/2071`、Backend `148/148`、Release `0 warnings / 0 errors`。下一纵向切片按冻结目录顺序为 `minigame.play_junimo_kart`，应先核对并复用已有 `executor.play_junimo_kart`，禁止再建第二套小游戏执行器。

## 2026-08-30 抓娃娃机原生玩家命令闭环（EVD-305）

`minigame.play_crane_game` 已按完整纵向切片闭合，但保持为玩家命令能力。透明桥发布机器占用、电影奖池规则、基础奖池、500g/3 次/900 tick 规则、交互端点，以及活动会话的原生状态、爪钩、奖品、传送带、速度、抓取与掉落字段。高层只授权一次会话；fresh 编译器绑定全部机械字段，运行层复用共享路线并只经原生交互、确认、D/S 输入和奖励菜单完成。

隐藏静音冒烟 `1/1` 完成三次原生机会、500g 精确扣费和两件奖励转移；不存在第二套 RNG、奖品物理或背包写入器。当前对账为 `190 registered / 211 semantic / 189 compiler-bound / 113 five-gate / 50 allowlist / 21 catalogued blocked / 0 Product Executor`，原生 `322/448/150` 分母已冻结且 full snapshot `152/136/16/0` blocking 0，回归为 Core `2066/2066`、Backend `148/148`、Release `0 warnings / 0 errors`。下一纵向切片按冻结目录顺序为 `minigame.play_darts`。

## 2026-08-30 Calico Jack 原生牌局闭环（EVD-304）

`minigame.play_calico_jack` 已按完整纵向切片闭合并进入策略训练范围。透明桥发布赌场权限、齐币、Rarecrow 需求、两张桌子、精确动作格、下一局种子、初始牌、隐藏牌、完整未来随机流、推荐要牌/停牌序列、预期结算和活动小游戏私有状态。高层只决定是否接受一个由 `(BC)126`/Deluxe Scarecrow 依赖产生的单局候选；fresh 编译器覆盖下注、牌、随机数、点击和结算等全部机械字段。

运行层复用共享路线，只经原生桌面 `checkAction`、`Play` 对话和 `CalicoJack` 左键输入完成。隐藏静音矩阵 `3/3` 覆盖 1000 齐币获胜、100 齐币失败和首次要牌获胜；不存在第二套牌局状态写入器。当前对账为 `188 registered / 210 semantic / 187 compiler-bound / 111 five-gate / 50 allowlist / 22 catalogued blocked / 0 Product Executor`，原生 `322/448/150` 分母和 full snapshot `151/135/16/0` 均 blocking 0，回归为 Core `2060/2060`、Backend `148/148`、Release `0 warnings / 0 errors`。下一纵向切片按冻结目录顺序为 `minigame.play_crane_game`。

## 2026-08-30 Field Office 原生调查闭环（EVD-303）

`island.field_office_survey` 已按完整纵向切片闭合并进入策略训练范围。透明桥发布唯一下一题、答案范围与锁定答案、问题/响应键、左右植物、当日失败锁、collected-nut、当前 debris、核桃前后计数、finale readiness 和原生交付方式。高层只选择是否答当前题；fresh 编译器重绑全部机械字段，跨地图 continuation 锁定题型与答案。

运行层复用共享路线，只经 `FieldOfficeSurvey -> Survey_Yes -> exact Correct` 原生输入完成。隐藏静音矩阵 `9/9` 覆盖两题、同日顺序、错误锁日、DayUpdate、130 上限和 finale，并区分瞬时 debris 生成与磁力拾取后的最终状态；没有第二套 Field Office 状态写入器。当前对账为 `186 registered / 209 semantic / 185 compiler-bound / 109 five-gate / 49 allowlist / 23 catalogued blocked / 0 Product Executor`，KnowledgeCompiler `585/585`、blocking `0`，回归为 Core `2054/2054`、Backend `148/148`、Release `0 warnings / 0 errors`。下一纵向切片按冻结目录顺序为 `minigame.play_calico_jack`。

## 2026-08-30 Field Office 原生捐赠闭环（EVD-302）

`island.field_office_donate` 已按完整纵向切片闭合。透明桥发布完整 11 槽化石状态、原生重复物品分配顺序、当前背包可捐候选、Desk/Survey 端点、解锁/教授/互斥锁/菜单、四组恢复标记、两项调查、finale readiness、GoldenWalnutsFound 和奖励队列。高层只选择一个精确捐赠候选并要求确认；fresh 编译器重绑所有机械字段，跨地图 continuation 只保留物品与目标槽身份。

运行层复用共享路线和连续移动，只经 `FieldOfficeDesk -> Safari_Donate -> FieldOfficeMenu` 输入完成。隐藏静音最终矩阵 `15/15` 覆盖所有槽位、两组多件集合奖励、两组单件普通奖励和 130 核桃替代奖励；没有第二套岛屿状态写入器。全量复核把此前被捐赠语义遮蔽的 `FieldOfficeSurvey` 拆为独立待办，因此当前对账为 `184 registered / 208 semantic / 183 compiler-bound / 107 five-gate / 48 allowlist / 24 catalogued blocked / 0 Product Executor`，KnowledgeCompiler `585/585`、blocking `0`，回归为 Core `2049/2049`、Backend `148/148`、Release `0 warnings / 0 errors`。下一纵向切片为 `island.field_office_survey`：复用当前透明状态、岛屿路线、通用对话输入和持久化回执，独立覆盖 22 朵紫花、18 只紫海星、当日失败锁和 finale 触发，不得扩写捐赠执行器。

## 2026-08-30 住宅装修原生玩家命令闭环（EVD-301）

`housing.renovate` 已按完整纵向切片闭合，但分类为玩家指令能力而非自主策略动作。透明桥实时发布基础 1.6.15 `Data/HomeRenovations` 的完整 18 项目录、原生可用顺序、要求/动作、区域、婴儿床特殊门、阻挡、费用、首次购买与退款投影。玩家命令只选择装修 ID、区域并给出原因/确认；fresh 编译器重绑机械字段并从碰撞网格选择可达 Robin 柜台站位。

运行层复用共享移动和菜单输入，只经 `Carpenter -> Renovate -> HouseRenovations -> RenovateMenu` 原生链执行。隐藏静音矩阵 `19/19` 覆盖 18 个原版分支及负价无首次购买标记不退款分支；跨地图 continuation 保留玩家命令授权并精确锁定装修 ID/区域，原生终端成功前禁止切换目标；没有第二套住宅状态写入器。当前对账为 `182 registered / 206 semantic / 181 compiler-bound / 105 five-gate / 48 allowlist / 24 catalogued blocked / 0 Product Executor`，KnowledgeCompiler `585/585`、blocking `0`，回归为 Core `2045/2045`、Backend `148/148`、Release `0 warnings / 0 errors`。下一纵向切片为 `island.field_office_donate`；必须先锁定捐赠集合、奖励、菜单与持久化分支，再复用既有岛屿路线、库存选择和原生菜单执行体系。

## 2026-08-30 垃圾桶翻找原生闭环（EVD-300）

`foraging.rummage_garbage` 已按完整纵向切片进入正式策略训练范围。透明桥基于一次地图交互扫描和锁定 `Data/GarbageCans` 发布全部原版 Garbage 端点、日级检查状态、统计、运气/书籍、非变异确定预测、完整物品状态、交付方式、NPC 反应与安全槽。候选先排除已检查、数据漂移、负好感目击、容量/空槽和路线问题，fresh 编译器再重绑全部机械字段。

运行层复用共享 BFS、连续移动、原生 `checkAction`、输出守恒和任务反馈，不复制路线、拾取或任务执行器。隐藏静音矩阵 `9/9` 覆盖全部有界交付/排除/NPC 分支以及两类收集任务；当前对账为 `180 registered / 205 semantic / 179 compiler-bound / 103 five-gate / 48 allowlist / 25 catalogued blocked / 0 Product Executor`，回归为 Core `2039/2039`、Backend `145/145`、Release `0 warnings / 0 errors`。下一纵向切片为 `housing.renovate`：先锁定 RenovateMenu 全部原版选项、前置条件、费用、房屋几何与可逆性，再决定哪些必须保持 PlayerCommandOnly，禁止把装饰偏好混入自主训练。

## 2026-08-30 普通树产品原生收获闭环（EVD-299）

`foraging.harvest_tree_product` 已按完整纵向切片进入正式策略训练范围。透明桥在既有 terrain feature 行发布精确基础 `Tree` 身份、种类、成熟/树桩/tapped/种子/摇动状态、`Data/WildTrees` 合同状态、确定输出、完整随机可选输出域、零经验合同、安全空槽和相邻交互状态。候选层先排除自定义类型、数据漂移、未成熟、树桩、tapped、无种子、采集等级不足、摇动中、输出不完整、无空槽及无可达站位，编译器再从 fresh snapshot 重绑全部机械字段。

运行层复用共享 terrain BFS 和连续移动，到站后才冻结背包与 debris 联合基线，并只经 `GameLocation.checkAction -> Tree.performUseAction -> Tree.shake` 执行。隐藏静音矩阵覆盖普通种子、秋季枫树榛子、岛屿棕榈及无种子/摇动中/tapped 上游排除；随机附加掉落只按完整有界域验收，不作为监督标签。当前对账为 `178 registered / 204 semantic / 177 compiler-bound / 102 five-gate / 47 allowlist / 26 catalogued blocked / 0 Product Executor`，回归为 Core `2035/2035`、Backend `144/144`。下一纵向切片为 `foraging.rummage_garbage`；它必须复用现有移动、对象交互、输出守恒和任务资源反馈，不得复制路由或拾取执行器。

## 2026-08-30 果树原生收获闭环（EVD-298）

`foraging.harvest_fruit_tree` 已按一个完整纵向切片进入正式策略训练范围。透明桥在既有 terrain feature 行中发布精确 base FruitTree 身份、成熟/树桩/摇动状态、实时 fruit 列表、按标识与品质分组的最终输出、雷击替换、零经验合同和相邻交互状态。候选层先排除自定义类型、未成熟、树桩、空树、瞬态空果、摇动中、输出不完整及无可达站位，编译器再从 fresh snapshot 重绑全部机械字段。

运行层复用共享 BFS 和连续移动，只经 `GameLocation.checkAction -> FruitTree.performUseAction -> FruitTree.shake` 执行。隐藏静音矩阵覆盖单果、三果金星、雷击三煤炭以及空树/摇动中排除，成功分支逐项核对输出守恒、fruit 清空和 Foraging XP 零变化。当前对账为 `176 registered / 203 semantic / 175 compiler-bound / 101 five-gate / 46 allowlist / 27 catalogued blocked / 0 Product Executor`，回归为 Core `2031/2031`、Backend `143/143`。下一纵向切片为 `foraging.harvest_tree_product`。

## 2026-08-30 鱼塘管理玩家命令闭环（EVD-297）

`fishing.manage_fish_pond` 已覆盖两种原版管理操作，并与自动鱼塘服务链保持单一职责：模型可训练的日循环负责收产出和交请求，换网与清塘只响应玩家明确指令。上游要求精确坐标、操作、原因及清塘二次确认；fresh 编译器重新绑定鱼种、数量、请求、饼干、标牌、产出、水色、网样式、站位和安全槽，任何漂移都闭锁。

运行层只通过共享 BFS、作用域右键输入边沿和原生 `PondQueryMenu` 公共按钮执行。隐藏静音样本 `runtime-fish-pond-management-20260830-013602` 已验证换网保全经济状态与清塘的完整 reset/preserve/debris 矩阵。当前对账为 `174 registered / 202 semantic / 173 compiler-bound / 100 five-gate / 45 allowlist / 28 catalogued blocked / 0 Product Executor`，回归为 Core `2027/2027`、Backend `142/142`。下一纵向切片为 `foraging.harvest_fruit_tree`。

## 2026-08-30 展览会转盘闭环（EVD-296）

`festival.spin_wheel` 已覆盖 Fall 16 原版转盘的完整策略与执行周期。透明桥发布实时入口/站位、数字下注菜单、活动转盘私有状态、完整 Fair 商店与 Stardrop 缺口、零幸运 `22/30` 绿方胜率、有效 `LuckLevel` 及原版随机/计时/结算合同。策略使用 `7/15` 零幸运 Kelly 下注并按需求封顶；精确缺一枚继续复用力量小游戏。

运行层只通过原生对话、数字输入和 `WheelSpinGame` 执行，胜负均按随机反馈验收。隐藏静音运行 `runtime-fair-wheel-spin-20260830-005054` 两次即覆盖 `466` 星币下注的胜负两支和清理。当前对账为 `173 registered / 202 semantic / 172 compiler-bound / 99 five-gate / 45 allowlist / 29 catalogued blocked / 0 Product Executor`，回归为 Core `2023/2023`、Backend `138/138`、Release `0 warnings / 0 errors`。下一纵向切片为 `fishing.manage_fish_pond`。

## 2026-08-29 展览会力量小游戏闭环（EVD-295）

`festival.play_strength_game` 已覆盖 Fall 16 原版力量小游戏的完整策略与执行周期。透明桥发布唯一入口和站位、免费/固定一星币合同、实时力量/速度/方向/动画/计时状态、全部 Fair 商店行和 Stardrop 缺口；候选仅服务于扣除陈列奖励后恰好剩余一枚星币的需求。

运行层复用共享 BFS，经真实 `Event.checkAction` 打开 `StrengthGame`，等待原生移动结算并根据点击后 `9` 次力量更新预测最大值，只发送一次原生点击。两个隐藏静音样本覆盖 changeSpeed `4` 和 `3`，分别达到力量 `100` 和 `99`，均原生获得一星币并清理菜单状态。当前对账为 `171 registered / 201 semantic / 170 compiler-bound / 97 five-gate / 44 allowlist / 30 catalogued blocked / 0 Product Executor`，回归为 Core `2018/2018`、Backend `138/138`、Release `0 warnings / 0 errors`。下一纵向切片为 `festival.spin_wheel`。

## 2026-08-29 展览会靶场闭环（EVD-294）

`festival.play_slingshot_game` 已覆盖 Fall 16 原版靶场的稳定策略周期。透明桥发布入口、费用、四段原生时序、完整 79 目标日程、实时目标/弹丸/临时装备状态、精确准确率/倍率/奖励公式、全部 Fair 商店行和 Stardrop 缺口；候选只为未获得 Stardrop 的剩余星币需求服务。

运行层复用共享 BFS 和普通矿井弹弓的唯一 `SlingshotAimPatch`，经真实节日 DialogueBox 点击进入原版 TargetGame，在物理更新前计算移动目标拦截点并只发原生输入。隐藏静音 E 盘样本 PASS：48 发 48 次有效命中，raw `95`、accuracy `102`、final `380`、封顶 `500` 星币；费用、返回和临时装备清理均验证。当前对账为 `169 registered / 200 semantic / 168 compiler-bound / 95 five-gate / 43 allowlist / 31 catalogued blocked / 0 Product Executor`，回归为 Core `2013/2013`、Backend `138/138`、Release `0 warnings / 0 errors`。下一纵向切片为 `festival.play_strength_game`。

## 2026-08-29 展览会钓鱼小游戏闭环（EVD-293）

`festival.play_fishing_game` 已覆盖 Fall 16 原版钓鱼游戏的稳定策略周期。透明桥发布入口、费用、时长、临时钓具、实时小游戏状态、精确评分/奖励公式、全部星币商店行和 Fair Stardrop 缺口；候选只为未获得 Stardrop 的剩余星币需求服务，并把尚未领取的展览陈列奖励计入供给，其他购买价值留给后续策略而不自动刷分。

运行层复用共享 BFS 和普通钓鱼控制器，经真实节日 DialogueBox 点击进入原版 100 秒 FishingGame，在 `UpdateTicking` 发合法输入并验证随机结果的完整公式，不写结果字段。隐藏静音 E 盘最终样本 PASS：`5/5` 完美、`364` 分、`432` 星币。Core `2008/2008`、Backend `138/138`、Release `0 warnings / 0 errors`；当前对账为 `167 registered / 199 semantic / 166 compiler-bound / 93 five-gate / 42 allowlist / 32 catalogued blocked / 0 Product Executor`。下一纵向切片为 `festival.play_slingshot_game`。

## 2026-08-29 展览会陈列闭环（EVD-292）

`festival.manage_grange_display` 已覆盖秋季 16 日展览会的完整稳定陈列周期：实时枚举共享展台和可用库存单位，按原版实际售价、品质、八类多样性、九件数量分及 Mayor 短裤惩罚求最优组合；评审前逐次替换至最佳可用陈列，评审后逐次取回。策略训练只学习安排该目标，坐标、槽位、物品身份、分数、互斥锁和一次操作都由 fresh snapshot 与编译器机械绑定。

运行层复用共享 BFS/移动，只经 `Event.checkAction` 打开原生 `StorageContainer` 并完成一次菜单点击对，不复制展台或评分实现，也不启动评审。隐藏静音 E 盘 `10/10` PASS，九次放入达到 `124` 分、超过一等奖阈值 `90`，并完成一次评审后取回；Core `2003/2003`、Backend `138/138`、Release `0 warnings / 0 errors`。当前对账为 `165 registered / 198 semantic / 164 compiler-bound / 91 five-gate / 41 allowlist / 33 catalogued blocked / 0 Product Executor`。下一纵向切片为 `festival.play_fishing_game`。

## 2026-08-29 五类传送图腾原生路由闭环（EVD-291）

`executor.use_warp_totem` 已覆盖锁定 1.6.15 的五种可达原版变体和完整稳定使用分支。透明桥按库存行绑定精确 Object 身份、公共使用门、Farm `WarpTotemEntry`/农场类型回退、固定目的地、被动节日替换链、主动节日入口、地图宽度修正和动画时序；编译器拒绝库存、目的地、节日、坐标、时序、颜色、指纹或原生合同漂移，并在会浪费图腾或需要联机 ReadyCheck 时上游关闭。

运行层复用唯一 `UseInventoryObjectNative`，等待 `Object.totemWarpForReal -> Game1.warpFarmer` 原生回调后验证单物品消费、精确目的地和角色状态，不复制传送实现。隐藏静音 E 盘五变体 `5/5` PASS，最终回归为 Core `1998/1998`、Backend `138/138`、Release `0 warnings / 0 errors`。当前对账为 `163 registered / 197 semantic / 162 compiler-bound / 89 five-gate / 40 allowlist / 34 catalogued blocked / 0 Product Executor`。下一纵向切片为 `festival.manage_grange_display`。

## 2026-08-29 宝藏图腾原生宝藏点闭环（EVD-290）

`executor.use_treasure_totem` 已覆盖锁定 1.6.15 的完整稳定世界使用分支。透明桥绑定精确 `(O)TreasureTotem`、公共物品使用门、室内状态、中心周围 16 格原生圆环、每格放置/占用/前景/灌木/地层判定、可生成集合、世界使用计数、确定性生成合同与指纹；编译器拒绝任何身份、地图、候选集合、计数、时序或合同漂移，并在零有效格时避免浪费图腾。

运行层复用唯一 `UseInventoryObjectNative`，只验证原生消费、计数递增和精确 `(O)590` 生成集合，不直接增删地图对象。后续掉落解析与挖掘复用现有 `ArtifactSpots -> executor.clear_obstacle` 链。隐藏静音 E 盘运行生成 `16/16`，最终回归为 Core `1976/1976`、Backend `138/138`、Release `0 warnings / 0 errors`。当前对账为 `162 registered / 197 semantic / 161 compiler-bound / 88 five-gate / 40 allowlist / 35 catalogued blocked / 0 Product Executor`。下一纵向切片为 `executor.use_warp_totem`。

## 2026-08-29 回城魔杖原生住宅传送闭环（EVD-289）

`executor.use_return_scepter` 已覆盖锁定 1.6.15 的完整稳定世界使用分支。透明桥绑定精确 `(T)ReturnScepter`/`Wand` 身份、不可消耗栈、当前角色 `homeLocation`、原生 `FarmHouse.getFrontDoorSpot()` 结果、房主/小屋差异、浴衣/桥上原生门和执行器稳定门；编译器拒绝库存、住宅类型、门前格、时序、指纹或原生合同漂移，并排除已在落点的无价值重复使用。

运行层复用游戏唯一即时工具入口 `Farmer.BeginUsingTool`，等待 `Wand.wandWarpForReal` 原生回调后验证落点、物品身份以及显示/无敌/移动状态，不复制传送算法。隐藏静音 E 盘运行 PASS，最终回归为 Core `1964/1964`、Backend `138/138`、Release `0 warnings / 0 errors`。当前对账为 `161 registered / 197 semantic / 160 compiler-bound / 87 five-gate / 40 allowlist / 36 catalogued blocked / 0 Product Executor`。下一纵向切片为 `executor.use_treasure_totem`。

## 2026-08-29 雨水图腾原生天气闭环（EVD-288）

`executor.use_rain_totem` 已覆盖锁定 1.6.15 的完整原生物品分支。透明桥绑定精确 `(O)681` 库存身份、公共使用门、`AllowRainTotem`、`RainTotemAffectsContext`、决策目标与实际天气状态归属、默认节日门、即时天气写入、2000ms 动画/提示，以及默认上下文的换日最终天气修正规则。编译器拒绝任何库存、路由、日期、最终天气、时序或指纹漂移，并在节日、最终天气覆盖、重复 Rain 等无效消耗发生前关闭动作。

运行层复用唯一 `UseInventoryObjectNative`，等待原生提示和控制恢复后验证库存、上下文天气与朝向；没有第二套天气执行器。隐藏静音 E 盘四分支均符合预期，最终回归为 Core `1949/1949`、Backend `138/138`、Release `0 warnings / 0 errors`。当前对账为 `160 registered / 197 semantic / 159 compiler-bound / 86 five-gate / 40 allowlist / 37 catalogued blocked / 0 Product Executor`。下一纵向切片为 `executor.use_return_scepter`。

## 2026-08-29 怪兽香水原生 Buff 闭环（EVD-287）

`executor.use_monster_musk` 已覆盖锁定 1.6.15 的完整原生物品分支。透明桥绑定精确 `(O)879` 库存身份、公共使用门、`Data/Buffs` 的 Buff 24 定义、当前 Buff 实例与剩余时间、全部确定性动画/精灵域，以及普通矿井和火山地牢的原生生成率消费者；生成编译器拒绝任何身份、数据、活动 Buff、时序、生成语义或指纹漂移。

运行层复用唯一 `UseInventoryObjectNative`，等待 750ms 回调和 1750ms 原生动作完全结算，再以新 Buff 实例、总时长、库存与朝向形成回执。隐藏静音 E 盘首次施加与替换刷新均为 `applied/verified`；最终回归为 Core `1932/1932`、Backend `138/138`、Release `0 warnings / 0 errors`。当前对账为 `159 registered / 197 semantic / 158 compiler-bound / 85 five-gate / 40 allowlist / 38 catalogued blocked / 0 Product Executor`。下一纵向切片为 `executor.use_rain_totem`。

## 2026-08-29 马笛原生召回闭环（EVD-286）

`executor.use_horse_flute` 已覆盖锁定 1.6.15 的完整原生分支：无马、室内、无落脚空间和马匹占用限制均在上游失败关闭；已有拥有马匹位于当前地点一格邻域内时成功但不传送，否则执行朝下、音频/动画/冻结、1500ms 延迟重检和 team event/mutex 原生召回。透明桥实时发布全部决策输入，编译器只接受 fresh 指纹一致的精确马匹与库存身份。

运行层不复制传送逻辑，只调用原生 `Object.performUseAction` 并验证精确拥有马匹的后状态以及马笛堆叠不变。隐藏静音 E 盘运行两分支均为 `applied/verified`；最终回归为 Core `1921/1921`、Backend `138/138`、Release `0 warnings / 0 errors`。该动作是 `ExecutorCalibration`，不是策略欲望；上层路线可以机械选择它作为移动加速。当前对账为 `158 registered / 197 semantic / 157 compiler-bound / 84 five-gate / 40 allowlist / 39 catalogued blocked / 0 Product Executor`。下一纵向切片为 `executor.use_monster_musk`。

## 2026-08-29 三色烟花原生放置闭环（EVD-285）

`executor.use_firework` 已覆盖锁定 1.6.15 的全部三色烟花分支。透明桥发布库存身份、三色类型与源图映射、当前地图原生合法区间、目标格临时精灵冲突、2400ms 引信以及完整随机结果域；读取阶段绝不推进共享 `Game1.random`。编译器只接受 fresh snapshot 中精确匹配的库存行、相邻站位与目标格。

运行层复用唯一共享原生物品放置入口，不复制精灵、音频或库存副作用。隐藏静音隔离运行三例均创建 5 个原生临时精灵、精确消耗一件物品并验证对应火箭类型。该动作严格为 `PlayerCommandOnly`，不产生自主候选也不进入训练 allowlist。当前对账为 `157 registered / 197 semantic / 156 compiler-bound / 83 five-gate / 40 allowlist / 40 catalogued blocked / 0 Product Executor`。下一纵向切片为 `executor.use_horse_flute`。

## 2026-08-29 秘密纸条原生读取闭环（EVD-284）

`executor.read_secret_note` 已覆盖锁定 1.6.15 的两类原生可读物：普通秘密纸条 `(O)79` 按存档与玩家身份种子从未读集合确定性选择，日记残页 `(O)842` 选择最小未读编号。完整数据目录、已读/未读状态、选择输入和结果、原文哈希、显示分支及 10/23 号任务副作用均由透明桥实时发布，编译器只接受 fresh snapshot 中精确匹配的一行。

运行层与 `executor.read_book` 共用唯一原生库存物品使用入口，不复制库存消耗逻辑，也不直接修改已读集合、任务或菜单。隐藏静音隔离运行覆盖多未读种子抽取、两个任务分支和日记残页，四例均为 `applied/verified`。该动作是 `ExecutorCalibration`，不直接进入策略训练 allowlist；后续高层规划可以把“读取已有纸条”组合为机械安排。当前对账为 `156 registered / 197 semantic / 155 compiler-bound / 82 five-gate / 40 allowlist / 41 catalogued blocked / 0 Product Executor`。下一纵向切片为 `executor.use_firework`。

## 2026-08-29 草种原生放置闭环（EVD-283）

`executor.plant_grass` 已覆盖锁定 1.6.15 的全部原生草种放置分支：普通草种 `(O)297 -> Grass(1,4)` 与蓝草种 `(O)BlueGrassStarter -> Grass(7,4)`。透明桥只在 full profile 发布当前加载地图的原生合法区间、精确库存变体与结果合同；当前地形输出补充 `grass_type` 和 `number_of_weeds`。用途、布局和精确位置属于上游规划，小模型或布局器必须明确给出，执行器不得自行扩张布局意图。

编译器在 fresh snapshot 上重绑槽位、堆叠、变体、目标、相邻站位、合法区间、通行投影与指纹。运行层复用共享相邻移动和唯一原生物品放置入口，只验证原生结果，不直接写地形或库存。隐藏静音 E 盘运行 `runtime-grass-placement-20260829-093605` 已验证两个变体及透明后状态。该动作是 `ExecutorCalibration`，不进入策略训练 allowlist。当前对账为 `155 registered / 197 semantic / 154 compiler-bound / 81 five-gate / 40 allowlist / 42 catalogued blocked / 0 Product Executor`。下一纵向切片为 `executor.read_secret_note`。

## 2026-08-29 Drum Block 原生调音闭环（EVD-282）

`world.tune_drum_block` 已完成 `read -> explicit-command exclusion -> plan -> fresh rebind -> native runtime -> receipt -> E3`。透明桥发布持久化原始/解析音色、下一档、完整七档循环、对应 `drumkitN` 音色和独立路过播放入口；编译器只允许安全空槽/工具槽并发起一次原生地点交互，不直接写音色、摇动或缩放。

Flute 与 Drum 已收敛到唯一 `NoteBlockTuning` 运行状态机，但身份、状态算法、请求字段和证据合同仍独立，避免一条运行结果外推到另一动作。隐藏静音 E 盘运行 `runtime-drum-block-20260829-040623` 已验证 `6->0`、`drumkit0`、摇动/缩放、身份与槽位恢复。该动作是 `PlayerCommandOnly`，不进入自主候选和训练。当前对账为 `154 registered / 197 semantic / 153 compiler-bound / 80 five-gate / 40 allowlist / 43 catalogued blocked / 0 Product Executor`。下一纵向切片固定为 `executor.plant_grass`。

## 2026-08-29 Flute Block 原生调音闭环（EVD-281）

`world.tune_flute_block` 已完成 `read -> explicit-command exclusion -> plan -> fresh rebind -> native runtime -> receipt -> E3`。透明桥发布 `preservedParentSheetIndex` 原始/解析值、下一档、完整 25 档循环、基础音色与独立路过播放入口；编译器只允许空槽/工具槽并发起一次原生地点交互，不直接写音高、摇动或缩放。

隐藏静音 E 盘运行 `runtime-flute-block-20260829-034718` 已验证 `2300->2400` 特殊边、`shakeTimer=200`、`scale.Y=1.3`、身份与槽位恢复。该动作是 `PlayerCommandOnly`，不进入自主候选和训练。当前对账为 `153 registered / 197 semantic / 152 compiler-bound / 79 five-gate / 40 allowlist / 44 catalogued blocked / 0 Product Executor`。下一纵向切片固定为 `world.tune_drum_block`。

## 2026-08-29 Farm Computer 原生报告闭环（EVD-280）

`farming.read_farm_computer_report` 已完成 `read -> explicit-command exclusion -> plan -> fresh rebind -> native runtime -> delayed receipt -> E3`。透明桥按锁定 1.6.15 的 `GetRootLocation()` 语义直接发布作物、耕地、成熟/未浇水作物、温室、采集物、机器、干草和农场洞穴字段，同时发布精确本地化报告摘要；模型不需要通过菜单读取策略信息。

该动作归类为 `PlayerCommandOnly`。生产执行复用共享对象移动器并只发起一次原生地点交互，等待 500ms 原生回调后验证 `DialogueBox` 和报告 SHA-256，不自行拼接菜单或修改对象。隐藏静音 E 盘运行 `runtime-farm-computer-20260829-031326` 已验证即时摇动/冻结、延迟报告、对象身份和槽位恢复。当前对账为 `152 registered / 197 semantic / 151 compiler-bound / 78 five-gate / 40 allowlist / 45 catalogued blocked / 0 Product Executor`。

下一纵向切片固定为 `world.tune_flute_block`。必须先从锁定反编译源确认调音方向、音高边界、持久化字段和原生可观测结果；若属于装饰/玩家表达，只建立显式命令执行闭环，不得进入自主策略候选或训练。

## 2026-08-29 Mini-Obelisk 原生路由闭环（EVD-279）

`movement.use_mini_obelisk` 已完成 `read -> upstream exclusion -> plan -> fresh rebind -> native runtime -> delayed receipt -> E3`。它只与其他静态对象动作共享 `NativeObjectInteractionMovement` 和 BFS；配对扫描、距离判定、落点顺序、50ms 原生延迟和传送回执均为独立合同。原生容器顺序必须实时读取，不能把测试夹具的赋值顺序当成 `location.objects.Pairs` 顺序。生产执行只发起一次 `GameLocation.checkAction`，不直接改角色坐标。

该动作归类为 `ExecutorCalibration`：路线编译器可以在明确的执行器校准流程中调用，但它不参与小模型的策略欲望训练，不能因为“传送成功”获得日计划价值标签。隐藏静音 E 盘运行 `runtime-mini-obelisk-20260829-011139` 已验证精确落点、配对身份和槽位恢复。当前对账为 `151 registered / 197 semantic / 150 compiler-bound / 77 five-gate / 40 allowlist / 46 catalogued blocked / 0 Product Executor`。

下一纵向切片固定为 `farming.read_farm_computer_report`：先从锁定反编译源确认报告字段、访问条件、菜单/对话生命周期及可观测结果，再确定它是纯透明信息、玩家指令动作还是校准动作。若透明桥已直接发布报告的全部原生来源，则候选不得重复制造“读菜单才能知道”的假依赖。

## 2026-08-28 原版删减动作占位与自动采集器闭环（EVD-277 / EVD-278）

Issue #31 的提速原则落为工程约束：按 `Object.checkForAction`、`performUseAction` 等共享原生底层分组推进，一组只维护一个移动/菜单事务实现；组内每个动作仍必须逐项完成 `read -> upstream exclusion -> plan -> fresh rebind -> native runtime -> receipt -> E3`，不得共享语义结论或跳过运行证据。当前组完成 Feed Hopper 与 Auto-Grabber，下一项是 `movement.use_mini_obelisk`。

`Lantern` 和 `Raft` 不再伪装成原版待实现动作，而是进入 `CompatibilitySemanticActionPlaceholderCatalog`。锁定 1.6.15 的 `Data/Tools` 含 Lantern、不含 Raft，但二者都没有正常存档获取链；生成目录必须把它们映射为 `cut_content_unreachable / mapped_to_compatibility_placeholder`，并从原版 `actions` 分母排除。未来 MOD 只有在透明桥证明可达来源并提供对应适配器后，才能激活这些占位。

`animals.collect_auto_grabber_contents` 已完整复用共享对象移动，并通过原生 `ItemGrabMenu` 逐栈转移。透明桥预演背包累计容量，候选在空容器、全部不可接纳、无安全站位时上游排除；编译器重绑容器全部堆栈身份，运行回执验证转移与留存集合严格分割并守恒。隐藏运行 `runtime-auto-grabber-20260828-165346` 已通过。当前对账为 `150 registered / 197 semantic / 149 compiler-bound / 76 five-gate / 40 allowlist / 47 catalogued blocked / 0 Product Executor`，另有 2 个分母外兼容占位。

## 2026-08-28 木筏分母修正与喂食斗闭环（EVD-275 / EVD-276）

锁定 1.6.15 中 `Raft` 只有残留类型与兼容状态分支，没有 `Data/Tools`、获取、工厂、事件或外部构造入口，因此从玩家语义动作分母移除；当前由 EVD-277 以分母外兼容占位保留原生证据。该历史修正当时把冻结语义分母从 `199` 校正为 `198`，没有改变 `322 surfaces / 448 branches / 150 map tokens` 的原生扫描范围。

`animals.withdraw_feed_hopper_hay` 已实现精确透明投影和原生执行。模型只看到“当前动物屋确有未喂动物时取草”这一有意义候选；料仓、动物、已摆干草、原生取草公式、背包接纳、站位和安全槽全部由最新快照与编译器重绑定。运行时复用共享对象移动，且只调用一次原生 `GameLocation.checkAction`，以料仓和背包守恒回执验收。隐藏静音 E 盘运行 `runtime-feed-hopper-20260828-130723` 通过。当前对账为 `149 registered / 198 semantic / 148 compiler-bound / 75 five-gate / 39 allowlist / 49 catalogued blocked / 0 Product Executor`；下一闭环固定为 `animals.collect_auto_grabber_contents`。

## 2026-08-28 声音石玩家指令闭环（EVD-274）

`world.play_singing_stone` 已从冻结待办分母替换为唯一实现，但它不是自主策略能力。目标必须是当前已加载地点中的精确基础 `(BC)94`；同名家具 `(F)1300`、子类或身份漂移均失败关闭。透明桥发布原生 `crystal` 音高的完整均匀分布 `0..2300 step 100`、共享 RNG 不可预读状态、`shakeTimer=100`、相邻安全站位和对象身份。小模型只在玩家明确要求时选择目标石头，编译器重绑全部机械字段。

执行器与 House Plant 共用同一个原生静态对象移动状态机，到站后只调用一次 `GameLocation.checkAction`；生产路径不直接发声、不写震动计时器、不消费共享 RNG。隐藏静音 E 盘运行验证对象身份、原生返回、震动计时器和工具栏槽恢复。该动作保持 `PlayerCommandOnly`、不发布默认候选、不进入训练 allowlist。当前对账为 `148 registered / 199 semantic / 147 compiler-bound / 74 five-gate / 38 allowlist / 51 catalogued blocked / 0 Product Executor`；下一闭环为 `animals.withdraw_feed_hopper_hay`，优先复用唯一库存转移引擎。

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

2026-08-11 最新机器检查点：`115 registered / 177 semantic / 114 compiler-bound /
41 five-gate / 28 training allowlist / 0 Product Executor`。原生分母保持
`320 surfaces / 428 branches / 150 map tokens` 且 blocking 为 0。`mail.process_letter`
已按 EVD-245 完成；新锁定 full 快照覆盖 107 个必需字段且 blocking 为 0，KnowledgeCompiler
为 `585/585`、blocking 0。下一动作切片是 `mining.use_elevator`，只允许在普通矿井既有链上
增加原生电梯选择与楼层收据，不得复制普通矿井、火山或金镰刀洞窟系统。

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

调用来源与机械复杂度是两条正交轴。一个动作即使完全机械，也可能因会修改玩家布局、外观或文字而标记为 `PlayerCommandOnly`。此类动作保留编译/执行能力，但默认候选、自动日计划和策略训练必须全部排除；只有显式玩家指令可以进入安全授权链。不得把运行时已验证误写成训练已准入。

1. 机械动作
   - 例：浇水、收机器、回家睡觉。
   - 小模型只发 option，动作编译器全权展开步骤。
   - 训练角色：executor_calibration。

2. 参数化机械动作
   - 例：去矿洞第 N 层、去某地点钓鱼。
   - 小模型给目标参数，动作编译器做路径、风险、时间、资源校验并生成动作队列。
   - 训练角色：mixed，策略层只学选择目标，不学低层操作。

3. 玩家指令专属动作
   - 例：旋转家具/House Plant、改变或粉刷建筑外观、放置家具、设置标牌展示物、编辑文字标牌。
   - 只在玩家显式请求时编译执行；不生成默认自动候选，不参与策略训练，也不作为自主陪玩日程的一部分。
   - 分类字段：`InvocationPolicy=PlayerCommandOnly`；调用字段：`InvocationSource=PlayerCommand`。两者缺一即失败关闭。

4. 自主策略动作
   - 例：收取自然史莱姆球、选择当日资源目标、购买或长期建设。
   - 模型只在透明、合法、时间与资源约束完整的候选中做有意义选择；确定性机械细节仍由编译器和共享执行引擎负责。

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

1. `capability_registry.v3` 独立记录五门、证据 ID、证据范围、调用来源策略和类型化排除原因；
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

## 2026-08-10 普通制作与建造任务闭环（EVD-235、EVD-236 已闭合）

EVD-235 将 `CraftingQuest` 接到目的限定的 `player.quest_crafting`、`quest.advance`、DailyPlan 和既有
原生 `CraftingPage` 执行器。任务只由原生 `CraftingQuest.OnRecipeCrafted` 完成，材料预留、输出容量、配方身份
和 Workbench 来源均在候选与编译时重绑定；隐藏静音 E 盘终点矩阵为 4/4。

EVD-236 将 `HaveBuildingQuest` 接到目的限定的 `player.quest_building_construction`。透明行读取精确建筑类型、
Robin 服务、蓝图价格/材料/工期、在建状态和农场放置候选；跨地图放置判断按反编译复刻目标地图的
`GameLocation.isBuildable` 必要谓词，避免其内部错误依赖 `Game1.currentLocation`。候选将原生
`Inventory.ReduceId` 槽位消耗计划交给既有材料承诺 guard，活动预留冲突在上游排除，并在编译和派发时绑定
同一账本 revision。

唯一 `executor.construct_building` 只执行 `Carpenter` 地图动作、`Construct` 对话、原生 `CarpenterMenu`
蓝图选择与放置；不直接调用 `buildStructure`，不直接扣钱/材料，也不直接完成任务。E 盘隐藏静音运行通过：
`runtime-quest-terminal-daily-plan-20260810-173859` 验证原生菜单生命周期、资源扣除和三天施工倒计时。
后续每日结算复用既有 `recovery.stabilize_day`/`executor.sleep`，最终由
`Building.FinishConstruction -> FarmerTeam.constructedBuildings.OnValueAdded -> HaveBuildingQuest.OnBuildingExists`
完成。在 EVD-236 检查点，任务目录为 23 bound、3 blocked、2 observation-only；当时的下一项是秘密物品取得，
之后是 type-11 除草和 Junimo Kart 分数。该检查点不构成后续状态的当前声明。

## 2026-08-12 通用目的限定建筑建造（EVD-248 已闭合）

`buildings.construct` 不是第二套建造器。它是模型的策略出口，只接受明确的 `building_type`、
`placement_location_id` 和 `construction_reason`；完全机械的寻路、服务交互、蓝图选择和原生落点点击仍由
DailyPlan 与唯一 `executor.construct_building` 展开。任务建造继续从 `quest.advance` 进入同一执行器。

透明桥的 `player.building_construction_catalog` 每张 full 快照枚举实时 `Game1.buildingData` 中所有非升级
Robin/Wizard 基础蓝图和所有 `IsBuildableLocation()` 地点，逐项核验 `BuildCondition`、Cabin 原生地点限制、
价格、原生库存扣除顺序、服务动作、静态合法落点、现有完成数量，以及同类型在建建筑的坐标和剩余天数。
服务入口在单张快照内复用扫描结果；放置合法性不能跨快照缓存，因为建筑、物体、地形和角色会改变占用。

候选层在上游排除无明确用途、蓝图/地点不存在、条件不满足、已有施工、钱或材料不足、落点不可用、菜单占用
和材料承诺冲突。到达服务地点前只复用现有滚动连接器；到达后才生成一次精确建造原语。运行时只执行地图
`Action`、原生问答、`CarpenterMenu` 蓝图/地点分页和放置点击，不直接修改钱、库存、建筑集合或任务。

隐藏静默隔离验证：通用策略建造 `runtime-quest-terminal-daily-plan-20260812-105048` 与任务回归
`runtime-quest-terminal-daily-plan-20260812-105331` 均 `applied/verified`。当前五门范围仅证明 Robin 当前服务点、
原版 `Coop`、`Farm`、一次目的限定建造；Wizard、建筑升级、换皮、跨地点分页矩阵、多人所有权及长期建设策略
仍需独立证据，不得由 EVD-248 外推。

## 2026-08-10 秘密物品取得状态机纠正（EVD-237 已闭合）

锁定版 `Data/Quests` 只有 128/129 两条 `SecretLostItemQuest`，均要求 `(O)191`。反编译确认
`Railroad.getFish` 先检查秘密纸条 25 与 `carolinesNecklace` 邮件状态，在返回项链的同一调用中加入
任务 128/129；随后原生收货回调才把 `itemFound` 置真。因此 `find_secret_lost_item` 不是任务创建后
可再次派发的取得动作，而是既有钓鱼事务内部短暂可见的状态。若把它实现成第二次钓鱼，邮件已 pending
会使原生分支失效，并制造错误训练负例。

透明桥既有 `railroad_carolines_necklace` 特殊来源已直接读取上述原生条件，候选聚合器把 `(O)191`
保留在完整结果分布中，DailyPlan 和动作队列继续复用唯一 `executor.catch_fish`；执行器只发送合法输入、
观察特殊收获与原生空闲收尾，不直接创建物品或任务。聚焦测试验证了铁路候选、唯一结果分布与编译链，
并保留 EVD-228 对原生无 BobberBar 特殊收获生命周期的运行证据。由于原生 `doneHoldingFish` 在背包
无法接收时会打开必要的 `ItemGrabMenu`，而项链邮件此时已 pending、不能再次钓取，候选层和执行器
起始校验统一要求 `player.inventory_capacity.has_empty_slot=true`；否则先由既有存储转移链腾位。本阶段
不把未单独运行校准的项链案例外推为新五门准入。

在 EVD-237 检查点，任务目录为 23 bound、2 blocked、3 observation-only；当时下一步是 type-11，
之后处理 Junimo Kart 分数。在二者关闭并重新做完整任务矩阵前，`quest.advance` 仍保持 PartiallyBlocked、
RegisteredOnly 且不进入训练白名单。

## 2026-08-10 type-11 不可达兼容状态纠正（EVD-238 已闭合）

实时复核锁定 1.6.15 后确认，`Quest.type_weeding = 11` 只是遗留兼容常量，不是缺失的原版动作族：
哈希为 `591d29ecf742b2b9e258c271d1e5e55bcfcd9d7dc38c9d64972cd3faaf1b0c6a` 的 66 行
`Data/Quests` 没有 Weeding 类型，`Quest.getQuestFromId` 没有对应字符串分支，任务源码目录中也没有
任何把 `questType` 写成 11 的位置。基类所有进度回调默认返回 false，因此为它拼接清杂草动作既不能
完成原生任务，也会制造不存在的训练标签。

目录新增独立 `native_unreachable` 状态，不把它混入“观察后可自然结算”的 observation-only。
KnowledgeCompiler 现在每轮扫描 `type_weeding=11` 常量、工厂分支和全部写入点；若未来版本让它可达，
立即产生 blocking。透明桥仍完整读取任意 live `Quest.questType`；若旧存档或模组注入 type-11，候选层
返回 `quest_type_11_unreachable_in_vanilla_1_6_15`，不会发明执行器。

任务目录现为 23 bound、1 blocked、3 observation-only、1 native-unreachable。唯一剩余明确阻塞阶段是
`JKScoreObjective` 的 Junimo Kart 分数；在该阶段完成并重跑任务矩阵前，`quest.advance` 继续保持
PartiallyBlocked、RegisteredOnly 且不进入训练白名单。

## 2026-08-10 Junimo Kart 分数静态主链（EVD-239 静态与透明 schema 已闭合）

锁定 1.6.15 反编译确认：`MinecartGame_Endless` 创建 `MineCart(0, 2)`；Endless 原生死亡分支调用
`submitHighScore()`，再通过 `SpecialOrder.onJKScoreAchieved` 更新 `JKScoreObjective`。`QuitGame()` 不提交分数，
因此执行器不得提前退出或直接写分数。街机入口来自 Saloon 地图 `Action=Arcade_Minecart`，进入条件是
`Farmer.hasSkullKey`，随后选择原生对话 `MinecartGame/Endless`。

透明桥新增 `current_location.arcade_action_tiles`，并由隐藏、静音的 E 盘隔离实例生成真实 full 快照；验证结果为
required state factors 102、带来源可读 85、场景性不可用 17、blocking 0。任务候选在上游排除无骷髅钥匙、菜单占用、
入口缺失和无可达站位；跨地图仍复用滚动连接器。DailyPlan 固定展开为移动、地图交互、Endless 对话选择和
`play_junimo_kart` 四步，动作队列最终映射到唯一 `executor.play_junimo_kart`。

运行时原语只通过 SMAPI 原生输入覆盖控制跳跃，读取原生轨道、障碍、玩家运动、分数和目标计数用于控制与验收；
源码守卫禁止调用 `submitHighScore()`、`Die()`、`SetCount()` 或写入 MineCart 私有状态。任务目录现为
24 bound、0 blocked、3 observation-only、1 native-unreachable，`quest.advance` 候选状态改为 Declared。
动作对账为 107 registered / 174 semantic / 106 compiler-bound / 39 five-gate / 26 allowlist；原生分母仍为
320 surfaces / 428 branches / 150 map tokens，KnowledgeCompiler 585/585、blocking 0，并已批准新语义指纹冻结。

EVD-239 尚未完成运行验收：必须在隔离存档中真实进入 Saloon 街机，以 Endless 原生输入达到至少 50,000 分，
由自然死亡路径提交分数，并观察同一个 `JKScoreObjective` 的计数达到目标。失败、重试、超时和输出反馈也必须落盘。
在该证据产生前，`executor.play_junimo_kart` 与 `quest.advance` 均不得新增五门证据或训练准入；完成标志不是
“目录无 blocked”，而是原生 50,000 分回调、fresh after-state 与可复核运行制品同时成立。

2026-08-11 运行复核：`runtime-junimo-kart-20260810-224026` 的 `30,190/50,000` 运行缺少每次运行独立的
`SMAPI_MODS_PATH`，加载了正常 Mods 目录中的 `JunimoTestClient`，违反 `runtime-test-harness.md` 的模组隔离约束。
该制品降级为受污染诊断样本，不可作为运行证据或控制器基线。Junimo Kart smoke 现复制且只加载
`StardewAI.TransparentBridge` 与 `StardewAI.RuntimeTestHarness`，并在汇总中记录白名单与实际路径；源码守卫禁止
重新引入 `JunimoTestClient`。

首个干净两模组矩阵为 `runtime-junimo-kart-20260811-002951`，峰值 `10,940/50,000`，8 次自然死亡与任务进度
回读完整，结果仍为 blocked。唯一控制器现从原生 `_entities` 读取 `Bubble`、现存 `FallingBoulder` 和
`FallingBoulderSpawner` 的下一次生成时刻；落石预测复刻 `210 px/s²`、`96 px/s` 上限和剩余轨道反弹序列，
不写任何游戏字段。

连续轨迹切片已在 `runtime-junimo-kart-20260811-011601` 完成校准：模拟复刻 `ReleaseJump()` 重力归零、
每帧位移前的 `x/x+4/x-4` 原生轨道探测、实时 `_speedMultiplier`、主题 5 强制倍率、黏液/冰轨更新和落地帧
水平位移；90 个按住时长不再选择第一个可落地解，而按前方安全跑道、瓦片中心余量和垂直速度评分。57 次实际
落地的预测 X 最大绝对误差为 `0px`，并写入 `landing_trace`。该矩阵峰值 `9,320`，7/8 次抽到 theme 0，仍为
blocked；干净最高检查点保持 `10,940`。

下一切片不再调整已对齐的运动方程，而是消除本轮 8 次 planner fallback：扫描原生轨道得到 gap 后下一段的
完整可行落地区间，替代固定 `hazard + 18px` 下界，并把“无物理解、动态障碍冲突、窗口过窄”分别落盘。
之后在相同两模组环境按主题做可重复比较。50,000 之前不形成证据；不得引入分数、轨道、碰撞或目标状态写入。

## 2026-08-11 并行动作闭环：每日委托接受

Junimo Kart 的控制器优化允许稍后复用唯一现有执行器继续，不得因此停止其他独立动作闭环。
`quest.accept_daily` 已按唯一链完成：实时读取原生 offer 和许可，从 Town 地图实时发现 `Billboard 3`，在上游排除
不可接受状态，按新快照滚动接近，最后只点击原生 Billboard 接受按钮。隔离运行验证成功后，动作对账更新为
109 registered / 175 semantic / 108 compiler-bound；原生分母仍为 320/428/150 且 blocking 0。
本动作暂不进入训练白名单。下一动作应继续从 66 个已编目未注册语义项中按依赖独立、可复用现有执行引擎、
可形成原生回执的顺序选择，不得重建移动、交互、任务或小游戏第二套系统。

## 2026-08-11 并行动作闭环：特别订单接受

`quest.accept_special_order` 已作为唯一高层链覆盖 Town、Qi 和沙漠节庆三种入口：透明桥读取原生 offer 与入口，
候选层在上游排除锁定、同类型已接受和陈旧身份，随后按 fresh snapshot 逐阶段复用既有寻路、移动、交互、
对话关闭和菜单机制，终端只点击 `SpecialOrdersBoard` 的原生左右按钮。不得再为三类板建立并行接受器。

Town 隐藏静默隔离运行已通过；Qi 和沙漠节庆保留为同一实现的待校准分支，不因源码覆盖提前进入训练。
动作对账更新为 111 registered / 176 semantic / 110 compiler-bound，剩余 65 个已编目未注册语义项。
后续仍按“权威字典身份 -> 透明字段 -> 上游许可 -> 复用编译链 -> 原生回执 -> 独立运行证据”的顺序逐项闭合，
不得以动作总数推进为理由复制移动、菜单、任务或执行器体系。

## 2026-08-11 Junimo Kart 训练等价模式与完美模式分流（EVD-243）

EVD-307 已覆盖本节早期的单机限制，但不删除其原生控制器和诊断证据。训练、联机陪玩和专用房主中的 AI actor 默认使用
`timed_equivalent`：以既有 15 分钟平均预算作为等价时长（54,000 游戏 tick），墙钟可配置加速；计时结束后只在
受控 AI actor 执行模式内设置本局 MineCart 分数，再调用原生 `UpdateScoreState()` 与
`submitHighScore()`，并以同一个 `JKScoreObjective` 的精确进度作为收据。结果必须标记
`simulated_equivalent` 和 `synthetic_score_assignment_not_native_perfect_play`，不得登记成原生完美五门证据。

原有轨道、障碍、物理预测和跳跃输入控制器保留在独立 `native_perfect` 模式，且该文件不得包含分数或任务进度写入。
它是后续“帮玩家打完美存档”的唯一继续校准路径，严格 `PlayerCommandOnly`；真实自然达到 50,000 分前，不登记原生 five-gate 证据。
隐藏静默 E 盘矩阵 `runtime-junimo-kart-20260811-194648` 已验证等价计时、原生提交回调和目标 `0 -> 50000`。

## 2026-08-11 普通任务奖励领取（EVD-242 已闭合）

`quest.claim_reward` 使用独立于 `quest.advance` 的奖励结算链，但复用既有菜单安全门。透明桥从实时
`Farmer.questLog` 枚举非隐藏、已完成且有金钱奖励的任务，使用 ID、运行时类型、标题、奖励、接受日和 daily 标记
生成稳定指纹。菜单占用、字段缺失、身份/金额漂移均在候选或队列编译上游排除。

唯一 `executor.claim_quest_reward` 构造原生 `QuestLog`，经原生 `receiveLeftClick` 选择精确行和 `rewardBox`，
观察 `OnMoneyRewardClaimed` 的金额、`moneyReward=0`、`destroy=true` 以及原生离页移除。生产路径禁止直接
`Money +=`、写 `moneyReward`/`destroy`、调用 `OnMoneyRewardClaimed()` 或手动删除任务。隐藏静默 E 盘矩阵
`runtime-quest-reward-claim-20260811-195512` 已验证 750g 精确增量与任务消失。最新 full snapshot schema 为
105 required、88 实时带来源可读、17 场景性、blocking 0；动作对账当时为 113 registered / 177 semantic /
112 compiler-bound / 64 catalogued-blocked，原生 320/428/150 不变；后续 EVD-244 计数见下节。

## 2026-08-11 原生职业选择（EVD-244 已闭合）

`skills.choose_profession` 是小模型可选择的高层决策，不是新的机械菜单执行器。透明桥从实时
`LevelUpMenu` 读取两个原生职业的 ID、标题和描述，并公开玩家已选职业与 `newLevels`；任何字段缺失、选项数量
不是二、菜单身份漂移或待处理升级不匹配都在上游失败关闭。两个职业候选共享互斥决策键，因此同一日计划只能
选择其中一个。

DailyPlan 把决定编译为现有 `close_menu` 阶段，动作队列继续只产生唯一 `executor.close_menu`。运行层遵循
锁定版 `LevelUpMenu` 的原生职业加入、即时 perk 和待处理升级移除顺序，回执对比职业、`newLevels`、生命和体力；
不得增加第二套职业执行器，也不得让恢复候选在职业菜单打开时抢占业务候选。

隐藏静默隔离矩阵 `runtime-profession-choice-20260811-203159` 覆盖 30/30 原版职业 ID；补齐 10 级战斗分支所需
5 级前置即时 perk 后，`runtime-profession-choice-20260811-203610` 再覆盖 6/6 战斗职业，并验证 Fighter 与
Defender 的最大生命增量。该动作进入训练准入后，总计为 114 registered / 177 semantic / 113 compiler-bound /
63 catalogued-blocked / 40 five-gate / 27 allowlist；Product Executor 仍为 0。下一主切片为 `mail.process_letter`，矿井电梯仅做
既有普通矿井链的复用核对，不另造矿井系统。
## 2026-08-12 建筑换肤闭环（EVD-249 已闭合）

`buildings.change_skin` 是小模型的外观策略出口，不是第二套 Robin 菜单系统。模型必须明确给出建筑地点、类型、坐标、目标皮肤和外观理由；透明桥负责读取当前皮肤、原生可用皮肤顺序、条件、权限、入口类型、最短点击方向与次数，以及换肤会重置三组油漆颜色这一副作用。缺少明确意图、权限、服务状态或实时菜单顺序发生漂移时，上游直接排除或执行前失败关闭。

DailyPlan 只负责滚动到 Robin 服务点并生成一次终端动作，动作队列只生成唯一 `executor.change_building_skin`。运行时通过原生 `Carpenter -> Construct -> Paint -> 建筑点击 -> BuildingSkinMenu` 完成；生产执行器不得直接写 `skinId`、油漆状态或建筑集合。EVD-249 在 E 盘隔离存档中完成 Pet Bowl 默认皮肤到 `Stone Pet Bowl` 的原生验证，精确使用一次 `next`，并验证返回 ScienceHouse、目标皮肤和三组默认油漆标志。

当前看板为 `122 registered / 180 semantic / 121 compiler-bound / 49 five-gate / 32 training allowlist / 0 Product Executor`；原生分母仍为 `320 surfaces / 428 branches / 150 map tokens` 且三类 blocking 均为 0。真实 full 快照覆盖 112 个必需字段、blocking 0，KnowledgeCompiler 为 585/585、blocking 0。证据只覆盖已声明的 Pet Bowl 分支；`buildings.paint` 仍是下一独立动作切片，必须复用本链的 Robin、建筑目标选择和菜单生命周期，不得复制第二套系统。

## 2026-08-27 彩虹尽头奖励闭环（EVD-268）

`rewards.claim_pot_of_gold` 已从待办分母替换为唯一高层实现：透明桥实时发布春 17 日 Forest 精确对象、四个站位、年份奖励数量、春 18 日失效与原生输出契约；候选、DailyPlan 和动作编译器在上游排除错误日期、缺失对象、菜单占用与不可达站位。小模型只发出领取意图，所有机械字段由新快照重绑定。

生产运行只复用共享 BFS 并调用原生 `GameLocation.checkAction`。满背包不会阻止领取，金币和帽子先进入 debris，后续只交给现有 `executor.pickup_debris`；禁止新增直接背包转移或第二套奖励拾取实现。E 盘隐藏静音运行在第二年验证 9 个金币与 1 顶帽子精确守恒。当前权威状态为 `143 registered / 199 semantic / 142 compiler-bound / 69 five-gate / 37 allowlist / 56 catalogued blocked / 0 Product Executor`，full 快照 `128 required / 112 readable / 16 contextual / 0 blocking`。下一切片为 `mining.choose_dwarf_statue_power`。

## 2026-08-27 矮人王雕像能力选择闭环（EVD-269）

`mining.choose_dwarf_statue_power` 已从待办分母替换为唯一高层实现。透明桥按原生日种子发布两个不同选项、五类真实效果、采矿精通门、已有全天 buff 锁、当前地图精确基础雕像和相邻站位。模型只负责在两个真实候选中选 `power_id`；编译器保留该策略选择，并覆盖模型提供的所有机械字段。

运行时复用共享 BFS 和原生对象/菜单 API，不直接写生产 buff。普通矿井、骷髅洞、火山和地图外石头继续消费同一个 `dwarfStatue_*` 状态，不复制执行器。隐藏静音 E 盘运行对当天两个菜单项均验证唯一 buff 和菜单关闭。当前权威状态为 `144 registered / 199 semantic / 143 compiler-bound / 70 five-gate / 38 allowlist / 55 catalogued blocked / 0 Product Executor`，full 快照 `129 required / 113 readable / 16 contextual / 0 blocking`。下一切片为 `rewards.claim_statue_blessing`，可复用对象交互与菜单生命周期，但必须独立反编译其随机规则和七种祝福。

## 2026-08-27 祝福雕像领取闭环（EVD-270）

`rewards.claim_statue_blessing` 已从待办分母替换为唯一高层实现。它不要求小模型选择祝福：原生日期种子已唯一决定结果，模型只输出无参数领取目标。透明桥与编译器冻结农业精通、日锁、天气/节日导致的 `6/7` 分母、当天祝福、七种原生效果、精确雕像和相邻站位；任何日期、分母、对象、站位或 buff 漂移都失败关闭。

## 2026-08-27 House Plant 轮转闭环（EVD-271）

`world.rotate_house_plant` 已从待办分母替换为一个显式授权的高层装饰动作。候选按当前地图每一盆精确基础 House Plant 展开，模型只负责选择目标盆；编译器重绑所有机械字段。永久 `ItemId/QualifiedItemId` 与可视 `ParentSheetIndex` 分开建模，防止把轮转误记为物品身份变化。

执行器固定空手语义，并保留原生地点层的边界行为：0..6 各调用对象一次并前进一帧；7 的首次对象调用回到 0 且返回 false，地点层再次调用后最终到 1。运行器只发一次 `GameLocation.checkAction`，不自己模拟 `%8`。四向不可通行对象包围会触发原生破坏性 `performToolAction(null)` 前导分支，因此透明站位、上游候选和运行时双检均在该条件下失败关闭。隐藏静音 E 盘 8/8 通过后，当前权威状态为 `146 registered / 199 semantic / 145 compiler-bound / 72 five-gate / 40 allowlist / 53 catalogued blocked / 0 Product Executor`。本动作不进入自主日计划；下一闭环为 `farming.collect_slime_ball`。

运行时只复用共享 BFS 并调用原生对象交互；七种效果继续由现有钓鱼、社交、战斗、体力和世界 critter 代码消费，不复制下游系统。隐藏静音 E 盘运行验证唯一幸运祝福与日锁。当前权威状态为 `145 registered / 199 semantic / 144 compiler-bound / 71 five-gate / 39 allowlist / 54 catalogued blocked / 0 Product Executor`，full 快照 `130 required / 114 readable / 16 contextual / 0 blocking`。下一切片为 `world.rotate_house_plant`。

## 2026-08-12 建筑涂装闭环（EVD-250 已闭合）

`buildings.paint` 是高层外观选择，不是新的机械执行系统。小模型必须明确建筑身份、涂装区域、恢复默认或自定义 H/S/L，以及外观理由。透明桥实时读取 `Data/PaintData`、权限、区域顺序、亮度边界、当前三组颜色和 Robin 服务入口；同时根据锁定版原生 284 像素滑杆公式公开每个通道的精确鼠标可达值。上游候选与队列编译均拒绝不可达整数、无效果目标、默认显示值无法解除默认标志的三元组、菜单占用和投影漂移。

DailyPlan 的语义步骤为 `paint_building_region`，但动作队列仍进入唯一 `executor.change_building_skin`。共享 `ActiveBuildingAppearanceChange` 负责 Robin 路由、Carpenter 对话、农场建筑选择和菜单收尾；参数存在 `paint_target_mode` 时才进入 `BuildingPaintMenu` 区域/滑杆/默认按钮分支。结算必须同时证明目标区域精确匹配和所有兄弟区域不变。生产代码禁止直接写 `BuildingPaintColor`；直接设定仅允许隔离 fixture 建立前态。

EVD-250 在隐藏、静音、E 盘隔离存档中完成 `Farmhouse/Building -> H180/S37/L-30` 原生全链，训练行已落盘。冻结语义分母保持 180：`123 registered + 57 catalogued_blocked`，说明本切片替换待办项而没有制造第 181 个重复动作；compiler-bound 为 122，Product Executor 仍为 0。最新 full snapshot 为 113 required、96 带来源可读、17 场景性、blocking 0；KnowledgeCompiler 585/585、blocking 0；Core 1666/1666、Backend 121/121。

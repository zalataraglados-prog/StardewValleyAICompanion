# StardewAI 当前工作

## 2026-08-29 当前权威检查点：EVD-294

- `festival.play_slingshot_game` 已闭合秋季 16 日展览会靶场的透明读取、Stardrop 有界需求候选、DailyPlan、fresh 编译重绑定、类型化请求和完整原生 TargetGame 回执。模型只决定是否花费 50g 开始一轮；自动候选仅在“当前星币 + 尚未领取的展览陈列奖励”仍不足 2000 星币且 Stardrop 未获得时出现。
- 透明桥实时发布 Buildings 501/502、50g 入场费、1000/50000/1000/16100ms 四段时序、79 个锁定目标、实时目标私有状态、临时 `(W)32` 与 `(O)390 x999`、命中/准确率/倍率/奖励公式、全部 Fair 商店行和共享星币缺口。固定规则来自锁定 1.6.15 反编译；活动实例与目标状态均实时核对。
- 执行器复用共享 BFS 和普通矿井弹弓唯一的 `SlingshotAimPatch`，经 `Event.checkAction -> DialogueBox.receiveLeftClick -> TargetGame` 进入原版小游戏，只调用原生按下/蓄力/释放输入。它不写 Money、计时器、目标、score、accuracy、festivalScore 或库存；原版拥有弹药、碰撞、命中、得分、奖励和清理。
- 隐藏静音 E 盘隔离运行 `runtime-fair-slingshot-game-20260829-225533` PASS：48 次原生发射、48 次有效命中，raw score `95`、accuracy `102`、final score `380`，原版高分封顶奖励 `500` 星币；50g 扣款、节庆返回、临时弹弓/弹药清理均通过。
- 最新 full snapshot schema 为 `142 required / 126 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `169 registered / 200 semantic / 168 compiler-bound / 95 five-gate / 43 training allowlist / 31 catalogued blocked / 0 Product Executor`。完整回归为 Core `2013/2013`、Backend `138/138`、Release `0 warnings / 0 errors`。下一语义切片固定为 `festival.play_strength_game`。

## 2026-08-29 当前权威检查点：EVD-293

- `festival.play_fishing_game` 已闭合秋季 16 日展览会钓鱼小游戏的透明读取、上游需求候选、DailyPlan、fresh 编译重绑定、类型化请求和原生执行回执。自动候选只在展览会进行中、玩家有 50g，且“当前星币 + 尚未领取的展览陈列奖励”仍不足以购买未获得的 `(O)434` Stardrop 时出现；其他商店行保持透明，但不会形成无限刷小游戏的自动需求。
- 透明桥实时发布 Buildings 503/504 交互图块、50g 入场费、100000ms 游戏时长、11100ms 结果时长、临时 Bamboo Pole / `(O)690` bait / `(O)687` tackle、原版得分与星币公式、当前小游戏私有计时器、全部星币商店行、Stardrop 价格与投影缺口。字段来自锁定 1.6.15 反编译和实时对象，不按 Wiki 示例推断。
- 执行器复用共享 BFS/连续移动与普通钓鱼的 BobberBar 控制器，只经 `Event.checkAction -> DialogueBox.receiveLeftClick -> Event.answerDialogue -> FishingGame` 进入原版小游戏；输入在 `UpdateTicking` 物理更新前发出，不直接写 Money、score、perfections、starTokensWon 或 festivalScore。随机完美率作为训练反馈，执行可靠性由合法输入、完整时序、精确原版公式、星币到账、节日返回和临时钓具清理共同验证。
- 隐藏静音 E 盘隔离运行 `runtime-fair-fishing-game-20260829-221047` PASS：原版 100 秒会话钓到 `5` 条有效鱼且 `5/5` 完美，最终 `364` 分、`432` 星币；50g、原版三连完美翻倍/奖励公式、返回图块和临时状态全部通过。首轮烟测同时抓到并修正了错误的普通对话调用，最终入口使用真实节日菜单点击。
- 最新 full snapshot schema 为 `141 required / 125 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `167 registered / 199 semantic / 166 compiler-bound / 93 five-gate / 42 training allowlist / 32 catalogued blocked / 0 Product Executor`。完整回归为 Core `2008/2008`、Backend `138/138`、Release `0 warnings / 0 errors`。下一语义切片固定为 `festival.play_slingshot_game`。

## 2026-08-29 当前权威检查点：EVD-292

- `festival.manage_grange_display` 已闭合秋季 16 日星露谷展览会陈列的透明读取、确定性最优九件选择、上游候选、DailyPlan、fresh 编译重绑定和原生执行回执。高层动作负责“为一等奖准备陈列/评审后取回”，进入策略训练；`executor.manage_grange_display` 每次只执行一件物品的放入或取回，严格为 `ExecutorCalibration`。
- 透明桥实时读取共享 `FarmerTeam.grangeDisplay`、玩家库存、原生实际售价 `sellToStorePrice(-1L)`、品质、八类多样性、九件数量分、Mayor 短裤惩罚、评审状态、展台交互图块和 `grangeMutex`。优化器按原版公式计算当前分与可达最优分，不依赖文档示例或静态物价。
- 生产执行器复用共享 BFS/移动状态机，到达相邻格后只调用节日 `Event.checkAction` 打开原生 `StorageContainer`，通过菜单点击完成一次变更并等待共享互斥锁释放；禁止直接写展台、库存、评分或评审状态，异常列表形状也只会 fail-closed。动作不会替玩家启动评审。
- 隐藏静音 E 盘隔离运行 `runtime-grange-display-20260829-203602` 为 `10/10` PASS：连续九次原生放入把陈列分数提升到 `124`，超过一等奖阈值 `90`，模拟评审后再原生取回一件；每次均验证菜单入口、互斥锁获取/释放、单次展台变更、库存守恒和评分状态。
- 最新 full snapshot schema 为 `140 required / 124 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `165 registered / 198 semantic / 164 compiler-bound / 91 five-gate / 41 training allowlist / 33 catalogued blocked / 0 Product Executor`。完整回归为 Core `2003/2003`、Backend `138/138`、Release `0 warnings / 0 errors`。下一语义切片固定为 `festival.play_fishing_game`。

## 2026-08-29 当前权威检查点：EVD-291

- `executor.use_warp_totem` 已闭合五种原版传送图腾 `(O)688/(O)689/(O)690/(O)261/(O)886` 的透明读取、fresh 路由重绑定、动作编译、类型化请求、共享原生库存物品使用和延迟回执。锁定 1.6.15 反编译、`Data/Objects`、`Data/CraftingRecipes` 与官方 Wiki，确认 Farm/Mountain/Beach/Desert/Island 全部可达变体及其消耗语义。
- 透明桥逐库存行发布精确物品身份、公共使用门、2000ms 动画和 1000ms 回调合同，并实时解析 Farm `WarpTotemEntry`/农场类型回退、固定目的地、地图宽度修正、主动节日入口和按顺序应用的被动节日地图替换。节日前误消耗但不传送、联机节日 ReadyCheck、已在精确目的地和基础使用门失败均在上游排除。
- 执行层只调用 `UseInventoryObjectNative`，不直接调用 `Game1.warpFarmer`，不写位置、库存、可见性、无敌或音频。回执要求一个精确图腾 `2->1`、原生首段至少 68 个效果精灵、目的地图/格、节日路由以及角色显示/无敌/移动状态全部结算。
- 隐藏静音 E 盘隔离运行 `runtime-warp-totem-20260829-181523` 五变体 `5/5` PASS：Farm `48,39`、Mountain `31,20`、Beach `20,4`、Desert `35,43`、IslandSouth `11,11` 均由原生回调抵达，每例 `68` 个即时精灵、约 `281 tick` 后完成并恢复控制。
- 最终回归为 Core `1998/1998`、Backend `138/138`、Release 全解决方案 `0 warnings / 0 errors`。最新 full snapshot schema 为 `139 required / 123 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `163 registered / 197 semantic / 162 compiler-bound / 89 five-gate / 40 training allowlist / 34 catalogued blocked / 0 Product Executor`。下一语义切片固定为 `festival.manage_grange_display`。

## 2026-08-29 当前权威检查点：EVD-290

- `executor.use_treasure_totem` 已闭合原版宝藏图腾 `(O)TreasureTotem` 的透明读取、fresh 候选环重绑定、动作编译、类型化请求、共享原生库存物品使用和严格回执。锁定 1.6.15 反编译、`Data/Objects`、`Data/CraftingRecipes` 与官方 Wiki，确认原生效果是中心周围按四舍五入距离 3 生成至多 16 个 `(O)590` 宝藏点，并递增 `TreasureTotemsUsed`。
- 透明桥逐格发布全部 16 个候选、放置/占用/前景层/灌木/可挖或冬季草地门、最终可生成集合、使用前计数和指纹。原生代码中形似 Forest 的判断实际比较物品名 `Treasure Totem`，对基础物品恒为 false；合同保留该真实操作数与结果，不按示例修正源码语义。
- 上游在公共物品门失败、室内或可生成集合为空时直接排除，策略只决定何时及在哪里使用。机械执行器只调用 `UseInventoryObjectNative`，不直接写地图、库存或世界计数；新宝藏点继续交给既有 `ArtifactSpots` 透明读取和 `executor.clear_obstacle` 挖掘链，不建立第二套采集实现。
- 隐藏静音 E 盘隔离运行 `runtime-treasure-totem-20260829-173258` PASS：Farm 中心 `(6,12)` 原生生成 `16/16` 个宝藏点，槽 9 图腾 `2->1`，`TreasureTotemsUsed 0->1`，地点宝藏点 `5->21`。
- 最终回归为 Core `1976/1976`、Backend `138/138`、Release 全解决方案 `0 warnings / 0 errors`。最新 full snapshot schema 为 `138 required / 122 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `162 registered / 197 semantic / 161 compiler-bound / 88 five-gate / 40 training allowlist / 35 catalogued blocked / 0 Product Executor`。下一语义切片固定为 `executor.use_warp_totem`。

## 2026-08-29 当前权威检查点：EVD-289

- `executor.use_return_scepter` 已闭合原版回城魔杖 `(T)ReturnScepter` 的透明读取、fresh 住宅/落点重绑定、动作编译、类型化请求、原生即时工具调用和严格异步回执。锁定 1.6.15 `Data/Tools` 与 `Wand.cs` 确认其为不可丢失、不可出售、非消耗型 `Wand`，入口必须经过 `Farmer.BeginUsingTool -> Tool.beginUsing(InstantUse) -> Game1.toolAnimationDone -> Wand.DoFunction`。
- 透明桥实时调用 `Utility.getHomeOfFarmer(player).getFrontDoorSpot()`，分别发布房主 `FarmHouse` 与农场工 `Cabin` 的精确门前格，不把主屋常量错误外推给联机角色。住宅缺失、浴衣、桥上、执行器瞬态门禁以及已在精确落点时均在上游排除；全部原生物品槽保持可读且栈前后均为 1。
- 原生时序为 12 个随机烟雾精灵加 17 个横向轨迹精灵、`wand` 音效、1000ms 回调和 2000ms `freezePause`。源码顺序表明 `Wand` 暂时写 `CanMove=false` 后，`Tool.beginUsing` 在返回前将其恢复为 true，实际输入冻结由 `freezePause` 保持；透明合同和运行断言按完整调用栈而非孤立方法体记录。
- 运行层只选择精确 `Wand` 并调用 `BeginUsingTool`，不直接调用 `warpFarmer`，也不写位置、显示、无敌、移动或库存。隐藏静音 E 盘隔离运行 `runtime-return-scepter-20260829-162520` PASS：从 Farm `(66,19)` 原生抵达主屋门前 `(64,15)`，同步观察 29 个精灵，125 tick 后状态结算，原槽 `(T)ReturnScepter` 仍为 stack 1。
- 官方 Wiki 二次确认回城魔杖不消耗、房主回主屋门前、农场工回自己的小屋门前；本地反编译仍是字段、门禁和时序的主证据。
- 最终回归为 Core `1964/1964`、Backend `138/138`、Release 全解决方案 `0 warnings / 0 errors`。最新 full snapshot schema 为 `137 required / 121 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `161 registered / 197 semantic / 160 compiler-bound / 87 five-gate / 40 training allowlist / 36 catalogued blocked / 0 Product Executor`。下一语义切片固定为 `executor.use_treasure_totem`。

## 2026-08-29 当前权威检查点：EVD-288

- `executor.use_rain_totem` 已闭合原版雨水图腾 `(O)681` 的透明读取、fresh 参数重绑定、动作编译、类型化请求、共享原生库存物品使用和严格异步回执。锁定 1.6.15 反编译确认 `Object.performUseAction -> rainTotem` 的上下文许可、`RainTotemAffectsContext` 路由、默认上下文节日门、天气写入、2000ms 动画与提示对话完整分支。
- 透明桥分别发布“分支判断目标上下文”和“实际天气状态归属上下文”，因此支持模组上下文重定向而不会错记写入对象。默认/沙漠重定向/姜岛分支分别绑定 Default/Default/Island；运行层只调用共享 `UseInventoryObjectNative`，不直接写天气、库存、精灵或音频。
- 官方 Wiki 的季节首日提示触发了源码复核：默认上下文即时写入 Rain 后，换日仍会经过 `Game1.getWeatherModificationsForDate`。透明字段和执行门现绑定明日日期、最终有效天气及有效性；季节首日、开局固定天气、绿雨、夏季固定风暴、主动/被动节庆等覆盖会在消耗前排除。已是 Rain 也在上游排除。
- 隐藏静音 E 盘隔离运行 `runtime-rain-totem-20260829-153640` 为 4/4 PASS：默认、沙漠重定向和姜岛均原生消耗 `2->1` 并写入正确天气实例；节日前分支保持 `stack=2` 并拒绝执行。原生 2000ms 信息对话由输入覆盖关闭，随后通过精确 `Farmer.canMoveNow` 回调恢复控制。
- 最终回归为 Core `1949/1949`、Backend `138/138`、Release 全解决方案 `0 warnings / 0 errors`。
- 最新 full snapshot schema 为 `136 required / 120 readable with provenance / 16 contextual / 0 blocking`。权威对账为 `160 registered / 197 semantic / 159 compiler-bound / 86 five-gate / 40 training allowlist / 37 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `executor.use_return_scepter`。

## 2026-08-29 当前权威检查点：EVD-287

- `executor.use_monster_musk` 已闭合原版怪兽香水 `(O)879` 的透明读取、fresh 参数重绑定、动作编译、类型化请求、共享原生库存物品使用和严格异步回执。锁定 1.6.15 反编译确认原生入口在 750ms 动画回调中调用 `Object.MonsterMusk -> Farmer.applyBuff("24")`，`BuffManager.Apply` 对同 ID 先移除再替换，不叠加旧时长。
- 透明桥实时发布全部精确库存行、公共物品使用门、Buff 24 数据定义与当前实例剩余时长、1750ms 冻结/750ms 回调/1400ms 后续动画合同，以及普通矿井与火山地牢的双倍怪物生成消费者。读取不推进三枚紫色精灵的随机 X 速度。
- 运行层只调用共享 `UseInventoryObjectNative`，不直接施加 Buff、不广播精灵、不播放音频、不写库存。回执等待 Buff 新实例、600000ms 总时长、精确一次消耗、方向 2 和完整原生冻结结算；已有 Buff 的快照到执行时间只允许最多 5000ms 的单调递减漂移。
- 隐藏静音 E 盘隔离运行 `runtime-monster-musk-20260829-141932` 双分支 PASS：首次施加库存 `2->1`，刷新分支 `1->0`；两次均观测到新 Buff 24 实例，刷新把约 `599968ms` 恢复到约 `599984ms`。
- 最终回归为 Core `1932/1932`、Backend `138/138`、Release 全解决方案 `0 warnings / 0 errors`。
- 最新 full snapshot schema 为 `135 required / 119 readable with provenance / 16 contextual / 0 blocking`。权威对账为 `159 registered / 197 semantic / 158 compiler-bound / 85 five-gate / 40 training allowlist / 38 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `executor.use_rain_totem`。

## 2026-08-29 当前权威检查点：EVD-286

- `executor.use_horse_flute` 已闭合原版马笛 `(O)911` 的透明读取、fresh 参数重绑定、动作编译、类型化请求、原生 `Object.performUseAction` 与严格延迟回执。锁定 1.6.15 反编译确认起始与 1500ms 回调各检查一次 `Utility.GetHorseWarpRestrictionsForFarmer`，远程分支再通过 team event、马匹 mutex 和 `Game1.warpCharacter` 完成原生召回。
- 透明桥发布完整库存身份/临时不可见状态、四位限制掩码与错误优先级、召回矩形、全地图拥有马匹的 GUID/位置/骑手/mutex 状态、邻近分支、朝向及延迟合同。指纹绑定玩家位置/朝向、限制、马匹身份与库存可见性；`Utility.findHorseForPlayer` 原生遍历全部已加载/生成地点，不依赖模型猜测马厩位置。
- 运行层只调用 `performUseAction` 和原生调用者的 `reduceActiveItemByOne`；不直接调用 team warp event、不写马匹位置。远程召唤强制朝下并等待精确拥有马匹到达玩家格；已有马在一格邻域内是成功无传送分支并保留原朝向。两条分支都验证可复用马笛堆叠不变。
- 隐藏静音 E 盘隔离运行 `runtime-horse-flute-20260829-120232` 双分支 PASS：透明投影、执行重绑和原生 team event 选中同一马厩马匹；该 GUID 从 `Farm 56,14` 原生召回 `40,7`，随后邻近使用保持 `40,7`，两次库存均为 `stack=1`。
- 最终回归为 Core `1921/1921`、Backend `138/138`、Release 全解决方案 `0 warnings / 0 errors`。
- 最新 full snapshot schema 为 `134 required / 118 readable with provenance / 16 contextual / 0 blocking`。权威对账为 `158 registered / 197 semantic / 157 compiler-bound / 84 five-gate / 40 training allowlist / 39 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `executor.use_monster_musk`。

## 2026-08-29 当前权威检查点：EVD-285

- `executor.use_firework` 已闭合三种原版烟花 `(O)893/(O)894/(O)895` 的透明读取、fresh 参数重绑定、动作编译、类型化请求、共享相邻移动、原生放置和严格瞬时回执。锁定 1.6.15 分支确认三者映射 `fireworkType=0/1/2` 与源图 X=`256/272/288`，共用 2400ms 引信和延迟火箭。
- 透明桥只在 full profile 且库存存在烟花时计算当前加载地图合法区间，并发布精确临时精灵占位格。读取阶段不调用 `Game1.random`；只发布火箭 ID `20..30` 与 Y 加速度 `-0.36..-0.27`、步长 `0.01` 的完整结果域。
- 动作严格为 `PlayerCommandOnly`：不进入自主候选或训练 allowlist。运行层只复用 `CanPlaceInventoryObjectNative -> PlaceInventoryObjectNative`，不直接广播精灵、播放音频或修改库存；执行回执要求 5 个新原生精灵、目标格 2 个主体精灵、精确三色身份、随机结果域和单物品消耗全部成立。
- 隐藏静音 E 盘隔离运行 `runtime-firework-20260829-111607` 三分支 PASS；观测火箭 ID 为 `22/20/25`，加速度为 `-0.27/-0.34/-0.31`，均在反编译域内。
- 最新 full snapshot schema 为 `133 required / 117 readable with provenance / 16 contextual / 0 blocking`。权威对账为 `157 registered / 197 semantic / 156 compiler-bound / 83 five-gate / 40 training allowlist / 40 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `executor.use_horse_flute`。

## 2026-08-29 当前权威检查点：EVD-284

- `executor.read_secret_note` 已闭合普通秘密纸条 `(O)79` 与日记残页 `(O)842` 的完整透明读取、fresh 参数重绑定、动作编译、类型化请求、共享原生物品使用和严格回执。锁定 1.6.15 反编译确认普通纸条用 `Utility.CreateRandom(gameId, playerId, unseenCount * 777)` 从原生未读顺序确定性抽取，日记残页取最小未读编号。
- 透明桥发布完整 `Data/SecretNotes` 目录、原文与 SHA-256、已读集合、每类未读原生顺序、选择结果、图片/文本菜单预期以及 10/23 号纸条的任务副作用。菜单投影新增 `secret_note_image` 和 `which_bg`，输出不再只凭运行时返回值判断。
- 书籍和纸条现共用唯一 `UseInventoryObjectNative` 调用原生 `performUseAction` 并仅在成功时执行原生调用者的 `reduceActiveItemByOne`；纸条执行器不直接写已读集合、任务、菜单或库存。菜单为空是产品安全门，不伪装成原版纸条分支条件。
- 隐藏静音 E 盘隔离运行 `runtime-secret-note-smoke-20260829-103012` 四分支 PASS：多未读集合确定性选择 18、10 号新增任务 30、23 号新增任务 29、1001 号日记残页无任务副作用，四例均精确已读、原生 LetterViewerMenu 和单物品消耗。
- 最新 full snapshot schema 为 `132 required / 116 readable with provenance / 16 contextual / 0 blocking`。权威对账为 `156 registered / 197 semantic / 155 compiler-bound / 82 five-gate / 40 training allowlist / 41 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `executor.use_firework`。

## 2026-08-29 当前权威检查点：EVD-283

- `executor.plant_grass` 已闭合普通草种 `(O)297` 与蓝草种 `(O)BlueGrassStarter` 的透明读取、精确地块重绑定、动作编译、v1 请求绑定、共享相邻移动、原生放置和后状态回执。锁定 1.6.15 反编译确认两条分支分别创建 `Grass(1,4)` 与 `Grass(7,4)`，均播放 `dirtyHit`。
- 上游用途/布局规划器必须提供 `grass_layout_reason` 和精确目标/站位；执行器不会推断牧草布局。编译器从 fresh full snapshot 重绑草种变体、槽位、堆叠、合法区间、可达性和投影指纹，任一漂移均失败关闭。
- 生产运行只调用共享 `Utility.playerCanPlaceItemHere -> Utility.tryToPlaceItem`，不直接写 `terrainFeatures`、`grassType`、`numberOfWeeds` 或库存。`current_location.terrain_features` 现直接发布草类型与 weeds 数，支持严格输出复核。
- 隐藏、静音、E 盘隔离运行 `runtime-grass-placement-20260829-093605` 双分支 PASS：type 1/type 7 均为四株初始 weeds，库存精确减一，透明后状态一致。
- 最新 full snapshot schema 为 `131 required / 115 readable with provenance / 16 contextual / 0 blocking`。权威对账为 `155 registered / 197 semantic / 154 compiler-bound / 81 five-gate / 40 training allowlist / 42 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `executor.read_secret_note`。

## 2026-08-29 当前权威检查点：EVD-282

- `world.tune_drum_block` 已闭合透明读取、显式玩家候选、DailyPlan、新鲜字段重绑定、动作编译、v1/v2 类型请求、共享原生运行和持久化音色回执。锁定 1.6.15 反编译确认 `(O)463` 每次右键将 `preservedParentSheetIndex` 按 `(current + 1) % 7` 推进，并播放 `drumkit0..6` 对应音色。
- 该动作严格为 `PlayerCommandOnly`，默认策略候选和训练 allowlist 均排除。路过播放由独立 `farmerAdjacentAction` 入口处理，不得替代调音。Drum 与 Flute 只共用一个 `NoteBlockTuning` 移动/原生交互状态机，各自保留独立透明字段、验证规则和 E3 证据。
- 隐藏、静音、E 盘隔离运行 `runtime-drum-block-20260829-040623` 为 PASS：`6->0`、`drumkit0`、`shakeTimer=200`、`scale.Y=1.3`、原生返回 true、对象身份与槽位保持，只加载 TransparentBridge 与 RuntimeTestHarness。
- 最新权威对账为 `154 registered / 197 semantic / 153 compiler-bound / 80 five-gate / 40 training allowlist / 43 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `executor.plant_grass`。

## 2026-08-29 当前权威检查点：EVD-281

- `world.tune_flute_block` 已闭合透明读取、显式玩家候选、DailyPlan、新鲜字段重绑定、动作编译、v1/v2 类型请求、共享原生运行和持久化音高回执。锁定 1.6.15 反编译确认 `(O)464` 每次右键按 `0..2400`、步长 `100`、共 25 档推进，特殊边为 `2300->2400->0`。
- 该动作严格为 `PlayerCommandOnly`。编译器选择空槽或工具槽以禁用手持物音色覆盖，执行一次原生 `GameLocation.checkAction`；路过触发的 `farmerAdjacentAction` 是独立播放入口，不得混入调音命令或训练。
- 隐藏、静音、E 盘隔离运行 `runtime-flute-block-20260829-034718` 为 PASS：`2300->2400`、`shakeTimer=200`、`scale.Y=1.3`、原生返回 true、对象身份与槽位保持，只加载 TransparentBridge 与 RuntimeTestHarness。
- 最新权威对账为 `153 registered / 197 semantic / 152 compiler-bound / 79 five-gate / 40 training allowlist / 44 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `world.tune_drum_block`，继续复用同一共享移动/交互骨架并单独锁定七档鼓声音色循环。

## 2026-08-29 当前权威检查点：EVD-280

- `farming.read_farm_computer_report` 已闭合透明读取、显式玩家候选、DailyPlan、机械字段新鲜重绑定、动作编译、v1/v2 类型请求、原生延迟对话和报告摘要回执。锁定 1.6.15 反编译确认原生 `(BC)239` 以对象所在地点的 `GetRootLocation()` 为报告根，精确读取作物、空闲耕地、成熟/未浇水作物、温室、采集物、已完成机器、干草与农场洞穴状态。
- 透明桥直接发布结构化报告字段及本地化原生报告 SHA-256；策略层无需打开菜单才能获知这些信息。该动作严格为 `PlayerCommandOnly`，仅显式玩家请求能生成，默认候选和训练 allowlist 均排除。
- 生产运行复用唯一 `NativeObjectInteractionMovement`，到站后只调用一次 `GameLocation.checkAction`，等待原生 500ms 延迟生成 `DialogueBox`；不合成报告、不直接设置菜单，并验证对象身份、精确报告摘要和工具栏槽恢复。显式命令完成后保留报告供玩家阅读。
- 隐藏、静音、E 盘隔离运行 `runtime-farm-computer-20260829-031326` 为 PASS：`native_handled=true`、即时 `shakeTimer=500`、`freezePause=500`、延迟菜单为 `DialogueBox`，报告摘要与透明投影一致，且只加载 TransparentBridge 与 RuntimeTestHarness。
- 最新权威对账为 `152 registered / 197 semantic / 151 compiler-bound / 78 five-gate / 40 training allowlist / 45 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens` 且 blocking 为 `0`，另有 `2` 个分母外兼容占位。
- 下一语义切片固定为 `world.tune_flute_block`：先实时反编译音高调整、输入方向与持久化语义，再确定 PlayerCommandOnly 边界；继续复用共享对象移动/交互，不复制第二套执行器。

## 2026-08-29 当前权威检查点：EVD-279

- `movement.use_mini_obelisk` 已闭合透明读取、校准候选、DailyPlan、机械字段重绑定、动作编译、v1/v2 类型请求、原生运行和延迟传送回执。锁定 1.6.15 反编译确认原生分支按 `location.objects.Pairs` 的实际枚举顺序取前两个 `(BC)238`，以 `Vector2.Zero` 为哨兵；从交互站位选择欧氏距离更远的一端，平局取第二端，再按下、左、右、上的顺序选择第一个 `IsTileBlockedBy(All,All)==false` 落点。
- 透明桥逐对象发布实际原生配对序号、两端坐标、每个安全站位对应的目的端与落点。第三个方尖碑、非基础对象、缺失配对、无落点和破坏性对象陷阱均在上游失败关闭；编译器从新鲜快照重绑全部机械字段，不能接受模型伪造的配对、目标或落点。
- 生产运行复用唯一 `NativeObjectInteractionMovement` 与共享 BFS，到站后只调用一次 `GameLocation.checkAction`。生产文件不写 `Farmer.Position`、不调用 `setTileLocation`；它等待原生 50ms 延迟回调完成，并以精确落点、两端引用/身份不变、角色重新显示、菜单为空和工具栏槽恢复验收。
- 隐藏、静音、E 盘隔离运行 `runtime-mini-obelisk-20260829-011139` 为 PASS：`native_handled=true`，预期/实际落点均为 `(1,2)`，配对坐标和对象引用不变，只加载 TransparentBridge 与 RuntimeTestHarness。该动作严格为 `ExecutorCalibration`，不会进入默认策略候选或训练 allowlist，运行样本范围为 `executor_calibration_only_not_strategy_desire`。
- 最新权威对账为 `151 registered / 197 semantic / 150 compiler-bound / 77 five-gate / 40 training allowlist / 46 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens` 且 blocking 为 `0`，另有 `2` 个分母外兼容占位。回归为 Core `1863/1863`、Backend `134/134`。
- 下一语义切片固定为 `farming.read_farm_computer_report`。先实时反编译原生 Farm Computer 分支及数据来源，再决定候选/训练分类；不得把“能读取报告”直接解释成策略收益，也不得复制第二套对象移动器。

## 2026-08-28 当前权威检查点：EVD-277 / EVD-278

- Issue #31 的“按共享原生底层批量推进”已采纳，但批量只表示复用同一移动/菜单状态机；每个语义动作仍保留独立透明字段、前后条件、运行夹具和 E3 证据，禁止把一条运行结果外推给整组动作。
- `Lantern` 与 `Raft` 均确认为原版不可达的删减工具，并转入显式兼容占位目录。事实纠正：锁定 1.6.15 的 37 项 `Data/Tools` **包含 Lantern、没有 Raft**；二者均只有残留类型/调试构造或兼容状态，没有正常存档获取路径。它们不进入原版语义分母、候选、训练和待实现列表，但占位保留原生类型与未来 MOD 适配入口。
- `animals.collect_auto_grabber_contents` 已闭合透明读取、上游排除、DailyPlan、机械字段重绑定、动作编译、原生菜单运行和物品守恒回执。它与喂食斗只共享 `NativeObjectInteractionMovement`；收取本身走精确 `(BC)165 -> ItemGrabMenu -> receiveLeftClick`，生产代码不直接改背包或 held Chest。
- 隐藏、静音、E 盘隔离运行 `runtime-auto-grabber-20260828-165346` 为 PASS：原生菜单转移 `2` 个堆栈、共 `5` 件物品，Auto-Grabber 余量 `0`，`native_handled=true`，对象/Chest 身份和选中槽均保持正确。
- 最新权威对账为 `150 registered / 197 semantic / 149 compiler-bound / 76 five-gate / 40 training allowlist / 47 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens` 且 blocking 为 `0`，另有 `2` 个分母外兼容占位。回归为 Core `1848/1848`、Backend `132/132`。
- 下一语义切片为 `movement.use_mini_obelisk`，继续处理 `Object.checkForAction` 组；只复用已验证的移动/交互底层，并独立锁定成对方尖碑、目标选择、传送后位置与新鲜快照终止条件。

## 2026-08-28 当前权威检查点：EVD-275 / EVD-276

- `StardewValley.Tools.Raft` 已确认为锁定 1.6.15 的不可达删减类型。类、`Farmer.isRafting` 和兼容移动分支仍存在，但 37 项 `Data/Tools` 没有 Raft，全部运行时内容资产也没有获取或事件入口，源码除类型自身 `GetOneNew` 外没有外部构造或工厂路径。当前分类已由上方 EVD-277 接管为分母外兼容占位；`executor.use_raft` 不进入候选、训练或原版待实现目录。
- `animals.withdraw_feed_hopper_hay` 已闭合透明读取、上游候选、DailyPlan、机械字段重绑定、动作编译、原生运行和守恒回执。透明投影精确发布根地点料仓干草、动物数、动物屋容量、已摆干草、未喂动物、原生取草量、背包接纳和安全站位；没有未喂动物、料仓为空、料槽无容量或背包不能接纳时在上游排除。
- 生产运行复用唯一 `NativeObjectInteractionMovement` 和共享 BFS，到站后只调用一次 `GameLocation.checkAction`。生产代码不直接写 `piecesOfHay`、不直接加背包；成功必须同时满足料仓精确减少、背包 `(O)178` 精确增加、喂食斗身份不变、菜单为空和选中槽恢复。
- 隐藏、静音、E 盘隔离运行 `runtime-feed-hopper-20260828-130723` 为 PASS：原生一次取出 `8`，料仓 `10 -> 2`、背包 `0 -> 8`，`native_handled=true`，喂食斗保留且槽位恢复；只加载 TransparentBridge 与 RuntimeTestHarness。
- 最新权威对账为 `149 registered / 198 semantic / 148 compiler-bound / 75 five-gate / 39 training allowlist / 49 catalogued blocked / 0 Product Executor`；原生分母为 `322 surfaces / 448 branches / 150 map tokens`，三类 blocking 均为 `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。回归为 Core `1844/1844`、Backend `131/131`。
- 下一语义切片按冻结待办顺序为 `animals.collect_auto_grabber_contents`；必须先核对原生菜单、互斥锁、容器库存和空容器分支，再复用唯一库存/菜单转移实现，不复制第二套容器执行器。

## 2026-08-28 当前权威检查点：EVD-274

- `world.play_singing_stone` 已闭合透明读取、显式玩家指令候选、DailyPlan 表述、机械字段重绑定、动作编译、原生运行与回执。锁定 1.6.15 反编译确认目标是基础 `StardewValley.Object` `(BC)94`，不是同名家具 `(F)1300`；原生分支执行 `Game1.random.Next(2400)` 后向下取整到百位，均匀产生 `0..2300` 共 24 种 `crystal` 音高，并把 `shakeTimer` 设为 `100`。
- `Game1.random` 是共享 RNG，透明桥只发布完整分布和 `unavailable_shared_rng_state_not_consumed`，不得读取、推进或猜测下一音高。候选只选择一个精确声音石；对象身份、相邻站位、安全工具栏槽、分布和原生契约全部由最新快照与编译器重绑定，坐标漂移时不得替换另一块石头。
- 该动作严格为 `PlayerCommandOnly`：默认候选、自主日计划和策略训练全部排除，只有 `InvocationSource=PlayerCommand` 且显式确认后可进入已有授权链。运行证据的范围是 `player_command_only_executor_evidence`，不能反推训练准入。
- 运行时与 House Plant 共用 `NativeObjectInteractionMovement`，保留原生动画/BFS，只调用一次 `GameLocation.checkAction`；生产代码不调用 `Game1.playSound`、不写 `shakeTimer=100`、不消费 RNG，并在交互前双检四向物体陷阱、目标身份、安全手持状态和槽位恢复。
- 隐藏、静音、E 盘隔离运行 `runtime-singing-stone-20260828-102438` 为 PASS：`native_handled=true`、`shake_timer=100`、`item_id=94`、`qualified_item_id=(BC)94`，选中槽恢复；只加载 TransparentBridge 与 RuntimeTestHarness。
- 权威对账为 `148 registered / 199 semantic / 147 compiler-bound / 74 five-gate / 38 training allowlist / 51 catalogued blocked / 0 Product Executor`；原生分母仍为 `322 surfaces / 448 branches / 150 map tokens` 且 blocking 均为 `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。全量回归为 Core `1839/1839`、Backend `130/130`。
- `executor.use_raft` 已从动作全集分母移除：锁定 1.6.15 虽保留 `Raft` 类、`Farmer.isRafting` 和移动兼容分支，但 37 项 `Data/Tools` 无该工具，源码也无获取、工厂、事件或外部构造入口；当前以分母外兼容占位保留原生证据，不作为候选、训练目标或原版待实现能力。
- 下一语义切片固定为 `animals.withdraw_feed_hopper_hay`：先锁定饲料斗交互、库存/料槽容量与取草数量分支，再判断是否完全复用唯一库存转移引擎；不得从历史段落回退到已闭合声音石或复制第二套转移系统。

## 2026-08-27 当前权威检查点：EVD-272 / EVD-273

- `farming.collect_slime_ball` 已闭合透明读取、逐对象候选、DailyPlan、机械字段重绑定、动作编译、原生运行与产物守恒回执。锁定 1.6.15 反编译确认自然史莱姆球必须是精确 `SlimeHutch` 中的基础 `StardewValley.Object`、`(BC)56`、`Fragility=2`；`CheckForActionOnSlimeBall` 先移除对象，再按 `DaysPlayed`、`uniqueIDForThisGame` 和目标格生成 `10..20` 个 `(O)766`，并按 `NextDouble()<0.33` 的几何分布生成 `(O)557`。
- 透明桥在 `current_location.objects[].slime_ball_collection` 发布来源类型、永久身份、随机种子、两类精确预期产量、原生返回值、生成契约、共享 debris 拾取交接和安全相邻站位。候选仅选择“收取哪一个球”；种子、产量、站位、空槽和原生调用细节均由编译器在新鲜快照上重绑定，指定坐标漂移时不得改选另一个球。
- 生产执行器复用共享 BFS 和通用破坏性四向物体陷阱检查，临时切到真实空工具栏槽位，只调用一次 `GameLocation.checkAction`。它不直接删对象、不直接造 debris，也不复制拾取器；回执以“背包 + 当前地点 debris”的 `(O)766`/`(O)557` 守恒量验证生成，剩余地面物品继续交给唯一 `executor.pickup_debris`。
- 隐藏、静音、E 盘隔离运行 `runtime-slime-ball-20260827-174122` 为 PASS：目标 `(2,3)` 原生移除，种子投影与实际守恒输出均为 `(O)766 x11`、`(O)557 x0`，工具栏槽恢复，只加载 TransparentBridge 与 RuntimeTestHarness。
- EVD-273 新增调用来源硬分类。`PlayerCommandOnly` 动作可以保留透明读取、编译和原生执行能力，但默认候选生成会排除，策略来源会被 SafetyPolicyGate 阻断，训练准入会给出 `PlayerCommandOnly` 类型化排除。当前包括 `buildings.change_skin`、`buildings.paint`、`world.rotate_house_plant`、`executor.change_building_skin`、`executor.place_furniture`、`executor.set_sign_display_item`、`executor.edit_text_sign`；只有 `InvocationSource=PlayerCommand` 且满足原有确认/权限门时才能执行。
- 权威对账为 `147 registered / 199 semantic / 146 compiler-bound / 73 five-gate / 38 training allowlist / 52 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens` 且 blocking 均为 `0`。本切片复用 `current_location.objects`、`current_location.debris` 和 `player.inventory`，不新增 full 快照顶层必需字段。
- 最终回归为 Core `1833/1833`、Backend `129/129`；Release 解决方案构建 `0` 错误，仅保留既有 `MiningReadAdapter.Objects.cs` 的一条 `AvoidNetField` 警告。下一语义切片按未闭合目录顺序为 `world.play_singing_stone`；必须先锁定原生声音石交互和可观测结果，再决定它属于自主策略、机械日计划还是 `PlayerCommandOnly`，不得由“能执行”反推训练准入。

## 2026-08-27 当前权威检查点：EVD-271

- `world.rotate_house_plant` 已闭合透明读取、逐对象候选、DailyPlan、机械字段重绑定、动作编译、原生运行与八帧回执。模型只选择“轮转哪一盆”这一有意义的装饰目标；当前贴图帧、预期帧、相邻站位、空工具栏槽位、恢复槽位和原生调用契约全部由最新快照与编译器绑定。
- 锁定 1.6.15 反编译与 `Data/BigCraftables` 确认：`(BC)0..7` 都是基础 `House Plant`，永久 `ItemId/QualifiedItemId` 与当前 `ParentSheetIndex` 相互独立。`CheckForActionOnHousePlant` 通常执行 `0→1→…→7→0`；但通过真实 `GameLocation.checkAction` 且空手交互时，起始帧 `7` 的第一次对象调用变为 `0` 并返回 `false`，地点层随后再次调用对象，所以一次玩家交互的最终结果是 `7→1`。
- 透明桥在 `current_location.objects[]` 中分别发布永久身份、当前帧、一次原生地点交互后的精确帧、预期对象调用次数、地点返回值与邻格。只有精确基础对象、`Crafting` 类型、`0..7` 有效帧、可达邻格、菜单关闭且存在真正空工具栏槽位时才开放候选；工具槽回退被拒绝，以冻结空手双调用语义。
- 生产执行器复用共享 BFS，临时切到编译器绑定的空槽，只调用一次 `GameLocation.checkAction`，然后恢复原槽位。生产文件不写 `ParentSheetIndex`，也不直接调用对象 `checkForAction`；成功必须同时观察预期帧、同一对象引用、永久 ID 不变、菜单为空和槽位恢复。
- 该装饰动作是严格 `PlayerCommandOnly`：不会进入默认候选、自主日计划或策略训练；只有玩家显式指令并通过原有确认门后，现有编译与原生执行链才可运行。
- 原生 `Object.checkForAction` 还存在四个基数方向均被不可通行对象封闭时对目标调用 `performToolAction(null)` 的破坏性前导分支。透明桥为每个站位发布 `object_trap_blocked`，候选在上游排除，运行时在寻路前和交互前再次核对并失败关闭，绝不让装饰轮转误删目标。
- 隐藏、静音、E 盘隔离运行 `runtime-house-plant-20260827-123652` 为 `8/8 PASS`：起始帧 `0..7` 分别得到 `1,2,3,4,5,6,7,1`，最后一例验证对象调用次数为 `2`；每例 `item_id=0`、`qualified_item_id=(BC)0` 与选中槽位均保持/恢复。
- 权威对账更新为 `146 registered / 199 semantic / 145 compiler-bound / 72 five-gate / 40 training allowlist / 53 catalogued blocked / 0 Product Executor`；原生分母仍为 `322 surfaces / 448 branches / 150 map tokens`，blocking 均为 `0`。full 快照顶层因本切片复用 `current_location.objects`，仍为 `130 required / 114 readable / 16 contextual / 0 blocking`。
- 最终回归为 Core `1826/1826`、Backend `128/128`；Release 解决方案构建 `0` 错误，仅保留既有 `MiningReadAdapter.Objects.cs` 的一条 `AvoidNetField` 警告。下一语义切片固定为 `farming.collect_slime_ball`：必须先核对史莱姆球的生成条件、每日状态、原生收取产物与背包/掉落分支，再决定复用对象交互还是库存转移内核。

## 2026-08-27 当前权威检查点：EVD-270

- `rewards.claim_statue_blessing` 已闭合透明读取、无参数日级候选、DailyPlan、机械字段重绑定、动作编译、原生运行与全天 buff 回执。小模型只输出“领取今日祝福”这一语义目标；祝福编号、雕像、站位、日期、天气/节日分母与对象交互全部由最新快照和编译器绑定。
- 锁定 1.6.15 反编译确认 `(BC)StatueOfBlessings` 需要 `StatKeys.Mastery(0) >= 1`，并以 `CreateDaySaveRandom(DaysPlayed*777)` 丢弃八次后抽取。普通日为 `Next(7)`；雨天或节日为 `Next(6)`，因此不会抽到蝴蝶祝福。`hasBeenBlessedByStatueToday` 或任一 `statue_of_blessings_*` buff 都会在上游排除重复领取。
- 透明投影覆盖七种原生效果：速度 `+0.5`、幸运 `+1`、体力不下降、前三次钓鱼减难度、交谈友谊 `20 -> 60`、暴击率 `+0.1`、17:00 前棱彩蝴蝶及其金钱/棱彩碎片结算。效果仍由原生 Buff、Farmer、BobberBar、NPC、GameLocation 和 Butterfly 分支消费，没有复制战斗、钓鱼或社交执行器。
- 生产执行器复用共享 BFS，到精确相邻格后只调用一次 `GameLocation.checkAction`。禁止生产路径直接 `applyBuff`、改写 `hasBeenBlessedByStatueToday` 或写 `AppliedBuffs`；结果同时要求唯一预测 buff、日锁为真、菜单为空。
- 隐藏、静音、E 盘隔离运行 `runtime-statue-blessing-20260827-115317` 返回 `applied/verified`：当天普通日预测并实际得到 `statue_of_blessings_1`，只观察到该幸运祝福，日领取锁变为 `true`。
- 最新真实 full 快照已安装：`130 required / 114 readable / 16 contextual / 0 blocking`；KnowledgeCompiler 为 `585/585`、blocking `0`。权威对账为 `145 registered / 199 semantic / 144 compiler-bound / 71 five-gate / 39 training allowlist / 54 catalogued blocked / 0 Product Executor`，原生分母仍为 `322 surfaces / 448 branches / 150 map tokens` 且三类 blocking 均为 `0`。
- 最终回归为 Core `1813/1813`、Backend `127/127`；Release 解决方案构建保持 `0` 错误，仅保留既有 `MiningReadAdapter.Objects.cs` 的一条 `AvoidNetField` 警告。
- 下一语义切片固定为 `world.rotate_house_plant`：先反编译八帧轮转、对象子类和交互返回值，再复用当前地图对象定位与原生直接交互；不得把装饰轮转伪装成长线策略，也不得建立第二套移动器。

## 2026-08-27 当前权威检查点：EVD-269

- `mining.choose_dwarf_statue_power` 已闭合透明读取、两个策略候选、DailyPlan、选择保留与机械字段重绑定、动作编译、原生运行和全天 buff 回执。小模型只从当天两个精确选项中输出 `dwarf_statue_power_id`；雕像、站位、菜单索引、buff ID、日期指纹和点击细节全部由最新快照与编译器绑定。
- 锁定 1.6.15 反编译确认 `(BC)StatueOfTheDwarfKing` 需要 `StatKeys.Mastery(3) >= 1`，以 `Utility.CreateRandom(DaysPlayed*77, uniqueID)` 每天固定生成两个不同的 `0..4` 选项。五种效果分别落在额外矿石、楼梯/竖井、煤炭、炸弹免伤和晶球概率的原生分支；已有任一 `dwarfStatue_*` buff 时当日不能重选。
- 生产执行器只复用共享 BFS，调用原生 `GameLocation.checkAction` 打开 `ChooseFromIconsMenu`，核对两个图标身份和顺序后调用原生 `receiveLeftClick`。禁止生产路径直接 `applyBuff` 或写 `AppliedBuffs`；普通矿井、骷髅洞和火山继续读取同一个 buff，不新增第二套矿洞或战斗系统。
- 隐藏、静音、E 盘隔离运行 `runtime-dwarf-king-statue-20260827-111935` 对当天两个选项 `0/3` 均返回 `applied/verified`，分别只观察到 `dwarfStatue_0` 和 `dwarfStatue_3`，原生菜单在 800ms 销毁后关闭。
- 最新真实 full 快照已安装：`129 required / 113 readable / 16 contextual / 0 blocking`；KnowledgeCompiler 为 `585/585`、blocking `0`。权威对账为 `144 registered / 199 semantic / 143 compiler-bound / 70 five-gate / 38 training allowlist / 55 catalogued blocked / 0 Product Executor`，原生分母保持 `322 surfaces / 448 branches / 150 map tokens` 且三类 blocking 均为 `0`。
- 最终 Release 回归为 Core `1802/1802`、Backend `126/126`；解决方案构建 `0` 错误，仅保留既有 `MiningReadAdapter.Objects.cs` 的一条 `AvoidNetField` 警告。
- 下一语义切片固定为 `rewards.claim_statue_blessing`。必须先锁定祝福雕像的每日随机状态、领取锁、全部祝福效果和原生菜单/对象路径；可以复用本切片的对象定位、日级候选和原生菜单生命周期，但不得把两个雕像的随机分母、buff ID 或规则混用。

## 2026-08-26 当前权威检查点：EVD-267

- `recovery.escape_object_trap` 已贯通透明读取、候选、日计划和动作编译链。小模型只需发出该高层恢复命令；编译器从玩家四个基数方向的对象行与 `farm.machines` 中按固定方向顺序选择首个 `removal_safe_now=true` 的相邻机器，不要求模型生成拆除坐标或工具细节。
- 锁定 1.6.15 反编译确认 `Object.checkForAction` 的四向封闭前导分支会调用目标 `performToolAction(null)`；base `Object` 随即从 `location.objects` 移除且只产生装饰 debris，不返还资产，并可能继续执行目标交互。透明桥发布四向存在性、可通行性、运行时/声明类型、交互状态及该破坏性契约，但 `destructive_native_fallback_enabled=false`。
- 安全恢复只编译到既有 `executor.remove_machine`：复用其精确机器身份、所有权、空闲/无产出/无附件、fragility、Pickaxe、指纹和原生 debris/自动回收验证；没有第二套移除运行时。非四向封闭、菜单/手持物/骑马冲突、无安全相邻机器或机器状态漂移均关闭失败。
- 隐藏、静音、E 盘隔离环境已采集真实 full 快照并安装：`127 required / 111 readable / 16 contextual / 0 blocking`。聚焦候选/日计划/编译/治理测试与全量 Core 回归通过；KnowledgeCompiler 为 `585/585`、blocking `0`。
- 当前权威对账为 `142 registered / 199 semantic / 141 compiler-bound / 68 five-gate / 36 training allowlist / 57 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens`，三类 blocking 均为 `0`。five-gate 未增加，因为尚未伪造一份专用四向陷阱运行回执；实际资产移除内核沿用已验证的 EVD-147/EVD-148。
- 下一语义切片固定为 `rewards.claim_pot_of_gold`：先从锁定反编译确定彩虹尽头奖励对象的生成、可领取状态、背包满分支、一次性状态与原生转移路径，再决定复用对象交互/背包转移内核；不把通用拾取冒充特殊奖励领取。

## 2026-08-26 当前权威检查点：EVD-266

- `recovery.sleep_in_tent` 已作为独立的终端跨日语义闭合；它复用共享移动、原生交互、确认输入、跨日等待、保存/出货结算和稳定世界判定，不并入 `executor.place_tent` 或普通床 `executor.sleep`。
- full 快照新增 `player.temporary_sleep`、`menus.tent_sleep_prompt_context`，并扩展精确 base `Tent` 行的睡眠地点、锚点、规范交互格、规范站位、朝向、问题键和原生许可。编译器冻结这些字段以及运行时身份、血量、可通行性和路径，只允许最后一个计划步骤执行。
- 生产执行器先走到 `anchor+(0,1)`、面朝上，再调用原生 `GameLocation.checkAction` 打开 `SleepTent`，随后发送原生确认输入。它不直接写日期、`sleptInTemporaryBed`、Tent health、玩家位置或 Tent 表；共享跨日结算必须观测 temporary-bed 标志、日期恰好增加一天、同地点同格醒来、保存菜单关闭、标志复位和 Tent 隔夜销毁。
- 隐藏、静音、E 盘隔离运行 `runtime-tent-sleep-20260826-164032` 返回 `applied/verified`：总日数 `222 -> 223`，在 `Farm:66,19` 同地点同格醒来，观测到 `SleepTent_Yes`、temporary-bed 标志先置位后复位、世界稳定且精确 Tent 消失。
- 当前权威对账为 `141 registered / 199 semantic / 140 compiler-bound / 68 five-gate / 36 training allowlist / 58 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens`，三类 blocking 均为 `0`；full 快照为 `126 required / 110 readable / 16 contextual / 0 blocking`，KnowledgeCompiler 为 `585/585`、blocking `0`。
- 下一语义切片从未闭合目录首项 `recovery.escape_object_trap` 开始：先锁定原生脱困分支与透明状态，再判断是否能复用现有移动/交互内核；在反编译和严格状态契约完成前不建立泛化执行器。

## 2026-08-26 当前权威检查点：EVD-265

- `executor.place_tent` 已作为独立放置语义闭合；它不包含 `recovery.sleep_in_tent`。full 快照的 `player.tent_placement` 仅接受精确 base `(O)TentKit`，逐方向压缩发布原生合法站位，并绑定室外限制、跨季节明日日期、普通节日、被动节日地图替换/多日窗口、3x2 矩形、中心锚点、初始 `Tent` 状态、日更销毁和后续睡眠交接。
- 编译器冻结精确槽位/堆叠/地点/站位/朝向探针/矩形/锚点/日历/投影指纹/布局指标/放置理由与原生契约。共享邻接执行器现会尊重已冻结的 `stand_tile`，再由唯一 `PlaceInventoryObjectNative` 调用 `Utility.tryToPlaceItem -> Object.placementAction`；生产代码不直接写 `largeTerrainFeatures` 或库存。
- 隐藏、静音、E 盘隔离运行 `runtime-tent-placement-20260826-160613` 为 `4/4 PASS`，覆盖方向 `0..3`。每例均验证原生返回、库存减一、精确 base `Tent`、方向派生锚点/3x2 边界、`health=5`、玩家可通行性及下一份透明快照回读。
- 当前权威对账为 `140 registered / 199 semantic / 139 compiler-bound / 67 five-gate / 36 training allowlist / 59 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens`，三类 blocking 均为 `0`；full 快照为 `123 required / 107 readable / 16 contextual / 0 blocking`，KnowledgeCompiler 为 `585/585`、blocking `0`。
- 下一语义切片固定为 `recovery.sleep_in_tent`：复用现有移动、交互、确认、跨日等待和 `executor.sleep` 的终止性规则，只新增 `Tent.performUseAction -> SleepTent_Yes -> sleptInTemporaryBed`、同地点醒来及隔夜销毁回执；不得把它重新并入放置动作或普通床睡眠。

## 2026-08-26 上一权威检查点：EVD-264

- `executor.edit_text_sign` 已作为独立语义闭合，不与标牌放置或展示物赋值合并。full 快照对精确 base `StardewValley.Object` 文字牌发布 raw/display 文本、`showNextIndex`、直接序列化 SHA-256、替换要求、60 UTF-16 code-unit 限制及完整原生菜单管线。
- 编译器严格绑定目标地点/格子/运行时类型/qid/状态哈希/投影指纹、相邻站位、旧文本与覆盖授权，并拒绝超过 60 code units、双引号或控制字符的非原生键盘输入。生产执行器仅复用共享相邻移动，调用 `GameLocation.checkAction`，逐字符输入 `TitleTextInputMenu.textBox` 并点击原生完成按钮；不直接写 `signText` 或 `showNextIndex`。
- 原生回执按实际顺序验证 `NamingMenu.FilterInput -> Utility.FilterDirtyWords -> Trim -> NetString -> TokenParser.ParseText -> Utility.FilterDirtyWords`，并验证 `showNextIndex == string.IsNullOrEmpty(SignText)`。隐藏、静音、E 盘隔离运行 `runtime-text-sign-editing-20260826-104822` 为 `5/5 PASS`，覆盖首次写入、旧文本替换、首尾空白裁剪、清空、中文 UTF-16 输入和 60 code-unit 边界。
- 当前权威对账为 `139 registered / 199 semantic / 138 compiler-bound / 66 five-gate / 36 training allowlist / 60 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens`、三类 blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。
- 标牌三条独立链现已全部闭合：`executor.place_sign`、`executor.set_sign_display_item`、`executor.edit_text_sign`。下一语义切片固定为 `executor.place_tent`，继续复用共享原生物品放置与布局安全，不建立第二套移动或放置系统。

更新时间：2026-08-26

## 2026-08-25 当前权威检查点：EVD-263

- `executor.set_sign_display_item` 已闭合：full 快照对每个精确 base `Sign` 发布全部非空背包物品、源对象直接序列化 SHA-256、原生展示类型、旧展示载荷和替换要求；读取端禁止调用 `getOne()`，避免构造副本时消耗 RNG。
- 编译器逐项绑定目标、相邻站位、源槽位/身份/品质/堆叠/状态哈希、旧载荷和替换授权。生产执行只复用共享相邻移动并调用 `GameLocation.checkAction -> Sign.checkForAction`，不直接写 `displayItem`、`displayType` 或背包。
- 隐藏、静音、E 盘隔离运行 `runtime-sign-display-item-20260825-170751` 为 `6/6 PASS`：覆盖展示类型 `1..5` 与非 Object 默认分支，后五例覆盖已有展示替换；源物品引用、数量和完整序列化状态全部不变。
- 当前权威对账为 `138 registered / 199 semantic / 137 compiler-bound / 65 five-gate / 36 training allowlist / 61 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens`、blocking `0`，KnowledgeCompiler 为 `585/585`。
- 下一语义切片固定为 `executor.edit_text_sign`：独立闭合原生 `TitleTextInputMenu`、trim、60 字限制和 `showNextIndex`，不得并入标牌放置或展示物赋值。

更新时间：2026-08-25

## 2026-08-25 当前权威检查点：EVD-262

- `executor.place_sign` 已闭合实时标牌目录、精确库存、当前地图原生合法格、严格编译、共享邻接移动、共享原生物品放置和逐字段回执。锁定 1.6.15 存在两条不同原生分支：带 `sign_item` 标签的三种展示牌生成精确 `StardewValley.Objects.Sign`；`(BC)TextSign` 生成精确 base `StardewValley.Object`。两者均只放置空牌且库存精确 `-1`。
- `player.sign_placement` 从实时 `Game1.bigCraftableData` 枚举全部 `4` 行，不硬编码标牌 ID 或数量。候选绑定数据行、运行时类型、分支、当前地图合法格、邻接站位、布局安全、空载荷预期和拓扑指纹；`current_location.objects[].sign_state` 回读展示物类型/身份、文字、`showNextIndex`、可通行性和运行时支持状态。
- 生产执行器只复用 `Utility.playerCanPlaceItemHere -> Utility.tryToPlaceItem -> Object.placementAction` 与既有 `PlaceInventoryObjectNative`，不直接写 `location.objects`、标牌载荷、文字或库存。隐藏、静默、E 盘隔离运行 `runtime-sign-placement-20260825-160744` 对实时目录全部 `4/4 PASS`，覆盖三种展示牌和文字牌两条分支。
- 反编译复核同时修复动作分母遗漏：`checkForAction` 与 `Objects/Sign.cs` 已进入扫描，新增 Object/Sign 动作面及 `20` 个原生分支。全部分支已逐项映射到已有能力或显式 `catalogued_blocked` 语义，不以通用交互伪装成完整支持。当前分母为 `322 surfaces / 448 branches / 150 map tokens`，三类 blocking 均为 `0`。
- 当前权威对账为 `137 registered / 199 semantic / 136 compiler-bound / 64 five-gate / 36 training allowlist / 62 catalogued blocked / 0 Product Executor`；fresh full 快照要求 `122` 个状态因子，其中 readable `106`、contextual/stale `16`、blocking `0`；KnowledgeCompiler 为 `585/585`、blocking `0`。
- 下一语义切片固定为 `executor.set_sign_display_item`，只实现 `Sign.checkForAction` 的展示物绑定和逐字段回执；其后独立实现 `executor.edit_text_sign` 的 `TitleTextInputMenu`、trim、60 字限制和 `showNextIndex` 语义。两者不得并入摆放动作。

更新时间：2026-08-25

## 2026-08-25 当前权威检查点：EVD-261

- `executor.place_furniture` 已闭合实时 `Data/Furniture` 全目录、精确库存家具、当前地图家具拓扑、严格编译、共享移动、共享原生物品放置和逐字段回执。普通落地家具进入 `location.furniture`；在空桌面放置的 1x1 家具进入该桌子的 `heldObject`，两种原生终点不得混淆。
- `player.furniture_placement` 发布实时目录中的 `645` 行，并只对当前已加载地图执行目的限定的原生合法格扫描。每个候选绑定运行时子类、地点限制、墙面锚点修正、矩形占地、可通行性、所有虚拟旋转状态、原生终点、空桌身份和拓扑指纹。`current_location.furniture` 回读相同身份、旋转、占地、碰撞、容器内容与桌面载荷。
- 读取探针禁止调用可能改变源家具旋转状态的 `Furniture.getOne()`；统一使用 `Furniture.GetFurnitureInstance` 创建脱离源对象的规范实例。每个候选格都必须重新创建探针并调用 `InitializeAtTile`，防止旧 bounding box 污染后续格子和桌面判断。生产执行器只调用虚拟 `rotate()` 与既有 `PlaceInventoryObjectNative`，不直接写 `currentRotation`、`location.furniture`、桌面载荷或库存。
- 隐藏、静默、E 盘隔离运行 `runtime-furniture-placement-20260825-101457` 从实时目录选择完整代表集并 `25/25 PASS`：覆盖 `Furniture`、`StorageFurniture`、`FishTankFurniture`、`BedFurniture`、`RandomizedPlantFurniture`、`TV` 六种规范运行时子类，家具类型 `0..17`，旋转步数 `0..3`，以及 `location_furniture` 与 `table_held_object` 两种终点。
- 当前权威对账为 `136 registered / 184 semantic / 135 compiler-bound / 63 five-gate / 36 training allowlist / 48 catalogued blocked / 0 Product Executor`；fresh full 快照要求 `121` 个状态因子，其中 readable `105`、contextual/stale `16`、blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。
- 下一语义切片固定为 `executor.place_sign`。先反编译锁定标牌身份、显示物绑定和交互/文字语义是否与普通物品放置重叠；复用既有共享移动和唯一原生放置内核，只新增标牌特有的透明字段、严格校验与回执，不建立第二套放置系统。

更新时间：2026-08-25

## 2026-08-25 当前权威检查点：EVD-260

- `executor.place_flooring` 已闭合实时地板目录、当前地图合法区间、严格编译、共享邻接移动、共享原生物品放置和逐字段回执。库存源必须是精确 base `StardewValley.Object`；原生 `Object.placementAction(IsFloorPathItem)` 在 `terrainFeatures` 中生成精确 base `StardewValley.TerrainFeatures.Flooring`，生产执行器不直接写地形表、视图或库存。
- `player.flooring_placement` 从实时 `Game1.floorPathData` 与 `Flooring.GetFloorPathItemLookup()` 发布完整目录。只有当前已加载地图按地板身份压缩原生合法区间，跨图必须到达后重新绑定；这保留全量数据透明，同时避免无关日程扫描所有持久地图。每个区间绑定同类八邻接掩码；`Random` 连接只绑定完整 `whichView=0..15` 结果域，非随机构造值为 `0`。
- 原生放置拒绝任何已有 `TerrainFeature` 的目标格，不存在放置时替换地板的能力。拆除仍是独立 Axe/Pickaxe/damage 工具语义。`Flooring.isPassable` 恒为 true，因此共享布局校验要求目标、邻接站位位于当前 BFS 可达域，放置前后可达格计数相同；不得错误复用围栏的虚拟阻塞规则。
- 隐藏、静默、E 盘隔离运行 `runtime-flooring-placement-20260825-015659` 从实时目录枚举 13 个规范物品并 `13/13 PASS`。全部返回 `applied/verified`，透明回读验证 data key、base Flooring、同类邻接掩码、可通行、视图域与库存减一；随机 `(O)415` 本轮实际视图为 `5`。
- 当前权威对账为 `135 registered / 184 semantic / 134 compiler-bound / 62 five-gate / 36 training allowlist / 49 catalogued blocked / 0 Product Executor`；fresh full 快照要求 `119` 个状态因子、blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。
- 下一语义切片进入 `executor.place_furniture`。必须先反编译区分普通家具、壁挂/地毯/旋转、室内地点限制与原生 `Furniture.placementAction`，继续复用共享移动和原生物品放置内核，不得复制第二套放置系统。

更新时间：2026-08-25

## 2026-08-22 当前权威检查点：EVD-258

- `executor.load_crab_pot_bait` 已闭合透明读取、严格编译、共享邻接移动、原生 `GameLocation.checkAction` 执行和逐字段回执。它是独立的蟹笼上饵 primitive，不并入通用机器投料，也不复制 EVD-209 的蟹笼收取链。
- full 快照对每个精确 base `CrabPot` 发布生命周期、owner/Luremaster 状态和背包内所有 `Category=-21` 饵料的槽位、堆叠、运行时类型、品质、单位状态哈希及原生 probe 结果。编译器绑定目标、邻接站位、owner before/after、饵料身份、理由与原生契约；任一漂移即失败关闭。
- 隐藏、静默、E 盘隔离运行 `runtime-crab-pot-bait-20260822-164015` 为 5/5 PASS：普通、豪华、万能、魔法和特定目标鱼饵均由原生 checkAction 装入，原生 `reduceActiveItemByOne` 精确消耗一件，下一份透明快照读回相同 qid、运行时类型、品质、单位状态哈希与 owner。
- 当前权威对账为 `133 registered / 184 semantic / 132 compiler-bound / 60 five-gate / 36 training allowlist / 51 catalogued blocked / 0 Product Executor`；full 快照要求 `117` 个状态因子、blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。
- 下一语义切片进入 `executor.place_fence`。必须先审查并复用现有物品放置、邻接移动、碰撞与布局安全实现，禁止形成第二套放置系统。

更新时间：2026-08-22

## 2026-08-22 当前权威检查点：EVD-257

- `executor.place_crab_pot` 已闭合透明读取、严格编译、共享邻接移动、共享原生物品放置和逐字段回执。源物品与放置结果均为 `(O)710`，但库存中的普通 `StardewValley.Object` 必须经原生 `Object.placementAction` 转成由当前玩家拥有的精确 `StardewValley.Objects.CrabPot`。
- `player.crab_pot_placement` 在 full profile 中枚举每个背包蟹笼、所有已加载持久地点的原生合法水格区间，以及每个区间的鱼区、栖息地、垃圾概率、Mariner/Luremaster 修正、原生顺序捕获行和生产签名。编译器绑定精确库存、地点、水格、邻接站位、拓扑指纹、生产签名、放置理由和原生契约；运行时再次执行原生合法性检查。
- 放置复用唯一 `PlaceInventoryObjectNative` 与既有邻接移动；后续收取继续复用已经由 EVD-209 闭合的 `fishing.collect_crab_pots -> executor.collect_crab_pot`，没有第二套蟹笼收取、移动或物品放置系统。该放置 primitive 保持 calibration/evaluation-only，上层策略负责布局、产能与放置理由。
- 隐藏、静默、E 盘隔离运行 `runtime-crab-pot-placement-20260822-152201` 返回 `applied/verified`：目标 `Farm:73,31` 生成精确 base `CrabPot`，owner、空饵料、空产出、未 ready 与库存减一均通过；下一份透明快照读回同一蟹笼及淡水生产签名 `|0.2|freshwater|721,716,722`。
- 复核 EVD-209 与锁定反编译时发现，原冻结分母遗漏了独立的原生上饵动作：`CrabPot.performObjectDropInAction` 接受 `Category=-21` 饵料并写入 owner/bait，它不属于通用机器输入链。现已把 `executor.load_crab_pot_bait` 作为 `catalogued_blocked` 显式补回分母，未伪装成已实现。
- 当前权威对账为 `132 registered / 184 semantic / 131 compiler-bound / 59 five-gate / 36 training allowlist / 0 Product Executor`；full 快照要求 `117` 个状态因子、blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。下一语义切片先闭合 `executor.load_crab_pot_bait`，再进入 `executor.place_fence`。

更新时间：2026-08-22

## 2026-08-22 当前权威检查点：EVD-256

- `executor.place_cookout_kit` 已闭合透明读取、严格编译、共享邻接移动、原生物品放置和逐字段回执。锁定 1.6.15 的源物品是 `(O)926`，`Object.placementAction` 落地的是 `StardewValley.Torch` / `(BC)278`，并带有 `Fragility=1`、`destroyOvernight=true`；两者不得混作同一个物品身份。
- `player.cookout_kit_placement` 在 full profile 中枚举背包内每个野炊工具和所有已加载持久地点的原生合法区间、布局指纹、当日生命周期及烹饪交接契约。编译器必须绑定精确槽位、堆叠、地点、落点、邻接站位、投影指纹、放置理由和原生契约；运行时仍会重新调用 `Utility.playerCanPlaceItemHere`。
- 机器、储物和野炊工具现在共用唯一 `PlaceInventoryObjectNative` 内核；野炊工具放置后由既有 `player.cooking` 立即识别为 `cookout:location:tile` 来源，没有复制烹饪、移动或布局系统。该底层 primitive 保持 calibration/evaluation-only，只有上层同日烹饪目的才能授权消耗。
- 隐藏、静默、E 盘隔离运行 `runtime-cookout-kit-placement-20260822-105210` 返回 `applied/verified`：库存 `1 -> 0`、目标生成精确 `(BC)278`、当日销毁标志为 true，且透明烹饪端点交接成功。
- 当前权威对账为 `131 registered / 183 semantic / 130 compiler-bound / 58 five-gate / 36 training allowlist / 0 Product Executor`；full 快照要求 `116` 个状态因子、blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。下一切片从 `executor.place_crab_pot` 开始，先审查是否能复用现有蟹笼读取、收取、物品放置和水域边界实现。

更新时间：2026-08-22

## 2026-08-18 当前权威检查点：EVD-255

- `executor.apply_tree_treatment` 已完成透明读取、严格编译、共享 BFS 邻接移动、原生物品放置与逐字段回执。锁定 1.6.15 的真实语义是 `(O)419` 醋永久禁止一棵树长苔，不是 `(O)805` 树肥；原生分支没有成长阶段限制。
- 隐藏静默隔离运行 `runtime-tree-treatment-20260818-162145` 已验证 `has_moss true -> false`、`stop_growing_moss false -> true` 和醋堆叠减一。生产执行器只调用 `Object.placementAction`，直接树字段写入仅存在于测试夹具。
- 该项保持 executor calibration / evaluation-only，不进入自主候选。上层以后必须提供 `tree_treatment_reason` 和策略授权，不能因为背包有醋、地图有树就自动安排永久处理。
- 当前机器对账为 `130 registered / 183 semantic / 129 compiler-bound / 57 five-gate / 36 training allowlist / 0 Product Executor`；权威 full 快照仍要求 `115` 个状态因子、blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。
- 下一语义切片从重生成后的 `catalogued_blocked` 清单选择；优先检查 `executor.place_cookout_kit` 是否应复用现有物品放置、布局安全与烹饪来源投影，禁止复制机器/储物放置或野炊烹饪执行器。

更新时间：2026-08-18

## 当前权威检查点（优先于下方历史记录）

- EVD-254 已闭合 `crafting.forge_item`：完整快照发布全部已加载原生锻造来源、实时背包工具/戒指与已装备戒指输入、原生碎片成本/返还、统计变化、精确确定性输出及 Diamond/Dragon Tooth 完整随机结果域。显式单次意图经唯一 DailyPlan/队列链进入 `executor.forge_item`，生产执行器只使用原生 `ForgeMenu` 输入与按钮并等待 1600 ms 生命周期。隐藏静默隔离运行 `runtime-forge-20260818-122957` 的九个操作族全部返回 `applied/verified`。
- EVD-253 已闭合 `crafting.cook_recipe`：完整快照发布所有已学配方在每个实时厨房/野炊工具来源上的精确材料消费顺序、主冰箱与原生枚举顺序的小冰箱拓扑、互斥锁、齐氏调味料、输出品质/订单标记和历史烹饪次数。显式单次烹饪意图经唯一 DailyPlan/队列链进入 `executor.cook_recipe`；普通制作、工作台和烹饪共用一个原生 `CraftingPage` 配方点击辅助函数，但厨房保留独立的容器、锁和 `recipesCooked` 语义。隐藏静默隔离运行 `runtime-cooking-20260817-202809` 对厨房银星煎蛋和野炊工具普通煎蛋均返回 `applied/verified`。
- EVD-252 已闭合 `animals.manage_animal`：透明桥发布精确动物、原生查询许可、繁殖、售价、当前家园和兼容家园；显式意图经唯一 DailyPlan/队列链进入 `executor.manage_animal`。隐藏静默隔离运行 `runtime-animal-management-20260816-012959` 对首次抚摸后改名、繁殖开关、搬家和确认出售四支均返回 `applied/verified`。生产执行器只发送原生动物交互和 `AnimalQueryMenu` 点击，不直接写动物或金钱字段；过宽名字在确认前阻塞。
- 当前机器对账为 `130 registered / 183 semantic / 129 compiler-bound / 57 five-gate / 36 training allowlist / 0 Product Executor`；权威 full 快照要求 `115` 个状态因子、blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。

- 锁定版本仍为 Stardew Valley 1.6.15；KnowledgeCompiler 当前为 `585/585` exports、blocking `0`。
- 动作对账当前为 `130 registered / 183 semantic / 129 compiler-bound / 57 five-gate / 36 training allowlist / 0 Product Executor`；
  原生分母仍为 `320 surfaces / 428 branches / 150 map tokens`，三类 blocking 均为 `0`。
- EVD-248 已闭合 `buildings.construct` 的第一个严格范围：模型必须明确给出建筑类型、目标地点和建设理由；
  透明桥从实时 `Game1.buildingData` 与全部原生可建地点读取基础蓝图、Builder、条件、价格、材料、现有与在建
  建筑、服务动作和原生合法落点。候选在上游排除缺意图、条件/资源/落点漂移、在建冲突和材料预留冲突，
  再经 DailyPlan 汇入 EVD-236 已存在的唯一 `executor.construct_building`。隐藏静默隔离运行分别验证了
  无任务策略建造 `runtime-quest-terminal-daily-plan-20260812-105048` 和原 `HaveBuildingQuest` 回归
  `runtime-quest-terminal-daily-plan-20260812-105331`，均由原生 Robin/`CarpenterMenu` 建造 `Farm` 上的
  `Coop`，核对钱、材料、坐标和三天倒计时。证据不外推到 Wizard、升级、换皮或长期建筑策略。
- 最新 full 快照覆盖 `114` 个必需字段，blocking `0`；KnowledgeCompiler 仍为 `585/585`、blocking `0`。
- EVD-246 已闭合 `mining.use_elevator`：透明桥读取玩家最深/当前矿层、入口 `Action=MineElevator`、
  楼层 `Buildings/mine` 索引 112 和精确 `MineElevatorMenu` 条目身份；DailyPlan 复用既有跨图移动、
  `interact` 与 `close_menu`。运行时只点击原生端点和菜单，不直接调用 `enterMine`/`warpFarmer`，并跨帧
  验证最终位置。隐藏静默隔离矩阵 `runtime-mine-elevator-20260812-004601` 为 2/2：25 层回入口及入口回
  25 层均通过。`mining.reach_depth` 仅在实时端点存在且已解锁检查点能推进最终目标时复用该链，并保留
  最终深度为 continuation；其余楼层继续使用原 current-floor planner。
- EVD-245 已闭合 `mail.process_letter`。共享解析器覆盖锁定 `Data/mail` 的 179 封信和 107 条指令，
  解析阻塞为 0；透明桥公开原生顺序队列、玩家实际拥有的邮箱位置、附件容量上界和完整
  `LetterViewerMenu` 状态。DailyPlan 只组合既有移动、`interact` 与 `close_menu`，运行时只发送原生
  菜单输入并核对附件、任务、特别订单和星之果实收据，没有第二套移动/菜单执行器，也不直接写钱、
  配方、任务或最大体力。严格隐藏静默矩阵
  `artifacts/runtime-mail-processing/runtime-mail-processing-20260811-221959/summary.json` 为 5/5；
  新 full 快照为 required 107、blocking 0，KnowledgeCompiler 为 585/585、blocking 0。
- 下一主切片按动作对账中剩余语义依赖继续选择；普通矿井电梯已经闭合，不得再建立平行实现。采石场
  金镰刀洞窟、Skull Cavern 和火山继续保持独立身份，不得混入普通矿井电梯。
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
## 2026-08-12 当前权威检查点：EVD-249

- `buildings.change_skin` 已完成透明读取、上游候选、DailyPlan、共享动作队列、类型化请求、原生 Robin/CarpenterMenu 执行与严格回执。
- 隔离运行 `runtime-quest-terminal-daily-plan-20260812-122957` 为 `applied/verified`：Pet Bowl 默认皮肤通过一次最短 `next` 切换到 `Stone Pet Bowl`，返回 ScienceHouse，并验证三组油漆颜色重置为默认。
- 当前动作对账：122 registered、180 semantic、121 compiler-bound、49 five-gate、32 training allowlist、0 Product Executor；320/428/150 原生分母 blocking 均为 0。
- 当前 full 快照 required 112、blocking 0；KnowledgeCompiler 585/585、blocking 0；Core 1663/1663、Backend 121/121。
- 下一切片为 `buildings.paint`。它必须复用现有 Robin 服务、Carpenter 建筑选择和菜单退出链，只新增实时颜色参数、上游外观意图约束和 `BuildingPaintMenu` 原生滑杆/回执，不得新增平行建筑菜单执行器。

## 2026-08-12 当前权威检查点：EVD-250

- `buildings.paint` 已完成透明读取、上游许可、DailyPlan、共享动作队列、类型化请求、原生 `BuildingPaintMenu` 控件与严格回执。
- 透明桥公开每栋可涂装建筑的一至三区域、原生 H/S/L 范围、当前值、默认标志、权限、Robin 服务入口，以及 284 像素滑杆的精确鼠标可达整数集合；上游拒绝不可达、无效果和默认显示值无法解除默认标志的目标。
- DailyPlan 生成 `paint_building_region`，但动作队列继续映射到唯一 `executor.change_building_skin`。共享 `ActiveBuildingAppearanceChange` 复用 Robin、Carpenter、建筑选择和退出生命周期，只在子菜单内部按冻结参数分流；不存在第二套 Robin 状态机。
- 隐藏静音隔离运行 `runtime-quest-terminal-daily-plan-20260812-133245` 已通过：Farmhouse `Building` 区域原生点击到 H180/S37/L-30，目标精确匹配，另外两区域保持默认，训练行落盘。
- 当前为 123 registered / 180 semantic / 122 compiler-bound / 57 catalogued-blocked；full snapshot 113 required、96 带来源可读、17 场景性、blocking 0；KnowledgeCompiler 585/585、blocking 0；Core 1666/1666、Backend 121/121。
- 下一切片应从剩余 57 个 `catalogued_blocked` 动作中，按权威字典依赖、已有机械引擎复用和可形成严格原生回执的顺序选择；不得以动作数量为理由复制执行系统。

## 2026-08-12 119 展示循环检查点：EVD-251

- 119 展示房间已改为版本化发布，继续使用原存档、原版联机房主、公开 UDP 24642 和既有可加入小屋；正式训练仍保持关闭，展示循环只运行目的受限的已实现候选集。
- `daily` 快照现有精确域校验；无手持物品以可读空字符串表达，不再把“没有 ActiveObject”误判为字段不可用。队列只执行首个阻塞项之前的连续可执行前缀，控制层 HTTP 超时不早于执行器预算。
- 无窗口房主通过原生 `IInputSimulator` 消费现有移动租约，不写坐标、不注入动作键；通用移动 BFS 避开可移除障碍，清障继续由唯一 `executor.clear_obstacle` 原语负责。
- 锁定版反编译确认多人 `Game1.shouldTimePass()` 由 `netWorldState.IsTimePaused` 决定。专用原版 AI 房主只在原生事件、菜单与显式暂停门槛均允许时清除残留联网暂停位，不直接写 `timeOfDay`。119 实测 58 秒内 `08:20 -> 09:40`，最终版本再次实测 `06:50 -> 08:10`。
- 服务器闭环先连续形成 6 条 applied/verified/fresh 执行记录；修复后无实际候选时只生成空队列并指数退避，不再以 `recovery:refresh_plan_after_stabilization` 制造永久 `wait_ticks` 样本。建筑施工中的等待仍由 `quest.advance` 在透明状态明确为 `construction_in_progress` 时局部复用。
- 既有晚间恢复链在 119 完成 `22:00 -> 回家/睡觉 -> NewDay`。三项服务为 server healthy、planner healthy、host-ai running，透明 `daily` 快照完整且 unavailable 0；这证明展示日循环主体成立，不等于完整动作全集、Product Executor 或正式训练完成。
- 本地最终回归：Core 1671/1671、Backend 122/122、Release solution build 0 errors；保留 1 个既有 `AvoidNetField` 警告。
- EVD-254 已完成上述 `crafting.forge_item` 切片，未建立第二套移动、库存或菜单系统。下一主开发切片为 `executor.apply_tree_treatment`：复用既有树木定位、移动、站位和工具/物品交互基础设施，先按锁定 1.6.15 原生分支明确适用树种、物品消费、成长状态变化、许可与严格回执，再决定其高层目的归属；不得把底层处理动作直接当作策略候选。

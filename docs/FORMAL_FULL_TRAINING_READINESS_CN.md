# StardewAI 正式全量训练准入与实施路线

## 2026-08-30 垃圾桶翻找训练准入（EVD-300）

`foraging.rummage_garbage` 已通过 read / candidate / compile / native runtime / output receipt 五门并进入 `StrategyValue` allowlist。小模型只决定是否翻找某个当前可达且安全的未检查垃圾桶；桶身份、地图位置、站位、路径、运气/书籍状态、确定输出、交付方式、安全槽、NPC 目击和任务回执都由编译执行层从 fresh snapshot 机械绑定。已检查、负友谊目击、数据/预测漂移、无安全槽和不可达目标在上游排除。

隐藏静音 E 盘矩阵 `9/9` 覆盖空结果、普通 debris、直接入包垃圾帽、Desert Festival 多 debris、两类排除、Linus 正向反应以及普通/特别收集任务。生产执行只调用一次原生 `GameLocation.checkAction`，不写 CheckedGarbage、统计、好感、库存、debris 或 RNG。最新 schema 为 `146/130/16/0`；对账为 `180 registered / 205 semantic / 179 compiler-bound / 103 five-gate / 48 allowlist / 25 catalogued blocked / 0 Product Executor`，回归为 Core `2039/2039`、Backend `145/145`、Release `0 warnings / 0 errors`。正式全量训练仍受剩余 25 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `housing.renovate`。

## 2026-08-30 普通树产品训练准入（EVD-299）

`foraging.harvest_tree_product` 已通过 read / candidate / compile / native runtime / output receipt 五门并进入 `StrategyValue` allowlist。小模型只决定是否收取当前可用普通树产品；树种、地点、站位、路径、安全空槽、确定掉落和随机附加掉落域全部由编译执行层机械生成并在 fresh snapshot 重绑。自定义树、数据漂移、未成熟、树桩、tapped、无种子、原生等级门不满足、摇动中、输出域不完整及不可达目标在上游直接排除。

隐藏静音 E 盘矩阵覆盖普通种子、秋季榛子、岛屿棕榈和三类排除分支。生产执行只调用一次原生 `GameLocation.checkAction`，以背包加 debris 守恒验收确定输出及至多一个有界可选输出，不读取或改写 RNG。最新 schema 仍为 `145/129/16/0`；对账为 `178 registered / 204 semantic / 177 compiler-bound / 102 five-gate / 47 allowlist / 26 catalogued blocked / 0 Product Executor`，回归为 Core `2035/2035`、Backend `144/144`。正式全量训练仍受剩余 26 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `foraging.rummage_garbage`。

## 2026-08-30 果树收获训练准入（EVD-298）

`foraging.harvest_fruit_tree` 已通过 read / candidate / compile / native runtime / output receipt 五门并进入 `StrategyValue` allowlist。小模型只决定是否在日计划中收获已就绪果树；位置、站位、路径、交互、live fruit、品质、雷击替换、数量与零经验回执全部由编译执行层机械生成并在 fresh snapshot 重绑。空树、未成熟、树桩、摇动中、自定义类型和不可达目标在上游直接排除，不产生无意义训练阻塞。

隐藏静音 E 盘矩阵覆盖单果普通、三果金星、雷击三煤炭、空树排除和摇动中排除；生产执行只调用一次原生 `GameLocation.checkAction`，不写任何结果状态。最新 schema 仍为 `145/129/16/0`；对账为 `176 registered / 203 semantic / 175 compiler-bound / 101 five-gate / 46 allowlist / 27 catalogued blocked / 0 Product Executor`，回归为 Core `2031/2031`、Backend `143/143`。正式全量训练仍受剩余 27 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `foraging.harvest_tree_product`。

## 2026-08-30 鱼塘管理玩家命令闭环（EVD-297）

`fishing.manage_fish_pond` 已完成五道证据闭环，但严格保持 `PlayerCommandOnly`，不进入正式训练 allowlist。玩家必须提供精确鱼塘、操作和原因，`empty_pond` 另需操作级确认；自动日计划继续只使用既有 `fishing.service_fish_ponds` 处理产出与请求，不会把换网装饰或清塘破坏性重置混入收益训练。

透明桥发布鱼塘管理状态、四种网样式、空手安全槽、精确站位、清塘前状态及反编译锁定的 reset/preserve 收据，菜单桥发布绑定鱼塘、确认状态和全部公共按钮。运行层复用共享 BFS，经作用域右键边沿和真实 `GameLocation.checkAction -> FishPond.doAction -> PondQueryMenu.receiveLeftClick` 执行；不构造菜单、不调用 `ClearPond`、不直接写鱼塘状态。隐藏静音运行 `runtime-fish-pond-management-20260830-013602` 覆盖换网和确认清塘两支。最新 schema 为 `145/129/16/0`，对账为 `174 registered / 202 semantic / 173 compiler-bound / 100 five-gate / 45 allowlist / 28 catalogued blocked / 0 Product Executor`，回归为 Core `2027/2027`、Backend `142/142`。正式全量训练仍受剩余 28 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `foraging.harvest_fruit_tree`。

## 2026-08-30 展览会转盘策略与原生随机执行闭环（EVD-296）

`festival.spin_wheel` 已完成五道证据闭环并进入 `StrategyValue` allowlist。模型只决定是否用绿方转盘补齐未获得 Fair Stardrop 的至少两枚星币缺口；编译器从 fresh snapshot 绑定节日、Buildings `308/309`、站位、节庆币、需求、零幸运 `22/30` 分布、有效 `LuckLevel`、数字菜单和 native contract。下注严格为 `min(remainingDemand, floor(festivalScore * 7 / 15))`，即等赔率零幸运 Kelly 比例，不把 50% 误标为 Kelly；`executor.spin_fair_wheel` 保持 `ExecutorCalibration` 与 policy confirmation。

运行层复用共享 BFS 和原生菜单输入，只经 `Event.checkAction -> DialogueBox(Green) -> NumberSelectionMenu -> WheelSpinGame` 启动，接受原版随机胜负并核对精确 `+/- wager`、结果文字与退出。最终隐藏静音运行 `runtime-fair-wheel-spin-20260830-005054` 用两次 `466` 星币下注覆盖胜负两支，festivalScore 分别 `1000->1466` 与 `1000->534`；生产代码不写 RNG、旋转、结算或结果。最新 schema 为 `145 required / 129 readable / 16 contextual / 0 blocking`，对账为 `173 registered / 202 semantic / 172 compiler-bound / 99 five-gate / 45 allowlist / 29 catalogued blocked / 0 Product Executor`，回归为 Core `2023/2023`、Backend `138/138`、Release `0 warnings / 0 errors`。正式全量训练仍受剩余 29 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `fishing.manage_fish_pond`。

## 2026-08-29 展览会力量小游戏策略与原生执行闭环（EVD-295）

`festival.play_strength_game` 已完成五道证据闭环并进入 `StrategyValue` allowlist。模型只在扣除未领取陈列奖励后，未获得 Fair Stardrop 的缺口恰好为 `1` 星币时决定是否进行这次免费尝试；其他缺口不会把单次固定一币的小游戏误当作高效循环。编译器从 fresh snapshot 绑定节日实例、Buildings `540`、站立 X=`29`、力量/速度/方向、动画和计时合同、商店需求及 native contract；`executor.play_fair_strength_game` 严格为 `ExecutorCalibration`。

运行层复用共享 BFS，等待移动输入结算后通过真实 `Event.checkAction` 打开 `StrengthGame`，以点击后恰好 `9` 次原生更新预测满力量窗口并只调用一次原生点击。隐藏静音样本覆盖两种初始速度：`64/+4 -> 100` 与 `72/+3 -> 99`，均由原版把 festivalScore `1999->2000` 并完成结果对话/退出；生产代码不直接写力量、得分、计时器、菜单或位置。最新 schema 为 `143 required / 127 readable / 16 contextual / 0 blocking`，对账为 `171 registered / 201 semantic / 170 compiler-bound / 97 five-gate / 44 allowlist / 30 catalogued blocked / 0 Product Executor`，回归为 Core `2018/2018`、Backend `138/138`、Release `0 warnings / 0 errors`。正式全量训练仍受剩余 30 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `festival.spin_wheel`。

## 2026-08-29 展览会靶场策略与原生执行闭环（EVD-294）

`festival.play_slingshot_game` 已完成五道证据闭环并进入 `StrategyValue` allowlist。模型只决定是否投入 50g 和原版 50 秒会话补齐未获得 Fair Stardrop 的星币缺口；尚未领取的展览陈列奖励会先从缺口扣除。编译器从 fresh snapshot 绑定节日实例、交互/站立图块、金额、星币、四段时序、79 个目标、Dialogue key 和 native contract；`executor.play_fair_slingshot_game` 严格为 `ExecutorCalibration`。

运行层复用共享移动与普通矿井弹弓唯一的瞄准补丁，在原生 TargetGame 物理更新前预测拦截点并发送按下/蓄力/释放输入。验收精确核对 50g、临时弹弓和弹药、shots/success、原版 accuracy 分母、75/85/90/95/100% 倍率、得分、40 分奖励门、280 分封顶、节庆返回和临时物品清理，不直接写任何结果。隐藏静音样本为 `48/48` 命中、raw `95`、accuracy `102`、final `380`、`500` 星币。最新 schema 为 `142 required / 126 readable / 16 contextual / 0 blocking`，对账为 `169 registered / 200 semantic / 168 compiler-bound / 95 five-gate / 43 allowlist / 31 catalogued blocked / 0 Product Executor`，回归为 Core `2013/2013`、Backend `138/138`、Release `0 warnings / 0 errors`。正式全量训练仍受剩余 31 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `festival.play_strength_game`。

## 2026-08-29 展览会钓鱼小游戏策略与原生执行闭环（EVD-293）

`festival.play_fishing_game` 已完成五道证据闭环并进入 `StrategyValue` allowlist。模型只决定是否投入 50g 和原版 100 秒来补齐未获得 Fair Stardrop 的星币缺口；上游会扣除尚未领取的展览陈列奖励，不会为其他装饰商店行无限重复。编译器从 fresh snapshot 绑定节日实例、交互/站立图块、金额、星币、缺口、时长、Dialogue key 和 native contract；`executor.play_fair_fishing_game` 严格为 `ExecutorCalibration`。

运行层复用共享移动和普通钓鱼预测输入，在游戏物理更新前控制原生 BobberBar。运行验收不把随机完美率误作执行器稳定性：必须精确验证 50g、真实 FishingGame、原版 raw score + perfection bonus + triple-perfect multiplier、星币公式、节日返回和临时钓具清理，完美数/有效鱼数则作为收益反馈。最终隐藏静音样本为 `5/5` 完美、`364` 分、`432` 星币。最新 schema 为 `141 required / 125 readable / 16 contextual / 0 blocking`，对账为 `167 registered / 199 semantic / 166 compiler-bound / 93 five-gate / 42 allowlist / 32 catalogued blocked / 0 Product Executor`。Core `2008/2008`、Backend `138/138`、Release `0 warnings / 0 errors`。正式全量训练仍受剩余 32 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `festival.play_slingshot_game`。

## 2026-08-29 展览会陈列策略与原生执行闭环（EVD-292）

`festival.manage_grange_display` 已完成五道证据闭环并进入 `StrategyValue` allowlist。模型只决定是否在展览会准备一等奖陈列或在评审后取回；透明桥与编译器从 fresh snapshot 绑定共享展台、库存单位、实际售价、品质、八类多样性、九件数量分、Mayor 短裤、评审状态、交互图块、互斥锁和下一次唯一机械操作。`executor.manage_grange_display` 严格为 `ExecutorCalibration`，每个快照只允许一次原生放入/取回，不进入策略训练，也不得启动评审。

隐藏静音隔离运行 `10/10` 通过：九次原生菜单放入达到 `124` 分，超过一等奖阈值 `90`，评审后一次原生取回；生产链复用共享 BFS/连续移动与 `Event.checkAction -> StorageContainer -> grangeMutex`，不直接写展台、库存、评分或评审状态。最新 schema 为 `140 required / 124 readable / 16 contextual / 0 blocking`，对账为 `165 registered / 198 semantic / 164 compiler-bound / 91 five-gate / 41 allowlist / 33 catalogued blocked / 0 Product Executor`。Core `2003/2003`、Backend `138/138`、Release `0 warnings / 0 errors`。正式全量训练仍受剩余 33 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `festival.play_fishing_game`。

## 2026-08-29 传送图腾执行器校准（EVD-291）

`executor.use_warp_totem` 已完成五道执行证据闭环，训练角色严格为 `ExecutorCalibration`。上游策略只决定目的地和“是否值得消耗”；机械链从 fresh snapshot 重绑五种精确库存、Farm 地图属性/农场回退、固定目的地、主动与被动节日路由、地图边缘修正、2000ms 动画和 1000ms 原生回调。会消耗但不传送的节日前分支、联机 ReadyCheck、精确目的地重复使用和基础物品门失败均在消费前排除。

隐藏静音隔离运行五变体 `5/5` 验证原生单物品消费、68 个即时效果精灵、五个精确落点和最终角色状态恢复。最新 schema 为 `139 required / 123 readable / 16 contextual / 0 blocking`，对账为 `163 registered / 197 semantic / 162 compiler-bound / 89 five-gate / 40 allowlist / 34 catalogued blocked / 0 Product Executor`。该切片不进入训练 allowlist，也不解除 Product Executor、剩余 34 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等阻挡。下一语义切片为 `festival.manage_grange_display`。

## 2026-08-29 宝藏图腾执行器校准（EVD-290）

`executor.use_treasure_totem` 已完成五道执行证据闭环，训练角色严格为 `ExecutorCalibration`。上游策略只决定是否值得在当前位置消耗图腾；机械链从 fresh snapshot 重绑精确库存、室内门、中心周围 16 格原生候选环、每格可生成原因、`TreasureTotemsUsed` 和时序合同。公共物品门失败、室内或零可生成格会在消费前排除，不能依赖下游失败补救。

隐藏静音隔离运行验证原生 `16/16` 宝藏点生成、图腾 `2->1`、世界计数 `0->1` 和地点宝藏点 `5->21`。生成结果由既有宝藏点读取与挖掘链继续处理，不训练也不复制机械挖掘。最新 schema 为 `138 required / 122 readable / 16 contextual / 0 blocking`，对账为 `162 registered / 197 semantic / 161 compiler-bound / 88 five-gate / 40 allowlist / 35 catalogued blocked / 0 Product Executor`。该切片不进入训练 allowlist，也不解除 Product Executor、剩余 35 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等阻挡。下一语义切片为 `executor.use_warp_totem`。

## 2026-08-29 回城魔杖执行器校准（EVD-289）

`executor.use_return_scepter` 已完成五道执行证据闭环，训练角色严格为 `ExecutorCalibration`。上游只决定“现在是否值得立即回家”；机械链从 fresh snapshot 重绑精确 `Wand`、当前角色自己的 `FarmHouse`/`Cabin`、门前格、稳定输入门、即时工具调用、1000ms 原生回调和最终状态。已在落点、住宅不可解析、浴衣、桥上或执行瞬态不稳定时均在候选/编译阶段排除，不能等到原生回调后再补救。

隐藏静音隔离运行验证房主分支的原生落点、29 精灵即时状态、最终显示/无敌/移动恢复和可复用库存。最新 schema 为 `137 required / 121 readable / 16 contextual / 0 blocking`，对账为 `161 registered / 197 semantic / 160 compiler-bound / 87 five-gate / 40 allowlist / 36 catalogued blocked / 0 Product Executor`。该切片不进入训练 allowlist，也不解除 Product Executor、剩余 36 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等阻挡。下一语义切片为 `executor.use_treasure_totem`。

## 2026-08-29 雨水图腾执行器校准（EVD-288）

`executor.use_rain_totem` 已完成五道执行证据闭环，但训练角色严格为 `ExecutorCalibration`。上游只决定未来天气是否值得消耗图腾；机械链从 fresh snapshot 绑定精确库存、上下文许可/重定向、天气状态归属、明日日期、换日最终天气、动画和对话结算。官方 Wiki 关于季节首日的可见规则已用锁定 1.6.15 的 `Game1.getWeatherModificationsForDate` 复核并扩大为完整默认上下文覆盖门，不能只凭即时 `WeatherForTomorrow=Rain` 认定有效。

隐藏静音隔离运行覆盖 Default、Desert->Default、Island 和默认节日前拒绝四条分支。最新 schema 为 `136 required / 120 readable / 16 contextual / 0 blocking`，对账为 `160 registered / 197 semantic / 159 compiler-bound / 86 five-gate / 40 allowlist / 37 catalogued blocked / 0 Product Executor`。该切片不进入训练 allowlist，也不解除 Product Executor、剩余 37 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等阻挡。下一语义切片为 `executor.use_return_scepter`。

## 2026-08-29 怪兽香水执行器校准（EVD-287）

`executor.use_monster_musk` 已完成五道执行证据闭环，但训练角色严格为 `ExecutorCalibration`。小模型或上游战斗目标只决定是否需要提高怪物密度；精确库存槽、Buff 24 当前状态、消费、朝向、动画时序和刷新回执全部由机械编译/执行链负责。普通矿井与火山地牢都按在线玩家 Buff 24 将怪物生成率乘以 2，普通矿井与驱怪 Buff 23 的组合继续服从原生优先级。

隐藏静音隔离运行覆盖“无 Buff 首次施加”和“已有 Buff 替换刷新”两条分支。最新 schema 为 `135 required / 119 readable / 16 contextual / 0 blocking`，对账为 `159 registered / 197 semantic / 158 compiler-bound / 85 five-gate / 40 allowlist / 38 catalogued blocked / 0 Product Executor`。该切片没有把机械原语直接加入策略训练，也不解除 Product Executor、剩余语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等阻挡。下一语义切片为 `executor.use_rain_totem`。

## 2026-08-29 马笛执行器校准（EVD-286）

`executor.use_horse_flute` 已完成五道执行证据闭环，但训练角色严格为 `ExecutorCalibration`：策略只需决定是否用马笛，机械层从 fresh snapshot 重绑库存、限制掩码、拥有马匹身份、邻近状态、朝向和延迟结果。模型不得预测或复制原生 team event、mutex 与传送副作用。

隐藏静音隔离运行覆盖远程 1500ms 召回与邻近成功无传送两条分支，并验证同一马匹 GUID、精确落点、朝向规则和可复用库存。最新 schema 为 `134 required / 118 readable / 16 contextual / 0 blocking`，对账为 `158 registered / 197 semantic / 157 compiler-bound / 84 five-gate / 40 allowlist / 39 catalogued blocked / 0 Product Executor`。因此该切片不解除 Product Executor、剩余 39 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等全量训练阻挡。下一语义切片为 `executor.use_monster_musk`。

## 2026-08-29 烟花玩家命令边界（EVD-285）

`executor.use_firework` 已闭合五道执行证据，但训练 allowlist 保持 `40`。烟花是显式玩家表达命令，不是第三年爷爷 21 分路线、日循环或资源规划的自主欲望；相关运行样本只属于 `player_command_only_executor_evidence`。模型不能学习或猜测共享 RNG 的精确下一值。

隐藏静音隔离运行已验证 `(O)893/(O)894/(O)895` 三个分支、原生 5 精灵图、目标格冲突、随机域和精确单件消耗。最新 schema 为 `133 required / 117 readable / 16 contextual / 0 blocking`，对账为 `157 registered / 197 semantic / 156 compiler-bound / 83 five-gate / 40 allowlist / 40 catalogued blocked / 0 Product Executor`。因此该切片不解除 Product Executor、剩余 40 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等全量训练阻挡。下一语义切片为 `executor.use_horse_flute`。

## 2026-08-29 秘密纸条执行器校准（EVD-284）

`executor.read_secret_note` 已完成五道执行证据闭环，但训练角色仍为 `ExecutorCalibration`：它校准透明桥、编译器和执行器能否忠实完成一个已由上游选择的纸条读取，不让策略模型学习原生随机数、菜单构造或库存扣减。普通纸条与日记残页的选择均在 fresh snapshot 中机械计算，小模型不能伪造 note id 或任务副作用。

隐藏静音隔离运行覆盖多未读种子抽取、任务 30、任务 29 和普通日记残页四条分支。最新 schema 为 `132 required / 116 readable / 16 contextual / 0 blocking`，对账为 `156 registered / 197 semantic / 155 compiler-bound / 82 five-gate / 40 allowlist / 41 catalogued blocked / 0 Product Executor`。因此该切片不解除 Product Executor、剩余 41 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等全量训练阻挡。下一语义切片为 `executor.use_firework`。

## 2026-08-29 草种放置执行器校准（EVD-283）

`executor.plant_grass` 已完成五道执行证据闭环，但训练角色严格为 `ExecutorCalibration`：它证明动作编译和执行器能忠实完成上游给定的普通/蓝草精确布局，不为策略模型生成“应该在哪里种草”的价值标签。上游必须给出用途、精确地块和时间预算；编译器从 fresh snapshot 重绑所有机械字段。

隐藏静音隔离运行已验证 `(O)297 -> Grass(1,4)` 与 `(O)BlueGrassStarter -> Grass(7,4)`，库存精确减一且透明后状态可读。最新 schema 为 `131 required / 115 readable / 16 contextual / 0 blocking`，对账为 `155 registered / 197 semantic / 154 compiler-bound / 81 five-gate / 40 allowlist / 42 catalogued blocked / 0 Product Executor`。因此该切片不解除 Product Executor、剩余 42 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等全量训练阻挡。下一语义切片为 `executor.read_secret_note`。

## 2026-08-29 Drum Block 玩家命令边界（EVD-282）

`world.tune_drum_block` 已闭合五门执行证据，但训练 allowlist 保持 `40`。调音属于显式玩家表达/谜题布置，不是自主日计划欲望；运行数据只属于 `player_command_only_executor_evidence`。路过自动播放是独立对象邻接回调，不生成第二个训练动作。

当前权威状态为 `154 registered / 197 semantic / 153 compiler-bound / 80 five-gate / 43 catalogued blocked / 0 Product Executor`。该切片不解除 Product Executor、剩余 43 个动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等全量训练阻挡。下一语义切片为 `executor.plant_grass`。

## 2026-08-29 Flute Block 玩家命令边界（EVD-281）

`world.tune_flute_block` 已闭合五门执行证据，但训练 allowlist 保持 `40`。调音是玩家表达/谜题布置命令，不是自主日计划欲望；运行数据只属于 `player_command_only_executor_evidence`。路过自动播放是对象邻接回调，不生成第二个训练动作。

当前权威状态为 `153 registered / 197 semantic / 152 compiler-bound / 79 five-gate / 44 catalogued blocked / 0 Product Executor`。该切片不解除 Product Executor、剩余 44 个动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等全量训练阻塞。

## 2026-08-29 Farm Computer 透明信息与训练边界（EVD-280）

`farming.read_farm_computer_report` 已闭合五门执行证据，但训练 allowlist 保持 `40`。透明桥已经按原生根地点语义直接发布报告的全部结构化来源和精确本地化摘要，因此策略模型可直接使用这些状态；打开 Farm Computer 只服务显式玩家查看，不得制造“先读菜单才能决策”的训练依赖。

运行时只复用共享移动器并调用一次原生地点交互，验证 500ms 延迟 `DialogueBox`、报告摘要、对象身份及槽位恢复。当前权威状态为 `152 registered / 197 semantic / 151 compiler-bound / 78 five-gate / 45 catalogued blocked / 0 Product Executor`。该切片减少一个执行缺口，但不解除 Product Executor、剩余 45 个动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等全量训练阻塞。

## 2026-08-29 Mini-Obelisk 校准边界（EVD-279）

`movement.use_mini_obelisk` 已闭合五门执行证据，但不增加训练 allowlist。它的作用是校准“机械路由原语能否严格复刻原生配对、目标和落点”，而不是让策略模型学习是否想传送。默认策略候选排除该动作；只有显式启用执行器校准候选时才发布，运行结果标记为 `executor_calibration_only_not_strategy_desire`，数据清洗不得把成功、耗时或落点当作策略偏好标签。

当前准入计数仍为 `40`，权威状态为 `151 registered / 197 semantic / 150 compiler-bound / 77 five-gate / 46 catalogued blocked / 0 Product Executor`。因此这次闭环减少了一个原版动作缺口，但没有解除 Product Executor、剩余 46 个动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等正式全量训练阻塞。

## 2026-08-28 Auto-Grabber 训练准入与兼容占位边界（EVD-277 / EVD-278）

`animals.collect_auto_grabber_contents` 已进入训练 allowlist。模型只决定是否在当前日计划中收取；精确对象、站位、安全槽、held Chest 全部堆栈身份、累计背包容量、转移集合和留存集合均由新鲜快照与编译器决定。空容器、全部物品不可接纳或无安全站位不会生成候选。生产执行只调用原生对象交互并向 `ItemGrabMenu` 发送原生点击，验收要求源集合严格等于“已转移 + 留存”、背包回执匹配、对象/Chest 身份不变且菜单关闭。

Lantern/Raft 是分母外兼容占位，不是训练缺口，也不得生成负样本。当前准入计数为 `40`，完整权威状态为 `150 registered / 197 semantic / 149 compiler-bound / 76 five-gate / 47 catalogued blocked / 0 Product Executor`。这只增加一个经过原生运行验证的策略动作，不改变正式全量训练仍受 Product Executor、剩余 47 个动作、正式轨迹 manifest/checkpoint、独立存档评测及第三年 21 分长跑验收阻塞的结论。

## 2026-08-28 喂食斗训练准入与木筏分母修正（EVD-275 / EVD-276）

`animals.withdraw_feed_hopper_hay` 已通过五门证据并进入训练 allowlist。训练样本只允许来自“当前动物屋至少有一只未喂动物、原生精确取草量为正、背包可接纳、菜单与站位安全”的候选；编译器必须从同一新鲜快照重绑根料仓、动物数、容量、已摆干草、取草量、安全槽和站位。生产运行只调用一次原生 `GameLocation.checkAction`，并以料仓 `-N`、背包 `(O)178 +N` 的守恒回执验收。E 盘隐藏静音运行 `runtime-feed-hopper-20260828-130723` 验证 `N=8`。

不可达的 `Raft` 遗留类型不属于训练缺口。锁定 1.6.15 没有原版获取或调用入口，因此该历史检查点把语义分母校正为 `198`；当前由 EVD-277 以分母外兼容占位保留原生表面。该检查点状态为 `149 registered / 198 semantic / 148 compiler-bound / 75 five-gate / 39 allowlist / 49 catalogued blocked / 0 Product Executor`；这仍不解除 Product Executor、正式数据 manifest/checkpoint、独立存档评测和第三年 21 分长跑阻塞。

## 2026-08-28 PlayerCommandOnly 原生声音交互边界

`world.play_singing_stone` 已闭合五门执行证据，但正式训练准入保持关闭。锁定 1.6.15 原生语义只允许透明发布共享 RNG 的 24 项均匀音高分布，不允许预读或猜测下一音高。显式玩家命令经确认后，编译器重绑精确 `(BC)94`、站位和安全槽，运行时通过共享移动器调用一次原生地点交互并验证 `shakeTimer=100`、对象身份与槽位恢复。其证据范围为 `player_command_only_executor_evidence`，默认候选、策略请求和训练清洗分别通过生成、授权与类型化排除三层阻断。当前 allowlist 仍为 `38`，没有因为该执行器验证而增长。

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
## 2026-08-04 EVD-214 更新

训练白名单现为 17 个有界范围。`farm.load_supported_machine_input` 仅在当前地图的精确已摆放机器支持
意图下准入，并要求实时确定性正净值、零附加耗材、精确玩家槽数量未被其他目标预留，以及账本、
预测和路线在编译/派发时没有漂移。隐藏静默 E 盘运行已完成原生投料、处理开始、训练行写入和意图
完成对账。该结果没有解除正式全量训练阻塞：Product Executor 仍为 0，生产轨迹、跨度观测、正式
manifest/checkpoint 和第三年 21 分独立评估仍未完成；广义机器策略和完整制作-摆放-投料生命周期也
仍在准入范围之外。

## 2026-08-04 机器容量生命周期编排更新

`farm.establish_supported_machine_capacity` 已登记并接入单一滚动编排链。它只服务
`goal.economy.earn_money` 的有界正收益容量缺口，并按持久 `MachineSupportIntent` 在制作、精确摆放、
首次投料三个既有执行分支之间推进；每个新快照最多选择一个当前阶段，摆放执行失败时保持原目标重试，
无效意图则失败关闭。阶段测试已证明它不会在非赚钱目标下排名，也不会复制执行器。

EVD-215 已通过隐藏静默隔离运行 `runtime-supported-machine-capacity-20260804-120211`。同一高层选项
连续驱动原生制作、规划器精确摆放、确定性首次投料、处理开始、意图完成和三条训练记录；五门闭合数
现为 19，allowlist 为 18。该准入只覆盖当前地图、有界正收益、零附加耗材且无预留冲突的单机器容量
生命周期，不覆盖任务/收集需求、远程摆放、随机机器或广义 `farm.process_machines`。

正式全量训练仍未准入：Product Executor 仍为 0，生产长 rollout、闭合跨度观测、正式 manifest、
生产 checkpoint、独立存档评测和第三年 21 分长跑尚未完成。下一开发切片是任务/收集需求机器处理，
之后继续按权威字典扩大五门范围并采集真实长期轨迹。

## 2026-08-11 每日委托接受链更新

`quest.accept_daily` 与 `executor.accept_daily_quest` 已注册并接通透明桥、上游候选排除、滚动跨地图接近、
DailyPlan、动作队列和原生 Billboard 点击。隔离 E 盘运行已验证同一原生 offer 进入任务日志且保留两天期限；
安装后的真实 full 快照为 required 103、blocking 0。两项仍为 RegisteredOnly，五门闭合数和训练白名单仍分别为
39 与 26；需要重复跨日、无任务、过期及联机归属证据后才能改变准入状态。该闭环不解除正式全量训练阻塞。

## 2026-08-11 特别订单接受链更新

`quest.accept_special_order` 与 `executor.accept_special_order` 已按单一实现覆盖 Town、Qi 和沙漠节庆三种原生入口，
接通实时左右 offer、上游许可排除、滚动接近、原生开板/对话以及精确选择。Town 隐藏静默隔离运行已验证原生
互斥锁延迟和 key、generation seed、fingerprint、accepted type 的一致回执；安装后的真实 full 快照为
required 104、blocking 0。Qi 与沙漠节庆只有锁定版本反编译和结构覆盖，必须分别完成运行校准后才能形成对应证据。

动作对账现为 111 registered / 176 semantic / 110 compiler-bound；five-gate 仍为 39，训练白名单仍为 26。
特别订单高层项和原语继续保持 RegisteredOnly，一次 Town 通过不构成正式训练准入，也不解除 Product Executor、
长期 rollout、独立存档评测和第三年 21 分长跑等全量训练阻塞项。

## 2026-08-27 彩虹尽头奖励准入（EVD-268）

`rewards.claim_pot_of_gold` 已按锁定 1.6.15 原生规则闭合五门并进入训练白名单。模型不输出坐标、站位、数量或领取细节；编译器从最新 `current_location.pot_of_gold_reward` 绑定 Forest、`52,98`、可用相邻格、春 17 日、`min(100, 7 + year)` 金币和帽子契约。隐藏静音满背包运行验证原生 `GameLocation.checkAction` 产生第二年 9 个金币 debris 与 1 顶帽子，随后由既有 debris 拾取链处理，不建立第二套奖励转移系统。

最新 full 快照为 `128 required / 112 readable / 16 contextual / 0 blocking`；权威对账为 `143 registered / 199 semantic / 142 compiler-bound / 69 five-gate / 37 allowlist / 56 catalogued blocked / 0 Product Executor`。该单项准入不解除正式全量训练对 Product Executor、生产长 rollout、冻结正式数据 manifest、独立存档评测和第三年 21 分长跑的总体阻塞。

## 2026-08-27 矮人王雕像每日能力准入（EVD-269）

`mining.choose_dwarf_statue_power` 已按锁定 1.6.15 原生规则闭合五门并进入训练白名单。透明桥实时发布采矿精通门、当前地图全部精确基础雕像、可达站位、由 `DaysPlayed*77 + uniqueID` 生成的两个不同选项、五种原生效果分支和已有 buff 锁。模型必须选择一个当天真实提供的 `power_id`；编译器拒绝缺失或伪造的 ID，并从新快照重绑菜单索引、buff、雕像和站位。

隐藏静音 E 盘运行对当天两个选项 `0/3` 分别验证了原生 `Object.checkForAction -> ChooseFromIconsMenu.receiveLeftClick`、唯一选中 buff 和菜单关闭回执。最新 full 快照为 `129 required / 113 readable / 16 contextual / 0 blocking`；权威对账为 `144 registered / 199 semantic / 143 compiler-bound / 70 five-gate / 38 allowlist / 55 catalogued blocked / 0 Product Executor`。该单项准入不解除正式全量训练对 Product Executor、生产长 rollout、冻结正式数据 manifest、独立存档评测和第三年 21 分长跑的总体阻塞。

## 2026-08-27 祝福雕像每日奖励准入（EVD-270）

`rewards.claim_statue_blessing` 已按锁定 1.6.15 原生规则闭合五门并进入训练白名单。它是无参数领取目标，不是策略选择菜单：透明桥预测当天唯一祝福，并发布农业精通、日锁、天气/节日分母、七种效果、当前地图精确基础雕像和相邻站位；编译器覆盖模型伪造的所有机械字段。

## 2026-08-27 House Plant 原生轮转准入（EVD-271）

`world.rotate_house_plant` 已按锁定 1.6.15 原生规则闭合五门。它是显式装饰目标，不是自主维护候选：模型选择当前地图中的一盆精确基础 House Plant，编译器从最新 `current_location.objects[]` 重绑永久物品身份、当前/预期 `ParentSheetIndex`、相邻站位、真正空工具栏槽位、恢复槽位和原生契约。工具槽不能替代空槽，因为起始帧 7 在空手 `GameLocation.checkAction` 下会触发地点层第二次对象调用，一次交互的真实结果为 `7→1`。

生产执行只复用共享 BFS 和一次地点级原生交互，不直接写贴图帧或调用对象级方法。透明桥、候选和运行时同时防守原生四向不可通行对象包围时会触发的 `performToolAction(null)` 破坏性前导分支。隐藏静音 E 盘矩阵对 0..7 全部通过，并验证永久 ID 与槽位不变。该项进入证据白名单但 `AutonomousCandidateEnabled=false`，普通日计划不会擅自改变玩家装饰。当前对账为 `146 registered / 199 semantic / 145 compiler-bound / 72 five-gate / 40 allowlist / 53 catalogued blocked / 0 Product Executor`；下一切片是 `farming.collect_slime_ball`。

生产执行只复用共享 BFS 和原生 `GameLocation.checkAction`，不直接施加 buff 或写日锁。隐藏静音 E 盘运行验证当天 `statue_of_blessings_1` 唯一回执和 `hasBeenBlessedByStatueToday=true`。最新 full 快照为 `130 required / 114 readable / 16 contextual / 0 blocking`；权威对账为 `145 registered / 199 semantic / 144 compiler-bound / 71 five-gate / 39 allowlist / 54 catalogued blocked / 0 Product Executor`。该单项准入仍不解除 Product Executor、生产长 rollout、正式 manifest/checkpoint、独立存档评测和第三年 21 分长跑阻塞。

# StardewAI 当前工作

## 2026-08-26 当前权威检查点：EVD-264

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

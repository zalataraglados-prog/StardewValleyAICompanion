# StardewAI 短交接：EVD-300 垃圾桶翻找

## 已完成

- 唯一动作链 `foraging.rummage_garbage -> rummage_garbage -> executor.rummage_garbage` 已完成 read、上游排除、DailyPlan、fresh 编译重绑定、类型化请求、共享 BFS、原生 `GameLocation.checkAction`、输出守恒、任务反馈和五门准入。
- 透明桥在一次 Buildings 扫描中发布当前地图所有 Garbage action。每行包含桶 ID、每日已查状态、统计、DailyLuck、Alleyway Buffet、锁定数据哈希、无副作用确定预测、选中条目、完整输出状态/交付方式、NPC 反应、安全槽和投影指纹。
- 任务收集链只增加来源绑定：普通 `ResourceCollectionQuest` 与特别订单 Collect 均可使用垃圾桶确定输出，后续领取仍复用既有原生任务反馈与 debris 拾取。

## 原生与安全边界

- 主合同来自锁定 1.6.15 `performAction Garbage -> CheckGarbage -> TryGetGarbageItem` 和实时 `Data/GarbageCans`，数据 payload SHA-256 为 `34621d9c92c472019c6e0a6bae4ac86a62576b7bccae4b9191590ed11e46911f`。Wiki 只作八个 Town 桶、日级确定性和 NPC 反应的二次核对。
- 无输出是有效成功；已查桶和负友谊目击在上游排除；Linus 精确正友谊分支允许。未知/漂移数据、未知非 Linus 目击、容量/安全槽或路线异常全部 fail closed。
- 运行时不直接写 CheckedGarbage、统计、好感、库存、debris 或 RNG，只调用一次原生 checkAction 并核对后状态。

## 验证与下一步

- 隐藏静音 E 盘运行 `9/9` PASS，覆盖七种夹具状态和两类任务收集回执。Core `2039/2039`、Backend `145/145`、Release `0 warnings / 0 errors`，KnowledgeCompiler `585/585`、blocking `0`。
- schema 为 `146/130/16/0`；对账为 `180 registered / 205 semantic / 179 compiler-bound / 103 five-gate / 48 allowlist / 25 catalogued blocked / 0 Product Executor`。
- 下一冻结语义切片是 `housing.renovate`。先实时反编译 `RenovateMenu` 的全部原版选项、前置条件、费用、房屋几何变化、物品/NPC 冲突、可逆性和持久化回执，再判定自主候选与 `PlayerCommandOnly` 边界；复用既有房屋、菜单、确认和移动体系，不建立第二套建筑管理实现。

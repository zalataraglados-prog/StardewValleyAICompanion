# StardewAI 短交接：秘密遗失物状态机 EVD-237

## 已确认

- 锁定版 `Data/Quests` 中只有任务 128/129 是 `SecretLostItemQuest`，目标均为 `(O)191`。
- `Railroad.getFish` 在同一原生调用中检查秘密纸条 25、设置 `carolinesNecklace` pending、加入任务 128/129 并返回项链。
- `SecretLostItemQuest.OnItemReceived` 随后才把 `itemFound` 置真。因此 `itemFound=false` 是取得事务中的瞬态，不是可再次派发的独立任务动作。
- 透明桥既有 `railroad_carolines_necklace` 投影和唯一 `fishing.catch_fish -> executor.catch_fish` 链已经拥有该动作；不得新增任务专用钓鱼执行器。
- 满包时原生 `FishingRod.doneHoldingFish` 会打开必要的 `ItemGrabMenu`。候选与执行器起始复核统一要求至少一个空背包格；不满足时先走既有存储转移链，避免一次性项链卡在领取菜单。

## 本次结果

- 目录从 `23 bound / 3 blocked / 2 observation-only` 修正为 `23 bound / 2 blocked / 3 observation-only`。
- 新增测试锁定铁路项链唯一结果分布 `(O)191`、DailyPlan 与动作队列复用。
- Core 1617/1617、Backend 121/121、Release 构建 0 错误；KnowledgeCompiler 585/585、blocking 0。
- 未启动游戏；EVD-228 只作为无 BobberBar 特殊收获原生生命周期的继承证据，不声称已单独校准项链案例。

## 下一步

处理 `Quest` type 11 的 `weeding_no_subclass`。必须先从锁定 `Data/Quests`、任务解析器、原生除草/物品收货回调和实际任务 ID 枚举其精确终点，再决定它是独立动作、既有动作组合还是另一个仅观察状态。禁止从显示文本或单个示例推断。完成透明读取、候选、DailyPlan、动作队列、原生运行反馈与输出证据后，再处理 `JKScoreObjective`。

在 type-11 与 Junimo Kart 均关闭前，`quest.advance` 继续保持 `PartiallyBlocked / RegisteredOnly`，不得进入训练白名单。

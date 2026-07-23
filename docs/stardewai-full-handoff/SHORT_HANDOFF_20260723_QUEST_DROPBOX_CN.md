# StardewAI 特别订单投递箱短交接（2026-07-23）

## 本轮完成

- `DonateObjective` 已从统一阻塞切换为类型化执行候选。
- 透明桥新增 `current_location.drop_box_action_tiles`，从当前地图
  `Action = "DropBox <box_id>"` 实时索引真正的交互格。
- `dropBoxTileLocation` 仅作为任务指示图标坐标，不再被误用为交互格。
- `DonateObjective.GetDropboxLocationName()` 的解析结果单独写入
  `resolved_drop_box_game_location`，覆盖 Trailer 升级后的原生地点切换。
- 跨图先执行一个 connector，携带精确 `quest_candidate_id` continuation；
  到达目标地图并取得新快照后才绑定投递箱 Action 格。
- 同图计划编译为 `move_to_tile` 加 `quest_drop_box_donate`。
- 运行时只通过 `GameLocation.checkAction`、订单原生 donate mutex 和
  `QuestContainerMenu.receiveLeftClick`/OK 确认执行。
- 不直接写 `OrderObjective.currentCount`、`DonateObjective.confirmed` 或
  `SpecialOrder.donatedItems`。

## 运行时防漂移

- 重新核对订单 key、目标 index、运行状态、box ID 和解析后的地点。
- 重新核对 Action 格、相邻站位、背包槽、物品 ID、栈数量和原生
  `SpecialOrder.GetAcceptCount`。
- 菜单打开后核对 `QuestContainerMenu` 绑定的是同一订单的
  `donatedItems`，并要求 donate mutex 已持有。
- 确认后核对物品栈减少、选中目标进度增加，以及达到目标时
  `confirmed=true`。

## 验证与制品

- Core：1109/1109。
- Backend：67/67。
- RuntimeTestHarness：构建成功，只有既有 Cat/Dog 过时警告。
- 权威制品：
  `I:\StardewAI-KnowledgeArtifacts\game-1.6.15\derived\game-1.6.15-20260723T093543Z-linux-v21`
- v21：89 options、61 step compilers、59 runtime dispatch、字段 blocker 0、
  下游 blocker 0。
- 本轮未运行游戏实机变更测试；原生菜单闭环仍需隔离存档校准。

## 下一步

1. 将 `CollectObjective` 绑定到现有采集、收获、制作和购买候选。
2. 将 `FishObjective` 绑定到现有完美钓鱼候选。
3. 将 `GiftObjective` 绑定到原生送礼终端并复核最低喜好等级。
4. 将 `SlayObjective` 绑定到普通矿洞、骷髅洞和火山的目标怪物候选。
5. `JKScoreObjective` 保持 fail-closed，直到原生小游戏执行器单独实现。

# StardewAI 任务类型化执行短交接（2026-07-23）

## 本轮完成

- `quest.advance` 不再把所有任务统一标成
  `quest_native_executor_not_implemented`。
- 普通任务已绑定：指定鱼捕获、到达地点、NPC 交付、杀怪/钓鱼/采集回报、
  社交问候、丢失物归还。
- 特别订单已绑定：`DeliverObjective`、`ShipObjective`、
  `ReachMineFloorObjective`。
- 机械部分复用现有钓鱼、跨图路线、出货和完美矿洞执行器。
- 新增唯一必要终点 `executor.quest_npc_interact`，通过原生
  `GameLocation.checkAction` 完成交付或回报。
- 跨图 NPC 任务带精确 `quest_candidate_id` continuation，重规划不能切换成
  普通聊天或其他任务。

## 反幻觉和原生顺序

- 编译器重新核对任务 ID、运行时类型、特别订单 objective index、当前计数、
  目标计数、NPC 位置、站位、物品槽位和物品 ID。
- 运行时先执行游戏原生 `probe:true`。
- NPC 原生顺序是先特别订单、再逆序普通任务；执行器要求“第一个原生接收者”
  就是计划目标，防止同一物品被另一任务抢先消耗。
- 实际修改只通过 `GameLocation.checkAction`；不得直接写任务计数、完成态或
  `donatedItems`。
- 后验必须看到同一目标计数变化、完成或从任务列表移除，否则记录 blocked。

## 仍然阻塞

- 普通任务：制作、采集过程、杀怪过程、收获、建筑、丢失物拾取、接受任务和
  基础无动作阶段的目标绑定。
- 特别订单：Collect、Donate/投递箱、Fish、Gift、Junimo Kart、Slay。
- preserved `ColoredObject` 的原生基础颜色标签尚未进入背包透明行，颜色标签
  匹配保持 fail-closed。
- 特别订单投递箱必须走原生 `QuestContainerMenu` 插入和确认流程，尚未实现。
- 新 NPC 任务终点尚未进行隔离存档的可见/后台实机校准。

## 权威制品和验证

- 新派生制品：
  `I:\StardewAI-KnowledgeArtifacts\game-1.6.15\derived\game-1.6.15-20260723T093543Z-linux-v20`
- `knowledge-artifacts.lock.json` 已锁定 v20 build manifest。
- 88 个 option，60 个 step compiler，58 个 runtime dispatch，目录 blocker 0。
- 89/89 必需透明字段已连接，字段 blocker 0。
- Core：1105/1105；Backend：67/67；RuntimeTestHarness：0 error。

## 下一步

1. 实现特别订单原生投递箱菜单闭环。
2. 将 Collect/Fish/Gift/Slay 的 context-tag 目标绑定到现有动作候选。
3. 补普通制作、建筑、收获、拾取和战斗目标绑定。
4. 用隔离存档校准 NPC 交付/回报，并记录任务前后状态。
5. 完成全部目标绑定后，再做一次 wiki 与 1.6.15 反编译全量差异审计。

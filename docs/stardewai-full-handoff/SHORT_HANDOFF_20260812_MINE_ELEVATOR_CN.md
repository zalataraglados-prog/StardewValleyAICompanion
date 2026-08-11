# 2026-08-12 矿井电梯短交接

## 已完成

- `mining.use_elevator` 已注册并闭合透明读取、候选、DailyPlan、队列校验、原生运行和输出回执。
- 入口通过地图 `Action=MineElevator` 发现；普通矿井楼层通过 `Buildings/mine` tile index `112` 发现。
- 菜单公开 `0,5..min(lowestLevelReached,120)` 的完整条目、可选状态和稳定身份。
- 执行仅复用 `move_to_tile`、`interact`、`close_menu`，选层只调用原生 `MineElevatorMenu.receiveLeftClick`。
- 选层结果跨 tick 等待原生 `LocationRequest`，不依赖传送后会被清除的 `ridingMineElevator`。
- `mining.reach_depth` 在当前实时端点存在且已解锁检查点推进目标时复用同一链；最终目标通过
  `continuation.target_depth` 保留。没有端点时仍走原 MiningFloorStepPlanner。

## 证据

- 反编译：`MineElevatorMenu.cs`、`MineShaft.checkAction`、矿井入口地图动作及 `LocationRequest.Warped`。
- 聚焦测试：矿井/电梯 42/42。
- 隐藏静默隔离运行：
  `artifacts/runtime-mine-elevator/runtime-mine-elevator-20260812-004601/summary.json`，2/2 通过。
- 正式证据：EVD-246。

## 明确边界

- 只覆盖原版普通矿井；Skull Cavern、采石场金镰刀洞窟、Volcano Dungeon 不得使用此电梯链。
- 该证据不等于任意深度全日长跑、多人所有权、模组电梯或 Product Executor 已完成。
- 后续先重新生成动作对账并跑全套 Core/Backend；通过后提交 main，再从剩余语义目录选择下一项。

# StardewAI 短交接：Full Shipment 原生商店购买路线

## 当前状态

- 分支：`codex/admit-native-shop-purchase-route`。
- 代表目标：`full_shipment:item:380 / (O)380 / sells / shop:Blacksmith`，数量 1，价格上限 150g。
- 继续复用唯一的 `economy.buy_supplies` 和共享执行原语；没有第二套路线规划器、商店编译器或购买执行器。
- 正式训练授权仍为 `false`。本切片只完成代码、离线证明和部分真实路线验证，不能宣称商店购买 strata 已完整通过。

## 本轮实现

- supporting transition 支持路线连接、柜台站位、原生地图交互和可选对话开店，购买仍作为 terminal transition。
- `ActionQueueCompiler` 顺序投影同一计划中已完成移动后的玩家格点，使 `move -> interact` 按计划前态校验；投影地点与 snapshot/目标地点不一致时不生效。
- 商店 supporting queue 只接纳精确 `move -> interact` 和 `move -> interact -> choose_dialogue_response` 形状，乱序、缺失或任意扩展继续失败关闭。
- connector 后新增稳定快照等待与 `stable-replan-snapshot.json`；ShopMenu 可读且非只读时，可证明零游戏分钟关菜单恢复。
- RuntimeTestHarness 只在玩家可移动时执行空闲冻结，避免进入 Blacksmith 的淡入阶段被永久冻结。

## 真实运行证据

- v3：Farm -> BusStop、BusStop -> Town、Town -> Blacksmith 均到达并稳定；Blacksmith 内 `player_can_move_now=true`、`route_timing_ready_now=true`。
- v3 原 stage 4 因 `interact_target_not_adjacent` 拒绝，暴露了同队列移动后位置未投影的问题。修复后对完全相同产物离线重编译通过：`ready_for_supporting_transition_dispatch`、`dispatch_ready=true`。
- v3 回放：`F:\StardewAI-TestLab\artifacts\runtime-full-shipment-shop-purchase-20261012-stage-loop-v3\runtime-full-shipment-shop-purchase-20261012-stage-loop-v3\planning\shop-support-04\support-compilation-replay.json`。
- v4：从 fresh 隔离存档再次通过前两段，随后为暂停而主动中止。所有产物保留，不记为失败，也不记为完整成功。

## 收尾验证

- GoalConditionedBootstrap Release：0 warning / 0 error。
- 聚焦测试：45/45。
- Core game-free：284/284。
- `self-test-acquisition-route-dispatch`：通过。
- `git diff --check`：通过。
- StardewValley、SMAPI、Backend、ProductHost 均已退出；端口 `8765/8767/8768/8798` 无监听。

## 唯一下一步

从 fresh 隔离存档完整重跑 `shop_purchase_sample`，依次验证：

1. 三段跨图路线和 Blacksmith 内稳定站位。
2. 柜台原生交互及必要的 `Shop` 对话响应。
3. ShopMenu fresh snapshot 后重新规划。
4. 精确购买 `(O)380 x1`，并核对 150g、库存、菜单关闭和原生回执。

推荐使用新的 run id/output root，不覆盖 v3/v4。通过后才登记该运行时 strata、续接完整 acquisition proof 和严格生产索引；失败时只从 fresh 失败阶段诊断，不复制执行链。不要提交或删除 `local-data/`。

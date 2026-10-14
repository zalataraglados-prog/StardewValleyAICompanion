# StardewAI 短交接：Full Shipment 原生商店购买路线

## 当时状态（历史快照）

- 分支：`codex/admit-native-shop-purchase-route`。
- 代表目标：`full_shipment:item:380 / (O)380 / sells / shop:Blacksmith`，数量 1，价格上限 150g。
- 继续复用唯一的 `economy.buy_supplies` 和共享执行原语；没有第二套路线规划器、商店编译器或购买执行器。
- 正式训练授权仍为 `false`。本切片已经完成该代表商店购买路线的代码、离线证明和完整原生回放，但单层通过不能替代剩余目标覆盖。

## 本轮实现

- supporting transition 支持路线连接、柜台站位、原生地图交互和可选对话开店，购买仍作为 terminal transition。
- `ActionQueueCompiler` 顺序投影同一计划中已完成移动后的玩家格点，使 `move -> interact` 按计划前态校验；投影地点与 snapshot/目标地点不一致时不生效。
- 商店 supporting queue 只接纳精确 `move -> interact` 和 `move -> interact -> choose_dialogue_response` 形状，乱序、缺失或任意扩展继续失败关闭。
- connector 后新增稳定快照等待与 `stable-replan-snapshot.json`；ShopMenu 可读且非只读时，可证明零游戏分钟关菜单恢复。
- RuntimeTestHarness 只在玩家可移动时执行空闲冻结，避免进入 Blacksmith 的淡入阶段被永久冻结。
- ShopMenu 安全计时器现在编译为有界等待；后续购买必须紧邻同一完整购买身份。旧快照中的商品必须除 `shop_menu_safety_timer_active` 外没有任何阻塞，原生执行时仍重新检查菜单、计时器、商店、库存、价格和容量。

## 真实运行证据

- v3：Farm -> BusStop、BusStop -> Town、Town -> Blacksmith 均到达并稳定；Blacksmith 内 `player_can_move_now=true`、`route_timing_ready_now=true`。
- v3 原 stage 4 因 `interact_target_not_adjacent` 拒绝，暴露了同队列移动后位置未投影的问题。修复后对完全相同产物离线重编译通过：`ready_for_supporting_transition_dispatch`、`dispatch_ready=true`。
- v3 回放：`F:\StardewAI-TestLab\artifacts\runtime-full-shipment-shop-purchase-20261012-stage-loop-v3\runtime-full-shipment-shop-purchase-20261012-stage-loop-v3\planning\shop-support-04\support-compilation-replay.json`。
- v4：从 fresh 隔离存档再次通过前两段，随后为暂停而主动中止。所有产物保留，不记为失败，也不记为完整成功。
- v7：四段 supporting transition 均通过，ShopMenu 已正确打开；终端编译暴露通用活动菜单门误杀有界等待，并且同一安全计时快照不能按普通可购商品验证。该失败产物保留为精确诊断证据。
- v8：fresh 隔离存档完整通过。三段跨图、Blacksmith 内柜台交互、18 tick 安全等待、铁矿购买和关菜单全部形成原生回执；终端三项均为 `applied/verified`。金额 `500g -> 350g`，`(O)380` 从 `0 -> 1`，菜单从 `ShopMenu -> none`。
- v8 证明根：`F:\StardewAI-TestLab\artifacts\runtime-full-shipment-shop-purchase-20261012-stage-loop-v8\runtime-full-shipment-shop-purchase-20261012-stage-loop-v8`。

## 收尾验证

- GoalConditionedBootstrap Release：0 warning / 0 error。
- 商店安全等待聚焦测试：3/3。
- Core game-free：288/288。
- `self-test-acquisition-route-dispatch`：通过。
- `self-test-shop-purchase-navigation-terminal-coverage`：通过。
- `git diff --check`：通过。
- StardewValley、SMAPI、Backend、ProductHost 均已退出；端口 `8765/8767/8768/8798` 无监听。

## 当时后续计划（已作废，仅历史）

本交接中的商店与后续社区中心奖励均已完成并进入严格 production evidence index。当前索引为 `9/26`，七个 high-risk 样本全部通过，共享 shipping/settlement 证明有效，剩余 `17` 个标准分层，正式训练仍未授权。

后续以 `SHORT_HANDOFF_20261013_FULL_SHIPMENT_COMMUNITY_CENTER_REWARD_CN.md` 为准。下一固定任务是标准批次首项 `parsnip_harvest_sample`；不要重复运行已通过的商店或社区中心样本，也不要提交或删除 `local-data/`。

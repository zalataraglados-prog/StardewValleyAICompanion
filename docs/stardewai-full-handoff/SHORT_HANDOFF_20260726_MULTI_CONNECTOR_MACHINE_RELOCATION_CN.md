# StardewAI 短交接：多连接器机器搬迁

日期：2026-07-26

## 本轮完成

- 战略机器搬迁不再限制为单连接器。
- 候选、日计划、动作编译和 Backend 承诺账本都携带完整的强类型路由段。
- 总成本包含源地图接近距离、所有中间地图静态 BFS 距离、连接器成本和目标地图 BFS 距离。
- 每次换图后必须读取新快照并匹配承诺路由的精确后缀；任一段漂移都在输入前失败关闭。
- 玩家住宅地窖被识别为玩家控制的持久地点。
- 连接器站位排除其他连接器、TouchAction 和 warp 格，避免配对入口走错。
- TouchAction 到达后允许下一帧完成原生换图，不再提前判定阻塞。

## 运行时证据

`artifacts/runtime-strategic-multi-connector-machine-relocation-smoke/runtime-strategic-multi-connector-cellar-final-20260726-180008/summary.json`

隔离存档 PASS：

- Keg `(BC)12`：`Farm 56,15 -> Cellar 6,7`
- 路由 1：`Farm 64,14 -> FarmHouse 27,30`，`building_door`
- 路由 2：`FarmHouse 19,35 -> Cellar 3,2`，`touch_action_warp`
- 完整路由成本：`2700` ticks
- 目标 BFS：`7` tiles
- 净收益：`2640` ticks
- 原生拆除、自动拾取、两次原生换图、精确放置和意图完成均通过

## 回归

- Core：`1218/1218`
- Backend：`86/86`
- Solution：0 errors

## 接续边界

普通空闲机器的任意已解析透明连接器链搬迁已经闭环。下一步不要重做这条链，应进入机器特殊分支矩阵：特殊、条件、随机和自定义机器，Workbench 生命周期，以及多人所有权/共享。承诺后的动态拓扑变化继续采用失败关闭和重新规划，不允许沿用陈旧路由。

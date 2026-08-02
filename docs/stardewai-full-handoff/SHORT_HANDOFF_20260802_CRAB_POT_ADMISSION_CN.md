# 螃蟹笼收取训练准入短交接

状态：2026-08-02，EVD-209 切片。

## 已完成

- `fishing.collect_crab_pots` 五门绑定 EVD-209；
- 沿用唯一 `collect_crab_pot -> executor.collect_crab_pot` 链；
- 既有隔离制品验证一次完整原版已就绪螃蟹笼原生收取生命周期；
- 权威对账为 97 registered / 165 semantic / 0 missing / 82 compiler-bound /
  13 five-gate-closed / 12 training-allowlisted / 0 Product Executors；
- 聚焦回归 Core 34/34 通过；本切片未启动游戏。

## 严格边界

候选只接纳当前地图、已就绪、精确原版基础 `CrabPot`，并要求实时产物、站位、背包接收和所有
副作用投影完整。透明桥与编译/运行链保留 Book of Crabbing 的确定性翻倍结果、精确物品单位状态、
Fishing XP、`caughtFish` 统计以及 bait、ready 和 tile-index 复位。原生 `checkForAction` 与刷新后的
前后状态仍是结果权威。

## 仍阻塞

未就绪、背包拒收、投影不完整和自定义子类失败关闭。本目标只负责收取已就绪螃蟹笼，不负责
放置或补饵。正式生产轨迹、跨度观测、manifest、checkpoint、Product Executor 与正式全量训练
均未开始。

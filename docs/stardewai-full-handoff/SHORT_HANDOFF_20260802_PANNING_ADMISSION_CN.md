# 淘盘训练准入短交接

状态：2026-08-02，EVD-208 切片。

## 已完成

- `foraging.pan_ore_spot` 五门绑定 EVD-208；
- 沿用唯一 `pan_ore_spot -> executor.pan_ore_spot` 链；
- 既有隔离制品验证铜盘与钢盘两次原生生命周期；
- 权威对账为 97 registered / 165 semantic / 0 missing / 81 compiler-bound /
  12 five-gate-closed / 11 training-allowlisted / 0 Product Executors；
- 聚焦回归 Core 33/33 通过；本切片未启动游戏。

## 严格边界

候选只在当前地图矿点处于 active/exact、站位可达、Pan 可用、背包可接收且奖励/副作用投影
完整时开放。透明桥以隔离 RNG 调用当前 Pan 的原生奖励解析，必须恢复全局 RNG；候选、编译器和
运行时保留矿点、Pan 槽位/状态、奖励多集、TimesPanned、收货统计、采矿/采集 XP 与矿点后状态。
不得用铜盘/钢盘制品推导固定奖励表；其他升级和附魔仍必须依赖其当前实时精确投影。

## 仍阻塞

缺失/不完整投影、不可达矿点、无 Pan、背包不足和未知模组语义失败关闭。正式生产轨迹、
跨度观测、manifest、checkpoint、Product Executor 与正式全量训练均未开始。

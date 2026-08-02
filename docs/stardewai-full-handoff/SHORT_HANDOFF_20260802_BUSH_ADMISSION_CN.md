# 灌木收获训练准入短交接

状态：2026-08-02，EVD-206 切片。

## 已完成

- `foraging.harvest_bushes` 五门绑定 EVD-120；
- 沿用唯一 `harvest_bush -> executor.harvest_bush` 链，没有第二套执行器；
- `DailyPlanCompiler` 显式登记该 option 的候选种类；
- 权威对账为 97 registered / 165 semantic / 0 missing / 79 compiler-bound /
  10 five-gate-closed / 9 training-allowlisted / 0 Product Executors；
- 聚焦回归 Core 34/34 通过；本切片未启动游戏。

## 严格边界

准入仅覆盖当前已加载地图上的原版 base `Bush`：普通浆果、Botanist 浆果、茶叶、
金核桃，以及已领取金核桃和 shake cooldown 两个上游排除。透明输入、完整 footprint、
站位、输出数量/品质、XP 和核桃 tracker 必须可读。自定义 Bush 子类、town bush 特殊
交互和其他采集族失败关闭。

## 仍阻塞

正式生产轨迹、跨度观测、manifest、checkpoint、Product Executor 与正式全量训练均未开始。
下一项仍须先证明已有唯一链和完整原生运行/输出证据，再进入训练白名单。

# 姜收获训练准入短交接

状态：2026-08-02，`main` 上的 EVD-205 切片。

## 已完成

- `foraging.harvest_ginger` 五门绑定 EVD-119；
- 沿用唯一 `harvest_ginger -> executor.harvest_ginger` 链，没有第二套执行器；
- `DailyPlanCompiler` 显式登记该 option 的候选种类，治理目录不再误报 compiler unbound；
- 权威对账为 97 registered / 165 semantic / 0 missing / 78 compiler-bound /
  9 five-gate-closed / 8 training-allowlisted / 0 Product Executors；
- 聚焦回归 Core 33/33 通过；本切片未启动游戏。

## 严格边界

准入仅覆盖原版、当前已加载地图、精确 ginger terrain feature：干燥普通 Hoe；雨天
Efficient Hoe 且背包满时落为 debris；以及体力不足时上游排除。透明输入、精确站位、
工具状态、天气、体力、库存/debris 输出和 XP 都必须存在。自定义 Hoe/Crop/HoeDirt
失败关闭；不得据此声称所有采集、灌木、作物或任务收集已经准入。

## 仍阻塞

正式生产轨迹、跨度观测、manifest、checkpoint、Product Executor 与正式全量训练均未开始。
下一步继续按权威字典选择已有完整五门证据的高层 option，先核对唯一链，再做有界准入。

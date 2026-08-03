# 原版地面生成物拾取训练准入短交接

状态：2026-08-03，EVD-211 切片。

## 已完成

- `foraging.collect_spawned_objects` 五门绑定 EVD-211；
- 复用唯一 `collect_spawned_object -> executor.collect_spawned_object` 链，没有第二套执行器；
- 训练请求补齐品质、Foraging XP、Farming XP 的端到端运输和执行前漂移校验；
- 隐藏隔离运行矩阵 5/5 通过：普通、Botanist、确定性 Gatherer 双倍、特殊 `724519`、动物屋内部；
- 权威对账为 97 registered / 165 semantic / 0 missing / 84 compiler-bound /
  15 five-gate-closed / 14 training-allowlisted / 0 Product Executors。
- 全量回归通过：Core 1499/1499、Backend 104/104、Release solution 0 errors；

## 严格边界

只准入当前加载地图、精确基础 `StardewValley.Object`、透明投影为 `ready/exact` 的原生拾取。
自定义子类、错误物品、非单栈语义、未激活任务物品和背包拒收均失败关闭。Lewis 地下室
`(O)789` 还会生成 Bat 并改变音画状态，因此在这些副作用透明建模前于读层和运行层排除。
移动 Debris、Bush、姜、树木和障碍物仍由各自独立链负责，不得合并。

## 证据

运行制品：
`artifacts/runtime-spawned-object-smoke/runtime-spawned-object-smoke-20260803-145000/summary.json`。
五类均通过原生 `GameLocation.checkAction` 移除精确对象，并匹配背包数量、品质和两类技能经验。

## 下一步

继续按权威字典依赖顺序选择下一个“已有完整链但尚未五门准入”的模型级候选。正式生产轨迹、
跨存档长期 rollout、manifest/checkpoint、第三年爷爷 21 分闭环和 Product Executor 仍未完成，
不得把本次单项准入表述为可以开始正式全量训练。

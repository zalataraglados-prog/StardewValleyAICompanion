# StardewAI 短交接：Workbench 机器生命周期

日期：2026-07-26

## 本轮完成

- `player.machine_crafting.workbench_crafting_sources` 的真实运行链已验证。
- 请求绑定 Workbench access point、相邻容器节点顺序、逐来源材料消耗计划、配方次数和输出。
- 执行器通过原生 `checkAction` 打开 Workbench，等待 Workbench 与箱子互斥锁，点击原生 CraftingPage 配方和背包。
- 菜单通过 `exitThisMenuNoSound()` 关闭，以执行 Workbench 注册的原生锁释放回调；禁止执行器直接解锁。
- Workbench 制作后复用普通移动、机器放置、装料、自然处理和收取实现。

## 实机证据

`artifacts/runtime-workbench-machine-lifecycle-smoke/runtime-workbench-machine-lifecycle-final-20260726-1905/summary.json`

- 隔离存档、隐藏窗口、静音运行：PASS
- Workbench：`Farm 55,15`
- 相邻箱：`Farm 54,15`
- 箱内配方材料：Copper Ore `20`、Stone `25`
- 制作输出：Furnace `(BC)13`
- 放置：`Farm 60,15`
- 加工投入：Iron Ore `5`、Coal `1`
- 自然处理：`120` 游戏分钟
- 预测/实际输出：Iron Bar `(O)335`
- 收取：`0 -> 1`
- 最终机器：空闲

## 回归

- Core：`1218/1218`
- Backend：`87/87`
- Solution：0 errors

## 接续边界

普通确定性机器的同地图 Workbench 材料来源已闭环，不要重写该执行器。下一步进入特殊、条件、随机和自定义机器分类矩阵；Storage Workbench、远端 Workbench 路由和多人锁竞争仍作为独立切片。

# StardewAI 短交接：职业选择闭环

日期：2026-08-11

## 已完成

- 新增正式语义动作 `skills.choose_profession`，当前动作对账为
  `114 registered / 177 semantic / 113 compiler-bound / 63 catalogued-blocked`。
- 透明桥公开当前两个职业选项的精确 ID、标题、描述，以及玩家持久职业和待处理升级。
- 候选缺字段即关闭；两个职业共享互斥决策键，同一计划不能同时编译。
- DailyPlan 复用 `close_menu`，运行时继续只用唯一 `executor.close_menu`，没有第二套职业执行器。
- 30/30 原版职业矩阵通过；修正 10 级战斗前置 perk 后，战斗分支 6/6 复核通过。
- Fighter ID 24 的最大生命增量为 15，Defender ID 27 为 25。
- five-gate 增加到 40，训练准入增加到 27；Product Executor 仍未集成，不能称为完整陪玩闭环。

## 权威证据

- 反编译：`I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Menus\LevelUpMenu.cs`
- 全职业矩阵：`artifacts/runtime-profession-choice/runtime-profession-choice-20260811-203159/summary.json`
- 战斗前置复核：`artifacts/runtime-profession-choice/runtime-profession-choice-20260811-203610/summary.json`
- 正式证据编号：EVD-244。

## 接续任务

下一主切片是 `mail.process_letter`：先核对原生信件类型、可见内容、附件/配方/任务/旗标副作用和菜单生命周期，
再接透明字段、上游候选、DailyPlan、唯一原生输入执行路径及独立运行证据。

`mining.use_elevator` 不应成为另一套矿井实现。先与普通矿井现有移动、入口交互、楼层转换和新快照链对账；
只有权威扫描证明存在独立未覆盖分支时才新增代码。

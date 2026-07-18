# StardewAI OwlAI 交接任务包

## 角色

OwlAI 是 StardewAI 的专用实现 worker。Codex 仍保留最终审计职责，但 worker 可以在 sandbox 项目副本中独立读写、运行测试、生成 patch 和交接说明。

## 不可越过的边界

- 不读取或输出任何 API key、`.env*`、SSH key、浏览器 cookie、凭据文件。
- 不直接修改真实仓库 `I:\StardewValleyAICompanion`。
- 不推送、不部署、不重置、不清理真实仓库。
- 所有代码产物必须通过 sandbox patch 交回。
- 不能用直接改坐标冒充路径规划；任何移动执行必须经过碰撞/边界校验。

## 当前项目状态

- C# 项目：`I:\StardewValleyAICompanion`
- 隔离运行目录：`E:\StardewValleyAICompanion-runtime`
- 训练数据：`E:\StardewAITraining`
- 当前目标：完全体星露谷 AI 陪玩。

## 已完成能力

- TransparentBridge 能读取运行时透明快照。
- RuntimeTestHarness 有 HTTP executor。
- 机械动作、参数化机械动作、策略动作已分层。
- `strategy.grandpa_progress` 已进入 policy training。
- `strategy_plan` 合同已接入 ActionQueueCompiler 和 TimeBudgetValidator。

## 当前失败点

`debug.visible_walk` 是直接改坐标的 demo，会穿墙。它不能作为路径规划或执行器能力继续保留为正式能力。

## Worker 第一阶段任务：碰撞安全移动执行器

目标：把可见移动 demo 改成碰撞安全的基础移动 executor。

要求：

1. 禁止直接设置任意目标坐标穿墙。
2. 每一步移动前必须校验目标 tile/rectangle 是否可通行。
3. 使用 Stardew/SMAPI 可验证的碰撞 API 或透明 `locations.collision_grid`。
4. 如果路径不可行，返回 blocked，原因明确。
5. 移动失败只进入 executor calibration，不影响 strategy_value。
6. 产出测试或最小验证脚本。

建议切片：

- 新增/修改 RuntimeTestHarness executor option：
  - `debug.visible_walk` 改成安全 tile-step。
  - 或新增 `executor.move_to_tile`，保留 debug 为 deprecated。
- 新增最小 BFS/A* tile path planner：
  - 输入当前位置、目标 tile、当前 location。
  - 输出 tile steps。
  - 无路返回 blocked。
- HTTP 请求字段暂时可复用 `max_crops` 作为步数上限，但更好是在合同里新增移动参数。
- 明确禁止穿墙：不能直接跨越不可通行 tile。

## Worker 第二阶段任务：爷爷 direction 输出层

目标：把 `auto_select_best_direction` 改为真实候选方向。

要求：

- 使用 `GrandpaTrainingSampleAdapter` 候选方向。
- 按透明评分事实选择最优 direction。
- direction 必须写入 `strategy_plan`。
- 时间预算按 direction required_minutes 估算。

必须覆盖 direction：

- `complete_community_center`
- `raise_friendships`
- `complete_full_shipment`
- `raise_skill_levels`
- `marriage_and_house_upgrade`
- `complete_master_angler`
- `complete_museum_collection`
- `obtain_rusty_key`
- `obtain_skull_key`
- `earn_money`
- `earn_pet_love`

说明：Joja 发展是完整游戏路线候选，但反编译的 `Utility.getGrandpaScore()` 没有 Joja 得分项，因此不得作为爷爷 21 分方向。

## Worker 第三阶段任务：演示验收

输出一个 `WORKER_NOTES.md`，包含：

- 改了哪些文件。
- 怎么运行测试。
- 哪些能力仍未完成。
- 是否仍有穿墙风险。
- 如何在可见窗口启动隔离训练并演示。

## 推荐测试命令

```powershell
dotnet test StardewValleyAICompanion.sln
dotnet build tools\StardewAI.RuntimeTestHarness\StardewAI.RuntimeTestHarness.csproj
```

## 验收标准

- 小 AI 能输出策略、参数化机械、机械三类任务。
- 动作编译器能区分 `compiled_action_steps` 与 `strategy_plan`。
- 移动 executor 不穿墙。
- 可见窗口演示中角色移动必须由安全路径步骤驱动。
- worker 产出 patch，不直接改真实仓库。

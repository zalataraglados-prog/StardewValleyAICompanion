# StardewAI 短交接 2026-07-13

## 当前已拉平进度

- 主仓 `I:\StardewValleyAICompanion` 已包含当前有效实现和文档证据；未发现 `C:\Users\18236\deepseek-worker` 近两天有新的待合并产物。
- 机器输入透明链路已从“HTTP 现场探针”改为“SMAPI 主线程缓存”，避免快照请求卡死。
- 新增 `profile=training_machine`，用于后端可 ingest 的轻量机器训练快照，不再依赖过重的 `profile=full`。
- `LiveTrainingLoop` 新增 `--continue-after-blocked-queue-items`，前序队列项 blocked 时可继续尝试后续独立项。
- Runtime harness 在机器 debug setup/load 后刷新透明机器缓存，隐藏后台测试不依赖窗口焦点或自然 tick 时机。

## 最新已验证证据

- `dotnet test StardewValleyAICompanion.sln --no-restore` 通过：
  - Core 216
  - Backend 36
- 机器输入透明/runtime 验证通过：
  - `artifacts\runtime-machine-input-smoke\runtime-machine-input-smoke-20260713-122831\summary.json`
- 机器 daily-plan -> queue -> real executor 验证通过：
  - `artifacts\runtime-machine-daily-plan-smoke\runtime-machine-daily-plan-smoke-20260713-124008\summary.json`
  - 12 个队列项全部尝试，最终到达并验证 `executor.load_machine_input`
  - 验证原因：`machine_input_loaded`, `machine_processing_started_or_output_ready`, `inventory_updated`, `qualified_item_id=(O)262`

## 当前事实边界

- 这不是字段缺失问题：`farm.machines[].loadable_inputs[].predicted_output` 已能进入候选、日计划、action queue 和真实执行器。
- 当前不是完整 replanning：blocked 项只是被记录并继续，尚未智能修复、重排或重新规划。
- 机器缓存仍有有意上限：
  - 最多 64 个机器行
  - 每台机器最多探测 16 个物品槽
  - 随机多输出和 custom output method 仍 blocked，不猜测
- broad multi-machine/day strategy 仍未完成，不能宣称机器系统完全自动优化。

## 下一步接续任务

1. 处理候选排序/过滤，减少明显会 blocked 的 ready-output 机器候选排到机器输入前面。
2. 加队列级 replan/reorder：blocked 后不只是继续，还要根据 after snapshot 更新后续队列或重排。
3. 扩大机器 recipe/runtime smoke 覆盖：Preserves Jar、Keg、Mayonnaise、Cheese、Furnace、Charcoal Kiln 等。
4. 把机器 daily-plan smoke 从 fixture 单点扩展为多机器批处理，但仍保留透明字段和时间预算校验。
5. 回到全项目主线：按同样标准推进下矿、钓鱼、社交等缺口模块，不能让模型训练依赖未透明字段。

## 交接提醒

- 不要回退 EVD-066 安全边界：HTTP 请求线程不得直接跑 live machine probes。
- 不要把 `profile=full` 当训练默认快照；它过重且曾超时。
- 下一位 AI 应优先阅读：
  - `docs\stardewai-full-handoff\evidence.md` 中 EVD-068/EVD-067
  - `docs\stardewai-full-handoff\test-results.txt` 最新 2026-07-13 段
  - `scripts\Invoke-RuntimeMachineDailyPlanSmoke.ps1`
  - `tools\StardewAI.LiveTrainingLoop\Program.cs`

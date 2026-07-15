# StardewAI 短交接 2026-07-14

## 当前结论

项目当前唯一正在处理的主阻塞是钓鱼执行器与 SMAPI 4.5.2 的输入兼容性，
不是透明字段缺失，也不是候选/编译队列没有生成。

2026-07-14 14:28 至约 16:00 为控制端整理窗口：不得新增 DeepSeek/OwlAI
worker 调用。当前 OpenCode 任务先独立收口；之后只审计其补丁，不盲目合并。

后续所有自动测试与游戏 smoke 均保持后台、隐藏、静音。除非用户再次明确要求，
不得打开可见游戏或测试控制台。

## 已验证基线

- Quest 契约测试：81/81 通过。
- Core 全量：399/399 通过。
- Backend 全量：48/48 通过。
- 矿井电梯起始层边界已修正：目标 45 且已解锁 45 时从 45 开始；只解锁
  到 40 时从 40 开始。
- MineShaft 部分透明字段的外层 envelope 已修正：真实非空读值使用 readable
  状态，具体缺口继续记录为嵌套 unavailable 路径；Backend 仍严格拒绝
  `unavailable + non-null value`。
- 上述最终 TRX 位于 `artifacts/test-results/full-20260714/`。

## 两次岩浆鳗鱼运行事实

### 运行 1：首个快照被拒绝

- 目录：`artifacts/runtime-fishing-daily-plan-smoke/runtime-lava-eel-visible-20260714-1348/`
- Backend 返回 422，因为 `mining.tiles`、`mining.objects`、
  `mining.floor_objectives` 外层标为 unavailable 却携带非空真实值。
- 该问题已修复并由新增 Backend 回归测试覆盖。

### 运行 2：抛竿首帧冻结

- 目录：`artifacts/runtime-fishing-daily-plan-smoke/runtime-lava-eel-visible-20260714-1405/`
- 快照上传、候选、daily plan、compiled queue 均成功。
- `executor.move_to_tile` 已 applied/verified，人物到达 `26,13`。
- 抛竿首帧后 SMAPI 每帧抛出：
  `InvalidCastException: ControlledInputState cannot be cast to SInputState`。
- 根因：RuntimeTestHarness 用自定义 `ControlledInputState` 替换 `Game1.input`，
  而 SMAPI `SCore.OnPlayerInstanceUpdating` 强制要求内部 `SInputState`。
- 测试已人工终止；游戏、Backend、LiveTrainingLoop 全部关闭，
  5129/8765/8767 端口已释放。

## 当前修复约束

必须移除自定义 `InputState` 与所有 `Game1.input = ...` 赋值，同时满足：

- 不使用 `Mouse.SetState`、SendInput 或键鼠自动化，不抢玩家物理输入。
- 不直接修改鱼种、RNG、背包、BobberBar 进度或成功状态。
- 不手工调用 `FishingRod.tickUpdate` 或 `BobberBar.update`。
- 保留原生蓄力条、抛竿动画、咬钩、BobberBar 物理和结果记录。
- 反编译确认的兼容入口是 `FishingRod.startCasting()`，以及 BobberBar 的
  `receiveLeftClick`、`leftClickHeld`、`releaseLeftClick` 输入方法。
- 必须加入静态守卫，永久禁止自定义输入替换和 OS 输入注入。

## 仓库与沙箱状态

主仓库包含大量 staged、unstaged、untracked 的历史有效成果。禁止执行
`git reset --hard`、`git clean`、整树覆盖或无审计的 `git add -A`。

有效、已审计并同步到主仓库的沙箱提交：

- `mining-regression-fix-v2`: `1b3dd5f`
- `mining-envelope-runtime-fix`: `9543208`, `0baae7f`, `9be1c79`

当前钓鱼输入修复沙箱：

- `C:\Users\18236\deepseek-worker\handoff\stardewai\sandboxes\fishing-smapi-input-fix\work`
- 基线提交：`cc17c31`
- `ModEntry.cs` 有一份被中断 worker 留下的未提交修改。
- 该修改已做静态审计并被拒绝合并，但必须保留作问题证据：它仍留下
  `ControlledInputState` 类型与三个 `RestoreControlledInputState()` 调用，会导致
  编译失败；它还直接赋值 `isTimingCast`/`castingPower`，绕过自然蓄力动画，且
  没有实现 BobberBar 的 click/hold/release 输入边沿状态机。
- 该草稿未同步到主仓库，不能视为完成成果，也不能被后续整树覆盖意外丢弃。
- 控制端已将原样草稿存档为沙箱提交 `86d1aa1`，提交说明明确标为
  `wip: preserve rejected fishing input draft`；后续只能审计/参考，禁止直接合并。

## 下一步 TODO

1. 等当前 OpenCode 任务结束；在约 16:00 前不新增 worker 调用。
2. 分别审计 OpenCode 产物与 `fishing-smapi-input-fix` 未提交 diff，选择语义正确
   的单一实现，不叠加两套输入状态机。
3. 先运行源码守卫与 focused fishing executor 测试，要求无
   `ControlledInputState`、`Game1.input =`、OS 输入、直接结果修改。
4. 后台隐藏运行 RuntimeTestHarness build、Backend 全量、Core 全量。
5. 后台隐藏/静音运行普通钓鱼 smoke；确认正常蓄力、抛竿、咬钩、小游戏、收杆。
6. 后台隐藏/静音运行 MineShaft 100 岩浆鳗鱼 smoke；要求 `(O)162`、
   `bobber_bar_observed`、训练输入输出完整落盘。
7. 每次 runtime 失败必须立即停止相关进程并限制日志增长，不允许再次逐帧刷错
   数分钟。
8. 全部通过后再更新 `evidence.md`、`transparency-coverage.md` 和测试记录，
   然后返回完全体陪玩主线。

## 明确未完成

- 岩浆鳗鱼真实 runtime 尚未通过。
- 钓鱼执行器当前不能标记为 runtime-ready。
- 正式训练不能依赖当前钓鱼动作样本。
- 不能宣称执行器已达到完整人类行为覆盖或项目已完全透明。

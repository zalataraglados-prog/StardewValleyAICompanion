# StardewAI 短交接：Junimo Kart EVD-239

## 已完成

- 锁定 1.6.15 反编译链已确认：Saloon `Arcade_Minecart`、骷髅钥匙门槛、`MinecartGame/Endless`、
  `MineCart` mode 2、Endless 死亡时 `submitHighScore()`、`onJKScoreAchieved` 与 `JKScoreObjective`。
- 透明桥新增 `current_location.arcade_action_tiles`。真实 E 盘隔离 full 快照已验证该字段
  `available`、带原生地图来源；schema validation 为 required 102、blocking 0。
- `JKScoreObjective` 已接入 `quest.advance` 候选；上游排除无钥匙、菜单占用、入口缺失和无站位。
- DailyPlan 固定复用 `move_to_tile -> interact -> choose_dialogue_response`，最后调用唯一新增原语
  `executor.play_junimo_kart`。
- 运行时只发送原生跳跃输入并观察原生分数与任务回调；禁止直接调用分数提交/死亡、写分数、轨道、碰撞或目标计数。
- 任务目录为 `24 bound / 0 blocked / 3 observation-only / 1 native-unreachable`。
- KnowledgeCompiler `585/585`、blocking `0`；动作分母 `320/428/150` 不变，语义动作 174 并重新冻结。
- Release 回归：Core `1622/1622`，Backend `121/121`。

## 仍未完成

- 尚未取得 Junimo Kart Endless 50,000 分的真实运行验收证据。
- `30,190/50,000` 历史运行加载了未声明的 `JunimoTestClient`，已拒绝作为证据和控制器基线。
- smoke 已改成每次运行独立的两模组 `SMAPI_MODS_PATH` 白名单。首个干净矩阵是
  `artifacts/runtime-junimo-kart/runtime-junimo-kart-20260811-002951/`，峰值 `10,940/50,000`，仍为 blocked。
- `Bubble`、现存 `FallingBoulder` 和 spawner 下一颗落石已进入唯一控制器的只读预测；入口、模式选择、原生输入、
  死亡重试和失败任务进度回写已经贯通。剩余主缺口是高速段精确落点与按主题可重复校准。
- `executor.play_junimo_kart` 当前只有声明、编译和 Harness 实现，不得登记 runtime/output evidence。
- `quest.advance` 是 `Declared / StepCompilerDeclared / RegisteredOnly`，仍不在训练 allowlist。

## 唯一下一步

以 `10,940` 干净两模组运行作为当前可比较检查点，补高速段连续轨迹与精确落点控制。每个切片都在 E 盘精确
`QiChallenge3` 夹具中后台运行，使用同一白名单并比较峰值、主题、死亡位置、垂直速度和附近动态实体；回退时不把
试验代码留成第二套控制器。最终必须由自然死亡路径提交至少 50,000 分，并取得同一目标 fresh after-state。只有
这些条件全部成立，才登记五门证据并重新评估训练准入。

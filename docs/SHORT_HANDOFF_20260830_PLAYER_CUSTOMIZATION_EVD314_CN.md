# StardewAI 短交接：玩家外观定制 EVD-314

## 当前状态

`player.customize -> customize_player -> executor.customize_player` 已完整闭合。该动作严格为 `PlayerCommandOnly`，不进入默认候选、自主日计划或策略训练；唯一保留的上游信息是玩家明确给出的模式、目标、原因和确认，所有机械字段在执行前从当前透明快照重新绑定。

当前对账：`208 registered / 220 semantic / 207 compiler-bound / 135 harness dispatch / 131 five-gate / 54 training allowlist / 12 catalogued blocked / 0 Product Executor`。full snapshot 为 `161/144/17/0`，原生冻结分母为 `322 surfaces / 448 branches / 150 map tokens`。

## 已完成能力

- 巫师神龛：共享路线到 `WizardHouseBasement`，原生 `WizardShrine` 对话、500g、`CharacterCustomization(Source.Wizard)` 全部可编辑字段和 OK 回执。域为姓名、最爱之物、性别、皮肤 `0..23`、实时发型 ID、饰品 `-1..29`、眼睛/头发六个 HSV `0..100`。
- 沙漠改造：目标地图必须是替换位置 `DesertFestival`。透明层覆盖当前造型师、节日日、每日标志、装备回收空位、完整 `Data/MakeoverOutfits` 过滤和原生日存档 RNG 投影；执行层只走 `DesertMakeover` TouchAction、原生可跳过事件和完成回调。
- 安全边界：生产路径不直接写金钱、外观、装备、每日标志，不直接调用 `ReceiveMakeOver`。染色归 `tailoring.dye_item`；新建角色归 onboarding；基础版未发现可达 Dresser 构造调用点。

## 验证

- 隐藏静音运行矩阵 `4/4`：`artifacts/runtime-player-customization/runtime-player-customization-20260830-215722/summary.json`。
- KnowledgeCompiler `585/585`，blocking 0。
- Core `2123/2123`；Backend `155/155`；Release `0 warnings / 0 errors`。

## 下一步

`minigame.play_junimo_kart` 按用户既定要求继续后置。下一实际纵向切片是 `processing.crack_geode`：先锁定 1.6.15 原生入口、服务/库存/费用门控、完整 geode 输出与 RNG/回执语义，再按同一套“透明投影 -> 上游候选 -> DailyPlan -> fresh 编译 -> 类型化运行 -> 原生输入 -> 结果回执 -> 隐藏静音验证”流程闭合，禁止另建第二套移动、库存或奖励系统。

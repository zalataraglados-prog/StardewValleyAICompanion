# 执行器单路径审计（2026-08-01）

## 审计目的

本审计根据本地 Codex 会话、Git 历史、EVD 账本与当前源码核对“同一能力是否被实现两次”。
判断必须区分三件事：

1. 一个语义 option 是否被重复注册、重复编译或重复 dispatch；
2. 不同领域是否有意使用不同编排，例如普通矿井与火山；
3. 不同领域是否又复制了本应共享的移动、输入、工具或菜单机械状态机。

普通矿井与火山战斗是明确的领域分离，不是重复系统。它们可以保留不同目标选择、地形、安全窗、
掉落追踪和终止条件，但仍必须使用统一输入仲裁及可复用的底层机械原语。

## 已确认事实

- 97 个已注册 option ID 唯一；165 个冻结语义动作没有未登记缺口。
- 编译器孤儿 ID 为 0，Harness/Product runtime 孤儿 ID 为 0。
- `RuntimeTestHarness` 的 66 个受支持 option 各自只有一个 `ModEntry` dispatch 分支；新增测试锁定该约束。
- `social.talk_npc` 与 `social.gift_npc` 共同复用 `SocialCandidateBuilder`、日计划、
  `executor.social_interact` 与同一原生 Harness 实现，没有第二套社交执行器。
- `recovery.stabilize_day` 只有一个候选入口、一个日计划入口和一个 action-queue 编译入口；跨图部分
  复用 `executor.traverse_connector`，终端部分复用 `executor.sleep`。
- `inventory.transfer_item` 复用唯一 `executor.transfer_material`，不存在第二套箱子运行时。
- Git 提交 `69238f3` 已将普通矿井主动请求与反应式遭遇统一到同一 `ActiveCombatMonster`；
  `d72d578` 已引入统一移动租约、原生工具生命周期和路径推进底座。

## 尚未统一的机械底座

当前没有发现同一 option 的竞争 dispatch，但发现共享底座采用不完整：

- 27 个 Harness 文件仍直接调用 `StartMoving` / `MovePlayerForTick`；共享路径推进目前由底座、睡眠、
  火山路线和火山清障 4 个文件采用 `TryAdvanceExecutorPath`。这些通常不是两个 option 实现，但重复了路径游标、卡住计数和
  动态阻塞处理，容易再次产生步态、转向和掉键差异。
- `NativeToolActionLifecycle` 当前只覆盖普通农场工具、通用清障和火山障碍；石头、资源块及部分
  专用工具仍有领域内生命周期。领域终态可以不同，按下/释放/动画等待不应复制。
- 结果 DTO 构造在多个 Handler 中重复。这不是游戏行为分叉，但会造成 verifier 字段遗漏风险，
  应在不影响实机证据的纵向切片中收敛到公共 result builder。
- 根 `ModEntry` 仍持有大量旧 `active*` 状态。现行规则是禁止新增根状态，并在触及某领域时迁入
  对应 Handler；不能用一次整仓重写破坏已有运行证据。

## 发现的原生输入旁路

以下是比代码重复更高风险的旧实现类型，必须逐项替换后才能扩大 Product Executor 声明：

- 睡觉旧链直接调用床 `performTouchAction`、`Sleep_Yes` 和菜单清空。本轮已修正：到床边复用
  `TryAdvanceExecutorPath`，床格触发由正常移动产生，确认使用真实 `Y` 键按下/释放，并禁止直接菜单修改。
- 普通矿井竖井和离矿确认仍直接调用 `answerDialogueAction`；领域仍与火山分开，但问答应改成原生输入。
- 钓鱼仍有 `FishingRod.beginUsing`、`DoFunction` 和异常清理直接关菜单的旧路径，需要与现有按键控制统一。
- 通用关菜单、材料转移、博物馆、任务投递和部分出货清理仍使用 `Game1.exitActiveMenu`；应按具体菜单
  使用原生键鼠输入，并保留菜单就绪和终态验证。
- Fixture 中的定位、时间推进和场景构造可以直接改测试状态，但必须保持 `debug.*` 隔离，不能被生产 option 调用。

## 防回退和迁移顺序

1. 每个 Harness option 保持唯一 dispatch；任何新 option 必须先登记唯一主引擎。
2. 当前恢复切片先完成原生睡觉修正，再跑“屋外 -> 透明连接器 -> 回家 -> 床 -> 新日”的真实闭环。
3. 然后按矿井问答、菜单关闭、钓鱼、其余路径游标的顺序迁移；每次只改一个已有 EVD 边界并复跑该领域实机证据。
4. 普通矿井与火山保持领域编排分离；仅抽取共享输入、移动、工具和结果底座。
5. Product Executor 在正式长期入口接入前仍为 0，不能以 Harness 成功替代产品完成声明。

## 本轮完成标志

- `ExecutorSinglePathGovernanceTests` 证明所有受支持 Harness option 各有且仅有一个 dispatch。
- `executor.sleep` 源码不再包含直接 `performTouchAction("Sleep")`、
  `answerDialogueAction("Sleep_Yes")`、`activeClickableMenu = null` 或 `dialogueUp = false`。
- 睡觉使用共享路径推进和 `SButton.Y` 原生按下/释放。
- 跨图恢复只有取得新鲜 before/after snapshot、逐连接器结果、原生睡觉结果和新日输出后才可关闭运行门。

以上标志已由 EVD-195 完成。隐藏、静音的 E 盘隔离运行从 `Farm@2200` 开始，第一轮高层恢复只编译
`executor.traverse_connector` 并到达 `FarmHouse`，第二轮在新鲜快照上只编译 `executor.sleep`，日期从
16 日推进到 17 日。普通矿井与火山继续保持领域编排分离；后续迁移不得把二者合并为同一规划器。

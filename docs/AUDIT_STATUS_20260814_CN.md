# StardewAI 审计状态补记（2026-08-14）

> 本文是一次只读复核后的维护性补记，不替代生成式 dashboard、capability/evidence index 或后续更新的 `docs/CURRENT_WORK_CN.md`。如果本文与更新后的机器生成事实冲突，应以后者为准。

## 复核范围

本次仅复核当前可见 `main`、最近提交、开放 Issue 与现有权威工作文档。没有启动游戏、SMAPI、构建、测试或运行新的 runtime smoke，因此本文不新增 E2/E3/E4 证据，只整理当前仓库已经存在的证据边界。

## 当前可确认检查点

截至当前可见 `main` 最新提交 `a56dd83a7a13252f4a072a6f0c743571f3d4080c`（2026-08-12，`Correct building skin verification baseline`）：

```text
122 registered
180 semantic
121 compiler-bound
49 five-gate
32 training allowlist
0 Product Executor

320 native surfaces
428 native branches
150 map tokens
blocking = 0

full snapshot required = 112
full snapshot blocking = 0
KnowledgeCompiler = 585/585, blocking 0
Core = 1663/1663
Backend = 121/121
```

这些数字只表示当前仓库记录的工程/证据状态，不应被解释为“122 个动作已经产品完成”或“32 个训练项已经具备长期策略质量”。尤其 `0 Product Executor` 仍是明确边界。

## 最近已确认的有界进展

### animals.purchase / EVD-247

近期提交把动物购买从终端交易扩展为 fresh-snapshot rolling chain，覆盖 Marnie 服务入口、原生分页、目标住房选择和最终原生购买回执。代表运行验证了精确动物类型、owner/home/name/occupancy 与 money delta。

仍不应外推到：完整动物产业、持续喂养、迁移、动物门、出售、多人 ownership、modded 动物/建筑、Product Executor 或长期策略质量。

### buildings.construct / EVD-248

基础 Robin 建造已形成严格目的绑定的代表性原生闭环：建筑类型、目标地点和建设理由必须明确，上游检查条件、资源、落点和预留冲突，最终复用唯一原生 CarpenterMenu 建造链。

当前证据明确不覆盖 Wizard、升级、迁移、拆除、换皮、长期布局策略或 Product Executor。

### buildings.change_skin / EVD-249

代表性 Pet Bowl 换皮路径已经完成透明读取、候选、DailyPlan、共享动作队列、原生 Robin/CarpenterMenu 执行和严格回执。

这不能替代 `buildings.paint`。当前工作文档仍把 `buildings.paint` 作为下一独立切片，要求复用已有建筑服务和菜单链，仅新增颜色参数、意图约束和 BuildingPaintMenu 回执。

### quest.advance 相关子链

近期已有普通 Daily Quest 接受、Special Order 接受、Quest Reward 领取、CraftingQuest、HaveBuildingQuest 等多个子链推进。目录绑定达到阶段性零 blocking 是重要进展，但不等于所有 objective terminal semantics 已闭合。

DropBox、NPC delivery、Ship pending settlement、Fresh/after-acceptance provenance、失败/Lost and Found、未知 callback 和各 objective 的独立 E3 仍应分别对账。

### skills.choose_profession / EVD-244

职业选择已成为显式模型决策，覆盖原版 30 个 profession ID 的隔离矩阵，并补充战斗前置与即时 perk 的修正验证。

这只支持 `skills.choose_profession` 的有界结论，不覆盖 respec、mastery、Perfection、Golden Walnut、Qi、长期 requirement graph、Product Executor 或 E4。

## Issue 维护原则

本轮没有关闭大型 Wave Issue。原因是这些 Issue 本身定义的是完整生命周期或跨域能力，而最近运行证据通常只闭合其中一个严格范围。

维护时继续遵循：

```text
registered != implemented
compiler-bound != executable
Harness/runtime smoke != Product Executor
single E3 slice != whole-domain completion
five-gate != E4
training allowlist != policy quality
```

对子项已有明确证据时，优先在对应 Issue 留下有界进度评论；只有 Issue 的完成定义全部满足且证据新鲜时才考虑关闭。

## 当前审计关注点

1. 保持 capability/evidence index 与生成式 dashboard 为主要进度来源，不让手写总数重新成为事实源。
2. 对每个新闭环继续检查 evidence freshness、exact scope、runtime mode、fixture、before/after 和 known limitations。
3. 继续防止 Harness 证据被升级描述为 Product Executor。
4. 对训练准入保持克制：五门通过只说明指定范围存在证据，不说明长期 reward、泛化、多人或产品稳定。
5. 对旧工作文档中的历史数字和“下一步”按时间解释；最新权威检查点优先。
6. 当新的 runtime/commit 改变上述数字或边界时，应更新或废弃本文，而不是维持旧基线。

## 本轮 Issue 更新

本次仅做审慎的进度补记，没有修改 Issue 完成定义：

- #2：补充 2026-08-14 总审计检查点与证据边界；
- #14：登记 `purchase + naming` 子链进展，保持动物产业总 Issue open；
- #16：登记基础建造和代表性换皮进展，保持建筑生命周期总 Issue open；
- #20：登记近期任务子链扩展，保持普通任务/特别订单总 Issue open；
- #22：登记职业选择闭环，保持长期进度总 Issue open。

本文刻意避免使用“整体完成”“已经完全可训练”“产品就绪”等超出当前证据范围的表述。

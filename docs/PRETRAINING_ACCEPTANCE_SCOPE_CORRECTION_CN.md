# 训练前验收层级纠偏与计划补充条款（R2）

<!-- acceptance-scope-20261103-r2; authored_at=2026-11-03T16:30:45+08:00; baseline=0b23350d60a67993172db4a5d099b01fd9b54693; owner-authorized; docs-only -->

**修订时间：2026-11-03T16:30:45+08:00（Asia/Kuala_Lumpur）。核查基线：`main@0b23350d60a67993172db4a5d099b01fd9b54693`。**

**依据：** [#224 criterion-local evidence / WIP=1](https://github.com/zalataraglados-prog/StardewValleyAICompanion/issues/224)、[#390 总验收证据误嵌入 Master Angler 局部门](https://github.com/zalataraglados-prog/StardewValleyAICompanion/issues/390)。[PR #389](https://github.com/zalataraglados-prog/StardewValleyAICompanion/pull/389) 只提供 v111 运行边界与局部测试编排事实，不能替代上述架构依据。

## 1. 生效范围与覆盖关系

本补充条款按用户本次授权，纠正 `pretraining-order-20261103-r1`（提交 `0b23350d`）遗漏 #390 的错误。它与 [Teacher / Student 技术合同](TEACHER_STUDENT_CONVERGENCE_CONTRACT_CN.md) 的同日修订一起，**覆盖上一版关于验收归属、Master Angler 强制 Gourmand/source-schedule 门，以及由这些错误门推出的固定第 10 顺位**。不删除历史记录，不把文档纠正写成代码修复或新原生证据。

具体对应位置如下；旧文保留只作追溯，在发生冲突时执行本条款而不是旧指令：

| 对应文档与位置 | 上一版问题 | 本次生效修正 |
| --- | --- | --- |
| `CURRENT_STATUS_CN.md` 顶部“本轮有效计划”、第 4 节及第 6 节 R1 表 | 用包含全局证明的成本固定 Master Angler 第 10；把“Rusty Key 当前 primary”解释为可以跳过层级纠偏 | 先处理 #390 验收责任解耦，再恢复单项施工；Rusty Key 可保留为下一覆盖工作的优先候选，但不是未经重估的整表已获确认 |
| 同文件第 6.1 节 D6、第 6.2 节 Master Angler 行、第 6.4 节第 5 点 | 把 source schedule、Gourmand 与 `72/14/3` 继续当作该根的完整退出门 | 局部门只验本 criterion；全局 source schedule 上移；Gourmand 为可选来源，不能统一附加为硬条件 |
| `CURRENT_WORK_CN.md` 顶部、接棒任务及 Master Angler 保留点 | 只改排期，没有安排错误验收耦合的纠正 | 立即执行本文件第 5 节的单项纠偏任务；保留原生修复与已解锁边界档，停止把重复 Day-0 长跑当局部排障入口 |
| `FULL_SYSTEM_COMPLETION_PLAN_CN.md` 2026-11-03 顺序修订及当前退出要求 | 将错误全局门后移，未移到正确层级 | 采用局部能力／共享证书／全局聚合／终局验证四层合同；完整新档证明不作为单根关闭的反向前置 |
| `FORMAL_FULL_TRAINING_READINESS_CN.md` 同日施工顺序及 Master Angler 准入说明 | 保留“既定门都不改”的笼统措辞 | 保留真实合法性、证据身份和正式训练门，撤销误加在局部根上的全局证明依赖；重新生成机器对账前不晋升 |
| `goal-conditioned-no-human-teacher-plan.md` 同日顺序、姜岛 prerequisite 与 runtime evidence 解释 | 将预选核桃方案的完整证明等同为钓鱼局部门 | 区分路线选择与必要条件，局部消费经验证共享能力证书；全局组合在其所属层执行 |
| `GOLDEN_WALNUT_BOOTSTRAP_CORRECTION.md` Runtime status | 将 boundary smoke 之后仍强制接完整 Day-0/Gourmand 来关闭钓鱼根 | 该处已原位替换；局部验收与全局 fresh-save 验收分别调度 |
| `GOLDEN_WALNUT_SOURCE_SCHEDULE_VERIFICATION.md` Scheduling amendment | 把完整 source-schedule 验证当 Master Angler 的固定恢复门 | 该处已原位替换；验证器保留，调用归属改为 Teacher 全局集成或明确请求的全链测试 |

`CURRENT_STATUS_CN.md` 的机器指标与历史运行事实仍保留权威；本条款只修正上述计划与证据责任，不用旧 Issue 的历史 Money primary 覆盖最新事实。#224 中长期有效的是共享架构、criterion-local 与 WIP=1，不是其 10 月历史状态数字。

## 2. 事故本质：不是单纯“把难项往后排”

#390 指出的错误依赖为：

`全局 fresh-save source schedule + 强制 Gourmand 3/3 → Master Angler 单根关闭 → 19/19 全局覆盖`。

单根因此提前承担多根组成的长期游戏进度证明。仅把它由第 5 移到第 10，不会消除反向依赖；以这份被放大的成本作为新排序依据也不成立。

在基线源码中，`MasterAnglerFreshSavePrerequisiteCompletionProofBuilder` 把缺少 Gourmand 回执列为 blocker，`MasterAnglerDependencyExpansionCompletionBuilder` 要求 `VerifiedGourmandTransitionCount == 3`，前置审计器也消费完整 source schedule 和连续 Gourmand 回执。这里记录的是待解耦调用关系，不宣称本次已经修改这些实现。

## 3. 修正后的四层验收合同

| 层级 | 证明对象 | 不能混入的要求 |
| --- | --- | --- |
| L1：criterion-local | 本 criterion 的权威分母、类型化合法前置、实际动作/终态、独立 Teacher comparison 与 native outcome，并按 train/validation/test 形成合法 join | 不要求先跑完整 Day-0 多根日程或取得其他 criterion 的最终成就 |
| L2：共享能力证书 | 被实际选择的路线需要哪些地点、技能、资源、授权、状态转移；检查证书的范围、版本、身份、前置与结果，消费者 fresh 重绑定 | 不把“上游已有函数/图节点”当证书，也不要求每个消费者重新完成全部上游游戏进度 |
| L3：Teacher 全局集成 | 将已闭合局部视图聚合为 `CoverageGateReady = Method executable ∩ Teacher comparison ∩ native outcome`；核验跨根资源、日历、长期连续性及所属的 source schedule/独立重放 | 不把本层的完整集成证明再反向作为任何 L1 根的先决门 |
| L4：完整新档与产品评测 | 在局部聚合准备完成后执行完整 Day-0/长期目标验证；正式训练、Provider/模型实验、manifest 和 checkpoint 按各自准入合同推进，Student 的新档 21/21 仍是训练后的验收目标 | 不把局部边界档、无错误日志或静态修复说成完整新档成功或正式开训 |

L1/L2 原生 smoke 与 L3/L4 fresh-save 证明可以复用经过校验的能力和工件，但不能混淆起始条件与证据归属。已解锁姜岛副本可以证明“在这些明确前置条件下捕鱼/交互正确”，不能证明“从零完成了社区中心和修船”。完整新档证明仍须记录真实取得前置的过程，不能拼接不同运行或未保存的内存状态。

### Master Angler 的局部范围

- 保持锁定版本的鱼类分母及原生规则，不因 Jelly 随机未命中而删除鱼种；在基线中对应 72 个鱼种及其类型化鱼区前置。
- 验证真实合法的捕获/蟹笼链、采集终态、窗口、随机重试合同，以及该 criterion 的 comparison/native-outcome 三分区。边界短测不是自动获得全部覆盖资格。
- 姜岛、锈钥匙、技能和资源等按实际选中路线消费 L2 证书；缺失证书仍是明确 blocker，但不把整个共享根的全球游戏进度重复压到捕鱼局部门。
- **Gourmand 是核桃来源 OR 方案，不是 Master Angler 无条件 AND 前置。** 选择 Gourmand 时必须证明该路线承诺的具体请求、作物和原生回执；若选择其他合法且充分的核桃来源，不能因为没有 Gourmand 3/3 拒绝捕鱼局部门。
- `72/14/3` 不再作为统一的局部完成口令：鱼类分母与鱼区前置保留，末尾的 `3` 只能描述所选 Gourmand 分支，不得成为所有路线共享的硬条件。

### 全局证明的保留与迁移

Golden Walnut source-schedule 构建器、独立重放器、真实原生回执及其精确 run/save/player/SHA、数量、时间和状态校验均保留。需要改变的是调用方和准入关系，不是删除证明或把返回值硬改成成功。完整 source schedule 属于 Teacher 全局集成中的相关岛屿取得子链，不等价于所有 19 项已经通过；Gourmand 也不能从局部硬条件简单迁成全局无条件硬条件。

## 4. 开发顺序：先修合同，再按有效局部成本排队

1. **立即优先项：既有 #390 的验收层级解耦。** 这是 WIP=1 下的单一 correctness 工作项，不新开 Issue，不并行新增根方法，不重跑昂贵 Day-0 来证明文档改对了。本次仅完成文档规范，不声称已执行代码整改。
2. 保留已冻结的 Community Center、Pet Love、Skull Key、Full Shipment，保留全部有效运行/修复证据；#381 owner PASS 不重复触发。
3. 解耦实现及局部正反回归通过后，重建每个根的 reconciliation、真实 blocker 和证据复用报告。此时再比较**局部范围内**的剩余工作，而不是旧的整场游戏长跑成本。
4. Rusty Key 的 60 件阈值/奖励与下水道证书，仍是有明确复用价值的优先候选；Skills 的必要 XP 子链可被消费者复用，不要求全部五技能评分先完成。Friendship→伴侣子链、Rusty Key→全博物馆属于复用关系，不能偷换成“全部上游 criterion 都要先通关”。
5. **撤回 Master Angler 固定第 10、以及整份 R1 表已是最终最优顺序的声明。** 在有效局部成本尚未重新测定前，不伪造新的精确全序；Master Angler 一旦必要共享证书齐备且局部缺口较小，应进入可选队列，不必排在无关的完整婚姻/博物馆之后。选择结果必须在 canonical 队列追加依据和时间戳，保持一次一个 primary。
6. Money 完整六阈值×三分区门保持独立；其他消费者只绑定其真实现金/材料缺口。不得为了避免倒置又把完整 Money 18/18 变成所有根的共同前置。

这是撤销失实全序并给出明确下一步，不是放弃计划或授权开发对话自由换项。两个平级常驻对话及其子代理共同服务同一工作项；不改变模型/算法实验归属，也不增设第三个主目标。

## 5. 实现整改的完成条件（尚待开发验证）

- 对照 #224/#390 检查 `MasterAnglerFreshSavePrerequisiteAuditBuilder`、`MasterAnglerFreshSavePrerequisiteCompletionProofBuilder`、`MasterAnglerDependencyExpansionCompletionBuilder` 与 `Invoke-ProductBootstrapCalibration.ps1` 等调用方；分清局部视图、共享证书和全局证明的消费者。
- 正向：有效局部鱼类证据与必要共享证书成立、但没有全局 source schedule/没有选择 Gourmand 时，局部门不应仅因此失败。
- 反向：前置证书缺失、非法捕获、身份/分区漂移、未满足原生终态仍必须拒绝；全局集成入口缺少其必需的连续新档证明也必须拒绝。
- 分支：选择 Gourmand 就检验其实际承诺；合法替代核桃方案不能被强制三次 Gourmand 排除。不改变原版区域解锁规则。
- 先在 PR #389 留存的隔离已解锁边界档上验证相应局部链；具体能力通过后才进行其所属层的短链组合和全局新档验证。静态、fixture、原生与完整新档证据分栏。
- 当前机器报告尚未重建，本次不更新任何覆盖分子，不关闭 #390，不修改程序或运行游戏。原 `5/19`、`4/11`、`0/18` 只作为基线状态，正式训练继续为 false。

## 6. 对上一轮指令解读偏差的记录

用户已提示汇报 Issue 可供参考，但上一版改序实际主要消费旧 canonical 队列、代码复用关系和 PR #389；没有在动笔前把 #390 与 #224 的 criterion-local 原则逐项转成约束。结果把“依赖倒置”只理解为施工先后，把错误的 source-schedule/Gourmand 局部门当成必须保留的质量要求，并据此高估 Master Angler 的剩余局部成本。

缺失的最后一道核对是“新指令 → 原始依据 → 修订条款 → 是否仍冲突”的映射。引用 #224 的 WIP=1，不等于已经落实其 criterion-local 架构；保护验证严格性，不等于保护误置的门。此次 R2 补充条款记录这项纠正，不归咎于用户表述，也不以工具问题解释理解错误。

**完成边界：文档规范修订与提交，不是 #390 代码修复，不是原生验收通过，不是模型训练授权。**

# StardewAI 当前状态与施工顺序（Canonical）

> 本文件是**当前人类可读状态入口**。机器可读 report / self-test / evidence index 仍是事实源；本文件负责把不同分母分开显示并规定开发顺序。
>
> 下方其他带日期的 handoff、readiness、plan 章节均保留作历史证据。除非其数字在本文件中再次出现，否则**不得把历史数字当作当前状态**。

## 1. 当前状态面板

基于当前主线状态（创建本次清理时为 `main@d87836d5`）：

| 指标 | 当前值 | 含义 |
| --- | ---: | --- |
| Goal Method executable criteria | **4/19** | root method 已达到 `executable_frontier` 的 criterion |
| Split-complete Teacher/native evidence | **4/19** | train/validation/test 两条证据通道完整的 criterion |
| Promotion-grade / CoverageGateReady intersection | **2/19** | 同时满足 executable + 正式 Teacher/native 三分区证据 |
| Coverage-ready root methods | **1/11** | 11 个战略 root method 中正式 coverage-ready 的数量 |
| Full Shipment runtime strata | **26/26** | Full Shipment 内部运行时执行签名证据，已闭合；**不是全局 /19** |
| Money production evidence cells | **0/18** | 6 个 Money criterion × 3 partitions；hermetic fixture 不计 production |
| Formal product training | **false** | 未通过完整正式训练准入 |

当前两个 `CoverageGateReady` criterion 固定为：

- `community_center_access_or_completion`
- `community_center_accessible_bonus`

### 不允许再使用的模糊短写

禁止单独写：

- “当前 coverage 是 4/19”
- “当前进度是 2/19”
- “Full Shipment 26/26，所以全局完成”
- “Money 已完成”

必须写完整指标名，例如：

```text
Executable criteria:       4/19
Split-complete evidence:   4/19
CoverageGateReady:         2/19
Root methods ready:        1/11
Full Shipment runtime:    26/26
Money production cells:    0/18
Formal training:           false
```

## 2. 已完成并冻结的施工单位

### Full Shipment runtime evidence

`26/26` runtime strata 已闭合。除非出现以下情况，否则**不得继续扩张分母或主动重跑全部 26 层**：

1. 权威 route/source contract 真实变化；
2. game/mod profile 或版本变化；
3. 共享 correctness 修复明确影响某个既有 stratum；
4. 正式 promotion review 要求 force-full revalidation。

正常共享代码修复只重验**受影响的代表 strata**，再由 production evidence index 重建确认。

## 3. 当前唯一 Coverage 主施工单位：Money / Revenue

当前 coverage 主线固定为：

```text
earn_money
  -> Revenue authoritative denominator
  -> terminal/rebound identity
  -> native execution proof
  -> reservation/ledger continuity
  -> exact Teacher comparison admission
  -> criterion-local native outcome join
  -> 6 criteria × 3 partitions production evidence
```

Money 的基础设施、hermetic fixtures、comparison/native channel separation 已建立，但 production 18 格尚未闭合，因此**不得提前把 Money 六项计入 CoverageGateReady**。

## 4. 单项闭环施工原则（WIP = 1）

从本文件生效后，Goal Method / formal-coverage 主线实行：

```text
同一时间只允许 1 个 primary root method / progression family 作为 coverage 施工单位。
当前：Money / Revenue
```

在当前项达到 Exit Gate 前，不启动 Friendship、Skill、Museum/Rusty、Marriage、Master Angler 等下一项的新的 coverage expansion。

### 允许的中断

仅以下情况可以临时中断当前项：

- P0 / P1 跨域 correctness 缺陷；
- 当前项被一个共享基础设施硬 blocker 阻塞；
- 已测量确认的性能热点使当前项无法在可接受反馈时间内继续。

中断处理完成后必须**回到同一 primary item**，不得顺势切换到另一个 Goal Method。

### 不算“并行开新项”的工作

以下工作可与当前项并行，但只能服务当前项或全局安全：

- review finding 修复；
- CI / source guard / deterministic-build correctness；
- 共享 Planner / Compiler / Executor / Verifier 的 P0/P1 修复；
- 已测量性能热点的优化；
- 文档/状态面板同步。

## 5. Money Exit Gate

Money 完成并允许切换下一项，至少要同时满足：

1. 当前 Revenue denominator / transition / ledger / reservation continuity 的 P1 review debt 清零；
2. 6×3 production work plan 的每个 cell 都具备：
   - independent native outcome；
   - exact Teacher comparison；
   - criterion-local join；
3. train / validation / test 分区身份独立且可重放；
4. production coverage request 实际接入这些证据；
5. Goal Method self-test / reconciliation 的 Money 六个 criterion 真实进入目标状态；
6. formal-training gate 仍按全局规则 fail-closed，不因 Money 单项完成自动授权；
7. 相关文档和本状态面板同步；
8. 高成本全量验证仅在该项 Exit Gate 时统一跑一次，不在每个小 PR 重复执行。

只有上述标准完成后，才选择下一个 primary item。

## 6. 下一项如何选择

Money 完成后，不预先固定 Friendship / Skill / Museum 的顺序。先重新生成：

- Goal Method reconciliation；
- blocker 分类；
- 当前 evidence reuse 情况；
- 预计新增工作量 / 共享基础设施收益。

然后只选**一个**下一施工单位。

## 7. 文档治理规则

1. 本文件只保留当前状态，不堆叠历史 changelog。
2. `FORMAL_FULL_TRAINING_READINESS_CN.md`、`goal-conditioned-no-human-teacher-plan.md`、`FULL_SYSTEM_COMPLETION_PLAN_CN.md`、`CURRENT_WORK_CN.md` 中的日期章节均视为历史/证据记录。
3. 任何历史章节中的数字若与本文件冲突，以当前机器报告 + 本文件最新同步值为准。
4. 新 PR 若改变上述七个状态量之一，必须在同一 PR 或紧随其后的状态同步 PR 更新本文件。
5. 不再通过新增 handoff 文档来表达一个简单数字变化；优先更新本面板和机器 report。

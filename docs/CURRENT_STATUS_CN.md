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

## 7. 两组 `4/19` 的差集与恢复队列

当前两组 `4/19` 不是同一组，也不能相加：

| 状态 | criterion | 后续缺口 |
| --- | --- | --- |
| executable 且 split-complete | Community Center 两项 | 已进入 `CoverageGateReady` |
| 仅 executable | Skull Key、Pet Love | 当前 authority request 未重新绑定历史三分区 corpus |
| 仅 split-complete | Full Shipment、Master Angler | 顶层 Goal Method 仍为 `pending_dependency_expansion` |
| 两侧均未闭合 | 其余 13 项 | Method 与生产证据都要闭合 |

Skull Key/Pet Love 的旧生产工件仍是待重放证据，不是当前分子。它们需要按当前字典、日历、输入契约和哈希重新验证；
完全一致时直接重绑，只有失效分区才重采。Full Shipment/Master Angler 已有两通道三分区证据，后续只允许补顶层依赖展开，
不得另写第二套动作、候选或执行器。

这些修复不打断当前 Money `WIP=1`。Money Exit Gate 完成后，重新生成 reconciliation，并把上述四项作为高复用候选优先
评估；仍然一次只选择一个 root method。这样既避免丢弃历史成果，也不重新引入多个半成品并行推进。

## 8. 最近的深度优化状态

已进入主线并作为后续施工基础的改进：

1. action-origin 输出归因与通用 acquisition output sufficiency，避免执行成功但产物归属错误或重复写专用校验；
2. recovery 到 settlement 的版本化持久状态边界，未知持久字段默认失败关闭；
3. 同一次 Goal Method 调用内确定性复用 frontier，避免相同 authority 输入重复构建；
4. Planner、候选与 Executor 共用 `RouteGridPathfinder`、到达站位和终端计时语义，同一快照只构建一份当前地图距离场；
5. strategy-ledger 前后边界改为内容寻址 capture，绑定阶段、请求、候选、原生运行、快照与账本身份，不能再靠裸路径声明连续性。

这些优化减少重复 BFS、重复 frontier 构建、伪输出和伪账本连续性，但它们是 correctness/feedback-time 基础设施，**不直接增加**
`CoverageGateReady 2/19`。

PR #275 正在重验受共享 acquisition 修复影响的现版本证据。8 个已迁移的 current-authority strata 已通过；随后第一个仍使用
旧版工件的防风草样本因确定性目标日期路线漂移而失败关闭。因此历史 `26/26` 仍是已冻结生产事实，但**不能表述为当前代码已经
fresh 重跑 26/26**；现版本迁移与正式 promotion revalidation 必须单独记账。

#274 / draft PR #276 已把 validation trace 升级为 v2，增加命令级 allocation/GC/private-memory、operation 级 inclusive allocation、
read-session 表示保留量和 deterministic-cache producer 占用，并在命令收尾释放冗余表示。该切片目前只有静态差异检查，按安静模式
要求尚未构建或运行。恢复测试后先做 focused trace，再决定 rollout single-computation、unique-proof-first index 或进一步生命周期优化；
不得直接重跑全 26 层，也不得用 cache 命中降低证据门禁。

## 9. 剩余规模

- 战略分母：`19` 个 criterion、`11` 个 root method。
- 细粒度证据分母：`19 x 2` 个独立通道（Teacher comparison/native outcome）`x 3` 个分区，共 `114` 个 channel-partition cell；也可视为 `57` 个必须同时具备双通道的 criterion-partition join cell。
- 当前机器报告实际闭合 `24/114` 个 channel cell，即 `12/57` 个 join cell；仍缺 `90` 个 channel cell。
- Skull Key/Pet Love 对应的 `12` 个 channel cell 有历史工件待 current-authority 重放；Full Shipment/Master Angler 的 `12` 个 channel cell 已闭合，但缺 Method executable。
- 其余 `13` 个 criterion 分属 Money、Museum、Friendship、Marriage/House、Skills、Rusty Key 六个根方法，对应 `78` 个尚未闭合的 channel cell。Money 当前以双通道 join 计为 `0/18` production cell。

这些逻辑格不等于同等数量的完整三年运行。相同根方法允许复用权威前缀、checkpoint、原生结果和批量分区编排，但每个逻辑格
仍必须保留独立的 save/player/partition/criterion 身份与可重放证据，不能为了提速合并监督标签。

## 10. 时间估计

假定不调用 worker、复用现有执行器和生产工件、游戏版本/权威字典不发生大漂移，并保持 `WIP=1`：

| 阶段 | 预计主动工程时间 | 说明 |
| --- | ---: | --- |
| Money / Revenue Exit Gate | `5-9` 天 | 关闭 review debt、18 个生产 join cell、接入 coverage request 并统一做一次高成本验收 |
| Skull/Pet 重放与 Full/Angler Method 修复队列 | `5-10` 天 | 每次一个根方法；若旧 corpus 哈希完全一致，耗时接近下界 |
| Museum、Rusty Key、Skills、Friendship、Marriage/House | `12-20` 天 | 长日历与社交/婚姻状态是主要不确定性 |
| 全局 reconciliation、回退检查与 promotion review | `3-5` 天 | 达到 `19/19` 前的统一收口，不在每个小 PR 重跑 |

从当前状态到 `19/19` 正式训练准入，合理估计为 `25-40` 个主动工作日；生产采证中的等待和原生重试可与部分开发重叠。
首轮全量训练、holdout 评估和首个第三年爷爷 `21` 分模型另需约 `3-7` 天，因此持续投入的墙钟总量约 `6-10` 个日历周；
间歇开发按 `10-14` 周准备。RTX 5070 8GB 主要用于 LoRA/小批量正式训练，不会明显缩短游戏原生采证与 Method 闭合。

首个 21 分模型之后，达到“除声音略机械外，长期表现接近人类玩家”的陪玩还需约 `4-8` 周的人类适应、联机协作、节奏与
长线回归。稳定 `100%` 完美存档是第二产品阶段，保守按额外 `8-12` 周估计，不应反向阻塞 21 分首训。

### 估计适用边界

上述时间不是承诺日期。以下事件会触发重新估算：游戏或 SMAPI 版本变化、权威字典/日历指纹变化、历史 corpus 无法重放、
新增 P0/P1 跨域 correctness 缺陷，或真实性能 baseline 证明当前反馈回路仍不可接受。

## 11. 防回退与文档治理规则

1. 本文件只保留当前状态，不堆叠历史 changelog。
2. `FORMAL_FULL_TRAINING_READINESS_CN.md`、`goal-conditioned-no-human-teacher-plan.md`、`FULL_SYSTEM_COMPLETION_PLAN_CN.md`、`CURRENT_WORK_CN.md` 中的日期章节均视为历史/证据记录。
3. 任何历史章节中的数字若与本文件冲突，以当前机器报告 + 本文件最新同步值为准。
4. 新 PR 若改变上述七个状态量之一，必须在同一 PR 或紧随其后的状态同步 PR 更新本文件。
5. 不再通过新增 handoff 文档来表达一个简单数字变化；优先更新本面板和机器 report。
6. 每次 authority rebuild 都必须输出 criterion 级四态矩阵：Method executable、Teacher comparison 三分区、native outcome 三分区及其交集。
7. 曾经 `CoverageGateReady` 的 criterion 在新报告中回退时，合并门默认失败；只有显式 invalidation artifact 记录失效原因、受影响分区和恢复计划后才允许接受。
8. 局部动作注册、runner、Full Shipment strata、hermetic fixture 或性能测试通过，均不得直接改变正式 coverage 分子。

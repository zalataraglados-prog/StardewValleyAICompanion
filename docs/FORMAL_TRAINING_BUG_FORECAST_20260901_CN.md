# StardewAI 正式训练缺陷预测与主动静态审计（2026-09-01）

> 本文是风险与审计文件，不是运行时通过证明。所有“已发现”条目都区分为：源码可直接证明的静态缺陷、需要运行复现的高风险项、以及仅用于排期的数量预测。不得把预测数量当成已存在缺陷数量，也不得把静态审计直接写成 E3/runtime_verified。

## 1. 当前基线

当前 `main` 基线为 r25 / `formal-r25-20260901`：

- 228 registered
- 230 semantic
- 227 compiler-bound
- 145 RuntimeTestHarness dispatch
- 145 Product Executor dispatch
- 151 five-gate
- 62 strategy-training allowlist
- 2 catalogued blocked：`tailoring.dye_item`、`minigame.play_junimo_kart`
- 正式数据集：accepted 186 / rejected 0 / train 128 / validation 5 / test 53
- train pairs 3415
- 当前结构化 checkpoint：`structured-policy-52c9f785cc6dcc46c02f94e7`
- r25 单目标邮件 episode：5/5 `applied + verified + fresh`
- 服务器仍约 92% 磁盘占用，因此当前只允许有界批次，不允许无界长训

两个 pending 都不重新阻塞首轮自主策略训练：染色属于后置玩家命令能力，真实 Junimo Kart 代打属于训练后的独立 Minigame Skill；AI 自主候选的训练分母已经冻结。

## 2. 已经证明第一轮实机训练会持续挖出跨层缺陷

r20-r24 已经连续暴露并闭合：

1. 跨地图后沿用旧动作队列；
2. `LetterViewerMenu` 暂时不可输入时缺少有界稳定等待；
3. 高层 continuation 完成后外层 episode 仍可能继续选择第二目标；
4. continuation 阶段缺失 `candidate_id` 来源，导致训练轨迹 provenance 不完整；
5. r25 又暴露并处理了正式训练快照证据无限增长导致的磁盘压力。

因此后续 bug 预测不能按“动作表已经补完，所以只剩零星问题”估计。现在进入的是长时序、跨日、跨季、跨系统状态空间，缺陷发现率会明显高于单动作 E3。

## 3. 剩余缺陷数量预测

以下只统计会值得单独修复、回归或记录的工程问题，不统计拼写和纯格式问题。

| 里程碑 | 预计新增有意义缺陷 | 其中 P0/P1 预计 | 主要来源 |
|---|---:|---:|---|
| 稳定连续 3-7 个游戏日 | 8-18 | 2-5 | 跨日恢复、菜单状态、目标 continuation、时间预算、存档/重启 |
| 连续跨季并覆盖主要系统 | 10-25 | 2-6 | 季节切换、节日、作物/资源生命周期、长期承诺与资源竞争 |
| 跨年并完成 Year 3 / Grandpa 21 长跑 | 12-30 | 3-8 | 长回报回填、信用分配、存档长期漂移、评测终点、稀有剧情状态 |
| Companion 产品层：独立 body + memory + LLM interaction + multiplayer | 20-50 | 4-10 | 并发角色、玩家打断、记忆一致性、语言意图、联机同步、身份与权限 |
| 首个公开 RC 到 stable 1.0 | 15-35 | 2-6 | 安装升级、配置迁移、崩溃恢复、性能、用户环境差异、日志与兼容 |

**从 r25 到稳定 1.0 的中位预期：约 60-120 个有意义缺陷。**

如果把轻微性能、文档镜像、断言、边界验证、发布脚本兼容等也计入，实际修复条目可能落在 **100-220**。这不是坏信号；对一个同时覆盖游戏运行时、策略学习、长期存档、多人和用户产品层的系统，这是正常量级。真正需要控制的是 P0/P1 的发现时间：越早在训练和 E4 中挖出，越便宜。

## 4. 主动静态审计：当前已经发现的高价值风险

### FTB-001：Linux 独立训练盘的剩余空间门可能检查错文件系统

**级别：P1（在迁移到独立挂载盘后） / 当前单盘环境未触发**

`tools/StardewAI.LiveTrainingLoop/Program.ArtifactBudget.cs` 先对 `SnapshotDir` 调用 `Path.GetPathRoot(...)`，然后对得到的根执行 `new DriveInfo(root).AvailableFreeSpace`。

在 Linux 中，`/state/formal-training/...` 的 `Path.GetPathRoot` 是 `/`。如果下一步把 `/state` 或训练根挂载到新的独立数据盘，这个检查仍可能观察根文件系统 `/`，而不是训练数据实际所在挂载点。

**风险：**

- 系统盘空间不足时错误阻止一个仍有大量空间的数据盘；或
- 数据盘已经接近满盘，但根文件系统空间充足，训练继续写入并打满数据盘。

**修复要求：** 以实际包含 `SnapshotDir` 的最长匹配 mount/drive 为容量来源，并增加“根盘与训练盘不同”的 Linux 集成测试。扩盘/挂载训练根之前应优先闭合。

### FTB-002：负长期回报不会把已选动作变成负样本

**级别：P1/P2 模型正确性**

`StructuredPolicyTrainer.BuildPairs` 永远把 `selected - negative` 作为正方向比较；`ReturnWeight` 读取最长可用回报，而 `Optimize` 又把权重变成：

`1 + min(maxReturnWeight - 1, max(0, return))`

因此负回报被截成 0 后仍得到权重 1，训练方向没有反转。也就是说：一个机械执行成功、但战略上产生负收益的已选动作，仍会被训练成“优于当时所有未选候选”，只是没有额外加权。

**修复要求：** 明确训练目标究竟是行为克隆还是 return-aware preference learning。若是后者，需要 signed advantage / pair reversal / target probability 等能够表达“这次选择后来证明更差”的监督方式，并加入负回报回归测试。

### FTB-003：策略特征中的“完整性”被硬编码为真，缺失数值会静默变成 0

**级别：P1 数据质量，影响取决于上游 ready gate 是否永远能保证这些字段有效**

`Program.Dataset.cs` 当前固定写入：

- `completeness.required_readable_ratio = 1`
- `completeness.all_required_facts_readable = true`
- `planner_inputs.blocked = false`

同时 `Program.JsonHttp.cs` 的 `ReadFieldDouble` / `ReadNestedFieldDouble` 等在字段缺失或不可读时返回 0，字符串返回 `unknown`。

这会把“真实为 0”和“字段不存在/不可用”合并到同一训练特征，并可能与真实 `unavailable_fields` 计数相互矛盾。

**修复要求：** 从 snapshot/status/provenance 真实派生完整性特征；关键数值缺失时要么阻止策略样本写入，要么显式增加 missingness mask，不能用 0 冒充真实值。

### FTB-004：horizon JSONL 不是崩溃安全写入

**级别：P1 恢复性**

`JsonlPolicyHorizonObservationWriter.AppendIfNew` 使用“先扫描是否存在 -> `File.AppendAllText`”的非事务写法。若进程或机器在写一行中途退出，`PolicyTrajectoryDatasetBuilder.ReadObservations` 对任何损坏 JSON 行都会直接抛异常，整个 dataset rebuild 会被一条尾部半行阻断。

策略轨迹 writer 也使用 append，但 trajectory parser 至少会把 invalid JSON 记为 rejection；horizon 文件当前更脆弱。

**修复要求：** 使用临时/WAL + 原子提交，或至少在恢复时只允许并修复“最后一行被截断”的明确 crash-tail；同时增加 kill-during-append 恢复测试。

### FTB-005：当前训练数据路径会随样本增长形成近似 O(N^2) 累计 I/O/计算

**级别：P2，长训高概率触发**

- `JsonlPolicyTrajectoryWriter.Append` 每追加一条后再次扫描整个文件统计 `RowCount`；
- `PolicyTrajectoryDatasetBuilder.Build` 每次训练都 `ReadAllLines` 全量原始轨迹与 horizon，并重新写 cleaned/train/validation/test；
- `StructuredPolicyTrainer` 再次全量读取各分区并从头训练。

小数据下简单可靠，但跨季/跨年后会使每轮更新时间随历史数据量持续上升，最终可能重新出现“规划/训练延迟侵蚀游戏时间、UPS 或恢复窗口”的问题。

**修复要求：** 先加 profiling 门；达到阈值后把 append row count 改成持久计数/索引，dataset 改增量 manifest + 定期 compact，模型更新改批次/增量或降低重训频率。不能等到 Year 2 才处理。

### FTB-006：同一存档只允许一个 Grandpa terminal observation

**级别：P2/P1 Year 3 风险**

`PolicyTrajectoryDatasetBuilder.EnsureUniqueGrandpaTerminalObservations` 对同一 `save_id` 出现第二条 `grandpa_21` observation 直接失败。

如果最终 Year 3 验收模型需要表达“第一次终点评估未达到目标，随后继续生活并再次得到终点评估”，当前数据合同无法表示该过程。

**修复要求：** 在 Year 3 长跑前明确产品语义：如果只允许唯一最终验收，必须确保 writer 永不产生中间 terminal；如果允许重评，则 observation 需要 evaluation identity/ordinal，回填逻辑需要选择决策之后的正确终点，而不是全 save 唯一一条。

### FTB-007：rolling retention 对 artifact family 使用硬编码文件名正则

**级别：P2 未来维护风险**

`RollingArtifactRetention` 只删除正则中列出的 artifact family。当前已知 `SnapshotDir` 写入族基本被覆盖，但今后新增一个新的 per-iteration JSON 文件时，如果开发者忘记同步正则，它会在 rolling 模式下永久累计，而测试只检查已知族。

**修复要求：** 由统一 artifact descriptor/iteration metadata 驱动保留策略，或至少建立测试：扫描 LiveTrainingLoop 所有 `SnapshotDir` 输出族，要求全部出现在 retention catalog 中。

## 5. 处理顺序

训练盘扩容/迁移之前：

1. FTB-001 挂载点容量识别；
2. FTB-004 crash-tail / 原子 horizon 写入；
3. FTB-003 missingness 与 completeness 真值；
4. FTB-002 signed return 训练语义冻结。

开始多日批次后并行：

5. FTB-005 数据规模 profiling 与阈值；
6. FTB-007 retention 自动覆盖；
7. FTB-006 在进入 Year 3 之前完成数据合同裁决。

## 6. 训练阶段 bug 记账规则

后续每个新问题应至少记录：

- 首次出现的 release/run，例如 `r26`；
- 类型：planner / compiler / executor / runtime lifecycle / dataset / model / control plane / release；
- 是否影响策略标签；
- 是否污染既有数据；
- 最早可检测层：E2 / E3 / E4 / long-run only；
- 修复 commit；
- 回归测试；
- 运行复验证据；
- 是否需要重建 dataset/checkpoint。

禁止只写“修好了”。训练 bug 的重要性不在数量，而在是否能够证明旧数据仍可信、是否需要作废/重建，以及是否已经把同类问题前移到自动化门禁。

## 7. 当前结论

动作语义分母已经不再是主风险。当前最大的工程价值来自让真实训练持续运行，并把每个跨层失败转成更早的门禁、测试、恢复逻辑和数据合同。r20-r25 已证明这条路线有效；下一阶段的目标不是追求“零 bug”，而是让 bug 从长跑现场逐步前移到静态/单元/短 E3 即可发现。

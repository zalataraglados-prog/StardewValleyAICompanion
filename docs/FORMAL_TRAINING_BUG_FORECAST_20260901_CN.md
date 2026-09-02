# StardewAI 正式训练缺陷预测与主动静态审计（更新至 2026-09-02 r29）

> 本文是风险与审计文件，不是运行时通过证明。所有条目必须区分：已由真实运行发现并闭合的问题、源码可直接证明的静态缺陷/风险、需要未来运行复现的高风险项，以及仅用于排期的数量预测。不得把预测数量当成已存在缺陷数量，也不得把静态审计直接写成 E3/runtime_verified。

## 1. 当前基线

当前训练运行权威基线为 r29 / `formal-r29-20260901`：

- 228 registered
- 230 semantic
- 227 compiler-bound
- 145 RuntimeTestHarness dispatch
- 145 Product Executor dispatch
- 151 five-gate 历史动作口径
- 62 strategy-training allowlist
- 2 catalogued blocked：`tailoring.dye_item`、`minigame.play_junimo_kart`
- 两个 pending 均后置，不阻塞当前自主策略训练
- r29：1 次策略排序 / 4 个高层候选 / 9 个机械动作 / 3 次候选边界刷新 / 5 次候选内部 continuation 刷新
- 上述 8 次 fresh 刷新均未重新调用策略模型
- 4 个高层候选各形成 1 条成功策略轨迹，`selected_queue_decision_complete=true`
- 修正前训练根已经归档，910/910 文件 SHA-256 通过
- 服务器剩余空间仍不足以支持无界长训

issue #89 之后，运行证据新鲜度又新增独立治理维度：首批六个动作已经把 runtime path revision、源文件哈希、artifact/build identity 与三份运行 DLL SHA-256 绑定；其他历史动作暂为 `LegacyUnbound`，因此旧 five-gate 总数不能直接解释成“全部证据已完成新鲜度强绑定”。

## 2. 已由真实训练运行发现并闭合的问题

r20-r29 已经证明第一轮实机训练会持续挖出单动作 E3 很难发现的跨层问题。当前至少包括：

1. 跨地图后沿用 stale 机械队列；
2. `LetterViewerMenu` 暂时不可输入时缺少有界稳定等待；
3. 高层 continuation 完成后 episode 边界不正确；
4. continuation 缺少 `candidate_id` provenance；
5. 正式训练快照证据无界增长造成磁盘压力；
6. fresh snapshot 后机械重编译误接回策略 `rank-options`，导致移动/交互/等待阶段重复模型调用；
7. 装箱站位等机械参数被误纳入高层语义身份；
8. continuation 尚未结束时可能跨入队列下一候选。

第 6-8 项由 r27/r28 暴露，r29 收口。当前规范语义是：

```text
一次模型排序
→ 持有有序高层候选队列
→ 候选边界 fresh 校验
→ 候选内部 C# continuation
→ 队列完成或失效
→ 才重新调用模型
```

因此 `fresh snapshot` 本身不再等价于一次新的策略决策。

## 3. r29 后新增的训练数据审计重点

### FTB-008：decision state 与 execution state 必须分离

**级别：P1 数据语义**

r29 保留模型一次生成的高层候选队列，但第 2、3、4…候选真正执行时会面对经过前序动作改变后的 fresh state。

因此训练轨迹必须能够区分：

- 模型真正排序时的 `ranking/decision state`；
- 当前候选执行前的 `fresh execution state`；
- 原始 queue identity / queue position；
- 当前候选是否只是 preserved queue continuation，而不是一次新模型选择。

**风险：** 如果把后续候选的 fresh execution state 直接写成“模型在该状态下选择了此候选”，会产生伪造策略标签。

**要求：** 任何 queue-level / candidate-level trajectory schema 演进必须保留原始排序身份和执行态哈希；trainer 不能把 preserved queue candidate 冒充 fresh independent policy choice。

### FTB-009：队列缓存不能覆盖世界变化导致的战略失效

**级别：P1/P2 长训正确性**

减少模型调用不能演化成“把旧队列硬执行到底”。每个高层候选开始前仍必须重新核验时间、能量、资源、身份、玩家干预、目标完成度和候选先后依赖；任一变化足以改变策略排序语义时应使剩余队列失效并重新调用模型。

r29 当前已经实现候选边界校验，但该边界必须继续作为长训回归门，特别是多人、节日、商店关闭、资源被玩家先取走和长 continuation 场景。

## 4. 剩余缺陷数量预测

以下只统计值得单独修复、回归或记录的工程问题，不统计拼写和纯格式问题。r29 闭合了一个高频控制面问题，但没有显著缩小后续产品状态空间，因此总体容量预测维持：

| 里程碑 | 预计新增有意义缺陷 | 其中 P0/P1 预计 | 主要来源 |
|---|---:|---:|---|
| 稳定连续 3-7 个游戏日 | 8-18 | 2-5 | 跨日恢复、队列失效、菜单状态、时间预算、存档/重启 |
| 连续跨季并覆盖主要系统 | 10-25 | 2-6 | 季节切换、节日、作物/资源生命周期、长期承诺与资源竞争 |
| 跨年并完成 Year 3 / Grandpa 21 长跑 | 12-30 | 3-8 | 长回报回填、信用分配、存档长期漂移、评测终点、稀有剧情状态 |
| Companion 产品层：独立 body + memory + LLM interaction + multiplayer | 20-50 | 4-10 | 并发角色、玩家打断、记忆一致性、语言意图、联机同步、身份与权限 |
| 首个公开 RC 到 stable 1.0 | 15-35 | 2-6 | 安装升级、配置迁移、崩溃恢复、性能、用户环境差异、日志与兼容 |

**从当前 r29 到稳定 1.0 的中位预期仍约为 60-120 个有意义缺陷。**

如果把轻微性能、文档镜像、断言、边界验证、发布脚本兼容等也计入，实际修复条目可能落在 **100-220**。真正需要控制的是 P0/P1 的发现时间，而不是追求低 bug 数字。

## 5. 主动静态审计：尚未闭合的高价值风险

### FTB-001：Linux 独立训练盘的剩余空间门可能检查错文件系统

**级别：P1（在迁移到独立挂载盘后） / 当前单盘环境未触发**

如果容量检查仅对 `/state/...` 使用 `Path.GetPathRoot`，Linux 得到 `/`；当训练根以后单独挂载到新盘时，可能检查系统根盘而不是实际训练盘。

**要求：** 以实际包含训练根的最长匹配 mount/drive 作为容量来源，并增加“根盘与训练盘不同”的 Linux 集成测试。

### FTB-002：负长期回报不会把已选动作变成负样本

**级别：P1/P2 模型正确性**

当前 pairwise trainer 的方向仍以“实际选择优于未选候选”为基础；若负 return 只降低权重而不反转监督方向，则战略上失败的选择仍可能被训练成正偏好。

**要求：** 明确行为克隆 vs return-aware preference learning；若目标是后者，需要 signed advantage、pair reversal 或等价机制，并加入负回报回归。

### FTB-003：策略特征完整性与 missingness 需要真实表达

**级别：P1 数据质量**

关键数值字段不能把“缺失/不可读”静默折叠成真实 0；完整性特征也不能固定写 true。

**要求：** 从 snapshot/provenance 真实派生完整性；关键字段缺失时 fail closed 或显式写 missingness mask。

### FTB-004：horizon JSONL 崩溃安全

**级别：P1 恢复性**

直接 append JSONL 时若进程在尾行中途退出，单条半行可能阻断整个 horizon dataset rebuild。

**要求：** WAL/临时文件原子提交，或只允许并可恢复明确的 crash-tail；增加 kill-during-append 测试。

### FTB-005：训练数据路径随样本增长的累计 I/O/计算

**级别：P2，长训高概率触发**

当前多处仍存在追加后全量计数、dataset 全量重建、模型全量重训的模式。数据量小的时候最可靠，但跨季/跨年后可能逐步重新侵蚀 UPS、规划窗口和恢复时间。

**要求：** 先加 profiling 门；达到阈值后引入持久计数/索引、增量 dataset manifest + 周期 compact、批次或增量模型更新。

### FTB-006：同一存档 Grandpa terminal observation 语义

**级别：P2/P1 Year 3 风险**

如果同一 save 只允许一个 `grandpa_21` terminal observation，则必须明确这是唯一最终验收；若产品允许第一次失败后继续生活再重评，合同需要 evaluation identity/ordinal，而不能简单拒绝第二次终点。

### FTB-007：rolling retention 对 artifact family 的维护漂移

**级别：P2**

新增 per-iteration artifact family 时，如果 retention catalog 未同步，可能绕过滚动窗口永久累计。

**要求：** 由统一 artifact descriptor 驱动，或建立“所有 SnapshotDir 输出族必须出现在 retention catalog”自动测试。

## 6. issue #89 后新增的风险分类

运行证据 freshness 本身现在已经从“潜在审计缺口”升级为机器治理能力，但迁移尚未覆盖全部历史动作。后续每次 executor/runtime path 修改必须回答：

- 哪些旧 E3 evidence 仍与当前源码/构建完全一致？
- 哪些动作因 source hash / DLL hash / revision 漂移而自动失效？
- 是否需要重跑对应运行矩阵？
- 失效是否影响 training allowlist 或仅影响历史产品能力宣称？

禁止再用“历史上跑过 E3”直接等价于“当前实现仍然 RuntimeVerified”。

## 7. 处理顺序

训练盘扩容/迁移之前：

1. FTB-001 挂载点容量识别；
2. FTB-004 crash-tail / 原子 horizon 写入；
3. FTB-003 missingness 与 completeness 真值；
4. FTB-002 signed return 训练语义冻结；
5. FTB-008 decision/execution state provenance 做 schema/trainer 审计。

开始多日批次后并行：

6. FTB-009 队列失效边界压力测试；
7. FTB-005 数据规模 profiling 与阈值；
8. FTB-007 retention 自动覆盖；
9. FTB-006 在进入 Year 3 之前完成数据合同裁决；
10. 按 #89 逐域迁移历史 runtime evidence freshness。

## 8. 训练阶段 bug 记账规则

后续每个新问题应至少记录：

- 首次出现的 release/run，例如 `r30`；
- 类型：planner / policy queue / compiler / executor / runtime lifecycle / dataset / model / control plane / release；
- 是否影响策略标签；
- 是否污染既有数据；
- 最早可检测层：E2 / E3 / E4 / long-run only；
- 修复 commit；
- 回归测试；
- 运行复验证据；
- runtime evidence freshness 是否失效；
- 是否需要重建 dataset/checkpoint。

禁止只写“修好了”。训练 bug 的重要性在于是否证明旧数据仍可信、是否需要作废/重建，以及是否已经把同类问题前移到自动化门禁。

## 9. 当前结论

动作语义分母已经不再是主风险。r29 已把一个高频、昂贵而且会污染策略语义的控制面错误从真实长跑现场前移成明确架构规则：**fresh snapshot 不等于 fresh policy decision**。下一阶段应继续让真实训练运行，把队列失效、长回报、数据恢复、证据新鲜度和长期性能问题逐步前移到静态/单元/短 E3 即可发现。
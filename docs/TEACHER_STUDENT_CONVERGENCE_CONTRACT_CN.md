# StardewAI Teacher / Student 收敛与监督合同

> **作用域：** 本文件定义训练 / Teacher / Student 技术合同，不定义完整产品后期顺序；产品阶段先后以 [COMPANION_PRODUCT_ROADMAP_CN.md](COMPANION_PRODUCT_ROADMAP_CN.md) 为唯一权威。

> **2026-11-03T16:30:45+08:00 验收层级纠偏（R2）：** 按 #224、#390，局部 criterion 门不得依赖全局 fresh-save source schedule；Gourmand 不是 Master Angler 的无条件前置。[本次补充条款](PRETRAINING_ACCEPTANCE_SCOPE_CORRECTION_CN.md) 显式覆盖 `0b23350d` 的错误门归属及据此固定的第 10 顺位。旧状态数字与运行记录保留为历史；本次未修改实现或晋升机器状态。

状态：2026-09-23 架构合同，验收层级由 2026-11-03 R2 补充。本文覆盖早期“把已执行候选直接当正样本”的训练解释，但不改写历史运行事实。

## 2026-09-23 StrategicPolicy 单入口覆盖

Issue #129 的核心架构成立：产品运行时对外只暴露一个战略选择入口，但统一入口不等于把确定性规则、学习偏好、编译和执行揉成一个实现。`StrategicPolicy` 必须复用同一份权威候选分母、hard feasibility、reservation/ledger 与 Pareto 证据，并按以下顺序工作：

1. 唯一 Pareto 前沿成员必须由确定性选择分支决定。这里的“唯一 strict-Pareto”严格沿用现有 `OpportunityCostDominates`：该成员支配所有其他 admitted 候选，不允许用标量加权、模型分数或任意 tie-break 冒充。
2. 只有两个及以上 admitted、non-dominated 且互不可比较/等价的前沿成员，学习模型才可在该前沿内部排序。模型不得生成候选、扩大分母、恢复 blocked/dominated 候选或提供合法性证据。
3. 两个分支输出同一种 `StrategicDecision`，显式记录 `selection_authority`、输入状态/ledger/denominator 身份、frontier、模型/checkpoint 身份与准入状态，再进入唯一 verifier、Planner/Compiler、Product Executor 和 fresh native receipt 链。
4. `TeacherOracle` 只保留离线监督、DAgger 重标、反事实、覆盖证明和 benchmark 职责。运行时确定性分支称为 `DeterministicSelection`，不得包装成第二个在线 Teacher 主脑。
5. 是否存在唯一确定性解必须在加载或调用 checkpoint 之前判定。模型缺失、损坏、版本不匹配或未获 runtime authority 时，唯一确定性解仍可工作并记录 `model_invoked=false`；可选 shadow audit 的失败也不得阻塞它。若只剩不可比较前沿且没有获准模型，则 fail closed。`rare bounded Teacher fallback` 暂不准入，必须另立证据和治理门。

战略重规划只在 day start、目标/偏好变化、资源或 reservation/ledger 漂移、已选方法完成、执行失败和玩家打断等边界触发，不能每帧重跑完整 Teacher proof。连接器、动作和队列 continuation 之后已有的 fresh-snapshot precondition/replan 仍然保留；这些机械刷新不自动构成新的策略选择、Student 样本或模型调用。

## 结论

这套系统可以按工程标准收敛，但不能宣称神经策略一定收敛到数学上的全局最优解。

- Teacher 部分是有限、确定、可终止的：目标、日期、候选、资源和搜索边界都必须有限，同一版本化输入必须得到同一偏序结果或同一不可满足证明。
- Student 部分采用可测量的经验收敛：独立验证集分歧、长期成功率、硬约束违规率和恢复能力达到冻结门槛后停止迭代。
- 2026-09-23 历史实现尚未达到该状态，当时 19 个爷爷评分 criterion 只有 2 个具有完整可执行前沿，其余 17 个仍在依赖扩展中；该历史计数不覆盖当前机器 reconciliation。
- 当时 `StructuredPolicyTrainer.BuildPairs` 仍以 `candidate.Selected` 选出正样本。它只能代表历史行为克隆管线，不满足独立 Teacher 监督合同；是否已修复以当前源码与准入报告核验，不能凭本文历史状态授权正式全量训练。

## 三种不可混合的信号

后续轨迹和数据集 schema 必须显式记录以下来源，不能靠 `selected=true` 猜测：

1. `teacher_preference`
   - Teacher 对同一合法候选集给出的 preferred set、pairwise ranking、value margin 和反事实说明。
   - 用于监督排序、蒸馏和 DAgger 重标。
2. `native_outcome`
   - 原版游戏 fresh before/after、完成日边界和长期跨度产生的真实结果。
   - 用于环境回报、价值学习和最终评测。
3. `student_observation`
   - Student 实际选择、队列位置、执行状态和结果。
   - 只描述行为；未经 Teacher 重标或独立 outcome 归因，不得成为正偏好标签。

Hard constraints 不属于上述任一软标签。候选合法性、授权、资源保留、时间窗、路线、所有权和不可逆操作门继续由确定性代码 mask / fail closed。

## Teacher 的收敛条件

Teacher 全局只有同时满足以下条件才算闭合；这些是全局组合要求，不能逐根复制为一个局部门先完成整场游戏的要求：

- 19/19 criterion 至少有一条从新存档到既有 training-eligible option 的完整可执行路线；各根先按 criterion-local 合同闭合，其实际共享前置经证书组合，再在全局验证完整路线。
- 所有 OR 替代和 AND 前置均为类型化边，未解析分支只能形成显式 blocker；
- 搜索具有固定 Year 3 边界、有限候选集、有限资源状态、确定性 tie-break 和循环支配剪枝；
- 随机结果使用版本化分布、成功阈值、重试预算和 fallback，不读取 Student 不可见的未来随机结果；
- 每个选中高层候选都复用唯一 DailyPlan / ActionQueue / Product Executor，并取得 fresh 原生回执；
- 同一版本化快照、目标、知识锁和配置必须产生字节稳定的 Teacher 偏序或不可满足证明。

这保证 Teacher 搜索终止和结果可复现，不等价于证明某条动作序列是游戏中唯一全局最优路线。

### 2026-11-03 验收层级与 criterion-local 责任（R2）

<!-- acceptance-scope-20261103-r2; authored_at=2026-11-03T16:30:45+08:00; sources=#224,#390; baseline=0b23350d60a67993172db4a5d099b01fd9b54693 -->

依 [#224](https://github.com/zalataraglados-prog/StardewValleyAICompanion/issues/224) 与 [#390](https://github.com/zalataraglados-prog/StardewValleyAICompanion/issues/390)，严格区分：

1. **局部验收：** 每个 criterion 独立验证其分母、合法方法、Teacher goal context、native terminal assertion 与 train/validation/test 证据 join。复用 progression 不等于广播所有 criterion 已完成。
2. **共享前置：** 消费范围、身份和版本均明确的能力证书并在执行前 fresh 重绑定；不把只有代码节点的声明当证书，也不把整个上游 root 的全局游戏日程重复要求给每个消费者。
3. **全局集成：** 聚合 `Method executable ∩ Teacher comparison ∩ native outcome`，再在该层验证 source schedule、跨根资源/日历、连续性及独立重放。严禁把全局 fresh-save 证明反向挂回单根闭合条件。
4. **完整新档与模型评测：** 局部能力和全局聚合准备好后再做所需 Day-0 长期证明；正式训练/Provider/manifest 门不跳过，Student 新档 21/21 仍按训练与评测阶段实现。

Master Angler 局部门不无条件要求 Golden Walnut fresh-save source schedule、Gourmand `3/3` 或复合 `72/14/3` 口令。它仍须具备本 criterion 的鱼类分母、鱼区相关类型化前置、合法捕获链及 comparison/native-outcome 三分区；缺失真正必要证书继续阻塞。Gourmand 只是核桃来源 OR 方案，只有实际选中并承诺该方案时才验证对应请求/作物/原生回执；不能把可选路线硬化为所有捕鱼验收的前置。

已解锁姜岛隔离副本只提供有声明前置的局部能力证据，不证明从零解锁；source-schedule 验证器及其原生身份/数量/时序校验保留并移至正确消费者，不通过删除验证或强置 Ready 修复层级问题。

<!-- audit_invalidation: scope=r2_old_implementation_status; invalidated_at=2026-11-03T16:30:45+08:00; annotated_at=2026-11-03T18:52:51+08:00; superseded_by=PRETRAINING_ACCEPTANCE_SCOPE_CORRECTION_CN.md; historical_only=true -->
> **实现状态更新（2026-11-03T17:49:49+08:00）：** 下方关于 Issue #390 未完成、代码尚未修改的语句是 R2 写作时的历史快照。PR #391 已合并、#390 已关闭，限定范围是局部前置与共享证书责任解耦；不宣称逐鱼、全局源日程、Teacher/native 三分区、完整新档、正式训练已经完成。

这项合同修订优先于 `pretraining-order-20261103-r1` 的相反条款。立即下一工作项是既有 #390 的实现解耦与受影响回归，然后用正确局部范围重新估算队列；不得继续把 Master Angler 固定第 10 当作已证最优排期。详细覆盖位置、有效下一步与原因见 [R2 计划补充条款](PRETRAINING_ACCEPTANCE_SCOPE_CORRECTION_CN.md)。本次只改文档，不提升任何 coverage、不关闭 #390、不宣称代码已经整改。

## Student 的训练阶段

### Stage A：Teacher bootstrap

Teacher 在合法候选集上生成偏序和近邻反事实。Student 先学习 goal-to-method，再学习 bundle、日计划和多日价值。等价且仍保持 deadline、资源储备和未来可行性的顺序不得被自动标成负例。

### Stage B：DAgger

Student 自主 rollout 到自己的分布。Teacher 对 learner-visited state 重新计算合法候选和偏序，再把独立标签追加到数据集。失败执行、玩家打断或 preserved queue continuation 不得伪装成新的 Student 决策标签。

### Stage C：native outcome

在 Teacher bootstrap 和 DAgger 稳定后，真实跨日、跨季、跨年结果成为长期价值的主要依据。Teacher 退为离线 oracle、重标器和 benchmark；它不成为常驻在线主脑。少量高风险 fallback 只是未来可能单独准入的能力，当前不得启用。RL/价值微调是后续增强，不是当前 Teacher 图完成的替代品。

### Stage D：长期联机对照适配（游玩完全体 -> 人类化）

Stage D 不在 Stage 1 的 21/21 后直接启动。必须先以已准入的 Stage 1 checkpoint 进入 Stage 2，逐批开放并验收 Perfection 新依赖，直到原生 Perfection tracker 达到 100%，冻结可独立复现的单人完美策略基线；随后才启动 Stage D。此时系统已经是“游玩完全体”，但允许行为仍偏机械；Stage D 专门负责把它推进为长期协作下的人类化 Companion。

原型将与玩家进行长时间真实联机。每个可用样本同时保留 AI 决策/执行、玩家同期操作、玩家打断/接管/纠正、双方角色与资源分工、时间/位置/候选上下文、原生 outcome 和后续恢复。训练侧优先构造**同状态或可证明可比状态**下的人类-vs-AI 成对偏好、序列 imitation 和 decision-fine-tuning 数据，而不是把玩家所有动作直接当作最优标签。

Stage D 重点学习软行为：协作时机、角色分工、资源礼让、路线与等待节奏、减少无意义往返、何时主动帮忙或保持不干扰、以及玩家长期稳定偏好。硬合法性、权限、不可逆操作、资源守恒、时间窗和执行正确性继续由既有确定性边界负责，不能通过“更像人”绕过。

Stage D 必须与 Stage 1 数据隔离：人类适配样本带独立 provenance；关闭 Companion 适配层时必须能回退到同一冻结单人策略；任何拟人微调若造成单人 21/21、硬约束或原生执行回归下降，都必须拒绝 promotion。

## 可验收的工程收敛

每次训练代际都必须在冻结的独立存档、种子、农场类型和 Community Center 配置上评测。至少同时满足：

- 硬约束、授权和直接状态写入违规均为 0；
- 训练集、验证集和测试集按存档日期隔离，Teacher 标签来源完整率为 100%；
- Student 对 held-out Teacher 偏序的分歧率连续若干代不再显著下降；
- learner-visited state 的新增高严重度分歧和恢复失败率进入预设平台区间；
- 固定种子及未见种子的新存档均能在初次 Year 3 Spring 1 评价取得原版精确 21/21；
- 无死锁、重复执行路径、跨快照 stale 队列和快照造成的 UPS 回退；
- 冻结 checkpoint、知识锁、特征 schema、option vocabulary、compiler、executor 和评测语料后可独立复现。

具体统计阈值必须在第一批完整 Teacher 数据产生后冻结，不能现在编造数值。门槛一旦冻结，同一轮不得根据测试结果追改。

## 开训前 Provider / Student 定型门

Student / Teacher 的可替换接口、权限边界、Codex 实验职责和低配部署要求统一由
`STUDENT_TEACHER_PROVIDER_INTERFACE_REQUIREMENTS_CN.md` 规定。

本合同只保留监督语义：

- Teacher truth、native outcome、Student observation 继续分离；
- Student 不拥有候选合法性、Planner、Compiler、Executor 或 native verifier 权限；
- Teacher 可以被独立替换或禁用，不能成为运行时必须绑定的内置实现；
- 达到正式 coverage / reconciliation 门后、formal training manifest 之前，Codex 必须自行实验并决定正式 Student 的实现与参数；
- 本文不预设模型结构、输入编码方式、训练算法或超参数。

provider / 模型实验不得改写 Teacher truth、coverage 分母、候选合法性或执行语义。

## 当前训练禁入项

以下任一项存在时不得恢复正式全量训练：

- 任何 criterion 没有完整可执行路线或只以 prose/占位结束；
- 轨迹没有显式监督来源、Teacher 查询身份、候选集身份和 decision-state hash；
- trainer 仍把 `selected=true` 自动解释成 positive；
- Teacher preference 与 native outcome reward 写在同一不可区分字段；
- learner-visited state 不能由 Teacher 独立重标；
- 真实 terminal、跨日回执、来源哈希或 missingness 不完整；
- 训练样本依赖 Student 推理时不可见的信息。

2026-09-23 的历史状态命中前三项；当前具体 blocker 以新机器报告为准，本文不据历史数字宣称已修复或已开训。既有 r24-r35 运行仍可作为控制面、执行器、性能和恢复证据，但不得直接升级为新 Teacher 监督数据。

## 运行时职责

```text
TransparentBridge -> authoritative candidates -> hard admission -> Pareto frontier
                                                                  |
                                                         StrategicPolicy
                                   unique frontier -> DeterministicSelection
                              incomparable frontier -> LearnedPreferenceModel
                                                                  |
                                                        DecisionVerifier
                                                                  |
                                             Planner/Compiler -> Product Executor
                                                                  |
                                                    fresh verifier/outcome

Offline TeacherOracle -> supervision/relabel/counterfactual/coverage/benchmark
```

- `StrategicPolicy` 是唯一运行时高层决策入口，不拥有第二套候选、约束、路线、编译器或执行器。
- `DeterministicSelection` 对唯一 strict-Pareto 解保持权威；Student/`LearnedPreferenceModel` 只解决合法不可比较前沿中的 soft preference。
- Planner/Compiler 负责组合、时间/资源验证和机械展开。
- Executor 负责移动、工具、战斗、菜单和原生交互。
- TeacherOracle 负责训练期“什么更好”和独立覆盖证明，不复制执行器，也不成为常驻在线主脑。

## 人类适配边界

Stage 1 先形成可关闭、可复现的单人 21 分基线，Stage 2 再扩展并冻结 Perfection 100% 的完美策略基线；Stage D 才允许学习联机中的人类偏好与协作风格。`player_intent`、`player_preferences`、`cooperation_state`、资源归属、角色分工、拟人化节奏以及 human/AI 行为来源都属于基线冻结后的 Companion 适配层。

“玩家这样做了”本身不是 Teacher truth。只有在状态、可见信息、权限和候选集合可对齐时，人类/AI 差异才能形成 preference、imitation 或 decision-finetune 标签；否则只保留为 observation/diagnostic。后续适配样本不得回写或重标 Stage 1 基线。关闭适配层时，系统必须回到同一冻结策略和同一评测结果。

## 本块退出条件

本次文档/Teacher 路线块只在以下事项同时成立后收口：

- Master Angler 当前日期的完整多 connector 路径与终端时间储备由唯一 Core 权威计算；
- 到达过晚时 Teacher 和 fresh candidate gate 都失败关闭；
- 没有新增第二套路线、钓鱼编译器或执行器；
- Core、知识导出、option matrix 和 isolated regression 全部通过；
- 所有训练、路线图、字段和交接文档指向本合同，并明确当前禁入状态。

本块收口不代表 Master Angler 全路线完成，也不代表 19/19 Teacher、正式训练或 21 分长跑完成。

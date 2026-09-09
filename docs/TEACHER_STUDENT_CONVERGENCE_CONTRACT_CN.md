# StardewAI Teacher / Student 收敛与监督合同

状态：2026-09-08 当前有效架构合同。本文覆盖早期“把已执行候选直接当正样本”的训练解释，但不改写历史运行事实。

## 结论

这套系统可以按工程标准收敛，但不能宣称神经策略一定收敛到数学上的全局最优解。

- Teacher 部分是有限、确定、可终止的：目标、日期、候选、资源和搜索边界都必须有限，同一版本化输入必须得到同一偏序结果或同一不可满足证明。
- Student 部分采用可测量的经验收敛：独立验证集分歧、长期成功率、硬约束违规率和恢复能力达到冻结门槛后停止迭代。
- 当前实现尚未达到该状态。19 个爷爷评分 criterion 只有 2 个具有完整可执行前沿；其余 17 个仍在依赖扩展中。
- 当前 `StructuredPolicyTrainer.BuildPairs` 仍以 `candidate.Selected` 选出正样本。它只能代表历史行为克隆管线，不满足独立 Teacher 监督合同，正式全量训练必须继续关闭。

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

Teacher 只有同时满足以下条件才算闭合：

- 19/19 criterion 至少有一条从新存档到既有 training-eligible option 的完整可执行路线；
- 所有 OR 替代和 AND 前置均为类型化边，未解析分支只能形成显式 blocker；
- 搜索具有固定 Year 3 边界、有限候选集、有限资源状态、确定性 tie-break 和循环支配剪枝；
- 随机结果使用版本化分布、成功阈值、重试预算和 fallback，不读取 Student 不可见的未来随机结果；
- 每个选中高层候选都复用唯一 DailyPlan / ActionQueue / Product Executor，并取得 fresh 原生回执；
- 同一版本化快照、目标、知识锁和配置必须产生字节稳定的 Teacher 偏序或不可满足证明。

这保证 Teacher 搜索终止和结果可复现，不等价于证明某条动作序列是游戏中唯一全局最优路线。

## Student 的训练阶段

### Stage A：Teacher bootstrap

Teacher 在合法候选集上生成偏序和近邻反事实。Student 先学习 goal-to-method，再学习 bundle、日计划和多日价值。等价且仍保持 deadline、资源储备和未来可行性的顺序不得被自动标成负例。

### Stage B：DAgger

Student 自主 rollout 到自己的分布。Teacher 对 learner-visited state 重新计算合法候选和偏序，再把独立标签追加到数据集。失败执行、玩家打断或 preserved queue continuation 不得伪装成新的 Student 决策标签。

### Stage C：native outcome

在 Teacher bootstrap 和 DAgger 稳定后，真实跨日、跨季、跨年结果成为长期价值的主要依据。Teacher 退为离线 oracle、重标器、benchmark 和少量高风险 fallback；它不成为常驻在线主脑。RL/价值微调是后续增强，不是当前 Teacher 图完成的替代品。

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

## 当前训练禁入项

以下任一项存在时不得恢复正式全量训练：

- 任何 criterion 没有完整可执行路线或只以 prose/占位结束；
- 轨迹没有显式监督来源、Teacher 查询身份、候选集身份和 decision-state hash；
- trainer 仍把 `selected=true` 自动解释成 positive；
- Teacher preference 与 native outcome reward 写在同一不可区分字段；
- learner-visited state 不能由 Teacher 独立重标；
- 真实 terminal、跨日回执、来源哈希或 missingness 不完整；
- 训练样本依赖 Student 推理时不可见的信息。

当前状态命中前三项，正式训练保持禁用。既有 r24-r35 运行仍可作为控制面、执行器、性能和恢复证据，但不得直接升级为新 Teacher 监督数据。

## 运行时职责

```text
TransparentBridge -> legal candidate set -> Student ranking
                                    |-> rolling planner/compiler
                                    |-> Product Executor
                                    |-> fresh verifier/outcome

Offline Teacher -> supervision/relabel/benchmark
```

- Student 是运行时高层主决策器。
- Planner/Compiler 负责组合、时间/资源验证和机械展开。
- Executor 负责移动、工具、战斗、菜单和原生交互。
- Teacher 负责训练期“什么更好”，不复制执行器，也不长期代替 Student。

## 人类适配边界

Stage 1 只训练可关闭、可复现的单人 21 分最强基线。`player_intent`、`player_preferences`、`cooperation_state`、资源归属、角色分工和拟人化节奏属于基线冻结后的 Companion 适配层。

后续适配样本必须携带这些上下文，但不得回写或重标 Stage 1 基线。关闭适配层时，系统必须回到同一冻结策略和同一评测结果。

## 本块退出条件

本次文档/Teacher 路线块只在以下事项同时成立后收口：

- Master Angler 当前日期的完整多 connector 路径与终端时间储备由唯一 Core 权威计算；
- 到达过晚时 Teacher 和 fresh candidate gate 都失败关闭；
- 没有新增第二套路线、钓鱼编译器或执行器；
- Core、知识导出、option matrix 和 isolated regression 全部通过；
- 所有训练、路线图、字段和交接文档指向本合同，并明确当前禁入状态。

本块收口不代表 Master Angler 全路线完成，也不代表 19/19 Teacher、正式训练或 21 分长跑完成。

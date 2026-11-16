# 联合消费者静态合同检查

## 2026-11-16 检查结论

**结果：未通过。先修新联合消费层的合同接线，不改旧执行器来迎合新要求。**

检查基线为 `e80bef81b839330cec29fec1bc2e019534d97a22`。本轮没有修改 C#、编译、启动测试、重新启动 Main 或派发游戏动作。现有 Main19088 自行在第155轮退出；保留原现场和全部工件，不以自动重跑掩盖问题。

本报告是静态检查及已有工件核对，不是新的原生验收。已有 CI 成功不代表下面的组合链成立，也不重开已经接受的 L1 19/19、Root 11/11 或169–172证据。完整联合终局、Teacher21及正式训练仍未通过；`formal=false`。

## 范围与方法

- 对联合入口、owner、选择/续行、原生读取、生产汇总和终局链建立了144个跟踪 C# 文件的 Roslyn 语法/门禁清单，共1553个 `if`、`Require`、throw-expression 节点。语法错误0，核对后144个输入哈希均未改变。
- 清单保留在仓库外 `I:\StardewAI-Collab\resident-refill-20261108\static-joint-consumer-audit-e80-inventory.json`，SHA256为 `79a43897ab1d697542099a0a9bbd8a0fe929eee6646ac53f3737c36ac4c5689d`。不把这份自动扫描冒充1553条语义均已证明正确。
- 人工沿下表的入口/边界核对实际生产者和消费者；追加读取原 `DailyPlanCompiler`、`ActionQueueCompiler`、可用性评估器、Runtime聚合写入、Product派发/查询以及生命周期/留存消费者。不是只读最近补丁或测试断言。
- 旧有界社交等待追溯到 `8c153c85`、`7decfcdc`。本机未找到对应旧聊天 JSON，故仅声称已核对 Git 源码/历史，不声称读到了当时对话。
- 读取现有116、126、134、136、138、139、147、152、153、155等工件，未改写或重新生成它们。

| 合同边界 | 核对结果 |
| --- | --- |
| 日选择持久化、Main恢复和组合门 | 发现 JC-01 |
| 原候选身份与新鲜路线/端点的关系 | 发现 JC-02 |
| 动态 typed continuation、重试次数、游戏时间 | 发现 JC-03 |
| 普通候选注解与原有界等待格式 | 225已有窄适配；不冒充原生执行成功 |
| 高层动作、executor原语及可空子计划 | 原生读取的 primitive-only 范围有依据，不放宽为直接执行高层计划 |
| wire字段、journal/nonce及Product源哈希 | 未确认新的合法拒绝；保留原身份/来源合同，见下文限制 |
| aggregate步骤、反馈状态、等价完成 | 新显式模式已有区分；不得把等价完成升级为原生完美证明 |
| prepared transfer、cooking、库存租约与来源归属 | 独立协议本身应保留；汇总入口发现 JC-04 |
| 共享来源、生产credit与原生命周期连续性 | 原来源/资源保护应保留；不得把外部收获记作本作物产出 |
| 保存读回、同现场续行、时钟重置谱系 | 具名消费者区别于旧默认；不能把任意状态漂移视作刷新 |
| 出货、控制等待、回家、睡觉和终局读回 | 终局suffix发现 JC-05 |
| 原宏选择、机械刷新、结果与训练轨迹 | 发现 JC-06 |

## 确定问题

### JC-01：恢复日选择的调用晚于组合门（P1，现场已观察）

`Program.cs:838`只在作物阶段为 `Complete` 时恢复日选择，但 `CropTeacherRunPlanner.DailyDispatch.cs:7` 在 `RouteHome` / `NativeSleep` 时需要已有日选择才进入组合。Main重启后内存选择为空，因而跳过原本应负责恢复的内部路径。

原136的候选完成、目标完成均为false；Main4036尝试138社交重试。Main19088随后以相同已接受状态接回，却执行139回家、147睡觉，没有执行已修准入的600tick社交等待。这是丢失未完成安排，不是等待本身已经验收成功。

修正方向：在决定是否组合之前，由既有owner读取本日原选择；不改旧作物阶段定义、不增加新的Planner、不回滚存档。

### JC-02：把上一张地图的物理候选ID当成跨图目标锁（P1，现场已观察）

`Program.CropTeacherDailyOtherGoal.cs`调用原选中候选编译入口；`Program.QueueBuilding.cs`在没有 typed continuation 时按原ID精确过滤。原152计划合法包含任务路线，部分路线并未声明 `continuation.quest_candidate_id`；旧读取器对此返回null，不等于旧路线不合法。

153执行的是 `quest.accept_daily` 的 Farm→BusStop连接器。原聚合写入 `selected_candidate_completed=true`、`objective_continuation=null`、`objective_continuation_completed=false`。这只证明该物理候选完成，不证明整个高层任务已接取。

155旧可用性返回合法候选 `quest:13:Quest:bound:route:BusStop:44,22:warp:to=Town:arrival=0,54`，但新锁仍指向原 `...route:Farm:80,15:warp:to=BusStop:arrival=11,23`。新过滤结果0，编译器依法报告 `empty_action_list`，Main停止。不是旧执行器拒绝合法路线，也不是游戏缺字段。

修正方向：明确“宏目标锁”和“本轮物理动作完成”的不同身份；复用已有任务身份、候选重物化及目标判定。对没有 typed cursor 的合法旧路线做显式联合适配，不将旧编译器强行改成新格式，不仅比较字符串前缀，不任意放开目标匹配。

### JC-03：联合续行丢弃执行后更新的目标状态（P1，静态调用链确定）

- `Program.QueueBuilding.cs:321`允许调用方传入当前 continuation，否则回落到初始锁内的 continuation。
- `Program.CropTeacherDailyOtherGoal.cs:21`没有传入当前值。
- `Program.cs:956`又用初始候选覆盖 `activeObjectiveContinuation`。
- `CompleteCropTeacherSharedSelection`在目标未完成时原样返回selection；持久化恢复仅重建完成集合，不恢复最后接受的动态cursor。
- `SharedSourceYield.ReadSharedSourceSelection`重新从原计划构造初始锁。因此只修调用参数还不够，准入和恢复也必须消费同一个已接受cursor。

原Runtime已有 `RefreshAppliedObjectiveContinuation`，会从已执行队列更新继续目标；社交评估器也读取 `retry_count`、`retry_game_time`，当前源码上限为12并要求游戏时间推进。新联合路径丢弃这些更新，可能反复回到初始状态。尚未把这点冒充现场已经复现无限等待，因为225修复的等待没有实际执行。

修正方向：从同owner已接受的原receipt恢复/推进同一cursor，统一供编译、准入、内存续行和重启使用；保留原次数/时间政策，不另建一套重试状态机。

### JC-04：公共生产汇总入口未按完整共享模式选择已有消费者（P1，静态调用链确定）

`CropTeacherManagedSeedOwner.ProductionEvidence.cs:31`主要按 `nativeWork` / `retainSaleStock` 选择分支。底层已实现的 `BuildJointRetainingTransferredSaleStockAsync` 和 `BuildJointRetainingSharedInventoryEffectsAsync` 只有测试反射调用，owner公共入口没有接入。

只包含已接受的prepared transfer/cooking、却不含 `AppliedNativeWorkQueue` 的合法联合历史，会落入旧的marker-only/普通留存消费者并被拒绝。含native work的混合历史还需区分库存/销售保留意图，不能仅因存在一个原生工作就推断完整模式已对齐。不能声称当前现场已经走到该终局，或它必然以完全相同原因失败。

修正方向：从已接受的类型化来源引用选择已有消费者；保留原转移、烹饪和库存来源账，不把库存变化伪装成资源中性。验证应经过owner公共汇总入口，而非仅反射测试底层方法。

### JC-05：终局suffix未接纳原控制恢复动作（P1，静态可达）

`Program.cs:627`在终局阶段同样优先调用原控制稳定逻辑；owner将其记录为 `operation_kind=control_recovery` / `transition_kind=control_recovery`，`ProductionTrace`也保留该行。

但 `CropTeacherTerminalSettlementEvidence.cs:112`只接受出货接近、出货、返家和最后睡觉，任何其中插入的合法控制恢复行都会落入拒绝分支。控制等待不是额外出货，也不应从原序列删除来凑连续性。

修正方向：复用已有控制恢复验证结果/读取器，把原控制行及连续状态关系显式纳入新终局消费；不重写移动、攻击、出货或睡觉，不虚构生产credit。验证只需短的“出货→控制稳定→回家/睡觉→结算”组合，不重跑十三天。

### JC-06：机械刷新证据被当成原策略ranking记录（P2，现场已有记录失败）

`Program.cs:946`将 `other.EvidencePath`（`selected_queue_candidate_refresh.v1`）交给 `AppendPolicyDecisionTrajectories`，同时覆盖原decision上下文。该文件明确 `policy_model_invoked=false`，不含完整 `ranked_event_candidates`；轨迹入口却按原策略ranking合同读取。

现有116、153均有 `policy_trajectories=0; trajectory_rejected:InvalidOperationException`。原native receipt、普通dataset行及owner接受记录仍在，不是执行结果全部丢失。旧日志只写异常类型，不能捏造具体throw位置或错误消息。

修正方向：原宏选择证据与机械刷新证据分开；结果关联回原不可变选择，不制造新的机械偏好标签，不放宽训练ranking合同。具体训练数据准入仍由原#569负责，不把本项扩成重新设计模型/数据集的前置门。

## 不应误修的旧合同与未确定项

- `executor.*`命令的 `SocialPlan` / `QuestPlan` 为null有旧编译路径依据：高层计划先降为原语；不因高层命令存在这些属性就放宽原语读取器。225的“省略null字段与显式null”等价只发生在比较副本。
- Product观察到的实时before可能漂移，但原服务仍发送原request JSON，原结果绑定原request哈希；单独的Product before/drift/replan观察负责解释差异。不能强迫两个不同语义的hash相等。
- Product receipt查询不会把已存原response改成重发结果。重复POST的 `product_idempotent_replay` 与原只读lookup不同，不能为了接纳重发而放开旧保护。
- 相同存档/玩家/版本、明确day边界、journal/nonce、完整原输入哈希、禁止重派发、库存来源和销售保留仍是必要校验，不是本轮要删除的“过严条件”。
- `selected_candidate_completed`与父目标完成的关系已有JC-02的真实语义错配；没有证据证明其余可信原writer随意伪报完成，不另外增设对抗式验收门。
- 数字wire文本与数值的等价、applied但无changed-facts的动作、更多宏原语别名目前未找到当前联合路径中确定可达的合法反例；只登记限制，不据猜测改旧parser/结果默认，也不声称全动作兼容已证明。

## 修复与验证顺序

1. 把JC-01/02/03作为同一“选择身份与动态续行”能力修正：复用旧目标读取/刷新核和owner已接受历史，覆盖不中断及同现场恢复两条入口。先让原合法安排不丢失、不被旧地图ID误拒绝，不更改旧执行行为。
2. 同时修JC-06的证据接线：保持原策略选择与机械续行各自身份，确保结果可关联，不制造训练标签。
3. 接入JC-04已有生产消费者，并在JC-05终局中保留、核对原控制行；只适配新聚合边界。
4. 每项只做改变层和相邻合同的定向验证。使用原编译器生成夹具及已有失败工件；保留负例以证明没有放开其他玩家/存档、其他任务、库存来源或重复派发。公共入口至少覆盖一次混合链，不能只测空共享列表或底层反射入口。
5. 上述修正通过后再决定是否续接同一现场；先核对当前owner头和未保存尾部，不直接重跑/重新load。原19/19、11/11及169–172不重验。

这是现有#550/#572联合链内部修复，不增加新的Teacher总验收条件，不变更后续原第5步实际全局组合、#570→#569的顺序。原#570 A1已有177的 `selected_route_unavailable` 结果，不再重新盘点或另造Planner。

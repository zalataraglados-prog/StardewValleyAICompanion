# 原全局日计划续行与集合控制模式

## 两个不同用途

| 显式参数 | 作物结束后的选择来源 | 要求/边界 |
| --- | --- | --- |
| `--continue-after-crop-teacher-run` | 原Stage One集合Teacher优先，必要时普通daily fallback | 原集合authority及原恢复/managed合同；保留旧用途 |
| `--continue-global-daily-plan-after-crop-teacher-run` | 原普通全局日计划/GoalResolver，不自动启用集合Teacher | 原managed owner、单人原生反馈/回执/保存、stop留存；全候选入口 |
| 均不启用 | 原校准/显式恢复的停止行为 | 不取得任何新续行权限 |

旧模式的依据是 `cfcac4674419ff95a06a9b3d8f3ab8000de4f3b9`
（2026-10-26，Continue collection control after crop teacher）及其原CURRENT_WORK/STATUS。
它是有意的集合控制模式，不是一个需要改掉的旧执行器bug。244真实记录131、145、
原生收杆/出货/日界等工作，但不能因此声称普通全局跨根选择已验证。

## 新入口

标准 `scripts/Invoke-ProductBootstrapCalibration.ps1` 增加尾部可选参数
`-ContinueGlobalDailyPlanAfterCropTeacherRun`，保持所有旧位置参数不变。
已有Main运行调用可以直接传同名CLI开关。参数与原
`-UseManagedCropTeacher -JointDailyCalendar -ArtifactRetentionMode stop`
一起用于R2当前共享日历路线；其余原保存、端口、反馈、留存与资源预算参数仍须提供。
这不是要求重启当前隔离现场或新建存档的命令。

新模式与旧集合续跑、standalone collection、require-source-schedule及require-Gourmand
互斥，防止在声称ordinary global时实际激活集合优先模式。原
`-CaptureGoldenWalnutFreshSaveEvidence` 单独采集来源仍可用，不把采集行为等同于
require完成或正式权限。初始化候选option/参数/kind/ID覆盖不允许；后续原编译器生成的
合法队列及其具体参数不因此被禁用。

新旧续行共同复用原业务边界：原episode完成、实际终局/睡眠已接受、原handoff落盘后
才释放owner及作物租约，之后再选择。普通维护夜/未接受日界不释放；原件改动或写入
冲突保留原owner，不能开始另一份。新模式避免原首日校准快捷退出，但不豁免原单次
尝试上限、完整owner业务、共享账本、保存或正式准入。机械维护仍走原owner/执行器，
不是每步新调用上游模型。新owner的控制次数由原dispatch重置。

报告/启动器summary只在新模式启用时增加
`continue_global_daily_plan_after_crop_teacher_run=true`，默认不增加false字段。
原run身份、独立artifact-run身份、保存槽与来源请求绑定规则保持。

## 全局主目标与顺手工作共享预算（247）

只有新global续行模式、joint calendar、原GoalResolver返回`resolved`且无owned composition、
恢复/剧情中断、active continuation或内部定向覆盖时，才启用本节。旧集合模式及默认入口不变。
原日计划子入口以前先按goal过滤候选，其他目标的顺手工作因此无法参加联合预算；现在保留
同一次排名的候选池，并把原`bound_candidate_ids`作为`primary_candidate_ids`传给同一个Core编译器。
不增加第二个Planner、模型请求、快照扫描或执行器。

- 主目标ID是一组**备选**，不是每项都必须完成。ID须唯一且来自本次候选；一个备选blocked
  不否决另一个合法备选。原`required_candidate_ids`仍是必须前置的工作，原验证和优先级不变。
- 顺序为原required/其商店前置、主目标/其商店前置、其余候选；后两类仍保留原排名。
  至少一项主目标被原编译器排入前，不允许无关工作代替主目标；空组也不退化为摸宠物。
  商店前置本身不算主目标已排入。原deferred等待后的fresh-source barrier继续阻止余项。
- 所有排入项共用原日历、体力和库存/消耗预留；主目标占用的时间、体力和物品不能再次花。
  不改原时间估计、跨图/动作展开、恢复或源回执规则，不把局部日程视为三年全局最优。
- 新请求字段与plan回显默认均为null且不写出，旧joint公开重载签名不变。新模式要求
  **匹配的新Backend/Core与Main一起部署**；Main在拿到相同主目标ID回显后才交出计划。
  旧Backend忽略未知字段会明确报`daily_plan_primary_goal_constraint_not_applied`，不会静默派发全池。
  这个兼容检查不是新增原生证据或正式训练授权门。
- 定向最终Core76项、Backend/实际私有Main接线108项通过，零失败/跳过；覆盖默认、旧模式、
  owned hook、handoff、空绑定、旧Backend、错误回显和重复回显。后端接线夹具的空queue不冒充原生。
  另以保留244的0001/0004/0008三组真实候选（196/101/1）及快照做新旧默认编译对比：
  计划一致；队列除原随机ID外逐字段相同。新主目标排序也交原ActionQueueCompiler得到pending。
  这是只读候选向量兼容检查，不是原过滤请求的精确重放或游戏执行；原件/hash保持。

## 已接受保存的Main交接（255）

原global交接本来就绑定已接受的终局过夜与原生保存；它仍不是完整聚合、语义读回或正式训练授权。
Main现在接收结构化的保存观察、前后指纹及接受后的state，复用原保存核，不把这项事实丢掉后
再以次日为基线重复睡觉。只有原完成策略同时确认主任务完成，才用这次保存结束本轮；后面还有
主任务时，待消费记录必须丢弃，后续工作仍需要自己的最终保存。默认校准及维护夜行为不变。

254实际同场景已接受8件作物投放、205旧货+1400作物=1605原生结算和126/native132的4文件保存，
到春28；133是此后多余睡觉的失败回执，必须保留，不能改报告冒充成功。255的38项及真实
126/132/133离线兼容通过，不声称重新跑出一次Main退出0。原生保存true与完整聚合/读回/formal
false必须分开。

跨进程接续时，`continue-live-crop-managed-owner`用于尚未完成的原owner，不应复活已交出的周期。
当前后继复用普通全局Main入口、原全候选和同一已隔离游戏/保存/预算；先用原已接受边界及
保留133尾部核对当前持久世界，不强求动态元数据或state hash完全不变，也不删字段/改原件。
例如当前接受头为`a7f4cdc4`，133观察为`c095a719`，原持久比较核相等、资源无变化；新规划须读
当前状态，不能把睡前/已接受快照当作当前画面。正式准入、第5步/#570/#569仍按原计划。

## 当前种植窗口的上游交接边界（256）

普通全局续跑在春28暴露了真实边界：有未种种子且无作物，不代表现在能启动本季生产。
Resolver沿用原Teacher的首收不晚于本季末规则；只有可读时钟和作物目录证明整个目录都
不适合该窗口时，才继续原方向绑定。不能只检查自有种子，因为Teacher也可买另一种作物。
未知/缺失资料继续原交接，不当作不可行；布局、供给、灌溉和经济完整可行性仍由原Teacher
负责，不另造规划器，不放松成熟规则，也不删候选/强制替代目标。

原134快照与149候选离线重放已证实上述排除，原Teacher仍拒绝50项，原绑定自然转到社交；
316项定向/相邻回归通过，不替代原生续跑。134准备阶段账本合法刷新到413，后继匹配部署
必须保留它及原journal。当前仍是同一春28现场，不重跑前27天或旧256调用。

后续257已按原预算完成普通日计划和原生保存到夏1，实际after147为9788cf69，4文件保存指纹
93b008dc独立核对；目标完成仍false，不冒称社交或完整Teacher成功。258从该保存/after及原
session、退出记录接续，全部普通Main参数保持不变；原Teacher自然选8格蓝莓并建立0148 owner。
因此旧春28启动包和原132保存交接已不是当前入口，不能重跑；最新现场以CURRENT_WORK顶部为准。

## 246验证及尚未完成的事

- 最终定向81项（26秒）及标准启动器64项AST/真实parser检查通过，零失败/跳过。
  原owner夹具覆盖新旧模式下接受终局释放、维护夜拒绝、未接受日界拒绝、证据变化、
  写入冲突与默认不续行；夹具不是新的真实游戏验收。
- 没有改变旧执行器、GoalResolver、DailyPlanCompiler、原native verifier、两项正式
  native blocker或报告九类gate；没有复制新Planner/Executor。
- 本块未启动新的多日游戏运行。当前仍使用244春15保存及236+244来源引用；不要恢复
  旧07de、重跑235或十三天前缀。选线先从新鲜观测交给原入口，再据真实结果绑定预算。
- **完整第5步未完成：** 仍须固定实际所选路线的来源、数量、期限、共享资源/日历及
  适用Day-0证据。旧crop dispatch默认8格是有界初始生产，不是100万收入能力证明；
  不得为过验盲改64格、强制某个候选、另造规划器或把本模式标记当成功证据。
- 完成实际路线及其原生缺口之后，才接#570同一Core正式准入核到报告/prepare/launch，
  再#569实验室。formal=false，原存档/回执/日志/local-data不进Git。

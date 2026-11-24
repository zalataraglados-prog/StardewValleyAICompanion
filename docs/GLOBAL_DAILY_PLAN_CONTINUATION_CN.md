# 原全局日计划续行与集合控制模式

## 正常交接与本机并行验证（300）

记录于 **2026-11-24T22:40:29+08:00**。用户明确裁定：“同步进行，交接正常完成，在本机异步开启验证测评，正式开训的准备和实验可以和测试同时跑”。据此，完整三年资源方案及长周期测评不再卡住本次工程交接、数据准备和选型实验；它们由实验室在当前电脑D盘单独推进，不能再倒挂成重做社区中心或整局的功能验收条件。

**交接消费范围。** 显式功能引用通过严格原生读取、完整候选/联合单日计划重放、六类原件固定及fresh校验后，原两个native gate分别为 `verified/functional_existing_access_verified` 和 `not_applicable/selected_existing_access_requires_no_gourmand_acquisition`。后者只表示本次已有访问的功能路径没有新增Gourmand取得，不冒领三单或替代核桃入账。完整Day-0证明器保留给实际声称完整原生取得的路径。

**保留的边界。** 无证、原件变更、身份/版本或计划不符继续拒绝；执行时的时间、体力、库存、预留和授权检查不变。独立Teacher监督、语料来源与分区、模型/Provider/低配验收、正式manifest与实际启动授权仍各按原合同。`FormalProductTrainingAuthorized`不会因本次功能门通过变true。允许准备和实验并行，不等于已正式开训或三年目标已通过。

**原启动入口。** Windows `Start-FormalProductTraining.ps1` 使用可选 `-NativeRouteProofPath <native-route-proof.json>`；Linux附着入口使用 `STARDEWAI_FORMAL_NATIVE_ROUTE_PROOF=<原证明文件路径>`。两者读取一次后把同一 `native_route_proof` 放入prepare/launch请求；传入引用不授额外权限，仍由原Core在两阶段fresh读证。不传时保持旧默认，缺证仍拒绝。启动脚本也须使用最终工程版本，不从旧脚本省略该字段后另加绕过开关。

**已可复用的交付。** 492文件的精确输入闭包保留原D路径及字节，通过未发布交接草稿传给接收机；接收机在隔离 `23388126` 构建后实际接受8帧及原联合日计划，恢复前后与重放后SHA全数一致，覆盖数0。原报告SHA为 `d2c3d4ed9cd1e6fc48396211a4747343fc34b3c424e0f5d53505b7feb09cb927`，已取回本机核对。报告中的正式门状态属于299旧范围，不能冒充本次新范围的最终结果；原件、读取及功能事实不需重采。当前新范围的fresh报告与CI由同一PR #596接续。

**并行工作。** 实验室用原验证器/测试入口在D盘启动独立后台验证与计划能力评估，明确区分契约检查、候选计划检查、原生功能和真正的三年达标。没有输入或尚未完成的项如实记录，不造预测、原生历史或PASS。选型环境、语料接收/冻结及正式训练配置准备同时推进，正式选型结论仍使用冻结真实输入。本机运行身份、PID、输出、阶段和退出码写入 `D:\StardewValleyAICompanion前瞻实验室\验证测评\20261124`；#569维护接手状态与未齐输入。

首批后台检查已完成76项期限/联合日历契约、真实14/14及三种准备后依赖变更拒绝；候选Gourmand日期194/206在原供货核中条件成立，不作为实际取得日期。通用目录报告中的未选随机来源缺项不转成当前路线阻塞。另已从封存的夏10存档复制启动原全局选择器的有界暖启动原生评测（`--skip-training`，512次尝试/128项验证预算）；它不是Day-0或完整三年成功证书，原始存档不变，运行状态见同目录`native-warm-baseline/state.json`。

<!-- audit_invalidation: scope=299及更早将完整三年资源方案绑定作为功能工程交接及选型准备前置; invalidated_at=2026-11-24T22:40:29+08:00; superseded_by=GLOBAL_DAILY_PLAN_CONTINUATION_CN.md#正常交接与本机并行验证300; reason=用户明确正常交接且本机验证测评与正式开训准备实验同步进行; historical_only=true -->
> **299的局部失效。** “缺目标计划因此不能通过当前功能交接”的准入关系被用户本次裁定取代。299的六依赖固定、prepared变更拒绝、独立复核和真实功能事实继续有效。完整三年方案未被宣布完成，而是转为并行验证责任；这项范围修订不是删除执行硬约束或授予正式训练权限。

## 复核：功能结论与正式门分开（299）

记录于 **2026-11-24T22:07:15+08:00**；修正代码 `6f3ea6e7`，仍在PR #596。主线独立复核确认298的功能重放有效，但发现两项有依据的准入缺口，现已修正并保留旧结果。

1. **功能通过不自动等于正式门通过。** `SourceReplayVerified=true`、`proof_scope=functional_existing_access` 表示八帧原生功能与联合单日计划已经验证。没有目标方案的资源、取得/支付/解锁顺序及期限对账时，两项正式门返回 `functional_verified_target_plan_not_bound`。这不否定第5年样本、不要求同档长跑。开训前是否还要求整个三年计划核验的范围裁定待用户明确，不能替用户默认放行或擅自新增采证任务。
2. **所有帧的输入均固定。** 每帧的queue、receipt、before、after、request journal和gzip共六份原件都在不可变功能引用里固定路径及SHA，并与严格读取器实际消费路径/哈希逐项一致。除真实14/14正反例外，在独立复制的协议夹具中分别改动另一动作的前帧、日志或gzip头部，prepare后的launch均拒绝；原生源文件未改。协议夹具的引用重定位不计为新原生结果。

当前真实功能输入在 `D:\StardewValleyAICompanion前瞻实验室\准备\R2隔离运行\GingerIsland后半段验证\functional-admission-20261124-reviewed\native-route-proof.json`。[新路径、SHA及复核矩阵](../experiments/r2-lab-evidence/20261124/functional-route-reviewed/)是当前接手入口。相关可移植回归205项通过（含先前40项及原完整来源验证器回归），构建0警告0错误；无新游戏、无正式启动。

<!-- audit_invalidation: scope=298及138e3a49工件将功能正例直接映射为两个正式门通过; invalidated_at=2026-11-24T22:07:15+08:00; superseded_by=GLOBAL_DAILY_PLAN_CONTINUATION_CN.md#复核功能结论与正式门分开299; reason=单日功能重放不能冒领未绑定目标计划的资源顺序与期限结论且所有原件须固定prepared基线; historical_only=true -->
> **298旧结论的局部失效。** 原8帧功能结果、单日预算和旧11项反例事实保留；旧两项正式门通过的映射不作为当前准入结论，旧公开工件保持历史原貌。实际三年组合计划尚无选中方案，273八格蓝莓和275春季共享预算不能冒充它；春27手工限定三种岛鱼的 `InvalidDenominator` 是已失效施工方向的诊断，不是主循环阻塞，禁止为其变绿伪造预测或强推岛鱼。当前功能接线可独立复核，整体准入仍按确定的实际范围判断。

## 功能证据与联合计划回放（298）

记录于 **2026-11-24T21:54:37+08:00**；代码基线 `00c5a75d6929a41a8f4a28cd16b6d24157555eab`，基于主线协作者的 `41e14b1f`。代码需合入后成为公共运行基线；以下是实验室在该源码上的实际重放结果。

**功能样本与目标期限分开。** 用户明确本次验证功能，不因已解锁样本位于第5年要求重玩社区中心或同档长链。原样保留年5春25/总日472，不能改成第1年，也不能把既有解锁算成计划新取得。第三年前达标仍是组合计划的期限要求。本次只证明下面这个联合日计划与其原生能力绑定，不宣称所有跨年资金、核桃取得及爷爷21分均已证明。

<!-- audit_invalidation: scope=296及更早接续中等待完整同档全局实跑封存后才验功能或接消费者; invalidated_at=2026-11-24T21:54:37+08:00; superseded_by=GLOBAL_DAILY_PLAN_CONTINUATION_CN.md#功能证据与联合计划回放298; reason=用户明确功能复用不受样本年份限制完整新档实际取得与计划期限分属不同证明; historical_only=true -->
> **范围失效说明。** 下方“等待实验室交付完整全局选线及原件”等描述，不再是功能复用接线的前置；297以前实验室将局部交付称为整个交接已完成的口径也不成立。局部事实继续保留，按本节明确消费。完整 Day-0 验证器保留给实际声称完整原生取得的路线，不降低其自身条件。

新显式引用 `functional_native_route_proof_reference.v1` 仍由原 `FormalNativeRouteAdmissionConsumer` 消费，报告、prepare、launch 不换入口。旧引用、缺证和未知 schema 继续阻塞；功能引用不能夹带 `native_evidence` 伪装成完整新档取得证明。

| 核对对象 | 实际结果与边界 |
| --- | --- |
| 原选择及预算 | 从实际黄貂鱼动作前源快照重新用原 evaluator/ranker 建立完整215项候选池，保留全部可用捕鱼备选，原 `CompileWithPrimaryGoal`、联合日历和 `ActionQueueCompiler` 编译。选择捕鱼及出货接近，时间 `770→727→648` 分钟、体力 `426→419→419`。完整重编译比较计划、审计和机械队列，不接受调用方自报预算或手改候选成本。 |
| 原生功能 | 原严格 Product-aware 读取器提取到 Core，由 Bootstrap 原接口复用；八个原回执/请求/前后快照均接受。计划中的捕鱼命令绑定真实源状态、执行参数及 `(O)836` 收藏 `false→true`。只排除重新生成的步骤ID和已经独立核验的计划预算注释，不放宽原回执本身。 |
| 既有访问与资源 | 原鱼区访问投影已解锁；各原生帧核桃 `12→12`。本计划不选择 Gourmand 或新增核桃取得/付款，故当前功能范围为 `functional_existing_access`，Gourmand 为该范围的 `not_applicable`。这不是三单完成证明，也不是证明从零取得了替代核桃。 |
| 实际执行范围 | 捕鱼与六段正反连接是独立用例，未把它们拼成同一条连续旅程。新联合计划的出货接近只重放编译，未声称本次已执行出货或收到收入；既有春季结算与回接继续按275自身范围复用。新鱼获未保存；继承升级、测试设置和未保存尾部均不升级为完整新档履历。 |
| 三个消费者 | 报告、prepare、launch 得到相同两项功能门结论；prepare后改变嵌套编译工件，launch会重新读证并拒绝。其余报告门使用既有隔离协议夹具，只证明接线，不产生新的Teacher覆盖、正式manifest或启动授权。 |

**可重放输入。** 本机原件位于 `D:\StardewValleyAICompanion前瞻实验室\准备\R2隔离运行\GingerIsland后半段验证\functional-admission-20261124-final\native-route-proof.json`；同目录保留原候选请求、完整编译响应和证据引用。原始八例仍留在原 `island-suffix-20261124-native` 目录，无覆盖和重新采集。[公开路径、SHA、编译摘要、构建身份及反例记录](../experiments/r2-lab-evidence/20261124/functional-route/)用于跨机复核；完整快照、存档和运行数据不入Git。外部原件不可访问时报告明确缺件，不把公开摘要当作原生输入。

```powershell
# BinaryRoot 指向此源码 Release 构建目录；两个脚本的输出目录必须为新目录。
./scripts/Build-FunctionalRouteEvidence.ps1 -SourcePacketPath <297原索引绝对路径> -ExpectedPacketSha256 26ac167ab082ed79eef74a82e9da5bbd247a8010afed0ff561bf0dffd10c5f5e -BinaryRoot <Bootstrap构建目录> -OutputDirectory <新证据目录>
./scripts/Test-FunctionalRouteEvidence.ps1 -ProofPath <新证据目录/native-route-proof.json> -BinaryRoot <Bootstrap构建目录> -OutputDirectory <新回归目录>
# 原报告命令保持 build-goal-method-formal-training-admission-reconciliation，附加：
# --native-route-proof <native-route-proof.json绝对路径>
```

**验证。** 489项相关定向、71项Backend相邻及40项可移植测试通过（有重叠，不相加作覆盖分母）；真实工件11/11正反例通过，三消费者一致且准备后嵌套变更被拒绝。Bootstrap构建0警告0错误。初次因系统仅有.NET6导致的CLI子进程失败、以及game-free项目漏列既有测试依赖的编译失败保留；仅设置本进程.NET8路径并修正项目清单后通过。无游戏启动，无新原生数据，`formal=false`。

**接续与责任。** 实验室交付上述功能绑定和消费者实现，主线协作者复核并集成同一分支；不再互相等待同档完整实跑。#570的现行功能接线按本输入复核；完整第三年组合计划、未选取得分支、正式Teacher/数据/Provider与#569授权，分别按其合同判断，不能由本单日正例自动宣布完成，也不能倒挂为重做已通过功能的理由。

## 实验室交付接收：姜岛后半段（297）

接收 [74741ed0 的选线、账本和证据索引](https://github.com/zalataraglados-prog/StardewValleyAICompanion/blob/74741ed0235a2a967431a020cc5416fa17681c49/experiments/r2-lab-evidence/20261124/island-suffix/route-handoff/selected-local-route-and-ledger.json)。
对照 [#570 交付记录](https://github.com/zalataraglados-prog/StardewValleyAICompanion/issues/570#issuecomment-7128708739)，以 Git blob 重建源 CRLF 字节后，索引、共享访问证书、重放摘要、构建身份及八对原计划/单动作队列共 20 份文件均匹配源 SHA-256；没有放宽原件哈希校验。八份记录按用例、动作、前后状态哈希及 `applied/verified` 对齐 `08f1b800` 的实验室离线重放。这是对公开文件的静态对账；D 盘原请求、回执及快照不在此主线主机，没有在本机再次声称严格原件重放。

可直接复用的是**已解锁档姜岛后半段的局部证据**：同一 `island-suffix-20261124-native` 运行/存档/玩家有已重放的 L2 访问证书，六段正反连接和两次单项捕鱼。年5春25（总日472）06:10→06:20 捕获 `(O)836`，原生收藏和库存 `0→1`；本段核桃 `12→12`，没有新来源或付款。既有解锁、已有 Gourmand 3/3、钓鱼 10 级测试设置及未保存的鱼获不产生原生取得、全局日期、正式训练或 Teacher 比较信用；独立用例不拼成一条旅程。无需重跑这段捕鱼、社区中心或已冻结 L1。

**现行验收口径：** 功能验证不按样本年份拒绝。年5已验证的连接和鱼获可以作为所选方案的原生能力来源，不需要从新档在同一存档物理重跑整条路线。此前把单一完整原生长链当成交接输入的说法不再适用：它混淆了动作可用性与第三年前的路线可行性。第三年目标仍须由原选择、组合计划重放和共享资源/日历对账验证，不能用样本历史替代计划上的取得、付款、访问和期限；使用 Gourmand 或其替代来源仍按实际选线核对。每份原生证据维持各自 run/save/player 和 SHA，不拼接出虚假的 Day-0 履历。完整 Day-0 验证器保留给实际声称完整新档证明的路径。

**当前待验：** 复用本包、已验作物结算与春季共享行为，绑定实际所选方案并验证原组合计划和正式消费者；不是等待一份新增的全局实跑封存。此对账未证明 #570 通过；`formal=false`。

**复核与交付入口：** 原件仍由实验室留在 D 盘；[原重放入口](https://github.com/zalataraglados-prog/StardewValleyAICompanion/blob/74741ed0235a2a967431a020cc5416fa17681c49/experiments/r2-lab-controller/island-suffix/Replay-IslandSuffixWithDevelopmentSource.ps1)只读复用原 Product-aware 读取器。主线构建用现有 Bootstrap、Backend 和 LiveTrainingLoop 项目；总报告命令 `build-goal-method-formal-training-admission-reconciliation` 与 `/api/v1/training/session/prepare`、`/api/v1/training/session/launch` 不换入口。[Provider 和数据责任](STUDENT_TEACHER_PROVIDER_INTERFACE_REQUIREMENTS_CN.md#6-开训前-provider-与模型验收)留给 [#569](https://github.com/zalataraglados-prog/StardewValleyAICompanion/issues/569) 按原合同接收，不生成正式 manifest。

## 第5步本机收口（296）

273 的自然选线仍是夏1八格蓝莓，确认为8包、640金。296只验证独立夏14晨档的首收结算修复：
8格、24颗实际入包、24颗待售预留请求，无出货和实收，不将独立副本改记为原 owner 的续行。
已有夏15独立落盘仍有效，无需重验。275原春季验证的同日跨目标预算与出货结算可直接复用，
但不能代替实际夏季选线尚缺的同谱系后续产出、收入、共享日历或姜岛日程；预计4,800金不是实收。
此时停止本机实跑，等待实验室交付第5步实际全局选线及其原件；不搜旧档、不放宽准入。

## 复用已接受的跨目标预算与原生回接（275）

春季封存 W（235）已能证明同一份日计划内的跨目标原生执行，不需要重跑。
从原 closeout 哈希闭包把 58 条 accepted shared source return 对回 12 份原联合日计划，
复用 `SelectedQueueDecisionLease` 对选择索引，核对作物与外目标共用的时间/体力计数、
原生回执与同一账本的回接。19 条标记原选择完成，其他是合法中间进展；42 条运行时
候选 ID 与原选择 ID 不同，分别保留并按原回执的对应关系核对。不把标签
`goal.social.raise_friendships` 解释成实际完成友谊目标。

| 原 W 选择 | 共同预算 | 实际回执 |
| --- | --- | --- |
| 第33轮，春1：浇水与买种 | 当次时间 580→496 分钟，体力 250→246 | 购买完成回执 seq38：`(O)427` +1、现金 -20 金。候选尾段的数字不是数量。 |
| 第231轮，春13：收获与出货 | 当次时间 780→757 分钟，体力 270→270 | 外目标 seq226/227 两件出货，待结算值 0→175→437 金。同一终局证明确认六件实际结算 1,137 金，437 已包含在内，不另加收入。 |

复用了仍可读、未修改的原 closeout；未重放原生动作或重开游戏。仓库外对账
`block275-accepted-joint-budget.json`（401 项原件哈希，SHA-256
`bf7060ebd46f3213fd6863034315369359333edc188bbba0064783c6a79f158e`）。
这证明的是 W 具体春季计划的组合能力，不是夏季蓝莓的后续收获、跨目标整年预算
或姜岛/钓鱼日程。原第5步仍须对所选路线的新增部分补原生证据；不重复验证 W
或把不同周期的货物及预算拼成一份结算。第6步与正式训练准入不变。

## 第5步实际选线对账（273）

原普通全局循环第148轮在夏1（total day 28）由满分目标解析到
 `grandpa.direct.earn_money`，只要求 Teacher 建立初期作物生产。原调度
 `joint-w-20261116-0637.crop-teacher.initial.0148` 与原 owner 的计划一致。
它不是覆盖第三年春1之前 100 万累计收入的完整选择。

| 对账项 | 已证实 | 尚未证实 |
| --- | --- | --- |
| 种源、数量、期限 | 零可用种子；夏1 09:10 在皮埃尔店以 80 金一包实际买入 8 包蓝莓种子，共 640 金；原 owner 接受序号 5–12。夏1 实际播完 8 格，序号 26–29。 | 首批以后扩大生产所需资金、物料及来源。 |
| 生产与收入 | 原计划 day 41／45／49／53 四批，各至少 24 个，合计 96 个、预计最低毛收入 4,800 金。原证据到夏3早晨；获授权的开发副本完成夏3维护并保存夏4（day 31）。 | 最早收获日 day 41，尚无本路线真实收获、留存、发货和收入结算证明。4,800 不是实际收入，也不是扣种子后的净收益。 |
| 共用预算与日历 | 只读核对 6 份挂在接受链上的日计划（原 day 28/29 各两份、开发副本 day 30 两份）。均由原联合编译器扣同一当次时间与体力；例如副本首批 4 格消耗规划 12 分钟、8 体力。 | 六份均只接受作物浇水；外目标因候选上限／必做工作未完全排入等未进入同队列。计划的 social 目标标签和余量都不能证明外目标已执行或获得跨日共同预算。 |
| 新档来源与姜岛 | 复用原 Day-0 验证器，原边界快照匹配原 run、存档、玩家；开发加载边界单列。 | 此有界作物选线没有姜岛核桃分支，原运行也未输出姜岛 episode manifest/source schedule，无法调用完整原生 schedule 重放。未选择不能写成 Gourmand 不适用。 |

用原 `verify-money-revenue-crop-teacher-dispatch` 命令对封存清单做只读重放，报告
SHA-256 `5e1d2042dccd0944b1e89834df4c679c8966f80841e9b91afaa8eb3ce33d8b36`。
实际来源对账为仓库外 `block273-selected-route-evidence-v2.json`（163 项哈希引用，
SHA-256 `4b22efb0896d9786d4f9afd30cb30c7fc40f88b3aa8826cc4165653d8f29c476`）。
原42–47及挂在该尾部的两份日计划不进入接受证据；本次未启动游戏。

**接续点：** 已固定有界选线，不重验种源、播种和夏3。下一处原生缺口是夏4以后同计划首次收获与资源回流，以及确有另一目标入队时由原共享账本证实其占用。现有授权只覆盖夏3，不从开发保存自动推进夏4，不将该副本升级为正式 Day-0 谱系。待真实主路线及其适用验证成立，再按 [#570](https://github.com/zalataraglados-prog/StardewValleyAICompanion/issues/570) 接正式报告/prepare/launch，再交 [#569](https://github.com/zalataraglados-prog/StardewValleyAICompanion/issues/569)；`formal=false`。

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

258随后在夏3浇水47校验中断：原生回执applied、owner尚未接受，真实after196不能被旧接受46
替换。先对照原合同与保留回执解释fresh来源及持久资源差异，不重浇/回档，不由CI绿推断原生通过。

## 246验证及当时的接续边界

后续实际选线对账见本文顶部273，本节保留246当时的验证结果和入口边界。

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

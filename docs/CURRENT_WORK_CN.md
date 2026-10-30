# StardewAI 当前工作

> **当前状态入口：** [CURRENT_STATUS_CN.md](CURRENT_STATUS_CN.md)。本文件下方带日期的状态、计数和“下一步”均保留为历史证据；若与当前机器报告或 CURRENT_STATUS_CN.md 冲突，不得按历史数字规划新工作。
>
> **施工顺序：** Goal Method / formal-coverage 主线实行 **WIP=1**。v15 已完成 v10 之后的一条 Money 连续生产段，Community Center、Pet Love、Skull Key 与 Full Shipment 已按 current authority 冻结；当前唯一施工项为 `complete_master_angler` 顶层 dependency、日历窗口与 coverage 接线。P0/P1 共享 correctness blocker 可临时中断，但不得静默改序或并行展开多个 root method。

## 2026-10-30 v87 原生回归已证明补水走廊修复

- PR `#369` 已合入 `main`，本机默认 MSBuild `GamePath` 由被忽略的机器配置固定到 E 盘隔离 SMAPI 运行时。普通构建不再进入 ModBuildConfig 对失效 F 盘路径的自动发现；具体绝对路径不进入公开仓库。
- v87 从全新 day-zero 运行到 iteration `104` 后在完整日界安全停止，取得 `100` 条 verified action，stderr 全为零。第二轮 Teacher 的实时承诺绑定 `(72,27)`，补水准备格为空；iteration `93` 完成场地准备，`100` 完成补水入口，`102..104` 完成回家、睡眠和新日。v86 树桩阻塞没有复现。
- 本轮仍是 `--skip-training` 校准，不能增加正式训练分子。下一 WIP=1 入口仍是 Golden Walnut source schedule：新运行身份继续首次登岛、核桃来源增量、First Parrot/Turtle/House、source schedule 生成及独立重放；不得因为走廊回归通过而跳过 #345 Provider/Student gate。

## 2026-10-30 v86 已耕补水走廊不得被重复清障替换

- `golden-walnut-source-schedule-v86-20261030-151929-exclusive-recovery` 从新 day-zero 身份在 iteration `97` 前取得 `94` 条 verified Product action、`9` 条日界回执并完成第一轮 Crop Teacher。第二次作物 handoff 在 iteration `81` 正确形成 `8` 格防风草承诺，但 iteration `98..102` 重复要求清理同一未升级斧头无法处理的 `2x2` 树桩，故安全停止。运行仍为 `--skip-training`。
- 根因位于透明布局语义而非执行器。旧 `watering_refill_approach_standable` 直接使用静态 occupancy，`HoeDirt` 即使无作物也被视为阻挡，导致首轮已清走廊不能复用。透明桥现新增 `native_walking_collision_blocked`，以原生 `IsTileBlockedBy` 的 passable 忽略掩码计算；Teacher 只对旧证据中的 `already_prepared` 做兼容，其余未知或不可执行阻挡不放行。
- v86 原始 request 离线重放得到 `(70,27)` 补水入口和三个普通对象清理格，不再包含 `(74,25)/(74,26)` 的树桩格。Teacher hermetic、GameFreeGovernance `530/530`、Backend `257/257` 与 TransparentBridge Release 均通过。代码变化后必须从全新 v87 重跑；不得把 v86 尾部、离线重放或 fixture 当作 source schedule。
- 构建环境同时完成一次性修正：仓库支持被忽略的 `Directory.Build.local.props`，本机文件与用户级 `GamePath` 均固定到 `E:\StardewValleyAICompanion-runtime\Stardew Valley`。公开仓库不提交本机绝对路径，普通构建也不再反复命中失效的 F 盘 Steam 目录。

## 2026-10-30 v85 独占恢复不得被战略 handoff 抢占

- `golden-walnut-source-schedule-v85-20261030-120056-legacy-dialogue` 从全新 day-zero 身份推进 `425` 次迭代，取得 `306` 条有语义 verified Product action，完成三轮 Crop Teacher，并推进到 Spring 22 / `total_days=21`。第三轮收获为 `8/8`；邮件、任务 `13`、钓鱼、容量恢复、出货与多次日界也均走现有原生执行链。该运行继续 `--skip-training`，没有参数训练或正式 checkpoint。
- iteration `421` 的日计划已选定 Backwoods -> Farm 的 `recovery:return_home`，ranking 同时明确 `exclusive_recovery_override=true`。旧主循环却先读取遗留的 `crop_production_teacher` planning handoff 并立即 dispatch，导致 19:00 的作物 Teacher 在不存在当前季可成熟种子的现场连续失败。不得通过放宽种子、季节或经济约束修复。
- 唯一修复点在 `PlanningHandoffCapture` 的调度资格：完整 handoff 遇到独占恢复时返回 defer，主循环记录原因后让原 queue 继续执行；恢复完成后按现有 fresh-replan 机制重新求解。没有第二套恢复、作物规划、编译或执行器。
- v85 原始 `ranking-response-0421.json` 对新程序集的离线结果为 `DeferHandoff=true / DispatchHandoff=false`。专项 `15/15`、GameFreeGovernance `530/530`、Backend `257/257`、LiveTrainingLoop Release 零警告零错误。未过滤旧 Core 套件仍有 36 个与本改动无关的 current-main 基线失败，另有 3043 项通过；它们不被伪报为本切片全绿，也不改变当前 WIP=1。下一固定入口为全新 day-zero v86，并继续既定 Golden Walnut source-schedule 退出条件。

## 2026-10-30 v84 旧式日界对话修复与 v85 入口

- `golden-walnut-source-schedule-v84-20261030-101752-menu-preflight` 从全新 day-zero 身份运行并继续携带 `--skip-training`。它证明 PR #365 的菜单预检能跨过 v83 边界：Crop Teacher 完成，两轮作物阶段取得 `5 + 4` 条日界回执和第二轮 `8` 条收获回执；Stage One 继续执行邮件、任务 `13`、钓鱼、容量恢复、出货和路线。iteration `198` 前共有 `147` 条有语义 verified Product action。
- iteration `198` 的睡眠把日期原生推进至 Spring 11 `06:00`，随后活动 `DialogueBox` 已关闭，但 `Game1.currentObjectDialogue` 仍有旧式全局对象对话，状态为 `dialogueUp=true / active_menu=none / CanMove=false`。旧透明 movement context 漏读 `dialogueUp`，旧睡眠执行器也只处理活动菜单，于是返回 `post_sleep_world_not_stable`。锁定 1.6.15 反编译确认：`DialogueBox.closeDialogue()` 可在队列尚有内容时保持 `dialogueUp=true`；原生 `Game1.pressActionButton()` 负责逐条推进并最终恢复移动。
- 修复没有增加第二套睡眠或对话系统。现有睡眠执行器仅在日界精确前进一日、菜单为空、无问题/事件/角色对话且 `currentObjectDialogue.Count > 0` 时发送有界原生右键脉冲；完成、阻塞或超时时都释放 override，未知状态保持失败关闭。`NativeNewDayWorldStable`、透明 native-control gate 和 `NativeControlSettleRecovery` 均显式要求 `dialogue_up=false`。
- iteration `199..227` 的 `29` 次 recovery wait 只有 wait 计数变化，时钟事实保持 `600->600`。no-progress 现把附带的未变化事实视为等待的一部分，只有其他真实游戏事实变化才重置 streak；达到 `MaxQueueNoProgressStreak` 后停止。旧报表中的 `176` 必须拆为 `147` 条有效执行和 `29` 条隔离校准噪声，不得写入训练或 coverage。
- v84 不能跨代码变更续接。下一固定入口是全新 day-zero v85，顺序仍为首次登岛、Golden Walnut 来源增量、First Parrot/Turtle/House、source schedule 生成与独立重放，再进入 #345 准入门。没有 Student 更新、checkpoint、GPU 训练或正式训练授权。

## 2026-10-30 v83 长跑、失败边界与 v84 入口

- `golden-walnut-source-schedule-v83-20261030-074742-global-settle` 从全新 day-zero 身份运行 `324` 次迭代，取得 `223` 条 verified Product action、`433` 个 fresh-save episode、`8` 条收获回执与 `5` 条日界回执。Crop Teacher 于 iteration `140` 完成；之后 Stage One 继续完成邮件、任务 `13`、钓鱼、背包恢复、Fishing `ItemGrabMenu` 恢复、出货、日终恢复和 Spring 17 -> 18 原生睡眠结算，金钱 `20 -> 461`。滚动保留窗口正常维持 `64` 次迭代并回收失去引用的内容寻址块。
- 最终失败快照固定在 Spring 18 `16:30`、`ScienceHouse (8,20)`、Carpenter `DialogueBox`。路线 timing 未就绪但菜单本身已有安全 `recovery_close_menu` 候选；Stage One 却在选择该候选前调用 Master Angler 目标日期意图构建器，连续五次抛出 `future_route_movement_timing_context_outside_calibration_scope`。根因是控制编排顺序，不是路线校准范围不足。
- PR #365 已将 mandatory menu 预检拆为独立 partial 控制链并放在严格意图构建之前。预检只在活动菜单时查询 recovery/profession/mail 三项，保留职业与信件处理对通用关菜单的抑制；恢复继续使用现有候选评估、排名、日计划、动作队列与 Product Executor，控制工件不生成 Teacher 标签。无合法恢复时仍走原严格构建器，不吞掉未知菜单错误。
- 验证为专项 `7/7`、Core `529/529`、Backend `257/257`，E 盘隔离 SMAPI `GamePath` 下完整解决方案测试/构建通过。v83 显式 `--skip-training`，没有 Student 参数、checkpoint 或 GPU 训练。其失败尾部不能跨未保存边界续接；下一固定入口是全新 day-zero v84，继续首次登岛、Golden Walnut 来源增量、First Parrot/Turtle/House、source schedule 生成与独立重放。

## 2026-10-27 落盘流程有界化

- 运行快照默认从普通全量 JSON/停止式保留改为内容寻址 gzip/64 轮 rolling；显式 `plain` 或 `stop` 仍可用于兼容诊断，但不再是普通入口默认值。
- 逻辑证据不合并：before、每个执行项 after、队列终态 after 和执行回执仍可逐项审计。物理层按 SHA-256 共享不变字段和重复终态，所以“逻辑不缺、引用不漏、内容不重复”。
- rolling 已覆盖实际写出的 dispatch recovery、native save boundary 与 Crop Teacher 文件族。删除过期逻辑文件后重新扫描所有剩余内容寻址清单；只有受管 `_snapshot-blobs/<2hex>/<sha256>.json.gz` 且无任何引用的文件可被回收。任何无效 JSON、畸形清单或缺失引用都会停止块回收并保留现场。
- `artifact-retention-state.json` 以原子替换记录每轮保留与物理回收统计；LiveTrainingLoop 最终报告记录实际 artifact mode、retention mode 与窗口。聚焦回归为 Core `29/29`、Backend `5/5`，工具独立构建零警告零错误；没有启动游戏或生成新的长跑目录。

## 已收口增量 · Golden Walnut v26 收获容量恢复

- v26 `golden-walnut-source-schedule-v26-20261026-023455` 已从全新隔离存档真实推进到 Spring 17（`total_days=16`）：购买 `14` 份花椰菜种子，完成首轮整地/播种/浇水/收获，处理邮件与任务 `13`，取得竹鱼竿并钓到 `(O)129`，再按节日门禁等待后由 Beach 原生返场，开始第二轮防风草生产。第 `173` 次迭代前旧路线、节日和作物闭环均未回退。
- 第 `173` 次真实快照为 Farm、背包 `12/12`、8 格成熟作物；作物批次正确生成 8 项 `executor.harvest_crop`，但普通手持收获无空槽，编译器连续五次失败关闭。跨日后最近钓获字段已经清空，证明 v25 的 exact-catch-only 恢复不足以覆盖 Crop Teacher 持权阶段。
- `economy.ship_items` 候选早已携带 `available_stack`，实际遗漏发生在 `EventCandidate -> PolicyEventCandidatePrediction -> Grandpa clone` 中间契约。该字段现已贯通，并有排名回归防止再次丢失；没有新增候选池或第二套出货实现。
- Crop Teacher 在编译收获批次前检测 fresh 满包状态，先把控制权交给统一容量恢复模式。恢复模式跳过 Master Angler 意图构建，不依赖 `RequireGoldenWalnutSourceScheduleReady`，也不调用策略模型；它只通过 Backend availability、确定性候选物化、`DailyPlanCompiler`、`ActionQueueCompiler`、共享路线和原生运输箱执行器。恢复队列不能结算被中断的收获步骤，完成后必须 fresh 重规划原批次。
- 安全选择只允许上游已判 available、`quantity=1`、`available_stack=1` 的候选；优先 Full Shipment 首次贡献，否则仅接受 `<=50g` 的低价值单件。既有自动出售保护继续排除种子和其他预留资源。learner score/model source 被中和，`formal_training_authorized=false`。
- v26 真实失败快照离线重建出 5 个合法出货候选，确定性选择 `(O)92 x1 / 2g / Full Shipment`；正式编译为 `pending / 2 items / first=move_to_tile / all pending`。未选择 6 个花椰菜、5 个木材或受保护种子。显式续跑会重建并结构比对原计划，源账本保持只读，当前 run 使用派生账本；只有已有后续日界回执的收获才作为已落盘进度继承。定向续跑/账本测试 `16/16`，此前定向测试 `42/42`、相关矩阵 `137/137`、Release solution `0 warning / 0 error`。
- 隐藏静音原生续跑 `crop-teacher-v26-resume-fork-20261026-040111` 已通过：`8 crops / 0 empty / sap x1` -> 原生投递 `(O)92 x1` -> `8 crops / 1 empty / sap x0` -> 同一 commitment 的 8 格收获全部 verified -> `0 crops / 0 empty / parsnip x8`。派生账本为 `8 harvest + 4 day boundary`，源账本保持 `0 + 4`，crop run complete。下一固定动作恢复为 collection Teacher、首次登岛和 Golden Walnut source schedule；该短验收没有新日界存档，不能冒充 source schedule，全局准入数字与 `formal_product_training=false` 不变。

## 当前增量 · Crop Teacher 到 collection Teacher 的原生交接

- 新的显式 continuation 模式只允许从已验证的 Crop Teacher 只读源账本进入，并同时校验 collection requirement inventory、acquisition lowering、Master Angler window index 与 route timing calibration。旧的首日完成快捷退出仅在未请求 continuation 时保留。
- v27c 已真实完成 Crop Teacher 并进入 Master Angler 路线，但首个路线候选失效后的权威同分状态错误落入通用 daily-plan fallback，后者重新派发了 Crop Teacher。该运行已立即停止，未把后续错误路线冒充 collection 证据。
- 现将“可用于 Teacher 标签的唯一权威偏好”与“控制器可从同样正确的选项中任选其一推进环境”分离。同分时 Teacher preference 仍保持 blocked/null；控制队列使用稳定顺序、归零 learner 信号并单独落盘 `stage_one_collection_control_selection.v1`，固定声明 `teacher_preference_label_eligible=false` 和 `formal_training_authorized=false`。除同分外的 authority blocker 会终止有界运行，不再落入作物 handoff。
- v27d 隐藏静音回归已再次完成作物恢复与 8 格收获，随后执行 Master Angler Teacher 路线；fresh 失效后成功生成一条 control-only Backwoods 路线，并连续取得 3 条 verified native action。全过程未再次派发 Crop Teacher，也没有产生伪 Teacher 标签。短测为 `9/10`，唯一未达到 10 的原因是 `12` 次尝试预算包含两次候选失效/replan；因此结果保留为受限 incomplete，而非伪装成功。
- v28 将同一链推进到 `18` 条 verified native action，普通 Teacher、control-only 路线、fresh 失效和重绑均真实运行且没有 Crop Teacher 回退。新的停止点是严格 selection contract 拒绝 Construction Bundle 的两个 `99` 木材槽同时认领同一条木匠店连接候选；这不是 Agent 汇报错误，而是会制造伪 Teacher 信用的真实数据建模缺陷。
- 修复统一为一次执行在同一 selection group 最多取得一个信用，跨独立目标的精确信用仍保留；route connector 只形成 `authoritative_collection_rolling_route_step`，不再冒充 acquisition endpoint；实际 acquisition endpoint 的收据只要求本次候选数量，而不是整个长期需求数量。v28 第 `25` 次真实工件已离线重放通过：木材候选只绑定 Crafts Room alternative `0`，其 Full Shipment 与 Community Center 信用均为路线进度，Teacher 继续因四个权威同分候选而阻塞且不输出标签/队列。current collection 全链自测通过，全解决方案 Release 为 `0 warning / 0 error`。
- v29b 把 Spring 7 基线存档误配给 Spring 13--17 账本，严格校验在执行前拒绝；该失败没有动作或训练数据。恢复循环现于首张真实快照执行一次性兼容性门，不再把同一错配重复记作五次运行错误，Planner 原有失败关闭不变。
- 正确使用 v26 Spring 17 隔离存档后，v29c `golden-walnut-collection-v29c-20261026-053000` 已通过：`31` 次迭代取得 `24/24` verified action、`24` 行执行数据、`2` 次 Teacher 队列和 `5` 次 control-only 队列；第 `25` 次和第 `26` 次均成功，旧 malformed group 没有复现，日志零 error。Crop Teacher 派生账本保持 `8 harvest + 4 day boundary`，源账本只读。
- day-zero v30 在 `14` 条 verified collection route action 后得到精确的 `blocked_no_current_collection_candidate / unified_current_collection_candidate_membership_is_empty`，旧循环因无条件终止没有进入共享日计划。新增 `StageOneCollectionFallbackPolicy` 只允许 `RequireGoldenWalnutSourceScheduleReady=true` 与该精确状态/原因组合让权；普通 collection continuation 和任何其他 authority blocker 保持失败关闭。
- 隐藏静音新档 v30b 已原生验收让权：第 `16` 次记录 `stage_one_collection_teacher_yielded`，随后共享日计划完成第 `15` 个 verified action 并产生 `1` 条策略轨迹；第 `17` 次派发既有 Crop Teacher 的 `84` 项原生计划，第 `18` 次继续 verified。诊断达到该退出条件后人工停止，三端及端口已回收；它不是 source schedule。定向测试 `6/6`，隔离 Release solution `0 warning / 0 error`。
- v31 已从新的 day-zero 隔离存档取得 `179` 条 verified action，真实完成两轮 Crop Teacher、节日等待、满包收获恢复和一次 `(O)131` 捕获；人工停止前零 `stage=error`。停止点为 Spring 17 09:10、背包 `12/12` 时 `(O)145` 在 Forest/Town 间往返：透明鱼规则完整，本地抛竿因满包正确阻塞，但跨地图路线遗漏了同一终端容量门。
- Master Angler 跨地图路线现先检查透明背包容量，满包时由既有 Stage One 安全单件出货恢复接管。v31 原第 `194` 次排名/快照离线重放选中合法出货；v31c 原生续跑从只读源存档的哈希一致副本验证 `12/12 -> (O)390 原生移除 -> 11/12 -> Master Angler 重新规划`，不再换图振荡。v31c 因误开 day-zero-only 证据归档门而在已执行动作的归档阶段停止，不能冒充完整 source schedule。定向测试 `7/7`，隔离 Release solution `0 warning / 0 error`。
- v32 `golden-walnut-source-schedule-v32-20261026-231505` 从新档取得 `75` 条 verified action 和 `7` 条日界后，真实暴露补水合同断链：Teacher 已证明并准备通道，但 run plan 没有保存可触发原生补水的终端接近格，水量归零后 `executor.water_crop` 只能失败为 `no_reachable_water_source`。
- 修复将 `watering_refill_approach_tile` 贯穿 Teacher decision/receipt/run plan，并从种植格中保留；控制层按实时 `water_left` 只编译当前水量可覆盖的浇水前缀，归零后把保存格交给既有 `move_to_tile + BFS + route repair`，抵达后仍由既有 `executor.water_crop` 原生补水并完成剩余地块。没有新增平行移动、清障或浇水系统；resume 包装器也恢复了原生 save-boundary 参数。
- v33 首轮新档到 `53` 条 verified action 后主动停在 `6` 格待浇、`4` 水边界，以便加入部分批次约束。续跑 `crop-teacher-refill-v33b-20261027-0023` 已依次验证 `4 次浇水 -> move_to_tile Farm(72,27) -> 2 次浇水`，最终跨到 Spring 13，得到 `45` 条 verified action、`12` 条日界、`6` 条收获回执、`0` 条错误并令 `crop_teacher_run_completed=true`。本轮没有策略标签，不能冒充正式训练或 Golden Walnut source schedule。
- 下一固定动作是从新的 day-zero 隔离存档继续完整长跑，取得首次登岛、至少一个 Golden Walnut 来源增量和 First Parrot/Turtle/House 三项原生跃迁并导出 source schedule；v31/v31c 均未启动正式训练。

## 已收口增量 · Golden Walnut v24 清障成本修复

- v22 已把节日等待后的角色送回 Farm 并生成场地接近队列，但长距离 route repair 没有读取全地图 blocker 身份；第一轮修复接通现有 `locations.route_blockers` 后，v22 原失败输入可以重编译为 pending。该结果只证明旧输入已可编译。
- 全新 v23 `golden-walnut-source-schedule-v23-20261025-233534` 已真实取得 `137` 个 verified native action、`12` 条原生日界和 `6` 条逐格收获回执。首轮作物 run 的接近、整地、播种、浇水、收获与结算全部完成；随后完成竹鱼竿邮件/任务路线、两次钓鱼、节日等待和 Beach -> Town -> BusStop -> Farm 返场。
- 第二轮作物接近在第 145--149 次迭代暴露成本盲区：同为三次清障时，旧贪心选择近处的 `(73,23)` 成熟枫树而不是 `(73,24)` 杂草。透明字段给出该树需要 starting axe `15` 次挥砍、`30` 点体力；旧 route repair 同时写死 `max_tool_swings=8`，故编译阻塞并按连续错误预算停止。
- 当前修复仍只使用一套 `move_to_tile + BFS + executor.clear_obstacle`。选择器在 `max_route_repair_clears` 内按每格透明清障分钟和体力执行有界最小成本搜索，编译项携带每个障碍的精确挥砍上限、分钟与能量；全地图树若没有精确成本则失败关闭。v23 原输入已由 Backend 严格重放为 `pending / 7 items / 0 blocked`，第三个清障从成熟树变为杂草，最终接近点不变。
- 验证：新增回归后 `ActionQueueCompilerTests 177/177`；Core 全量 `2959 passed / 21` 个既有基线失败；隔离 Release solution 构建 `0 warning / 0 error`。v23 仍为 incomplete，不能冒充修复后的原生清障证据。
- 全新 v24 `golden-walnut-source-schedule-v24-20261026-003430` 已关闭原生退出条件。它从 day-zero 新档运行到第二轮作物 handoff，在有界停止前累计 `145` 个 verified action、`12` 条日界和 `6` 条收获回执。第 144 次队列选择并原生完成两格杂草 `(50,27)`、`(71,27)`，而非成熟树；整条 `move/clear/move/clear/move` 队列为 `5/5 applied + primitive verified + fresh`。第 145--150 次继续完成 `till/move/till/move/clear/till`，每项均 `1/1 applied + verified`，旧编译错误未复现。
- v24 在上述有界验收点人工停止，不是 source schedule 或正式训练。对其中的 `trajectory_rejected:InvalidOperationException` 逐项重放后确认：`recovery.stabilize_day` 属于校准专用控制动作，`quest.advance` 属于证据专用聚合动作，二者按能力注册表都不进入策略训练；因此不存在应补写的睡眠/任务 Teacher trajectory。LiveTrainingLoop 已在 builder 前复用统一准入过滤器，将两类成功执行记为 `policy_training_option_not_admitted`，继续保留执行证据且不污染策略数据集。真实 v24 第 `88`、`93`、`94`、`95` 次制品离线调用全部得到 `0 appended / 1 skipped` 和该精确原因码，聚焦回归 `11/11`。下一动作恢复 Golden Walnut 首次登岛主线。

## 当前增量 · Golden Walnut 长跑相邻访问清障

- `golden-walnut-source-schedule-v17-20261025-203000` 在第 47 次迭代真实失败于 `till_soil_no_adjacent_route_stand_tile`。两个 planting tile 本身可耕，但周围可移除障碍让执行器没有合法相邻站位；旧 planner 只清目标格，因而正确失败关闭却无法推进。
- 场地准备现复用同一候选与执行器，在阻塞原因**仅为**无相邻路线站位时，确定性选择相邻 `executor.clear_obstacle` 或 `executor.break_farm_resource_clump`，状态标记为 `clear_access_then_fresh_snapshot_rebind`。执行后仍须 fresh 重绑；缺工具、体力不足、超时或其他阻塞不能借此放行。专用 snapshot bounds 在仍不超过 `32x32` 时只扩一格，以覆盖外围站位和障碍。
- v17 原第 47 次真实请求已用当前源码重建为 ready：`blocked_count=0`、`clear_then_rebind_count=2`、`access_clear_then_rebind_count=2`，精确选择 `(70,19)` 石头和 `(71,21)` 杂草。该证据证明编译修复覆盖原失败输入，但不是一次新的原生执行。
- 全新修正版长跑 `golden-walnut-source-schedule-v17b-20261025-181200` 的随机布局走直接耕地分支，最终真实完成六格整地、播种、逐日浇水、六格收获和作物 run 结算，形成 `94` 个 verified episode、`12` 个日界与 `6` 条收获回执。随后统一 Stage 1 Teacher 完成 `spring_2_1` 邮件及任务 `13` 的三段 Beach 路线，确认作物 Teacher 能正常退场。
- v17b 的第 100 次迭代在事件 `739330` 处失败：候选本身为 `story.advance_event / ready_now`，却被 planning-handoff 空绑定过滤为 `empty_action_list`。队列构建现对原生剧情 interrupt 使用独立的精确过滤，只接纳 `story.advance_event`，不应用战略 goal binding；普通 handoff 仍失败关闭。全解决方案构建 `0 warning / 0 error`、聚焦回归 `64/64`。v17b 保留为 incomplete，不得续接其未保存尾部。当前下一退出条件不变：从新隔离存档重跑，直到首次登岛、至少一个 Golden Walnut 来源增量和 First Parrot/Turtle/House 三项原生跃迁齐备，再由既有 builder 导出 source schedule。
- v18 全新隔离长跑再次结算作物 run，并真实通过事件 `739330`：原生剧情回执为 `applied/verified`，透明库存出现竹鱼竿，随后执行两次 Beach 钓鱼候选。该 run 在新作物 handoff 的 Beach→Farm 首段暴露附带系统提示关闭缺口：连接器走到 `(38,0)` 后，无角色普通 `DialogueBox` 仍在逐字显示，旧移动器过快耗尽 `16` 次点击，下一轮 route 因活动菜单而失败关闭。
- 修复复用同一 `executor.close_menu` 与现有连接器。移动器改为按 `dialogueFinished/showTyping/transitioning` 状态变化节流，最多 `60` 次输入、`600` tick；仅当透明字段同时证明非事件、非问题、零响应、非睡眠且无角色/空 speaker 时，Teacher 才先编译 `safe_dialogue_recovery`，并禁止该中断结算原作物步骤。v18 原始失败快照已重放为单项 pending close-menu 队列，聚焦 `17/17` 与全构建 `0 warning / 0 error` 通过。v18 仍为 incomplete；下一固定动作是全新 v19 实跑，取得关闭提示并继续跨图的原生回执后，再继续原 Golden Walnut 退出条件。
- v19 已取得两条真实钓鱼库存增量：两个 `fishing.catch_fish` 回执分别新增 `(O)131`，第二条使背包达到 `12/12`；不能再把该段归类为 collection Teacher 假成功。菜单恢复也确实关闭了系统提示，但 Spring 13 07:10 的 Beach→Town 普通 warp 每次都会重新触发同一原生提示并把玩家恢复到 `(38,0)`。
- 反编译确认这是 `Game1.warpFarmer` 的 active-festival 分支：`weatherIcon == 1`、目标为 `whereIsTodaysFest`，节日前拒绝并弹提示，节日时段进入事件而非普通通行。现由唯一 `ActiveFestivalTransitReader` 同时服务图腾透明读、普通 warp 门禁和未来路线 Teacher；普通路线在节日结束前不可作为普通 transit，当前执行器会在派发前失败关闭，Future Route Teacher 则按 `end + 10 minutes` 重新计算同日最早抵达。没有增加第二套路由器或日期/地点特判。
- `DialogueBox.getCurrentString()` 已按反编译的只读方法体加入 `menu_specific_state.dialogue.current_text`，用于区分重复系统提示；该读取不点击、不推进、不关闭菜单。透明桥构建通过，精确回归 `5/5`。下一固定动作是全新 v20 有界运行，取得“上游避开/延后节日普通 warp”及随后正常跨图的原生回执；通过后继续首次登岛与 Golden Walnut source schedule，不在此处扩张动作集合。
- v20 `golden-walnut-source-schedule-v20-20261025-204818` 已从全新 Spring 1 隔离存档真实走到 Spring 13：作物 Teacher 完成 `12` 条原生日界、`6` 格收获和零重派发结算，随后完成邮件、任务 `13` 的三段路线并执行一条 Beach 钓鱼回执。旧提示循环没有复现；新增普通 warp 节日门禁在作物 Teacher 回退返场时正确阻止 Beach→Town。新的精确失败是跨地图 `move_to_tile` 在“路径存在但当前节日暂时关闭”时没有生成等待，而是保留普通移动；Teacher 路由适配只接受 connector，于第 102–106 次迭代连续报 `crop_teacher_route_compile_invalid` 并停止。v20 固定为 incomplete，不得续接为 Golden Walnut 耐久证据。
- 跨地图编译器现只对可证明同日恢复的 `ordinary_warp_active_festival` 生成有界 `executor.wait_ticks`：每轮最多 `600` tick，保留原目标、开放时间、首段 connector 身份和 fresh-replan 要求；显式 connector 请求在关闭时仍 blocked，永久/未知门禁不变。作物 Teacher 路由校验只接受原 `executor.traverse_connector` 或带精确 festival route-wait reason 的这一等待项。聚焦回归 `203/203`、全解决方案构建 `0 warning / 0 error`。下一固定动作是从 Spring 1 新建完整 v21 耐久运行，验收等待后正常跨图，再继续首次登岛和 Golden Walnut source schedule。
- v21 `golden-walnut-source-schedule-v21-20261025-213726` 已真实完成该验收：`140` 个 verified action、`12` 条日界、`6` 条收获回执；第 100–142 次在 Beach 按 exact 14:10 门禁有界等待，第 143–145 次自动切换为 Beach -> Town -> BusStop -> Farm 三段原生 connector。旧编译错误和提示循环均未复现。
- 抵达 Farm 后出现的是独立的场地接近缺口，不是节日路线回退。角色在南入口 `(40,64)`，新布局在 `(69..73,16..25)`；旧场地准备在尚未接近布局时直接计算局部动作可达性，连续 5 次以 `till_soil_no_adjacent_route_stand_tile` / `clear_obstacle_no_adjacent_route_stand_tile` 失败关闭。v21 因该真实间隙固定为 incomplete。
- 唯一 Teacher 链现增加“接近后准备”的前置编译阶段：从同一 bounded Farm 投影选择原生可站立的相邻格，优先避开所有种植与补水准备目标，再交给既有 `move_to_tile + BFS route repair`，最多 4 个既有清障并 fresh replan；没有作物专用寻路器。v21 第 146 次失败快照确定性重建选中 `(69,26)`，原 state hash 由后端严格接纳并编译为 pending `executor.move_to_tile Farm(69,26)`。下一固定动作是全新 v22：取得原生接近、继续场地准备和无间隙 manifest 后，才继续首次登岛与 Golden Walnut source schedule。

## 当前增量 · 新档开局礼盒透明执行链

- 真实 Spring 1 旧快照只有商店、社交和小游戏候选，缺少领取农舍开局礼盒这一必经前置，导致 Grandpa 最大分数目标只能在低价值候选中选择。现已新增唯一高层项 `rewards.claim_starter_gift`，并贯通透明读取、候选生成、日计划、fresh 编译重绑和 Product Executor 共用的原生运行状态机。
- 透明桥只在当前玩家拥有的 `FarmHouse` 中发布唯一 `giftbox + starterGift` Chest，绑定精确 tile/runtime/item 身份、实时 contents、背包容量、相邻站位和农场类型对应任务（普通农场 `6`、Meadowlands `132`）。运行时只调用原生 `GameLocation.checkAction`，由 `Chest.dumpContents` 完成任务、背包/举起物品和空礼盒移除；禁止直接改 Chest、背包或任务。
- 该项已进入 `earn_money` 的 fresh-save 支撑转移。隐藏静音原生运行 `runtime-starter-gift-20261024-172156` 在真实隔离新档中通过：领取前后 full snapshot 哈希分别为 `e31c45f8cb50c9a9576cab27a5769faf6f193bfffdc80e3a2465c29d8369204f` 与 `eed1329a1bee2d239da5e35961ede172e865f3e0b7b99987dc087e1df608bb71`，405 tick 后 `(O)472 x15`、任务 `6` 和空礼盒移除均由原生回执验证。`EVD-342` 已将本项提升为 five-gate / training-eligible；目录预期为 `241 registered / 162 five-gate / 67 training allowlist`，以本轮生成报告为最终事实源。
- 首次原生运行同时暴露新档淡入时控制门未开放和失败回执写死后状态两处硬伤。执行器现在在 paused/global-fade/freeze/warp 门关闭时只等待且不累计 stuck，故事事件会显式失败关闭；无论成功或失败，回执均从当前 Chest 与任务读取真实 after 状态。
- 领取后真实快照已重放。通用售卖/出货现在对 `player.seed_inventory` 的精确槽位与 qualified ID 双重保护可种种子；透明桥的新快照也直接发布同一保护原因。真实旧快照回放中，种子售卖/出货可用候选为 `0`。
- 新地块布局不属于 `farm.maintain_crops` 的机械职责。Grandpa 解析器现于“赚钱方向优先、已有可种种子、农场无作物、没有精确 ready 赚钱候选”时发出 `planning_handoff_required`，携带 `crop_production_teacher`、`farm_layout` 和 `establish_initial_crop_production`；LiveTrainingLoop 在该状态下禁止把完整候选池交给日计划，因此不会再回退到社交。按需布局采集、自动 Teacher 调度和首日 continuation 均已接通，直接复用现有 decision、ledger、obligation plan、native run plan builder 与动作编译执行链。真实隐藏静音运行 `runtime-crop-teacher-first-day-20261024-183952` 已通过：真实 Bridge 的 `farm_site_preparation_context.v1` 嵌套 `bounds` 契约被用于 80×65 Farm 的 9 分块采集；随后完成 6 单位购种、场地准备、6 格播种、6 格浇水、回家和原生睡眠。49 次控制尝试产生 47 次已验证执行，跨日与存档哈希变化均由既有 durable save boundary 验证。现已把该 continuation 泛化为唯一全日期 `CropTeacherRunPlanner`，并把 `executor.harvest_crop` 接回同一机械批次编译链；持久化 transition ledger 逐地块记录已验证原生收获，部分成功可恢复、重复执行被抑制。每个非终端 run day 还必须持有独立的日期加一和存档 SHA-256 变化回执，回执写入前循环只等待落盘，不执行下一天。次日仅保留 5 株、计划日未成熟、错误种子和无回执缺株现在只对明确的战略漂移触发最多 8 次自动 Teacher 重派发；重派发复用同一 capture/dispatch helper，旧 plan/ledger 保留作 lineage，存档边界缺证不会被误分类。完整周期 `runtime-crop-teacher-full-cycle-ledger-fix-20261024-211500` 已修正并验证水量守恒：无补水路线时 Teacher 只选择水壶剩余量可覆盖的 `3` 格花椰菜（`36/40` 次），从 Spring 1 连续运行到 Spring 13，写入 `12` 条存档边界和 `3` 条逐格原生收获回执；收获批次绑定来自实际 `effective_queue_item`，账本重放幂等，运行零重派发完成。作物生产交接退出条件已达到，不新增第二套清障、耕地、播种、浇水或收获实现；下一步恢复 `complete_master_angler` 的 Golden Walnut 连续新档证据链。
- 06:00 商店门已在共享 `ShopObjectiveStageCandidates` 路线层修正：跨图候选现在同时合并第一段 connector 与最终商店服务的 `AllowedNow/AllowedToday`、最晚开门、最早关门和等待成本。商店尚未开门时保留“今日稍后可用”，但不得进入 ready-now 或 Grandpa 当前方向绑定；排序器将其延后，不再先走到闭门商店。该修复同时覆盖购买、售卖、动物购买和蟹笼补给，没有新增分支实现。精确受影响回归 `88/88` 通过；一次宽筛另暴露 4 个与本改动无关的既有 fixture 失败（清障 2、特别订单候选身份 1、浇水耗时旧期望 1），未混入本切片。

## 当前增量 · Master Angler 原生竹鱼竿前置闭环

- 当前权威来源是原版 Spring 2 事件链，不是先去 FishShop 购买。鱼竿为空时，统一 Stage One Teacher 依次消费 `spring_2_1` 邮件、基础任务 `13`、实时任务路线和事件 `739330`；高层出口仍是既有 `mail.process_letter`、`quest.advance`、`story.advance_event`，机械层仍只使用共享路线、邮箱/菜单和原生剧情执行器。FishShop 购买只保留恢复兜底，不与原生首发链形成第二套系统。
- `quest.advance` 的路线 provenance 现在会穿过 `DailyPlanCompiler` 保留到机械 connector，回执可逐段验证任务 ID、运行时类型、动作、目标地点、事件、邮件和权威资源键。它仍不在全局策略训练 allowlist；三段路线成功只生成 `verified_evidence_only_policy_option_not_admitted`，不会把局部支撑动作冒充正式策略标签。
- 原生事件先前会在 Teacher 快照与产品派发之间自行推进。根因是 `STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE=true` 仍要求 `Game1.player.CanMove`，而剧情期间该值为 false。Runtime Harness 现只对非节日、未跳过、非玩家控制、无小游戏且可由剧情执行器接管的事件边界追加空闲持有；请求在队列中时继续暂停，`activeStoryEvent` 取得执行租约后恢复。原有事件 ID、位置、命令索引、命令原文和决策响应校验保持严格。
- 隐藏、静音、隔离实跑 `rod-native-chain-v19j-20261025-193000` 从开局基线在同一进程连续完成 `9` 集：邮件 `5`、任务路线 `3`、原生事件 `1`。第 9 集 Teacher 与 Product fresh-before 均为状态哈希 `66afb8fc504b60a584dde33e6cc9f5599185dc3700583e9fff232e004cc1474b`，`product_request_state_hash_drift=false`，事件执行为 `applied/verified`；after snapshot 的 `fishing.rod_inventory` 精确为 `(T)BambooPole / BambooPole / 竹鱼竿`。摘要为 `passed`、`6` 条 Teacher admitted、`3` 条 evidence-only、规范数据集 `6 accepted / 0 rejected`，并按透明库存退出，没有启动正式训练。
- 该切片不提升 19 criterion、11 root method 或 `formal_product_training_authorized`。下一固定施工项是复用现有长跑控制面取得首次登岛、Golden Walnut 来源增量及 First Parrot/Turtle/House 三项原生跃迁，再导出 source schedule 并重建 Master Angler audit。

## 当前增量 · Golden Walnut 新档证据归档

- LiveTrainingLoop 新增显式 `--capture-golden-walnut-fresh-save-evidence`。该模式必须同时提供稳定 `--run-id`、最大 Grandpa 分数目标和 `--snapshot-artifact-mode content_addressed_gzip`；默认训练与普通 smoke 不受影响。
- 第一条 success + fresh episode 在任何耐久写入前必须通过 `initial_boundary`：`full` 快照、day `0`、同一 save/run/state/tick、姜岛与三项鹦鹉状态均未推进，且 `69 keys / 130 walnuts` 权威目录逐行完整、全部 `collected=0`。之后前后快照的内容寻址清单与重写引用的 episode 以原子顺序写到同一 `live-snapshots` 顶层，文件名不会被 rolling retention 正则删除，且继续复用原 `_snapshot-blobs`。没有逐帧扫描，也不会每轮重写不断增长的 JSON 数组。
- 正常退出时，循环在正式训练事务提交**之前**一次扫描耐久 episode 并生成 `golden-walnut-fresh-save-episode-manifest.json`；报告记录捕获开关、manifest 路径与 episode 数。若进程异常退出，可用 `build-golden-walnut-fresh-save-episode-manifest --episode-directory <run>/live-snapshots --schedule-id <id> --run-id <id> --output <path>` 从耐久别名重建，不依赖已被滚动删除的临时文件。
- schedule 读取现同时支持普通 JSON 与 `content_addressed_gzip`，证据根哈希绑定物化后的逻辑 JSON。滚动清理回归证明原 `plan-execution-episode-*` 全部删除后，4 个耐久 episode 仍可重建完整测试时间线；相关定向测试 `16/16`、Bootstrap 构建 `0 warning / 0 error`。
- `Invoke-GoldenWalnutFreshSaveCalibration.ps1` 现只编排两个既有入口：先用 `Invoke-RuntimeFreshSaveSmoke.ps1` 生成并核验原生春 1 存档，再交给 `Invoke-ProductBootstrapCalibration.ps1` 复用现有 Backend、Product Executor、日计划和 LiveTrainingLoop。该路径固定 `skip-training`、`full`、`content_addressed_gzip`、rolling retention 与最大 Grandpa 分数目标；策略校准仍要求完整多候选 trajectory，核桃证据校准则以 verified episode 和耐久 manifest 为退出条件，两种准入不混用。
- 真实隐藏静音运行 `golden-walnut-fresh-save-smoke-20261024-161207` 已生成 `1` 条 verified success+fresh episode、day-0 initial boundary 和 `golden-walnut-fresh-save-episode-manifest.json`。首次运行暴露顺序队列聚合回执把原始 `before_game_tick` 与最后机械步骤的 `effective_before_snapshot_path` 混在一起；聚合层现统一把 state hash、snapshot path 和 tick 重绑到整条 episode 起点，未放宽验证器。修正后报告为 `rows_appended=1`、`verified_actions=1`、manifest episode `1`，定向回归 `23/23`、LiveTrainingLoop Release `0 warning / 0 error`。
- 这只证明真实新档采集入口和首条 episode 的端到端链路，不是连续新档日程，更不是正式训练。首次登岛、三次鹦鹉跃迁、51 核桃来源变化、Gourmand 第 2/3 单及后续 Master Angler prerequisite audit 仍未生成，`formal_product_training_authorized=false` 不变。下一固定切片是在磁盘容量充足的训练位置延长同一新档运行，而不是再增加启动器。
- 首轮排序证据显示捕获脚本使用的 `grandpa.maximum_21` 只通过了捕获校验，却被日目标解析器判为 `not_applicable`，导致 62 个候选全为 `-0.034` 并按 candidate ID 先选 AnimalShop。现已由 `GrandpaEvaluationGoalDefinition` 统一识别权威 ID、战略别名和证据别名，脚本改传权威 ID；状态为 `resolved` 时，LiveTrainingLoop 在编译边界仅保留解析器的精确 `bound_candidate_ids`，不篡改全量排序、分数或结构化策略的绑定内顺序。对同一真实 Spring 1 快照离线回放得到 `raise_friendships / social / 28` 个绑定候选，首个计划动作变为社交路线 connector，不再是无目标商店访问。
- `initial-snapshot.json` 过去经 PowerShell 对象往返重写后仍携带桥接旧哈希，不能被当前 Backend 重放。校准脚本现用 `Invoke-WebRequest` 保留原始 JSON，并以无 BOM UTF-8 直接落盘；`summary.json` 等普通对象文件仍使用结构化写入。该修复只保证后续运行可复现，不追改已有证据。目标别名、绑定过滤、脚本保真与既有 fresh-save 守卫的定向回归共 `164/164` 通过；未启动游戏。下一步仍是在有足够磁盘余量的位置延长真实新档 episode 链，而不是增加候选硬编码。
- 连续链入口已修正为显式 bounded target：Golden Walnut 包装器默认 `RequiredVerifiedActions=4 / MaxAttempts=8`，并把参数传入原 Product Bootstrap；捕获模式达标后强制复用 LiveTrainingLoop 的 native save boundary，按 `SaveSlot` 核验跨日和存档指纹变化。最终验收同时要求 loop report 的 requested/verified 数、耐久 episode 数及 boundary required/verified 全部一致。普通非 Golden Walnut 校准保持默认单动作且不强制边界；该离线门已经由下述 v6 真实运行验证。
- 隐藏静音运行 `golden-walnut-starter-fix-v6-20261024-2140` 已把上述 bounded target 跑成真实连续链：`82` 次 verified Product 执行、`82` 个耐久 episode、`12` 条原生日界/存档回执、`3` 条原生收获回执、作物 Teacher 零重派发完成，最终 manifest 接纳 `82/82`。第一条策略轨迹为 `rewards.claim_starter_gift`；后续执行均保持机械校准角色，没有把 Teacher 展开步骤伪装成新的策略选择。
- 该运行先暴露、后关闭两项运行基础设施问题。开局礼盒候选过去没有写 `AllowedNow/AllowedToday`，会被 Grandpa 绑定层丢弃并回退到社交；现在 ready 礼盒稳定绑定 `earn_money`。另有旧 `dotnet` 进程锁住共享 Release 输出，校准入口现把三个可执行项目发布到运行目录内的独立 artifacts/output；失败日志也随运行保存。磁盘门默认仍为 `8192 MiB`，只有显式参数才能为受控短跑降低，并写入摘要。
- manifest 不再要求相邻 episode 的完整状态哈希逐字相等；只允许同 save/player/day/time、最多 `4` 个 tick 且姜岛访问与完整核桃来源目录完全不变的被动观察间隙。真实场地准备又暴露 `farm_layout` 之前没有 `world_progress`，现已在保持 `32x32` 有界地图的同时加入该证据域；验证器本身没有降级。正式 schedule 导出已在这份 manifest 上运行，并准确拒绝：`Fresh-save episodes do not contain the native first-island-arrival transition.`
- Golden Walnut 长期目标控制面现已落地。显式 `--require-golden-walnut-source-schedule-ready` 只有在 `full + content_addressed_gzip` 证据捕获、日计划与原生存档边界同时启用时才可启动；普通 bounded calibration 默认行为不变。循环从每个 fresh before/after snapshot 严格验证 `game-1.6.15`、同一可读存档、`69 keys / 130 walnuts` 目录身份与逐行原生数量、至少一个真实来源增量，以及首次登岛、First Parrot、Western Turtle、Island House 当前原生终态。作物 Teacher 第一日完成不再耗尽整个长期目标预算，每个有界 handoff 收口后回到现有 Grandpa/日计划重算；若最后一个预算内动作恰好完成里程碑，也会直接读取其 fresh after snapshot，不会先误判 attempt 用尽。`total_days >= 224`、attempt、磁盘余量或连续错误任一先到均失败关闭。里程碑只决定循环何时停止；正常退出仍必须由既有 episode schedule builder 从同一 manifest 重新证明首次登岛和三次鹦鹉均发生过 `false -> true`，验证成功后才写 `golden-walnut-fresh-save-source-schedule.json`，否则整轮报告为 incomplete。长跑包装器现可把原生存档/游戏运行目录与证据输出目录分开；默认路径完全不变，只在空间门要求时显式把大体积证据放到另一驱动器。专项回归通过，尚未启动长期游戏运行。下一固定步骤是在磁盘余量满足 `MinFreeSpaceMb` 后执行一次受限长期新档运行并导出 schedule，再重建 Master Angler prerequisite audit；其后采集 Gourmand 第 2、3 单连续原生回执。
- 首次正式受限长跑 `golden-walnut-source-schedule-v1-20261024-222622` 已实际启动：游戏、原生存档和运行目录留在 E 盘，耐久证据写入 I 盘。它完成两轮作物 Teacher 并推进到 Spring 17（`total_days=16`），随后在仍持有 `(O)472 x14` 时被错误拒绝；根因是 Teacher 把水壶当前 `3/40` 的余水当成了整个生长周期总供给，而运行时早已具备 `CanRefillWateringCanOnTile + BFS` 的原生补水链。修正后的 `farm_site_preparation_context.v2` 在九块完整农场快照中逐格发布原生补水源和可站立事实，Teacher 复用共享 `RouteGridPathfinder` 证明补水连通后按单日水壶容量预算；无补水证据仍按旧规则失败关闭。长跑的五次失败尝试还正确触发 episode 连续性拒绝，但 manifest 构建异常过去会越过报告退出；现在改为稳定的 `golden_walnut_fresh_save_manifest_invalid` 未完成结果。该旧证据链已经存在不可修补间隙，禁止续接或放宽验证器；下一步必须用修正版从新的隔离新档重跑。尚未产生 source schedule，`formal_product_training_authorized=false` 不变。
- v2 隔离诊断已纠正“程序卡在购种”的误判：物化后的 content-addressed 快照从 06:00 连续推进至 07:40，`continuation.quantity=1` 是既定的单份 fresh 购买切片，当前动作只是在等待 Pierre 09:00 开门。完整农场布局显示作物区到补水源的最优路线为 18 步，只被 `(70,23)` 的一棵树隔断。Teacher 现以最少清障数、再最短路的确定性 Dijkstra 生成最多 4 个 `watering_refill_preparation_tiles`；承诺、receipt、run-plan request 与 site preparation 均保持该集合，机械作物批次仍只消费 planting tiles。`executor.clear_obstacle` 复用共享路径与原生工具生命周期，不允许移动器同步改地图，也没有新增平行执行器。
- v3 实跑完成两格补水通道清障/锄地后，播种因旧 `ActiveAdjacentTileAction` 自维护路径状态而触发 `adjacent_action_route_drifted`。相邻动作现统一持有 `ExecutorPathCursor` 并调用 `TryAdvanceExecutorPath`；原生播种、施肥、放置和收获终端逻辑不变。v4 `golden-walnut-source-schedule-v4-20261025-0003` 已真实完成 6 次购种、两格准备清障、`(70,23)` 树木 Axe 清除、清后锄地、`6/6` 播种、`6/6` 首日浇水、原生睡眠/存档边界和次日 `6/6` 浇水。运行随后主动停止，因此不冒充 Golden Walnut source schedule。当前固定下一步为提交该闭环修复后重新启动隔离长期日程，继续首次登岛和 69/130 来源历史。
- v4 的 `55` 个耐久 episode 已在异常退出后由正式 builder 重建为合法 manifest，证明现有前缀未损坏；但停止点位于 Spring 2 浇水后、下一次原生存档边界前，所以禁止把重载存档硬接到该尾部。全新 v5 `golden-walnut-source-schedule-v5-20261025-001624` 随后真实完成礼盒、84 动作 Teacher 派发、6 份购种、返场和场地准备，首批播种却稳定失败为 `adjacent_action_route_drifted`。停止后正式 builder 已从 success-only 耐久别名重建并接纳 `46` 条连续 episode；阻塞播种尾部没有进入 manifest。结构化回执证明普通相邻动作的路径构建仍沿用 `allowRemovableObstacles=true`，会把未编译清障的障碍当成可通行高成本格，而共享执行跟随器随后正确拒绝它。普通相邻动作现固定 `avoidSoftObstacles=true / allowRemovableObstacles=false`；同轮调用审计把浇水、锄地、姜和宠物水碗也改为显式 false，并将 helper 默认值改为 fail-safe false；只有会主动破障的普通矿井/火山战斗显式 true。需清障时仍只允许现有 site-preparation 与 `executor.clear_obstacle` 链处理。源码守卫回归已通过，尚未重新启动游戏验证；下一固定步骤是从全新隔离存档复跑至至少首日播种/浇水和原生存档边界，再继续首次登岛与来源日程。
- v6 `golden-walnut-source-schedule-v6-20261025-004151` 已证明上述 fail-safe 路径不再进入播种穿障，但随机布局的 site-preparation plan 同时含当前可执行步骤和后续不可达步骤时，控制环仍错误要求 `BlockedCount == 0`，在返场后拒绝整个准备阶段。现新增唯一 `CropTeacherSitePreparationFrontier`：先选择现有 plan 的 `direct_till_ready` / `clear_then_fresh_snapshot_rebind` 前沿，每步 fresh rebind；若全部 `already_prepared` 才完成，若未完成且无前沿才保留原原因失败关闭。它不更改 BFS、执行器或动作所有权。v7 `golden-walnut-source-schedule-v7-20261025-005622` 已真实完成场地准备、`6/6` 播种、`6/6` 浇水和 Spring 1→2 原生睡眠/存档边界，关闭 v5/v6 两个失败边界。第二天缺一株来自无稻草人阶段的原生乌鸦损失；六个种植和浇水 item 均有不同目标及原生 verified 回执，既有 bounded Teacher 重派发行为正确。异常停止后 manifest 正式重建并接纳 `57` 条 success-only 连续 episode，仍固定 `training_label_eligible=false`。下一固定步骤是启动全新长期隔离运行，不再扩张作物专用实现，让随机损失经现有重规划收敛后继续首次登岛、核桃来源增量和三项鹦鹉跃迁，最终由既有 builder 导出 source schedule。
- v8 `golden-walnut-source-schedule-v8-20261025-011556` 已越过更多原生日界，并在乌鸦损失后由 Teacher 正确把缺失的 `(O)474` 替换为 `(O)472`；重排计划完成 `9` 个场地准备动作后，在 `Farm(71,20)` 重复锄地。反编译核对证明后台帧的原生 `GetToolLocation()` 会忽略 click target，工具目标必须依赖站位与朝向；旧工具移动在相邻 tile 边缘停下，可能继续命中已处理邻格。当前工具执行已删除手写移动循环，统一复用 `TryAdvanceExecutorPath`，并在出手前只用模拟方向输入居中到站立格；禁止直接修改角色位置。校准脚本现向 runtime 固定传入 `MaxUnverifiedExecutionStreak=8`，同一未验证结果达到上限即失败关闭。v9 `golden-walnut-source-schedule-v9-20261024175054` 有界诊断前缀取得 `73` 个 verified Product actions、`7` 个原生睡眠/存档边界，真实完成购种、返场、连续相邻场地准备、`6/6` 播种浇水及跨日维护，没有 unverified streak 或重复锄地错位。该随机种子没有重现乌鸦损失，且运行在首次登岛前主动停止，因此只关闭后台工具定位边界，不冒充 source schedule；`training_label_eligible=false` 与正式训练授权不变。下一步固定为新的有界 source-schedule 长跑，直接推进首次登岛、核桃来源增量和三项鹦鹉跃迁，不增加作物专用分支。
- v10/v11 将后续停滞定位为剧情事件与活动作物阶段的控制竞争。当前实现把 `priority_story_event` 提升到所有作物阶段之前，中断队列不得写作物 harvest/day-boundary settlement，结束后保留原 commitment 并从 fresh snapshot 继续；无效结构化输入也不再逃逸成未捕获异常。v13 `golden-walnut-source-schedule-v13-20261025-035800` 已取得两轮完整 Teacher run，并在第三轮 Spring 20 Marnie 剧情处真实验证抢占：一次 verified 剧情执行、一次安全 blocked 残留尝试后恢复原 `execute_crop_batch`，完成 7 个机械项和原生睡眠，ledger 仍精确为 `0 harvest / 4 day-boundary`。该运行在验收后主动停止，不冒充 source schedule。
- 长跑空闲阶段现调用既有统一 collection Teacher，而不是继续依赖普通候选池碰运气。它从当前 full snapshot 构建 Master Angler target-date intent，把意图作为 additional candidates 送入现有 baseline ranking，再调用 `CurrentStageOneCollectionTeacherPreferenceBuilder` 和 `TeacherPreferenceQueueLoader`；剧情仍优先，Teacher 非 ready 时回退原日计划。requirement inventory、acquisition lowering、Master Angler window index 与 route timing calibration 四份权威输入均为长期模式必填，缺失即启动失败。真实 v13 快照烟测产生 `35 + 2` 条意图、`297` 个唯一候选和一条 `fishing.catch_fish` pending 队列；同鱼多窗口 candidate ID 碰撞已修复。该链仍为 `skip-training`，下一步是新隔离长跑取得至少一条动态 collection Teacher 原生回执，然后继续首次登岛和核桃 source schedule。
- v14 `golden-walnut-source-schedule-v14-20261025-045000` 已让动态 collection Teacher 在真实 Spring 1 新档连续完成 `FarmHouse -> Farm -> BusStop -> Town -> Beach` 的四段原生 verified 路线，但到达 Beach 后才暴露快照中的 `fishing.rod_inventory` 为空：旧 target-date builder 只校验训练竿兼容性，没有先证明持有任意鱼竿，因而错误发布徒手捕鱼意图并与普通候选往返。现在只有 `rod_inventory` 为可读非空数组时才发布 location/mine 持竿捕鱼意图；缺字段或空数组均失败关闭该类意图，已就绪的 crab pot 与天气支援不受影响。v14 原快照重放结果为 `candidate_count=0 / weather_supply_candidate_count=1`，统一 Teacher 不再选择海滩捕鱼而可继续选择其他 exact collection 候选；完整 Stage One 组合自测通过。四段路线只算 supporting transition，不算 Master Angler 原生捕获终态。
- v15 `golden-walnut-source-schedule-v15-20261025-052000` 已在真实新档证明无鱼竿时不再前往 Beach，并由统一 Teacher 选择 AnimalShop 收集采购候选。开店前每 600 tick 的安全等待能够被 starter gift 原生事件抢占，之后正常进入作物 Teacher；但旧控制面每轮仍重建约 `3.8 MB` 全量 collection ranking。v16 `golden-walnut-source-schedule-v16-20261025-054000` 已复用现有 `SelectedQueueDecisionLease`：首次 Teacher 决策后，后续 fresh snapshot 只做同一候选的 deterministic availability/materialization/compile，不再调用策略模型。4 个完成轮次均保持同一候选、同一 queue index 和 `economy_purchase` continuation；全量 Stage One ranking 仅 1 份，3 个已核对续租轮次的 refresh 均为 `policy_model_invoked=false`，约 `10 KB/轮`，避免约 `11.5 MB` 重复 ranking。下一步继续有界新档长跑，先闭合鱼竿前置，再推进首次登岛、核桃 source schedule 与 Gourmand 后续回执；不得把本次控制层优化记为 Master Angler 原生终态或正式训练授权。
- v15 的冻结 ranking 实际已经含唯一 FishShop 竹鱼竿购买路线，但旧 Master Angler frontier 没有把静态 `rod_equipment_acquisition` 生产者投影成当前 Teacher 先决条件，因而排序仍选了 AnimalShop。现新增单一共享协议，严格绑定既有 `economy.buy_supplies`、`FishShop`、`(T)BambooPole x1`、单价上限 `500g` 及 continuation 身份；没有新增模型 option、商店候选、动作编译器或执行器。透明 `rod_inventory` 为空时，这条现有候选以独立 `hard_prerequisite` 优先进入选择；已有任意合法鱼竿时视为满足，缺失/不可用/畸形字段和多个精确匹配均失败关闭。路线、柜台菜单和终端购买分别要求 fresh 地点、FishShop 非只读菜单和竹鱼竿 `0 -> >=1` 回执。该前置与 72 个鱼种 requirement 分开建模，分母仍为 `72`。独立 Release 构建 `0 warning / 0 error`，聚焦回归现直接覆盖路线、FishShop 菜单、`buy_shop_item -> close_menu` 队列和鱼竿库存 `0 -> 1` 三阶段回执，同时覆盖价格上限、已有鱼竿抑制及错误身份拒绝；fresh-save prerequisite 静态审计通过，最新证据位于 `I:\StardewAITraining\rod-prerequisite-focused-20261025-11`。这只关闭“无鱼竿却不会主动买竿”的确定性派发缺口，尚无 v17 原生购买回执，`formal_product_training_authorized=false` 保持不变。下一固定步骤是隐藏静音运行新的有界 v17，先闭合竹鱼竿路线/菜单/购买，再继续首次登岛、source schedule 与 Gourmand 后续回执。

## 当前切片 · 2026-10-21 Full Shipment 冻结并切换 Master Angler

- `money-crop-native-20261021-v10-continuation-v15-full-chain` 已完成既定的第二条有界 Money 连续段：`26` 条 transition、`8` 个实际作物批次、`8` 次单件投箱、checkpoint `6/6 verified`、pending `0`。购种链精确取得 `8` 份 `(O)475`，源存档哈希保持 `9429c6d01d327111af8c81d0c36f2e318ccc4f1b0ea3094d84e19f25308b22e4`，最终状态哈希为 `bb70acbd0b477b7e14d7fe1b4450ee85292fa29e56de854924e4ee67f1216849`。该有界段没有关闭 Money threshold cell，Money 回到复杂度队列第 11 位。
- 旧 current-source corpus 直接重放因 target-date time/energy budget 与当前确定性编译结果漂移而失败关闭。随后使用 `game-1.6.15` 原始字典、当前 goal dependency、authoritative graph 和当前源码重新生成 requirement inventory、acquisition lowering 及三分区 corpus；没有把旧漂移结果伪装成当前证据。
- Pet Love 三个历史原生运行已按当前 authority 严格重放，无需启动游戏或重采。canonical corpus 为 `ready_split_complete_pet_love_teacher_corpus`，包含 `3 rows / 3 partitions`，Teacher comparison 与 native terminal outcome 的 train、validation、test 均完整；SHA-256 为 `99f885037a21a9ae82865cdeec612c61d5bb8018e4701f1d149f8a4738cfb546`。
- 历史 `snapshot.v1` 的 hash 含现已非语义的 `machine_probe_cache_tick`；适配器改为复用共享 `SnapshotHash.MatchesStateHash`，仅兼容当前/legacy 两种严格哈希，不放宽 schema、版本、身份、原生结果或篡改门。完整 Goal Method coverage 自测以退出码 0 通过，并覆盖 legacy 正例、来源替换/碰撞和篡改负例。
- Pet Love reconciliation 已达到 `4/19 executable、5/19 split-complete、3/19 CoverageGateReady、2/11 root ready` 并冻结；随后 Skull Key 三条真实普通矿井终端 episode 按当前 authority 严格重放。train 第 209 天 EVD-106、validation/test 两个独立第 223 天运行分别绑定第 6、31、4 步的 `claim_skull_key`，没有混入骷髅洞、火山或 Quarry Mine。
- Skull Key canonical corpus 为 `ready_split_complete_skull_key_teacher_corpus`、`3 rows / 3 partitions`，Teacher/native train、validation、test 均完整，SHA-256 `ff2afcb27b6e011c3e9261ad1f086f3934cc7710a59adf8f9de021a721df275a`。历史 snapshot hash 通过共享 current-or-legacy matcher；schema、身份、普通矿井 120 层、原生终态和篡改拒绝均未放宽。
- Skull Key reconciliation 已达到 `4/19 executable、6/19 split-complete、4/19 CoverageGateReady、3/11 root ready` 并冻结。随后复核 Full Shipment 发现 dependency graph 节点已经明确 `full_recurrence_required_for_training=false`，但顶层仍保留“必须采完整 fresh-save 154 项 recurrence”的过时 blocker。
- 当前机器证据重新确认 `154/154` 权威静态需求、`641` 条来源、`26` 个执行签名层、静态 blocker `0`；严格原生 index 为 `26/26 verified、0 missing`，共享 shipping/settlement 通过。完整 154 项 recurrence 仍保留给正常游玩与最终可选验收，不是 Teacher coverage 前置。index SHA-256 为 `e30edb0e6aca07deca484c8b8f1a4437eef68cb01120f13074329d7e2e0830b1`。
- 移除自相矛盾的过时 blocker 后，Full Shipment 为 `executable_frontier + coverage_ready`，blocker/next action 均为空。新 reconciliation 为 `5/19 executable、6/19 split-complete、5/19 CoverageGateReady、4/11 root ready`；文件为 `E:\StardewAI-TestLab\artifacts\full-shipment-topology-current-source-20261021\goal-method-coverage-reconciliation.json`，SHA-256 `71f0e4ed0e7928c1e63f39641f4b90153674f154a575ec331342a3d5241adf43`。完整 Goal Method coverage 自测以退出码 0 通过。
- 正式训练仍为 `Money production cells=0/18`、`formal_product_training_authorized=false`。下一步只处理 `complete_master_angler` 的顶层 dependency、日历窗口与 coverage 接线，复用现有钓鱼/蟹笼执行链和已完整的三分区证据。
- Master Angler 的当前日执行链已闭合：72/72 静态窗口会在 fresh snapshot 上重新核对动态查询、地点/路线/计时、可钓 tile、装备与完整 live spawn outcome；蟹笼也已由唯一模型出口 `fishing.collect_crab_pots` 逐次展开收取、装饵、跨图、放置、制作和原生跨日结算。`executor.craft_crab_pot` 已补 EVD-339 原生个人制作菜单回执，`(O)710` 库存 `0 -> 1`、`timesCrafted 20 -> 21`，不再以调度或单测冒充 runtime evidence。刷新后的 option matrix 为 `235 options / 159 runtime-verified / 66 training-eligible`，该 method 的 `option_blockers=[]`，状态从治理阻塞恢复为 `pending_dependency_expansion`。新 `master_angler_fresh_save_prerequisite_audit.v1` 进一步证明 72/72 都有至少两个**静态日历日**，但明确拒绝把它们冒充新档可达机会。钓鱼等级生产者现已严格复用 `dependency.raise_skill_levels.acquire_fishing_xp` 和唯一 `fishing.catch_fish` 出口，按当前缺鱼所需最高等级、原生 XP 阈值、逐次 fresh 回执及 224 日截止重算；删除任一关键图边会重新标记未绑定。
- 普通矿井钓鱼地点生产者现已复用唯一 `mining.reach_depth` 高层出口，而不是增加第二套矿井规划器。透明路线图会把 `Mine` 大厅的原生 `Mine` 地图动作投影为 `UndergroundMine1`；共享 connector 执行器只在当前位置精确为普通矿井大厅、入口层精确为 1 时放行，并仍由游戏原生 `performAction` 完成进入。矿外候选每次只编译一条路线/入口 primitive，然后用 fresh snapshot 重算；进入普通 MineShaft 后继续使用既有楼梯、竖井、战斗、清障与电梯逻辑。普通矿井、Skull Cavern、Volcano Dungeon 和 Quarry Mine 仍严格分离。Master Angler 当前所需普通矿井目标为 floor `20/60/100`，分别覆盖 Ghostfish/Stonefish/White Algae、Ice Pip、Lava Eel/Cave Jelly。该接线关闭 `mine_access_and_depth`、`mine_depth_or_ginger_island_caldera`、`one_of_advanced_location_access` 三个 fresh-save 前置族；阶段审计哈希为 `3374944677ec627995f61d8f4cd1295ff9d80db687d0af653b1b50c49af2b219`，未绑定族从 `12` 降为 `9`。
- 鱼竿装备生产者已按锁定 `game-1.6.15` 的 `Data/Shops FishShop` 与 `Data/Tools BambooPole` 收敛为一个充分硬前置：`(T)BambooPole x1`、价格上限 `500g`。它复用现有 `economy.buy_supplies` 的滚动商店路线、端点交互、单件购买与 fresh 回执；透明预览、实时菜单和运行时执行共同调用唯一 `AuditedShopPurchasePolicy`，只对竹鱼竿放行非 Object 商品，其他鱼竿、工具、家具、配方及带回调商品继续失败关闭。竹鱼竿足以覆盖原生 72 项分母；后续鱼竿、鱼饵和浮漂只作为效率与随机预算优化，不被错误升级为训练硬门。最新审计哈希为 `3c4016ecdadbc5964147d434578dd86c5ce8498adb39ed1cad48938430508e09`，未绑定族从 `9` 降为 `8`。剩余为蟹笼配方/材料/饵料、沙漠巴士、姜岛、Mutant Bug Lair、秘密森林或森林农场、Rusty Key/下水道、天气保留或 Rain Totem、女巫沼泽；最后再组合 seed-robust 随机重试与 Year 3 截止证明。全局指标暂不增加。
- 沙漠巴士前置已在同一共享 `executor.traverse_connector` 路径闭合，没有新增模型动作或第二套路由器。透明桥把 BusStop 票机与 Desert 返回触发器投影为 `native_bus_transport`，上游候选在 `ccVault`、Pam/当日自驾、`BusStop.TicketPrice`、余额、坐骑和忙碌状态不满足时直接排除；运行时仅调用锁定 `game-1.6.15` 的原生 `checkAction`、`DesertBus` touch action 与 `Yes` 对话，并验证地点、到达 tile 和钱款变化。Master Angler recurrence 复用既有 `complete_community_center.native_completion` 充分路线与 `fishing.catch_fish` 高层出口，每次只编译一个巴士 primitive 后 fresh 重绑目标。删依赖边或把 connector 篡改为普通 warp 都会失败关闭。离线编译、3 项定向测试和 fresh-save prerequisite 自测已通过；尚未取得本切片的真实游戏运行回执，因此不把 source-locked 实现写成 runtime evidence。最新审计 SHA-256 为 `40e3df4d946f9dbf09cf70c399f994b94e7c3038b619f76e7108e05fccd73890`，未绑定族从 `8` 降为 `7`：蟹笼配方/材料/饵料、姜岛、Mutant Bug Lair、秘密森林或森林农场、Rusty Key/下水道、天气保留或 Rain Totem、女巫沼泽。全局准入指标仍不增加。
- Rusty Key/下水道前置已组合进 Master Angler，但未把 `obtain_rusty_key` 根的未完成证据抹掉。锁定 `GameLocation.performAction` 的 `EnterSewer` 分支表明：已有 `OpenedSewer` 时一次交互进入；只有 Rusty Key 时首次交互写入该 mail 并显示普通文本，第二次才进入 `Sewer(16,11)`。此前共享 connector 在首次文本后不会重试，现已修成关闭原生文本、确认 mail 回执、仅重置同一 connector 一次，再执行第二次原生交互；无钥匙则在候选层直接排除。依赖图严格引用 `dependency.obtain_rusty_key.has_rusty_key`、其 recurrence/event 关键边和唯一 `fishing.catch_fish` 路由出口，删 Master Angler 目标日期边会失败关闭。定向测试、Runtime Harness/Bootstrap Release 构建和 fresh-save 自测已通过；仍没有新增真实游戏运行回执。最新审计 SHA-256 为 `00042d57ed53321e9b7510aa6dc1d8c5502aa11d16496513ced7146bb4e3f04c`，未绑定族从 `7` 降为 `6`：蟹笼配方/材料/饵料、姜岛、Mutant Bug Lair、秘密森林或森林农场、天气保留或 Rain Totem、女巫沼泽。`obtain_rusty_key` 根本身仍按固定复杂度队列第 6 项完成 60 件捐赠和正式证据；全局指标与训练禁令不变。
- 蟹笼供应族现已在原有生命周期内闭合，没有增加第二个模型动作。隔离的 E 盘存档副本与隐藏静音 full snapshot 证明 FishShop 在钓鱼 `2` 级提供 `(O)685` 鱼饵、单价 `5g`，但尚不出售蟹笼；钓鱼 `3` 级开始提供 `(O)710` 蟹笼、单价 `1500g`，两者均为无限库存且通过既有商店安全策略。`fishing.collect_crab_pots` 现在只在精确缺失蟹笼鱼种的随机容量不足时补设施：能制作则优先复用 `executor.craft_crab_pot`，不能制作才经 FishShop 路线、柜台交互、单件购买、关菜单和 fresh replan 买一个蟹笼；远程空饵蟹笼在背包无任意原生鱼饵时先买一个鱼饵。商店关菜单阶段已补齐 continuation 透传，防止购买成功后丢失原目标。定向回归 `48/48`、Bootstrap Release 构建及防篡改自测通过；本切片的运行时证据只证明实时商店投影，不冒充实际购买回执。最新审计 SHA-256 为 `8cd925d8ede0a0c5d3e5b51817095958ffdd7f5ff100d588400f8dda2f46204b`，未绑定族从 `6` 降为 `5`：姜岛、Mutant Bug Lair、秘密森林或森林农场、天气保留或 Rain Totem、女巫沼泽。`training_label_eligible=false` 与全局准入分子不变。
- 天气族先完成一个不夸大的可复用叶：`dependency.complete_master_angler.consume_owned_rain_totem` 精确绑定已由 EVD-288 隔离原生运行 `4/4` 验证的 `executor.use_rain_totem`，锁定 `(O)681 x1`、location-context 路由、非默认节日、明日最终天气仍为 Rain、原生动画/消耗和 fresh 回执。该 primitive 仍由编译器拥有，不作为策略标签；依赖边只说明“已有图腾可制造明日天气前置并 fresh 重算”。防篡改自测会在有效明日天气改成 Sun 时失败关闭。该阶段天气族状态为 `runtime_consumer_bound_supply_and_scheduler_missing`，审计 SHA-256 为 `7dd897e3cfb2a52a5016c4f342eef003b6e6848f5d12d7d44f46aae2f84468a9`。
- 透明桥随后新增 `player.rain_totem_crafting` 的 full-profile 精确投影：从实时已学配方和锁定 `Data/CraftingRecipes` 读取 `(O)681`，发布原始配方、制作次数、逐槽材料可用性、精确可制作数量、产物容量以及个人背包/Workbench 材料来源。实现把原 Crab Pot 逻辑抽为唯一 `ReadExactSingleOutputCraftingProjection`，两个薄包装共同复用，未形成第二套制作读取。依赖图现以 `dependency.complete_master_angler.rain_totem_crafting_read_authority` 机器化锁定 schema、配方和只读边界，篡改 schema 会失败关闭；天气族状态更新为 `runtime_consumer_and_crafting_read_bound_executor_supply_scheduler_missing`。本切片未启动游戏、未新增原生制作回执，也未接入材料生产或“今晚制作并使用 -> 明日钓鱼”调度，所以缺失族仍为 `5`，最新审计 SHA-256 为 `09d9053c264208f11e95b1080da7b5d4db0958a790aa18d1f2640d76cd4d4748`，训练禁令不变。
- 明日雨窗调度现已闭合到同一个 `fishing.catch_fish` 高层出口。Core 生产器从 fresh snapshot、72/72 哈希锁定窗口索引、精确缺鱼集合、装备、location-context 和 Rain Totem 最终天气投影生成且复验一天期意图；排名 API 以 `additional_candidates` 将当前日捕鱼与明日天气支援在默认候选之外注入，修复旧脚本“先排名、后生成意图”的断链。无图腾时只展开共享原生制作候选，制作后 fresh 重算；已有图腾时直接复用 EVD-288 的 `executor.use_rain_totem`，次日再回到目标日期捕鱼。定向回归 `125/125`、Bootstrap 防篡改审计及 Backend/Bootstrap Release 构建通过。天气族现为 `runtime_consumer_crafting_read_and_scheduler_bound_material_supply_and_native_crafting_receipt_missing`；审计 SHA-256 为 `ee97bff20a4991a8f4db70b6b7cf7e6ad9b1a3749ec89b9a048362c281eb9572`。剩余工作是三种材料生产者与一次原生 Rain Totem 制作回执，未绑定族仍为 `5`，正式训练继续禁止。
- Rain Totem 的材料消费 reservation 与原生制作回执已闭合。候选先保留目标日/location-context 的稳定授权身份，Backend 在日计划编译边界将精确节点、槽位、物品和数量作为一个 portfolio 原子提交；编译器和 dispatch gate 都要求同一 ledger revision 与完整 reservation ID 集合。实时闭环只在原生制作 applied/verified 且 after snapshot fresh 时结算全部三项，任何拒绝或异常都会把 primitive 回执改为 `reservation_settlement_failed`，禁止进入训练。EVD-340 的隐藏静音 E 盘运行实际观察到 `(O)681 0 -> 1`、`timesCrafted 0 -> 1`、原生 `CraftingPage` 材料/产物/任务/成就回调验证全部通过；机械 `executor.craft_rain_totem` 仍不在训练白名单。定向 Core `28/28`、Backend 结算 `17/17`、LiveTrainingLoop 构建零警告零错误。该证据不替代 Axe 升级运行回执、fresh-save 材料可达或 Year 3 截止证明；下一步固定为重建 Master Angler fresh-save prerequisite audit，再按其残余 blocker 收敛截止证明。
- Master Angler fresh-save prerequisite audit 已用当前依赖图重建，并修正了旧审计遗漏的 Rain Totem 觅食 9 级前置。未学配方时，天气支援续程只复用现有 `foraging.chop_wild_tree`：选择一棵当前可达、投影完整且具有正向原生 Foraging XP 的成熟野树，执行一个既有清障 primitive 后 fresh 重算，不增加模型出口。权威图另把 Hardwood reserve、三材料原子 reservation/settlement 与 EVD-340 制作回执拆成独立可篡改节点；任意改动目标等级、工具生命周期或证据 ID 都会重新打开 blocker。天气族现为 `implemented_and_bound`，审计 SHA-256 为 `473be51d47822b9576327c5c6979b2c3b302c5c1828bdd50f7f5cb787e99862d`，未绑定族从 `5` 降为 `4`：姜岛、Mutant Bug Lair、秘密森林或森林农场、女巫沼泽。定向 Rain Totem `8/8` 与审计自测通过；正式训练仍为 false。下一固定切片按复杂度从小到大闭合秘密森林/森林农场替代分支，再处理共享 Dark Talisman 的 Mutant Bug Lair 与女巫沼泽，姜岛最后。
- 秘密森林/森林农场替代分支现已闭合。目标日期生成器先按实时 `farm.farm_type_key` 过滤 `Farm_Forest` 数据地点，并把命中的森林农场窗口映射到运行时 `Farm`；其他布局消费全局 `locations.secret_woods_access`，复用唯一 Axe 升级生命周期，精确清除 Forest `(1,6)` 的原生 `ResourceClump(602,2,2)`，fresh 重算后再走原生 Forest→Woods warp。清除动作继续使用既有 `executor.break_current_location_resource_clump`，没有新增模型出口或第二套工具执行器；其注册与时间假设已泛化为受支持的当前地图 ResourceClump，但 EVD-212 仍只覆盖绿雨 `44/46`，新 602 用途在取得真实运行回执前保持 calibration-only、`training_label_eligible=false`。依赖审计严格锁定森林农场替代、钢斧等级 `2`、原木身份、共享执行器和三条 recurrence 边；篡改钢斧门槛会重新打开 blocker。最新审计 SHA-256 为 `78f868edd55d4b5807ad211afc10289811c86473df056df9992996b7cd4f618e`，未绑定族从 `4` 降为 `3`：姜岛、Mutant Bug Lair、女巫沼泽。下一固定切片是合并处理共享 Dark Talisman 前置的 Mutant Bug Lair 与女巫沼泽，姜岛最后；Year 3 截止组合和 seed-robust 随机重试仍在其后，正式训练继续禁止。
- 共享 Dark Talisman 前置现已同时关闭 Mutant Bug Lair 与 Witch Swamp 两族。透明桥实时读取 Railroad `529952/C`、quest `28`、Krobus 的 Sewer 身份、`krobusUnseal`、BugLand `(31,5)` 唯一 `SpecialItem(6)` 宝箱、Railroad `(54,35)` Buildings tile `287`、`witchStatueGone` 与 WitchWarpCave `(4,5)` 的 `MagicWarp WitchSwamp 20 42`。高层出口仍只有 `fishing.catch_fish`：BugLand 目标在原生 Krobus 交互产生 `krobusUnseal` 后停止前置展开；WitchSwamp 目标继续复用同一链，开箱与 Skull Key 共用唯一原生 SpecialItem 两阶段状态机，雕像调用原生 `Railroad.checkAction` 并等待冻结动画结束，所有 primitive 后 fresh 重算。编译器要求专用 profile、精确地图、运行时类型、物品数组、tile/tilesheet 与后置条件，任一漂移失败关闭；代码不直接写 quest、mail 或 talisman 字段。定向回归 `5/5`、Master Angler/route 回归 `16/16`、依赖防篡改自测和 Bootstrap/Runtime 构建均通过；没有启动游戏，因此不把源码锁定与单测冒充真实运行回执。fresh-save prerequisite 审计 SHA-256 为 `74460f8612d92ab09ead68aa2c5aeedd7eaa5aaed8dc1a6ced923f84b5578892`，未绑定族从 `3` 降为 `1`，只剩 `ginger_island_access`。下一固定切片为姜岛访问链，之后组合 Year 3 截止与 seed-robust 随机重试证明；`formal_product_training_authorized=false` 和全局准入分子不变。
- 姜岛访问链已完成第二个代码级切片。透明桥与专用原生交互链此前已覆盖 Willy 邀请、船体三项维修、`1000g` 船票、出航事件/BoatJourney，以及第一只鹦鹉、Western Turtle、Island House、Resort 四个 `ParrotUpgradePerch`；本切片把即时材料与货币缺口接回既有机器收取、保证掉落 Hardwood 的清障、投箱和原生睡眠结算，并把“当前 fresh snapshot 中可证明”的金核桃来源接回唯一既有动作链：金核桃灌木、特殊钓鱼、地面核桃、确定性挖掘点、金椰子预测和田野办公室调查。每次只执行一个原生 primitive，并保留 `fishing.catch_fish` continuation 后 fresh 重算；没有新增第二套采集、钓鱼、挖掘、晶球或田野办公室执行器。四次鹦鹉支出是依次持有 `1 + 10 + 20 + 20`，新档到 Resort 的总依赖为 **51** 个金核桃，不是 21。当前地图无精确生产者时明确失败关闭，不会随机游走冒充证明。姜岛定向回归 `7/7`、Core 测试工程构建 `0 warning / 0 error`；未启动游戏，没有新增原生运行回执。**该族尚未完成**：透明桥仍只对已加载/current-location 来源给出可执行候选，跨地图及未加载地点的全 51 核桃权威来源目录、路线与随机重试预算尚未闭合，因此不得写入 `dependency.complete_master_angler.ginger_island_access` 完成节点，也不得重建为“零未绑定族”的正式审计。下一固定切片只做全 51 核桃来源的跨地图权威投影与逐来源复用；之后才重建 fresh-save prerequisite audit，再组合 Year 3 截止和 seed-robust 随机重试证明。`formal_product_training_authorized=false` 不变。
- 姜岛 51 核桃的**来源能力分母**现已闭合，但尚未升级为正式依赖完成。锁定 1.6.15 的 `IslandHut.ShowNutHint` 分母被整理为 `69` 个来源键、`130` 个核桃；透明桥实时读取 tracker、limited-drop、world flag、master mail，并对普通埋藏点调用当前世界 `IsBuriedNutLocation`，对日记埋藏点核对具体坐标、当前玩家已见日记和 `Diggable`。不购买可选 Field Office Bridge、也不使用 Resort 后置来源时，已经接通既有执行器的来源总量精确为 `51`：灌木、岛屿钓鱼、蚌结石、岛屿作物、固定埋藏点和两处西部日记埋藏点。随机来源每次只做一次原生尝试后读取 fresh limited-drop 计数；固定/日记埋藏点复用唯一 `executor.till_soil`，动作队列按来源键、坐标、剩余量和实时验证状态防漂移。跨地图只走一个已解析 connector 后 fresh 重算。相关回归 `42/42` 通过，未启动游戏、未新增原生运行回执。下一固定切片是建立 `1+10+20+20` 支出顺序、阶段可达来源和随机重试在 Year 3 截止内的机器证明，再重建 Master Angler prerequisite audit；在此之前 `ginger_island_access` 与正式训练仍为未完成。
- `golden_walnut_unlock_schedule_proof.v2` 已把金核桃容量按四个原生解锁阶段机器化，当前已接线来源累计容量为 `7/25/51/51`。随机 Resort 路线仍需 `IslandFishing/MusselStone/IslandFarming` 全部 `15` 个限量核桃及联合 `95%` 的 `69/105/214` 次尝试；首选 `deterministic_gourmand_requests` 改以 House 后三次 Gourmand 请求各 `5` 个确定性核桃补足同一缺口。透明桥、候选、编译、跨图 continuation 与唯一 `executor.advance_gourmand_request` 已闭合；E 盘隐藏隔离运行 `runtime-island-gourmand-smoke-20261023-110034` 通过引介和首单原生事件，EVD-341 记录 request index `0 -> 1`、`GoldenWalnutsFound 0 -> 5`、`IslandGourmand1` tracker、五碎片生成/拾取及互斥锁释放，且生产执行器不直接写进度或奖励。`executor.advance_gourmand_request` 因此升级为 `RuntimeVerified`，动作目录为 `161 five-gate`。新增 `island_gourmand_supply_schedule.v1` 已按锁定 1.6.15 `Data/Crops`、`Data/Shops`、`Data/Objects` 及 `IslandLocation/IslandWest` 原生规则闭合供给：大蒜种子必须在第 2 年春季购入并保留，甜瓜/小麦从第 2 年夏 1 起可购，三株在 IslandWest 全年可种且并行接受浇水/睡眠结算，最长甜瓜为 `12` 夜；成熟作物必须保持站立，不调用收获动作，三次交付继续由 Gourmand 链 fresh 重算。每种采购有独立窗口，标准总价 `130g` 不替代 fresh 商店报价。Resort 目标的 Ginger Island 候选现携带 Turtle `211`、House `223` 的最晚边界；Turtle 尚未完成而当前日超过 `211` 时在候选层直接失败关闭，IslandNorth/IslandWest 等无需 Resort 的目标不受影响。路线证明已新增实际日期组合入口，`211/223` 会关闭选路门而 `212/223` 继续失败；第二、三单连续原生回执仍单独挡住训练门。回归 `26/26` 通过，但默认实际日期仍须由 fresh-save 上游日程给出，故 `route_selection_complete=false`、正式训练仍禁用。下一固定切片是生成并绑定这两个实际日期，不再修改公式或动作；全量 hermetic 自检仍被既有 Money Revenue seed supporting-step fixture 缺失 snapshot path 阻塞。

- `golden_walnut_fresh_save_unlock_timeline_proof.v1` 已把上述“两个实际日期”从裸参数改成可审计的上游日程投影。输入必须是哈希绑定的 `golden_walnut_fresh_save_source_schedule.v1`：包含同一 `grandpa.maximum_21` 新档计划的首次登岛日、逐来源确定性核桃完成日，以及同一计划实际安排的 First Parrot、Western Turtle、Island House 三次鹦鹉交互日。投影只接受当前 51 核桃目录中已绑定动作、非 Resort、非可选桥且 `exact_native_*` 的来源；拒绝未知/重复/超量来源、随机来源，以及前置鹦鹉尚未执行就提前采集的后置来源。它独立计算 First Parrot `1`、Western Turtle 累计 `11`、Island House 累计 `31` 的钱包就绪日，并要求每次计划交互不得早于对应就绪日；最终期限使用计划中的实际交互日，而不是把“当天刚凑够”冒充“当天已经完成交互”。Master Angler 审计可通过 `--golden-walnut-source-schedule` 读取并哈希绑定该产物，路线证明不再把两个手填日期当作正式证据。定向回归现为 `21/21`，Bootstrap Release 构建 `0 warning / 0 error`。仓库尚没有真实新档 source schedule，因此默认审计仍失败关闭；下一固定切片是让上游新档 Teacher/日计划导出这一份来源日程（首先要给出真实首次登岛日），然后重建审计。之后只剩 Gourmand 第 2、3 单连续原生回执，不能提前提升训练准入。
- 上述导出接口现已落地为 `build-golden-walnut-fresh-save-source-schedule --episode-manifest ... --output ...`。输入清单必须来自同一 `run_id`、同一存档且哈希连续的 `plan_execution_episode.v1`；每条记录必须 `success=true` 且拥有 fresh after snapshot。导出器只承认透明桥中确定性来源的 `collected_quantity` 实际增量，并分别要求首次登岛、First Parrot、Western Turtle、Island House 出现原生 `false -> true` 状态跃迁，因此路径候选、路过动作、失败回执和手填日期都不能形成教师标签。产物同时绑定 manifest SHA-256、全部 episode/前后快照的有序证据根、run/save 身份和 episode 数量；旧式无来源日程会被时间线证明拒绝。限量来源跨日增长时只在最近一次已观察增量日登记当前累计量，保持保守而不提前计算核桃。定向回归 `12/12`、Bootstrap Release `0 warning / 0 error`。真实连续新档 episode 清单仍未采集，所以下一固定切片是运行同一 fresh-save Teacher 闭环并生成该清单，再以导出的 schedule 重建 Master Angler 审计；之后采集 Gourmand 第 2、3 单连续原生回执。正式训练继续禁止。
- Rain Totem 即时缺料展开已接回同一个 `fishing.catch_fish` 高层出口。它只接受精确 `709 x1 + 432 x1 + 726 x5` 材料行；缺料时复用现有原生候选收取已完成机器产物、砍伐保证掉落 Hardwood 的树、拾取猪生成的 Truffle，或把 Truffle 投入 Oil Maker，每执行一个机械 primitive 都要求 fresh snapshot 后重新判断。没有新增策略标签、机器系统、采集系统或执行器。松焦油 5 天、猪成熟 10 天、Deluxe Barn/Oil Maker/Tapper 建设及 Hardwood 长期储备仍属于长期 reserve，依赖图明确锁为 `long_horizon_reserve_bound=false`；原生 Rain Totem 制作回执也仍缺失，所以天气族仍未完成、未绑定族仍为 `5`。权威图 SHA-256 为 `c437060b231ca4649c96798dfb521fa1bc7767eb5cf5ee2e7dbbabb28c7ccb17`；Rain Totem 回归 `22/22`、Bootstrap Release `0 warning / 0 error`。本机现存 Master Angler 审计夹具早于 60/2/10 权威字段，不能用于生成新的正式 audit hash；不得把临时补字段夹具当作当前证据。
- Pine Tar 长线储备现已作为天气族的独立部分节点接入，仍只暴露 `fishing.catch_fish`。生产器从哈希锁定的 Stage 1 鱼窗、fresh 缺鱼集合、节日日历、Rain Totem location-context、背包 `(O)726` 和当前 Pine Tapper 到期产出计算最早未来雨窗；新增 Tapper 数量按 `ceil(max(0,5-背包-已在产)/floor(使用前天数/5))` 计算，不再固定建 5 台。透明桥按锁定 `Object.placementAction` 额外核对成熟、非树桩、未占用、可 Tap、夏季临时绿雨树例外，并公开松树 `wild_tree:3:0 -> (O)726 / 5 天` 的精确生产行；执行继续复用通用机器制作、放置、支持账本和 fresh 重规划，每次只补一台。专项回归 `46/46`、Core/TransparentBridge/Bootstrap Release 均通过，未启动游戏。依赖图 SHA-256 为 `e6a4637b5501e12bc4f6e45deacd1b551376e0daad838f84ff1b15787ba4dccc`；`long_horizon_reserve_bound` 仍为 `false`，下一步固定为 Pig/Truffle Oil reserve，其后依次是 Hardwood reserve、材料消费 reservation 和原生 Rain Totem 制作回执。现存正式审计夹具仍缺当前 60/2/10 字段，因此本切片没有伪造新的 audit hash；全局指标和未绑定族 `5` 均不变。
- Pig reserve 的建筑前置不再被目录静默丢弃。`building_construction_catalog` 现同时公开新建与升级项目；升级行逐个绑定实时来源建筑类型和坐标，并锁定反编译确认的 `CarpenterMenu.Action.Upgrade -> ConsumeResources -> upgradeName + daysUntilUpgrade` 原生收据。高层仍只有 `buildings.construct`，底层仍复用 `executor.construct_building`，以 `construction_project_kind` 分支，未增加第二套建筑规划器或执行器。编译时会 fresh 重绑来源类型、坐标、价格、材料和原生契约；运行时点击前再次核对来源建筑，点击后同时验证升级名、倒计时、金钱和材料变化。专项测试 `11/11`、Runtime Harness Release `0 warning / 0 error`，未启动游戏。该切片只解除 Barn -> Big Barn -> Deluxe Barn 的执行硬阻塞，尚未建立 Pig 购买、成熟、出门产 Truffle 与 Oil Maker 的跨日 reserve，因此天气族、依赖图哈希、未绑定族 `5` 和正式训练禁令均不变。
- Pig/Truffle Oil 长线 reserve 已接入同一个未来雨窗意图和 `fishing.catch_fish` 出口。Teacher 按 fresh 状态逐次选择 Barn 新建、Big/Deluxe Barn 原生升级、精确猪舍购买 Pig、等待 10 日成熟、用 `Building.doAction` 原生右键开动物门、建立 Oil Maker、拾取 Truffle、投料或领取 `(O)432`；每个会改变世界的 primitive 后都重新投影，不跨状态边界预编译后续动作。非冬季产出机会、建筑/升级工期和 Pig 成熟期进入使用日前 lead-time，已有按期完成的 Truffle Oil 会抑制重复建设；库存已有 Truffle 时会直接建立 Oil Maker，远程 Farm 地面 Truffle 会先路由采集，不会误判为缺 Pig 产能。动物门透明层公开精确建筑/门坐标，编译器会对建筑身份漂移失败关闭，运行时不直接写 `animalDoorOpen`。`ActionQueueCompilerTests` 定向集合 `159/159`、Core/TransparentBridge/Runtime Harness/Bootstrap Release 均零警告零错误，Master Angler fresh-save 防篡改自测通过，未启动游戏。依赖图 SHA-256 为 `2178040959b142f04b3d5be0795945d0286a05ccfad405fe6c84ba63c54a5fd3`；本机审计报告仍为 partial、未绑定族 `5`，状态精确更新为 `runtime_consumer_crafting_read_scheduler_immediate_supply_pine_tar_and_pig_truffle_capacity_bound_hardwood_reservation_and_native_crafting_receipt_missing`。`long_horizon_reserve_bound=false` 与正式训练禁令不变；下一固定切片是 Hardwood reserve，其后是材料消费 reservation 与原生 Rain Totem 制作回执。
- Hardwood reserve 的现有来源链已接入同一未来雨窗意图。Pine Tar 与 Truffle Oil 已满足后，Teacher 会从 fresh snapshot 选择当前 Farm 的 `ResourceClump`、当前地图保证掉落 `(O)709` 的成熟野树，或先执行去 Farm 的 route connector；每次仅执行一个既有 primitive 后重新规划。树桩 parent `600` 绑定保守最低 `2`，倒木 parent `602` 绑定最低 `8`，来源地点、格点、parent、最低产量和状态都进入 canonical 参数与 fresh 验证。即时缺料链同步复用 `executor.break_farm_resource_clump`，没有平行执行器。当前只可宣称 `ready_hardwood_source_reserve_bound=true`：缺铜斧/钢斧时精确阻塞于 `blocked_required_axe_upgrade_missing`，通用商店购买器又刻意禁止工具与 on-purchase action，因此 `hardwood_tool_upgrade_lifecycle_bound=false`、`hardwood_reserve_bound=false`。依赖图 SHA-256 为 `87f9dacd6ed7f85bd8088514da0d4e21308df9d25f4579490740302b866b0802`；Rain Totem `22/22`、Hardwood `7/7`、图联合回归 `24/24` 均通过且未启动游戏。下一固定切片只做原生工具升级 lifecycle，之后才进入材料消费 reservation 与 Rain Totem 原生制作回执。
- 原生 Axe 升级 lifecycle 已完成代码级纵向闭环，但尚未提升为运行时证据。`player.tool_upgrade` 从实时 `Game1.toolData.ConventionalUpgradeFrom` 与 `ShopBuilder.GetToolUpgradeData` 发布下一等级 Axe、精确来源槽、价格、5 根对应金属锭、Clint/Blacksmith 端点、两日等待和待领取工具；Hardwood reserve 据此只生成一个 `submit_tool_upgrade`、一个 `claim_tool_upgrade` 或显式等待/阻塞阶段。日计划与动作编译器 fresh 重绑全部身份、成本、站位和投影指纹，Runtime Harness 仅通过原生 `Blacksmith` 对话、`ClintUpgrade` ShopMenu 行点击、`Tool.actionWhenPurchased` 及 Blacksmith 原生领取完成动作，不直接写钱、库存、`toolBeingUpgraded` 或升级计时器。隐藏静音 E 盘 full snapshot 已将透明模式刷新为 `177 required / 160 readable / 17 contextual / 0 blocking`；目录为 `238 registered / 240 semantic / 237 compiler-bound / 151 product-executor`。新增链路与目录定向测试 `12/12` 通过，完整 Core 为 `2791/2805`，剩余 14 个是本切片前已存在的其他模块失败。两个新 executor 仍为 `RegisteredOnly`、runtime/output gate 缺失，不得训练；`hardwood_tool_upgrade_lifecycle_bound` 只能解释为“源码锁定的离线实现已接线、原生提交/领取回执待采”。下一固定切片为三种材料的消费 reservation，随后接入一次原生 Rain Totem 制作回执；正式训练禁令不变。

## 已收口 · 2026-10-21 Money 首条完整原生作物生产段

- `money-crop-native-20261021-pricefix-v10` 已从不可变源存档完整执行并通过。运行先用现有 `economy.buy_supplies` 链完成 `21` 个有界原生过渡、精确购买 `5` 份 `(O)474` 花椰菜种子，再完成场地准备、春 6 日播种、连续条件浇水、春 18 日收获、`5` 次单件原生投箱、资源中性回家和唯一原生睡眠。整条 crop lifecycle 为 `48` 条连续 transition、`12` 个实际作物批次；两个无需浇水的批次按透明状态跳过。
- checkpoint v12 已达到 `verified fragments=6 / pending requests=0`。planting layout、seed acquisition、seed reservation、aggregate daily capacity、shared native lifecycle 与 terminal settlement 六个请求全部由当前源码独立重建；output retention 和 settlement capacity 继续作为支持证明。终局 recurrence 从 total day `18` 推进到 `19`（春 20 日 06:00），`money 50 -> 925`、`totalMoneyEarned 0 -> 875`，实际原生收入为 `875g`。
- 源存档目录哈希在运行前后均为 `68ddc8dbbe09382f2e7ba1c9d5d1e8dd6d0ffaef59acf9367415b4678f59366d`，隔离 Mod 根目录也未变化。最终透明状态哈希为 `5d0e30548a10e62a94caef5c241bd439ee3fd68dec11aa48e35d757e4d17d058`，最终隔离存档树哈希为 `9429c6d01d327111af8c81d0c36f2e318ccc4f1b0ea3094d84e19f25308b22e4`。睡眠回执包含确认、跨日、原生保存、菜单关闭、世界稳定和非职业升级菜单原生关闭六项原因。
- `money_revenue_crop_native_production_segment_receipt.v1` 已对 v10 做完整深回放。它从 run summary 逐层重建 Teacher 决策、commit receipt、obligation plan、native run plan、checkpoint 和 terminal recurrence，并重新哈希源存档、双 Mod、最终透明快照及最终隔离存档树。回执确认 Teacher 完整候选数 `50`、选中 `(O)474` 共 `5` 格、购种成本 `400g`、保守/实际毛收入 `875g`、净现金收入 `475g`；结果哈希为 `9cb93fde39c45b8e83cc5f541f64b3de7929985381a79620b3d2724acf04f6ec`。
- `money_revenue_crop_production_teacher_comparison_receipt.v1` 已把上述原生结果接回完整 Teacher 分母：`50` 个候选中 `5` 个 admitted、`45` 个带明确原因 blocked，选中候选对另外 `4` 个 admitted 候选形成确定性偏好对。只有选中候选 `crop:474:SeedShop:5` 带原生观测，未执行候选不会伪装成 native outcome。comparison receipt 哈希为 `6ed3e5ab911d6b3c1ce6bca8f3edce7a81de2b6a5ab0293e283792a22874b392`。
- 该结果关闭的是第一条完整生产段及其 segment-level outcome-backed Teacher comparison，不是 Money threshold cell：`875g` 尚未跨越最低 `50,000g` criterion，且 comparison receipt 明确保持 `criterion_local_join_ready=false`。因此 `Money production cells=0/18`、`CoverageGateReady=2/19`、`formal_product_training_authorized=false` 均保持不变。
- 后续 v15 已从该段最终隔离存档闭合下一次 Teacher 边界，满足有界退出条件。Money 现暂停并回到 CURRENT_STATUS_CN.md 第 6 节第 11 位；最终返回时，只有累计原生轨迹真实跨越权威阈值，才接入既有 criterion-local `6 x 3` production join。
- 未来 runner 摘要升级为 `money_crop_native_production_run_summary.v2`，会直接落盘最终快照路径/哈希、最终状态哈希和最终隔离存档树哈希；回执构建器仍兼容已成功的 v1/v10，但 v2 缺失任一最终身份都会失败关闭。当前验证基线：PowerShell AST 通过，Core game-free `455/455`，GoalConditionedBootstrap Release `0 warning / 0 error`。

## 历史记录 · 2026-10-20 Money v22 证据恢复与终端结算

- 首份真实隔离作物运行 `money-crop-native-20261019-v22` 已完成 13 个连续游戏日的机械执行。共享生命周期证明现重放 `48` 条连续 transition，其中作物队列 `12`、作物/睡眠支持路线各 `12`、原生跨日 `12`；验证了 `5` 次播种、`50` 次浇水和 `5` 次收获。逐波留存证明确认 `(O)190` 实际库存从 `0` 增至 `5`，保守毛收入下界为 `1750g`。这些是真实运行 fragment，但尚未形成任一 Money production cell，`Money=0/18`、正式训练仍为 false。
- 运行后证明构建不再强迫重跑游戏。`RuntimeMoneyCropEvidenceFinalization.ps1` 是原 runner 与离线 finalizer 的唯一共享实现；`Finalize-RuntimeMoneyCropNativeProductionEvidence.ps1` 会重新验证 production input manifest、源存档树、执行过的 run plan 和全部既有 proof，再只补 output-retention、fragment index 与 checkpoint。它不启动游戏、不修改存档，也不另写第二套证明语义。
- v22 的 checkpoint v12 当前为 `verified fragments=5 / pending requests=1`。已验证：planting layout、exact seed acquisition、exact seed reservation、aggregate daily capacity、shared native lifecycle；output retention 作为 terminal supporting proof 已重建。seed reservation 由透明库存投影生成精确原子 commit request，再通过共享 `ReservationPortfolioLedgerService.Commit` 重放，未手写最终账本。剩余精确 request 只有 `output-retention-terminal:<wave>`。
- 运输箱终局契约已升级为单波 v3、同日多波共享 v2。两者都显式分离投箱前路线、逐件原生投递、投箱后资源中性回家路线和唯一原生睡眠；回家路线必须由统一队列回执重放并抵达透明 `home_location_id`。在运输箱旁直接构造睡眠、路线夹带资源变化、没有移动 primitive 或睡眠未从回家终态开始均失败关闭。
- 共享作物生命周期已升级为 v4，并为短续跑加入显式 `checkpoint_resumes`。恢复点只允许位于已验证 `native_day_rollover` 之后、下一天 `supporting_route_to_crop` 之前；前后完整透明状态在递归移除运行 ID、隔离路径、读取 tick 与 Movie Theater 投影 fingerprint 后必须完全一致。背包、货币、作物、日期、位置或其他游戏状态任一漂移都会拒绝；普通连续运行仍使用空恢复列表和原有精确 state-hash/tick 链。
- 下一固定顺序：从 v22 已保存的第 18 天原生起点做短 continuation，复用既有收获、单件投箱、回家路线和唯一原生睡眠，生成 settlement-capacity 与 terminal/shared shipping proof。唯一 pending request 清零后，才进入 Teacher comparison 与 criterion-local join。
- PR #337 的两项明确 P1 review debt 已关闭：递归 source-boundary 归一化会移除所有层级的三个非语义 tick 字段并精确报告持久字段漂移；商店 endpoint 与 entrance 的未来营业窗口会在上游 defer 到两者较晚开放时间，不再被永久 block。当前 GitHub 没有新的明确修复 issue；ongoing/deferred/roadmap issue 只保留为约束，不扩张本轮审计。
- 验证：PowerShell AST 通过，Core game-free `447/447`，GoalConditionedBootstrap Release `0 warning / 0 error`，完整 hermetic 自测与 v22 离线 finalizer 均成功。下一步不重跑 13 日轨迹。

## 当前切片 · 2026-10-18 Money 原生作物生产收集器

- `build-money-revenue-crop-production-teacher-decision` 已补上原先缺失的正式 Teacher 决策生成。输入是同一状态边界的 fresh full snapshot 与一组互不重叠的 bounded `farm_layout` snapshots；候选只来自透明桥实时作物目录、实时商店 stock/价格、现有种子、money、物料供给和可准备地块。确定性策略先按保守净收入最大、再按最早终结日、浇水动作数和种子 ID 排序，并输出完整候选评估、选中作物和紧凑布局。架豆、稻田、特殊种植规则和非 `Grab` 收获在各自拓扑/留存证明接入前明确失败关闭，不会被静默遗漏或假设可执行。
- `Initialize-MoneyRevenueCropProductionTeacherDecision.ps1` 负责写入 typed Teacher request 并调用上述构建器；生产承诺与布局不再由调用者手写。`money_revenue_crop_production_decision_request.v2` 绑定 Teacher request、全部 bounded layout snapshots、完整候选评估和选择策略；`money_revenue_crop_production_decision_receipt.v2` 会从 Teacher request 重新生成决定并要求 canonical JSON 完全相等，再按顺序复用唯一的 `CropCommitmentLedgerService.Upsert`。旧 v1 只保留给历史 hermetic fixture，不允许进入当前 production initializer。
- `farm_layout` 的 bounded 场地投影现由 `FarmSitePreparationProjector` 唯一实现。当前地点适配器与 `FarmReadAdapter` 都复用它，因此玩家尚在 `FarmHouse` 时也能对实际 `Game1.getFarm()` 进行目的受限的最多 `32×32` 读取；没有复制第二套地块分类规则，也没有扩大 full snapshot 的扫描面。
- `Initialize-MoneyRevenueCropNativeProduction.ps1` 已把 v2 收据接到唯一生产输入物化链。它拒绝覆盖已有输入目录，先保护源存档目录哈希，再原样提取 `final_ledger`，依次调用既有 obligation-plan 与 native run-plan builder，最后生成 `money_crop_native_production_input_manifest.v1`，绑定源存档目录、Teacher request、Teacher 决策、快照、账本、两级请求和两级计划共 9 个引用制品的绝对路径与 SHA-256。该 manifest 仍不是原生证据或训练授权。
- `Invoke-RuntimeMoneyCropNativeProduction.ps1` 现在只接受上述 production input manifest，不再允许调用方分别传入源存档与 run-plan request。独立验证库会在构建、部署或启动游戏之前复核源存档树哈希、全部 9 个制品哈希、Teacher request/selection policy、决策最终账本的原样提取、各级 path/hash 血缘、固定 Money 目标边界，以及 decision/receipt/obligation/run-plan 四层完全相同且不重复的 commitment 集合；运行器直接复制并消费清单中的既有 run plan，不再重复生成第二份计划。
- `Invoke-RuntimeMoneyCropNativeProduction.ps1` 已把 `money_revenue_crop_native_production_run_plan.v2` 接入隔离源存档、精确双 Mod allowlist、现有 action/plan compiler、RuntimeTestHarness 原生执行和统一回执链。v2 先枚举全部非零种子 shortfall，并复用既有 `economy.buy_supplies` 候选与编译执行链，以一次一份、fresh snapshot rebind 的有界原生切片精确补齐，禁止脚本直接改库存或货币；随后按连续日期逐批执行 `plant -> conditional water -> harvest`，每次跨地图只编译一个透明 connector 并 fresh replan，每个非终端日复用既有 `recovery.stabilize_day` 生成唯一原生睡眠。每项补购现在同时物化 v2 执行清单和可重建证明，直接进入同一 crop checkpoint。
- run plan 现在从权威 crop-production 源快照当天开始，而不是错误地跳到最早种植日；源日到种植日前的每一天只能有一个原生 sleep boundary。运行器在任何动作前输出 `money_crop_native_source_preflight.v1`，要求隔离存档首次 fresh full snapshot 的 `state_hash` 和 `total_days` 同时精确匹配计划源边界；不匹配时立即停止，不能用同日期的相似存档代替。
- 播种日不再假设选中地块已经长期保持 HoeDirt。每个承诺的布局必须能被一个 `32×32` `farm_layout` 投影完整覆盖；运行器在布局与容量取证前调用既有 `money_revenue_crop_site_preparation_plan.v1`，一次只执行一个现有 `clear_obstacle`、`break_farm_resource_clump` 或 `till_soil` 候选，每次状态变化后重新抓取 fresh bounded snapshot 并重建计划，直到全部目标格被透明状态确认为 `already_prepared`。阻塞、候选缺失、日期/地点漂移或有界动作数耗尽都会停止运行，不会直接尝试播种。
- 浇水批次在执行前只按透明 `farm.crops[].needs_watering` 过滤；雨天不生成冗余浇水，但若当天逐日容量证明要求边界快照，仍可通过资源中性的 crop-ingress 到达 Farm。跨地图 move 被现有 compiler 重写为 connector 时现在保留唯一 `candidate_id`，因此路线、作物动作和睡眠的 queue/receipt/after snapshot 都能维持可审计候选身份。
- 当前收集器会物化 seed-acquisition v2、planting-layout、aggregate daily-capacity、shared lifecycle v3、逐波 output-retention、fragment index v13 和 partial checkpoint v12；源存档及隔离 Mod 目录都做前后哈希保护。上方 2026-10-20 切片记录了首份真实 v22 制品；terminal ShopMenu/shipping settlement 尚未闭合，因此 `Money production cells=0/18`、`formal_product_training_authorized=false` 不变。
- 下一固定切片已由 v22 checkpoint 收敛为 seed reservation 与 terminal shipping settlement 两项，再进入 Teacher comparison 与 criterion-local join。不得手写生产 JSON，也不得另写第二套购买器、作物候选、规划器或执行器。

## 已收口 · 2026-10-18 Money 购种原生切片证明 v2

- 旧 v1 只接受单一 acquisition terminal transition，无法表示安全购买链必须执行的“路线/开店 + 多次 quantity-one 购买 + 每步 fresh rebind”。v2 改为读取不可变执行清单，但每条 transition 仍来自现有 `economy.buy_supplies -> DailyPlanCompiler -> ActionQueueCompiler -> RuntimeTestHarness`，没有第二套购买器或执行器。
- 每条回执都经统一 `QueueExecutionReceiptValidator` 重放；支持阶段只允许资源中性的移动、连接器、交互、对话、等待和关菜单，购买阶段只允许等待、单次 `executor.buy_shop_item` 和关菜单。continuation 必须锁定种子、商店、stock row、输出 stack/quality、价格上限和 `quantity=1`；实际库存增量和 money 减量必须与编译参数一致。
- 状态链要求同一 save/player/game、同日、逐 tick 连续且不晚于 seed deadline。首个缺种承诺必须从计划源状态开始；后续缺种承诺必须按计划顺序引用并重建上一份购种 proof，以其终态作为唯一新起点。支持阶段的完整持久资源边界必须不变；购买阶段除玩家库存、money/余额镜像和物料图外的持久状态必须不变。全部切片的累计净增必须精确等于计划 shortfall，少买、超买、断链、跳过前序承诺、错误物品或身份漂移均失败关闭。
- hermetic 自测覆盖一段资源中性路线和两次购买的成功聚合，并拒绝缺最后一片、替换 before boundary 及非单份购买。该 proof 已接入 fragment index/checkpoint；尚未产生正式原生 fragment，所以 Money 仍为 `0/18`，训练授权仍为 false。

## 已收口 · 2026-10-18 Money 整计划共享原生生命周期

- `money_revenue_crop_native_lifecycle_proof.v3` 不再为每个承诺接受一条互相独立的世界时间线。每份逐承诺 proof 都必须从 aggregate daily-capacity proof 加载整份计划的全部布局，并重放同一条连续 world-state/tick 链；每个原生睡眠只出现一次，同时验证全部仍活跃作物的反编译 `Crop.newDay` 转换。v3 进一步承认睡醒位于 `FarmHouse`、睡前又必须回家的真实原生状态：`supporting_route_to_crop` 和 `supporting_route_to_sleep` 分别位于当天作物动作前后，只能复用既有移动/连接器/有界等待 primitive，必须由统一执行回执验真、保持同一日期、保持库存/货币/持久世界状态不变并最终到达声明的 `Farm` 或 home 地点；缺路线、隐藏传送或夹带资源动作均失败关闭。雨天没有实际作物动作时不得生成无依据路线；只有 aggregate daily-capacity 明确要求当天 Farm 边界快照时，才允许资源中性的 crop-ingress。
- 作物动作队列仍复用现有播种、浇水和收获执行器，但每个同类队列必须完整归属于一个不重叠承诺布局。transition proof 记录 `action_commitment_id`；output-retention 只选择本承诺的连续收获批次，因此同日多承诺不会互相吞并库存增量或动作分母。
- fragment index 已升级为 v13、checkpoint 已升级为 v12。hermetic 双承诺正例不再建立两份独立生命周期后合成终点库存，而是在一条共享轨迹中顺序完成 `2+3` 格的全部动作、逐承诺留存、5 次原生投箱与一次共享睡眠结算，并在每天动作前后用两类显式资源中性路线连接 `FarmHouse` 与 `Farm`。正式 fragment 仍未采集，Money 保持 `0/18`。
- 上方收集器已实现该逐日循环。下一固定切片是首次真实隔离运行及 terminal settlement 接线；不得另建作物规划器或动作执行器。

## 已收口 · 2026-10-18 Money 同日多波共享原生结算证明

- 新增的同日多波证明现为 `money_revenue_crop_shared_shipping_settlement_proof.v2`，用于一个收获日存在多个承诺/波次时的真实组合边界。它独立重建作物义务计划、整计划结算容量证明与每份输出留存证明，要求终端 fresh snapshot 在同一存档、玩家和收获日中持有全部保守库存分母。
- 投箱逻辑没有复制：单波 `money_revenue_crop_terminal_settlement_proof.v3` 与共享证明共同调用同一个单件原生投递批次校验器、资源中性路线校验器和 Revenue 睡眠 recurrence 校验器。共享模式先按容量证明固定顺序连续投递全部波次，待结算值必须从 `0` 跨波次严格递增；投递完成后必须沿显式资源中性路线回到透明 home location，最后才允许一次原生睡眠结算。
- 当时 fragment index 升级为 v11、checkpoint 升级为 v10；当前已由共享生命周期及显式睡醒路线切片推进到 v13/v12。共享证明覆盖的每个 `output-retention-terminal:*` ID 才进入 request numerator；留存与聚合容量仍只作支持。单波 proof 与共享 proof 重复覆盖同一 request、重复波次、跨日波次、错误顺序、库存不足或金额链断裂均失败关闭。
- hermetic 正例对同日两个承诺建立 `2+3` 件真实留存支持，执行 5 条连续单件投箱队列并以唯一睡眠结算 `600g`，checkpoint 同时关闭两个终端 request。正式 fragment 仍未采集，所以 Money 保持 `0/18`、`formal_product_training_authorized=false`。下一固定切片是 real Money native production collection runner：从 Teacher 承诺生成有退出条件的逐日队列，复用现有播种/浇水/收获/投箱/睡眠执行器采集上述全部 proof fragment。

## 已收口 · 2026-10-18 Money 作物结算日聚合容量证明

- 新增 `money_revenue_crop_settlement_capacity_proof.v1`。它重建完整作物义务计划、整计划逐日容量证明和全部布局证明，再把不同承诺的保守收获波次按同一 `harvest_total_day` 合并；每个保守单位都必须占用一条只含一次原生单件投递的 fresh-replan 队列。
- 路线和时序没有产生第二套实现：作物维护层公开既有确定性 BFS 的终点；运输候选与容量证明共用 `ShippingBinRouteProjector`；投递编译器与容量证明共用 `ShippingSettlementBudgetPolicy.NativeSingleItemDepositTicks`。聚合日预算由既有维护 ticks、维护终点至运输箱站位的 BFS 尾程和 `N` 次投递 ticks 组成，必须在 autonomous recovery cutoff 前整体可达且不超时。
- 当时 checkpoint fragment index 升级为 v10、checkpoint 升级为 v9；当前已由共享生命周期及显式睡醒路线切片推进到 v13/v12。整计划容量证明仍只作支持，不进入 request denominator，也不增加 numerator。
- 该切片只证明计划可容纳；上方共享结算证明现已补齐多个真实波次连续投递和共享睡眠的组合边界。正式 fragment 尚未采集，Money 仍为 `0/18`，`formal_product_training_authorized=false`。

## 已收口 · 2026-10-18 Money 作物逐波输出留存证明

- 新增 `money_revenue_crop_output_retention_proof.v1`。每份证明只覆盖一个精确承诺的一个收获波次，独立重建作物义务计划与原生生命周期 proof，并只选择该波次连续的既有 `executor.harvest_crop` 队列和统一执行回执；收获动作数必须等于承诺地块总数。
- 留存以透明玩家库存为唯一当前终点：精确 `qualified_item_id` 的库存净增必须不少于计划中的保守最小产量。额外随机产量只记录实际增量，不抬高计划的保守收入下界；地面 debris 不等于玩家已持有库存，也不能通过此证明。
- hermetic 正例覆盖 2 格草莓的 5 个再生收获波次，每波都验证 `2` 个动作与至少 `2` 个实际库存增量；负例保留合法作物收割状态转换但移除最后一波库存增量，证明会失败关闭。镰刀作物若先落为 debris，当前 v1 会失败关闭；后续必须把既有拾取执行与回执接入留存边界，不能把 debris 冒充留存，也不能另建作物执行器。
- fragment index 已升级为 v7，checkpoint 已升级为 v6，并可逐份重建 output-retention fragment。尚未采集正式 fragment，Money 仍为 `0/18`，`formal_product_training_authorized=false`。下一固定切片是把已留存输出接入既有 ShopMenu sale 或 shipping recurrence，证明原生结算与 Money/`totalMoneyEarned` 增量。

## 已收口 · 2026-10-18 Money 原生作物生命周期证明

- 当时新增 `money_revenue_crop_native_lifecycle_proof.v1`，只编排并验证既有 `executor.plant_seed`、`executor.water_crop`、`executor.harvest_crop` 与 `executor.sleep` 队列，不新增作物候选、规划器或执行器。训练队列每批最多 8 项；证明允许连续分批，但要求每批同类、状态哈希连续、执行回执完整，且同一天对承诺布局的播种/收获格必须不重不漏。浇水日要求最终逐格已浇水；雨天已湿地块无需生成冗余动作，显式浇水仍不得重复。当前契约已由上方整计划共享轨迹修正升级为 v2。
- 每个跨日边界必须是唯一原生睡眠队列。验证器从透明桥 `farm.crops` 读取种子、阶段数组、当前阶段、阶段内天数、成熟/再生倒计时、死亡和浇水状态，并逐格重放反编译的 `Crop.newDay` 规则。快照身份、世界日期偏移、队列回执、目标位置和种子身份任何一项漂移都会失败关闭。
- hermetic 正例完整覆盖同一批次 2 格草莓从播种、48 个逐格浇水动作、24 次原生睡眠到 10 个逐格再生收获动作，共 54 条连续队列转换；它同时验证多项目队列的逐项回执链和按批次聚合的种子消耗，缺最后一段生命周期会被拒绝。fragment index 已升级为 v6，checkpoint 已升级为 v5，并能独立重建 lifecycle fragment。
- 该层证明原生生命周期执行，不证明每轮实际产量、产物保留或收入结算。后续逐波 output-retention proof 已实现，但尚未采集生产 fragment，Money 仍为 `0/18`，`formal_product_training_authorized=false`。

## 已收口 · 2026-10-18 Money 作物逐日容量证明

- 新增 `money_revenue_crop_daily_capacity_proof.v1`。每个证明都会重建精确作物义务计划和**全部活动承诺**的布局证明，再按所有承诺的播种、保守浇水和收获日期并集逐日聚合；不同承诺不能分别通过后再在同一天超额占用时间、体力或水量。
- 每一天必须提供同一存档、玩家和游戏版本的 fresh snapshot，透明字段状态必须可读，`total_days` 必须与 year/season/day 保持同一世界日期偏移。容量计算复用共享单源 BFS 距离场、游戏时钟换算和编译器所有的移动、播种、浇水、收获时序；透明桥新增原版作物 `is_raised` 与精确 power-zero 水壶状态。v1 在需要补水路线时失败关闭，不猜测或省略补水。
- hermetic 正例覆盖 25 天、100 格作物：播种日 `100 播种 + 100 浇水`、同日 `100 浇水 + 100 收获` 及最终纯收获日；负例拒绝缺一天、日期字段漂移和 `99/100` 水量。fragment index 已升级为 v5，checkpoint 已升级为 v4。
- 该层只证明计划内逐日维护容量，不证明原生生长生命周期、逐波输出保留、结算或正式训练。后续原生生命周期 proof 已实现，但正式 fragment 仍需真实采集。

## 已收口 · 2026-10-18 Money production 前 correctness 收口

本轮已关闭 **#307**、**#308**、**#309**、**#310** 与 **#311**：

- native outcome summary 升级为 v2，绑定隔离 Mods 根目录运行前后哈希及两个允许模组的目录、UniqueID、版本、manifest 与整目录哈希；回放重新枚举实物，缺失、额外、漂移、错配或历史 v1 均失败关闭。PowerShell 5.1 / C# 哈希一致性已加入 hermetic 测试。
- 共享 raw JSON 提取已改为 Windows PowerShell 5.1 / PowerShell 7 共用的严格词法扫描，目标 ledger 值按原始字节区间写出，不经过对象往返序列化。双壳层实测覆盖嵌套、转义、时区小数以及缺失/重复属性失败关闭；七条生产调用路径不变。
- 种子预留 proof request/proof 升级为 v2。proof 不再接受调用方手写最终 ledger：必须同时提交原子 commit request/result，且验证器会从计划基准 ledger 精确重放 `ReservationPortfolioLedgerService.Commit`，核对 revision、状态哈希、claim ID、数量、槽位、用途和最终 ledger。预留快照不得早于计划源快照，同日还必须保持 tick 单调；伪造账本和早期快照均有负例。
- 作物义务计划升级为 `money_revenue_crop_production_obligation_plan.v2`。已有同承诺种子 reservation 会显式计入 `current_reserved_seed_quantity`，仅未预留余量参与后续承诺分配；已经过去的种植日失败关闭。proof fragment index 升级为 v3，并以精确计划 SHA-256 和当前文件系统路径语义绑定所有 fragment。
- Revenue denominator 现在逐项验证依赖图声明的 `required_field_paths`，缺字段、不可用状态或空值都会阻断相应节点。农场局部读取使用 overflow-safe 32×32 边界和不会回绕的遍历；资源团块站位统一复用共享 BFS；锄地候选和编译在体力缺失或 `<=0` 时失败关闭。
- terminal-proof collection request/plan 升级为 v2，显式绑定期望 run/partition、骨架分母、concrete denominator 与请求分母。合法 skeleton 可以展开为多个 concrete request；错误 catalog/run/partition/boundary 失败关闭。唯一离线复用 sale episode 的 before capture 现严格使用 request/verifier 的 terminal snapshot。
- 验证通过：Core game-free `372/372`、Bootstrap Release `0 warning / 0 error`、完整 hermetic 自测、E 盘隔离游戏依赖下 TransparentBridge Release `0 warning / 0 error`。未启动游戏，Money production cells 仍为 `0/18`，formal training 仍为 false。

当前已知 P0/P1 review debt 清零，唯一 primary item 继续是 **Money / Revenue**。购种 shortfall 与逐日容量 proof 适配器已经落地；下一切片是原生作物生命周期，然后依次闭合逐波输出保留与结算，再进入真实 Money native production collection、terminal proof completion、Teacher comparison 与 criterion-local join。**不得因为已有 terminal/crop infrastructure 就提前把 Money 从 `0/18` 上调。** Full Shipment 已冻结为 current-authority `26/26`，不再作为当前施工项。

## 已收口 · 2026-10-18 Money 作物种子短缺获取证明

- 历史 `money_revenue_crop_seed_acquisition_proof.v1` 只适配既有 `AcquisitionRoutePortfolioRolloutProofBuilder` 的单 terminal rollout，现已由上方 v2 连续原生切片证明取代；该历史实现同样没有新增商店候选、寻路、购买编译器或执行器。
- proof 强制 rollout 从精确 crop-plan snapshot 起步，`goal_id=earn_money`，只有一个完成的 terminal transition，物品等于承诺种子、`required_amount` 和实际增量都等于计划 shortfall，并在 seed-supply deadline 当日或之前完成。同存档/玩家、状态哈希、文件哈希和 fresh terminal receipt 均独立核验。
- crop fragment index 当时升级为 v4，checkpoint 升级为 v3，可累积该 proof；少买一份、错误计划边界、额外 continuation 或未完成 rollout 均失败关闭。后续逐日容量切片已把它们分别升级为 v5/v4。尚未采集正式购种 fragment，Money 仍为 `0/18`，正式训练仍为 false。

## 已收口 · 2026-10-18 Money 种子预留 proof

- `build-money-revenue-crop-seed-reservation-proof` 已能重建义务计划，并核对同一 save/player、透明物料图、活动 reservation、种子身份、状态哈希、库存槽与完整数量。
- #310 已关闭：committed reservation ledger 必须由原 crop plan 的 strategy-ledger lineage 经原子提交服务合法演化，reservation snapshot 具备 post-plan freshness 下界。
- seed-reservation fragment 已通过 correctness gate；购种 shortfall 与逐日容量已有精确 proof 类型但正式 fragment 仍待采集，生命周期、输出保留与结算继续 pending。Money 保持 `0/18`，训练授权仍为 false。
## 已收口 · 2026-10-18 Money 作物场地准备计划

- 透明桥已有目的受限的 `farm_layout` 投影，计划器也已复用现有 `executor.clear_obstacle`、`executor.break_farm_resource_clump`、`executor.till_soil` 候选；清障后必须取得 fresh snapshot 再绑定锄地。
- #311 已关闭：局部边界与遍历防整数回绕，resource-clump 站位统一使用共享 BFS，zero-stamina 与缺失 stamina 均在候选和编译阶段失败关闭；crop denominator 的 required fields、过期种植日、已有预留和精确计划哈希也已收口。
- 该层仍只是机械执行计划，不是 planting-layout proof、生产证据或训练授权；Money 保持 `0/18`。
- 当前原生作物运行器已消费该计划：播种日按一个候选一次执行并在每次清障/锄地后 fresh rebind，完成后才捕获 planting-layout 与 aggregate daily-capacity 边界。场地准备动作本身仍不增加证据分子，也不改变训练授权。
## 历史记录 · 2026-10-16 Money 作物布局证明检查点

- `money_revenue_crop_production_obligation_plan.v1` 的每项承诺现显式保留种植季节、基础生长天数，并把 `planting-layout:<commitment>` 纳入同一个证据分母。旧计划缺字段或请求分母漂移时必须重建，不能沿用。
- 新增 `build-money-revenue-crop-planting-layout-proof`。调用方提供的是非机械的精确地块选择；构建器逐项重放原计划和 planting-day 透明快照，核对同一 save/player、精确日期、Farm 室外季节、空且无对象的非花盆 HoeDirt、原生种植许可、季末成熟许可及不晚于承诺上界的原生调整后生长日数。实际播种继续复用 `farm.maintain_crops -> executor.plant_seed`，没有第二套作物执行器。
- 新增 `money_revenue_crop_production_proof_fragment_index.v1` 与可恢复 checkpoint。v1 只认可重新构建一致的布局 proof；fixture 中 `1/10 verified` 时其余种子预留/购种、日容量、原生命周期和逐波终端保留请求全部保持 pending，三个训练/日程 gate 均保持 false。分母外、重复、篡改或不可用地块均失败关闭。
- 这一步同时暴露真实后续缺口：如果种植日没有足量合法 HoeDirt，proof 不会假装可执行。下一切片先把已有清障、锄地和路线动作组合为前置场地准备计划/证明，再闭合种子供给、逐日时间体力容量、生命周期和输出保留；Money 生产仍为 `0/18`。

## 历史记录 · 2026-10-16 Money 未来作物生产义务计划

- 既有 `CropCommitmentLedgerService` 现在从透明 `farm.crop_catalog` 为每项种植承诺固定种子身份、是否需浇水、逐日浇水义务、每次收获日、最小波次数、最小产量、零品质单价和保守毛收入；所有日期和数量由共享 `CropProductionObligationProjector` 计算，没有另建作物规划器或执行器。
- 新增 `money_revenue_crop_production_obligation_plan.v1`。它重新核验快照、策略账本、原生作物目录和当前未预留种子库存，为每项活跃承诺生成购种/预留、日容量、原生成长以及每个收获波次的终端保留证据请求；同一批种子不能被多个承诺重复计算。
- 旧版、字段缺失或被篡改的承诺失败关闭。计划固定保持 `future_crop_production_proof_ready=false`、`fresh_save_revenue_schedule_ready=false` 与 `formal_product_training_authorized=false`；它关闭的是未来作物义务遗漏和采证分母不明确，不是生产证据本身，Money 仍为 `0/18`。
- 下一切片只沿该计划补可恢复的原生 proof checkpoint：验证购种/预留、每日时间与体力容量、作物生命周期和每波输出保留，再由 fresh-save 下界日程组合到百万元目标。验证已通过 Core game-free `366/366`、Backend `229/229`、Bootstrap Release 零警告及 hermetic 关键链，未启动游戏。

## 历史记录 · 2026-10-16 Money terminal-proof 可恢复 checkpoint

- 新增 `money_revenue_terminal_proof_fragment_index.v1` 与 checkpoint builder。fragment 可以分批累积，但每一项必须在计数前通过既有 terminal execution verifier；重复 request、denominator 外 request、上游计划漂移、账本/快照/recurrence 篡改都会失败关闭。
- `Initialize-MoneyRevenueTerminalProofCollection.ps1` 自动物化 collection plan 中唯一的 `reuse_verified_shop_sale_episode`：不启动游戏，只使用已验证 episode 的真实 before/after snapshot 和 run ID，生成独立前后 strategy-ledger capture 与 Revenue recurrence，再进入 checkpoint 深验。脚本可重入，已有 fragment 先验证后保留。
- partial checkpoint 只报告 verified/pending request IDs，不能进入 completion。`build-money-revenue-terminal-proof-index` 仅在 `pending=0`、verified count 等于完整 request denominator 且 checkpoint 精确重建一致时输出 v1 final index；formal training 始终保持 false。
- hermetic 回归覆盖完整 fragment、空 checkpoint、重复 fragment 拒绝与 final-index gate。当前只关闭“已有原生 episode 仍需人工手拼 proof”的缺口；其他 shop/shipping/rebound 反事实仍需后续原生采集适配，Money 仍为 `0/18`。

## 历史记录 · 2026-10-16 Money terminal-proof 采集适配

- `Initialize-MoneyRevenueComparisonWork.ps1` 在 concrete denominator 与 counterfactual request catalog 完成后，现自动调用 `build-money-revenue-terminal-proof-collection-plan`，不再把“下一步如何采证”留给调用者从 catalog 手工猜测。
- 新计划重建 comparison plan 和 counterfactual catalog，保持一请求一工作项。每项固定列出 terminal snapshot/candidate/strategy-ledger 身份、原生收入证明种类和有序阶段；acquisition rebound 会显式要求恢复已验证终态，shop 与 shipping 进入不同采集模式。
- 唯一已有原生 sale episode 只有在 source state、catalog candidate、shop、item、slot、quantity 和 unit price 全部匹配时才标记为 `offline_materialization_ready`。其余项保持 `native_collection_required`；计划固定 `terminal_proof_index_ready=false`、`formal_product_training_authorized=false`，没有把计划伪装成证据。
- Bootstrap hermetic 新增三路分流和重复 request 拒绝回归；后续可恢复 checkpoint 已按上节落地。

## 历史记录 · 2026-10-16 Money 作物生产支持边界修正

- 首次真实 Money 原生采证诊断暴露的“`farm.maintain_crops` 需要进入训练 allowlist”是旧依赖图中的层级错误，不是动作缺失。EVD-226 已闭合该高层选项的五道证据门，但其职责是把透明的当前地点作物义务确定性展开为浇水、播种、施肥、普通收获或巨型作物收获；它不得成为 Student 策略标签。
- `earn_money` 依赖图现在把 `farm.maintain_crops` 接成 `deterministic_transition`：Teacher 负责选择作物收益方法及经济参数，编译器负责逐 fresh snapshot 展开机械动作，只有验证过的原生收获回执才能流入 reserve-safe 待售库存。当前快照 denominator 会枚举该支持节点，但不会把它计入 comparison candidate。
- 原错误 blocker 已替换为真实剩余工作：未来日期的购种、播种、成长、浇水、收获和库存保留承诺仍需完整证明。fresh-save 百万累计收入下界日程和反事实机会成本标签也仍未闭合，所以 Money 保持 `0/18`，`formal_product_training_authorized=false`。
- 新增权威 JSON 架构回归，固定作物节点类型、五种候选 kind、运行时证据状态、非训练准入状态和图连接；bootstrap 自测同时证明确定性作物候选不会泄漏到 Teacher 比较标签。验证为 Core game-free `356/356`、Backend `229/229`、Bootstrap Release `0 warning / 0 error`、hermetic 自测与当前 checkpoint 检查通过，未启动游戏。

## 历史记录 · 2026-10-16 current-authority Full Shipment 26/26 收口

- 从锁定的 `9/26` 检查点按冻结顺序完成剩余 17 个标准 strata。最终里程碑检查点为 `26 records / 0 remaining / completed_selected_batch`，严格 evidence index 为 `verified=26 / missing=0 / shared_shipping=true`；原生游戏、Backend 与 Product 进程均已退出。
- 首次防风草运行被精确重放守卫拒绝。根因是 PowerShell 把后端回执内的 ledger 历史时间从 `+00:00` 往返序列化为本机 `+08:00`，语义时刻相同但 canonical JSON 不同。没有放宽 verifier，也没有改写旧证明。
- 新增共享 `Write-RawJsonProperty`，标准提交/结算、断点恢复结算、规划重建提交以及商店提交/支持提交/结算共七个路径统一直接从后端原始响应提取 `ledger`。全仓运行脚本已无 `.Value.ledger` 的有损提取，源守卫固定该约束。
- 修复后真实防风草路线依次通过 atomic commit、原生采收、fresh terminal、settlement 与 rollout proof；其余 16 个标准层连续通过同一链，证明修复覆盖真实闭环而非只通过离线测试。
- 收尾验证为完整 solution Release `0 warning / 0 error`、Core `352/352`、Backend `229/229`；Full Shipment settlement、dispatch、static compilability、runtime sample evidence、Bootstrap batch 与 hermetic 自测全部通过，变更脚本 AST 和 `git diff --check` 通过。
- 该结果只完成 Full Shipment 的当前权威运行签名层。`formal_product_training_authorized=false` 保持不变，Goal Method / formal coverage 分母没有增加；收尾验证和提交完成后返回 Money / Revenue WIP=1，不启动训练。

## 历史记录 · 2026-10-16 current-authority 批处理与规划流水线优化

- 固定顺序小批次连续完成 `location_fish_spawn_sample`、`location_artifact_spot_sample`、`geode_drop_sample` 与 `shop_purchase_sample`；优化完成后又以 `community_center_reward_sample` 做真实原生代表回归。五层均通过当前 Teacher、原生执行、运输箱、睡眠结算与 rollout proof；current-authority evidence index 从 `4/26` 推进到 `9/26`，剩余 `17` 层，`formal_product_training_authorized=false`。
- 性能审查确认同一套 calendar + 12 个 target-date 轴此前在首次规划、商店支持过渡重规划和断点恢复重建中各维护一份，并且每个轴启动一个独立 Bootstrap 进程。现统一为 `RuntimeAcquisitionPlanningPipeline.ps1`，14 个确定性命令在一个有界顺序批处理中执行；只有显式标记为 `BatchSafe` 的纯规划构建命令可进入共享会话，失败即停、禁止递归、拒绝重复或覆盖控制文件的输出，且保留逐进程 oracle。
- 使用刚通过的地点鱼真实输入做等价对照：批处理 `14.51s`，逐进程 oracle `93.69s`，14/14 输出 SHA-256 完全一致；紧凑 JSON 将该段落盘量从约 `145 MiB` 降到 `72.5 MiB`。随后社区中心奖励真实原生场景通过同一优化链并进入当前 evidence index。这只消除重复进程、重复读取和空白格式，不改变候选、计划、编译、执行、证明或训练准入语义。
- evidence index 的实测 trace 还暴露默认 32 MiB read session 在 9 份 proof 上产生约 `13.4 GiB` 物理读取和 `3204` 次逐出。索引调用点现单独使用 `256 MiB` 总预算与 `64 MiB` 单项预算；同一输入 A/B 输出逐字节一致，墙钟由 `106.7s` 降至 `92.9s`、物理读取降至约 `10.57 GiB`，峰值工作集由约 `1127 MiB` 增至 `1327 MiB`。确定性构建缓存不扩容，因为 512 MiB A/B 没有增加命中数。
- 全量 correctness 审查同时关闭三个独立缺口：放射性矿石当前候选只接受 `native_radioactive_ore_node / GameLocation.breakStone / (O)909`，普通滚动矿点显式排除怪物掉落与放射性矿点；legacy unbound 证明回退到唯一候选时会保留真实 `candidate_id`；Money 空初始化在 canonical corpus 或非零计划已存在时于任何写入前失败关闭，不能把真实进度覆盖成 `0/18`。
- 回归结果为完整 solution Release `0 warning / 0 error`、Core game-free `351/351`、Backend `229/229`，五项聚焦 Bootstrap 自测与 PowerShell AST 均通过。Money 防覆盖的功能测试证明 fresh 空计划仍为 `0/18 + 3` 个 native waves，已有 corpus 时请求/计划均不落盘且 corpus SHA-256 不变。
- 全仓检查确认旧性能分支的核心观测/缓存能力已经被后续主线实现覆盖，不能直接合入其旧业务修复。当前剩余结构性热点是 2,000 行以上的运行烟测编排脚本；它们应按 fixture/setup、planning、native execution、proof 四个责任边界渐进拆分，不与本轮 correctness/performance 修改捆绑。

## 历史记录 · 2026-10-15 current-authority 高风险小批次推进

- 放射性矿石修复合入后，milestone 按冻结顺序继续运行 `wild_tree_tapper_output_sample` 与 `monster_drop_sample`。两层均直接通过当前 Teacher、dispatcher、防篡改候选重建、原生动作、发货、睡眠结算与 rollout proof，未新增候选、编译器或执行器。
- 怪物掉落仍走 `mining.reach_depth -> CollectMonsterDrop -> executor.combat_monster -> executor.pickup_debris`，证明放射性矿石新增的 `CollectResourceOrArtifact` 分流没有回退现有战斗链。当前 evidence index 为 `4/26`，剩余 `22` 层，正式训练继续失败关闭。

## 历史记录 · 2026-10-15 current-authority 放射性矿石原生重验

- `radioactive_ore_node_sample` 的实时夹具已在普通矿井 99 层生成精确 `(O)95` 节点与保证掉落 `(O)909`。首次 current-authority 运行暴露两处同一链路缺口：`mining.reach_depth` 未携带精确采集意图时被现成梯子抢占；dispatcher 从同状态重建候选时又只为怪物掉落保留采集意图。
- 修复继续复用唯一的 `MiningFloorStepPlanner -> executor.mine_stone -> executor.pickup_debris` 链。放射性矿石意图映射到既有 `CollectResourceOrArtifact` 目标，按 `native_radioactive_ore_node / GameLocation.breakStone / (O)909` 绑定；防篡改重建和 selection 都要求同一组三字段，未跳过候选证据比较。
- 隐藏、静音、E 盘隔离运行 `runtime-full-shipment-evidence-radioactive_ore_node_sample-20261015-204018` 已完成原生破坏、延迟掉落拾取、运输箱、睡眠结算和 rollout proof。当前 evidence index 为 `2/26`，剩余 `24` 层；shared shipping evidence 保持有效，`formal_product_training_authorized=false`。

## 历史记录 · 2026-10-15 current-authority Sap 原生重验

- 当前预算与位置协议合入后，E 盘 9 类旧 `passed` 摘要均由当前严格验证器重新判定为 stale，不能作为 current-authority 证据；旧 Sap 证明也命中 daily time/energy drift。
- 新原生运行 `runtime-full-shipment-sap-current-authority-v7-20261015` 暴露并修复唯一共享 `executor.pickup_debris` 的两个执行问题：可进入掉落格必须真正走入；不可进入格旁必须保持朝向输入以进入原生磁吸范围；BFS 不得规划穿过本动作不会清除的可移除障碍。
- 修复后的同一运行原生获得并运输 `6` 个 `(O)92`，完成睡眠结算和 recurrence proof。当前源码 evidence index 精确验证 `native_wild_tree_chop_drop` 为 `1/26`，shared shipping evidence 为真；其余 `25` 层仍须重新执行原生动作，formal training 仍为 `false`。

## 历史记录 · 2026-10-15 #274 当前权威 force-full 收口

- 对冻结的 Full Shipment `26` 层证据执行当前源码扫描：Sap 的当前重建证明通过，其余 `25` 层分别因 location route、daily time/energy、calendar 或 Teacher preference 漂移而失效。离线 planning 重建已能重新通过 Teacher、commit 与 dispatch，但旧 execution receipt 缺少当前动作队列新增的玩家位置/tile 上下文，因此必须原生重跑，不能改写历史收据或放宽相等性。
- 修正当前权威重建链的真实缺口：shipping deposit verifier 使用现行 `target_location/target_tile_x/target_tile_y` 协议；仅在“当前矿层 + 精确站位 + 候选保守最大移动格数 + 计时校准”全部成立时，允许 purpose-limited mining snapshot 不携带全局 social route graph；跨图或其他缺证据仍失败关闭。
- recovery settlement 的持久状态投影现忽略明确的瞬时定位/缓存字段，同时保留未知持久字段失败关闭。自测临时工件改为进程级隔离，并修复 continuation 夹具的占位哈希、空出货端点及固定 20 金出售协议；这些修正使完整 Stage-One fixture 首次在当前严格哈希/原生收入链上通过。
- 验证通过：Bootstrap Release 构建、bootstrap hermetic、acquisition dispatch、Full Shipment 静态可编译性、完整 Stage-One、Core game-free `350/350`、Backend `229/229`、KnowledgeCompiler/LiveTrainingLoop Release 构建。未启动游戏；current-authority 仍为 `1/26`，#274 和正式训练准入保持关闭。

## 历史记录 · 2026-10-15 rollout proof 单次重验

- `AcquisitionRoutePortfolioRolloutProofBuilder.BuildVerifiedReceipt` 从 observe-only 改为 invocation-scoped deterministic certificate。身份覆盖 proof manifest、初始/续段 execution inputs、支持过渡、checkpoint、执行、终态和结算文件；任一传递内容漂移都会产生新键并重新完整验证。
- 缓存不保留整棵 mutable checkpoint graph，只保存 proof receipt、末端 checkpoint 路径与哈希。每个 consumer 仍重新读取独立末端 checkpoint 对象并核对状态、ledger、transition count 与完成状态；禁用 deterministic session 时恢复完整重验。
- Full Shipment runtime evidence index 增加 exact manifest/receipt content identity 的 unique-proof-first 映射。同一 proof 可服务多个合法 stratum，但每个 route occurrence、authority hash 与 stratum 归属仍逐项验证，26 层分母不合并。
- 该切片只完成 #274 的 rollout single-computation / unique-proof-first 核心，不代表全仓性能验收完成。canonical equality、缓存预算、表示生命周期与真实 A/B/C baseline 仍待后续；Money 仍为 `0/18`，正式训练仍为 `false`。

## 历史记录 · 2026-10-15 Money 首轮 work plan 初始化

- 新增 `Initialize-MoneyThresholdProductionWorkPlan.ps1`，不再要求人工书写首个空 work-plan request。它只调用现有权威 builder，并固定校验 `6` 个 criterion、`3` 个 partition、`18` 格分母和 `formal_product_training_authorized=false`。
- 未提供 corpus 时，输出必须是 native/comparison/complete 均为 `0`、原生波次恰为 `3`、comparison 波次为 `0`；提供 corpus 时仍由原 builder 完整重放验证。脚本不创建伪空 corpus，也不启动游戏。
- 该计划只用于给 `Invoke-MoneyThresholdNativeCollectionWave.ps1` 提供首轮调度；第一条 accepted native run 后，后续唯一输入改为 batch updater 生成的刷新计划。当前生产状态仍为 `0/18`。

## 历史记录 · 2026-10-15 Money comparison 波次身份交接

- 新增 `Initialize-MoneyRevenueComparisonWave.ps1`，用 `dataset_partition + source_row_id` 从 canonical work plan 定位唯一 comparison wave，核对 corpus row、source ID、native run summary 和缺失 criterion，再复用既有 `Initialize-MoneyRevenueComparisonWork.ps1`。不再人工搜索 summary 或抄 criterion。
- 新的 `money_revenue_comparison_wave_handoff.v1` 绑定 source work plan、corpus、run summary、comparison plan 的绝对路径与 SHA-256，以及 row/source/partition/criterion identity。可变 canonical work plan/corpus 会先复制并核对到 comparison work directory，后续其他波次刷新 batch 不会使在途 handoff 失效；completion 使用前仍重新哈希，漂移即失败关闭。
- `Complete-MoneyRevenueComparisonWork.ps1` 在提供 handoff 时只生成该波仍缺的 admission；可选 existing index 会按 `run_id + criterion_id` 精确归并为累计 v3 index，重复 key、非法 outcome override 或版本漂移均拒绝。它仍要求真实 rollout、ledger 和 terminal proof，不改变 `Money production evidence cells: 0/18` 或训练授权。

## 历史记录 · 2026-10-15 Money 原生波次自适应执行

- 新增 `build-money-threshold-native-outcome-wave-selection`：它先校验固定 `18` 格 work plan 和目标分区波次，再从实时权威 Revenue denominator 中选择当前可达的最高缺失阈值；最终动作仍由原有单阈值 selector、daily-plan compiler 与 executor 完成，没有第二套执行链。
- 计划中的最高阈值只表示本波理论上界，不再被误当成当前存档一定可达。源累计收入必须低于最低缺失阈值；候选只能关闭真实 crossed 的连续前缀，更高阈值留给 batch 刷新后的后续波次。若没有任何候选能推进，动作入队前失败关闭。
- 新增 `Invoke-MoneyThresholdNativeCollectionWave.ps1`，负责读取一个当前 work plan、调用既有隔离采集器、重建 canonical batch/work plan，并验证至少一个计划格由真实 native row 关闭。它不生成标签、不把计划当证据，也不改变 `Money production evidence cells: 0/18` 或正式训练状态。本轮未启动游戏或本地高负载测试。

## 历史记录 · 2026-10-15 Money 生产采证波次收敛

- `money_threshold_evidence_work_plan.v1` 继续保留六个权威阈值乘三个固定分区的 `18` 格分母，但新增从同一 verified corpus 派生的采集波次。原生波次按分区聚合尚缺 outcome 的阈值；比较波次按一条既有 native row 聚合尚缺 exact comparison 的 criterion。
- 空生产 corpus 不再被误读为 18 次游戏启动，而是显示 `3` 个条件性原生波次：每个分区各一波。单波只有在源累计收入低于该分区最低缺失阈值、且一个权威 Revenue outcome 到达最高缺失阈值时才可同时关闭多格；选择器仍逐项证明真实 crossed criterion。若当前候选不能满足上下界，必须拆波并重建计划，不能合并或伪造证据。
- 已有 native row 时，比较波次只复用一次 Revenue denominator 初始化，随后仍为列出的每个 criterion 建立独立 comparison admission。同一个 seed 的全部 admission 必须选择同一个 native outcome；若不同 criterion 的唯一优选不同，必须使用独立 seed run。
- 此调度压缩不改变 `Money production evidence cells: 0/18`，也不授权正式训练。未启动游戏、本地构建或高负载测试；验证交给远端质量门禁。

## 历史记录 · 2026-10-15 Money 生产包单次深验编排

- `Update-MoneyThresholdNativeOutcomeBatch.ps1` 现通过一次 `build-money-threshold-production-package` 调用生成 batch、canonical corpus、18-cell work plan 与可选 coverage request，不再启动三个进程并在同一轮内重复深验同一 corpus。
- 原生 run、comparison admission 与 trajectory 仍在 batch corpus 构建时完整重放一次；后续两步只能复用该进程内已经验证的对象，并必须同时匹配刚落盘 corpus 的绝对路径、文件 SHA-256 和行级不变量。独立命令与下游 coverage/reconciliation 仍在各自信任边界完整重放，未引入跨运行缓存，也未弱化 fail-closed 规则。
- 这项优化服务 #267 的 proof DAG 单次计算与 #274 的测量优先原则：它消除代码上可确认的重复调用，但没有猜测缓存预算、并发度或内存淘汰参数。没有启动本地游戏或高负载测试，Money production 状态仍为 `0/18`，正式训练仍为 `false`。

## 历史记录 · 2026-10-15 Money 完整原生轨迹与 corpus v4

- 新增 `money_threshold_native_trajectory_receipt.v2`。它从权威 Revenue request denominator 中选择一个待验 request，重建完整 terminal proof，并复用既有单候选 verifier 连接可选 acquisition/processing rollout、终端 sale/shipping 动作与 Revenue recurrence；不新增第二套执行器、收益验证或比较器。
- trajectory receipt 保存 source/final checkpoint、最终快照、concrete alternative、完整 Revenue prefix、terminal evidence SHA 与传递 artifact 哈希，固定 `teacher_preference_label_verified=false`、`formal_product_training_authorized=false`。manifest 中的 request ID 只指定验哪条原生轨迹，不构成偏好标签。
- Money corpus/request 升级为 v4，同一 canonical builder 现在接受隔离 sale episode 或完整 trajectory。comparison join 对 trajectory 强制匹配 selected concrete alternative、acquisition 标志、terminal evidence、完整 prefix 与 terminal artifacts，禁止把 rebound 或 shipping 截断成最后一次出售/结算。
- batch request v4 与 comparison index v3 可携带一个 comparison-selected trajectory；完成脚本会先构建并独立重放 trajectory，再建立 criterion-local join。若同一 seed 跨越的 criterion 选择不同 alternative，仍要求不同 seed run，不能把多个原生结果塞进一行。
- 单候选验证现在把已经验证的 source checkpoint 与 final snapshot 通过内部上下文交给 trajectory builder，避免重复读取同一 source/recurrence；同一次完成编排中，多个 criterion 选择同一 alternative 时也只构建一次 trajectory receipt，再分别执行 criterion-local join。
- 新增 `money_threshold_coverage_request_manifest.v1` 与 `build-money-threshold-coverage-request`。它先重放 Money corpus，再从现有 coverage request 中移除全部旧 Money source、保留其他 source，并加入恰好一个新 Money source；与非 Money source ID 冲突时失败关闭。batch updater 仅在显式传入 `-CoverageRequestTemplate` 时生成该请求，默认不会触碰 production coverage。
- 这些变更关闭表示与编排代码缺口，但没有采集 production evidence；Money 仍为 `0/18`，正式训练仍为 `false`。


## 历史记录 · 2026-10-14 Strategy-ledger 边界制品身份

- 新增 `build-strategy-ledger-boundary-capture` 与 `strategy_ledger_boundary_capture.v1`。每份制品以内容寻址 ID 绑定 `before_terminal_action` 或 `after_terminal_action`、request、concrete alternative、原生执行 `run_id`、快照 SHA/state/tick，以及账本 SHA/ID/revision/source-state/actor；调用方不能再只提交两条裸账本路径。
- Money Revenue terminal manifest/receipt 升级为 v2。证明器会从制品引用的真实快照和账本重新构造完整 capture 并逐 JSON 核对，同时要求 before/after 是不同文件、不同 capture ID、相同执行 run；before 仍必须等于 request 锚点，after 仍必须是独立账本文件且字节和 SHA 不变。
- hermetic 回归覆盖正常出售、前后 capture 互换拒绝、账本改写后旧 capture 失效及全量 reservation 拒绝；Bootstrap Release 构建 `0 warning / 0 error`。未启动游戏或外部测试机，`formal_product_training_authorized=false` 保持不变。
- 这关闭 #236 的外部 strategy-ledger before/after artifact identity 代码缺口。该 issue 只剩代表性真实快照的 wall/CPU 测量与证据索引验收，不能用 hermetic 计时替代。

## 历史记录 · 2026-10-14 Planner / Executor 路径与终端计时统一

- `RouteGridPathfinder` 现在是当前地图候选、执行器到达校验和未来日期距离场的共享 BFS 原语。当前候选按 `SnapshotEnvelope + StateHash` 缓存一份整数距离场；同一快照重复评估不会按候选重扫全图，执行器单目标校验仍采用命中即停止的 BFS。
- Full Shipment 当前态采集链已从 Manhattan 估算切换为同一 BFS 语义，覆盖动物产物、机器产出、地面采集、姜、灌木、果树、春葱、树上产物、树苔、野树砍伐、鱼塘、晶球、远古斑点及社区中心奖励。不可达站位在候选上游失败关闭，不再等到执行器阻塞。
- `AcquisitionCurrentRouteCandidate` 统一保留 `route_distance_tiles`、`terminal_action_ticks` 与计时投影模式。日预算只计算一次“到达精确站位 + 终端动作”；野树砍伐与放射性矿石/怪物目标也已接入该入口，移除了各自手写的路程扣减或当前时间直算分支。
- 定向回归证明绕墙场景的候选与执行器均采用 5 格 BFS 距离，且同一快照两次候选评估只构建 1 次距离场。Bootstrap Release 构建为 `0 warning / 0 error`，Core game-free 回归 `346/346`，acquisition dispatch 与 bootstrap hermetic 自测通过；未启动游戏或外部测试机。
- 本切片关闭 #236 的共享 route/arrival/terminal timing 代码缺口，但不据此授权正式训练；`formal_product_training_authorized=false` 保持不变。后续外部 strategy-ledger 边界身份已由上一节补齐；#236 仍需在代表性真实快照上补一次 wall/CPU 测量后再关闭性能验收项。

## 历史记录 · 2026-10-14 Money evidence work plan

- 新增 `build-money-threshold-evidence-work-plan`，从权威六档 Money criterion 与固定 train/validation/test 分区生成 `6 x 3 = 18` 个不可增删的工作格。输入可以不带 corpus（全部待采）或带一份可重放验证的 Money corpus v3。
- 每格分别统计 native outcome 与 exact-denominator Teacher comparison，不用 method 汇总掩盖 criterion/partition 缺口。输出保留命中的 row ID，并只给出三种状态：完整、已有 outcome 但缺 comparison、两通道都缺。
- work plan 只负责调度缺失证据，不能生成 episode、提升 fixture 或授权训练。当时本机 E 盘 Money 目录只有 self-test；hermetic 示例因此是 `native 1/18`、`comparison 0/18`、`complete 0/18`，正式覆盖仍不变。

## 历史记录 · 2026-10-14 Money criterion comparison join

- `money_threshold_teacher_corpus_request/corpus` 已升级为 v3。它仍以原生 ShopMenu sale episode 为 native outcome 唯一来源，但现在允许每个 crossed criterion 显式附带一份 Revenue comparison admission；没有 admission 的行继续只计 native outcome。
- `money_revenue_comparison_admission_receipt.v2` 在既有 exact-ID 分母和严格 Pareto 结果之外，保留已验真的 native terminal artifact 列表。Money join 会重新构建 manifest/receipt，并要求 criterion、阈值、唯一优选、完整 rejected 集合、save/player、首条 Revenue transition，以及 sale episode、before/after snapshot、execution result 的路径和 SHA-256 全部一致。
- Goal Method adapter 只为完成上述 join 的 criterion/partition 增加 Teacher comparison coverage；原生成功本身仍不能自我标注。hermetic 正例绑定 49,990 -> 50,010 的优选 sale，删除选中 episode artifact 的负例会失败关闭。
- 该切片打通了真实比较凭据进入 Money criterion view 的代码路径，但没有制造 production 三分区数据；正式状态仍为 `2/19`、根方法 `1/11`、`formal_product_training_authorized=false`。

## 历史记录 · 2026-10-14 Money threshold native-outcome isolation

- `money_threshold_teacher_corpus_request/corpus` 升级为 v2。raw corpus 现在只证明原生 ShopMenu sale 的 before/after、收益增量与权威阈值跨越，不再把“已经执行的出售”反推成 Teacher preferred action。
- row 与 criterion 的 `teacher_comparison_verified` 固定为 `false`，preferred/rejected 字段固定为空；`native_terminal_outcome_verified` 保持真实。任何 raw corpus 混入 Teacher 声明都会在构建、重放或 coverage adapter 处失败关闭。
- Goal Method coverage 的 raw Money adapter 现在只计入 native outcome partition，Teacher comparison partition 保持为空。当时后续计划必须用上一切片的 exact-ID comparison admission 与对应原生 episode 显式 join，才能恢复 criterion-local comparison coverage。
- 新增轻量 hermetic 阈值跨越回归：49,990 -> 50,010 的原生 sale 只产生 `money_50000` native outcome，不产生偏好。正式状态与 `2/19`、`1/11`、`formal_product_training_authorized=false` 均不变。

## 历史记录 · 2026-10-14 Revenue exact-ID comparison admission

- 新增 `build-money-revenue-comparison-admission`、`money_revenue_comparison_admission_manifest.v1` 与 receipt。
  admission 会重建 terminal execution proof 与 counterfactual request catalog，要求 request、terminal proof、terminal evidence 三组 ID 完全相等，并按 request 顺序推导 concrete alternative ID；调用方不能再自报比较分母。
- direct alternative 只消费已验证 terminal opportunity cost；rebound alternative 复用既有 comparison artifact verifier 重建 acquisition rollout、reservation 与 acquisition opportunity cost，随后和 terminal cost 聚合。没有新增第二套 rollout、Revenue recurrence 或 Pareto 比较实现。
- 每条 recurrence 必须连接对应 terminal action，并保持 source save/player/total-money identity。完整分母进入现有 reserve-aware strict-Pareto comparer；只有唯一前沿候选时，receipt 才设置 `production_teacher_preference_label_eligible=true`，不可比较前沿保持无标签。
- hermetic 用同一 source state 构造两条收益相同、真实终端耗时分别为 1/2 游戏分钟的直接出售证明；较快候选被唯一选中，删除任一 evidence 会失败关闭。Release 构建 `0 warning / 0 error`，未启动游戏或外部测试机。
- 该层关闭 Revenue 当时 concrete denominator 的比较准入，但不冒充 criterion 三分区原生 outcome admission。下一固定切片是让 Money method-family 的独立 train/validation/test criterion view 消费该 preference 与原生结果；正式状态仍为 `2/19`、根方法 `1/11`、`formal_product_training_authorized=false`。

## 历史记录 · 2026-10-14 Revenue terminal execution proof

- 新增 `build-money-revenue-terminal-execution-proof`、
  `money_revenue_terminal_execution_proof_manifest.v2` 与 receipt。manifest 必须对 authoritative request catalog
  中每个 request ID 恰好提供一份证明，漏项、重复项和额外项均失败关闭；request catalog 会从源 manifest 重建并逐 JSON 核对。
- 每份证明从 request 固定的 terminal snapshot 与 terminal strategy-ledger anchor 开始。可选 route transition 只能复用现有
  queue/receipt validator 和资源中性移动、连接器、交互、菜单及等待动作；版本化 persistent boundary 必须证明路由期间库存、货币、精力和持久状态未变。
- 终端动作复用现有原生 shop-sale episode 或 shipping-deposit verifier，并强制接入现有 Revenue recurrence receipt。
  商店出售额外核对透明 material graph 的精确槽位、物品、品质、单价与数量减少；出货仍要求精确入箱队列并以次日原生结算作为 recurrence 首跳。
- before/after 必须各自提供 `strategy_ledger_boundary_capture.v1`，绑定边界阶段、request、候选、原生执行 run、快照与账本内容身份。before ledger 必须就是 request 推导出的权威终端账本；after ledger 必须是独立路径但字节和 SHA 完全相同。
  当时玩家库存槽位通过共享 `MaterialSupplyProjection` 与全部 active reservation 联合投影，只有未预留数量足以覆盖出售/入箱数量时才准入。
- 终端总耗时取真实 receipt 的起止 tick，并统一通过 `GameClockBudgetPolicy.TicksToGameMinutes` 转换；没有复制 BFS、商店、出货、账本或执行器实现。
  Release 构建为 `0 warning / 0 error`，轻量 hermetic 覆盖未预留商店出售正例和全量预留拒绝，未启动游戏或外部测试机。
- 本层得到的是完整 terminal proof denominator，不是生产偏好。下一固定切片是以这些 evidence 的 request ID 为唯一分母，构建严格 comparison admission，随后才可判断
  `earn_money` 是否获得 production Teacher preference。正式状态保持 `2/19`、根方法 `1/11`、`formal_product_training_authorized=false`。

## 历史记录 · 2026-10-14 Revenue counterfactual proof requests

- 新增 `build-money-revenue-counterfactual-proof-requests`、
  `money_revenue_counterfactual_proof_request_manifest.v1` 与 catalog。它重建 concrete alternative catalog，绑定同一
  source snapshot 的 strategy ledger，并为每个 concrete ID 恰好生成一份不可由调用方增删的采证请求。
- direct 候选明确不要求也不允许伪造 acquisition rollout；rebound 候选保留已验证 rollout manifest/receipt。
  sale 请求要求原生 shop-sale proof，shipping 请求同时要求原生 deposit 与次日 settlement；两类都要求完整 Revenue
  recurrence。
- 每个请求现已携带不可由调用方填写的 terminal strategy-ledger 锚点。direct 候选必须逐字段沿用 source ledger；
  rebound 候选会重建 acquisition rollout，并从最后一笔已验证 settlement 反查 ledger path/hash/ID/revision/source state。
  终端账本与 rollout receipt 或 terminal snapshot 任一漂移都会失败关闭。
- 审查中确认现有 comparison 只聚合 acquisition opportunity cost，不能把最终路由、出售或入箱动作默认为零成本。
  外部 strategy ledger 的保留项也不能只靠游戏内保护标记推断。因此 8/8 请求统一强制
  `terminal_opportunity_cost_proof_required=true` 与 `terminal_reservation_claim_proof_required=true`。在共享时序/成本和
  储备安全证明接入前，已有 comparison 只能保持非生产证据，不能提升 Teacher 标签。
- hermetic 覆盖 `8 concrete -> 8 proof requests`、`3` 份 rebound 保留 rollout、`5` 份 direct 不伪造 rollout、
  `5` 条 sale 与 `3` 条 shipping 证明形态，以及陈旧 ledger 拒绝。Release 为 `0 warning / 0 error`，未启动游戏。
- request denominator、采证调度与终端账本起点已权威，但执行结果和账本前后连续性证明仍为空；正式状态继续为 `2/19`、根方法 `1/11`、
  `formal_product_training_authorized=false`。下一固定切片是实现共享 terminal opportunity-cost 与 reservation proof，
  再让每个请求绑定 native recurrence receipt，最后使 comparison receipt 的 ID 集与 request catalog 完全相等。

## 历史记录 · 2026-10-14 Revenue concrete alternative 联合分母

- 新增 `build-money-revenue-concrete-alternative-denominator`、
  `money_revenue_concrete_alternative_denominator_manifest.v1` 与 catalog。构建器会重新构建路径骨架、terminal rebind
  receipt 及其全部叶子证据，再将 direct sale/shipping 骨架和 acquisition/processing 后的 rebound 候选合并为一个
  不由调用方声明 ID 的具体候选分母。
- direct 候选严格复用源 candidate 身份和源 state；rebound 候选严格复用已验证的新槽位、terminal state、rollout
  ID/manifest/receipt 与 terminal candidate。两类候选进入同一 schema，但保留不同的 acquisition、shop-sale、shipping
  deposit 和 shipping settlement 证明要求，不会用空 rollout 冒充直接销售，也不会把直接出货当成已隔夜结算。
- hermetic fixture 中 7 个路径骨架稳定展开为 `5 direct + 3 rebound = 8 concrete`；同一新物品的两个商店路径均保留，
  删除任一 rebound 骨架证据会失败关闭。Release 构建为 `0 warning / 0 error`，快速密封回归通过，未启动游戏。
- 该 catalog 现在可以声明 `current_concrete_denominator_authoritative=true`，含义仅限当前状态下的候选身份完整。
  尚未为每个 concrete ID 生成隔离 counterfactual 收益 recurrence，因此 production label eligibility 与
  `formal_product_training_authorized` 仍为 `false`，正式状态仍是 `2/19`、根方法 `1/11`。
- 下一固定切片：由 concrete catalog 生成逐项 proof request，并使 Revenue comparison proof 精确消费该目录 ID；
  direct 候选走原生 sale 或 deposit+settlement，rebound 候选先验 acquisition rollout 再走相同终端证据，不再接受
  comparison manifest 自报候选全集。

## 历史记录 · 2026-10-14 Revenue fresh terminal 候选重绑定

- 新增 `build-money-revenue-terminal-candidate-rebind`、
  `money_revenue_terminal_candidate_rebind_manifest.v1` 与对应 receipt。manifest 必须对路径骨架中每个
  `requires_fresh_terminal_candidate_rebind` 项恰好提供一份完整 acquisition rollout proof；漏项、重复项和额外项
  均失败关闭。
- 构建器会重建 alternative denominator、完整 rollout 与持久化 receipt，再按骨架的原始 `source_candidate_id`
  自动定位唯一已执行 transition。调用方不能声明产物、槽位、价格或终端候选；这些字段只从已验证 fresh receipt、
  前后透明快照和现有 `MoneyRevenueCandidateDenominatorBuilder` 得出。
- 库存绑定按精确品质和槽位计算。旧库存中的同类物品不会被误算为本次产物；若同一新增槽位可由多个商店收购，
  所有不同商店/价格/路线候选都会保留为不同 concrete alternative，而不是任意只取一个。sale 与 shipping 继续使用
  原候选生成器，没有新增第二套售价、保护、BFS、编译器或执行器逻辑。
- Release 构建为 `0 warning / 0 error`，快速 hermetic 已覆盖旧槽排除、一个新槽展开两个商店候选、同槽出货以及
  receipt 净增量漂移拒绝；未启动游戏，也未运行高成本完整 Stage-One fixture。
- 该 receipt 只证明“所有待重绑定骨架均已得到可重放的具体终端候选”，尚未把直接终端骨架与 rebound 结果合并成
  最终 concrete alternative denominator。因此正式状态保持 `2/19`、根方法 `1/11`、
  `formal_product_training_authorized=false`。下一固定切片是构建 direct + rebound 的唯一联合目录，并让 comparison
  proof 从该目录生成候选集合，而不再接受调用方自报 alternatives。

## 历史记录 · 2026-10-14 Revenue 原生出货入箱证据

- 新增唯一 `NativeShippingDepositEvidenceVerifier`，把 `executor.ship_inventory_item_to_bin` 的末队列项、fresh
  回执、精确库存槽、物品/品质/数量/实时单价、Farm 箱端点、待结算金额、现金、累计收入和日期绑定为同一证据。
- 修复共享钱包多物理出货箱重复展示同一内容时的重复计数风险：正式证据只读队列绑定端点；旧 Stage-One
  兼容读取要求同 scope 端点完全一致并只计一次。
- 密封测试覆盖双共享端点正例，以及错槽位、错单价、错端点、入箱即到账、changed-facts 与快照不一致五类负例；
  Bootstrap Release 为 0 warning / 0 error，关键密封路径通过，未启动游戏。
- 状态不升级：该切片只闭合入箱，隔夜结算复用既有共享验证器；正式覆盖仍为 `2/19`、根方法 `1/11`，
  `formal_product_training_authorized=false`。
- 下一固定切片：对每个 acquisition/processing rollout 的 fresh terminal snapshot 重建 Revenue 候选目录，将实际
  产物唯一重绑定到路径骨架要求的 `economy.sell_items` 或 `economy.ship_items` 终端，再生成具体 alternative 分母。

## 历史记录 · 2026-10-14 Revenue 候选到结算路径骨架

- 新增 `build-money-revenue-alternative-denominator` 与 `money_revenue_alternative_denominator_catalog.v1`。它先重建并逐 JSON 核对当前候选目录，再沿权威 `earn_money` 依赖图把每个 policy candidate 展开到唯一累计收益里程碑；只枚举简单路径，因此不会进入 recurrence 回边形成无限展开。
- 获取/加工候选会自然分叉为 shop-sale 与 shipping 两条路径，机器容量会自然经过 load/collect；直接 sell/ship 候选只保留对应终端。路径、edge kind、源候选 SHA 与 proof shape 全部进入确定性 alternative ID，没有手写第二套行为路由表。
- 该层只证明 `alternative_skeleton_denominator_complete`。直接出售/出货的具体终端身份已知；采集、加工、钓鱼等动作必须在结果快照上重新绑定实际可卖/可出货候选，尤其不能把随机鱼种提前当成已知结果。因此当前 fixture 正确输出 `current_alternative_denominator_authoritative=false`，并记录 fresh terminal rebind 数量。
- Release 构建与快速 hermetic 通过；覆盖采集候选的双结算分叉、直接 sell/ship 证明形态、断边失败关闭。真实 13 节点图烟测正确识别 `settled_milestone_ladder`；临时快照透明字段不全时仍失败关闭。下一块是 fresh terminal rebind 与 shipping deposit 原生 proof，而不是把原子候选直接改名成 rollout。

## 历史记录 · 2026-10-14 Revenue 当时快照权威候选分母

- 新增 `build-money-revenue-candidate-denominator`；当前 schema 为 `money_revenue_candidate_denominator_catalog.v2`。构建器从 `goal_method_dependency_expansions.v1` 的 `earn_money` 节点自动发现全部带 `option_id` 的 policy transition 与确定性 support transition，再对同一透明快照只调用一次现有 `CandidateOptionAvailabilityEvaluator`；没有复制售价、保护、路径、编译器或执行器规则。v2 同时保留服务时间窗、allowed-now/allowed-today、等待成本、gate 原因、库存/货币及 Full Shipment 相关候选语义，防止采证调度与反事实比较漏字段。
- 目录同时保留 event/economic 两类候选、滚动 continuation、参数、阻塞原因和逐候选内容哈希。节点漏评、重复候选 ID、缺失透明字段、候选 kind 既不直接匹配依赖图也不绑定 continuation 时均失败关闭；阻塞候选不会被静默删除。
- 权限被拆成三层：`current_snapshot_denominator_authoritative` 只说明当前快照的依赖节点和候选枚举不重不漏；`current_comparison_denominator_ready` 还要求至少两个当前可比较候选；`long_horizon_denominator_authoritative` 继续受 fresh-save schedule、counterfactual opportunity cost 和 crop route 三个现有 blocker 约束。目录不含 rollout proof，因此 production Teacher label 与正式训练授权始终为 `false`。
- Release 构建 `0 warning / 0 error`，快速 `self-test-bootstrap-hermetic` 通过；测试覆盖直接候选、跨图 staged continuation、漏 option 与未绑定 kind。未启动游戏或运行高成本 Stage-One fixture。该目录是替代方案路径展开的源分母，不能跳过动作后 fresh terminal rebind 直接绑定整条 rollout。

## 历史记录 · 2026-10-14 Revenue 候选比较可重放工件

- 新增 `build-money-revenue-alternative-comparison-proof`、`money_revenue_alternative_comparison_proof_manifest.v1` 与对应 receipt。每个候选不再由调用方直接拼接 prefix/cost/claim，而是必须引用完整 acquisition rollout manifest+receipt 和 Revenue recurrence manifest+receipt；两张 receipt 都必须逐项等于控制器重算结果。
- acquisition rollout 的每个已执行 transition 都重新绑定唯一 route occurrence、确定性 opportunity-cost report、该 route 的 reservation claim set 与已验原子 portfolio commit。所有 transition 的非标量成本由共享 `AcquisitionOpportunityCostVectorAggregator` 聚合，原 portfolio admission 同步复用该实现，没有出现第二套成本算法。
- 比较候选必须共享完全相同的起始 state hash、snapshot SHA-256、strategy-ledger SHA-256 与 ledger revision；rollout 最终 snapshot 的完整 Money checkpoint 必须等于 Revenue prefix 的第一 checkpoint，且 acquisition 过程不得暗中增加累计收入。篡改持久化 Revenue receipt 会失败关闭。
- 当时 manifest 仍是调用方声明的候选集合，尚无权威 Revenue alternative enumerator 证明“所有可行赚钱方案均已列入”。因此 receipt 明确输出 `candidate_denominator_authoritative=false`、`production_teacher_preference_label_eligible=false`、`formal_product_training_authorized=false`；正式状态仍为 promotion-grade `2/19`、root method `1/11`。下一块固定为从透明快照与权威 acquisition/processing route 生成 Revenue 候选全集，再让本工件消费该权威 denominator。
- 本机只执行低成本验证：GoalConditionedBootstrap Release `0 warning / 0 error`，`self-test-bootstrap-hermetic` 退出码 `0`。完整 Stage-One fixture 历史开销约 30 分钟、183 GiB 读取，本轮没有在本机重跑；新增成功链回归已挂接在该完整 fixture，但轻量 PR CI 不执行这一高成本项，因此本轮不能宣称完整工件成功链已动态通过。未启动游戏。

## 历史记录 · 2026-10-14 Recovery -> settlement 持久状态边界

- 经核对，远端最新 issue 仍为长期总纲 `#236`，没有更新编号。其 action-origin debris 与 acquisition output sufficiency 两项已由共享实现覆盖；recovery 只比较 Full Shipment 进度、日期和 shipping bin 的意见仍然成立。
- 新增版本化 `full_shipment_recovery_settlement_boundary.v1` 投影。连接器移动、菜单、动画和场景候选上下文是显式瞬态白名单；玩家背包、四类原生货币视图、累计收入、日期、shipping bin、world progress、quests 及其余未声明字段进入稳定指纹，未知字段默认按持久字段处理。
- `FullShipmentRecurrenceProofBuilder` 现在同时验证 prior -> before 边界连续性以及 before -> after 的背包、货币和总持久状态不变性。合法移动继续通过，注入 Money 或 inventory 漂移会失败关闭。GoalConditionedBootstrap Release 构建 `0 warning / 0 error`，聚焦 `self-test-full-shipment-settlement` 通过；未启动游戏或全量回归。
- 该切片修复快照内持久状态漏洞，但不宣称关闭整个 `#236`：controller 外部 strategy ledger 的 before/after 工件身份、统一 route/timing authority 与实测 profiling 仍是后续项。正式状态保持 promotion-grade `2/19`、root method `1/11`、`formal_product_training_authorized=false`。

## 历史记录 · 2026-10-14 Revenue recurrence 可重放工件

- 新增 `build-money-revenue-recurrence-proof` 与 `MoneyRevenueRecurrenceProofBuilder`。manifest 的每条边只能选择 `native_shop_sale` 或 `native_shipping_settlement` 一种证明形态；前者重新验真 shop execution episode，后者重新验真 sleep queue、before/after snapshot 与 queue receipt，混填或缺字段直接拒绝。
- receipt 保存 manifest 哈希、每条边四个源工件的绝对路径与 SHA-256、重新投影的 Money progression 以及最终连续前缀。hermetic 已把 20g 即时销售与 300g 隔夜结算组合为 320g 两边工件，并证明相邻 checkpoint 连续；伪造 selected candidate 无法通过。
- 这关闭了“内存 prefix 无法重放”和“各边真实但拼接时偷换状态”两个缺口。它仍是合成 fixture，不增加 production coverage；下一块让 Revenue comparison manifest 引用该 receipt、确定性 opportunity report、strategy ledger/commit 与隔离 counterfactual rollout。

## 历史记录 · 2026-10-14 Revenue 储备感知候选比较

- 新增 `MoneyRevenueAlternativeComparer`，只接受来自权威 `GrandpaEvaluationGoalEvaluator.MoneyThresholds` 的 criterion、完整且不重不漏的候选 ID 分母、同一收益前缀起点及同一 reservation ledger revision。调用方不能自报任意收益目标。
- 每个候选重新验证 Revenue 前缀，并要求 acquisition 既有的非标量时间/能量/材料/货币成本向量与原子 reservation claim set 数量一致；陈旧状态、替换他人储备、claim/成本漂移均 fail closed。目标阈值满足是第一层权威终态，同一终态层内仅在收益进度、完成日和储备感知成本上存在唯一严格优势时才生成偏好；不可比候选保留在前沿，不读取 learner score。
- hermetic 覆盖唯一达标候选、收益/木材/时间 trade-off、候选漏项、陈旧 claim 和数量漂移。Revenue prefix 现已有独立可重放 receipt，但 comparison 尚未绑定真实 acquisition route、ledger commit 与隔离 counterfactual rollout，因此 `formal_product_training_authorized=false` 和正式覆盖率不变。

## 历史记录 · 2026-10-14 Revenue 连续前缀

- 新增 `MoneyRevenueRecurrenceVerifier`，把已验真的即时商店销售与原生隔夜发货结算编排为同一 save/player 的类型化收益前缀。相邻边必须完整复用上一边的 `MoneyRevenueCheckpoint`，因此状态哈希、日数、现金、累计收入和 shipping scope 不能在边界间静默漂移。
- 验证器不会信任调用方构造的公开 record：每条边都会重新核对 kind、正收益 delta、日推进、pending shipping 消耗、Year 3 截止和聚合总额。hermetic 已覆盖 320g 双类型前缀，并拒绝断链哈希、伪造 delta 与截止日外边。
- 该类型证明“收益边可以连续组合”，但尚未证明从新存档取得这些边。后续 reserve-aware comparison 已接入现有非标量成本与 reservation claim 语义，但真实 acquisition/processing 支持转移和 counterfactual 工件仍待绑定。正式状态仍为 promotion-grade `2/19`、root method `1/11`、`formal_product_training_authorized=false`。

## 历史记录 · 2026-10-14 通用原生 shipping-settlement evidence

- 单项原生 sleep queue、queue execution receipt 与隔夜 Money progression 现由 `NativeShippingSettlementEvidenceVerifier` 联合验真；`executor.sleep` 和带 `execution_option_id=executor.sleep` 的 `recovery.stabilize_day` 继续复用同一执行器。
- Full Shipment 普通与终局 settlement receipt 已改为消费该共享入口，不再分别只校验“睡了一天”和 collection 变化而遗漏实际金额归因。合法发货收益必须与前夜去重后的 pending shipping value 精确一致。
- hermetic 同时覆盖有效 sleep receipt 与非 sleep queue 拒绝。历史工件若缺少 Money 或完整 shipping-scope 字段将 fail closed 并需重采；不会降低或伪造现有 production 分子。
- 即时 sale 与隔夜 shipping 两种收益边至此都有唯一原生验真入口。当时后续计划把它们编排成同 actor、状态哈希连续、截止 Year 3 的 Revenue recurrence prefix，并接入 reserve-aware alternative 比较。

## 历史记录 · 2026-10-14 通用原生 shop-sale evidence

- 原先位于 Money 阈值 corpus 内部的 execution episode/header/hash/queue/result 验真已下沉为唯一 `NativeShopSaleEpisodeVerifier`。阈值 corpus 现在只负责在通用 sale progression 上派生 crossed criterion，不再复制执行证据验证。
- 通用证据保留 `shop_id`、`qualified_item_id`、inventory slot、quantity、unit price、total value、queue item、before/after/result 哈希及 Money progression；结构化 command 中任一字段缺失、重复、冲突或总价不等于实际收入增量都会 fail closed。
- 该入口不要求一次销售必须跨 Grandpa 阈值，因此后续 Revenue recurrence 能记录全部有效销售边，而阈值 Teacher coverage 仍只接收真实跨线视图。现有伪造 quantity 回归仍通过，正式覆盖率不变。

## 历史记录 · 2026-10-14 Money progression 共享验真基础

- `MoneyRevenueProgressionVerifier` 统一投影 `save/player/day/money/totalMoneyEarned` 与 shipping settlement scope，现有 Money 阈值 sale corpus 已改为复用该入口，不再保留第二套金额/天数/actor 读取逻辑。
- 商店即时结算必须满足当前现金与累计收入同额正增长、同日、同 actor，且 pending shipping value/signature 完全不变；这阻止一次销售证据混入隔夜发货收益。
- 原生发货结算必须恰好跨一天，现金与累计收入增量都严格等于前夜 pending 值，结算后 pending 为零。多个物理 shipping-bin endpoint 若属于同一 shared/personal scope 只计一次，并且所有副本必须一致，避免重复端点把收益翻倍。
- hermetic 已覆盖即时销售、发货结算、同 scope 三端点去重、pending 漂移拒绝和重复计价拒绝。该层只是 `earn_money` recurrence 的共享原生状态验真基础，不会单独提高 production coverage；当时后续计划仍是把 acquisition/reserve/processing/sale/shipping/settlement 组合成连续的 fresh-save recurrence。

## 历史记录 · 2026-10-14 Money 阈值族判据级证据边界

- Teacher coverage 不再假设“共享同一 root method 的 criterion 必须拥有完全相同证据”。旧实现会让任意一个 Money 阈值样本污染全部六档；现改为 method 级共享 progression 加 criterion 级独立证据，方法汇总只取所有所属 criterion 的共同三分区覆盖。
- 新增 `money_threshold_progression_corpus` 强类型入口。请求只能提交原生执行 episode，不能提交 criterion ID、阈值或分区标签；构建器只接受 `executor.sell_shop_item` 的 applied/verified 原生回执、fresh 前后快照、完整哈希绑定，以及 `player.money` 与 `player.total_money_earned` 完全相同的正增量。
- 六个阈值现在由 `GrandpaEvaluationGoalEvaluator.MoneyThresholds` 单点定义。一个真实 progression 可以派生多个 crossed-threshold view，但每个 view 独立保存 Teacher goal context、终态断言、comparison receipt 和 train/validation/test 覆盖，不允许“高阈值通过即自动放行低阈值”。
- 隔离自测位于 `E:\StardewAI-TestLab\artifacts\goal-method-money-threshold-selftest-20261014`：三份独立 save/player/day 样本只跨越 50,000，报告证明仅 `money_50000` 获得三分区证据，其余五档仍为空；伪造 criterion 归属会在重放时失败关闭。
- 该自测是结构与验真测试，不是 production native evidence。正式状态仍为 promotion-grade `2/19`、root method `1/11`、`formal_product_training_authorized=false`。下一施工单位是闭合 `earn_money` 的 fresh-save 收益 recurrence 与 opportunity-cost Teacher 依赖，再采集生产三分区证据。

## 历史记录 · 2026-10-14 正式训练准入重算与下一施工单位

- 已使用当前 frontier 输入和当前代码重新生成三分区 acquisition 语料、checkpoint 与支持转移终端谱系，并完成只读 `build-goal-method-formal-training-admission-reconciliation`。报告位于 `E:\StardewAI-TestLab\artifacts\formal-training-admission-current-source-v1-20261014\formal-training-admission-reconciliation.json`；旧 2026-09-28 冻结语料因日历解析漂移已被拒绝，不能继续代表当前状态。
- 当时机器结论为 `2/19` promotion-grade criterion、`1/11` coverage-ready root method；语料为 `3` 个来源、`9` 行，train/validation/test 各 `3` 行、各 `2` 个 pair，重算 validation/test accuracy 均为 `1.0`。支持转移终端谱系已从旧记录的 `5/5` 更新为 `6/6`。
- 完整性、语料重放、三分区、checkpoint 绑定、holdout 重算、支持谱系与叶制品权限隔离均通过。Teacher 仍阻塞于 `dependency_graph_incomplete:15` 与 `evidence_not_connected:2`。全局 option 对账确认三个清障 primitive 原本只是未在 capability source 绑定既有 `EVD-112/189/212`；绑定后相关透明读取、原生运行和 five-gate 告警全部消失，且三者仍为 `EvaluationOnly`、不进入策略训练 allowlist。
- 三个社交高层 option 现已显式登记到唯一 `DailyPlanCompiler -> executor.social_interact` 管线：候选种类白名单覆盖跨图 connector、菜单恢复、有限重试与 talk/gift/partnership 终端，能力源同步声明 step+parameter compiler 和 internal execution。直接高层 action 仍失败关闭，求婚/室友等确认规则不变。新 matrix/lowering 重算后 `referenced_option_execution_inventory=true`、`runtime_product_promotion_ready=true`，下游 promotion blocker 已为零；正式报告为 `E:\StardewAI-TestLab\artifacts\formal-training-admission-current-source-v1-20261014\formal-training-admission-reconciliation-social-pipeline.json`。
- 训练授权继续保持 `formal_product_training_authorized=false`。下一施工单位按 #224 的 method-family 顺序进入 Money 单调阈值族：复用一个 progression、候选分母、Teacher source adapter 与三分区管线，为六个 money criterion 生成独立目标上下文和终态视图；不得按六个 criterion 复制六套实现。
- Goal-method corpus 重放失败现在会报告精确 source index、`rollout_id` 与 proof manifest 路径，同时继续失败关闭；这用于快速定位漂移，不放宽 digest、回放或授权规则。

## 历史记录 · 2026-10-14 Full Shipment 运行时证据分母闭合

- Full Shipment 权威清单保持 `154/154` 个需求、`641` 条来源、`26` 个执行签名层；严格 production evidence index 已达到 `26/26`，共享 shipping/settlement 证明有效，缺失层为 `0`。索引根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v38-20261014`。
- 最后一层 `tree_moss_harvest_sample / full_shipment:item:Moss / (O)Moss / native_tree_moss_harvest` 已闭合。唯一动作链为 `foraging.harvest_tree_moss -> executor.clear_obstacle -> executor.pickup_debris`：镰刀仍由原生工具周期执行，落地物继续复用共享自然拾取执行器，没有新增第二套采收或拾取系统。
- 正式树苔 proof 位于 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-tree-moss-harvest-v7-20261014`。队列精确包含移动、一次清障和一次拾取；取得 `(O)Moss x1`，冻结源存档 SHA-256 保持 `5af603dbb99bafadbb14580952b6e4b8c0d9e257963174759f53fbc8b2d671cc`。
- 重建历史层时发现旧 `machine_item_query_output_sample` 已被当前路线身份规则淘汰。处理层现在把“同物品但具有完整、不同权威来源”的当前机器产出判为确定性 route miss；只有来源缺失或不完整时才以 `machine_active_output_source_unresolved` 失败关闭。新的 v3 proof 位于 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-machine-item-query-output-v3-20261014`。
- `formal_product_training_authorized=false` 仍是正确结果。`26/26` 只闭合 Full Shipment 内部运行时样本分母；当时后续固定计划是运行并修正现有 `build-goal-method-formal-training-admission-reconciliation`，从当时 19 项爷爷评分目标、语料、三分区、checkpoint、支持转移和所引用执行器生成真实阻塞清单。任何叶子证据均不得自行授权训练。

## 历史记录 · 2026-10-14 Full Shipment 标准批次：共享机器产出族闭环

- `machine_output_sample`、`machine_flavored_output_sample`、`machine_item_query_output_sample` 与 `solar_panel_output_sample` 已从冻结源存档的隔离副本完成原生采证。四层都复用唯一的 `farm.collect_machine_outputs -> executor.collect_machine_output` 链，没有复制候选、编译器或执行器。
- 当前机器候选以完整 `route_kind + source_id + qualified_item_id` 保留权威来源身份；同一已物化产出存在多个等价权威描述时，只让字典序 canonical 来源进入 Teacher 前沿，透明桥仍保留全部来源。数量与品质统一通过 `AcquisitionOutputProof.CanSatisfy` 验证，缺失证明继续失败关闭。索引缓存 schema 升至 `v13`。
- 太阳能板不再依赖静态占位：只有透明快照存在精确、可收取的当前太阳能板产出时，才进入日历、位置、设施、资源、加工、随机性、日内预算与 dispatch 轴；旧快照或来源漂移不会生成候选。
- 原生收取验证器现在区分“产物清空”和“原生立即排入下一生产周期”。后一种成功必须同时满足交互成功、目标物品实际入包、机器退出可收获态、下一周期计时为正且 held object 已更换；树液采集器仍额外要求同格原生已安装树液采集器树。蜂房回放据此不再被误判为失败。
- 四个严格 proof 根依次为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-machine-output-v5-20261014`、`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-machine-flavored-output-v2-20261014`、`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-machine-item-query-output-v1-20261014`、`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-solar-panel-output-v1-20261014`。冻结源存档 SHA-256 均保持 `5af603dbb99bafadbb14580952b6e4b8c0d9e257963174759f53fbc8b2d671cc`。
- 严格 production evidence index 已集中深验到 `25/26`；唯一剩余层为 `full_shipment.runtime_sample.21.native_tree_moss_harvest`，`formal_product_training_authorized=false`。索引根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v36-20261014`。Core `2680/2680`、Backend `229/229`，Bootstrap 与 RuntimeTestHarness Release 均为 `0 warning / 0 error`。
- 当时后续固定计划是闭合 `tree_moss_harvest_sample / full_shipment:item:Moss / (O)Moss / native_tree_moss_harvest`。必须复用现有 `foraging.harvest_tree_moss` 与共享移动/自然拾取链；通过后重建 `26/26` 索引，再评估正式训练准入，不能仅凭样本通过直接授权训练。

## 历史记录 · 2026-10-14 Full Shipment 标准批次：原生鱼塘产物闭环

- `fish_pond_output_sample` 已从冻结源存档的隔离副本完成原生采证。Teacher 精确选择 `full_shipment:item:812 / (O)812 / native_fish_pond_output / fish_pond:Sturgeon:0 / full_shipment:full_shipment:item:812:0:46`，唯一动作链仍为 `fishing.service_fish_ponds -> collect_fish_pond_output -> executor.collect_fish_pond_output`；没有新增第二套候选、编译器或执行器。
- 透明桥继续完整保留 `Sturgeon:0..5` 六条权威产出来源。当前候选索引按完整 `route_kind + source_id + qualified_item_id` 建立来源身份，并在同一实时鱼塘可对应多条静态来源时只允许字典序首个来源进入规划与派发；其他来源仍可读，但不会重复占据 Teacher 前沿。候选新增真实 `route_distance_tiles` 与 `expected_output_quality`，索引缓存 schema 升至 `v12`。
- 当前鱼塘输出已贯穿日历、位置、设施、资源、加工、随机性、日内时间/能量和派发轴。现有鱼塘、已物化 `(O)812 x1`、质量 `0`、零输入、确定性当前输出、零能量和原生收取编译链均须精确一致；来源、物品、位置、数量、质量或快照漂移继续失败关闭。
- 正式 v5 原生执行复用游戏鱼塘交互取得 `(O)812 x1`，动作回执为 `executor.collect_fish_pond_output`，初始状态哈希 `9640ddc868bab5f4aff44f9b3f065337a90685968ffeec29a2d63ff7d30b3902`，终态状态哈希 `d73e96ac09969e3587d09fb6cb495baadbc40c172a5952a7789655d4de8d5dcb`。离线重放复用同一原生执行回执并完整通过 fresh terminal、settlement 与 rollout proof，不重新启动游戏。
- 重放期间修复两个开发效率问题：`Resume-RuntimeFullShipmentAcquisitionProof.ps1` 现在可从唯一透明当前候选恢复未绑定的 option-only 排名，不再把它强制改写成显式参数候选并破坏队列哈希；canonical 鱼塘解析继续沿用历史“当前候选不存在”诊断字符串，避免无行为变化的文案调整令旧证据大面积漂移。Sap 历史位置轴重算结果为 `CHANGED=0`。
- 严格 production evidence index 已推进到 `21/26`。剩余 `5` 层为 `machine_output`、`native_machine_flavored_output`、`native_machine_item_query_output`、`native_solar_panel_output`、`native_tree_moss_harvest`；`formal_product_training_authorized=false`。鱼塘 proof 根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-fish-pond-output-v5-20261014\runtime-full-shipment-evidence-fish-pond-output-v5-20261014`，机器可读索引根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v35-20261014`。
- 下一固定切片是共享机器产出族：先审计并统一 `machine_output`、`native_machine_flavored_output`、`native_machine_item_query_output` 与 `native_solar_panel_output` 的现有当前机器候选、设施、输入、加工、预算和 `farm.collect_machine_outputs` 执行链，再集中采证；不得为四种来源复制四套规划或执行系统。树苔在机器族之后单独闭环。

## 历史记录 · 2026-10-14 Full Shipment 标准批次：原生动物产物闭环

- `farm_animal_product_sample` 与 `farm_animal_deluxe_product_sample` 已从冻结源存档的隔离副本完成原生采证。普通牛奶绑定 `full_shipment:item:184 / (O)184 / native_farm_animal_produce / farm_animal:White Cow:0`；大壶牛奶绑定 `full_shipment:item:186 / (O)186 / native_farm_animal_deluxe_produce / farm_animal:White Cow:0`。
- 两层严格复用唯一动作链 `farm.collect_animal_products -> collect_animal_product -> executor.collect_animal_product`。没有新增第二套候选、编译器或执行器；新增内容仅把现有当前动物候选的精确来源、位置、目标/站位格、路线距离、非消耗工具和能量证据贯穿日历、位置、设施、资源、加工、随机性与日内预算轴。候选索引缓存 schema 升至 `v11`。
- 普通牛奶原生回执在 `97` tick 内完成，验证 `current_produce -> null`、银星 `(O)184 x1`、能量 `270 -> 266`、耕种经验 `0 -> 5`、友好度 `500 -> 505`、产奶统计 `0 -> 1`，状态哈希与终态快照均新鲜。大壶牛奶使用同一执行链和 `DeluxeProduceItemIds` 权威来源通过完整 fresh terminal、settlement 与 rollout proof。
- 严格 production evidence index 已推进到 `20/26`，剩余 `6` 层：`machine_output`、`native_fish_pond_output`、`native_machine_flavored_output`、`native_machine_item_query_output`、`native_solar_panel_output`、`native_tree_moss_harvest`。共享 shipping/settlement 证明有效，`formal_product_training_authorized=false`。机器可读索引根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v32-20261014`。
- 现有证明预检仍需逐条重建 receipt，约需数分钟；在 #200/#201/#202 的 26 层门槛满足前，不以跨 proof 缓存削弱验真。`Invoke-RuntimeFullShipmentEvidenceMilestone.ps1 -CompactOutput` 现只返回状态、计数、剩余层与完整文件路径，避免把数百行 checkpoint/index 倒灌控制器上下文；同族路线完成后集中刷新一次严格索引。
- Bootstrap 与 RuntimeTestHarness Release 均 `0 warning / 0 error`，Core `2680/2680`、Backend `229/229` 全量通过。下一固定切片是 `fish_pond_output_sample / full_shipment:item:812 / (O)812 / native_fish_pond_output`，优先复用现有 `fishing.service_fish_ponds` 透明、候选、编译和原生收取链。

## 历史记录 · 2026-10-14 Full Shipment 标准批次：原生水果树产物闭环

- `fruit_tree_harvest_sample` 已从冻结源存档的隔离副本完成原生采证。Teacher 精确选择 `full_shipment:item:638 / (O)638 / native_fruit_tree_produce / fruit_tree:628:0 / full_shipment:full_shipment:item:638:0:0`，继续复用唯一的 `foraging.harvest_fruit_tree -> harvest_fruit_tree -> executor.harvest_fruit_tree` 链，没有新增第二套候选、编译器或执行器。
- 当前精确水果树候选现将来源、地点、目标格、站位格和真实路线距离贯穿到日历、位置、随机性、设施、加工与日内时间/能量轴；同物品、同路线族的非当前水果树来源在存在精确当前来源时判为不适用。候选索引缓存 schema 升至 `v10`。
- v1 在 Teacher preference 前失败关闭，暴露设施容量与加工轴尚未识别当前现成果树；v2 证明规划和原生摇树成立，但回执错误地把“果实生成在 debris”当成“采收完成”。执行器现改为两阶段：先验证原生 `GameLocation.checkAction -> FruitTree.performUseAction -> FruitTree.shake` 产出，再复用共享 BFS、正常移动输入和游戏碰撞机制自然拾取；不直接写 debris 或背包。
- 正式 v3 在 `93` tick 内完成，回执为 `applied/verified`，验证 `fruit_count 1 -> 0`、`(O)638` 品质 `0` 数量 `1`、背包 `0 -> 1`、觅食经验不变，以及 fresh terminal、settlement 和完整 rollout proof chain。rollout manifest SHA-256 为 `3c599efe1e7510c2ffa37a5e3e8e95bbcb0070a8fe135f0dd643c3ef279481ba`，receipt SHA-256 为 `1ce50884e8f31c9a614867f17be28dbb66e73758db931aafed7dcca0a4a44e55`。
- 严格 production evidence index 已推进到 `18/26`，剩余 `8` 层，共享 shipping/settlement 证明有效，`formal_product_training_authorized=false`。正式 proof 根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-fruit-tree-v3-20261014\runtime-full-shipment-evidence-fruit-tree-v3-20261014`，机器可读索引根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v30-20261014`。
- Bootstrap 与 RuntimeTestHarness Release 均 `0 warning / 0 error`，Core `2679/2679`、Backend `229/229` 全量通过。当时后续计划先优化单样本 1599 路线全量重算及严格索引重复验真，不得削弱 fresh snapshot、原生执行、终态回执或 proof 哈希门槛；随后进入 `farm_animal_product_sample / native_farm_animal_produce`。
- 即时开发效率优化先消除 `quality` 在功能分支 push 与 PR 上的重复触发：分支只由 `pull_request` 验证，`main` push 继续完整验证，并以 workflow + PR/ref 为并发键取消同一分支的陈旧运行。精确 requirement 级路线裁剪与跨 proof 证书复用仍受 #200/#201/#202 的 26 层启动门限制，不提前削弱；仅执行器发生变化且初始 StateHash 与规划输入不变的重试，应使用现有 `ReplayAcquisitionQueue` 入口复验原生执行和后续 proof。

## 历史记录 · 2026-10-14 Full Shipment 标准批次：原生地点觅食拾取闭环

- `location_forage_spawn_sample` 已从冻结源存档的隔离副本完成原生采证。Teacher 精确选择 `full_shipment:item:16 / (O)16 / native_location_forage_spawn / location:Forest:0 / full_shipment:full_shipment:item:16:0:2`，继续复用唯一的 `foraging.collect_spawned_objects -> collect_spawned_object -> executor.collect_spawned_object` 链；没有新增第二套候选、编译器或执行器。
- v1 在正式执行前失败关闭并暴露两处真实规划缺口：当前精确地点觅食候选尚未进入日内时间/能量轴，同物品、同路线族的非当前静态来源也没有在存在精确当前替代来源时判为不适用。修复后，候选把地点、目标格、站位格和真实路线距离贯穿到位置与日内预算；随机轴按完整 `route_kind + source_id + qualified_item_id` 绑定当前来源，并排除同路线的非当前替代来源。候选索引缓存 schema 同步升至 `v9`。
- v2 原生动作经 `GameLocation.checkAction` 在 `261 -> 265` game tick 内从 `Forest (1,6)` 取得 `(O)16 x1`，回执为 `applied/verified`；验证目标对象移除、背包 `0 -> 1`、觅食经验 `0 -> 7`、耕种经验 `0 -> 0`、输出品质 `0`、fresh terminal receipt、settlement 与完整 rollout proof chain。没有直接删除对象、写背包或写经验。
- rollout proof manifest SHA-256 为 `6aad341921b5b081064ecb0865556e38582b2e1d55e219c7ab0da50858e5068b`，receipt SHA-256 为 `31d5347d06e9d607532af4e1c80150f170eb2b40b089bd33077024bede49383b`；冻结源存档 SHA-256 保持 `5af603dbb99bafadbb14580952b6e4b8c0d9e257963174759f53fbc8b2d671cc`。
- 严格 production evidence index 已推进到 `17/26`，剩余 `9` 层，共享 shipping/settlement 证明有效，`formal_product_training_authorized=false`。正式 proof 根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-location-forage-v2-20261014\runtime-full-shipment-evidence-location-forage-v2-20261014`，机器可读索引根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v29-20261014`。
- 本轮单样本仍触发 1599 条路线的目标日期轴重算，17 层索引也需跨进程重验全部 proof；它们是 #195/#200/#201/#202/#211 已登记的开发效率瓶颈。当前不能以缓存跳过 fresh snapshot、原生执行或终态回执，但后续应使用依赖 DAG 与可失效证书缩小重算范围。下一固定切片是 `fruit_tree_harvest_sample / native_fruit_tree_produce`。

## 历史记录 · 2026-10-14 Full Shipment 标准批次：原生春葱采收闭环

- `spring_onion_harvest_sample` 已从冻结源存档的隔离副本完成原生采证。Teacher 精确选择 `full_shipment:item:399 / (O)399 / native_spring_onion_harvest / Crop.harvest`，继续复用唯一的 `foraging.harvest_spring_onions -> harvest_crop_tile -> executor.harvest_crop` 链；没有新增第二套候选、编译器或执行器。
- 透明桥仅为原版 `Crop`、`(O)399` 且投影状态为 `exact_from_decompiled_native_spring_onion_branch` 的当前可收获作物发布权威路线来源。当前候选将地点、目标格、站位格、BFS 前置所需的移动上限、路线距离和完整来源身份贯穿到日历、位置、日内时间/能量、Teacher 与动作队列；来源、物品、地点、格子或预算漂移继续失败关闭。
- 原生动作在 `261 -> 264` game tick 内取得 `(O)399 x1`，回执为 `applied/verified`；验证当前作物从 ready 变为移除、背包 `0 -> 1`、耕种经验 `0 -> 0`、觅食经验 `0 -> 3`、fresh terminal receipt、settlement 与完整 rollout proof chain。动作仍由游戏原生 `Crop.harvest` 产生物品与经验，没有直接改背包或经验。
- rollout proof manifest SHA-256 为 `7976da974cbec8c931e1b52d137f09f2891336864e31e82fe4c72d9787335677`，receipt SHA-256 为 `da94fcddbd89a96e11395c0142b2852075cbf1728250ce22b077c8e6ce750057`；冻结源存档 SHA-256 保持 `5af603dbb99bafadbb14580952b6e4b8c0d9e257963174759f53fbc8b2d671cc`。
- 严格 production evidence index 已推进到 `16/26`，剩余 `10` 层，共享 shipping/settlement 证明有效，`formal_product_training_authorized=false`。正式 proof 根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-spring-onion-v2-20261014\runtime-full-shipment-evidence-spring-onion-v2-20261014`，机器可读索引根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v28-20261014`。
- 候选索引缓存 schema 随新增春葱路线升至 `v8`，避免同一进程内误复用旧候选集合。下一固定切片是 `location_forage_spawn_sample / native_location_forage_spawn`；只复用现有地点觅食、移动和自然拾取链补齐严格 proof，不重跑已通过的 16 层。

## 历史记录 · 2026-10-13 Full Shipment 标准批次：原生野树种子闭环

- `wild_tree_seed_sample` 已从冻结源存档的隔离副本完成完整原生采证。Teacher 精确选择 `full_shipment:item:88 / (O)88 / native_wild_tree_seed / wild_tree:6 / full_shipment:full_shipment:item:88:0:1`，继续复用唯一的 `foraging.harvest_tree_product -> executor.harvest_tree_product` 链。
- `(O)88` 的权威清单同时包含 `wild_tree:6` 与 `wild_tree:9`。当前 fixture 只物化 `wild_tree:6`；规划器现在用 `route_kind + source_id + qualified_item_id` 绑定可执行候选，同时允许由锁定的 `Data/WildTrees` 证明同物品、同路线族的另一来源无额外日历限制。位置轴仍要求精确 `source_id` 的实时树候选，因此缺席的 `wild_tree:9` 明确为不适用，不会借用 `wild_tree:6` 冒充执行。
- 最终 v2 原生动作在 `70` tick 内经 `GameLocation.checkAction -> Tree.performUseAction -> Tree.shake` 取得 `(O)88 x1`。回执为 `applied/verified`，验证 `has_seed true -> false`、`was_shaken_today false -> true`、完整输出域、背包自然拾取、觅食经验 `0` 和安全槽位恢复；没有新增第二套候选、编译或执行系统。
- v1 仅用于暴露 `wild_tree:9` 日历占位导致的 Teacher 分母缺口，不属于正式证据。最终规则落地后先重建既有 `14` 层确认无 proof 漂移，再运行 v2 并构建最终索引。
- 最终 rollout proof manifest SHA-256 为 `c83584b99a8602b7c449a92135f137325adffff74411aeadc3c05036ab4191d7`，receipt SHA-256 为 `7c22379b83bec6cdaa9f367ec37ec0575bd6a1c353bb861ca3852858c64353c2`。冻结源存档 SHA-256 仍为 `5af603dbb99bafadbb14580952b6e4b8c0d9e257963174759f53fbc8b2d671cc`。
- 严格 production evidence index 已推进到 `15/26`，剩余 `11` 层；共享 shipping/settlement 证明有效，`formal_product_training_authorized=false`。正式 proof 根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-wild-tree-seed-v2-20261013\runtime-full-shipment-evidence-wild-tree-seed-v2-20261013`，机器可读索引根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v26-20261013`。
- 下一固定项为 `spring_onion_harvest_sample / full_shipment:item:399 / (O)399 / native_spring_onion_harvest`，按 sample plan 顺序复用既有春葱透明读、作物采收编译与原生执行链，只补严格 Full Shipment proof。

## 历史记录 · 2026-10-13 Full Shipment 标准批次：原生野树种子掉落闭环

- `wild_tree_seed_drop_sample` 已从冻结源存档的隔离副本完成完整原生采证。Teacher 精确选择 `full_shipment:item:408 / (O)408 / native_wild_tree_seed_drop / wild_tree:2:0 / full_shipment:full_shipment:item:408:0:4`，唯一动作链仍为 `foraging.harvest_tree_product -> executor.harvest_tree_product`。
- 严格路线轴现在按 `route_kind + source_id + qualified_item_id` 绑定当前野树产物候选，并从锁定的 `Data/WildTrees payload.2.SeedDropItems[0].ItemId` 解析来源。路线距离进入日内预算，原生摇树后的自然碎片拾取获得独立时间余量；没有增加第二套野树候选、编译器或执行器。
- `(O)408` 同时有 4 条地点觅食来源。它们未在当前快照物化，而精确的野树确定性来源已经存在，因此随机性轴将这些即时不可执行的替代路线标为不适用；若没有当前确定性替代来源，原有精确概率缺失门仍会失败关闭。该规则避免伪造概率，也没有放宽完整 Teacher 分母。
- 最终 v4 原生动作在 `56` tick 内经 `GameLocation.checkAction -> Tree.performUseAction -> Tree.shake` 取得 `(O)408 x1`。回执为 `applied/verified`，验证 `has_seed true -> false`、`was_shaken_today false -> true`、完整输出域、背包自然拾取、觅食经验 `0` 和安全槽位恢复；没有直接写树、RNG、碎片、背包或技能状态。
- v2 暴露了同需求替代觅食路线令 Teacher 分母不完整；v3 证明动作可行，但随后发现共享位置轴改写会使历史 proof 漂移，因此只保留为诊断产物。最终实现收窄到精确身份和随机性轴后，历史 `13` 层全部重建通过，再由 v4 形成正式 proof。
- 最终 rollout proof manifest SHA-256 为 `5ccc6aed8f59a0f6f061b834f8ae16ac6eb5ebd8df76139a49c3f2b581dacce9`，receipt SHA-256 为 `44516df2daebcbfbfcfa15ea4ab21a6cb17aea837e9856c355fdc08dac97a9d6`。冻结源存档 SHA-256 仍为 `5af603dbb99bafadbb14580952b6e4b8c0d9e257963174759f53fbc8b2d671cc`。
- 严格 production evidence index 已推进到 `14/26`，剩余 `12` 层；共享 shipping/settlement 证明有效，`formal_product_training_authorized=false`。正式 proof 根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-wild-tree-seed-drop-v4-20261013\runtime-full-shipment-evidence-wild-tree-seed-drop-v4-20261013`，机器可读索引根为 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v24-20261013`。
- 下一固定项为 `wild_tree_seed_sample / full_shipment:item:88 / (O)88 / native_wild_tree_seed`。应复用本轮同一候选、严格路线轴、原生摇树和自然拾取链，只验证 `Data/WildTrees payload.6.SeedItemId` 的独立来源层。

## 历史记录 · 2026-10-13 Full Shipment 标准批次：原生茶树采收闭环

- `tea_bush_harvest_sample` 已从冻结源存档的隔离副本完成完整原生采证。Teacher 精确选择 `full_shipment:item:815 / (O)815 / native_tea_bush_harvest / Bush.GetShakeOffItem / full_shipment:full_shipment:item:815:0:0`。
- 本轮没有新增业务代码。茶叶原样复用浆果切片统一后的透明桥、`foraging.harvest_bushes -> executor.harvest_bush` 候选/编译/执行链、共享灌木 fixture、BFS 移动、自然碎片拾取和严格终态回执，证明茶叶与浆果没有形成两套系统。
- 原生 `checkAction` 在 `39` tick 内取得 `(O)815 x1`，回执为 `applied/verified`；验证灌木偏移复位、原生输出增量、背包自然拾取和茶叶分支原生觅食经验 `0`。没有直接写背包或调用碎片直收 API。
- 完整 rollout 状态为 `verified_complete_rollout_proof_chain`；冻结源存档 SHA-256 保持 `5af603dbb99bafadbb14580952b6e4b8c0d9e257963174759f53fbc8b2d671cc`。茶叶 proof 根位于 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-tea-bush-v2-20261013\runtime-full-shipment-evidence-tea-bush-v2-20261013`。
- 严格 production evidence index 已推进到 `13/26`，剩余 `13` 层；共享 shipping/settlement 证明有效，`formal_product_training_authorized=false`。机器可读检查点位于 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v19-20261013`。
- 首次运行因合并后源码时间戳晚于 DLL 被 `-SkipBuild` 新鲜度门禁拒绝，未启动游戏或生成证据；完成受约束组件重建后只运行一次茶叶样本和一次最终索引。该门禁按设计阻止旧二进制证据，不能为提速绕过。
- 下一固定项为 `wild_tree_seed_drop_sample / native_wild_tree_seed_drop`。应先审查并复用现有野树摇落/掉落、移动与自然拾取链，只补当前严格路线轴和 Full Shipment proof；不得重跑已通过的 13 层。

## 历史记录 · 2026-10-13 Full Shipment 标准批次：原生姜采收闭环

- `ginger_harvest_sample` 已从冻结源存档的隔离副本完成完整原生采证。Teacher 精确选择 `full_shipment:item:829 / (O)829 / native_ginger_harvest / Crop.hitWithHoe / full_shipment:full_shipment:item:829:0:0`，继续复用唯一的 `foraging.harvest_ginger -> executor.harvest_ginger` 链，没有新增第二套规划、编译或执行系统。
- 透明桥只为精确原版 `HoeDirt/Crop/Hoe` 暴露 `native_ginger_harvest / Crop.hitWithHoe / (O)829` 权威来源。当前候选、日历、地点、日内时间/能量、Teacher 和调度编译均绑定同一个当前快照候选；来源、日期、地点、格子、工具、输出或预算漂移继续失败关闭。
- 执行器保留原生锄头生命周期。锄出姜后复用现有 BFS/移动原语靠近原生碎片，由游戏自身磁吸进入背包；不直接移除作物、不直接增加经验、不调用碎片直收 API，也不直接写背包。最终动作在 `47` tick 内取得 `(O)829 x1`，回执为 `applied/verified`，并验证作物移除、HoeDirt 状态、原生碎片生成、觅食经验 `+7` 和自然拾取。
- 完整 rollout 状态为 `verified_complete_rollout_proof_chain`；冻结源存档 SHA-256 保持 `5af603dbb99bafadbb14580952b6e4b8c0d9e257963174759f53fbc8b2d671cc`。姜 proof 根位于 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-ginger-v1-20261013\runtime-full-shipment-evidence-ginger-v1-20261013`。
- 严格 production evidence index 已推进到 `12/26`，剩余 `14` 层；共享 shipping/settlement 证明有效，`formal_product_training_authorized=false`。机器可读检查点位于 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v18-20261013`。
- 本轮只执行一次原生样本和一次最终 evidence-index 构建，没有重跑已通过的 11 层。后续开发性能继续遵守 #195/#200/#201/#202/#211/#223：相同确定性输入单次计算，完整传递身份和哈希，证据按依赖失效；不能用缓存绕过 fresh snapshot、原生执行或严格终态回执。
- 下一固定项为 `tea_bush_harvest_sample / native_tea_bush_harvest`。应复用现有 `foraging.harvest_bushes -> executor.harvest_bush` 链和共享灌木 fixture，只补茶叶当前候选的严格路线轴与 Full Shipment proof，不得复制浆果执行器，也不得重跑已通过的 12 层。

## 历史记录 · 2026-10-13 Full Shipment 标准批次：原生浆果灌木闭环

- `berry_bush_harvest_sample` 已从冻结源存档的隔离副本完成完整原生采证。Teacher 精确选择 `full_shipment:item:296 / (O)296 / native_bush_shake / Bush.GetShakeOffItem / full_shipment:full_shipment:item:296:0:0`，继续复用唯一的 `foraging.harvest_bushes -> executor.harvest_bush` 链，没有建立第二套收获执行器。
- 透明桥现在为当前可收获灌木暴露精确权威来源；当前候选索引、日历、地点、日内时间/能量、Teacher、调度编译和执行绑定均按同一候选身份闭合。普通浆果和茶树只在当前快照存在精确候选时进入对应日历；来源、物品、地点、格子或参数漂移继续失败关闭。
- 夹具站位按灌木真实像素包围盒外围计算，避免两格宽灌木把锚点相邻格误当成可站立格。原生 `checkAction` 后，执行器先验证灌木状态、掉落守恒、经验和核桃分支，再复用现有 BFS 与自然磁吸语义收集落在灌木另一侧的碎片；不会直接写背包或调用直接收集 API。最终动作在 `52` tick 内取得 `(O)296 x3`，回执为 `applied/verified`；候选按包含自然拾取的保守 `180` tick 交互预算参与日内时间校验。
- 完整 rollout 状态为 `verified_complete_rollout_proof_chain`，原生收获、精确库存增量、装箱、睡眠、次日 Full Shipment 结算和 proof chain 均已验证。冻结源目录哈希保持 `5af603dbb99bafadbb14580952b6e4b8c0d9e257963174759f53fbc8b2d671cc`。
- 严格 production evidence index 已推进到 `11/26`，剩余 `15` 层，共享 shipping/settlement 证明有效，`formal_product_training_authorized=false`。机器可读检查点位于 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v17-20261013`；本轮原生证明位于 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-berry-bush-v16-20261013\runtime-full-shipment-evidence-berry-bush-v16-20261013`。
- `-SkipBuild` 现在会校验 Backend、ProductExecutor、LiveTrainingLoop 与 GoalConditionedBootstrap 的 Release 输出新鲜度，防止在线排名器与离线验证器因旧二进制产生证据形状分裂。当前主要开发性能瓶颈仍是既有证据索引和 1599 条路线的目标日期轴跨进程重复重算；后续优化必须沿 #195/#200/#201/#202/#211 的依赖哈希与可失效 proof 复用方向推进，不能削弱 fresh snapshot、原生执行或严格回执。
- 下一固定项为 `ginger_harvest_sample / native_ginger_harvest`。应先复用现有 `foraging.harvest_ginger -> executor.harvest_ginger` 链并只补当前严格采证缺口；不得重跑已通过的 11 个分层，也不得把隔离重放摘要当作正式证据。

## 历史记录 · 2026-10-13 Full Shipment 标准批次：普通作物收获

- standard 批次首项 `parsnip_harvest_sample` 已从冻结源存档的隔离副本重新完成原生采证。Teacher 保持选择 `full_shipment:item:24 / (O)24 / harvests_as / full_shipment:full_shipment:item:24:0:0`，动作队列复用唯一的 `farm.maintain_crops -> executor.harvest_crop` 链。
- 原生执行获取 `(O)24 x1`，冻结源目录哈希保持 `5af603dbb99bafadbb14580952b6e4b8c0d9e257963174759f53fbc8b2d671cc`；rollout 状态为 `verified_complete_rollout_proof_chain`，portfolio completion 与 proof chain 均为 `true`。
- 严格 production evidence index 已推进到 `10/26`，共享 shipping/settlement 证明有效，剩余 `16` 层，`formal_product_training_authorized=false`。检查点位于 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v6-20261013`。
- 下一固定项为 `berry_bush_harvest_sample / native_bush_shake`。不得重跑已通过的十个分层；优先复用现有 `foraging.harvest_bushes -> executor.harvest_bush` 链并只修真实严格证明缺口。

## 历史记录 · 2026-10-13 Full Shipment 社区中心奖励闭环与高风险批次完成

- `creates_reward_item` 现已贯通当前透明候选、日历、地点、材料、日内时间/能量、Teacher、动作队列与原生执行绑定。代表路线固定为 `full_shipment:item:336 / (O)336 / full_shipment:full_shipment:item:336:0:0 / bundle:Bulletin Board/33:reward`，继续复用唯一的 `community_center.donate_bundle_items -> executor.claim_community_center_bundle_reward` 链，没有第二套奖励执行器。
- 解析器只在当前快照精确枚举出奖励候选且 `world_progress.community_center` 的 bundle 分母完整时启用；缺少当前候选的旧快照继续保留原 `blocked_pending_route_kind_calendar_parser` 结果。该兼容门避免全局日历 JSON 无意义漂移，也使现有八份冻结原生证明在当前代码下原样通过严格重建。
- 原生样本从完成但未领奖的 Bulletin Board bundle 出发，奖励状态由 `true -> false`，Gold Bar `(O)336` 由 `0 -> 5`，动作回执为 `applied`。完整 rollout proof 为 `verified_complete_rollout_proof_chain`，portfolio 完成与 proof chain 均已验证；正式训练标志仍为 `false`。
- 回放脚本现会在非 Sap acquisition sample 成功后验证冻结源哈希并直接生成样本摘要，不再误入 Sap 专属投递/睡眠路径。里程碑脚本在高风险证据已齐时写入 `high_risk_evidence_verified`，并把空待办规范化为真正的空数组。
- 严格 production evidence index 已推进到 `9/26`，共享 shipping/settlement 证明继续有效，剩余 `17` 层；七个 high-risk 样本已全部完成。机器可读检查点位于 `E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v5-20261013`。下一固定开发项是标准批次首项 `parsnip_harvest_sample`，不得重复运行已通过的高风险样本。

## 历史记录 · 2026-10-13 Full Shipment 商店证据入库与 Sap 结算证明重基

- Full Shipment 运行时样本计划中的商店代表已从过时的 `full_shipment:item:388 / (O)388` 修正为原生 v8 实际验证的 `full_shipment:item:380 / (O)380 / sells / shop:Blacksmith`。`Test-FullShipmentRuntimeSamplePlan.ps1` 重新对账为精确 `26/26`，商店层候选路线包含已验证的 `full_shipment:full_shipment:item:380:0:10`。
- 当前编译器增加队列内位置投影后，旧 Sap Teacher receipt 按完整编译语义正确失效。Sap 已从 fresh 隔离存档重新执行 acquisition、发货、回家、原生睡眠与次日结算；新 checkpoint 为 `prefix_proof_verified=true`、`verified_iteration_count=1`、`remaining_item_count=153`、`ready_for_next_iteration=true`。
- Sap 回家后的快照处于淡入阶段，睡眠队列前快照已恢复玩家控制，两者相隔 `41` tick。两份快照的实际差异仅为淡入/控制门、功能投影 tick/指纹和机器探针缓存 tick；游戏内时间、位置、Full Shipment 进度及待结算 Sap 均未变化。recurrence verifier 不再错误要求这两个独立采样快照整份哈希相同，改为同时验证各自 state hash、tick 单调性、角色/存档身份、游戏内时间、位置、完整收集进度和精确待结算数量；位置、进度、发货箱或哈希漂移负例均失败关闭。
- 严格 production evidence index 已从冻结原生证据重建为 `8/26`：共享 shipping/settlement 证明有效，商店购买已作为 `verified_exact_native_sample` 入库，剩余 `18` 层。高风险批次当时只缺 `community_center_reward_sample`；检查点位于 `F:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v4-20261013`。
- 本轮原生 Sap 重基完成后，checkpoint 与 evidence export 均通过；随后 evidence preflight 只读冻结证据，没有重复运行商店样本。`formal_product_training_authorized=false` 保持不变。下一固定开发项为社区中心奖励层，之后再进入其余标准分层。

## 历史记录 · 2026-10-12 #224 Teacher 构建去重与 Full Shipment 原生商店购买闭环

- Grandpa 目标分母已按权威目录固定为 `19` 个评分准则、`11` 个根方法，不再把方法数误当准则数。Goal-method 对账 schema 升至 `v3`，新增 `10` 个推进方法族和 `19` 个准则视图，其中 Money、Friendship、Skill 共 `3` 个单调阈值族共享各自推进过程，同时保留每个准则的独立终态断言与 Teacher 目标上下文。
- `GoalMethodCoverageReconciliationBuilder` 现在只构建一次权威 frontier，旧路径的 `2` 次降为 `1` 次；理论独立推进构建由 `19` 次合并为 `10` 次，避免 `9` 次确定性重复构建。以上是结构计数，不冒充 wall-time 实测。当时仍只有 `4/19` executable、`2/19` coverage-ready，`formal_product_training_authorized=false` 不变。
- 历史回归常量已从过时的 `877` 条对齐到当前权威 `1382 resolved / 217 blocked`；完整 `experiments/Run-Regression.ps1` 已通过。商店购买 supporting transition 现覆盖第六类 `shop_purchase_navigation`，统一复用既有路线、柜台交互、购买和关菜单原语，没有第二套执行器。
- 原生 v8 回放 `runtime-full-shipment-shop-purchase-20261012-stage-loop-v8` 已从 fresh 隔离存档完整通过三段跨图、Blacksmith 内柜台交互、ShopMenu 安全计时等待、精确购买和菜单关闭。终端队列为 `executor.wait_ticks -> executor.buy_shop_item -> executor.close_menu`，三项均为 `applied/verified`；金额 `500g -> 350g`，`(O)380` 铁矿 `0 -> 1`，菜单终态为关闭。
- 编译器只允许 `ShopMenu + 正安全计时器 + 非只读/无手持物 + 完整购买延续身份 + 唯一阻塞理由为安全计时器` 的有界等待；后续购买必须紧邻同一身份等待，并在原生执行时再次检查菜单、计时器、商店、库存、价格和容量。身份漂移或缺少安全约束继续失败关闭。Core game-free `288/288`、GoalConditionedBootstrap Release `0 warning / 0 error`、acquisition dispatch 与商店导航终端覆盖自测均通过。
- 近期开发过慢确有一部分来自重复确定性 frontier/推进构建、逐路线空间扫描、重复钓鱼概率投影以及陈旧 fixture/计数造成的无效回归；这些已分别去重或修正。原生 fresh snapshot、执行前安全门、终态回执和 rollout proof 属于必要验证，不能按性能优化名义删除。

## 历史记录 · 2026-10-11 Full Shipment 高风险证据与严格投影优化

- 当时权威 evidence index 已从锚点阶段的 `2/26` 推进到 `7/26`。新增通过 `native_wild_tree_tapper_output`、`native_monster_drop_table`、`native_location_fish_spawn`、`native_location_artifact_spot` 与 `native_geode_drop` 五个 exact native sample；对应树液收集器产物 `(O)725`、怪物掉落 `(O)766`、地点钓获 `(O)388`、古物点黏土 `(O)330` 和 Golden Coconut `(O)791` 精确开出的 5 个铱矿 `(O)386` 均完成原生执行、fresh terminal、settlement、checkpoint 与 rollout proof 重建。共享 shipping/settlement 证据继续有效，剩余 `19` 层，`formal_product_training_authorized=false` 不变。
- 古物点路线现在从当前透明快照精确绑定地点、目标格、站位格、移动距离、锄头次数、能量和确定性产出；静态 `Default` 掉落规则只在存在匹配的当前古物点时解析为实际地点。动作编译继续复用唯一的 `executor.clear_obstacle`，并在同一队列追加既有 `executor.pickup_debris`；规划编译、动作队列编译和运行执行三层均重新核对权威来源、物品、品质、数量与掉落物基线。零移动场景允许省略 `move_to_tile`，但仍严格核对教师动作时间，不允许瞬移或直接改背包。
- 晶球路线只在当前透明快照存在精确 `processing.crack_geode` 候选时启用 `Data/Objects` 来源解析，并由同一候选贯通 Blacksmith/Clint 柜台设施、背包晶球槽位、25g 服务费、原生 RNG 精确输出和日内时间预算；动作编译与执行继续复用唯一的 `executor.crack_geode`。调度器从权威 `geode:<id>:<row>[:random:<index>]` 来源重建晶球输入，并从需求集规范化用途标签，防止排名参数与防篡改重建漂移。
- 本轮还修复了跨运行恢复缺陷：同一路线的 active material/currency reservation 只有 `SourceStateHash` 与当前快照一致时才算 `claim_already_committed`；执行前中断留下的旧声明会走既有 `claim_replacement_required` 原子 upsert 路径，保留稳定 reservation ID 和完整账本历史，不会被空 portfolio marker 错误继承。
- 钓鱼场景暴露的主要瓶颈不在游戏动作层。validation trace 显示一次严格 proof 的约 `151.6 s` 中，约 `131.5 s` 用于对 `1599` 条路线、`920` 个可钓水格和鱼类规则重复做 terminal probability projection。现在每个冻结 fishing forecast 只解析一次水格、规则适用格和玩家站位限制，并以“目标物品 + 水深 + 规则适用集合 + 站位许可集合”为完整等价键复用概率；当前格坐标、站位坐标和结果元数据仍逐候选生成。
- 同一钓获 rollout proof 的严格重建从约 `151 s` 降到 `20.5 s`，新旧 receipt JSON 完全相等且 `proof_chain_verified=true`。包含现有 5 条 proof 的 evidence index 重建从约 `219 s` 降到约 `81 s`。这些缓存只存在于当时 forecast/当时 CLI invocation；不跨进程、run 或 snapshot，不跳过 authority 哈希、fresh-terminal、settlement 或 rollout 校验。
- 所有 acquisition target-date 轴现通过既有 `InvocationDeterministicBuildSession` 做同一命令内的内容哈希去重，命中后重新反序列化独立 DTO，避免共享可变结果。GoalConditionedBootstrap Release 为 `0 warning / 0 error`，Core game-free 基线为 `276/276`，Bootstrap hermetic、Full Shipment runtime evidence 与 acquisition dispatch 自测均通过。下一证据顺序为商店购买、社区中心奖励；已通过的地点蚯蚓与晶球掉落不得重复运行。

## 历史记录 · 2026-10-11 Full Shipment 当时权威锚点恢复

- Sap 原生回放现在通过独立 `stardewai.runtime_full_shipment_sap_evidence_export.v1` 出口进入里程碑。该出口会重新深度构建 recurrence-prefix checkpoint，并精确绑定 Sap `full_shipment:item:92 / (O)92 / native_wild_tree_chop_drop`、rollout manifest/receipt 及四个文件哈希；它不复用旧 smoke 摘要的声明，也不能授权训练。
- 放射性矿石历史原生执行证据已按当前 requirement、lowering、Master Angler 窗口和路线计时输入完整重建。修复内容包括：确定性构建键允许空的可选路径值；恢复脚本在 Teacher 前和 ledger commit 后都重新请求当前 ranking；普通下矿候选不再输出三个空的 acquisition 意图字段，显式怪物掉落意图仍要求三个字段完整一致。旧原生运行的两步 `executor.mine_stone -> executor.pickup_debris` 没有改写，当前 fresh-terminal、settlement、checkpoint 与 rollout proof 已全部重新验证。
- 当时权威 evidence preflight 为 `2/26`：Sap 与 `native_radioactive_ore_node` 两个 anchor 均为 `verified_exact_native_sample`，共享 shipping/settlement 证明有效，剩余 `24` 层。下一批固定为 7 个 high-risk 场景：树液收集器、怪物掉落、地点钓获、地点蚯蚓、晶球掉落、商店购买、社区中心奖励。
- `formal_product_training_authorized=false` 保持不变。本轮只读取和重建 F 盘冻结证据并启动隐藏 Backend，没有启动游戏、修改存档或产生正式训练样本。当时机器可读检查点位于 `F:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v2-20261011`。

## 历史记录 · 2026-10-11 #214 snapshot 派生空间缓存正确性

- `SnapshotStateCache<T>` 统一替换 collision grid、route connector/route-repair 候选与清障编译验证索引的裸 `ConditionalWeakTable<SnapshotEnvelope,...>`。命中现在必须同时满足同一快照引用和同一非空 `StateHash`；空 hash 永不复用，构建过程中 hash 改变立即失败关闭。
- 同一个 `SnapshotEnvelope` 原地替换 `State/StateHash` 后，路线候选会从碰撞阻塞重建为可用，清障编译不会继续读取旧对象索引，机器放置也会按新碰撞格阻断；hash 不变时仍只构建一次。没有新增第二套路由、候选或编译逻辑。
- game-free Core 回归扩展到 `274/274`，Bootstrap hermetic 通过，Core 与 GoalConditionedBootstrap Release 均为 `0 warning / 0 error`。F 盘冻结 execution-binding 复算约 `14.149 s`，旧/新 SHA-256 同为 `62b7dd89707ec55b8d8494bd3ea7c16deebfb0e9ca0ae4e642c5e4e90e83e739`；fresh snapshot、runtime dispatch 和 verifier 门禁未降低。

## 历史记录 · 2026-10-11 #200 Phase A observe-only dependency DAG

- validation trace 新增独立子 schema `stardewai.validation_dependency_graph.v1`。节点记录 artifact kind/id、输出 SHA-256、producer/version、输入 artifact/hash/path、reuse scope、调用/缓存观察和 wall/CPU 成本；嵌套重算形成真实依赖边，并从每个外部输入计算理论失效后代。
- 图已覆盖 calendar resolution、全部 target-date 轴、opportunity cost、Teacher preference、portfolio admission/commit、dispatch validation、execution binding、fresh terminal、settlement、checkpoint 和 rollout proof；没有建立第二套 planner/compiler/executor/verifier。固定安全字段为 `observation_only=true`、`can_affect_admission=false`、`incremental_reuse_authorized=false`。
- F 盘冻结 Full Shipment Sap recurrence-prefix 对照输出单根、25 个 producer 节点、42 个外部输入失效集合；初始 snapshot 漂移会理论失效全部 25 个后代。输出 SHA-256 仍为 `98222532fcb4df7b16847311159095bf5941e87f12188d2b19290906db8d6f12`，状态仍是 `verified_recurrence_prefix` 且 `prefix_proof_verified=true`。
- 独立 Teacher supervision 链现复用同一 rollout proof，并新增 `training_sample_admission_certificate -> teacher_dataset_certificate` 两个观察节点。冻结一 transition 终态 rollout 输出单根、27 个节点、44 组外部输入失效集合；初始 snapshot 漂移理论失效全部 27 个节点，仅 proof manifest 漂移则精确失效 proof、rollout admission 与 Teacher dataset 三层。带 trace 与无 trace 的数据集 SHA-256 均为 `c91a75874801bb2d8e1c7459de35862ed0605aeb876a3ba6a563f5bcd90b4579`，状态仍为 `ready_verified_portfolio_supervision_dataset`，`formal_product_training_authorized=false`。
- observe-only 图的完整哈希和失效解释令本次显式 trace 从上一轮约 `24.340 s` 增至约 `29.200 s`；直接 Release 无 trace 回放为 `24.927 s` 且 SHA 不变，默认路径不执行图构建。Phase A 不允许跳算、proof inheritance、跨 run 缓存或 selective native replay，`formal_training_authorized=false` 不变。详见 `docs/validation-dependency-dag-phase-a.md`。

## 历史记录 · 2026-10-11 #195 路线清障与空间索引深度优化

- Windows CPU 采样确认剩余热点不在游戏或模型，而在 `CandidateOptionAvailabilityEvaluator`：旧实现对每个“被阻挡出口 × 可清障物”分别重读碰撞 JSON，并在清障前后各跑一次全图 0-1 BFS。现改为同一快照先计算一次起点距离场，再按唯一出口计算反向距离场；一个障碍当且仅当位于某条最小清障数路径上时生成原有 route-repair 候选。候选顺序、字段、阻塞理由和 `clear_one_obstacle_then_fresh_snapshot` 策略不变。
- 碰撞格和 `ActionQueueCompiler` 的地形/对象校验新增以 `SnapshotEnvelope` 实例为弱键的只读坐标索引，消除每个障碍再次线性扫描整个数组；索引不跨快照。location、stochastic-retry、daily-time-energy 三个轴重复读取的当前路线候选与野树候选，则只在既有 `InvocationDeterministicBuildSession` 中按精确快照 SHA 复用内部不可变索引，不跨命令、run 或 snapshot。
- 同一冻结 1599-route Sap 输入上，execution-binding 从上一轮 `104.668 s` 继续降为 `13.475 s`，相对本轮起点缩短约 `87.1%`；旧/新输出 SHA-256 都是 `62b7dd89707ec55b8d8494bd3ea7c16deebfb0e9ca0ae4e642c5e4e90e83e739`。真实消费入口 recurrence-prefix checkpoint 从 `114.952 s` 降为 `24.340 s`，仍输出 `verified_recurrence_prefix`、`verified_iteration_count=1`、`remaining_item_count=153`、`prefix_proof_verified=true`。
- GoalConditionedBootstrap Release 为 `0 warning / 0 error`，Core game-free `256/256` 与 hermetic Bootstrap self-test 通过；性能验证只读取 F 盘冻结制品，没有启动游戏、修改存档或生成训练数据。`formal_training_authorized=false` 不变。下一性能阶段仍是 #200 Phase A 的完整 DAG/成本观测；本轮不授权跨运行缓存、proof inheritance 或 selective native replay。

## 历史记录 · 2026-10-11 #195 确定性证明链单次 invocation 去重

- Full Shipment Sap 完整原生回放已经生成有效的 acquisition、deposit、sleep settlement、rollout proof 与 recurrence-prefix manifest；但旧 `build-full-shipment-recurrence-prefix-checkpoint` 在持续计算超过 12 分钟后仍未结束，开发反馈成本已不可接受。该中断不否定已落盘的原生动作与结算证据，但当时没有生成 prefix checkpoint/summary，不能宣称完整 runner 通过。
- Bootstrap 新增默认启用、可显式关闭的 `InvocationDeterministicBuildSession`。缓存只存在于当前进程/当前命令，键包含 producer/version 及每个输入文件的内容 SHA-256；不跨命令、不跨 run、不跨 snapshot。缓存值只保存序列化字节，命中时重新反序列化为新 DTO，不共享可变对象。`--deterministic-build-session disabled` 或 `STARDEWAI_DETERMINISTIC_BUILD_SESSION=disabled` 可恢复原完整重算路径。
- 首批只接入实测重复最重的四个节点：target-date processing、opportunity cost、portfolio Teacher preference 与 execution binding。每个唯一子图在本命令中仍完整反算一次；相同输入再次由 fresh/settlement/checkpoint 递归请求时才复用。validation trace 新增 `deterministic_build` operation，能按 producer 区分调用数、命中和累计 wall/CPU，不改变 admission。
- 同一冻结 1599-route Sap 输入上，execution-binding 从原运行约 `469 s` 降为 `104.668 s`，旧/新输出 SHA-256 均为 `62b7dd89707ec55b8d8494bd3ea7c16deebfb0e9ca0ae4e642c5e4e90e83e739`，缩短约 `77.7%`。此前超过 12 分钟仍未完成的 recurrence-prefix checkpoint 现于 `114.952 s` 完成，输出 `verified_recurrence_prefix`、`verified_iteration_count=1`、`remaining_item_count=153`、`prefix_proof_verified=true`。
- 性能回归只消费 F 盘冻结制品，没有启动游戏、修改存档或产生正式训练数据。GoalConditionedBootstrap Release 为 `0 warning / 0 error`，hermetic Bootstrap self-test 通过，Core game-free 为 `256/256`。本轮解除的是 proof 重复重算的开发效率硬伤；`formal_training_authorized=false` 不变。
- 当时后续计划先用现有 trace 完成 #200 Phase A 的全节点依赖/成本观测，再评估 opt-in batch Bootstrap。未建立完整 DAG 和 full-shadow 等价前，不启用跨命令增量跳算、proof inheritance 或 selective native replay。

## 历史记录 · 2026-10-11 Full Shipment Sap 原生证据恢复

- F 盘隔离实验室已经从真实原生新档生成独立 Spring 1 基线：存档槽为 `ProofFarm_451377026`，Full Shipment 初态为 `0/154`，只加载 TransparentBridge 与 RuntimeTestHarness。`Invoke-RuntimeFreshSaveSmoke.ps1` 新增可选 `OutputRoot`，默认行为不变，允许把高负载制品稳定留在 F 盘而不污染 I 盘仓库。
- 隐藏训练模式此前没有安装已有的 `HeadlessMovementInputSimulator`，导致路线、碰撞判断和当时后续计划方向均正确时角色仍停在原地。RuntimeTestHarness 现在仅在 `STARDEWAI_TRAINING_MODE=1` 且 `STARDEWAI_SUPPRESS_LOCAL_RENDER=1` 时复用同一个原生输入泵；没有新增第二套移动器、直接坐标写入或 BFS 分支。原生 FarmHouse 出门回归已通过。
- Full Shipment runner 不再硬编码历史 `ProofFarm_450250338`。未显式指定时从归档存档目录名派生槽名；显式槽名与目录叶名不一致、主存档文件不与槽同名或缺少 `SaveGameInfo` 时均在启动游戏前失败关闭。
- 新档 Sap 规划真实暴露了一个下游选择错误：`wild_tree:1:0` 有 27 个已解析原生树目标，旧日预算层只取第一个目标；该目标的二次静态寻路失败后，整条 Sap 来源被误判为机会分母不完整。现在按现有候选顺序逐个调用同一个 `EvaluateTerminal`，首个完整匹配立即返回；若没有匹配，仍只保留最佳已解析结果并维持失败关闭，不降低 Teacher 完整分母标准。
- 同一原生快照上的严格离线重算已通过：Sap `11/11` 路线无阻塞，`1` 条进入 Pareto 前沿；Teacher 为 `UniqueStrictPareto`，分母 `1` 且完整，标签可生成，阻塞为 `0`。目标树为 Farm `(49,20)`，保守完成时间 `06:53`。这只是 Sap 单层偏好证明，`formal_training_authorized=false` 保持不变，不得把离线重算冒充生产证据。
- 完整原生回放 `runtime-full-shipment-evidence-sap_prefix-20261011-150812` 已通过规划、Teacher、Sap 原生采集、投递、fresh terminal receipt 和 rollout proof，但 recurrence 的 `executor.sleep` 暴露了两个顺序故障：`DialogueBox` 的睡眠 Yes 与 `ShippingMenu` 的原生 OK 都不能由 `UpdateTicking` 阶段安装的 SMAPI 轮询按键覆盖触发。按 F 盘精确 `1.6.15` DLL 反编译结果，两处现在分别复用原生 `receiveKeyPress(Keys.Y)` 与 `receiveKeyPress(Keys.Escape)` 派发；睡眠结算和独立 ShippingMenu 恢复共用唯一的结算派发函数，没有直接改菜单、日期、出货统计或存档。
- 隔离下游重放 `runtime-full-shipment-sap-shippingfix-20261011-161922` 已真实通过：从新档副本原生获取 Sap `1`、投递并睡眠，存档从 Spring 1 写入 Spring 2，`basicShipped` 原生收据从 `0` 增至 `1`，Full Shipment 为 `1/154`，最终 `total_day=1`。该重放只复用了上一轮已经验证的 acquisition queue，用于证明执行与结算修复，不替代完整生产 recurrence 证明。
- 验证已通过：GoalConditionedBootstrap Release `0 warning / 0 error`、RuntimeTestHarness Debug `0 warning / 0 error`、Core game-free `256/256`、`git diff --check`。下一固定动作是以修复后的执行器完整重跑 Sap prefix，生成 recurrence manifest/summary 后重建 anchor evidence index，再进入既定 7 个 high-risk 场景；`formal_training_authorized=false` 保持不变。

## 历史记录 · 2026-10-11 #195 invocation-scoped immutable read session

- #199 在 Core 新增单次进程/单次命令生命周期的 `InvocationReadSession`。Bootstrap 默认只登记启动时已存在的文件参数；output/output-root、checkpoint、ledger/lease/ownership、控制参数、输出同路径与输出根后代全部禁止复用。`--read-session disabled` 或 `STARDEWAI_READ_SESSION=disabled` 可恢复原始逐次读取路径。
- `ContentAddressedJsonArtifactReader` 与 `CurrentTeacherFrontierSupport.HashFile` 共享 raw bytes、stored SHA-256 和验证后的 materialized text/logical SHA-256；内容寻址 blob 或最终 payload 只要有字节数/哈希错误仍立即失败关闭。当前 DTO 多为可变 class，因此本轮明确不共享 typed deserialize 实例，避免对象污染。
- validation trace 现在会在 operation 与 command 层真实记录 session cache hit；Phase 0 baseline builder 继续拒绝 cache-bearing trace，冻结基线须用 disabled 模式重放。反向顺序代表样本输出 SHA 完全一致，读盘由 `1464034` 降至 `913287` bytes（`37.618%`），全量哈希由 `936153` 降至 `787201` bytes（`15.911%`）；温缓存 wall 样本由 `260.714 ms` 降至 `258.003 ms`，仅作参考。详见 `docs/invocation-read-session.md` 与机器可读样本 `docs/invocation-read-session-sample.json`。
- 本轮不缓存 live snapshot、正在生成的 receipt、ownership/lease、训练准入或跨 snapshot 事实，不修改 Planner/Compiler/Executor/Verifier，不建立跨 run cache。#195 的 #200–#203 后期 DAG/增量验证顺序保持不变。

## 历史记录 · 2026-10-11 #195 本地 validation impact advisory

- #198 新增只读 `scripts/Get-ValidationImpactAdvisory.ps1`：默认仅读取 staged 与 unstaged 的已跟踪文件，也可读取显式文件清单或 `base...head` git range，输出 `stardewai.validation_impact_advisory.v1` JSON。新建未跟踪文件必须显式传入，避免把无关本地数据扫入分析。
- 映射覆盖 docs、Contracts/Core、Backend、GoalConditionedBootstrap、TransparentBridge adapter、RuntimeTestHarness、训练/产品工具、runtime scripts 与版本化 authority。未知路径、共享 runtime script、workflow/project/package 配置一律 broad fallback，并设置 `uncertain_impact=true`。
- 输出只包含本地第一轮 build/test/source-guard/native replay 建议；固定 `full_quality_still_required=true`、`evidence_reuse_authorized=false`、`native_replay_execution_authorized=false`。脚本不执行测试、不启动游戏、不继承证据，也不修改 GitHub `quality.yml`。
- game-free 行为回归直接重放 #186、#187、#192、#194、#196 的代表文件集合，并覆盖 docs-only 与 unknown fallback。下一性能切片仍是 #199 单次 invocation 内 immutable bytes/hash/materialization/parse 去重；不得扩展为跨 run 或跨 snapshot cache。

## 历史记录 · 2026-10-11 #195 Phase 0 validation trace 基线

- #197 已完成默认关闭的 validation trace：Bootstrap 可用 `--validation-trace PATH` 与 `--validation-trace-scenario NAME` 输出独立 JSON；运行时脚本仅在显式设置 `STARDEWAI_VALIDATION_TRACE_DIR` 时为子进程采集 trace。追踪失败不会改变命令结果、stdout 协议或既有 fail-closed 门禁，所有记录固定 `cache_hit=false`。
- 五个冻结代表场景已覆盖：单次低风险决策、机器 supporting-transition/portfolio、Full Shipment 单层 proof、anchor/high-risk preflight、Teacher dataset generation/relabel。聚合基线为 `complete`、`5/5`、零缓存命中，总墙钟 30.04 分钟。
- 完整 Stage 1 Teacher 链占 29.99 分钟并读取 183.27 GiB：同一命令内出现 616729 次 content-addressed JSON read、454111 次 JSON deserialize 与 857757 次 frontier file hash。热点来自严格重建的重复只读工作，不是原生游戏执行。
- 追踪记录按同一路径/producer/operation 聚合，保留首尾 operation ID、调用次数、累计时间/字节/哈希/解析成本以及有界 artifact ID，避免观测本身产生数百 MB 输出。完整链追踪由此前失败样本的 551 MB 降为约 2.1 MB；业务校验次数没有减少。
- 路线证据负例同步修正：移除全局 social route evidence 后，当前 Farm 上的 3 条原生作物路线仍可由实时当前位置证明；Beach/FixtureShop 两条跨图路线必须以 `social_route_date_evidence_unavailable` 阻断。整体仍为 partial、不可训练。完整 `self-test-current-stage-one-collection` 已以退出码 0 覆盖该边界。
- 下一顺序固定为 #198 本地 fast-check advisory，再做 #199 单次 invocation 内 immutable read/parse/hash 去重。不得先引入跨 run 或跨 snapshot cache，也不得缩小 GitHub PR 全量 quality、fresh snapshot、ownership、lease、execution receipt 或 formal-training 门禁。详见 `docs/validation-trace-phase0.md` 与机器可读基线 `docs/validation-trace-phase0-baseline.json`。

## 历史记录 · 2026-10-08 Full Shipment anchor 证明重基

- 10 月 8 日的机器首个有效输出修复把 34 条机器路线从错误的确定性通过改为显式阻塞。Sap 与放射性矿目标路线本身没有变化，但旧证明中的全局日历因此按设计失效，production evidence preflight 会以 `Calendar resolution drifted from deterministic source compilation` 失败关闭。
- `Resume-RuntimeFullShipmentAcquisitionProof.ps1 -RebuildPlanning` 现在可在不启动游戏的情况下，从原 rollout manifest 读取权威 inventory/lowering/Master Angler/计时输入和原始 `run_id`，重建日历、全部目标日约束轴、Teacher 选路、承诺、动作队列、execution binding、fresh terminal、结算和 rollout proof。它必须重新选择与旧证据完全相同的 route occurrence，并让旧原生执行回执通过新队列校验，否则立即拒绝。
- Sap 特殊链会额外重绑 recurrence manifest，并重新计算 prefix checkpoint；不是只改 summary 或文件指针。重基应在原 artifact 的副本上执行，历史证据目录保持不变。
- 异机无游戏验证已完成：Sap 和放射性矿两个重基证明均通过，随后 milestone 以 `-EvidencePreflightOnly` 独立重建 production evidence index，checkpoint 为 `anchor_evidence_verified`。下一批仍是原计划中的 7 个 high-risk 场景；`formal_product_training_authorized` 继续保持 `false`。

## 历史记录 · 2026-10-05 Full Shipment 采证里程碑计划

- 执行批次不再把“anchor 摘要及其引用文件存在”当成验真。默认 `high_risk/standard/all_missing` 会在启动首个游戏场景前，用 production evidence index 独立重建 Sap 共享出货/睡眠证明和两个 anchor 的精确 rollout proof；`all` 从零运行时则在两个 anchor 生成完、进入首个非 anchor 场景前执行同一验真。任何漂移立即写入失败 checkpoint 并停止，不消耗后续原生场景运行。
- `standard` 也不能绕过高风险批次：单独执行时要求 7 个高风险摘要齐全，并在启动游戏前用同一 production evidence index 验证其 exact native sample；`all/all_missing` 则在进入首个 standard 场景前执行同一阶段门。仅有摘要和引用文件仍不算通过。
- `-EvidencePreflightOnly` 支持 SSH/无交互桌面验真：它执行当前批次所需的 anchor 或 high-risk production index 重建，输出 checkpoint/index 后在原生 runner 循环前退出；它与仅核对计划的 `-PlanOnly` 互斥，也不能绕过缺失的前置阶段。
- 已从当前权威 requirement/lowering、动作 reconciliation 与支持终端覆盖重建 `full_shipment_static_compilability_inventory.v2`：`154` 个需求、`641` 条来源、`26` 个运行层、`0` blocker。
- `catalogs/vanilla-1.6.15/full-shipment-runtime-sample-plan.json` 为 26 层各绑定一个 runner 场景；`Test-FullShipmentRuntimeSamplePlan.ps1` 按完整执行签名核对，不把同层多来源误判为多个层，也不允许一个场景跨层。
- `Invoke-RuntimeFullShipmentEvidenceMilestone.ps1` 先执行 7 个高风险原生样本，逐场景写 checkpoint，成功可续跑、失败立即停止且不自动重试；`-PlanOnly` 只验证计划。默认高负载游戏运行目录为异机 `F:\StardewAI-TestLab\runtime`，其测试实验根仍为 `F:\StardewAI-TestLab`，本机不启动游戏。
- 里程碑编排器显式绑定异机 `F:\StardewAI-TestLab\inputs` 下的基准存档、requirement/lowering/Master Angler 输入和实验根下的移动计时校准；这些参数逐项透传给唯一原生样本 runner，不再依赖旧工作区的未提交文件或本机 I 盘默认路径。
- 原生样本 runner 的首次 Release 构建不再假设所有项目都被解决方案预恢复；每个实际入口自行恢复并构建，部署与逐项目构建输出写入本次 artifact。干净 worktree 因缺少 `obj/project.assets.json` 失败时不再只剩一个无上下文退出码。
- `RuntimeEvidenceCommon.Invoke-RuntimeBootstrap` 在 Windows PowerShell 下显式合并捕获原生 stderr，再按退出码判定；bootstrap 的结构化 JSON 错误会保留末尾诊断，不再被全局 `ErrorActionPreference=Stop` 截断为单个 `{`。
- `Invoke-InteractiveFullShipmentEvidenceMilestone.ps1` 是异机交互桌面的唯一薄入口：先确认与 Explorer 同会话，绑定 F 盘便携 SDK，并把只读权威镜像精确绑定为 inventory 已哈希绑定的 I 盘路径。已有映射仅在目标完全相同时复用且保留，目标不同则失败关闭；只有本次创建的映射才在 `finally` 撤销，进程环境始终恢复，实际工作仍委托给唯一 milestone/runner。
- 非钓鱼 acquisition 样本的 forecast manifest 现在显式写入 `snapshots: []`；不再让 Windows PowerShell 将条件表达式中的空数组折叠为 `null`，从而满足 `FishingForecastSnapshotReference[]` 的严格输入合同。
- 当时 `26/26` 是入口与采证计划覆盖，生产证据仍只有 Sap 与放射性矿石可独立复核；必须先导入/重验这两个 anchor，再跑高风险 7 层和其余标准层，最后统一构建 evidence manifest/index。训练授权仍为 `false`。

## 历史记录 · 2026-10-05 Full Shipment runtime 编排边界拆分

- `scripts/lib/RuntimeEvidenceCommon.ps1` 现统一承载场景无关的 UTF-8/JSON、目录哈希、HTTP、fresh snapshot、环境恢复、隐藏进程生命周期，以及 DailyPlan、Teacher preference、precompiled queue 的现有 LiveTrainingLoop 调用包装。
- `Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1` 只保留 Full Shipment 场景选择、夹具编排、权威来源绑定、Sap/出货/睡眠/recurrence 断言和对通用函数的薄参数绑定；它仍调用原有 planner/compiler/product executor/verifier，没有新增第二套运行时或脚本游戏语义。
- archived source save 哈希、隔离存档、typed queue/receipt、fresh after snapshot、checkpoint 与 `formal_product_training_authorized=false` 语义保持不变。本次是 #162 的维护性拆分，不增加 production evidence 分子；下一里程碑仍是代表性原生抽样后统一重建 26 层索引。

## 历史记录 · 2026-10-05 Full Shipment 地点钓获入口

- 最后一层代表固定为 `full_shipment:item:388 / (O)388 / native_location_fish_spawn / location_fish:Town:3`，复用现有 `fishing.catch_fish -> executor.catch_fish` 原生抛竿、上钩、小游戏和结果回执链。
- 修正了 dispatch 的旧假设：Full Shipment 中该层是地点钓鱼规则产生的非鱼物品，不能塞进 72 种鱼的 Master Angler 缺失集合。真正鱼种仍要求严格的 Master Angler 窗口；非鱼地点钓获物则必须由透明候选的完整结果分布同时证明物品、地点规则和 source index，错误物品或错误规则行继续 fail closed。
- 隔离夹具只加载 Town、装备钓竿并选择原生可钓水域，不注入 `(O)388`；runner 同时保存同一状态的需求型 fishing forecast。至此入口配置为 `26/26`，但生产 evidence index 尚未重建，正式训练仍保持 `false`。

## 历史记录 · 2026-10-05 Full Shipment 社区中心奖励入口

- 奖励物代表固定为 `full_shipment:item:336 / (O)336 / creates_reward_item / bundle:Bulletin Board/33:reward`，继续复用 `community_center.donate_bundle_items` 下已经存在的待领奖候选、DailyPlan、动作编译器和 `executor.claim_community_center_bundle_reward` 原生菜单执行链。
- 隔离夹具只把当前原生 `Data/Bundles` 中的精确 bundle 置于“已完成、奖励未领取”状态，并移动到社区中心交互点；它不会创建或注入 `(O)336`。共享 runner 必须从透明快照再次验证 bundle key、奖励物品和精确权威来源，之后产品执行器才可通过原生 Junimo Note 奖励菜单领取。
- 当时 runner 配置入口达到 `25/26`；只剩 `native_location_fish_spawn`。本轮仍不增加生产 evidence index，正式训练保持 `false`。

## 历史记录 · 2026-10-05 Full Shipment 商店购买入口

- 商店购买代表修正为 `full_shipment:item:380 / (O)380 / sells / shop:Blacksmith`。当前 fresh-save 权威分母的 13 条替代路线均可判定，150g 的 Blacksmith 购买是唯一 Pareto 前沿；候选显式约束 `Blacksmith`、铁矿 `(O)380`、单价不高于 150 和数量 1，继续复用 `economy.buy_supplies` 的跨图路径、柜台交互和 `executor.buy_shop_item`。旧木材 `(O)388` 代表同时存在 Town 钓鱼杂物路线，缺少对应概率证据时会被严格 Teacher 正确拒绝，不再用它伪造单一购买分母。
- 隔离夹具只使用既有 `debug.advance_time_to` 把春 1 推进到 9:00；不会打开菜单、生成物品或修改商店库存。候选来源身份由透明商店预览的 `shop_id` 验证，关闭或不可达时仍在上游排除。
- 当时 runner 配置入口达到 `24/26`；剩余 2 层为奖励物和地点鱼。本轮不增加生产 evidence index，正式训练仍为 `false`。

## 历史记录 · 2026-10-05 Full Shipment 晶球掉落入口

- 晶球代表固定为 `full_shipment:item:386 / (O)386 / native_geode_drop / geode:791:1:random:6`，输入为金色椰子 `(O)791`，执行继续复用 `processing.crack_geode -> executor.crack_geode` 的铁匠柜台与原生 `GeodeMenu` 链。
- 隔离夹具只搜索 `geodes_cracked_before` 以定位能产生目标的原生 RNG 前态；透明桥必须同时投影 `(O)386` 和精确来源，随后产品执行器才会敲开晶球。夹具不生成铱矿，且第一颗金色椰子固定奖励分支被显式排除。
- 该阶段 runner 配置入口达到 `23/26`，随后商店购买入口使当时总数达到 `24/26`。本轮不增加生产 evidence index，正式训练仍为 `false`。

## 历史记录 · 2026-10-05 Full Shipment 蚯蚓地入口

- 蚯蚓地代表固定为 `full_shipment:item:330 / (O)330 / native_location_artifact_spot / location:Default:10`，复用现有 `foraging.excavate_artifact_spots -> executor.clear_obstacle` 原生锄地链。
- 隔离夹具不会直接生成黏土；它只轮换原生 `(O)590` 蚯蚓地坐标，并通过透明桥的精确输出和权威来源投影选择可产生目标的合法前态。每次尝试前移除上一个夹具蚯蚓地，避免失败样本污染候选池。
- 该阶段 runner 配置入口达到 `22/26`，随后晶球掉落入口使当时总数达到 `23/26`。本轮仍未启动游戏，生产 evidence index 与训练准入状态不变。

## 历史记录 · 2026-10-05 Full Shipment 树液收集器与怪物掉落入口

- 野树树液收集器代表固定为 `full_shipment:item:725 / (O)725 / native_wild_tree_tapper_output / wild_tree:1:0`。夹具在同一格建立原生基础树与 `(BC)105` 树液收集器，并继续复用 `farm.collect_machine_outputs`、统一机器编译器、执行器和 verifier。
- 怪物掉落代表固定为 `full_shipment:item:766 / (O)766 / native_monster_drop_table / monster:Green Slime`。夹具只建立可归因的原生史莱姆及确定掉落，正式动作继续复用 `mining.reach_depth`、共享自动战斗和延迟拾取链。
- 该阶段 runner 配置入口达到 `21/26`，随后蚯蚓地入口使当时总数达到 `22/26`。PowerShell 解析、定向守卫 `26/26`、Core game-free `215/215` 与 RuntimeTestHarness `0 warning / 0 error` 均通过。本轮没有启动游戏，生产 evidence index 不增加，当时可独立验收的新版本原生证明仍只有 Sap 与放射性矿石，正式训练仍为 `false`。

## 历史记录 · 2026-10-05 Full Shipment 两类机器查询产物入口

- 已从当时权威 requirement/lowering 重新枚举 26 个运行时分层；此前脚本实际装配 17 层。本轮新增 `native_machine_flavored_output` 与 `native_machine_item_query_output`，该阶段配置入口达到 `19/26`。随后树液收集器与怪物掉落入口使当时总数达到 `21/26`。
- flavored 代表固定为 `full_shipment:item:340 / (O)340 / machine:(BC)10:rule:0:output:0`；item-query 代表固定为 `full_shipment:item:257 / (O)257 / machine:(BC)128:rule:0:output:2`。两者复用现有机器透明投影、`farm.collect_machine_outputs`、DailyPlan、编译器、产品执行器与 verifier，没有新增机器动作系统。
- 测试夹具现在显式携带原生 `lastOutputRuleId`。普通 `machine_output` 代表强制 `Default`，两类 legacy query 代表强制空值；非空规则在原生 `Data/Machines` 中不唯一时立即失败关闭，避免三个机器分层在真正运行前串线。
- PowerShell 解析通过，定向 game-free 守卫 `21/21`，RuntimeTestHarness 使用 E 盘隔离游戏引用编译为 `0 warning / 0 error`。本轮未启动游戏，因此生产 evidence index 不增加，当时可独立验收的新版本原生证明仍只有 Sap 与放射性矿石，正式训练仍为 `false`。

## 历史记录 · 2026-10-05 原生样本收口与测试制度收缩

- 原生放射性矿石样本已通过：`runtime-full-shipment-radioactive-node-20261005-134024` 在异机 `F:\StardewAI-TestLab` 以 `(O)95 -> GameLocation.breakStone -> (O)909` 完成 `native_radioactive_ore_node`，复用 `mining.reach_depth -> executor.mine_stone -> executor.pickup_debris`，没有第二套采矿执行器。候选身份和同地点目标证据均按目标逐项 fail-closed。
- Sap 当前版本重放已通过：`runtime-full-shipment-sap-refresh-20261005-183733` 完成采集、入箱、回家、原生睡眠与次日结算；睡眠为 `applied/verified`，日期 `1 -> 2`、`total_days 0 -> 1`、`active_menu=none`，recurrence checkpoint 为 `1` 次验证且剩余 `153`，rollout proof 独立重建通过。
- ShippingMenu 后台关闭不再移动系统光标。执行器使用 SMAPI `Escape` 按键覆盖并等待原生菜单关闭；本轮产物中旧错误 `shipping_summary_cursor_position_mismatch` 命中数为 `0`。
- 本轮还暴露并修正了部署漂移：此前远端源码/Release DLL 已更新，但 Mods 仍加载旧 Debug DLL。两个部署脚本现在对 `-NoBuild` 执行源码时间戳防陈旧检查，并对每个部署文件校验源/目标 SHA-256。定向守卫 `3/3`、Core game-free `212/212`；此前 Backend `225/225`、Bootstrap/Harness clean 继续有效。
- 防风草当时版本全量证明因低边际覆盖主动停止，已有产物保留但不得计入新证据。严格生产索引本轮不宣称 `3/26`；当时可独立验收的新版本原生证明为 Sap 与放射性矿石两层，正式训练仍为 `false`。
- 测试改为三层：动作/执行器改动只跑对应原生链路；共享代码跑离线全套和少量代表性原生回放；26 层生产索引只在分层新增、权威字典/日历变化或训练准入里程碑重建，不再随每次小改重复展开。
- 拓扑固定：源码仓库 `I:\StardewValleyAICompanion`，本机轻量/历史运行数据在 `E:`，高负载隔离游戏测试在异机 `F:\StardewAI-TestLab`。

## 历史记录 · 2026-10-05 Full Shipment 树苔藓样本复用原生镰刀链

- `(O)Moss` 已按唯一权威身份 `full_shipment:item:Moss / native_tree_moss_harvest / Tree.CreateMossItem / foraging.harvest_tree_moss` 接入共享样本 runner。
- proof 根外只用现有 `debug.setup_clear_obstacle` 建立成熟、无种子、带苔藓的 base `Tree` 和原生镰刀；正式 acquisition 继续复用既有透明投影、候选、DailyPlan、`executor.clear_obstacle`、原生 `Tree.performToolAction -> Tree.CreateMossItem` 与严格回执，没有新增第二套苔藓执行器。
- 严格 resume tuple 为 `full_shipment:item:Moss|(O)Moss|native_tree_moss_harvest`。`LZT` 样本守卫 `17/17`、Core game-free `138/138`、Bootstrap Release `0 warning / 0 error`。尚无 fresh Full Shipment receipt，生产索引仍为 `2/26`；当时已装配待运行 14 层、未装配 10 层。

## 历史记录 · 2026-10-05 Full Shipment 样本执行绑定按路线类型失败关闭

- 每个现有样本场景现在显式声明唯一预期 `route_kind`；生成 execution binding 后、进入任何游戏动作前，runner 会逐字段核对 `requirement_id / qualified_item_id / route_kind`。
- 同一需求或物品存在多条来源时，Teacher 即使选中了正确 requirement，只要来源层不符也会立即失败，不再把错误路线带入昂贵的原生运行。resume 端原有严格 tuple 校验继续保留，形成执行前与断点恢复双门。
- `LZT` 上样本守卫 `16/16`、Core game-free `137/137`、Bootstrap Release `0 warning / 0 error`。这是防误跑与节省测试资源的控制改进，不产生新的原生回执，生产索引仍为 `2/26`。

## 历史记录 · 2026-10-05 Full Shipment 太阳能板产物样本入口

- `native_solar_panel_output` 的唯一权威身份为 `full_shipment:item:787 / (O)787 / machine:(BC)231:OutputSolarPanel / farm.collect_machine_outputs`。
- proof 根外夹具锁定太阳能板机器 ID `231` 与电池组 `(O)787`，随后复用现有专用透明投影、共享机器候选、DailyPlan、编译器、`executor.collect_machine_output` 和 verifier。严格 resume tuple 为 `full_shipment:item:787|(O)787|native_solar_panel_output`。
- `LZT` 上样本守卫 `15/15`、Core game-free `136/136`、Bootstrap Release `0 warning / 0 error`。尚无 fresh Full Shipment acquisition proof，生产索引仍为 `2/26`。

## 历史记录 · 2026-10-05 Full Shipment 机器直接产物样本入口

- `machine_output` 的代表采用 `full_shipment:item:257 / (O)257 / farm.collect_machine_outputs`，权威来源固定为蘑菇树桩 `machine:(BC)128:rule:Default`。
- proof 根外夹具同时锁定机器 ID `128` 与输出 `(O)257`，随后复用现有机器投影、候选、DailyPlan、编译器、`executor.collect_machine_output` 和 verifier。严格 resume tuple 为 `full_shipment:item:257|(O)257|machine_output`；没有把相邻的 `native_machine_item_query_output` 混入本层。
- `LZT` 上样本守卫 `14/14`、Core game-free `135/135`、Bootstrap Release `0 warning / 0 error`。本层尚无 fresh Full Shipment acquisition proof，生产索引仍为 `2/26`。

## 历史记录 · 2026-10-05 Full Shipment 鱼塘产物样本复用原生收取链

- `native_fish_pond_output` 的代表采用鱼籽 `full_shipment:item:812 / (O)812 / fishing.service_fish_ponds`，夹具鱼种为鲟鱼 `(O)698`。权威清单包含鲟鱼产生 `(O)812` 的精确 route occurrence，因此没有沿用未被现有 smoke 精确覆盖的建议虚空蛋 `(O)305`。
- 样本在 proof 根外复用 `debug.setup_fish_pond_output`，随后走唯一的鱼塘透明投影、DailyPlan、编译器、`executor.collect_fish_pond_output` 和 verifier。严格 resume tuple 为 `full_shipment:item:812|(O)812|native_fish_pond_output`。
- `LZT` 上样本守卫 `13/13`、Core game-free `134/134`、Bootstrap Release `0 warning / 0 error`。历史鱼塘 output smoke 只证明链可复用；本层尚无 fresh Full Shipment acquisition proof，生产索引仍为 `2/26`。

## 历史记录 · 2026-10-05 Full Shipment 高级动物产物样本入口

- `native_farm_animal_deluxe_produce` 的代表采用大瓶牛奶 `full_shipment:item:186 / (O)186 / farm.collect_animal_products`，权威清单中精确来源为 Brown Cow 与 White Cow。
- 样本与普通牛奶共用 `debug.setup_animal_product_target` 配置表、Milk Pail、唯一候选/编译/执行/verifier 链；严格 resume tuple 为 `full_shipment:item:186|(O)186|native_farm_animal_deluxe_produce`，没有新增高级产物执行器。
- `LZT` 上样本守卫 `12/12`、Core game-free `133/133`、Bootstrap Release `0 warning / 0 error`。现有 EVD-222 证明 Milk Pail 链可复用，但不构成 `(O)186` 的 Full Shipment 原生回执；生产索引仍为 `2/26`。

## 历史记录 · 2026-10-05 Full Shipment 基础动物产物样本复用挤奶链

- `native_farm_animal_produce` 的运行时分层代表采用牛奶 `full_shipment:item:184 / (O)184 / farm.collect_animal_products`。该 requirement 在权威清单中精确绑定 Brown Cow 与 White Cow，避免把建议鸡蛋 `(O)176` 错配到只验证过挤奶/剪毛的夹具。
- 样本在 proof 根外使用既有 `debug.setup_animal_product_target` 建立 `White Cow / Milk Pail / (O)184 / quality 2 / cracker x1`，随后复用唯一的候选、DailyPlan、编译器、`executor.collect_animal_product` 和 fresh verifier。严格 resume tuple 为 `full_shipment:item:184|(O)184|native_farm_animal_produce`。
- `LZT` 上样本守卫 `11/11`、Core game-free `132/132`、Bootstrap Release `0 warning / 0 error`。EVD-222 的精确牛奶回执只证明链可复用；本层尚无 fresh Full Shipment acquisition proof，生产索引仍为 `2/26`。

## 历史记录 · 2026-10-05 Full Shipment 果树产物样本复用原生果树链

- `native_fruit_tree_produce` 的运行时分层代表采用樱桃 `full_shipment:item:638 / (O)638 / foraging.harvest_fruit_tree`。它与清单中的其他果树产物属于同一分层；选择樱桃是因为现有 `fruit_tree / single_normal` 原生夹具和 action smoke 已精确覆盖该物品，不需要为苹果另造执行路径。
- 共享 forage 场景表新增 `fruit_tree / single_normal / Farm 64,15`，继续复用既有果树候选、DailyPlan、编译器、执行器和 verifier。严格 resume tuple 为 `full_shipment:item:638|(O)638|native_fruit_tree_produce`，未知组合仍失败关闭。
- `LZT` 上样本守卫 `10/10`、Core game-free `131/131`、Bootstrap Release `0 warning / 0 error`。历史果树 smoke 只证明原生链可复用；尚无本层 fresh Full Shipment acquisition proof，生产索引继续为 `2/26`。

## 历史记录 · 2026-10-05 Full Shipment 地点觅食物样本入口

- `native_location_forage_spawn` 的生产代表为 `full_shipment:item:16 / (O)16 / foraging.collect_spawned_objects`。现有 EVD-211 链覆盖透明 spawned-object 投影、DailyPlan、`executor.collect_spawned_object` 与原生拾取回执。
- 共享 forage 场景表新增 `spawned_object / ordinary / Forest`，请求 `debug.setup_forage_source_fixture` 在 proof 根外建立 `(O)16`；实际合法格由 fixture 验证并通过 fresh snapshot 进入候选，不把请求坐标冒充最终坐标。
- 严格 resume tuple 为 `full_shipment:item:16|(O)16|native_location_forage_spawn`。`LZT` 上样本守卫 `9/9`、Core game-free `130/130`、Bootstrap Release `0 warning / 0 error`。
- 尚无本层 fresh Full Shipment acquisition proof，生产索引继续为 `2/26`。

## 历史记录 · 2026-10-05 Full Shipment 春葱样本复用作物采集链

- 权威路线固定为 `full_shipment:item:399 / (O)399 / native_spring_onion_harvest / Crop.harvest / whichForageCrop 1 => (O)399 / foraging.harvest_spring_onions`。既有 EVD-335 原生 DailyPlan smoke 验证 `executor.harvest_crop`、背包增量、作物移除和 Foraging XP `+3`。
- 防风草与春葱现在由同一 crop 场景表驱动 `debug.setup_harvest_crop_target`：防风草使用 `seed_id=472`，春葱使用 `rule_key=spring_onion`。fixture 仍在 proof 根外，没有新增作物执行器。
- 严格 resume tuple 为 `full_shipment:item:399|(O)399|native_spring_onion_harvest`。`LZT` 上样本守卫 `8/8`、Core game-free `129/129`、Bootstrap Release `0 warning / 0 error`。
- 历史 EVD-335 只证明执行链可复用；新的 Full Shipment acquisition proof 待采集，生产索引仍为 `2/26`。

## 历史记录 · 2026-10-05 Full Shipment 岛屿棕榈样本精确地点根

- `native_wild_tree_seed` 的代表路线为 `full_shipment:item:88 / (O)88 / wild_tree:6 / foraging.harvest_tree_product`。匹配的 `island_palm` 原生 smoke 位于 `IslandSouth 20,20`，不能借用 Farm 根。
- 共享 forage 场景配置现显式携带 `LocationId / TargetTileX / TargetTileY`。现有样本继续绑定 `Farm 64,15`，岛屿棕榈单独绑定 `IslandSouth 20,20`；acquisition 根必须等于场景声明的精确地点。完整 Sap recurrence 仍要求 Farm 且 `total_day=0`，没有被放宽。
- 严格 resume tuple 为 `full_shipment:item:88|(O)88|native_wild_tree_seed`。`LZT` 上样本守卫 `7/7`、Core game-free `128/128`、Bootstrap Release `0 warning / 0 error`。
- 这只完成跨地点样本入口。新 Full Shipment 原生回执尚未产生，生产索引仍为 `2/26`。

## 历史记录 · 2026-10-05 Full Shipment 野树落种样本入口

- `native_wild_tree_seed_drop` 的生产代表为 `full_shipment:item:408 / (O)408 / wild_tree:2:0 / foraging.harvest_tree_product`。已有 `fall_hazelnut` 原生 smoke 在 Farm 精确产出 `(O)408`，验证原生 `checkAction`、种子消耗、完整输出域、零 Foraging XP 与空槽恢复。
- Full Shipment runner 现通过共享 forage 场景表复用 `wild_tree / fall_hazelnut` fixture 和既有树产品候选、编译器、执行器、verifier；resume 只接受精确 tuple `full_shipment:item:408|(O)408|native_wild_tree_seed_drop`。
- `LZT` 上样本守卫 `6/6`、Core game-free `127/127`、Bootstrap Release `0 warning / 0 error`。现有 action smoke 仍不替代 fresh Full Shipment acquisition rollout proof，生产索引保持 `2/26`。
- 岛屿棕榈 `(O)88` 虽有匹配 smoke，但其证明根必须位于 `IslandSouth`，本轮没有放宽 runner 的 Farm 根合同；该层留作独立跨地点切片。

## 历史记录 · 2026-10-05 Full Shipment 茶树样本复用既有灌木链

- 生产清单将茶叶 `(O)815` 精确绑定为 `full_shipment:item:815 / native_tea_bush_harvest / Bush.GetShakeOffItem / foraging.harvest_bushes`。已有 `tea_leaf` 原生 action smoke 验证输出 `(O)815`、原生 `checkAction`、灌木偏移/产物增量和 XP/追踪器合同。
- Full Shipment runner 新增 `tea_bush_harvest_sample`，复用 `debug.setup_forage_source_fixture` 的 `bush / tea_leaf` 配置及既有灌木候选、DailyPlan、编译器、执行器和 verifier。浆果、姜、茶叶的 fixture 参数现由同一场景配置表生成，避免继续扩展二选一分支。
- `LZT` 上专项守卫 `5/5`、Core game-free `126/126`、GoalConditionedBootstrap Release `0 warning / 0 error`。首次远端命令因漏传仓库规定的 `GameFreeGovernance=true` 而在 ModBuildConfig 的 GamePath 检查前停止，恢复正确参数后全部通过；这不是代码测试失败。
- 历史茶树 action smoke 不等于当时 Full Shipment acquisition rollout proof。浆果、姜、茶叶均待 fresh 原生样本和独立 proof，生产索引继续为 `2/26`，正式训练仍禁用。

## 历史记录 · 2026-10-05 Full Shipment 姜采集样本入口与异机验证

- 权威需求构建器与 lowering 已锁定姜 `(O)829` 为 `full_shipment:item:829 / native_ginger_harvest / foraging.harvest_ginger`，来源语义为 `Crop.hitWithHoe` 的 `whichForageCrop 2 => (O)829`。样本复用既有 `debug.setup_forage_source_fixture`、候选、DailyPlan、动作编译器和原生姜采集执行链，没有新增第二套执行器。
- `Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1` 新增 `ginger_harvest_sample`，与浆果样本共用 forage fixture 编排，但分别绑定 `dry_standard` 与 `berry_standard`；`Resume-RuntimeFullShipmentAcquisitionProof.ps1` 只接受精确的 requirement、物品与 route kind 三元组，未知组合仍失败关闭。
- 为避免本机再次因测试负载卡死，本轮较大验证转移到 `LZT` 隔离测试目录。远端 GoalConditionedBootstrap Release 构建为 `0 warning / 0 error`，acquisition route dispatch 和 Full Shipment 静态可编译性自测通过，Core game-free `125/125` 通过；本机只执行轻量解析、差异与 Git 检查。
- 这些结果证明样本入口、映射和离线合同没有回退，不是原生游戏运行证据。新电脑当时没有游戏运行环境，因此浆果和姜均仍待 fresh 原生执行、终态核验及独立 rollout proof；生产索引继续严格保持 `2/26`，`formal_product_training_authorized=false`。
- 下一固定动作：继续为剩余分层准备可复用样本入口；当具备可交互游戏环境时，优先运行浆果，再运行姜。只有每条原生回执被生产 evidence index 接纳后才能逐层增加覆盖数。

## 历史记录 · 2026-10-04 Full Shipment 灌木采集样本已就绪、待原生运行

- 权威 requirement/lowering 已复核 `(O)296`：`full_shipment:item:296`、`native_bush_shake`、`foraging.harvest_bushes`，与已有 `executor.harvest_bush` 原语及 `berry_standard` 原生夹具一致；没有新增候选、编译器或执行器。
- `Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1` 现支持 `berry_bush_harvest_sample`，按夹具设置后的真实 `total_days` 构建目标日期；仅完整 Sap recurrence 继续强制第 0 天根，因此样本适配没有放宽递归证明。
- `Resume-RuntimeFullShipmentAcquisitionProof.ps1` 不再写死防风草摘要，而是从已验证 execution binding 读取 requirement、物品和 route kind，并对当前两种样本严格映射。未知组合失败关闭。旧防风草证据已离线完整重放通过。
- PowerShell 解析、GoalConditionedBootstrap Release、acquisition route dispatch、自包含 Full Shipment 静态可编译性及 Core game-free `124/124` 均通过。当时系统没有可用的交互桌面会话，MonoGame 原生运行没有启动；因此生产索引仍严格保持 `2/26`，不得提前声称 `3/26`。
- 下一固定动作：出现交互桌面会话后运行一次浆果灌木样本；若原生执行、fresh 终态和最终 rollout proof 全部通过，再登记 `full_shipment.runtime_sample.04.native_bush_shake` 并重建严格索引为预期 `3/26`。

## 历史记录 · 2026-10-04 审计维护：Issue 与文档状态清理

- 新建 #162 跟踪 `scripts/Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1` 的维护性拆分。当前脚本约 1.4k 行，同时承担进程生命周期、快照、规划轴、Teacher、rollout proof、Sap 出货、恢复/睡眠和 recurrence checkpoint 编排。该项定级为 P2 维护债，不阻塞当前证据工作；拆分必须复用既有 planner/compiler/executor/verifier，不得形成第二套运行链。
- 关闭历史 P0 #7。当前 TransparentBridge 已移除 `ApplyAiControlSettings` 与直接 `Game1.options.*` 写路径；Capability Manifest 从真实 Adapter 与二进制身份构建；event.v2 使用 `observed_snapshot_hash / published_snapshot_hash / snapshot_relation`；Bridge 继续 `CanExecuteCommands=false / CanWriteGameState=false`。
- #6、#8、#12 经 fresh audit 后继续保持开放。#6 的仓库级 E3/E4 Evidence Index/Build Manifest/Action Capability Matrix 尚未全部闭合；#8 的完整 locked restore + TRX + authoritative materialization + per-commit CI 仍未确认完成；#12 虽已有 material/currency reservation、partial consume、relocation、machine-support intent 等实现，但通用 commitment union、CoopOwnership 与 Interaction Lease 仍未完整闭合。
- `FORMAL_FULL_TRAINING_READINESS_CN.md` 已补 2026-10-04 当时口径：Full Shipment 保留 154/154 静态全覆盖和完整 recurrence 的游戏/可选验收能力，但训练运行时证据采用 26 个真实执行层的分层抽样；历史 `0/154` 与“154 recurrence 是训练前置”的描述仅作历史记录。
- 本轮没有发现需要新增 P0/P1 的代码质量回退；当前最明确的新维护债仍是 #162。

<!-- BEGIN GENERATED CURRENT CHECKPOINT -->
## Machine-generated current checkpoint

- Source commit: `4141faf511bd07879b83ec36cd1448b57e42ddc7` (generation input; must be an ancestor of current HEAD)
- Latest evidence: `EVD-338`; generated at: `2026-10-28T22:48:57.6909601+00:00`
- Catalog: `242 registered / 244 semantic / 241 compiler-bound / 163 five-gate / 67 training-allowlist`
- Execution: `154 product-executor / 2 catalogued-blocked`
- Native evidence: `322 surfaces / 448 branches / 150 map tokens`; fingerprint: `a2ba3ac0193fa78aece39a1eb4c18cb3c9b3248ea7390194dcfe61605af02d0e`
- Planning catalog: `planning_semantic_catalog_complete`; `stardewai.planning_semantic_catalog_fingerprint.v1`; fingerprint: `ef0335aa0fca8744c3f61bee7e2f32075857c3c60eef32da1057bb21e699da5a`
<!-- END GENERATED CURRENT CHECKPOINT -->

## 历史记录 · 2026-10-04 Full Shipment 普通作物运行时样本闭合

- Stardew Valley 1.6.15 反编译复核确认：普通作物的来源种子身份来自 `Crop.netSeedIndex`，`Crop.whichForageCrop` 只适用于野生种子分支。透明桥已按该边界修正，普通防风草实时候选现在发布 `harvest_source_seed_id=472`，不再因来源字段为空被 acquisition dispatch 拒绝。
- 隔离运行 `runtime-full-shipment-parsnip-sample-20261004-135111` 已沿唯一现有链完成 `farm.maintain_crops -> executor.harvest_crop`：严格 dispatch 成功，原生回执为 `applied/verified`，作物从成熟变为已移除，背包增加 `(O)24 x1`，Farming XP `0 -> 8`。随后结算、checkpoint 与最终独立 rollout proof 全部通过。
- 生产抽样索引现为 `2/26`：`harvests_as` 由防风草证明，`native_wild_tree_chop_drop` 继续由 Sap 证明；共享出货/睡眠 recurrence 仍通过，剩余 24 层，`formal_product_training_authorized=false`。这不是 `2/154` 进度，也不得退回逐物品重复执行。
- 中间结算新增 verified-artifact 快路径：复用前必须逐项校验 schema、状态、路线/候选身份和全部源文件 SHA-256；最终 rollout proof 仍执行一次完整独立重建。严格/快路径 JSON 等价回归通过。新增续证脚本只从已完成的原生执行产物接续结算与证明，不重启游戏或重做动作。
- 验证：GoalConditionedBootstrap 与 Backend Release 构建均为 `0 warning / 0 error`，`self-test-acquisition-route-dispatch` 通过，普通作物来源守卫 `1/1` 通过，严格生产索引发布 `verified=2 / missing=24`。下一固定切片是盘点剩余 24 层中可复用的既有原生 smoke，并逐层生成同一 acquisition rollout 合同；不得新增第二套候选、编译器或执行器。

## 历史记录 · 2026-10-03 Full Shipment 抽样证据索引基础设施

- 新增 `build-full-shipment-runtime-sample-evidence-index`。它以 `full_shipment_static_compilability_inventory.v2` 的分层结果为唯一分母，重新构建每个 acquisition rollout proof，并精确匹配 `route_occurrence_id`、权威 requirement/lowering 哈希和所属路径族；只读 action smoke、人工声明和同一层内重复物品均不能增加覆盖数。
- 出货、睡眠结算和 Full Shipment 进度增量是 26 个 acquisition 路径族共享的尾链，只要求一条经 `FullShipmentRecurrenceProofBuilder` 重新计算且与落盘 checkpoint 完全相等的真实 recurrence prefix。每个路径族则只要求一条完整 acquisition proof，不要求再为该层其他物品重复出货。
- 索引显式区分 `partial_runtime_sample_evidence` 与 `verified_complete_runtime_sample_evidence`，发布已验证数、缺失数和精确缺失层 ID；重复层、重复 route occurrence、陈旧 receipt、错误 authority 哈希、错误路径族及不完整 rollout 均 fail-closed。完整 154 项 recurrence 继续仅用于正常游戏和可选最终验收。
- Release 构建为 `0 warning / 0 error`；新索引的部分/完整/缺共享尾链/重复样本回归和原静态分层回归均通过。当时尚未把普通历史 smoke 冒充正式样本，Goal Method 生产覆盖仍为 `4/19`；当时后续计划只建立生产静态清单与证据 manifest，先登记 Sap 的精确 proof，再列出真正缺失的路径族。

## 历史记录 · 2026-10-03 Full Shipment 验证改为分层抽样

- `154/154` 仍是权威静态分母和 AI 实际游玩时必须完成的游戏目标，但不再是开发验证或正式训练准入所需的 154 次重复原生运行。此前 `1/154` 只保留为首轮端到端诊断证据，不作为要求继续逐项跑到 154 的项目进度条。
- 生产 lowering 中的 `641` 条来源和 `154` 个物品归并为 `26` 个“路线族 + 执行签名”运行时抽样层。每层只需一条精确绑定来源、候选、编译队列、原生回执和 fresh 前后快照的代表证据；同层其余物品由 `154/154` 静态来源身份、参数编译和运行时绑定审计覆盖。已有合格原生 smoke 可以复用，不要求重新从新档重复采集。
- 完整 `154/154` fresh-save recurrence 构建器继续保留，用于 AI 正常游玩和最终可选的整局成就验收，不再阻塞 Full Shipment Teacher corpus、`4/19 -> 5/19` 或正式训练准入。最终 achievement 34 的 `153/154 -> 154/154` 原生边界仍由现有独立终态 smoke 验证。
- 当前机器可读清单升级为 `full_shipment_static_compilability_inventory.v2`，显式发布抽样层、`full_recurrence_required_for_training=false` 和 `full_recurrence_retained_for_acceptance=true`。当时后续计划是建立抽样证据索引，先复用已有真实回执，再只补没有代表证据的路线族；不得继续机械执行剩余 153 个物品。

## 历史记录 · 2026-10-03 Full Shipment 真实 fresh-save 首轮前缀闭合

- 隔离原生新档 `ProofFarm_450250338` 已完成第一条真实递归：从 Spring 1 的 `0/154` 出发，复用既有 `foraging.chop_wild_tree` 路线原生砍树、拾取 Sap、投入出货箱、回家并原生睡眠结算。没有 fixture、传送、直接改档或第二套动作执行器。
- 野树掉落采用 typed deferred-pickup 合同：编译时绑定树格、权威来源、保证掉落、执行前库存/地面物基线；运行时砍树后只允许在同队列内按 fresh state 重绑定拾取目标。队列收据校验同时覆盖普通连续步骤与显式 Teacher state rebind，未声明的状态重绑定继续 fail-closed。
- 出货阶段真实运行时，角色走向出货箱途中磁吸到第二个 Sap。Teacher 验证现在只在最后一个精确 `executor.ship_inventory_item_to_bin` 步骤同时证明库存 `2 -> 1`、出货箱 `0 -> 1`、队列最终哈希一致时接受该情况；整队列净库存不再被误当作唯一出货证据，其他库存或箱体漂移仍拒绝。
- 恢复链暴露了透明桥哈希边界问题：同一 `game_tick=1181` 的两份等价快照仅有 `machine_probe_cache_tick: 1179 -> 1181` 不同。该字段是采样缓存元数据而非游戏语义状态，现不再参与新状态哈希；后端兼容验证旧哈希。旧证据只有在两侧嵌入哈希各自可验证且规范化状态完全一致时才允许连接，真实库存、日期、地点或机器状态变化仍会断链。
- 离线全轴证明已生成 `full_shipment_recurrence_prefix_checkpoint.v1`：`prefix_proof_verified=true`、`verified_iteration_count=1`、`remaining_item_count=153`、`final_total_day=1`、`ready_for_next_iteration=true`、`complete=false`、`achievement_34_verified=false`。这是首个真实 `1/154` 检查点，不是完整 Full Shipment 训练准入；`formal_training_authorized=false` 保持不变。
- 下一固定切片从该检查点的 final snapshot 作为唯一递归根，选择下一个权威 acquisition endpoint，继续复用现有候选、编译、原生执行、出货和睡眠链。不得重写已完成动作，也不得把单轮前缀冒充 154 项完整证明。全轴前缀验证目前约需 12 分钟，后续可缓存不变的权威轴输入，但优化不得削弱证据重算与哈希绑定。

## 历史记录 · 2026-09-28 Full Shipment 跨图结算证明修复

- 审计发现 recurrence 证明错误地要求“投入出货箱后的快照”与“最终睡眠前快照”哈希完全相同，但现有 `recovery.stabilize_day` 按设计每次只执行一个原生连接点并刷新快照。角色从农场出货箱走回农舍必然改变状态，因此旧合同实际上只能依赖传送或调试夹具，不能承载真实新档证据。
- settlement manifest 现可记录最多 8 段有序 `recovery_transitions`。每段仍复用现有 recovery 候选、DailyPlan、动作编译器和产品执行器，只允许已知的跨连接点、关闭阻塞菜单或短等待原语；中间段禁止睡眠。队列、前后快照和原生执行回执都经共享校验器重建验证，并强制同一存档/玩家、哈希连续、日期不变、Full Shipment 进度不变以及本轮物品仍精确留在出货箱中。
- prefix/whole-recurrence 的每轮 evidence 会固化每段恢复队列、前后快照和执行回执的 SHA-256；重复文件、断链、乱序、跨角色、提前跨日、出货进度漂移和待结算物品消失均失败关闭。链尾仍必须接原有 ordinary/terminal 原生睡眠回执，没有新增第二套路由、恢复、睡眠或出货实现。
- 专项 Release 构建为 `0 warning / 0 error`，Full Shipment settlement 自测覆盖正常恢复及睡眠混入、日期/进度/出货箱/角色/顺序篡改拒绝。该修复只解除首轮真实 recurrence 的证明层硬阻塞，生产进度仍是 `0/154`，`formal_product_training_authorized=false`；当时后续计划仍是从已验证原生 fresh save 采集 Sap 的第一条真实 `1/154` 前缀。

## 历史记录 · 2026-09-28 原生 fresh-save 运行根已闭合

- `RuntimeTestHarness` 新增严格隔离的 `CreateFreshSave` 模式。实现先按当前 1.6.15 反编译结果调用 `Game1.resetPlayer()`，设置确定性的标准农场角色配置，再调用原生 `TitleMenu.createdNewCharacter(skipIntro: true)`；不构造或改写存档 XML，也不复制历史存档充当新档。
- fresh-save 模式必须同时满足 `STARDEWAI_TRAINING_MODE=1`、显式且相同的 `STARDEWAI_TEST_SAVES`/`STARDEWAI_SAVE_ISOLATION_PATH`、空的 `fresh-save-*` 专用目录、非盘符根和非重解析点，并且不能指定已有 slot。任一条件不满足时整个 harness fail-closed，执行器也不会启动。
- 新增隐藏静音 `Invoke-RuntimeFreshSaveSmoke.ps1`。真实运行 `runtime-fresh-save-20260928-133156` 已在游戏 1.6.15 中原生创建 `ProofFarm_450250338`，透明桥读到 Spring 1、Full Shipment `0/154`、无 achievement 34，并等到主存档和 `SaveGameInfo` 都落盘后才通过。第一次运行暴露并修复了健康检查硬编码 60 秒、忽略脚本 180 秒启动预算的问题；失败证据保留，未覆盖旧存档。
- 这只闭合“可复现的新存档根”，尚未闭合任何 Full Shipment acquisition/deposit/settlement 迭代，因此生产 recurrence 仍是 `0/154`，`formal_product_training_authorized=false`。下一切片从该原生根执行可审计的出屋连接动作，再用现有 `foraging.chop_wild_tree` 获取 Sap、原生投入出货箱并跨日结算，最后交给 prefix checkpoint 验证为第一条 `1/154` 证据；不得增加第二套移动、砍树或出货实现。

## 历史记录 · 2026-09-28 Full Shipment 真实 recurrence 增量检查点

- 新增 `build-full-shipment-recurrence-prefix-checkpoint`，复用既有 whole-recurrence 的 acquisition rollout、`economy.ship_items` Teacher receipt、ordinary/terminal sleep settlement 验证核心，使真实证据可以按 `1..154` 轮逐步落盘，而不必等 154 轮全部完成后才发现中间链路错误。
- prefix 仍必须从同一新存档的第 0 天、零出货状态开始；每轮 shipped count 必须连续 `n -> n+1`，前一轮 settlement after snapshot 必须是下一轮 acquisition 的精确根。未完成前缀不得出现 achievement 34、terminal settlement 或占用第 224 天，且 checkpoint 会发布精确的剩余 requirement/item 集合与最终快照哈希。
- 原 `build-full-shipment-recurrence-proof-receipt` 现在调用同一共享验证核心，仍严格要求全部 `154` 轮、最终 achievement 34 和完整分母；没有复制第二套 recurrence、候选、编译或执行系统。空前缀、提前终态、把前缀冒充完整序列、把完整序列冒充未完成前缀及超期非终态均有拒绝回归。
- 当时只完成增量验证基础设施，尚未取得真实 fresh-save 第一轮证据，因此生产 recurrence 进度仍是 `0/154`，`formal_product_training_authorized=false` 不变。当时后续计划是由隐藏静音原生运行生成第一轮真实 manifest/artifacts，再用该 checkpoint 验证并固定下一轮根；合成夹具不得计入进度。

## 历史记录 · 2026-09-28 Full Shipment 154 项静态可编译性清单完成

- 新增 `build-full-shipment-static-compilability-inventory`，从权威需求清单、acquisition lowering、动作实现 reconciliation 和五类 supporting-transition 终端谱系证据中重建 Full Shipment 的静态可编译性；它不信任调用方声明的汇总状态。
- 当时生产输入严格核对为 `154/154` 个必需物品组、`641` 条权威 acquisition route、`26` 种实际路由、`504` 个 `route_kind + source_id` 身份和 `24` 个被引用动作选项。全部来源身份契约、endpoint/supporting option 编译绑定、运行时绑定和支持链谱系通过，阻塞项为 `0`。
- 权威 acquisition 总目录的 `33` 种路由均有显式来源身份契约；未知路由种类、空/畸形来源、陈旧 lowering 哈希或摘要计数、缺失依赖轴、未绑定编译器、缺失运行时绑定和不完整支持谱系全部 fail-closed。没有引入第二套收集规划、编译器或执行器。
- `static_compilability_complete=true` 只证明：当时状态满足先决条件时，每个 Full Shipment 需求至少存在一条可由现有链路精确编译的路线。它不证明同一新存档已实际完成 154 项递归 acquisition/shipping 闭环；因此 `fresh_save_recurrence_evidence_complete=false`、`formal_product_training_authorized=false` 保持不变，Goal Method 生产覆盖仍为 `4/19`。
- 验证通过：Release 构建 `0 warning / 0 error`、新增静态清单正/负例、真实生产 154 项清单、`self-test-acquisition-route-dispatch`、`self-test-bootstrap-hermetic`、Core game-free `109/109` 以及完整 `self-test-current-stage-one-collection`。完整回归覆盖五类 supporting transition、三分区连续重规划及篡改拒绝，没有发现共享链路回退。
- 下一固定切片是建立第一条真实新存档 Full Shipment recurrence 证据链：从 fresh snapshot 选择权威需求，经现有 route/source/support 编译与原生执行，取得终态物品及 shipping credit，重新规划下一项，并把每一轮的哈希、队列、原生回执和剩余分母落盘。不得以合成 fixture 冒充生产证据，也不得重新开发已有的 154 项动作链。

## 历史记录 · 2026-09-28 Full Shipment 当时候选权威来源身份闭合

- 修复了 `CurrentFullShipmentTeacherFrontierBuilder` 的错误正标签边界：旧实现只要求当前候选产物精确、option 属于该物品的权威 endpoint，因而同一 option 下来自错误作物、商店或原生掉落源的候选仍可能被标成 Teacher 正例。现在 Full Shipment 前沿直接复用生产 acquisition dispatch 的唯一来源匹配器，同时绑定 `route_kind + source_id + qualified_item_id`；没有建立第二套来源解析或收集规划。
- acquisition 正例现在必须在该需求的 admitted endpoint routes 中恰好匹配一条权威来源。商店、作物种子来源、普通地点/矿井钓鱼、蟹笼、固定反编译原生来源和候选携带的 typed authoritative source JSON 均沿用实际调度规则；来源缺失、来源错误、来源歧义或多条 route 同时命中均失败关闭。原生 `economy.ship_items` 终态仍由精确 Full Shipment 贡献字段证明，不伪造获取来源。
- 同时修复共享参数读取器的歧义漏洞：两个同名同值参数过去会先经 `Distinct()` 折叠后被当成“唯一”，现在必须物理上恰好出现一次。Full Shipment 前沿与 acquisition dispatch 均加入重复同值、缺失、错误和歧义来源负例；Stage 1 共享防风草候选夹具补齐真实 `harvest_source_seed_id=472`，继续验证一次执行同时给 Full Shipment 和社区中心记 exact credit。
- 验证通过：GoalConditionedBootstrap Release `0 warning / 0 error`、`self-test-acquisition-route-dispatch`、完整 `self-test-current-stage-one-collection`、`self-test-bootstrap-hermetic`、Core game-free `109/109` 及 `git diff --check`。首次未带 `GameFreeGovernance=true` 的 Core 命令被本机缺少 SMAPI 的旧游戏目录门禁拒绝，使用仓库 CI 标准 game-free 命令重跑后全部通过；该环境性拒绝不计为代码测试失败。
- 本切片没有增加生产 Teacher/native 三分区证据，Goal Method 覆盖仍为 `4/19`，聚合状态仍为 `blocked_incomplete_goal_method_teacher_coverage`，`formal_product_training_authorized=false`。下一固定切片是对 154 项 ordered recurrence 的实际 endpoint/source/supporting-option 可编译性做精确缺口盘点，再按现有 dispatch/compiler 路径闭合第一组确定性来源；不得回退到“同产物 + 同 option”弱匹配，也不得新增第二套执行链。

## 历史记录 · 2026-09-28 宠物爱心真实三分区生产证据完成

- `Invoke-RuntimePetCareSmoke.ps1` 新增只运行终端宠物爱心转移的 `TeacherTerminalOnly` 模式，并持久化 fresh before/after 快照及原生执行结果。快照保存改为直接写入桥返回的原始 JSON；不再把 PowerShell 对象重新序列化后冒充权威快照。Windows PowerShell 5.1 的快照 GET 显式使用 `-UseBasicParsing`，避免旧 IE 解析器造成空引用和假超时。
- 三个隔离存档分别产生真实 train、validation、test 证据：`runtime-pet-love-train-raw-20260928-0915`、`runtime-pet-love-validation-raw-20260928-0920`、`runtime-pet-love-test-raw-20260928-0925`。每条均由原生 `executor.pet_interact` 完成友情 `994 -> 1000`、当天抚摸次数递增和 `petLoveMessage false -> true`，且 fresh 前后状态哈希不同。
- 严格 `PetLoveTeacherCorpusBuilder` 已重新计算三组原始快照状态哈希并接受全部来源；分区由 `save_id + player_id + total_day` 派生，结果为 `ready_split_complete_pet_love_teacher_corpus`、`3 rows / 3 partitions`，Teacher 比较与原生终态验证全部为真。此前由 PowerShell 重序列化生成的无效快照未进入该 corpus。
- 生产 Goal Method 覆盖在保持同一权威字典、依赖展开、动作矩阵和 claim ledger 的前提下由 `3/19` 提升至 `4/19`。宠物爱心 criterion 现为 `executable_frontier`、train/validation/test Teacher 与原生结果分区齐全、`coverage_gate_ready=true`；全局仍为 `blocked_incomplete_goal_method_teacher_coverage:4/19`，`formal_product_training_authorized=false`。
- 验证通过：PowerShell AST、宠物运行源码守卫 `5/5`、Core game-free `109/109`、GoalConditionedBootstrap Release `0 warning / 0 error`、`self-test-bootstrap-hermetic`、三次隐藏静音 Teacher 原生运行，以及普通抚摸/满心礼物/隔夜水碗的完整旧路径 `3/3`。隔夜测试后已用克隆自身的 `_old` 文件恢复第 223 天，不触及原始游玩存档。GitHub 当时没有新增代码审查 issue；#130 的剩余夹具拆分已在上一切片完成并关闭。
- 下一固定切片是补齐 Full Shipment 当前实时 acquisition candidate 的权威来源身份和所需 supporting option 编译，使既有 154 项 ordered recurrence 能消费真实当前端点；不得复制第二套收集规划、执行或训练链。随后才采集该方法的 fresh-save 完整证据。

## 历史记录 · 2026-09-28 Stage 1 collection 自测夹具分解（#130）

- 将 `BootstrapSelfTest.StageOneCollectionFrontier.cs` 从约 4900 行的单方法拆为薄入口和 8 个同前缀 partial 文件，职责分别为夹具路径/调度、静态权威输入、目标日期轴、组合提交与执行、组合语义、负例与篡改拒绝、当时前沿准入、共享构建支持；单文件最大约 1350 行。
- 拆分只显式传递原方法已有的 `fish`、`stateHash`、货币路由与 reservation 依赖，原有 `BuildFacilityFixtureChain` / `BuildProcessingFixtureChain` 仍是唯一共享实现，没有复制第二套候选、编译、执行或训练链。既有 `BootstrapSelfTest.StageOneCollectionFrontier.Fixture.cs` 内容保持不变。
- 验证通过：GoalConditionedBootstrap Release 构建 `0 warning / 0 error`、完整 `self-test-current-stage-one-collection`、`self-test-bootstrap-hermetic`、Core game-free `104/104`，且 `git diff --check` 通过。本切片不改变任何业务输出、训练准入或 `formal_product_training_authorized=false` 状态。

## 历史记录 · 2026-09-28 Skull Key 真实三分区生产证据完成

- `build-skull-key-teacher-corpus` 现已重放三条真实 `plan_execution_episode.v1`：既有 EVD-106 作为 train（第 209 天），本轮隔离运行 `runtime-skull-key-validation-20260928-062858` 作为 validation（第 223 天），`runtime-skull-key-test-20260928-064032` 作为 test（第 223 天）。三个分区均由 `save_id + player_id + total_day` 哈希派生，调用方不能指定分区。
- validation 隐藏静音运行通过 `32/32` 个原生步骤：119 层清障、下梯、120 层移动、`claim_skull_key` 的 `false -> true` 转移以及退出矿井全部验证；test 运行通过 `5/5` 个步骤，并在第 4 步完成相同终态。三条 corpus 行的 Teacher 对比与原生终态验证均为真，状态为 `ready_split_complete_skull_key_teacher_corpus`。
- 真实采集暴露并修复两个运行缺陷：无楼梯物品是已知业务阻塞，透明桥现发布 `blocked_no_staircase_inventory`，不再把整组矿井数据误判为不可读；矿井快照脚本的执行端健康检查现复用 `StartupTimeoutSeconds`，不再用 30 秒硬编码误杀正常冷启动。两项均有定向回归测试。
- 真实 EVD 还确认历史 episode 使用高层 `mining.obtain_skull_key` 队列项，并在 `normalized_command` 内下沉到 `executor.interact`；旧快照缺少 `time.total_days` 且使用历史字段 `skull_key_special_item_which`。适配器只在严格日历换算和唯一旧字段别名下兼容历史证据，冲突字段、冲突重复参数或队列漂移继续失败关闭。
- 生产 Goal Method 覆盖现为 `3/19`，Skull Key 的 Teacher 与原生终态分区均为 `train/validation/test`，该 criterion 已 `coverage_gate_ready=true`。全局仍为 `blocked_incomplete_goal_method_teacher_coverage`，`formal_product_training_authorized=false`；不得把单项闭环写成全量训练准入。下一固定切片是连接宠物爱心的真实三分区原生证据，然后再处理剩余依赖图，而不是继续扩写第二套矿井/箱子执行系统。

## 历史记录 · 2026-09-28 Skull Key Teacher 源适配与真实桥接契约修正

- 新增 `build-skull-key-teacher-corpus`。它不接受裸执行结果作为监督，而是从 `plan_execution_episode.v1` 重放并绑定 fresh before/after snapshot、有效队列项和原生 `training_execution_result.v1`。只有同一存档、玩家、版本和 Stage-1 日期内，普通矿井 120 层唯一原生 Skull Key 奖励箱从存在到消失、`player.has_skull_key` 从 `false` 到 `true`，且原生开箱、领奖和状态转移原因全部成立时，才生成 Teacher 行。
- 审查真实透明桥时发现字段漂移：桥原先发布 `skull_key_special_item_which`，编译器与既有快照契约读取 `special_item_which`。现已统一为后者，并增加 game-free 源守卫；否则合成夹具会通过，而真实 120 层快照永远无法编译领奖动作。
- corpus 会从 `save_id + player_id + total_day` 自行派生 train/validation/test，不接受调用方伪造分区。验证器还精确绑定 `queue_item_id`、`requested_effect`、`observed_effect`、changed facts、目标格、overlay object、`SkullKeyChest` 和 `player.has_skull_key=true` 后置条件。篡改 corpus 行、episode 队列项或 effect 均失败关闭。
- 定向三分区夹具使 Skull Key criterion 精确进入准入，覆盖报告从 `2/19` 变为 `3/19`；但该夹具不是生产证据。生产准入总门仍为 `2/19` criterion、`1/11` root method，Teacher 阻塞现为 `dependency_graph_incomplete:15`、`evidence_not_connected:2`，不再存在 `teacher_source_adapter_missing`。
- Release 构建零警告，Core game-free `104/104`、bootstrap hermetic、定向 Goal Method 覆盖/篡改回归与正式准入总门重算通过。`formal_product_training_authorized=false` 和 `ready_for_separate_promotion_review=false` 保持不变。当时后续计划不是再写 Skull Key 执行器或适配器，而是从三个独立存档/玩家/日期分区采集真实 120 层原生回执并接入生产 coverage request；未取得真实回执前不得把 `3/19` 夹具结果写成当时进度。

## 历史记录 · 2026-09-28 正式训练准入总门重算

- 新增只读命令 `build-goal-method-formal-training-admission-reconciliation`。它从原始 frontier 输入重建 19 项 Grandpa 21 分 criterion，重放 acquisition corpus 及其 rollout/admission 来源，核验 train/validation/test 分区，重新绑定 checkpoint 并用其权重重算训练/验证/测试指标，同时重建 5 类支持动作的终端谱系矩阵；旧汇总 JSON 不能自行证明通过。
- 当时可信基线是 `2/19` criterion、`1/11` root method、`3` 个来源/`9` 行 corpus，train/validation/test 各 `3` 行、`2` 个 pair，validation/test pair accuracy 均为 `1.0`，支持谱系 `5/5`。这只证明现有小语料和证据链内部一致，不代表样本规模充分，也不代表正式训练已经准入。
- 九项总门中，权威分母、corpus 重放、split 完整性、checkpoint/corpus 绑定、留出集指标重算、支持谱系和叶制品权限隔离通过；完整 Teacher 覆盖与引用动作执行库存仍失败。Skull Key typed adapter 完成后的精确 Teacher 缺口为 `dependency_graph_incomplete:15`、`evidence_not_connected:2`。
- 下游真实动作缺口缩到两组：三个清障 executor 缺透明读取、原生运行与 five-gate 证据；三个社交动作缺内部执行管线。高层策略动作只要能经既有 `internal_execution_pipeline` 下沉，就不再因没有直接 Product Executor 被误报为阻塞。
- pairwise ranker 的两个 checkpoint 加载入口现都会从绑定 corpus 和模型权重重算训练/验证/测试摘要；结构合法但伪造 `test_pair_accuracy` 的 checkpoint 会失败关闭。Release 构建、bootstrap hermetic 以及全新输出根上的完整 `self-test-current-stage-one-collection` 均通过。
- `formal_product_training_authorized=false`、`ready_for_separate_promotion_review=false` 保持不变。`obtain_skull_key` 与宠物爱心现在都已有 typed adapter，但都仍需生产来源接线和真实 train/validation/test 原生回执；不得用 hermetic fixture 冒充。

## 历史记录 · 2026-09-28 支持动作终端训练谱系覆盖矩阵 5/5

- `crop_planting` 现已复用既有 `farm.maintain_crops -> executor.plant_seed` 候选、编译和原生状态回执，完成 `support request -> atomic commit -> shared compilation -> native receipt -> nonterminal settlement -> fresh replan -> terminal rollout -> supervision` 的 file-backed 全链证明；没有新增第二套农田规划、动作编译、寻路、ledger 或执行器。
- 证明逐项核验防风草种子 `(O)472` 从 `3 -> 2`、目标地块作物从不存在变为唯一 `(O)24` 防风草、作物未死亡且尚未成熟，并完成唯一种子 material reservation。种植不会伪装成即时收获；fresh Teacher 在同一 `full_shipment:item:24` 需求内改选确定性商店终态备选，种植 run ID 仍从终态 native outcome 中排除。
- 严格覆盖矩阵现为 `5/5`：`crop_planting`、`machine_input_load`、`machine_input_material_transfer`、`machine_input_purchase`、`machine_capacity_establishment` 均恰好有一条可重算证据，全部 `coverage_verified=true` 且 `support_excluded_from_terminal_outcomes=true`；缺失、重复或支持动作泄漏为终态标签仍失败关闭。
- Release 构建、定向 crop planting 谱系、三条受共享夹具影响的机器支持链、acquisition dispatch、bootstrap hermetic 与全新输出根上的完整 `self-test-current-stage-one-collection` 均通过。全量报告状态为 `verified_complete_support_terminal_lineage_coverage`，无 missing/blocking 项。
- `formal_product_training_authorized=false` 保持不变。下一固定切片转入正式训练准入总门审计：从现有 corpus、split、rollout、admission 和 holdout 制品中重新计算剩余阻塞，区分预期的产品安全锁与真实缺口；不得因支持矩阵完成而直接放开训练。

## 历史记录 · 2026-09-28 支持动作终端训练谱系覆盖矩阵 4/5

- `machine_input_purchase` 现已复用既有权威商店报价、`economy.buy_supplies -> executor.buy_shop_item -> executor.close_menu`、原生购买回执与 currency reservation 结算，完成 `support request -> atomic commit -> shared compilation -> native receipt -> nonterminal settlement -> fresh replan -> terminal rollout -> supervision` 的 file-backed 全链证明；没有新增第二套商店、购买、寻路、ledger、终端编译器或执行器。
- 证明绑定唯一 `FixtureShop / fixture-wheat-seed / (O)262 / 80g / 1` 库存行，逐项核验金币 `1000 -> 920`、物品 `0 -> 1`、菜单安全关闭以及唯一货币 reservation `80 -> 0`。支持动作 run ID 继续从终端 native outcome 中排除。
- 该链暴露并修正了生产制品验证器中的契约矛盾：精确机器输入购买本来就以 `resolved_resource_input_miss` 进入 typed purchase prerequisite，旧验证却要求所有资源已经满足。现在只有通过既有 `ReservationMaterialInputs` 精确绑定校验的购买缺口可被接受；无购买前置或绑定不精确的资源缺口仍失败关闭。夹具同时补齐 `sleep_prompt_context`，没有跳过 `close_menu` 的 required-state 校验。
- Release 构建、定向 purchase 谱系测试、acquisition dispatch、bootstrap hermetic 与完整 `self-test-current-stage-one-collection` 均通过。矩阵现为 `4/5`，唯一缺口是 `crop_planting`；`formal_product_training_authorized=false` 保持不变。
- 下一固定切片是复用既有种子获取、农田位置方案、整地/浇水/播种队列、原生作物状态回执和材料 reservation 结算，建立 `crop_planting` 的同型 file-backed 终端谱系证明；不得新增第二套农田规划、动作编译、寻路、ledger 或终端执行实现。

## 历史记录 · 2026-09-28 支持动作终端训练谱系覆盖矩阵 3/5

- `machine_input_material_transfer` 现已复用既有 `inventory.transfer_item -> executor.move_to_tile -> executor.transfer_material`、原生搬运回执与 reservation relocation 结算，完成 `support request -> atomic commit -> shared compilation -> native receipt -> nonterminal settlement -> fresh replan -> terminal rollout -> supervision` 的 file-backed 全链证明；没有新增第二套搬运、寻路、ledger、终端编译器或执行器。
- 证明夹具从当前地图普通箱子精确搬运一份 `(O)262` 到玩家背包，逐节点、逐槽核验源数量 `1 -> 0`、目标数量 `0 -> 1`，并要求唯一活动 reservation 保持数量不变且迁移到实际观测到的背包槽。支持动作 run ID 仍必须从终端 native outcome 中排除。
- 该链暴露并修正了测试权威清单未列出真实支持选项 `inventory.transfer_item` 的问题；同时把新增搬运快照夹具拆到独立 partial 文件，避免继续扩张机器容量快照文件。生产候选、编译和执行路径均直接复用，没有按测试特例放宽准入。
- Release 构建、定向 material-transfer 谱系测试、acquisition dispatch、bootstrap hermetic 与完整 `self-test-current-stage-one-collection` 均通过。当时矩阵为 `3/5`，尚缺 `crop_planting` 与 `machine_input_purchase`；`formal_product_training_authorized=false` 保持不变。
- 下一固定切片是复用已经闭合的权威商店报价、开放窗口、`economy.buy_supplies`、原生购买回执和货币 reservation 结算，建立 `machine_input_purchase` 的同型 file-backed 终端谱系证明；不得新增第二套商店、购买、货币 ledger、寻路或终端执行实现。

## 历史记录 · 2026-09-28 支持动作终端训练谱系覆盖矩阵 2/5

- 新增严格的 file-backed 覆盖矩阵。支持类型只能从已结算支持请求的制品中推导，调用方不能自行声明；每类必须恰好有一条可重算的 rollout proof、controller admission 与 supervision dataset，缺失或重复均失败关闭。
- `machine_input_load` 与 `machine_capacity_establishment` 已完成 `support request -> atomic commit -> shared compilation -> native receipt -> nonterminal settlement -> fresh replan -> terminal rollout -> supervision` 全链证明。支持动作的执行 run ID 必须从终端 native outcome 中排除，即使二者合法共享同一个高层 route occurrence。
- 修正支持请求读取了非权威 `time.time_of_day` 的错误；现统一读取透明桥 schema 的 `time.time`。文件夹具同时补齐必需的 `player.inventory_capacity`，没有放宽 required-state 或期限门禁。
- Release 构建、acquisition dispatch、bootstrap hermetic 与完整 `self-test-current-stage-one-collection` 均通过。当时矩阵明确为 `2/5`，尚缺 `crop_planting`、`machine_input_material_transfer`、`machine_input_purchase`；`formal_product_training_authorized=false` 保持不变。
- 下一固定切片是复用既有 `inventory.transfer_item -> executor.transfer_material` 及其 relocation settlement，建立 `machine_input_material_transfer` 的同型 file-backed 终端谱系证明；不得新增第二套搬运、寻路、ledger 或终端编译实现。

## 历史记录 · 2026-09-28 机器容量支持过渡接入终端 rollout 证明链

- 机器放置现以真实文件制品完成 `support request -> commit receipt -> shared compilation -> native execution receipt -> nonterminal settlement -> fresh replan`，并把这份精确血缘作为 `SupportingTransitionInitialProof` 传入既有终端执行、reservation 结算、rollout checkpoint、proof receipt、rollout admission 与 supervision dataset 构建器。没有新增 planner、ledger、动作编译器或执行器。
- fresh Teacher 会从放置后的快照与结算后 ledger 重建完整候选分母；本 fixture 最终选择可立即完成的 Beer 商店路线。监督数据只包含该终端原生结果，前置机器放置仅作为可验证上下文，绝不伪装成机器产出或 terminal native outcome。
- acquisition Teacher 编译边界现在根据目标、路线、候选、state/ledger、portfolio、ranking、角色和完整血缘生成稳定的 plan/queue/item/primitive ID；同一证明链重建不再受随机 GUID 干扰。商店报价读取器同时修正合法 `null` 交易数量的解析，测试快照补齐同一日期绑定的地图可行走证据，均保持失败关闭。
- 完整 `self-test-current-stage-one-collection` 已通过，覆盖新机器容量链及既有 train/validation/test continuation、语料、pairwise、live-shadow 和篡改拒绝。正式产品训练仍为 `false`。
- 下一固定切片是建立支持动作家族的终端训练谱系覆盖矩阵，并逐项证明 `crop_planting`、`machine_input_load`、`machine_input_material_transfer`、`machine_input_purchase`、`machine_capacity_establishment` 都能经同一 file-backed recurrence 接入终端 rollout；已有闭环只登记复用，不得复制实现。

## 历史记录 · 2026-09-27 机器容量制作后放置 hermetic 闭环

- 缺机器容量的 hermetic 路线现连续执行两次非终端支持过渡：先经既有 `executor.craft_machine_item` 得到机器，再用 fresh 快照和结算后 ledger 重新枚举既有 `executor.move_to_tile -> executor.place_machine` 队列。第二次原子提交把同一个 source-bound intent 从 `craft_selected` 推进到 `placement_bound`，没有创建第二个 intent 或第二套放置器。
- 两项放置回执保留真实中间移动 state hash/tick 边界；终态必须精确观测到背包机器减一、绑定目标格从无机器变成唯一同型空闲机器。相同库存变化但机器落在错误格子的负例会以 `supporting_transition_machine_placement_delta_mismatch` 失败关闭。
- 放置结算仍只写 machine-intent support marker，不伪造材料/货币消费或 route-complete；结算后的 state/ledger 能生成 fresh Teacher request，证明该生命周期已重新接入现有 portfolio replan 边界。Release 构建与完整 hermetic bootstrap 通过，正式训练仍为 `false`。
- 下一固定切片是把这份机器放置 settlement/replan 作为 file-backed `SupportingTransitionInitialProof` 的前置血缘，跑通随后的 terminal route、rollout checkpoint、proof receipt 与 supervision dataset；支持动作本身仍不得伪装成 terminal native outcome。

## 历史记录 · 2026-09-27 机器缺失容量支持递归闭环

- 目标日期机器路线在透明设施轴确认目标机器没有已摆放容量时，现在会发出精确的 `machine_capacity_establishment` 支持过渡。它只接受权威路线绑定的机器 qualified ID、规范化来源集合和确定性地点缺失证据；身份未知、来源多义或并非真实缺容量时仍在上游失败关闭。
- 候选、日计划和动作编译继续复用既有 `farm.establish_supported_machine_capacity` 生命周期：背包已有机器时进入原放置链，否则进入原制作链；放置之后的投料继续由既有机器支持链负责。没有新增制作、放置、投料或寻路执行器。
- 原子提交现可在同一 reservation portfolio 事务中建立唯一的 source-bound `MachineSupportIntent`。该分支允许没有材料/货币 claim，但只限精确机器容量请求；提交回执会重放并核对 intent。fresh 回执分别证明制作后的精确库存增量，或放置后的库存减一与目标格唯一空闲机器。
- 非终端结算使用 machine-intent marker，不伪造 reservation 消费；它只把同一个 active intent 重绑到 fresh state/ledger，并强制完整重规划。聚焦候选、Backend intent settlement、Core game-free `103/103`、Backend `219/219`、Release 构建和完整 hermetic bootstrap 均通过；正式训练仍为 `false`。
- 下一固定切片只补直接放置分支的完整 hermetic 端到端证据，并把该支持过渡纳入后续 portfolio rollout/proof 数据链；不得重新实现机器生命周期，也不得在证据闭合前授权正式训练。

## 历史记录 · 2026-09-27 机器触发器条件语义修复

- `Data/Machines.OutputRules[].Triggers[]` 的条件现在保留为显式 OR 备选集合，不再从目标日期链中丢失。单触发器的 `LOCATION_SEASON Target` 条件进入地点轴，并使用透明桥逐地点输出的 `effective_season` 精确判断；山谷冬季蜂房不会再被错误视为可产蜜，实际季节非冬的地点仍可通过。
- 多触发器只要包含跨轴地点或随机条件就失败关闭，不会把 OR 错压成 AND；未知 GSQ 谓词同样失败关闭。单触发器 `RANDOM` 概率由既有随机重试轴与输出项概率相乘；`ITEM_CONTEXT_TAG Input` 与 `ITEM_EDIBILITY Input` 继续由既有资源轴处理，没有复制第二套物品条件求值器。
- 修复只收紧静态日历与目标地点准入，不改变执行器、Teacher/Student 权限或正式训练门槛；`formal_training_authorized=false` 保持不变。

## 历史记录 · 2026-09-27 机器缺料采购递归闭环

- 机器资源轴缺料时不再直接终止：货币轴只对一个确定性的机器输入缺口查询当前原生商店预览，按总价、单价、商店和库存键稳定选择精确的正价金币报价。当前已有材料与未来采购严格分离；reservation 只占用已经实际存在的材料和完成全部缺口所需的金币，不虚构尚未购买的物品。
- processing 轴在存在采购前置时明确停在 `upstream_machine_input_purchase`。支持链复用现有 `economy.buy_supplies`，逐次执行跨图连接、柜台交互和单件购买；购买阶段仍使用既有 `DailyPlanCompiler -> ActionQueueCompiler -> executor.buy_shop_item`，并保留安全关菜单动作，没有建立第二套商店或购买执行器。
- route/交互进度要求金币和目标物品都不变，只把仍活动的 claims 重绑到 fresh state。购买回执要求精确金币减少一个绑定单价、玩家背包中精确 qualified item 增加一个绑定堆叠；共享 ledger 支持货币 claim 的部分消费与最终完成，完全耗尽的 claim 不得继续重绑。每个阶段结束后旧队列失效，并从 fresh snapshot 与新 ledger revision 完整重规划，直到材料足够后自然回到既有箱存暂存或机器投料链。
- Release 构建、bootstrap hermetic 与共享 ledger 聚焦回归通过；该链仍是非终端监督证据，`formal_training_authorized=false`。下一固定切片是把“缺少已摆放机器”的路线接入既有 `farm.establish_supported_machine_capacity` 生命周期并执行同样的提交、回执、结算和完整重规划，不新增制作、放置或投料实现。

## 历史记录 · 2026-09-27 机器箱存原料暂存与 reservation 搬迁闭环

- `resource_inputs` 仍优先使用当前角色背包，但现在允许当前地图内、执行者获权、普通、未锁且交互坐标完整的放置箱子证明机器路线材料充足；此类槽位显式标记 `requires_player_staging=true`，绝不直接编译为机器投料。
- 当背包没有可直接投料的已预留材料时，支持递归只生成一个确定性 `inventory.transfer_item` 候选，并复用现有 `executor.move_to_tile -> executor.transfer_material` 两步宏。原生投影必须把完整 claim 放入恰好一个玩家背包槽；跨槽拆分、非当前地图、特殊箱、锁定箱、歧义节点或容量不足均在上游失败关闭。
- fresh 前后快照回执逐节点和逐槽核验箱子减少量与背包增加量。共享 reservation ledger 新增原子 relocation：活动 claim 从原箱子槽搬到实际观测到的背包槽，数量不变、状态仍为 `Active`；它不是消耗，不写 route-complete。部分搬迁、消费与搬迁混用、目标槽未观测到足量物品均拒绝，成功后必须使用新 state hash 与 ledger revision 做完整重规划，随后才可进入既有机器投料支持动作。
- acquisition dispatch、bootstrap hermetic、Core game-free `91/91` 与 Backend `213/213` 已通过；正式训练授权仍为 `false`。下一固定切片是让已有权威商店报价和开放窗口的确定性材料采购复用同一非终端递归协议；已有机器容量生命周期只做接线复用，不建立第二套制作、放置或投料执行器。

## 历史记录 · 2026-09-27 机器投料部分结算与完整重规划复入

- 共享 `ReservationPortfolioLedgerService` 的非终端支持结算现已接受结构化材料消费数组。每项都必须绑定一个活动 reservation、精确库存节点/槽位/qualified ID，且消费量不得超过 claim 余量；重复 reservation、重复槽位、新旧表示混用、超量或溢出输入均原子失败关闭。旧单条全量消费请求继续兼容。
- 全量消费仍把 claim 标记为 `Completed`；部分消费只扣减 `Quantity`、保持 `Active`、递增 claim revision，并分别记录 `material_reservation_consumed` 或 `material_reservation_partially_consumed`。一次动作包含主料和附加料时，所有消费在同一个 ledger revision 结算，只写一个非终端 support marker，不写 route-complete marker。
- 实验控制面不再假设支持动作必为种植。作物与机器都从各自已验证的 fresh 前后态回执生成同一种 canonical 消费集合；结算回执逐项核对余量、状态、history 与精确重放。机器两次投料场景的第一次动作现保留一份活动余量，并使用结算后 ledger revision 和 fresh 机器快照复入既有完整 Teacher 重规划链。
- 聚焦 acquisition dispatch、bootstrap hermetic、Core game-free `91/91` 与 Backend `209/209` 已通过。该闭环仍只提供非终端监督证据，`formal_training_authorized=false`；下一固定切片是把同一支持递归协议扩展到需要先采购输入或建立机器容量的上游支持动作，而不是绕过候选、reservation 或原生执行回执。

## 历史记录 · 2026-09-27 机器投料支持回执：真实扣料与机器加工态

- 现有 `acquisition_route_supporting_transition_receipt.v1` 已从作物专用核验扩展为按 `crop_planting / machine_input_load` 显式分派，仍只接受一个经过哈希绑定的非终端队列项；畸形的空队列或多项队列只会失败关闭，不会抛异常或被误判为完成。
- 机器请求和队列现在携带逐 claim、逐库存节点、逐槽位的单次材料消费计划。回执要求每个槽位的前后数量差精确等于计划消耗量，并要求目标机器从空闲态进入加工或已完成态；机器类型、最后输入、持有输出、唯一权威 route kind/source ID 及原生剩余计时必须全部与编译血缘一致。
- 透明桥新增原生 `last_input_item` 与 `last_output_rule_id` 投影，回执不靠显示名或预计收益猜测投料结果。资源轴同时修正为只把当前执行者的玩家背包视为原生投料来源；箱子里的材料必须先经过取物动作进入背包，不能直接满足机器输入。
- 聚焦 acquisition dispatch、GoalConditionedBootstrap Release 与 TransparentBridge 真实游戏引用构建均通过，均为零警告、零错误。正式训练授权仍为 `false`；下一固定切片是让共享 reservation 结算支持一次动作只扣除已核验的部分数量、保留 claim 余量，并用结算后的 ledger 和 fresh 快照复入既有完整重规划链。

## 历史记录 · 2026-09-27 机器投料支持请求：期限、材料占用与提交门控

- 现有作物支持协议没有被复制。`acquisition_route_supporting_transition_request.v1` 新增 `crop_planting / machine_input_load` 类型分派；机器分支只接受已经由唯一原生 route kind、source ID 和输出 qualified ID 命中的 `load_machine_input_tile` 候选。
- 投料候选现在结构化携带机器 qualified ID、原生触发器 `RequiredCount` 和探针执行 `ReadyTimeModifiers` 后的有效加工分钟。请求会把地点、格子、机器类型、输入槽、输入物品、单次消耗量和处理轴的 `machine_schedule_binding` 逐项重绑；任何数量、时长或槽位漂移都在原子提交前失败关闭。
- 完成日计算与当时 1.6.15 反编译语义一致：`DaysUntilReady` 按 `Utility.CalculateMinutesUntilMorning` 转换，普通分钟计时跨 2:00/6:00 边界继续递减。只有有效时长与处理轴权威值完全一致、预计完成日落在显式期限和权威窗口内时才放行；未绑定时间修正器、动态输出方法和仅隔夜完成仍由处理轴阻塞。
- 路线的完整材料 claim set 继续通过共享 `ReservationPortfolioLedgerService` 一次性预检和提交；当前动作只绑定其中与候选槽位相同的单次主输入量。提交回执之后仍复用同一 `DailyPlanCompiler`、`ActionQueueCompiler` 和 `executor.load_machine_input`，不新增机器专用执行器，且保持非终端、成功后强制新快照与完整重规划。
- 聚焦 dispatch、bootstrap hermetic、Core game-free `91/91` 与完整 `experiments/Run-Regression.ps1` 均通过。下一固定切片是核验投料前后库存与机器状态，并让结算只扣除本次观测到的输入数量、保留同一 claim 的剩余数量；在回执、部分结算和复入链闭合前，正式训练授权仍为 `false`。

## 历史记录 · 2026-09-27 机器投料支持过渡：精确来源与共享编译链

- 透明桥现在把投料探针已经选中的 `MachineOutputRule` 与 `MachineItemOutput` 重新绑定到 `Data/Machines` 的原生规则索引，并在预测结果中输出唯一的 `authoritative_route_sources`。有稳定规则 ID 时沿用 `machine_output` 身份；无规则 ID 时按精确 rule/output 索引输出 `native_machine_item_query_output` 或 `native_machine_flavored_output`。规则、输出行或目标物品不能唯一绑定时保持空来源并失败关闭。
- Core 的 `load_machine_input_tile` 候选保留上述结构化来源及预测输出物品；目标日期支持选择同时核对输出物品、route kind 与 source ID，不按显示名、物品相同或预计收益猜测。错误输出、错误规则与同一目标物品的多来源歧义均不能进入队列。
- 三类机器路线的 lowering 复用现有 `farm.process_machines` 候选作为原子投料支持动作，再经同一 `DailyPlanCompiler` 与 `ActionQueueCompiler` 编译为 `executor.load_machine_input`。该动作角色为 `supporting_transition`，不允许终端回执，并要求成功后读取新快照、完整重规划；`farm.establish_supported_machine_capacity` 与经济型 `farm.load_supported_machine_input` 仍保留，机器制作/放置没有证据时继续失败关闭。
- 聚焦 dispatch 与 hermetic 自测、Core/TransparentBridge Release 构建均通过。该切片闭合“已有空闲机器的精确投料候选到原生动作队列”；机器专属完成期限、原子材料 claim 与提交门控已由上方后续切片接入共享协议，投料后机器状态回执及部分 claim 结算仍待闭合；正式训练授权仍为 false。

## 历史记录 · 2026-09-27 机器既有产物抵扣：下游闭环

- processing 轴会在同一份透明机器快照上重新核验物品、堆叠、品质以及唯一 route kind/source ID，并按地点与格子稳定分配 `credited_existing_output_quantity`。部分抵扣只排程剩余尝试；完全抵扣保留显式零尝试 binding，并以既有机器计时器作为最迟完成时点。
- stochastic 轴只让未抵扣余量承担原生输出选择概率。完全抵扣的手动机器为零基线、零重试；部分抵扣扩张时使用“已抵扣数量 + 重试次数 * 权威最小产量”重新经过资源、货币、原子预留和 processing，抵扣量漂移即失败关闭。
- daily 轴把抵扣转换为真实动作队列：旧产物先收取并按精确抵扣量记账，剩余尝试继续使用既有投料/等待/收取状态机；完全抵扣只生成收取动作。无关旧产物仍只负责腾出机器，不会误算目标产量；手动路线必须同时满足所有逐机安排完成与目标数量收齐。
- Release 构建、hermetic、Core game-free `91/91` 与完整 `experiments/Run-Regression.ps1` 均通过（退出码 `0`），覆盖无抵扣、部分抵扣、完全抵扣和随机重试扩张场景；训练授权仍保持关闭。

## 历史记录 · 2026-09-27 机器既有产物上游抵扣：资源余量

- `resource_inputs` 只汇总 facility 轴中 `machine_active_output_route_matches=true`、物品一致且品质达标的旧产物，并将抵扣量封顶为当前需求；剩余产量再按权威最小堆叠换算投料次数。
- 部分抵扣会同步缩减主投入与全部附加消耗，且每个 machine binding 保留 `credited_existing_output_quantity`；完全抵扣生成显式零尝试/零消耗 binding，不要求虚构一个可用材料槽。来源不匹配或品质不足仍按完整需求投料。
- Release 构建与 hermetic 的无抵扣、部分抵扣、完全抵扣回归通过；processing、随机重试和 daily 日程现已共同消费该 credit，细节由上方“下游闭环”记录。

## 历史记录 · 2026-09-27 机器既有产物上游抵扣：设施证据

- `facility_capacity` 现在随逐机目标保留透明 `held_item` 的精确物品、堆叠和品质；只有 `active_output_authoritative_route_sources` 恰有一个来源，且 route kind、source ID、输出物品都与当前权威路线一致时，才标记 `machine_active_output_route_matches=true`。
- 该切片只建立抵扣证据，不在设施轴提前改变材料需求。同一机器仍可作为生产设施使用，但缺失、多义或不匹配的旧产物不会获得抵扣资格；后续固定切片由 `resource_inputs` 计算剩余需求并让下游轴共同消费同一结果。
- GoalConditionedBootstrap Release 构建为零警告，hermetic 覆盖精确命中与错误 source ID 不抵扣。完整回归在 resource/downstream 闭环后统一执行。

## 历史记录 · 2026-09-27 机器日内移动与交互预算

- 机器 processing binding 新增逐次 `attempt_schedule`，保留全局尝试序号、单机尝试序号、处理开始偏移和完成偏移；随机重试扩量后的每次投料不再只剩一个汇总计数。日内轴直接消费该排程，不重新猜测尝试如何分配到机器。
- 手动机器路线会从同一快照位置出发，使用既有路线图、日期证据和移动校准，逐次生成 `load_machine_input -> 原生加工等待 -> collect_machine_output`；多台机器可并行加工，但每次投料和收取都必须重新证明可达站位。自动触发机器则等待透明桥已绑定的目标产物完成时点，再移动并收取足量产物。
- `MachineInteractionBudgetPolicy` 现在是投料/收取 30 tick 估时的唯一常量来源，动作编译器与 Teacher 日内预算共用它。玩家动作时间只统计投料和收取；机器自身加工等待只推进保证完成时刻，不会重复计入 `terminal_action_game_minutes`，机器交互原生不消耗体力。
- 本切片先让快照时 `processing` 或 `ready_output` 的手动机器失败关闭，空闲手动机器和已唯一绑定输出的自动机器聚焦测试通过，Core 回归为 `2570/2570`，完整 `experiments/Run-Regression.ps1` 回归通过（退出码 `0`）；紧随其后的“非空闲手动机器复用”切片在同一日程状态机内闭合该分支。

## 历史记录 · 2026-09-27 非空闲手动机器复用

- processing/daily 两轴继续共用同一份机器快照与逐机 attempt schedule；daily 轴现在能按 `minutes_until_ready` 等待已有产物，执行现有 `collect_machine_output`，再执行现有 `load_machine_input`，最后等待并领取本轮目标产物，没有引入第二套机器动作。
- 复用前必须存在精确 `held_item` 身份、数量和品质，并按每台待清空机器保守预留一个透明 `player.inventory_capacity.empty_slots`；容量不足或字段不一致时上游直接失败关闭。只有设施、资源、processing、随机重试和 daily 各轴都核验为同一路线的既有产物才计入目标；其他旧产物仍只清空机器，不抵扣输入。
- 本切片聚焦回归覆盖 `processing` 与 `ready_output` 两种初态的“等待/领取/投料/领取”顺序和零空格拒绝；Release 构建为零警告，`self-test-bootstrap-hermetic` 与完整 `experiments/Run-Regression.ps1` 均通过（退出码 `0`）。

## 历史记录 · 2026-09-27 机器随机输出概率预算

- `Data/Machines` 的每条静态机器来源现在保留同一原生规则的完整输出选择行，而不再只有目标行和行数。锁定版反编译证明：`UseFirstValidOutput=true` 按顺序返回第一个通过 `GameStateQuery` 的输出；否则从全部有效输出中均匀选择；`RANDOM p` 通过该次上下文的 `Random.NextDouble()` 判定。
- 随机轴只解析可严格证明的形态：单行、只含简单 `RANDOM p` 的首个有效链，以及全部条件恒真的随机有效集合。首个有效行的概率为前置各行失败概率乘目标行通过概率；随机有效集合为 `1/N`。复合条件、条件化随机有效集合、随机物品查询、输出回调或逐物品条件继续 fail-closed。
- `processing_lead_time` 的“目标日可领取”与“快照时已落地”已拆成独立事实，避免把当天稍后完成的机器产物误判为现有库存并跳过随机预算。已由透明桥唯一绑定的自动机器在制产物另按“输出选择已完成”处理，不重复抽概率。
- 随机预算新增基础尝试次数，额外重试改为 `总尝试次数 - ceil(需求数量 / 保证最小堆叠)`，不再错误地直接减需求数量。概率预算扩大机器投料或处理次数时，扩大后的总尝试数会重新进入资源、货币、原子预留和机器排程轴，不能沿用基础 claim 放行。
- Release 构建、hermetic 机器概率测试和完整 `Run-Regression.ps1` 均通过；全量回归覆盖 1599 条路线、train/validation/test continuation 与伪造/篡改拒绝。下一固定切片是把已排程的机器投料与收取交互接入日内移动、动作时间和体力预算，且不得把机器自身等待时间重复计为玩家动作时间。

## 历史记录 · 2026-09-27 机器随机重试增量预留闭合

- 额外随机尝试不再停留在“需要重验”的标志位。随机轴按 `所需总尝试数 * 原生保证最小产量` 构造扩大后的路线需求，并复用现有 `resource_inputs -> currency_budget -> inventory_reservation -> processing_lead_time` 唯一链路；没有新增第二套材料选择、claim 或机器调度算法。
- 扩大后的资源轴按总尝试数重算主投入和全部附加消耗，reservation 只能从资源轴已证明的精确节点/槽位生成原子 claim；材料不足、现有 claim 冲突、货币不足、机器容量不足或排程次数漂移都会保留对应轴的原始原因并失败关闭。
- 随机轴成功时直接把扩大后的 processing route 作为自己的 `upstream_route`，因此后续日内预算、机会成本、组合提交和执行绑定会自然遍历到扩大后的 claim set，不通过旁路字段携带第二份预留。聚焦测试锁定 claim 数量和机器排程次数均等于概率预算的总尝试数。
- GoalConditionedBootstrap Release 构建、hermetic 自测和完整 `Run-Regression.ps1` 均通过；完整回归覆盖 train/validation/test continuation、组合语料以及伪造/篡改拒绝。正式训练准入仍为 false；下一固定切片是机器投料/收取的日内动作预算。

## 历史记录 · 2026-09-27 机器处理提前量闭合

- 三类 `Data/Machines` 路线已从资源预留继续接入既有 `processing_lead_time` 轴，没有新增第二套调度器。手动投料路线复用资源轴证明的加工次数，并按设施轴锁定的真实机器位置、当前 `idle / processing / ready_output` 状态与剩余分钟并行排程；机器正在加工时，现有剩余占用时间会先从当日容量中扣除。
- 计时语义按 1.6.15 反编译锁定：`DaysUntilReady >= 0` 优先于 `MinutesUntilReady`，分钟计时只在当前可游玩日剩余时间内判定同日完成，交互耗时仍归下游 `daily_time_energy_budget`。当前 188 条权威机器路线全部为 `OnlyCompleteOvernight=false` 且没有 `ReadyTimeModifiers`；未来模组或版本若出现过夜限定、时间修正器或 `OutputMethod` 时间覆盖，在对应语义接入前会明确阻塞。
- 透明桥新增 `active_output_authoritative_route_sources`，把加工中或待收取产物绑定到原生 route kind、source ID 与物品 ID。自动触发机器只有在该绑定唯一且与当前路线完全一致时，才允许把在制产物算作目标日产出；同物品多来源、旧快照缺字段或来源无法唯一归属均 fail-closed。
- hermetic 测试已覆盖两台机器并行同日完成、单机容量不足、`DaysUntilReady` 跨日、自动触发在制产物精确命中及来源不明阻塞。GoalConditionedBootstrap 与 TransparentBridge Release 构建均为 `0 warning / 0 error`。下一固定切片是把机器随机输出的额外尝试数反向增量绑定到材料、机器处理时间和日内动作预算，随后才允许这类路线进入完整 Teacher 候选比较。

## 历史记录 · 2026-09-27 机器投入资源与非消耗库存条件

- 机器三类来源现已从 `facility_capacity` 接入唯一的 `resource_inputs` 轴。只有带 `ItemPlacedInMachine` 位的原生触发器会产生投料需求；纯 `DayUpdate`、`OutputCollected` 或 `MachinePutDown` 触发器不会错误扣除 `AdditionalConsumedItems`。同一路由混合自动与投料触发器时继续失败关闭，等待静态路由按触发方式展开。
- 资源轴按权威最小产出量计算满足目标数量所需的基础加工次数，再将主投入的 `RequiredCount` 和煤等全部 `AdditionalConsumedItems` 按次数放大。随机成功所需的额外尝试仍由后续 `stochastic_retry` 轴独立追加，当前结果不提前冒充完整随机预算。
- `farm.material_inventory_graph` 的每个槽位新增原生 `Item.GetContextTags()`、对象 `Edibility` 及各自投影状态。标签型和可食用度型机器规则必须找到同一槽位集合中的真实输入见证；旧快照或不完整元数据只能支持无需这些字段的精确 ID 规则，不能把空标签猜成“不匹配”。
- 机器资源结果保留所选原生触发器、每次数量、加工次数和允许预约的精确节点/槽位。后续 reservation 只能从这组槽位扣量，避免资源求值与实际预约使用不同物品。`PLAYER_HAS_ITEM` 资源条件也已按反编译语义接入，但它只读取当前玩家随身库存节点、保留否定与最小/最大数量，且不创建消耗预约；原生核桃/Qi 宝石特例在相应货币证据接入前失败关闭。
- Release 构建、机器资源 hermetic 自测和 Core `2570/2570` 回归通过。下一固定切片是机器 `processing_lead_time`：使用同一 `machine_source` 的分钟/天数、过夜完成和时间修正器计算最迟投料时点，再进入机器随机输出的重试与资源增量闭环。

## 历史记录 · 2026-09-27 机器来源地点与设施容量绑定

- `Data/Machines` 静态来源现已接入既有 `location_route -> facility_capacity` 轴，不新增第二套路由或执行器。地点轴只接受同一透明快照中 `farm.machines[]` 的完整、未截断舰队，按机器 qualified ID、地点和格子绑定来源；空舰队是有证据的来源缺失，字段缺失、行数漂移或重复地点格子则失败关闭。
- 设施轴会在同一快照中再次按地点和格子重绑机器，保留 `idle / processing / ready_output` 状态并要求该机器原生声明可产出。它只证明现有机器容量，不推测制作、摆放、投料、吞吐量、完成时间或随机成功率；这些仍分别归资源输入、处理提前量和随机重试轴。
- Release 构建和 hermetic 机器舰队/地点/设施测试通过。现有历史训练快照不能提供同一时刻的新版全地图日期路线与完整机器舰队，因此真实 188 条机器路线仍按缺失证据阻塞；回归脚本锁定这一结果，禁止把两份不同快照拼成伪证据。
- 机器资源输入已由上节闭合；当前下一固定切片是机器处理提前量，随后才进入随机输出预算。

## 历史记录 · 2026-09-21 单路线 reservation 结算与重放回执

- 新增 `POST /api/v1/strategy/commitments/reservation-portfolios/settle-completed-route`。请求必须绑定 fresh after snapshot、当前 ledger revision、portfolio/goal/route source decision、fresh terminal receipt 的小写 SHA-256，以及该路线当前全部 active 材料/货币 reservation ID。服务在内存副本中一次性把精确集合改为 `completed`，记录完成原因和证据哈希，只前进一个 revision，并写入逐 claim 历史与唯一 `reservation_portfolio_route_complete` 标记；缺少、多余或重复 ID 均整体拒绝。无 claim 路线仍写入可审计标记。
- 新增 `acquisition_route_portfolio_settlement_receipt.v1`、`build-acquisition-route-portfolio-settlement-request` 和 `build-acquisition-route-portfolio-settlement-receipt`。请求生成器先确定性重建 execution binding 与 fresh terminal receipt，再从已提交 ledger 推导完整 active claim set；回执随后用历史标记时间精确重放 Core 事务，要求 settlement result、settled ledger 和重放结果逐对象一致。额外历史行篡改会被拒绝。
- Backend 全量回归为 `202/202`；实验项目 Release 为 `0 warning / 0 error`；聚焦 76 路线链通过真实商店路线的 `revision 1 -> 2`、两条 claim 完整结算、exact replay 与篡改拒绝。该回执只证明一条已选路线完成后的 reservation 生命周期，强制 `fresh_replan_required=true`，不代表组合已完成，`formal_training_authorized` 仍为 false。

## 历史记录 · 2026-09-21 路线组合 Teacher 偏好与 caller 绕过关闭

- 新增 `acquisition_route_portfolio_teacher_preference_request.v1`、`acquisition_route_portfolio_teacher_preference.v1` 与 `build-acquisition-route-portfolio-teacher-preference`。请求只声明目标、同一快照/ledger revision 和需求组 scope；构建器从权威 selection rule 与当前全部目标日 Pareto route occurrence 自动枚举组合，不接收调用方候选列表，也不读取 learner rank/score。
- `all_required` 枚举全部替代项，`choose_at_least_required_slots` 枚举从最低所需槽位到全部可行替代项的所有组合；每个替代项再与全部 Pareto 路线做笛卡尔积。候选分母超过 `4096` 时整体失败而不截断。每个生成 proposal 都复用唯一 portfolio admission 与原子 preflight；不可用组合只作 defer，不生成负标签。
- Teacher 只在一个 admitted 组合的非标量聚合成本向量严格 Pareto 支配其他全部 admitted 组合时给出偏好。相等或耗时/体力/材料/原生货币互有取舍时显式阻塞，不按 proposal ID、learner 分数、售价汇总或跨货币换算破平局。选中 proposal/admission 可直接按规范 JSON 落盘并由原构建器逐对象重建。
- `acquisition_route_execution_binding.v1` 现在还会重算完整 Teacher preference，并要求实际 proposal/admission、commit receipt 与 preference 的唯一选中结果一致；合法但由 caller 私自选择的组合不能借用后续 reservation 回执进入执行。聚焦 76 路线全链与严格支配/相等/互有取舍纯回归通过，Release 为 `0 warning / 0 error`。正式训练仍为 false；下一固定边界是组合内顺序执行、逐路线 fresh 重规划、reservation 生命周期和组合级完成回执。

## 历史记录 · 2026-09-21 路线组合原子提交回执与执行所有权闭合

- `acquisition_route_portfolio_commit_receipt.v1` 会从原始 proposal、完整目标日依赖链、提交前 ledger、admission、提交结果和提交后 ledger 确定性重建组合提交事实。发生写入时必须只前进一个 revision，全部材料/货币 claim 必须成为精确 active 行，全部显式 release 必须取消，并且每个分量历史与唯一 `reservation_portfolio_commit` 标记必须处于同一 revision；任一后置 claim 失败时不得泄漏前置写入。无需新 claim 的组合只接受规范 JSON 等价的未变化 ledger 和无 commit result 的幂等路径。
- `acquisition_route_execution_binding.v1` 现已消费并重算上述回执。被调度的路线必须在该组合中精确出现一次；每个 normalized command 除原有 11 个路线身份参数外，还必须携带精确 `acquisition_reservation_portfolio_id` 与 `acquisition_reservation_ledger_revision`。因此旧队列、只做过 preflight 的组合、错误 revision 或脱离组合选择的单路线均不能绕过 reservation 所有权边界。
- Backend 回归为 `199/199`；实验项目 Release 构建为 `0 warning / 0 error`；聚焦 76 路线全链自测通过真实商店材料+货币原子提交、提交后回执、篡改 ledger 拒绝、无 reservation 幂等回执、缺组合所有权参数拒绝和 fresh 终态回执。未启动游戏或训练。
- 这一步完成的是“组合 claim 原子落账 -> 提交后证明 -> 单路线队列持有组合所有权”。独立 Teacher 的完整有界候选枚举、唯一严格 Pareto 偏好和执行绑定已由上节接入；相等/不可比组合继续阻塞。剩余固定边界是组合内多路线的顺序执行、fresh 重规划、reservation 生命周期和组合级完成回执；这些完成前 `formal_training_authorized` 继续保持 false。

## 历史记录 · 2026-09-21 目标日路线执行绑定与 fresh 终态回执闭合

- 新增执行前 `acquisition_route_execution_binding.v1` 和命令 `build-acquisition-route-execution-binding`。构建器会从原始输入确定性重建完整机会成本报告，只允许完整 Pareto 前沿中的一个精确 `route_occurrence_id` 进入执行；候选 ID 由 occurrence 确定生成，每个队列项必须重复携带精确需求、路线、来源、数量和品质身份，同时哈希绑定 lowering、目标日快照、动作队列、全部队列项和 option。每个 option 必须属于该路线权威 endpoint/support 集，且至少包含一个 endpoint，不能再用调用方别名或物品 ID 猜测实际执行了哪条获取路线。
- 新增执行后 `acquisition_route_fresh_terminal_receipt.v1` 和命令 `build-acquisition-route-fresh-terminal-receipt`。它会重算并逐对象核对执行绑定，复用统一的顺序队列回执验证器，要求同存档/玩家、同目标日、新鲜 after snapshot、精确状态哈希和 tick 边界、逐项身份/顺序/primitive 原生验证以及最终队列闭合。
- `item_id` 路线必须证明满足最低品质的玩家库存净增量不少于完整 `required_amount`；“要求 2 个但只增加 1 个”已成为显式失败回归。`money_payment` 路线另走社区中心原生 ingredient `false -> true` 与 money 精确减少量双重验证，不冒充库存产物回执。原 Stage 1 Teacher 回执也改用同一精确数量验证器，修复了过去任意正增量即可放行多数量需求的问题。
- Release 构建为 0 warning / 0 error；聚焦 Bootstrap 76 路线全链自测通过，覆盖确定性绑定、真实商店 Pareto 路线正向回执以及部分数量拒绝。机会成本的延后回归已完成，12 个固定依赖轴至此都有明确契约边界。
- fresh 回执只证明“这条已选路线的这一队列确实完成并产生了要求的终态”，不能倒过来替代路线选择。跨需求路线组合、原子 reservation commit、提交后所有权回执及其单路线 dispatch 绑定已由上节闭合；`route_training_evidence_eligible=true` 仍不等于正式训练准入，剩余边界是独立 Teacher 组合偏好、组合内顺序执行/fresh 重规划、组合级完成回执和正式 rollout controller 准入。

## 历史记录 · 2026-09-20 目标日机会成本轴实现待回归

- `feat/target-date-opportunity-cost` 已接入 `acquisition_route_target_date_opportunity_cost.v1` 与 `build-acquisition-route-target-date-opportunity-cost`。它会确定性重建完整 `daily_time_energy_budget` 上游、核对同一快照身份，并保持全部 route occurrence；上游报告漂移或被篡改时直接拒绝。
- 机会成本不压成一个人为分数。向量独立保留保证耗时、原生体力、按 `qualified_item_id + quality + live sale_price` 分开的精确物料数量，以及 money、star tokens、club coins、Qi gems 四个原生货币域。物料售价合计只作审计摘要，不代替物品身份，也不进行跨货币换算。
- 比较严格限制在同一 `(requirement_set_id, requirement_id, alternative_index)` 内，只使用严格 Pareto 支配：所有维度不更差且至少一维更好才淘汰；等价向量和互有得失的路线都保留在前沿。Learner 分数、未来收入和推测效用不得进入本轴。
- 库存成本从 claim 指向的实时材料槽读取；`quality` 或 `sale_price` 缺失、槽位/物品/状态哈希漂移、未知货币域、溢出或非正 claim 均失败关闭。fixture 已补显式成本字段，并新增两条当前可行路线及三类 Pareto 边界自检。
- 该轴的延后回归已于 2026-09-21 随 fresh 终态回执聚焦全链自测完成；机会成本报告仍只提供候选前沿，不直接选择或执行路线。

## 历史记录 · 2026-09-13 目标日日级时间/体力预算轴闭合

- 新增 `acquisition_route_target_date_daily_time_energy_budget.v1` 与 `build-acquisition-route-target-date-daily-time-energy-budget`。构建器会确定性重建完整随机重试预算，核对静态日历、钓鱼概率、快照、路线计时校准及全部 76 条 route occurrence 的身份；上游报告漂移或被篡改会直接拒绝，不能成为训练输入。
- 当前上游匹配的两种终端均有保守预算。普通无饵钓鱼必须从同一快照到达概率投影指定的精确合法站位，14 次尝试完整计入最大首次咬钩等待、原生抛竿/上钩/完美锁定/收杆时长，并按反编译 `max(0, 8 - effective_fishing_level * 0.1)` 计算 105 体力及至少 1 点保留体力；未证明 Efficient 时不推测其零消耗。商店购买必须到达权威交互格的相邻站位，并按现有 rolling 编译链的菜单等待、交互、对话、逐次购买和关闭菜单最坏时长计入完整数量。
- 路线抵达与终端动作必须整体落在同一个已由上游目标日解析的来源窗口内。钓鱼临时等级加成、带饵/定向鱼饵、固定种子跨捕获状态及尚未实现的其他活动终端继续显式阻塞；本轴不选择替代路线、不组合日计划、不计算机会成本，也不证明 fresh 产物回执。
- 聚焦 fixture 现为 `76/76` 已解析、2 条匹配、74 条上游不适用、0 条阻塞；钓鱼和商店预算分别验证精确站位、抵达/完成时刻、尝试数量、体力与编译时长，篡改随机预算报告会被确定性重建拒绝。Core game-free `77/77`、Backend `194/194` 及 Bootstrap 全链自测通过，实验项目 Release 构建为 0 warning / 0 error；训练授权保持 false，未启动游戏或训练。
- 下一固定轴是 `opportunity_cost`，随后才是 fresh 终态回执。当时成果只证明“若选择这一条路线，单日时间和体力是否可行”，不能提前宣称 Teacher 路线组合或正式训练准入完成。

## 历史记录 · 2026-09-14 普通无饵钓鱼重试预算闭合

- `fishing_forecast` 已成为按 `location_id + rod_slot_index` 隔离缓存的轻量透明快照。它只运行 world/fishing 必需读取器，不再连带扫描玩家全库存、菜单、农场、NPC 或全部地点；目标地点必须已经加载，错误地点、鱼竿槽位、跨存档/玩家/日期/时刻或超过 30 tick 的预测都会失败关闭。
- 透明桥按锁定版反编译公式精确发布 `PreciseFishCaught * 859` 固定种子规则的本次通过/失败，并区分原生连续随机。Core 对指定落点、站位、完整 Default+地点规则、优先级、同级最坏顺序、随机物品选择和 `Data/Fish` 接受概率生成首轮保守下界；固定种子不能冒充可重复概率。
- 重复抛竿另有独立证明：当前只放行明确无饵、无魔法鱼饵/好奇诱饵、无临时钓鱼等级加成，且目标及所有前置竞争规则均无可变条件、逐物品条件、捕获上限、捕获标志、未来等级解锁或固定种子的上下文。单次概率可读但重复性不成立时，随机预算轴仍阻塞。
- `StochasticRetryPolicy` 以统一 95% 阈值计算多成功事件的最小二项重试次数，使用对数域下尾求和与有界二分；额外重试数已从错误的 `总尝试-1` 修正为 `总尝试-所需产物数`。聚焦 `(O)145` 路线以单次保守下界 0.2 得到 14 次总尝试、13 次额外尝试；因明确无饵，不扩大材料预留。
- 76 条目标路线现为随机预算轴 `76/76` 已解析、2 条目标日匹配、0 概率阻塞，训练授权仍为 false。Core game-free `58/58`、Backend `194/194`、Bootstrap 全链自测、实验项目和真实程序集桥接构建均通过；处理报告或钓鱼概率报告被篡改都会被确定性重建拒绝。
- 下一固定轴是 `daily_time_energy_budget`：必须把选中终端站位接入可达性、把 14 次尝试换算为同一鱼类开放窗口内的保守时间/体力预算，再进入机会成本和 fresh 终态回执。带饵重试、定向鱼饵第二轮以及固定种子跨捕获状态仍保持显式阻塞，不能在本轴偷放行。

## 历史记录 · 2026-09-13 Master Angler 原生地点鱼概率输入目录

- 权威需求清单新增独立的 `native_fish_spawn_chance_rule` 证据，哈希锁定反编译 `SpawnFishData.cs`，并逐段守卫 `GetChance` 的基础概率、好奇诱饵、每日运气、数量修正器、特定鱼饵和运气等级公式。原有 `native_location_spawn_rules` 证据同时明确覆盖 `GameLocation.getFish` 的规则排序与解析入口。
- `master_angler_opportunity_catalog.v1` 现在对全部 180 条匹配原生地点规则保留直接/随机物品选择、`Chance`、每日运气开关、好奇诱饵修正、特定鱼饵加成与倍率、运气等级加成、完整数量修正器、修正器模式和 `UseFishCaughtSeededRandom`。真实 1.6.15 目录为 174 条直接选择、6 条随机选择、14 条精确鱼获计数种子随机，静态输入未解析项为 0。
- 新完整性门会拒绝缺少这些字段却仍声称 `complete` 的旧目录；Master Angler 窗口、获取日历和当前 Teacher 前沿均继承此门。合成回归另外覆盖当前 180 条原生规则未使用的 `ChanceModifiers` 分支，避免未执行代码冒充支持。
- 本步只闭合“目标鱼匹配规则的静态 spawn-chance 输入”，不等于目标投点的终端捕获概率。当时后续计划必须按目标地点和投点组合 Default 与地点全部竞争规则，执行原生 `Precedence + 随机同级顺序`、两轮定向鱼饵回退、ItemQuery 与 `CheckGenericFishRequirements`，再生成单次目标产物概率；在此之前随机重试轴继续失败关闭，训练授权保持 false。

## 历史记录 · 2026-09-13 显式目标日期随机重试预算轴首段

- 新增 `acquisition_route_target_date_stochastic_retry_budget.v1` 与 `build-acquisition-route-target-date-stochastic-retry-budget`。构建器会逐对象重建处理提前量报告并核对静态来源，`uncertainty_mode` 也从 lowering 开始无损穿过静态日历、目标日日历和解锁记录，避免末端按 route kind 临时猜测随机性。全部 33 种 route kind 均有唯一随机性分类，分类漂移会直接拒绝输入。
- 确定性 fresh receipt 与处理轴已证明的保守产出使用零重试预算；原生随机路线若目标产物已在透明快照中物化，也可按实际数量和品质零重试领取。其余原生随机路线必须给出目标地点、目标终端动作的精确单次概率、产量、品质和条件上下文；当前地点钓鱼投影不能冒充远程地点概率。
- 95% 成功阈值已抽到 Core 唯一的 `StochasticRetryPolicy`，现有蟹笼容量评估与新目标日期轴共用，不保留第二个常量。重试若增加鱼饵、晶球或其他耗材，在重新通过资源、货币和原子预留校验前仍不能授权执行。
- 聚焦 fixture 保留 `76/76`：74 条处理轴不适用，商店确定购买路线零重试通过，唯一可达的普通钓鱼路线因缺少 Beach 目标投点概率显式阻塞；篡改处理报告会被确定性重建拒绝。训练授权仍为 false，下一修正是补齐可跨地点查询的钓鱼终端概率证据，再计算多成功次数所需的重试预算与耗材回环；之后才进入日时间/体力轴。

## 历史记录 · 2026-09-13 目标路线数量与最低品质契约修复

- 权威 lowering 中的 `match_kind`、`amount` 和 `minimum_quality` 现在从静态来源解析开始无损进入日历、解锁及全部后续目标日期制品；每个阶段都会把嵌套路线与原始静态行逐字段核对。已删除货币轴末端临时回查数量的 `AcquisitionRouteAmountIndex`，不再允许中间轴把“需要多份/指定品质”退化成“一份普通品质”。
- 作物静态证据直接读取 `Data/Crops` 的 `HarvestMinStack`、`HarvestMaxStack`、`ExtraHarvestChance`、`HarvestMinQuality` 和 `HarvestMaxQuality`。设施容量按 `ceil(required_amount / HarvestMinStack)` 汇总所有匹配地点的现存目标作物槽与开放备耕槽；种子需求只扣除最低产量可保证的现存作物产出。现有逐格状态还不能证明高于静态最低值的收获品质，因此高品质作物要求会显式阻塞，不能用概率或示例猜测通过。
- 商店报价沿用透明桥现有的原生 `stack` 与 `quality`，购买次数为 `ceil(required_amount / output_stack)`，价格、有限库存和交换物数量按次数同步放大，并拒绝低于最低品质的商品。处理轴汇总成熟作物及蟹笼的可证明堆叠和品质；透明桥为蟹笼补充 `current_output_stack/current_output_quality`，不足数量不再因任意一个目标产物已就绪而通过。
- 聚焦夹具将同一需求固定为 2 份且最低品质为非零，验证 2 个作物槽、2 枚种子、10 木材交换物、200g、2 次有限库存购买、2 份成熟产出及蟹笼数量/品质过滤；GoalConditionedBootstrap Release 和聚焦自测通过，训练授权仍为 false。本次没有启动游戏或训练。
- 下一固定轴仍是 `stochastic_retry_budget`，但不得直接附加在当前预留结果后授权执行。随机尝试数若增加种子、鱼饵、晶球或其他耗材，最终需求必须重新经过资源、货币与原子库存预留校验；首尝试的 claim 不能冒充完整重试预算。随后才进入日时间/体力、机会成本和 fresh 终态回执。

## 历史记录 · 2026-09-13 显式目标日期处理提前量轴求值

- 新增 `acquisition_route_target_date_processing_lead_time.v1` 与 `build-acquisition-route-target-date-processing-lead-time`。构建器必须读取并逐对象重建完整库存预留报告，同时核对静态来源、同日透明快照和全部 route occurrence；聚焦 fixture 继续保留 `76/76`，未知来源类型不能默认为零等待。
- 本轴只回答“确定性产出等待是否允许该路线在目标日交付”。商店、钓鱼、地点采集等即时交互没有生产等待；交互耗时仍归 `daily_time_energy_budget`，钓鱼、挖掘、掉落等随机尝试仍归 `stochastic_retry_budget`，两者都没有被伪装成本轴成功。
- 作物路线复用反编译锁定的 `Data/Crops DaysInPhase`、最低产量/品质与透明桥逐格 `farm.crops/current_location.crops`。空耕地当天播种会保留权威基础生长期，但考虑肥料、技能和水田加速后只发布反编译可证明的“不早于次日”下限，因此可靠排除同日交付而不伪造精确成熟日；已有作物只有 `ready_for_harvest=true` 且可证明产出总量和最低品质覆盖完整需求才会同日通过。容量汇总声称存在目标作物但逐格状态缺失、数量不一致、产物投影非 exact 或生长倒计时矛盾时失败关闭。
- 蟹笼路线复用全局 `player.crab_pot_network`：一致的现成目标产物按实际堆叠与品质汇总为零日等待；已服务但尚未产出时依据锁定 `CrabPot.DayUpdate` 只证明最早次晨尝试，产物身份概率仍留给随机重试轴。机器、畜牧、鱼塘、果树、太阳能板和树液采集器等尚未穿过上游的生产类已显式登记，若提前到达本轴会失败关闭，不能偷用即时交互规则。
- 聚焦自测结果为 72 条上游不适用、2 条即时匹配、2 条新种作物确定性超期；成熟作物变体恢复为 4 条匹配，删除逐格作物状态后仅两条作物路线阻塞，篡改上游报告被拒绝。训练授权仍为 false；下一固定依赖轴为 `stochastic_retry_budget`，之后仍有日时间/体力、机会成本和 fresh 终态回执。

## 历史记录 · 2026-09-13 显式目标日期库存预留轴求值

- 新增 `acquisition_route_target_date_inventory_reservation.v1` 与 `build-acquisition-route-target-date-inventory-reservation`。构建器必须显式读取 `--strategy-ledger`，重新生成并逐对象核对完整货币预算报告，再保持每个 route occurrence 原样进入预留轴；缺账本、错误保存/玩家、旧或损坏契约不能退化为空账本。
- 每条已经通过资源与货币轴的路线独立生成确定性的原子 claim set，而不是把 1599 条替代路线同时写入账本。可消耗材料继续复用 `MaterialSupplyProjection`，按已授权节点、槽位和物品确定性拆分数量；`money`、`star_tokens`、`club_coins`、`qi_gems` 继续复用 `NativeCurrencySupplyProjection`。求值前扣除其他活动决策的预留，同一决策自己的 claim 则排除后重建并逐字段比对，以区分首次提案、精确已提交和需要整组替换。
- 全局预检查会把所有活动预留与当前透明供给重新投影；槽位消失、玩家漂移、货币域漂移、重复 ID、余额或堆叠超占都会使输入失败关闭。取消/完成记录保留审计但不占供给。已附着在鱼竿上的魔法鱼饵尚无现有账本可表达的嵌套槽位地址，因此该路线会显式阻塞，不能伪装成普通背包材料。
- 聚焦 fixture 保持 `76/76`：72 条继承不适用，4 条当时路线匹配；普通钓鱼无需 claim，两条作物分别提出 1 枚和 2 枚精确种子的槽位 claim，商店路线提出同一原子组中的 10 木材槽位 claim 与 200g claim，总计 3 个待提交组、3 个材料 claim、1 个货币 claim。其他路线占用 1 木材或 450g 时只让商店路线形成确定冲突；取消记录恢复供给，精确已提交组被幂等识别，错误玩家及全局超占账本被拒绝。
- 本轴只证明“若控制器选择该路线，这一整组当前资源能否被预留”，不负责替 Teacher 选择替代项，也不允许逐 claim 部分提交后执行。原子批量提交/路线组合属于后续控制器选择边界；固定下一依赖轴为 `processing_lead_time`，之后仍有随机重试、日时间/体力、机会成本和 fresh 终态回执。训练授权仍为 false，未启动游戏或训练。

## 历史记录 · 2026-09-13 控制器原生货币预留基础设施

- `strategy_commitment_ledger.v1` 新增 `currency_reservations[]`，沿用材料预留的保存/玩家隔离、乐观 `ledger_revision`、取消状态与不可变历史。Backend 新增原生货币预留 upsert/cancel 接口；每次写入均从指定同日快照重算未预留余额，其他活动路线已占用的金额会先扣除，过量预留、旧修订和身份漂移均失败关闭。
- 预留域严格复用唯一的 `NativeShopCurrencies` 定义，只有 `0=money`、`1=star_tokens`、`2=club_coins`、`4=qi_gems`。透明桥、商店报价、目标日期预算轴和控制器供给投影不再各持一张映射表；读取还会核对 `shop_currency_balances` 的 money 行与 `player.money` 完全一致。
- 活动预留才减少可用余额，取消和已完成记录只保留审计；错误玩家、未知 ID/key、非正数、重复预留 ID、溢出和余额超占都会阻塞。Core game-free 36/36、Backend 189/189、Bootstrap Release 与 TransparentBridge E 盘真实游戏程序集构建均通过。
- 这是库存预留轴的原子账本前置条件。逐路线预留轴现已生成精确材料槽位/货币 claim，但仍不替 Teacher 同时选择全部替代路线；控制器选定路线后必须整组写入账本，后续候选再从扣除后的供给继续求值。训练授权仍为 false，未启动游戏或训练。

## 历史记录 · 2026-09-13 显式目标日期货币预算轴求值

- 新增 `acquisition_route_target_date_currency_budget.v1` 与 `build-acquisition-route-target-date-currency-budget`。构建器会重新生成并逐对象核对完整资源输入报告，再把每个 occurrence 与原始 lowering 数量、静态来源和同日快照连接；全部 `33` 种来源类型必须进入唯一货币分类，未知类型不能静默视为免费。
- 透明桥新增 `player.shop_currency_balances.value`，严格按 1.6.15 反编译的 `ShopMenu.getPlayerCurrencyAmount` / `chargePlayer` 域读取 `0=money`、`1=star_tokens`、`2=club_coins`、`4=qi_gems`。玩家快照、全商店预览和已打开商店菜单共同复用这一个读取入口，不保留第二套货币映射。
- 商店路线以 `(shop_id, synced_key, qualified_item_id)` 绑定当前 `ShopBuilder.GetShopStock` 报价，价格、货币、有效交换物数量、库存与 `CanBuyItem` 都来自同一次原生投影。资源轴也改为复用该报价中的实际交换条件，因此物品查询动态改写交换物时不会与货币轴分叉；缺报价、售罄、不可购买或余额不足分别形成失败关闭或确定 miss。
- `native_money_payment` 使用 lowering 中的精确数量和 money 余额；当时权威库存中的四条 Vault 路线保持 2500/5000/10000/25000。商店路线按原生每次产出堆叠计算购买次数，再同步放大价格、有限库存和交换物数量；其余当时来源只有在上游资源匹配后才明确判为本轴无直接货币成本。
- 本轴只回答“一条路线在这张快照中能否支付”。跨路线重复花费、未来收入与资金预留由下一固定轴 `inventory_reservation` 处理；处理时长、随机重试、日时间/体力、机会成本和最终购买/付款回执仍属后续轴。聚焦 fixture 为 `76/76`、4 条活动路线匹配、3 条无需货币、72 条上游不适用；缺商店报价或缺余额只阻塞受影响购买，50g 对 200g 总价形成 1 条确定 miss。训练授权仍为 false，未启动游戏或训练。

## 历史记录 · 2026-09-13 显式目标日期资源输入轴求值

- 新增 `acquisition_route_target_date_resource_inputs.v1` 与 `build-acquisition-route-target-date-resource-inputs`。构建器从同一输入重新生成并逐对象核对设施容量报告，再按路线 occurrence 对齐静态来源；全部 `33` 种来源类型必须进入唯一资源分类，未知类型和奖励、机器、畜牧、鱼塘、晶球、配方等尚未绑定输入的来源逐条失败关闭。
- 资源数量复用既有规范 `farm.material_inventory_graph.v1` 及 `MaterialSupplyProjection`，统计随身物品和获授权持久容器的立即可用节点，不另写一套背包/箱子汇总。材料图、鱼竿和蟹笼各自惰性读取并缓存，只有当天仍活动的对应路线才触达；共享或其他玩家资源继续默认不可花费，预留冲突仍由后续 `inventory_reservation` 轴持有。
- 当前纵向闭合：现存目标作物只按权威最低产量抵扣，剩余数量按最低单格产量向上取整为精确种子数；商店交易物按所需购买次数放大而金币/其他原生货币留给下一轴；普通目标日钓鱼无需耗材，只有所有保留窗口都要求魔法鱼饵时才核对可装饵鱼竿与已装/散装 `(O)908` 的合计数量；蟹笼现成产物、已装饵或主人 Luremaster 可通过，空未服务蟹笼在完整原生饵候选域接入前阻塞。
- 本轴只判定路线消耗或占用的输入。锄头、鱼竿、武器等可复用工具继续由既有候选/动作编译器 fresh 前置条件持有；设施建立、处理提前量、随机重试、时间体力和终端回执不在本轴内。这样不会把工具当作可消耗库存，也不会允许缺工具的动作通过执行门。
- 聚焦 fixture 保留 `76/76`：72 条上游不适用，2 条作物分别由 `(O)472 x1` 与 `(O)472 x2` 满足，1 条商店交易由规范图中的 `(O)388 x10` 满足，1 条普通钓鱼明确无需耗材。删除材料图只阻塞这 3 条输入路线；删除种子形成 2 条确定 miss。第 223 天旧快照保留全部 1599 条路线，197 条上游不适用、1402 条继承阻塞、0 条猜测通过。训练授权仍为 false；下一固定轴是 `currency_budget`。

## 历史记录 · 2026-09-13 显式目标日期设施容量轴求值

- 新增 `acquisition_route_target_date_facility_capacity.v1` 与 `build-acquisition-route-target-date-facility-capacity`。它从同一权威输入、快照和移动校准重新生成并逐对象核对上游地点报告，全部 `33` 种来源类型必须进入唯一容量分类表；未知类型不能静默当作“不需要设施”。
- 透明桥在已有低频 `social_route_date_evidence.v2` 每张地图行中加入紧凑 `prepared_cultivation_capacity.v1`：只遍历现有 HoeDirt 与无灌木花盆，完整记录开放槽、占用槽、花盆数和按收获物分组的占用槽。无法识别收获物身份的占用槽单独计数，既不冒充目标作物也不冒充确定空位。它不扫描或猜测尚未开垦的地块。
- 当前能通过前四轴的来源中，钓鱼、地点采集和商店明确为容量轴不适用；蟹笼复用地点轴已经证明的实际放置网络；作物按权威最低单格产量要求足够数量的现存目标作物槽与开放备耕槽，可跨全部匹配目标地图汇总。开放槽只满足容量轴，不证明种子、浇水、生长期或目标日收获；这些仍归资源和 processing lead time 等后续轴。
- 机器、鱼塘、动物、果树、树液采集与烹饪设施类已经显式分类为容量来源，但当前仍在更早的静态来源/地点轴阻塞。若其在相应原生绑定求值器完成前意外到达本轴，会逐条失败关闭，不会被“无设施要求”放行。
- 聚焦 fixture 保留 `76/76` 条路线：72 条上游静态不适用，4 条活动来源容量均匹配，其中 2 条作物绑定 Farm 的 2 个开放耕地槽、2 条普通来源无需容量。删除 Farm 容量字段只阻塞 2 条作物路线；完整零容量则形成 2 条已解析 miss 而不是缺证据。第 223 天旧快照保留全部 1599 条路线：197 条上游静态不适用，1402 条继承地点轴及更早阻塞，0 条用旧字段猜测容量。训练授权仍为 false。
- GoalConditionedBootstrap Release、TransparentBridge 真实程序集构建与聚焦自测通过，未启动游戏或训练。下一固定切片进入 `resource_inputs`；容量来源类仍须在更早来源/地点轴放行时补齐其精确原生绑定，不能由资源轴代偿。

## 历史记录 · 2026-09-13 显式目标日期地点路线轴求值

- 新增 `acquisition_route_target_date_location_route.v1` 与 `build-acquisition-route-target-date-location-route`。构建器会从同一组权威输入和同一快照重建并逐对象比较节日状态报告，再把当前适用来源绑定到目标地点，批量复用 Core 唯一的 `FutureRouteDateEvidenceProducer.ProduceLocationArrivals`。没有新增第二套路由或 BFS 实现。
- 地点绑定按来源保持原生身份：正常季节作物落到 `Farm`；反季节作物只接受实时 `SeedsIgnoreSeasonsHere()` 为真的地图；商店使用实时 `route_graph` 柜台端点，并只在静态地图资产名与实时地点 ID 精确相等时采用静态端点；蟹笼使用完整实时网络中的实际放置地点；`Default`、`Farm_<type>`、Data/Locations 精确地点和矿井覆盖分别按反编译规则处理。透明桥为此新增每张地图的 `location_context_id`、`seeds_ignore_seasons_here` 以及农场 `farm_type_key`，不从示例或地图名猜测。
- 路由必须有同日全地图静态可行走证据、完整连接器门禁和版本化保守移动校准。保证到达时间必须早于至少一个来源时段结束；受天气限制的来源按目标地图的实时天气上下文求值。该轴只证明“能在来源窗口内到达来源地图”，不宣称随机来源已出现、鱼点可用、商店有货、资源足够、终端交互成功或物品已取得。
- 聚焦 fixture 保留 `76/76` 条路线，目标日活动来源 `4/4` 匹配；锁门商店等待到 9:00 后于 9:02 到店。删去全地图路线证据后，72 条静态不适用路线仍保持已解析，4 条活动来源逐条阻断。第 223 天真实旧快照保留全部 1599 条路线：197 条静态窗口不适用、927 条继承上游阻断、475 条活动来源因旧快照缺路线日期证据和移动上下文而阻断，0 条被猜测放行。训练授权仍为 false。
- GoalConditionedBootstrap Release 构建与地点路线聚焦自测通过，未启动游戏或训练。下一固定切片进入 `facility_capacity`；随机、资源、预算、提前量和 fresh 回执继续由各自依赖轴独立持有。

## 历史记录 · 2026-09-13 显式目标日期节日状态轴求值

- 透明桥新增轻量 `world_progress.game_state_query_calendar_state`：实时读取 `Game1.stats.DaysPlayed`、当前总日数与时间、`Data/Festivals/FestivalDates` 的完整日期键、当前活动的被动节日 ID，以及 `Data/PassiveFestivals` 中每个条目的季节、起止日、开始时间与原始条件。它只遍历两个小型已加载数据表，不读取地图或执行节日逻辑。带地点上下文的普通节日形式仍明确标为 `not_projected`；当前权威 1599 条路线没有这种形式。
- 新增 `acquisition_route_target_date_festival_state.v1` 与 `build-acquisition-route-target-date-festival-state`。该阶段从同一原始输入和同一快照重新编译并逐字段比较上游解锁报告，随后按 1.6.15 反编译实现 `DAYS_PLAYED`、`IS_FESTIVAL_DAY` 和 `IS_PASSIVE_FESTIVAL_OPEN`，包括单重否定、普通节日日期偏移、被动节日开始时间的包含式边界，以及各原生处理器对尾随参数的真实行为。非法双重否定、缺字段和未投影地点上下文均失败关闭。
- 新报告不再复制一遍逐步膨胀的路线字段，而是把完整强类型 `upstream_route` 嵌入每条结果；fixture 和总回归均做逐对象 JSON 等值检查，避免后续轴再次丢失来源、窗口或解锁证据。锁定第 37 天的旧归档快照实跑保留全部 `1599` 条路线：`919` 条继承上游阻塞，`191` 条静态窗口不适用，`483` 条节日轴无需额外事实即可继续，剩余 `6` 条正是 Trout Derby、SquidFest 和普通节日条件，因旧快照早于新桥字段而明确阻塞。未知条件仍为 0，训练授权仍为 false。
- 聚焦 fixture 在被动节日开始时刻边界验证三类条件全部命中，并覆盖日期偏移、否定、地点上下文阻塞、缺字段逐路线封闭和上游制品篡改拒绝。GoalConditionedBootstrap 与 TransparentBridge Release 构建通过，未启动游戏或训练。下一固定切片进入 `location_route`；随机与资源条件继续由各自轴持有。

## 历史记录 · 2026-09-13 显式目标日期解锁状态轴求值

- 透明桥新增轻量 `world_progress.game_state_query_unlock_state`。它一次性读取 `Game1.getAllFarmers()` 中每位玩家的精确 ID、Current/Host 身份、三类邮件集合、完整 `Stats.Values`，以及该玩家队伍中仍为 `InProgress` 的特别订单 ID 和逗号拆分后的特别规则；同时读取 `IslandNorth.bridgeFixed`。字段来源逐项绑定锁定 1.6.15 的 `GameStateQuery.WithPlayer`、`PLAYER_HAS_MAIL`、`PLAYER_SPECIAL_ORDER_ACTIVE`、`PLAYER_SPECIAL_ORDER_RULE_ACTIVE`、`PLAYER_STAT` 与 `IS_ISLAND_NORTH_BRIDGE_FIXED` 反编译分支。该读取只遍历小型集合，不加载地图资产或生成全量世界快照副本。
- 新增 `acquisition_route_target_date_unlock_state.v1` 与 `build-acquisition-route-target-date-unlock-state`。构建器会从同一组权威输入重新编译目标日日历报告并逐字段比较，再要求透明快照的 `game_version` 和 `time.total_days` 与目标日一致。Current、Host、Any、All 和已存在的数值玩家 ID 按原生选择规则求值；Target 因来源调用上下文未被制品携带而明确阻塞，不会默认冒充 Current。
- 解锁轴只接收上述五类已反编译谓词。`IS_FESTIVAL_DAY`/`IS_PASSIVE_FESTIVAL_OPEN`/`DAYS_PLAYED` 仍归日历动态条件，`RANDOM`/`SYNCED_*` 归随机轴，`PLAYER_HAS_ITEM` 归资源轴，玩家地点谓词归位置轴；未知条件阻塞。邮件 `Any` 额外保留原生 `mailForTomorrow` 的 `%&NL&%` 后缀语义，玩家统计缺失键按原生 `Stats.Get` 返回 0，特别订单只认可 `InProgress` 行和精确逗号规则。每条结果继续无损携带上游 `source_resolution_status` 和命中的完整时间、天气及动态条件窗口，后续轴不需要回读或猜测另一份报告。
- 锁定 1.6.15 的旧归档快照（夏 10，`target_total_day=37`）实跑仍保留 `1599` 条 occurrence：`910` 条上游来源阻塞，`191` 条静态窗口不命中，`498` 条命中。命中项中 `489` 条无需解锁事实即可继续，`9` 条因旧快照尚无新桥字段而明确阻塞；另保留 6 个动态日历条件、1 个随机条件和 1 个资源条件，未知条件为 0。没有把旧快照中的零散相似字段拼成伪证据。
- fixture 已验证不同 Current/Host 玩家、Any/All/数值 ID、邮件类型与延迟邮件后缀、否定特别订单规则、统计包含区间、姜岛桥、Target 阻塞、缺字段按路线失败关闭、跨日快照拒绝和上游制品篡改拒绝。TransparentBridge 与 GoalConditionedBootstrap Release 均为 `0 warning / 0 error`，组合自测通过；未启动游戏或训练。其后续动态节日日历条件已由上节独立闭合，解锁轴本身不越权消费其他轴。

## 历史记录 · 2026-09-13 显式目标日期日历轴求值

- 新增 `acquisition_route_target_date_calendar.v1` 和 `build-acquisition-route-target-date-calendar`。输入为权威需求库存、路线 lowering、Master Angler 窗口、静态来源报告与显式 `target_total_day`；构建时重新运行静态来源编译并按完整类型化内容比对报告，任何中间制品篡改、来源哈希漂移、窗口结构错误或超出第三年爷爷评估前的日期都会失败关闭。
- 每个 occurrence 只会得到三种日历轴结果：静态窗口命中目标日、静态窗口不命中目标日、上游静态来源未解析。字段明确命名为 `static_window_matches_target_date`，不得解释为完整候选可用；命中的原始时段、天气、随机标志、位置要求和动态条件原样保留，其他 11 个依赖轴没有被隐式满足。
- 锁定 1.6.15 的春 1 日（`target_total_day=0`）实跑保留全部 `1599` 个 occurrence：`689` 条日历轴可判定，其中 `451` 条静态窗口命中、`238` 条不命中；`910` 条因来源解析器尚未覆盖而明确阻塞。15 条命中路线仍带动态条件。总报告保持 `training_label_eligible=false`，下游不得据此单轴结果发放标签。
- 聚焦 fixture 同时验证第一年春 1 日命中与第二年同日受 `!YEAR 2` 排除、包含式原生时间上界、动态条件保留、静态报告篡改拒绝和日期越界拒绝。Release 构建与聚焦自测通过；未启动游戏或训练。后续 `unlock_state` 轴已在上节实现；这里的 `calendar_axis_resolved` 只表示静态窗口可判定，仍携带的节日类动态条件必须在目标日快照上继续求值。

## 历史记录 · 2026-09-13 Data/Shops 库存与访问来源解析

- 权威需求库存新增并哈希绑定唯一 `runtime_data_shops`、`access_constraint_index`、`native_shop_stock_rule`、`native_shop_open_rule` 与 `native_shop_purchase_rule`。构建时核对锁定 1.6.15 的 `ShopBuilder.GetShopStock/CheckItemCondition/GetBasePrice`、`Utility.TryOpenShopMenu`、`ShopMenu` 扣款/交换/配方/同步库存/购买动作分支，以及原生 `GameStateQuery` 日期谓词；证据缺失、哈希漂移或源码标志漂移均失败关闭。
- `sells` 必须按精确 `shop:<shop id> / Data/Shops / payload.<shop id>.Items[<row>]` 绑定，并再次与权威访问索引的同一库存行及原生条件处理器核对。每条来源保留货币类型、价格、有限库存模式、交换物、配方、利润率/价格/库存修饰器需求、购买副作用、店主条件、地图柜台端点和可关联的门禁时间；完整原行由来源路径、文件哈希和行哈希共同固定。
- 商店日期投影按反编译语义求值 `SEASON`、`YEAR`（含否定）、`DAY_OF_WEEK`、`DAY_OF_MONTH` 与 `TIME`；玩家状态、同步选择和同步随机等条件原样留作目标日动态谓词。库存行的日期窗口不等于商店当日可进入：节日地图、店主在岗、NPC 日程、地图变化、实时剩余库存、价格/交换资源和原生菜单回执仍由下游轴逐项求值，所有行继续是 `target_date_pending`，`training_label_eligible=false`。
- 锁定 1.6.15 实跑保留全部 `1599` 个 occurrence，结果从 `580 resolved / 1019 blocked` 提升为 `689 resolved / 910 blocked`。`109/109 sells` 全部解析，覆盖 32 个商店；其中 68 条交换购买、69 条有限库存、1 条配方、2 条同步随机来源，原生条件处理器缺口和空窗口均为 0。27 条目标路线带直接柜台端点，22 条还能关联基础地图门禁；其余节日/动态入口明确留在位置访问轴，未被猜测补齐。
- 商店实现已拆为路由绑定、日历投影和访问证据三个文件；组合自测覆盖星期、否定年份、动态随机、交换/库存、端点/门禁以及 Data/Shops 与访问索引篡改失败关闭。Release 构建和精确数据制品已通过，`Run-Regression.ps1` 的新计数/字段断言与语法已更新。未启动游戏或正式训练。下一固定切片进入目标日期日历轴求值，不再继续添加静态来源家族。

## 历史记录 · 2026-09-13 Data/Crops 日历来源与生长约束

- 权威需求库存现在新增并哈希绑定唯一 `runtime_data_crops`、`native_crop_growth_rule` 与 `native_crop_planting_rule`。构建阶段同时检查锁定 1.6.15 `Crop.IsInSeason`、`Crop.DaysInPhase/RegrowDays`、野生种子随机分支，以及 `HoeDirt` 的季节许可和生长加速分支；缺文件、哈希漂移或源码标志漂移均失败关闭。
- `harvests_as` 按精确 `crop:<seed id> / Data/Crops / payload.<id>.HarvestItemId` 绑定。每条解析结果保留原生季节、逐阶段天数、基础首收天数、再生周期、浇水、稻田、纹理/精灵、种植地点规则和可能产物全集。正常季节生成 `crop_native_season` 窗口；非正常季节只生成要求地点实时证明 `seeds_ignore_seasons` 的窗口，不能把温室/姜岛许可外推到任意地块。
- 春夏秋冬野生种子 `495..498` 依据运行时行的 `TileSheets\\crops / SpriteIndex 23` 与反编译随机分支双重核对，公开完整可能产物域并标记 `stochastic_outcome=true`。`harvests_as` lowering 因此改为 `source_resolved_downstream`，不再把所有作物错误归为确定性 fresh receipt。
- 季节窗口只解决 `calendar_window` 的静态来源部分。肥料、农学家、稻田加速、实际浇水连续性、种子/地块/地点解锁、目标日首收或再生波次仍分别属于生长提前量和其他依赖轴；这些事实虽然已完整携带，尚未被求值，所有行继续是 `target_date_pending`，`training_label_eligible=false`。
- 锁定数据实跑保留全部 `1599` 个 occurrence，结果由 `505 resolved / 1094 blocked` 前进到 `580 resolved / 1019 blocked`；新增的 `75` 条均为真实作物路线，其中 `8` 条为野生种子随机来源。四集合自测、作物证据篡改负例、真实制品约束检查、GoalConditionedBootstrap Release、Core game-free `33/33` 和 Backend `188/188` 均通过；未启动游戏或训练。
- `Run-Regression.ps1` 已更新新计数和作物域断言且语法检查通过；完整脚本在本次逻辑执行前被既有 `OptionCapabilityRegistrySource.cs` 输入锁漂移拦截，工作树中的该文件未被本分支修改，本切片不擅自刷新无关权威锁。下一固定切片解析商店库存、开放与条件来源；随后才进入逐目标日期的动态依赖求值。

## 历史记录 · 2026-09-13 Data/Locations 日历来源解析

- 把 Master Angler 原有日历归一化逻辑抽成唯一 `NativeCalendarConstraintNormalizer`，Master Angler 与 Data/Locations 路线共同使用。原有 72 种鱼机会目录重建哈希保持 `fab38278265ca6019e8962b4cdfe4ff58897ae1dd7aa05de7ea7baff40c7fbcb`，没有形成第二套鱼类条件系统。
- 新解析器静态求交 `LOCATION_SEASON`、`SEASON`、`YEAR`、`TIME`、`WEATHER`；当前 1.6.15 Data/Locations 中已知但必须在目标日实时判断的邮件、节日、特别订单、地点、已持物、桥梁、累计天数和 `RANDOM` 等谓词原样保留为 dynamic conditions。任何未来出现的未知谓词进入 `unparsed_conditions` 并阻塞路线，不能静默放行。
- `acquisition_route_calendar_resolution.v1` 现在额外哈希绑定唯一 `runtime_data_locations` 证据，并按原始 `location/source row index` 精确连接考古点、地点采集和非鱼钓获。夹具刻意使用原始键名，大小写漂移、行号越界、未知条件、静态空窗口、过期窗口或证据哈希变化均失败关闭。
- 锁定 1.6.15 实跑仍完整保留 `1599` 个路线 occurrence：`505 resolved / 1094 explicit blocks`。已解析组成是蟹笼 `20`、Master Angler 地点钓鱼 `241`、矿井钓鱼覆盖 `3`、考古点 `68`、地点采集 `165`、非鱼钓获 `8`；所有 resolved 行均有至少一个窗口，当前精确地点数据不存在未知条件阻塞。剩余 `28` 种 route kind 继续统一标记 `blocked_pending_route_kind_calendar_parser`。
- 这批结果仍是 `resolved_static_source_window_target_date_pending`：动态谓词、目标日选择和其余 11 个依赖轴尚未求值，`training_label_eligible=false`，未启动游戏或正式训练。下一批先解析作物来源季节与生长期，再解析商店库存条件；生长/加工提前量仍归独立 `processing_lead_time` 轴，不能用来源季节替代。
- 验证通过：GoalConditionedBootstrap Release `0 warning / 0 error`，四收集组合自测（地点证据哈希、已知动态条件保留、未知条件阻塞及篡改负例）通过，Core game-free `33/33`，Backend `188/188`，`Run-Regression.ps1` 语法检查通过。

## 历史记录 · 2026-09-13 跨日期依赖轴防遗漏门禁

- 权威获取降层现在为每个 route kind 和每条 requirement route occurrence 固定列出同一组 12 个下游依赖轴：日历窗口、解锁状态、位置路线、设施容量、资源输入、货币预算、库存预留、加工提前量、随机重试预算、每日时间/体力预算、机会成本以及 fresh 终态回执。列表长度、唯一性或成员有任一偏差都会失败关闭。
- `GoalMethodFrontierBuilder` 把这份清单写入每个 `authoritative_acquisition_route` 节点，并明确标记 `dynamic_dependency_resolution_status=pending_per_route_axis_evidence`。这只证明依赖类别没有被遗漏，不表示这些依赖已经按具体日期求值，也不允许把“字段存在”冒充“路线当前可行”。
- 当前四收集 Teacher、全出货 Teacher 与统一目标前沿都拒绝旧的或被删轴的降层制品；总回归同时核对顶层、route kind 和全部 route occurrence，重复轴也不能绕过门禁。历史 `acquisition_route_option_lowering.v1` 若没有新清单会被视为 stale，必须从锁定权威输入重新生成。
- 离线验证已通过：GoalConditionedBootstrap Release 构建 `0 warning / 0 error`，四收集组合自测通过（包含删去一个轴后必须抛出 `InvalidDataException` 的负例），Core game-free `33/33`，Backend `188/188`。未启动游戏，未写正式训练数据，`formal_training_started=false`。
- 固定当时后续计划是实现逐路线、逐轴的动态证据解析，首先闭合 `calendar_window`，随后按解锁、资源/预算/预留、加工与随机重试、日内时间体力和 fresh 终态回执依次接入。只有 12/12 轴对某个候选均得到 `satisfied`、有证据的 `not_applicable` 或显式 `blocked`，该未来日期候选才可进入 Teacher 选择。

## 历史记录 · 2026-09-13 获取路线日历来源解析第一批

- 新增哈希绑定的 `acquisition_route_calendar_resolution.v1`，它逐条保留 requirement set、requirement、alternative、route 索引、物品和原始 source identity，并同时绑定需求库存、获取降层、Master Angler 窗口索引及机会目录。窗口还会从已验真的 catalog 确定性重编译并逐字段比较；任一旧降层、错库存、错窗口内容或被篡改的 catalog 哈希都会失败关闭。
- 没有新写第二套钓鱼日历：首批直接复用既有的反编译/运行时数据链 `MasterAnglerOpportunityCatalog -> MasterAnglerStageOneWindowIndex`，精确连接 `native_location_fish_spawn`、`native_mine_fishing_override` 和 `native_crab_pot_output`。窗口保留年份、季节、时段、天气、动态条件、地点、矿区、装备限制和随机结果标记。
- 锁定 1.6.15 全量制品实跑结果为 `1599 routes / 264 resolved static sources / 1335 explicit blocks`。已解析部分为蟹笼 `20`、地点钓鱼 `241`、矿井钓鱼覆盖 `3`；所有 resolved 行都至少携带一个精确窗口。
- 实跑额外识别出 `8` 条同属 `native_location_fish_spawn`、但目标物品不在 72 种鱼分母中的路线（例如钓点的木头、石头等非鱼产物）。它们没有套用任意鱼种窗口，而是保留为 `blocked_authoritative_fish_window_not_found`；总回归锁定这 8 个 occurrence identity，防止以后静默遗漏或误接。
- 当前报告状态仍为 `partial_static_sources_explicitly_blocked`，且每个已解析行仍标记 `target_date_pending`。静态来源窗口不等于某个目标日的动态条件已满足，更不等于其余 11 个依赖轴已满足；`training_label_eligible=false`，正式训练仍未启动。
- 离线验证通过：GoalConditionedBootstrap Release `0 warning / 0 error`，四收集组合自测及篡改哈希负例通过，Core game-free `33/33`，Backend `188/188`。下一批先把 Data/Locations 的非鱼钓点、采集物和蚯蚓点共用日历条件抽成单一原生条件解析器，再接作物季节/生长期和商店库存条件，避免按 route kind 复制解析逻辑。

## 历史记录 · 2026-09-13 博物馆与社区中心捐赠滚动路线

- `museum.donate_items` 与 `community_center.donate_bundle_items` 在目标建筑之外时，现按透明 `route_graph` 只发布当前地图可验证的首个 `route_connector_tile`；连接器携带精确物品、库存槽位以及博物馆/Bundle 身份。常规 daily-plan 可在 fresh 快照后锁定同一机械 continuation 续编；Teacher product 则把该连接器作为独立监督步骤，由外层 controller 在下一 episode 从 fresh 全量状态重建候选并选择终态捐赠。
- 社区中心 continuation 额外固定 `bundle_data_key`、ingredient 索引、需求数量和最低品质证据。Teacher frontier 只接纳身份、槽位、数量、品质均一致的路线步骤；负槽位、低品质或被篡改的 continuation 在上游失败关闭。
- 路线步骤使用 `authoritative_collection_rolling_route_step`，只证明声明的连接器端点确实抵达，不冒充捐赠完成。博物馆与社区中心的最终正样本仍分别要求逐项 `donated=false -> true` 和精确 Bundle ingredient `false -> true`。
- 目标地图内不新增独立站位动作：既有 `executor.donate_museum_item` 与 `executor.donate_community_center_item` 已负责本地 BFS、交互端点和原生菜单生命周期。这样避免与既有原生执行器形成两套实现，也不会把目标地图坐标提前绑定到来源地图快照。
- 结构化候选、续程锁定与 Teacher frontier 已通过隔离真实运行验收。隐藏、静音 E 盘运行 `runtime-collection-donation-routes-20260913-011522` 为 2/2：博物馆和社区中心均从 Town 的真实透明连接器出发，在 fresh 状态下续编既有原生捐赠器，连接器与终端原语全部 `applied/verified`，continuation 完成且精确终态变化成立。该运行使用 `--skip-training`，正式训练仍未解禁。
- 实测同时修正了原生 `WarpCommunityCenter` 特殊入口：透明桥现在按锁定版反编译公开 `CommunityCenter (32,23)` 目标和 `Game1.MasterPlayer` 的 `ccDoorUnlock || JojaMember` 门禁；执行器只通过原生 `GameLocation.checkAction` 触发，不直接传送。当时后续计划固定为跨日期调度，把日历、解锁、资源/库存预留和机会窗口接入同一 Teacher 依赖链。

## 历史记录 · 2026-09-11 有界多原语 Teacher 重复闭环

- 出货终端的“接近/投放”已收拢：同在农场但不处于出货箱站位时，`economy.ship_items` 现在一次编译为 `move_to_tile -> ship_inventory_item_to_bin`，两个原语共享精确物品 continuation，并只在原语之间 fresh 重绑定状态哈希。跨地图部分仍保持每次一个已解析连接器、抵达农场后再 fresh 编译上述本地有界队列，未把未来地图坐标提前绑定到旧快照。
- 隐藏、静音、隔离实测 `bounded-shipping-candidate-queue-20260911-221453` 连续执行 2 个 episode：第一轮机器收取按两原语完成；第二轮同一 `economy.ship_items` 候选按 `move_to_tile -> ship_inventory_item_to_bin` 完成，`queue_item_count=2`、回执 `ready`、需求转移 `1`，两轮均接纳。规范数据集为 `2 accepted / 0 rejected`，`formal_training_started=false`。
- `TeacherPreferenceQueueLoader` 现在接收 `1..8` 个有序 pending 队列项，仍要求每项恰好一个机械原语，并逐项核对所选 candidate、编译计划 step、来源 action、actor、执行模式和原始状态哈希；重复 queue-item/plan-step ID、越界队列或语义漂移均在派发前失败关闭。
- 运行器复用唯一的顺序队列执行链，不新增第二套执行器。每个后续原语派发前只把持久化命令的 `state_hash` 重绑定到上一原语的 fresh 后状态，其他命令语义保持不变；整队列回执记录原始编译哈希、逐步有效哈希、tick、changed facts、原语验证结果和严格的前后哈希链，且只有最后一步可以标记本次固定 Teacher 候选完成。
- `CurrentStageOneCollectionTeacherReceiptBuilder` 新增 `queue_execution_receipt.v1` 准入。它要求队列 ID、项顺序、原语、有效命令、逐步哈希/tick 链、fresh 状态和正向原生验证全部吻合，并在完整候选的权威 requirement credit 确实变化后才生成一条 `policy_decision_trajectory.v2`；旧单原语 `plan_execution_episode.v1` 路径继续兼容。
- 隐藏、静音、隔离实测 `bounded-stage-one-collection-teacher-20260911-211944` 连续执行 3 个 episode：第一轮同一 `farm.collect_machine_outputs` 候选按 `move_to_tile -> collect_machine_output` 两步收取 Raisins 并接纳；第二轮仅接近出货箱，需求未变化，因此保留执行证据但不写训练行；第三轮原生投放出货箱后才接纳。规范数据集结果为 `2 input / 2 accepted / 0 rejected / 0 duplicate / 0 conflict`。
- 控制器每次最多执行 16 个 episode，每个 episode 都从新的透明状态重新生成完整候选与独立 Teacher 偏好。中间 continuation 不会被误标为成功，真正的证据合同错误则立即停止。当前仍为 `formal_training_started=false`。
- 固定当时后续计划：在隔离真实游戏中验证博物馆与社区中心的远程滚动捐赠。跨地图只允许执行当前快照可验证的首个连接器；抵达目标地图后 fresh 续编既有原生捐赠 primitive，由该 primitive 自己完成本地 BFS 与菜单生命周期，不再生成重复的独立站位动作。完成捐赠回执验证后，再扩展跨日期候选与日历/解锁/资源约束，进入其余 17 个长期目标证明。

## 历史记录 · 2026-09-11 首条四收集 Teacher Product 真实轨迹

- 新增隔离、隐藏、静音的 `Invoke-CurrentStageOneCollectionTeacherProductRollout.ps1`。它从 E 盘源存档复制运行专用存档，只加载 TransparentBridge 与 RuntimeTestHarness，依次生成 fresh 全量快照、同状态候选排名、Master Angler 当前日期意图、独立 Teacher 偏好，并通过既有 Product Executor 执行偏好内嵌的精确队列；运行全过程保持 `--skip-training`。
- `TeacherPreferenceQueueLoader` 只接收 `training_singleplayer` 下恰好一个 pending 队列项和一个机械原语，严格核对状态哈希、actor、偏好、编译计划、来源 step 与队列。高层候选允许经唯一 `DailyPlanCompiler -> ActionQueueCompiler` 降为其执行器原语，但任意脱离所选编译 step 的替换队列都会在执行前被拒绝。
- 真实运行暴露并修复两项执行边界错误：独立移动不再把 `MaxCrops=16` 误当路线长度上限，`executor.move_to_tile` 显式携带 512 格上限；相邻机器候选不再生成零距离移动。Community Center 已完成 Bundle 计数也改为使用原生 required-slot 完成语义，而不是错误要求所有备选 ingredient 同时完成。
- 完整回归 `stage-one-collection-teacher-20260911-014412` 一次通过：原生碰撞移动实际走 41 格抵达 Dehydrator，随后 `executor.collect_machine_output` 收取 `(O)Raisins`；Product 请求哈希与执行前 fresh 哈希一致，回执为 `applied/verified`，并验证机器输出、库存及经验投影。
- 回执准入确认 `full_shipment:item:Raisins` 为精确库存 `0 -> 1`，生成首条 `policy_decision_trajectory.v2` Teacher 轨迹。标准 PolicyDataset 验证为 `1 input / 1 accepted / 0 rejected / 0 conflicts`。正向 primitive verification reasons 作为证据保留；只有 block/failure reasons 才拒绝准入。
- 本切片仍明确 `formal_training_started=false`，没有把单条证据混入正式训练组件。当时后续计划是建立有界重复采集控制器：优先支持同一候选的多原语整队列回执，在每个原语后 fresh 重绑定并仅在完整候选需求转移成功时写一条 Teacher 轨迹；随后扩大四收集候选种类与日期覆盖，再进入其余 17 个长期目标证明。

## 历史记录 · 2026-09-10 四收集集合 fresh 回执训练行准入

- 新增唯一 `CurrentStageOneCollectionTeacherReceiptBuilder`。调用方必须先落盘实际执行的 Teacher 偏好制品，再把同一制品的单个 pending 队列项、原生 `plan_execution_episode.v1` 回执和 fresh 后快照送入准入器；不得重新生成 queue ID 后拿新队列匹配旧回执。
- 偏好制品的五份源 SHA-256 必须仍与需求库存、获取降层、候选排名、前快照和 Master Angler 意图一致。准入器会重新计算 Teacher 选择与编译语义，核对选择、完整当时候选成员、顺序和去除随机 ID 后的队列内容；因此既保留实际执行 queue ID，也不信任可篡改的中间 JSON。
- 回执门严格绑定 run、queue、primitive option、唯一机械 step 的 primitive kind、前后 state hash、前后 tick、`applied/success/verified/fresh`、非空 changed facts 和完整 effective queue item。当前切片只接收恰好一个 pending 原语；多原语高层动作在拥有可靠的整队列完成回执前继续失败关闭。
- 每个 selected candidate credit 都必须由 fresh 后状态逐项证明：精确库存增加、原生 Full Shipment 完成或“库存减少并进入下一日原生结算”的 shipping-bin 增量、博物馆捐赠、Community Center 精确 ingredient 完成、鱼类收集完成，或 Master Angler 精确路线端点抵达。任一 credit 未变化都不生成训练行。
- 成功结果复用 `policy_decision_trajectory.v2`，只保留 Teacher 当前可用候选，清零 learner rank/score/model score/expected reward，并在 `audit.teacher_supervision` 中记录偏好、源输入、回执、后快照 SHA-256 及逐 credit 转移。既有数据集构建器已接收该行；错 queue 和“成功但需求未变”均被回归拒绝。
- 本切片仍明确 `formal_training_authorized=false`，总前沿仍为 `2/19 executable / 17/19 pending`。固定当时后续计划是把该准入器接入隔离的四集合 Teacher Product rollout，持久化同一偏好制品并取得第一条真实游戏回执训练行；之后再扩展可靠的多原语整候选回执、未来日期调度和其余长期目标证明。

## 历史记录 · 2026-09-10 四收集集合确定性 Teacher 偏好

- 新增唯一 `CurrentStageOneCollectionTeacherPreferenceBuilder`。它从需求库存、获取降层、同状态候选排名、透明快照和 Master Angler 目标日期意图重新构建四集合当前候选前沿，并逐一核对状态、目标、分母、候选 credit、选择组和五份输入 SHA-256；任一漂移均失败关闭。
- 固定字典序只使用权威当日截止、截止余量和时刻、终端需求转移、跨集合与逐需求精确 credit、候选稀缺度、确定性耗时和体力。Teacher 不读取 learner 的 rank、score、model score 或 expected reward；选中候选进入既有 `DailyPlanCompiler -> ActionQueueCompiler` 前，这些学习器信号会被清零。
- 候选 ID 只稳定报告顺序，不负责打破监督平局。两个最高候选的权威向量完全相同时返回 `blocked_authoritatively_tied_top_candidates`，不会按 ID 伪造偏好。当前不可用需求继续 deferred，未选中的当前可用候选只作为反事实比较项，不生成二元负标签。
- 结构化回归已证明：故意反转 learner 分数不会改变选择；精确平局会被拒绝；当日截止的 Master Angler 路线可经原有日计划和动作队列编译为一个 pending 项。测试夹具使用与透明桥同构的原生字段来源、碰撞网格和连接器，没有放宽生产执行门槛。
- 当时只形成单状态偏好标签，仍明确 `formal_training_authorized=false`，总前沿保持 `2/19 executable / 17/19 pending`。固定当时后续计划是把选中偏好绑定到 fresh 原生动作前后回执，只有精确 credited requirement 转移验证成功才生成训练行；之后仍需未来日期调度和剩余长期目标证明。

## 历史记录 · 2026-09-09 四收集集合统一实时候选合同

- 新增 `CurrentMasterAnglerTeacherFrontierBuilder`，把原生 72 种鱼的逐项完成态、权威需求库存、逐项获取降层、同状态候选排名和既有目标日期意图做严格连接。快照、库存、降层、排名、意图、窗口索引、机会目录和路线计时校准均保留 SHA-256 并交叉校验；旧状态或复制错配的意图失败关闭。
- Master Angler 当前候选统一经过 Core 的 `MasterAnglerCurrentCandidateMatcher`。路线连接、终端钓鱼和已就绪蟹笼必须携带精确目标意图、完整剩余路线加终端预留标记以及精确可能产物。错误鱼种的蟹笼候选不能仅靠附加合法意图参数获得绑定。
- 新增唯一 `CurrentStageOneCollectionTeacherFrontierBuilder`，统一 Full Shipment、Master Angler、Museum Collection 和标准 Community Center。一个实际候选只出现一次，但可同时获得它确实推进的所有需求 credit；社区中心 OR Bundle 保留剩余槽位上限。当前不可用需求继续 deferred，不生成负样本。
- 该产物只证明“候选属于当时可教集合”，还没有选出 Teacher 偏好。统一选择合同不读取 learner 排名或分数，不生成负标签，并明确记录 `teacher_preference_label_eligible=false`。因此总前沿仍为 `2/19 executable / 17/19 pending`，正式训练仍未解禁。
- 四集合结构化 fixture、聚焦 self-test、完整隔离 bootstrap 回归、Core game-free 测试和 Backend 测试均通过。历史 r36 快照缺少逐鱼透明分母，Master Angler 分支被正确拒绝，没有伪造标签。
- 固定当时后续计划：在这个统一当前候选集合上实现与 learner 无关的确定性 Teacher 偏好查询；随后仍需原生回执、未来日期调度和其余长期目标证明，才能开放正式全量训练。

## 历史记录 · 2026-09-09 博物馆与标准社区中心实时 Teacher 前沿

- 新增唯一 `CurrentCollectionTeacherFrontierBuilder`，复用 Full Shipment 已验证的当前候选门控、精确 `qualified_item_id` 绑定和权威获取端点证据。输入仍绑定需求库存、获取降层、同状态排名和实时快照四份 SHA-256；排名与快照 `state_hash` 不一致时失败关闭。
- 博物馆适配器要求 95 项 `donatable_items` 与权威分母逐项一致，并交叉检查 `donated_count`、`missing_item_count`、`missing_item_ids` 和 `collection_complete`。正标签只来自精确原生捐赠投影或对应缺件的权威获取端点。只有旧聚合计数、没有逐项分母的历史快照会把博物馆集合单独标为 blocked，不会猜测 95 项完成态。
- 标准社区中心适配器按 30 个权威 `bundle_data_key` 对齐运行时 Bundle 行，逐槽验证 OR 选择数、物品身份、数量、最低品质和完成位。每个未完成 Bundle 都公开 `remaining_slot_count`；物品备选要求“达到数量与最低品质前保留、随后原生捐赠”，金库要求“保留资金、随后原生付款”，不会把一个 OR Bundle 的全部备选误教成同时采购。
- 直接捐赠标签必须同时证明 Bundle、ingredient index、数量、品质、库存前后量和完成投影。获取标签必须有精确产物、正数量、权威 endpoint；最低品质大于 0 时还必须有一致的精确品质参数。未知品质、低品质和没有原生付款投影的金库候选均 deferred，不生成负标签。
- 结构化回归得到 4 种合法绑定：博物馆捐赠、博物馆获取、Bundle 捐赠、高品质 Bundle 获取；不完整品质与金库伪候选均被排除。真实 r36 为 `museum blocked (legacy per-item denominator absent) / community center 30 observed / 0 current binding`，准确保持不可训练。当时后续计划把现有 Master Angler 日期窗口意图接入同一实时需求前沿，并统一四个收集集合的候选选择合同；正式训练仍未解禁。

## 历史记录 · 2026-09-09 全出货实时 Teacher 前沿

- 新增唯一 `CurrentFullShipmentTeacherFrontierBuilder`，把实时快照、同状态排名、154 项权威需求库存和逐需求获取降层做严格交集。输入文件均记录 SHA-256，排名 `state_hash` 必须与快照完全一致；透明进度的分母、逐项身份、已出货计数、缺失列表、完成标志和比例必须彼此一致，否则失败关闭。
- 正标签只有两种：带完整原生 Full Shipment 贡献字段的当前出货候选，或产物 `qualified_item_id` 精确匹配缺失物品且 option 是该物品权威获取路线端点的当前候选。已完成物品、同 option 的错误产物、被门控候选和没有精确依赖绑定的 supporting option 均不得进入。
- 没有当前候选的需求被标为 deferred，不生成负标签。真实 r36 快照验证为 `154 required / 6 completed / 148 missing / 0 current exact binding`，因此报告正确保持 `training_label_eligible=false`，没有把候选池暂时未出现的 148 项错误训练成负例。
- 该切片只闭合 Full Shipment 的“实时缺项 -> 当时具体动作”证据边界，没有改变总前沿 `2/19 executable / 17/19 pending`，也没有启动正式训练。博物馆与标准社区中心的同类实时适配现已由后续切片完成；当时当时后续计划是接入 Master Angler 已有的日期窗口意图。

## 历史记录 · 2026-09-09 Teacher 逐需求获取绑定

- 获取降层不再只报告 `33/33` 路线种类总数。现有唯一
  `AcquisitionRouteOptionLoweringBuilder` 为 4 个 Stage 1 收集分母、351 个需求组、450 个
  具体备选物品保留全部 1599 条权威来源，并逐条绑定 supervision/uncertainty、端点高层选项、
  支持选项及 Teacher/runtime 准入状态。
- Goal-Method 超图直接消费同一报告，形成
  `existing_high_level_option -> authoritative_acquisition_route ->
  authoritative_requirement_alternative -> authoritative_requirement -> requirement_set -> root method`
  的可追溯链。四个收集方向均显示自己的分母和准入计数，不能再用全局 33 类路线替代逐物品证明。
- `complete_full_shipment` 中声称树苔路线缺失的旧阻塞已删除；树苔、野树、春葱和文物点都由当前
  获取报告按具体物品绑定。剩余阻塞准确限定为实时日历/解锁/资源/路线时间/概率重试与一件预留，
  以及 154 项跨日结算和第三年前完成证明。
- 本切片没有把 `2/19` 改写为更高数字。它闭合的是 Teacher 从“缺少哪件物品”定位到现有高层
  获取动作所需的静态连接；跨日/跨季调度和原生新存档证明仍未完成，正式训练继续禁入。
- `current-option-input-lock.v1.json` 的仓库文本源改为显式 `utf8_lf` 规范化哈希，修复新检出工作树
  因 CRLF 与历史混合换行产生的假漂移。外部知识和反编译输入仍保持原始字节锁。

## 历史记录 · 2026-09-08 当时权威检查点：EVD-335

- `foraging.harvest_spring_onions` 已闭合读、候选、编译、原生运行和输出五门。它只接受当前已加载地图中原版基类 `Crop` 的 `forageCrop=true / whichForageCrop=1`，并复用唯一 `harvest_crop_tile -> executor.harvest_crop` 生产链；普通作物、姜和自定义作物不会进入该候选。
- 本步真实运行发现并修复了透明桥缺口：原生春葱的 `indexOfHarvest` 为空，`Crop.harvest` 才按觅食 ID 动态创建 `(O)399`。桥现在只对精确原版春葱发布 `harvest_item_projection_status=exact_from_decompiled_native_spring_onion_branch`，未知分支继续失败关闭；必需事实门也已授权该专用 adapter。
- 隐藏静音 E 盘隔离运行 `artifacts/runtime-spring-onion-daily-plan/runtime-spring-onion-daily-plan-20260908-233958/summary.json` 通过：DailyPlan 选择 `harvest:Farm:64,15`，原生执行 `applied/verified`，库存 `(O)399` 为 `0 -> 1`，Foraging XP 为 `19376 -> 19379`，目标作物被移除且状态哈希变化。运行使用 `--skip-training`，不得作为正式 Teacher 轨迹。
- 权威生成结果为 `230 registered / 232 semantic / 229 compiler-bound / 153 runtime-verified / 64 training-eligible / 145 Product Executor / 2 catalogued blocked`；获取路由为 `30/33 admitted / 3 blocked / 1599 occurrences`。剩余路由是树苔、砍野树产物和砍野树种子掉落，后两者共享同一个高层砍树获取缺口。
- Teacher 前沿仍为 `2/19 executable / 17/19 dependency pending / 0 missing graph / 0 governance blocked`，所以正式全量训练仍禁入。直接当时后续计划是闭合 `native_tree_moss_harvest` 的单一高层选项，并复用已验证的野树交互/收获内核，不创建第二套树木执行系统。

## 历史记录 · 2026-09-08 当时权威检查点：EVD-334

- `foraging.excavate_artifact_spots` 已用 E 盘隔离存档完成隐藏静音高层运行验收。唯一 `(O)590` 候选从 `foraging.excavate_artifact_spots` 经 DailyPlan 降为 `clear_obstacle_tile -> executor.clear_obstacle`，没有新增第二套挖掘执行器。
- 原生 Hoe 回执验证目标移除、完整输出 unit-state 集合、`+15` Foraging XP、`ArtifactSpotsDug`、地形与 Defense Book 邮件状态，并取得新鲜且变化后的快照哈希。`(O)SeedSpot` 与其他可清理物仍在上游排除；运行使用 `--skip-training`，夹具校准行不得冒充正式 Teacher 数据。
- 权威生成结果为 `229 registered / 152 runtime-verified / 63 training-eligible`；获取路由为 `29/33 admitted / 4 blocked / 1599 occurrences`。剩余四类是春葱收获、树苔收获、砍野树产物和砍野树种子掉落。
- Teacher 前沿仍为 `2/19 executable / 17/19 dependency pending / 0 missing graph / 0 governance blocked`。本步消除了多个终局共享的获取叶缺口，但尚未完成相应日历、解锁、资源、路线、保留量、概率重试和新存档期限证明，因此正式全量训练仍禁入。
- 直接当时后续计划：为 `native_spring_onion_harvest` 注册并闭合单一高层采集选项，复用现有 `executor.harvest_crop`，不得把机械原语直接作为 Teacher 输出。

## 历史记录 · 2026-09-08 Teacher / Student 收敛合同与当时禁入状态

- issue #90 与 #91 内容完全重复；其中 Teacher bootstrap、DAgger 重标、native outcome 后期优化和运行时 Student 主决策的方向正确，已整理为唯一规范 `docs/TEACHER_STUDENT_CONVERGENCE_CONTRACT_CN.md`。
- 系统目标是可验收的工程收敛，不宣称神经策略的数学全局最优。有限 Teacher 必须同输入同输出并终止；Student 以独立 Teacher 分歧、learner-state 恢复、新存档 21/21、零硬约束违规和性能门冻结。
- 当时并未收敛：Teacher 前沿为 2/19 executable、17/19 expansion pending，但 19/19 已具有可执行叶或类型化依赖图，不再有缺图 criterion。Master Angler 仅闭合当时日期的全 connector 路径和终端时间储备；未来日期、主动蟹笼容量、概率重试和真实跨日回执仍未完成。
- 代码审计确认 `StructuredPolicyTrainer.BuildPairs` 仍以 `candidate.Selected` 选正例。正式全量训练继续禁用；旧 r24-r35 只保留为控制面、执行器、恢复和性能证据。后续必须先类型化 `teacher_preference`、`native_outcome`、`student_observation` 并实现 learner-state Teacher 重标。
- 本块离线验证基线：Core `2467/2467`、KnowledgeCompiler `585/585` blocking 0、option matrix `228 registered / 62 training-eligible / 151 runtime-verified`，isolated full regression 和 Master Angler late-arrival/no-terminal-reserve 回归通过。没有启动游戏。

## 历史记录 · 2026-09-05 当时权威检查点：r35 round01

- `train.server.20260905.r34.plan05` 从 Summer 9 精确基线启动后，暴露了卧室窄通道中的真实执行缺陷：摸完宠物后，通用移动会为占路宠物反复规划不存在的替代路线，却不继续发送原生移动输入，最终六次命中 `movement_soft_obstacle_timeout`。该轮只有 1/2 个主动作验证，未进入跨日边界，事务保持 `staged_not_committed`；Summer 9 存档和五项 canonical 哈希均未改变，失败证据及 6 份执行器诊断已归档为 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r34-round05-failed-20260905-112856`，远端/本机 130 / 130 且哈希差异为 0。
- `20fff604434584f966824e043360b1802647027d` 保留唯一通用移动链：动态角色阻挡时先按 soft-obstacle BFS 重规划；无替代路线时持续发送原生方向输入，让宠物/NPC 按游戏机制让路；180 tick 超时仍失败关闭。RuntimeTestHarness Release 构建 0 warning / 0 error，game-free governance 20/20；`formal-r35-20fff60-20260905` 的 367 项发布清单通过校验。
- `train.server.20260905.r35.plan01` 从未改变的 Summer 9 基线重跑。日志记录角色从 FarmHouse `(10,9)` 开始移动并在 2 秒后原生切换到 Farm，旧软障碍超时未复现；5 次迭代完成 2 个 verified 主决策和 3 个原生保存边界尝试，进入 Summer 10。新增 3 条 applied 高层轨迹：每日摸宠物、完整处理 `passedOut3_Billed 8` 邮件、完整处理 `spring_23` 邮件；控制面回屋/睡眠仍为 0 条策略轨迹。
- 事务为 `committed_after_native_save_boundary`，存档树 SHA-256 从 `c542c7c9ed8192c2e26b19cc714c94e9589dad27ea586efe800e6208a6fa7756` 变为 `19de382994da925d5a47707eb143a89f0710a38e89dec7374b4d217faaaf62d7`。canonical 更新为 accepted 221 / rejected 0、163 / 5 / 53、5094 pairs，执行特征 230 行、horizon 观测 8 行；checkpoint / manifest SHA-256 为 `dcb6ff2bc8dd28a223fcd0fe5a27c4a58215ff23084b6013a30f9299daac774b` / `dc7628a1add5a9db7247980bc7e19e4f671c93c3b0f51bcf12e4917e3085904b`。
- unresolved Product pending 为 0，resolved 为 13；正式容器 `exit=0 / OOM=false`。完整成功归档为 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r35-round01-20260905-120836`，远端/本机均为 165 个文件，SHA-256 不一致为 0。
- 后续唯一合法起点是 Summer 10 存档与上述 canonical。服务器正式训练进程已停止；跨季、跨年、Grandpa 21 和 Companion 产品验收仍未完成。

## 历史记录 · 2026-09-05 当时权威检查点：r34 round04

- `train.server.20260905.r34.plan04` 从 round03 已提交的 Summer 8、存档和 canonical 精确哈希准入；并发 1，5 次迭代包括 2 次主决策和 3 次原生保存边界尝试，最终进入 Summer 9。存档树 SHA-256 从 `4bb3420051c550a5f8c371bec8867e3101ca155ad0c6a69a60ed9ae36ad7096f` 变为 `c542c7c9ed8192c2e26b19cc714c94e9589dad27ea586efe800e6208a6fa7756`。
- 新增 3 条完整策略轨迹：`mail.process_letter:route:fertilizers:FarmHouse:3,12`、每日摸宠物和 `mail.process_letter:open:spring_19_1`，最终 primitive 分别闭合到 `close_menu`、`pet_interact`、`close_menu`，均为 applied。另一个未完整闭合的队列尾项只记录 skip；`recovery:native_save_boundary` 的控制面回屋/睡眠仍为 0 条策略轨迹。
- canonical 更新为 accepted 218 / rejected 0、160 / 5 / 53、5011 pairs；执行特征从 224 增至 227，horizon 观测保持 8。checkpoint `structured-policy-775fb64e3351349790e74543` 的 SHA-256 为 `0b31d8084c6fcc728defd4ba916d8542b2e1efb13cbf97695f1db94a910b1035`，manifest SHA-256 为 `0af71721588d0da1bd78cb72cfca6e3cd74e7545f2edb2726390d3d9b08cf96b`；train/validation/test pair accuracy 为 0.992217 / 1.0 / 0.965602。
- 两次主排序耗时 2.228 秒和 82.095 秒；三个控制面边界中的后两个排序为 138.9 毫秒和 9.5 毫秒。原生日志记录 `SaveGame.Save() completed without exceptions.` 及 `starting summer 9 Y1`；unresolved Product pending 为 0，正式容器 exit 0、OOM false。
- 完整归档为 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r34-round04-20260905-111352`，远端/本机均为 172 个文件，缺失、额外、SHA-256 不一致均为 0；本机按源码算法重算的存档树摘要与报告一致。
- 后续唯一合法起点是 Summer 9 存档与上述 canonical；跨季、跨年、Grandpa 21 和 Companion 产品验收仍未完成。

## 历史记录 · 2026-09-05 当时权威检查点：r34 round03

- `train.server.20260905.r34.plan03` 从 round02 已提交的 Summer 7、存档和 canonical 精确哈希准入；并发 1，4 次迭代包括 2 次主决策和 2 次原生保存边界尝试，最终进入 Summer 8。存档树 SHA-256 从 `e967b3990b683b78a9b6bcad59fcfa0457907cf8e7f7b13ce5829c05bf54397a` 变为 `4bb3420051c550a5f8c371bec8867e3101ca155ad0c6a69a60ed9ae36ad7096f`。
- 两个主候选分别为 `ship:Farm:71,14:0:311:route:FarmHouse:3,12` 和 `mail.process_letter:approach:fertilizers`。前者完成原生出货，后者完成到邮箱的移动，机械回执均为 applied/verified；但两条 selected queue candidate 都没有完整闭合，所以只记录执行特征和 horizon 证据，没有伪造高层策略轨迹。`recovery:native_save_boundary` 的回屋与睡眠继续只属于控制面。
- canonical 策略数据保持 accepted 215 / rejected 0、157 / 5 / 53、4920 pairs；执行特征从 220 增至 224，horizon 观测从 7 增至 8。checkpoint `structured-policy-1946ee6d8c59b1a72f692a50` 的 SHA-256 为 `64283a58cb8e48eed06baffb1c8116a241033c26b00e792ec01807db537c345c`，manifest SHA-256 为 `47ac7aaff2798f3546eea1e0a5772a524dc3d9ee959e4f0506136d7e3fb18a78`；train/validation/test pair accuracy 为 0.992276 / 1.0 / 0.96683。
- 两次主排序耗时 2.427 秒和 45.624 秒；两个控制面边界排序为 203.5 毫秒和 12.4 毫秒。原生日志记录 `SaveGame.Save() completed without exceptions.` 及 `starting summer 8 Y1`；unresolved Product pending 为 0，正式容器 exit 0、OOM false。
- 完整归档为 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r34-round03-20260905-085309`，远端/本机均为 119 个文件，缺失、额外、SHA-256 不一致均为 0；本机按源码算法重算的存档树摘要也与报告一致。
- 本轮目标已经完成并停止。后续若继续，唯一合法起点是 Summer 8 存档与上述 canonical；跨季、跨年、Grandpa 21 和 Companion 产品验收仍未完成。

## 历史记录 · 2026-09-05 当时权威检查点：r34 round02

- `train.server.20260905.r34.plan02` 从 round01 已提交的 Summer 6 和 canonical 精确哈希直接准入；并发 1，5 次迭代包括 2 次主决策和 3 次原生保存边界尝试，最终进入 Summer 7。存档 SHA-256 从 `08b6e014081653f6625ad57106624417e0e02ca5f5778d744a393911f7dc6a49` 变为 `e967b3990b683b78a9b6bcad59fcfa0457907cf8e7f7b13ce5829c05bf54397a`。
- 4 条新增策略轨迹依次为完整处理两封邮件、照料宠物和收取树产品，均为 applied。邮件候选 ID 中的 route/open 只表示进入阶段；第一封邮件在同一 lease 内连续完成 `traverse_connector → move_to_tile → interact → wait_ticks → close_menu` 后才入库，不是机械步骤独立训练。队列中 fresh 后不可用的社交尾项被拒绝并释放旧 lease，只记 `selected_queue_candidate_not_fully_verified` skip；没有伪造成功轨迹，`recovery.stabilize_day` 入库数仍为 0。
- 两次主排序耗时 2.412 秒和 43.828 秒；三次控制面边界中的后两次排序为 154.6 毫秒和 22.2 毫秒。没有回到 r33 的数分钟重复候选构建。
- 事务为 `committed_after_native_save_boundary`；canonical 为 accepted 215 / rejected 0、157 / 5 / 53、4920 pairs。checkpoint `structured-policy-bd3f223f2cdda380168406bc` 的 SHA-256 为 `bc5369df5a47bfdf27d9a49b99cc4498b54a4cd4dc27bba1b02de907419c15a4`，manifest SHA-256 为 `24b18a5bf0317e36f36398609b9e65c79a69f42bef73cee35b57191ae56ec653`；validation/test pair accuracy 为 1.0 / 0.96683。
- 原生日志记录 `SaveGame.Save() completed without exceptions.`；unresolved Product pending 为 0，容器 exit 0、OOM false。完整归档为 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r34-round02-20260905-043913`，远端/本机 170 / 170，缺失、额外、SHA-256 不一致均为 0。
- 直接当时后续计划：以 Summer 7 存档和当前 canonical 哈希创建下一份有界计划，继续扩大真实日序列；跨季、跨年、Grandpa 21 和 Companion 产品验收仍未完成。

## 历史记录 · 2026-09-05 当时权威检查点：r34 round01

- r33 round10 已从 Summer 4 推进到 Summer 5，完成 2 个 verified 主决策和 3 个原生存档边界迭代，事务正常提交；它证明 r33 修正后的 canonical 可以连续第三次进入下一轮。随后 `10b7722` 在不删减 full snapshot 字段的前提下，将同一 `SnapshotEnvelope` 的完整 route connector 候选构建结果缓存，并从缓存按调用上限截取。
- 默认自主候选集合现在只有一个权威入口；显式目标候选（尤其 `exploration.visit_location`）、校准候选和玩家指令候选不会被空请求擅自纳入。`recovery.stabilize_day` 仅显式加入控制面边界，未写入训练轨迹。
- 冷启动 Farm full snapshot 默认排序由约 227.9 秒降至 28.266 秒；119 上 `train.server.20260905.r34.plan01` 的室内首次排序为 1.918 秒、室外首次排序为 43.329 秒，随后边界排序为 145.6 毫秒和 9.7 毫秒。透明字段未裁剪，性能收益来自候选派生复用。
- r34 使用 `formal-r34-10b7722-20260905`、并发 1，4 次迭代完成 2 个主决策和 2 个原生存档边界，产生 `mail.process_letter`、`economy.ship_items`、`mail.process_letter`、`farm.care_for_pets` 四条 applied 策略轨迹。Summer 5 → Summer 6；存档 SHA-256 从 `09bf903b36e1642339784f21a7331d309511280cb96abf458f7f7d8dca86c9a2` 变为 `08b6e014081653f6625ad57106624417e0e02ca5f5778d744a393911f7dc6a49`。
- 事务状态为 `committed_after_native_save_boundary`；canonical 为 accepted 211 / rejected 0、train / validation / test = 153 / 5 / 53、4783 pairs。checkpoint / manifest SHA-256 为 `b33d54f66fdcbe304e5207043751a0a153dcd45d24064b299903b978d28bd010` / `103770377bdca5b4979bd196ae4756c93e080f8924bc1de136a9bd35cc07c738`，manifest 的 input、horizon、cleaned 和三分区摘要均已复核。
- 完整制品归档在 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r34-round01-20260905-041036`；远端/本机 189 / 189，缺失、额外、SHA-256 不一致均为 0。正式容器以 exit 0、OOM false 退出，只保留独立观察隧道。
- 直接当时后续计划：从 Summer 6 存档和当前 canonical 哈希创建 r34 round02 有界计划，继续真实 Product rollout。跨季、跨年、Grandpa 21 和 Companion 适配尚未验收，不得称为全量训练完成。

## 历史记录 · 2026-09-02 issue #89 运行证据新鲜度与 CI 修正

- `native_object_execution.v2` 现由 `RuntimeEvidenceCatalogSource` 冻结运行路径修订、32 个源文件的规范化 SHA-256，以及六个 EVD-271/272/274/276/278/279 运行证据对应的 artifact/source/build identity 和三份精确 DLL SHA-256。未知证据、修订变化、源码漂移或 DLL 身份缺失一律按 stale 关闭 Runtime/Output 准入，不能再因 evidence id 非空而继承 `RuntimeVerified`。
- 本轮只强绑定 #88 实际重跑的六个动作：旋转屋内植物、演奏 Singing Stone、收取 Slime Ball、从料斗取草、收取 Auto-Grabber、使用 Mini-Obelisk。其他历史动作暂为 `LegacyUnbound`，保留原行为但不冒充已完成强绑定；后续必须按实际运行产物逐域迁移。
- capability 输出新增 `runtime_evidence_freshness`、`expected_runtime_path_revision`、`runtime_path_source_sha256` 和 `runtime_evidence_bindings`，供训练准入与审计直接追溯。
- GitHub Actions 新增不依赖 Stardew 私有游戏 DLL 的 Core governance profile，覆盖原生对象机械治理和证据新鲜度测试。此次本机结果：governance `16/16`、Core `2252/2252`、Backend `171/171`、Release 构建 `0 warnings / 0 errors`。
- issue #86 的 typed payload v2 经复核属于增量架构迁移，不是本轮阻断 bug；Transfer/Place/Craft/Quest 等对象化扩展仍按原计划推进，不与 #89 修复混写。服务器本轮只更新静态发布物，游戏、Backend、Product Executor 和训练循环均保持停止。

## 历史记录 · 2026-09-01 当时接续点：r29 有序高层队列边界闭环通过

- 训练根已先迁移到 `I:\StardewAITrainingArchive\119.91.139.160\formal-training-r18-pre-queue-boundary-fix-20260901`；远端清单 910 个文件、本地 910 个文件，逐文件 SHA-256 验证 910/910，通过且远端源目录未改动。
- 唯一有效决策边界现为：模型/策略排序器一次选择一条有序高层候选队列；运行时持有该队列及游标，依次在每个候选边界读取 fresh snapshot、重新物化并由 C# 确定性编译。候选内部的寻路、移动、交互、等待、关菜单等 continuation 也只做确定性重编译，不重新调用模型。
- 只有整条队列完成，或当前候选因时间、能量、资源、身份漂移、阻塞或回执不确定而使队列失效时，才允许重新调用策略排序器。队列间先后关系在每个候选边界重新验证；失败候选不得计为完成，也不得越过 continuation 执行下一个候选。
- 119 上 `formal-r29-20260901 / train.server.20260901.r29` 的有界证明为：1 次策略排序选择 4 个高层候选，执行 9 个机械动作；发生 3 次候选边界刷新和 5 次候选内部 continuation 刷新，均未调用策略模型。4 个高层候选各形成 1 条成功策略轨迹，最终 `selected_queue_decision_complete=true`，无跳候选、无阻塞入库。
- r27/r28 是用于暴露错误边界的受控回归探针：分别发现了装箱站位被误纳入高层语义身份、以及 continuation 尚未结束却跨入下一候选的问题；两者均已在 r29 前修正，不得作为成功训练证据。下方 r24/r25 中“每次 fresh replan 形成独立策略轨迹”以及“每个外层 iteration 只能容纳一个高层目标”的解释已被本节取代，只保留为历史诊断记录。
- 当前并非多日全量训练完成。服务器约剩 4.2 GB，继续长训前仍须扩容或迁移训练根；之后先做有界多外层迭代恢复验证，再启动可恢复的多日正式批次，并同时监控策略调用次数、候选边界编译次数、UPS、磁盘和 Product 回执。

## 历史记录 · 2026-09-01 当时接续点：r25 四轮滚动证据通过

- 正式训练请求与 `training_run_manifest.v2` 新增 `max_persisted_iterations`，默认并冻结为 4；launcher、
  Windows/Linux 启动器和服务器 Compose 已贯通。只滚动当前 run 的旧 `live-snapshots` 迭代家族，
  不删除策略轨迹、Product 回执、数据集、checkpoint、manifest 或日志。
- 119 已部署 `formal-r25-20260901`。`train.server.20260901.r25` 的 manifest 实际记录
  `max_attempts=1 / max_persisted_iterations=4 / min_free_space_mb=4800`；邮件五步再次全部
  `applied/verified/fresh`，5 条显式候选轨迹全部入库，来源遗漏为 0。
- 数据集现为 accepted 186 / rejected 0 / train 128 / validation 5 / test 53，首次出现非空 validation；
  checkpoint 为 `structured-policy-52c9f785cc6dcc46c02f94e7`，SHA-256
  `b97bbdc1b64ba77b38097fc691581d4397c32807246f312b00dd883249e23b67`。
- 训练后约 54.124 UPS，full snapshot 12 次、平均 1406.835 ms。服务器只有一块 60GB 系统盘，约
  4999 MiB 可用、使用率 92%。滚动窗口降低未来单 run 增长，但当前仍无第二数据盘；不得开启无界长训。
  下一外部准入是扩容/挂载训练盘，或经审核回收明确可删除的旧服务器资产；在此之前只允许有界批次。

## 历史记录 · 2026-09-01 当时接续点：r24 单目标正式闭环通过，准备多日长训

- 119 服务器已部署 `formal-r24-20260901`。运行 `train.server.20260901.r24` 在同一隔离存档上完成一个
  邮件高层目标 episode，内部原生执行 `traverse_connector -> move_to_tile -> interact -> wait_ticks -> close_menu`；
  五步全部 `applied/verified/fresh`，最后以 `objective_continuation_completed_iteration_boundary` 停止，
  没有在同一外层迭代误跑第二个目标。
- 训练单位已固定：外层 iteration 是模型级目标，不是机械动作数；机械动作完整保存在 execution episode。
  每次 fresh replan 中被明确选中的高层 continuation 候选各形成一条策略轨迹，纯编译器展开的按键/寻路
  原语不伪装成模型决策。r24 补齐三个邮件阶段的 `candidate_id` 后，本轮生成 5 条成功 Product 轨迹，
  `effective_candidate_id_missing=0`。
- 正式数据从 accepted 176 增至 181，rejected 仍为 0；train/validation/test 为 128/0/53，train pairs
  为 3415。当前 checkpoint 是 `structured-policy-35eb3439e19036a75b9c628b`，SHA-256 为
  `11f38de448370bb401278bd8cd32ca4faea1e42e67df3b5299e20394c53ef0f7`。
- 低频观察下实测约 56.346 UPS，full snapshot 12 次、平均 1430.682 ms，未形成持续卡顿。游戏、Backend
  和 Product Executor 仍运行，LiveTrainingLoop 已正常退出，因此现在不是后台长训状态。
- 正式 Product 控制闭环已经成立并产生真实 checkpoint 更新，但“全量训练完成”仍未成立：尚未连续覆盖
  完整游戏日、季节、年份和第三年爷爷 21 分目标，也没有足量 validation 数据。服务器仅约 5072 MiB 可用、
  使用率 92%；当时后续计划先扩容或迁移 `/state/formal-training-r18`，复核全量哈希后按有界、可恢复的多日批次
  启动长训。当前 C# 线性 ranker 不需要 RTX 5070 8GB，GPU 留给后续神经模型阶段。

## 历史记录 · 2026-09-01 当时接续点：EVD-326 正式训练编排与真实结构化更新

- Product Executor 轨迹已从测试 Harness 版本解绑：正式回执写入 `executor=product_executor.v1`，清洗器可识别历史 Harness 校准数据，但 `StructuredPolicyTrainer` 与 checkpoint 只接受 Product 版本。当前权威轨迹 schema 是 `policy_decision_trajectory.v2`，旧文档中的 `.v1` 表述不得继续沿用。
- `training_run_manifest.v2` 冻结结构化 checkpoint、policy dataset manifest 及其 SHA-256，并记录 Product Executor、游戏和 LiveTrainingLoop 三个进程。正式 launch 必须复用同一个已成功 prepare 的 manifest；任一工具缺失、路径越界、hash 漂移、非 SMAPI、非隐藏静音或非 loopback 执行端都会失败关闭。启动任一后续进程失败时，已启动进程会回收，避免半闭环残留。
- `training_ready_probe.v2` 现在验证透明快照/run-id、游戏与两个训练进程、Product `/health`、收据目录、孤立 pending、数据 manifest/checkpoint 及全部 cleaned/train/validation/test 文件 hash。pending 不会重发；探针报告 `formal_product_receipt_recovery_required`，由同请求恢复为 indeterminate final 后重规划。
- 正式 LiveTrainingLoop 不再训练旧 baseline 冒充小模型更新。每个训练周期使用 Product 轨迹重建确定性 80/10/10 数据 manifest，训练结构化 ranker checkpoint，并通过 `FormalTrainingManifestStore` 原子刷新 run manifest hash；下一轮直接读取更新后的 checkpoint。非 `--skip-training` 模式同时强制 Product Executor、真实 feedback、结构化 checkpoint 和 prepared manifest。
- 本轮尚未宣称全量训练开始。E 盘目前只有历史自动化存档和旧 2026-07 feature rows，没有独立新存档的 Product v2 轨迹、正式 policy manifest 或结构化 checkpoint。当时后续计划固定为：在独立新存档上用 `--skip-training` 仅采集最小 Product bootstrap 校准轨迹，离线生成首个真实 checkpoint；随后 prepare/launch 正式闭环并以完整首个游戏日验收。bootstrap 不是正式训练，不能计入启动完成标志。

## 历史记录 · 2026-08-31 当时接续点：EVD-325 Product Executor 之后

- 当时机器可读状态为 `228 registered / 230 semantic / 227 compiler-bound / 145 harness dispatch / 145 Product Executor / 151 five-gate / 62 training allowlist / 2 catalogued blocked`；full snapshot 为 `171 required / 154 readable with provenance / 17 contextual / 0 blocking`，KnowledgeCompiler 为 `585/585`、blocking 0。原生分母保持 `322 surfaces / 448 branches / 150 map tokens` 且三类 blocking 均为 0。
- EVD-325 已建立独立 `StardewAI.ProductExecutor`。正式执行地址为 loopback `/api/v1/product/execute`，只接受能力目录支持的非 debug 动作、精确 run-id、执行模式/actor、GUID nonce、精确隔离存档根和有效时间戳；`LiveTrainingLoop` 在非 `--skip-training` 模式下同时强制 Product Executor 与真实 executor feedback，拒绝 RuntimeTestHarness 或无反馈绕过。
- Product Executor 不复制 145 个动作状态机，只在现有唯一原生执行端点前增加产品授权、串行分发、fresh 前后快照、持久化 pending/final 收据和结果契约校验。请求中的决策快照哈希与实际分发前全量哈希均保留；自然世界漂移不再误杀动作，动作级 fresh 前置条件由游戏线程中的现有原生状态机验证，发生漂移时结果强制要求重规划。
- 分发采用 at-most-once 恢复语义：pending 必须在原生 POST 前原子落盘；相同 nonce/相同请求的 final 可跨请求过期窗口幂等返回；nonce 复用不同载荷立即拒绝；重启后发现孤立 pending 时禁止再次下发，落盘为 `native_dispatch_indeterminate_no_replay` 并要求重规划。
- 最终隐藏静音产品冒烟 `3/3` PASS：`artifacts/product-executor-smoke/product-executor-20260831-235239/summary.json`。真实 `executor.tailor_item` 经产品入口 `applied/verified`，同一持久收据重放未二次执行，debug fixture 被产品授权层拒绝。服务级产品测试 `10/10`，全量回归 Core `2191/2191`、Backend `162/162`，Release 解决方案构建 `0 warnings / 0 errors`。
- 当时后续计划不再补动作注册：先用独立新存档重建正式 `policy_decision_trajectory.v1` 数据清单和结构化策略 checkpoint，完成 Product Executor、run-id、快照、manifest、断点恢复和静音进程编排的启动前探针；全部通过后在服务器启动全量训练。启动成功仍以至少一个真实游戏日的连续准入动作、产品原生 verified 回执、数据集/checkpoint 哈希落盘和恢复探针通过为准，不能以进程存在或 `--skip-training` 冒充。

## 历史记录 · 2026-08-31 训练优先动作闭合快照：EVD-324

- 当前冻结状态为 `228 registered / 230 semantic / 227 compiler-bound / 145 harness dispatch / 151 five-gate / 62 training allowlist / 2 catalogued blocked / 0 Product Executor`；full snapshot 为 `171 required / 154 readable with provenance / 17 contextual / 0 blocking`。EVD-317 至 EVD-324 已闭合，顶部旧检查点不得再被解释为当前当时后续计划。
- `story.advance_event -> advance_story_event -> executor.advance_story_event` 已闭合普通事件链。透明桥发布活动事件全部命令、当前命令、问题键、完整响应数组、原生菜单和下一边界；模型只在新问题边界选择响应，编译器重绑事件实例与投影指纹，运行时仅使用原生 `Event.Update` 和菜单输入并在结束、新问题、小游戏或玩家控制序列边界收口。EVD-322 隐藏静音测试的自动消息和第二响应两例均为 `applied/verified`，禁止 `skipEvent`、直接命令索引或已看事件写入。
- `story.advance_event_minigame -> advance_story_event_minigame -> executor.advance_story_event_minigame` 已闭合。透明桥按实时原生类型发布九类剧情小游戏及其运行状态：五种事件过场和 `BoatJourney` 可执行；`GrandpaStory`/`Intro` 明确属于开档与角色创建的玩家边界；1.6.15 无实例化和完成路径的 `TelescopeScene` 保留废弃兼容占位。执行器不调用 `tick`、`forceQuit` 或 `skipEvent`，仅等待 `Game1.currentMinigame` 原生更新，并在 `FantasyBoardGame` 中转发编译器绑定的原生对话输入。EVD-323 隐藏静音测试的被动消息和第二响应均为 `applied/verified`。
- `social.watch_movie -> watch_movie -> executor.watch_movie` 已闭合。模型只选择当周电影、可选宾客和可选零食；编译器以 fresh 快照滚动展开买票、跨图移动、原生邀请、入场、等待宾客、柜台购买和完整放映。EVD-321 隐藏静音矩阵四阶段全部 `applied/verified`，玩家与规范宾客周标记、精确好感增量和影院大厅清理一致。
- 影院运行验证修复了三个不得回退的真实缺陷：进入影院后不得再要求第三张票；柜台/影院门相邻站位必须由透明桥发布当前玩家 BFS 可达性并被核心和运行时共同重绑；同名影院观众实例不得覆盖 `Game1.getCharacterFromName`/`MovieInvitation.invitedNPC` 的规范 NPC 周标记。
- `tailoring.sew_item -> tailor_item -> executor.tailor_item` 已闭合训练优先动作。透明桥实时读取全部 `Data/TailoringRecipes`、库存输入对、原生 Tailoring/`(BC)247` 入口、确定输出或完整随机域、鞋靴属性转移、裁缝历史与余量容量；染色/棱彩外观分支仍归 `tailoring.dye_item` 玩家指令。运行层只使用原生 `TailoringMenu` 输入和 1500ms 生命周期，并回收全部左右余料，不直接改库存、鞋靴、服装、历史或 RNG。
- RuntimeTestHarness 原生闭环不能冒充正式训练执行器。该历史检查点之后的 Product Executor 装配、授权、快照审计、回执持久化和失败重规划已由 EVD-325 完成；不得再把本条解释为当前待办。
- EVD-324 原生烟测为 `3/3` PASS，证据位于 `artifacts/runtime-tailoring/runtime-tailoring-20260831-225159/summary.json`；确定配方、原生随机配方和鞋靴属性转移均为 `applied/verified`。动作对账为 585/585 导出、blocking 0；最终静态回归为 Core `2191/2191`、Backend `155/155`，全解决方案 Release 构建 `0 warnings / 0 errors`。
- 服务器全量训练的启动退出条件是：独立新存档、SMAPI、静音/隐藏、run-id 与快照绑定、非空正式 allowlist、结构化策略检查点、Product Executor 健康、真实执行回执、轨迹 manifest/checkpoint 持久化和可恢复停机全部通过；禁止使用玩家存档、测试 fixture、`--skip-training` 或 baseline 聚合器冒充全量训练。
- 服务器训练成功启动的完成标志不是“进程存在”，而是至少一个真实游戏日产生连续的 `policy_decision_trajectory.v1`，其中所有被选动作均准入、经 Product Executor 原生执行并得到 fresh verified 回执，数据集与检查点哈希落盘，断点恢复探针通过。启动完成后再补两项后置玩家能力。

## 历史记录 · 2026-08-31 当时权威检查点：EVD-316

- `quest.cancel` 已闭合透明读取、显式玩家命令候选、DailyPlan、fresh 编译重绑定、类型化请求、原生 `QuestLog` 输入和严格回执。唯一动作链为 `quest.cancel -> cancel_quest -> executor.cancel_quest`；严格 `PlayerCommandOnly`，不进入默认候选、自主日计划或策略训练。
- 锁定 1.6.15 原生条件为普通 `Quest` 尚未完成、未隐藏、已接受、未等待删除且 `CanBeCancelled()`。透明桥发布当前 `questLog` 中每一条普通任务及阻断诊断、完整身份指纹、任务数和 `acceptedDailyQuest` 副作用。`SpecialOrder.CanBeCancelled()` 恒为 false，且原生点击分支只处理普通 `Quest`，因此特别订单不会被该动作冒领。
- 原版取消按钮没有二次确认菜单：选中任务后点击 `cancelQuestButton` 会立即执行 `accepted=false`、从 `questLog` 删除，并在同日接受的每日委托上清除 `acceptedDailyQuest`。StardewAI 在原生点击前额外要求精确指纹、非空原因和显式玩家确认；fresh 编译器重绑全部任务字段、计数和每日标志。生产执行器只创建原生 `QuestLog`、点击精确任务行和取消按钮，不直接写任务、列表或每日标志。
- 隐藏静音 E 盘矩阵 `4/4` PASS：`artifacts/runtime-quest-cancellation/runtime-quest-cancellation-20260831-011556/summary.json`。覆盖同日每日委托清标志、普通任务保留标志、伪造指纹拒绝和不可取消任务双层拒绝；金钱、任务完成统计和特别订单数量保持不变。
- 最新 full snapshot 为 `163 required / 146 readable with provenance / 17 contextual / 0 blocking`；对账为 `212 registered / 222 semantic / 211 compiler-bound / 137 harness dispatch / 135 five-gate / 55 training allowlist / 10 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens`；KnowledgeCompiler `585/585`、blocking 0；Core `2133/2133`、Backend `155/155`、Release `0 warnings / 0 errors`。`minigame.play_junimo_kart` 继续按既定边界后置，下一实际纵向切片为 `rewards.claim_adventure_guild_reward`。

## 历史记录 · 2026-08-31 当时权威检查点：EVD-315

- `processing.crack_geode` 已闭合透明读取、自主候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求、共享逐帧移动、原生铁匠对话与 `GeodeMenu` 输入，以及严格领取回执。唯一动作链为 `processing.crack_geode -> crack_geode -> executor.crack_geode`，没有第二套库存、奖励或晶球 RNG 系统。
- 锁定 1.6.15 范围覆盖全部八种基础输入：`(O)275/535/536/537/749/791/MysteryBox/GoldenMysteryBox`。透明预测复刻 `Utility.getTreasureFromGeode` 的计数时序、种子、两段固定次数随机预热、数据驱动掉落、文物保护、Mystery Book 邮件和首次金椰子互斥分支；两个原生共享 `Game1.random` 作物分支只发布完整当季候选族，不伪造确定结果。
- 上游在 Clint 不在铁匠铺、成品工具领取拦截、菜单/事件/控制锁、金钱不足、库存容量不足或端点不可达时排除候选。fresh 编译器重新绑定输入槽位/品质/堆叠、计数、金椰子与金核桃状态、邮件/技能/季节/矿层上下文、输出族、柜台和站位。生产执行器只经 `Blacksmith checkAction -> Process -> GeodeMenu inventory/geodeSpot click -> 2700ms native animation`；不直接写金钱、库存、统计、邮件或团队状态。
- 隐藏静音 E 盘矩阵 `9/9` PASS：`artifacts/runtime-geode-processing/runtime-geode-processing-20260831-003313/summary.json`。八种基础晶球均 `applied/verified`，另含伪造计数拒绝；运行中发现并修正了随机预热循环、产物占原槽后的剩余堆叠归还、首次矿物 `artifactFound` 和金椰子产物转团队金核桃而不留库存四个真实缺口。
- 最新 full snapshot 为 `162 required / 145 readable with provenance / 17 contextual / 0 blocking`；对账为 `210 registered / 221 semantic / 209 compiler-bound / 136 harness dispatch / 133 five-gate / 55 training allowlist / 11 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens`；KnowledgeCompiler `585/585`、blocking 0；Core `2128/2128`、Backend `155/155`、Release `0 warnings / 0 errors`。`minigame.play_junimo_kart` 按既定边界继续后置，下一实际纵向切片为 `quest.cancel`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-314

- `player.customize` 已闭合完整透明读取、显式玩家命令候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求和两条原生执行分支。唯一动作链为 `player.customize -> customize_player -> executor.customize_player`，严格 `PlayerCommandOnly`，不进入默认候选、自主日计划或策略训练。
- 巫师神龛分支绑定 `WizardHouseBasement` 的 `WizardShrine`、500g 前态和完整原生控件域：姓名、最爱之物、性别、皮肤 `0..23`、实时 `Farmer.GetAllHairstyleIndices`、饰品 `-1..29`、眼睛与头发六个 HSV 滑条 `0..100`。运行层只经共享移动、`checkAction`、Yes 对话和 `CharacterCustomization(Source.Wizard)` 控件/OK 输入，不直接写金钱或外观。
- 沙漠改造分支明确读取替换地图 `DesertFestival`，不是普通 `Desert`；绑定当前造型师、每日标志、装备回收所需空位、完整 `Data/MakeoverOutfits` 过滤和原生日存档 RNG 投影。运行层走上 `DesertMakeover` TouchAction、跳过原生事件并等待 `ReceiveMakeOver` 完成回调，不直接写装备、每日标志或调用回调。
- 源边界保持分离：染色仍属 `tailoring.dye_item`；NewGame/HostNewFarm/NewFarmhand 属于建档控制面；锁定基础版调用点中没有可达 Dresser 构造链。隐藏静音 E 盘矩阵 `4/4` PASS：`artifacts/runtime-player-customization/runtime-player-customization-20260830-215722/summary.json`，含巫师全字段、六滑条 100 边界、伪造金钱拒绝与沙漠改造。
- 最新 full snapshot 为 `161 required / 144 readable with provenance / 17 contextual / 0 blocking`；对账为 `208 registered / 220 semantic / 207 compiler-bound / 135 harness dispatch / 131 five-gate / 54 training allowlist / 12 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens`；KnowledgeCompiler `585/585`、blocking 0；Core `2123/2123`、Backend `155/155`、Release `0 warnings / 0 errors`。`minigame.play_junimo_kart` 按既定边界后置，下一实际纵向切片为 `processing.crack_geode`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-313

- `player.choose_jukebox_track` 已闭合透明读取、显式玩家命令候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求、共享逐帧移动、原生 Saloon 点唱机菜单输入和严格回执。唯一动作链为 `player.choose_jukebox_track -> choose_jukebox_track -> executor.choose_jukebox_track`，没有第二套音乐切换或 Mini-Jukebox 状态写入系统。
- 锁定 1.6.15 规则为 Saloon `Buildings[(1,17),(2,17)] Action=Jukebox` 打开 `ChooseFromListMenu`；曲目按 `Utility.GetJukeboxTracks` 的原生顺序生成：先收录 `Data/JukeboxTracks` 中 `Available=true` 的行，再规范化 `Farmer.songsHeard` 的替代曲目 ID，仅保留 soundbank 中实际存在且未被数据明确禁用的曲目。菜单从索引 0 开始，前进按钮循环，OK 调用原生选择动作但菜单不自动关闭，Cancel 才关闭。绿雨期间非 `rain` 曲目由原生 `Game1.changeMusicTrack` 规则禁止。
- 该能力严格为 `PlayerCommandOnly`：玩家必须给出精确曲目、原因和确认；未知、未解锁、绿雨期不合法或指纹漂移的曲目在上游排除，不进入默认候选、自主日计划或策略训练。fresh 编译器从当前曲目目录、Saloon 端点、站位、天气和默认音乐轨重绑定全部机械字段。生产执行器只使用共享移动、原生 `checkAction` 以及菜单的前进/OK/Cancel `receiveLeftClick`，不直接调用 `Game1.changeMusicTrack`，不写播放、解锁或 Mini-Jukebox 状态。
- 隐藏静音 E 盘矩阵 `3/3` PASS：`artifacts/runtime-jukebox-selection/runtime-jukebox-selection-20260830-201036/summary.json`。原生第一首、循环前进到最后一首均 `applied/verified`，伪造曲目索引被严格拒绝；只加载 TransparentBridge 与 RuntimeTestHarness。
- 最新 full snapshot 为 `160 required / 143 readable with provenance / 17 contextual / 0 blocking`；对账为 `206 registered / 219 semantic / 205 compiler-bound / 134 harness dispatch / 129 five-gate / 54 training allowlist / 13 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens` 且已冻结；KnowledgeCompiler `585/585`、blocking 0；Core `2118/2118`、Backend `153/153`、Release `0 warnings / 0 errors`。`minigame.play_junimo_kart` 继续按既定边界后置原生完美代打，下一实际纵向切片为 `player.customize`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-312

- `player.choose_bobber` 已闭合透明读取、显式玩家命令候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求、共享逐帧移动、原生鱼店菜单输入和严格回执。唯一动作链为 `player.choose_bobber -> choose_bobber_style -> executor.choose_bobber_style`，没有第二套钓鱼装备或偏好写入系统。
- 锁定 1.6.15 规则为 FishShop `Buildings[10,4] Action=Bobbers` 打开 `ChooseFromIconsMenu("bobbers")`；固定样式 `0..38` 的解锁条件是 `style_id <= fishCaught.Count()/2`，随机样式为 `-2` 且始终可选。声呐浮标 `(O)789` 只把实际显示样式临时覆盖为 `39`，不改变本动作可选的偏好目录。随机样式只在原生 `FishingRod.getBobberStyle` 使用阶段消耗共享 RNG；透明读取不推进 RNG。
- 该能力严格为 `PlayerCommandOnly`：必须给出精确样式、原因和确认；锁定样式在上游排除，不进入默认候选、自主日计划或策略训练。fresh 编译器从当前鱼获数、偏好、FishShop 端点和站位重绑定全部机械字段。生产执行器只调用原生 `checkAction`、准确非锁定图标的 `receiveLeftClick` 和原生关闭按钮，不直接写 `bobberStyle`、`usingRandomizedBobber` 或 RNG。
- 隐藏静音 E 盘矩阵 `3/3` PASS：`artifacts/runtime-bobber-selection/runtime-bobber-selection-20260830-193216/summary.json`。固定样式 7、随机样式 -2 均 `applied/verified`，鱼获种类数为 2 时固定样式 2 被严格拒绝；只加载 TransparentBridge 与 RuntimeTestHarness。
- 最新 full snapshot 为 `159 required / 142 readable with provenance / 17 contextual / 0 blocking`；对账为 `204 registered / 218 semantic / 203 compiler-bound / 127 five-gate / 54 training allowlist / 14 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens` 且已冻结；KnowledgeCompiler `585/585`、blocking 0；Core `2112/2112`、Backend `152/152`、Release `0 warnings / 0 errors`。`minigame.play_junimo_kart` 继续按既定边界后置原生完美代打，下一实际纵向切片为 `player.choose_jukebox_track`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-311

- `multiplayer.send_chat` 已闭合完整透明读取、显式玩家命令候选、DailyPlan、fresh 编译重绑定、类型化请求、原生 ChatBox 输入和严格发送者本地回执。唯一动作链为 `multiplayer.send_chat -> send_multiplayer_chat -> executor.send_multiplayer_chat`，没有第二套聊天网络协议。
- 该能力严格为 `PlayerCommandOnly`：玩家必须给出作用域、原因、正文和确认；只接受普通全局消息或绑定当前在线玩家的私聊。任意 `/` 命令、控制字符、模糊私聊目标、超出原生 880px 内容宽度和未确认文本均在上游排除，不进入默认候选、自主规划或策略训练。
- fresh 编译器从 `player.multiplayer_chat` 重绑发送者、语言、默认颜色、网络角色、消息数、输入宽度、在线收件人原生顺序与精确 first-match。运行层只经 `ChatBox.activate -> ChatTextBox.RecieveTextInput -> ChatBox.textBoxEnter`，由原版完成脏词过滤、全局 `AllPlayers` 或私聊 exact recipient 的 type-10 传输；不直接调用 `sendChatMessage`/`receiveChatMessage`，也不伪造远端送达回执。
- 隐藏静音 E 盘矩阵 `2/2` PASS：`artifacts/runtime-multiplayer-chat/runtime-multiplayer-chat-20260830-183025/summary.json`。全局 kind 0 与私聊 kind 3 均 `applied/verified`，输入框原生复位、消息上限、语言、颜色、格式化片段和发送者本地回执全部一致。
- 最新 full snapshot 为 `158 required / 141 readable with provenance / 17 contextual / 0 blocking`；对账为 `202 registered / 217 semantic / 201 compiler-bound / 125 five-gate / 54 training allowlist / 15 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens` 且已冻结；KnowledgeCompiler `585/585`、blocking 0；Core `2105/2105`、Backend `151/151`、Release `0 warnings / 0 errors`。`minigame.play_junimo_kart` 继续按既定边界后置原生完美代打，下一实际纵向切片为 `player.choose_bobber`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-310

- `multiplayer.manage_wallet` 已闭合透明读取、显式玩家命令候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求、共享 BFS、原生 ManorHouse LedgerBook 菜单输入和严格回执。唯一动作链为 `multiplayer.manage_wallet -> manage_multiplayer_wallet -> executor.manage_multiplayer_wallet`，没有第二套钱包执行系统。
- 完整命令域为 `schedule_separate`、`cancel_separate`、`schedule_merge`、`cancel_merge`、`transfer`。前四项只允许房主；转账允许独立钱包模式下的实际玩家，收款人严格绑定原生 `Game1.getAllFarmers()` 已认领顺序、响应键和玩家 ID。模式更改只设置今晚待结算状态，次日由原生 `ManorHouse.SeparateWallets/MergeWallets` 完成；独立钱包按原生整数除法分配共享余额并舍弃余数，合并钱包汇总已认领玩家余额。转账通过原生收款人对话和 `DigitEntryMenu` 结算。
- 该能力严格为 `PlayerCommandOnly`：要求原因和显式确认，转账另需独立转账确认；默认候选、自主规划和策略训练均排除。fresh 编译器只保留操作意图，并从当前透明投影重绑模式、参与者、余额、收款人响应键、LedgerBook 站位和投影指纹。
- 隐藏静音 E 盘矩阵 `7/7` PASS：`artifacts/runtime-multiplayer-wallet/runtime-multiplayer-wallet-20260830-154907/summary.json`。5 个即时命令和共享转独立、独立转共享两个原生次日结算全部 `applied/verified`。生产执行器不直接写钱包模式、待结算标记、共享余额、个人余额或赠款统计。
- 最新 full snapshot 为 `157 required / 140 readable with provenance / 17 contextual / 0 blocking`；对账为 `200 registered / 216 semantic / 199 compiler-bound / 123 five-gate / 54 training allowlist / 16 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens` 且已冻结；KnowledgeCompiler `585/585`、blocking 0；Core `2096/2096`、Backend `150/150`、Release `0 warnings / 0 errors`。`minigame.play_junimo_kart` 按既定决定继续复用现有等价执行并后置完美代打，下一实际纵向切片为 `multiplayer.send_chat`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-309

- `mining.activate_calico_statue` 已闭合透明读取、自主候选、DailyPlan、fresh 编译重绑定、类型化请求、共享 BFS、原生 MineShaft 地块交互和严格结果回执。唯一动作链为 `mining.activate_calico_statue -> activate_calico_statue -> executor.activate_calico_statue`；小模型只接受或拒绝当前精确投影的下一效果，移动、交互和结果验收由机械层完成。
- 锁定 1.6.15 规则使用 `Utility.CreateDaySaveRandom(totalActivatedToday + 1)`、队伍平均每日运气和当前效果栈选择下一效果。透明桥发布完整 18 效果目录、当前/预计效果栈、评分、奖励蛋、生命/耐力/速度回执、雕像 `284 -> 285` 地块状态和可达相邻站位；非沙漠节骷髅洞、非房主、已激活、地块漂移或无站位均在上游排除。
- 隐藏静音 E 盘矩阵 `18/18` PASS：`artifacts/runtime-calico-statue/runtime-calico-statue-20260830-144828/summary.json`。全部效果 ID `0..17` 均经原生 `MineShaft.checkAction` 应用并验证；四档蛋奖励 `10/25/50/100`、速度、完全恢复、评分 `+1`、效果字典和单次地块转换均一致。生产执行器不直接写评分、效果、奖励、生命、耐力、Buff、地块或 RNG。
- 最新 full snapshot 为 `156 required / 139 readable with provenance / 17 contextual / 0 blocking`；对账为 `198 registered / 215 semantic / 197 compiler-bound / 121 five-gate / 54 training allowlist / 17 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens` 且已冻结；KnowledgeCompiler `585/585`、blocking 0；Core `2087/2087`、Backend `149/149`、Release `0 warnings / 0 errors`。下一实际纵向切片为 `multiplayer.manage_wallet`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-308

- `minigame.play_slots` 已闭合透明读取、Rarecrow 齐币需求候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求、共享路线、原生 `ClubSlots`/下注按钮输入和随机结算回执。唯一动作链为 `minigame.play_slots -> play_slots -> executor.play_slots`；小模型只决定是否安排一次下注，转轴、RNG、倍率、齐币和统计完全由原版游戏产生。
- 锁定 1.6.15 规则为 10/100 两档下注；`DailyLuck` 与 `LuckLevel` 形成 `1 + DailyLuck*2 + LuckLevel*0.08` 倍概率阈值。透明桥发布完整 11 分支概率/图案/倍率、期望收益、四台实时机器、活动转轴及赌场商店。共享 `Game1.random` 不具备稳定独占前瞻，因此明确采用 live feedback，不伪造确定结果。
- 自动候选严格限定为缺少 `(BC)126`、Deluxe Scarecrow 依赖开放且齐币不足 10000 时，每次只旋转一次并结算后重规划。`BuyQiCoins` 继续由既有 `executor.choose_dialogue_response` 原生分支负责，不复制购币执行器。
- 隐藏静音 E 盘矩阵 `2/2` PASS：`artifacts/runtime-slots-smoke/runtime-slots-smoke-20260830-132407/summary.json`。10 币无奖分支 `50->40`，100 币单七 `x2` 分支 `1000->1100`；两次均原生 `timesPlayedSlots +1`、图案/倍率/币差一致且 Done 后退出。
- 最新 full snapshot 为 `155 required / 139 readable with provenance / 16 contextual / 0 blocking`；对账为 `196 registered / 214 semantic / 195 compiler-bound / 119 five-gate / 53 training allowlist / 18 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens` 且已冻结；KnowledgeCompiler `585/585`、blocking 0；Core `2080/2080`、Backend `148/148`、Release `0 warnings / 0 errors`。下一实际纵向切片为 `mining.activate_calico_statue`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-307

- `minigame.play_prairie_king` 已闭合透明读取、自主候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求、共享路线和定时等价执行。唯一动作链为 `minigame.play_prairie_king -> play_prairie_king -> executor.play_prairie_king`；小模型只学习是否、何时安排，机械层始终执行不可见等价会话。
- 该边界适用于训练和正式 AI 陪玩：AI 不进行原生逐帧草原大王操作。运行层经原生 `Arcade_Prairie` 创建 `AbigailGame`，在 108000 游戏 tick 等价预算期间暂停开始界面，随后只调用原生 `usePowerup(-3)`，由游戏自身增加通关/无伤统计、补发 `Beat_PK`、检查成就并发送全局消息。结果必须记录为 `simulated_equivalent` 和 `not_native_perfect_proxy_play`。
- 隐藏静音 E 盘冒烟 `runtime-prairie-king-smoke-20260830-120617` PASS：原生统计 `0->1`、无伤统计 `0->1`、`Beat_PK=True`，最终闸门为 `complete_prairie_king_without_dying`。执行器未直接写统计、邮件、成就或奖励。
- 最新 full snapshot 为 `154 required / 138 readable with provenance / 16 contextual / 0 blocking`；对账为 `194 registered / 213 semantic / 193 compiler-bound / 117 five-gate / 52 training allowlist / 19 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens` 且已冻结；KnowledgeCompiler `585/585`、blocking 0；Core `2075/2075`、Backend `148/148`、Release `0 warnings / 0 errors`。
- Junimo Kart 的 AI actor 继续复用已有 `timed_equivalent`，覆盖训练、陪玩和专用房主；其原生完美代打只保留为核心能力训练完成后的 `PlayerCommandOnly` 占位，不阻塞主线。下一实际纵向切片为 `minigame.play_slots`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-306

- `minigame.play_darts` 已闭合透明读取、自主候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求、共享路线、原生交互/确认/鼠标输入和限量核桃回执。唯一动作链为 `minigame.play_darts -> play_darts -> executor.play_darts`；小模型只决定是否完成下一轮，瞄准、充能、投掷和结果对话均由机械执行层负责。
- 锁定 1.6.15 规则为非雨、偶数日、20:00 后的海盗夜；`Darts` 限量掉落计数 `0/1/2` 分别给 `20/15/10` 支飞镖，最多发放 3 个核桃。六投精确方案为 `T20,T20,T20,T20,T17,D5`，总分 301。候选在跨地图时直接读取 `IslandSouthEastCave.IsRainingHere()`，不会误用玩家当时地图天气。
- 隐藏静音 E 盘矩阵 `3/3` PASS：`artifacts/runtime-darts-game-smoke/runtime-darts-game-smoke-20260830-110428/summary.json`。三轮均由原生 `DartsGame`、Yes 对话和鼠标按下/释放完成，命中 `60,60,60,60,51,10`，6 投清零并依次得到第 1、2、3 个核桃；生产路径不写分数、投掷数、计时器、RNG、奖励或进度。
- 最新 full snapshot 为 `153 required / 137 readable with provenance / 16 contextual / 0 blocking`；对账为 `192 registered / 212 semantic / 191 compiler-bound / 115 five-gate / 51 training allowlist / 20 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens`，冻结状态为 `frozen`，KnowledgeCompiler `585/585`、blocking 0；Core `2071/2071`、Backend `148/148`、Release `0 warnings / 0 errors`。下一冻结目录切片为 `minigame.play_junimo_kart`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-305

- `minigame.play_crane_game` 已闭合透明读取、玩家命令候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求、共享路线、原生机器/确认对话/小游戏方向输入和原生奖励菜单回执。唯一动作链为 `minigame.play_crane_game -> play_crane_game -> executor.play_crane_game`；该动作严格为 `PlayerCommandOnly`，要求显式确认，不进入默认候选或策略训练。
- 锁定 1.6.15 反编译确认一次会话原生收费 500g、3 次机会、每次 900 tick，只接受先向右再向下的方向输入。机器占用规则、当日电影奖池覆盖、基础奖池、实时奖品/传送带/爪钩/掉落检查/计时与生命均由 `player.crane_game` 发布；奖品使用共享 `Game1.random`，因此不伪造确定性预测，执行器每次只按实时状态选择可达奖品。
- 隐藏静音 E 盘冒烟 `1/1` PASS：`artifacts/runtime-crane-game-smoke/runtime-crane-game-smoke-20260830-095512/summary.json`。原生扣费 `10000->9500`，完成 3 次机会，2 件奖品经原生 `ItemGrabMenu` 转入背包，小游戏和菜单正常退出。方向输入固定在 `UpdateTicking`，生产路径不写 Money、RNG、奖品/爪钩位置、小游戏状态或背包结果。
- 最新 full snapshot 为 `152 required / 136 readable with provenance / 16 contextual / 0 blocking`；对账为 `190 registered / 211 semantic / 189 compiler-bound / 113 five-gate / 50 training allowlist / 21 catalogued blocked / 0 Product Executor`。原生分母仍为 `322 surfaces / 448 branches / 150 map tokens` 且已冻结、blocking 为 0；Core `2066/2066`、Backend `148/148`、Release `0 warnings / 0 errors`。下一冻结目录切片为 `minigame.play_darts`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-304

- `minigame.play_calico_jack` 已闭合透明读取、自主需求候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求、共享路线、原生牌桌/对话/小游戏输入和严格齐币结算回执。唯一动作链为 `minigame.play_calico_jack -> play_calico_jack -> executor.play_calico_jack`；生产执行不直接写牌、RNG、齐币、牌局结果或统计。
- 锁定 1.6.15 反编译确认低注 `ClubCards` 为 100、高注 `BlackJack 1000`，初始牌和后续抽牌共用由 `timesPlayedCalicoJack / DaysPlayed / uniqueID` 建立的确定性随机流；庄家 18/19/20 的对玩家分支、抽牌偏置和结算均已完整复刻到只读投影。Wiki 关于 Luck 不影响牌局的概述不能覆盖源码：基础牌局不消费 Luck，但原生 Qi fruit `999` 分支明确读取 `DailyLuck + LuckLevel`，透明桥保留这一冲突及实时值。
- 自主候选严格限定为玩家缺少赌场 Rarecrow `(BC)126` 且 Deluxe Scarecrow 依赖仍开放时补足 `10000` 齐币。每次只允许一局；正收益种子且余额至少 1000 时选高注，损失/平局种子降到低注，若只剩 100 且投影为损失则上游延后。该动作是 `R2 Consumptive / PolicyAuthorizationRequired`，不是逐局显式确认，因此在证据闭合后进入策略训练 allowlist。
- 隐藏静音 E 盘矩阵 `3/3` PASS：高注 seed 1 站牌庄家爆牌 `+1000`、低注 seed 23 站牌庄家更高 `-100`、低注 seed 4 首次要牌后庄家爆牌 `+100`。证据为 `artifacts/runtime-calico-jack-smoke/runtime-calico-jack-smoke-20260830-085329/summary.json`。
- 最新 full snapshot 为 `151 required / 135 readable with provenance / 16 contextual / 0 blocking`；对账为 `188 registered / 210 semantic / 187 compiler-bound / 111 five-gate / 50 training allowlist / 22 catalogued blocked / 0 Product Executor`。原生分母为 `322 surfaces / 448 branches / 150 map tokens` 且 blocking 为 0；Core `2060/2060`、Backend `148/148`、Release `0 warnings / 0 errors`。冻结目录的下一切片为 `minigame.play_crane_game`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-303

- `island.field_office_survey` 已闭合透明读取、自主候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求、共享 BFS、原生 `FieldOfficeSurvey -> Survey_Yes -> PurpleFlowerSurvey/PurpleStarfishSurvey` 对话输入和严格结算回执。唯一动作链为 `island.field_office_survey -> answer_field_office_survey -> executor.answer_field_office_survey`；生产执行不直接写植物、当日失败锁、核桃标记、debris、邮件或 finale。
- 锁定 1.6.15 反编译确认紫花答案 `22`、紫海星答案 `18`、答错后当天禁答、`IslandFieldOffice.DayUpdate` 次日清锁、两侧各一次 collected-nut、`GoldenWalnutsFound < 130` 时先生成 `(O)73` debris、磁力拾取后核桃数 `+1`，以及 11 件化石和两项调查全完成后的 finale。已有核桃 debris 会在上游要求先拾取，避免路线移动造成投影漂移。
- 隐藏静音 E 盘最终矩阵 `9/9` PASS：两道普通答案、同日连续两题、错误答案日锁、原生 DayUpdate 解锁、两道 `GoldenWalnutsFound=130` 无掉落分支和最后一题触发 finale 全部匹配。运行层同时捕获瞬时原生 debris 生成与共享磁力拾取后的最终 debris/核桃计数。证据为 `artifacts/runtime-field-office-survey-smoke/runtime-field-office-survey-final-20260830/summary.json`。
- 最新 full snapshot 为 `148 required / 132 readable with provenance / 16 contextual / 0 blocking`；对账为 `186 registered / 209 semantic / 185 compiler-bound / 109 five-gate / 49 training allowlist / 23 catalogued blocked / 0 Product Executor`。KnowledgeCompiler `585/585`、blocking `0`；回归为 Core `2054/2054`、Backend `148/148`、Release `0 warnings / 0 errors`。下一目录切片为 `minigame.play_calico_jack`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-302

- `island.field_office_donate` 已闭合透明读取、显式确认候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、类型化请求、共享 BFS、原生 `FieldOfficeDesk -> Safari_Donate -> FieldOfficeMenu` 输入和严格结算回执。唯一动作链为 `island.field_office_donate -> donate_field_office_piece -> executor.donate_field_office_piece`；生产执行不直接写 fossil、奖励、核桃标记、邮件或 finale。
- 锁定 1.6.15 反编译确认 11 个槽位和物品映射，其中 `(O)823` 按槽 0 后槽 2、`(O)826` 按槽 7 后槽 6 的原生顺序解析。透明桥同时公开解锁/教授/互斥锁/菜单、Desk 与 Survey 端点、11 项捐赠状态、四组恢复状态、两道调查状态与答案、finale readiness、GoldenWalnutsFound、未领取奖励和每个可执行捐赠候选。
- 隐藏静音 E 盘最终矩阵 `15/15` PASS：11 个槽位逐一原生捐赠，中心骨架奖励 `(O)73 x6 + (O)69`、蛇骨架 `(O)73 x3 + (O)835`，蝙蝠/青蛙普通核桃奖励，以及 `GoldenWalnutsFound=130` 时 `(O)TentKit` / `(O)926` 替代奖励全部匹配。最终证据为 `artifacts/runtime-field-office-donation-smoke/runtime-field-office-donation-smoke-20260830-063003/summary.json`。
- 全仓库分母复核纠正了此前将 `FieldOfficeSurvey` 错并入捐赠的问题。它现作为独立 `island.field_office_survey` 保持 `catalogued_blocked`；22/18 两个固定答案和调查状态已透明，但候选、编译和原生执行尚未借用捐赠证据。最新 full snapshot 为 `148 required / 132 readable with provenance / 16 contextual / 0 blocking`；对账为 `184 registered / 208 semantic / 183 compiler-bound / 107 five-gate / 48 training allowlist / 24 catalogued blocked / 0 Product Executor`。KnowledgeCompiler `585/585`、blocking `0`；回归为 Core `2049/2049`、Backend `148/148`、Release `0 warnings / 0 errors`。下一切片固定为 `island.field_office_survey`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-301

- `housing.renovate` 已闭合透明读取、显式玩家命令候选、DailyPlan、fresh 编译重绑定、类型化请求、共享碰撞网格站位、Robin 原生对话、`HouseRenovations` 商店和 `RenovateMenu` 世界区域点击。唯一动作链为 `housing.renovate -> executor.renovate_home`；住宅升级、建筑建造/外观、家具摆放和移动继续使用各自既有实现。
- 透明桥实时读取完整 `Data/HomeRenovations`，锁定基础 1.6.15 的 18 个条目、原生可用顺序、`Value`/`Mail` 要求与动作、全部矩形组、婴儿床特殊区域、阻挡、价格、`FirstPurchase_<RoomId>` 和退款资格。数据 payload SHA-256 为 `26bdcd0681a57c1f749d249ad9305ffa1d58c433c86c1a0b954d0052c6d5d40b`；目录数量、顺序、字段或哈希漂移均失败关闭。
- 全部 18 项严格保持 `PlayerCommandOnly`：必须给出精确 `renovation_id`、`selected_index`、原因和确认；破坏动画另需破坏性确认。它们从默认候选、策略训练和 allowlist 排除。LiveTrainingLoop 已补齐 `PlayerCommand` 调用来源传输；编译器从 fresh `locations.collision_grid` 派生或复核可达柜台站位，不再要求模型提供机械站位。
- 隐藏静音 E 盘矩阵 `19/19` PASS：18 个实时原版条目全部经原生 Robin/商店/装修菜单执行，并额外覆盖负价项目在没有首次购买标记时不退款。跨地图续跑会锁定同一装修 ID/区域，保留玩家指令来源与两级确认，到达服务地图后才重建终端动作。full snapshot 为 `146 required / 130 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `182 registered / 206 semantic / 181 compiler-bound / 105 five-gate / 48 training allowlist / 24 catalogued blocked / 0 Product Executor`。KnowledgeCompiler `585/585`、blocking `0`；完整回归为 Core `2045/2045`、Backend `148/148`、Release `0 warnings / 0 errors`；下一语义切片固定为 `island.field_office_donate`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-300

- `foraging.rummage_garbage` 已闭合透明读取、上游候选排除、DailyPlan、fresh 编译重绑定、类型化请求、共享 BFS、原生垃圾桶交互和输出/任务反馈。唯一动作链为 `rummage_garbage -> executor.rummage_garbage`；移动、物品接收、debris 拾取和普通/特别收集任务继续复用既有实现。
- 锁定 1.6.15 `performAction Garbage -> CheckGarbage -> TryGetGarbageItem` 与实时 `Data/GarbageCans`。透明桥在一次 Buildings 扫描中发布 Garbage action、桶 ID、当日已查集合、`trashCansChecked`、DailyLuck、Alleyway Buffet、确定性无副作用预测、选中数据行、精确物品状态/品质/数量/交付方式、目击 NPC 及反应、空工具栏槽、数据哈希与投影指纹。原版日随机只在预测副本上计算，不推进游戏 RNG。
- 当日已查、未知/漂移数据、无安全槽、不可达及负友谊目击在上游直接排除；无物品是有效原生结果，Linus 的精确正友谊分支允许执行。运行层只调用一次 `GameLocation.checkAction`，核对 CheckedGarbage、统计 `+1`、背包加 debris 的精确单位状态守恒、可选 Linus 好感及槽位恢复，不直接写结果状态。
- 隐藏静音 E 盘矩阵 `9/9` PASS：普通输出、无输出、垃圾帽直接入包、沙漠节多 debris、当日已查排除、负面目击排除、Linus 正向目击、普通收集任务和特别订单。full snapshot 为 `146 required / 130 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `180 registered / 205 semantic / 179 compiler-bound / 103 five-gate / 48 training allowlist / 25 catalogued blocked / 0 Product Executor`。完整回归为 Core `2039/2039`、Backend `145/145`、Release `0 warnings / 0 errors`；下一语义切片固定为 `housing.renovate`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-299

- `foraging.harvest_tree_product` 已闭合普通原版 `Tree` 的透明读取、上游排除、DailyPlan、fresh 编译重绑定、类型化请求、共享 terrain BFS、原生摇树与完整输出域回执。唯一动作链为 `harvest_tree_product -> executor.harvest_tree_product`；果树、砍树、清障和苔藓采集继续使用各自既有语义，不新增第二套移动或拾取系统。
- 锁定 1.6.15 `Tree.performUseAction -> shake` 与 `Data/WildTrees` 确认：成熟、非树桩、未 tapped、有种子、满足原生采集等级门且 `maxShake==0` 时才形成候选。默认种子、秋季枫树 `(O)408` 榛子替换、岛屿棕榈 `(O)88` 椰子及可选 `(O)791` 金色椰子均按实时地点和数据投影；神秘箱、金色动物饼干、原生稀有物品族、技能书和齐豆只发布完整有界输出域，不预读 RNG。锁定数据的榛子日期为秋 14–28 日；Wiki 页面存在 14/15 日起始描述差异，运行合同以数据与反编译为准。
- 运行层到站后冻结背包与 debris 的联合输出基线，临时切到经 fresh snapshot 验证的空工具栏槽，只调用一次 `GameLocation.checkAction`，并在 `finally` 恢复槽位；不直接调用 `Tree.shake`，不写树、RNG、debris、库存或技能。隐藏静音 E 盘矩阵全部 PASS：普通种子、秋季榛子、岛屿棕榈、无种子排除、摇动中排除、tapped 排除；成功分支验证确定输出、可选输出域、种子消耗、当日摇动标记、零 Foraging XP 与槽位恢复。
- full snapshot schema 仍为 `145 required / 129 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `178 registered / 204 semantic / 177 compiler-bound / 102 five-gate / 47 training allowlist / 26 catalogued blocked / 0 Product Executor`。完整回归为 Core `2035/2035`、Backend `144/144`，全解构建 `0 warnings / 0 errors`；下一语义切片固定为 `foraging.rummage_garbage`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-298

- `foraging.harvest_fruit_tree` 已闭合当前地点果树的透明读取、上游候选排除、DailyPlan、fresh 编译重绑定、类型化请求、共享 BFS 与原生摇树回执；唯一动作链为 `harvest_fruit_tree -> executor.harvest_fruit_tree`，没有新增第二套路由或移动系统。
- 锁定 1.6.15 `FruitTree.performUseAction -> shake` 确认：成熟且非树桩、`maxShake==0`、实时 `fruit` 非空才可收获；普通分支保留每个 live Item 的标识、品质和数量，雷击分支按果槽替换为零品质 `(O)382` 煤炭；原生收获清空果实且不给 Foraging XP。季节只影响后续产出，不阻挡树上已有果实的摇取。官方 Wiki 仅二次确认每日一果、最多积累三果、树龄品质与雷击煤炭。
- 运行层只调用一次 `GameLocation.checkAction`，不直接调用 `FruitTree.shake`，也不写果实、debris、库存或技能。隐藏静音 E 盘矩阵全部 PASS：单果普通、三果金星、三份雷击煤炭、空树上游排除、摇动中上游排除；每个成功分支均核对完整 `qualified_item_id + quality + quantity` 守恒和零经验变化。
- full snapshot schema 仍为 `145 required / 129 readable with provenance / 16 contextual / 0 blocking`，因为新增内容位于既有 `current_location.terrain_features` 行内；权威对账为 `176 registered / 203 semantic / 175 compiler-bound / 101 five-gate / 46 training allowlist / 27 catalogued blocked / 0 Product Executor`。完整回归为 Core `2031/2031`、Backend `143/143`；下一语义切片固定为 `foraging.harvest_tree_product`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-297

- `fishing.manage_fish_pond` 已闭合透明读取、显式玩家命令候选、DailyPlan、fresh 编译重绑定、类型化请求和原生 `PondQueryMenu` 回执；复用既有鱼塘 BFS/站位基础设施，不替代 `fishing.service_fish_ponds` 的产出收取与请求交付链。
- 该动作只接受玩家明确给出的鱼塘坐标、`cycle_netting|empty_pond` 和原因；清塘还需操作级 `confirm_empty_pond=true`。它是 `PlayerCommandOnly`，不进入默认候选、策略训练或 allowlist，避免模型因装饰偏好或破坏性重置自主操作鱼塘。
- 锁定 1.6.15 反编译确认：换网只执行 `(nettingStyle+1)%4`；`ClearPond` 按当前鱼数生成同种鱼 debris，并重置鱼种/数量/请求项/天数/闸门/金色饼干/生成标记/水色，同时保留容量、请求完成标记、标牌、产出引用和网样式。官方 Wiki 仅作清塘语义与金色饼干丢失的二次核验。
- 隐藏静音 E 盘隔离运行 `runtime-fish-pond-management-20260830-013602` PASS：换网 `2->3` 且经济状态不变；清塘 `fish_count 1->0`，精确鱼 debris、重置字段、保留字段和工具栏恢复均通过。入口使用一次性作用域右键边沿进入 `GameLocation.checkAction -> FishPond.doAction`，不构造菜单、不调用 `ClearPond`、不直接写鱼塘状态。
- 最新 full snapshot schema 为 `145 required / 129 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `174 registered / 202 semantic / 173 compiler-bound / 100 five-gate / 45 training allowlist / 28 catalogued blocked / 0 Product Executor`。完整回归为 Core `2027/2027`、Backend `142/142`；下一语义切片固定为 `foraging.harvest_fruit_tree`。

## 历史记录 · 2026-08-30 当时权威检查点：EVD-296

- `festival.spin_wheel` 已闭合秋季 16 日展览会转盘的透明读取、Stardrop 有界需求候选、DailyPlan、fresh 编译重绑定、类型化请求和完整原生随机回执。自动候选仅在未获得 Fair Stardrop、扣除尚未领取的陈列奖励后仍缺至少 `2` 星币时出现；恰好缺 `1` 星币继续交给免费力量小游戏。
- 锁定 1.6.15 反编译确认 Buildings `308/309`、`wheelBet -> Green -> NumberSelectionMenu -> WheelSpinGame`、1000ms 预启动、固定减速度、`LuckLevel` 重试门和原生 `+/- wager` 结算。零幸运 30 个等可能构造结果中绿方胜 `22`、橙方胜 `8`，因此下注使用零幸运 Kelly 比例 `7/15`，再按剩余需求封顶；Wiki 的 73.3% 与 46.7% 只作玩家可见规则二次核验。
- 执行器复用共享 BFS，点击真实 `Green` 响应，在原生数字菜单输入精确下注，然后等待真实转盘、结果文字、菜单退出和人物控制恢复。它不写 RNG、旋转速度、得分、下注结算或菜单结果；原生胜负都属于有效训练反馈。
- 隐藏静音 E 盘隔离运行 `runtime-fair-wheel-spin-20260830-005054` PASS：两次真实下注均为 `466`，分别覆盖 `1000->1466` 的胜局和 `1000->534` 的负局，原生结果与清理均通过。
- 最新 full snapshot schema 为 `145 required / 129 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `173 registered / 202 semantic / 172 compiler-bound / 99 five-gate / 45 training allowlist / 29 catalogued blocked / 0 Product Executor`。完整回归为 Core `2023/2023`、Backend `138/138`、Release `0 warnings / 0 errors`。下一语义切片固定为 `fishing.manage_fish_pond`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-295

- `festival.play_strength_game` 已闭合秋季 16 日展览会力量小游戏的透明读取、精确一枚星币的 Stardrop 缺口候选、DailyPlan、fresh 编译重绑定、类型化请求和完整原生回执。该动作免费但固定只奖励 `1` 星币，因此自动候选仅在扣除尚未领取的陈列奖励后恰好还缺 `1` 星币时出现。
- 透明桥发布 Buildings 图块 `540`、站立 X=`29`、实时 `StrengthGame` 私有状态、力量 `0..100`、变化速度 `3/4`、摆锤方向、点击/结果/透明度/计时器、玩家动画状态、完整 Fair 商店行和共享星币缺口。固定合同来自锁定 1.6.15 反编译，活动状态逐帧读取。
- 执行器复用共享 BFS，到位并等待原生移动输入完全结算后，经 `Event.checkAction -> StrengthGame.receiveLeftClick -> FarmerSprite.animateOnce(168,80ms,8) -> StrengthGame.afterSwingAnimation` 进入和完成原版流程。它预测点击后 `9` 次原生力量更新，只点击一次，不写 power、festivalScore、计时器、菜单或玩家位置。
- 隐藏静音 E 盘隔离运行 `runtime-fair-strength-game-20260829-234510` 与 `runtime-fair-strength-game-20260829-234614` 均 PASS，分别覆盖 changeSpeed `4` 与 `3`：点击力量 `64->100`、`72->99`，星币均为 `1999->2000`，原版结果对话和清理完成。
- 最新 full snapshot schema 为 `143 required / 127 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `171 registered / 201 semantic / 170 compiler-bound / 97 five-gate / 44 training allowlist / 30 catalogued blocked / 0 Product Executor`。完整回归为 Core `2018/2018`、Backend `138/138`、Release `0 warnings / 0 errors`。下一语义切片固定为 `festival.spin_wheel`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-294

- `festival.play_slingshot_game` 已闭合秋季 16 日展览会靶场的透明读取、Stardrop 有界需求候选、DailyPlan、fresh 编译重绑定、类型化请求和完整原生 TargetGame 回执。模型只决定是否花费 50g 开始一轮；自动候选仅在“当前星币 + 尚未领取的展览陈列奖励”仍不足 2000 星币且 Stardrop 未获得时出现。
- 透明桥实时发布 Buildings 501/502、50g 入场费、1000/50000/1000/16100ms 四段时序、79 个锁定目标、实时目标私有状态、临时 `(W)32` 与 `(O)390 x999`、命中/准确率/倍率/奖励公式、全部 Fair 商店行和共享星币缺口。固定规则来自锁定 1.6.15 反编译；活动实例与目标状态均实时核对。
- 执行器复用共享 BFS 和普通矿井弹弓唯一的 `SlingshotAimPatch`，经 `Event.checkAction -> DialogueBox.receiveLeftClick -> TargetGame` 进入原版小游戏，只调用原生按下/蓄力/释放输入。它不写 Money、计时器、目标、score、accuracy、festivalScore 或库存；原版拥有弹药、碰撞、命中、得分、奖励和清理。
- 隐藏静音 E 盘隔离运行 `runtime-fair-slingshot-game-20260829-225533` PASS：48 次原生发射、48 次有效命中，raw score `95`、accuracy `102`、final score `380`，原版高分封顶奖励 `500` 星币；50g 扣款、节庆返回、临时弹弓/弹药清理均通过。
- 最新 full snapshot schema 为 `142 required / 126 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `169 registered / 200 semantic / 168 compiler-bound / 95 five-gate / 43 training allowlist / 31 catalogued blocked / 0 Product Executor`。完整回归为 Core `2013/2013`、Backend `138/138`、Release `0 warnings / 0 errors`。下一语义切片固定为 `festival.play_strength_game`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-293

- `festival.play_fishing_game` 已闭合秋季 16 日展览会钓鱼小游戏的透明读取、上游需求候选、DailyPlan、fresh 编译重绑定、类型化请求和原生执行回执。自动候选只在展览会进行中、玩家有 50g，且“当前星币 + 尚未领取的展览陈列奖励”仍不足以购买未获得的 `(O)434` Stardrop 时出现；其他商店行保持透明，但不会形成无限刷小游戏的自动需求。
- 透明桥实时发布 Buildings 503/504 交互图块、50g 入场费、100000ms 游戏时长、11100ms 结果时长、临时 Bamboo Pole / `(O)690` bait / `(O)687` tackle、原版得分与星币公式、当时小游戏私有计时器、全部星币商店行、Stardrop 价格与投影缺口。字段来自锁定 1.6.15 反编译和实时对象，不按 Wiki 示例推断。
- 执行器复用共享 BFS/连续移动与普通钓鱼的 BobberBar 控制器，只经 `Event.checkAction -> DialogueBox.receiveLeftClick -> Event.answerDialogue -> FishingGame` 进入原版小游戏；输入在 `UpdateTicking` 物理更新前发出，不直接写 Money、score、perfections、starTokensWon 或 festivalScore。随机完美率作为训练反馈，执行可靠性由合法输入、完整时序、精确原版公式、星币到账、节日返回和临时钓具清理共同验证。
- 隐藏静音 E 盘隔离运行 `runtime-fair-fishing-game-20260829-221047` PASS：原版 100 秒会话钓到 `5` 条有效鱼且 `5/5` 完美，最终 `364` 分、`432` 星币；50g、原版三连完美翻倍/奖励公式、返回图块和临时状态全部通过。首轮烟测同时抓到并修正了错误的普通对话调用，最终入口使用真实节日菜单点击。
- 最新 full snapshot schema 为 `141 required / 125 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `167 registered / 199 semantic / 166 compiler-bound / 93 five-gate / 42 training allowlist / 32 catalogued blocked / 0 Product Executor`。完整回归为 Core `2008/2008`、Backend `138/138`、Release `0 warnings / 0 errors`。下一语义切片固定为 `festival.play_slingshot_game`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-292

- `festival.manage_grange_display` 已闭合秋季 16 日星露谷展览会陈列的透明读取、确定性最优九件选择、上游候选、DailyPlan、fresh 编译重绑定和原生执行回执。高层动作负责“为一等奖准备陈列/评审后取回”，进入策略训练；`executor.manage_grange_display` 每次只执行一件物品的放入或取回，严格为 `ExecutorCalibration`。
- 透明桥实时读取共享 `FarmerTeam.grangeDisplay`、玩家库存、原生实际售价 `sellToStorePrice(-1L)`、品质、八类多样性、九件数量分、Mayor 短裤惩罚、评审状态、展台交互图块和 `grangeMutex`。优化器按原版公式计算当前分与可达最优分，不依赖文档示例或静态物价。
- 生产执行器复用共享 BFS/移动状态机，到达相邻格后只调用节日 `Event.checkAction` 打开原生 `StorageContainer`，通过菜单点击完成一次变更并等待共享互斥锁释放；禁止直接写展台、库存、评分或评审状态，异常列表形状也只会 fail-closed。动作不会替玩家启动评审。
- 隐藏静音 E 盘隔离运行 `runtime-grange-display-20260829-203602` 为 `10/10` PASS：连续九次原生放入把陈列分数提升到 `124`，超过一等奖阈值 `90`，模拟评审后再原生取回一件；每次均验证菜单入口、互斥锁获取/释放、单次展台变更、库存守恒和评分状态。
- 最新 full snapshot schema 为 `140 required / 124 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `165 registered / 198 semantic / 164 compiler-bound / 91 five-gate / 41 training allowlist / 33 catalogued blocked / 0 Product Executor`。完整回归为 Core `2003/2003`、Backend `138/138`、Release `0 warnings / 0 errors`。下一语义切片固定为 `festival.play_fishing_game`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-291

- `executor.use_warp_totem` 已闭合五种原版传送图腾 `(O)688/(O)689/(O)690/(O)261/(O)886` 的透明读取、fresh 路由重绑定、动作编译、类型化请求、共享原生库存物品使用和延迟回执。锁定 1.6.15 反编译、`Data/Objects`、`Data/CraftingRecipes` 与官方 Wiki，确认 Farm/Mountain/Beach/Desert/Island 全部可达变体及其消耗语义。
- 透明桥逐库存行发布精确物品身份、公共使用门、2000ms 动画和 1000ms 回调合同，并实时解析 Farm `WarpTotemEntry`/农场类型回退、固定目的地、地图宽度修正、主动节日入口和按顺序应用的被动节日地图替换。节日前误消耗但不传送、联机节日 ReadyCheck、已在精确目的地和基础使用门失败均在上游排除。
- 执行层只调用 `UseInventoryObjectNative`，不直接调用 `Game1.warpFarmer`，不写位置、库存、可见性、无敌或音频。回执要求一个精确图腾 `2->1`、原生首段至少 68 个效果精灵、目的地图/格、节日路由以及角色显示/无敌/移动状态全部结算。
- 隐藏静音 E 盘隔离运行 `runtime-warp-totem-20260829-181523` 五变体 `5/5` PASS：Farm `48,39`、Mountain `31,20`、Beach `20,4`、Desert `35,43`、IslandSouth `11,11` 均由原生回调抵达，每例 `68` 个即时精灵、约 `281 tick` 后完成并恢复控制。
- 最终回归为 Core `1998/1998`、Backend `138/138`、Release 全解决方案 `0 warnings / 0 errors`。最新 full snapshot schema 为 `139 required / 123 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `163 registered / 197 semantic / 162 compiler-bound / 89 five-gate / 40 training allowlist / 34 catalogued blocked / 0 Product Executor`。下一语义切片固定为 `festival.manage_grange_display`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-290

- `executor.use_treasure_totem` 已闭合原版宝藏图腾 `(O)TreasureTotem` 的透明读取、fresh 候选环重绑定、动作编译、类型化请求、共享原生库存物品使用和严格回执。锁定 1.6.15 反编译、`Data/Objects`、`Data/CraftingRecipes` 与官方 Wiki，确认原生效果是中心周围按四舍五入距离 3 生成至多 16 个 `(O)590` 宝藏点，并递增 `TreasureTotemsUsed`。
- 透明桥逐格发布全部 16 个候选、放置/占用/前景层/灌木/可挖或冬季草地门、最终可生成集合、使用前计数和指纹。原生代码中形似 Forest 的判断实际比较物品名 `Treasure Totem`，对基础物品恒为 false；合同保留该真实操作数与结果，不按示例修正源码语义。
- 上游在公共物品门失败、室内或可生成集合为空时直接排除，策略只决定何时及在哪里使用。机械执行器只调用 `UseInventoryObjectNative`，不直接写地图、库存或世界计数；新宝藏点继续交给既有 `ArtifactSpots` 透明读取和 `executor.clear_obstacle` 挖掘链，不建立第二套采集实现。
- 隐藏静音 E 盘隔离运行 `runtime-treasure-totem-20260829-173258` PASS：Farm 中心 `(6,12)` 原生生成 `16/16` 个宝藏点，槽 9 图腾 `2->1`，`TreasureTotemsUsed 0->1`，地点宝藏点 `5->21`。
- 最终回归为 Core `1976/1976`、Backend `138/138`、Release 全解决方案 `0 warnings / 0 errors`。最新 full snapshot schema 为 `138 required / 122 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `162 registered / 197 semantic / 161 compiler-bound / 88 five-gate / 40 training allowlist / 35 catalogued blocked / 0 Product Executor`。下一语义切片固定为 `executor.use_warp_totem`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-289

- `executor.use_return_scepter` 已闭合原版回城魔杖 `(T)ReturnScepter` 的透明读取、fresh 住宅/落点重绑定、动作编译、类型化请求、原生即时工具调用和严格异步回执。锁定 1.6.15 `Data/Tools` 与 `Wand.cs` 确认其为不可丢失、不可出售、非消耗型 `Wand`，入口必须经过 `Farmer.BeginUsingTool -> Tool.beginUsing(InstantUse) -> Game1.toolAnimationDone -> Wand.DoFunction`。
- 透明桥实时调用 `Utility.getHomeOfFarmer(player).getFrontDoorSpot()`，分别发布房主 `FarmHouse` 与农场工 `Cabin` 的精确门前格，不把主屋常量错误外推给联机角色。住宅缺失、浴衣、桥上、执行器瞬态门禁以及已在精确落点时均在上游排除；全部原生物品槽保持可读且栈前后均为 1。
- 原生时序为 12 个随机烟雾精灵加 17 个横向轨迹精灵、`wand` 音效、1000ms 回调和 2000ms `freezePause`。源码顺序表明 `Wand` 暂时写 `CanMove=false` 后，`Tool.beginUsing` 在返回前将其恢复为 true，实际输入冻结由 `freezePause` 保持；透明合同和运行断言按完整调用栈而非孤立方法体记录。
- 运行层只选择精确 `Wand` 并调用 `BeginUsingTool`，不直接调用 `warpFarmer`，也不写位置、显示、无敌、移动或库存。隐藏静音 E 盘隔离运行 `runtime-return-scepter-20260829-162520` PASS：从 Farm `(66,19)` 原生抵达主屋门前 `(64,15)`，同步观察 29 个精灵，125 tick 后状态结算，原槽 `(T)ReturnScepter` 仍为 stack 1。
- 官方 Wiki 二次确认回城魔杖不消耗、房主回主屋门前、农场工回自己的小屋门前；本地反编译仍是字段、门禁和时序的主证据。
- 最终回归为 Core `1964/1964`、Backend `138/138`、Release 全解决方案 `0 warnings / 0 errors`。最新 full snapshot schema 为 `137 required / 121 readable with provenance / 16 contextual / 0 blocking`；权威对账为 `161 registered / 197 semantic / 160 compiler-bound / 87 five-gate / 40 training allowlist / 36 catalogued blocked / 0 Product Executor`。下一语义切片固定为 `executor.use_treasure_totem`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-288

- `executor.use_rain_totem` 已闭合原版雨水图腾 `(O)681` 的透明读取、fresh 参数重绑定、动作编译、类型化请求、共享原生库存物品使用和严格异步回执。锁定 1.6.15 反编译确认 `Object.performUseAction -> rainTotem` 的上下文许可、`RainTotemAffectsContext` 路由、默认上下文节日门、天气写入、2000ms 动画与提示对话完整分支。
- 透明桥分别发布“分支判断目标上下文”和“实际天气状态归属上下文”，因此支持模组上下文重定向而不会错记写入对象。默认/沙漠重定向/姜岛分支分别绑定 Default/Default/Island；运行层只调用共享 `UseInventoryObjectNative`，不直接写天气、库存、精灵或音频。
- 官方 Wiki 的季节首日提示触发了源码复核：默认上下文即时写入 Rain 后，换日仍会经过 `Game1.getWeatherModificationsForDate`。透明字段和执行门现绑定明日日期、最终有效天气及有效性；季节首日、开局固定天气、绿雨、夏季固定风暴、主动/被动节庆等覆盖会在消耗前排除。已是 Rain 也在上游排除。
- 隐藏静音 E 盘隔离运行 `runtime-rain-totem-20260829-153640` 为 4/4 PASS：默认、沙漠重定向和姜岛均原生消耗 `2->1` 并写入正确天气实例；节日前分支保持 `stack=2` 并拒绝执行。原生 2000ms 信息对话由输入覆盖关闭，随后通过精确 `Farmer.canMoveNow` 回调恢复控制。
- 最终回归为 Core `1949/1949`、Backend `138/138`、Release 全解决方案 `0 warnings / 0 errors`。
- 最新 full snapshot schema 为 `136 required / 120 readable with provenance / 16 contextual / 0 blocking`。权威对账为 `160 registered / 197 semantic / 159 compiler-bound / 86 five-gate / 40 training allowlist / 37 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `executor.use_return_scepter`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-287

- `executor.use_monster_musk` 已闭合原版怪兽香水 `(O)879` 的透明读取、fresh 参数重绑定、动作编译、类型化请求、共享原生库存物品使用和严格异步回执。锁定 1.6.15 反编译确认原生入口在 750ms 动画回调中调用 `Object.MonsterMusk -> Farmer.applyBuff("24")`，`BuffManager.Apply` 对同 ID 先移除再替换，不叠加旧时长。
- 透明桥实时发布全部精确库存行、公共物品使用门、Buff 24 数据定义与当前实例剩余时长、1750ms 冻结/750ms 回调/1400ms 后续动画合同，以及普通矿井与火山地牢的双倍怪物生成消费者。读取不推进三枚紫色精灵的随机 X 速度。
- 运行层只调用共享 `UseInventoryObjectNative`，不直接施加 Buff、不广播精灵、不播放音频、不写库存。回执等待 Buff 新实例、600000ms 总时长、精确一次消耗、方向 2 和完整原生冻结结算；已有 Buff 的快照到执行时间只允许最多 5000ms 的单调递减漂移。
- 隐藏静音 E 盘隔离运行 `runtime-monster-musk-20260829-141932` 双分支 PASS：首次施加库存 `2->1`，刷新分支 `1->0`；两次均观测到新 Buff 24 实例，刷新把约 `599968ms` 恢复到约 `599984ms`。
- 最终回归为 Core `1932/1932`、Backend `138/138`、Release 全解决方案 `0 warnings / 0 errors`。
- 最新 full snapshot schema 为 `135 required / 119 readable with provenance / 16 contextual / 0 blocking`。权威对账为 `159 registered / 197 semantic / 158 compiler-bound / 85 five-gate / 40 training allowlist / 38 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `executor.use_rain_totem`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-286

- `executor.use_horse_flute` 已闭合原版马笛 `(O)911` 的透明读取、fresh 参数重绑定、动作编译、类型化请求、原生 `Object.performUseAction` 与严格延迟回执。锁定 1.6.15 反编译确认起始与 1500ms 回调各检查一次 `Utility.GetHorseWarpRestrictionsForFarmer`，远程分支再通过 team event、马匹 mutex 和 `Game1.warpCharacter` 完成原生召回。
- 透明桥发布完整库存身份/临时不可见状态、四位限制掩码与错误优先级、召回矩形、全地图拥有马匹的 GUID/位置/骑手/mutex 状态、邻近分支、朝向及延迟合同。指纹绑定玩家位置/朝向、限制、马匹身份与库存可见性；`Utility.findHorseForPlayer` 原生遍历全部已加载/生成地点，不依赖模型猜测马厩位置。
- 运行层只调用 `performUseAction` 和原生调用者的 `reduceActiveItemByOne`；不直接调用 team warp event、不写马匹位置。远程召唤强制朝下并等待精确拥有马匹到达玩家格；已有马在一格邻域内是成功无传送分支并保留原朝向。两条分支都验证可复用马笛堆叠不变。
- 隐藏静音 E 盘隔离运行 `runtime-horse-flute-20260829-120232` 双分支 PASS：透明投影、执行重绑和原生 team event 选中同一马厩马匹；该 GUID 从 `Farm 56,14` 原生召回 `40,7`，随后邻近使用保持 `40,7`，两次库存均为 `stack=1`。
- 最终回归为 Core `1921/1921`、Backend `138/138`、Release 全解决方案 `0 warnings / 0 errors`。
- 最新 full snapshot schema 为 `134 required / 118 readable with provenance / 16 contextual / 0 blocking`。权威对账为 `158 registered / 197 semantic / 157 compiler-bound / 84 five-gate / 40 training allowlist / 39 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `executor.use_monster_musk`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-285

- `executor.use_firework` 已闭合三种原版烟花 `(O)893/(O)894/(O)895` 的透明读取、fresh 参数重绑定、动作编译、类型化请求、共享相邻移动、原生放置和严格瞬时回执。锁定 1.6.15 分支确认三者映射 `fireworkType=0/1/2` 与源图 X=`256/272/288`，共用 2400ms 引信和延迟火箭。
- 透明桥只在 full profile 且库存存在烟花时计算当前加载地图合法区间，并发布精确临时精灵占位格。读取阶段不调用 `Game1.random`；只发布火箭 ID `20..30` 与 Y 加速度 `-0.36..-0.27`、步长 `0.01` 的完整结果域。
- 动作严格为 `PlayerCommandOnly`：不进入自主候选或训练 allowlist。运行层只复用 `CanPlaceInventoryObjectNative -> PlaceInventoryObjectNative`，不直接广播精灵、播放音频或修改库存；执行回执要求 5 个新原生精灵、目标格 2 个主体精灵、精确三色身份、随机结果域和单物品消耗全部成立。
- 隐藏静音 E 盘隔离运行 `runtime-firework-20260829-111607` 三分支 PASS；观测火箭 ID 为 `22/20/25`，加速度为 `-0.27/-0.34/-0.31`，均在反编译域内。
- 最新 full snapshot schema 为 `133 required / 117 readable with provenance / 16 contextual / 0 blocking`。权威对账为 `157 registered / 197 semantic / 156 compiler-bound / 83 five-gate / 40 training allowlist / 40 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `executor.use_horse_flute`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-284

- `executor.read_secret_note` 已闭合普通秘密纸条 `(O)79` 与日记残页 `(O)842` 的完整透明读取、fresh 参数重绑定、动作编译、类型化请求、共享原生物品使用和严格回执。锁定 1.6.15 反编译确认普通纸条用 `Utility.CreateRandom(gameId, playerId, unseenCount * 777)` 从原生未读顺序确定性抽取，日记残页取最小未读编号。
- 透明桥发布完整 `Data/SecretNotes` 目录、原文与 SHA-256、已读集合、每类未读原生顺序、选择结果、图片/文本菜单预期以及 10/23 号纸条的任务副作用。菜单投影新增 `secret_note_image` 和 `which_bg`，输出不再只凭运行时返回值判断。
- 书籍和纸条现共用唯一 `UseInventoryObjectNative` 调用原生 `performUseAction` 并仅在成功时执行原生调用者的 `reduceActiveItemByOne`；纸条执行器不直接写已读集合、任务、菜单或库存。菜单为空是产品安全门，不伪装成原版纸条分支条件。
- 隐藏静音 E 盘隔离运行 `runtime-secret-note-smoke-20260829-103012` 四分支 PASS：多未读集合确定性选择 18、10 号新增任务 30、23 号新增任务 29、1001 号日记残页无任务副作用，四例均精确已读、原生 LetterViewerMenu 和单物品消耗。
- 最新 full snapshot schema 为 `132 required / 116 readable with provenance / 16 contextual / 0 blocking`。权威对账为 `156 registered / 197 semantic / 155 compiler-bound / 82 five-gate / 40 training allowlist / 41 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `executor.use_firework`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-283

- `executor.plant_grass` 已闭合普通草种 `(O)297` 与蓝草种 `(O)BlueGrassStarter` 的透明读取、精确地块重绑定、动作编译、v1 请求绑定、共享相邻移动、原生放置和后状态回执。锁定 1.6.15 反编译确认两条分支分别创建 `Grass(1,4)` 与 `Grass(7,4)`，均播放 `dirtyHit`。
- 上游用途/布局规划器必须提供 `grass_layout_reason` 和精确目标/站位；执行器不会推断牧草布局。编译器从 fresh full snapshot 重绑草种变体、槽位、堆叠、合法区间、可达性和投影指纹，任一漂移均失败关闭。
- 生产运行只调用共享 `Utility.playerCanPlaceItemHere -> Utility.tryToPlaceItem`，不直接写 `terrainFeatures`、`grassType`、`numberOfWeeds` 或库存。`current_location.terrain_features` 现直接发布草类型与 weeds 数，支持严格输出复核。
- 隐藏、静音、E 盘隔离运行 `runtime-grass-placement-20260829-093605` 双分支 PASS：type 1/type 7 均为四株初始 weeds，库存精确减一，透明后状态一致。
- 最新 full snapshot schema 为 `131 required / 115 readable with provenance / 16 contextual / 0 blocking`。权威对账为 `155 registered / 197 semantic / 154 compiler-bound / 81 five-gate / 40 training allowlist / 42 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `executor.read_secret_note`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-282

- `world.tune_drum_block` 已闭合透明读取、显式玩家候选、DailyPlan、新鲜字段重绑定、动作编译、v1/v2 类型请求、共享原生运行和持久化音色回执。锁定 1.6.15 反编译确认 `(O)463` 每次右键将 `preservedParentSheetIndex` 按 `(current + 1) % 7` 推进，并播放 `drumkit0..6` 对应音色。
- 该动作严格为 `PlayerCommandOnly`，默认策略候选和训练 allowlist 均排除。路过播放由独立 `farmerAdjacentAction` 入口处理，不得替代调音。Drum 与 Flute 只共用一个 `NoteBlockTuning` 移动/原生交互状态机，各自保留独立透明字段、验证规则和 E3 证据。
- 隐藏、静音、E 盘隔离运行 `runtime-drum-block-20260829-040623` 为 PASS：`6->0`、`drumkit0`、`shakeTimer=200`、`scale.Y=1.3`、原生返回 true、对象身份与槽位保持，只加载 TransparentBridge 与 RuntimeTestHarness。
- 最新权威对账为 `154 registered / 197 semantic / 153 compiler-bound / 80 five-gate / 40 training allowlist / 43 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `executor.plant_grass`。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-281

- `world.tune_flute_block` 已闭合透明读取、显式玩家候选、DailyPlan、新鲜字段重绑定、动作编译、v1/v2 类型请求、共享原生运行和持久化音高回执。锁定 1.6.15 反编译确认 `(O)464` 每次右键按 `0..2400`、步长 `100`、共 25 档推进，特殊边为 `2300->2400->0`。
- 该动作严格为 `PlayerCommandOnly`。编译器选择空槽或工具槽以禁用手持物音色覆盖，执行一次原生 `GameLocation.checkAction`；路过触发的 `farmerAdjacentAction` 是独立播放入口，不得混入调音命令或训练。
- 隐藏、静音、E 盘隔离运行 `runtime-flute-block-20260829-034718` 为 PASS：`2300->2400`、`shakeTimer=200`、`scale.Y=1.3`、原生返回 true、对象身份与槽位保持，只加载 TransparentBridge 与 RuntimeTestHarness。
- 最新权威对账为 `153 registered / 197 semantic / 152 compiler-bound / 79 five-gate / 40 training allowlist / 44 catalogued blocked / 0 Product Executor`；下一语义切片固定为 `world.tune_drum_block`，继续复用同一共享移动/交互骨架并单独锁定七档鼓声音色循环。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-280

- `farming.read_farm_computer_report` 已闭合透明读取、显式玩家候选、DailyPlan、机械字段新鲜重绑定、动作编译、v1/v2 类型请求、原生延迟对话和报告摘要回执。锁定 1.6.15 反编译确认原生 `(BC)239` 以对象所在地点的 `GetRootLocation()` 为报告根，精确读取作物、空闲耕地、成熟/未浇水作物、温室、采集物、已完成机器、干草与农场洞穴状态。
- 透明桥直接发布结构化报告字段及本地化原生报告 SHA-256；策略层无需打开菜单才能获知这些信息。该动作严格为 `PlayerCommandOnly`，仅显式玩家请求能生成，默认候选和训练 allowlist 均排除。
- 生产运行复用唯一 `NativeObjectInteractionMovement`，到站后只调用一次 `GameLocation.checkAction`，等待原生 500ms 延迟生成 `DialogueBox`；不合成报告、不直接设置菜单，并验证对象身份、精确报告摘要和工具栏槽恢复。显式命令完成后保留报告供玩家阅读。
- 隐藏、静音、E 盘隔离运行 `runtime-farm-computer-20260829-031326` 为 PASS：`native_handled=true`、即时 `shakeTimer=500`、`freezePause=500`、延迟菜单为 `DialogueBox`，报告摘要与透明投影一致，且只加载 TransparentBridge 与 RuntimeTestHarness。
- 最新权威对账为 `152 registered / 197 semantic / 151 compiler-bound / 78 five-gate / 40 training allowlist / 45 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens` 且 blocking 为 `0`，另有 `2` 个分母外兼容占位。
- 下一语义切片固定为 `world.tune_flute_block`：先实时反编译音高调整、输入方向与持久化语义，再确定 PlayerCommandOnly 边界；继续复用共享对象移动/交互，不复制第二套执行器。

## 历史记录 · 2026-08-29 当时权威检查点：EVD-279

- `movement.use_mini_obelisk` 已闭合透明读取、校准候选、DailyPlan、机械字段重绑定、动作编译、v1/v2 类型请求、原生运行和延迟传送回执。锁定 1.6.15 反编译确认原生分支按 `location.objects.Pairs` 的实际枚举顺序取前两个 `(BC)238`，以 `Vector2.Zero` 为哨兵；从交互站位选择欧氏距离更远的一端，平局取第二端，再按下、左、右、上的顺序选择第一个 `IsTileBlockedBy(All,All)==false` 落点。
- 透明桥逐对象发布实际原生配对序号、两端坐标、每个安全站位对应的目的端与落点。第三个方尖碑、非基础对象、缺失配对、无落点和破坏性对象陷阱均在上游失败关闭；编译器从新鲜快照重绑全部机械字段，不能接受模型伪造的配对、目标或落点。
- 生产运行复用唯一 `NativeObjectInteractionMovement` 与共享 BFS，到站后只调用一次 `GameLocation.checkAction`。生产文件不写 `Farmer.Position`、不调用 `setTileLocation`；它等待原生 50ms 延迟回调完成，并以精确落点、两端引用/身份不变、角色重新显示、菜单为空和工具栏槽恢复验收。
- 隐藏、静音、E 盘隔离运行 `runtime-mini-obelisk-20260829-011139` 为 PASS：`native_handled=true`，预期/实际落点均为 `(1,2)`，配对坐标和对象引用不变，只加载 TransparentBridge 与 RuntimeTestHarness。该动作严格为 `ExecutorCalibration`，不会进入默认策略候选或训练 allowlist，运行样本范围为 `executor_calibration_only_not_strategy_desire`。
- 最新权威对账为 `151 registered / 197 semantic / 150 compiler-bound / 77 five-gate / 40 training allowlist / 46 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens` 且 blocking 为 `0`，另有 `2` 个分母外兼容占位。回归为 Core `1863/1863`、Backend `134/134`。
- 下一语义切片固定为 `farming.read_farm_computer_report`。先实时反编译原生 Farm Computer 分支及数据来源，再决定候选/训练分类；不得把“能读取报告”直接解释成策略收益，也不得复制第二套对象移动器。

## 历史记录 · 2026-08-28 当时权威检查点：EVD-277 / EVD-278

- Issue #31 的“按共享原生底层批量推进”已采纳，但批量只表示复用同一移动/菜单状态机；每个语义动作仍保留独立透明字段、前后条件、运行夹具和 E3 证据，禁止把一条运行结果外推给整组动作。
- `Lantern` 与 `Raft` 均确认为原版不可达的删减工具，并转入显式兼容占位目录。事实纠正：锁定 1.6.15 的 37 项 `Data/Tools` **包含 Lantern、没有 Raft**；二者均只有残留类型/调试构造或兼容状态，没有正常存档获取路径。它们不进入原版语义分母、候选、训练和待实现列表，但占位保留原生类型与未来 MOD 适配入口。
- `animals.collect_auto_grabber_contents` 已闭合透明读取、上游排除、DailyPlan、机械字段重绑定、动作编译、原生菜单运行和物品守恒回执。它与喂食斗只共享 `NativeObjectInteractionMovement`；收取本身走精确 `(BC)165 -> ItemGrabMenu -> receiveLeftClick`，生产代码不直接改背包或 held Chest。
- 隐藏、静音、E 盘隔离运行 `runtime-auto-grabber-20260828-165346` 为 PASS：原生菜单转移 `2` 个堆栈、共 `5` 件物品，Auto-Grabber 余量 `0`，`native_handled=true`，对象/Chest 身份和选中槽均保持正确。
- 最新权威对账为 `150 registered / 197 semantic / 149 compiler-bound / 76 five-gate / 40 training allowlist / 47 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens` 且 blocking 为 `0`，另有 `2` 个分母外兼容占位。回归为 Core `1848/1848`、Backend `132/132`。
- 下一语义切片为 `movement.use_mini_obelisk`，继续处理 `Object.checkForAction` 组；只复用已验证的移动/交互底层，并独立锁定成对方尖碑、目标选择、传送后位置与新鲜快照终止条件。

## 历史记录 · 2026-08-28 当时权威检查点：EVD-275 / EVD-276

- `StardewValley.Tools.Raft` 已确认为锁定 1.6.15 的不可达删减类型。类、`Farmer.isRafting` 和兼容移动分支仍存在，但 37 项 `Data/Tools` 没有 Raft，全部运行时内容资产也没有获取或事件入口，源码除类型自身 `GetOneNew` 外没有外部构造或工厂路径。当前分类已由上方 EVD-277 接管为分母外兼容占位；`executor.use_raft` 不进入候选、训练或原版待实现目录。
- `animals.withdraw_feed_hopper_hay` 已闭合透明读取、上游候选、DailyPlan、机械字段重绑定、动作编译、原生运行和守恒回执。透明投影精确发布根地点料仓干草、动物数、动物屋容量、已摆干草、未喂动物、原生取草量、背包接纳和安全站位；没有未喂动物、料仓为空、料槽无容量或背包不能接纳时在上游排除。
- 生产运行复用唯一 `NativeObjectInteractionMovement` 和共享 BFS，到站后只调用一次 `GameLocation.checkAction`。生产代码不直接写 `piecesOfHay`、不直接加背包；成功必须同时满足料仓精确减少、背包 `(O)178` 精确增加、喂食斗身份不变、菜单为空和选中槽恢复。
- 隐藏、静音、E 盘隔离运行 `runtime-feed-hopper-20260828-130723` 为 PASS：原生一次取出 `8`，料仓 `10 -> 2`、背包 `0 -> 8`，`native_handled=true`，喂食斗保留且槽位恢复；只加载 TransparentBridge 与 RuntimeTestHarness。
- 最新权威对账为 `149 registered / 198 semantic / 148 compiler-bound / 75 five-gate / 39 training allowlist / 49 catalogued blocked / 0 Product Executor`；原生分母为 `322 surfaces / 448 branches / 150 map tokens`，三类 blocking 均为 `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。回归为 Core `1844/1844`、Backend `131/131`。
- 下一语义切片按冻结待办顺序为 `animals.collect_auto_grabber_contents`；必须先核对原生菜单、互斥锁、容器库存和空容器分支，再复用唯一库存/菜单转移实现，不复制第二套容器执行器。

## 历史记录 · 2026-08-28 当时权威检查点：EVD-274

- `world.play_singing_stone` 已闭合透明读取、显式玩家指令候选、DailyPlan 表述、机械字段重绑定、动作编译、原生运行与回执。锁定 1.6.15 反编译确认目标是基础 `StardewValley.Object` `(BC)94`，不是同名家具 `(F)1300`；原生分支执行 `Game1.random.Next(2400)` 后向下取整到百位，均匀产生 `0..2300` 共 24 种 `crystal` 音高，并把 `shakeTimer` 设为 `100`。
- `Game1.random` 是共享 RNG，透明桥只发布完整分布和 `unavailable_shared_rng_state_not_consumed`，不得读取、推进或猜测下一音高。候选只选择一个精确声音石；对象身份、相邻站位、安全工具栏槽、分布和原生契约全部由最新快照与编译器重绑定，坐标漂移时不得替换另一块石头。
- 该动作严格为 `PlayerCommandOnly`：默认候选、自主日计划和策略训练全部排除，只有 `InvocationSource=PlayerCommand` 且显式确认后可进入已有授权链。运行证据的范围是 `player_command_only_executor_evidence`，不能反推训练准入。
- 运行时与 House Plant 共用 `NativeObjectInteractionMovement`，保留原生动画/BFS，只调用一次 `GameLocation.checkAction`；生产代码不调用 `Game1.playSound`、不写 `shakeTimer=100`、不消费 RNG，并在交互前双检四向物体陷阱、目标身份、安全手持状态和槽位恢复。
- 隐藏、静音、E 盘隔离运行 `runtime-singing-stone-20260828-102438` 为 PASS：`native_handled=true`、`shake_timer=100`、`item_id=94`、`qualified_item_id=(BC)94`，选中槽恢复；只加载 TransparentBridge 与 RuntimeTestHarness。
- 权威对账为 `148 registered / 199 semantic / 147 compiler-bound / 74 five-gate / 38 training allowlist / 51 catalogued blocked / 0 Product Executor`；原生分母仍为 `322 surfaces / 448 branches / 150 map tokens` 且 blocking 均为 `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。全量回归为 Core `1839/1839`、Backend `130/130`。
- `executor.use_raft` 已从动作全集分母移除：锁定 1.6.15 虽保留 `Raft` 类、`Farmer.isRafting` 和移动兼容分支，但 37 项 `Data/Tools` 无该工具，源码也无获取、工厂、事件或外部构造入口；当前以分母外兼容占位保留原生证据，不作为候选、训练目标或原版待实现能力。
- 下一语义切片固定为 `animals.withdraw_feed_hopper_hay`：先锁定饲料斗交互、库存/料槽容量与取草数量分支，再判断是否完全复用唯一库存转移引擎；不得从历史段落回退到已闭合声音石或复制第二套转移系统。

## 历史记录 · 2026-08-27 当时权威检查点：EVD-272 / EVD-273

- `farming.collect_slime_ball` 已闭合透明读取、逐对象候选、DailyPlan、机械字段重绑定、动作编译、原生运行与产物守恒回执。锁定 1.6.15 反编译确认自然史莱姆球必须是精确 `SlimeHutch` 中的基础 `StardewValley.Object`、`(BC)56`、`Fragility=2`；`CheckForActionOnSlimeBall` 先移除对象，再按 `DaysPlayed`、`uniqueIDForThisGame` 和目标格生成 `10..20` 个 `(O)766`，并按 `NextDouble()<0.33` 的几何分布生成 `(O)557`。
- 透明桥在 `current_location.objects[].slime_ball_collection` 发布来源类型、永久身份、随机种子、两类精确预期产量、原生返回值、生成契约、共享 debris 拾取交接和安全相邻站位。候选仅选择“收取哪一个球”；种子、产量、站位、空槽和原生调用细节均由编译器在新鲜快照上重绑定，指定坐标漂移时不得改选另一个球。
- 生产执行器复用共享 BFS 和通用破坏性四向物体陷阱检查，临时切到真实空工具栏槽位，只调用一次 `GameLocation.checkAction`。它不直接删对象、不直接造 debris，也不复制拾取器；回执以“背包 + 当前地点 debris”的 `(O)766`/`(O)557` 守恒量验证生成，剩余地面物品继续交给唯一 `executor.pickup_debris`。
- 隐藏、静音、E 盘隔离运行 `runtime-slime-ball-20260827-174122` 为 PASS：目标 `(2,3)` 原生移除，种子投影与实际守恒输出均为 `(O)766 x11`、`(O)557 x0`，工具栏槽恢复，只加载 TransparentBridge 与 RuntimeTestHarness。
- EVD-273 新增调用来源硬分类。`PlayerCommandOnly` 动作可以保留透明读取、编译和原生执行能力，但默认候选生成会排除，策略来源会被 SafetyPolicyGate 阻断，训练准入会给出 `PlayerCommandOnly` 类型化排除。当时包括 `buildings.change_skin`、`buildings.paint`、`world.rotate_house_plant`、`executor.change_building_skin`、`executor.place_furniture`、`executor.set_sign_display_item`、`executor.edit_text_sign`；只有 `InvocationSource=PlayerCommand` 且满足原有确认/权限门时才能执行。
- 权威对账为 `147 registered / 199 semantic / 146 compiler-bound / 73 five-gate / 38 training allowlist / 52 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens` 且 blocking 均为 `0`。本切片复用 `current_location.objects`、`current_location.debris` 和 `player.inventory`，不新增 full 快照顶层必需字段。
- 最终回归为 Core `1833/1833`、Backend `129/129`；Release 解决方案构建 `0` 错误，仅保留既有 `MiningReadAdapter.Objects.cs` 的一条 `AvoidNetField` 警告。下一语义切片按未闭合目录顺序为 `world.play_singing_stone`；必须先锁定原生声音石交互和可观测结果，再决定它属于自主策略、机械日计划还是 `PlayerCommandOnly`，不得由“能执行”反推训练准入。

## 历史记录 · 2026-08-27 当时权威检查点：EVD-271

- `world.rotate_house_plant` 已闭合透明读取、逐对象候选、DailyPlan、机械字段重绑定、动作编译、原生运行与八帧回执。模型只选择“轮转哪一盆”这一有意义的装饰目标；当前贴图帧、预期帧、相邻站位、空工具栏槽位、恢复槽位和原生调用契约全部由最新快照与编译器绑定。
- 锁定 1.6.15 反编译与 `Data/BigCraftables` 确认：`(BC)0..7` 都是基础 `House Plant`，永久 `ItemId/QualifiedItemId` 与当前 `ParentSheetIndex` 相互独立。`CheckForActionOnHousePlant` 通常执行 `0→1→…→7→0`；但通过真实 `GameLocation.checkAction` 且空手交互时，起始帧 `7` 的第一次对象调用变为 `0` 并返回 `false`，地点层随后再次调用对象，所以一次玩家交互的最终结果是 `7→1`。
- 透明桥在 `current_location.objects[]` 中分别发布永久身份、当前帧、一次原生地点交互后的精确帧、预期对象调用次数、地点返回值与邻格。只有精确基础对象、`Crafting` 类型、`0..7` 有效帧、可达邻格、菜单关闭且存在真正空工具栏槽位时才开放候选；工具槽回退被拒绝，以冻结空手双调用语义。
- 生产执行器复用共享 BFS，临时切到编译器绑定的空槽，只调用一次 `GameLocation.checkAction`，然后恢复原槽位。生产文件不写 `ParentSheetIndex`，也不直接调用对象 `checkForAction`；成功必须同时观察预期帧、同一对象引用、永久 ID 不变、菜单为空和槽位恢复。
- 该装饰动作是严格 `PlayerCommandOnly`：不会进入默认候选、自主日计划或策略训练；只有玩家显式指令并通过原有确认门后，现有编译与原生执行链才可运行。
- 原生 `Object.checkForAction` 还存在四个基数方向均被不可通行对象封闭时对目标调用 `performToolAction(null)` 的破坏性前导分支。透明桥为每个站位发布 `object_trap_blocked`，候选在上游排除，运行时在寻路前和交互前再次核对并失败关闭，绝不让装饰轮转误删目标。
- 隐藏、静音、E 盘隔离运行 `runtime-house-plant-20260827-123652` 为 `8/8 PASS`：起始帧 `0..7` 分别得到 `1,2,3,4,5,6,7,1`，最后一例验证对象调用次数为 `2`；每例 `item_id=0`、`qualified_item_id=(BC)0` 与选中槽位均保持/恢复。
- 权威对账更新为 `146 registered / 199 semantic / 145 compiler-bound / 72 five-gate / 40 training allowlist / 53 catalogued blocked / 0 Product Executor`；原生分母仍为 `322 surfaces / 448 branches / 150 map tokens`，blocking 均为 `0`。full 快照顶层因本切片复用 `current_location.objects`，仍为 `130 required / 114 readable / 16 contextual / 0 blocking`。
- 最终回归为 Core `1826/1826`、Backend `128/128`；Release 解决方案构建 `0` 错误，仅保留既有 `MiningReadAdapter.Objects.cs` 的一条 `AvoidNetField` 警告。下一语义切片固定为 `farming.collect_slime_ball`：必须先核对史莱姆球的生成条件、每日状态、原生收取产物与背包/掉落分支，再决定复用对象交互还是库存转移内核。

## 历史记录 · 2026-08-27 当时权威检查点：EVD-270

- `rewards.claim_statue_blessing` 已闭合透明读取、无参数日级候选、DailyPlan、机械字段重绑定、动作编译、原生运行与全天 buff 回执。小模型只输出“领取今日祝福”这一语义目标；祝福编号、雕像、站位、日期、天气/节日分母与对象交互全部由最新快照和编译器绑定。
- 锁定 1.6.15 反编译确认 `(BC)StatueOfBlessings` 需要 `StatKeys.Mastery(0) >= 1`，并以 `CreateDaySaveRandom(DaysPlayed*777)` 丢弃八次后抽取。普通日为 `Next(7)`；雨天或节日为 `Next(6)`，因此不会抽到蝴蝶祝福。`hasBeenBlessedByStatueToday` 或任一 `statue_of_blessings_*` buff 都会在上游排除重复领取。
- 透明投影覆盖七种原生效果：速度 `+0.5`、幸运 `+1`、体力不下降、前三次钓鱼减难度、交谈友谊 `20 -> 60`、暴击率 `+0.1`、17:00 前棱彩蝴蝶及其金钱/棱彩碎片结算。效果仍由原生 Buff、Farmer、BobberBar、NPC、GameLocation 和 Butterfly 分支消费，没有复制战斗、钓鱼或社交执行器。
- 生产执行器复用共享 BFS，到精确相邻格后只调用一次 `GameLocation.checkAction`。禁止生产路径直接 `applyBuff`、改写 `hasBeenBlessedByStatueToday` 或写 `AppliedBuffs`；结果同时要求唯一预测 buff、日锁为真、菜单为空。
- 隐藏、静音、E 盘隔离运行 `runtime-statue-blessing-20260827-115317` 返回 `applied/verified`：当天普通日预测并实际得到 `statue_of_blessings_1`，只观察到该幸运祝福，日领取锁变为 `true`。
- 最新真实 full 快照已安装：`130 required / 114 readable / 16 contextual / 0 blocking`；KnowledgeCompiler 为 `585/585`、blocking `0`。权威对账为 `145 registered / 199 semantic / 144 compiler-bound / 71 five-gate / 39 training allowlist / 54 catalogued blocked / 0 Product Executor`，原生分母仍为 `322 surfaces / 448 branches / 150 map tokens` 且三类 blocking 均为 `0`。
- 最终回归为 Core `1813/1813`、Backend `127/127`；Release 解决方案构建保持 `0` 错误，仅保留既有 `MiningReadAdapter.Objects.cs` 的一条 `AvoidNetField` 警告。
- 下一语义切片固定为 `world.rotate_house_plant`：先反编译八帧轮转、对象子类和交互返回值，再复用当前地图对象定位与原生直接交互；不得把装饰轮转伪装成长线策略，也不得建立第二套移动器。

## 历史记录 · 2026-08-27 当时权威检查点：EVD-269

- `mining.choose_dwarf_statue_power` 已闭合透明读取、两个策略候选、DailyPlan、选择保留与机械字段重绑定、动作编译、原生运行和全天 buff 回执。小模型只从当天两个精确选项中输出 `dwarf_statue_power_id`；雕像、站位、菜单索引、buff ID、日期指纹和点击细节全部由最新快照与编译器绑定。
- 锁定 1.6.15 反编译确认 `(BC)StatueOfTheDwarfKing` 需要 `StatKeys.Mastery(3) >= 1`，以 `Utility.CreateRandom(DaysPlayed*77, uniqueID)` 每天固定生成两个不同的 `0..4` 选项。五种效果分别落在额外矿石、楼梯/竖井、煤炭、炸弹免伤和晶球概率的原生分支；已有任一 `dwarfStatue_*` buff 时当日不能重选。
- 生产执行器只复用共享 BFS，调用原生 `GameLocation.checkAction` 打开 `ChooseFromIconsMenu`，核对两个图标身份和顺序后调用原生 `receiveLeftClick`。禁止生产路径直接 `applyBuff` 或写 `AppliedBuffs`；普通矿井、骷髅洞和火山继续读取同一个 buff，不新增第二套矿洞或战斗系统。
- 隐藏、静音、E 盘隔离运行 `runtime-dwarf-king-statue-20260827-111935` 对当天两个选项 `0/3` 均返回 `applied/verified`，分别只观察到 `dwarfStatue_0` 和 `dwarfStatue_3`，原生菜单在 800ms 销毁后关闭。
- 最新真实 full 快照已安装：`129 required / 113 readable / 16 contextual / 0 blocking`；KnowledgeCompiler 为 `585/585`、blocking `0`。权威对账为 `144 registered / 199 semantic / 143 compiler-bound / 70 five-gate / 38 training allowlist / 55 catalogued blocked / 0 Product Executor`，原生分母保持 `322 surfaces / 448 branches / 150 map tokens` 且三类 blocking 均为 `0`。
- 最终 Release 回归为 Core `1802/1802`、Backend `126/126`；解决方案构建 `0` 错误，仅保留既有 `MiningReadAdapter.Objects.cs` 的一条 `AvoidNetField` 警告。
- 下一语义切片固定为 `rewards.claim_statue_blessing`。必须先锁定祝福雕像的每日随机状态、领取锁、全部祝福效果和原生菜单/对象路径；可以复用本切片的对象定位、日级候选和原生菜单生命周期，但不得把两个雕像的随机分母、buff ID 或规则混用。

## 历史记录 · 2026-08-26 当时权威检查点：EVD-267

- `recovery.escape_object_trap` 已贯通透明读取、候选、日计划和动作编译链。小模型只需发出该高层恢复命令；编译器从玩家四个基数方向的对象行与 `farm.machines` 中按固定方向顺序选择首个 `removal_safe_now=true` 的相邻机器，不要求模型生成拆除坐标或工具细节。
- 锁定 1.6.15 反编译确认 `Object.checkForAction` 的四向封闭前导分支会调用目标 `performToolAction(null)`；base `Object` 随即从 `location.objects` 移除且只产生装饰 debris，不返还资产，并可能继续执行目标交互。透明桥发布四向存在性、可通行性、运行时/声明类型、交互状态及该破坏性契约，但 `destructive_native_fallback_enabled=false`。
- 安全恢复只编译到既有 `executor.remove_machine`：复用其精确机器身份、所有权、空闲/无产出/无附件、fragility、Pickaxe、指纹和原生 debris/自动回收验证；没有第二套移除运行时。非四向封闭、菜单/手持物/骑马冲突、无安全相邻机器或机器状态漂移均关闭失败。
- 隐藏、静音、E 盘隔离环境已采集真实 full 快照并安装：`127 required / 111 readable / 16 contextual / 0 blocking`。聚焦候选/日计划/编译/治理测试与全量 Core 回归通过；KnowledgeCompiler 为 `585/585`、blocking `0`。
- 当前权威对账为 `142 registered / 199 semantic / 141 compiler-bound / 68 five-gate / 36 training allowlist / 57 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens`，三类 blocking 均为 `0`。five-gate 未增加，因为尚未伪造一份专用四向陷阱运行回执；实际资产移除内核沿用已验证的 EVD-147/EVD-148。
- 下一语义切片固定为 `rewards.claim_pot_of_gold`：先从锁定反编译确定彩虹尽头奖励对象的生成、可领取状态、背包满分支、一次性状态与原生转移路径，再决定复用对象交互/背包转移内核；不把通用拾取冒充特殊奖励领取。

## 历史记录 · 2026-08-26 当时权威检查点：EVD-266

- `recovery.sleep_in_tent` 已作为独立的终端跨日语义闭合；它复用共享移动、原生交互、确认输入、跨日等待、保存/出货结算和稳定世界判定，不并入 `executor.place_tent` 或普通床 `executor.sleep`。
- full 快照新增 `player.temporary_sleep`、`menus.tent_sleep_prompt_context`，并扩展精确 base `Tent` 行的睡眠地点、锚点、规范交互格、规范站位、朝向、问题键和原生许可。编译器冻结这些字段以及运行时身份、血量、可通行性和路径，只允许最后一个计划步骤执行。
- 生产执行器先走到 `anchor+(0,1)`、面朝上，再调用原生 `GameLocation.checkAction` 打开 `SleepTent`，随后发送原生确认输入。它不直接写日期、`sleptInTemporaryBed`、Tent health、玩家位置或 Tent 表；共享跨日结算必须观测 temporary-bed 标志、日期恰好增加一天、同地点同格醒来、保存菜单关闭、标志复位和 Tent 隔夜销毁。
- 隐藏、静音、E 盘隔离运行 `runtime-tent-sleep-20260826-164032` 返回 `applied/verified`：总日数 `222 -> 223`，在 `Farm:66,19` 同地点同格醒来，观测到 `SleepTent_Yes`、temporary-bed 标志先置位后复位、世界稳定且精确 Tent 消失。
- 当时权威对账为 `141 registered / 199 semantic / 140 compiler-bound / 68 five-gate / 36 training allowlist / 58 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens`，三类 blocking 均为 `0`；full 快照为 `126 required / 110 readable / 16 contextual / 0 blocking`，KnowledgeCompiler 为 `585/585`、blocking `0`。
- 下一语义切片从未闭合目录首项 `recovery.escape_object_trap` 开始：先锁定原生脱困分支与透明状态，再判断是否能复用现有移动/交互内核；在反编译和严格状态契约完成前不建立泛化执行器。

## 历史记录 · 2026-08-26 当时权威检查点：EVD-265

- `executor.place_tent` 已作为独立放置语义闭合；它不包含 `recovery.sleep_in_tent`。full 快照的 `player.tent_placement` 仅接受精确 base `(O)TentKit`，逐方向压缩发布原生合法站位，并绑定室外限制、跨季节明日日期、普通节日、被动节日地图替换/多日窗口、3x2 矩形、中心锚点、初始 `Tent` 状态、日更销毁和后续睡眠交接。
- 编译器冻结精确槽位/堆叠/地点/站位/朝向探针/矩形/锚点/日历/投影指纹/布局指标/放置理由与原生契约。共享邻接执行器现会尊重已冻结的 `stand_tile`，再由唯一 `PlaceInventoryObjectNative` 调用 `Utility.tryToPlaceItem -> Object.placementAction`；生产代码不直接写 `largeTerrainFeatures` 或库存。
- 隐藏、静音、E 盘隔离运行 `runtime-tent-placement-20260826-160613` 为 `4/4 PASS`，覆盖方向 `0..3`。每例均验证原生返回、库存减一、精确 base `Tent`、方向派生锚点/3x2 边界、`health=5`、玩家可通行性及下一份透明快照回读。
- 当时权威对账为 `140 registered / 199 semantic / 139 compiler-bound / 67 five-gate / 36 training allowlist / 59 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens`，三类 blocking 均为 `0`；full 快照为 `123 required / 107 readable / 16 contextual / 0 blocking`，KnowledgeCompiler 为 `585/585`、blocking `0`。
- 下一语义切片固定为 `recovery.sleep_in_tent`：复用现有移动、交互、确认、跨日等待和 `executor.sleep` 的终止性规则，只新增 `Tent.performUseAction -> SleepTent_Yes -> sleptInTemporaryBed`、同地点醒来及隔夜销毁回执；不得把它重新并入放置动作或普通床睡眠。

## 历史记录 · 2026-08-26 上一权威检查点：EVD-264

- `executor.edit_text_sign` 已作为独立语义闭合，不与标牌放置或展示物赋值合并。full 快照对精确 base `StardewValley.Object` 文字牌发布 raw/display 文本、`showNextIndex`、直接序列化 SHA-256、替换要求、60 UTF-16 code-unit 限制及完整原生菜单管线。
- 编译器严格绑定目标地点/格子/运行时类型/qid/状态哈希/投影指纹、相邻站位、旧文本与覆盖授权，并拒绝超过 60 code units、双引号或控制字符的非原生键盘输入。生产执行器仅复用共享相邻移动，调用 `GameLocation.checkAction`，逐字符输入 `TitleTextInputMenu.textBox` 并点击原生完成按钮；不直接写 `signText` 或 `showNextIndex`。
- 原生回执按实际顺序验证 `NamingMenu.FilterInput -> Utility.FilterDirtyWords -> Trim -> NetString -> TokenParser.ParseText -> Utility.FilterDirtyWords`，并验证 `showNextIndex == string.IsNullOrEmpty(SignText)`。隐藏、静音、E 盘隔离运行 `runtime-text-sign-editing-20260826-104822` 为 `5/5 PASS`，覆盖首次写入、旧文本替换、首尾空白裁剪、清空、中文 UTF-16 输入和 60 code-unit 边界。
- 当时权威对账为 `139 registered / 199 semantic / 138 compiler-bound / 66 five-gate / 36 training allowlist / 60 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens`、三类 blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。
- 标牌三条独立链现已全部闭合：`executor.place_sign`、`executor.set_sign_display_item`、`executor.edit_text_sign`。下一语义切片固定为 `executor.place_tent`，继续复用共享原生物品放置与布局安全，不建立第二套移动或放置系统。

更新时间：2026-08-26

## 历史记录 · 2026-08-25 当时权威检查点：EVD-263

- `executor.set_sign_display_item` 已闭合：full 快照对每个精确 base `Sign` 发布全部非空背包物品、源对象直接序列化 SHA-256、原生展示类型、旧展示载荷和替换要求；读取端禁止调用 `getOne()`，避免构造副本时消耗 RNG。
- 编译器逐项绑定目标、相邻站位、源槽位/身份/品质/堆叠/状态哈希、旧载荷和替换授权。生产执行只复用共享相邻移动并调用 `GameLocation.checkAction -> Sign.checkForAction`，不直接写 `displayItem`、`displayType` 或背包。
- 隐藏、静音、E 盘隔离运行 `runtime-sign-display-item-20260825-170751` 为 `6/6 PASS`：覆盖展示类型 `1..5` 与非 Object 默认分支，后五例覆盖已有展示替换；源物品引用、数量和完整序列化状态全部不变。
- 当时权威对账为 `138 registered / 199 semantic / 137 compiler-bound / 65 five-gate / 36 training allowlist / 61 catalogued blocked / 0 Product Executor`；原生分母保持 `322 surfaces / 448 branches / 150 map tokens`、blocking `0`，KnowledgeCompiler 为 `585/585`。
- 下一语义切片固定为 `executor.edit_text_sign`：独立闭合原生 `TitleTextInputMenu`、trim、60 字限制和 `showNextIndex`，不得并入标牌放置或展示物赋值。

更新时间：2026-08-25

## 历史记录 · 2026-08-25 当时权威检查点：EVD-262

- `executor.place_sign` 已闭合实时标牌目录、精确库存、当前地图原生合法格、严格编译、共享邻接移动、共享原生物品放置和逐字段回执。锁定 1.6.15 存在两条不同原生分支：带 `sign_item` 标签的三种展示牌生成精确 `StardewValley.Objects.Sign`；`(BC)TextSign` 生成精确 base `StardewValley.Object`。两者均只放置空牌且库存精确 `-1`。
- `player.sign_placement` 从实时 `Game1.bigCraftableData` 枚举全部 `4` 行，不硬编码标牌 ID 或数量。候选绑定数据行、运行时类型、分支、当前地图合法格、邻接站位、布局安全、空载荷预期和拓扑指纹；`current_location.objects[].sign_state` 回读展示物类型/身份、文字、`showNextIndex`、可通行性和运行时支持状态。
- 生产执行器只复用 `Utility.playerCanPlaceItemHere -> Utility.tryToPlaceItem -> Object.placementAction` 与既有 `PlaceInventoryObjectNative`，不直接写 `location.objects`、标牌载荷、文字或库存。隐藏、静默、E 盘隔离运行 `runtime-sign-placement-20260825-160744` 对实时目录全部 `4/4 PASS`，覆盖三种展示牌和文字牌两条分支。
- 反编译复核同时修复动作分母遗漏：`checkForAction` 与 `Objects/Sign.cs` 已进入扫描，新增 Object/Sign 动作面及 `20` 个原生分支。全部分支已逐项映射到已有能力或显式 `catalogued_blocked` 语义，不以通用交互伪装成完整支持。当前分母为 `322 surfaces / 448 branches / 150 map tokens`，三类 blocking 均为 `0`。
- 当时权威对账为 `137 registered / 199 semantic / 136 compiler-bound / 64 five-gate / 36 training allowlist / 62 catalogued blocked / 0 Product Executor`；fresh full 快照要求 `122` 个状态因子，其中 readable `106`、contextual/stale `16`、blocking `0`；KnowledgeCompiler 为 `585/585`、blocking `0`。
- 下一语义切片固定为 `executor.set_sign_display_item`，只实现 `Sign.checkForAction` 的展示物绑定和逐字段回执；其后独立实现 `executor.edit_text_sign` 的 `TitleTextInputMenu`、trim、60 字限制和 `showNextIndex` 语义。两者不得并入摆放动作。

更新时间：2026-08-25

## 历史记录 · 2026-08-25 当时权威检查点：EVD-261

- `executor.place_furniture` 已闭合实时 `Data/Furniture` 全目录、精确库存家具、当前地图家具拓扑、严格编译、共享移动、共享原生物品放置和逐字段回执。普通落地家具进入 `location.furniture`；在空桌面放置的 1x1 家具进入该桌子的 `heldObject`，两种原生终点不得混淆。
- `player.furniture_placement` 发布实时目录中的 `645` 行，并只对当前已加载地图执行目的限定的原生合法格扫描。每个候选绑定运行时子类、地点限制、墙面锚点修正、矩形占地、可通行性、所有虚拟旋转状态、原生终点、空桌身份和拓扑指纹。`current_location.furniture` 回读相同身份、旋转、占地、碰撞、容器内容与桌面载荷。
- 读取探针禁止调用可能改变源家具旋转状态的 `Furniture.getOne()`；统一使用 `Furniture.GetFurnitureInstance` 创建脱离源对象的规范实例。每个候选格都必须重新创建探针并调用 `InitializeAtTile`，防止旧 bounding box 污染后续格子和桌面判断。生产执行器只调用虚拟 `rotate()` 与既有 `PlaceInventoryObjectNative`，不直接写 `currentRotation`、`location.furniture`、桌面载荷或库存。
- 隐藏、静默、E 盘隔离运行 `runtime-furniture-placement-20260825-101457` 从实时目录选择完整代表集并 `25/25 PASS`：覆盖 `Furniture`、`StorageFurniture`、`FishTankFurniture`、`BedFurniture`、`RandomizedPlantFurniture`、`TV` 六种规范运行时子类，家具类型 `0..17`，旋转步数 `0..3`，以及 `location_furniture` 与 `table_held_object` 两种终点。
- 当时权威对账为 `136 registered / 184 semantic / 135 compiler-bound / 63 five-gate / 36 training allowlist / 48 catalogued blocked / 0 Product Executor`；fresh full 快照要求 `121` 个状态因子，其中 readable `105`、contextual/stale `16`、blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。
- 下一语义切片固定为 `executor.place_sign`。先反编译锁定标牌身份、显示物绑定和交互/文字语义是否与普通物品放置重叠；复用既有共享移动和唯一原生放置内核，只新增标牌特有的透明字段、严格校验与回执，不建立第二套放置系统。

更新时间：2026-08-25

## 历史记录 · 2026-08-25 当时权威检查点：EVD-260

- `executor.place_flooring` 已闭合实时地板目录、当前地图合法区间、严格编译、共享邻接移动、共享原生物品放置和逐字段回执。库存源必须是精确 base `StardewValley.Object`；原生 `Object.placementAction(IsFloorPathItem)` 在 `terrainFeatures` 中生成精确 base `StardewValley.TerrainFeatures.Flooring`，生产执行器不直接写地形表、视图或库存。
- `player.flooring_placement` 从实时 `Game1.floorPathData` 与 `Flooring.GetFloorPathItemLookup()` 发布完整目录。只有当前已加载地图按地板身份压缩原生合法区间，跨图必须到达后重新绑定；这保留全量数据透明，同时避免无关日程扫描所有持久地图。每个区间绑定同类八邻接掩码；`Random` 连接只绑定完整 `whichView=0..15` 结果域，非随机构造值为 `0`。
- 原生放置拒绝任何已有 `TerrainFeature` 的目标格，不存在放置时替换地板的能力。拆除仍是独立 Axe/Pickaxe/damage 工具语义。`Flooring.isPassable` 恒为 true，因此共享布局校验要求目标、邻接站位位于当前 BFS 可达域，放置前后可达格计数相同；不得错误复用围栏的虚拟阻塞规则。
- 隐藏、静默、E 盘隔离运行 `runtime-flooring-placement-20260825-015659` 从实时目录枚举 13 个规范物品并 `13/13 PASS`。全部返回 `applied/verified`，透明回读验证 data key、base Flooring、同类邻接掩码、可通行、视图域与库存减一；随机 `(O)415` 本轮实际视图为 `5`。
- 当时权威对账为 `135 registered / 184 semantic / 134 compiler-bound / 62 five-gate / 36 training allowlist / 49 catalogued blocked / 0 Product Executor`；fresh full 快照要求 `119` 个状态因子、blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。
- 下一语义切片进入 `executor.place_furniture`。必须先反编译区分普通家具、壁挂/地毯/旋转、室内地点限制与原生 `Furniture.placementAction`，继续复用共享移动和原生物品放置内核，不得复制第二套放置系统。

更新时间：2026-08-25

## 历史记录 · 2026-08-22 当时权威检查点：EVD-258

- `executor.load_crab_pot_bait` 已闭合透明读取、严格编译、共享邻接移动、原生 `GameLocation.checkAction` 执行和逐字段回执。它是独立的蟹笼上饵 primitive，不并入通用机器投料，也不复制 EVD-209 的蟹笼收取链。
- full 快照对每个精确 base `CrabPot` 发布生命周期、owner/Luremaster 状态和背包内所有 `Category=-21` 饵料的槽位、堆叠、运行时类型、品质、单位状态哈希及原生 probe 结果。编译器绑定目标、邻接站位、owner before/after、饵料身份、理由与原生契约；任一漂移即失败关闭。
- 隐藏、静默、E 盘隔离运行 `runtime-crab-pot-bait-20260822-164015` 为 5/5 PASS：普通、豪华、万能、魔法和特定目标鱼饵均由原生 checkAction 装入，原生 `reduceActiveItemByOne` 精确消耗一件，下一份透明快照读回相同 qid、运行时类型、品质、单位状态哈希与 owner。
- 当时权威对账为 `133 registered / 184 semantic / 132 compiler-bound / 60 five-gate / 36 training allowlist / 51 catalogued blocked / 0 Product Executor`；full 快照要求 `117` 个状态因子、blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。
- 下一语义切片进入 `executor.place_fence`。必须先审查并复用现有物品放置、邻接移动、碰撞与布局安全实现，禁止形成第二套放置系统。

更新时间：2026-08-22

## 历史记录 · 2026-08-22 当时权威检查点：EVD-257

- `executor.place_crab_pot` 已闭合透明读取、严格编译、共享邻接移动、共享原生物品放置和逐字段回执。源物品与放置结果均为 `(O)710`，但库存中的普通 `StardewValley.Object` 必须经原生 `Object.placementAction` 转成由当前玩家拥有的精确 `StardewValley.Objects.CrabPot`。
- `player.crab_pot_placement` 在 full profile 中枚举每个背包蟹笼、所有已加载持久地点的原生合法水格区间，以及每个区间的鱼区、栖息地、垃圾概率、Mariner/Luremaster 修正、原生顺序捕获行和生产签名。编译器绑定精确库存、地点、水格、邻接站位、拓扑指纹、生产签名、放置理由和原生契约；运行时再次执行原生合法性检查。
- 放置复用唯一 `PlaceInventoryObjectNative` 与既有邻接移动；后续收取继续复用已经由 EVD-209 闭合的 `fishing.collect_crab_pots -> executor.collect_crab_pot`，没有第二套蟹笼收取、移动或物品放置系统。该放置 primitive 保持 calibration/evaluation-only，上层策略负责布局、产能与放置理由。
- 隐藏、静默、E 盘隔离运行 `runtime-crab-pot-placement-20260822-152201` 返回 `applied/verified`：目标 `Farm:73,31` 生成精确 base `CrabPot`，owner、空饵料、空产出、未 ready 与库存减一均通过；下一份透明快照读回同一蟹笼及淡水生产签名 `|0.2|freshwater|721,716,722`。
- 复核 EVD-209 与锁定反编译时发现，原冻结分母遗漏了独立的原生上饵动作：`CrabPot.performObjectDropInAction` 接受 `Category=-21` 饵料并写入 owner/bait，它不属于通用机器输入链。现已把 `executor.load_crab_pot_bait` 作为 `catalogued_blocked` 显式补回分母，未伪装成已实现。
- 当时权威对账为 `132 registered / 184 semantic / 131 compiler-bound / 59 five-gate / 36 training allowlist / 0 Product Executor`；full 快照要求 `117` 个状态因子、blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。下一语义切片先闭合 `executor.load_crab_pot_bait`，再进入 `executor.place_fence`。

更新时间：2026-08-22

## 历史记录 · 2026-08-22 当时权威检查点：EVD-256

- `executor.place_cookout_kit` 已闭合透明读取、严格编译、共享邻接移动、原生物品放置和逐字段回执。锁定 1.6.15 的源物品是 `(O)926`，`Object.placementAction` 落地的是 `StardewValley.Torch` / `(BC)278`，并带有 `Fragility=1`、`destroyOvernight=true`；两者不得混作同一个物品身份。
- `player.cookout_kit_placement` 在 full profile 中枚举背包内每个野炊工具和所有已加载持久地点的原生合法区间、布局指纹、当日生命周期及烹饪交接契约。编译器必须绑定精确槽位、堆叠、地点、落点、邻接站位、投影指纹、放置理由和原生契约；运行时仍会重新调用 `Utility.playerCanPlaceItemHere`。
- 机器、储物和野炊工具现在共用唯一 `PlaceInventoryObjectNative` 内核；野炊工具放置后由既有 `player.cooking` 立即识别为 `cookout:location:tile` 来源，没有复制烹饪、移动或布局系统。该底层 primitive 保持 calibration/evaluation-only，只有上层同日烹饪目的才能授权消耗。
- 隐藏、静默、E 盘隔离运行 `runtime-cookout-kit-placement-20260822-105210` 返回 `applied/verified`：库存 `1 -> 0`、目标生成精确 `(BC)278`、当日销毁标志为 true，且透明烹饪端点交接成功。
- 当时权威对账为 `131 registered / 183 semantic / 130 compiler-bound / 58 five-gate / 36 training allowlist / 0 Product Executor`；full 快照要求 `116` 个状态因子、blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。下一切片从 `executor.place_crab_pot` 开始，先审查是否能复用现有蟹笼读取、收取、物品放置和水域边界实现。

更新时间：2026-08-22

## 历史记录 · 2026-08-18 当时权威检查点：EVD-255

- `executor.apply_tree_treatment` 已完成透明读取、严格编译、共享 BFS 邻接移动、原生物品放置与逐字段回执。锁定 1.6.15 的真实语义是 `(O)419` 醋永久禁止一棵树长苔，不是 `(O)805` 树肥；原生分支没有成长阶段限制。
- 隐藏静默隔离运行 `runtime-tree-treatment-20260818-162145` 已验证 `has_moss true -> false`、`stop_growing_moss false -> true` 和醋堆叠减一。生产执行器只调用 `Object.placementAction`，直接树字段写入仅存在于测试夹具。
- 该项保持 executor calibration / evaluation-only，不进入自主候选。上层以后必须提供 `tree_treatment_reason` 和策略授权，不能因为背包有醋、地图有树就自动安排永久处理。
- 当时机器对账为 `130 registered / 183 semantic / 129 compiler-bound / 57 five-gate / 36 training allowlist / 0 Product Executor`；权威 full 快照仍要求 `115` 个状态因子、blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。
- 下一语义切片从重生成后的 `catalogued_blocked` 清单选择；优先检查 `executor.place_cookout_kit` 是否应复用现有物品放置、布局安全与烹饪来源投影，禁止复制机器/储物放置或野炊烹饪执行器。

更新时间：2026-08-18

## 当前权威检查点（优先于下方历史记录）

- EVD-254 已闭合 `crafting.forge_item`：完整快照发布全部已加载原生锻造来源、实时背包工具/戒指与已装备戒指输入、原生碎片成本/返还、统计变化、精确确定性输出及 Diamond/Dragon Tooth 完整随机结果域。显式单次意图经唯一 DailyPlan/队列链进入 `executor.forge_item`，生产执行器只使用原生 `ForgeMenu` 输入与按钮并等待 1600 ms 生命周期。隐藏静默隔离运行 `runtime-forge-20260818-122957` 的九个操作族全部返回 `applied/verified`。
- EVD-253 已闭合 `crafting.cook_recipe`：完整快照发布所有已学配方在每个实时厨房/野炊工具来源上的精确材料消费顺序、主冰箱与原生枚举顺序的小冰箱拓扑、互斥锁、齐氏调味料、输出品质/订单标记和历史烹饪次数。显式单次烹饪意图经唯一 DailyPlan/队列链进入 `executor.cook_recipe`；普通制作、工作台和烹饪共用一个原生 `CraftingPage` 配方点击辅助函数，但厨房保留独立的容器、锁和 `recipesCooked` 语义。隐藏静默隔离运行 `runtime-cooking-20260817-202809` 对厨房银星煎蛋和野炊工具普通煎蛋均返回 `applied/verified`。
- EVD-252 已闭合 `animals.manage_animal`：透明桥发布精确动物、原生查询许可、繁殖、售价、当前家园和兼容家园；显式意图经唯一 DailyPlan/队列链进入 `executor.manage_animal`。隐藏静默隔离运行 `runtime-animal-management-20260816-012959` 对首次抚摸后改名、繁殖开关、搬家和确认出售四支均返回 `applied/verified`。生产执行器只发送原生动物交互和 `AnimalQueryMenu` 点击，不直接写动物或金钱字段；过宽名字在确认前阻塞。
- 当前机器对账为 `130 registered / 183 semantic / 129 compiler-bound / 57 five-gate / 36 training allowlist / 0 Product Executor`；权威 full 快照要求 `115` 个状态因子、blocking `0`，KnowledgeCompiler 为 `585/585`、blocking `0`。

- 锁定版本仍为 Stardew Valley 1.6.15；KnowledgeCompiler 当前为 `585/585` exports、blocking `0`。
- 动作对账当前为 `130 registered / 183 semantic / 129 compiler-bound / 57 five-gate / 36 training allowlist / 0 Product Executor`；
  原生分母仍为 `320 surfaces / 428 branches / 150 map tokens`，三类 blocking 均为 `0`。
- EVD-248 已闭合 `buildings.construct` 的第一个严格范围：模型必须明确给出建筑类型、目标地点和建设理由；
  透明桥从实时 `Game1.buildingData` 与全部原生可建地点读取基础蓝图、Builder、条件、价格、材料、现有与在建
  建筑、服务动作和原生合法落点。候选在上游排除缺意图、条件/资源/落点漂移、在建冲突和材料预留冲突，
  再经 DailyPlan 汇入 EVD-236 已存在的唯一 `executor.construct_building`。隐藏静默隔离运行分别验证了
  无任务策略建造 `runtime-quest-terminal-daily-plan-20260812-105048` 和原 `HaveBuildingQuest` 回归
  `runtime-quest-terminal-daily-plan-20260812-105331`，均由原生 Robin/`CarpenterMenu` 建造 `Farm` 上的
  `Coop`，核对钱、材料、坐标和三天倒计时。证据不外推到 Wizard、升级、换皮或长期建筑策略。
- 最新 full 快照覆盖 `114` 个必需字段，blocking `0`；KnowledgeCompiler 仍为 `585/585`、blocking `0`。
- EVD-246 已闭合 `mining.use_elevator`：透明桥读取玩家最深/当前矿层、入口 `Action=MineElevator`、
  楼层 `Buildings/mine` 索引 112 和精确 `MineElevatorMenu` 条目身份；DailyPlan 复用既有跨图移动、
  `interact` 与 `close_menu`。运行时只点击原生端点和菜单，不直接调用 `enterMine`/`warpFarmer`，并跨帧
  验证最终位置。隐藏静默隔离矩阵 `runtime-mine-elevator-20260812-004601` 为 2/2：25 层回入口及入口回
  25 层均通过。`mining.reach_depth` 仅在实时端点存在且已解锁检查点能推进最终目标时复用该链，并保留
  最终深度为 continuation；其余楼层继续使用原 current-floor planner。
- EVD-245 已闭合 `mail.process_letter`。共享解析器覆盖锁定 `Data/mail` 的 179 封信和 107 条指令，
  解析阻塞为 0；透明桥公开原生顺序队列、玩家实际拥有的邮箱位置、附件容量上界和完整
  `LetterViewerMenu` 状态。DailyPlan 只组合既有移动、`interact` 与 `close_menu`，运行时只发送原生
  菜单输入并核对附件、任务、特别订单和星之果实收据，没有第二套移动/菜单执行器，也不直接写钱、
  配方、任务或最大体力。严格隐藏静默矩阵
  `artifacts/runtime-mail-processing/runtime-mail-processing-20260811-221959/summary.json` 为 5/5；
  新 full 快照为 required 107、blocking 0，KnowledgeCompiler 为 585/585、blocking 0。
- 下一主切片按动作对账中剩余语义依赖继续选择；普通矿井电梯已经闭合，不得再建立平行实现。采石场
  金镰刀洞窟、Skull Cavern 和火山继续保持独立身份，不得混入普通矿井电梯。
- `quest.advance` 的 28 个目录阶段为 `24 bound / 0 blocked / 3 observation-only / 1 native-unreachable`；反编译扫描为
  `12` 种普通任务类型和 `9` 种特别订单目标类型，未发现未登记类型。
- EVD-235 已把任务终端矩阵扩展为 `4/4`：新增普通 `CraftingQuest`，从目的限定的
  `player.quest_crafting` 经 `quest.advance`、DailyPlan、动作队列和既有原生 `CraftingPage` 执行器完成，
  并写入精确任务身份、前后存在/完成状态及 terminal 事实。EVD-236 又闭合普通 `HaveBuildingQuest`：
  `player.quest_building_construction`、候选、DailyPlan 和唯一 `executor.construct_building` 通过原生
  Robin/`CarpenterMenu` 放置建筑，原生扣除钱和材料并生成三天施工倒计时；后续天数复用既有恢复睡眠链。
- `quest.advance` 已因目录零阻塞提升为 `Declared / StepCompilerDeclared / RegisteredOnly`，但没有进入训练白名单；
  目录绑定完成不等于原生运行证据闭环。
- EVD-237 已确认秘密物品取得不是独立任务动作：原版只有任务 128/129，`Railroad.getFish` 在同一
  原生钓鱼事务中返回 `(O)191` 并创建两条任务；透明桥与现有 `fishing.catch_fish` 已覆盖该特殊收获，
  `itemFound=false` 任务行只是事务中的瞬态观察，不得再建第二套任务钓鱼执行器。候选与运行时均复用
  `player.inventory_capacity` 要求至少一个空格；满包必须先走既有存储转移链，避免唯一项链进入未接管的
  `ItemGrabMenu` 后无法重新取得。
- EVD-238 已关闭 type-11 假缺口：锁定 `Data/Quests` 的 66 行没有 Weeding 类型，`Quest.getQuestFromId`
  没有对应工厂分支，原生任务源码没有任何 `questType=11` 写入点；它只是保留的兼容常量。目录使用独立
  `native_unreachable` 状态，KnowledgeCompiler 每次对账复核常量、工厂分支和写入点；旧存档或模组强塞
  type-11 时明确失败关闭，不生成除草执行器。
- EVD-239 已闭合 Junimo Kart 分数的静态主链：真实 full 快照验证
  `current_location.arcade_action_tiles` 可读且带来源；`JKScoreObjective` 绑定 Saloon 街机，复用移动、地图交互和
  `MinecartGame/Endless` 对话原语和唯一 `executor.play_junimo_kart`。经 EVD-307 更新后，所有 AI actor 的默认策略均为 `timed_equivalent`：按既有
  15 分钟平均预算计时 54,000 tick，运行时可加速墙钟，但只通过原生 `MineCart.submitHighScore()` 提交并核对
  `JKScoreObjective`，结果必须标记 `simulated_equivalent`，不得伪装成原生完美游玩证据。
- 原有只发送跳跃输入的 `native_perfect` 控制器完整保留且与等价分数写入隔离；它后续只用于玩家明确触发的完美存档代打。
  只有该模式真实达到 Endless 50,000 分并自然提交，才能登记 Junimo Kart 原生五门证据。等价模式可服务训练、联机陪玩和专用房主，
  但不得冒充原生完美证据；独立 `minigame.play_junimo_kart` 高层项继续后置，不阻塞其他游戏能力。
- 2026-08-11 复核发现 `30,190` 历史运行的 smoke 脚本没有设置 `SMAPI_MODS_PATH`，实际还加载了
  `JunimoTestClient`，因此该制品只保留为受污染诊断样本，不再作为运行验收或回退基线。脚本现使用每次运行独立的
  两模组白名单，并把白名单写入汇总。首个干净矩阵为
  `artifacts/runtime-junimo-kart/runtime-junimo-kart-20260811-002951/`，峰值 `10,940/50,000`，仍为 blocked。
- 当前唯一控制器已加入 `Bubble` 和 `FallingBoulder`/下一次 spawner 的只读轨迹预测，复刻落石原生加速度、
  速度上限和逐轨道反弹顺序；干净矩阵证明这些实体进入运行轨迹。连续跳跃模拟现复刻原生释放时重力归零、
  位移前 `x/x+4/x-4` 落地检测、坡道/冰面/黏液速度倍率和落地帧水平位移。运行
  `runtime-junimo-kart-20260811-011601` 观察 57 次落地，预测与实际 X 的最大绝对误差为 `0px`。
- `native_perfect` 当前干净最高分仍为 `10,940/50,000`；精确落点矩阵峰值为 `9,320`，其中 7/8 次为 theme 0，不能直接作为
  算法回退判断。剩余主缺口是 8 次 planner fallback：需要从原生轨道求下一段可行落地区间，替代固定
  `gap + 18px` 目标，并按主题进行可重复校准。不得用直接改分、改轨道或改任务目标替代控制问题。
- EVD-240 已完成 `quest.accept_daily`：透明桥读取实时 `questOfTheDay`、接受许可、原生任务身份和从 Town
  地图发现的 `Billboard 3` 入口；候选层先做上游许可排除，再按新快照逐连接器接近柜台，终端编译为原生
  Billboard 交互与接受。隐藏静默隔离运行 `runtime-daily-quest-acceptance-20260811-125209` 已验证任务进入
  quest log、`acceptedDailyQuest=true`、原生接受字段和 `daysLeft=2`。新快照基线为 required 103、blocking 0。
  该高层项及原语暂保持 RegisteredOnly，不因一次通过直接进入训练白名单。
- EVD-241 已完成统一的 `quest.accept_special_order` 链：Town、Qi 和沙漠节庆入口共享透明读取、滚动寻路、
  原生开板和精确左右选择，不另建移动或菜单系统。Town 隐藏静默隔离运行
  `runtime-special-order-acceptance-20260811-172636` 已通过，验证原生互斥锁延迟、`Robin2` 的 key/seed/指纹和
  accepted type；新快照基线为 required 104、blocking 0。Qi 与沙漠节庆目前只有反编译和结构覆盖，待独立运行
  校准，因此两项仍保持 RegisteredOnly，five-gate 与 allowlist 不变。
- EVD-242 已完成 `quest.claim_reward`：透明桥实时枚举普通任务日志中的可领取金钱奖励，并用任务 ID、运行时类型、
  标题、奖励、接受日和 daily 标记生成稳定指纹；候选层在菜单非空、身份或金额漂移时上游排除。唯一
  `executor.claim_quest_reward` 构造原生 `QuestLog`、选择精确任务行、点击 `rewardBox`，验证原生
  `OnMoneyRewardClaimed`/`OnLeaveQuestPage` 收据；生产代码没有直接写钱、`moneyReward`、`destroy` 或任务日志。
  隐藏静默隔离运行 `runtime-quest-reward-claim-20260811-195512` 验证 `144755 -> 145505`、奖励 750g 和任务移除。
  最新 full 快照基线为 required 105、blocking 0。
- 最新运行证据：
  `artifacts/runtime-quest-reward-claim/runtime-quest-reward-claim-20260811-195512/summary.json` 和
  `artifacts/runtime-junimo-kart/runtime-junimo-kart-20260811-194648/summary.json`。

## 当前阶段

锁定 Stardew Valley 1.6.15 的动作全集对账和独立分母冻结已经完成，现已转入逐动作纵向
闭环。当前 114 个注册项是可复用的实现基线，不是被废弃的旧代码；63 个已编目未注册语义项
用于记录已证实但尚未实现的能力。正式训练保持阻塞。

## 已完成

- 现有注册表、治理表、编译器注册和 Harness 能力表已做集合一致性校验；
- 取消 `OptionRegistry` 中手写的 31/65 固定计数，改为逐 ID 对账；
- 每个现有动作已归属唯一主执行引擎；
- KnowledgeCompiler 开始生成：
  - `native-action-surface-inventory.json`
  - `native-action-branch-inventory.json`
  - `native-map-interaction-coverage.json`
  - `semantic-action-catalog.json`
  - `action-implementation-reconciliation.json`
  - `action-progress-dashboard.json`
  - `native-action-denominator-fingerprint.json`
- 独立冻结文件 `native-action-denominator-freeze.json` 已与当前指纹核对一致；覆盖状态和
  实现进度不进入分母身份哈希，因此后续把占位动作提升为实现不会伪造“分母变化”；
- 原生方法扫描已改为 Roslyn 语法解析，方法重载按完整签名独立建档；
- 60 个宽入口已展开为 428 条带源码行号和哈希的分支证据；
- 1,102 个有效地图交互实例已归并为 150 个 Action/TouchAction token，并逐项连接
  到原生处理分支。

## 当前任务

当前生成基线位于 `catalogs/vanilla-1.6.15/`：

- 320 个原生输入表面，表面级未分类 0；
- 60 个宽入口全部生成分支目录，428 条分支中待语义审查 0、缺注册 0；
- 150 个地图交互 token 中 142 个映射到语义动作，8 个经原生分支证实为无玩家语义、
  失效/遗留静态 token，待审查 0；
- 语义动作目录共 177 项：114 项已有 `OptionSpec`，63 项为
  `catalogued_blocked`，确认存在但尚未登记的动作数为 0。

机器状态为 `native_action_denominator_frozen`，当前锁定扫描范围已闭合并通过独立审批文件
核对。不能把“已登记”解释为“已实现”：现有代码的编译器孤儿 0、运行 ID 孤儿 0；
Product Executor 仍为 0；EVD-204 回填后，五门证据闭环为 8，训练准入为 7。

## 退出条件

- 原生动作表面未分类数为 0；
- 宽入口分支和有效地图交互 token 未审查数为 0；
- 锁定扫描范围的语义动作分母可确定性重生成并完成治理冻结；
- 所有已证实语义动作均已注册，未实现项必须保持显式 blocked；
- 所有现有代码零孤儿，每个动作只有一个主执行引擎；
- 固定口径看板可重复生成。

## 紧接任务

首个缺口 `inventory.transfer_item` 已完成纵向闭环：强类型明确意图、透明库存图投影、
上游候选、路径站位、日计划展开和既有 `executor.transfer_material` 原语复用均已接通。
EVD-192 在 E 盘隔离存档中验证了“箱子到玩家”和“玩家到箱子”两个方向，均经原生
`Chest`/`ItemGrabMenu`、逐单位右键、互斥锁释放、before/after 数量差分和训练记录；往返后
箱子数量恢复，过期源栈投影在菜单打开前失败关闭且零点击。该项五门证据已登记，可进入其
明确意图范围内的训练；Product Executor 仍未集成，不得把 Harness 闭环称为产品陪玩闭环。
`player.storage_crafting` 与 `player.storage_placement` 的透明性 join 已修复：旧快照稳定复现
94 项 required state factor 中 2 项缺失，当前实时快照为 77 项带完整来源可读、17 项场景性
不可用、0 项阻塞。新安装器会先校验全部 required factors、哈希与版本，再原子更新外部权威
字典的 current 指针；完整 KnowledgeCompiler 已以该指针达到 585/585 exports、blocking 0。
复核确认 `recovery.stabilize_day` 的全部当前候选到日计划/队列编译链早已完成，普通社交对话和
送礼也共用唯一 `executor.social_interact` 原生 Harness 执行器，不得重复实现第二套。EVD-195 已在
隐藏、静音的 E 盘隔离存档中闭合 `Farm@2200 -> 单连接器回家 -> 新鲜快照重规划 -> 原生睡眠 -> 新日`，
因此 recovery 五门已通过，但它仍是校准型高层动作，不进入策略训练。EVD-196 又通过现有社交链
完成 Abigail 的实时远端滚动追踪：35 轮中 31 个连接器动作和一次原生送礼均验证通过，普通礼物
`(O)388` 严格由栈 1 变为 `null`，且未读取未来日程。`social.gift_npc` 现仅在“当前已加载原版 NPC、
同图或滚动连接器追踪、普通单件礼物”范围内五门闭合并进入训练准入；不得外推为模组 NPC、特殊
物品或全部社交完成。下一切片按既定路线重建准入策略轨迹并接入 C# 结构化排序器，不重做候选、
编译器或社交执行器。

正式轨迹与数据治理硬闸已经完成：`PolicyTrainingAdmissionFilter` 直接消费生成式 allowlist，
校准行与未准入行分开计数；每条 v2 轨迹绑定 effective ranking、完整源候选、版本化状态特征、
编译队列、执行结果、fresh after-state 和观察型长回报。清洗器按存档/游戏日确定性切分，拒绝
冲突标签并生成逐文件 SHA-256 manifest。旧 `BaselineFeatureRowTrainer` 仍只作聚合烟测；正式路径
使用下节的 C# 结构化提供器。

## 历史记录 · 2026-08-02 正式模型链状态

EVD-201 已完成首个真实 C# 结构化策略提供器。轨迹 schema 升级为
`policy_decision_trajectory.v2` / `policy_features.v2`，每条轨迹除版本化状态特征外，还保留
完整源候选对象，商店、位置、物品、价格、开放时窗、排程、原因、参数与结构化效果字段不会在
训练前丢失。训练器只对“已准入且当时可用”的候选建立成对比较，检查点绑定数据清单及三个分区
SHA-256、特征/候选/能力/字典/编译器/执行器版本；推理只重排既有候选，不复制候选生成、日计划、
编译器或执行器。`--require-structured-policy` 在检查点缺失时失败关闭。

当前标准 E 盘生产轨迹、跨度观测、正式 manifest 和检查点四个路径均不存在。因此完成的是模型
基础设施与合成契约验收，不是生产训练。直接当时后续计划仍是按权威字典依赖顺序扩大五门准入范围，
再用真实、verified/fresh 的长期 rollout 生成 v2 轨迹和闭合跨度标签；形成正式 manifest 后才运行
`StardewAI.PolicyModel`。只有独立存档评测与第三年 21 分长跑通过后，才冻结“最强完美 AI”基线。

EVD-202 没有新增第二套矿洞候选、编译器或执行器，而是把已有 EVD-106 运行证据登记到
`mining.obtain_skull_key` 的五门：范围严格限定为普通矿井 119 -> 120 层、原生骷髅钥匙宝箱领取、
`has_skull_key false -> true` 与原生退出。该目标现进入训练白名单；沙漠矿洞、采石场矿洞金镰刀和
火山矿洞仍是独立族，未被本证据放行。当时五项准入为 `inventory.transfer_item`、
`mining.obtain_skull_key`、`mining.reach_depth`、`social.gift_npc`、`social.talk_npc`。

EVD-203 随后独立登记 `volcano.reach_caldera`：EVD-190 的火山 0..9 -> Caldera 完整原生滚动链
记录 106 步、82 次 applied/verified 和 24 次安全重规划；EVD-191 的战斗目的链记录 66 步、
27 次验证动作和 39 次安全脱离/重规划。两份制品均无非新鲜快照或未变化状态。模型只决定是否选择
“到达 Caldera”这个高层目标；浇岩浆、清石、战斗、移动、门和连接器仍由确定性候选/编译/执行链
完成。普通矿井、沙漠矿洞、采石场矿洞金镰刀与火山矿洞证据继续互不借用。当时六项准入为
`inventory.transfer_item`、`mining.obtain_skull_key`、`mining.reach_depth`、`social.gift_npc`、
`social.talk_npc`、`volcano.reach_caldera`。

EVD-204 复核并登记 `skills.read_books`。能力目录此前只识别动作队列直接编译器，漏记了现有
`DailyPlanCompiler` 的 `read_inventory_book -> executor.read_book -> wait_ticks` 展开，因此错误显示
`compiler=unbound`；现已用同一治理入口登记日计划 option compiler，没有新增读书执行器。EVD-124
七用例矩阵覆盖六类原版基础书籍分支，全部 applied/verified。当前 compiler-bound 为 77，五门闭环
为 8，训练准入为 7；新增准入项是 `skills.read_books`。自定义 `performUseAction`、畸形模组标签和
原版证据范围外分支继续失败关闭。

## 历史记录 · 2026-08-11 职业选择闭环（EVD-244）

`skills.choose_profession` 已从恢复链中的隐式自动处理提升为正式语义动作。透明桥同时公开当前
`LevelUpMenu` 的两个原生职业选项、精确 ID、标题、描述，以及玩家持久职业列表和待处理升级列表；候选层只在
两个选项均完整可读时开放，并把同一选择界面的两个候选登记为互斥决策。DailyPlan 只把所选职业编译到既有
`close_menu -> executor.close_menu`，没有新增第二套职业或菜单执行器。

锁定版反编译确认 5 级分支为每技能 `skill * 6 + 0/1`，10 级分支按已有 5 级职业选择 `+2/+3` 或 `+4/+5`；
运行回执记录选择前后的职业、待处理升级、生命、体力等即时变化。隐藏静默 E 盘全矩阵
`runtime-profession-choice-20260811-203159` 覆盖原版 30 个职业 ID，修正前置即时 perk 的战斗复核矩阵
`runtime-profession-choice-20260811-203610` 为 6/6 通过，并验证 Fighter `+15` 与 Defender `+25` 最大生命。
最新 full snapshot schema 为 105 required、88 实时带来源可读、17 场景性、blocking 0；动作对账为
114 registered / 177 semantic / 113 compiler-bound / 63 catalogued-blocked，five-gate 为 40，训练准入为 27。

直接下一主切片是 `mail.process_letter`。`mining.use_elevator` 必须先按普通矿井既有移动、菜单和楼层切换链做
复用对账，只有证明存在未覆盖的原生分支才允许新增实现。

## 禁止事项

- 不把 97 当作总动作数；
- 不把 Harness dispatch 当作 Product Executor；
- 不因独立架构重构暂停动作主线；
- 不开始短训或正式训练；
- 不启动游戏，除非当前动作已完成静态和单元测试且明确进入运行验收。

## 历史记录 · 2026-09-01 当时权威检查点：EVD-327

- Product bootstrap 隐藏静音运行 `product-bootstrap-20260901-013441` 已在 E 盘 Spring 4 隔离副本上通过；3 条真实 Product v2 轨迹全部被数据集接收，前两条分别含 107/143 个准入候选。
- 首个结构化 bootstrap checkpoint 已由 3 条轨迹和 248 个比较对生成，执行器版本固定为 `product_executor.v1`。validation/test 仍为 0，所以它只是正式循环初始权重，不是训练质量结论。
- 正式 launcher 已新增 manifest 级 `save_slot` 与存档文件校验，并显式传递自动加载环境变量；正式进程不能再停在主菜单却被计为训练。
- Core `2193/2193`、Backend `166/166` 全量回归通过；校准结束后 SMAPI 与 8765/8767/8768/8795 均无残留监听。
- 当时后续计划固定为：种入标准正式轨迹路径，prepare/launch 同一份 `training_run_manifest.v2`，完成一个真实游戏日的 Product 训练并验证 checkpoint/hash 更新；之后部署到 119 的全量训练实例，再补非训练动作。
## 历史记录 · 2026-08-12 当时权威检查点：EVD-249

- `buildings.change_skin` 已完成透明读取、上游候选、DailyPlan、共享动作队列、类型化请求、原生 Robin/CarpenterMenu 执行与严格回执。
- 隔离运行 `runtime-quest-terminal-daily-plan-20260812-122957` 为 `applied/verified`：Pet Bowl 默认皮肤通过一次最短 `next` 切换到 `Stone Pet Bowl`，返回 ScienceHouse，并验证三组油漆颜色重置为默认。
- 当时动作对账：122 registered、180 semantic、121 compiler-bound、49 five-gate、32 training allowlist、0 Product Executor；320/428/150 原生分母 blocking 均为 0。
- 当时 full 快照 required 112、blocking 0；KnowledgeCompiler 585/585、blocking 0；Core 1663/1663、Backend 121/121。
- 下一切片为 `buildings.paint`。它必须复用现有 Robin 服务、Carpenter 建筑选择和菜单退出链，只新增实时颜色参数、上游外观意图约束和 `BuildingPaintMenu` 原生滑杆/回执，不得新增平行建筑菜单执行器。

## 历史记录 · 2026-08-12 当时权威检查点：EVD-250

- `buildings.paint` 已完成透明读取、上游许可、DailyPlan、共享动作队列、类型化请求、原生 `BuildingPaintMenu` 控件与严格回执。
- 透明桥公开每栋可涂装建筑的一至三区域、原生 H/S/L 范围、当前值、默认标志、权限、Robin 服务入口，以及 284 像素滑杆的精确鼠标可达整数集合；上游拒绝不可达、无效果和默认显示值无法解除默认标志的目标。
- DailyPlan 生成 `paint_building_region`，但动作队列继续映射到唯一 `executor.change_building_skin`。共享 `ActiveBuildingAppearanceChange` 复用 Robin、Carpenter、建筑选择和退出生命周期，只在子菜单内部按冻结参数分流；不存在第二套 Robin 状态机。
- 隐藏静音隔离运行 `runtime-quest-terminal-daily-plan-20260812-133245` 已通过：Farmhouse `Building` 区域原生点击到 H180/S37/L-30，目标精确匹配，另外两区域保持默认，训练行落盘。
- 当时为 123 registered / 180 semantic / 122 compiler-bound / 57 catalogued-blocked；full snapshot 113 required、96 带来源可读、17 场景性、blocking 0；KnowledgeCompiler 585/585、blocking 0；Core 1666/1666、Backend 121/121。
- 下一切片应从剩余 57 个 `catalogued_blocked` 动作中，按权威字典依赖、已有机械引擎复用和可形成严格原生回执的顺序选择；不得以动作数量为理由复制执行系统。

## 历史记录 · 2026-08-12 119 展示循环检查点：EVD-251

- 119 展示房间已改为版本化发布，继续使用原存档、原版联机房主、公开 UDP 24642 和既有可加入小屋；正式训练仍保持关闭，展示循环只运行目的受限的已实现候选集。
- `daily` 快照现有精确域校验；无手持物品以可读空字符串表达，不再把“没有 ActiveObject”误判为字段不可用。队列只执行首个阻塞项之前的连续可执行前缀，控制层 HTTP 超时不早于执行器预算。
- 无窗口房主通过原生 `IInputSimulator` 消费现有移动租约，不写坐标、不注入动作键；通用移动 BFS 避开可移除障碍，清障继续由唯一 `executor.clear_obstacle` 原语负责。
- 锁定版反编译确认多人 `Game1.shouldTimePass()` 由 `netWorldState.IsTimePaused` 决定。专用原版 AI 房主只在原生事件、菜单与显式暂停门槛均允许时清除残留联网暂停位，不直接写 `timeOfDay`。119 实测 58 秒内 `08:20 -> 09:40`，最终版本再次实测 `06:50 -> 08:10`。
- 服务器闭环先连续形成 6 条 applied/verified/fresh 执行记录；修复后无实际候选时只生成空队列并指数退避，不再以 `recovery:refresh_plan_after_stabilization` 制造永久 `wait_ticks` 样本。建筑施工中的等待仍由 `quest.advance` 在透明状态明确为 `construction_in_progress` 时局部复用。
- 既有晚间恢复链在 119 完成 `22:00 -> 回家/睡觉 -> NewDay`。三项服务为 server healthy、planner healthy、host-ai running，透明 `daily` 快照完整且 unavailable 0；这证明展示日循环主体成立，不等于完整动作全集、Product Executor 或正式训练完成。
- 本地最终回归：Core 1671/1671、Backend 122/122、Release solution build 0 errors；保留 1 个既有 `AvoidNetField` 警告。
- EVD-254 已完成上述 `crafting.forge_item` 切片，未建立第二套移动、库存或菜单系统。下一主开发切片为 `executor.apply_tree_treatment`：复用既有树木定位、移动、站位和工具/物品交互基础设施，先按锁定 1.6.15 原生分支明确适用树种、物品消费、成长状态变化、许可与严格回执，再决定其高层目的归属；不得把底层处理动作直接当作策略候选。

## 历史记录 · 2026-08-31 当时权威检查点：EVD-317

- `rewards.claim_adventure_guild_reward` 已完成透明读取、自治正收益候选、DailyPlan、fresh 编译重绑、类型化传输、共享 BFS、原生 Gil 交互、可选对话、整批奖励菜单点击和严格回执。小模型只输出无参数的“领取已获得奖励”，整批身份和所有机械字段由当前快照绑定。
- 锁定版反编译确认 `gil()` 会在一次交互中枚举全部已完成且未领取的怪物讨伐目标，并在奖励菜单出现前立即安排全部邮件/标志副作用。因此上游必须用克隆背包和原生 `Utility.addItemToThisInventoryList` 证明整批容量，不能把目标拆成逐条领取，也不能等下游菜单卡住再恢复。
- 隐藏静音 E 盘运行 `runtime-adventure-guild-reward-20260831-023014` 为 3/3：伪造批次指纹拒绝、背包容量漂移拒绝、真实原生批次领取成功。成功用例新增一个 `Gil_<goalId>`、收取一个精确物品并使 pending 归零；生产代码不直接写背包、邮件、标志或击杀统计。
- 当时 full 快照为 165 required / 148 带来源可读 / 17 场景性 / 0 blocking；KnowledgeCompiler 585/585、blocking 0；动作看板为 214 registered / 223 semantic / 213 compiler-bound / 138 harness dispatch / 137 five-gate / 56 training allowlist / 9 catalogued blocked / 0 Product Executor。
- 最终回归为 Core 2138/2138、Backend 155/155、Release 0 warnings / 0 errors。原生分母为 322 surfaces / 448 branches / 150 map tokens，三类阻塞均为 0，冻结指纹为 `10b8329f92466d34ce5570679fea0096b298a36693668e8ff107be1794804902`。
- 下一主切片是 `rewards.claim_prize_ticket`。先锁定 `PrizeTicketMenu` 的批次、随机性、容量、菜单与副作用语义，再复用现有路线、菜单输入和收据基础设施。`minigame.play_junimo_kart` 按既有决策继续暂缓，不得在下一轮误当主切片。

## 历史记录 · 2026-08-31 当时权威检查点：EVD-318

- `rewards.claim_prize_ticket` 已完成透明读取、上游许可、DailyPlan、两阶段 fresh 重规划、类型化传输、共享 BFS、原生地点交互、原生 `PrizeTicketMenu` 点击和严格回执。模型只输出无参数的“领取当前奖券奖励”；票券来源、当前奖励等级、精确奖励指纹、地点、站位、库存或 debris 结算均由当前快照和编译器负责。
- 锁定版反编译确认：持有 `(O)PrizeTicket` 时直接前往 ManorHouse `PrizeMachine`；仅有 `specialOrderPrizeTickets` 待发数量时，先在 Town 的 `SpecialOrdersPrizeTickets` 原生端点领取一张实体票券，然后必须用新快照重规划。兑换只领取 `currentPrizeTrack[0]`，背包可接收时进入库存，否则生成原生 debris；随后票券减一且 `ticketPrizesClaimed` 加一。等级 `0..21` 和之后的 9 级循环均已冻结。
- 隐藏静音 E 盘运行 `runtime-prize-ticket-reward-20260831-121025` 为 6/6：伪造投影拒绝、Town 待发票券领取、等级 0、升级房屋等级 5、满背包等级 21 debris、等级 22 循环均通过。生产执行器不直接写票券、奖励、统计或背包。
- 本轮运行还暴露并修正了透明观察器副作用：Cooking、Forge、SpecialOrderBoard、Building 与 FarmHouse 上下文只读取已经加载的地图/床/入口状态，不再因完整快照访问 `Map` 或房屋资产懒加载而改变运行环境。异常多人房屋组合也能生成完整只读快照。
- 当时 full 快照为 166 required / 149 带来源可读 / 17 场景性 / 0 blocking；KnowledgeCompiler 585/585、blocking 0；动作看板为 216 registered / 224 semantic / 215 compiler-bound / 139 harness dispatch / 139 five-gate / 57 training allowlist / 8 catalogued blocked / 0 Product Executor。
- 最终回归为 Core 2145/2145、Backend 155/155、Release 0 warnings / 0 errors。原生分母保持 322 surfaces / 448 branches / 150 map tokens，三类阻塞均为 0；冻结指纹为 `7327c9af60be86fc79d7aff82a33af4ad09ca0aa58398d2bebed1affa5ae6f67`。
- 下一主切片是 `skills.claim_mastery`。先锁定五种精通领取资格、菜单入口、星级/经验结算、奖励与一次性标志，再复用现有路线和菜单输入链。`minigame.play_junimo_kart` 继续暂缓；不得把 8 个显式 blocked、0 Product Executor 或尚未开始的正式全量训练描述为完成。

## 历史记录 · 2026-08-31 当时权威检查点：EVD-319

- `skills.claim_mastery` 已完成透明读取、五分支战略候选、DailyPlan、跨图 fresh 续接、类型化传输、共享 BFS、原生 `MasteryCave.checkAction`、原生 `MasteryTrackerMenu.mainButton` 和严格回执。小模型只在当前可领取的技能中选一个；路线、碑牌、站位、奖励和菜单操作全部由编译器与执行器展开。
- 锁定版反编译确认入口要求五项基础技能均达到 10；MasteryExp 阈值依次为 10000/25000/45000/70000/100000。五块碑牌分别对应耕作、钓鱼、采集、采矿和战斗，领取会原生增加所选 `mastery_N` 与 `masteryLevelsSpent`，授予精确配方/直接物品，满背包时直接物品落为 debris；战斗额外设置一个饰品槽，第五块触发完成态。
- 隐藏静音 E 盘矩阵最终连续两轮 6/6：`runtime-mastery-claim-20260831-130708`、`runtime-mastery-claim-20260831-130839`。覆盖伪造投影、耕作直接物品、钓鱼满背包 debris、采集双配方、采矿双配方、战斗饰品槽和第五碑牌。生产执行器不直接写精通统计、配方、背包、饰品槽或终局状态。
- 首轮 5/6 暴露了无关掉落物数量进入全局指纹导致的误阻塞。修正后全局指纹只锁技能/碑牌/点数结构，所选分支的奖励基线继续由独立 option 指纹严格锁定；这不是放宽状态校验。
- 当时 full 快照为 166 required / 149 带来源可读 / 17 场景性 / 0 blocking；KnowledgeCompiler 585/585、blocking 0；动作看板为 218 registered / 225 semantic / 217 compiler-bound / 140 harness dispatch / 141 five-gate / 58 training allowlist / 7 catalogued blocked / 0 Product Executor。
- 最终回归为 Core 2154/2154、Backend 155/155、Release 0 warnings / 0 errors。原生分母保持 322 surfaces / 448 branches / 150 map tokens，三类阻塞均为 0；冻结指纹为 `c5ba5f114d07060305187ab7266e26ae13c8b24fb2ed9177d5f06f86b4fbe1f4`。
- 下一主切片是 `social.emote`。先锁定原生表情候选、触发权限、多人可见副作用和玩家指令/自治边界，再复用现有社交定位及输入链。`minigame.play_junimo_kart` 继续暂缓；正式全量训练仍未开始。

## 历史记录 · 2026-08-31 当时权威检查点：EVD-320

- `social.emote` 已完成实时透明读取、显式玩家指令候选、DailyPlan、fresh 编译重绑、类型化传输、原生聊天输入和严格回执。小模型/玩家只提供准确表情 key、理由与确认；机械层生成并输入完整 `/emote <key>`，不需要路线，也不复制第二套社交执行器。
- 锁定版反编译确认 `Farmer.EMOTES` 恰有 22 项，其中 `blush/taunt/music/jar` 四项隐藏；透明桥实时发布顺序、显示键/名称、图标、方向、逐帧动画、raw favorites、无副作用推导的八项 effective defaults、performed 状态、selector 可见性和全部 `CanEmote`/聊天/菜单/小游戏门槛。锁定目录顺序或隐藏集合漂移时整体失败关闭，读取侧禁止调用会补写默认值的 `GetEmoteFavorites()`。
- 生产运行只激活原生 ChatBox、逐字符输入并调用 `ChatBox.textBoxEnter`，后续由 `ChatCommands.Emote -> Farmer.netDoEmote -> doEmoteEvent -> performPlayerEmote` 负责联网与结果；禁止直接调用后三者或写 `performedEmotes`。本地回执核对 performed entry 及准确图标/动画；远端可见性归原生网络事件所有，不伪造远端收据。
- 隐藏静音 E 盘矩阵 `runtime-player-emote-20260831-145638` 为 23/23：一项伪造投影被拒，22 个原生表情逐一成功。两项 Option 都是 `player_command_only`、需显式确认，并从默认自主候选与策略训练中排除；训练 allowlist 保持 58。
- 当时 full 快照为 168 required / 151 带来源可读 / 17 场景性 / 0 blocking；KnowledgeCompiler 585/585、blocking 0；动作看板为 220 registered / 226 semantic / 219 compiler-bound / 141 harness dispatch / 143 five-gate / 58 training allowlist / 6 catalogued blocked / 0 Product Executor。
- 最终回归为 Core 2162/2162、Backend 155/155、Release 0 warnings / 0 errors。原生分母保持 322 surfaces / 448 branches / 150 map tokens，三类阻塞均为 0；冻结指纹为 `d145570835f06f8ffb14460ce1107950a4aed243939ed60ee08c583bc17a97e9`。
- 下一主切片固定为 `social.watch_movie`。先反编译锁定购票/邀请/放映/同伴反应/奖励与一次性状态，再判断哪些选择属于模型、哪些机械流程归既有社交/商店/菜单执行层。`minigame.play_junimo_kart` 继续暂缓；正式全量训练仍等待剩余动作、Product Executor 与长线验收。

## 历史记录 · 2026-09-05 当时权威检查点：r32 round07

- 部署时提交 `e0a6221` 把跨日完成门改为必须观察到 `newDaySync.hasSaved` 且 `SaveGame.IsProcessing=false`；`af432ed` 修正透明桥把隔夜系统对话误报为睡眠确认框的问题，并仅用原生输入推进安全的非角色隔夜对话。远端整合后的公开 `main` 等价提交分别为 `bcc0bc0`、`d881555`。
- Core `2273/2273`、Backend `171/171`、Release solution build 为 0 warnings / 0 errors。
- 119 上的 `train.server.20260905.r32.plan07` 在并发 1 下完成 3 次外层迭代：1 次主动作 verified，2 次原生存档边界推进，Summer 2 → Summer 3；SMAPI 日志明确记录 `SaveGame.Save() completed without exceptions.`。
- 存档树 SHA-256 从 `7822c135afa09a355fbed3ce1462784d1551fdf8cfdf81ae4efebd95fcba31a3` 变为 `a4af6a79e6138085b07e7c63c7977fdc1c12e1bf34df28955c6a7614816af27b`，训练事务为 `committed_after_native_save_boundary`，canonical 制品已更新。
- 正式数据集为 accepted 200 / rejected 0，train / validation / test = 142 / 5 / 53，train pairs = 4367；checkpoint SHA-256 为 `4f937ec73f2a0f58bdac00ff9345fd4fbcc201010d627b53939a132357a2181f`。
- 完整证据已归档到 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r32-round07-20260905-011000`；远端与本机 146 / 146 文件逐一 SHA-256 一致。服务器上的正式训练、游戏与执行器进程随后已停止，既有观察隧道不属于训练进程。
- round05/round06 均保持 `staged_not_committed`，没有更新 canonical：前者暴露保存完成判定过早，后者暴露 Summer 2 地震隔夜对话。它们是失败诊断证据，不得计为训练成功轮次。
- 该结果只解除“正式事务无法跨原生存档边界提交”这一硬阻塞，不代表全量训练或完全体陪玩完成。当时后续计划是以 Summer 3 存档和上述 canonical 哈希创建下一份有界计划，继续真实 Product rollout；每轮必须在退出前确认无未解决 Product pending、原生存档已持久化、数据集/checkpoint/manifest 哈希一致并完成本机归档。

## 历史记录 · 2026-09-05 当时权威检查点：r33 round09

- round08 在执行动作前被 `formal_checkpoint_dataset_binding_mismatch` 拒绝。根因是 round07 把 staging 数据文件复制到了 canonical，但 manifest 和 checkpoint 内容中的绝对路径未重绑定；该轮未启动 Product Executor/训练循环，也未修改 canonical，7 个失败控制制品已完成本机 SHA-256 归档。
- `7b1da8d` 修复事务提升：所有 manifest digest 路径必须位于 staging 或 canonical 数据根，提升后统一重绑定到 canonical；manifest 哈希、checkpoint ID/绑定和最终训练报告随之重算。新增回归直接证明提升后的产物满足下一轮绑定门。
- 一次性 canonical 重建保持 200 accepted / 0 rejected、142 / 5 / 53 和 4367 pairs 不变；6 个 manifest 数据摘要逐一验证，修复证据 8 / 8 文件远端与本机哈希一致。
- `train.server.20260905.r33.plan09` 随后直接通过 prepare，并以并发 1 完成 3 次迭代：同一高层队列的邮件与两个装箱候选形成 3 条 fresh/success 轨迹，睡眠边界不调用策略模型、不写控制面轨迹。
- 原生存档从 Summer 3 推进到 Summer 4，SHA-256 从 `a4af6a79e6138085b07e7c63c7977fdc1c12e1bf34df28955c6a7614816af27b` 变为 `84872422a42380d669856693052cf58606273b58aed20566a9732c9751c69493`；日志明确记录一次无异常原生保存。
- canonical 数据变为 accepted 203 / rejected 0、train / validation / test = 145 / 5 / 53、train pairs = 4547；checkpoint / manifest SHA-256 为 `4247b9feed96fbb40fbe263dd6f260c006d8f2db96c28b4a21bc0b5ffc717eeb` / `b35080e21a10ae61109df1577a4e6534fa2e60150917e3d75671d8d8734c45d5`。
- 完整归档位于 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r33-round09-20260905-015829`，远端/本机 142 / 142，三类差异均为 0。Core `2274/2274`、Backend `171/171`、Release 0 warnings / 0 errors；正式训练进程已停止。
- 当时后续计划是从 Summer 4 与当前 canonical 哈希开始连续有界批次，并观察队列失效、长期回报、JSONL 恢复和累计 I/O；在跨季、跨年、Grandpa 21 与 Companion 适配验收前仍不得宣称全量训练完成。

## 历史记录 · 2026-09-21 当时权威检查点：目标日期路线组合与原子 reservation

- 新增 `acquisition_route_portfolio_proposal.v1` / `acquisition_route_portfolio_admission.v1`。调用者必须明确本批覆盖的 requirement group 和精确路线 occurrence；上游逐组执行 `all_required` 或 `choose_at_least_required_slots`，每个已选 alternative 只允许一条路线，且所有路线必须仍位于完整目标日期 Pareto 前沿。
- 组合成本继续保留非标量维度：时间、体力、精确物品/品质/实时售价与各原生货币分别聚合，不把不可比较路线强行压成单一分数，也不把调用者提案冒充独立 Teacher 偏好。
- Backend 新增唯一原子入口 `POST /api/v1/strategy/commitments/reservation-portfolios/commit`。显式释放项及全部 material/currency claim 先在内存 staging ledger 中校验；任一后项失败即整组丢弃，成功时在 repository 锁内只保存一次、ledger 只前进一个 revision，全部明细和 portfolio marker 使用同一 revision。
- Backend 全量回归 198/198；76 路线 Bootstrap 回归通过真实商店路线的材料 + 金钱联合 claim，并确认错误 requirement scope 在上游被拒绝。完整解决方案仍受本机 `f:/steam/.../Stardew Valley` 缺少 SMAPI 的既有环境门槛阻塞，非本提交编译错误。
- 正式训练仍关闭。下一固定切片是 post-commit receipt：重算提交前 admission，核验提交后 active claim 精确集合与 `base revision + 1`，再把该已提交 revision 强制绑定进每条 route execution binding；未完成前不得把“可原子提交”描述成“可正式 rollout”。

## 历史记录 · 2026-09-22 当时权威检查点：首个路线结算滚动检查点

- `acquisition_route_portfolio_settlement_receipt.v1` 已闭合完成路线的原子 reservation 结算：从已验证执行绑定和 fresh terminal receipt 派生精确请求，核对所有 completed 行、历史、route marker、单 revision 前进和带 marker 时间戳的精确重放。结算回执新增 `after_state_hash`，后续控制层不必从非权威旁路猜测终态。
- 新增 `acquisition_route_portfolio_rollout_checkpoint.v1` 与 `build-acquisition-route-portfolio-rollout-checkpoint`。首个检查点会重建当前 Teacher 选择与路线结算，把已完成 occurrence 映射回权威 requirement alternative，并按 `all_required` / `choose_at_least_required_slots` 计算已完成 alternative 和剩余槽位。
- 只有全部 scope 已满足、当前 Teacher 选中的路线全部完成、且这些路线没有遗留 active reservation 时，检查点才输出 `verified_initial_portfolio_completion`。单路线商店夹具已证明该路径；两项 `all_required` 只完成一项的纯回归会保持 `remaining_required_slots=1`。
- 未完成或多路线组合不会沿用旧 route ID，而是输出 `fresh_replan_required=true`。首检查点本身不授权下一路线；下述 continuation 边界已经闭合“累计完成 alternative -> 最新 state/ledger 剩余 Teacher 分母 -> 提交回执 -> 下一路线派发 -> fresh 终态 -> 结算 -> 下一检查点”。线性 proof manifest 可按同一实现重复任意轮次；源 proof/checkpoint 始终保持 `formal_training_authorized=false`，不能自我越权。
- `acquisition_route_portfolio_continuation_teacher_request.v1` 及两个 continuation CLI 已实现。请求必须先精确重建首检查点，并把其哈希、终态、结算后 ledger 哈希/revision、累计完成 alternative 和 transition count 一并绑定；已完成组合禁止生成请求。Teacher 只在内部排除已完成 alternatives、减少剩余 required slots，外部普通 proposal 填写完成项会被 `unverified_portfolio_continuation_evidence` 拒绝。
- 续轮候选继续复用同一完整有界组合、严格 Pareto、原子 admission/preflight 和成本聚合实现，没有第二套规划器。纯回归证明 `choose_at_least` 与 `all_required` 都不再枚举已完成项，原有 76 路线聚焦回归仍通过，Release 为 0 warnings / 0 errors。
- continuation Teacher 选出的 proposal/admission 现可经对应 continuation CLI 进入既有提交、派发、fresh 回执、结算和累计检查点证明。旧首检查点参数保留兼容；新 `acquisition_route_portfolio_rollout_proof_manifest.v1` 从首 proof 开始逐项重建全部 continuation，后续各层只消费同一个已验证 latest checkpoint 核心，不存在第三套 builder。
- 回归发现并修正了无 claim 路线的旧硬伤：过去“ledger 不变”的回执没有新 portfolio commit marker，Backend 必然拒绝后续结算。现在每个 admitted portfolio 都执行一次原子提交；无 material/currency claim 时只写唯一 ownership marker、前进一个 revision，不放宽 settlement 门禁。
- 双路线离线正例已通过：商店路线先完成并结算，首检查点留下钓鱼路线；fresh state/settled ledger 只枚举未完成钓鱼 scope，marker-only 提交后执行和结算钓鱼路线，第二检查点以 `transition_count=2` 精确证明两个 scope 全部完成。Release 构建为 0 warnings / 0 errors，聚焦四收集/76 路线自测及 Core reservation portfolio 单测通过。
- `acquisition_route_portfolio_rollout_proof_receipt.v1` 已绑定 manifest 哈希、latest checkpoint 哈希、transition count、终态 state/ledger 和完成状态。除原双路线正例外，现有三路线正例会依次完成商店购买、透明成熟作物收获和钓鱼，并在每次 fresh replan 后重建完整目标日链及与当前 tick 对齐的钓鱼预测，最终以 `transition_count=3` 通过 proof/admission；篡改 checkpoint、伪造 receipt 和不完整链仍失败关闭。
- `acquisition_route_portfolio_rollout_admission_receipt.v1` 已闭合作用域限定的 rollout controller 准入。controller 会从 manifest 重算整条有序 proof chain，并要求调用方提供的 proof receipt 与重算结果逐字段一致；只有 `proof_chain_verified=true`、`portfolio_completion_verified=true` 的终态组合可获得 `teacher_training_evidence_eligible=true`。未完成链返回 typed blocker，伪造 receipt 失败关闭。
- 成熟作物路线现复用 `executor.harvest_crop` 的同一 `CropHarvestBudgetPolicy`，只在透明逐格作物、权威最低产量和单地块目标共同证明完整需求时进入每日时间预算；多地块局部路线尚未有完整证据时继续失败关闭，不用任意常数放行。
- `acquisition_route_portfolio_supervision_dataset.v1` 现把已准入终态 portfolio 转为逐 transition、哈希链接的数据行。构建器不信任调用方标签：它重新计算 proof 与 rollout admission，要求两份外部 receipt 逐字段一致，再逐段绑定 decision snapshot/ledger、完整 Teacher 候选分母与严格 Pareto 选择、实际 queue execution、fresh terminal、settlement 和 checkpoint。
- 每行显式分开 `teacher_preference`、`native_outcome`、`student_observation`。Teacher 通道保留完整候选评估、pairwise preference、选中 proposal/route/cost，且不可用候选固定为 `defer_without_negative_label`；native 通道只记录 fresh 原生执行结果；本次 Teacher rollout 没有 Student 参与，所以 Student 通道明确为 `not_observed_teacher_rollout`，不会从 Teacher 执行伪造 Student 正例。
- 只有终态 controller admission 才能生成数据；未完成链、伪造 admission、调用方篡改数据集均失败关闭。产物仅为 `teacher_training_evidence_eligible=true`、`formal_product_training_authorized=false`，不绕过 option allowlist、正式 dataset/checkpoint、Product Executor、版本锁和原生存档边界。
- 每行还从同一已验证 decision snapshot 提取 `decision_context`：game/bridge version、save/player、年月日、time、total day、tick 和不可由调用方填写的 `save_id:year:season:day` split key；日期轴、顶层时间与 FieldEnvelope 可读性不一致时失败关闭。
- `acquisition_route_portfolio_supervision_corpus_request.v1` / `...corpus_manifest.v1` 及 `build-acquisition-route-portfolio-supervision-corpus` 已实现多 rollout 治理。每个源重新执行 dataset/proof/admission 验证；相同源只在本次构建内缓存计算但仍保留输入审计，相同行精确去重，冲突 row ID 失败，cleaned/train/validation/test JSONL 按权威 save-day 哈希分割并逐文件记录 SHA-256、bytes、rows 和 split-key count。
- `acquisition_route_target_date_daily_time_energy_budget.v2` 已把单格与多格成熟作物统一到同一类型化终端路线：按权威最低产量计算精确作物格数，逐段重证 BFS/时序，并保留每一步站位、抵达、动作和完成时刻；任一地块不可达或超出来源时间窗都会失败关闭。当前正例以 2 格 Parsnip 路线验证，不再用“单格常数”代替多格移动证明。
- continuation fixture 现暴露 3 个完整 portfolio：2 个高品质 Parsnip、1 个 Green Bean、两者都做。确定性 Teacher 严格选择 Green Bean 方案并产生 2 个真实 pairwise preference；没有 learner score，也没有伪造负例。两份重复输入仍得到 `6 input / 3 accepted / 3 exact duplicate`，全部 3 行仍属于同一 save-day/validation 分区，所以 `goal_method_trainer_input_ready=false`，明确阻塞于 train/test 缺少 pairwise 比较，而不是全局没有 Teacher 偏好。

## 历史记录 · 2026-09-22 Issue #128：source-resolved 随机结果失败关闭

- `source_resolved_downstream` 现在只表示静态来源身份已解析，不再无条件映射为零随机重试。只有 source-specific 证据证明唯一确定目标产物时才允许 `resolved_stochastic_retry_not_required`；随机来源若既未物化也没有精确概率及扩量预留重校验证据，会阻塞在 stochastic retry 轴，正式训练授权继续为 false。
- 锁定的 1.6.15 `Crop` 反编译结果表明，原版季节野生种子 `495..498` 在播种时把随机选择写入 `replaceWithObjectOnFullGrown`，成熟日再将该已选结果物化。透明桥现在读取并校验这个实时字段，而不是误把 `indexOfHarvest` 的 Data/Crops 代表值当最终产物；耕作容量和逐格作物状态共用同一个解析器，避免形成两套身份规则。
- 处理提前量轴对 `stochastic_outcome=true` 的作物只接受 `exact_from_live_native_wild_seed_replacement`。聚焦回归同时固定三种语义：普通单一产物作物可零重试、未解析的野生种子目标必须要求随机证据、fresh 状态已经确定目标结果时无需再次随机。
- 验证通过：GoalConditionedBootstrap Release `0 warning / 0 error`，完整 Stage 1 collection self-test 及未完成链/伪造 admission/篡改 dataset/corpus 源负例全部通过。本切片未启动游戏，也未写正式训练数据。下一固定切片是构造彼此独立的 train/validation/test save-day，并让每个分区都具有真实 pairwise 证据；之后实现专用 goal-to-method trainer。旧 `StructuredPolicyTrainer.BuildPairs` 仍依赖 `candidate.Selected`，不得消费本 corpus；19/19 Teacher 覆盖仍是正式全量训练的独立硬门。

## 历史记录 · 2026-09-22 独立 save-day Teacher corpus 跨过训练器输入门

- 同一条三 transition rollout 验证链现在分别从 `fixture-save-0`、`fixture-save`、`fixture-save-2` 三个独立存档身份重新生成。每份来源都独立经历完整 Teacher 分母、严格 Pareto 偏好、原子 reservation、执行回执、fresh 终态、结算、continuation 重规划、终态 proof 和 controller admission；没有复制数据集或改写 split 标签。
- 钓鱼预测刷新器随之修正一个真实身份漏绑：派生预测快照现在从基准快照同步 save/player FieldEnvelope、state identity 以及 year/season/day/total_days/time，再重新计算 state hash。旧实现只同步 tick/time/total_days，会让其他存档的预测证据混入当前 rollout；该情况现在由既有身份门失败关闭。
- 三个不可由调用方填写的 split key 分别确定落入 train、validation、test。正向 corpus 得到 `3 source / 3 unique / 9 accepted / 0 duplicate / 6 pairwise`，每个分区都是 `3 rows / 1 split key`，manifest 状态为 `ready_goal_method_trainer_input` 且 blocker 为空。单一 validation 来源重复输入的负例仍得到 `6 input / 3 accepted / 3 duplicate`，并继续因 train/test pairwise 为空而阻塞；篡改 source 仍被拒绝。
- `goal_method_trainer_input_ready=true` 只证明这批类型化 Teacher corpus 满足专用训练器的最小分区与比较信号门，不等于正式产品训练。`formal_product_training_authorized=false` 保持不变，19/19 Teacher 覆盖仍是独立硬门。本切片没有启动游戏或训练；下一固定切片是实现只消费该 corpus schema 的 goal-to-method pairwise trainer，禁止复用依赖 `candidate.Selected` 的旧 `StructuredPolicyTrainer.BuildPairs`。

## 历史记录 · 2026-09-22 专用 goal-to-method 成对训练器

- 新增 `train-acquisition-route-goal-method` 和 `explicit_teacher_pairwise_portfolio_ranker.v1`。训练器只从每行的 `teacher_preference.pairwise_preferences` 构造正负差分，明确禁止把 `candidate.Selected`、learner score、proposal/save 身份或 `dominated_by` 当作标签或特征；没有复用旧 `StructuredPolicyTrainer.BuildPairs`。
- 训练入口不会只信 corpus manifest。它逐源重读 proof manifest、proof receipt、rollout admission 和 supervision dataset，重新执行来源证明，核对每个文件摘要、cleaned/partition 摘要、split key、行数、显式 pair 引用及严格成本支配；阻塞 corpus、伪造源摘要和伪造标签声明均失败关闭。
- 检查点绑定 corpus 哈希、cleaned/train/validation/test 哈希、game/bridge/schema 版本、超参数和可选初始化检查点。`checkpoint_id` 可由这些输入重算，单独改写 ID 也会被拒绝；原子落盘不留下半成品。
- 当前独立三存档夹具产出 27 个无身份泄漏特征；train/validation/test 各为 `3 rows / 2 explicit pairs / 1.0 pair accuracy`，检查点为 `goal-method-0bde5f09208dcdea25c31bc9`。这些数字仅证明专用训练链、分区和标签边界能工作，不是泛化能力结论。
- 验证通过：GoalConditionedBootstrap Release `0 warning / 0 error`、完整 Stage 1 collection self-test、Core game-free `81/81`、Backend `203/203`。本切片没有启动游戏，也没有写入正式训练数据。
- `formal_product_training_authorized=false` 继续保持。运行时方法排序尚未消费该检查点，19/19 Teacher 覆盖门也尚未解除。下一固定切片是加入只读的检查点推理/评分边界，并在不绕过确定性候选准入、reservation 和原生执行回执的前提下接入 goal-to-method 选择；随后扩充权威覆盖语料，而不是用当时 9 行直接启动正式产品训练。

## 历史记录 · 2026-09-22 goal-to-method 只读影子评分

- 新增 `score-acquisition-route-goal-method-row` 与 `acquisition_route_goal_method_scoring_result.v1`。评分前会重新验证 checkpoint 绑定的完整 corpus、来源 proof/admission/dataset、分区摘要和目标 row；任意换 corpus、换版本或换 row 身份都不能借用旧检查点。
- 评分器只遍历该权威 row 中 `admission_ready=true` 且具有完整非标量成本向量的候选，不接收调用方候选列表。离线训练和只读推理共用同一个特征上下文与编码器，避免字段含义分叉。
- 三候选比较样本中，模型第一名与 Teacher 选择一致，Teacher 候选 rank 为 1。输出同时保留全排名、Pareto 前沿标记和一致性结论，但固定 `selection_mode=read_only_teacher_comparison`、`portfolio_commit_authorized=false`、`formal_product_training_authorized=false`。
- 验证通过：独立评分 CLI、GoalConditionedBootstrap Release `0 warning / 0 error`、完整 Stage 1 collection self-test、Core game-free `81/81`、Backend `203/203`。未启动游戏或正式训练。
- 这一步证明检查点能被确定性重放和推理，不是实时策略接管。下一固定切片是对当时 fresh snapshot 重新构造完整候选分母，在唯一严格 Pareto 时继续服从 Teacher，在不可比前沿时只生成受门控的模型影子选择；19/19 覆盖门解除前仍不得接入 commit/dispatch。

## 历史记录 · 2026-10-12 Full Shipment 原生商店购买路线断点

- 商店购买代表已锁定为 `full_shipment:item:380 / (O)380 / sells / shop:Blacksmith`。它复用唯一的 `economy.buy_supplies` 路线、共享移动、`executor.interact`、可选 `executor.choose_dialogue_response`、`executor.buy_shop_item` 和菜单清理链；没有新增平行商店执行器。
- 编译器现在在同一计划内顺序投影 `move_to_tile` 后的地点与格点，使紧随其后的相邻交互按计划前态校验；投影仅在当前、目标和投影地点精确一致时生效，跨图或身份不一致仍回退到 fresh snapshot 并失败关闭。商店 supporting transition 只额外允许精确的 `move -> interact` 和 `move -> interact -> dialogue` 队列形状，乱序或扩展形状继续拒绝。
- 运行时等待会在 connector 后保存 `stable-replan-snapshot.json`，要求地点、兼容性和路线时序同时稳定。ShopMenu 已打开且 `shop_stock` 可读、非只读时，允许零游戏分钟的关菜单恢复证明，不把菜单暂停误判为路线时序失效。RuntimeTestHarness 的空闲冻结仅在 `Game1.player.CanMove` 时介入，避免地图切换淡入阶段被永久冻结。
- v3 隔离运行原生通过 Farm 到 BusStop、BusStop 到 Town，并到达 Blacksmith 稳定站位；旧 stage 4 因队列内位置未投影而拒绝。修复后对该完全相同失败产物离线重编译得到 `ready_for_supporting_transition_dispatch / dispatch_ready=true`。v4 从 fresh 隔离存档再次通过前两段，随后按用户要求主动中止；它不是失败证据，也尚不能登记为完整购买成功。
- 收尾验证：GoalConditionedBootstrap Release `0 warning / 0 error`，聚焦测试 `45/45`，完整 Core game-free `284/284`，`self-test-acquisition-route-dispatch` 通过，`git diff --check` 通过。游戏、Backend、ProductHost 和 `8765/8767/8768/8798` 监听均已退出；F 盘 v3/v4 运行证据保留，`local-data/` 未纳入版本控制。
- `formal_product_training_authorized=false` 保持不变。下一固定步骤只做一件事：从 fresh 隔离存档完整重跑 `shop_purchase_sample`，验证 Blacksmith 柜台交互、ShopMenu fresh 重规划、精确购买 `(O)380 x1`、菜单关闭及原生回执。通过前不得把本切片记为完整运行时 strata，也不得开始正式训练。

## 2026-10-27 v34 天气许可与语义路线振荡修复

- `golden-walnut-source-schedule-v34-20261027-0041` 从 day-zero 运行到 `crop_teacher_run_complete`，累计 `221` 条原生 verified action、`15` 个原生日边界且 `stage=error=0`；随后在 Spring 16 `18:10` 晴天因 `(O)143` Catfish 的 Town/Forest 路线交替停止推进。
- 根因不是 Agent 文本层：上游 Master Angler 目标日期意图没有把鱼种窗口的 `weather_modes` 与透明地点上下文天气相交。终端捕获正确拒绝晴天鲶鱼，但 supporting route 仍被反复发布。
- `MasterAnglerTargetDateIntentBuilder` 与 `MasterAnglerWindowIntentValidator` 现在共用同一地点上下文天气语义，意图生成前排除不可能窗口，运行时再次失败关闭。对 v34 真实快照重算仍有 `15` 个合法钓鱼意图，Catfish 为 `0`。
- LiveTrainingLoop 新增通用语义路线振荡守卫：同一 objective/target 在游戏时钟不前进时出现 `A -> B -> A` 反向转移即结构化停止，触发动作不进入训练数据。它只做最后防线，不把下游熔断代替上游候选许可。
- 聚焦回归 `37/37` 与完整 Stage One collection self-test 已通过；全量 Core 为 `2991 passed / 21` 个既有基线失败，隔离全解决方案 Release 构建为 `0 warning / 0 error`。v34 振荡前缀可保留为运行诊断，尾部不得形成训练监督；正式训练与全局 coverage 分子不变。下一固定步骤是提交，再从新的隔离 day-zero 存档执行 post-fix 长跑。

## 2026-10-27 v35 喷泉终止规则与钓鱼候选修复

- v35 新档长跑真实跨过 v34：累计 `196` 条 verified action、`16` 个原生日边界、`0` 条执行错误和 `0` 次语义路线振荡；晴天 Catfish 未再出现，并取得 Beach `(O)708` 与 Town `(O)145` 的合法原生捕获。作物 Teacher、任务/鱼竿、容量恢复和第二轮 8 格收获前缀均继续成立。
- 新断点来自 Town Fountain。真实透明快照显示浮标格 `(25,26)` 同时静态匹配太阳鱼，但 precedence `-10` 的 Fountain 资源规则以概率 `1` 在 precedence `0` 的太阳鱼规则之前终止；运行连续得到 Wood/Stone/Decorative Trash Can。旧候选构建器丢失了规则顺序与 fallthrough，把永远不可达的太阳鱼输出错误加入该抛竿点。
- 修复只改唯一 `FishingEventCandidateBuilder`：当更低 precedence 的适用规则被透明概率字段证明必然终止时，后续规则不再贡献该抛竿点，base fallback 也不再重复加入；同 precedence 规则和概率未完全解析的规则保持保守可达。没有新增第二套钓鱼执行器、特殊 Town 坐标表或运行时黑名单。
- 钓鱼聚焦回归 `23/23`、Master Angler `44/44`、完整 Stage One collection self-test 通过；全 Core 为 `2993 passed / 21` 个既有基线失败，失败集合未增加。v35 为人工诊断中止，没有 summary/source schedule，不能提升正式训练分子。下一固定步骤是全新 v36 原生长跑，先证明太阳鱼选择非 Fountain 抛竿点并 fresh 捕获，再继续首次登岛与 Golden Walnut source schedule。

## 2026-10-27 v36 节日等待核对与低空间恢复门

- v36 从 day-zero 新档取得 `107` 条持久化 verified action、至少 `12` 条原生日界且零执行错误。Spring 13 的 Beach -> Farm 控制项不是无效移动：Egg Festival 使 Beach -> Town 普通 warp 在 `14:10` 前关闭，因此唯一编译队列是每轮有界 `executor.wait_ticks=600`，游戏时间由 `08:40` 正常推进到 `12:30`，地点保持 Beach `(8,16)` 符合合同。
- 控制器在误判该等待为停滞后人工中止了 v36；因此这条运行只保留为诊断前缀，不声明 `(O)145` 非 Fountain 捕获或 source schedule。后续诊断必须先读取 effective queue 的 `option_id` 与 `route_wait_reason`，不能仅凭位置未变判定移动失败。
- 使用 v35 Spring 17 存档和既有 transition ledger 的恢复入口已经真实启动。它正确保留 `4` 条耐久日界、丢弃 `8` 条未被存档状态支持的 volatile harvest receipt，并执行回农场与 `crop_teacher_harvest_inventory_capacity_recovery` 两个 verified 动作，证明恢复机制本身可用。
- 恢复轮在第 3 次尝试前因 E 盘 `8,576,143,360 < 8,589,934,592 bytes` 触发 `minimum_free_space_not_met`，没有越门继续写数据。下一固定步骤是先按审核清单释放或迁移足够 E 盘空间，再复用该原生存档和 ledger 续接到 collection Teacher；不得降低 `8192 MiB` 门、重复 day-zero 或将未完成轮次提升为 coverage/训练证据。

## 2026-10-27 v39/v40 终态保存与续跑收口

- 作物 Teacher 的 terminal harvest 不再依赖进程内临时 receipt。新 ledger 创建即持久化；Teacher 完成后保留待保存终态，关闭阶段重新采集当日/存档基线，只有真正的 `executor.sleep` 结果才能创建边界 receipt，并在同一执行轮完成日期、保存哈希和 ledger 结算。
- v39 原生取得 `8` 条收获 receipt，并在 Spring 22 -> 23 的真实睡眠后写入第 `5` 条日界；v40 续接时丢弃数为 `0`，直接越过已完成 Crop Teacher，进入 Stage One collection，完成一次 objective-safe 单件出货并再次独立跨日保存。两轮最终均由 Product Executor/fresh 快照闭合，v40 无 error。
- 当前固定起点是 v40 的 Spring 24 耐久存档和已完成 crop ledger。下一施工单位只做 collection 主线：验证 `(O)145` 使用非 Fountain 浮标格并形成 fresh 原生捕获，然后继续 Golden Walnut source schedule。不得再次派发已完成作物 Teacher；若发生即视为回退。
- 该闭环仍是正式训练前置运行证据，`formal_product_training_authorized=false`，不得把控制动作或 recovery 当作 Teacher preference 标签。

## 2026-10-27 v41/v42 压缩续跑与非 Fountain 太阳鱼收口

- v41 暴露恢复长跑仍使用普通全量 JSON 快照，虽取得 6 条 verified action，但在最终睡眠前触发 `minimum_free_space_not_met`；没有最终原生存档边界，因此只保留为诊断，不算完成。
- 产品校准包装器现对 `CaptureGoldenWalnutFreshSaveEvidence` 或 Crop Teacher resume 统一传递 `--snapshot-artifact-mode content_addressed_gzip`，并把实际模式写入 summary。fresh-save 捕获开关仍只在 Golden Walnut 模式下传递，未合并两种证据语义。源守卫与内容寻址回归共 `34/34` 通过。
- v42 从 v40 只读 ledger 恢复，确认 `discarded_volatile_harvest_receipts=0` 后直接进入 Stage One collection。运行取得 `24/24` verified action；Town 非 Fountain 浮标格原生捕获 `(O)145`，随后按 exact latest catch singleton 走既有容量恢复出货，没有专用太阳鱼执行器。
- v42 最终从 Spring 24 原生睡到 Spring 25，存档哈希发生变化，持久化 `fishCaught` 含 `(O)145`；summary 为 `status=passed`、`crop_teacher_run_completed=true`、`native_save_boundary_verified=true`、`snapshot_artifact_mode=content_addressed_gzip`、`formal_training_started=false`。
- 下一固定施工单位改为独立 Golden Walnut day-zero/fresh-save source schedule。目标是首次登岛、核桃来源增量、First Parrot/Turtle/House 三项跃迁和可重放 source schedule；不得续用 crop-resume 身份冒充 fresh-save 证据，也不得提前开启正式训练。

## 2026-10-27 v43 跨目标路线续行修复

- v43 从真正 day-zero 新档启动，已完成防风草重规划、`8` 条 harvest receipt、邮件/任务/竹鱼竿原生事件链以及 `(O)131`、`(O)145`、`(O)136` 的 fresh 捕获和出货；随机鱼与杂物未被冒充为目标结果。人工停止点为 iteration `195`、verified action `162`。
- 18:20 出现 `(O)129` Beach 与 `(O)Goby` Forest 两个目标交替抢占：每次 connector 成功后 exact candidate ID 随地点变化，旧代码把候选身份丢失误当成目标完成，清空 continuation，随后 fresh ranking 又选择另一个目标。旧振荡守卫只检测同一 objective key，无法识别跨目标 `Farm -> BusStop -> Farm`。
- 修复只改控制面，不新增动作、候选或执行器：候选租约失效时保留高层 continuation；Stage One collection 和通用 DailyPlan 都通过现有 `QueueReplanFilter` 绑定同一目标；仅当当前排名中没有任何匹配候选时才写 `continuation_released` 并抑制至次日。路线守卫按真实地点边检测同一时刻的 `A -> B -> A`，objective key 仅用于诊断。
- `RouteOscillationGuardTests` 与 queue-replan/Stage One/Master Angler/Golden Walnut 相关回归共 `138/138` 通过。v43 是诊断运行，没有最终 summary/source schedule，禁止进入正式 corpus。下一固定步骤：提交修复，使用更新后的产品二进制从隔离耐久起点受限回放，要求 `(O)129` 从 BusStop 继续 Town/Beach、不得切换 Goby；通过后继续 Golden Walnut source schedule。

## 2026-10-27 v44/v45 路线续行与原生保存边界收口

- v44 使用修复后的高层 continuation 连续执行同一 `quest:13` 路线，从 Farm 经 BusStop、Town 到 Beach，没有在 fresh connector 后被其他目标抢占；随后在原生保存边界暴露新的真实缺陷：FarmHouse `building_door` 的触发格 `(64,14)` 是结构碰撞格，旧路线修复把它误当可站立终点，失败后选择 Farm→Forest→Farm 的反向绕路。通用振荡守卫在第 14 次迭代正确停止，未把失败尾部写成成功证据。
- 恢复候选现复用唯一透明路由、连接器站位解析和通用清障候选。`building_door`、`action_warp`、`locked_door_warp`、`native_bus_transport` 使用透明 `stand_tile_x/y`；step-on warp 继续以触发格为目标。若最短连接器前段只被可清除障碍阻塞，上游直接返回 `clear_obstacle_tile`，DailyPlan 进入既有 `executor.clear_obstacle`；直接高层恢复入口只等待 fresh replan，不建立第二套清障执行器。即时反向边继续失败关闭。
- v44 的真实 Farm 快照离线重算后得到可用的 `recovery:native_save_boundary / clear_obstacle_tile`，目标保持 FarmHouse，不再选择 Forest。相应连接器修复和恢复回归 `3/3` 通过。
- v45 从隔离耐久存档重新运行：8/8 Product action 完成；同一 `quest:13` 高层目标依次执行 Farm→BusStop→Town→Beach；关闭阶段按 Beach→Town→BusStop→Farm→FarmHouse 滚动重规划，随后原生睡眠。日期从 `1:spring:14` 变为 `1:spring:15`，存档 SHA-256 从 `a9dbf56f...` 变为 `74400983...`，summary 为 `status=passed`、`native_save_boundary_verified=true`、`formal_training_started=false`。
- v45 证明通用跨图 continuation 与原生保存边界闭合，但没有单独重现 `(O)129`；不得把 quest 路线证据改写成该鱼的完成证据。下一固定施工单位仍是独立 Golden Walnut day-zero/fresh-save source schedule，并把 `(O)129` 跨 BusStop/Town 连续性作为长跑观测点，随后证明首次登岛、核桃来源增量及 First Parrot/Turtle/House 三项跃迁。

## 2026-10-27 v46/v47 商店候选前置日程门

- v46 在 Spring 1 周一错误锁定 AnimalShop 购买目标并做了 14 个机械等待块；`08:20` 的 fresh snapshot 才正确指出玛妮不在柜台。复核完整透明日程后确认，玛妮当天没有任何位置落入柜台服务区且与入口开放时间重叠，因此错误发生在 `06:00` 候选生成，而不是店主状态读取或购买执行器。
- 修复复用 `NpcCurrentScheduleSnapshotProjectionResolver`、`NpcCurrentLoadedSchedulePresenceResolver` 与 `NpcFuturePresenceWindowResolver`。购买候选只接受店主稳定位置窗口、服务区矩形、店铺时间和入口时间的真实交集；无交集当天排除，有未来交集精确延迟，证据不完整则失败关闭。`SnapshotEnvelope` 原生入口避免序列化整份全量快照，单快照同店主解析结果被缓存。
- `v47` 首轮真实 ranking 中 AnimalShop 候选全部以 `shop_owner_has_no_service_window_today` 排除，随后直接编译 `claim_starter_gift`，没有重现 v46 的购买等待。运行在该诊断点停止，未宣称 Golden Walnut source schedule 完成。
- 下一步：从独立新档继续 source-schedule 长跑；若出现新断点，只修该 fresh 快照证明的候选、编译或执行缺口，保持首次登岛 -> 核桃来源增量 -> First Parrot/Turtle/House -> source schedule 生成/重放的顺序。

## 2026-10-27 v48 双作物原生循环与节日等待优化

- v48 新档真实完成两轮 Crop Teacher：`6` 格花椰菜和 `8` 格防风草都走过购买/库存取种、场地准备、播种、逐日浇水、补水、跨日保存与全量收获。第二轮满包时复用既有安全单件出货恢复两次，最终 `8/8` 收获回执齐全。人工停止前共有 `185` 条 verified action，Stage One 已重新接管。
- 邮件、任务 `13`、Beach 路线、节日门禁和 shared daily plan 之间的控制交接都真实发生；节日关闭没有卡死，`14:10` 后自动改编译连接器。但每个 `600` tick 等待片段都触发 full snapshot、候选生成与排名，属于已证实的性能缺口。
- 修复只扩展既有 `executor.wait_ticks` 合同：`wait_ticks` 继续限制为 `1..600`；只有编译器给出精确 `route_wait_until_time` 时，Runtime Harness 才允许最多 `3` 个连续原生片段，开放即提前返回，段末仍由 Product Executor/fresh full snapshot 闭合。普通等待保持单片，clock/full 哈希不交叉复用，时钟无进展失败关闭。
- 有界等待 primitive 的隐藏原生 Harness smoke 已通过：`06:00 -> 06:20`、`actual_ticks=875`、`applied + verified`，证明同一原生请求能越过原 `600` tick 单片并在目标时刻提前停止；它未绕过执行器写游戏状态。全量 Core 为 `3004 passed / 21` 个既有基线失败，没有新增失败。下一步是提交本切片并继续 Golden Walnut source schedule，在真实 Product Executor/节日门再次观察路线级集成。v48 及本优化均不等于首次登岛或正式训练准入。

## 2026-10-27 v49-v52 crop-resume collection 闭环

- v49 以 v48 的 Spring 17 存档和只读 transition ledger 恢复，按耐久边界丢弃 `8` 条尚未跨日落盘的收获回执，重新完成两次容量恢复与 `8/8` 防风草收获；原生睡眠后得到 Spring 18 存档和 `8 harvest + 5 day boundaries` 派生账本。该轮 `5` 条 verified action、`2` 条已接纳多候选轨迹，包装器通过。
- v50 从耐久账本直接越过 Crop Teacher，执行 `(O)330` 安全单件出货和 Master Angler 路线，取得 `4` 条 verified action 并保存到 Spring 19。v51 随后实际捕获 `(O)147`，但因有界尝试同时计入 fresh 失效/continuation 释放而停在 `6/8`，未写原生保存边界；该轮只作诊断。
- v52 以 `6/12` 的可达门重跑未落盘的 Spring 19 状态，原生捕获目标 `(O)131`，产生 `1` 条 `fishing.catch_fish` 已接纳轨迹；满包恢复通过唯一跨图 continuation 将该鱼精确投箱，库存 `1 -> 0`、出货箱 `0 -> 1`。原生睡眠把存档推进至 Spring 20，summary 为 passed，所有服务和端口正常回收。
- 下一固定施工单位仍是独立 Golden Walnut day-zero/fresh-save source schedule：首次登岛 -> 来源增量 -> First Parrot -> Turtle -> House -> schedule 生成与重放。v49-v52 不得用于提升 fresh-save coverage 或开启正式训练。当前 E 盘只保留约 `8.1 GiB` 可用空间，继续长跑前必须恢复高于 `8192 MiB` 门槛的实际写入余量；不得关闭或降低该门。

## 2026-10-27 v53 fresh-save 诊断收口

- v53 新档真实取得 `193` 条 verified Product action、两轮完整 Crop Teacher 和 total day 16 的耐久前缀，但在 `19:20` 的第三次作物 handoff 中，普通 `route_to_farm` 与共享 exclusive recovery 门冲突，连续八次被 dispatch 拒绝后停止。该运行是高价值诊断，不是完成的 Golden Walnut source schedule。
- Crop Teacher 现于 `19:00` 后复用既有 `recovery.stabilize_day`，不再重建普通作物路线；执行步骤覆写保留原 ledger 的 `NativeSleep` 日界结算语义。故事事件和安全对话恢复仍先于该门，其他普通 interrupt 仍不得结算作物或睡眠转移。
- fresh-save continuity 从整块富投影文本相等改为稳定进度投影相等，并把控制器交接窗口锁为 v53 实测的 `24` tick。当前位置、支付能力和当前地图校验状态可变化；任一实际核桃、来源收集、修船、到岛、鹦鹉工程或访问解锁变化若没有 episode 仍被拒绝。v53 的 `193/193` episode 已离线重建 manifest 成功。
- 验证：聚焦 `22/22 + 11/11`；全量 Core `3008 passed / 21` 个既有基线失败。下一步只运行新的独立 day-zero 长跑，先观察晚间 recovery 原生保存，再继续首次登岛与核桃 schedule；本轮没有写正式训练数据，`formal_product_training_authorized=false`。

## 2026-10-29 EVD-343 钓鱼奖励菜单恢复闭环

- 长跑中满包钓鱼后的 essential FishingRod `ItemGrabMenu` 不再落入普通关菜单或停滞。透明投影、强制恢复候选、DailyPlan、编译、类型化请求、原生槽位点击和 fresh 回执已贯通；无空位时只允许一项透明、可丢弃、未保护且总店售价值最低的 Object 栈，缺少安全项即失败关闭。
- E 盘隐藏静音原生矩阵 `runtime-fishing-item-grab-recovery-20261029-064618` 为 `2/2 PASS`：直接领取与授权单项丢弃后领取均 `applied/verified`，奖励库存精确 `0 -> 1`，菜单关闭。诊断 fixture 与生产执行器继续分离。
- 为避免恢复动作触发全地图扫描，新增 `fishing_reward_recovery` 目的配置。复合选项只在精确菜单身份下允许缺少与该分支无关的三个路线字段；其他恢复动作仍按原 `recovery` 配置失败关闭。最终性能为 39.381 UPS、末次构建 40.673 ms、启动期最大 559.851 ms。
- EVD-343 登记为底层 executor 校准证据，TrainingAllowlist 不变，`formal_product_training_authorized=false`。下一施工单位回到 Golden Walnut fresh-save source schedule，而不是继续扩展此恢复动作。

## 2026-10-29 v61 长跑断点与 v62 固定入口

- v61 从独立 day-zero 新档推进到 Spring 23 / total day 22，共 `395` 次迭代、`295` 条 verified Product action。它真实通过 EVD-343 钓鱼奖励恢复，但仍未首次登岛，未生成 Golden Walnut source schedule。
- 第一个断点是 episode 297 的优先剧情中断：`executor.advance_story_event` 产品回执实际为 `applied + verified`，但旧聚合回执把“当前作物候选尚未完成”误写成“本次执行 blocked”，导致持久化 episode 从 296 跳到 298。PR #349 现将“已执行步骤全部验证且快照新鲜”和“整个候选完成”分开；前者保留 executor-calibration episode，后者仍为 false，策略轨迹过滤器继续拒绝不完整候选。`24` tick 连续性上限没有放宽。
- 第二个断点发生在睡醒后的短暂原生控制交接：校准与 native gate 已开放、菜单/事件/工具/warp 均关闭，但 `CanMove=false` 且 route timing 暂不可用。PR #348 只在这一精确状态编译一次 `30` tick 等待并 fresh 重规划；该等待进入 no-progress 退避，不计有效训练动作。
- v61 已存在不可补写的 episode 缺口，只保留为诊断，不得重建为正式 corpus。下一固定入口是全新 v62 day-zero 隔离长跑；退出条件依次为：复现并通过剧情中断连续性、通过 native-control settle、保持零未验证执行，随后继续首次登岛 -> 核桃来源增量 -> First Parrot -> Turtle -> House -> source schedule 生成与独立重放。#345 仍生效，运行必须使用 `--skip-training`，`formal_product_training_authorized=false`。
- v62 启动前的夜间降载修正已闭合：产品校准脚本现在显式设置并恢复 `STARDEWAI_SUPPRESS_LOCAL_RENDER=1`，RuntimeTestHarness 的既有 Draw/SaveGameMenu 补丁同时覆盖 headless training，而不影响普通可见游戏。短原生 smoke `headless-training-render-suppression-20261029` 为 passed，日志确认本地 Draw 被抑制、stderr 为零、原生新档提交成功且全部进程/端口回收；该 smoke 不构成 source schedule 或训练证据。

## 2026-10-29 v63-v65 节日长等待闭环

- v63 从独立 day-zero 新档运行 `208` 次迭代并取得 `155` 条 verified action，推进到 Spring 13 / total day 12。Stage One collection 在 Egg Festival 普通 Town transit 于 `14:10` 前关闭时正确让权，但共享日计划把所有 `AllowedToday=true / NextOpenTime=1410` 候选过滤为空；连续八轮得到 `empty_action_list` 后结构化停止。
- 根因是 `EventCandidateTimelineScheduler` 的默认等待预算固定为 `21600` ticks（6 游戏小时），而 `06:40 -> 14:10` 需要 `27000` ticks。PR #352 把**默认**预算改为当前时刻到 `19:00` autonomous recovery 窗口的剩余时间；显式调用者给出的硬上限仍原样生效。ready-now 候选仍排在 deferred 之前，DailyPlan 仍只编译单个 `600` tick 滚动等待并 fresh 重规划，因此不会整天盲等或抢占可立即执行的工作。节日路径聚焦回归 `8/8`、GameFreeGovernance Core `503/503` 通过。
- v64 尝试从 v63 的白天存档直接续跑时，被 day-zero initial-boundary validator 正确拒绝。跨进程重载只能回到最近一次原生保存边界，不能把 Spring 13 内存态拼接成连续 fresh-save corpus；该尝试没有接纳 episode，相关进程和端口已完整回收。
- v65 随后从新的 day-zero 隔离存档启动，并复用已验证的预构建二进制降低启动开销。截至 iteration `117`，耐久链已推进到 Spring 13 `15:00`、三类 stderr 均为零；iteration `112-113` 为单步 `executor.wait_ticks`，`14:10` 开放后 iteration `114-116` 自动切换为 `executor.traverse_connector`，iteration `117` 继续移动/显式清障。该结果真实关闭 v63 的长等待阻塞，而不是只证明单元测试。
- v65 最终运行 `356` 次 primary attempt，留下 `265` 条耐久 episode，并推进到 Spring 22 / total day 21；作物 Teacher 的首日、整轮、一次重规划、`8` 条收获回执和 `4` 条日界回执均完成。运行在 episode `279` 的 verified `executor.advance_story_event` 与 episode `281` 的 `executor.water_crop` 之间暴露 `335` tick 控制器暂停交接。两侧保持同日 `06:00`、Farm `(64,15)`、`403g`、`270` energy、`100` health、Watering Can 和逐项相同背包，岛屿/核桃进度也完全相同；这不是未记账游戏进展。
- 连续性验证器现只为上述已验证剧情动作增加最大 `360` tick 的窄交接：前一动作必须是成功且 primitive verified 的 `executor.advance_story_event`，下一条必须是成功 executor episode，并且存档/玩家、日与分钟、地点/格、金钱、能量、生命、工具、背包、姜岛与核桃进度全部相同。普通交接仍严格限制为 `24` tick，任何玩家移动、背包变化、岛屿进度或超限都失败关闭。聚焦边界测试 `5/5`、GameFreeGovernance Core `508/508` 通过。
- 使用修复后二进制对 v65 耐久目录重建，`265/265` episode 已形成合法 `golden_walnut_fresh_save_episode_manifest.v2`。随后 source-schedule builder 精确失败为 `Fresh-save episodes do not contain the native first-island-arrival transition.`，证明证据基础设施阻塞已关闭，真实缺口回到首次登岛。v65 已停止且不能跨进程拼接未保存内存态；下一运行必须是新的 day-zero v66，顺序仍为首次登岛 -> 来源增量 -> First Parrot -> Turtle -> House -> source schedule 生成与独立重放。#345 完成前继续 `--skip-training`，`formal_product_training_authorized=false`。

## 2026-10-30 v69 部分成功聚合的严格证据闭环

- v69 从新 day-zero 存档运行到 Spring 22，累计 `327` 次迭代、`263` 个 verified Product action。iteration `316` 的八项收获中一项成功、七项因容量 blocked，已真实产生八个 continuity-only 耐久 alias，不写政策数据；旧 early-return 证据空洞因此关闭。
- 离线 builder 已从停止后的耐久目录生成 `golden_walnut_fresh_save_episode_manifest.v3`，严格接纳 `494` 个 episode，包含上述八个别名。同一 manifest 构建 source schedule 时只因缺少原生首次登岛 transition 而失败，不再是连续性或快照错误。
- 停止点在第三轮 `8/8` 收获完成后，但未再跨原生睡眠/保存边界。下一唯一施工单元是从最近耐久保存恢复的有界回放：重结算必要的 volatile 收获，先完成原生保存，再继续首次登岛与核桃 source schedule。仍使用 `--skip-training`，不改变任何正式训练准入分子。

## 2026-10-30 v70 校准结果与下一施工单元

- v69 的未保存尾部不能跨进程升级为 fresh-save corpus，因此 v70 直接采用新的 day-zero 身份；这是对上一节“先有界回放”表述的修正。v70 运行到 Spring 17 / iteration `189`，取得 `173` 条 verified Product action、两轮完整作物收获、节日 transit 和 Stage One 钓鱼前缀，但仍没有首次登岛。
- 真实排名工件证明容量恢复把 Full Shipment contribution 错排在腾格效率之前：`20` 木材压过两个 singleton，形成 20 次原生单件出货。PR #360 只调整同一 `StageOneInventoryCapacityRecovery` 的候选顺序，优先最少 native deposit 次数；候选、编译器、产品执行器和回执均继续复用现有单一路径。
- v70 在缺陷归因后停止，未保存尾部不续接。下一施工单元为全新 v71：用合并后代码复核 singleton 恢复，然后继续首次登岛和 Golden Walnut source schedule。运行仍为 `--skip-training` 的校准采证，不是模型训练。

## 2026-10-30 v71 校准结果与下一施工单元

- v71 仍为 `--skip-training` 校准。iteration `149` 已在真实满包场景选择可用 singleton `(O)92 x1`，通过既有单件出货 primitive 释放槽位；iteration `150` 完成 `8/8` 收获。容量恢复的最短动作排序已从单元证明提升为原生运行证明。
- iteration `162` 的原生钓鱼在满包时打开 essential `ItemGrabMenu`。iteration `165` 的统一排名中已有可执行的 mandatory `recovery_claim_fishing_item_grab_reward`，但旧 Stage One membership 只接纳四类 collection 目标，因而先错误让出控制；后续 Crop Teacher 路线被仍开启的菜单正确拒绝。该断点是控制编排顺序缺口，不是缺候选、缺字段或缺执行器。
- 当前修复让已存在的 mandatory menu recovery 在 continuation、collection membership 与策略选择之前执行，并继续复用唯一 DailyPlanCompiler、ActionQueueCompiler 和 Product Executor。它只形成 control queue，不生成 Teacher preference 标签，也不解除正式训练门。验证为聚焦 `11/11`、GameFreeGovernance `521/521`、Release build 零警告零错误。
- v71 尾部不可跨未保存边界续接。下一施工单元为全新 v72 day-zero 校准：先原生证明钓鱼奖励恢复被 Stage One 接管、菜单未关闭前不派发 Crop Teacher，再继续首次登岛、核桃来源增量、First Parrot/Turtle/House、source schedule 生成与独立重放；#345 完成前不启动模型参数训练。

## 2026-10-30 v72-v82 淡入控制交接修复与当前施工单元

- v72 在 `--skip-training` 下取得 `176` 条 verified Product action 并推进到 Spring 9，最终由五次相同的 `future_route_movement_timing_context_outside_calibration_scope` 受控停止。真实快照证明这是日界后的精确淡入交接：校准兼容、路线 timing 未就绪、native gate 未开、`player_can_move_now=true`、`global_fade=true`，其余菜单/事件/工具/暂停/冻结/warp 条件全部关闭。
- 唯一 `NativeControlSettleRecovery` 现并列接受两个精确且互斥的状态：原 post-sleep control gap，以及 v72 的 day-start fade control gap。两者都只产生一次 `30` tick 控制等待并 fresh 重规划；混合状态、缺字段或其他控制状态继续返回 null。调用点已从 Stage One collection builder 提升到所有 DailyPlan producer 之前的唯一全局抢占点，避免 active Crop Teacher 或普通日计划先行覆盖恢复。
- v73 从 v72 的 Spring 9 保存边界隔离启动，完成 `2/2` verified action 和 Spring 9 -> 10 的原生保存。v76 正确拒绝过期 fixture；v77 的 Crop Teacher 睡眠在返回前已等待淡入结束；v79/v80 的 fresh-process 共享睡眠同样未复现。v82 从 v72 Spring 8 `_old` 精确哈希副本执行 `8 x 600` tick 预热后，再通过同一 DailyPlanCompiler、ActionQueueCompiler 与 Product Executor 执行共享睡眠及次日新规划；预热全部 verified 且不进入训练数据，但仍未出现 transient。包装器缺少 settle wait/fresh replan 双标志时严格失败，因此这些运行均没有冒充正向命中。
- 当前验证为专项 `19/19`、完整 GameFreeGovernance `526/526`、Release build 零警告零错误。人工探针在 v82 收口，不再继续扩大；实时正向命中只在后续自然长跑碰到 v72 状态时补录。当前 WIP 直接恢复首次登岛、核桃增量、First Parrot、Turtle、House、source schedule 生成/重放与 Master Angler prerequisite audit。所有运行继续 `--skip-training`，#345 与 `formal_product_training_authorized=false` 保持。

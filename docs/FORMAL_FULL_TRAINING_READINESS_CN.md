# StardewAI 正式全量训练准入与实施路线

## 2026-09-05 r34 round03 连续批次结果

round03 从 Summer 7 和 round02 canonical 精确哈希通过 prepare，以并发 1 完成 2 个主决策和 2 个原生保存边界迭代。出货与邮件 approach 的机械回执均为 applied/verified，但 selected queue candidate 均未完整闭合，因此正确地产生 0 条新策略轨迹；控制面 return-home/sleep 也没有进入训练策略数据。执行特征从 220 增至 224，horizon 观测从 7 增至 8。

原生存档推进到 Summer 8，事务为 `committed_after_native_save_boundary`，未解决 pending 为 0，正式容器 exit 0 / OOM false。canonical 仍为 accepted 215 / rejected 0、157 / 5 / 53、4920 pairs；checkpoint / manifest SHA-256 更新为 `64283a58cb8e48eed06baffb1c8116a241033c26b00e792ec01807db537c345c` / `47ac7aaff2798f3546eea1e0a5772a524dc3d9ee959e4f0506136d7e3fb18a78`，checkpoint 精确绑定新 horizon 清单。train/validation/test pair accuracy 为 0.992276 / 1.0 / 0.96683；两次主排序为 2.427/45.624 秒，两个边界排序为 203.5/12.4 毫秒。

完整制品位于 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r34-round03-20260905-085309`，远端/本机 119 / 119，缺失、额外和摘要不一致为 0，存档树聚合摘要也独立复算一致。下一批只能从 Summer 8、存档摘要 `4bb3420051c550a5f8c371bec8867e3101ca155ad0c6a69a60ed9ae36ad7096f` 和上述 canonical 起步。

## 2026-09-05 r34 round02 连续批次结果

round02 从 Summer 6 和 round01 canonical 精确哈希直接通过 prepare，2 个主决策生成 4 条 applied Product 策略轨迹。fresh 后不可用的社交队列尾项被失败关闭并触发重新规划，只留下 skip 诊断；控制面保存边界没有写策略轨迹。原生存档推进到 Summer 7，事务为 `committed_after_native_save_boundary`，未解决 pending 为 0，正式容器 exit 0 / OOM false。

canonical 更新为 accepted 215 / rejected 0、157 / 5 / 53、4920 pairs；checkpoint / manifest SHA-256 为 `bc5369df5a47bfdf27d9a49b99cc4498b54a4cd4dc27bba1b02de907419c15a4` / `24b18a5bf0317e36f36398609b9e65c79a69f42bef73cee35b57191ae56ec653`。清单六摘要与实际文件一致；validation/test pair accuracy 为 1.0 / 0.96683。两次主排序为 2.412/43.828 秒，控制面后续排序为 154.6/22.2 毫秒。

完整制品位于 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r34-round02-20260905-043913`，远端/本机 170 / 170，缺失、额外和摘要不一致为 0。下一批以 Summer 7 为唯一合法起点，继续遵守并发 1、有界退出、失败不提升、原生保存和停机归档门。

## 2026-09-05 r34 候选性能门与连续批次结果

`10b7722` 只复用由同一不可变快照派生出的 route connector 候选，不缓存跨快照事实，也不裁剪透明字段。默认自主候选由单一权威集合提供，显式目标、校准和玩家指令候选继续失败关闭；`recovery.stabilize_day` 只在控制面显式补入，不参与策略训练。game-free governance 18/18、Backend 172/172 和针对性缓存/候选边界测试通过；真实 119 运行继续承担游戏程序集相关验证。

冷启动 Farm full snapshot 默认排序从约 227.9 秒降至 28.266 秒，119 室外首次排序为 43.329 秒，后续同快照边界排序为 145.6/9.7 毫秒。该结果解除“每次排序重复构建全部跨图连接候选”的 I/O/CPU 瓶颈，但不改变策略调用边界，也不代表可无界运行。

`train.server.20260905.r34.plan01` 从 Summer 5 推进至 Summer 6，以 4 次迭代完成 2 个主决策、2 个原生存档边界和 4 条 applied Product 策略轨迹。事务为 `committed_after_native_save_boundary`，规范数据为 accepted 211 / rejected 0、153 / 5 / 53、4783 pairs；checkpoint / manifest SHA-256 为 `b33d54f66fdcbe304e5207043751a0a153dcd45d24064b299903b978d28bd010` / `103770377bdca5b4979bd196ae4756c93e080f8924bc1de136a9bd35cc07c738`。原生保存完成、存档哈希变化、六个数据摘要、0 unresolved Product pending 与正式容器正常退出均已复核。

完整制品已归档到 `I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r34-round01-20260905-041036`，远端/本机 189 / 189 且三类差异为 0。下一批继续沿用并发 1、静音后台、计划退出、失败不提升和逐轮归档；跨季、跨年、Grandpa 21 与 Companion 产品层仍是未完成准入。

## 2026-09-02 运行证据强绑定准入

正式训练不得把“存在 evidence id”视为 Runtime/Output 已验证。`native_object_execution.v2` 的当前准入同时要求：证据 ID 已登记、运行路径修订完全匹配、32 个源文件的规范化 SHA-256 未漂移、artifact/source/build identity 完整，以及 RuntimeTestHarness、Contracts、TransparentBridge 三份运行 DLL 的 SHA-256 与登记值一致。任一项未知、缺失或变化均 fail closed，必须重新构建并取得新的原生运行证据后才能恢复准入。

首批强绑定范围是 #88 重跑的六个动作：`world.rotate_house_plant`、`world.play_singing_stone`、`farming.collect_slime_ball`、`animals.withdraw_feed_hopper_hay`、`animals.collect_auto_grabber_contents` 和 `movement.use_mini_obelisk`。其余历史域暂标记 `LegacyUnbound`，不能据此宣称全目录已完成强证据迁移；应按真实运行产物逐域补齐。CI 使用不依赖私有游戏 DLL 的 game-free governance profile 持续检查登记表、源码指纹、序列化追溯字段和原生对象机械约束，完整本地 Core/Backend/Release 回归仍作为合入门槛。

## 2026-09-01 r29 队列级决策边界修正

正式训练的规范单位不是机械动作，也不是每次 fresh snapshot。策略模型一次排序并选择一条有序高层候选队列；该选择、原始排序、编译队列和候选顺序由 `SelectedQueueDecisionLease` 持有。运行时逐候选执行，并在每个候选边界从 fresh snapshot 重新物化候选、校验队列顺序以及累计时间/能量，再由 C# 编译器确定性展开。候选内部的寻路、移动、交互、等待和菜单 continuation 同样只进行确定性重编译。

只有队列全部完成，或当前候选不可用、阻塞、身份漂移、回执不确定以及时间/能量预算失效导致整条队列失效时，才能重新调用策略模型。每个成功完成并验证的高层候选写入一条 `policy_decision_trajectory.v2`；机械原语不单独生成策略轨迹。已完成候选的轨迹可以在后续候选失效时保留，但失败候选不得计为完成。

119 服务器 `formal-r29-20260901 / train.server.20260901.r29` 已证明该边界：1 个排序文件选择 4 个高层候选，随后执行 9 个机械动作；3 次候选边界刷新和 5 次候选内部 continuation 刷新均为本地确定性编译，`policy_model_invoked=false`；最终 4 条高层候选轨迹入库且 `selected_queue_decision_complete=true`。r24/r25 关于“一次 fresh replan 对应一条策略轨迹”或“一个外层 iteration 只能容纳一个高层目标”的历史解释由本节明确取代。

迁移证据位于 `I:\StardewAITrainingArchive\119.91.139.160\formal-training-r18-pre-queue-boundary-fix-20260901`，远端与本地 910/910 文件 SHA-256 一致，远端源训练根未删除。当前仍不是多日全量训练完成；服务器约剩 4.2 GB，扩容或迁移训练根仍是长训硬门槛。

## 2026-09-01 119 服务器 r25 有界证据保留

r25 将正式运行的诊断快照窗口从 LiveTrainingLoop 默认 64 轮改为 manifest 冻结的 4 轮。请求、
`training_run_manifest.v2`、launcher CLI、Linux attached 脚本、Windows 启动器和 Compose 使用同一
`max_persisted_iterations`；范围限制为 1-64。滚动清理只处理当前 run 的 `live-snapshots` 中旧迭代
家族，保留最近四轮；正式策略轨迹、Product pending/final 回执、数据集、checkpoint、manifest 和日志不受影响。

发布 `formal-r25-20260901`、运行 `train.server.20260901.r25` 再次完成邮件目标五步，全部为
`applied/verified/fresh`，5 条候选轨迹全部入库且来源遗漏为 0。manifest 实际冻结
`max_attempts=1 / max_persisted_iterations=4 / min_free_space_mb=4800`。正式数据集现为 accepted 186、
rejected 0、train/validation/test 128/5/53，首次得到非空 validation 分区；checkpoint 更新为
`structured-policy-52c9f785cc6dcc46c02f94e7`，SHA-256 为
`b97bbdc1b64ba77b38097fc691581d4397c32807246f312b00dd883249e23b67`。训练结束后约 54.124 UPS，
12 次 full snapshot 平均 1406.835 ms。服务器只有一块 60GB 系统盘，当前约 4999 MiB 可用、使用率
92%；四轮滚动可限制后续单 run 工作集，但不能替代扩盘，也不得据此直接开启无界长训。

## 2026-09-01 119 服务器 r24 正式闭环实证

正式 Product 训练控制闭环已经开始产生真实更新，但全量长训尚未启动。服务器使用隔离槽位
`StardewAIDebug_16564609768130219756`、发布 `formal-r24-20260901` 和运行
`train.server.20260901.r24`，以 `max_attempts=1` 完成一个有界高层目标。这里的一轮是一个模型级目标
episode，不是一个按键：模型选择“处理 landslideDone 邮件”，编译/执行层依次完成跨图、走到信箱、
原生交互、LetterViewer 输入稳定等待和原生关信五个机械动作；每次 fresh snapshot 后只对同一类型化
continuation 重规划，目标完成后以 `objective_continuation_completed_iteration_boundary` 结束本轮，
不得在同一 episode 中误选第二个全局目标。

r20-r24 的服务器证据把该边界逐层闭合：r20 暴露跨地图后沿用旧队列，r21 改为 fresh snapshot
重规划；r22 为暂时不可输入的 `LetterViewerMenu` 增加严格绑定菜单身份的 30 tick 有界等待；r23 在
目标 continuation 完成时结束外层 episode；r24 为打开信箱、菜单等待和关信阶段补齐 `candidate_id`
来源。r24 最终为 `5/5 applied + verified + fresh`，形成 5 条互异且成功的 Product 策略轨迹，
`effective_candidate_id_missing=0`。正式数据集由 176 增至 181 条接收、0 条拒收，分区为
train 128 / validation 0 / test 53，训练比较对 3415；新 checkpoint 为
`structured-policy-35eb3439e19036a75b9c628b`，SHA-256 为
`11f38de448370bb401278bd8cd32ca4faea1e42e67df3b5299e20394c53ef0f7`。

本轮后游戏保持运行，Backend 与 Product Executor 保持空闲，LiveTrainingLoop 正常退出。低频观察模式
为 `STARDEWAI_OBSERVER_RENDER_INTERVAL_MS=1000`，实测约 56.346 UPS；12 次 full snapshot 平均
1430.682 ms，没有形成持续卡顿。服务器当前仅约 5072 MiB 可用、磁盘使用率 92%，因此不得在现盘上
启动无界长训。下一准入动作是扩容或迁移正式训练根并复核哈希，然后按可恢复的小批游戏日运行；每批必须
同时验收 Product 原生回执、轨迹增量、dataset/checkpoint 哈希、磁盘门、游戏 UPS 和恢复探针。当前模型仍是
`return_weighted_pairwise_linear_ranker.v1`，RTX 5070 8GB 对这一阶段不是必需资源；神经策略模型或
QLoRA 替换属于积累足量、多分区长期轨迹后的后续阶段。

## 2026-09-01 EVD-327 Product bootstrap 与隔离存档绑定

E 盘隔离副本 `StardewAIDebug_16564609768130219756` 已完成隐藏静音 Product bootstrap 校准。运行
`product-bootstrap-20260901-013441` 形成 3 条 `policy_decision_trajectory.v2 / product_executor.v1`
真实轨迹，其中 2 条包含至少两个准入候选；首两组分别保留 107 与 143 个可训练候选。数据集构建为
3/3 接收、0 拒绝、0 重复，并形成 248 个训练比较对。初始 checkpoint 为
`E:\StardewAITraining\checkpoints\structured-policy-bootstrap-20260901.json`，模型类型是
`return_weighted_pairwise_linear_ranker.v1`。当前三条数据全部位于 train 分区，validation/test 均为 0，
因此该 checkpoint 只作为正式循环冷启动，不构成泛化评测或全量训练完成证据。

正式 launcher 现把 `save_slot` 冻结进 `training_run_manifest.v2`，并在 prepare 阶段验证槽位名、
隔离根和主存档文件。游戏进程显式接收 `STARDEWAI_TEST_SAVES`、`STARDEWAI_TEST_SLOT` 与
`STARDEWAI_TEST_AUTO_LOAD=true`；缺失或越界槽位失败关闭，不能再以停在主菜单的进程冒充运行中的训练。
下一准入门是把 bootstrap 轨迹按哈希种入正式训练根，prepare/launch 同一份 manifest，并由新存档完整运行
至少一个真实游戏日，使 Product 轨迹、正式数据 manifest 和 checkpoint 哈希发生受控更新。

## 2026-09-01 EVD-326 正式训练编排与模型更新准入

正式版本绑定已固定为 `policy_decision_trajectory.v2 / policy_features.v2 / action_queue.v1 / product_executor.v1`。轨迹写入端会按实际执行入口区分 Product 与 RuntimeTestHarness；清洗层允许保留 Harness 校准数据，但正式结构化训练器和 checkpoint store 只接受 Product 数据，且一个 manifest 只能包含一个不可混合的版本集。

`training_run_manifest.v2` 是启动与运行中的唯一控制记录。prepare 冻结数据 manifest/checkpoint hash、路径、run-id、隔离存档、执行端和工具二进制；launch 必须加载同一份 prepared manifest，并拉起 Product Executor、SMAPI 与 LiveTrainingLoop。`training_ready_probe.v2` 逐轮核对三进程、Product health、透明快照、run-id、收据 pending、dataset manifest、cleaned/三分区文件及 checkpoint hash。任何崩溃中间态都不得继续发动作。

正式循环已经改为真实结构化更新：Product applied/verified/fresh 轨迹落盘后，重建 policy dataset，训练结构化 ranker，并原子刷新 checkpoint 和 run manifest hash；旧 baseline 训练只保留非正式兼容路径。正式模式缺 Product、feedback、prepared manifest 或结构化 checkpoint 时启动即失败。

EVD-327 已完成 Product bootstrap 与初始 checkpoint，控制面不再受“无真实初始数据”阻塞。当前仍未满足
“全量训练已开始”：必须由正式 prepared manifest 拉起 Product Executor、SMAPI 和 LiveTrainingLoop，
完整运行一个真实游戏日，并证明 checkpoint 随新增 Product 数据更新。bootstrap 校准运行本身不得计入正式训练时长。

## 2026-08-31 Product Executor 准入（EVD-325）

独立 `StardewAI.ProductExecutor` 已把 145 个现有原生 Harness dispatch 状态机装配到产品入口，但不复制动作实现。产品授权锁定 loopback、非 debug 产品能力、run-id、执行模式/actor、精确隔离存档根、nonce 和时间戳；正式 `LiveTrainingLoop` 未选择产品入口或关闭 executor feedback 时直接失败。62 项策略 allowlist 仍独立决定哪些高层候选可进入训练，Product Executor 数量不能反向扩大训练准入。

产品层在原生动作前原子写 pending，随后采集并记录实际执行前/后快照、校验原生回执身份与 verified 状态并写 final。全量世界状态在运行中会自然漂移，因此请求决策哈希与实际分发哈希同时保留，动作安全由游戏线程中的动作级 fresh 前置条件负责；漂移会强制后续重规划。相同 final 收据可长期幂等返回；nonce 冲突拒绝；孤立 pending 永不重发并转为结果不确定的阻断回执。隐藏静音 E 盘 `3/3` 产品冒烟位于 `artifacts/product-executor-smoke/product-executor-20260831-235239/summary.json`，服务测试 `10/10`，全量 Core `2191/2191`、Backend `162/162`、Release `0 warnings / 0 errors`。

当前训练阻塞已从“无产品执行器”转为“尚未重建并冻结正式轨迹 manifest/checkpoint，服务器启动与恢复探针尚未完成”。下一步必须使用独立新存档和 62 项 allowlist 生成真实 `policy_decision_trajectory.v1`，验证 Product Executor 回执、数据集/checkpoint 哈希与断点恢复后，才启动服务器全量训练。

## 2026-08-30 联机钱包执行准入（EVD-310）

`multiplayer.manage_wallet` 已通过 read / candidate / compile / native runtime / output receipt 五门，但治理为 `PlayerCommandOnly`，不进入策略训练 allowlist。玩家命令层给出五类操作之一；转账还必须给出精确收款人、金额和第二级确认。模式、房主权限、参与者、余额、原生菜单响应键、LedgerBook 站位、路线、即时回执和次日结算均由 fresh snapshot 与机械执行层绑定，模型不能自主决定分钱、合并或转账。

隐藏静音 E 盘 `7/7` 原生矩阵覆盖五项即时命令及共享转独立、独立转共享的次日结算；生产执行仅使用原生 `ManorHouse.checkAction`、对话和数字输入，不直接写钱包状态。最新 schema 为 `157/140/17/0`；对账为 `200 registered / 216 semantic / 199 compiler-bound / 123 five-gate / 54 allowlist / 16 catalogued blocked / 0 Product Executor`。正式全量训练仍受剩余 16 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；Junimo Kart 完美代打维持后置，下一实际目录切片为 `multiplayer.send_chat`。

## 2026-08-30 卡利科雕像训练准入（EVD-309）

`mining.activate_calico_statue` 已通过 read / candidate / compile / native runtime / output receipt 五门并进入策略训练 allowlist。小模型只决定是否接受 fresh snapshot 投影的精确下一效果；沙漠节/骷髅洞/房主权限、日存档随机种子、效果栈、站位、移动和原生交互均由透明桥与机械执行层绑定。每次激活后必须以 live feedback 重规划，模型不得自行指定不同效果。

隐藏静音 E 盘矩阵 `18/18` 覆盖全部效果 ID、四档 Calico Egg 奖励、速度、完全恢复、正负效果栈、评分和单次地块转换；生产执行不写评分、效果、奖励、生命、耐力、Buff、地块或 RNG。最新 schema 为 `156/139/17/0`；对账为 `198 registered / 215 semantic / 197 compiler-bound / 121 five-gate / 54 allowlist / 17 catalogued blocked / 0 Product Executor`。正式全量训练仍受剩余 17 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一实际目录切片为 `multiplayer.manage_wallet`。

## 2026-08-30 赌场老虎机训练准入（EVD-308）

`minigame.play_slots` 已通过 read / candidate / compile / native runtime / output receipt 五门并进入策略训练 allowlist。小模型只决定是否在缺少 `(BC)126` 且 Deluxe Scarecrow 依赖开放时安排一次齐币下注；赌场权限、机器、路线、10/100 下注、Luck 概率分布、转轴、倍率、原生币差和退出均由 fresh snapshot 与机械执行层绑定。共享 `Game1.random` 的未来结果不作确定性伪预测，每次原生结算后以 live feedback 重规划。

隐藏静音 E 盘矩阵 `2/2` 验证 10 币无奖和 100 币单七 `x2`，图案、倍率、`ClubCoins` 差、`timesPlayedSlots +1` 与 Done 清理完全一致；生产执行不写 RNG、转轴、倍率、齐币或统计。最新 schema 为 `155/139/16/0`；对账为 `196 registered / 214 semantic / 195 compiler-bound / 119 five-gate / 53 allowlist / 18 catalogued blocked / 0 Product Executor`。正式全量训练仍受剩余 18 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一实际目录切片为 `mining.activate_calico_statue`。

## 2026-08-30 草原大王等价训练准入（EVD-307）

`minigame.play_prairie_king` 已通过 read / candidate / compile / equivalent runtime / native output receipt 五门并进入策略训练 allowlist。训练的标签是高层“是否、何时安排无伤通关”，不是逐帧射击动作；`executor.play_prairie_king` 仍为机械执行层。训练、联机陪玩和专用房主中的 AI actor 均使用相同的不可见 108000-tick 定时等价会话，禁止将其标成原生完美操作。

隐藏静音 E 盘冒烟验证原生 `Arcade_Prairie -> AbigailGame -> usePowerup(-3)` 结算，通关与无伤统计均精确 `+1`，`Beat_PK` 邮件由游戏自身产生。最新 schema 为 `154/138/16/0`；对账为 `194 registered / 213 semantic / 193 compiler-bound / 117 five-gate / 52 allowlist / 19 catalogued blocked / 0 Product Executor`。正式全量训练仍受剩余 19 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡。Junimo Kart 原生完美代打后置为训练完成后的 `PlayerCommandOnly`；下一实际目录切片为 `minigame.play_slots`。

## 2026-08-30 飞镖训练准入（EVD-306）

`minigame.play_darts` 已通过 read / candidate / compile / native runtime / output receipt 五门并进入策略训练 allowlist。小模型只决定是否取得下一枚尚未发放的海盗湾飞镖核桃；海盗夜、目标地图天气、路线、20/15/10 支限额、301 分六投方案、鼠标瞄准/充能/释放、结果对话和限量奖励都由 fresh snapshot 与机械执行层绑定。非海盗夜、3 枚奖励已完成、端点/路线/菜单忙碌或投影漂移会在上游或 fresh 编译阶段关闭。

隐藏静音 E 盘 `3/3` 原生矩阵覆盖全部三个限量核桃阶段，每轮均以 6 投和 `60,60,60,60,51,10` 完成。生产执行不直接写分数、投掷数、计时器、RNG、奖励或进度。最新 schema 为 `153/137/16/0`；对账为 `192 registered / 212 semantic / 191 compiler-bound / 115 five-gate / 51 allowlist / 20 catalogued blocked / 0 Product Executor`。正式全量训练仍受剩余 20 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一目录切片为 `minigame.play_junimo_kart`。

## 2026-08-30 抓娃娃机执行准入（EVD-305）

`minigame.play_crane_game` 已通过 read / candidate / compile / native runtime / output receipt 五门，但治理为 `PlayerCommandOnly`，只形成可验证的玩家命令执行能力，不进入策略训练 allowlist。玩家只授权一次 500g 会话；机器占用、路线、三次机会、实时奖品选择、横向/纵向释放时机、原生随机物理和奖励转移均由 fresh snapshot 与机械执行层处理。费用不足、少于 3 个空槽、机器占用、忙碌、路线或投影漂移会在上游或 fresh 编译阶段关闭。

隐藏静音 E 盘 `1/1` 原生冒烟完成 3 次机会并通过原生奖励菜单转移 2 件奖品，Money 精确 `-500`；生产执行仅使用 `MovieTheater.checkAction`、原生 Yes 对话、`UpdateTicking` D/S 输入和 `ItemGrabMenu`，不写结果状态。最新 schema 为 `152/136/16/0`；对账为 `190 registered / 211 semantic / 189 compiler-bound / 113 five-gate / 50 allowlist / 21 catalogued blocked / 0 Product Executor`。正式全量训练仍受剩余 21 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一目录切片为 `minigame.play_darts`。

## 2026-08-30 Calico Jack 训练准入（EVD-304）

`minigame.play_calico_jack` 已通过 read / candidate / compile / native runtime / output receipt 五门并进入策略训练 allowlist。小模型只决定是否执行一局由稀有稻草人依赖驱动的牌局；牌桌、路线、下注、初始牌、隐藏牌、未来抽牌、要牌/停牌序列、齐币结算和单局退出都由 fresh snapshot 与共享确定性模型机械绑定。缺少赌场权限、无 Rarecrow 需求、菜单/路线/牌局漂移、余额不足或仅剩 100 且下一局投影损失会在上游排除。

隐藏静音 E 盘 `3/3` 原生矩阵覆盖高注获胜、低注失败和首次要牌获胜；生产执行只使用赌场原生 `checkAction`、`DialogueBox` 和 `CalicoJack.receiveLeftClick`，不写 RNG、牌、齐币或结果。最新 schema 为 `151/135/16/0`；对账为 `188 registered / 210 semantic / 187 compiler-bound / 111 five-gate / 50 allowlist / 22 catalogued blocked / 0 Product Executor`。正式全量训练仍受剩余 22 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一目录切片为 `minigame.play_crane_game`。

## 2026-08-30 Field Office 调查训练准入（EVD-303）

`island.field_office_survey` 已通过 read / candidate / compile / native runtime / output receipt 五门并进入策略训练 allowlist。小模型只决定是否完成当前唯一未完成调查；问题身份、固定答案、端点、站位、路线、当日失败锁、核桃上限、瞬时 debris 生成、共享拾取和 finale 结算均由 fresh snapshot 与编译执行层机械绑定。已有核桃 debris、答错锁日、完成态、锁/菜单/教授/路线漂移会在上游排除。

隐藏静音 E 盘矩阵 `9/9` 覆盖 22/18、同日连续两题、错误答案锁日、原生 DayUpdate 重置、130 核桃无掉落和 finale。生产执行只经 `checkAction(FieldOfficeSurvey)` 与两个原生 `answerDialogue` 响应，不直接写游戏结果。最新 schema 为 `148/132/16/0`；对账为 `186 registered / 209 semantic / 185 compiler-bound / 109 five-gate / 49 allowlist / 23 catalogued blocked / 0 Product Executor`。正式全量训练仍受剩余 23 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一目录切片为 `minigame.play_calico_jack`。

## 2026-08-30 Field Office 化石捐赠闭环（EVD-302）

`island.field_office_donate` 已通过 read / candidate / compile / native runtime / output receipt 五门，但当前要求显式确认且未进入策略训练 allowlist。模型或玩家只选择“捐赠当前已拥有且原生可接受的一件化石”；具体背包槽、原生重复物品槽位顺序、Desk 端点、站位、路线、菜单点击、集合奖励、核桃标记和 finale readiness 均由 fresh snapshot 与编译执行层机械绑定。远程候选每次只走一个已解析连接器，并锁定同一背包物品和目标槽位直到原生捐赠成功。

隐藏静音 E 盘最终矩阵 `15/15` 覆盖 11 个显示槽、中心/蛇集合完成、蝙蝠/青蛙普通奖励及 130 核桃替代奖励。生产执行只经 `FieldOfficeDesk` 互斥锁、`Safari_Donate` 回答、`FieldOfficeMenu` 背包/精确 holder/OK 输入完成，不直接修改持久状态。最新 schema 为 `148/132/16/0`；对账为 `184 registered / 208 semantic / 183 compiler-bound / 107 five-gate / 48 allowlist / 24 catalogued blocked / 0 Product Executor`。正式全量训练仍受剩余 24 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡。下一语义切片为独立 `island.field_office_survey`；透明读已存在，但不得用 EVD-302 捐赠回执替代调查执行证据。

## 2026-08-30 住宅装修玩家命令闭环（EVD-301）

`housing.renovate` 已通过 read / candidate / compile / native runtime / output receipt 五门，但严格保持 `PlayerCommandOnly`，不进入正式策略训练 allowlist。模型或玩家命令层只给出精确装修 ID、区域、原因和确认；目录、要求、原生商店顺序、价格、首次购买退款语义、区域几何、阻挡、柜台站位和菜单输入均由 fresh snapshot 与编译执行层机械绑定。破坏动画另需独立破坏性确认。

隐藏静音 E 盘矩阵 `19/19` 覆盖实时 `Data/HomeRenovations` 的 18 个原版条目，以及一个负价、无 `FirstPurchase` 标记、不退款分支。生产执行只通过 Robin 原生对话、`ShopMenu("HouseRenovations")` 和 `RenovateMenu` 输入完成，不直接写钱、邮件、`NetInt`、地图、家具或事件。跨地图 continuation 以装修 ID、区域、原因和确认锁定同一目标，并在每次新快照排名时恢复 `PlayerCommand` 来源，终端原生执行成功后才结束。最新 schema 为 `146/130/16/0`；对账为 `182 registered / 206 semantic / 181 compiler-bound / 105 five-gate / 48 allowlist / 24 catalogued blocked / 0 Product Executor`，回归为 Core `2045/2045`、Backend `148/148`、Release `0 warnings / 0 errors`。正式全量训练仍受剩余 24 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `island.field_office_donate`。

## 2026-08-30 垃圾桶翻找训练准入（EVD-300）

`foraging.rummage_garbage` 已通过 read / candidate / compile / native runtime / output receipt 五门并进入 `StrategyValue` allowlist。小模型只决定是否翻找某个当前可达且安全的未检查垃圾桶；桶身份、地图位置、站位、路径、运气/书籍状态、确定输出、交付方式、安全槽、NPC 目击和任务回执都由编译执行层从 fresh snapshot 机械绑定。已检查、负友谊目击、数据/预测漂移、无安全槽和不可达目标在上游排除。

隐藏静音 E 盘矩阵 `9/9` 覆盖空结果、普通 debris、直接入包垃圾帽、Desert Festival 多 debris、两类排除、Linus 正向反应以及普通/特别收集任务。生产执行只调用一次原生 `GameLocation.checkAction`，不写 CheckedGarbage、统计、好感、库存、debris 或 RNG。最新 schema 为 `146/130/16/0`；对账为 `180 registered / 205 semantic / 179 compiler-bound / 103 five-gate / 48 allowlist / 25 catalogued blocked / 0 Product Executor`，回归为 Core `2039/2039`、Backend `145/145`、Release `0 warnings / 0 errors`。正式全量训练仍受剩余 25 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `housing.renovate`。

## 2026-08-30 普通树产品训练准入（EVD-299）

`foraging.harvest_tree_product` 已通过 read / candidate / compile / native runtime / output receipt 五门并进入 `StrategyValue` allowlist。小模型只决定是否收取当前可用普通树产品；树种、地点、站位、路径、安全空槽、确定掉落和随机附加掉落域全部由编译执行层机械生成并在 fresh snapshot 重绑。自定义树、数据漂移、未成熟、树桩、tapped、无种子、原生等级门不满足、摇动中、输出域不完整及不可达目标在上游直接排除。

隐藏静音 E 盘矩阵覆盖普通种子、秋季榛子、岛屿棕榈和三类排除分支。生产执行只调用一次原生 `GameLocation.checkAction`，以背包加 debris 守恒验收确定输出及至多一个有界可选输出，不读取或改写 RNG。最新 schema 仍为 `145/129/16/0`；对账为 `178 registered / 204 semantic / 177 compiler-bound / 102 five-gate / 47 allowlist / 26 catalogued blocked / 0 Product Executor`，回归为 Core `2035/2035`、Backend `144/144`。正式全量训练仍受剩余 26 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `foraging.rummage_garbage`。

## 2026-08-30 果树收获训练准入（EVD-298）

`foraging.harvest_fruit_tree` 已通过 read / candidate / compile / native runtime / output receipt 五门并进入 `StrategyValue` allowlist。小模型只决定是否在日计划中收获已就绪果树；位置、站位、路径、交互、live fruit、品质、雷击替换、数量与零经验回执全部由编译执行层机械生成并在 fresh snapshot 重绑。空树、未成熟、树桩、摇动中、自定义类型和不可达目标在上游直接排除，不产生无意义训练阻塞。

隐藏静音 E 盘矩阵覆盖单果普通、三果金星、雷击三煤炭、空树排除和摇动中排除；生产执行只调用一次原生 `GameLocation.checkAction`，不写任何结果状态。最新 schema 仍为 `145/129/16/0`；对账为 `176 registered / 203 semantic / 175 compiler-bound / 101 five-gate / 46 allowlist / 27 catalogued blocked / 0 Product Executor`，回归为 Core `2031/2031`、Backend `143/143`。正式全量训练仍受剩余 27 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `foraging.harvest_tree_product`。

## 2026-08-30 鱼塘管理玩家命令闭环（EVD-297）

`fishing.manage_fish_pond` 已完成五道证据闭环，但严格保持 `PlayerCommandOnly`，不进入正式训练 allowlist。玩家必须提供精确鱼塘、操作和原因，`empty_pond` 另需操作级确认；自动日计划继续只使用既有 `fishing.service_fish_ponds` 处理产出与请求，不会把换网装饰或清塘破坏性重置混入收益训练。

透明桥发布鱼塘管理状态、四种网样式、空手安全槽、精确站位、清塘前状态及反编译锁定的 reset/preserve 收据，菜单桥发布绑定鱼塘、确认状态和全部公共按钮。运行层复用共享 BFS，经作用域右键边沿和真实 `GameLocation.checkAction -> FishPond.doAction -> PondQueryMenu.receiveLeftClick` 执行；不构造菜单、不调用 `ClearPond`、不直接写鱼塘状态。隐藏静音运行 `runtime-fish-pond-management-20260830-013602` 覆盖换网和确认清塘两支。最新 schema 为 `145/129/16/0`，对账为 `174 registered / 202 semantic / 173 compiler-bound / 100 five-gate / 45 allowlist / 28 catalogued blocked / 0 Product Executor`，回归为 Core `2027/2027`、Backend `142/142`。正式全量训练仍受剩余 28 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `foraging.harvest_fruit_tree`。

## 2026-08-30 展览会转盘策略与原生随机执行闭环（EVD-296）

`festival.spin_wheel` 已完成五道证据闭环并进入 `StrategyValue` allowlist。模型只决定是否用绿方转盘补齐未获得 Fair Stardrop 的至少两枚星币缺口；编译器从 fresh snapshot 绑定节日、Buildings `308/309`、站位、节庆币、需求、零幸运 `22/30` 分布、有效 `LuckLevel`、数字菜单和 native contract。下注严格为 `min(remainingDemand, floor(festivalScore * 7 / 15))`，即等赔率零幸运 Kelly 比例，不把 50% 误标为 Kelly；`executor.spin_fair_wheel` 保持 `ExecutorCalibration` 与 policy confirmation。

运行层复用共享 BFS 和原生菜单输入，只经 `Event.checkAction -> DialogueBox(Green) -> NumberSelectionMenu -> WheelSpinGame` 启动，接受原版随机胜负并核对精确 `+/- wager`、结果文字与退出。最终隐藏静音运行 `runtime-fair-wheel-spin-20260830-005054` 用两次 `466` 星币下注覆盖胜负两支，festivalScore 分别 `1000->1466` 与 `1000->534`；生产代码不写 RNG、旋转、结算或结果。最新 schema 为 `145 required / 129 readable / 16 contextual / 0 blocking`，对账为 `173 registered / 202 semantic / 172 compiler-bound / 99 five-gate / 45 allowlist / 29 catalogued blocked / 0 Product Executor`，回归为 Core `2023/2023`、Backend `138/138`、Release `0 warnings / 0 errors`。正式全量训练仍受剩余 29 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `fishing.manage_fish_pond`。

## 2026-08-29 展览会力量小游戏策略与原生执行闭环（EVD-295）

`festival.play_strength_game` 已完成五道证据闭环并进入 `StrategyValue` allowlist。模型只在扣除未领取陈列奖励后，未获得 Fair Stardrop 的缺口恰好为 `1` 星币时决定是否进行这次免费尝试；其他缺口不会把单次固定一币的小游戏误当作高效循环。编译器从 fresh snapshot 绑定节日实例、Buildings `540`、站立 X=`29`、力量/速度/方向、动画和计时合同、商店需求及 native contract；`executor.play_fair_strength_game` 严格为 `ExecutorCalibration`。

运行层复用共享 BFS，等待移动输入结算后通过真实 `Event.checkAction` 打开 `StrengthGame`，以点击后恰好 `9` 次原生更新预测满力量窗口并只调用一次原生点击。隐藏静音样本覆盖两种初始速度：`64/+4 -> 100` 与 `72/+3 -> 99`，均由原版把 festivalScore `1999->2000` 并完成结果对话/退出；生产代码不直接写力量、得分、计时器、菜单或位置。最新 schema 为 `143 required / 127 readable / 16 contextual / 0 blocking`，对账为 `171 registered / 201 semantic / 170 compiler-bound / 97 five-gate / 44 allowlist / 30 catalogued blocked / 0 Product Executor`，回归为 Core `2018/2018`、Backend `138/138`、Release `0 warnings / 0 errors`。正式全量训练仍受剩余 30 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `festival.spin_wheel`。

## 2026-08-29 展览会靶场策略与原生执行闭环（EVD-294）

`festival.play_slingshot_game` 已完成五道证据闭环并进入 `StrategyValue` allowlist。模型只决定是否投入 50g 和原版 50 秒会话补齐未获得 Fair Stardrop 的星币缺口；尚未领取的展览陈列奖励会先从缺口扣除。编译器从 fresh snapshot 绑定节日实例、交互/站立图块、金额、星币、四段时序、79 个目标、Dialogue key 和 native contract；`executor.play_fair_slingshot_game` 严格为 `ExecutorCalibration`。

运行层复用共享移动与普通矿井弹弓唯一的瞄准补丁，在原生 TargetGame 物理更新前预测拦截点并发送按下/蓄力/释放输入。验收精确核对 50g、临时弹弓和弹药、shots/success、原版 accuracy 分母、75/85/90/95/100% 倍率、得分、40 分奖励门、280 分封顶、节庆返回和临时物品清理，不直接写任何结果。隐藏静音样本为 `48/48` 命中、raw `95`、accuracy `102`、final `380`、`500` 星币。最新 schema 为 `142 required / 126 readable / 16 contextual / 0 blocking`，对账为 `169 registered / 200 semantic / 168 compiler-bound / 95 five-gate / 43 allowlist / 31 catalogued blocked / 0 Product Executor`，回归为 Core `2013/2013`、Backend `138/138`、Release `0 warnings / 0 errors`。正式全量训练仍受剩余 31 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `festival.play_strength_game`。

## 2026-08-29 展览会钓鱼小游戏策略与原生执行闭环（EVD-293）

`festival.play_fishing_game` 已完成五道证据闭环并进入 `StrategyValue` allowlist。模型只决定是否投入 50g 和原版 100 秒来补齐未获得 Fair Stardrop 的星币缺口；上游会扣除尚未领取的展览陈列奖励，不会为其他装饰商店行无限重复。编译器从 fresh snapshot 绑定节日实例、交互/站立图块、金额、星币、缺口、时长、Dialogue key 和 native contract；`executor.play_fair_fishing_game` 严格为 `ExecutorCalibration`。

运行层复用共享移动和普通钓鱼预测输入，在游戏物理更新前控制原生 BobberBar。运行验收不把随机完美率误作执行器稳定性：必须精确验证 50g、真实 FishingGame、原版 raw score + perfection bonus + triple-perfect multiplier、星币公式、节日返回和临时钓具清理，完美数/有效鱼数则作为收益反馈。最终隐藏静音样本为 `5/5` 完美、`364` 分、`432` 星币。最新 schema 为 `141 required / 125 readable / 16 contextual / 0 blocking`，对账为 `167 registered / 199 semantic / 166 compiler-bound / 93 five-gate / 42 allowlist / 32 catalogued blocked / 0 Product Executor`。Core `2008/2008`、Backend `138/138`、Release `0 warnings / 0 errors`。正式全量训练仍受剩余 32 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `festival.play_slingshot_game`。

## 2026-08-29 展览会陈列策略与原生执行闭环（EVD-292）

`festival.manage_grange_display` 已完成五道证据闭环并进入 `StrategyValue` allowlist。模型只决定是否在展览会准备一等奖陈列或在评审后取回；透明桥与编译器从 fresh snapshot 绑定共享展台、库存单位、实际售价、品质、八类多样性、九件数量分、Mayor 短裤、评审状态、交互图块、互斥锁和下一次唯一机械操作。`executor.manage_grange_display` 严格为 `ExecutorCalibration`，每个快照只允许一次原生放入/取回，不进入策略训练，也不得启动评审。

隐藏静音隔离运行 `10/10` 通过：九次原生菜单放入达到 `124` 分，超过一等奖阈值 `90`，评审后一次原生取回；生产链复用共享 BFS/连续移动与 `Event.checkAction -> StorageContainer -> grangeMutex`，不直接写展台、库存、评分或评审状态。最新 schema 为 `140 required / 124 readable / 16 contextual / 0 blocking`，对账为 `165 registered / 198 semantic / 164 compiler-bound / 91 five-gate / 41 allowlist / 33 catalogued blocked / 0 Product Executor`。Core `2003/2003`、Backend `138/138`、Release `0 warnings / 0 errors`。正式全量训练仍受剩余 33 个目录动作、Product Executor、长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收阻挡；下一语义切片为 `festival.play_fishing_game`。

## 2026-08-29 传送图腾执行器校准（EVD-291）

`executor.use_warp_totem` 已完成五道执行证据闭环，训练角色严格为 `ExecutorCalibration`。上游策略只决定目的地和“是否值得消耗”；机械链从 fresh snapshot 重绑五种精确库存、Farm 地图属性/农场回退、固定目的地、主动与被动节日路由、地图边缘修正、2000ms 动画和 1000ms 原生回调。会消耗但不传送的节日前分支、联机 ReadyCheck、精确目的地重复使用和基础物品门失败均在消费前排除。

隐藏静音隔离运行五变体 `5/5` 验证原生单物品消费、68 个即时效果精灵、五个精确落点和最终角色状态恢复。最新 schema 为 `139 required / 123 readable / 16 contextual / 0 blocking`，对账为 `163 registered / 197 semantic / 162 compiler-bound / 89 five-gate / 40 allowlist / 34 catalogued blocked / 0 Product Executor`。该切片不进入训练 allowlist，也不解除 Product Executor、剩余 34 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等阻挡。下一语义切片为 `festival.manage_grange_display`。

## 2026-08-29 宝藏图腾执行器校准（EVD-290）

`executor.use_treasure_totem` 已完成五道执行证据闭环，训练角色严格为 `ExecutorCalibration`。上游策略只决定是否值得在当前位置消耗图腾；机械链从 fresh snapshot 重绑精确库存、室内门、中心周围 16 格原生候选环、每格可生成原因、`TreasureTotemsUsed` 和时序合同。公共物品门失败、室内或零可生成格会在消费前排除，不能依赖下游失败补救。

隐藏静音隔离运行验证原生 `16/16` 宝藏点生成、图腾 `2->1`、世界计数 `0->1` 和地点宝藏点 `5->21`。生成结果由既有宝藏点读取与挖掘链继续处理，不训练也不复制机械挖掘。最新 schema 为 `138 required / 122 readable / 16 contextual / 0 blocking`，对账为 `162 registered / 197 semantic / 161 compiler-bound / 88 five-gate / 40 allowlist / 35 catalogued blocked / 0 Product Executor`。该切片不进入训练 allowlist，也不解除 Product Executor、剩余 35 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等阻挡。下一语义切片为 `executor.use_warp_totem`。

## 2026-08-29 回城魔杖执行器校准（EVD-289）

`executor.use_return_scepter` 已完成五道执行证据闭环，训练角色严格为 `ExecutorCalibration`。上游只决定“现在是否值得立即回家”；机械链从 fresh snapshot 重绑精确 `Wand`、当前角色自己的 `FarmHouse`/`Cabin`、门前格、稳定输入门、即时工具调用、1000ms 原生回调和最终状态。已在落点、住宅不可解析、浴衣、桥上或执行瞬态不稳定时均在候选/编译阶段排除，不能等到原生回调后再补救。

隐藏静音隔离运行验证房主分支的原生落点、29 精灵即时状态、最终显示/无敌/移动恢复和可复用库存。最新 schema 为 `137 required / 121 readable / 16 contextual / 0 blocking`，对账为 `161 registered / 197 semantic / 160 compiler-bound / 87 five-gate / 40 allowlist / 36 catalogued blocked / 0 Product Executor`。该切片不进入训练 allowlist，也不解除 Product Executor、剩余 36 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等阻挡。下一语义切片为 `executor.use_treasure_totem`。

## 2026-08-29 雨水图腾执行器校准（EVD-288）

`executor.use_rain_totem` 已完成五道执行证据闭环，但训练角色严格为 `ExecutorCalibration`。上游只决定未来天气是否值得消耗图腾；机械链从 fresh snapshot 绑定精确库存、上下文许可/重定向、天气状态归属、明日日期、换日最终天气、动画和对话结算。官方 Wiki 关于季节首日的可见规则已用锁定 1.6.15 的 `Game1.getWeatherModificationsForDate` 复核并扩大为完整默认上下文覆盖门，不能只凭即时 `WeatherForTomorrow=Rain` 认定有效。

隐藏静音隔离运行覆盖 Default、Desert->Default、Island 和默认节日前拒绝四条分支。最新 schema 为 `136 required / 120 readable / 16 contextual / 0 blocking`，对账为 `160 registered / 197 semantic / 159 compiler-bound / 86 five-gate / 40 allowlist / 37 catalogued blocked / 0 Product Executor`。该切片不进入训练 allowlist，也不解除 Product Executor、剩余 37 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等阻挡。下一语义切片为 `executor.use_return_scepter`。

## 2026-08-29 怪兽香水执行器校准（EVD-287）

`executor.use_monster_musk` 已完成五道执行证据闭环，但训练角色严格为 `ExecutorCalibration`。小模型或上游战斗目标只决定是否需要提高怪物密度；精确库存槽、Buff 24 当前状态、消费、朝向、动画时序和刷新回执全部由机械编译/执行链负责。普通矿井与火山地牢都按在线玩家 Buff 24 将怪物生成率乘以 2，普通矿井与驱怪 Buff 23 的组合继续服从原生优先级。

隐藏静音隔离运行覆盖“无 Buff 首次施加”和“已有 Buff 替换刷新”两条分支。最新 schema 为 `135 required / 119 readable / 16 contextual / 0 blocking`，对账为 `159 registered / 197 semantic / 158 compiler-bound / 85 five-gate / 40 allowlist / 38 catalogued blocked / 0 Product Executor`。该切片没有把机械原语直接加入策略训练，也不解除 Product Executor、剩余语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等阻挡。下一语义切片为 `executor.use_rain_totem`。

## 2026-08-29 马笛执行器校准（EVD-286）

`executor.use_horse_flute` 已完成五道执行证据闭环，但训练角色严格为 `ExecutorCalibration`：策略只需决定是否用马笛，机械层从 fresh snapshot 重绑库存、限制掩码、拥有马匹身份、邻近状态、朝向和延迟结果。模型不得预测或复制原生 team event、mutex 与传送副作用。

隐藏静音隔离运行覆盖远程 1500ms 召回与邻近成功无传送两条分支，并验证同一马匹 GUID、精确落点、朝向规则和可复用库存。最新 schema 为 `134 required / 118 readable / 16 contextual / 0 blocking`，对账为 `158 registered / 197 semantic / 157 compiler-bound / 84 five-gate / 40 allowlist / 39 catalogued blocked / 0 Product Executor`。因此该切片不解除 Product Executor、剩余 39 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等全量训练阻挡。下一语义切片为 `executor.use_monster_musk`。

## 2026-08-29 烟花玩家命令边界（EVD-285）

`executor.use_firework` 已闭合五道执行证据，但训练 allowlist 保持 `40`。烟花是显式玩家表达命令，不是第三年爷爷 21 分路线、日循环或资源规划的自主欲望；相关运行样本只属于 `player_command_only_executor_evidence`。模型不能学习或猜测共享 RNG 的精确下一值。

隐藏静音隔离运行已验证 `(O)893/(O)894/(O)895` 三个分支、原生 5 精灵图、目标格冲突、随机域和精确单件消耗。最新 schema 为 `133 required / 117 readable / 16 contextual / 0 blocking`，对账为 `157 registered / 197 semantic / 156 compiler-bound / 83 five-gate / 40 allowlist / 40 catalogued blocked / 0 Product Executor`。因此该切片不解除 Product Executor、剩余 40 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等全量训练阻挡。下一语义切片为 `executor.use_horse_flute`。

## 2026-08-29 秘密纸条执行器校准（EVD-284）

`executor.read_secret_note` 已完成五道执行证据闭环，但训练角色仍为 `ExecutorCalibration`：它校准透明桥、编译器和执行器能否忠实完成一个已由上游选择的纸条读取，不让策略模型学习原生随机数、菜单构造或库存扣减。普通纸条与日记残页的选择均在 fresh snapshot 中机械计算，小模型不能伪造 note id 或任务副作用。

隐藏静音隔离运行覆盖多未读种子抽取、任务 30、任务 29 和普通日记残页四条分支。最新 schema 为 `132 required / 116 readable / 16 contextual / 0 blocking`，对账为 `156 registered / 197 semantic / 155 compiler-bound / 82 five-gate / 40 allowlist / 41 catalogued blocked / 0 Product Executor`。因此该切片不解除 Product Executor、剩余 41 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等全量训练阻挡。下一语义切片为 `executor.use_firework`。

## 2026-08-29 草种放置执行器校准（EVD-283）

`executor.plant_grass` 已完成五道执行证据闭环，但训练角色严格为 `ExecutorCalibration`：它证明动作编译和执行器能忠实完成上游给定的普通/蓝草精确布局，不为策略模型生成“应该在哪里种草”的价值标签。上游必须给出用途、精确地块和时间预算；编译器从 fresh snapshot 重绑所有机械字段。

隐藏静音隔离运行已验证 `(O)297 -> Grass(1,4)` 与 `(O)BlueGrassStarter -> Grass(7,4)`，库存精确减一且透明后状态可读。最新 schema 为 `131 required / 115 readable / 16 contextual / 0 blocking`，对账为 `155 registered / 197 semantic / 154 compiler-bound / 81 five-gate / 40 allowlist / 42 catalogued blocked / 0 Product Executor`。因此该切片不解除 Product Executor、剩余 42 个语义动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等全量训练阻挡。下一语义切片为 `executor.read_secret_note`。

## 2026-08-29 Drum Block 玩家命令边界（EVD-282）

`world.tune_drum_block` 已闭合五门执行证据，但训练 allowlist 保持 `40`。调音属于显式玩家表达/谜题布置，不是自主日计划欲望；运行数据只属于 `player_command_only_executor_evidence`。路过自动播放是独立对象邻接回调，不生成第二个训练动作。

当前权威状态为 `154 registered / 197 semantic / 153 compiler-bound / 80 five-gate / 43 catalogued blocked / 0 Product Executor`。该切片不解除 Product Executor、剩余 43 个动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等全量训练阻挡。下一语义切片为 `executor.plant_grass`。

## 2026-08-29 Flute Block 玩家命令边界（EVD-281）

`world.tune_flute_block` 已闭合五门执行证据，但训练 allowlist 保持 `40`。调音是玩家表达/谜题布置命令，不是自主日计划欲望；运行数据只属于 `player_command_only_executor_evidence`。路过自动播放是对象邻接回调，不生成第二个训练动作。

当前权威状态为 `153 registered / 197 semantic / 152 compiler-bound / 79 five-gate / 44 catalogued blocked / 0 Product Executor`。该切片不解除 Product Executor、剩余 44 个动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等全量训练阻塞。

## 2026-08-29 Farm Computer 透明信息与训练边界（EVD-280）

`farming.read_farm_computer_report` 已闭合五门执行证据，但训练 allowlist 保持 `40`。透明桥已经按原生根地点语义直接发布报告的全部结构化来源和精确本地化摘要，因此策略模型可直接使用这些状态；打开 Farm Computer 只服务显式玩家查看，不得制造“先读菜单才能决策”的训练依赖。

运行时只复用共享移动器并调用一次原生地点交互，验证 500ms 延迟 `DialogueBox`、报告摘要、对象身份及槽位恢复。当前权威状态为 `152 registered / 197 semantic / 151 compiler-bound / 78 five-gate / 45 catalogued blocked / 0 Product Executor`。该切片减少一个执行缺口，但不解除 Product Executor、剩余 45 个动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等全量训练阻塞。

## 2026-08-29 Mini-Obelisk 校准边界（EVD-279）

`movement.use_mini_obelisk` 已闭合五门执行证据，但不增加训练 allowlist。它的作用是校准“机械路由原语能否严格复刻原生配对、目标和落点”，而不是让策略模型学习是否想传送。默认策略候选排除该动作；只有显式启用执行器校准候选时才发布，运行结果标记为 `executor_calibration_only_not_strategy_desire`，数据清洗不得把成功、耗时或落点当作策略偏好标签。

当前准入计数仍为 `40`，权威状态为 `151 registered / 197 semantic / 150 compiler-bound / 77 five-gate / 46 catalogued blocked / 0 Product Executor`。因此这次闭环减少了一个原版动作缺口，但没有解除 Product Executor、剩余 46 个动作、正式长期轨迹、独立存档评测和第三年爷爷 21 分长跑验收等正式全量训练阻塞。

## 2026-08-28 Auto-Grabber 训练准入与兼容占位边界（EVD-277 / EVD-278）

`animals.collect_auto_grabber_contents` 已进入训练 allowlist。模型只决定是否在当前日计划中收取；精确对象、站位、安全槽、held Chest 全部堆栈身份、累计背包容量、转移集合和留存集合均由新鲜快照与编译器决定。空容器、全部物品不可接纳或无安全站位不会生成候选。生产执行只调用原生对象交互并向 `ItemGrabMenu` 发送原生点击，验收要求源集合严格等于“已转移 + 留存”、背包回执匹配、对象/Chest 身份不变且菜单关闭。

Lantern/Raft 是分母外兼容占位，不是训练缺口，也不得生成负样本。当前准入计数为 `40`，完整权威状态为 `150 registered / 197 semantic / 149 compiler-bound / 76 five-gate / 47 catalogued blocked / 0 Product Executor`。这只增加一个经过原生运行验证的策略动作，不改变正式全量训练仍受 Product Executor、剩余 47 个动作、正式轨迹 manifest/checkpoint、独立存档评测及第三年 21 分长跑验收阻塞的结论。

## 2026-08-28 喂食斗训练准入与木筏分母修正（EVD-275 / EVD-276）

`animals.withdraw_feed_hopper_hay` 已通过五门证据并进入训练 allowlist。训练样本只允许来自“当前动物屋至少有一只未喂动物、原生精确取草量为正、背包可接纳、菜单与站位安全”的候选；编译器必须从同一新鲜快照重绑根料仓、动物数、容量、已摆干草、取草量、安全槽和站位。生产运行只调用一次原生 `GameLocation.checkAction`，并以料仓 `-N`、背包 `(O)178 +N` 的守恒回执验收。E 盘隐藏静音运行 `runtime-feed-hopper-20260828-130723` 验证 `N=8`。

不可达的 `Raft` 遗留类型不属于训练缺口。锁定 1.6.15 没有原版获取或调用入口，因此该历史检查点把语义分母校正为 `198`；当前由 EVD-277 以分母外兼容占位保留原生表面。该检查点状态为 `149 registered / 198 semantic / 148 compiler-bound / 75 five-gate / 39 allowlist / 49 catalogued blocked / 0 Product Executor`；这仍不解除 Product Executor、正式数据 manifest/checkpoint、独立存档评测和第三年 21 分长跑阻塞。

## 2026-08-28 PlayerCommandOnly 原生声音交互边界

`world.play_singing_stone` 已闭合五门执行证据，但正式训练准入保持关闭。锁定 1.6.15 原生语义只允许透明发布共享 RNG 的 24 项均匀音高分布，不允许预读或猜测下一音高。显式玩家命令经确认后，编译器重绑精确 `(BC)94`、站位和安全槽，运行时通过共享移动器调用一次原生地点交互并验证 `shakeTimer=100`、对象身份与槽位恢复。其证据范围为 `player_command_only_executor_evidence`，默认候选、策略请求和训练清洗分别通过生成、授权与类型化排除三层阻断。当前 allowlist 仍为 `38`，没有因为该执行器验证而增长。

状态日期：2026-08-01

## 1. 当前结论

项目已经具备可运行的工程闭环：

`透明快照 -> 候选生成 -> 日计划 -> 动作编译 -> 原生执行 -> 前后状态 -> 训练记录`

但这不等于已经具备正式全量训练条件。当前的主要阻塞不是“再跑一次短训”，而是训练准入证据、全量候选覆盖、正式轨迹数据和真实模型提供器尚未同时闭合。

短训只允许用于验证数据管线、显存、检查点和推理接口，不能作为训练成果汇报。

### 调用来源硬边界

`capability_registry.v3` 将“具备执行能力”和“允许策略模型选择”分开。`PlayerCommandOnly` 动作可以拥有完整透明读取、编译器、运行时和回执，但必须同时满足：默认候选不发布、策略来源请求由安全门阻断、训练清洗以类型化原因排除、仅 `InvocationSource=PlayerCommand` 的显式玩家请求可继续原有授权链。装饰轮转、建筑外观、家具放置、标牌展示物与文字编辑属于该类；它们的运行证据不得被解释成策略训练准入。

机械但具有正常日循环价值的动作不因此被排除。例如 `farming.collect_slime_ball` 只让模型选择当前合法目标，随机种子、产量、站位、空槽、原生交互和后续 debris 拾取均由透明桥与编译器绑定，因此在 EVD-272 五门证据闭合后可以进入有界策略训练。

2026-08-02 进展：`capability_registry.v2` 已实现五门证据、证据 ID、范围和类型化排除原因，空 allowlist 会使注册表初始化失败。该阶段准入项为 `mining.reach_depth`、`mining.obtain_skull_key`、`inventory.transfer_item`、`social.talk_npc` 和 `social.gift_npc`，每项都仅限其登记的 EVD 范围；这不是任意深度、任意库存、全矿洞或全社交完成声明。EVD-202 仅把 EVD-106 已验证的普通矿井 119 -> 120 层、原生骷髅钥匙宝箱领取和退出链登记为 `mining.obtain_skull_key` 五门证据，不覆盖沙漠矿洞、采石场矿洞金镰刀或火山矿洞。EVD-195 已闭合 `recovery.stabilize_day` 的滚动跨图回家和原生终端睡眠运行/输出门，但该项按策略仍是校准型高层动作，不进入训练白名单。EVD-196 已闭合当前已加载原版 NPC 的滚动远端送礼和普通单件礼物栈归零。EVD-197 又使旧聚合训练器、默认/显式排序和预测 API 统一经过生成式 allowlist。EVD-198/EVD-199 已建立正式策略轨迹契约并接入 LiveTrainingLoop 的有效排序/队列/源哈希绑定。EVD-200 已完成确定性清洗、冲突去重、按存档/日切分、SHA-256 清单、不可变版本锁和日/季/年/爷爷 21 分回报回填；EVD-201 已完成 C# 结构化排序器和检查点往返。未闭合跨度保留为 `null/pending`，不得猜测标签。当前 E 盘还没有真实策略轨迹和跨度观测，因此这是数据治理与模型基础设施就绪，不是正式数据集或训练完成。下一步继续按权威字典依赖顺序扩大五门准入；在真实长期 rollout 和其余正式准入条件闭合前仍不能启动正式全量训练。

2026-08-02 EVD-203 当前增量：`volcano.reach_caldera` 已独立绑定 EVD-190/EVD-191 的火山
0..9 滚动原生动作、目的化战斗与 Caldera 终态。它不借用普通矿井、Skull Cavern 或采石场
矿洞金镰刀证据，也不让模型控制机械原语。当前准入为 `mining.reach_depth`、
`mining.obtain_skull_key`、`volcano.reach_caldera`、`inventory.transfer_item`、
`social.talk_npc`、`social.gift_npc` 六个有界范围；生产轨迹、跨度观测、正式 manifest 和
checkpoint 仍不存在，因此正式全量训练仍然阻塞。

2026-08-02 EVD-204 当前增量：`skills.read_books` 已绑定 EVD-124 的六类原版基础书籍分支和七用例
原生矩阵。治理目录现在同时识别日计划 option 展开与动作队列直接编译，仍只保留唯一
`read_inventory_book -> executor.read_book -> wait_ticks` 链。该切片完成时训练 allowlist 为七个有界范围；
自定义书籍覆盖和畸形模组标签不在准入内。

2026-08-02 EVD-205 当前增量：`foraging.harvest_ginger` 已绑定 EVD-119 的原版当前地图精确姜收获
矩阵，复用唯一 `harvest_ginger -> executor.harvest_ginger` 链。覆盖干燥普通锄、雨天 Efficient 且
背包满后的 debris 输出，以及体力不足上游排除；自定义 Hoe/Crop/HoeDirt 和其他采集族不在准入内。
该切片完成时训练 allowlist 为八个有界范围，正式生产轨迹、manifest、checkpoint 与 Product Executor 仍不存在。

2026-08-02 EVD-206 当前增量：`foraging.harvest_bushes` 已绑定 EVD-120 的原版当前地图精确 Bush
六用例矩阵，复用唯一 `harvest_bush -> executor.harvest_bush` 链。普通浆果、Botanist 浆果、茶叶和
金核桃原生成功，已领取金核桃与摇动冷却在上游排除；自定义 Bush 和 town bush 不在准入内。
该切片完成时训练 allowlist 为九个有界范围，正式全量训练的其他阻塞不变。

2026-08-02 EVD-207 当前增量：`mining.claim_reward_chests` 已绑定 EVD-122 的已加载原版 MineShaft
精确奖励箱范围，复用唯一领取链。固定奖励、星之果实与强制随机奖励完成原生领取和清箱；骷髅钥匙
特殊箱、金镰刀祭坛与未知箱体不在准入内。金镰刀的完整运行证据不覆盖其显式玩家确认门，因此仍
不得进入策略训练。该切片完成时训练 allowlist 为十个有界范围。

2026-08-02 EVD-208 当前增量：`foraging.pan_ore_spot` 已绑定当前地图精确活动矿点和实时 Pan 奖励
投影。铜盘、钢盘两次原生生命周期验证了精确奖励、收货统计、TimesPanned、采矿/采集 XP 与矿点
消费；所有候选仍必须通过当前 Pan 状态与隔离 RNG 精确投影，不使用固定奖励表。当前训练 allowlist
在该切片完成时为十一个有界范围。

2026-08-02 EVD-209 当前增量：`fishing.collect_crab_pots` 已绑定当前地图已就绪原版基础 `CrabPot`
的精确原生收取生命周期，复用唯一收取链。实时产物、Book of Crabbing 确定性翻倍、背包入账、
Fishing XP、`caughtFish` 统计以及 bait/ready/tile-index 复位均纳入读、候选、编译、运行和输出证据。
未就绪、背包拒收、投影不完整与自定义子类失败关闭；放置和补饵不在该范围。当前训练 allowlist
在该切片完成时为十二个有界范围。

2026-08-03 EVD-210 当前增量：`fishing.service_fish_ponds` 已绑定已完成原版基础 `FishPond` 的
产物收取与人口请求双分支。产物保持原生优先；请求逐件消耗透明绑定物品，并核验人口上限、解锁
门槛、刷新计时和 Fishing XP。请求的 `PolicyAuthorizationRequired` 表示模型只能在授权策略内决策，
不同于会阻断训练准入的 `ExplicitUserConfirmationRequired`；运行时约束没有放宽。当前训练 allowlist
为十三个有界范围。

2026-08-03 EVD-211 当前增量：`foraging.collect_spawned_objects` 已绑定当前加载地图中精确原版基础
`StardewValley.Object` 的原生拾取链。隔离运行矩阵覆盖普通、Botanist、确定性 Gatherer 双倍、特殊
`724519` 和动物屋内部五类，并逐项核验数量、品质、Foraging/Farming XP。训练请求现完整运输这些
上游投影，运行层不再用自行重算掩盖字段断点。Lewis 地下室 `(O)789` 还有生成 Bat 和音画状态副作用，
在透明建模前于读层和运行层失败关闭。当前训练 allowlist 为十四个有界范围。

## 2. “正式全量训练”的定义

正式全量训练只训练模型应当决定的内容：

- 从当前合法候选中选择高层目标、目标对象和策略参数；
- 安排必要任务、附加任务、资源预算和时间预算；
- 处理经济、剧情、收集、关系与长期目标之间的权衡；
- 在不确定地图或随机事件中选择目标与退出条件。

以下内容不进入策略模型的自由输出空间：

- WASD、转向、挥刀、工具使用、拾取、开门、柜台交互等机械输入；
- BFS、动态避障、可清除障碍估时、安全窗口、补血和战斗微操；
- 已确定布局后的农场维护、固定机器收取与补料；
- 动作合法性、时间许可、资源许可和不可逆操作授权。

这些由动作编译器和执行器确定性完成，并通过校准数据持续验证。模型只能输出受类型约束的候选 ID 和必要参数，不能直接生成任意按键脚本。

“全量”是指训练所有已经通过准入证据的模型级候选，并覆盖从新存档到第三年爷爷评分 21 分目标所需的长期轨迹；不是训练所有登记名称，更不是把执行器原语混入策略训练。

## 3. 正式训练前的四个工程包

### 3.1 训练准入与证据注册表

为每个模型级候选建立五道独立门：

1. `read`：透明桥能实时读取决策所需字段；
2. `candidate`：上游能在正确时间、地点和资源条件下生成或排除候选；
3. `compile`：动作编译器能生成有界、可审计的动作队列；
4. `runtime`：原生执行器完成过真实运行验证；
5. `output`：结果、失败原因、耗时和状态变化能完整回写。

必须区分：

- 已实现但证据尚未登记；
- 已登记但仅有模拟证据；
- 真实运行已通过；
- 确有实现缺口；
- 因不可逆授权或环境限制而暂不准入。

退出条件：

- 训练 allowlist 非空；
- allowlist 内每项五门全通过；
- allowlist 外每项都有类型化排除原因；
- 候选生成器只向训练暴露 allowlist；
- 空 allowlist 不得让测试以“空集合成立”方式误通过。

### 3.2 能力缺口闭合与候选扩展

先用证据注册表找出真实缺口，再补代码，避免把“未登记”误判为“未实现”。重点包括：

- 普通任务、特别订单与候选/结果的完整绑定；
- Joja、房屋升级等不可逆路径的隔离和授权；
- 钓鱼、矿井、骷髅洞、火山等长链随机环境；
- 工作台、远端库存、箱子、机器和建筑布局；
- 特殊、随机、条件性机器的剩余矩阵；
- 社交、剧情、收集和跨地图目标；
- 新存档到第三年 21 分目标的完整日循环。

#### 3.2.1 共享原生执行底座硬门

在继续扩展目标族前，先闭合所有动作共同依赖的执行底座。该硬门不增加模型自由度，只保证机械展开不会制造错误训练反馈：

- 行走使用持续移动租约，方向切换原子完成；快照、模型和外部编排等待不得造成掉键；
- 工具、武器、交互和菜单使用统一输入仲裁与原生动作生命周期；
- 每个动作只在原生动画和状态允许时进入下一阶段，禁止直接世界状态修改和人为延时伪装；
- 最近数秒的输入、位置、朝向、`UsingTool`、`CanMove`、动画、碰撞和原语状态进入有界环形缓冲，只在异常时落盘；
- 底座由确定性夹具验证，各目标族只组合并补充领域终态，不复制底层输入状态机。

五门证据回答“该候选是否能透明读取、生成、编译、执行和回写”，原生可见符合性回答“执行过程是否真的像原版玩家输入”。正式训练要求两者同时通过。服务器/后台运行不能替代本地可见短测：前者负责长周期逻辑、恢复、死锁和资源边界，后者负责动画、步态、按键和交互节奏。

退出条件：

- 每个目标族至少有一条真实闭环证据；
- 编译失败和运行失败可分类恢复，不产生无动作污染；
- 上游能排除已知不可能候选，不依赖下游反复阻塞；
- 不可逆动作必须经过显式策略授权。
- 共享执行底座契约测试通过，且生产执行路径不存在直接世界状态修改；
- 每个准入目标族同时具有领域终态证据和对应的原生可见符合性证据。

### 3.3 正式策略轨迹数据

数据必须分层保存：

- `policy`：模型级候选、选择、预算、结果与长期回报；
- `mixed`：用于回放分析但不能直接进入策略训练的数据；
- `calibration`：动作编译器和执行器校准数据。

每个策略样本至少记录：

- 决策时刻的版本化特征；
- 当时全部候选，而不只是最终选择；
- 每个候选的排除原因、时间预算和资源预算；
- 选择结果、实际耗时、状态增减和失败类型；
- 与日、季节、年度及第三年 21 分目标关联的长期回报；
- 字段字典、候选词表、编译器和执行器版本。

清洗要求：

- 删除旧 schema、模拟结果、已知 bug、重复样本和无动作污染；
- 按存档与游戏日切分训练/验证/测试集，禁止随机拆散同一轨迹；
- 保留负例和未选候选，避免只学习成功动作；
- 原始快照可归档压缩，训练数据使用流式、去重后的结构化表示。

退出条件：

- schema 审计通过；
- 数据来源和版本可追溯；
- 三类数据不会互相倒灌；
- 训练、验证、测试之间没有同轨迹泄漏；
- 数据集哈希和清洗报告进入检查点清单。

2026-08-01 工程进展：`policy_decision_trajectory.v1` 已建立强类型候选全集、选择、版本、
存档/日切分键、执行结果和长回报槽位，并通过源哈希一致性与准入选择校验。LiveTrainingLoop
现把每个有效决策的模型计划、完整排序响应、编译队列和源状态哈希绑定到实际 verified/fresh
执行；派发前重规划立即替换绑定，动作后重规划只对下一动作生效。同一决策的重复编译原语、
源哈希漂移、候选 ID 缺失和非准入选择均失败关闭。`StardewAI.PolicyDataset` 现执行严格
schema/版本/准入/结果校验、语义冲突去重、`SHA-256(save_id:year:season:day)` 的
80/10/10 确定性切分、逐文件 SHA-256 清单和拒绝报告。LiveTrainingLoop 在原生日期跨越时写入
日/季/年闭合观测，只在第三年首次评价边界且透明 `farm.grandpa_score` 可读时写入唯一 21 分
终点；回填只使用已闭合跨度，终点之后的决策不反向获得标签。当前标准生产路径尚无真实轨迹
文件，因此本节的工程实现已闭合，但“真实数据来源与清单进入检查点”退出条件仍需长期 rollout
产物验证。

### 3.4 正式模型提供器、检查点与评估

正式基线采用 C# 结构化排序模型。模型面对有限候选排序问题，不需要先用语言模型生成动作。优先评估 ML.NET 的 LightGBM/FastTree 排序或等价结构化模型，并保持：

- C# 特征契约、候选词表和推理接口为权威；
- 确定性规则继续负责硬约束；
- 模型只替换候选评分，不替换候选生成、编译和执行；
- 训练与推理检查点可以完整往返加载。

ML.NET 的 LightGBM/FastTree 排序器目前不支持直接导出 ONNX；若必须跨运行时部署，应选择可导出的替代任务形式，或把原生 ML.NET 模型作为正式检查点格式。

每个检查点清单必须绑定：

- 权重、超参数和随机种子；
- 特征 schema 与候选词表；
- 权威字典版本；
- 编译器与执行器版本；
- 数据集哈希和切分信息；
- 离线评估结果。

硬性评估门：

- schema 合法率 100%；
- 输出 allowlist 外候选为 0；
- 检查点往返推理结果一致；
- 无动作和过期 schema 不进入训练；
- 不可逆动作授权规则不可被模型绕过；
- 长运行无日志失控、存储失控或死锁。

## 4. 执行顺序

1. 实现证据注册表和生成式准入清单；
2. 回填已有真实运行证据，分离“未登记”和“未实现”；
3. 闭合持续移动、输入仲裁、原生动作生命周期和异常诊断组成的共享执行底座；
4. 按权威字典的真实缺口逐个闭合目标族，每个稳定纵向切片及时合并；
5. 用本地可见短测验证动画/按键，用后台或服务器长测验证逻辑/恢复，最后做全系统回归；
6. 重建并审计正式策略轨迹；
7. 接入 C# 结构化排序模型并完成检查点往返；
8. 做离线回放和独立存档验证；
9. 从新存档进行长期完整 rollout，目标为第三年爷爷评分 21 分；
10. 冻结“最强完美 AI”基线；
11. 再开发声音、节奏、失误容忍和玩家适应性，不得提前污染完美基线。

当前第 1、3、6、7 步的数据治理与结构化模型基础设施已经闭合，第 2、4、5 步仍按证据范围持续推进；现有十三项 allowlist 不能代表全量目标覆盖。直接下一步是继续按权威字典扩大第 4 步准入项。只有真实长期 rollout 产生轨迹和闭合跨度观测后，才运行第 6 步工具形成可供检查点引用的正式数据清单；不得用合成测试数据冒充正式训练集。

2026-08-09 EVD-226 已将 `farm.maintain_crops` 收敛为当前地点透明候选到五类类型化机械原语的唯一链路，并通过浇水、播种、普通收获、普通地块施肥、花盆施肥和巨型作物后台隔离验证。该项五门已闭合，但按 `CalibrationOnlyHighLevelIds` 仍为评估/执行器校准用途，不增加策略训练 allowlist。当前生成看板为 103 registered / 170 semantic / 98 compiler-bound / 67 runtime dispatch / 29 five-gate / 25 allowlist，KnowledgeCompiler 为 585/585、blocking 0。下一步仍是继续闭合未准入高层目标并采集真实长期 rollout，不是提前启动全量训练。

2026-08-09 EVD-228 已将 `fishing.catch_fish` 收敛为透明候选经 DailyPlan 到 `executor.catch_fish` 的唯一链路，并以普通海滩 3/3、鱼塘无 BobberBar 1/1、矿井 100 层 12/12（含两次岩浆鳗鱼）后台隔离运行闭合读、候选、编译、运行和输出五门。高层动作进入策略训练 allowlist；机械原语仅作执行器校准。运行时只发送原生等价输入，不改写鱼、绿条、进度、结果或背包；低技能/低装备下的真实失败继续作为阻塞反馈，上游必须结合实时技能、鱼竿、浮标和鱼难度评估。当前生成看板为 103 registered / 170 semantic / 99 compiler-bound / 67 runtime dispatch / 32 five-gate / 26 allowlist，KnowledgeCompiler 为 585/585、blocking 0。正式全量训练仍等待其余高层目标闭合和生产长 rollout，不得把本次有界钓鱼验证外推为所有传奇鱼、宝箱优化或模组覆盖。

## 5. 新训练笔记本与模型路线

目标训练节点为用户报告的新笔记本：

- CPU：AMD Ryzen 9 9955HX，16 核 32 线程；
- 内存：32 GB；
- GPU：GeForce RTX 5070 Laptop GPU，8 GB GDDR7；
- 状态：配置来自用户报告，尚未在目标机器上完成驱动、功耗、散热和存储验收。

### 5.1 资源判断

- CPU 适合结构化数据预处理、回放、树模型训练、压缩和多环境评估；
- 32 GB 内存足够当前结构化路线，但必须使用流式数据、限制并行环境和避免同时常驻完整快照；
- 8 GB 显存是本地神经模型训练的硬边界，不能按桌面版 RTX 5070 或更大显存估算；
- 机器型号的 GPU TGP、散热和内存扩展上限需以整机厂商规格和实测为准。

存储建议：

- 结构化训练最低预留 150 GB 快速 SSD 空间；
- 保留长期原始快照和多轮 rollout 时建议 300 GB 以上；
- 冷数据归档与训练热数据分离，按哈希去重和压缩；
- 不再把 500 GB 写成所有训练路线的统一最低门槛。

### 5.2 模型分级

1. **V0 确定性基线**：现有规则和执行链，作为回归参照；
2. **V1 正式结构化排序器**：全量训练的首个必需模型，优先在 C#/ML.NET 内完成；
3. **V2 可选 0.6B 级受约束模型**：只输出候选 ID 和参数，用 4-bit QLoRA、短上下文、batch 1、梯度累积和检查点技术做比较实验；
4. **1.7B 级模型**：8 GB 显存上的边界实验，必须先通过显存烟测，不作为默认正式路线；
5. **3B 及以上训练**：不作为该笔记本的本地目标，应使用更大显存设备或远端资源。

可选神经模型不得直接读取未经裁剪的全量快照文本，也不得拥有动作执行权。其输出仍经过 C# schema、候选 allowlist、编译器和执行器。

### 5.3 实机验收门

开始正式训练前在目标笔记本完成：

- `nvidia-smi` 正确识别 RTX 5070 Laptop GPU 和 8 GB 显存；
- 驱动与选定 CUDA/训练工具链版本锁定；
- 接通电源并启用稳定性能模式；
- 记录持续负载下的显存峰值、内存峰值、温度、功耗和吞吐；
- 验证至少 150 GB 可用快速存储及数据落盘速度；
- C# 结构化模型完成训练、保存、加载和推理烟测；
- 若启用 QLoRA，先完成 0.6B 级最小批次显存烟测，失败即回退，不挤占正式 V1 路线。

## 6. 权威参考

- [AMD Ryzen 9 9955HX 官方规格](https://www.amd.com/en/products/processors/laptop/ryzen/9000-series/amd-ryzen-9-9955hx.html)
- [NVIDIA GeForce RTX 50 系列笔记本 GPU 官方规格](https://www.nvidia.com/en-gb/geforce/laptops/50-series/)
- [NVIDIA RTX 50 系列发布规格](https://www.nvidia.com/en-us/geforce/news/rtx-50-series-graphics-cards-gpu-laptop-announcements/)
- [Microsoft ML.NET 算法选择说明](https://learn.microsoft.com/zh-cn/dotnet/machine-learning/how-to-choose-an-ml-net-algorithm)
- [Hugging Face bitsandbytes 安装与平台支持](https://huggingface.co/docs/bitsandbytes/installation)
- [Hugging Face Transformers 4-bit/QLoRA 说明](https://huggingface.co/docs/transformers/main/quantization/bitsandbytes)
- [Qwen3 官方仓库](https://github.com/QwenLM/Qwen3)

## 7. 2026-08-02 实现状态与下一门

首个正式 C# 模型提供器已经实现为
`return_weighted_pairwise_linear_ranker.v1`。它消费
`policy_decision_trajectory.v2` / `policy_features.v2`，使用同一套投影代码完成采集和推理，
保留完整源候选字段，并只在生成式 allowlist 内对当前可用候选排序。候选生成、上游排除、时间与
资源许可、日计划、动作编译和原生执行仍是确定性权威。

检查点 `structured_policy_checkpoint.v1` 必须绑定正式 manifest、cleaned/train/validation/test
SHA-256、超参数、特征 schema、候选/能力词表、权威字典、编译器和执行器版本。训练器会重新校验
每个分区的哈希、行数、确定性 split 与轨迹 schema；推理会拒绝过期或损坏的检查点。Backend 的
结构化训练端点和现有 rank-options 单路径重排已接通；LiveTrainingLoop 可用
`--policy-checkpoint-path` 和 `--require-structured-policy` 显式启用，缺失时不得静默回退。

这仍未解除正式全量训练阻塞：标准 E 盘生产轨迹、跨度观测、manifest 和 checkpoint 均不存在，
当前训练 allowlist 也只有十六个有界范围。2026-08-04 EVD-213 已把当前加载地图中精确、已完成、
非孵化器的原版机器产物收取通过既有单链纳入五门准入；投料、制作、摆放、搬迁、存储和孵化器
流程仍留在 `farm.process_machines` 校准范围。2026-08-03 EVD-212 已把精确原版绿雨灌木索引 44/46
通过既有单链纳入五门准入；确定性核心掉落、采集经验和任务收取已验证，秘密纸条仍按概率边界与
执行后观测处理。下一门仍是扩大五门准入并采集真实长期 v2 rollout，不是
继续用合成数据调模型。形成正式 manifest 后执行 `StardewAI.PolicyModel`，再通过独立存档评测和
第三年 21 分长跑；未通过前不得冻结完美策略或开始拟人适配。
## 2026-08-04 EVD-214 更新

训练白名单现为 17 个有界范围。`farm.load_supported_machine_input` 仅在当前地图的精确已摆放机器支持
意图下准入，并要求实时确定性正净值、零附加耗材、精确玩家槽数量未被其他目标预留，以及账本、
预测和路线在编译/派发时没有漂移。隐藏静默 E 盘运行已完成原生投料、处理开始、训练行写入和意图
完成对账。该结果没有解除正式全量训练阻塞：Product Executor 仍为 0，生产轨迹、跨度观测、正式
manifest/checkpoint 和第三年 21 分独立评估仍未完成；广义机器策略和完整制作-摆放-投料生命周期也
仍在准入范围之外。

## 2026-08-04 机器容量生命周期编排更新

`farm.establish_supported_machine_capacity` 已登记并接入单一滚动编排链。它只服务
`goal.economy.earn_money` 的有界正收益容量缺口，并按持久 `MachineSupportIntent` 在制作、精确摆放、
首次投料三个既有执行分支之间推进；每个新快照最多选择一个当前阶段，摆放执行失败时保持原目标重试，
无效意图则失败关闭。阶段测试已证明它不会在非赚钱目标下排名，也不会复制执行器。

EVD-215 已通过隐藏静默隔离运行 `runtime-supported-machine-capacity-20260804-120211`。同一高层选项
连续驱动原生制作、规划器精确摆放、确定性首次投料、处理开始、意图完成和三条训练记录；五门闭合数
现为 19，allowlist 为 18。该准入只覆盖当前地图、有界正收益、零附加耗材且无预留冲突的单机器容量
生命周期，不覆盖任务/收集需求、远程摆放、随机机器或广义 `farm.process_machines`。

正式全量训练仍未准入：Product Executor 仍为 0，生产长 rollout、闭合跨度观测、正式 manifest、
生产 checkpoint、独立存档评测和第三年 21 分长跑尚未完成。下一开发切片是任务/收集需求机器处理，
之后继续按权威字典扩大五门范围并采集真实长期轨迹。

## 2026-08-11 每日委托接受链更新

`quest.accept_daily` 与 `executor.accept_daily_quest` 已注册并接通透明桥、上游候选排除、滚动跨地图接近、
DailyPlan、动作队列和原生 Billboard 点击。隔离 E 盘运行已验证同一原生 offer 进入任务日志且保留两天期限；
安装后的真实 full 快照为 required 103、blocking 0。两项仍为 RegisteredOnly，五门闭合数和训练白名单仍分别为
39 与 26；需要重复跨日、无任务、过期及联机归属证据后才能改变准入状态。该闭环不解除正式全量训练阻塞。

## 2026-08-11 特别订单接受链更新

`quest.accept_special_order` 与 `executor.accept_special_order` 已按单一实现覆盖 Town、Qi 和沙漠节庆三种原生入口，
接通实时左右 offer、上游许可排除、滚动接近、原生开板/对话以及精确选择。Town 隐藏静默隔离运行已验证原生
互斥锁延迟和 key、generation seed、fingerprint、accepted type 的一致回执；安装后的真实 full 快照为
required 104、blocking 0。Qi 与沙漠节庆只有锁定版本反编译和结构覆盖，必须分别完成运行校准后才能形成对应证据。

动作对账现为 111 registered / 176 semantic / 110 compiler-bound；five-gate 仍为 39，训练白名单仍为 26。
特别订单高层项和原语继续保持 RegisteredOnly，一次 Town 通过不构成正式训练准入，也不解除 Product Executor、
长期 rollout、独立存档评测和第三年 21 分长跑等全量训练阻塞项。

## 2026-08-27 彩虹尽头奖励准入（EVD-268）

`rewards.claim_pot_of_gold` 已按锁定 1.6.15 原生规则闭合五门并进入训练白名单。模型不输出坐标、站位、数量或领取细节；编译器从最新 `current_location.pot_of_gold_reward` 绑定 Forest、`52,98`、可用相邻格、春 17 日、`min(100, 7 + year)` 金币和帽子契约。隐藏静音满背包运行验证原生 `GameLocation.checkAction` 产生第二年 9 个金币 debris 与 1 顶帽子，随后由既有 debris 拾取链处理，不建立第二套奖励转移系统。

最新 full 快照为 `128 required / 112 readable / 16 contextual / 0 blocking`；权威对账为 `143 registered / 199 semantic / 142 compiler-bound / 69 five-gate / 37 allowlist / 56 catalogued blocked / 0 Product Executor`。该单项准入不解除正式全量训练对 Product Executor、生产长 rollout、冻结正式数据 manifest、独立存档评测和第三年 21 分长跑的总体阻塞。

## 2026-08-27 矮人王雕像每日能力准入（EVD-269）

`mining.choose_dwarf_statue_power` 已按锁定 1.6.15 原生规则闭合五门并进入训练白名单。透明桥实时发布采矿精通门、当前地图全部精确基础雕像、可达站位、由 `DaysPlayed*77 + uniqueID` 生成的两个不同选项、五种原生效果分支和已有 buff 锁。模型必须选择一个当天真实提供的 `power_id`；编译器拒绝缺失或伪造的 ID，并从新快照重绑菜单索引、buff、雕像和站位。

隐藏静音 E 盘运行对当天两个选项 `0/3` 分别验证了原生 `Object.checkForAction -> ChooseFromIconsMenu.receiveLeftClick`、唯一选中 buff 和菜单关闭回执。最新 full 快照为 `129 required / 113 readable / 16 contextual / 0 blocking`；权威对账为 `144 registered / 199 semantic / 143 compiler-bound / 70 five-gate / 38 allowlist / 55 catalogued blocked / 0 Product Executor`。该单项准入不解除正式全量训练对 Product Executor、生产长 rollout、冻结正式数据 manifest、独立存档评测和第三年 21 分长跑的总体阻塞。

## 2026-08-27 祝福雕像每日奖励准入（EVD-270）

`rewards.claim_statue_blessing` 已按锁定 1.6.15 原生规则闭合五门并进入训练白名单。它是无参数领取目标，不是策略选择菜单：透明桥预测当天唯一祝福，并发布农业精通、日锁、天气/节日分母、七种效果、当前地图精确基础雕像和相邻站位；编译器覆盖模型伪造的所有机械字段。

## 2026-08-27 House Plant 原生轮转准入（EVD-271）

`world.rotate_house_plant` 已按锁定 1.6.15 原生规则闭合五门。它是显式装饰目标，不是自主维护候选：模型选择当前地图中的一盆精确基础 House Plant，编译器从最新 `current_location.objects[]` 重绑永久物品身份、当前/预期 `ParentSheetIndex`、相邻站位、真正空工具栏槽位、恢复槽位和原生契约。工具槽不能替代空槽，因为起始帧 7 在空手 `GameLocation.checkAction` 下会触发地点层第二次对象调用，一次交互的真实结果为 `7→1`。

生产执行只复用共享 BFS 和一次地点级原生交互，不直接写贴图帧或调用对象级方法。透明桥、候选和运行时同时防守原生四向不可通行对象包围时会触发的 `performToolAction(null)` 破坏性前导分支。隐藏静音 E 盘矩阵对 0..7 全部通过，并验证永久 ID 与槽位不变。该项进入证据白名单但 `AutonomousCandidateEnabled=false`，普通日计划不会擅自改变玩家装饰。当前对账为 `146 registered / 199 semantic / 145 compiler-bound / 72 five-gate / 40 allowlist / 53 catalogued blocked / 0 Product Executor`；下一切片是 `farming.collect_slime_ball`。

生产执行只复用共享 BFS 和原生 `GameLocation.checkAction`，不直接施加 buff 或写日锁。隐藏静音 E 盘运行验证当天 `statue_of_blessings_1` 唯一回执和 `hasBeenBlessedByStatueToday=true`。最新 full 快照为 `130 required / 114 readable / 16 contextual / 0 blocking`；权威对账为 `145 registered / 199 semantic / 144 compiler-bound / 71 five-gate / 39 allowlist / 54 catalogued blocked / 0 Product Executor`。该单项准入仍不解除 Product Executor、生产长 rollout、正式 manifest/checkpoint、独立存档评测和第三年 21 分长跑阻塞。

## 8. 2026-09-05 r32 原生存档事务准入结果

`train.server.20260905.r32.plan07` 是首个同时满足真实 Product 动作、原生跨日、原生存档完成、训练事务提交和 canonical 制品更新的 r32 有界轮次。发布固定为 `formal-r32-af432ed-20260905`，并发为 1；运行从 Summer 2 推进到 Summer 3，存档树 SHA-256 从 `7822c135afa09a355fbed3ce1462784d1551fdf8cfdf81ae4efebd95fcba31a3` 变为 `a4af6a79e6138085b07e7c63c7977fdc1c12e1bf34df28955c6a7614816af27b`。睡眠回执包含 `sleep_yes_confirmed`、`new_day_observed`、`native_save_committed`、`post_sleep_menu_closed`、`native_new_day_world_stable` 和 `post_sleep_dialogue_advanced_natively`。

事务最终状态为 `committed_after_native_save_boundary`。正式数据集 accepted 200 / rejected 0，train / validation / test = 142 / 5 / 53，train pairs = 4367；canonical checkpoint SHA-256 为 `4f937ec73f2a0f58bdac00ff9345fd4fbcc201010d627b53939a132357a2181f`，dataset manifest SHA-256 为 `bfbeb9e5f943726a9e830e31bb6926724566e74e42505f1613159cef706d7a16`。运行、事务副本、canonical 数据、执行器诊断、精确存档和四项控制文件已经归档，本机与远端 146 个文件逐一校验一致。

round05 和 round06 分别因保存完成判定过早、隔夜地震系统对话未推进而失败，二者均保持 `staged_not_committed`，不能并入成功训练证据。现有结果证明单轮正式事务可以可靠提交，但不证明策略已经完成全量训练，也不覆盖连续多日、跨季、跨年、第三年 21 分和 Companion 人类适应层。

下一准入阶段固定为可恢复的连续有界批次：从 Summer 3 的已提交存档和 canonical 哈希开始，每批只允许一个训练实例；达到计划日数或 attempt 上限即退出。成功退出必须同时满足主动作均有 fresh/verified Product 回执、无未解决 `*.pending.json`、原生存档哈希发生预期变化、事务已提交、canonical dataset/checkpoint/manifest 相互一致、全部制品归档并通过远端到本机 SHA-256 核对。任一门失败则保留 staged 诊断且不得推进基线。

## 9. 2026-09-05 r33 连续事务准入结果

round08 首次用 round07 的 canonical 产物准备下一轮时，被 `formal_checkpoint_dataset_binding_mismatch` 在动作执行前拒绝。动态证据证明提升逻辑只复制文件，却没有把 manifest/checkpoint 内的 staging 绝对路径改为 canonical 路径。该失败没有启动 Product 执行与训练循环，canonical 未被该轮推进。

`7b1da8d` 将重绑定纳入事务提交本身：manifest 的 input、horizon、cleaned 和三分区路径必须位于受控根内并统一重绑定；canonical manifest SHA-256、checkpoint dataset binding、checkpoint ID 和最终报告身份一起更新。回归为 Core 2274/2274、Backend 171/171、Release 0 warnings / 0 errors。历史 canonical 经同一版本的 PolicyDataset/PolicyModel 工具重建，样本数、切分、pairs 与模型指标均未漂移，6 个 digest 全部通过。

round09 随后证明下一轮 prepare 与提交均成立：3 个高层候选轨迹全部 fresh/success；原生睡眠控制步不调用策略模型；Summer 3 → Summer 4 且存档树 SHA-256 改变；事务为 `committed_after_native_save_boundary`。最终为 accepted 203 / rejected 0、145 / 5 / 53、4547 pairs，checkpoint / manifest SHA-256 为 `4247b9feed96fbb40fbe263dd6f260c006d8f2db96c28b4a21bc0b5ffc717eeb` / `b35080e21a10ae61109df1577a4e6534fa2e60150917e3d75671d8d8734c45d5`，未解决 Product pending 为 0。

该结果把“单轮可提交”提升为“提交结果可直接供下一轮使用”，允许进入连续有界批次；仍不等于跨季/跨年/Grandpa 21 全量长训通过。下一批必须继续维持并发 1、静音后台、失败不提升、原生保存、canonical 内部绑定验证和本机逐文件归档。

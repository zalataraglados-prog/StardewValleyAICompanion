# Stardew Valley AI Companion

StardewAI 是一个面向《Stardew Valley》的分层 AI Companion / Agent 工程。最终目标不是“自动把游戏打得最优”，而是让一个 AI 作为独立主体长期生活在同一个新存档里：能看见真实游戏状态、自己规划和行动、记住共同经历、接受玩家委托，并始终通过原版游戏机制完成实际行为。

核心价值可以概括为：**共同在场 + 连续记忆 + 低审判反馈**。

工程上严格分离“高层决定做什么”和“机械层怎么做”：

```text
透明真实事实
→ 预注册语义动作 / 候选
→ 高层策略一次选择有序候选队列
→ 候选边界 fresh 校验
→ 权限与硬约束门禁
→ Action Compiler 确定性展开 / continuation
→ Product Executor / 原版具身执行
→ Before / After 事实验证
→ 轨迹、审计、重规划与训练
```

## 当前状态

截至 2026-09-05，公开 `main` 已进入 **r35 可重复有界正式训练、透明候选缓存与动态角色避障修复** 阶段；以下动作覆盖数沿用 r25 冻结基线，运行证据治理沿用 r29 / issue #89 强绑定口径：

- 228 registered actions
- 230 semantic actions
- 227 compiler-bound actions
- 145 RuntimeTestHarness dispatch
- 145 Product Executor dispatch
- 151 five-gate validated actions（历史口径；运行证据强绑定正在逐域迁移）
- 62 strategy-training allowlisted actions
- 2 catalogued blocked semantics
- native inventory baseline: 322 surfaces / 448 branches / 150 map tokens
- KnowledgeCompiler coverage: 585 / 585 known fields, blocking 0

剩余两个显式 pending 是：

- `tailoring.dye_item`：后置玩家外观命令；
- `minigame.play_junimo_kart`：后置的真实原生完美代打能力。

它们都不重新阻塞当前自主策略训练。AI 自主玩 Junimo Kart 继续使用已经锁定的 `timed_equivalent` 边界；真实逐帧完美代打属于后续玩家明确委托的 Minigame Skill。

## 正式训练状态

正式 Product 训练闭环已经在 119 服务器产生真实数据和 checkpoint 更新，不再只是测试 Harness、bootstrap 或“进程存在”。当前结构化策略模型是 C# `return_weighted_pairwise_linear_ranker.v1`，只负责高层候选排序；WASD、路径、菜单、工具使用、等待、交互和逐帧机械动作都留在确定性 C# 层。

### r29 的决策边界

旧循环曾在每次 fresh snapshot 后重新进入策略排序入口，导致移动、交互、等待等机械 continuation 反复触发 `rank-options`。r29 已把规范边界修正为：

1. 模型/策略排序器一次选择一条**有序高层候选队列**；
2. `SelectedQueueDecisionLease` 持有原始排序、候选顺序和游标；
3. 每到一个候选边界，读取 fresh snapshot，重新验证时间、能量、资源、身份、先后关系和可执行性；
4. 候选内部的寻路、移动、交互、等待、关菜单等 continuation 只由 C# 本地确定性重编译；
5. 只有整条队列完成，或当前候选使剩余队列失效时，才重新调用策略模型；
6. 机械原语不伪装成策略决策轨迹。

119 服务器 `formal-r29-20260901 / train.server.20260901.r29` 的有界实证为：

- 1 次策略排序选择 4 个高层候选；
- 共执行 9 个机械动作；
- 3 次候选边界刷新 + 5 次候选内部 continuation 刷新；
- 上述 8 次刷新均 `policy_model_invoked=false`；
- 4 个高层候选各形成 1 条成功策略轨迹；
- 最终 `selected_queue_decision_complete=true`；
- r27/r28 仅用于暴露站位身份与 continuation 跨候选问题，不作为成功训练证据。

这意味着当前训练结构已经从“高频模型重规划”收敛为：

> **低频高层策略决策 + 高频本地确定性闭环控制。**

当前仍未完成连续多日、跨季、跨年和第三年 Grandpa 21 分长跑，因此不能描述为“全量训练完成”。服务器磁盘仍是下一轮长训的硬门槛；修正前训练根已迁移归档并完成 910/910 文件 SHA-256 校验。

## 运行证据新鲜度

issue #89 后，运行证据不再只凭“evidence id 非空”继承 `RuntimeVerified`。

`native_object_execution.v2` 对首批六个重跑动作绑定：

- 运行路径 revision；
- 32 个相关源文件的规范化 SHA-256；
- artifact / source / build identity；
- RuntimeTestHarness、Contracts、TransparentBridge 三份运行 DLL 的精确 SHA-256。

源码、revision、artifact 或 DLL 身份任一漂移都会把对应 Runtime/Output 准入按 stale 关闭。首批强绑定范围是：

- `world.rotate_house_plant`
- `world.play_singing_stone`
- `farming.collect_slime_ball`
- `animals.withdraw_feed_hopper_hay`
- `animals.collect_auto_grabber_contents`
- `movement.use_mini_obelisk`

其他历史运行证据暂为 `LegacyUnbound`，保留历史事实，但不能冒充已经完成新鲜度强绑定。CI 已加入不依赖 Stardew 私有游戏 DLL 的 game-free governance profile；本地最新验证为 governance 16/16、Core 2252/2252、Backend 171/171、Release 0 warnings / 0 errors。

### r32 的原生跨日事务

r32 round07（2026-09-05）的最新有界训练实证：

- 发布与代码：`formal-r32-af432ed-20260905` / 部署时 `af432ed`，远端整合后的公开 `main` 等价提交为 `d881555`；
- 运行：`train.server.20260905.r32.plan07`，并发固定为 1；
- 1 个主动作完成 `applied + verified + fresh`，随后通过原生睡眠、隔夜系统对话和 `SaveGame.Save()` 完成 Summer 2 → Summer 3；
- 存档树 SHA-256 从 `7822c135afa09a355fbed3ce1462784d1551fdf8cfdf81ae4efebd95fcba31a3` 变为 `a4af6a79e6138085b07e7c63c7977fdc1c12e1bf34df28955c6a7614816af27b`；
- 训练事务状态为 `committed_after_native_save_boundary`，正式数据集 accepted 200 / rejected 0；
- train / validation / test = 142 / 5 / 53，train pairs = 4367；
- checkpoint SHA-256 = `4f937ec73f2a0f58bdac00ff9345fd4fbcc201010d627b53939a132357a2181f`；
- 远端与本机归档逐文件校验为 146 / 146，缺失、额外、哈希不一致均为 0。

round07 首次证明“真实 Product 动作 → 原生跨日存档 → 事务提交 → dataset rebuild → structured checkpoint 与 manifest/hash 更新”的单轮闭环成立。下一轮 prepare 随即发现 canonical manifest/checkpoint 内仍保留 staging 绝对路径；round08 因 `formal_checkpoint_dataset_binding_mismatch` 在执行任何动作前失败关闭，canonical 未被该轮修改。

### r33 的连续轮次证明

`7b1da8d` 将事务提升改为：重绑定 manifest 内全部数据文件到 canonical 根，重算 manifest 哈希、checkpoint ID 与最终报告身份。一次性修复没有改变样本集合（200 accepted / 0 rejected、142/5/53、4367 pairs）。随后 `formal-r33-7b1da8d-20260905 / train.server.20260905.r33.plan09` 通过下一轮准入并完成：

- 3 次外层迭代、1 次 Product 执行请求；同一有序高层队列形成 `mail.process_letter` 和两个 `economy.ship_items` 轨迹，均为 fresh/success；
- 睡眠控制步 `policy_model_invoked=false`，未写入策略轨迹；
- Summer 3 → Summer 4，存档树 SHA-256 从 `a4af6a79e6138085b07e7c63c7977fdc1c12e1bf34df28955c6a7614816af27b` 变为 `84872422a42380d669856693052cf58606273b58aed20566a9732c9751c69493`；
- 正式数据集 accepted 203 / rejected 0，train / validation / test = 145 / 5 / 53，train pairs = 4547；
- canonical checkpoint / manifest SHA-256 分别为 `4247b9feed96fbb40fbe263dd6f260c006d8f2db96c28b4a21bc0b5ffc717eeb` / `b35080e21a10ae61109df1577a4e6534fa2e60150917e3d75671d8d8734c45d5`；
- 6 个 manifest 数据摘要全部验证，未解决 Product pending 为 0；远端与本机归档 142 / 142，缺失、额外、哈希不一致均为 0。

这证明 canonical 产物可以直接进入下一轮并再次提交，但**连续多日批次、跨季、跨年和第三年 Grandpa 21 分长跑仍未完成**，不能描述为“全量训练完成”。服务器上的正式训练进程已停止；下一批仍须使用有退出条件的计划、并发 1、原生存档边界和逐文件本机归档。

### r34 的透明候选性能与第三次连续提交

`10b7722` 在不删减 full snapshot 字段的前提下，把同一快照上的跨图 route connector 候选构建结果按快照实例缓存，并统一默认自主候选集合。显式目标候选仍不被擅自纳入自主训练，`recovery.stabilize_day` 仍是控制面边界，不写策略轨迹。冷启动 Farm full snapshot 的默认候选排序从约 227.9 秒降至 28.266 秒；119 上 r34 的室外首次排序为 43.329 秒，随后同快照边界排序为 145.6/9.7 毫秒。

`formal-r34-10b7722-20260905 / train.server.20260905.r34.plan01` 以并发 1 完成 2 个主决策和 2 个原生存档边界迭代，产生 4 条 Product 策略轨迹；Summer 5 → Summer 6，存档 SHA-256 从 `09bf903b36e1642339784f21a7331d309511280cb96abf458f7f7d8dca86c9a2` 变为 `08b6e014081653f6625ad57106624417e0e02ca5f5778d744a393911f7dc6a49`。规范数据为 accepted 211 / rejected 0、153 / 5 / 53、4783 pairs；checkpoint / manifest SHA-256 为 `b33d54f66fdcbe304e5207043751a0a153dcd45d24064b299903b978d28bd010` / `103770377bdca5b4979bd196ae4756c93e080f8924bc1de136a9bd35cc07c738`。远端和本机归档均为 189 个文件，缺失、多余和哈希不一致均为 0；正式训练进程已停止。

这仍是连续有界训练证据，不是跨季、跨年、Grandpa 21 或 Companion 产品验收。直接下一步是从 Summer 6 和上述 canonical 哈希生成下一份有退出条件的计划，继续真实 Product rollout。

`train.server.20260905.r34.plan02` 已继续完成 Summer 6 → Summer 7：2 个主决策产生完整处理两封邮件、照料宠物和收取树产品 4 条 applied 策略轨迹；fresh 快照上失效的社交队列尾项被拒绝并触发重新规划，没有误写成功轨迹。邮件候选 ID 记录进入时的 route/open 阶段，但 outcome 聚合同一 lease 内直到 `close_menu` 的完整 continuation，并非把机械步骤独立训练。控制面边界仍无策略轨迹。canonical 更新为 accepted 215 / rejected 0、157 / 5 / 53、4920 pairs；checkpoint / manifest SHA-256 为 `bc5369df5a47bfdf27d9a49b99cc4498b54a4cd4dc27bba1b02de907419c15a4` / `24b18a5bf0317e36f36398609b9e65c79a69f42bef73cee35b57191ae56ec653`。完整归档远端/本机 170 / 170 且三类差异为 0；下一轮起点为 Summer 7。

`train.server.20260905.r34.plan03` 已从 Summer 7 原生保存到 Summer 8：2 个主候选的机械动作均由 Product 回执验证，但出货候选和邮件 approach 候选都只执行到局部队列边界，因此失败关闭为 0 条新增策略轨迹；控制面 return-home/sleep 同样不进入策略训练。canonical 保持 accepted 215 / rejected 0、157 / 5 / 53、4920 pairs，执行特征增至 224、horizon 观测增至 8；checkpoint / manifest SHA-256 为 `64283a58cb8e48eed06baffb1c8116a241033c26b00e792ec01807db537c345c` / `47ac7aaff2798f3546eea1e0a5772a524dc3d9ee959e4f0506136d7e3fb18a78`。完整归档远端/本机 119 / 119 且三类差异为 0；下一轮唯一合法起点为 Summer 8。

`train.server.20260905.r34.plan04` 已继续完成 Summer 8 → Summer 9：2 个主决策形成完整处理 `fertilizers` 邮件、照料宠物和完整处理 `spring_19_1` 邮件 3 条 applied 策略轨迹；不完整队列尾项只记 skip，控制面边界仍不进入策略训练。canonical 更新为 accepted 218 / rejected 0、160 / 5 / 53、5011 pairs，执行特征增至 227；checkpoint / manifest SHA-256 为 `0b31d8084c6fcc728defd4ba916d8542b2e1efb13cbf97695f1db94a910b1035` / `0af71721588d0da1bd78cb72cfca6e3cd74e7545f2edb2726390d3d9b08cf96b`。完整归档远端/本机 172 / 172 且三类差异为 0；下一轮唯一合法起点为 Summer 9。

`train.server.20260905.r34.plan05` 随后在 Summer 9 暴露宠物堵住卧室窄路时通用移动只重规划、不发送原生方向输入的问题；该轮事务未提交，存档与 canonical 均保持不变。`20fff60` 在同一移动链中加入“先绕行、无路则原生推让、180 tick 后失败关闭”，`formal-r35-20fff60-20260905 / train.server.20260905.r35.plan01` 重跑后从 FarmHouse `(10,9)` 正常切换到 Farm，并完成 Summer 9 → Summer 10。新 canonical 为 accepted 221 / rejected 0、163 / 5 / 53、5094 pairs、230 条执行特征和 8 条 horizon 观测；checkpoint / manifest SHA-256 为 `dcb6ff2bc8dd28a223fcd0fe5a27c4a58215ff23084b6013a30f9299daac774b` / `dc7628a1add5a9db7247980bc7e19e4f671c93c3b0f51bcf12e4917e3085904b`。成功归档远端/本机 165 / 165 且哈希差异为 0；下一轮唯一合法起点为 Summer 10。

## 设计原则

### 1. 模型负责“做什么”，机械层负责“怎么做”

策略模型只处理高层目标、目标对象、优先级、预算和取舍。坐标、路线、菜单、战斗微操、库存合法性、原版状态机、权限和回执由确定性 C# 层处理。fresh snapshot 用于候选边界验证和机械重编译，**不等于必须重新调用模型**。

### 2. 不直接修改游戏结果

执行优先走 Stardew Valley / SMAPI 的原生交互、菜单、对话、输入和状态机，不以直接写结果状态代替原版机制。

### 3. 能力存在、训练资格和产品可用性分开证明

```text
registered
→ facts joined
→ candidate bound
→ compiler branch
→ Harness dispatch
→ offline/source tests
→ runtime evidence
→ runtime evidence freshness
→ runtime verified
→ Product Executor
→ training eligible
→ product ready
```

RuntimeTestHarness 只承担测试/证据职责，不能冒充产品执行器；当前正式训练只接受 `product_executor.v1` 的真实、fresh、verified Product 轨迹。

### 4. 决策状态与执行状态分离

策略模型选择队列时的 ranking/decision state 与后续各候选执行前的 fresh execution state 必须分别记录。保留队列中的后续候选不能被伪装成“模型在新的 fresh state 下重新选择了一次”。

### 5. 玩家命令与自主策略分离

不是所有“AI 能做的事”都进入训练。装饰、破坏性操作、特定外观调整、完美小游戏代打等可以保留为 `PlayerCommandOnly` / delegated skill；自主策略空间只学习 AI 作为一个长期角色本来应该自己决定的行为。

## 主要项目

- `StardewAI.Contracts`：跨 Bridge / Core / Backend / Compiler / Training 的强类型合同。
- `StardewAI.TransparentBridge`：透明读取 Stardew Valley / SMAPI 真实状态与 provenance。
- `StardewAI.KnowledgeCompiler`：事实、能力、原生分母和语义动作对账。
- `StardewAI.Core`：候选、规划、约束、验证、轨迹与结构化策略训练。
- `StardewAI.Backend`：ASP.NET Core API 与训练/控制入口。
- `StardewAI.RuntimeTestHarness`：隔离 E3 运行验收与机械状态机证据。
- `StardewAI.ProductExecutor`：正式产品授权、持久回执、at-most-once 分发和唯一原生状态机接入。
- `StardewAI.LiveTrainingLoop`：正式 Product rollout、队列 lease、fresh candidate-boundary validation、数据集与 checkpoint 更新。
- `schemas/json`：版本化接口合同。
- `docs`：架构、证据、训练准入、能力清单、交接和风险记录。

## 当前风险与主动审计

正式长训现在承担“全系统压力测试”的作用。r20-r29 已连续暴露跨地图 stale queue、暂时不可输入菜单、episode/continuation 边界、训练 provenance、重复策略调用、制品增长等跨层问题；issue #89 又进一步把“历史运行证据是否仍对应当前执行路径”纳入可机器验证的治理边界。

主动静态审计、后续 bug 数量预测与优先处理项见：

`docs/FORMAL_TRAINING_BUG_FORECAST_20260901_CN.md`

其中明确区分“源码可证明的静态缺陷/高风险项”“需要运行复现的问题”和“仅用于排期的数量预测”，不能把预测数冒充已发现 bug 数。

## 本地构建

```powershell
cd I:\StardewValleyAICompanion
dotnet restore
dotnet build
dotnet test
```

启动 Backend：

```powershell
dotnet run --project src\StardewAI.Backend\StardewAI.Backend.csproj
```

SMAPI / Bridge 运行后，常用本地接口包括：

```text
http://127.0.0.1:8765/api/v1/snapshot
http://127.0.0.1:8765/api/v1/capabilities
http://127.0.0.1:8765/api/v1/audit
```

## 完全体与发布状态

动作机械底座和正式 Product 训练闭环已经进入晚期，但“完整 AI 伙伴”仍包含后续的大块工作：

```text
有界正式训练
→ 多日 / 跨季 / 跨年长期策略
→ Grandpa 21 与独立长线评测
→ 独立 AI body / farmhand
→ long memory / persona / Companion Runtime
→ Player Language / IntentMediator
→ multiplayer / interruption / recovery
→ 安装、配置、升级、发布工程
→ stable 1.0
```

因此当前最准确的定位是：

> **formal-training-stage pre-release AI companion engineering build**

它已经不是 observer 原型，也不是只有 Harness 的研究 demo；但仍不能冒充已经完成的稳定 1.0 陪玩产品。

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

截至 2026-09-02，公开 `main` 已进入 **r29 有序高层队列正式训练边界 + issue #89 运行证据强绑定** 阶段：

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
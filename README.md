# Stardew Valley AI Companion

StardewAI 是一个面向《Stardew Valley》的分层 AI Companion / Agent 工程。最终目标不是“自动把游戏打得最优”，而是让一个 AI 作为独立主体长期生活在同一个新存档里：能看见真实游戏状态、自己规划和行动、记住共同经历、接受玩家委托，并始终通过原版游戏机制完成实际行为。

核心价值可以概括为：**共同在场 + 连续记忆 + 低审判反馈**。

工程上严格分离“高层决定做什么”和“机械层怎么做”：

```text
透明真实事实
→ 预注册语义动作 / 候选
→ 高层策略排序与长期规划
→ 权限与硬约束门禁
→ Action Compiler
→ Product Executor / 原版具身执行
→ Before / After 事实验证
→ 轨迹、审计、重规划与训练
```

## 当前状态

截至 2026-09-01 r25 / `formal-r25-20260901`：

- 228 registered actions
- 230 semantic actions
- 227 compiler-bound actions
- 145 RuntimeTestHarness dispatch
- 145 Product Executor dispatch
- 151 five-gate validated actions
- 62 strategy-training allowlisted actions
- 2 catalogued blocked semantics
- native inventory baseline: 322 surfaces / 448 branches / 150 map tokens
- KnowledgeCompiler coverage: 585 / 585 known fields, blocking 0

剩余两个显式 pending 是：

- `tailoring.dye_item`：后置玩家外观命令；
- `minigame.play_junimo_kart`：后置的真实原生完美代打能力。

它们都不重新阻塞当前自主策略训练。AI 自主玩 Junimo Kart 继续使用已经锁定的 `timed_equivalent` 边界；真实逐帧完美代打属于后续玩家明确委托的 Minigame Skill。

## 正式训练状态

正式 Product 训练闭环已经在 119 服务器产生真实数据和新 checkpoint，不再只是测试 Harness、bootstrap 或“进程存在”。当前结构化策略模型是 C# `return_weighted_pairwise_linear_ranker.v1`：它只负责在完整透明状态下排序“现在应该做哪个高层候选”，不会学习 WASD、菜单像素、挥刀或逐帧机械动作。

r25 的有界训练实证：

- 单个邮件高层目标 episode；
- 原生机械链：跨图 → 走到信箱 → 交互 → 等待 LetterViewer 可输入 → 关闭信件；
- 5/5 `applied + verified + fresh`；
- 正式数据集 accepted 186 / rejected 0；
- train / validation / test = 128 / 5 / 53；
- train pairs = 3415；
- checkpoint = `structured-policy-52c9f785cc6dcc46c02f94e7`。

当前已经证明“真实 Product 数据 → dataset rebuild → structured checkpoint 更新 → manifest/hash 更新”的闭环成立，但**连续多日、跨季、跨年和第三年 Grandpa 21 分长跑尚未完成**，因此不能把当前状态描述成“全量训练完成”。服务器当前磁盘约 92% 使用率，长训在扩容/迁移训练盘之前只允许有界批次。

## 设计原则

### 1. 模型负责“做什么”，机械层负责“怎么做”

策略模型只处理高层目标、目标对象、优先级、预算和取舍。坐标、路线、菜单、战斗微操、库存合法性、原版状态机、权限和回执由确定性 C# 层处理。

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
→ runtime verified
→ Product Executor
→ training eligible
→ product ready
```

RuntimeTestHarness 只承担测试/证据职责，不能冒充产品执行器；当前正式训练只接受 `product_executor.v1` 的真实、fresh、verified Product 轨迹。

### 4. 运行后必须重新观察

重要动作要求 fresh snapshot 与 before/after receipt。跨地图、菜单状态变化、世界自然推进或玩家干预后必须允许重绑定和重规划，不能沿用已经失效的机械队列。

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
- `StardewAI.LiveTrainingLoop`：正式 Product rollout、fresh replan、数据集与 checkpoint 更新。
- `schemas/json`：版本化接口合同。
- `docs`：架构、证据、训练准入、能力清单、交接和风险记录。

## 当前风险与主动审计

正式长训现在承担“全系统压力测试”的作用。r20-r25 已连续暴露跨地图 stale queue、暂时不可输入菜单、episode 边界、训练 provenance 和制品增长等跨层问题。

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

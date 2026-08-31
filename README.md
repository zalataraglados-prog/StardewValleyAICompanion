# Stardew Valley AI Companion

StardewAI 是一个面向《Stardew Valley》的分层 AI Companion / Agent 工程。目标不是让语言模型直接操作游戏状态，而是把“高层意图”与“原版游戏机制执行”严格分离：

```text
透明真实事实
→ 预注册语义动作
→ 确定性候选 / 规划
→ 权限与硬约束门禁
→ 原版具身执行
→ Before / After 事实验证
→ 审计、解释与重规划
```

## 当前状态

项目已经明显超过早期的 `Phase 1A observer + planner` 阶段。公开 `main` 当前包含完整的透明读取、语义动作注册、候选生成、KnowledgeCompiler、Harness 执行与运行时验证链路，并持续用真实 Stardew Valley 1.6.15 行为做闭环验证。

截至 EVD-320 / `e3cb412`：

- 220 registered actions
- 226 semantic actions
- 219 compiler-bound actions
- 143 five-gate validated actions
- 58 strategy-training allowlisted actions
- 6 catalogued semantic actions remain pending
- native inventory baseline: 322 surfaces / 448 branches / 150 map tokens
- KnowledgeCompiler coverage: 585 / 585 known fields

当前剩余目录主要集中在电影、剧情事件、裁缝以及 Junimo Kart 等较重交互。

> 注意：仓库中已经存在可验证的机械执行路径，但 **Product Executor 仍未完成**。因此不能再把本项目描述成“没有任何自动执行路径”，同时也不应把当前状态误称为已经完成的稳定公开产品。

## 设计原则

### 1. 模型负责“做什么”，机械层负责“怎么做”

训练模型只应处理高层目标、目标对象、策略参数、预算、取舍等决策；坐标、路线、菜单操作、战斗微操、资源合法性、权限、原版状态机等由确定性机械层处理。

### 2. 不直接修改游戏结果

执行优先走 Stardew Valley / SMAPI 的原生交互、菜单、对话、动作和状态机，不以直接写入结果状态代替原版机制。

### 3. 每个动作都需要证据链

能力按以下层级区分，而不是用“有代码”直接等价于“完成”：

```text
registered
→ facts joined
→ candidate bound
→ compiler branch
→ Harness dispatch
→ offline/source tests
→ runtime evidence
→ runtime verified
→ training eligible
→ product ready
```

### 4. 运行后重新读取真实状态

所有重要动作都要求 fresh snapshot / before-after receipt。计划不能假设世界仍保持在编译时状态；执行结果必须重新观察并允许重规划。

## 主要项目

- `StardewAI.Contracts`：跨 Bridge / Core / Backend / Compiler 的强类型合同。
- `StardewAI.TransparentBridge`：透明读取 Stardew Valley / SMAPI 真实状态。
- `StardewAI.KnowledgeCompiler`：把事实、能力和语义动作编译成可执行绑定。
- `StardewAI.Core`：目标、候选、规划、约束、验证与训练相关逻辑。
- `StardewAI.Backend`：ASP.NET Core API 与训练/控制入口。
- `StardewAI.RuntimeTestHarness`：真实游戏运行时验证和机械执行测试路径。
- `schemas/json`：版本化接口合同。
- `docs`：架构、证据、训练准入、能力清单与工程记录。

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

早期 Python FastAPI 原型已经从工作树移除；当前后端和动作编译链以 C# 工程为准。

## 训练与发布状态

当前工作重点是完成剩余语义动作、冻结训练准入语义，并建立正式轨迹数据与离线策略训练链。

**Product Executor 不是第一轮正式离线策略训练的前置条件。** 首轮训练可以在语义准入、轨迹合同、数据快照与评测条件满足后开始。

面向普通用户的稳定公开版本仍需要后续 Product Executor、端到端集成、长线存档 / 跨季跨年验证、异常恢复以及发布工程验收。

因此当前仓库更准确的定位是：

> **late-stage pre-release research/engineering build with validated native execution slices**

而不是早期只读原型，也还不是已经完成的稳定 1.0 产品。

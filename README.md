# Stardew Valley AI Companion

本仓库按蓝图 `Stardew Valley AI 副官 -> 完全体陪玩智能体` 搭建。

当前阶段定义为：

```text
Phase 1A: Transparent Read-Only Bridge + Typed Planning Preview
全透明只读接入与强类型规划预览
```

## 当前范围

- `StardewAI.Contracts`：Bridge、Core、Backend 共享的强类型合同。
- `StardewAI.TransparentBridge`：SMAPI 只读 MOD。
- `StardewAI.Core`：GoalSpec、OptionSpec Registry、Verifier、CommandPreview 编译。
- `StardewAI.Backend`：ASP.NET Core Minimal API transport。
- `schemas/json`：版本化接口合同。
- `docs`：蓝图源文件和工程笔记。

第一版只允许 `observer + planner`。禁止自动执行、键鼠模拟、OCR、截图推断、直接读写存档、直接读进程内存，以及绕过 MOD 获取游戏事实。

## 本地环境

```powershell
cd I:\StardewValleyAICompanion
dotnet run --project src\StardewAI.Backend\StardewAI.Backend.csproj
```

MOD 构建：

```powershell
cd I:\StardewValleyAICompanion
dotnet restore
dotnet build
```

测试：

```powershell
dotnet test
```

`backend/` 下的 Python FastAPI 原型已经退役，仅作为迁移参考，不再新增功能。

SMAPI 运行后，Bridge 默认监听：

```text
http://127.0.0.1:8765/api/v1/snapshot
http://127.0.0.1:8765/api/v1/capabilities
http://127.0.0.1:8765/api/v1/audit
```

## 开发顺序

1. TransparentBridge 输出真实、版本化、可审计的 Snapshot/Event/Capability/Audit。
2. 所有模块引用 `StardewAI.Contracts`，不复制数据模型。
3. 用户自然语言先编译为 `GoalSpec`。
4. Planner 只能选择已注册 `OptionSpec` 并绑定为 `OptionInstance`。
5. Verifier 输出 `feasible`、`blocked` 或 `unknown`。
6. `CommandPreview` 明确分离 `feasibility` 和 `execution_permission`。
7. `execution_permission` 在 Phase 1A 始终为 `disabled`。
8. 不存在任何自动执行路径。

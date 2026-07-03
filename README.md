# Stardew Valley AI Companion

本仓库按蓝图 `Stardew Valley AI 副官 -> 完全体陪玩智能体` 搭建，当前只做阶段 0 / 0.5 的工程基础。

## 当前范围

- `StardewAI.TransparentBridge`：SMAPI 只读 MOD 骨架。
- `backend`：Python FastAPI 后端状态存储骨架。
- `schemas/json`：版本化接口合同。
- `docs`：蓝图源文件和工程笔记。

第一版只允许 `observer + planner`。禁止自动执行、键鼠模拟、OCR、截图推断、直接读写存档、直接读进程内存，以及绕过 MOD 获取游戏事实。

## 本地环境

```powershell
cd I:\StardewValleyAICompanion
.\.venv\Scripts\Activate.ps1
pip install -r backend\requirements.txt
uvicorn backend.main:app --reload --host 127.0.0.1 --port 8787
```

MOD 构建：

```powershell
cd I:\StardewValleyAICompanion
dotnet restore
dotnet build
```

SMAPI 运行后，Bridge 默认监听：

```text
http://127.0.0.1:8765/api/v1/snapshot
http://127.0.0.1:8765/api/v1/capabilities
http://127.0.0.1:8765/api/v1/audit
```

## 开发顺序

1. 固定完整 Schema：CanonicalState、Event、Capability、Command、AuditRecord、ActionSpec、OptionSpec、ExecutorPort。
2. 完善 TransparentBridge 只读 MOD。
3. 接入 Snapshot API、Event Stream API、Capability Manifest、Audit Log。
4. 后端保存状态、事件和审计日志。
5. 再接聊天、记忆和规划。
6. 最后才考虑命令预览和白名单执行。

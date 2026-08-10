# StardewAI 短交接：type-11 不可达状态 EVD-238

## 结论

- `Quest.type_weeding = 11` 是 1.6.15 遗留兼容常量，不是可创建的原版任务。
- 锁定 66 行 `Data/Quests` 无 Weeding 类型；任务工厂无分支；原生任务源码无 `questType=11` 写入。
- 不新增除草候选、编译器或执行器。目录使用 `native_unreachable`，与 observation-only 分开。
- 任何旧存档或模组注入的 live type-11 行明确失败关闭。
- KnowledgeCompiler 每次对账复核常量、工厂分支和写入点，证据变化即 blocking。

## 当前状态

- 任务目录：`23 bound / 1 blocked / 3 observation-only / 1 native-unreachable`，共 28 阶段。
- KnowledgeCompiler：`585/585`，blocking `0`。
- 回归：Core `1619/1619`，Backend `121/121`，Release 构建 0 错误。
- 唯一剩余任务阶段：`JKScoreObjective / achieve_junimo_kart_score`。
- 在 Junimo Kart 完成前，`quest.advance` 保持 `PartiallyBlocked / RegisteredOnly`，不进入训练白名单。

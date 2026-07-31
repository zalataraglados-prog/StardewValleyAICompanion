# StardewAI 当前工作

更新时间：2026-07-31

## 当前阶段

动作全集对账与分母冻结。当前 96 个注册项只是已有代码基线，不是全游戏动作全集。
正式训练保持阻塞。

## 已完成

- 现有注册表、治理表、编译器注册和 Harness 能力表已做集合一致性校验；
- 取消 `OptionRegistry` 中手写的 31/65 固定计数，改为逐 ID 对账；
- 每个现有动作已归属唯一主执行引擎；
- KnowledgeCompiler 开始生成：
  - `native-action-surface-inventory.json`
  - `action-implementation-reconciliation.json`
  - `action-progress-dashboard.json`

## 当前任务

使用锁定的 Stardew Valley 1.6.15 反编译源补齐原生动作表面扫描，将
`unclassified` 和 `generic_interaction_only` 逐项归并为语义动作；缺少下游实现的动作
仍要注册，但状态必须是 blocked，不能用通用 `executor.interact` 冒充完整覆盖。

当前生成基线位于 `catalogs/vanilla-1.6.15/`：共发现 308 个原生输入表面，其中
35 个已映射到具体注册动作，49 个仅落到通用交互，224 个未分类。现有 96 个动作归属
10 个主引擎，编译器孤儿 0、运行 ID 孤儿 0；Product Executor 仍为 0，五门证据闭环
仍为 1。后续不得把这些数字解释为全游戏完成度。

## 退出条件

- 原生动作表面未分类数为 0；
- 全游戏语义动作分母冻结；
- 所有语义动作已注册；
- 所有现有代码零孤儿，每个动作只有一个主执行引擎；
- 固定口径看板可重复生成。

## 紧接任务

对账退出后，按看板选择首个缺口：完整 `transfer_item` 语义动作与容器适配器闭环。
每个后续动作必须复用已登记主引擎，并完成透明读取、候选、编译、Product 原生执行、
前后验证、E3 和训练记录。#85/#86 只在该动作触及对应旧结构时顺带迁移。

## 禁止事项

- 不把 96 当作总动作数；
- 不把 Harness dispatch 当作 Product Executor；
- 不因独立架构重构暂停动作主线；
- 不开始短训或正式训练；
- 不启动游戏，除非当前动作已完成静态和单元测试且明确进入运行验收。

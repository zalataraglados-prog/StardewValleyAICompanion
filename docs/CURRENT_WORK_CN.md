# StardewAI 当前工作

更新时间：2026-07-31

## 当前阶段

锁定 Stardew Valley 1.6.15 的动作全集对账已经完成当前源扫描切片，正在做分母治理冻结并
转入逐动作纵向闭环。当前 96 个注册项是可复用的已有实现基线，不是被废弃的旧代码；
新增 69 个显式 blocked 项用于记录已证实但尚未实现的能力。正式训练保持阻塞。

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
- 语义动作目录共 165 项：96 项已有 `OptionSpec`，69 项为
  `catalogued_blocked`，确认存在但尚未登记的动作数为 0。

机器状态为 `provisional_native_surface_denominator_closed`：当前锁定扫描范围已经
闭合，但还需通过确定性重生成和治理冻结，不能把“已登记”解释为“已实现”。现有代码
的编译器孤儿 0、运行 ID 孤儿 0；Product Executor 仍为 0，五门证据闭环仍为 1。

## 退出条件

- 原生动作表面未分类数为 0；
- 宽入口分支和有效地图交互 token 未审查数为 0；
- 锁定扫描范围的语义动作分母可确定性重生成并完成治理冻结；
- 所有已证实语义动作均已注册，未实现项必须保持显式 blocked；
- 所有现有代码零孤儿，每个动作只有一个主执行引擎；
- 固定口径看板可重复生成。

## 紧接任务

完成确定性重生成和分母冻结后，按看板选择首个缺口：完整 `inventory.transfer_item`
语义动作与容器适配器闭环。
每个后续动作必须复用已登记主引擎，并完成透明读取、候选、编译、Product 原生执行、
前后验证、E3 和训练记录。#85/#86 只在该动作触及对应旧结构时顺带迁移。

## 禁止事项

- 不把 96 当作总动作数；
- 不把 Harness dispatch 当作 Product Executor；
- 不因独立架构重构暂停动作主线；
- 不开始短训或正式训练；
- 不启动游戏，除非当前动作已完成静态和单元测试且明确进入运行验收。

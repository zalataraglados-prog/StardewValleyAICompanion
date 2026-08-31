# StardewAI EVD-325 短交接

## 已完成

- 新增独立 `tools/StardewAI.ProductExecutor`，复用现有 145 个原生动作状态机，不复制候选、编译器或运行时实现。
- 产品入口为 loopback `/api/v1/product/execute`；授权绑定产品能力、非 debug 动作、run-id、执行模式/actor、精确隔离存档根、GUID nonce 和时间戳。
- 正式 `LiveTrainingLoop` 必须同时启用 `--use-product-executor` 和 executor feedback；RuntimeTestHarness、无反馈模式只能配合 `--skip-training` 做校准或离线流程。
- pending 在原生分发前原子落盘，final 可长期幂等重放；nonce 冲突拒绝；孤立 pending 永不重发，转为 `native_dispatch_indeterminate_no_replay` 后重规划。
- 请求决策哈希和实际分发前后哈希均进入回执。运行世界的自然全量哈希漂移不再误杀动作；动作级 fresh 前置条件仍由唯一游戏线程原生状态机负责，漂移强制后续重规划。

## 验证

- 产品隐藏静音冒烟：`artifacts/product-executor-smoke/product-executor-20260831-235239/summary.json`，真实裁缝、幂等重放、debug 拒绝 `3/3` PASS。
- Product Executor 聚焦测试 `10/10` PASS。
- 全量回归：Core `2191/2191`、Backend `162/162`。
- Release 解决方案构建：`0 warnings / 0 errors`。
- 对账：`228 registered / 230 semantic / 227 compiler-bound / 145 harness / 145 product / 151 five-gate / 62 allowlist / 2 blocked`；KnowledgeCompiler `585/585`、blocking 0。

## 唯一下一步

不要再回到动作注册。使用独立新存档重建正式 `policy_decision_trajectory.v1` 数据清单和结构化策略 checkpoint，完成服务器 Product Executor、run-id、快照、manifest、静音进程、持久化与断点恢复探针；全部通过后启动全量训练。只有至少一个真实游戏日连续产生准入决策、产品原生 verified 回执、数据集/checkpoint 哈希并通过恢复探针，才算训练成功启动。

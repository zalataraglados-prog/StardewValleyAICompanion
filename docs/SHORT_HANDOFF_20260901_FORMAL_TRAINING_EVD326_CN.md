# StardewAI EVD-326 短交接

## 已完成

- Product 轨迹版本绑定为 `product_executor.v1`；Harness 数据不能训练正式结构化 checkpoint。
- `training_run_manifest.v2` 冻结 dataset/checkpoint hash，并记录 Product、游戏、LiveTrainingLoop 三进程。
- 正式 launch 复用 prepare 的同一 manifest，同时隐藏启动三进程；部分启动失败会回收已启动进程。
- `training_ready_probe.v2` 验证 Product health、三进程、透明快照/run-id、全套数据 hash 与 pending 收据恢复门。
- LiveTrainingLoop 每周期重建 policy dataset、训练结构化 checkpoint，并原子刷新 manifest hash；不再用 baseline 更新冒充正式训练。
- `scripts/Invoke-DailyPlanLiveTrainingLoop.ps1` 已改为强制 Product、prepared manifest 和结构化 checkpoint，默认不再限制四项候选。

## 当前阻塞

E 盘没有独立新存档的 Product v2 轨迹、正式 policy dataset manifest 或结构化 checkpoint；只有历史自动化存档和旧 feature rows。因此尚未启动正式全量训练。

## 下一步

1. 建立独立新存档根，使用 Product Executor 与 `--skip-training` 采集最小 bootstrap 校准轨迹；只接受 applied/verified/fresh，明确不称为正式训练。
2. 运行 `StardewAI.PolicyDataset` 和 `StardewAI.PolicyModel`，生成首个 Product 绑定 checkpoint。
3. 调用 formal prepare，确认所有 hash、二进制、SMAPI、静音隐藏和隔离路径门通过。
4. 使用同一 manifest formal launch，连续跑完首个真实游戏日；确认 Product 回执、轨迹增长、结构化 checkpoint 更新、manifest hash 更新和 ready/recovery probe 全部通过。
5. 本地验收通过后再更新服务器部署，不把 bootstrap、进程存在或 baseline 输出描述成正式训练。

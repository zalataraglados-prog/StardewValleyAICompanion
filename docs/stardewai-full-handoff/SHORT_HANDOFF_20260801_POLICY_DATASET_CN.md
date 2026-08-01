# StardewAI 正式策略数据集短交接（2026-08-01）

## 已完成

- `policy_decision_trajectory.v1` 的有效决策绑定由 EVD-198/EVD-199 保持不变。
- `StardewAI.PolicyDataset` 已实现严格 schema、准入、结果和不可变版本校验。
- 相同决策的语义重复只保留一条；标签或结果冲突时整组失败关闭并写拒绝报告。
- 切分键固定为 `save_id:year:season:day`，使用 SHA-256 确定性分入 80/10/10，禁止同日跨集。
- 清洗集、三个分区和输入/观测均进入 SHA-256 清单；清单记录版本集合、拒绝原因和回报覆盖率。
- LiveTrainingLoop 在真实日期跨越时写日、季、年闭合观测；只在第三年首次评价边界且 `farm.grandpa_score` 为透明可读/派生值时写唯一终点。
- 日、季、年和爷爷 21 分回报只从已观测闭合跨度回填；未知值保持 `null/pending`，终点评价后的决策不反向获得标签。
- 权威字典默认锁定 `game-1.6.15-20260723T093543Z-linux-v24`。

## 当前事实边界

标准生产路径当前不存在以下文件：

- `E:\StardewAITraining\datasets\policy-decision-trajectories.jsonl`
- `E:\StardewAITraining\datasets\policy-horizon-observations.jsonl`
- `E:\StardewAITraining\datasets\formal-policy\policy-dataset-manifest.json`

因此当前结论是“正式数据治理链已实现并通过合成/集成测试”，不是“真实正式数据集已生成”，更不是“模型已训练”。不得把测试夹具输出复制到生产路径。

## 正式构建命令

真实 rollout 产生上述两个输入文件后运行：

```powershell
dotnet run --project tools\StardewAI.PolicyDataset\StardewAI.PolicyDataset.csproj --configuration Release -- --input E:\StardewAITraining\datasets\policy-decision-trajectories.jsonl --horizon-observations E:\StardewAITraining\datasets\policy-horizon-observations.jsonl --output-root E:\StardewAITraining\datasets\formal-policy --knowledge-dictionary-version game-1.6.15-20260723T093543Z-linux-v24
```

仅诊断未闭合轨迹时才允许显式使用 `--no-horizon-observations`；该输出不得进入正式检查点。

## 下一步

1. 实现 C# 结构化候选排序器，使模型只重排已生成且已准入的候选，不获得编译或执行权限。
2. 定义检查点清单，绑定模型参数、特征/候选/能力/字典/编译器/执行器版本和 EVD-200 数据集 SHA-256。
3. 完成保存、加载和同输入同输出的往返一致性测试。
4. 继续按权威字典扩大五门准入覆盖；当前四项 allowlist 不是全量目标空间。
5. 从新存档运行真实长期 rollout，收集闭合跨度观测，再构建并冻结正式数据集。

禁止事项：不得启动旧聚合 baseline 冒充学习模型；不得猜测长回报；不得跨版本混合构建；不得在本步骤新增第二套候选、编译器或执行器。

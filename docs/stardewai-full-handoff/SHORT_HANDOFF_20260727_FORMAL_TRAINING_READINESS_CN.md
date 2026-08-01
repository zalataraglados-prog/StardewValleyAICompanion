# StardewAI 正式全量训练短交接（2026-07-27）

## 当前边界

- 主链已经能完成透明读取、候选、日计划、编译、原生执行和结果落盘。
- 这仍不是正式全量训练：当前专用服务器只暴露五类候选并跳过训练，`BaselineFeatureRowTrainer` 也不是真实学习模型。
- 不得通过删除 `--skip-training`、重复短训或扩大 episode 数量绕过训练准入。

## 2026-07-28 接续状态

版本化训练证据注册表已经实现，并从
`read/candidate/compile/runtime/output` 五门证据生成 allowlist。空 allowlist 会直接失败，所有排除项都有类型化原因，知识编译器会输出 `training-admission-manifest.json`。

当前 allowlist 只有 `mining.reach_depth`，范围严格限定为 EVD-095 的候选绑定普通矿井滚动链，不代表任意深度完成。全部 `executor.*` 以及三个校准型高层 option 不进入策略训练。

当前下一步：

1. 继续回填仓库已有真实证据；
2. 分离“已实现但未登记”和“真实缺口”；
3. 对真实缺口进入动作编译器、执行器和输出回写开发；
4. 不得把静态测试或窄烟测直接提升为完整 option 证据。

## 2026-08-01 接续状态

- 当前 allowlist 已增至 4 项：`mining.reach_depth`、`inventory.transfer_item`、`social.talk_npc`、`social.gift_npc`，均只在各自 EVD 登记范围内成立。
- `policy_decision_trajectory.v1` 已接入 LiveTrainingLoop；初始排序和两类滚动重规划均绑定自身模型计划、完整排序、编译队列和源状态哈希。
- 动作后重规划只标记下一动作；源哈希漂移、缺失候选、未准入选择、非 verified/fresh 结果和同一决策的重复原语不写策略轨迹。
- 当前直接下一步是数据清洗、按存档/日的确定性切分、数据集哈希清单和长期回报回填，然后才进入 C# 结构化排序器与检查点往返。
- 现有 baseline 仍只是聚合烟测器，正式全量训练仍未准入。

退出后依次完成：真实缺口闭合、正式轨迹重建、C# 结构化排序器、
离线回放、新存档到第三年 21 分长期 rollout、完美基线冻结、拟人适配。

## 训练节点

用户报告的新笔记本为 Ryzen 9 9955HX / 32 GB / RTX 5070 Laptop
8 GB。它尚未实机验收。

- V1：C# 结构化排序器，正式必需；
- V2：0.6B 级 4-bit 受约束模型，可选对照；
- 1.7B：显存边界实验；
- 3B 及以上：不在该机器本地训练。

不得把 RTX 5070 Laptop 当作更大显存的桌面 GPU 估算。

## 权威入口

- 总准入与实施：
  [`../FORMAL_FULL_TRAINING_READINESS_CN.md`](../FORMAL_FULL_TRAINING_READINESS_CN.md)
- 总完成路线：
  [`../FULL_SYSTEM_COMPLETION_PLAN_CN.md`](../FULL_SYSTEM_COMPLETION_PLAN_CN.md)
- 全链阶段：
  [`../full-chain-task-planning-roadmap.md`](../full-chain-task-planning-roadmap.md)
- 硬件和模型：
  [`../training-hardware-assessment.md`](../training-hardware-assessment.md)
- 风险：
  [`risk.md`](risk.md)

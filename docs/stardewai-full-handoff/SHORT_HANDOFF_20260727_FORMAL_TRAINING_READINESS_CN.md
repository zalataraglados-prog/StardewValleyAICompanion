# StardewAI 正式全量训练短交接（2026-07-27）

## 当前边界

- 主链已经能完成透明读取、候选、日计划、编译、原生执行和结果落盘。
- 这仍不是正式全量训练：当前专用服务器只暴露五类候选并跳过训练，`BaselineFeatureRowTrainer` 也不是真实学习模型。
- 不得通过删除 `--skip-training`、重复短训或扩大 episode 数量绕过训练准入。

## 当前唯一下一步

实现版本化训练证据注册表，并从
`read/candidate/compile/runtime/output` 五门证据生成 allowlist。

本切片必须：

1. 阻止空 allowlist 让测试空集合成立；
2. 回填已有真实证据；
3. 分离“已实现但未登记”和“真实缺口”；
4. 给所有排除项写类型化原因；
5. 让候选生成、策略数据和模型提供器绑定同一准入清单。

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

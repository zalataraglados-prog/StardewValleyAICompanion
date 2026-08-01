# StardewAI 结构化策略模型短交接（2026-08-02）

## 已完成

- `policy_decision_trajectory.v2` / `policy_features.v2` 记录版本化状态特征和完整源候选。
- 完整源候选保留位置、商店、物品、价格、开放/排程时窗、原因、参数和值、结构化效果等字段。
- 采集与推理共用 `PolicyStateFeatureProjector` 和 `StructuredPolicyFeatureEncoder`。
- `StructuredPolicyTrainer` 实现确定性返回加权成对线性行为克隆；只比较已准入、可用候选。
- `structured_policy_checkpoint.v1` 绑定 manifest 与 cleaned/train/validation/test SHA-256、超参数、
  特征/候选/能力/字典/编译器/执行器版本，并原子保存、严格加载。
- `StructuredPolicyRanker` 只重排现有完整候选集，不能提升 allowlist 外候选。
- Backend 增加 `/api/v1/training/structured/train`，现有 rank-options 路径原位接入结构化重排，
  没有第二套候选、日计划、编译器或执行器。
- LiveTrainingLoop 增加 `--policy-checkpoint-path` 与 `--require-structured-policy`；强制模式缺检查点
  会失败关闭。
- 新增 `StardewAI.PolicyModel` 命令行训练入口。

## 验证

- Core：`1485/1485` PASS。
- Backend：`104/104` PASS。
- Release 解决方案：0 errors，5 个既有 warnings。
- 聚焦测试：`14/14` PASS，覆盖确定性训练/保存/重载、状态条件排序、完整候选字段、非准入隔离、
  分区篡改拒绝、旧检查点拒绝和强制检查点参数门。
- 未启动游戏。

## 真实状态

以下标准生产路径在 2026-08-02 均不存在：

- `E:\StardewAITraining\datasets\policy-decision-trajectories.jsonl`
- `E:\StardewAITraining\datasets\policy-horizon-observations.jsonl`
- `E:\StardewAITraining\datasets\formal-policy\policy-dataset-manifest.json`
- `E:\StardewAITraining\checkpoints\structured-policy-v1.json`

因此没有生产数据集，也没有生产训练检查点。测试中的合成轨迹只能证明工程契约，不能汇报为
正式训练、完美策略或 21 分能力。

## 架构边界

模型输出是对上游已经生成并准入的高层候选进行评分。候选生成器负责把透明状态变成合法候选；
日计划负责必要/附加安排与预算组合；动作编译器把高层命令机械展开；执行器负责原生输入、路径、
战斗、工具和交互。模型不得直接输出按键，不得生成不存在的候选，不得绕过时间、资源、授权和
不可逆操作门。

当前 V1 是透明的行为克隆基线，返回值只调整已观察选择的训练权重，不提供反事实最优性证明。
后续可替换为更强结构化模型或受约束小模型，但必须沿用同一候选/检查点/编译/执行边界。

## 下一步与退出条件

1. 从生成式 capability registry 与权威字典依赖树选择下一个未闭合模型级目标族。
2. 复用现有候选/编译/执行代码，补齐该目标族 read/candidate/compile/runtime/output 五门及原生可见证据。
3. 每个稳定纵向切片及时合并，持续扩大训练 allowlist；不要为已有动作复制第二套系统。
4. 用隔离新存档运行真实长期闭环，采集 v2 轨迹；只接受 applied、primitive-verified、fresh 输出。
5. 闭合日/季/年/爷爷 21 分观察，运行 `StardewAI.PolicyDataset` 生成正式哈希 manifest。
6. 运行 `StardewAI.PolicyModel` 训练生产检查点，并用 `--require-structured-policy` 进行独立存档评测。
7. 独立评测、长期无死锁和第三年 21 分长跑全部通过后，才冻结“最强完美 AI”基线。

直接下一项不是继续改模型，而是回到五门准入扩张。先从当前 capability dashboard 中按 21 分依赖
优先级选一项具有现有实现基础但证据未闭合的模型级目标，完成单路径纵向闭环；生产轨迹不存在时
不得执行正式训练命令。

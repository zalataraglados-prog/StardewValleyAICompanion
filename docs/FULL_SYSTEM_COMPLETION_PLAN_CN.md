# StardewAI 完全体完成路线图

## 目标架构

最终目标是一个星露谷 AI 陪玩系统：透明读取全游戏状态，小模型只输出高层目标和策略方向，动作编译器负责机械展开、路径/时间/资源校验，执行器在训练单人或未来联机陪玩角色上执行，不抢玩家键鼠。

## 行为分类

1. 机械动作
   - 例：浇水、收机器、回家睡觉。
   - 小模型只发 option，动作编译器全权展开步骤。
   - 训练角色：executor_calibration。

2. 参数化机械动作
   - 例：去矿洞第 N 层、去某地点钓鱼。
   - 小模型给目标参数，动作编译器做路径、风险、时间、资源校验并生成动作队列。
   - 训练角色：mixed，策略层只学选择目标，不学低层操作。

3. 策略动作
   - 例：爷爷四蜡烛目标方向、采购、社交、全出货、社区中心、技能成长。
   - 小模型输出策略方向和约束。
   - 动作编译器校验时间、预算、地点、前置条件，必要时拆成可执行子任务。
   - 训练角色：strategy_value。

## 完全体切片

### A. 透明读取闭环
- 保持 TransparentBridge 实时读取，不依赖文档举例。
- 所有目标评估只能使用透明字段或显式 unavailable。
- 缺失字段分级：评分事实缺失会阻塞策略训练；展示/重评上下文字段缺失只记录，不阻塞方向训练。

### B. 策略出口层
- `strategy.grandpa_progress` 是长期目标 option。
- 输出层增加 `direction_id`，候选包括：
  - `complete_community_center`
  - `complete_joja_development`
  - `raise_friendships`
  - `complete_full_shipment`
  - `raise_skill_levels`
  - `marriage_and_house_upgrade`
  - `complete_master_angler`
  - `complete_museum_collection`
  - `obtain_rusty_key`
  - `obtain_skull_key`
  - `earn_money`
  - `earn_pet_love`
- 每个方向必须带：domain、potential_points、priority_score、feedback_key、required_minutes、hard_preconditions。
- `unlock community center` 只能作为前置条件，不作为爷爷得分方向。
- 社区中心和 Joja 路线互斥，完成任一路线都能满足对应进度收益，但不能同时推荐两个互斥方向。
- 博物馆/锈钥匙、社交/婚房、技能/钓鱼允许依赖重叠，但不得重复累计同一评分收益。
- 金钱、技能、好感人数必须按下一档阈值建模，优先推荐最小可达下一分的方向。

### C. 动作编译器
- 机械 option 必须生成 `CompiledActionStep[]`。
- 策略 option 必须生成 `StrategyPlanStep[]` 或等价结构：
  - direction selection
  - hard preconditions
  - estimated required/optional minutes
  - resource budget
  - executor handoff option
- 所有计划必须过 TimeBudgetValidator。

### D. 执行器
- 训练单人模式用 RuntimeTestHarness。
- 联机陪玩模式未来用 companion actor，不抢玩家键鼠。
- 执行失败不进入策略惩罚，进入 executor_calibration。

### E. 训练闭环
- mechanical rows：calibration_only，排除 policy。
- strategy rows：policy_ranker，纳入 baseline/后续小模型训练。
- 爷爷目标用透明评分方向生成正样本。
- 随机地图类任务必须用完美执行器，避免策略层把低层危险误判成目标不可取。

### F. 演示验收
- 可展示：
  - 当前透明读数
  - 小模型输出
  - 编译后队列
  - 时间校验
  - 训练行分流
  - policy 排序
- 完整演示最低要求：
  - 读出爷爷评分当前分与缺口。
  - 选择一个策略方向。
  - 编译出可校验计划。
  - 写入 policy 样本。
  - policy 排名反映该方向。

## Worker 分工

### Worker A：策略出口审查
检查 `strategy.grandpa_progress` 的 direction 分类是否不重不漏，补充缺失 direction 和测试建议。

### Worker B：动作编译器审查
检查 ActionQueueCompiler/TimeBudgetValidator 应如何承载 strategy direction plan，不改真实仓库，只给 patch 建议。

### Worker C：训练闭环审查
检查 LiveTrainingLoop/Backend append/train/rank 是否仍可能把 calibration 样本混入 policy，或把 strategy 样本错误排除。

### Worker D：演示验收审查
检查桌面展示文件和 API 输出是否足以证明当前阶段效果，提出最小演示缺口。

## 当前下一步

实现 `strategy.grandpa_progress` direction 输出层，并让动作编译器对 direction 做计划化时间校验。

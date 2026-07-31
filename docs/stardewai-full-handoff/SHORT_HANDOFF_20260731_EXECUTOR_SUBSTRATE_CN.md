# StardewAI 短交接：共享执行底座与明日任务

日期：2026-07-31  
明日执行日期：2026-08-01  
仓库：`I:\StardewValleyAICompanion`  
基线：`main` / `a1a1708`

## 1. 当前事实

- 权威字典锁定为 `game-1.6.15-20260723T093543Z-linux-v24`，正式根目录为 `I:\StardewAI-KnowledgeArtifacts\game-1.6.15`。
- 便于人工阅读的校验副本位于 `I:\OneDrive - DRC 创意科技有限责任公司\桌面\StardewAI_权威字典_1.6.15_linux-v24`；22 个文件已逐个 SHA-256 校验，无不一致。
- 火山隔离测试已经证明从 level 0 到 Caldera 的一条状态闭环，也证明非必要战斗目标可以脱离后继续推进。
- 可见测试仍暴露两类底层风险：清障画面可能快于原生动作，执行器在普通行走期间可能出现移动输入间隙。
- 已核实 `ModEntry.MovementSleep.ObstacleClearance.cs` 的通用清障旧路径会直接调用 `performToolAction`/`DoFunction`，并在部分分支直接移除对象或地形。这条路径不符合正式训练的原生执行要求。
- 火山石头路径已经使用 `BeginUsingTool`/`EndUsingTool` 状态机，因此不能在没有诊断证据时把火山画面问题直接归因于通用清障旧路径；也必须区分“高级工具合法一次击碎”和“跳过原生动画”。
- 无渲染服务器只能验证逻辑和长周期稳定性，不能验证步态、动画或按键节奏。

## 2. 已确定的开发决策

不先穷举修复所有假设动作 bug，也不等“全部执行器写完”后才做一次大测试。采用以下门控顺序：

1. 先闭合所有目标族共用的执行底座；
2. 再按权威字典逐个完成纵向能力切片；
3. 每个切片分别通过本地可见符合性门和后台逻辑门；
4. 全部切片完成后再做服务器全系统长回归；
5. 执行器门未通过前不开始正式策略训练，不把执行器失败写入 `strategy_value`。

## 3. 明日唯一主任务

**任务 ID：`executor.shared_native_input_lifecycle.v1`**

目标：实现并证明“持续移动租约 + 统一原生动作生命周期 + 有界异常诊断”的最小共享底座，先覆盖通用移动、通用清障和火山石头清障，不扩展新的游戏目标族。

### 3.1 首要入口

- `tools/StardewAI.RuntimeTestHarness/ModEntry.MovementSleep.PathingInput.cs`
- `tools/StardewAI.RuntimeTestHarness/ModEntry.MovementSleep.ObstacleClearance.cs`
- `tools/StardewAI.RuntimeTestHarness/ModEntry.MovementSleep.cs`
- `tools/StardewAI.RuntimeTestHarness/ModEntry.Volcano.Obstacle.cs`
- `tools/StardewAI.RuntimeTestHarness/ModEntry.State.Volcano.cs`
- `scripts/Invoke-RuntimeVolcanoReachCalderaLoop.ps1`
- `tests/StardewAI.Core.Tests/VolcanoReachCalderaRuntimeSourceGuardTests.cs`

### 3.2 实施切片

1. **先观测，不先加延时**
   - 建立低开销环形缓冲，记录最近数秒的输入所有者、持有方向、像素/格坐标、朝向、`UsingTool`、`CanMove`、精灵动画阶段、碰撞/重规划和当前原语。
   - 正常只保留内存与结束摘要；仅在卡顿、非许可掉键、动作超时或人工触发时落盘。

2. **持续移动租约**
   - 普通路径跟随期间保持一个方向输入。
   - 转向原子切换，不同时持有对向键，不插入无理由中性帧。
   - 只允许原生动作锁、菜单、连接器、碰撞重规划、安全中断或玩家接管释放，并记录类型化原因。
   - 快照获取、外部编排等待和模型等待不拥有释放权。

3. **统一原生工具周期**
   - 抽出可复用的“按下 -> 原生开始 -> 原生允许释放 -> 等待动画结束 -> 终态校验”状态机。
   - 通用清障迁移出直接对象/地形移除路径。
   - 火山石头复用同一生命周期；领域代码只提供目标、工具、站位、安全窗和终态判定。
   - 高级工具一次击碎可以成功，但必须完整经历一个原生挥击周期。

4. **最小验证**
   - 先跑静态测试和 RuntimeTestHarness 构建。
   - 在用户允许可见测试时，用隔离 E 盘运行做短可见夹具：连续路径、转向、普通石头清障、高级工具一次击碎。
   - 随后运行后台火山短回归，确认状态闭环没有因底座抽取回退。
   - 不在本任务中启动训练或服务器全量长跑。

## 4. 验收与退出条件

必须同时满足：

- 持续行走期间不存在快照、模型或外部编排造成的非许可移动释放；
- 每次转向不同时按住对向键，且没有无理由的停步帧；
- 每次清障挥击的按下、释放、原生动画开始和原生动画结束能够一一对应；
- 原生动作尚未结束时不会发起第二次挥击；
- 生产执行路径不再直接删除清障对象/地形，也不直接改写玩家位置、目标生命或掉落结果；
- 异常转储足以回答“谁释放了输入、当时处于哪个原生状态、为何进入下一阶段”，正常日志量保持有界；
- 核心测试、RuntimeTestHarness 构建、本地可见短测和后台火山回归均通过；
- 文档记录证据范围，不把单一工具等级、单一地图种子或一次通过扩大成全量声明。

达到以上条件后，本任务退出。下一任务回到权威字典差异矩阵，选择尚未闭环的最高优先级动作族，按 `read/candidate/compile/runtime/output + 原生可见符合性` 完成一个纵向切片。

## 5. 禁止事项

- 禁止用任意 `Start-Sleep`、固定帧空转或调慢游戏速度掩盖生命周期错误；
- 禁止用服务器无渲染结果声明动画正确；
- 禁止在同一任务内顺手扩展多个目标族或重写整套执行器；
- 禁止把执行器失败样本送入策略价值训练；
- 禁止触碰正式游玩存档或正式游戏实例；
- 当前不得调用 worker，除非用户重新明确授权。

## 6. 明日开工检查

1. `git status --short` 必须确认工作区状态并保留任何用户已有改动；
2. 核对 `main` 基线是否仍为 `a1a1708`，若远端或本地已有新提交，先读差异再继续；
3. 先跑现有测试取得基线，不运行游戏；
4. 完成诊断和底座代码后，再确认用户是否允许可见游戏测试；
5. 稳定且验证过的切片及时提交，不拉成长分支。

## 7. 2026-07-31 实施结果

工程切片已经完成：

- 新增 `MovementLease`，普通移动持续持有单一方向，转向在同一输入更新内切换；
- 新增 `NativeToolActionLifecycle`，同时支持“可蓄力工具显式释放”和“非蓄力工具原生自行结束”两种合法生命周期，但都要求先观察到原生动作开始；
- 新增容量 600 帧的 `ExecutorDiagnosticRingBuffer`，正常运行不逐 tick 落盘，异常触发才输出最近窗口；
- 通用清障改为异步跨 tick 原生工具动作，删除生产路径中的直接对象/地形移除；
- 农场原生工具和火山石头改用同一生命周期；
- 火山石头即使目标对象先消失，也要等原生动画与移动锁结束后才完成；
- 转弯中心逻辑修正为：旧方向仍在实际移动时才继续对齐；若旧方向被碰撞锁住，则原子切到规划方向。

验证证据：

- 全量静态测试：Backend `102/102`，Core `1430/1430`；
- 通用清障真实运行：草/镰刀、树枝/斧头、藏宝点/锄头均通过，经验、掉落 multiset、地形和统计增量与透明投影一致；
- 火山 level 9 后台回归：`11/11` 步、全部 fresh snapshot、全部 state hash 改变并进入 Caldera；
- 失败复现 `executor-lifecycle-volcano-l9-20260731` 精确暴露旧方向锁死，修复后 `executor-lifecycle-volcano-l9-fixed-20260731` 通过；
- 第一次解锁可见回归在石头动作开始前外层超时，第二次在熔岩冷却路径转弯时卡住；两次都按失败保留证据，没有放宽成功条件；
- 根因是火山冷却/清障仍各自复制简化路径推进逻辑，没有继承普通移动的转弯中心修复；两者现已改用共享 `ExecutorPathCursor`/`TryAdvanceExecutorPath`；
- 外层火山清障超时现在也触发容量 600 帧的异常诊断；
- 最终可见回归 `executor-lifecycle-volcano-visible-l9-path-owner-20260731` 通过：`14/14` 步进入 Caldera，4 次石头清障、3 次移动、3 次熔岩冷却、3 次等待、1 次出口穿越，全部 after snapshot fresh 且 state hash 改变。

## 8. GitHub #85/#86 审计结论

### #85 RuntimeTestHarness 状态所有权

事实判断正确：文件 partial 化没有改变根 `ModEntry` 持有大量 active state、调度和清理责任。P0 应解释为“立即停止继续扩大根对象责任”，而不是一次性迁移全部 Handler；整仓重写会同时扰动现有真实运行证据。

采纳方式：

1. 本轮已把输入租约、原生动作生命周期和诊断缓冲抽为独立、可单测的 owner；
2. 后续每处理一个权威字典纵向切片，就把该领域 active state 与 cleanup 迁给对应 Handler；
3. `ModEntry` 最终只保留 SMAPI lifecycle、调度和 Handler 注册；
4. 从现在起禁止向 `ModEntry` 新增领域 active state；新增领域必须先有独立 Handler owner；
5. 禁止建立第二执行循环，旧路径必须在同一切片中删除并由证据守卫防回退。

### #86 TrainingExecutionRequest v2

事实判断正确，而且严重程度高于先前估计：当前 `TrainingExecutionRequest` 分散在 8 个 partial 声明文件中，共有 304 个 public instance 属性；大量互斥领域字段在类型上可以任意组合。P0 应解释为“冻结 flat DTO 扩张并建立 v2 强类型入口”，而不是一次性迁移全部 304 个字段；后者会同时改动 compiler、runtime、verifier 与历史证据，回归面不可控。

采纳方式：

1. 立即冻结向 v1 flat DTO 新增字段；现有 v1 保留只读兼容窗口和历史证据；
2. 先定义版本化 payload envelope、option-to-payload 判别器及错误组合拒绝测试；
3. 按 `TransferItem -> PlaceObject -> Craft -> Quest` 逐族迁移，编译器、Handler 和 verifier 同步切换；
4. 每族完成后停止向 flat DTO 增加该族字段，最终才删除 v1 写入；
5. 该迁移不得阻塞已经明确的共享执行底座可见验收。

## 9. 紧接任务

1. `executor.shared_native_input_lifecycle.v1` 已满足退出条件，不再回到局部输入补丁循环；
2. 从权威字典差异矩阵选择最高优先级未闭环动作族，按五门加原生可见符合性完成下一纵向切片；
3. #85 从现在起禁止新增根状态，并随纵向切片渐进迁移 Handler；所有需要沿路径接近目标的领域动作必须复用共享路径推进器；
4. #86 立即冻结 v1 字段增长，另开 v2 envelope 兼容切片，不把 304 字段大迁移插入当前动作开发中间。

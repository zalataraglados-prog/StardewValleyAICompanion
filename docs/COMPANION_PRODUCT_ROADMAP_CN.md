# StardewAI Companion 产品阶段总路线（Canonical）

> **用途：** 本文件是 StardewAI **产品阶段顺序与阶段交接条件**的唯一权威入口。
>
> **用户确认日期：** 2026-10-28。
>
> 若其他文档在“先做什么、后做什么”上与本文件冲突，以本文件为准；其他文档继续保留各自的技术细节和历史证据，不因此失效。

## 1. 文档职责与优先级

- `CURRENT_STATUS_CN.md`：当前事实、指标、WIP、时间预测。
- `goal-conditioned-no-human-teacher-plan.md`：Stage 1 / Stage 2 的 Teacher、Student、21/21 与 Perfection 训练细节。
- `full-chain-task-planning-roadmap.md`：从计划合同到训练、执行、评测的工程链。
- `FUTURE_COMPANION_ARCHITECTURE_CN.md`：Companion body、memory、persona、LLM interaction、multiplayer 等不可返工架构边界。
- **本文件：产品后期阶段的唯一顺序。**

仓库中提到的用户桌面原文件 `stardewai_未来规划，修正部分.txt` 当前没有作为完整原文纳入仓库；本文件只固化已经在仓库中有依据、并由用户再次明确确认的产品顺序，不擅自补写未确认的体验参数。

---

## 2. 产品主线总览

```text
Stage 1：新档 Grandpa 21/21
        ↓
Stage 2：单机原生 Perfection 100%
        ↓
冻结“单机 100% 完美策略基线”
        ↓
Stage D：与玩家长期真实联机，对照训练与决策拟人化
        ↓
补全正式 Multiplayer Companion / 独立 AI body
        ↓
人类能力与经验 Profile：新手 ↔ 普通 ↔ 老手
        ↓
Companion long memory / persona
        ↓
语气 / 声线 / voice behavior 适配
        ↓
外挂式 LLM 人类交互层：Player Language / IntentMediator
        ↓
玩家委托与 AI 自主生活共存
        ↓
安装 / 配置 / 升级 / 诊断 + 长时间 E4
        ↓
stable 1.0
```

**关键纠正：**
- Grandpa 21/21 是 Stage 1 的强基线和训练里程碑，**不是“游玩完全体”交付点**。
- **先完成可独立运行的单机 Perfection 100% 存档，再进入与玩家的长期联机拟人训练。**
- Stage D 能够“和玩家长期一起玩”只要求一个足够稳定的**原型联机桥**；它不等于后续完整 Multiplayer 产品已经做完。

---

## 3. Stage 1：21/21 单机强基线

目标：从新存档开始，在首次 Year 3 Spring 1 Grandpa evaluation 取得原版精确 21/21。

本阶段继续遵循既有 Teacher / Student 合同：
- Teacher 独立产生或重标监督，不把 Student 自己的选择自动当成正标签；
- hard constraints、Teacher preference、native outcome、Student observation 分栏；
- 只通过唯一 candidate → plan → ActionQueue → Product Executor → fresh verification 链执行；
- 21/21 的正式准入、holdout 与回归标准以训练计划文档为准。

**退出：** 可复现的 Stage 1 checkpoint 与 21/21 新档长跑验收完成。

---

## 4. Stage 2：单机 Perfection 100% 与完美基线冻结

Stage 2 从已准入的 Stage 1 checkpoint **warm-start**，目标是原生 `Perfection tracker = 100%`。

### 4.1 受控扩展
Perfection 不是把新目标一次性全部放给模型。每个新增子目标仍必须补齐：
- 原生规则；
- dependency route；
- transparent input；
- candidate；
- compiler binding；
- native receipt；
- 独立验收与回归。

Student 可以提出新路线；Teacher 可以对到访状态重新验证、重标；模型置信度不能替代未知规则。

### 4.2 继承但不污染
Stage 2 复用 Stage 1 已学到的 farming、economy、routing、timing、resource allocation 等能力；但 Stage 2 的新复杂度不得降低 Stage 1 的 21/21 门槛。

### 4.3 完美基线
当系统能够在**不依赖人工接管**的单机新档中稳定完成原生 Perfection 100%，冻结：
- policy checkpoint；
- feature schema；
- option vocabulary；
- compiler / executor version；
- evaluation corpus；
- 完整依赖和 native receipt authority。

**退出：** 单机原生 Perfection 100% 可重复完成，且 Stage 1 21/21、硬约束和执行正确性无回退。

这一步完成后，才称为**游玩能力完全体 / perfect-policy baseline**。

---

## 5. Stage D：长期真实联机对照适配

这是**单机 100% 完美基线**与最终人类化 Companion 之间的正式桥接阶段。

### 5.1 为什么先在原型联机上训练
Stage D 需要一个足够稳定、能够长期共同游玩的联机原型，但不要求最终 Multiplayer 产品的所有 hosting、权限、同步、发布能力已经完成。

先通过真实共同游玩获得人类协作数据，再用这些数据决定后续完整联机产品的行为层细节，避免先把错误协作假设固化进 Multiplayer 架构。

### 5.2 采集内容
同一真实合作上下文中同时记录：
- AI 的高层决策和执行；
- 玩家的实际操作和决策；
- 玩家对 AI 的打断、接管、纠正；
- 双方角色分工；
- 资源归属与保留；
- 等待、节奏、路线选择；
- 对话或委托上下文；
- fresh native outcome 与恢复结果。

### 5.3 对照训练
只在状态、可见信息、权限和候选集合能够对齐时，生成：
- human-vs-AI preference；
- pairwise / ranking supervision；
- sequence imitation；
- decision fine-tuning；
- 协作节奏与非干扰偏好。

“玩家这样做了”本身不是 Teacher truth；不可比片段只能作为 observation / diagnostic。

### 5.4 学习目标
本阶段主要学习软行为，而不是重新学习游戏规则：
- 分工；
- 主动帮忙与保持不干扰的时机；
- 资源礼让；
- 减少无意义往返；
- 等待和行动节奏；
- 玩家长期稳定偏好；
- 打断后的恢复；
- 合作中的模糊优先级。

Stage D 不能破坏 100% 单机基线。适配必须可关闭、可版本化、可独立回退。

**退出：**
- 单机 Perfection 100% 基线仍可复现；
- 多人硬约束违规为 0；
- 玩家接管需求下降并进入稳定区间；
- 人类化 / usefulness / disruption 有独立评测；
- 适配 profile 可独立开关。

---

## 6. 完整 Multiplayer Companion

Stage D 结束后再补全 Multiplayer 产品能力，而不是把“能一起训练”误当作“联机产品已完成”。

### 6.1 三种角色部署模式
长期目标支持：
1. 玩家开房，本机后台 AI farmhand 加入；
2. AI 主持，玩家加入；
3. 未来服务器托管 AI 角色。

三种模式共享原生多人规则、同一 StrategicPolicy、同一角色状态合同和唯一执行链。

### 6.2 必须补全
- 独立 AI body / farmhand；
- 独立输入通道，不能抢玩家键鼠、焦点、菜单；
- 玩家与 AI 的角色身份；
- 物品与资源 ownership；
- player-reserved goals；
- 角色分工和交换请求；
- pause / interruption / recovery；
- 玩家先拿资源、改地图、完成目标后的 fresh replan；
- 联机同步与恢复；
- 共享资源显式、版本化授权；无授权继续 default-deny。

**退出：** 联机运行不依赖测试 Harness 假设，长期共同游玩可以持续恢复、重新规划，并保持原生多人状态守恒。

---

## 7. 人类能力 / 经验 Profile：新手到老手

完美策略本身与“表现得像什么水平的人”必须分离。

需要独立、可版本化的轴至少包括：
- strategy optimality；
- execution proficiency；
- knowledge visibility；
- communication initiative；
- cooperation relationship；
- personal preference；
- pacing；
- intentional bounded suboptimality；
- “装作新手”的提问 / 学习比例。

产品应能组合出至少：
- **新手型**：知识可见度低、会问、会学习、允许合理失误和低效，但不破坏硬规则；
- **普通玩家型**：正常知识、正常节奏、有限优化；
- **老手 / veteran 型**：高知识、高执行熟练度、高前瞻，仍服从玩家协作和资源边界。

这些 Profile 不能通过篡改 perfect-policy 训练标签实现；完美基线必须始终可选。

---

## 8. Companion long memory / persona

策略轨迹、执行回执、Companion memory 是三套不同数据。

长期记忆至少要求：
- save / actor / date / provenance / version；
- 共同经历记录；
- 摘要与长期压缩；
- 遗忘；
- 冲突修正；
- 可审计的事实来源。

Persona 只能决定：
- 表达方式；
- 关注点；
- 主动性；
- 稳定偏好；
- 合作风格。

Persona 不能改写已发生事实，也不能覆盖 TransparentBridge 的权威状态。

---

## 9. 语气 / 声线 / Voice behavior

语气、声音、声线风格属于可关闭的人类化输出层，与策略正确性分离。

本阶段固化：
- voice / tone profile；
- communication/personality 映射；
- 不同人类 Profile 对应的表达节奏；
- consent controls；
- 可关闭、可替换的语音实现接口。

具体 TTS、声线合成或供应商实现不是当前 Canonical 顺序的一部分，可在进入本阶段时另行冻结规格；任何声音实现都不得反向改变游戏事实或策略标签。

---

## 10. 外挂式 LLM 人类交互层

LLM 用来处理**人与 Companion 的交互**，不是用来直接操纵游戏原语。

正式边界：
```text
玩家自然语言 / 对话
→ Player Language / IntentMediator
→ 类型化 player_intent / goal / preference / temporary parameter
→ StrategicPolicy / Planner
→ Compiler
→ Product Executor
```

LLM 可以参与：
- 位置口语理解；
- 自然语言委托；
- 临时参数和偏好解释；
- 主动对话；
- 物品交换请求；
- 合作任务协商；
- 长期记忆检索后的对话表达。

LLM **不得**：
- 直接控制 WASD、菜单、工具或 primitive input；
- 直接改游戏状态；
- 绕过权限、ownership、reservation、hard constraint；
- 用语言模型记忆冒充游戏事实。

无网络或 LLM 不可用时，仍保留简单量化协议和核心游戏能力作为降级路径。

---

## 11. 玩家委托与 AI 自主生活共存

最终 Companion 不是单纯的命令机器人，也不是不理玩家的全自动 Bot。

需要同时支持：
- AI 自己的长期生活计划；
- 玩家临时委托；
- 玩家改变目标；
- 玩家接管或打断；
- AI 主动提出协作；
- 资源与角色分工；
- 完成委托后恢复自主计划。

委托和自主行为都走同一权威状态、策略和执行系统。

---

## 12. 发布工程与 stable 1.0

stable 1.0 至少同时要求：

```text
长期策略稳定
+ 单机 Perfection 100% 完美基线
+ 长期联机拟人适配
+ 独立 AI body / farmhand
+ 完整 multiplayer / interruption / recovery
+ 人类新手—老手 Profile
+ Companion memory / persona
+ 语气 / 声线层
+ Player Language / IntentMediator / 外挂式 LLM interaction
+ 玩家委托与自主生活共存
+ 安装 / 配置 / 升级 / 诊断
+ 长时间 E4
```

如果最终只得到一个极强的 Stardew Valley automation framework，而没有共同在场、连续记忆、人类化协作和长期共存体验，则工程能力很强，但产品目标仍未完成。

---

## 13. 防止再次混淆的命名规则

- **Stage 1 / Stage 2**：只用于单机训练课程（21/21 → Perfection 100%）。
- **Stage D**：固定指单机 100% 基线之后、完整 Multiplayer 产品之前的长期玩家联机对照适配。
- `full-chain-task-planning-roadmap.md` 中的 Stage 编号属于工程链编号，不得再拿来推断完整产品顺序。
- “perfect-policy baseline”从本文件起统一指**已通过原生 Perfection 100% 的单机冻结基线**；只有 21/21 不得使用这个称呼。

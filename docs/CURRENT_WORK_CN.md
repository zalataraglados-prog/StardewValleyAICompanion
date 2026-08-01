# StardewAI 当前工作

更新时间：2026-08-01

## 当前阶段

锁定 Stardew Valley 1.6.15 的动作全集对账和独立分母冻结已经完成，现已转入逐动作纵向
闭环。当前 97 个注册项是可复用的实现基线，不是被废弃的旧代码；68 个显式 blocked 项
用于记录已证实但尚未实现的能力。正式训练保持阻塞。

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
  - `native-action-denominator-fingerprint.json`
- 独立冻结文件 `native-action-denominator-freeze.json` 已与当前指纹核对一致；覆盖状态和
  实现进度不进入分母身份哈希，因此后续把占位动作提升为实现不会伪造“分母变化”；
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
- 语义动作目录共 165 项：97 项已有 `OptionSpec`，68 项为
  `catalogued_blocked`，确认存在但尚未登记的动作数为 0。

机器状态为 `native_action_denominator_frozen`，当前锁定扫描范围已闭合并通过独立审批文件
核对。不能把“已登记”解释为“已实现”：现有代码的编译器孤儿 0、运行 ID 孤儿 0；
Product Executor 仍为 0；EVD-196 回填后，五门证据闭环为 5，训练准入为 4。

## 退出条件

- 原生动作表面未分类数为 0；
- 宽入口分支和有效地图交互 token 未审查数为 0；
- 锁定扫描范围的语义动作分母可确定性重生成并完成治理冻结；
- 所有已证实语义动作均已注册，未实现项必须保持显式 blocked；
- 所有现有代码零孤儿，每个动作只有一个主执行引擎；
- 固定口径看板可重复生成。

## 紧接任务

首个缺口 `inventory.transfer_item` 已完成纵向闭环：强类型明确意图、透明库存图投影、
上游候选、路径站位、日计划展开和既有 `executor.transfer_material` 原语复用均已接通。
EVD-192 在 E 盘隔离存档中验证了“箱子到玩家”和“玩家到箱子”两个方向，均经原生
`Chest`/`ItemGrabMenu`、逐单位右键、互斥锁释放、before/after 数量差分和训练记录；往返后
箱子数量恢复，过期源栈投影在菜单打开前失败关闭且零点击。该项五门证据已登记，可进入其
明确意图范围内的训练；Product Executor 仍未集成，不得把 Harness 闭环称为产品陪玩闭环。
`player.storage_crafting` 与 `player.storage_placement` 的透明性 join 已修复：旧快照稳定复现
94 项 required state factor 中 2 项缺失，当前实时快照为 77 项带完整来源可读、17 项场景性
不可用、0 项阻塞。新安装器会先校验全部 required factors、哈希与版本，再原子更新外部权威
字典的 current 指针；完整 KnowledgeCompiler 已以该指针达到 585/585 exports、blocking 0。
复核确认 `recovery.stabilize_day` 的全部当前候选到日计划/队列编译链早已完成，普通社交对话和
送礼也共用唯一 `executor.social_interact` 原生 Harness 执行器，不得重复实现第二套。EVD-195 已在
隐藏、静音的 E 盘隔离存档中闭合 `Farm@2200 -> 单连接器回家 -> 新鲜快照重规划 -> 原生睡眠 -> 新日`，
因此 recovery 五门已通过，但它仍是校准型高层动作，不进入策略训练。EVD-196 又通过现有社交链
完成 Abigail 的实时远端滚动追踪：35 轮中 31 个连接器动作和一次原生送礼均验证通过，普通礼物
`(O)388` 严格由栈 1 变为 `null`，且未读取未来日程。`social.gift_npc` 现仅在“当前已加载原版 NPC、
同图或滚动连接器追踪、普通单件礼物”范围内五门闭合并进入训练准入；不得外推为模组 NPC、特殊
物品或全部社交完成。下一切片按既定路线重建准入策略轨迹并接入 C# 结构化排序器，不重做候选、
编译器或社交执行器。

正式轨迹阶段的第一道硬闸已完成：`PolicyTrainingAdmissionFilter` 直接消费生成式训练 allowlist，
校准行与未准入行分开计数；非校准行必须且只能包含一个准入模型 option。旧
`BaselineFeatureRowTrainer` 不再接受 `strategy.grandpa_progress` 等未准入样本，默认和显式排序
请求也不能输出 allowlist 外 option；显式候选过滤为空时直接返回空，不再回退到报告内容。
该基线仍只是聚合烟测，不是正式模型。紧接任务是建立强类型 `policy trajectory` schema/writer，
把决策时全部候选、排除原因、预算、最终选择、执行结果和长期回报按存档/游戏日绑定。

`policy_decision_trajectory.v1` 契约、构建器与 JSONL 写入器现已完成：完整候选集中的未准入项不会
消失，而是作为带排除原因的负例保留；只有一个已准入且当前可用的候选可以成为选择标签。轨迹
同时绑定版本、存档/日切分键、执行原语、实际 tick、fresh after-state 和长回报占位。构建器会拒绝
决策/执行源哈希漂移。下一步把它接入 LiveTrainingLoop，但必须选用滚动重规划后真正对应最终执行
的 effective ranking，不能把初始 ranking 与后续执行错误配对。

## 禁止事项

- 不把 97 当作总动作数；
- 不把 Harness dispatch 当作 Product Executor；
- 不因独立架构重构暂停动作主线；
- 不开始短训或正式训练；
- 不启动游戏，除非当前动作已完成静态和单元测试且明确进入运行验收。

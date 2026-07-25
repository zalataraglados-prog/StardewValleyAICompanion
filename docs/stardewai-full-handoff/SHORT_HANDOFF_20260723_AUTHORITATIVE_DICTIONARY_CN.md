# StardewAI 权威字典短交接（2026-07-23）

## 当前权威产物

- 外部制品根目录：`%STARDEWAI_KNOWLEDGE_ROOT%`，默认值为 `I:\StardewAI-KnowledgeArtifacts\game-1.6.15`
- 原始运行时导出：`%STARDEWAI_KNOWLEDGE_ROOT%\raw\game-1.6.15-20260723T093543Z`
- 当前派生版本：`%STARDEWAI_KNOWLEDGE_ROOT%\derived\game-1.6.15-20260723T093543Z-linux-v19`
- 仓库锁定清单：`knowledge-artifacts.lock.json`
- 总说明：`docs/authoritative-knowledge-dictionary.md`
- 构建状态：`complete_authoritative_identity_graph_stage`
- 权威知识 blocker：0
- 下游能力目录 blocker：0
- 上下文待观察项：235

原始导出是不可变证据，派生版本可以重建。不要手工修改派生 JSON。

## 已完成范围

- 3,550 个 XNB 文件身份清单，585 个运行时语义载荷，失败 0。
- 259 张原生 xTile 地图、1,167 条 warp、5,102 个交互属性，拓扑 blocker 0。
- 824 条条件、258 个事件、31 个 TriggerAction 条目绑定到精确 Linux 1.6.15 程序集及原生解析器。
- 邮件、事件、TriggerAction、商店、NPC 日程、配方、收集包和爷爷评分依赖均已索引。
- 爷爷评分终点是全部 21 分；19 条评分条件总分严格等于 21。
- 10 类评分输入全部透明并带来源证明。
- 权威依赖图有 35,335 个唯一节点、41,262 条唯一边、0 个悬空端点。

## 下游接入状态

- 87 个 OptionRegistry 选项已写入 `downstream-capability-matrix.json`。
- 59 个完整动作全部有步骤编译器。
- 57 个运行动作全部有显式执行器分派。
- 已编译的所有 `executor.*` 都与运行能力目录一致。
- 未知 OptionId 现在明确 blocked，不再伪装成农场维护成功。
- 完整动作若编译出空步骤，会在进入运行队列前 blocked。
- 矿洞、战斗、弹弓、炸弹、食物、梯子、竖井、退矿和出货原语已补参数完整性门禁。
- 49 类已知日候选已经纳入能力目录：47 类可编译，2 类明确为实现阻塞。
- 矿洞、采集黄金镰刀、取得骷髅钥匙和火山滚动候选已接入日计划及动作队列。
- 柜台出售已接通候选、日计划、动作编译、LiveTraining 请求和运行时后验验证。
- 出售使用原生 `ShopMenu.receiveLeftClick`；类别/标签、售价倍率、保护项、槽位、整栈数量和商店身份全部实时复核。
- `quest_candidate`、`special_order_candidate` 仍需任务目标绑定器和少量终端原生交互。

这里的“下游 blocker 0”仅表示 OptionRegistry、步骤编译器和运行分派目录一致。两个任务候选级实现 blocker 单独列账，不能据此宣称完全体陪玩或正式全量训练已经就绪。

## 验证

- KnowledgeCompiler：0 warning，0 error。
- 完整 1.6.15 字典 v19：source blocker 0、source warning 0、ledger blocker 0、option capability blocker 0。
- v19 构建清单：19/19 产物字节数与 SHA-256 独立复核一致。
- Core 测试：1,098/1,098 通过。
- Backend 测试：67/67 通过。
- RuntimeTestHarness：构建成功，0 error。

## 下一步

1. 建立任务目标绑定器：杀怪、钓鱼、采集、社交、移动、下矿和出货复用现有候选与执行器。
2. 实现无法复用的终端任务动作：NPC 交付、特别订单投递箱、接受与领取。
3. 将每类候选逐项绑定到候选生成、日计划、运行原语和后验验证证据。
4. 将候选排除原因、时间窗、执行结果和后验状态差异写入同一训练记录。
5. 采集 18 个场景相关透明字段与 217 个动态/随机边界的运行证据。
6. Windows 客户端进入训练证据链前，生成并绑定 Windows 自身的 runtime-semantics；不得按版本号复用 Linux 方法身份。

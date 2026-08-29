# StardewAI 短交接：EVD-299 普通树产品

## 已完成

- `foraging.harvest_tree_product -> executor.harvest_tree_product` 已完成 read、上游候选排除、DailyPlan、fresh 编译重绑定、类型化请求、共享 terrain BFS、原生 `GameLocation.checkAction`、完整输出域回执与五门准入。
- 权威来源是锁定 1.6.15 反编译与 `I:\StardewAI-KnowledgeArtifacts\game-1.6.15\raw\game-1.6.15-20260723T093543Z\Data_WildTrees-7ca92bf80f36.json`，其 SHA-256 为 `c5a0e4ca6ca34162d4a442b2143e2358663b88cf5a0b7049e3b2dfaa220bff15`。Wiki 仅二次核验；榛子起始日冲突时以实时数据的秋 14 日为准。
- 运行矩阵 6/6 PASS：普通种子、秋季榛子、岛屿棕榈、无种子排除、摇动中排除、tapped 排除。失败的早期 Island fixture 运行不是产品证据；修正为先调用原生 `resetForPlayerEntry` 后，`runtime-wild-tree-island-palm-reset-20260830` 通过。
- 当前对账：`178 registered / 204 semantic / 177 compiler-bound / 102 five-gate / 47 allowlist / 26 blocked / 0 Product Executor`；Core `2035/2035`、Backend `144/144`、全解 `0 warnings / 0 errors`。

## 边界

- 随机附加掉落只给完整条件域，不预读 RNG，也不作为精确监督标签。
- 生产执行不得直接调用 `Tree.shake` 或写树、debris、库存、经验；只允许一次原生 `GameLocation.checkAction`。
- 普通树产品、FruitTree 果实、砍树/清障、苔藓、树木种植与成长是独立语义，不得合并或复制基础设施。

## 下一步

下一冻结语义切片是 `foraging.rummage_garbage`。先用锁定 1.6.15 反编译与实时 `Data/GarbageCans` 确认全部地点/垃圾桶身份、可用时间、NPC 目击惩罚、帽子/特殊掉落、季节/天气/任务/书籍/运气与随机域；再按 `read -> upstream exclusion -> plan -> fresh rebind -> native runtime -> output receipt -> E3` 闭合。必须复用现有共享移动、对象交互、输出守恒、任务资源反馈和随机域表示，不得新建第二套路由、输入或拾取系统。

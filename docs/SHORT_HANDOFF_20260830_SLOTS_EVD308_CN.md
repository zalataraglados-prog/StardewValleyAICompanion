# StardewAI 短交接：Slots EVD-308

## 已完成

`minigame.play_slots` 已形成唯一生产链：`minigame.play_slots -> play_slots -> executor.play_slots`。透明桥发布共享 Rarecrow 齐币需求、赌场权限、ClubSlots 机器、10/100 下注、Luck 倍率、完整概率/图案/倍率分布、期望净收益和活动转轴；上游只在 `(BC)126` 缺失且 Deluxe Scarecrow 依赖开放时生成一次旋转候选；fresh 编译器重绑全部字段。

执行器复用共享 BFS，只经原生 `ClubSlots checkAction`、下注按钮和 Done 输入。共享 `Game1.random` 保持原版所有权，生产代码不写 RNG、转轴、倍率、齐币或 `timesPlayedSlots`。`BuyQiCoins` 已由既有对话响应语义覆盖，没有塞入第二套老虎机执行器。

## 证据和检查点

- EVD-308 隐藏静音 E 盘矩阵 `2/2`：`artifacts/runtime-slots-smoke/runtime-slots-smoke-20260830-132407/summary.json`。
- full snapshot：`155 required / 139 readable / 16 contextual / 0 blocking`。
- 对账：`196 registered / 214 semantic / 195 compiler-bound / 119 five-gate / 53 allowlist / 18 catalogued blocked / 0 Product Executor`。
- 回归：Core `2080/2080`，Backend `148/148`，Release `0 warnings / 0 errors`，KnowledgeCompiler `585/585` blocking 0。

## 下一步

按冻结目录顺序进入 `mining.activate_calico_statue`。先反编译核对沙漠节 Calico Statue 的日期/位置/费用或每日限制、随机祝福、菜单/对话输入、持久状态和退出回执；再复用现有 MineShaft/节日上下文、路线、对话执行和祝福类只读投影，禁止扩大 Slots 或 Dwarf King Statue 证据。

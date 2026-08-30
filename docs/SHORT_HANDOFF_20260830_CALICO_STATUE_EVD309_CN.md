# StardewAI 短交接：Calico Statue EVD-309

## 已完成

`mining.activate_calico_statue` 已形成唯一生产链：`mining.activate_calico_statue -> activate_calico_statue -> executor.activate_calico_statue`。透明桥发布沙漠节骷髅洞门控、房主权威种子输入、雕像地块和站位、完整 18 效果目录、当前/预计效果栈、评分、蛋、生命、耐力与速度回执；上游只在当前层雕像可合法激活时生成一个精确效果候选，fresh 编译器会重新计算并拒绝任何漂移。

执行器复用共享 BFS，只调用一次原生 `MineShaft.checkAction`。生产代码不写评分、效果字典、奖励、生命、耐力、Buff、雕像地块或 RNG。小模型只接受或拒绝当前精确效果，不能选择另一个结果；激活后必须从新快照重规划。

## 证据和检查点

- EVD-309 隐藏静音 E 盘矩阵 `18/18`：`artifacts/runtime-calico-statue/runtime-calico-statue-20260830-144828/summary.json`。
- full snapshot：`156 required / 139 readable / 17 contextual / 0 blocking`。
- 对账：`198 registered / 215 semantic / 197 compiler-bound / 121 five-gate / 54 allowlist / 17 catalogued blocked / 0 Product Executor`。
- 回归：Core `2087/2087`，Backend `149/149`，Release `0 warnings / 0 errors`，KnowledgeCompiler `585/585` blocking 0。

## 下一步

按冻结目录顺序进入 `multiplayer.manage_wallet`。先实时反编译核对独立/共享钱包切换的房主权限、多人菜单端点、资金迁移与守恒、玩家确认边界、在线成员状态和持久化回执；优先复用既有多人透明状态、菜单输入、显式确认和资金守恒设施，禁止建立第二套钱包或金额写入路径。

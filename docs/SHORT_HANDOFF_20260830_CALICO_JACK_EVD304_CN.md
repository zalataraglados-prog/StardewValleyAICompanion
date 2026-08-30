# StardewAI 短交接：Calico Jack EVD-304

## 已完成

`minigame.play_calico_jack` 已形成唯一完整生产链：透明桥发布赌场权限、齐币、Rarecrow 需求、两张桌子、下一局精确随机流、牌、决策和结算；上游只在缺失 `(BC)126` 且 Deluxe Scarecrow 依赖开放时产生单局候选；DailyPlan 与动作编译器从 fresh snapshot 重绑全部机械字段；运行层只驱动原生桌面、对话框和 `CalicoJack` 输入。

低注为 100，高注为 1000。正收益种子且余额允许时采用高注，损失/平局投影降到低注；只有最后 100 且投影损失时延后。动作治理为 `R2 Consumptive / PolicyAuthorizationRequired`，不是逐次玩家确认。小模型不输出牌、随机数、下注点击或执行队列细节。

## 权威与证据

- 锁定 1.6.15 反编译是实现权威。Wiki 的“Luck 不影响”只适用于普通牌局概述；源码 Qi fruit `999` 分支明确读取 `DailyLuck + LuckLevel`，透明桥已记录冲突。
- 隐藏静音原生矩阵 `3/3`：`artifacts/runtime-calico-jack-smoke/runtime-calico-jack-smoke-20260830-085329/summary.json`。
- Core `2060/2060`，Backend `148/148`，Release `0 warnings / 0 errors`。
- full snapshot `151 required / 135 readable with provenance / 16 contextual / 0 blocking`。
- 对账 `188 registered / 210 semantic / 187 compiler-bound / 111 five-gate / 50 allowlist / 22 catalogued blocked / 0 Product Executor`；原生分母 `322/448/150`，blocking 0。

## 下一切片

冻结目录首项是 `minigame.play_crane_game`。先从锁定反编译、实时数据和二级 Wiki 比对建立完整规则/状态/输出分母，再决定它是自主候选还是玩家指令；随后按“透明字段 -> 上游许可 -> DailyPlan -> fresh 编译 -> 复用共享机械引擎 -> 原生运行回执 -> 五门准入”的固定流程完成。禁止复制移动、菜单、库存或小游戏输入体系，也不得因 Calico Jack 的确定性规则外推 Crane Game。

正式全量训练仍未开始。剩余 22 个目录动作、Product Executor、生产长期轨迹、正式 manifest/checkpoint、独立存档评测与第三年爷爷 21 分长跑必须全部通过。

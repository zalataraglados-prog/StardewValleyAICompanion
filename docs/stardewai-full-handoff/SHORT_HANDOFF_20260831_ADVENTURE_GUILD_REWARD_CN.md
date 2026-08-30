# StardewAI 短交接：EVD-317

## 当前结论

`rewards.claim_adventure_guild_reward` 已完成五门闭环并进入高层训练白名单。模型只需输出无参数领取意图；透明桥和 fresh 编译器绑定当前全部完成且未领取的怪物讨伐奖励。唯一运行路径复用共享 BFS，调用原生 Gil 交互，推进可选对话并逐项点击原生奖励菜单，不直接写游戏结果。

## 关键语义

锁定 1.6.15 的 `AdventureGuild.gil()` 一次处理整个当前批次，不支持按目标单独领取。邮件和标志副作用在奖励菜单打开前已经安排，所以候选层必须先用克隆背包和原生插入算法证明整批容量。Gil 柜台贴图会在 `1291/1292/1355/1356/1357/1358` 间动画，运行校验必须接受同一端点集合，不能要求跨帧贴图索引相等。

## 验收状态

- Runtime：`artifacts/runtime-adventure-guild-reward/runtime-adventure-guild-reward-20260831-023014/summary.json`，3/3 PASS。
- Snapshot：165 required / 148 readable / 17 contextual / 0 blocking。
- KnowledgeCompiler：585/585，blocking 0。
- Actions：214 registered / 223 semantic / 9 blocked / 213 compiler-bound / 138 harness dispatch / 137 five-gate / 56 training allowlist / 0 Product Executor。
- Tests：Core 2138/2138；Backend 155/155；Release build 0 warnings / 0 errors。
- Freeze fingerprint：`10b8329f92466d34ce5570679fea0096b298a36693668e8ff107be1794804902`。

## 下一步

下一主切片为 `rewards.claim_prize_ticket`。先从本地反编译确定 `PrizeTicketMenu` 的候选批次、随机边界、背包容量、菜单步骤和所有副作用，再接入现有透明桥、DailyPlan、fresh 编译、共享菜单输入、类型化收据和隐藏隔离测试。`minigame.play_junimo_kart` 是明确暂缓项，不要先做。

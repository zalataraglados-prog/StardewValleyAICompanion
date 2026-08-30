# StardewAI 短交接：普通任务取消 EVD-316

## 当前状态

`quest.cancel -> cancel_quest -> executor.cancel_quest` 已完整闭合。该能力严格为 `PlayerCommandOnly`：只有玩家给出一个当前透明投影中的精确指纹、非空原因和显式确认，才会产生候选；它不进入默认候选、自主日计划或策略训练。

当前对账：`212 registered / 222 semantic / 211 compiler-bound / 137 harness dispatch / 135 five-gate / 55 training allowlist / 10 catalogued blocked / 0 Product Executor`。full snapshot 为 `163/146/17/0`，原生冻结分母仍为 `322 surfaces / 448 branches / 150 map tokens`。

## 已完成能力

- 透明桥发布普通 `questLog` 全部行，而不只发布可执行项；隐藏、未接受、已完成、不可取消或待删除任务带明确阻断诊断。
- 指纹绑定任务 ID、运行类型、标题、当前目标、任务类型、接受/完成/隐藏/每日/可取消/待删除状态、接受日、剩余日和奖励。
- fresh 编译器只保留玩家原因与确认，从当前快照重绑任务身份、日志数量、每日委托标志、副作用和原生合同；伪造的机械字段会被覆盖，缺失/漂移指纹会阻断。
- 生产执行器只创建原生 `QuestLog`，点击精确任务行和 `cancelQuestButton`，验证任务被移除、`accepted=false`、日志数量和同日每日标志，并要求金钱、完成统计、特别订单数量不变。
- 原版没有取消确认框；StardewAI 的显式确认是点击前的外部安全门。不得添加等待不存在菜单的执行阶段。
- `SpecialOrder.CanBeCancelled()` 恒为 false，且原生点击分支只转换普通 `Quest`；特别订单不属于该能力。

## 验证

- 隐藏静音 E 盘矩阵 `4/4`：`artifacts/runtime-quest-cancellation/runtime-quest-cancellation-20260831-011556/summary.json`。
- 同日每日委托清除 `acceptedDailyQuest`；普通任务保持该标志；伪造指纹与不可取消任务均严格拒绝。
- KnowledgeCompiler `585/585`，blocking 0。
- Core `2133/2133`；Backend `155/155`；Release `0 warnings / 0 errors`。

## 下一步

`minigame.play_junimo_kart` 按既定要求继续后置。下一实际纵向切片为 `rewards.claim_adventure_guild_reward`：先用锁定 1.6.15 反编译确认奖励类型、Adventure Guild 原生入口、菜单/对话流程、库存容量、一次性标志和所有副作用，再沿透明投影、上游候选、DailyPlan、fresh 编译、类型化原生执行与隐藏静音回执闭合；不得复用普通任务奖励或直接发放物品。

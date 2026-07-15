# StardewAI 社交原生运行时交接说明 (2026-07-14)

## 状态

**原生社交主链已在 E: 隔离运行环境通过；扩展边界仍需继续验证。**

Controller 于 2026-07-14 在主仓库执行构建+测试验证：
- 社交专项测试：全部通过
- Core 全部测试：通过
- Backend 全部测试：通过
- RuntimeTestHarness 构建：0 警告，0 错误
- 该轮为静态基线；2026-07-15 已补充隐藏、静音的真实运行验证，见下节。

### 2026-07-15 真实运行结论

- 通过工件：`artifacts/runtime-native-social-smoke/runtime-native-social-smoke-20260715-183304/summary.json`。
- 生产链路完整执行：候选排名 -> 日计划 -> 动作队列 -> E: 隔离游戏原生执行 -> after snapshot -> 训练行与 episode。
- 说话：移动至 Pierre 相邻站位，正确朝向，`GameLocation.checkAction` 原生打开 `DialogueBox`，执行结果 `applied/verified`。
- 普通对话关闭：仅用 SMAPI MouseLeft 输入推进一页普通非事件、非问题对话；`dialogue_press_attempts=1`，菜单从 `DialogueBox` 变为 `none`，执行结果 `applied/verified`。
- 送礼：Pierre 收到 `(O)388`，堆叠 `4 -> 3`，当天/本周礼物计数 `0 -> 1`，友谊 `66 -> 46`，负向结果被原样记录，执行结果 `applied/verified`。
- 三个循环各写入 1 条 verified training row，三段 episode 均落盘；游戏、后端和 worker 进程已清理。
- 同轮修复两个上游问题：无配偶的已知空值现在编码为 `player.spouse value="", status="available"`；社交 64 条候选上限先保留可执行/当前地图候选，异地图诊断行不再挤掉本地候选。

## 原生社交运行时冒烟状态 (2026-07-15)

Worker 已完成两次控制器拒绝补丁，替换方案：

1. **PowerShell 候选合法性重新实现已移除**：`Find-LegalSocialTalkCandidate`、`Find-LegalSocialGiftCandidate`、`Build-SocialTalkExecutionRequest`、`Build-SocialGiftExecutionRequest`、`Compute-StandTile` 全部删除。
2. **生产编译链**：冒烟脚本现在启动 ASP.NET Backend（隔离端口 5158），通过 `StardewAI.LiveTrainingLoop --use-daily-plan` 分别以 `social.talk_npc` 和 `social.gift_npc` 候选选项运行。
3. **排名持久化 (NEW)**：LiveTrainingLoop 现在透明保存 `ranking-response-0001.json` 到 `live-snapshots`，replan 路径也保存 `replan-ranking-response-...json`。
4. **正确工件字段路径 (FIXED)**：计划步骤读取 `dailyPlan.plan.steps`，移动步骤通过 `kind=move_to_tile` + `step_id` 包含 `move_to_social_stand` 标识，社交步骤通过 `kind=social_interact` 标识。队列中移动项为 `option_id=executor.move_to_tile` 匹配 `source_action_id`，社交项为 `option_id=executor.social_interact`。
5. **Episode 路径 (FIXED)**：从 `live-snapshots/plan-execution-episode-0001.json` 读取，不再使用不存在的 `episode.json`。
6. **对话关闭 (FIXED)**：通过 `recovery.stabilize_day` 的生产 LiveTrainingLoop 关闭，不再使用手写直接执行请求。
7. **礼物阶段失败封闭 (FIXED)**：仅当对话和礼物都通过时返回 `"passed"`，永不返回 `"passed_talk_only"` 或 `"skipped"`。缺失礼物候选或受阻执行会 throw/fail。
8. **社交执行字段直接验证 (NEW)**：对话需要 native handled + 精确 NPC/位置/瓦片 + 相邻玩家位置/朝向 + dialogue 打开/关闭证据。礼物需要 native handled + 精确礼物槽/物品 + `stack_after=stack_before-1` + gifts-today/week 增量 + 前后 friendship 非 null。
9. **locations.route_graph BFS 遍历 (NEW)**：当无同位置候选人时，读取透明 `locations.route_graph`，BFS 查找已加载普通 NPC 的位置，通过生产 `executor.traverse_connector` 遍历每个边，保存请求/结果并验证到达。无有界路由时 fail closed。
10. **端口冲突守卫**：Backend 端口（可配置，默认 5158）加入守卫，Backend 和游戏进程均精确 PID 清理。
11. **源守卫重写 (FIXED)**：断言实际 JSON 字段路径和精确 option/kind 映射。拒绝 `.plan_steps`、`.candidates`、`executor.move_to_social_stand`、可选的 episode/ranking、`passed_talk_only`、手写关闭请求、缺失的 route traversal。
12. **仅限有界编辑**：无全文件编码/格式化重写，无游戏启动。715 全部测试通过，0 警告 0 错误。

## 已实现能力

1. **`executor.social_interact`** — 原生社交交互执行器，仅通过 `Game1.currentLocation.checkAction` 实现唯一状态变更路径，不直接调用 `NPC.checkAction`、`tryToReceiveActiveObject`、`receiveGift`、`changeFriendship`、物品/位置/NPC 直接变异。
2. 执行前验证：世界就绪、NPC 身份/存在/位置/瓦片/相邻/动作矩形/可见/睡眠/CanSocialize/CanReceiveGifts、菜单关闭、gift 的精确槽位/物品/堆叠及礼物上限；每日上限仅由 Stardrop Tea 绕过，每周上限可由配偶、生日或 Stardrop Tea 绕过。
3. 完整前后输出：NPC 存在/位置/瓦片/可见/睡眠/普通村民、玩家瓦片/朝向/选中槽位、礼物 ID/堆叠/品质/槽位、友谊行/点数/对话计数/礼物计数、菜单/对话计数/对话键/说话者、原生 handled、时间戳/滴答数、验证/失败/校准范围、变更事实。未知/未解析状态为 null/空，不猜测。
4. 所有阻塞执行器结果在 `FailureCategory` 记录精确运行时原因，并使用 `TrainingImpactScope=executor_calibration`；运行时失败不进入策略负反馈。
5. **`social.talk_npc`/`social.gift_npc` 仍通过日计划编译器门控**，不直接运行时启用。只有编译后的 `executor.social_interact` 在运行时可用。
6. `recovery.stabilize_day` 候选→日计划链完整（close_menu / refresh_plan / sleep）。
7. 社交候选构建器（SocialCandidateBuilder）当前状态说话/礼物候选，含完整合法性检查。
8. 日计划编译器（DailyPlanCompiler）将社交候选人编译为 `move_to_social_stand` + `social_interact` 两步。
9. 动作队列编译器（ActionQueueCompiler）验证并编译 `executor.social_interact` 计划步骤。
10. `SocialPlanEnvelope` 训练记录契约已定义。

## 未实现 / 已知限制

- 无跨地图社交路由（仅同位置）。
- 无未来日程窗口（仅当前状态快照）。
- 无礼物嫉妒、对话分支、拒绝文本确定性处理。
- 无 Mod/重写 NPC 社交方法——失败关闭。
- 已完成普通说话、普通对话关闭和普通堆叠礼物的 E: 隔离运行验证；尚未覆盖单件礼物 `1 -> null` 与受阻后重规划。
- 执行器持续时间仍为规划器预算假设，需运行时校准。

## 下一步（社交扩展验收）

1. 单件礼物 `1 -> null` 冒烟测试
2. 阻塞/重规划案例
3. 持续时间校准
4. Mod/重写 NPC 与问题对话继续 fail closed

运行时失败始终为执行器校准，永不策略负反馈。

## 关键事实（与前次交接不同）

- **`social_native_executor_not_implemented` 已移除**。`executor.social_interact` 已在 OptionRegistry、ActionQueueCompiler、DailyPlanCompiler、RuntimeTestHarness 中完整实现。
- 所有社交代码产物在 sandbox 中，通过 patch 交付。
- 保持 Stage 6 完美策略冻结和 Stage 7 人类适应设计不变。

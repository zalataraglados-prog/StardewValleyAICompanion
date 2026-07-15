# StardewAI 社交原生运行时交接说明 (2026-07-14)

## 状态

**静态就绪，未在实时运行环境中验证。**

Controller 于 2026-07-14 在主仓库执行构建+测试验证：
- 社交专项测试：163/163 通过
- Core 全部测试：462/462 通过
- Backend 全部测试：49/49 通过
- RuntimeTestHarness 构建：0 警告，0 错误
- 未启动游戏。

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
- 无实时运行环境测试（E: 隔离运行环境待集成）。
- 执行器持续时间仍为规划器预算假设，需运行时校准。

## 下一步（E: 运行环境集成）

1. 说话冒烟测试
2. 普通礼物冒烟测试（含单品→null）
3. 阻塞/重规划案例
4. 输出产物审计
5. 持续时间校准

运行时失败始终为执行器校准，永不策略负反馈。

## 关键事实（与前次交接不同）

- **`social_native_executor_not_implemented` 已移除**。`executor.social_interact` 已在 OptionRegistry、ActionQueueCompiler、DailyPlanCompiler、RuntimeTestHarness 中完整实现。
- 所有社交代码产物在 sandbox 中，通过 patch 交付。
- 保持 Stage 6 完美策略冻结和 Stage 7 人类适应设计不变。

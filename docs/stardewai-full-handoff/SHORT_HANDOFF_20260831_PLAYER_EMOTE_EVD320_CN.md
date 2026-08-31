# StardewAI 短交接：EVD-320

## 已完成

`social.emote` 与 `executor.perform_emote` 已闭合。两者仅接受玩家明确给出的表情 key、理由和确认，不进入自主候选或策略训练。透明桥实时读取锁定版 22 项表情、4 项隐藏表情、完整图标/动画、收藏与 performed 状态；锁定目录漂移会失败关闭。

生产执行器只逐字符输入 `/emote <key>` 并调用原生 `ChatBox.textBoxEnter`。`netDoEmote`、`performPlayerEmote`、`doEmote` 与 `performedEmotes` 写入均由原生游戏拥有；生产代码只读并验证本地 performed 与图标/动画回执。远端可见性依赖原生网络事件，不伪造远端回执。

## 验证与口径

- 隐藏静音 E 盘：`runtime-player-emote-20260831-145638`，23/23。
- 快照：168 required / 151 readable / 17 contextual / 0 blocking。
- 动作：220 registered / 226 semantic / 219 compiler-bound / 141 harness / 143 five-gate / 58 allowlist / 6 blocked / 0 Product Executor。
- KnowledgeCompiler：585/585、blocking 0。
- 回归：Core 2162/2162、Backend 155/155、Release 0 warnings / 0 errors。
- 冻结指纹：`d145570835f06f8ffb14460ce1107950a4aed243939ed60ee08c583bc17a97e9`。

## 下一步

下一切片为 `social.watch_movie`：先反编译并冻结购票、邀请、放映、同伴反应、奖励和一次性状态，再划分模型选择与既有社交/商店/菜单机械层。`minigame.play_junimo_kart` 继续暂缓；正式全量训练尚未开始。

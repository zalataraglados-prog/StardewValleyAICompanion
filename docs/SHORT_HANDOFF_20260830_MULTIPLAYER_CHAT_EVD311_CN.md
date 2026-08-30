# StardewAI 短交接：联机聊天 EVD-311

## 已完成

`multiplayer.send_chat` 已形成唯一生产链：`multiplayer.send_chat -> send_multiplayer_chat -> executor.send_multiplayer_chat`。透明桥发布 live sender、语言和默认颜色、网络角色、消息队列边界、ChatTextBox 宽度及在线收件人的原生枚举/匹配信息。

该能力严格为 `PlayerCommandOnly`。玩家必须给出作用域、原因、正文和确认；私聊另需绑定一个当前在线玩家。任意斜杠命令、控制字符、模糊目标和原生 880px 内容宽度无法容纳的文本都 fail closed，不进入默认候选、自主规划或策略训练。

fresh 编译器只保留玩家意图，全部机械字段从当前 `player.multiplayer_chat` 重绑。运行层只经 `ChatBox.activate -> ChatTextBox.RecieveTextInput -> ChatBox.textBoxEnter`，由原版完成过滤、全局 AllPlayers/私聊 exact recipient 和 type-10 分发，再验证发送者本地 kind 0/kind 3 回执。生产代码不直接调用 `sendChatMessage` 或 `receiveChatMessage`，也不伪造远端送达。

## 证据和检查点

- EVD-311 隐藏静音 E 盘矩阵：`artifacts/runtime-multiplayer-chat/runtime-multiplayer-chat-20260830-183025/summary.json`，全局与私聊 `2/2 applied/verified`。
- full snapshot：`158 required / 141 readable / 17 contextual / 0 blocking`。
- 对账：`202 registered / 217 semantic / 201 compiler-bound / 125 five-gate / 54 allowlist / 15 catalogued blocked / 0 Product Executor`。
- 回归：Core `2105/2105`，Backend `151/151`，Release `0 warnings / 0 errors`，KnowledgeCompiler `585/585` blocking 0。

## 下一步

`minigame.play_junimo_kart` 继续复用既有 AI 等价执行，原生完美代打后置到核心能力训练完成后且仅由玩家命令触发。下一实际纵向切片是 `player.choose_bobber`：先核对已有钓鱼菜单、浮标/鱼漂身份、装备限制和确认治理，优先复用现有原生菜单输入与钓鱼透明状态，禁止建立第二套钓鱼装备执行器。

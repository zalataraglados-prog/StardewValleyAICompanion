# StardewAI 短交接：联机钱包 EVD-310

## 已完成

`multiplayer.manage_wallet` 已形成唯一生产链：`multiplayer.manage_wallet -> manage_multiplayer_wallet -> executor.manage_multiplayer_wallet`。透明桥发布 live ManorHouse LedgerBook 端点、共享/独立模式、今晚待切换状态、已认领参与者、收款人原生响应顺序、共享及个人余额、赠款统计、五项命令门控和次日结算投影。

五项命令是 `schedule_separate`、`cancel_separate`、`schedule_merge`、`cancel_merge`、`transfer`。模式命令仅房主可用；转账仅独立钱包模式可用，并绑定精确收款人 ID、原生响应键和金额。模式变化在次日由原生 `ManorHouse.SeparateWallets/MergeWallets` 结算。所有操作均为 `PlayerCommandOnly`，要求显式原因和确认；转账另需第二级确认，不进入默认候选或策略训练。

fresh 编译器只继承操作意图，并重新绑定实时模式、权限、余额、收款人、LedgerBook 站位和投影指纹。跨地图 continuation 保留同一操作、收款人和金额，只允许匹配的 `executor.manage_multiplayer_wallet` 原生成功回执结束目标。运行层复用共享 BFS，只使用原生 LedgerBook、`DialogueBox` 和 `DigitEntryMenu`；生产代码不写钱包标记、共享余额、个人余额或赠款统计。

## 证据和检查点

- EVD-310 隐藏静音 E 盘矩阵：`artifacts/runtime-multiplayer-wallet/runtime-multiplayer-wallet-20260830-154907/summary.json`，五项即时命令与两项次日结算共 `7/7 applied/verified`。
- full snapshot：`157 required / 140 readable / 17 contextual / 0 blocking`。
- 对账：`200 registered / 216 semantic / 199 compiler-bound / 123 five-gate / 54 allowlist / 16 catalogued blocked / 0 Product Executor`。
- 回归：Core `2096/2096`，Backend `150/150`，Release `0 warnings / 0 errors`，KnowledgeCompiler `585/585` blocking 0。

## 下一步

`minigame.play_junimo_kart` 继续按既定边界复用现有定时等价执行，原生完美代打后置为核心能力训练完成后的玩家命令扩展。下一实际切片是 `multiplayer.send_chat`：先锁定 ChatBox 原生发送、命令解析、聊天类型、收件范围、长度/内容限制、多人传输和日志回执，再复用玩家命令治理与类型化输入链；不得把自由文本自动生成纳入当前策略训练，也不得建立第二套聊天网络协议。

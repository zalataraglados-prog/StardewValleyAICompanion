# StardewAI 短交接：EVD-312

`player.choose_bobber` 已完整闭合为严格 `PlayerCommandOnly` 纵向切片：透明字段 `player.bobber_selection`、显式候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、`executor.choose_bobber_style`、类型化请求、共享逐帧移动和原生菜单回执均已接通。

原版 1.6.15 规则已经按反编译锁定：FishShop `Action=Bobbers` 打开 `ChooseFromIconsMenu("bobbers")`；固定样式 `0..38` 在 `style <= fishCaught.Count()/2` 时解锁，随机样式为 `-2`，声呐浮标只把显示覆盖为 `39`。生产执行器只使用 `checkAction`、准确非锁定图标点击和原生关闭按钮，禁止直接写偏好或 RNG。

隐藏静音矩阵 `3/3` PASS：`artifacts/runtime-bobber-selection/runtime-bobber-selection-20260830-193216/summary.json`。权威状态为 `204 registered / 218 semantic / 203 compiler-bound / 127 five-gate / 54 allowlist / 14 blocked`；full snapshot `159/142/17/0`；KnowledgeCompiler `585/585`、blocking 0；Core `2112/2112`、Backend `152/152`、Release `0 warnings / 0 errors`。

下一实际纵向切片是 `player.choose_jukebox_track`。继续复用当前玩家命令治理、跨地图 continuation、共享移动和原生菜单输入；先反编译核对 Jukebox 曲目目录、解锁来源、菜单身份、当前播放状态和多人同步，不得把外观/音乐命令加入自动候选或策略训练。`minigame.play_junimo_kart` 仍按既定决定后置原生完美代打。

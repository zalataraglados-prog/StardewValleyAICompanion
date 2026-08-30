# StardewAI 短交接：EVD-313

`player.choose_jukebox_track` 已完整闭合为严格 `PlayerCommandOnly` 纵向切片：透明字段 `player.jukebox_selection`、显式候选、跨地图 continuation、DailyPlan、fresh 编译重绑定、`executor.choose_jukebox_track`、类型化请求、共享逐帧移动和原生菜单回执均已接通。唯一动作链是 `player.choose_jukebox_track -> choose_jukebox_track -> executor.choose_jukebox_track`。

原版 1.6.15 规则已经按反编译锁定：Saloon 两个 `Action=Jukebox` 地块打开 `ChooseFromListMenu`；`Utility.GetJukeboxTracks` 先加入数据中明确可用的曲目，再加入玩家听过并经 AlternativeTrackIds 规范化、soundbank 存在且未被明确禁用的曲目。菜单从索引 0 开始，前进循环，OK 应用曲目，Cancel 关闭；绿雨期间非 `rain` 曲目失败关闭。生产执行器只使用共享移动、`checkAction` 和菜单原生点击，禁止直接切换音乐或写解锁、播放及 Mini-Jukebox 状态。

隐藏静音矩阵 `3/3` PASS：`artifacts/runtime-jukebox-selection/runtime-jukebox-selection-20260830-201036/summary.json`。权威状态为 `206 registered / 219 semantic / 205 compiler-bound / 134 harness dispatch / 129 five-gate / 54 allowlist / 13 blocked`；full snapshot `160/143/17/0`；KnowledgeCompiler `585/585`、blocking 0；Core `2118/2118`、Backend `153/153`、Release `0 warnings / 0 errors`。

下一实际纵向切片是 `player.customize`。继续复用当前玩家命令治理、跨地图 continuation、共享移动和原生菜单输入；先锁定 1.6.15 的入口、完整可选域、费用/解锁/角色约束、原生菜单状态及回执，再决定是否需要新的透明字段。`minigame.play_junimo_kart` 仍按既定决定后置原生完美代打。

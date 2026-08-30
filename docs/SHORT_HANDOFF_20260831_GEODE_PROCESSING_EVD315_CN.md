# StardewAI 短交接：铁匠晶球处理 EVD-315

## 当前状态

`processing.crack_geode -> crack_geode -> executor.crack_geode` 已完整闭合。高层动作进入自主候选和策略训练；模型只决定是否处理哪一种晶球及目的，路线、柜台、槽位、费用、计数、随机上下文、输出和原生输入均由 fresh 编译器与机械执行层负责。

当前对账：`210 registered / 221 semantic / 209 compiler-bound / 136 harness dispatch / 133 five-gate / 55 training allowlist / 11 catalogued blocked / 0 Product Executor`。full snapshot 为 `162/145/17/0`，原生冻结分母为 `322 surfaces / 448 branches / 150 map tokens`。

## 已完成能力

- 全部八种基础输入：Artifact Trove、四种普通晶球、Golden Coconut、Mystery Box、Golden Mystery Box。
- 透明预测：原生计数增量时序、save/player seed、固定次数 RNG 预热、数据驱动掉落、文物保护、Mystery Book 邮件和金椰子互斥。原生共享 RNG 作物分支发布完整当季族，不宣称精确身份。
- 上游门控：Clint、成品工具领取、角色控制、菜单/事件、25g、容量、目标位置和可达站位；不可行项不进入排序。
- 原生执行：共享移动、Blacksmith `checkAction`、`Process` 对话、GeodeMenu 两次点击、2700ms 动画、剩余堆叠归还和菜单关闭。
- 回执：输入/金钱/计数/库存、首次矿物或文物邮件、石头与矿石领取副作用、金椰子标志和团队金核桃；生产路径不直接写这些状态。

## 验证

- 隐藏静音 E 盘矩阵 `9/9`：`artifacts/runtime-geode-processing/runtime-geode-processing-20260831-003313/summary.json`。
- KnowledgeCompiler `585/585`，blocking 0。
- Core `2128/2128`；Backend `155/155`；Release `0 warnings / 0 errors`。

## 下一步

`minigame.play_junimo_kart` 按既定要求继续后置。下一实际纵向切片是 `quest.cancel`：先按锁定 1.6.15 反编译确认 QuestLog 可取消类型、菜单入口、确认流程、不可取消/剧情/特别订单边界和副作用，再沿同一条透明投影、上游候选、DailyPlan、fresh 编译、类型化原生执行与隐藏静音回执流程闭合，禁止另建第二套任务状态写入系统。

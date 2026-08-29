# StardewAI 短交接：EVD-301 住宅装修

## 已完成

- 唯一语义链为 `housing.renovate -> renovate_home -> executor.renovate_home`。它复用既有碰撞网格、移动、Robin 服务和菜单基础设施，没有第二套房屋/建筑系统。
- 透明桥实时发布完整 `Data/HomeRenovations` 18 项目录及原生可用顺序、要求、动作、区域、阻挡、婴儿床门、费用、`FirstPurchase_<RoomId>` 和退款投影。基础 1.6.15 payload SHA-256 锁为 `26bdcd0681a57c1f749d249ad9305ffa1d58c433c86c1a0b954d0052c6d5d40b`。
- 所有 18 项均为 `PlayerCommandOnly`。命令必须给出 `renovation_id`、`selected_index`、原因和确认；破坏动画另需 `confirm_destructive=true`。默认候选、策略训练和 allowlist 必须继续排除。
- fresh 编译器重绑目录、原生商店索引、价格、首次购买、要求/动作 JSON、区域和投影指纹，并从 `locations.collision_grid` 派生或复核相邻可达站位。LiveTrainingLoop 新增 `--daily-plan-invocation-source PlayerCommand`，解决显式命令在安全门丢失来源的问题。
- 正式执行器只走 `Carpenter -> Renovate -> ShopMenu("HouseRenovations") -> RenovateMenu hover -> region click`。不得直接写钱、邮件、`NetInt`、地图、家具、菜单、视口或事件；直接前态构造只允许 `debug.setup_home_renovation` 隔离夹具。
- 跨地图滚动执行把 `option_id`、装修 ID、区域、原因、普通确认和破坏性确认写入 continuation；每轮排名恢复 `PlayerCommand` 来源和明确确认，到达 Robin 后从 fresh snapshot 重建终端候选。过滤器只接受同一装修目标，并仅在匹配的 `executor.renovate_home` 原生执行成功后结束。

## 证据

- 隐藏静音 E 盘：`artifacts/runtime-home-renovation/runtime-home-renovation-20260830-051022/summary.json`，实时 18 项加负价无首次购买标记分支，`19/19 applied/verified`。
- Core `2045/2045`、Backend `148/148`、Release `0 warnings / 0 errors`。
- full snapshot schema `146/130/16/0`；KnowledgeCompiler `585/585`、blocking `0`。
- 对账 `182 registered / 206 semantic / 181 compiler-bound / 105 five-gate / 48 allowlist / 24 catalogued blocked / 0 Product Executor`；原生分母 `322 surfaces / 448 branches / 150 map tokens` 不变。

## 下一步

- 下一冻结切片是 `island.field_office_donate`。先按实时反编译和数据锁定捐赠集合、菜单分支、奖励、完成状态和持久化回执，再复用既有岛屿路线、库存选择、显式确认和原生菜单执行基础设施。
- 不要把住宅装修加入正式策略训练。它的五门证据仅证明玩家指令执行能力。

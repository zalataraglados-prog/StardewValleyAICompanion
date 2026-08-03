# 鱼塘服务训练准入短交接

状态：2026-08-03，EVD-210 切片。

## 已完成

- `fishing.service_fish_ponds` 双分支五门绑定 EVD-210；
- 沿用唯一 `collect_fish_pond_output -> executor.collect_fish_pond_output` 和
  `complete_fish_pond_request -> executor.complete_fish_pond_request` 链；
- 既有隔离制品验证产物收取和人口请求两次原生生命周期；
- 权威对账为 97 registered / 165 semantic / 0 missing / 83 compiler-bound /
  14 five-gate-closed / 13 training-allowlisted / 0 Product Executors；
- 聚焦回归 Core 36/36 通过；本切片未启动游戏。

## 严格边界

只接纳已完工、精确原版基础 `FishPond`。产物存在时必须优先收取，且精确核验单位状态、背包入账
和价格派生 Fishing XP。只有产物为空、请求未解决、物品与数量完整且全部绑定在工具栏时，才逐件
调用原生交互完成请求，并核验物品消耗、人口上限、解锁门槛、刷新计时、完成标记和请求 XP。
sign 与 Golden Animal Cracker 截获必须在上游排除。

`PolicyAuthorizationRequired` 允许模型在已授权策略中选择资源消耗，不是每次弹窗确认；它不得被
误写为 `ExplicitUserConfirmationRequired`，也不得取消候选、编译和运行时的资源校验。

## 仍阻塞

运行制品只覆盖一种鱼、产物和请求组合；其他原版数据驱动组合依赖同一实时精确投影，漂移即失败
关闭。未完工/自定义鱼塘、加鱼、标牌、饼干、查询菜单和鱼塘钓鱼不在本目标。正式生产轨迹、跨度
观测、manifest、checkpoint、Product Executor 与正式全量训练均未开始。

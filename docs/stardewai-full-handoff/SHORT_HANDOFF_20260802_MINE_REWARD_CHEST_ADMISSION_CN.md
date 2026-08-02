# 矿井奖励箱训练准入短交接

状态：2026-08-02，EVD-207 切片。

## 已完成

- `mining.claim_reward_chests` 五门绑定 EVD-122；
- 沿用唯一 `claim_mine_reward_chest -> executor.claim_mine_reward_chest` 链；
- 原生矩阵覆盖 20 层固定奖励、100 层星之果实和 320 层两个强制随机奖励；
- 权威对账为 97 registered / 165 semantic / 0 missing / 80 compiler-bound /
  11 five-gate-closed / 10 training-allowlisted / 0 Product Executors；
- 聚焦回归 Core 35/35 通过；本切片未启动游戏。

## 严格边界

只准入当前已加载 MineShaft 中透明读取完整、精确奖励已知、状态为 ready、存在可达站位且
库存可接收的原版奖励箱。候选和编译器重绑箱体、奖励、分支、星之果实效果和原生清箱生命周期。
骷髅钥匙特殊箱使用独立链；金镰刀祭坛、未知或模组箱体、未加载推测奖励均不在范围内。

## 授权门

`mining.acquire_golden_scythe` 的隔离运行已验证 59/59 动作，但该 option 明确要求玩家显式确认。
运行证据不能覆盖授权策略，所以它仍不进入训练白名单。

## 仍阻塞

正式生产轨迹、跨度观测、manifest、checkpoint、Product Executor 与正式全量训练均未开始。

# 短交接：普通矿井骷髅钥匙训练准入

状态日期：2026-08-02

## 已完成

- `mining.obtain_skull_key` 的五门证据全部绑定到已有 `EVD-106`；
- 证据范围仅为普通矿井 119 -> 120 层、原生 `which=4` 宝箱领取、
  `has_skull_key false -> true` 和原生退出；
- 复用现有矿井候选、`MiningFloorStepPlanner`、`MiningFloorStepCompiler` 和内部执行链，
  没有新增第二套系统；
- 权威看板为：97 个注册项、165 个语义动作、0 个漏注册、6 个五门闭环、5 个训练准入，
  Product Executor 仍为 0；
- 训练白名单现为 `inventory.transfer_item`、`mining.obtain_skull_key`、
  `mining.reach_depth`、`social.gift_npc`、`social.talk_npc`。

## 严格边界

普通矿井、沙漠矿洞、采石场矿洞金镰刀和火山矿洞是四个独立族。
`EVD-106` 不得放行 `mining.acquire_golden_scythe`、`volcano.reach_caldera` 或任何
Skull Cavern 目标，也不得被解释为全矿洞完成。

## 下一步

继续按权威字典依赖顺序选择已有完整运行证据的窄候选，先审计五门和输出字段，再登记准入；
缺任一门时补原生运行证据，不复制已有候选、编译器或执行器。`farm.maintain_crops` 当前仍是
宽目标，不能用单格播种证据整体放行。扩大准入后再采集真实 verified/fresh 的长期 v2 rollout，
生成正式 manifest，才允许运行正式 V1 全量训练。

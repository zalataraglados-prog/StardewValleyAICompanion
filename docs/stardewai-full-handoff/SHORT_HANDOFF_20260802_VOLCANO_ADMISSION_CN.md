# 短交接：火山 Caldera 目标训练准入

状态日期：2026-08-02

## 已完成

- `volcano.reach_caldera` 五门绑定 `EVD-190`、`EVD-191`；
- 复用现有透明火山读取、`VolcanoReachCalderaCandidateBuilder`、
  `VolcanoFloorStepPlanner`、`VolcanoFloorStepCompiler` 和唯一原生运行链；
- 权威看板为 97 个注册项、165 个语义动作、0 个漏注册、7 个五门闭环、
  6 个训练准入、0 个 Product Executor；
- 训练白名单为 `inventory.transfer_item`、`mining.obtain_skull_key`、
  `mining.reach_depth`、`social.gift_npc`、`social.talk_npc`、
  `volcano.reach_caldera`。

## 准入含义

模型可以学习“当前是否应选择到达 Caldera”这个高层目标。模型不输出浇岩浆、清石、
战斗、移动或开门的逐帧安排；这些动作仍由透明状态和确定性编译执行器生成。只有
applied/verified、fresh、状态确实变化且与有效候选/队列绑定的轨迹才能进入正式数据集。

## 严格边界

火山证据不覆盖普通矿井、Skull Cavern 或采石场矿洞金镰刀。EVD-190/191 证明当前原版
生成层滚动链与目的化战斗，但任意种子/属性、模组怪物、多人和 Product Executor 仍未完成。
正式生产轨迹、跨度标签、manifest 和 checkpoint 仍不存在，不能开始或宣称正式全量训练。

## 下一步

继续从权威清单选择“候选实际边界与运行证据边界一致”的高层目标。宽泛的商店、钓鱼、
种植或机器 option 若只有单商品、单地点、单格或单机器烟测，不得整体准入；先拆出可执行的
有界候选或补齐覆盖，再登记五门。

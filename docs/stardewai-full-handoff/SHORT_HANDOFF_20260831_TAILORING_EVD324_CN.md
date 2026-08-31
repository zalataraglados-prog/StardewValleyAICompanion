# 裁缝训练准入短交接：EVD-324

`tailoring.sew_item -> tailor_item -> executor.tailor_item` 已完整闭合。小模型只选择一个实时发布的裁缝候选；透明桥与 fresh 编译器绑定库存输入、原生入口、配方、输出域、余料、容量和裁缝历史。染色及棱彩外观分支不属于该动作，继续由 `tailoring.dye_item` 玩家指令拥有。

锁定版 1.6.15 依据：`Action=Tailoring` 在事件 `992559` 后开放，放置的 `(BC)247` 可直接打开 `TailoringMenu`；鞋靴对执行属性转移，其他输入使用第一个匹配的实时 `Data/TailoringRecipes`；随机配方发布完整 `CraftedItemIds` 域。运行层仅点击原生库存、左右输入槽和开始按钮，等待 1500ms 生命周期并原生收回所有余料。

隐藏静音 E 盘测试 `3/3` PASS：

- `BasicPullover_FromWood` 确定配方；
- `PrismaticClothes` 原生随机结果域；
- 运动鞋外观保留并继承太空之靴 `4防御/4免疫`。

证据：`artifacts/runtime-tailoring/runtime-tailoring-20260831-225159/summary.json`。full snapshot 为 `171 required / 154 readable / 17 contextual / 0 blocking`；动作对账为 `228 registered / 230 semantic / 227 compiler-bound / 145 harness / 151 five-gate / 62 allowlist / 2 blocked / 0 Product Executor`；Core `2191/2191`、Backend `155/155`、Release `0 warnings / 0 errors`。

下一步固定为 Product Executor。必须把已验证的唯一动作状态机接入正式产品分发、授权、fresh 漂移门、持久回执和失败重规划；在机器可读看板仍为 `0 Product Executor` 时，禁止用 RuntimeTestHarness、fixture、`--skip-training` 或 baseline 汇总冒充正式全量训练。

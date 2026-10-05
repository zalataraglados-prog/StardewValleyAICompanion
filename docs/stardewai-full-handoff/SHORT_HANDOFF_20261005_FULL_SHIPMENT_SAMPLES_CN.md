# Full Shipment 分层样本短交接（2026-10-05）

## 当前事实

- 生产运行时分层索引严格为 `2/26`：防风草覆盖 `harvests_as`，Sap 覆盖 `native_wild_tree_chop_drop`。
- 浆果入口已绑定 `full_shipment:item:296 / (O)296 / native_bush_shake / foraging.harvest_bushes`。
- 姜入口已绑定 `full_shipment:item:829 / (O)829 / native_ginger_harvest / foraging.harvest_ginger`。
- 茶叶入口已绑定 `full_shipment:item:815 / (O)815 / native_tea_bush_harvest / foraging.harvest_bushes`。
- 榛子入口已绑定 `full_shipment:item:408 / (O)408 / native_wild_tree_seed_drop / foraging.harvest_tree_product`。
- 椰子入口已绑定 `full_shipment:item:88 / (O)88 / native_wild_tree_seed / foraging.harvest_tree_product`。
- 五者共用既有 forage fixture 编排、候选、DailyPlan、动作编译器、产品执行器和 verifier；没有第二套动作系统。
- 浆果、姜、茶叶、榛子位于 `Farm 64,15`；椰子使用 `wild_tree / island_palm / IslandSouth 20,20`。夹具设置不属于 acquisition proof 根，但 proof 根必须匹配场景的精确地点。

## 已验证

- `LZT` 隔离测试机上：GoalConditionedBootstrap Release `0 warning / 0 error`。
- acquisition route dispatch 与 Full Shipment 静态可编译性自测通过。
- 最新样本守卫 `7/7`、Core game-free `128/128` 通过。
- 本机仅做轻量解析和 Git 检查，避免再次因大测试卡死。

## 未完成与退出条件

- 新电脑当前没有可用的星露谷原生运行环境；当前五个待证样本都没有 fresh Full Shipment MonoGame 回执，不得计入覆盖。历史 action smoke 只能证明可复用性。
- 原生环境可用时按浆果、姜、茶叶、榛子、椰子顺序运行。每层必须同时满足：原生执行 `applied/verified`、fresh 终态一致、精确 execution binding、独立 rollout proof 重建通过、生产 evidence index 接纳。
- 满足一层才把 `2/26` 增加一；在全部 26 层与其余训练门完成前，`formal_product_training_authorized=false`。

## 下一步

继续从剩余 24 个生产缺失分层中选择可复用既有原生链的样本，沿同一脚本和断点 proof 合同增加入口；不要新增候选、编译器或执行器。大测试继续放到 `LZT`，本机不运行游戏或完整回归。

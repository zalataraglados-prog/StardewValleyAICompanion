# Full Shipment 分层样本短交接（2026-10-05）

## runner 维护边界

- 场景无关的 JSON/快照/HTTP、存档哈希、进程与环境恢复、DailyPlan/Teacher/precompiled queue 包装已迁到 `scripts/lib/RuntimeEvidenceCommon.ps1`。
- 主 runner 仍是唯一 Full Shipment 场景组合层，保留所有来源、物品、夹具、出货、睡眠与 recurrence 断言；共享库不得出现 `full_shipment` 或 `sap_prefix` 规则。
- 拆分没有新增执行路径或生产证据。接续时先跑 PowerShell/source guard/game-free 离线回归，再做少量代表性原生抽样，最后统一重建 26 层 production evidence index。

## 入口配置完成

- milestone 已将 production evidence index 验真前移：导入摘要后必须独立重建并确认 Sap 共享 settlement、两个 anchor 的 exact native sample、route occurrence 与 requirement/item 身份，才允许启动首个非 anchor 游戏场景。`-Batch all` 从零生成 anchor 后也在跨入高风险层前执行同一门；失败写 checkpoint 后立即停止。
- 首个 standard 场景还有第二道阶段门：7 个 high-risk 摘要必须齐全，并由同一 production evidence index 逐层验明 exact native sample。直接运行 `standard` 或通过 `all/all_missing` 跨阶段都不能绕过；断点续跑若已从 standard 开始，只重建一次索引并同时复核 anchor 与 high-risk。
- SSH 或无 Explorer 同会话时使用 milestone 的 `-EvidencePreflightOnly`；它会构建 index、执行相同阶段断言并在 runner 循环前退出。不要把 `-PlanOnly` 的 `planned_only` checkpoint 当成 anchor 验真结果。
- 共享 runner 已配置 `26/26` 个权威运行时入口。最后一层为 `location_fish:Town:3 -> (O)388`，使用完整透明钓获分布和既有原生 `catch_fish` 链。
- `26/26` 是编排入口覆盖，不是生产证据覆盖。runner 的场景无关编排拆分已经完成；当前可独立验收的新版本生产证明仍只有 Sap 和放射性矿石。下一步先导入并独立重验这两个 anchor，再执行高风险与标准里程碑抽样，最后统一重建 production evidence index；训练准入仍为 `false`。

## 接续状态更新

- 当前权威分母为 26 层，runner 已配置 `25/26` 个入口。新增代表为 `bundle:Bulletin Board/33:reward -> (O)336` 的 `creates_reward_item`，复用现有社区中心原生领奖链；严格生产证据没有因此增长。
- 剩余入口严格为 1 层：`native_location_fish_spawn`。配置入口完成不等于原生生产证明；训练准入仍为 `false`。

## 最新可接续状态

- 源码在 `I:\StardewValleyAICompanion`；本机轻量/历史测试在 `E:`；高负载隔离运行环境在异机 `F:\StardewAI-TestLab`。异机已有完整游戏运行时，不再沿用“新电脑无运行环境”的旧描述。
- Sap 当前版本证明 `runtime-full-shipment-sap-refresh-20261005-183733` 已通过全部四阶段。睡眠 `applied/verified`，日期 `1 -> 2`，菜单关闭，recurrence checkpoint 与 rollout proof 均通过，旧光标错误为零。
- 放射性矿石证明 `runtime-full-shipment-radioactive-node-20261005-134024` 已通过，精确来源为 `(O)95 -> GameLocation.breakStone -> (O)909`，复用共享采矿/拾取链。
- 防风草重放 `runtime-full-shipment-parsnip-refresh-20261005-192710` 因测试收益低被主动停止；产物保留但不可计入生产索引。本轮不宣称 `3/26`，正式训练仍禁用。
- 远端包装器已取消错误的 `-SkipBuild` 使用。仓库部署脚本同时新增陈旧 DLL 拒绝和部署 SHA-256 校验；Core game-free `212/212`，部署守卫 `3/3`。
- 后续默认只跑变更链路的原生回放和共享离线回归；26 层索引留到分层/日历/训练准入里程碑统一重建。不要再次为无关小改跑完整日历和全部样本。
- 当前权威分母为 26 层，runner 已配置 `24/26` 个入口。晶球掉落之外，已增加 `shop:Carpenter -> (O)388` 的 `sells`；严格生产证据没有因此增长。
- 剩余入口严格为 2 层：奖励物和地点鱼。配置入口完成不等于原生生产证明；训练准入仍为 `false`。

## 树苔藓入口

- 苔藓已绑定 `full_shipment:item:Moss / (O)Moss / native_tree_moss_harvest / foraging.harvest_tree_moss`，复用 `debug.setup_clear_obstacle(tree_moss)`、现有透明苔藓投影、DailyPlan、`executor.clear_obstacle` 与原生镰刀回执。
- 最新异机守卫 `17/17`、Core game-free `138/138`、Bootstrap Release clean。已装配待运行 14 层、未装配 10 层，生产证据仍 `2/26`。

## 最新控制修正

- runner 现为每个样本声明预期 `route_kind`，并在 execution binding 生成后、进入游戏执行前核对 `requirement_id / qualified_item_id / route_kind` 完整三元组；resume 端继续独立复核。
- 同物品多来源不再可能仅因 requirement 相同而串层。远端守卫 `16/16`、Core game-free `137/137`、Bootstrap Release `0 warning / 0 error`。
- 该修正本身没有产生新原生证据；随后新增苔藓入口后，当前十四个待证样本、生产 `2/26`。

## 当前事实

- 生产运行时分层索引严格为 `2/26`：防风草覆盖 `harvests_as`，Sap 覆盖 `native_wild_tree_chop_drop`。
- 浆果入口已绑定 `full_shipment:item:296 / (O)296 / native_bush_shake / foraging.harvest_bushes`。
- 姜入口已绑定 `full_shipment:item:829 / (O)829 / native_ginger_harvest / foraging.harvest_ginger`。
- 茶叶入口已绑定 `full_shipment:item:815 / (O)815 / native_tea_bush_harvest / foraging.harvest_bushes`。
- 榛子入口已绑定 `full_shipment:item:408 / (O)408 / native_wild_tree_seed_drop / foraging.harvest_tree_product`。
- 椰子入口已绑定 `full_shipment:item:88 / (O)88 / native_wild_tree_seed / foraging.harvest_tree_product`。
- 春葱入口已绑定 `full_shipment:item:399 / (O)399 / native_spring_onion_harvest / foraging.harvest_spring_onions`。
- 野山葵入口已绑定 `full_shipment:item:16 / (O)16 / native_location_forage_spawn / foraging.collect_spawned_objects`。
- 樱桃入口已绑定 `full_shipment:item:638 / (O)638 / native_fruit_tree_produce / foraging.harvest_fruit_tree`。
- 牛奶入口已绑定 `full_shipment:item:184 / (O)184 / native_farm_animal_produce / farm.collect_animal_products`。
- 大瓶牛奶入口已绑定 `full_shipment:item:186 / (O)186 / native_farm_animal_deluxe_produce / farm.collect_animal_products`。
- 鱼籽入口已绑定 `full_shipment:item:812 / (O)812 / native_fish_pond_output / fishing.service_fish_ponds`。
- 蘑菇树桩产物入口已绑定 `full_shipment:item:257 / (O)257 / machine_output / farm.collect_machine_outputs`。
- 太阳能板产物入口已绑定 `full_shipment:item:787 / (O)787 / native_solar_panel_output / farm.collect_machine_outputs`。
- 调味机器产物入口已绑定 `full_shipment:item:340 / (O)340 / native_machine_flavored_output / farm.collect_machine_outputs`。
- 普通机器查询产物入口已绑定 `full_shipment:item:257 / (O)257 / native_machine_item_query_output / farm.collect_machine_outputs`。
- 树苔藓入口已绑定 `full_shipment:item:Moss / (O)Moss / native_tree_moss_harvest / foraging.harvest_tree_moss`。
- 蚯蚓地入口已绑定 `full_shipment:item:330 / (O)330 / native_location_artifact_spot / foraging.excavate_artifact_spots`；夹具按透明输出与 `location:Default:10` 来源搜索合法坐标。
- 晶球入口已绑定 `full_shipment:item:386 / (O)386 / native_geode_drop / processing.crack_geode`；夹具搜索原生计数器前态，正式动作通过铁匠柜台和 `GeodeMenu` 完成。
- 商店入口已绑定 `full_shipment:item:388 / (O)388 / sells / economy.buy_supplies`；候选约束 `shop:Carpenter`，夹具只把隔离时间推进到 9:00。
- 前五者、野山葵与樱桃共用 forage fixture 编排，春葱与既有防风草共用 crop fixture 编排；全部复用现有候选、DailyPlan、动作编译器、产品执行器和 verifier，没有第二套动作系统。
- 浆果、姜、茶叶、榛子位于 `Farm 64,15`；椰子使用 `wild_tree / island_palm / IslandSouth 20,20`。夹具设置不属于 acquisition proof 根，但 proof 根必须匹配场景的精确地点。

## 已验证

- `LZT` 隔离测试机上：GoalConditionedBootstrap Release `0 warning / 0 error`。
- acquisition route dispatch 与 Full Shipment 静态可编译性自测通过。
- 最新样本守卫 `17/17`、Core game-free `138/138` 通过。
- 本机仅做轻量解析和 Git 检查，避免再次因大测试卡死。

## 未完成与退出条件

- 异机 `F:\StardewAI-TestLab` 已有可用原生运行环境。除 Sap 与放射性矿石外，其余已配置入口没有当前版本 fresh Full Shipment MonoGame 回执，不得计入覆盖；历史 action smoke 只能证明链路可复用。
- 后续仅按变更链路选择代表样本运行。每层必须同时满足：原生执行 `applied/verified`、fresh 终态一致、精确 execution binding、独立 rollout proof 重建通过、生产 evidence index 接纳。
- 满足一层才把 `2/26` 增加一；在全部 26 层与其余训练门完成前，`formal_product_training_authorized=false`。

## 下一步

入口已经是 `26/26`，旧的“补齐剩余 7 个入口”表述作废。先在异机用 milestone runner 导入并复核 Sap/放射性矿石 anchor，再按树液收集器、怪物掉落、地点鱼、蚯蚓地、晶球、商店、奖励物顺序执行 `high_risk` 批次；全绿后执行 `standard`。每次失败立即停并保留 checkpoint，不自动重试。全部 26 层形成 fresh proof 后统一重建 production evidence index；在此之前正式训练保持关闭。

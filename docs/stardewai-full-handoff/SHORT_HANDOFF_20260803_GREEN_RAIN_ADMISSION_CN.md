# 绿雨灌木训练准入短交接

状态：2026-08-03，EVD-212 切片。

## 已完成

- `foraging.clear_green_rain_bushes` 五门绑定 EVD-212。
- 复用唯一 `clear_green_rain_resource_clump -> executor.break_current_location_resource_clump` 链，没有第二套候选、编译器或执行器。
- 隔离夹具和冒烟脚本可分别指定原版父索引 44/46。
- 隐藏静音 E 盘隔离运行中，两种索引均通过原生斧头生命周期、精确核心掉落和 `+15` 采集经验验证。
- 既有 EVD-187 继续覆盖普通任务和特别订单的原生收取进度 `0/1 -> 1/1`。
- 权威对账为 97 registered / 165 semantic / 0 missing / 85 compiler-bound / 16 five-gate-closed / 15 training-allowlisted / 0 Product Executors。

## 严格边界

只准入当前已加载地图、精确基础 `ResourceClump`、索引 44/46、2x2、透明投影为 ready 的分支。核心 Moss/Fiber/可选 Mossy Seed 由日/存档/锚点局部 RNG 精确重放。秘密纸条依赖全局 RNG，只携带身份、未见数量和概率边界，执行后记录实际增量，不承诺必掉。自定义子类、错误身份、不可达站位、缺斧头和投影漂移继续失败关闭。

Debris 可能在执行器完成验证后被原生近身自动拾取。因此结果权威是执行器完成帧的精确物品多集、经验和灌木移除，不要求稍后快照仍保留 Debris。

## 证据

- `artifacts/runtime-green-rain-resource-clump-smoke/runtime-green-rain-index44-admission-v2-20260803/summary.json`
- `artifacts/runtime-green-rain-resource-clump-smoke/runtime-green-rain-index46-admission-v2-20260803/summary.json`
- EVD-187 普通任务/特别订单既有制品。

## 下一步

继续按权威字典依赖顺序准入下一项已有完整链的模型级候选。正式长期 `policy_decision_trajectory.v2`、manifest/checkpoint、Product Executor 和第三年爷爷 21 分长跑仍未完成，不能把本切片表述为正式全量训练就绪。

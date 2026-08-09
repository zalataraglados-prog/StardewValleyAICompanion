# StardewAI 短交接：作物维护单链 EVD-226

日期：2026-08-09

## 已完成

- `farm.maintain_crops` 只负责从透明状态生成五类当前地点候选：浇水、播种、施肥、普通收获、巨型作物收获。
- 高层 option 不再拥有动作队列直达编译器或运行时分派。唯一链路为候选 -> 日计划 -> 类型化 executor -> 原生执行 -> 新快照/反馈。
- `current_location.crops` 覆盖地形 `HoeDirt` 与 `IndoorPot.hoeDirt`；缺失当前地点字段时失败关闭，不回退读取农场目标。
- 施肥绑定精确槽位、物品身份与 `HoeDirt.CheckApplyFertilizerRules`。普通地块走 `Object.placementAction`，花盆走 `IndoorPot.performObjectDropInAction`，成功后只扣除一次物品。
- 巨型作物复用现有逐帧 `ResourceClump` 原生斧具生命周期，绑定锚点、尺寸、运行时类型、父索引、站位、命中格、工具槽位和最大挥击数；没有直接删对象或同步循环。
- 清障、普通资源块和掉落拾取不再混入作物维护，继续由既有 option 家族负责。

## 验证

- 后台隔离运行通过：浇水 `20260809-125801`、播种 `20260809-130104`、普通收获 `20260809-130150`、普通地块施肥 `20260809-131355`、花盆施肥 `20260809-132247`、巨型作物 `20260809-131443`。
- Core `1575/1575`、Backend `114/114` 通过；RuntimeTestHarness 构建 0 错误；唯一现有警告为 `MiningReadAdapter.Objects.cs` 的 `AvoidNetField`。
- KnowledgeCompiler `585/585`，blocking 0。看板：103 registered、170 semantic、98 compiler-bound、67 runtime dispatch、29 five-gate、25 allowlist。

## 边界

- EVD-226 是执行器校准/评估证据；`farm.maintain_crops` 仍由 `CalibrationOnlyHighLevelIds` 排除在策略训练之外。
- 当前证据不等于跨季种植策略、批处理策略、未来作物承诺或自定义 MOD 类型已验证。
- 后续不得恢复 `farm.maintain_crops` 的高层直达执行，也不得为五类机械原语创建第二套运行时。

## 下一步

从生成对账与权威字典选择下一个尚未闭合的高层 option，继续按 read/candidate/compile/runtime/output 五门纵向推进。正式全量训练仍需其余目标族准入和真实长期 rollout，不能用本次机械校准烟测替代。

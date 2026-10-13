# StardewAI 短交接：Full Shipment 原生姜采收

## 当前状态

- 分支：`codex/admit-native-ginger-harvest`。
- 严格运行时证据：`12/26`，剩余 `14` 层；共享 shipping/settlement 有效，正式训练仍未授权。
- 本轮路线：`full_shipment:full_shipment:item:829:0:0 / full_shipment:item:829 / (O)829 / native_ginger_harvest / Crop.hitWithHoe`。
- 唯一动作链：`foraging.harvest_ginger -> executor.harvest_ginger`。
- 原生证明根：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-ginger-v1-20261013\runtime-full-shipment-evidence-ginger-v1-20261013`。
- 严格索引根：`E:\StardewAI-TestLab\artifacts\runtime-full-shipment-evidence-current-preflight-v18-20261013`。
- 冻结源存档 SHA-256：`5af603dbb99bafadbb14580952b6e4b8c0d9e257963174759f53fbc8b2d671cc`。

## 本轮闭合内容

- 透明桥为精确原版姜地块暴露 `native_ginger_harvest / Crop.hitWithHoe / (O)829` 权威来源；当前候选携带相同来源、格子、工具、距离、时间和能量身份。
- 日历、地点、日内时间/能量、Teacher 和调度编译只接受同一个当前候选。缺少候选或来源、日期、地点、格子、预算漂移均失败关闭。
- 执行器使用原生锄头动作，随后复用现有 BFS/移动原语自然吸取原生姜碎片。没有直接移除 Crop、直接写背包、直接改经验或调用碎片直收 API。
- 原生回执在 `47` tick 内取得 `(O)829 x1`，验证作物移除、HoeDirt 状态、碎片生成、觅食经验 `+7` 和自然拾取，最终为 `applied/verified`。
- evidence index 只在样本完成后构建一次；既有 11 层没有重跑。姜层 proof manifest SHA-256 为 `7996ac887535d20c229894f40a8fa7c34ff856fa6c52d2c53f54db75f47c4ade`，receipt SHA-256 为 `830eb70fabe0cfcac66a2698a5b6f61fd133b78a03fbad7f1024202030d179b2`。

## 下一固定任务

执行 `tea_bush_harvest_sample / full_shipment:item:815 / (O)815 / native_tea_bush_harvest`。复用现有 `foraging.harvest_bushes -> executor.harvest_bush`、共享灌木 fixture、自然碎片拾取和严格回执链，只补茶叶尚缺的当前候选路线轴与正式 Full Shipment proof。

不得复制浆果执行器，不得重跑已通过的 12 层，不得触碰或提交 `local-data/`。性能优化单独切片，遵守 #195/#200/#201/#202/#211/#223 的依赖哈希、失效与单次计算边界。

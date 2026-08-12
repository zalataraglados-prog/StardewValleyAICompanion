# StardewAI 短交接：建筑换肤 EVD-249

## 已完成

- 新增 `player.building_skin_catalog`，full 快照实时枚举建筑身份、权限、原生皮肤顺序、当前/目标索引、最短点击序列、Robin 服务状态和油漆重置副作用。
- 新增策略动作 `buildings.change_skin`，要求模型明确给出建筑地点、类型、坐标、目标皮肤和外观理由；无完整意图不上游候选。
- DailyPlan 与 ActionQueue 只汇入唯一 `executor.change_building_skin`；执行器只发送原生地图/对话/菜单输入，不直接改皮肤或油漆状态。
- fixture 已与生产执行器分文件，源守卫禁止生产文件出现直接 `skinId`、`SetSkin` 或 paint-default 写入。

## 验证

- 隐藏静音 E 盘隔离运行：`artifacts/runtime-quest-terminal-daily-plan/runtime-quest-terminal-daily-plan-20260812-122957/summary.json`。
- Pet Bowl `__default__ -> Stone Pet Bowl`，一次最短 `next`，原生 Robin `Construct -> Paint -> BuildingSkinMenu -> OK`，最终 `applied/verified`。
- full 快照 required 112、blocking 0；KnowledgeCompiler 585/585、blocking 0。
- Core 1662/1662；Backend 121/121。项目仅有既存 `mine.isFarm` 分析警告。

## 范围与下一步

EVD-249 只证明原版 Pet Bowl 默认到 Stone 的直接换肤路径。paintable 建筑、Cabin/Farmhouse 权限、多皮肤条件矩阵、多人和模组不得外推。下一切片是 `buildings.paint`：复用同一 Robin/Carpenter 目标选择与收尾生命周期，增加实时颜色意图、原生 `BuildingPaintMenu` 控件和严格回执，不复制第二套系统。

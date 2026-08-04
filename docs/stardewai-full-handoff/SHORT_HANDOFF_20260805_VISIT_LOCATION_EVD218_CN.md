# `exploration.visit_location` EVD-218 短交接

状态：当前地图单步滚动访问已完成五门准入。没有新增第二套移动、清障或跨图执行器。

## 已完成

- 候选 ID 包含源地图/格、连接器种类、目标地图和可用时的到达格，避免出口去重冲突。
- 未解析连接器、同地图目标、关闭或未知门禁、不可达路径和不支持的 Action 失败关闭。
- 直连候选完整经过 `EventCandidateRanker -> DailyPlanCompiler -> ActionQueueCompiler -> executor.traverse_connector`。
- 路线清障保留既有 `clear_obstacle_tile` 的全部参数，只附加原路线身份、目标地图与刷新策略。
- 每次连接或清障后都必须获取新快照，不允许在旧快照上编译长动作队列。

## 运行证据

`artifacts/runtime-route-connector-smoke/runtime-route-connector-smoke-evd218-pass-20260805/summary.json`

隔离存档从 `FarmHouse` 读出 3 个透明候选，选中 `27,31 -> Farm:64,15`，产生一个计划步和一个
`executor.traverse_connector` 队列项。原生执行 `applied/verified`，after snapshot 新鲜、状态哈希变化，写入 1 条训练行。

## 严格边界

EVD-218 只证明一次当前地图连接器的滚动闭环。EVD-189 证明清障原语，但没有把“清障后跨图”伪装成同一旧队列的
运行证据。任意多连接器终点、未加载地图碰撞、条件门、未知或自定义连接器继续分片验证。

## 下一步

继续按权威字典选择尚未五门准入的高层选项。优先复用已经存在的候选、计划、编译器和原生执行器；先做静态负例和
有界运行矩阵，再登记 evidence/allowlist。不要把通用 `visit_location` 扩展成第二套路由系统。

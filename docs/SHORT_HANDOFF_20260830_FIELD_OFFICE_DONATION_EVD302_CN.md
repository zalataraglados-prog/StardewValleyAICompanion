# StardewAI 短交接：EVD-302 Field Office 化石捐赠

## 已完成

- 唯一语义链为 `island.field_office_donate -> donate_field_office_piece -> executor.donate_field_office_piece`。它复用既有岛屿路线、连接器、碰撞网格、连续移动、库存身份和菜单输入，没有第二套岛屿或库存系统。
- 透明桥实时发布 11 个槽位、原生物品映射/重复顺序、当前背包可捐候选、Desk/Survey 端点、解锁/教授/互斥锁/菜单、四组恢复状态、两项调查、finale、GoldenWalnutsFound 和未领取奖励。
- 候选要求 `confirm_donation=true`。远程候选每次只走一个连接器，并将背包槽、QualifiedItemId 和目标 piece index 写入 continuation；终端成功前不得改捐另一个槽。
- fresh 编译器重绑站位、背包栈、piece count、集合完成、奖励前后 JSON、collected-nut key 和 finale readiness。任一状态漂移都以 `field_office_donation_projection_drifted` 失败关闭。
- 生产执行只调用原生 `FieldOfficeDesk`、`Safari_Donate`、`FieldOfficeMenu.receiveLeftClick` 和 OK/对话退出。直接状态构造只存在于 `debug.setup_field_office_donation` 隔离夹具。

## 证据

- 反编译锁：`totalPieces=11`；`(O)823` 按槽 0 后 2，`(O)826` 按槽 7 后 6；中心、蛇、蝙蝠、青蛙奖励及 130 核桃替代分支完整。
- 最终隐藏静音 E 盘矩阵：`artifacts/runtime-field-office-donation-smoke/runtime-field-office-donation-smoke-20260830-063003/summary.json`，`15/15 applied/verified`。
- Core `2049/2049`、Backend `148/148`、Release `0 warnings / 0 errors`。
- full snapshot schema `148/132/16/0`；KnowledgeCompiler `585/585`、blocking `0`。
- 对账 `184 registered / 208 semantic / 183 compiler-bound / 107 five-gate / 48 allowlist / 24 catalogued blocked / 0 Product Executor`；原生分母 `322 surfaces / 448 branches / 150 map tokens` 不变。

## 下一步

- 下一切片是独立 `island.field_office_survey`。当前透明桥已经提供 Survey action tiles、`plantsRestoredLeft/Right`、`hasFailedSurveyToday`、下一题和固定答案 22/18、finale readiness。
- 需要补候选、DailyPlan、fresh 编译、类型化请求和原生调查对话矩阵，覆盖正确答案、错误答案当日锁、两题顺序、奖励/finale 和次日重置。复用通用对话原语，但不得把调查塞入捐赠执行器或借用 EVD-302 的运行证据。
- `island.field_office_donate` 五门已闭合但当前未进训练 allowlist；不要把 Harness 证据称为 Product Executor。

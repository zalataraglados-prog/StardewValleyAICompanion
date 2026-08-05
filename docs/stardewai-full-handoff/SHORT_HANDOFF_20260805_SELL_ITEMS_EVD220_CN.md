# StardewAI 短交接：商店出售 EVD-220

日期：2026-08-05

## 本轮完成

- `economy.sell_items` 已接入生产用滚动闭环：全局商店出售预览 -> 逐段路由 -> 柜台交互 -> 白名单对白 -> 精确原生出售 -> 菜单清理。
- continuation 固定 `shop_id + qualified_item_id + slot_index + quantity + expected_unit_price`，每次动作后重新读取快照；只有匹配的 `executor.sell_shop_item` 为 `applied/verified` 才完成目标。
- `ShopData.SalableItemTags` 进入透明桥，候选层在开菜单前即可排除不接收该物品、当前关门或店主不在柜台的商店。
- 商店出售与运输箱出售已分离；本轮没有启用 `economy.ship_items`。
- 默认保护字段仍是硬门。战略余量、任务保留量和用户偏好由上游策略授权，不由执行器猜测；`economy.sell_items` 仍非 autonomous。

## 运行证据

- EVD-220：`artifacts/runtime-sale-mainline-smoke/runtime-sale-mainline-20260805-124924/summary.json`
- 隔离 E 盘、静默后台通过。Blacksmith 接收 `(O)378` 铜矿 3 个，实时单价 5g；精确出售、金额/库存反馈、菜单关闭、continuation 清理和 7 条训练记录均通过。
- 反编译确认 `ShopMenu.readyToClose()` 等待 `animations.Count == 0`，而动画只在 `draw()` 更新。执行器先给原生绘制留出有界时间，随后仅在 `heldItem == null && safetyTimer <= 0` 时跳过纯视觉动画阻塞并调用菜单原生清理。

## 下一步

下一模块是 `economy.ship_items`。它必须保持独立语义：透明运输箱选择和路线、精确物品/数量授权、原生投入箱、当天库存变化、次日结算金额及出售记录对账。不能复用商店即时到账作为完成条件，也不能把商店出售与运输候选重新合并。

完成标志：独立 continuation、候选/编译/运行/输出五门证据全部通过；隐藏隔离运行至少覆盖原生投递和跨日结算；训练准入范围明确排除自动清仓与未授权战略库存。

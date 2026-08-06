# StardewAI 短交接：运输箱出售 EVD-221

日期：2026-08-07

## 已完成

- `economy.ship_items` 已接入生产 rolling 链：异地图时只走一个透明 connector；Farm 内只走到精确箱体交互位；到位后只执行一次原生投递。每一步后必须重新读取快照。
- continuation 固定 `qualified_item_id + slot_index + quantity=1 + expected_unit_price + bin_location/bin_tile + stand_tile`。只有完全匹配的 `executor.ship_inventory_item_to_bin` 为 `applied/verified` 才结束。
- 原生价格语义已纠正：本地 1.6.15 反编译证明日终运输按 `sellToStorePrice(-1L)` 计价，随后调用 `Farmer.shippedBasic`。`salePrice()` 只保留为审计字段。
- 修复了 LiveTrainingLoop 在终端动作上首次发现 continuation 时可能重复执行的问题：现在同一 terminal item 会立即完成，不会下一轮再次投递。

## 运行证据

- 高层闭环：`artifacts/runtime-shipping-mainline-smoke/runtime-shipping-mainline-20260807-012414/summary.json`。
  只提交 `economy.ship_items`，得到 `move_to_tile -> ship_inventory_item_to_bin`；木材 `(O)388` 5 -> 4，箱内 0 -> 1，continuation 清空，写入 2 条真实执行记录。
- 原语与跨日：`artifacts/runtime-ship-inventory-smoke/runtime-ship-inventory-smoke-20260807-011702/summary.json`。
  原生投递后原生睡眠跨日，pending receipt 按 `basicShipped` 精确结算为 `completed`。

## 严格边界

该准入只允许策略明确选择的一件未保护、正收益物品。它不自行推断余量，不自动清仓，不批量运输，也不覆盖 Mini-Shipping Bin 或自定义运输逻辑。日/季/年训练标签仍由真实跨度观测闭合；pending receipt 是逐物品结算审计，不替代长期回报标签。

## 接续

先运行能力对账和全量回归，确认 EVD-221 五门及 allowlist 计数落盘。下一能力必须从更新后的动作对账表选择，不能凭旧 TODO 猜测。

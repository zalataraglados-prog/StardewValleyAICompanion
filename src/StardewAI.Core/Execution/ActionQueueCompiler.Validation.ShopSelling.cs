using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static string[] ValidateSellShopItemPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.sell_shop_item")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            if (!ActionSeesShopMenuOpen(action, snapshot))
            {
                reasons.Add("shop_menu_not_open");
            }

            var slotIndex = ReadIntParameter(action, "slot_index");
            var quantity = ReadIntParameter(action, "quantity");
            var expectedUnitPrice = ReadIntParameter(action, "expected_unit_price");
            var expectedTotalValue = ReadIntParameter(action, "expected_total_value");
            var qualifiedItemId = ReadParameter(action, "qualified_item_id");
            if (!slotIndex.HasValue || slotIndex.Value < 0) reasons.Add("sell_slot_index_required");
            if (!quantity.HasValue || quantity.Value <= 0) reasons.Add("sell_quantity_positive_required");
            if (!expectedUnitPrice.HasValue || expectedUnitPrice.Value <= 0) reasons.Add("sell_expected_unit_price_positive_required");
            if (string.IsNullOrWhiteSpace(qualifiedItemId)) reasons.Add("sell_qualified_item_id_required");

            var sellContext = ReadStateFieldValue(snapshot, "menus", "sell_context");
            if (!sellContext.HasValue || sellContext.Value.ValueKind != JsonValueKind.Object)
            {
                reasons.Add("menus_sell_context_unavailable");
                return reasons.Distinct(StringComparer.Ordinal).ToArray();
            }

            var context = sellContext.Value;
            if (ReadBool(context, "read_only") != false) reasons.Add("shop_menu_read_only_or_unknown");
            if (ReadBool(context, "held_item_present") != false) reasons.Add("shop_menu_held_item_present_or_unknown");
            if (ReadInt(context, "safety_timer") > 0) reasons.Add("shop_menu_safety_timer_active");
            if (ReadInt(context, "currency") != 0) reasons.Add("non_money_shop_sale_requires_audit");
            if (ReadBool(context, "custom_on_sell_present") != false) reasons.Add("shop_custom_on_sell_requires_audit");
            if (ReadBool(context, "storage_shop") != false) reasons.Add("storage_shop_sale_requires_audit");
            var sellPercentage = ReadDouble(context, "sell_percentage");
            if (sellPercentage <= 0d) reasons.Add("shop_sell_percentage_missing_or_non_positive");

            var expectedShopId = ReadParameter(action, "expected_shop_id");
            if (string.IsNullOrWhiteSpace(expectedShopId) ||
                !string.Equals(expectedShopId, ReadString(context, "shop_id"), StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("shop_menu_id_mismatch");
            }

            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            var item = inventory.HasValue && inventory.Value.ValueKind == JsonValueKind.Array && slotIndex.HasValue
                ? inventory.Value.EnumerateArray().FirstOrDefault(row =>
                    row.ValueKind == JsonValueKind.Object &&
                    ReadInt(row, "slot_index") == slotIndex.Value)
                : default;
            if (item.ValueKind != JsonValueKind.Object)
            {
                reasons.Add("sell_inventory_slot_not_found");
                return reasons.Distinct(StringComparer.Ordinal).ToArray();
            }

            if (!string.Equals(ReadString(item, "qualified_item_id"), qualifiedItemId, StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrWhiteSpace(ReadParameter(action, "item_id")) &&
                !string.Equals(ReadString(item, "item_id"), ReadParameter(action, "item_id"), StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("sell_inventory_item_identity_drift");
            }
            if (quantity.HasValue && ReadInt(item, "stack") != quantity.Value)
            {
                reasons.Add("sell_inventory_stack_drift");
            }
            if (ReadBool(item, "protected_from_auto_sell") == true ||
                item.TryGetProperty("auto_sell_protection_reasons", out var protectionReasons) &&
                protectionReasons.ValueKind == JsonValueKind.Array &&
                protectionReasons.GetArrayLength() > 0)
            {
                reasons.Add("inventory_item_protected_from_auto_sell");
            }

            var actualUnitPrice = (int)(ReadInt(item, "sell_to_store_price") * sellPercentage);
            if (expectedUnitPrice.HasValue && actualUnitPrice != expectedUnitPrice.Value)
            {
                reasons.Add("sell_unit_price_drift");
            }
            if (expectedTotalValue.HasValue && quantity.HasValue &&
                expectedTotalValue.Value != actualUnitPrice * quantity.Value)
            {
                reasons.Add("sell_total_value_drift");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
    }
}

using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    internal static void VerifyShippingStock(SnapshotEnvelope before, SnapshotEnvelope after,
        NativeShippingDepositEvidence deposit)
    {
        var (first, next, priorPlayer, player) = ReadStockGraphs(before, after);
        var index = deposit.SlotIndex!.Value;
        Require(priorPlayer.Capacity is > 0 and <= 512 && player.Capacity == priorPlayer.Capacity && index < player.Capacity,
            "Shipping changed player capacity or selected an inactive slot.");
        var expected = Field(before, "player", "inventory")!;
        var actual = Field(after, "player", "inventory")!;
        var rows = Rows(Value(expected)); var current = Rows(Value(actual));
        Require(rows.Keys.Order().SequenceEqual(current.Keys.Order()) && rows.ContainsKey(index),
            "Shipping changed raw inventory slot identities.");
        var source = rows[index];
        Require(source["is_empty"]?.GetValue<bool>() == false &&
                source["qualified_item_id"]?.GetValue<string>() == deposit.QualifiedItemId &&
                Int(source, "quality") == deposit.Quality && Int(source, "stack") == deposit.BeforeInventorySlotQuantity &&
                Int(source, "sell_to_store_price") == deposit.UnitPrice,
            "Shipping raw source differs from the native evidence.");
        var count = checked(Int(source, "stack") - 1);
        Require(count >= 0, "Shipping raw source was exhausted.");
        if (count > 0) source["stack"] = count;
        else ProjectShippingEmptyRawSlot(source);
        foreach (var (slot, row) in rows)
            Require(JsonNode.DeepEquals(row, current[slot]), "Shipping changed raw item metadata or another inventory slot.");
        expected["value"] = Value(actual).DeepClone();
        EqualField(expected, actual, "shipping raw inventory envelope");

        var graphEnvelope = Field(before, "farm", "material_inventory_graph")!;
        var graph = Value(graphEnvelope).AsObject();
        var slots = Array(Unique(Array(graph["inventory_nodes"], "shipping graph nodes"), "node_id", priorPlayer.NodeId)["slots"], "shipping player slots");
        var graphSlot = Slot(slots, index);
        Require(graphSlot["qualified_item_id"]?.GetValue<string>() == deposit.QualifiedItemId &&
                Int(graphSlot, "quality") == deposit.Quality && Int(graphSlot, "stack") == count + 1,
            "Shipping material source differs from the native evidence.");
        // Compare the observed raw/material item mirrors before projecting the sole decrement.
        var rawSource = Rows(Value(Field(before, "player", "inventory")!))[index];
        foreach (var name in new[] { "item_id", "qualified_item_id", "display_name", "runtime_type", "context_tags", "stack", "maximum_stack_size", "quality", "sale_price" })
            Require(rawSource.ContainsKey(name) && rawSource[name] is not null && JsonNode.DeepEquals(rawSource[name], graphSlot[name]),
                "Shipping material source metadata differs from raw inventory: " + name);
        if (count == 0) slots.Remove(graphSlot);
        else graphSlot["stack"] = count;

        var quantities = Array(graph["quantity_rows"], "shipping quantities");
        VerifyShippingQuantities(first, quantities);
        var quantity = quantities.OfType<JsonObject>().Single(row =>
            row["qualified_item_id"]?.GetValue<string>() == deposit.QualifiedItemId && Int(row, "quality") == deposit.Quality);
        quantity["available_quantity"] = checked(Int(quantity, "available_quantity") - 1);
        if (count == 0) quantity["source_slot_count"] = checked(Int(quantity, "source_slot_count") - 1);
        if (Int(quantity, "source_slot_count") == 0) quantities.Remove(quantity);
        // Unlike the addition-only helper, final-unit removal also removes its empty aggregate.
        VerifyShippingQuantities(next, quantities);
        EqualField(graphEnvelope, Field(after, "farm", "material_inventory_graph"),
            "shipping material graph, foreign custody or unknown metadata");
        VerifyCapacity(before, after, priorPlayer, player);
        foreach (var name in new[] { "money", "shop_currency_balances", "multiplayer_wallet", "seed_inventory" })
            SameField(before, after, "player", name, required: name is "money" or "shop_currency_balances");
    }

    private static void VerifyShippingQuantities(MaterialInventoryGraph graph, JsonArray rows)
    {
        var keys = graph.InventoryNodes.SelectMany(node => node.Slots).Where(slot => slot.Stack > 0)
            .Select(slot => (slot.QualifiedItemId, slot.Quality)).Distinct().ToHashSet();
        Require(rows.All(row => row is JsonObject) && rows.Count == keys.Count,
            "Shipping material aggregate identities are incomplete or duplicated.");
        foreach (var row in rows.OfType<JsonObject>())
        {
            var id = row["qualified_item_id"]?.GetValue<string>() ?? throw new InvalidDataException("Shipping aggregate item missing.");
            var quality = Int(row, "quality");
            Require(keys.Remove((id, quality)), "Shipping aggregate identity differs from native stock.");
            VerifySourceQuantity(row, graph, id, quality);
        }
    }

    private static void ProjectShippingEmptyRawSlot(JsonObject slot)
    {
        // PlayerReadAdapter.Collect emits a retained index with nullable item fields for a null Item.
        // Graph slots/quantity rows, in contrast, omit that removed physical item.
        foreach (var name in new[] { "item_id", "qualified_item_id", "display_name", "stack", "quality", "category",
                     "maximum_stack_size", "can_be_shipped", "can_be_trashed", "sell_to_store_price", "sale_price",
                     "runtime_type", "special_state", "special_item", "context_tags", "base_tag_not_giftable",
                     "object_type", "object_quest_item", "object_big_craftable", "can_be_given_as_gift" })
            if (slot.ContainsKey(name)) slot[name] = null;
        foreach (var name in new[] { "is_object", "is_furniture", "is_wallpaper", "protected_from_auto_sell" })
            if (slot.ContainsKey(name)) slot[name] = false;
        if (slot.ContainsKey("auto_sell_protection_reasons")) slot["auto_sell_protection_reasons"] = new JsonArray();
        if (slot.ContainsKey("donate_color_context")) slot["donate_color_context"] = new JsonObject
        {
            ["is_colored_object"] = false, ["preserved_parent_item_id"] = null,
            ["preserved_parent_base_context_tags"] = new JsonArray(), ["projection_status"] = "not_applicable_not_colored_object"
        };
        slot["is_empty"] = true;
    }
}

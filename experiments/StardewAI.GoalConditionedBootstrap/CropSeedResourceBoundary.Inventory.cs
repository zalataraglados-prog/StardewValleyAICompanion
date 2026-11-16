using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    private static void ProjectEmptyPlayerInventorySlot(JsonObject slot)
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

    private static void VerifyInventory(SnapshotEnvelope before, SnapshotEnvelope after, MaterialInventoryNode player,
        JsonObject seed, MaterialTransferProjection physical)
    {
        var expected = Field(before, "player", "inventory")!;
        var actual = Field(after, "player", "inventory")!;
        var first = Rows(Value(expected));
        var next = Rows(Value(actual));
        var added = physical.DestinationSlotChanges.Where(row => row.StackBefore == 0)
            .Select(row => row.SlotIndex).ToHashSet();
        Require(next.Keys.Order().SequenceEqual(first.Keys.Union(added).Order()),
            "Owned seed transfer changed unrelated player inventory slots.");
        foreach (var (index, row) in first)
        {
            if (added.Contains(index))
            {
                Require(row["is_empty"]?.GetValue<bool>() == true, "Owned seed transfer overwrote an occupied raw slot.");
                continue;
            }
            var projected = player.Slots.SingleOrDefault(slot => slot.SlotIndex == index);
            if (projected is not null) row["stack"] = projected.Stack;
            Require(JsonNode.DeepEquals(row, next[index]), "Owned seed transfer changed existing player item metadata.");
        }
        foreach (var index in added)
        {
            foreach (var property in seed.Where(row => row.Key is not "slot_index" and not "stack"))
                if (next[index].ContainsKey(property.Key))
                    Require(JsonNode.DeepEquals(property.Value, next[index][property.Key]),
                        "Owned seed transfer new player item differs from its native source: " + property.Key);
        }
        // New item metadata is observed, not invented from fields absent in the chest graph.
        expected["value"] = Value(actual).DeepClone();
        EqualField(expected, actual, "player inventory envelope");
    }

    private static void VerifyCapacity(SnapshotEnvelope before, SnapshotEnvelope after,
        MaterialInventoryNode first, MaterialInventoryNode next)
    {
        var expected = Field(before, "player", "inventory_capacity", required: false);
        var actual = Field(after, "player", "inventory_capacity", required: false);
        if (!Readable(expected) || !Readable(actual)) { EqualField(expected, actual, "inventory capacity availability"); return; }
        var value = Value(expected!).AsObject();
        var maximum = Int(value, "max_items");
        var occupied = first.Slots.Count(row => row.SlotIndex < maximum);
        Require(maximum == first.Capacity && next.Capacity == maximum && Int(value, "occupied_item_stacks") == occupied &&
                Int(value, "empty_slots") == Math.Max(0, maximum - occupied) &&
                value["has_empty_slot"]?.GetValue<bool>() == (occupied < maximum),
            "Owned seed transfer source player capacity is inconsistent.");
        occupied = next.Slots.Count(row => row.SlotIndex < maximum);
        value["occupied_item_stacks"] = occupied;
        value["empty_slots"] = Math.Max(0, maximum - occupied);
        value["has_empty_slot"] = occupied < maximum;
        EqualField(expected, actual, "player inventory capacity");
    }

    private static void VerifySeedInventory(SnapshotEnvelope before, SnapshotEnvelope after, MaterialInventoryNode priorPlayer, MaterialInventoryNode player,
        string seedId, string resolvedSeedId)
    {
        var expected = Field(before, "player", "seed_inventory", required: false);
        var actual = Field(after, "player", "seed_inventory", required: false);
        if (!Readable(expected) || !Readable(actual)) { EqualField(expected, actual, "seed inventory availability"); return; }
        var first = Rows(Value(expected!));
        var next = Rows(Value(actual!));
        VerifySeedRows(before, first, priorPlayer, seedId, resolvedSeedId);
        VerifySeedRows(after, next, player, seedId, resolvedSeedId);
        foreach (var (index, row) in first)
        {
            Require(next.ContainsKey(index), "Owned seed transfer lost an existing seed inventory row.");
            if (row["qualified_item_id"]?.GetValue<string>() == seedId) row["stack"] = Int(next[index], "stack");
            Require(JsonNode.DeepEquals(row, next[index]), "Owned seed transfer changed existing seed metadata.");
        }
        Require(next.Where(row => !first.ContainsKey(row.Key)).All(row => row.Value["qualified_item_id"]?.GetValue<string>() == seedId) &&
                player.Slots.Where(row => row.QualifiedItemId == seedId).All(row => next.ContainsKey(row.SlotIndex)),
            "Owned seed transfer seed echo omitted or invented a slot.");
        expected!["value"] = Value(actual!).DeepClone();
        EqualField(expected, actual, "seed inventory envelope");
    }

    private static void VerifySeedRows(SnapshotEnvelope snapshot, Dictionary<int, JsonObject> seeds, MaterialInventoryNode player,
        string seedId, string resolvedSeedId)
    {
        var inventory = Rows(Value(Field(snapshot, "player", "inventory")!));
        foreach (var (index, row) in seeds)
        {
            var slot = player.Slots.SingleOrDefault(slot => slot.SlotIndex == index);
            Require(slot is not null && row["qualified_item_id"]?.GetValue<string>() == slot.QualifiedItemId &&
                    Int(row, "stack") == slot.Stack && Int(row, "quality") == slot.Quality,
                "Owned seed transfer seed inventory disagrees with native slots.");
            if (row["qualified_item_id"]?.GetValue<string>() == seedId)
                Require(row["seed_id"]?.GetValue<string>() == resolvedSeedId && row["crop_catalog_match"]?.GetValue<bool>() == true,
                    "Owned seed transfer seed echo differs from the original crop catalog commitment.");
            foreach (var property in row.Where(row => inventory[index].ContainsKey(row.Key)))
                Require(JsonNode.DeepEquals(property.Value, inventory[index][property.Key]),
                    "Owned seed transfer seed metadata disagrees with player inventory: " + property.Key);
        }
        Require(player.Slots.Where(row => row.QualifiedItemId == seedId).All(row => seeds.ContainsKey(row.SlotIndex)),
            "Owned seed transfer source/after seed echo omitted a native slot.");
    }

    private static Dictionary<int, JsonObject> Rows(JsonNode value)
    {
        var array = Array(value, "inventory rows");
        Require(array.All(row => row is JsonObject) && array.OfType<JsonObject>().All(row => Int(row, "slot_index") >= 0) &&
                array.OfType<JsonObject>().Select(row => Int(row, "slot_index")).Distinct().Count() == array.Count,
            "Owned seed transfer inventory slot identities are invalid.");
        return array.OfType<JsonObject>().ToDictionary(row => Int(row, "slot_index"));
    }
}

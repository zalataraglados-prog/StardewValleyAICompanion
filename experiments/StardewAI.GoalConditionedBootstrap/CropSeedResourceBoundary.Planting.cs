using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    internal static void VerifyPlanting(SnapshotEnvelope before, SnapshotEnvelope after,
        TrainingExecutionRequest inventory, string nodeId, CropPlantingCommitment crop, string location, int x, int y)
    {
        Require(CropSeedPlantingInventoryContract.ValidShape(inventory), "Owned planting inventory contract is invalid.");
        Require(MaterialTransferIntentBinder.TryReadGraph(before, out var first) &&
                MaterialTransferIntentBinder.TryReadGraph(after, out _), "Owned planting material graph is unavailable.");
        var player = first!.InventoryNodes.Single(row => row.NodeId == nodeId);
        Require(player.InventoryKind == "player_inventory" && player.ActorUseAuthorized && player.SupplyState == "available",
            "Owned planting selected inventory is not authorized player custody.");
        CropTeacherOwnedSeedSupply.VerifyPlayerSlots(before, player);
        var index = inventory.InventorySlotIndex!.Value;
        var slot = player.Slots.Single(row => row.SlotIndex == index);
        Require(CropSeedPlantingInventoryContract.MatchesNativeSlot(inventory, index, slot.QualifiedItemId,
                slot.ItemId, crop.SeedId, slot.Quality, slot.Stack), "Owned planting source native slot drifted.");
        var graphEnvelope = Field(before, "farm", "material_inventory_graph")!;
        var graph = Value(graphEnvelope).AsObject();
        var rawNode = Unique(Array(graph["inventory_nodes"], "inventory nodes"), "node_id", nodeId);
        var rawSlots = Array(rawNode["slots"], "player slots");
        var rawSlot = Slot(rawSlots, index);
        var remaining = inventory.ExpectedStackAfter!.Value;
        if (remaining == 0) rawSlots.Remove(rawSlot); else rawSlot["stack"] = remaining;
        var quantities = Array(graph["quantity_rows"], "quantity rows").OfType<JsonObject>().Where(row =>
            row["qualified_item_id"]?.GetValue<string>() == slot.QualifiedItemId && Int(row, "quality") == slot.Quality).ToArray();
        Require(quantities.Length == 1, "Owned planting material aggregate is not unique.");
        var quantity = quantities[0];
        var nativeCount = VerifySourceQuantity(quantity, first, slot.QualifiedItemId, slot.Quality);
        quantity["available_quantity"] = checked(Int(quantity, "available_quantity") - 1);
        quantity["source_slot_count"] = nativeCount - (remaining == 0 ? 1 : 0);
        if (nativeCount == 1 && remaining == 0) Array(graph["quantity_rows"], "quantity rows").Remove(quantity);
        EqualField(graphEnvelope, Field(after, "farm", "material_inventory_graph"), "owned planting material graph");
        var nextPlayer = JsonSerializer.Deserialize<MaterialInventoryNode>(rawNode.ToJsonString(), JsonDefaults.Options)!;
        CropTeacherOwnedSeedSupply.VerifyPlayerSlots(after, nextPlayer);
        VerifyConsumedInventory(before, after, index, remaining);
        VerifyCapacity(before, after, player, nextPlayer);
        VerifyConsumedSeedEcho(before, after, player, nextPlayer, index, slot.QualifiedItemId, crop.SeedId, remaining);
        foreach (var name in new[] { "money", "shop_currency_balances", "multiplayer_wallet" })
            SameField(before, after, "player", name, required: name != "multiplayer_wallet");
        MoneyRevenueCropNativeLifecycleProofBuilder.ValidateOwnedPlanting(before, after, location, x, y, crop);
        VerifyPlantedCropRows(before, after, location, x, y);
        VerifyPlantingPersistentState(before, after);
    }

    private static void VerifyConsumedInventory(SnapshotEnvelope before, SnapshotEnvelope after, int index, int remaining)
    {
        var expected = Field(before, "player", "inventory")!;
        var rows = Rows(Value(expected));
        var row = rows[index];
        if (remaining > 0) row["stack"] = remaining;
        else
        {
            // Exact null-item shape published by PlayerReadAdapter; unknown metadata remains guarded.
            foreach (var key in row.Select(pair => pair.Key).ToArray())
                if (key is "is_object" or "is_furniture" or "is_wallpaper" or "protected_from_auto_sell") row[key] = false;
                else if (key == "is_empty") row[key] = true;
                else if (key == "auto_sell_protection_reasons") row[key] = new JsonArray();
                else if (key is "item_id" or "qualified_item_id" or "display_name" or "stack" or "quality" or "category" or
                    "maximum_stack_size" or "can_be_shipped" or "can_be_trashed" or "sell_to_store_price" or "sale_price" or
                    "runtime_type" or "special_state" or "special_item" or "context_tags" or "donate_color_context" or
                    "base_tag_not_giftable" or "object_type" or "object_quest_item" or "object_big_craftable" or "can_be_given_as_gift") row[key] = null;
        }
        EqualField(expected, Field(after, "player", "inventory"), "owned planting exact inventory/no compaction");
    }

    private static void VerifyConsumedSeedEcho(SnapshotEnvelope before, SnapshotEnvelope after, MaterialInventoryNode player,
        MaterialInventoryNode nextPlayer, int index, string seedId, string resolvedId, int remaining)
    {
        var expected = Field(before, "player", "seed_inventory")!;
        var actual = Field(after, "player", "seed_inventory")!;
        VerifySeedRows(before, Rows(Value(expected)), player, seedId, resolvedId);
        VerifySeedRows(after, Rows(Value(actual)), nextPlayer, seedId, resolvedId);
        var rows = Array(Value(expected), "seed inventory");
        var selected = Slot(rows, index);
        if (remaining == 0) rows.Remove(selected); else selected["stack"] = remaining;
        EqualField(expected, actual, "owned planting seed inventory");
    }

    private static void VerifyPlantedCropRows(SnapshotEnvelope before, SnapshotEnvelope after, string location, int x, int y)
    {
        var expected = Field(before, "farm", "crops")!;
        var actual = Field(after, "farm", "crops")!;
        var original = Array(Value(expected), "crops");
        var current = Array(Value(actual), "crops");
        bool Target(JsonNode? row) => row is JsonObject value && value["location_id"]?.GetValue<string>() == location &&
            Int(value, "tile_x") == x && Int(value, "tile_y") == y;
        Require(!original.Any(Target) && current.Count == original.Count + 1 && current.Count(Target) == 1,
            "Owned planting crop delta differs from one exact new target.");
        var retained = current.Where(row => !Target(row)).ToArray();
        Require(original.Zip(retained).All(pair => JsonNode.DeepEquals(pair.First, pair.Second)),
            "Owned planting changed another crop or prior crop metadata.");
        expected["value"] = current.DeepClone();
        EqualField(expected, actual, "owned planting crop envelope");
    }
}

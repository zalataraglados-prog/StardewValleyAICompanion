using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    internal static void VerifyObservedPickup(SnapshotEnvelope before, SnapshotEnvelope after, NativePickupInventorySummary pickup) =>
        VerifyObservedPickupCore(before, after, pickup, null);

    private static void VerifyObservedPickupCore(SnapshotEnvelope before, SnapshotEnvelope after,
        NativePickupInventorySummary pickup, SnapshotEnvelope? verifiedInvariantView)
    {
        var (priorPlayer, player) = VerifyPickupGraph(before, after, pickup);
        var inventoryEnvelope = Field(before, "player", "inventory")!;
        inventoryEnvelope["value"] = Value(Field(after, "player", "inventory")!).DeepClone();
        EqualField(inventoryEnvelope, Field(after, "player", "inventory"), "pickup inventory envelope");
        VerifyCapacity(before, after, priorPlayer, player);
        foreach (var name in new[] { "money", "shop_currency_balances", "multiplayer_wallet", "seed_inventory" })
            SameField(before, after, "player", name, required: name is "money" or "shop_currency_balances");
        var house = VerifyPickupHouseMirror(before, after);
        var comparison = verifiedInvariantView ?? after;
        var view = new SnapshotEnvelope { SaveId = after.SaveId, PlayerId = after.PlayerId, GameVersion = after.GameVersion,
            State = new Dictionary<string, JsonElement>(comparison.State, StringComparer.Ordinal) };
        if (house is not null)
        {
            // Only a comparison view restores the independently verified inventory-count echo.
            var world = JsonNode.Parse(comparison.State["world_progress"].GetRawText())!.AsObject();
            world["marriage_house"] = house;
            view.State["world_progress"] = JsonSerializer.SerializeToElement(world);
        }
        Require(FullShipmentRecoverySettlementBoundaryProjector.TryProjectExpectedResourceMutationInvariant(before, out var first) &&
                FullShipmentRecoverySettlementBoundaryProjector.TryProjectExpectedResourceMutationInvariant(view, out var next) && first == next,
            "Pickup changed unrelated persistent state.");
    }

    private static JsonObject? VerifyPickupHouseMirror(SnapshotEnvelope before, SnapshotEnvelope after)
    {
        var expected = Field(before, "world_progress", "marriage_house", required: false);
        var actual = Field(after, "world_progress", "marriage_house", required: false);
        if (!Readable(expected) || !Readable(actual))
        {
            EqualField(expected, actual, "pickup house mirror availability");
            return null;
        }
        var house = Value(expected!).AsObject();
        var upgrade = house["house_upgrade"] as JsonObject;
        if (upgrade is not null)
        {
            var id = upgrade["required_item_id"]?.GetValue<string>() ?? throw new InvalidDataException("Pickup house required item is missing.");
            int Count(SnapshotEnvelope snapshot) => id.Length == 0 ? 0 : checked((int)Rows(Value(Field(snapshot, "player", "inventory")!))
                .Values.Where(row => row["is_empty"]?.GetValue<bool>() != true && row["qualified_item_id"]?.GetValue<string>() == id)
                .Sum(row => (long)Int(row, "stack")));
            Require(Int(upgrade, "inventory_item_count") == Count(before), "Pickup original house inventory mirror is inconsistent.");
            var projectsStatus = upgrade.ContainsKey("action_status");
            if (projectsStatus)
                Require(upgrade["action_status"]?.GetValue<string>() == PickupHouseActionStatus(house, upgrade, before),
                    "Pickup original house action-status mirror is inconsistent.");
            upgrade["inventory_item_count"] = Count(after);
            if (projectsStatus) upgrade["action_status"] = PickupHouseActionStatus(house, upgrade, after);
        }
        EqualField(expected, actual, "pickup house inventory mirror");
        return Field(before, "world_progress", "marriage_house")!;
    }

    // Mirrors ProgressReadAdapter.MarriageHouse's ordered native prerequisites;
    // no arbitrary replacement status is accepted when stock crosses the threshold.
    private static string PickupHouseActionStatus(JsonObject house, JsonObject upgrade, SnapshotEnvelope snapshot)
    {
        bool Flag(JsonNode value, string name) => value[name] is JsonValue flag && flag.TryGetValue<bool>(out var result)
            ? result : throw new InvalidDataException("Pickup house boolean is unavailable: " + name);
        Require(Int(house, "money") == Value(Field(snapshot, "player", "money")!).GetValue<int>(),
            "Pickup house money mirror differs from raw player money.");
        if (Int(house, "days_until_farmhouse_upgrade") >= 0) return "farmhouse_upgrade_already_in_progress";
        if (Flag(house, "building_under_construction")) return "another_building_under_construction";
        if (Int(house, "money") < Int(upgrade, "price")) return "insufficient_money";
        if (Int(upgrade, "inventory_item_count") < Int(upgrade, "required_item_count")) return "insufficient_required_material";
        if (!Flag(house, "is_current_location")) return "science_house_not_current_location";
        Require(house.ContainsKey("carpenter_action_tile_x") && house.ContainsKey("carpenter_action_tile_y"),
            "Pickup carpenter action-tile mirror is unavailable.");
        if (house["carpenter_action_tile_x"] is null || house["carpenter_action_tile_y"] is null)
            return "carpenter_action_tile_unavailable";
        if (!Flag(house, "robin_present_at_counter")) return "robin_not_present_at_counter";
        var menu = Value(Field(snapshot, "player", "active_menu")!).GetValue<string>();
        var timing = Value(Field(snapshot, "player", "movement_timing_context")!);
        return menu != "none" || Flag(timing, "dialogue_up") ? "carpenter_menu_or_dialogue_not_clear" : "ready";
    }
}

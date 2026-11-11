using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    internal static void VerifyObservedPickup(SnapshotEnvelope before, SnapshotEnvelope after, NativePickupInventorySummary pickup)
    {
        var (priorPlayer, player) = VerifyPickupGraph(before, after, pickup);
        var inventoryEnvelope = Field(before, "player", "inventory")!;
        inventoryEnvelope["value"] = Value(Field(after, "player", "inventory")!).DeepClone();
        EqualField(inventoryEnvelope, Field(after, "player", "inventory"), "pickup inventory envelope");
        VerifyCapacity(before, after, priorPlayer, player);
        foreach (var name in new[] { "money", "shop_currency_balances", "multiplayer_wallet", "seed_inventory" })
            SameField(before, after, "player", name, required: name is "money" or "shop_currency_balances");
        var house = VerifyPickupHouseMirror(before, after);
        var view = new SnapshotEnvelope { SaveId = after.SaveId, PlayerId = after.PlayerId, GameVersion = after.GameVersion,
            State = new Dictionary<string, JsonElement>(after.State, StringComparer.Ordinal) };
        if (house is not null)
        {
            // Only a comparison view restores the independently verified inventory-count echo.
            var world = JsonNode.Parse(after.State["world_progress"].GetRawText())!.AsObject();
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
        var upgrade = Value(expected!).AsObject()["house_upgrade"] as JsonObject;
        if (upgrade is not null)
        {
            var id = upgrade["required_item_id"]?.GetValue<string>() ?? throw new InvalidDataException("Pickup house required item is missing.");
            int Count(SnapshotEnvelope snapshot) => id.Length == 0 ? 0 : checked((int)Rows(Value(Field(snapshot, "player", "inventory")!))
                .Values.Where(row => row["is_empty"]?.GetValue<bool>() != true && row["qualified_item_id"]?.GetValue<string>() == id)
                .Sum(row => (long)Int(row, "stack")));
            Require(Int(upgrade, "inventory_item_count") == Count(before), "Pickup original house inventory mirror is inconsistent.");
            upgrade["inventory_item_count"] = Count(after);
        }
        EqualField(expected, actual, "pickup house inventory mirror");
        return Field(before, "world_progress", "marriage_house")!;
    }
}

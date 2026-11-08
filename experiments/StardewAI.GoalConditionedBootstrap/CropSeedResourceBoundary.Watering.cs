using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    internal static void VerifyWatering(SnapshotEnvelope before, SnapshotEnvelope after, string location,
        MoneyRevenueCropPlantingTile tile, bool applied)
    {
        var expectedCrops = Field(before, "farm", "crops")!;
        var actualCrops = Field(after, "farm", "crops")!;
        var rows = Array(Value(expectedCrops), "watering crops");
        bool Target(JsonNode? row) => row is JsonObject value && value["location_id"]?.GetValue<string>() == location &&
            Int(value, "tile_x") == tile.TileX && Int(value, "tile_y") == tile.TileY;
        Require(rows.Count(Target) == 1, "Crop watering native target row is not unique.");
        if (applied)
        {
            var target = rows.Single(Target)!;
            Require(target["requires_watering_for_growth"]?.GetValue<bool>() == true &&
                    target["needs_watering"]?.GetValue<bool>() == true && target["watered"]?.GetValue<bool>() == false,
                "Crop watering native target lacks its original dry-soil projection.");
            target["watered"] = true;
            target["needs_watering"] = false;
        }
        EqualField(expectedCrops, actualCrops, "watering crop delta or another crop");

        var expectedCan = Field(before, "player", "watering_can_state")!;
        var actualCan = Field(after, "player", "watering_can_state")!;
        var can = Value(expectedCan);
        Require(can["projection_status"]?.GetValue<string>() == "exact_vanilla_power_zero" &&
                can["runtime_type"]?.GetValue<string>() == "StardewValley.Tools.WateringCan",
            "Crop watering requires the original exact vanilla tool projection.");
        var left = Int(can, "water_left");
        var capacity = Int(can, "capacity");
        var bottomless = can["is_bottomless"]!.GetValue<bool>();
        var enchanted = can["farmer_has_watering_can_enchantment"]!.GetValue<bool>();
        var efficient = can["is_efficient"]!.GetValue<bool>();
        var level = Int(can, "farming_level");
        Require(capacity > 0 && left <= capacity && level >= 0 && Int(can, "slot_index") >= 0 && Int(can, "upgrade_level") >= 0,
            "Crop watering tool capacity/level is invalid.");
        var cost = efficient ? 0f : 2f - level * 0.1f;
        var projectedCost = can["energy_cost_per_power_zero_action"]!.GetValue<float>();
        Require(float.IsFinite(projectedCost) && Math.Abs(projectedCost - cost) < 0.00001f,
            "Crop watering energy projection differs from native WateringCan.DoFunction.");
        if (applied)
        {
            var available = left <= 0 && !enchanted ? capacity : left;
            can["water_left"] = bottomless ? available : available - 1;
        }
        else if (Int(Value(actualCan), "water_left") != left)
        {
            Require(left <= 0 && !enchanted, "Blocked crop watering changed non-refill tool water.");
            can["water_left"] = capacity;
        }
        EqualField(expectedCan, actualCan, "watering tool state");

        var expectedEnergy = Field(before, "player", "energy")!;
        var actualEnergy = Field(after, "player", "energy")!;
        var initialEnergy = Value(expectedEnergy).GetValue<float>();
        var finalEnergy = Value(actualEnergy).GetValue<float>();
        Require(float.IsFinite(initialEnergy) && float.IsFinite(finalEnergy) &&
                finalEnergy == (applied ? initialEnergy - cost : initialEnergy),
            "Crop watering native energy delta differs from its verified tool action.");

        var bowlMirrors = VerifyWateringBowlMirrors(before, after);
        var state = new Dictionary<string, JsonElement>(after.State, StringComparer.Ordinal);
        foreach (var (domain, names) in new[]
        {
            ("farm", bowlMirrors ? new[] { "crops", "pet_bowls" } : new[] { "crops" }),
            ("player", new[] { "watering_can_state", "energy" })
        })
        {
            var fields = JsonNode.Parse(after.State[domain].GetRawText())!.AsObject();
            // Normalize only the independently verified native deltas, in a comparison view.
            foreach (var name in names) fields[name]!["value"] = Value(Field(before, domain, name)!).DeepClone();
            state[domain] = JsonSerializer.SerializeToElement(fields, JsonDefaults.Options);
        }
        var view = new SnapshotEnvelope { SaveId = after.SaveId, PlayerId = after.PlayerId, GameVersion = after.GameVersion, State = state };
        Require(FullShipmentRecoverySettlementBoundaryProjector.TryProject(before, out var first) &&
                FullShipmentRecoverySettlementBoundaryProjector.TryProject(view, out var next) && first == next,
            "Crop watering changed unrelated persistent resources or state.");
    }
}

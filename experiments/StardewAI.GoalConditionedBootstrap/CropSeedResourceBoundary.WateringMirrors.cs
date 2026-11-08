using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    private static bool VerifyWateringBowlMirrors(SnapshotEnvelope before, SnapshotEnvelope after)
    {
        var first = Field(before, "farm", "pet_bowls", required: false);
        var next = Field(after, "farm", "pet_bowls", required: false);
        if (first is null && next is null) return false;
        Require(first is not null && next is not null && Readable(first) && Readable(next),
            "Crop watering pet-bowl mirror availability changed.");
        var rows = Array(Value(first!), "watering pet-bowl mirrors");
        var actual = Array(Value(next!), "watering pet-bowl mirrors");
        Require(rows.Count == actual.Count, "Crop watering changed the pet-bowl denominator.");
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index] as JsonObject ?? throw new InvalidDataException("Crop watering pet-bowl mirror is invalid.");
            var expectedBefore = row.DeepClone().AsObject();
            ProjectWateringBowlResources(expectedBefore, before);
            Require(JsonNode.DeepEquals(expectedBefore, row), "Crop watering pet-bowl before resource projection is inconsistent.");
            ProjectWateringBowlResources(row, after);
        }
        EqualField(first, next, "watering pet-bowl resource projection or persistent bowl/pet state");
        return true;
    }

    private static void ProjectWateringBowlResources(JsonObject row, SnapshotEnvelope snapshot)
    {
        var can = Value(Field(snapshot, "player", "watering_can_state")!);
        var energy = Value(Field(snapshot, "player", "energy")!).GetValue<float>();
        var cost = can["energy_cost_per_power_zero_action"]!.GetValue<double>();
        foreach (var (mirror, native) in new[]
        {
            ("watering_can_slot_index", "slot_index"), ("watering_can_runtime_type", "runtime_type"),
            ("watering_can_upgrade_level", "upgrade_level"), ("watering_can_bottomless", "is_bottomless"),
            ("farmer_has_watering_can_enchantment", "farmer_has_watering_can_enchantment"), ("watering_can_efficient", "is_efficient")
        })
            Require(JsonNode.DeepEquals(row[mirror], can[native]), "Crop watering pet-bowl tool mirror differs: " + mirror);
        var left = Int(can, "water_left");
        row["watering_can_water_left"] = left;
        row["expected_watering_can_water_after"] = can["is_bottomless"]!.GetValue<bool>() ? left : unchecked(left - 1);
        row["energy_before"] = energy;
        row["watering_energy_cost"] = cost;
        row["expected_energy_after"] = energy - cost;
        Require(row["energy_delta_projection_status"]?.GetValue<string>() == "exact_vanilla_power_zero",
            "Crop watering pet-bowl energy projection is unavailable.");
        if (row["action_status"]?.GetValue<string>() is "watering_can_empty" or "insufficient_stamina" or "pet_love_already_maximum" or "ready")
        {
            var friendship = row["friendship_before_next_day"]!.GetValue<int>();
            var projected = row["friendship_after_fill_and_next_day_update"]!.GetValue<int>();
            row["action_status"] = left <= 0 && !can["farmer_has_watering_can_enchantment"]!.GetValue<bool>() ? "watering_can_empty" :
                energy < cost ? "insufficient_stamina" : projected <= friendship ? "pet_love_already_maximum" : "ready";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] FishPondManagementCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] intent)
    {
        var operation = FishPondManagementIntent(intent, "management_operation");
        var reason = FishPondManagementIntent(intent, "management_reason");
        var buildingX = FishPondManagementIntentInt(intent, "building_tile_x");
        var buildingY = FishPondManagementIntentInt(intent, "building_tile_y");
        if (operation is not ("cycle_netting" or "empty_pond") ||
            string.IsNullOrWhiteSpace(reason) || !buildingX.HasValue || !buildingY.HasValue ||
            (operation == "empty_pond" && FishPondManagementIntent(intent, "confirm_empty_pond") != "true"))
        {
            return Array.Empty<EventCandidate>();
        }

        var buildings = ReadStateFieldValue(snapshot, "farm", "buildings");
        if (!buildings.HasValue || buildings.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }
        var building = buildings.Value.EnumerateArray().FirstOrDefault(row =>
            row.ValueKind == JsonValueKind.Object &&
            ReadInt(row, "tile_x") == buildingX.Value && ReadInt(row, "tile_y") == buildingY.Value);
        if (building.ValueKind != JsonValueKind.Object ||
            !building.TryGetProperty("fish_pond", out var pond) || pond.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<EventCandidate>();
        }

        var farmIdentity = ReadStateFieldValue(snapshot, "farm", "farm_identity");
        var farmLocation = farmIdentity.HasValue && farmIdentity.Value.ValueKind == JsonValueKind.Object
            ? ReadString(farmIdentity.Value, "location_id")
            : "Farm";
        var reasons = new List<string>();
        var status = ReadString(pond, "management_status");
        if (!string.Equals(status, "ready", StringComparison.Ordinal))
            reasons.Add(string.IsNullOrWhiteSpace(status) ? "fish_pond_management_status_unavailable" : status);
        if (!string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), farmLocation, StringComparison.OrdinalIgnoreCase))
            reasons.Add("fish_pond_player_not_on_farm");

        var targetX = NullableInt(pond, "preferred_target_tile_x");
        var targetY = NullableInt(pond, "preferred_target_tile_y");
        var standX = NullableInt(pond, "preferred_stand_tile_x");
        var standY = NullableInt(pond, "preferred_stand_tile_y");
        if (!targetX.HasValue || !targetY.HasValue || !standX.HasValue || !standY.HasValue ||
            Math.Abs(targetX.Value - standX.Value) + Math.Abs(targetY.Value - standY.Value) != 1)
        {
            reasons.Add("fish_pond_interaction_geometry_unavailable");
            return Array.Empty<EventCandidate>();
        }

        var parameters = FishPondManagementParameters(
            building, pond, farmLocation, operation, reason,
            FishPondManagementIntent(intent, "confirm_empty_pond"),
            targetX.Value, targetY.Value, standX.Value, standY.Value);
        reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
        {
            OptionId = "fishing.manage_fish_pond",
            Parameters = parameters
        }));

        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        var distance = Math.Abs(playerX - standX.Value) + Math.Abs(playerY - standY.Value);
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "manage-fish-pond:" + farmLocation + ":" + buildingX + "," + buildingY + ":" + operation,
                Kind = "manage_fish_pond",
                Available = reasons.Count == 0,
                LocationId = farmLocation,
                TileX = targetX,
                TileY = targetY,
                DisplayName = operation == "empty_pond" ? "Empty Fish Pond" : "Cycle Fish Pond Netting",
                Quantity = operation == "empty_pond" ? ReadInt(pond, "fish_count") : 1,
                ExpectedEffect = FishPondManagementExpectedEffect(building, pond, operation),
                EstimatedTicks = Math.Max(180, distance * 60 + 180),
                EnergyCost = 0,
                AvailabilityClass = "transparent_native_pond_query_menu_player_command",
                BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = parameters
            }
        };
    }

    private static SmallModelActionParameter[] FishPondManagementParameters(
        JsonElement building,
        JsonElement pond,
        string farmLocation,
        string operation,
        string reason,
        string confirmation,
        int targetX,
        int targetY,
        int standX,
        int standY)
    {
        return new[]
        {
            Parameter("management_operation", operation),
            Parameter("management_reason", reason),
            Parameter("confirm_empty_pond", confirmation),
            Parameter("target_location", farmLocation),
            Parameter("target_tile_x", targetX.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", targetY.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", standX.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", standY.ToString(CultureInfo.InvariantCulture)),
            Parameter("building_tile_x", ReadInt(building, "tile_x").ToString(CultureInfo.InvariantCulture)),
            Parameter("building_tile_y", ReadInt(building, "tile_y").ToString(CultureInfo.InvariantCulture)),
            Parameter("target_runtime_type", ReadString(pond, "runtime_type")),
            Parameter("fish_type_item_id", ReadString(pond, "fish_type_item_id")),
            Parameter("expected_fish_count", ReadInt(pond, "fish_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_fish_count_after", ReadInt(pond, "management_empty_expected_fish_count_after").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_maximum_occupants_before", ReadInt(pond, "maximum_occupants").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_maximum_occupants_after", ReadInt(pond, "management_empty_expected_maximum_occupants_after").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_last_unlocked_population_gate_before", ReadInt(pond, "last_unlocked_population_gate").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_last_unlocked_population_gate_after", ReadInt(pond, "management_empty_expected_last_unlocked_population_gate_after").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_days_since_spawn_before", ReadInt(pond, "days_since_spawn").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_days_since_spawn_after", ReadInt(pond, "management_empty_expected_days_since_spawn_after").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_needed_item_qualified_item_id_before", ReadString(pond, "needed_item_qualified_item_id")),
            Parameter("expected_needed_item_count_before", ReadInt(pond, "needed_item_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_needed_item_count_after", ReadInt(pond, "management_empty_expected_needed_item_count_after").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_has_completed_request_before", BoolInt(pond, "has_completed_request")),
            Parameter("expected_has_completed_request_after", BoolInt(pond, "management_empty_expected_has_completed_request_after")),
            Parameter("expected_golden_animal_cracker_before", BoolInt(pond, "golden_animal_cracker")),
            Parameter("expected_golden_animal_cracker_after", BoolInt(pond, "management_empty_expected_golden_animal_cracker_after")),
            Parameter("expected_has_spawned_fish_before", BoolInt(pond, "has_spawned_fish")),
            Parameter("expected_has_spawned_fish_after", BoolInt(pond, "management_empty_expected_has_spawned_fish_after")),
            Parameter("expected_netting_style_before", ReadInt(pond, "netting_style").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_netting_style_after", (operation == "cycle_netting"
                ? ReadInt(pond, "management_cycle_expected_netting_style_after")
                : ReadInt(pond, "management_empty_expected_netting_style_after")).ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_fish_debris_qualified_item_id", ReadString(pond, "management_empty_expected_fish_debris_qualified_item_id")),
            Parameter("expected_fish_debris_count", ReadInt(pond, "management_empty_expected_fish_debris_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_sign_qualified_item_id_before", ReadString(pond, "sign_qualified_item_id")),
            Parameter("expected_output_qualified_item_id_before", ReadString(pond, "output_qualified_item_id_before_management")),
            Parameter("expected_override_water_color_packed_before", ReadLongText(pond, "override_water_color_packed")),
            Parameter("safe_slot_index", ReadInt(pond, "management_safe_slot_index").ToString(CultureInfo.InvariantCulture)),
            Parameter("restore_slot_index", ReadInt(pond, "management_restore_slot_index").ToString(CultureInfo.InvariantCulture)),
            Parameter("native_contract", ReadString(pond, "management_native_contract")),
            Parameter("max_movement_tiles", "512")
        };
    }

    private static string FishPondManagementExpectedEffect(JsonElement building, JsonElement pond, string operation) =>
        operation == "cycle_netting"
            ? "farm.fish_pond[" + ReadInt(building, "tile_x") + "," + ReadInt(building, "tile_y") + "].netting_style=" + ReadInt(pond, "management_cycle_expected_netting_style_after") + ";economic_state_unchanged=true"
            : "farm.fish_pond[" + ReadInt(building, "tile_x") + "," + ReadInt(building, "tile_y") + "].fish_type=empty;fish_count=0;fish_debris_count=" + ReadInt(pond, "management_empty_expected_fish_debris_count") + ";native_clear_pond_reset_verified=true";

    private static string FishPondManagementIntent(SmallModelActionParameter[] intent, string name) =>
        intent.FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal))?.Value?.Trim() ?? string.Empty;

    private static int? FishPondManagementIntentInt(SmallModelActionParameter[] intent, string name) =>
        int.TryParse(FishPondManagementIntent(intent, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static string ReadLongText(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText()
            : string.Empty;
}

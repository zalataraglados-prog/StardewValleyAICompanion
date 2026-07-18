using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static string[] ValidateFishPondPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var collectOutput = action.OptionId == "executor.collect_fish_pond_output";
        var completeRequest = action.OptionId == "executor.complete_fish_pond_request";
        if (!collectOutput && !completeRequest)
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        var targetX = ReadIntParameter(action, "target_tile_x");
        var targetY = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var buildingX = ReadIntParameter(action, "building_tile_x");
        var buildingY = ReadIntParameter(action, "building_tile_y");
        var quantity = ReadIntParameter(action, "quantity");
        var expectedExperience = ReadIntParameter(action, "expected_skill_experience_delta");
        if (!targetX.HasValue || !targetY.HasValue || !standX.HasValue || !standY.HasValue ||
            !buildingX.HasValue || !buildingY.HasValue || !quantity.HasValue || quantity.Value <= 0 ||
            !expectedExperience.HasValue || expectedExperience.Value <= 0 ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "qualified_item_id")) ||
            !string.Equals(ReadParameter(action, "target_runtime_type"), "StardewValley.Buildings.FishPond", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "expected_skill_id"), "fishing", StringComparison.Ordinal))
        {
            return new[] { "fish_pond_typed_projection_required" };
        }
        if (Math.Abs(targetX.Value - standX.Value) + Math.Abs(targetY.Value - standY.Value) != 1)
        {
            reasons.Add("fish_pond_stand_tile_not_adjacent");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("fish_pond_menu_must_be_clear");
        }
        var targetLocation = ReadParameter(action, "target_location");
        if (!string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("fish_pond_target_location_mismatch");
        }

        var buildings = ReadStateFieldValue(snapshot, "farm", "buildings");
        var building = buildings.HasValue && buildings.Value.ValueKind == JsonValueKind.Array
            ? buildings.Value.EnumerateArray().FirstOrDefault(row =>
                row.ValueKind == JsonValueKind.Object &&
                ReadInt(row, "tile_x") == buildingX.Value && ReadInt(row, "tile_y") == buildingY.Value)
            : default;
        if (building.ValueKind != JsonValueKind.Object ||
            !building.TryGetProperty("fish_pond", out var pond) || pond.ValueKind != JsonValueKind.Object ||
            ReadString(pond, "status") != "exact" ||
            ReadString(pond, "runtime_type") != ReadParameter(action, "target_runtime_type"))
        {
            reasons.Add("fish_pond_target_not_found_or_drifted");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        if (ReadInt(pond, "preferred_target_tile_x") != targetX.Value ||
            ReadInt(pond, "preferred_target_tile_y") != targetY.Value ||
            ReadInt(pond, "preferred_stand_tile_x") != standX.Value ||
            ReadInt(pond, "preferred_stand_tile_y") != standY.Value)
        {
            reasons.Add("fish_pond_interaction_geometry_drifted");
        }
        CompareFishPondCommonProjection(action, pond, reasons);
        if (collectOutput)
        {
            ValidateFishPondOutputProjection(action, pond, reasons);
        }
        else
        {
            ValidateFishPondRequestProjection(action, pond, reasons);
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static void CompareFishPondCommonProjection(SmallModelAction action, JsonElement pond, List<string> reasons)
    {
        if (ReadString(pond, "fish_type_item_id") != ReadParameter(action, "fish_type_item_id") ||
            ReadInt(pond, "fish_count") != ReadIntParameter(action, "expected_fish_count") ||
            ReadInt(pond, "maximum_occupants") != ReadIntParameter(action, "expected_maximum_occupants_before") ||
            ReadInt(pond, "last_unlocked_population_gate") != ReadIntParameter(action, "expected_last_unlocked_population_gate_before") ||
            ReadInt(pond, "days_since_spawn") != ReadIntParameter(action, "expected_days_since_spawn_before"))
        {
            reasons.Add("fish_pond_common_state_drifted");
        }
    }

    private static void ValidateFishPondOutputProjection(SmallModelAction action, JsonElement pond, List<string> reasons)
    {
        if (ReadString(pond, "output_status") != "ready" ||
            ReadString(pond, "output_qualified_item_id") != ReadParameter(action, "qualified_item_id") ||
            ReadInt(pond, "output_stack") != ReadIntParameter(action, "quantity") ||
            ReadString(pond, "output_items_json") != ReadParameter(action, "expected_output_items_json") ||
            ReadString(pond, "output_state_context") != ReadParameter(action, "expected_output_state_context") ||
            ReadInt(pond, "output_safe_slot_index") != ReadIntParameter(action, "safe_slot_index") ||
            ReadInt(pond, "output_fishing_experience_delta") != ReadIntParameter(action, "expected_skill_experience_delta") ||
            ReadString(pond, "output_receipt_callbacks_status") != ReadParameter(action, "native_receipt_callbacks_status"))
        {
            reasons.Add("fish_pond_output_projection_drifted");
        }
    }

    private static void ValidateFishPondRequestProjection(SmallModelAction action, JsonElement pond, List<string> reasons)
    {
        if (ReadString(pond, "request_status") != "ready" ||
            ReadString(pond, "request_item_qualified_item_id") != ReadParameter(action, "qualified_item_id") ||
            ReadString(pond, "request_item_runtime_type") != ReadParameter(action, "request_item_runtime_type") ||
            ReadInt(pond, "request_item_count_remaining") != ReadIntParameter(action, "quantity") ||
            ReadString(pond, "request_item_toolbar_slots_json") != ReadParameter(action, "request_item_toolbar_slots_json") ||
            ReadInt(pond, "request_fishing_experience_delta") != ReadIntParameter(action, "expected_skill_experience_delta") ||
            ReadInt(pond, "request_expected_maximum_occupants_after") != ReadIntParameter(action, "expected_maximum_occupants_after") ||
            ReadInt(pond, "request_expected_last_unlocked_population_gate_after") != ReadIntParameter(action, "expected_last_unlocked_population_gate_after") ||
            ReadInt(pond, "request_expected_days_since_spawn_after") != ReadIntParameter(action, "expected_days_since_spawn_after") ||
            ReadInt(pond, "request_expected_needed_item_count_after") != ReadIntParameter(action, "expected_needed_item_count_after") ||
            (ReadBool(pond, "request_expected_has_completed_request_after") == true ? 1 : 0) != ReadIntParameter(action, "expected_has_completed_request_after"))
        {
            reasons.Add("fish_pond_request_projection_drifted");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static readonly string[] FishPondManagementBoundParameterNames =
    {
        "target_location", "target_tile_x", "target_tile_y", "stand_tile_x", "stand_tile_y",
        "building_tile_x", "building_tile_y", "target_runtime_type", "fish_type_item_id",
        "expected_fish_count", "expected_fish_count_after", "expected_maximum_occupants_before",
        "expected_maximum_occupants_after", "expected_last_unlocked_population_gate_before",
        "expected_last_unlocked_population_gate_after", "expected_days_since_spawn_before",
        "expected_days_since_spawn_after", "expected_needed_item_qualified_item_id_before",
        "expected_needed_item_count_before", "expected_needed_item_count_after",
        "expected_has_completed_request_before", "expected_has_completed_request_after",
        "expected_golden_animal_cracker_before", "expected_golden_animal_cracker_after",
        "expected_has_spawned_fish_before", "expected_has_spawned_fish_after",
        "expected_netting_style_before", "expected_netting_style_after",
        "expected_fish_debris_qualified_item_id", "expected_fish_debris_count",
        "expected_sign_qualified_item_id_before", "expected_output_qualified_item_id_before",
        "expected_override_water_color_packed_before", "safe_slot_index", "restore_slot_index",
        "native_contract", "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildFishPondManagementParameters(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var parameters = action.Parameters
            .Where(parameter => !FishPondManagementBoundParameterNames.Contains(parameter.Name, StringComparer.Ordinal))
            .ToList();
        var target = SelectFishPondManagementTarget(action, snapshot);
        if (target is null)
            return parameters.ToArray();

        parameters.AddRange(new[]
        {
            Parameter("target_location", target.LocationId),
            Parameter("target_tile_x", target.TargetX.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", target.TargetY.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", target.StandX.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", target.StandY.ToString(CultureInfo.InvariantCulture)),
            Parameter("building_tile_x", target.BuildingX.ToString(CultureInfo.InvariantCulture)),
            Parameter("building_tile_y", target.BuildingY.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_runtime_type", ReadString(target.Pond, "runtime_type")),
            Parameter("fish_type_item_id", ReadString(target.Pond, "fish_type_item_id")),
            Parameter("expected_fish_count", ReadInt(target.Pond, "fish_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_fish_count_after", ReadInt(target.Pond, "management_empty_expected_fish_count_after").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_maximum_occupants_before", ReadInt(target.Pond, "maximum_occupants").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_maximum_occupants_after", ReadInt(target.Pond, "management_empty_expected_maximum_occupants_after").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_last_unlocked_population_gate_before", ReadInt(target.Pond, "last_unlocked_population_gate").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_last_unlocked_population_gate_after", ReadInt(target.Pond, "management_empty_expected_last_unlocked_population_gate_after").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_days_since_spawn_before", ReadInt(target.Pond, "days_since_spawn").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_days_since_spawn_after", ReadInt(target.Pond, "management_empty_expected_days_since_spawn_after").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_needed_item_qualified_item_id_before", ReadString(target.Pond, "needed_item_qualified_item_id")),
            Parameter("expected_needed_item_count_before", ReadInt(target.Pond, "needed_item_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_needed_item_count_after", ReadInt(target.Pond, "management_empty_expected_needed_item_count_after").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_has_completed_request_before", BoolParameter(target.Pond, "has_completed_request")),
            Parameter("expected_has_completed_request_after", BoolParameter(target.Pond, "management_empty_expected_has_completed_request_after")),
            Parameter("expected_golden_animal_cracker_before", BoolParameter(target.Pond, "golden_animal_cracker")),
            Parameter("expected_golden_animal_cracker_after", BoolParameter(target.Pond, "management_empty_expected_golden_animal_cracker_after")),
            Parameter("expected_has_spawned_fish_before", BoolParameter(target.Pond, "has_spawned_fish")),
            Parameter("expected_has_spawned_fish_after", BoolParameter(target.Pond, "management_empty_expected_has_spawned_fish_after")),
            Parameter("expected_netting_style_before", ReadInt(target.Pond, "netting_style").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_netting_style_after", (target.Operation == "cycle_netting"
                ? ReadInt(target.Pond, "management_cycle_expected_netting_style_after")
                : ReadInt(target.Pond, "management_empty_expected_netting_style_after")).ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_fish_debris_qualified_item_id", ReadString(target.Pond, "management_empty_expected_fish_debris_qualified_item_id")),
            Parameter("expected_fish_debris_count", ReadInt(target.Pond, "management_empty_expected_fish_debris_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_sign_qualified_item_id_before", ReadString(target.Pond, "sign_qualified_item_id")),
            Parameter("expected_output_qualified_item_id_before", ReadString(target.Pond, "output_qualified_item_id_before_management")),
            Parameter("expected_override_water_color_packed_before", RawText(target.Pond, "override_water_color_packed")),
            Parameter("safe_slot_index", ReadInt(target.Pond, "management_safe_slot_index").ToString(CultureInfo.InvariantCulture)),
            Parameter("restore_slot_index", ReadInt(target.Pond, "management_restore_slot_index").ToString(CultureInfo.InvariantCulture)),
            Parameter("native_contract", ReadString(target.Pond, "management_native_contract")),
            Parameter("max_movement_tiles", "512")
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileFishPondManagementStep(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var bound = BoundFishPondManagementAction(action, snapshot);
        var operation = ReadParameter(bound, "management_operation");
        var x = ReadIntParameter(bound, "building_tile_x");
        var y = ReadIntParameter(bound, "building_tile_y");
        if (!x.HasValue || !y.HasValue || operation is not ("cycle_netting" or "empty_pond"))
            return Array.Empty<CompiledActionStep>();
        var effect = operation == "cycle_netting"
            ? "farm.fish_pond[" + x + "," + y + "].netting_style=" + ReadParameter(bound, "expected_netting_style_after") + ";economic_state_unchanged=true;selected_slot_restored=true"
            : "farm.fish_pond[" + x + "," + y + "].fish_type=empty;fish_count=0;fish_debris_count=" + ReadParameter(bound, "expected_fish_debris_count") + ";native_clear_pond_reset_and_preservation_verified=true;selected_slot_restored=true";
        return new[]
        {
            Step("manage_fish_pond", ReadParameter(bound, "target_location") + "(" + x + "," + y + "):" + operation, effect, 1200)
        };
    }

    private static string[] ValidateFishPondManagementPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "fishing.manage_fish_pond")
            return Array.Empty<string>();
        var reasons = new List<string>();
        var operation = ReadParameter(action, "management_operation");
        if (operation is not ("cycle_netting" or "empty_pond") || string.IsNullOrWhiteSpace(ReadParameter(action, "management_reason")))
            reasons.Add("fish_pond_management_operation_and_reason_required");
        if (operation == "empty_pond" && ReadParameter(action, "confirm_empty_pond") != "true")
            reasons.Add("fish_pond_empty_explicit_confirmation_required");
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("fish_pond_management_menu_must_be_clear");

        var target = SelectFishPondManagementTarget(action, snapshot);
        if (target is null)
        {
            reasons.Add("fish_pond_management_target_not_found_or_drifted");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
        if (ReadString(target.Pond, "management_status") != "ready")
            reasons.Add("fish_pond_management_not_ready:" + ReadString(target.Pond, "management_status"));
        if (!string.Equals(target.LocationId, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            reasons.Add("fish_pond_management_target_location_mismatch");

        var bound = BoundFishPondManagementAction(action, snapshot);
        var safeSlot = ReadIntParameter(bound, "safe_slot_index");
        var restoreSlot = ReadIntParameter(bound, "restore_slot_index");
        var beforeStyle = ReadIntParameter(bound, "expected_netting_style_before");
        var afterStyle = ReadIntParameter(bound, "expected_netting_style_after");
        if (!safeSlot.HasValue || safeSlot is < 0 or > 11 || !restoreSlot.HasValue || restoreSlot is < 0 or > 11 ||
            !beforeStyle.HasValue || beforeStyle is < 0 or > 3 || !afterStyle.HasValue || afterStyle is < 0 or > 3 ||
            !string.IsNullOrWhiteSpace(ReadParameter(bound, "expected_output_qualified_item_id_before")) ||
            string.IsNullOrWhiteSpace(ReadParameter(bound, "fish_type_item_id")) ||
            !string.Equals(ReadParameter(bound, "target_runtime_type"), "StardewValley.Buildings.FishPond", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(ReadParameter(bound, "native_contract")))
        {
            reasons.Add("fish_pond_management_typed_projection_required");
        }
        if (operation == "cycle_netting" && afterStyle != (beforeStyle + 1) % 4)
            reasons.Add("fish_pond_netting_projection_drifted");
        if (operation == "empty_pond" &&
            (ReadIntParameter(bound, "expected_fish_debris_count") != ReadIntParameter(bound, "expected_fish_count") ||
             (ReadIntParameter(bound, "expected_fish_count") > 0 && string.IsNullOrWhiteSpace(ReadParameter(bound, "expected_fish_debris_qualified_item_id"))) ||
             ReadIntParameter(bound, "expected_fish_count_after") != 0 ||
             ReadIntParameter(bound, "expected_last_unlocked_population_gate_after") != 0 ||
             ReadIntParameter(bound, "expected_days_since_spawn_after") != 0 ||
             ReadIntParameter(bound, "expected_needed_item_count_after") != -1 ||
             ReadIntParameter(bound, "expected_golden_animal_cracker_after") != 0 ||
             ReadIntParameter(bound, "expected_has_spawned_fish_after") != 0))
        {
            reasons.Add("fish_pond_empty_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundFishPondManagementAction(SmallModelAction action, SnapshotEnvelope snapshot) =>
        new()
        {
            ActionId = action.ActionId,
            OptionId = action.OptionId,
            Rationale = action.Rationale,
            Parameters = BuildFishPondManagementParameters(action, snapshot)
        };

    private static FishPondManagementCompilerTarget? SelectFishPondManagementTarget(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var operation = ReadParameter(action, "management_operation");
        var x = ReadIntParameter(action, "building_tile_x");
        var y = ReadIntParameter(action, "building_tile_y");
        if (operation is not ("cycle_netting" or "empty_pond") || !x.HasValue || !y.HasValue)
            return null;
        var buildings = ReadStateFieldValue(snapshot, "farm", "buildings");
        if (!buildings.HasValue || buildings.Value.ValueKind != JsonValueKind.Array)
            return null;
        var building = buildings.Value.EnumerateArray().FirstOrDefault(row =>
            row.ValueKind == JsonValueKind.Object && ReadInt(row, "tile_x") == x.Value && ReadInt(row, "tile_y") == y.Value);
        if (building.ValueKind != JsonValueKind.Object || !building.TryGetProperty("fish_pond", out var pond) ||
            pond.ValueKind != JsonValueKind.Object || ReadString(pond, "status") != "exact")
            return null;
        var targetX = NullableFishPondInt(pond, "preferred_target_tile_x");
        var targetY = NullableFishPondInt(pond, "preferred_target_tile_y");
        var standX = NullableFishPondInt(pond, "preferred_stand_tile_x");
        var standY = NullableFishPondInt(pond, "preferred_stand_tile_y");
        if (!targetX.HasValue || !targetY.HasValue || !standX.HasValue || !standY.HasValue ||
            Math.Abs(targetX.Value - standX.Value) + Math.Abs(targetY.Value - standY.Value) != 1)
            return null;
        var farmIdentity = ReadStateFieldValue(snapshot, "farm", "farm_identity");
        var locationId = farmIdentity.HasValue && farmIdentity.Value.ValueKind == JsonValueKind.Object
            ? ReadString(farmIdentity.Value, "location_id")
            : "Farm";
        return new FishPondManagementCompilerTarget(
            operation, locationId, x.Value, y.Value, targetX.Value, targetY.Value, standX.Value, standY.Value, pond);
    }

    private static int? NullableFishPondInt(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    private static string BoolParameter(JsonElement row, string property) =>
        ReadBool(row, property) == true ? "1" : "0";

    private static string RawText(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText()
            : string.Empty;

    private sealed record FishPondManagementCompilerTarget(
        string Operation,
        string LocationId,
        int BuildingX,
        int BuildingY,
        int TargetX,
        int TargetY,
        int StandX,
        int StandY,
        JsonElement Pond);
}

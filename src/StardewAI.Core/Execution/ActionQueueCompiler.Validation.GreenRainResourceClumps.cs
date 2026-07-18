using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static string[] ValidateCurrentLocationResourceClumpPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.break_current_location_resource_clump")
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        var anchorX = ReadIntParameter(action, "resource_clump_tile_x");
        var anchorY = ReadIntParameter(action, "resource_clump_tile_y");
        var width = ReadIntParameter(action, "resource_clump_width");
        var height = ReadIntParameter(action, "resource_clump_height");
        var index = ReadIntParameter(action, "resource_clump_parent_sheet_index");
        var targetX = ReadIntParameter(action, "target_tile_x");
        var targetY = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var toolSlot = ReadIntParameter(action, "tool_slot_index");
        var maximumHits = ReadIntParameter(action, "max_tool_swings");
        var expectedXp = ReadIntParameter(action, "expected_foraging_experience_delta");
        var unseenNotes = ReadIntParameter(action, "unseen_secret_note_count");
        var totalNotes = ReadIntParameter(action, "total_secret_note_count");
        var outerProbability = ReadDoubleParameter(action, "secret_note_outer_roll_probability");
        var innerProbability = ReadDoubleParameter(action, "secret_note_inner_roll_probability");
        var combinedProbability = ReadDoubleParameter(action, "secret_note_combined_probability");
        if (!anchorX.HasValue || !anchorY.HasValue || !width.HasValue || !height.HasValue || !index.HasValue ||
            !targetX.HasValue || !targetY.HasValue || !standX.HasValue || !standY.HasValue ||
            !toolSlot.HasValue || !maximumHits.HasValue || !expectedXp.HasValue ||
            !unseenNotes.HasValue || !totalNotes.HasValue || !outerProbability.HasValue ||
            !innerProbability.HasValue || !combinedProbability.HasValue)
        {
            return new[] { "green_rain_resource_clump_typed_target_fields_required" };
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("green_rain_resource_clump_menu_must_be_clear");
        }
        var targetLocation = ReadParameter(action, "target_location");
        if (!string.IsNullOrWhiteSpace(targetLocation) &&
            !string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("green_rain_resource_clump_location_drifted");
        }
        if (width.Value != 2 || height.Value != 2 || index.Value is not 44 and not 46 ||
            !TileInsideRectangle(targetX.Value, targetY.Value, anchorX.Value, anchorY.Value, width.Value, height.Value) ||
            TileInsideRectangle(standX.Value, standY.Value, anchorX.Value, anchorY.Value, width.Value, height.Value) ||
            Math.Abs(standX.Value - targetX.Value) + Math.Abs(standY.Value - targetY.Value) != 1)
        {
            reasons.Add("green_rain_resource_clump_geometry_or_family_invalid");
        }
        if (!string.Equals(ReadParameter(action, "required_tool_kind"), "axe", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "target_runtime_type"), "StardewValley.TerrainFeatures.ResourceClump", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "native_contract"), "axe_DoFunction_to_GameLocation.performToolAction_then_ResourceClump.destroy", StringComparison.Ordinal))
        {
            reasons.Add("green_rain_resource_clump_native_contract_incomplete");
        }

        var clumps = ReadStateFieldValue(snapshot, "current_location", "resource_clumps");
        var clump = clumps.HasValue && clumps.Value.ValueKind == JsonValueKind.Array
            ? clumps.Value.EnumerateArray().FirstOrDefault(row =>
                ReadInt(row, "tile_x") == anchorX.Value && ReadInt(row, "tile_y") == anchorY.Value &&
                ReadInt(row, "width") == width.Value && ReadInt(row, "height") == height.Value &&
                ReadInt(row, "parent_sheet_index") == index.Value)
            : default;
        if (clump.ValueKind != JsonValueKind.Object ||
            !string.Equals(ReadString(clump, "clear_kind"), "green_rain_bush", StringComparison.Ordinal))
        {
            reasons.Add("green_rain_resource_clump_target_not_found_or_drifted");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
        var status = ReadString(clump, "clear_obstacle_executor_status");
        if (!string.Equals(status, "ready", StringComparison.Ordinal))
        {
            reasons.Add(string.IsNullOrWhiteSpace(status) ? "green_rain_resource_clump_projection_unavailable" : status);
        }
        if (ReadInt(clump, "tool_slot_index") != toolSlot.Value ||
            ReadInt(clump, "expected_tool_hits_to_clear") > maximumHits.Value)
        {
            reasons.Add("green_rain_resource_clump_tool_projection_drifted");
        }
        if (expectedXp.Value != 15 || ReadInt(clump, "expected_foraging_experience_delta") != expectedXp.Value ||
            !string.Equals(ReadString(clump, "expected_core_output_items_json"), ReadParameter(action, "expected_output_items_json"), StringComparison.Ordinal) ||
            !string.Equals(ReadString(clump, "output_distribution_status"), ReadParameter(action, "output_distribution_status"), StringComparison.Ordinal) ||
            !string.Equals(ReadString(clump, "possible_secret_note_qualified_item_id"), ReadParameter(action, "possible_secret_note_qualified_item_id"), StringComparison.OrdinalIgnoreCase) ||
            ReadInt(clump, "unseen_secret_note_count") != unseenNotes.Value ||
            ReadInt(clump, "total_secret_note_count") != totalNotes.Value ||
            Math.Abs(ReadDouble(clump, "secret_note_outer_roll_probability") - outerProbability.Value) > 1e-12 ||
            Math.Abs(ReadDouble(clump, "secret_note_inner_roll_probability") - innerProbability.Value) > 1e-12 ||
            Math.Abs(ReadDouble(clump, "secret_note_combined_probability") - combinedProbability.Value) > 1e-12 ||
            !string.Equals(ReadString(clump, "secret_note_projection_status"), ReadParameter(action, "secret_note_projection_status"), StringComparison.Ordinal))
        {
            reasons.Add("green_rain_resource_clump_output_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }
}

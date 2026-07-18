using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static string[] ValidateHarvestBushPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.harvest_bush")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var actionX = ReadIntParameter(action, "interaction_tile_x");
            var actionY = ReadIntParameter(action, "interaction_tile_y");
            var standX = ReadIntParameter(action, "stand_tile_x");
            var standY = ReadIntParameter(action, "stand_tile_y");
            var quantity = ReadIntParameter(action, "quantity");
            var quality = ReadIntParameter(action, "expected_output_quality");
            var foragingXp = ReadIntParameter(action, "expected_foraging_experience_delta");
            var offsetAfter = ReadIntParameter(action, "expected_tile_sheet_offset_after");
            if (!targetX.HasValue || !targetY.HasValue || !actionX.HasValue || !actionY.HasValue ||
                !standX.HasValue || !standY.HasValue || !quantity.HasValue || !quality.HasValue ||
                !foragingXp.HasValue || !offsetAfter.HasValue)
            {
                return new[] { "harvest_bush_typed_target_fields_required" };
            }
            if (Math.Abs(actionX.Value - standX.Value) + Math.Abs(actionY.Value - standY.Value) != 1)
            {
                reasons.Add("harvest_bush_stand_not_adjacent_to_interaction_tile");
            }
            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("harvest_bush_menu_must_be_clear");
            }
            if (!string.Equals(ReadParameter(action, "target_runtime_type"), "StardewValley.TerrainFeatures.Bush", StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "bush_projection_status"), "exact_from_native_bush_shake", StringComparison.Ordinal) ||
                offsetAfter.Value != 0)
            {
                reasons.Add("harvest_bush_native_contract_incomplete");
            }

            var targetLocation = ReadParameter(action, "target_location");
            if (!string.IsNullOrWhiteSpace(targetLocation) &&
                !string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("harvest_bush_target_location_mismatch");
            }

            var features = ReadStateFieldValue(snapshot, "current_location", "large_terrain_features");
            var target = features.HasValue && features.Value.ValueKind == JsonValueKind.Array
                ? features.Value.EnumerateArray().FirstOrDefault(feature =>
                    ReadBool(feature, "is_bush") == true &&
                    ReadInt(feature, "tile_x") == targetX.Value && ReadInt(feature, "tile_y") == targetY.Value)
                : default;
            if (target.ValueKind != JsonValueKind.Object)
            {
                reasons.Add("harvest_bush_target_not_found_or_drifted");
                return reasons.Distinct(StringComparer.Ordinal).ToArray();
            }

            var width = Math.Max(1, ReadInt(target, "bounding_tile_width"));
            var interactionInside = actionY.Value == targetY.Value && actionX.Value >= targetX.Value && actionX.Value < targetX.Value + width;
            var standInside = standY.Value == targetY.Value && standX.Value >= targetX.Value && standX.Value < targetX.Value + width;
            if (!interactionInside || standInside)
            {
                reasons.Add("harvest_bush_interaction_geometry_drifted");
            }
            if (!string.Equals(ReadString(target, "bush_harvest_status"), "ready", StringComparison.Ordinal))
            {
                reasons.Add("harvest_bush_not_ready_by_transparent_state");
            }
            if (!string.Equals(ReadString(target, "runtime_type"), ReadParameter(action, "target_runtime_type"), StringComparison.Ordinal) ||
                !string.Equals(ReadString(target, "bush_kind"), ReadParameter(action, "bush_kind"), StringComparison.Ordinal) ||
                !string.Equals(ReadString(target, "bush_output_qualified_item_id"), ReadParameter(action, "qualified_item_id"), StringComparison.OrdinalIgnoreCase) ||
                ReadInt(target, "bush_output_quantity_min") != quantity.Value ||
                ReadInt(target, "bush_output_quantity_max") != quantity.Value ||
                ReadInt(target, "bush_output_quality") != quality.Value ||
                ReadInt(target, "bush_foraging_experience_on_success_min") != foragingXp.Value ||
                ReadInt(target, "bush_foraging_experience_on_success_max") != foragingXp.Value ||
                ReadInt(target, "tile_sheet_offset_expected_after") != offsetAfter.Value ||
                !string.Equals(ReadString(target, "bush_nut_key"), ReadParameter(action, "bush_nut_key") ?? string.Empty, StringComparison.Ordinal))
            {
                reasons.Add("harvest_bush_output_projection_drifted");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
    }
}

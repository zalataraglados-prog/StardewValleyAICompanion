using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static string[] ValidateHarvestGingerPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.harvest_ginger")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var standX = ReadIntParameter(action, "stand_tile_x");
            var standY = ReadIntParameter(action, "stand_tile_y");
            var toolSlot = ReadIntParameter(action, "tool_slot_index");
            var outputId = ReadParameter(action, "qualified_item_id");
            var quantity = ReadIntParameter(action, "quantity");
            var expectedQuality = ReadIntParameter(action, "expected_output_quality");
            var expectedXp = ReadIntParameter(action, "expected_foraging_experience_delta");
            var expectedState = ReadIntParameter(action, "expected_hoe_dirt_state_after");
            var expectedEnergyText = ReadParameter(action, "expected_energy_cost");
            if (!targetX.HasValue || !targetY.HasValue || !standX.HasValue || !standY.HasValue ||
                !toolSlot.HasValue || !expectedQuality.HasValue || !expectedXp.HasValue || !expectedState.HasValue ||
                !double.TryParse(expectedEnergyText, NumberStyles.Float, CultureInfo.InvariantCulture, out var expectedEnergy))
            {
                reasons.Add("harvest_ginger_typed_target_fields_required");
                return reasons.ToArray();
            }
            if (Math.Abs(targetX.Value - standX.Value) + Math.Abs(targetY.Value - standY.Value) != 1)
            {
                reasons.Add("harvest_ginger_stand_tile_not_adjacent");
            }
            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("harvest_ginger_menu_must_be_clear");
            }
            if (!string.Equals(ReadParameter(action, "required_tool_kind"), "Hoe", StringComparison.Ordinal) ||
                !string.Equals(outputId, "(O)829", StringComparison.Ordinal) || quantity != 1 || expectedXp != 7)
            {
                reasons.Add("harvest_ginger_native_contract_mismatch");
            }
            if (!string.Equals(ReadParameter(action, "ginger_projection_status"), "exact_from_native_crop_hit_with_hoe", StringComparison.Ordinal))
            {
                reasons.Add("harvest_ginger_projection_incomplete");
            }

            var targetLocation = ReadParameter(action, "target_location");
            if (!string.IsNullOrWhiteSpace(targetLocation) &&
                !string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("harvest_ginger_target_location_mismatch");
            }

            var terrainFeatures = ReadStateFieldValue(snapshot, "current_location", "terrain_features");
            var target = terrainFeatures.HasValue && terrainFeatures.Value.ValueKind == JsonValueKind.Array
                ? terrainFeatures.Value.EnumerateArray().FirstOrDefault(feature =>
                    ReadInt(feature, "tile_x") == targetX.Value && ReadInt(feature, "tile_y") == targetY.Value)
                : default;
            if (target.ValueKind != JsonValueKind.Object || ReadBool(target, "is_ginger") != true)
            {
                reasons.Add("harvest_ginger_target_not_found_or_drifted");
                return reasons.Distinct(StringComparer.Ordinal).ToArray();
            }
            if (!string.Equals(ReadString(target, "ginger_harvest_status"), "ready", StringComparison.Ordinal))
            {
                reasons.Add("harvest_ginger_not_ready_by_transparent_state");
            }
            if (ReadInt(target, "ginger_tool_slot_index") != toolSlot.Value)
            {
                reasons.Add("harvest_ginger_tool_slot_projection_drifted");
            }
            if (Math.Abs(ReadDouble(target, "ginger_energy_cost") - expectedEnergy) > 0.001d ||
                (ReadStateFieldDoubleOptional(snapshot, "player", "energy") ?? 0d) < expectedEnergy)
            {
                reasons.Add("harvest_ginger_energy_projection_drifted_or_insufficient");
            }
            if (ReadInt(target, "ginger_hoe_dirt_state_expected_after") != expectedState.Value ||
                !string.Equals(ReadString(target, "ginger_output_qualified_item_id"), "(O)829", StringComparison.Ordinal) ||
                ReadInt(target, "ginger_output_quality") != expectedQuality.Value || expectedQuality.Value != 0 ||
                ReadInt(target, "ginger_output_quantity_min") != 1 ||
                ReadInt(target, "ginger_output_quantity_max") != 1 ||
                ReadInt(target, "ginger_foraging_experience_on_success_min") != 7 ||
                ReadInt(target, "ginger_foraging_experience_on_success_max") != 7)
            {
                reasons.Add("harvest_ginger_output_projection_drifted");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
    }
}

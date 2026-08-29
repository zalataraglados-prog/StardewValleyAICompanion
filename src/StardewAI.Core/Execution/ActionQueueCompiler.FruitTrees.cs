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
        private const string FruitTreeHarvestNativeContract =
            "GameLocation.checkAction -> FruitTree.performUseAction -> FruitTree.shake; no direct fruit, debris, inventory, or skill mutation";

        private static CompiledActionStep[] CompileHarvestFruitTreeStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            var outputsJson = ReadParameter(action, "expected_output_items_json");
            if (!x.HasValue || !y.HasValue || string.IsNullOrWhiteSpace(outputsJson))
            {
                return Array.Empty<CompiledActionStep>();
            }

            var estimatedTicks = Math.Max(1, ReadIntParameter(action, "estimated_minutes") ?? 1) * 60;
            return new[]
            {
                Step(
                    "harvest_fruit_tree",
                    "current_location(" + x.Value + "," + y.Value + "):native_fruit_tree_shake",
                    "current_location.terrain_features[" + x.Value + "," + y.Value + "].fruit_count=0;outputs=" + outputsJson,
                    estimatedTicks)
            };
        }

        private static string[] ValidateHarvestFruitTreePlan(
            SmallModelAction action,
            SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.harvest_fruit_tree")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var interactionX = ReadIntParameter(action, "interaction_tile_x");
            var interactionY = ReadIntParameter(action, "interaction_tile_y");
            var standX = ReadIntParameter(action, "stand_tile_x");
            var standY = ReadIntParameter(action, "stand_tile_y");
            var fruitBefore = ReadIntParameter(action, "expected_fruit_count_before");
            var fruitAfter = ReadIntParameter(action, "expected_fruit_count_after");
            var foragingXp = ReadIntParameter(action, "expected_foraging_experience_delta");
            var outputsJson = ReadParameter(action, "expected_output_items_json");
            if (!targetX.HasValue || !targetY.HasValue || !interactionX.HasValue || !interactionY.HasValue ||
                !standX.HasValue || !standY.HasValue || !fruitBefore.HasValue || !fruitAfter.HasValue ||
                !foragingXp.HasValue || string.IsNullOrWhiteSpace(outputsJson))
            {
                return new[] { "harvest_fruit_tree_typed_target_fields_required" };
            }
            if (interactionX != targetX || interactionY != targetY ||
                Math.Abs(interactionX.Value - standX.Value) + Math.Abs(interactionY.Value - standY.Value) != 1)
            {
                reasons.Add("harvest_fruit_tree_interaction_geometry_drifted");
            }
            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("harvest_fruit_tree_menu_must_be_clear");
            }
            if (!string.Equals(ReadParameter(action, "target_runtime_type"), "StardewValley.TerrainFeatures.FruitTree", StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "fruit_tree_projection_status"), "exact_from_native_fruit_tree_performUseAction_and_shake", StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "fruit_tree_native_contract"), FruitTreeHarvestNativeContract, StringComparison.Ordinal) ||
                fruitAfter.Value != 0 || foragingXp.Value != 0)
            {
                reasons.Add("harvest_fruit_tree_native_contract_incomplete");
            }

            var targetLocation = ReadParameter(action, "target_location");
            if (!string.IsNullOrWhiteSpace(targetLocation) &&
                !string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("harvest_fruit_tree_target_location_mismatch");
            }

            var features = ReadStateFieldValue(snapshot, "current_location", "terrain_features");
            var target = features.HasValue && features.Value.ValueKind == JsonValueKind.Array
                ? features.Value.EnumerateArray().FirstOrDefault(feature =>
                    ReadBool(feature, "is_fruit_tree") == true &&
                    ReadInt(feature, "tile_x") == targetX.Value && ReadInt(feature, "tile_y") == targetY.Value)
                : default;
            if (target.ValueKind != JsonValueKind.Object)
            {
                reasons.Add("harvest_fruit_tree_target_not_found_or_drifted");
                return reasons.Distinct(StringComparer.Ordinal).ToArray();
            }
            if (!string.Equals(ReadString(target, "fruit_tree_harvest_status"), "ready", StringComparison.Ordinal))
            {
                reasons.Add("harvest_fruit_tree_not_ready_by_transparent_state");
            }

            var snapshotOutputs = target.TryGetProperty("fruit_tree_expected_outputs", out var outputValue) &&
                outputValue.ValueKind == JsonValueKind.Array
                    ? JsonSerializer.Serialize(outputValue)
                    : string.Empty;
            if (!string.Equals(ReadString(target, "runtime_type"), ReadParameter(action, "target_runtime_type"), StringComparison.Ordinal) ||
                !string.Equals(ReadString(target, "fruit_tree_id"), ReadParameter(action, "fruit_tree_id"), StringComparison.Ordinal) ||
                ReadInt(target, "fruit_count") != fruitBefore.Value ||
                ReadInt(target, "fruit_tree_expected_fruit_count_after") != fruitAfter.Value ||
                ReadInt(target, "fruit_tree_expected_foraging_experience_delta") != foragingXp.Value ||
                !FruitTreeJsonEquivalent(snapshotOutputs, outputsJson))
            {
                reasons.Add("harvest_fruit_tree_output_projection_drifted");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static bool FruitTreeJsonEquivalent(string left, string right)
        {
            try
            {
                using var leftDocument = JsonDocument.Parse(left);
                using var rightDocument = JsonDocument.Parse(right);
                return JsonSerializer.Serialize(leftDocument.RootElement) ==
                    JsonSerializer.Serialize(rightDocument.RootElement);
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}

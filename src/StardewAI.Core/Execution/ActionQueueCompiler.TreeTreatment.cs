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
        private static string[] ValidateApplyTreeTreatmentPlan(
            SmallModelAction action,
            SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.apply_tree_treatment")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var slotIndex = ReadIntParameter(action, "slot_index");
            var qualifiedItemId = ReadParameter(action, "qualified_item_id");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                reasons.Add("apply_tree_treatment_target_tile_required");
            }
            if (!slotIndex.HasValue || !string.Equals(qualifiedItemId, "(O)419", StringComparison.Ordinal))
            {
                reasons.Add("apply_tree_treatment_vinegar_inventory_identity_required");
            }
            if (!string.Equals(
                    ReadParameter(action, "target_runtime_type"),
                    "StardewValley.TerrainFeatures.Tree",
                    StringComparison.Ordinal))
            {
                reasons.Add("apply_tree_treatment_exact_tree_runtime_type_required");
            }
            if (string.IsNullOrWhiteSpace(ReadParameter(action, "tree_treatment_reason")))
            {
                reasons.Add("apply_tree_treatment_reason_required");
            }
            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("apply_tree_treatment_menu_must_be_clear");
            }
            if (!TargetLocationMatchesCurrent(action, snapshot))
            {
                reasons.Add("apply_tree_treatment_target_location_mismatch");
            }
            if (targetX.HasValue && targetY.HasValue && slotIndex.HasValue &&
                !TreeTreatmentContextAllows(snapshot, targetX.Value, targetY.Value, slotIndex.Value))
            {
                reasons.Add("apply_tree_treatment_not_allowed_by_transparent_context");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static bool TreeTreatmentContextAllows(
            SnapshotEnvelope snapshot,
            int targetX,
            int targetY,
            int slotIndex)
        {
            var terrain = ReadStateFieldValue(snapshot, "current_location", "terrain_features");
            if (!terrain.HasValue || terrain.Value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var treeMatches = terrain.Value.EnumerateArray().Any(row =>
                row.ValueKind == JsonValueKind.Object &&
                ReadInt(row, "tile_x") == targetX &&
                ReadInt(row, "tile_y") == targetY &&
                string.Equals(ReadString(row, "type"), "StardewValley.TerrainFeatures.Tree", StringComparison.Ordinal) &&
                string.Equals(ReadString(row, "tree_treatment_required_qualified_item_id"), "(O)419", StringComparison.Ordinal) &&
                ReadBool(row, "tree_treatment_native_allowed") == true &&
                ReadNullableBool(row, "stop_growing_moss") == false);
            if (!treeMatches)
            {
                return false;
            }

            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return inventory.Value.EnumerateArray().Any(row =>
                row.ValueKind == JsonValueKind.Object &&
                NullableReadInt(row, "slot_index") == slotIndex &&
                string.Equals(ReadString(row, "qualified_item_id"), "(O)419", StringComparison.Ordinal) &&
                ReadInt(row, "stack") > 0);
        }
    }
}

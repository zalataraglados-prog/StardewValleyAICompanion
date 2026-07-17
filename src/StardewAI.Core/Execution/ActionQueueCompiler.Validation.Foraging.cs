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
        private static string[] ValidateCollectSpawnedObjectPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.collect_spawned_object")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var standX = ReadIntParameter(action, "stand_tile_x");
            var standY = ReadIntParameter(action, "stand_tile_y");
            var qualifiedItemId = ReadParameter(action, "qualified_item_id");
            var quantity = ReadIntParameter(action, "quantity");
            var projectedQuality = ReadIntParameter(action, "projected_harvest_quality");
            var foragingExperienceMin = ReadIntParameter(action, "foraging_experience_on_success_min");
            var foragingExperienceMax = ReadIntParameter(action, "foraging_experience_on_success_max");
            var farmingExperienceMin = ReadIntParameter(action, "farming_experience_on_success_min");
            var farmingExperienceMax = ReadIntParameter(action, "farming_experience_on_success_max");
            if (!targetX.HasValue || !targetY.HasValue || !standX.HasValue || !standY.HasValue ||
                string.IsNullOrWhiteSpace(qualifiedItemId) || !quantity.HasValue || !projectedQuality.HasValue ||
                !foragingExperienceMin.HasValue || !foragingExperienceMax.HasValue ||
                !farmingExperienceMin.HasValue || !farmingExperienceMax.HasValue)
            {
                reasons.Add("collect_spawned_object_typed_target_fields_required");
                return reasons.ToArray();
            }
            if (Math.Abs(targetX.Value - standX.Value) + Math.Abs(targetY.Value - standY.Value) != 1)
            {
                reasons.Add("collect_spawned_object_stand_tile_not_adjacent");
            }
            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("collect_spawned_object_menu_must_be_clear");
            }
            var targetLocation = ReadParameter(action, "target_location");
            if (!string.IsNullOrWhiteSpace(targetLocation) &&
                !string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("collect_spawned_object_target_location_mismatch");
            }

            var objects = ReadStateFieldValue(snapshot, "current_location", "objects");
            var target = objects.HasValue && objects.Value.ValueKind == JsonValueKind.Array
                ? objects.Value.EnumerateArray().FirstOrDefault(item =>
                    ReadInt(item, "tile_x") == targetX.Value &&
                    ReadInt(item, "tile_y") == targetY.Value &&
                    string.Equals(ReadString(item, "qualified_item_id"), qualifiedItemId, StringComparison.OrdinalIgnoreCase))
                : default;
            if (target.ValueKind != JsonValueKind.Object || ReadBool(target, "is_spawned_object") != true)
            {
                reasons.Add("collect_spawned_object_target_not_found_or_drifted");
                return reasons.Distinct(StringComparer.Ordinal).ToArray();
            }
            var status = ReadString(target, "spawned_object_pickup_status");
            if (!string.Equals(status, "ready", StringComparison.Ordinal))
            {
                reasons.Add(string.IsNullOrWhiteSpace(status) ? "collect_spawned_object_projection_unavailable" : status);
            }
            if (ReadInt(target, "projected_total_quantity") != quantity.Value)
            {
                reasons.Add("collect_spawned_object_quantity_projection_drifted");
            }
            if (ReadInt(target, "projected_harvest_quality") != projectedQuality.Value)
            {
                reasons.Add("collect_spawned_object_quality_projection_drifted");
            }
            if (!string.Equals(ReadParameter(action, "harvest_experience_status"), "exact", StringComparison.Ordinal) ||
                !string.Equals(ReadString(target, "harvest_experience_status"), "exact", StringComparison.Ordinal))
            {
                reasons.Add("collect_spawned_object_experience_projection_incomplete");
            }
            if (ReadInt(target, "foraging_experience_on_success_min") != foragingExperienceMin.Value ||
                ReadInt(target, "foraging_experience_on_success_max") != foragingExperienceMax.Value ||
                ReadInt(target, "farming_experience_on_success_min") != farmingExperienceMin.Value ||
                ReadInt(target, "farming_experience_on_success_max") != farmingExperienceMax.Value)
            {
                reasons.Add("collect_spawned_object_experience_projection_drifted");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
    }
}

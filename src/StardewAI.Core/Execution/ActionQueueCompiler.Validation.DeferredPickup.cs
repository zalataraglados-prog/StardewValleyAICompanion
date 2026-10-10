using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static string[] ValidateDeferredPickupPlan(
            SmallModelAction action,
            SnapshotEnvelope snapshot,
            string sourceKind,
            int? targetX,
            int? targetY)
        {
            var reasons = new List<string>();
            if (sourceKind is not (
                    "native_wild_tree_chop_drop" or
                    "native_radioactive_ore_node" or
                    "native_monster_drop_table"))
            {
                reasons.Add("deferred_pickup_source_kind_unsupported");
                return reasons.ToArray();
            }

            var qualifiedItemId =
                ReadParameter(action, "qualified_item_id") ?? string.Empty;
            var quality = ReadIntParameter(action, "item_quality");
            var inventoryBefore = ReadIntParameter(
                action,
                "inventory_item_total_before");
            var debrisBefore = ReadIntParameter(
                action,
                "deferred_pickup_debris_item_total_before");
            var guaranteedMinimum = ReadIntParameter(
                action,
                "deferred_pickup_guaranteed_minimum_quantity");
            if (!targetX.HasValue || !targetY.HasValue ||
                string.IsNullOrWhiteSpace(qualifiedItemId) ||
                !quality.HasValue || quality.Value < 0 ||
                !inventoryBefore.HasValue || inventoryBefore.Value < 0 ||
                !debrisBefore.HasValue || debrisBefore.Value < 0 ||
                !guaranteedMinimum.HasValue || guaranteedMinimum.Value <= 0)
            {
                reasons.Add("deferred_pickup_typed_baseline_required");
                return reasons.ToArray();
            }

            var sourceId = ReadParameter(
                action,
                "acquisition_source_id") ?? string.Empty;
            var routeKind = ReadParameter(
                action,
                "acquisition_route_kind") ?? string.Empty;
            if (routeKind != sourceKind ||
                string.IsNullOrWhiteSpace(sourceId))
            {
                reasons.Add("deferred_pickup_authoritative_source_drifted");
            }

            if (sourceKind == "native_wild_tree_chop_drop")
            {
                ValidateDeferredWildTreePickup(
                    action,
                    snapshot,
                    targetX.Value,
                    targetY.Value,
                    routeKind,
                    sourceId,
                    qualifiedItemId,
                    quality.Value,
                    guaranteedMinimum.Value,
                    reasons);
            }
            else if (sourceKind == "native_radioactive_ore_node")
            {
                ValidateDeferredRadioactiveOrePickup(
                    snapshot,
                    targetX.Value,
                    targetY.Value,
                    routeKind,
                    sourceId,
                    qualifiedItemId,
                    quality.Value,
                    guaranteedMinimum.Value,
                    reasons);
            }
            else
            {
                ValidateDeferredMonsterDropPickup(
                    snapshot,
                    targetX.Value,
                    targetY.Value,
                    routeKind,
                    sourceId,
                    qualifiedItemId,
                    quality.Value,
                    guaranteedMinimum.Value,
                    reasons);
            }
            if (SnapshotInventoryCount(
                    snapshot,
                    qualifiedItemId,
                    quality.Value) != inventoryBefore.Value ||
                SnapshotDebrisCount(
                    snapshot,
                    qualifiedItemId,
                    quality.Value) != debrisBefore.Value)
            {
                reasons.Add("deferred_pickup_baseline_drifted");
            }
            if (!InventoryMayAcceptProjectedItem(
                    snapshot,
                    qualifiedItemId,
                    quality.Value))
            {
                reasons.Add("pickup_debris_inventory_cannot_accept_item");
            }
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static void ValidateDeferredWildTreePickup(
            SmallModelAction action,
            SnapshotEnvelope snapshot,
            int targetX,
            int targetY,
            string routeKind,
            string sourceId,
            string qualifiedItemId,
            int quality,
            int guaranteedMinimum,
            ICollection<string> reasons)
        {
            var feature = TerrainFeatureAt(snapshot, targetX, targetY);
            if (!feature.HasValue ||
                ReadString(feature.Value, "runtime_type") !=
                    "StardewValley.TerrainFeatures.Tree" ||
                ReadString(feature.Value, "tree_chop_acquisition_status") !=
                    "ready")
            {
                reasons.Add("deferred_pickup_source_tree_not_ready");
                return;
            }

            if (!TreeDeclaresRouteSource(
                    feature.Value,
                    routeKind,
                    sourceId,
                    qualifiedItemId))
            {
                reasons.Add("deferred_pickup_authoritative_source_drifted");
            }

            var guaranteedJson = ReadParameter(
                action,
                "tree_chop_guaranteed_minimum_outputs_json") ?? string.Empty;
            if (!TreeGuaranteeMatches(
                    feature.Value,
                    guaranteedJson,
                    qualifiedItemId,
                    quality,
                    guaranteedMinimum))
            {
                reasons.Add("deferred_pickup_guaranteed_output_drifted");
            }
        }

        private static void ValidateDeferredRadioactiveOrePickup(
            SnapshotEnvelope snapshot,
            int targetX,
            int targetY,
            string routeKind,
            string sourceId,
            string qualifiedItemId,
            int quality,
            int guaranteedMinimum,
            ICollection<string> reasons)
        {
            var node = MiningObjectAt(snapshot, targetX, targetY);
            if (!node.HasValue ||
                ReadString(node.Value, "item_id") != "95" ||
                ReadString(node.Value, "qualified_item_id") != "(O)95" ||
                ReadBool(node.Value, "is_breakable_stone") != true ||
                ReadString(node.Value, "drop_rule_branch") !=
                    "game_location_break_stone_direct_node")
            {
                reasons.Add("deferred_pickup_source_mining_object_not_ready");
                return;
            }

            if (!MiningNodeDeclaresRouteSource(
                    node.Value,
                    routeKind,
                    sourceId,
                    qualifiedItemId))
            {
                reasons.Add("deferred_pickup_authoritative_source_drifted");
            }
            if (quality != 0 ||
                guaranteedMinimum != 1 ||
                !MiningNodeGuaranteesItem(node.Value, qualifiedItemId))
            {
                reasons.Add("deferred_pickup_guaranteed_output_drifted");
            }
        }

        private static JsonElement? TerrainFeatureAt(
            SnapshotEnvelope snapshot,
            int targetX,
            int targetY)
        {
            var features = ReadStateFieldValue(
                snapshot,
                "current_location",
                "terrain_features");
            if (!features.HasValue ||
                features.Value.ValueKind != JsonValueKind.Array)
                return null;
            var matches = features.Value.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.Object &&
                    ReadInt(value, "tile_x") == targetX &&
                    ReadInt(value, "tile_y") == targetY)
                .ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        private static JsonElement? MiningObjectAt(
            SnapshotEnvelope snapshot,
            int targetX,
            int targetY)
        {
            var objects = ReadStateFieldValue(snapshot, "mining", "objects");
            if (!objects.HasValue ||
                objects.Value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            var matches = objects.Value.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.Object &&
                    ReadInt(value, "tile_x") == targetX &&
                    ReadInt(value, "tile_y") == targetY)
                .ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        private static bool MiningNodeDeclaresRouteSource(
            JsonElement node,
            string routeKind,
            string sourceId,
            string qualifiedItemId)
        {
            if (!node.TryGetProperty(
                    "authoritative_route_sources",
                    out var sources) ||
                sources.ValueKind != JsonValueKind.Array)
            {
                return false;
            }
            return sources.EnumerateArray().Count(value =>
                value.ValueKind == JsonValueKind.Object &&
                ReadString(value, "route_kind") == routeKind &&
                ReadString(value, "source_id") == sourceId &&
                ReadString(value, "qualified_item_id") ==
                    qualifiedItemId) == 1;
        }

        private static bool MiningNodeGuaranteesItem(
            JsonElement node,
            string qualifiedItemId)
        {
            return node.TryGetProperty(
                    "guaranteed_drop_qualified_item_ids",
                    out var drops) &&
                drops.ValueKind == JsonValueKind.Array &&
                drops.EnumerateArray().Count(value =>
                    value.ValueKind == JsonValueKind.String &&
                    value.GetString() == qualifiedItemId) == 1;
        }

        private static bool TreeDeclaresRouteSource(
            JsonElement feature,
            string routeKind,
            string sourceId,
            string qualifiedItemId)
        {
            if (!feature.TryGetProperty(
                    "tree_chop_authoritative_route_sources",
                    out var sources) ||
                sources.ValueKind != JsonValueKind.Array)
                return false;
            return sources.EnumerateArray().Count(value =>
                value.ValueKind == JsonValueKind.Object &&
                ReadString(value, "route_kind") == routeKind &&
                ReadString(value, "source_id") == sourceId &&
                ReadString(value, "qualified_item_id") ==
                    qualifiedItemId) == 1;
        }

        private static bool TreeGuaranteeMatches(
            JsonElement feature,
            string projectedJson,
            string qualifiedItemId,
            int quality,
            int guaranteedMinimum)
        {
            if (!feature.TryGetProperty(
                    "tree_chop_guaranteed_minimum_outputs",
                    out var outputs) ||
                outputs.ValueKind != JsonValueKind.Array)
                return false;
            try
            {
                using var projected = JsonDocument.Parse(projectedJson);
                if (projected.RootElement.ValueKind != JsonValueKind.Array ||
                    !GuaranteedOutputRows(projected.RootElement)
                        .SequenceEqual(
                            GuaranteedOutputRows(outputs),
                            StringComparer.Ordinal))
                    return false;
            }
            catch (JsonException)
            {
                return false;
            }
            var matches = outputs.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.Object &&
                    ReadString(value, "qualifiedItemId") ==
                        qualifiedItemId &&
                    ReadInt(value, "quality") == quality)
                .ToArray();
            return matches.Length == 1 &&
                ReadInt(matches[0], "quantityMin") == guaranteedMinimum;
        }

        private static string[] GuaranteedOutputRows(JsonElement outputs) =>
            outputs.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.Object)
                .Select(value =>
                    ReadString(value, "qualifiedItemId") + "|" +
                    ReadInt(value, "quality") + "|" +
                    ReadInt(value, "quantityMin"))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

        private static int SnapshotInventoryCount(
            SnapshotEnvelope snapshot,
            string qualifiedItemId,
            int quality)
        {
            var inventory = ReadStateFieldValue(
                snapshot,
                "player",
                "inventory");
            return !inventory.HasValue ||
                inventory.Value.ValueKind != JsonValueKind.Array
                    ? -1
                    : inventory.Value.EnumerateArray()
                        .Where(value => value.ValueKind ==
                                JsonValueKind.Object &&
                            ReadString(value, "qualified_item_id") ==
                                qualifiedItemId &&
                            ReadInt(value, "quality") >= quality)
                        .Sum(value => Math.Max(0, ReadInt(value, "stack")));
        }

        private static int SnapshotDebrisCount(
            SnapshotEnvelope snapshot,
            string qualifiedItemId,
            int quality)
        {
            var debris = ReadStateFieldValue(
                snapshot,
                "current_location",
                "debris");
            return !debris.HasValue ||
                debris.Value.ValueKind != JsonValueKind.Array
                    ? -1
                    : debris.Value.EnumerateArray()
                        .Where(value => value.ValueKind ==
                                JsonValueKind.Object &&
                            ReadString(value, "qualified_item_id") ==
                                qualifiedItemId &&
                            ReadInt(value, "item_quality") >= quality)
                        .Sum(value => Math.Max(
                            0,
                            ReadInt(value, "chunk_count")));
        }

        private static bool InventoryMayAcceptProjectedItem(
            SnapshotEnvelope snapshot,
            string qualifiedItemId,
            int quality)
        {
            var capacity = ReadStateFieldValue(
                snapshot,
                "player",
                "inventory_capacity");
            if (capacity.HasValue &&
                capacity.Value.ValueKind == JsonValueKind.Object &&
                (ReadBool(capacity.Value, "has_empty_slot") == true ||
                 ReadInt(capacity.Value, "empty_slots") > 0))
                return true;

            var inventory = ReadStateFieldValue(
                snapshot,
                "player",
                "inventory");
            return inventory.HasValue &&
                inventory.Value.ValueKind == JsonValueKind.Array &&
                inventory.Value.EnumerateArray().Any(value =>
                    value.ValueKind == JsonValueKind.Object &&
                    (ReadBool(value, "is_empty") == true ||
                     ReadString(value, "qualified_item_id") ==
                        qualifiedItemId &&
                     ReadInt(value, "quality") == quality &&
                     ReadInt(value, "stack") <
                        ReadInt(value, "maximum_stack_size")));
        }
    }
}

using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteDispatchCompilationBuilder
{
    private const string DeferredWildTreePickupKind =
        "native_wild_tree_chop_drop";
    private const string DeferredRadioactiveOrePickupKind =
        "native_radioactive_ore_node";
    private const string DeferredMonsterDropPickupKind =
        "native_monster_drop_table";

    internal static string[] AppendDeferredNativeDropPickup(
        SmallModelPlanEnvelope plan,
        AcquisitionRouteTargetDateUnlock requirement,
        PolicyEventCandidatePrediction source,
        SnapshotEnvelope snapshot)
    {
        if (requirement.RouteKind is not (
                DeferredWildTreePickupKind or
                DeferredRadioactiveOrePickupKind or
                DeferredMonsterDropPickupKind))
            return Array.Empty<string>();

        if (requirement.RequiredAmount <= 0 ||
            string.IsNullOrWhiteSpace(requirement.QualifiedItemId))
        {
            return new[] { "native_drop_deferred_pickup_source_invalid" };
        }

        var targetX = source.TileX;
        var targetY = source.TileY;
        var minimumOutputsJson = string.Empty;
        var guaranteedMinimum = 0;
        if (requirement.RouteKind == DeferredWildTreePickupKind)
        {
            if (source.OptionId != "foraging.chop_wild_tree" ||
                source.Kind != "clear_obstacle_tile" ||
                !targetX.HasValue ||
                !targetY.HasValue)
            {
                return new[] { "native_drop_deferred_pickup_source_invalid" };
            }

            minimumOutputsJson = UniqueParameter(
                source,
                "tree_chop_guaranteed_minimum_outputs_json");
            if (!TryReadGuaranteedMinimum(
                    minimumOutputsJson,
                    requirement.QualifiedItemId,
                    requirement.MinimumQuality,
                    out guaranteedMinimum))
            {
                return new[]
                {
                    "native_drop_deferred_pickup_guarantee_insufficient"
                };
            }
        }
        else if (requirement.RouteKind == DeferredRadioactiveOrePickupKind)
        {
            targetX = UniqueIntParameter(source, "target_tile_x");
            targetY = UniqueIntParameter(source, "target_tile_y");
            if (source.OptionId != "mining.reach_depth" ||
                source.Kind != "mining_reach_depth_plan_envelope" ||
                !targetX.HasValue ||
                !targetY.HasValue ||
                !CandidateDeclaresAuthoritativeRouteSource(
                    source,
                    requirement.RouteKind,
                    requirement.SourceId,
                    requirement.QualifiedItemId) ||
                !TryReadRadioactiveNodeGuaranteedMinimum(
                    snapshot,
                    targetX.Value,
                    targetY.Value,
                    requirement,
                    out guaranteedMinimum))
            {
                return new[] { "native_drop_deferred_pickup_source_invalid" };
            }
        }
        else
        {
            targetX = UniqueIntParameter(source, "target_tile_x");
            targetY = UniqueIntParameter(source, "target_tile_y");
            if (source.OptionId != "mining.reach_depth" ||
                source.Kind != "mining_reach_depth_plan_envelope" ||
                !targetX.HasValue ||
                !targetY.HasValue ||
                !CandidateDeclaresAuthoritativeRouteSource(
                    source,
                    requirement.RouteKind,
                    requirement.SourceId,
                    requirement.QualifiedItemId) ||
                !TryReadMonsterDropGuaranteedMinimum(
                    snapshot,
                    targetX.Value,
                    targetY.Value,
                    source,
                    requirement,
                    out guaranteedMinimum))
            {
                return new[] { "native_drop_deferred_pickup_source_invalid" };
            }
        }

        if (guaranteedMinimum < requirement.RequiredAmount)
        {
            return new[]
            {
                "native_drop_deferred_pickup_guarantee_insufficient"
            };
        }

        var inventoryBefore = InventoryCount(
            snapshot,
            requirement.QualifiedItemId,
            requirement.MinimumQuality);
        var debrisBefore = DebrisCount(
            snapshot,
            requirement.QualifiedItemId,
            requirement.MinimumQuality);
        if (!inventoryBefore.HasValue || !debrisBefore.HasValue)
        {
            return new[]
            {
                "native_drop_deferred_pickup_baseline_unavailable"
            };
        }

        var pickup = new SmallModelPlanStep
        {
            StepId = "acquisition_native_drop_pickup." +
                source.CandidateId,
            Kind = "pickup_debris",
            TargetLocation = string.IsNullOrWhiteSpace(source.LocationId)
                ? "current_location"
                : source.LocationId,
            TargetTileX = targetX,
            TargetTileY = targetY,
            EstimatedMinutes = 0,
            Preconditions = new[]
            {
                "candidate_id:" +
                    AcquisitionRouteExecutionBindingBuilder.SelectedCandidateId(
                        requirement.RouteOccurrenceId),
                "prior_native_source_execution_verified=true",
                "guaranteed_target_debris_spawned=true"
            },
            ExpectedEffects = new[]
            {
                "player.inventory[" + requirement.QualifiedItemId +
                "].count_increases_by_at_least=" +
                requirement.RequiredAmount.ToString(CultureInfo.InvariantCulture)
            },
            SafetyConstraints = new[]
            {
                "reuse_shared_native_debris_pickup_executor",
                "no_direct_debris_or_inventory_mutation"
            },
            FailurePolicy = new[] { "refresh_snapshot_and_replan" },
            Parameters = new[]
            {
                Parameter(
                    "qualified_item_id",
                    requirement.QualifiedItemId),
                Parameter(
                    "item_quality",
                    requirement.MinimumQuality.ToString(
                        CultureInfo.InvariantCulture)),
                Parameter(
                    "inventory_item_total_before",
                    inventoryBefore.Value.ToString(
                        CultureInfo.InvariantCulture)),
                Parameter(
                    "deferred_pickup_source_kind",
                    requirement.RouteKind),
                Parameter(
                    "deferred_pickup_debris_item_total_before",
                    debrisBefore.Value.ToString(
                        CultureInfo.InvariantCulture)),
                Parameter(
                    "deferred_pickup_guaranteed_minimum_quantity",
                    guaranteedMinimum.ToString(
                        CultureInfo.InvariantCulture))
            }.Concat(requirement.RouteKind == DeferredWildTreePickupKind
                ? new[]
                {
                    Parameter(
                        "tree_chop_guaranteed_minimum_outputs_json",
                        minimumOutputsJson)
                }
                : Array.Empty<SmallModelActionParameter>()).ToArray()
        };
        plan.Steps = (plan.Steps ?? Array.Empty<SmallModelPlanStep>())
            .Append(pickup)
            .ToArray();
        return Array.Empty<string>();
    }

    private static string UniqueParameter(
        PolicyEventCandidatePrediction candidate,
        string name)
    {
        var values = (candidate.Parameters ??
                Array.Empty<SmallModelActionParameter>())
            .Where(value => value.Name == name)
            .Select(value => value.Value)
            .ToArray();
        return values.Length == 1 ? values[0] : string.Empty;
    }

    private static int? UniqueIntParameter(
        PolicyEventCandidatePrediction candidate,
        string name) => int.TryParse(
            UniqueParameter(candidate, name),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
                ? value
                : null;

    private static bool TryReadRadioactiveNodeGuaranteedMinimum(
        SnapshotEnvelope snapshot,
        int targetX,
        int targetY,
        AcquisitionRouteTargetDateUnlock requirement,
        out int guaranteedMinimum)
    {
        guaranteedMinimum = 0;
        if (requirement.MinimumQuality != 0 ||
            requirement.RouteKind != DeferredRadioactiveOrePickupKind ||
            requirement.SourceId != "GameLocation.breakStone" ||
            requirement.QualifiedItemId != "(O)909")
        {
            return false;
        }

        var objects = StateValue(snapshot, "mining", "objects");
        if (!objects.HasValue ||
            objects.Value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var matches = objects.Value.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.Object &&
                Int(value, "tile_x") == targetX &&
                Int(value, "tile_y") == targetY)
            .ToArray();
        if (matches.Length != 1)
            return false;

        var node = matches[0];
        if (String(node, "item_id") != "95" ||
            String(node, "qualified_item_id") != "(O)95" ||
            String(node, "drop_rule_branch") !=
                "game_location_break_stone_direct_node" ||
            !ArrayContainsExactlyOnce(
                node,
                "guaranteed_drop_qualified_item_ids",
                requirement.QualifiedItemId) ||
            !RouteSourceExistsExactlyOnce(
                node,
                "authoritative_route_sources",
                requirement.RouteKind,
                requirement.SourceId,
                requirement.QualifiedItemId))
        {
            return false;
        }

        guaranteedMinimum = 1;
        return true;
    }

    private static bool ArrayContainsExactlyOnce(
        JsonElement source,
        string propertyName,
        string expected)
    {
        return source.TryGetProperty(propertyName, out var values) &&
            values.ValueKind == JsonValueKind.Array &&
            values.EnumerateArray().Count(value =>
                value.ValueKind == JsonValueKind.String &&
                value.GetString() == expected) == 1;
    }

    private static bool RouteSourceExistsExactlyOnce(
        JsonElement source,
        string propertyName,
        string routeKind,
        string sourceId,
        string qualifiedItemId)
    {
        return source.TryGetProperty(propertyName, out var rows) &&
            rows.ValueKind == JsonValueKind.Array &&
            rows.EnumerateArray().Count(row =>
                row.ValueKind == JsonValueKind.Object &&
                String(row, "route_kind") == routeKind &&
                String(row, "source_id") == sourceId &&
                String(row, "qualified_item_id") == qualifiedItemId) == 1;
    }

    private static bool TryReadGuaranteedMinimum(
        string json,
        string qualifiedItemId,
        int minimumQuality,
        out int quantity)
    {
        quantity = 0;
        try
        {
            using var document = JsonDocument.Parse(json);
            var matches = document.RootElement
                .EnumerateArray()
                .Where(value =>
                    String(value, "qualifiedItemId") == qualifiedItemId &&
                    Int(value, "quality") >= minimumQuality)
                .Select(value => Int(value, "quantityMin"))
                .ToArray();
            if (matches.Length != 1 || matches[0] <= 0)
                return false;
            quantity = matches[0];
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static int? InventoryCount(
        SnapshotEnvelope snapshot,
        string qualifiedItemId,
        int minimumQuality)
    {
        var inventory = StateValue(snapshot, "player", "inventory");
        if (!inventory.HasValue ||
            inventory.Value.ValueKind != JsonValueKind.Array)
            return null;
        return inventory.Value.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.Object &&
                String(value, "qualified_item_id") == qualifiedItemId &&
                Int(value, "quality") >= minimumQuality)
            .Sum(value => Math.Max(0, Int(value, "stack")));
    }

    private static int? DebrisCount(
        SnapshotEnvelope snapshot,
        string qualifiedItemId,
        int minimumQuality)
    {
        var debris = StateValue(snapshot, "current_location", "debris");
        if (!debris.HasValue || debris.Value.ValueKind != JsonValueKind.Array)
            return null;
        return debris.Value.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.Object &&
                String(value, "qualified_item_id") == qualifiedItemId &&
                Int(value, "item_quality") >= minimumQuality)
            .Sum(value => Math.Max(0, Int(value, "chunk_count")));
    }

    private static JsonElement? StateValue(
        SnapshotEnvelope snapshot,
        string section,
        string field)
    {
        if (!snapshot.State.TryGetValue(section, out var sectionValue) ||
            sectionValue.ValueKind != JsonValueKind.Object ||
            !sectionValue.TryGetProperty(field, out var envelope) ||
            envelope.ValueKind != JsonValueKind.Object ||
            !envelope.TryGetProperty("status", out var status) ||
            status.GetString() is not ("available" or "derived") ||
            !envelope.TryGetProperty("value", out var value))
        {
            return null;
        }
        return value;
    }

    private static string String(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private static int Int(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) &&
        property.TryGetInt32(out var result)
            ? result
            : 0;
}

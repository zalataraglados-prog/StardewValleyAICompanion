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

    private static string[] AppendDeferredNativeDropPickup(
        SmallModelPlanEnvelope plan,
        AcquisitionRouteTargetDateUnlock requirement,
        PolicyEventCandidatePrediction source,
        SnapshotEnvelope snapshot)
    {
        if (requirement.RouteKind != DeferredWildTreePickupKind)
            return Array.Empty<string>();

        if (source.OptionId != "foraging.chop_wild_tree" ||
            source.Kind != "clear_obstacle_tile" ||
            !source.TileX.HasValue ||
            !source.TileY.HasValue ||
            requirement.RequiredAmount <= 0 ||
            string.IsNullOrWhiteSpace(requirement.QualifiedItemId))
        {
            return new[] { "native_drop_deferred_pickup_source_invalid" };
        }

        var minimumOutputsJson = UniqueParameter(
            source,
            "tree_chop_guaranteed_minimum_outputs_json");
        if (!TryReadGuaranteedMinimum(
                minimumOutputsJson,
                requirement.QualifiedItemId,
                requirement.MinimumQuality,
                out var guaranteedMinimum) ||
            guaranteedMinimum < requirement.RequiredAmount)
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
            TargetTileX = source.TileX,
            TargetTileY = source.TileY,
            EstimatedMinutes = 0,
            Preconditions = new[]
            {
                "prior_native_wild_tree_chop_verified=true",
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
                    DeferredWildTreePickupKind),
                Parameter(
                    "deferred_pickup_debris_item_total_before",
                    debrisBefore.Value.ToString(
                        CultureInfo.InvariantCulture)),
                Parameter(
                    "deferred_pickup_guaranteed_minimum_quantity",
                    guaranteedMinimum.ToString(
                        CultureInfo.InvariantCulture)),
                Parameter(
                    "tree_chop_guaranteed_minimum_outputs_json",
                    minimumOutputsJson)
            }
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

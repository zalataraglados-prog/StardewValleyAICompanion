using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteDispatchCompilationBuilder
{
    private static bool TryReadMonsterDropGuaranteedMinimum(
        SnapshotEnvelope snapshot,
        int targetX,
        int targetY,
        PolicyEventCandidatePrediction source,
        AcquisitionRouteTargetDateUnlock requirement,
        out int guaranteedMinimum)
    {
        guaranteedMinimum = 0;
        const string sourcePrefix = "monster:";
        if (requirement.MinimumQuality != 0 ||
            requirement.RouteKind != DeferredMonsterDropPickupKind ||
            !requirement.SourceId.StartsWith(
                sourcePrefix,
                StringComparison.Ordinal) ||
            requirement.SourceId.Length == sourcePrefix.Length ||
            UniqueParameter(source, "source_match_status") !=
                "guaranteed_monster_drop" ||
            UniqueParameter(source, "target_drop_probability_status") !=
                "guaranteed_from_live_projection" ||
            !double.TryParse(
                UniqueParameter(source, "target_drop_chance_preview"),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var chance) ||
            chance != 1d ||
            UniqueParameter(source, "target_name") !=
                requirement.SourceId[sourcePrefix.Length..] ||
            !ContainsExactlyOnce(
                UniqueParameter(source, "expected_drop_qualified_item_ids"),
                requirement.QualifiedItemId))
        {
            return false;
        }

        var monsters = StateValue(snapshot, "mining", "monsters");
        if (!monsters.HasValue ||
            monsters.Value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        var matches = monsters.Value.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.Object &&
                Int(value, "tile_x") == targetX &&
                Int(value, "tile_y") == targetY &&
                String(value, "name") ==
                    requirement.SourceId[sourcePrefix.Length..] &&
                String(value, "runtime_identity") ==
                    UniqueParameter(source, "target_runtime_identity"))
            .ToArray();
        if (matches.Length != 1 ||
            !ArrayContainsExactlyOnce(
                matches[0],
                "guaranteed_drop_qualified_item_ids",
                requirement.QualifiedItemId) ||
            !RouteSourceExistsExactlyOnce(
                matches[0],
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

    private static bool ContainsExactlyOnce(string csv, string expected) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Count(value => value == expected) == 1;
}

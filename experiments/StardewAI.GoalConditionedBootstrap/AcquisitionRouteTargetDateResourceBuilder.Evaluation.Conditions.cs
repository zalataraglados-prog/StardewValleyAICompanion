using System.Globalization;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateResourceBuilder
{
    private static ResourceConditionSet EvaluateResourceConditions(
        AcquisitionRouteTargetDateFacility route,
        AcquisitionResourceInputSnapshotState state)
    {
        var conditions = route.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .PendingResourceConditions;
        if (conditions.Length == 0)
            return ResourceConditionSet.Match;
        var evaluations = new List<AcquisitionResourceInputEvaluation>();
        var misses = new List<string>();
        var blocks = new List<string>();
        foreach (var condition in conditions)
        {
            var tokens = condition.Split(' ',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);
            var negated = tokens.Length > 0 && tokens[0].StartsWith('!');
            var predicate = tokens.Length == 0
                ? string.Empty
                : negated ? tokens[0][1..] : tokens[0];
            if (predicate != "PLAYER_HAS_ITEM" ||
                tokens.Length is < 3 or > 5 ||
                (tokens.Length >= 4 && !int.TryParse(tokens[3],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out _)) ||
                (tokens.Length == 5 && !int.TryParse(tokens[4],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out _)))
            {
                blocks.Add("resource_condition_syntax_unsupported:" +
                    StableConditionToken(condition));
                continue;
            }
            var selector = tokens[1];
            var qualifiedItemId = QualifyObjectId(tokens[2]);
            if (qualifiedItemId is "(O)73" or "(O)858")
            {
                blocks.Add("resource_condition_native_currency_not_bound:" +
                    qualifiedItemId);
                continue;
            }
            var minimum = tokens.Length >= 4
                ? int.Parse(tokens[3], CultureInfo.InvariantCulture)
                : 1;
            var maximum = tokens.Length == 5
                ? int.Parse(tokens[4], CultureInfo.InvariantCulture)
                : int.MaxValue;
            if (minimum > maximum)
            {
                blocks.Add("resource_condition_count_range_invalid:" +
                    StableConditionToken(condition));
                continue;
            }
            var quantity = state.PlayerInventoryQuantity(
                selector,
                qualifiedItemId);
            if (!quantity.EvidenceAvailable)
            {
                blocks.Add(quantity.BlockingReason);
                continue;
            }
            var nativeResult = quantity.Quantity >= minimum &&
                quantity.Quantity <= maximum;
            var matches = negated ? !nativeResult : nativeResult;
            evaluations.Add(new AcquisitionResourceInputEvaluation(
                "non_consuming_player_inventory_condition",
                qualifiedItemId,
                0,
                quantity.Quantity,
                matches
                    ? "resolved_resource_input_match"
                    : "resolved_resource_input_miss",
                new[]
                {
                    "state.farm.material_inventory_graph.value." +
                    "inventory_nodes[player_inventory].slots[]"
                },
                Array.Empty<string>(),
                ResourceConditionBinding: new AcquisitionResourceConditionBinding(
                    condition,
                    predicate,
                    negated,
                    selector,
                    minimum,
                    maximum,
                    nativeResult)));
            if (!matches)
            {
                misses.Add("resource_condition_miss:" +
                    StableConditionToken(condition));
            }
        }
        if (blocks.Count > 0)
        {
            return new ResourceConditionSet(
                false,
                false,
                evaluations.ToArray(),
                Array.Empty<string>(),
                blocks.Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray());
        }
        return new ResourceConditionSet(
            true,
            misses.Count == 0,
            evaluations.ToArray(),
            misses.Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            Array.Empty<string>());
    }

    private static string StableConditionToken(string condition) =>
        condition.Replace(' ', '_').Replace(',', '_');

    private sealed record ResourceConditionSet(
        bool Resolved,
        bool Matches,
        AcquisitionResourceInputEvaluation[] Evaluations,
        string[] NonMatchingReasons,
        string[] BlockingReasons)
    {
        public static ResourceConditionSet Match { get; } = new(
            true,
            true,
            Array.Empty<AcquisitionResourceInputEvaluation>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }
}

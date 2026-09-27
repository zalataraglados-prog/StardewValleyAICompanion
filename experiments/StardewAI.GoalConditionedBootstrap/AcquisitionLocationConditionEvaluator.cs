namespace StardewAI.GoalConditionedBootstrap;

internal static class AcquisitionLocationConditionEvaluator
{
    public static bool Supports(string condition) =>
        Clauses(condition).All(IsSupportedLocationSeasonClause);

    public static AcquisitionLocationConditionEvaluation Evaluate(
        IEnumerable<string> conditions,
        string locationId,
        string effectiveSeason)
    {
        var clauses = conditions.SelectMany(Clauses).ToArray();
        if (clauses.Any(clause => !IsSupportedLocationSeasonClause(clause)))
        {
            return AcquisitionLocationConditionEvaluation.Blocked(
                "unsupported_location_condition");
        }
        if (clauses.Length == 0)
            return AcquisitionLocationConditionEvaluation.Match;
        if (string.IsNullOrWhiteSpace(effectiveSeason))
        {
            return AcquisitionLocationConditionEvaluation.Blocked(
                "target_location_effective_season_missing:" + locationId);
        }

        foreach (var clause in clauses)
        {
            var tokens = Tokens(clause);
            var negated = tokens[0].StartsWith('!');
            var nativeResult = tokens.Skip(2).Any(value => string.Equals(
                value,
                effectiveSeason,
                StringComparison.OrdinalIgnoreCase));
            if (negated ? nativeResult : !nativeResult)
            {
                return new AcquisitionLocationConditionEvaluation(
                    true,
                    false,
                    new[]
                    {
                        "state.locations.social_route_date_evidence.value.locations[].effective_season"
                    },
                    Array.Empty<string>());
            }
        }
        return new AcquisitionLocationConditionEvaluation(
            true,
            true,
            new[]
            {
                "state.locations.social_route_date_evidence.value.locations[].effective_season"
            },
            Array.Empty<string>());
    }

    private static string[] Clauses(string condition) =>
        string.IsNullOrWhiteSpace(condition)
            ? Array.Empty<string>()
            : condition.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

    private static bool IsSupportedLocationSeasonClause(string clause)
    {
        var tokens = Tokens(clause);
        if (tokens.Length < 3)
            return false;
        var predicate = tokens[0].StartsWith('!')
            ? tokens[0][1..]
            : tokens[0];
        return predicate == "LOCATION_SEASON" &&
            tokens[1] == "Target" &&
            tokens.Skip(2).All(value => value.ToLowerInvariant() is
                "spring" or "summer" or "fall" or "winter");
    }

    private static string[] Tokens(string clause) => clause.Split(
        ' ',
        StringSplitOptions.RemoveEmptyEntries |
        StringSplitOptions.TrimEntries);
}

internal sealed record AcquisitionLocationConditionEvaluation(
    bool Resolved,
    bool? Matches,
    string[] EvidencePaths,
    string[] BlockingReasons)
{
    public static AcquisitionLocationConditionEvaluation Match { get; } =
        new(true, true, Array.Empty<string>(), Array.Empty<string>());

    public static AcquisitionLocationConditionEvaluation Blocked(
        string reason) => new(
            false,
            null,
            Array.Empty<string>(),
            new[] { reason });
}

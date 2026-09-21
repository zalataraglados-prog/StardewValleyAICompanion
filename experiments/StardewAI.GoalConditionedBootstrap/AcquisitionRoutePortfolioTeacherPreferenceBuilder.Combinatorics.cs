using System.Numerics;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioTeacherPreferenceBuilder
{
    internal static BigInteger CountGroupCandidates(
        ResolvedScope scope,
        ICollection<string> reasons)
    {
        if (scope.SelectionRule == "all_required")
        {
            if (scope.RequiredAlternativeCount != scope.AlternativeCount)
            {
                reasons.Add("portfolio_teacher_selection_rule_invalid:" +
                    ScopeKey(scope.Scope));
                return BigInteger.Zero;
            }
            var count = BigInteger.One;
            foreach (var index in RemainingAlternatives(scope))
            {
                count *= scope.RoutesByAlternative
                    .GetValueOrDefault(index, Array.Empty<string>()).Length;
            }
            return count;
        }
        if (scope.SelectionRule != "choose_at_least_required_slots" ||
            scope.RequiredAlternativeCount <= 0 ||
            scope.RequiredAlternativeCount > scope.AlternativeCount)
        {
            reasons.Add("portfolio_teacher_selection_rule_invalid:" +
                ScopeKey(scope.Scope));
            return BigInteger.Zero;
        }
        var required = RemainingRequiredCount(scope);
        if (required <= 0)
        {
            reasons.Add("portfolio_teacher_continuation_scope_complete:" +
                ScopeKey(scope.Scope));
            return BigInteger.Zero;
        }
        var available = RemainingAlternatives(scope)
            .Where(index => scope.RoutesByAlternative
                .GetValueOrDefault(index, Array.Empty<string>()).Length > 0)
            .ToArray();
        var subsetCounts = new BigInteger[available.Length + 1];
        subsetCounts[0] = BigInteger.One;
        var visited = 0;
        foreach (var index in available)
        {
            var routeCount = scope.RoutesByAlternative[index].Length;
            visited++;
            for (var selectedCount = visited; selectedCount >= 1;
                 selectedCount--)
            {
                subsetCounts[selectedCount] +=
                    subsetCounts[selectedCount - 1] * routeCount;
            }
        }
        return subsetCounts
            .Skip(required)
            .Aggregate(BigInteger.Zero, (sum, count) => sum + count);
    }

    internal static string[][] EnumerateGroupRouteSelections(
        ResolvedScope scope)
    {
        var available = RemainingAlternatives(scope)
            .Where(index => scope.RoutesByAlternative
                .GetValueOrDefault(index, Array.Empty<string>()).Length > 0)
            .ToArray();
        IEnumerable<int[]> alternativeSelections =
            scope.SelectionRule == "all_required"
                ? new[] { RemainingAlternatives(scope) }
                : Enumerable.Range(
                        RemainingRequiredCount(scope),
                        available.Length -
                        RemainingRequiredCount(scope) + 1)
                    .SelectMany(selectedCount => Combinations(
                        available,
                        selectedCount));
        return alternativeSelections
            .SelectMany(indexes => CartesianProduct(indexes.Select(index =>
                    scope.RoutesByAlternative.GetValueOrDefault(
                        index,
                        Array.Empty<string>())).ToArray())
                .Select(routes => routes.ToArray()))
            .ToArray();
    }

    private static IEnumerable<T[]> CartesianProduct<T>(T[][] choices)
    {
        IEnumerable<T[]> result = new[] { Array.Empty<T>() };
        foreach (var options in choices)
        {
            result = result.SelectMany(prefix => options.Select(option =>
                prefix.Append(option).ToArray()));
        }
        return result;
    }

    private static IEnumerable<int[]> Combinations(int[] values, int count)
    {
        if (count == 0)
        {
            yield return Array.Empty<int>();
            yield break;
        }
        for (var index = 0; index <= values.Length - count; index++)
        {
            foreach (var suffix in Combinations(
                         values[(index + 1)..],
                         count - 1))
            {
                yield return new[] { values[index] }.Concat(suffix).ToArray();
            }
        }
    }

    private static int[] RemainingAlternatives(ResolvedScope scope)
    {
        var completed = (scope.CompletedAlternativeIndices ??
                Array.Empty<int>())
            .ToHashSet();
        return Enumerable.Range(0, scope.AlternativeCount)
            .Where(index => !completed.Contains(index))
            .ToArray();
    }

    private static int RemainingRequiredCount(ResolvedScope scope) =>
        scope.RemainingRequiredAlternativeCount ??
        scope.RequiredAlternativeCount;
}

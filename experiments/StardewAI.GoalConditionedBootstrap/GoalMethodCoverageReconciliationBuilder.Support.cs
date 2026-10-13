namespace StardewAI.GoalConditionedBootstrap;

public static partial class GoalMethodCoverageReconciliationBuilder
{
    private static Dictionary<string, HashSet<string>> BuildOptionRoles(
        GoalMethodFrontierMethod method)
    {
        var roles = new Dictionary<string, HashSet<string>>(
            StringComparer.Ordinal);
        AddRoles(roles, method.PermittedOptionIds, "direct");
        AddRoles(roles, method.DependencyOptionIds, "policy_dependency");
        AddRoles(roles, method.DeterministicDependencyOptionIds,
            "deterministic_dependency");
        return roles;
    }

    private static void AddRoles(
        IDictionary<string, HashSet<string>> roles,
        IEnumerable<string> optionIds,
        string role)
    {
        foreach (var optionId in optionIds)
        {
            if (!roles.TryGetValue(optionId, out var optionRoles))
            {
                optionRoles = new HashSet<string>(StringComparer.Ordinal);
                roles.Add(optionId, optionRoles);
            }
            optionRoles.Add(role);
        }
    }

    private static string[] ImplementedSourceKinds(
        GoalMethodFrontierMethod method)
    {
        var kinds = new List<string>();
        if (method.RequirementSetReadiness.Length > 0)
        {
            kinds.Add(GoalMethodTeacherCoverageSourceKinds
                .AcquisitionRoutePortfolioCorpus);
        }
        if (method.DirectionId == "earn_pet_love")
        {
            kinds.Add(GoalMethodTeacherCoverageSourceKinds
                .PetLoveTerminalInteractionCorpus);
        }
        if (method.DirectionId == "obtain_skull_key")
        {
            kinds.Add(GoalMethodTeacherCoverageSourceKinds
                .SkullKeyTerminalInteractionCorpus);
        }
        if (method.DirectionId == "earn_money")
        {
            kinds.Add(GoalMethodTeacherCoverageSourceKinds
                .MoneyThresholdProgressionCorpus);
        }
        return kinds.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static GoalMethodCoverageDispositionCount[] CountDispositions(
        IEnumerable<string> dispositions) => dispositions
        .GroupBy(value => value, StringComparer.Ordinal)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .Select(group => new GoalMethodCoverageDispositionCount(
            group.Key,
            group.Count()))
        .ToArray();

    private static void ValidateSharedAuthority(
        GoalMethodFrontierReport frontier,
        GoalMethodTeacherCoverageReport coverage)
    {
        if (frontier.GoalId != coverage.GoalId ||
            frontier.TargetScore != coverage.TargetScore ||
            frontier.CriterionCount != coverage.CriterionDenominatorCount ||
            frontier.OptionMatrixSha256 != coverage.OptionMatrixSha256 ||
            frontier.DependencyExpansionSha256 !=
                coverage.DependencyExpansionSha256 ||
            coverage.FormalProductTrainingAuthorized)
        {
            throw new InvalidDataException(
                "Coverage reconciliation inputs do not share one authority.");
        }
    }

    private static GoalMethodTeacherCriterionCoverage AggregateCoverage(
        GoalMethodFrontierMethod method,
        GoalMethodTeacherCriterionCoverage[] rows)
    {
        if (rows.Length == 0 ||
            rows.Length != method.CriterionIds.Length ||
            rows.Select(row => row.CriterionId)
                .Distinct(StringComparer.Ordinal).Count() != rows.Length ||
            !rows.Select(row => row.CriterionId)
                .Order(StringComparer.Ordinal)
                .SequenceEqual(
                    method.CriterionIds.Order(StringComparer.Ordinal),
                    StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Method coverage does not contain its exact criterion denominator: " +
                method.MethodId);
        }

        var first = rows[0];
        if (rows.Any(row =>
                row.MethodId != first.MethodId ||
                row.DirectionId != first.DirectionId ||
                row.MethodStatus != first.MethodStatus))
        {
            throw new InvalidDataException(
                "Criteria sharing one method have inconsistent authority.");
        }

        var comparisonPartitions = IntersectPartitions(rows.Select(row =>
            row.TeacherComparisonPartitions));
        var outcomePartitions = IntersectPartitions(rows.Select(row =>
            row.NativeOutcomePartitions));
        var splitComplete = HasAllPartitions(comparisonPartitions) &&
            HasAllPartitions(outcomePartitions);
        return new GoalMethodTeacherCriterionCoverage(
            first.CriterionId,
            rows.Sum(row => row.Points),
            first.DirectionId,
            first.MethodId,
            first.MethodStatus,
            rows.SelectMany(row => row.RequirementSetIds)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            rows.SelectMany(row => row.TeacherSourceKinds)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            comparisonPartitions,
            outcomePartitions,
            comparisonPartitions.Length > 0,
            outcomePartitions.Length > 0,
            splitComplete,
            rows.All(row => row.CoverageGateReady),
            rows.SelectMany(row => row.BlockingReasons)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    private static string[] IntersectPartitions(
        IEnumerable<string[]> partitionSets)
    {
        HashSet<string>? intersection = null;
        foreach (var partitions in partitionSets)
        {
            if (intersection is null)
                intersection = partitions.ToHashSet(StringComparer.Ordinal);
            else
                intersection.IntersectWith(partitions);
        }
        return RequiredPartitions.Where(partition =>
                intersection?.Contains(partition) == true)
            .ToArray();
    }
}

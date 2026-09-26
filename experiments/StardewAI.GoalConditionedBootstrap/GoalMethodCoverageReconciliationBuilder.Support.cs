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

    private static GoalMethodTeacherCriterionCoverage ConsistentCoverage(
        GoalMethodTeacherCriterionCoverage[] rows)
    {
        var first = rows[0];
        if (rows.Any(row =>
                row.MethodId != first.MethodId ||
                row.DirectionId != first.DirectionId ||
                row.MethodStatus != first.MethodStatus ||
                !row.TeacherSourceKinds.SequenceEqual(
                    first.TeacherSourceKinds, StringComparer.Ordinal) ||
                !row.TeacherComparisonPartitions.SequenceEqual(
                    first.TeacherComparisonPartitions,
                    StringComparer.Ordinal) ||
                !row.NativeOutcomePartitions.SequenceEqual(
                    first.NativeOutcomePartitions,
                    StringComparer.Ordinal) ||
                row.CoverageGateReady != first.CoverageGateReady))
        {
            throw new InvalidDataException(
                "Criteria sharing one method have inconsistent coverage.");
        }
        return first;
    }
}

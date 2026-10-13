namespace StardewAI.GoalConditionedBootstrap;

using StardewAI.Core.Training;

internal static class GoalMethodFamilyProjectionBuilder
{
    internal static GoalMethodFamilyProjection[] Build(
        string knowledgePath,
        GoalMethodFrontierReport frontier,
        GoalMethodCoverageCriterionReconciliation[] coverage)
    {
        var knowledge = KnowledgeIndex.Load(knowledgePath);
        if (knowledge.Sha256 != frontier.KnowledgeSha256 ||
            knowledge.Criteria.Length != frontier.CriterionCount)
        {
            throw new InvalidDataException(
                "Method-family projection knowledge identity drifted.");
        }

        var criteria = knowledge.Criteria.ToDictionary(
            criterion => criterion.Id,
            StringComparer.Ordinal);
        var coverageById = coverage.ToDictionary(
            criterion => criterion.CriterionId,
            StringComparer.Ordinal);
        var progressionFamilyByDirection = GrandpaDirectionCatalog.Entries
            .ToDictionary(
                entry => entry.DirectionId,
                entry => string.IsNullOrWhiteSpace(entry.ProgressionFamilyId)
                    ? throw new InvalidDataException(
                        "Direction has no progression family: " +
                        entry.DirectionId)
                    : entry.ProgressionFamilyId,
                StringComparer.Ordinal);
        var families = frontier.Methods
            .GroupBy(
                method => progressionFamilyByDirection.TryGetValue(
                    method.DirectionId,
                    out var familyId)
                        ? familyId
                        : throw new InvalidDataException(
                            "Unknown direction in method frontier: " +
                            method.DirectionId),
                StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => BuildFamily(
                group.Key,
                group.OrderBy(method => method.MethodId,
                    StringComparer.Ordinal).ToArray(),
                criteria,
                coverageById))
            .ToArray();
        var projected = families.SelectMany(family =>
                family.CriterionViews.Select(view => view.CriterionId))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var authoritative = knowledge.Criteria.Select(criterion =>
                criterion.Id)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!projected.SequenceEqual(authoritative, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Method-family projection did not cover the authoritative " +
                "criterion denominator exactly once.");
        }
        return families;
    }

    private static GoalMethodFamilyProjection BuildFamily(
        string familyId,
        GoalMethodFrontierMethod[] methods,
        IReadOnlyDictionary<string, GoalCriterion> criteria,
        IReadOnlyDictionary<string,
            GoalMethodCoverageCriterionReconciliation> coverage)
    {
        var rows = methods.SelectMany(method => method.CriterionIds)
            .Distinct(StringComparer.Ordinal)
            .Select(id => criteria.TryGetValue(id, out var criterion) &&
                    coverage.TryGetValue(id, out var criterionCoverage)
                ? new CriterionContext(criterion, criterionCoverage)
                : throw new InvalidDataException(
                    "Method family references an unknown criterion: " + id))
            .ToArray();
        var monotone = IsMonotoneThresholdFamily(methods, rows);
        var ordered = monotone
            ? rows.OrderBy(row => ParseThreshold(row.Criterion.Target))
                .ThenBy(row => row.Criterion.Id, StringComparer.Ordinal)
                .ToArray()
            : rows.OrderBy(row => row.Criterion.Id, StringComparer.Ordinal)
                .ToArray();
        var views = ordered.Select((row, index) =>
                new GoalMethodCriterionView(
                    row.Criterion.Id,
                    row.Criterion.Points,
                    row.Criterion.StatePaths.Order(StringComparer.Ordinal)
                        .ToArray(),
                    row.Criterion.Operation,
                    row.Criterion.Target,
                    index,
                    "grandpa.criterion:" + row.Criterion.Id + ":" +
                    row.Criterion.Operation + ":" + row.Criterion.Target,
                    string.Join(",", row.Criterion.StatePaths
                        .Order(StringComparer.Ordinal)) + " " +
                    row.Criterion.Operation + " " + row.Criterion.Target,
                    row.Coverage.CoverageGateReady))
            .ToArray();
        var familyKind = monotone
            ? "monotone_threshold"
            : views.Length > 1
                ? "shared_progression"
                : "singleton";
        return new GoalMethodFamilyProjection(
            familyId,
            familyKind,
            methods.Select(method => method.MethodId)
                .Order(StringComparer.Ordinal).ToArray(),
            methods.Select(method => method.DirectionId)
                .Order(StringComparer.Ordinal).ToArray(),
            methods.SelectMany(method => method.PermittedOptionIds)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray(),
            views,
            views.Length > 1,
            views.Length > 1,
            1,
            views.Length);
    }

    private static bool IsMonotoneThresholdFamily(
        GoalMethodFrontierMethod[] methods,
        CriterionContext[] rows)
    {
        if (methods.Length != 1 || rows.Length < 2 ||
            rows.Select(row => row.Criterion.Operation)
                .Distinct(StringComparer.Ordinal).Count() != 1 ||
            rows.Select(row => string.Join("\n", row.Criterion.StatePaths
                    .Order(StringComparer.Ordinal)))
                .Distinct(StringComparer.Ordinal).Count() != 1 ||
            !rows.All(row => long.TryParse(
                row.Criterion.Target,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out _)))
        {
            return false;
        }
        var operation = rows[0].Criterion.Operation;
        return operation == "gte" ||
            operation.StartsWith("count_", StringComparison.Ordinal);
    }

    private static long ParseThreshold(string value) =>
        long.Parse(
            value,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture);

    private sealed record CriterionContext(
        GoalCriterion Criterion,
        GoalMethodCoverageCriterionReconciliation Coverage);
}

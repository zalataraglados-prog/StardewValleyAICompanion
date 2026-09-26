namespace StardewAI.GoalConditionedBootstrap;

public static partial class GoalMethodCoverageReconciliationBuilder
{
    private static string PrimaryDisposition(
        GoalMethodFrontierMethod method,
        GoalMethodTeacherCriterionCoverage coverage,
        string[] implementedSources,
        string[] activeSources,
        bool coverageReady)
    {
        if (coverageReady)
            return GoalMethodCoverageDispositions.CoverageReady;
        if (method.Status == "blocked_by_option_governance")
            return GoalMethodCoverageDispositions.OptionGovernanceGap;
        if (method.Status == "pending_dependency_expansion")
            return GoalMethodCoverageDispositions.DependencyGraphIncomplete;
        if (implementedSources.Length == 0)
        {
            return GoalMethodCoverageDispositions
                .TeacherSourceAdapterMissing;
        }
        if (!implementedSources.Intersect(
                activeSources,
                StringComparer.Ordinal).Any())
        {
            return GoalMethodCoverageDispositions.EvidenceNotConnected;
        }
        if (!HasAllPartitions(coverage.TeacherComparisonPartitions))
            return GoalMethodCoverageDispositions.TeacherComparisonMissing;
        if (coverage.NativeOutcomePartitions.Length == 0)
        {
            return GoalMethodCoverageDispositions
                .NativeSplitEvidenceMissing;
        }
        return GoalMethodCoverageDispositions.SplitEvidenceIncomplete;
    }

    private static string[] OpenWorkKinds(
        GoalMethodFrontierMethod method,
        GoalMethodTeacherCriterionCoverage coverage,
        string[] implementedSources,
        string[] activeSources)
    {
        var result = new List<string>();
        if (method.Status == "pending_dependency_expansion")
        {
            result.Add(GoalMethodCoverageDispositions
                .DependencyGraphIncomplete);
        }
        if (method.Status == "blocked_by_option_governance")
        {
            result.Add(GoalMethodCoverageDispositions.OptionGovernanceGap);
        }
        if (implementedSources.Length == 0)
        {
            result.Add(GoalMethodCoverageDispositions
                .TeacherSourceAdapterMissing);
        }
        else if (!implementedSources.Intersect(
                     activeSources,
                     StringComparer.Ordinal).Any())
        {
            result.Add(GoalMethodCoverageDispositions.EvidenceNotConnected);
        }
        if (!HasAllPartitions(coverage.TeacherComparisonPartitions))
        {
            result.Add(GoalMethodCoverageDispositions
                .TeacherComparisonMissing);
        }
        if (!HasAllPartitions(coverage.NativeOutcomePartitions))
        {
            result.Add(GoalMethodCoverageDispositions
                .NativeSplitEvidenceMissing);
        }
        return result.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] NextActions(
        GoalMethodFrontierMethod method,
        GoalMethodTeacherCriterionCoverage coverage,
        string[] implementedSources,
        string[] activeSources)
    {
        var result = new List<string>();
        result.AddRange(method.UnexpandedRequirements.Select(blocker =>
            "close_dependency_blocker:" + blocker));
        if (implementedSources.Length == 0)
        {
            result.Add("implement_typed_teacher_source_adapter:" +
                method.DirectionId);
        }
        else
        {
            foreach (var kind in implementedSources.Except(
                         activeSources,
                         StringComparer.Ordinal))
            {
                result.Add("connect_production_teacher_source:" + kind);
            }
        }
        var missingComparisons = MissingPartitions(
            coverage.TeacherComparisonPartitions);
        if (missingComparisons.Length > 0)
        {
            result.Add("collect_explicit_teacher_comparisons:" +
                string.Join(",", missingComparisons));
        }
        var missingOutcomes = MissingPartitions(
            coverage.NativeOutcomePartitions);
        if (missingOutcomes.Length > 0)
        {
            result.Add("collect_verified_native_outcomes:" +
                string.Join(",", missingOutcomes));
        }
        return result.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool HasAllPartitions(IEnumerable<string> partitions)
    {
        var available = partitions.ToHashSet(StringComparer.Ordinal);
        return RequiredPartitions.All(available.Contains);
    }

    private static string[] MissingPartitions(IEnumerable<string> partitions)
    {
        var available = partitions.ToHashSet(StringComparer.Ordinal);
        return RequiredPartitions.Where(partition =>
                !available.Contains(partition))
            .ToArray();
    }
}

using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class GoalMethodTeacherCoverageBuilder
{
    private static readonly string[] RequiredPartitions =
    {
        StardewAI.Contracts.Training.PolicyDatasetPartitions.Train,
        StardewAI.Contracts.Training.PolicyDatasetPartitions.Validation,
        StardewAI.Contracts.Training.PolicyDatasetPartitions.Test
    };

    public static GoalMethodTeacherCoverageReport Build(
        GoalMethodFrontierBuildInputs inputs,
        string requestPath)
    {
        var fullRequestPath = RequiredFullPath(requestPath, "Coverage request");
        var request = CurrentTeacherFrontierSupport.Read<
            GoalMethodTeacherCoverageRequest>(
            fullRequestPath,
            "Goal-method Teacher coverage request");
        ValidateRequest(request);
        var frontier = GoalMethodFrontierBuilder.Build(
            inputs.ExpansionPath,
            inputs.DependencyExpansionPath,
            inputs.IsolatedTrainingAuthorizationPath,
            inputs.RequirementInventoryPath,
            inputs.AcquisitionLoweringPath,
            inputs.AcquisitionLoweringCatalogPath,
            inputs.KnowledgePath,
            inputs.OptionMatrixPath,
            inputs.ClaimLedgerPath,
            inputs.DirectionCatalogSourcePath);
        ValidateFrontier(frontier);

        var evidence = frontier.Methods.ToDictionary(
            method => method.MethodId,
            method => new MethodCoverageEvidence(method),
            StringComparer.Ordinal);
        var sourceDigests = request.Sources
            .OrderBy(source => source.SourceId, StringComparer.Ordinal)
            .Select(source => VerifySource(source, frontier, evidence))
            .ToArray();
        var methods = frontier.Methods.ToDictionary(
            method => method.MethodId,
            StringComparer.Ordinal);
        var catalog = GrandpaDirectionCatalog.Entries.ToDictionary(
            entry => entry.DirectionId,
            StringComparer.Ordinal);
        var criteria = frontier.Criteria
            .OrderBy(criterion => criterion.CriterionId, StringComparer.Ordinal)
            .Select(criterion =>
            {
                if (criterion.MethodIds.Length != 1 ||
                    !methods.TryGetValue(criterion.MethodIds[0], out var method) ||
                    !catalog.TryGetValue(method.DirectionId, out var direction) ||
                    !direction.CriterionIds.Contains(
                        criterion.CriterionId,
                        StringComparer.Ordinal))
                {
                    throw new InvalidDataException(
                        "Goal-method criterion does not have one catalog-backed method: " +
                        criterion.CriterionId);
                }
                var row = evidence[method.MethodId];
                var comparisonPartitions = OrderedPartitions(
                    row.TeacherComparisonPartitions);
                var outcomePartitions = OrderedPartitions(
                    row.NativeOutcomePartitions);
                var teacherSourceKinds = row.TeacherSourceKinds
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                var comparisonCovered = comparisonPartitions.Length > 0;
                var outcomeCovered = outcomePartitions.Length > 0;
                var splitComplete = RequiredPartitions.All(partition =>
                    row.TeacherComparisonPartitions.Contains(partition) &&
                    row.NativeOutcomePartitions.Contains(partition));
                var blockers = new List<string>();
                if (method.Status != "executable_frontier")
                {
                    blockers.Add(
                        "goal_method_not_executable:" + method.Status);
                }
                if (teacherSourceKinds.Length == 0)
                {
                    blockers.Add(
                        "goal_method_teacher_source_adapter_missing");
                }
                if (!comparisonCovered)
                    blockers.Add("explicit_teacher_comparison_missing");
                else if (!RequiredPartitions.All(
                             row.TeacherComparisonPartitions.Contains))
                {
                    blockers.Add(
                        "explicit_teacher_comparison_split_incomplete");
                }
                if (!outcomeCovered)
                    blockers.Add("verified_native_outcome_missing");
                else if (!RequiredPartitions.All(
                             row.NativeOutcomePartitions.Contains))
                {
                    blockers.Add("verified_native_outcome_split_incomplete");
                }
                var ready = method.Status == "executable_frontier" &&
                    teacherSourceKinds.Length > 0 &&
                    splitComplete;
                return new GoalMethodTeacherCriterionCoverage(
                    criterion.CriterionId,
                    criterion.Points,
                    method.DirectionId,
                    method.MethodId,
                    method.Status,
                    method.RequirementSetReadiness
                        .Select(requirement => requirement.RequirementSetId)
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)
                        .ToArray(),
                    teacherSourceKinds,
                    comparisonPartitions,
                    outcomePartitions,
                    comparisonCovered,
                    outcomeCovered,
                    splitComplete,
                    ready,
                    blockers.Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)
                        .ToArray());
            })
            .ToArray();
        var satisfied = criteria.Length == 19 &&
            criteria.All(criterion => criterion.CoverageGateReady);
        var blockingReasons = satisfied
            ? Array.Empty<string>()
            : new[]
            {
                "goal_method_teacher_coverage_incomplete:" +
                criteria.Count(criterion => criterion.CoverageGateReady) +
                "/" + criteria.Length
            };
        return new GoalMethodTeacherCoverageReport
        {
            Status = satisfied
                ? "ready_complete_goal_method_teacher_coverage"
                : "blocked_incomplete_goal_method_teacher_coverage",
            CoverageId = request.CoverageId,
            GoalId = frontier.GoalId,
            TargetScore = frontier.TargetScore,
            CriterionPointSum = criteria.Sum(criterion => criterion.Points),
            RequestSha256 = CurrentTeacherFrontierSupport.HashFile(
                fullRequestPath),
            FrontierStatus = frontier.Status,
            KnowledgeSha256 = frontier.KnowledgeSha256,
            DirectionCatalogSha256 = frontier.DirectionCatalogSha256,
            ExpansionOverlaySha256 = frontier.ExpansionOverlaySha256,
            DependencyExpansionSha256 =
                frontier.DependencyExpansionSha256,
            IsolatedTrainingAuthorizationSha256 =
                frontier.IsolatedTrainingAuthorizationSha256,
            RequirementInventorySha256 =
                frontier.RequirementInventorySha256,
            AcquisitionRouteLoweringSha256 =
                frontier.AcquisitionRouteLoweringSha256,
            AcquisitionRouteLoweringCatalogSha256 =
                frontier.AcquisitionRouteLoweringCatalogSha256,
            OptionMatrixSha256 = frontier.OptionMatrixSha256,
            ClaimLedgerSha256 = frontier.ClaimLedgerSha256,
            Sources = sourceDigests,
            CriterionDenominatorCount = criteria.Length,
            CatalogMappedCriterionCount = criteria.Length,
            ExecutableCriterionCount = criteria.Count(criterion =>
                criterion.MethodStatus == "executable_frontier"),
            TeacherComparisonCoveredCriterionCount = criteria.Count(
                criterion => criterion.TeacherComparisonCovered),
            NativeOutcomeCoveredCriterionCount = criteria.Count(
                criterion => criterion.NativeOutcomeCovered),
            SplitCompleteTeacherCriterionCount = criteria.Count(
                criterion => criterion.SplitCoverageComplete),
            CoverageGateReadyCriterionCount = criteria.Count(
                criterion => criterion.CoverageGateReady),
            Criteria = criteria,
            CoverageGateSatisfied = satisfied,
            FormalProductTrainingAuthorized = false,
            BlockingReasons = blockingReasons
        };
    }

    private static void ValidateRequest(
        GoalMethodTeacherCoverageRequest request)
    {
        if (request.SchemaVersion !=
                "goal_method_teacher_coverage_request.v1" ||
            string.IsNullOrWhiteSpace(request.CoverageId) ||
            request.Sources.Length == 0 ||
            request.FormalProductTrainingAuthorized ||
            request.Sources.Any(source =>
                string.IsNullOrWhiteSpace(source.SourceId) ||
                string.IsNullOrWhiteSpace(source.SourceKind) ||
                string.IsNullOrWhiteSpace(source.ArtifactPath)) ||
            request.Sources.Select(source => source.SourceId)
                .Distinct(StringComparer.Ordinal).Count() !=
                request.Sources.Length)
        {
            throw new InvalidDataException(
                "Goal-method Teacher coverage request is invalid.");
        }
    }

    private static void ValidateFrontier(GoalMethodFrontierReport frontier)
    {
        var catalog = GrandpaDirectionCatalog.Entries;
        var catalogCriteria = catalog.SelectMany(entry => entry.CriterionIds)
            .ToArray();
        if (frontier.SchemaVersion != "goal_method_frontier_graph.v3" ||
            frontier.GoalId != GoalMethodTeacherCoverageGoalIds.Authoritative ||
            frontier.TargetScore != 21 ||
            frontier.CriterionCount != 19 ||
            frontier.Criteria.Length != frontier.CriterionCount ||
            frontier.Criteria.Sum(criterion => criterion.Points) != 21 ||
            frontier.Methods.Length != catalog.Length ||
            catalogCriteria.Length != 19 ||
            catalogCriteria.Distinct(StringComparer.Ordinal).Count() != 19 ||
            !frontier.Criteria.Select(criterion => criterion.CriterionId)
                .Order(StringComparer.Ordinal)
                .SequenceEqual(
                    catalogCriteria.Order(StringComparer.Ordinal),
                    StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Authoritative goal-method frontier is not the complete 19-criterion denominator.");
        }
    }

    private static GoalMethodTeacherCoverageSourceDigest VerifySource(
        GoalMethodTeacherCoverageSource source,
        GoalMethodFrontierReport frontier,
        IReadOnlyDictionary<string, MethodCoverageEvidence> evidence)
    {
        var fullPath = RequiredFullPath(
            source.ArtifactPath,
            "Coverage source artifact");
        return source.SourceKind switch
        {
            GoalMethodTeacherCoverageSourceKinds
                .AcquisitionRoutePortfolioCorpus =>
                VerifyAcquisitionCorpus(
                    source,
                    fullPath,
                    frontier,
                    evidence),
            _ => throw new InvalidDataException(
                "Unsupported goal-method Teacher coverage source kind: " +
                source.SourceKind)
        };
    }

    private static string[] OrderedPartitions(IReadOnlySet<string> values) =>
        RequiredPartitions.Where(values.Contains).ToArray();

    private static string RequiredFullPath(string value, string label) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException(label + " is required.")
            : Path.GetFullPath(value);

    private sealed class MethodCoverageEvidence
    {
        public MethodCoverageEvidence(GoalMethodFrontierMethod method)
        {
            Method = method;
        }

        public GoalMethodFrontierMethod Method { get; }
        public HashSet<string> TeacherComparisonPartitions { get; } =
            new(StringComparer.Ordinal);
        public HashSet<string> NativeOutcomePartitions { get; } =
            new(StringComparer.Ordinal);
        public HashSet<string> TeacherSourceKinds { get; } =
            new(StringComparer.Ordinal);
    }
}

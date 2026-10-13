namespace StardewAI.GoalConditionedBootstrap;

public static partial class GoalMethodCoverageReconciliationBuilder
{
    private static readonly string[] RequiredPartitions =
    {
        StardewAI.Contracts.Training.PolicyDatasetPartitions.Train,
        StardewAI.Contracts.Training.PolicyDatasetPartitions.Validation,
        StardewAI.Contracts.Training.PolicyDatasetPartitions.Test
    };

    public static GoalMethodCoverageReconciliationReport Build(
        GoalMethodFrontierBuildInputs inputs,
        string coverageRequestPath)
    {
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
        var coverage = GoalMethodTeacherCoverageBuilder.Build(
            inputs,
            coverageRequestPath,
            frontier);
        ValidateSharedAuthority(frontier, coverage);
        var options = LoadOptions(
            inputs.OptionMatrixPath,
            frontier.OptionMatrixSha256);
        var criteriaById = frontier.Criteria.ToDictionary(
            criterion => criterion.CriterionId,
            StringComparer.Ordinal);
        var frontierMethodsById = frontier.Methods.ToDictionary(
            method => method.MethodId,
            StringComparer.Ordinal);
        var coverageByMethod = coverage.Criteria
            .GroupBy(criterion => criterion.MethodId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray(),
                StringComparer.Ordinal);

        var methods = frontier.Methods
            .OrderBy(method => method.MethodId, StringComparer.Ordinal)
            .Select(method => ReconcileMethod(
                method,
                coverageByMethod[method.MethodId],
                criteriaById,
                options))
            .ToArray();
        var methodsById = methods.ToDictionary(
            method => method.MethodId,
            StringComparer.Ordinal);
        var criteria = coverage.Criteria
            .OrderBy(criterion => criterion.CriterionId, StringComparer.Ordinal)
            .Select(criterion =>
            {
                var method = methodsById[criterion.MethodId];
                var frontierMethod = frontierMethodsById[criterion.MethodId];
                var implementedSources = ImplementedSourceKinds(
                    frontierMethod);
                var activeSources = criterion.TeacherSourceKinds
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                return new GoalMethodCoverageCriterionReconciliation(
                    criterion.CriterionId,
                    criterion.Points,
                    criterion.DirectionId,
                    criterion.MethodId,
                    criterion.MethodStatus,
                    PrimaryDisposition(
                        frontierMethod,
                        criterion,
                        implementedSources,
                        activeSources,
                        criterion.CoverageGateReady),
                    criterion.CoverageGateReady,
                    OpenWorkKinds(
                        frontierMethod,
                        criterion,
                        implementedSources,
                        activeSources));
            })
            .ToArray();
        var families = GoalMethodFamilyProjectionBuilder.Build(
            inputs.KnowledgePath,
            frontier,
            criteria);

        return new GoalMethodCoverageReconciliationReport
        {
            Status = criteria.All(criterion => criterion.CoverageGateReady)
                ? "reconciled_coverage_complete"
                : "reconciled_open_work",
            GoalId = frontier.GoalId,
            TargetScore = frontier.TargetScore,
            CriterionDenominatorCount = criteria.Length,
            RootMethodCount = methods.Length,
            ReferencedOptionCount = methods
                .SelectMany(method => method.ReferencedOptions)
                .Select(option => option.OptionId)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            ExecutableCriterionCount = coverage.ExecutableCriterionCount,
            CoverageGateReadyCriterionCount =
                coverage.CoverageGateReadyCriterionCount,
            FrontierStatus = frontier.Status,
            TeacherCoverageStatus = coverage.Status,
            FormalProductTrainingAuthorized = false,
            OptionMatrixSha256 = frontier.OptionMatrixSha256,
            CriterionDispositionCounts = CountDispositions(
                criteria.Select(criterion => criterion.PrimaryDisposition)),
            MethodDispositionCounts = CountDispositions(
                methods.Select(method => method.PrimaryDisposition)),
            MethodFamilies = families,
            Efficiency = new GoalMethodCoverageEfficiency
            {
                FrontierBuildCount = 1,
                PreviousFrontierBuildCount = 2,
                CriterionViewCount = criteria.Length,
                RootMethodCount = methods.Length,
                ProgressionFamilyCount = families.Length,
                MonotoneThresholdFamilyCount = families.Count(family =>
                    family.FamilyKind == "monotone_threshold"),
                TheoreticalIndependentProgressionBuildCount =
                    criteria.Length,
                SharedProgressionBuildCount = families.Length,
                TheoreticalProgressionBuildsAvoided =
                    criteria.Length - families.Length
            },
            Methods = methods,
            Criteria = criteria
        };
    }

    private static GoalMethodCoverageMethodReconciliation ReconcileMethod(
        GoalMethodFrontierMethod method,
        GoalMethodTeacherCriterionCoverage[] criterionCoverage,
        IReadOnlyDictionary<string, GoalCriterionFrontier> criteriaById,
        IReadOnlyDictionary<string, OptionEvidence> options)
    {
        var coverage = AggregateCoverage(method, criterionCoverage);
        var roles = BuildOptionRoles(method);
        var optionRows = roles
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair =>
            {
                if (!options.TryGetValue(pair.Key, out var option))
                {
                    throw new InvalidDataException(
                        "Reconciliation references an unknown option: " +
                        pair.Key);
                }
                return new GoalMethodCoverageOptionReconciliation(
                    option.OptionId,
                    pair.Value.Order(StringComparer.Ordinal).ToArray(),
                    option.TrainingEligibility,
                    option.RuntimeStatus,
                    option.ProductStatus,
                    option.TransparentReadGateReady,
                    option.FiveGateTrainingReady,
                    option.InternalExecutionPipelineSupported,
                    option.ProductExecutorSupported,
                    option.EvidenceIds,
                    option.TrainingExclusionReasons);
            })
            .ToArray();
        var implementedSources = ImplementedSourceKinds(method);
        var activeSources = coverage.TeacherSourceKinds
            .Order(StringComparer.Ordinal)
            .ToArray();
        var transparentGaps = optionRows
            .Where(option => !option.TransparentReadGateReady)
            .Select(option => option.OptionId)
            .ToArray();
        var runtimeGaps = optionRows
            .Where(option => option.RuntimeStatus is not
                ("RuntimeVerified" or "LongDurationVerified"))
            .Select(option => option.OptionId)
            .ToArray();
        var productGaps = optionRows
            .Where(option => !option.ProductExecutorSupported)
            .Select(option => option.OptionId)
            .ToArray();
        var coverageReady = method.CriterionIds.All(criterionId =>
                criteriaById.ContainsKey(criterionId)) &&
            criterionCoverage.Length == method.CriterionIds.Length &&
            criterionCoverage.All(row => row.CoverageGateReady);
        var primary = PrimaryDisposition(
            method,
            coverage,
            implementedSources,
            activeSources,
            coverageReady);
        var openWork = OpenWorkKinds(
            method,
            coverage,
            implementedSources,
            activeSources);
        var nextActions = NextActions(
            method,
            coverage,
            implementedSources,
            activeSources);

        return new GoalMethodCoverageMethodReconciliation(
            method.MethodId,
            method.DirectionId,
            method.CriterionIds.Order(StringComparer.Ordinal).ToArray(),
            method.CriterionIds.Sum(id => criteriaById[id].Points),
            method.Status,
            primary,
            method.UnexpandedRequirements.Order(StringComparer.Ordinal)
                .ToArray(),
            method.RequirementSetReadiness
                .Select(requirement => requirement.RequirementSetId)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            implementedSources,
            activeSources,
            coverage.TeacherComparisonPartitions,
            coverage.NativeOutcomePartitions,
            optionRows,
            transparentGaps.Length == 0,
            runtimeGaps.Length == 0,
            optionRows.All(option => option.FiveGateTrainingReady),
            optionRows.All(option =>
                option.InternalExecutionPipelineSupported),
            productGaps.Length == 0,
            transparentGaps,
            runtimeGaps,
            productGaps,
            false,
            optionRows.SelectMany(option => option.EvidenceIds)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            openWork,
            nextActions);
    }

}

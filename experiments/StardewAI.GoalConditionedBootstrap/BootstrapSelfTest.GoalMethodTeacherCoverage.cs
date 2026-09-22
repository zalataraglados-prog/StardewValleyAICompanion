namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    internal static void RunGoalMethodTeacherCoverage(
        GoalMethodFrontierBuildInputs inputs,
        string corpusManifestPath,
        string outputRoot)
    {
        var fullOutputRoot = Path.GetFullPath(outputRoot);
        Directory.CreateDirectory(fullOutputRoot);
        var source = new GoalMethodTeacherCoverageSource(
            "current-acquisition-route-corpus",
            GoalMethodTeacherCoverageSourceKinds
                .AcquisitionRoutePortfolioCorpus,
            Path.GetFullPath(corpusManifestPath));
        var request = new GoalMethodTeacherCoverageRequest
        {
            CoverageId = "self-test-current-goal-method-teacher-coverage",
            Sources = new[] { source },
            FormalProductTrainingAuthorized = false
        };
        var requestPath = Path.Combine(
            fullOutputRoot,
            "goal-method-teacher-coverage-request.json");
        Write(requestPath, request);

        var report = GoalMethodTeacherCoverageBuilder.Build(
            inputs,
            requestPath);
        Require(
            report.Status ==
                "blocked_incomplete_goal_method_teacher_coverage" &&
            report.GoalId ==
                GoalMethodTeacherCoverageGoalIds.Authoritative &&
            report.TargetScore == 21 &&
            report.CriterionPointSum == 21 &&
            report.CriterionDenominatorCount == 19 &&
            report.CatalogMappedCriterionCount == 19 &&
            report.ExecutableCriterionCount == 2 &&
            report.TeacherComparisonCoveredCriterionCount == 2 &&
            report.NativeOutcomeCoveredCriterionCount == 4 &&
            report.SplitCompleteTeacherCriterionCount == 2 &&
            report.CoverageGateReadyCriterionCount == 0 &&
            report.Criteria.Length == 19 &&
            !report.CoverageGateSatisfied &&
            !report.FormalProductTrainingAuthorized &&
            report.BlockingReasons.SequenceEqual(
                new[] { "goal_method_teacher_coverage_incomplete:0/19" },
                StringComparer.Ordinal),
            "Current 19/19 goal-method Teacher coverage gate drifted.");

        var reportPath = Path.Combine(
            fullOutputRoot,
            "goal-method-teacher-coverage-report.json");
        Write(reportPath, report);

        request.FormalProductTrainingAuthorized = true;
        var forgedAuthorizationPath = Path.Combine(
            fullOutputRoot,
            "forged-training-authorization-request.json");
        Write(forgedAuthorizationPath, request);
        var forgedAuthorizationRejected = false;
        try
        {
            GoalMethodTeacherCoverageBuilder.Build(
                inputs,
                forgedAuthorizationPath);
        }
        catch (InvalidDataException)
        {
            forgedAuthorizationRejected = true;
        }
        Require(forgedAuthorizationRejected,
            "Coverage request self-authorized formal product training.");

        request.FormalProductTrainingAuthorized = false;
        request.Sources = new[]
        {
            source with { SourceKind = "unverified_source_kind" }
        };
        var unverifiedSourcePath = Path.Combine(
            fullOutputRoot,
            "unverified-source-request.json");
        Write(unverifiedSourcePath, request);
        var unverifiedSourceRejected = false;
        try
        {
            GoalMethodTeacherCoverageBuilder.Build(
                inputs,
                unverifiedSourcePath);
        }
        catch (InvalidDataException)
        {
            unverifiedSourceRejected = true;
        }
        Require(unverifiedSourceRejected,
            "Unverified source kind entered the Teacher coverage gate.");
    }
}

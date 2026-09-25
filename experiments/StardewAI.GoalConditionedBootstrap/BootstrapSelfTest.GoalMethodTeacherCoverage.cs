using System.Text.Json;
using System.Text.Json.Nodes;

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
        var petCorpusPath = BuildPetLoveTeacherCorpusFixture(fullOutputRoot);
        var petSource = new GoalMethodTeacherCoverageSource(
            "pet-love-terminal-corpus",
            GoalMethodTeacherCoverageSourceKinds
                .PetLoveTerminalInteractionCorpus,
            petCorpusPath);
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
            report.ExecutableCriterionCount == 4 &&
            report.TeacherComparisonCoveredCriterionCount == 2 &&
            report.NativeOutcomeCoveredCriterionCount == 4 &&
            report.SplitCompleteTeacherCriterionCount == 2 &&
            report.CoverageGateReadyCriterionCount == 2 &&
            report.Criteria.Length == 19 &&
            !report.CoverageGateSatisfied &&
            !report.FormalProductTrainingAuthorized &&
            report.BlockingReasons.SequenceEqual(
                new[] { "goal_method_teacher_coverage_incomplete:2/19" },
                StringComparer.Ordinal),
            "Current 19/19 goal-method Teacher coverage gate drifted.");
        var readyCriteria = report.Criteria
            .Where(criterion => criterion.CoverageGateReady)
            .Select(criterion => criterion.CriterionId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Require(
            readyCriteria.SequenceEqual(
                new[]
                {
                    "community_center_access_or_completion",
                    "community_center_accessible_bonus"
                },
                StringComparer.Ordinal) &&
            report.Criteria
                .Where(criterion => criterion.DirectionId ==
                    "complete_community_center")
                .All(criterion =>
                    criterion.MethodStatus == "executable_frontier" &&
                    criterion.TeacherSourceKinds.SequenceEqual(
                        new[]
                        {
                            GoalMethodTeacherCoverageSourceKinds
                                .AcquisitionRoutePortfolioCorpus
                        },
                        StringComparer.Ordinal) &&
                    criterion.SplitCoverageComplete &&
                    criterion.BlockingReasons.Length == 0),
            "Community Center coverage did not become the exact admitted 2/19 slice.");
        var directNonCollection = report.Criteria.Single(criterion =>
            criterion.DirectionId == "obtain_skull_key");
        Require(
            directNonCollection.MethodStatus == "executable_frontier" &&
            directNonCollection.TeacherSourceKinds.Length == 0 &&
            directNonCollection.BlockingReasons.Contains(
                "goal_method_teacher_source_adapter_missing",
                StringComparer.Ordinal),
            "A non-collection method was assigned an implicit requirement-set source adapter.");

        var reportPath = Path.Combine(
            fullOutputRoot,
            "goal-method-teacher-coverage-report.json");
        Write(reportPath, report);

        var reconciliation =
            GoalMethodCoverageReconciliationBuilder.Build(
                inputs,
                requestPath);
        var criterionDispositions = reconciliation
            .CriterionDispositionCounts.ToDictionary(
                value => value.Disposition,
                value => value.Count,
                StringComparer.Ordinal);
        var methodDispositions = reconciliation
            .MethodDispositionCounts.ToDictionary(
                value => value.Disposition,
                value => value.Count,
                StringComparer.Ordinal);
        var petReconciliation = reconciliation.Methods.Single(method =>
            method.DirectionId == "earn_pet_love");
        var skullKeyReconciliation = reconciliation.Methods.Single(method =>
            method.DirectionId == "obtain_skull_key");
        Require(
            reconciliation.Status == "reconciled_open_work" &&
            reconciliation.CriterionDenominatorCount == 19 &&
            reconciliation.RootMethodCount == 11 &&
            reconciliation.ExecutableCriterionCount == 4 &&
            reconciliation.CoverageGateReadyCriterionCount == 2 &&
            !reconciliation.FormalProductTrainingAuthorized &&
            criterionDispositions[
                GoalMethodCoverageDispositions.CoverageReady] == 2 &&
            criterionDispositions[
                GoalMethodCoverageDispositions
                    .DependencyGraphIncomplete] == 15 &&
            criterionDispositions[
                GoalMethodCoverageDispositions.EvidenceNotConnected] == 1 &&
            criterionDispositions[
                GoalMethodCoverageDispositions
                    .TeacherSourceAdapterMissing] == 1 &&
            methodDispositions[
                GoalMethodCoverageDispositions.CoverageReady] == 1 &&
            methodDispositions[
                GoalMethodCoverageDispositions
                    .DependencyGraphIncomplete] == 8 &&
            methodDispositions[
                GoalMethodCoverageDispositions.EvidenceNotConnected] == 1 &&
            methodDispositions[
                GoalMethodCoverageDispositions
                    .TeacherSourceAdapterMissing] == 1 &&
            petReconciliation.PrimaryDisposition ==
                GoalMethodCoverageDispositions.EvidenceNotConnected &&
            petReconciliation.ImplementedTeacherSourceKinds.SequenceEqual(
                new[]
                {
                    GoalMethodTeacherCoverageSourceKinds
                        .PetLoveTerminalInteractionCorpus
                },
                StringComparer.Ordinal) &&
            petReconciliation.ActiveTeacherSourceKinds.Length == 0 &&
            petReconciliation.TransparentReadEvidenceComplete &&
            petReconciliation.NativeRuntimeEvidenceComplete &&
            petReconciliation.EvidenceIds.Contains(
                "EVD-223",
                StringComparer.Ordinal) &&
            skullKeyReconciliation.PrimaryDisposition ==
                GoalMethodCoverageDispositions
                    .TeacherSourceAdapterMissing &&
            skullKeyReconciliation.TransparentReadEvidenceComplete &&
            skullKeyReconciliation.NativeRuntimeEvidenceComplete &&
            skullKeyReconciliation.EvidenceIds.Contains(
                "EVD-106",
                StringComparer.Ordinal),
            "Goal-method reconciliation confused action evidence with criterion coverage.");
        Write(
            Path.Combine(
                fullOutputRoot,
                "goal-method-coverage-reconciliation.json"),
            reconciliation);

        var petFixtureRequest = new GoalMethodTeacherCoverageRequest
        {
            CoverageId = "self-test-pet-love-adapter-fixture",
            Sources = new[] { source, petSource },
            FormalProductTrainingAuthorized = false
        };
        var petFixtureRequestPath = Path.Combine(
            fullOutputRoot,
            "pet-love-adapter-fixture-coverage-request.json");
        Write(petFixtureRequestPath, petFixtureRequest);
        var petFixtureReport = GoalMethodTeacherCoverageBuilder.Build(
            inputs,
            petFixtureRequestPath);
        var petLove = petFixtureReport.Criteria.Single(criterion =>
            criterion.DirectionId == "earn_pet_love");
        Require(
            petFixtureReport.CoverageGateReadyCriterionCount == 3 &&
            petFixtureReport.BlockingReasons.SequenceEqual(
                new[] { "goal_method_teacher_coverage_incomplete:3/19" },
                StringComparer.Ordinal) &&
            petLove.MethodStatus == "executable_frontier" &&
            petLove.CoverageGateReady &&
            petLove.SplitCoverageComplete &&
            petLove.BlockingReasons.Length == 0 &&
            petLove.TeacherSourceKinds.SequenceEqual(
                new[]
                {
                    GoalMethodTeacherCoverageSourceKinds
                        .PetLoveTerminalInteractionCorpus
                },
                StringComparer.Ordinal),
            "Pet-love fixture did not exercise the exact admitted non-collection slice.");
        Write(
            Path.Combine(
                fullOutputRoot,
                "pet-love-adapter-fixture-coverage-report.json"),
            petFixtureReport);

        var tamperedCorpusPath = Path.Combine(
            fullOutputRoot,
            "tampered-pet-love-teacher-corpus.json");
        var tamperedCorpus = JsonNode.Parse(File.ReadAllText(petCorpusPath))!
            .AsObject();
        tamperedCorpus["rows"]![0]!["friendship_after"] = 999;
        File.WriteAllText(
            tamperedCorpusPath,
            tamperedCorpus.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            }) + Environment.NewLine);
        petFixtureRequest.Sources = new[]
        {
            source,
            petSource with { ArtifactPath = tamperedCorpusPath }
        };
        var tamperedCorpusRequestPath = Path.Combine(
            fullOutputRoot,
            "tampered-pet-love-coverage-request.json");
        Write(tamperedCorpusRequestPath, petFixtureRequest);
        var tamperedCorpusRejected = false;
        try
        {
            GoalMethodTeacherCoverageBuilder.Build(
                inputs,
                tamperedCorpusRequestPath);
        }
        catch (InvalidDataException)
        {
            tamperedCorpusRejected = true;
        }
        Require(tamperedCorpusRejected,
            "Tampered pet-love terminal evidence entered the coverage gate.");

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

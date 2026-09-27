namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyFormalTrainingAdmissionPolicy()
    {
        var criteria = Enumerable.Range(0, 19)
            .Select(index => new GoalMethodCoverageCriterionReconciliation(
                "criterion-" + index,
                index < 2 ? 2 : 1,
                "direction",
                "method",
                "executable_frontier",
                GoalMethodCoverageDispositions.CoverageReady,
                true,
                Array.Empty<string>()))
            .ToArray();
        var option = new GoalMethodCoverageOptionReconciliation(
            "option",
            new[] { "terminal" },
            "TrainingEligible",
            "RuntimeVerified",
            "ProductExecutor",
            true,
            true,
            true,
            false,
            new[] { "EVD-HERMETIC" },
            Array.Empty<string>());
        var method = new GoalMethodCoverageMethodReconciliation(
            "method",
            "direction",
            criteria.Select(criterion => criterion.CriterionId).ToArray(),
            21,
            "executable_frontier",
            GoalMethodCoverageDispositions.CoverageReady,
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[]
            {
                GoalMethodTeacherCoverageSourceKinds
                    .AcquisitionRoutePortfolioCorpus
            },
            new[]
            {
                GoalMethodTeacherCoverageSourceKinds
                    .AcquisitionRoutePortfolioCorpus
            },
            new[] { "train", "validation", "test" },
            new[] { "train", "validation", "test" },
            new[] { option },
            true,
            true,
            true,
            true,
            true,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            false,
            new[] { "EVD-HERMETIC" },
            Array.Empty<string>(),
            Array.Empty<string>());
        var reconciliation = new GoalMethodCoverageReconciliationReport
        {
            Status = "reconciled_coverage_complete",
            GoalId = GoalMethodTeacherCoverageGoalIds.Authoritative,
            TargetScore = 21,
            CriterionDenominatorCount = 19,
            RootMethodCount = 1,
            ReferencedOptionCount = 1,
            ExecutableCriterionCount = 19,
            CoverageGateReadyCriterionCount = 19,
            FrontierStatus = "ready",
            TeacherCoverageStatus =
                "ready_complete_goal_method_teacher_coverage",
            CriterionDispositionCounts = new[]
            {
                new GoalMethodCoverageDispositionCount(
                    GoalMethodCoverageDispositions.CoverageReady,
                    19)
            },
            MethodDispositionCounts = new[]
            {
                new GoalMethodCoverageDispositionCount(
                    GoalMethodCoverageDispositions.CoverageReady,
                    1)
            },
            Methods = new[] { method },
            Criteria = criteria,
            FormalProductTrainingAuthorized = false
        };
        var support = new
            AcquisitionRouteSupportingTransitionTerminalCoverageReport
        {
            Status =
                "verified_complete_support_terminal_lineage_coverage",
            RequiredSupportTransitionKinds =
                AcquisitionRouteSupportingTransitionKinds.All.ToArray(),
            CoveredSupportTransitionKinds =
                AcquisitionRouteSupportingTransitionKinds.All.ToArray(),
            TerminalLineageCoverageComplete = true,
            FormalProductTrainingAuthorized = false
        };
        var sha256 = new string('a', 64);
        var binding = new GoalMethodFormalTrainingArtifactBinding(
            "hermetic",
            "hermetic.json",
            sha256);
        var evidence = new GoalMethodFormalTrainingAdmissionBuilder
            .VerifiedEvidence(
                binding,
                binding,
                binding,
                binding,
                3,
                9,
                new GoalMethodPairwiseTrainingSummary
                {
                    TrainRows = 3,
                    TrainPairs = 2,
                    TrainPairAccuracy = 1,
                    ValidationRows = 3,
                    ValidationPairs = 2,
                    ValidationPairAccuracy = 1,
                    TestRows = 3,
                    TestPairs = 2,
                    TestPairAccuracy = 1,
                    FeatureCount = 1
                },
                "hermetic-checkpoint",
                sha256,
                support,
                true);

        var ready = GoalMethodFormalTrainingAdmissionBuilder.BuildReport(
            reconciliation,
            evidence);
        Require(ready.Status ==
                    "ready_for_separate_formal_training_promotion_review" &&
                ready.TeacherTrainingEvidenceReady &&
                ready.RuntimeProductPromotionReady &&
                ready.ReadyForSeparatePromotionReview &&
                !ready.FormalProductTrainingAuthorized &&
                ready.CurrentTeacherBlockingReasons.Length == 0 &&
                ready.DownstreamPromotionBlockingReasons.Length == 0,
            "Formal admission reconciliation self-authorized or blocked a complete review fixture.");

        reconciliation.Criteria[0] = reconciliation.Criteria[0] with
        {
            PrimaryDisposition =
                GoalMethodCoverageDispositions.DependencyGraphIncomplete,
            CoverageGateReady = false,
            OpenWorkKinds = new[]
            {
                GoalMethodCoverageDispositions.DependencyGraphIncomplete
            }
        };
        reconciliation.CoverageGateReadyCriterionCount = 18;
        reconciliation.CriterionDispositionCounts = new[]
        {
            new GoalMethodCoverageDispositionCount(
                GoalMethodCoverageDispositions.CoverageReady,
                18),
            new GoalMethodCoverageDispositionCount(
                GoalMethodCoverageDispositions.DependencyGraphIncomplete,
                1)
        };
        var blocked = GoalMethodFormalTrainingAdmissionBuilder.BuildReport(
            reconciliation,
            evidence);
        Require(blocked.Status ==
                    "blocked_open_formal_training_evidence" &&
                !blocked.TeacherTrainingEvidenceReady &&
                !blocked.ReadyForSeparatePromotionReview &&
                !blocked.FormalProductTrainingAuthorized &&
                blocked.CurrentTeacherBlockingReasons.SequenceEqual(
                    new[] { "dependency_graph_incomplete:1" },
                    StringComparer.Ordinal),
            "Formal admission reconciliation did not fail closed on an incomplete Teacher denominator.");
    }
}

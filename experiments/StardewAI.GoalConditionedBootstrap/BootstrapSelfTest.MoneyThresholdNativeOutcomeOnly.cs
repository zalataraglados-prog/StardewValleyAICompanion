namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyThresholdNativeOutcomeOnly()
    {
        var before = MoneyRevenueTerminalSnapshot(
            gameTick: 100,
            money: 1_000,
            totalMoneyEarned: 49_990,
            inventoryStack: 1);
        var after = MoneyRevenueTerminalSnapshot(
            gameTick: 142,
            money: 1_020,
            totalMoneyEarned: 50_010,
            inventoryStack: 0);
        var episodePath = VerifyMoneyRevenueShopRecurrenceArtifact(
            before,
            after,
            "money-threshold-native-outcome-only");
        var root = Path.GetDirectoryName(episodePath) ??
            throw new InvalidDataException(
                "Money threshold native-outcome fixture root is missing.");
        var requestPath = Path.Combine(root, "money-threshold-request.json");
        Write(requestPath, new MoneyThresholdTeacherCorpusRequest
        {
            CorpusId = "money-threshold-native-outcome-only",
            Sources = new[]
            {
                new MoneyThresholdTeacherCorpusSource(
                    "native-shop-sale-50000",
                    episodePath)
            }
        });
        var corpus = MoneyThresholdTeacherCorpusBuilder.Build(requestPath);
        var row = corpus.Rows.Single();
        var threshold = row.CrossedThresholds.Single();
        Require(corpus.SchemaVersion ==
                    MoneyThresholdTeacherCorpusVersions.Corpus &&
                corpus.RowCount == 1 &&
                row.TeacherComparisonCount == 0 &&
                row.NativeTerminalOutcomeVerified &&
                row.NativeOutcomeKind ==
                    MoneyThresholdNativeOutcomeKinds.ShopSaleEpisode &&
                row.NativeOutcomePrimaryPath == episodePath &&
                row.RevenuePrefix.TotalRevenue == 20 &&
                row.NativeOutcomeArtifacts.Length == 4 &&
                row.TerminalExecutionEvidenceSha256.Length == 0 &&
                threshold.CriterionId == "money_50000" &&
                threshold.TeacherComparison is null &&
                threshold.NativeTerminalOutcomeVerified &&
                corpus.TeacherComparedCriterionCount == 0 &&
                corpus.TeacherComparisonPairCount == 0 &&
                !corpus.TeacherComparisonSplitComplete,
            "Raw Money threshold corpus inferred a Teacher preference from a native outcome.");
        var corpusPath = Path.Combine(root, "money-threshold-corpus.json");
        Write(corpusPath, corpus);
        MoneyThresholdTeacherCorpusBuilder.Verify(corpusPath);

        var workPlanRequestPath = Path.Combine(
            root,
            "money-threshold-work-plan-request.json");
        Write(workPlanRequestPath, new MoneyThresholdEvidenceWorkPlanRequest
        {
            PlanId = "money-threshold-native-outcome-work",
            MoneyThresholdCorpusPath = corpusPath
        });
        var workPlan = MoneyThresholdEvidenceWorkPlanBuilder.Build(
            workPlanRequestPath);
        var crossedCell = workPlan.Cells.Single(value =>
            value.CriterionId == "money_50000" &&
            value.DatasetPartition == row.DatasetPartition);
        Require(workPlan.RequiredCellCount == 18 &&
                workPlan.NativeOutcomeCompleteCellCount == 1 &&
                workPlan.TeacherComparisonCompleteCellCount == 0 &&
                workPlan.FullyCompleteCellCount == 0 &&
                workPlan.NativeCollectionWaveCount == 3 &&
                workPlan.ComparisonCollectionWaveCount == 1 &&
                workPlan.ComparisonBlockedOnNativeCellCount == 17 &&
                !workPlan.CoverageComplete &&
                !workPlan.FormalProductTrainingAuthorized &&
                crossedCell.NativeOutcomeVerified &&
                !crossedCell.TeacherComparisonVerified &&
                crossedCell.Status == "teacher_comparison_missing" &&
                workPlan.ComparisonCollectionWaves.Single()
                    .MissingCriterionIds.SequenceEqual(
                        new[] { "money_50000" }) &&
                workPlan.NativeCollectionWaves.Single(value =>
                        value.DatasetPartition == row.DatasetPartition)
                    .MissingCriterionIds.First() == "money_100000" &&
                workPlan.NativeCollectionWaves.Single(value =>
                        value.DatasetPartition == row.DatasetPartition)
                    .RecommendedCriterionId == "money_1000000" &&
                workPlan.Cells.Count(value => value.Status ==
                    "native_outcome_and_teacher_comparison_missing") == 17,
            "Money-threshold work plan did not preserve the exact 6x3 evidence matrix.");

        var emptyWorkPlanRequestPath = Path.Combine(
            root,
            "empty-money-threshold-work-plan-request.json");
        Write(emptyWorkPlanRequestPath, new MoneyThresholdEvidenceWorkPlanRequest
        {
            PlanId = "money-threshold-empty-work"
        });
        var emptyWorkPlan = MoneyThresholdEvidenceWorkPlanBuilder.Build(
            emptyWorkPlanRequestPath);
        Require(emptyWorkPlan.RequiredCellCount == 18 &&
                emptyWorkPlan.NativeOutcomeCompleteCellCount == 0 &&
                emptyWorkPlan.TeacherComparisonCompleteCellCount == 0 &&
                emptyWorkPlan.FullyCompleteCellCount == 0 &&
                emptyWorkPlan.NativeCollectionWaveCount == 3 &&
                emptyWorkPlan.ComparisonCollectionWaveCount == 0 &&
                emptyWorkPlan.ComparisonBlockedOnNativeCellCount == 18 &&
                emptyWorkPlan.NativeCollectionWaves.All(value =>
                    value.MissingCriterionIds.Length == 6 &&
                    value.SourceTotalMoneyEarnedMaxExclusive == 50_000 &&
                    value.OutcomeTotalMoneyEarnedMinInclusive == 1_000_000 &&
                    value.RecommendedCriterionId == "money_1000000") &&
                emptyWorkPlan.Cells.All(value => value.Status ==
                    "native_outcome_and_teacher_comparison_missing"),
            "An empty Money-threshold work plan did not expose all 18 missing cells.");
    }
}

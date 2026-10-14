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
                !row.TeacherComparisonVerified &&
                row.NativeTerminalOutcomeVerified &&
                string.IsNullOrWhiteSpace(row.PreferredAlternative) &&
                string.IsNullOrWhiteSpace(row.RejectedAlternative) &&
                threshold.CriterionId == "money_50000" &&
                !threshold.TeacherComparisonVerified &&
                threshold.NativeTerminalOutcomeVerified &&
                string.IsNullOrWhiteSpace(
                    threshold.PreferredAlternative) &&
                string.IsNullOrWhiteSpace(
                    threshold.RejectedAlternative),
            "Raw Money threshold corpus inferred a Teacher preference from a native outcome.");
        var corpusPath = Path.Combine(root, "money-threshold-corpus.json");
        Write(corpusPath, corpus);
        MoneyThresholdTeacherCorpusBuilder.Verify(corpusPath);
    }
}

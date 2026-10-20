namespace StardewAI.Core.Tests;

public sealed class MoneyCropNativeProductionSegmentSourceGuardTests
{
    private static readonly string Builder = ReadRepositoryFile(
        "experiments",
        "StardewAI.GoalConditionedBootstrap",
        "MoneyRevenueCropNativeProductionSegmentBuilder.cs");
    private static readonly string Contracts = ReadRepositoryFile(
        "experiments",
        "StardewAI.GoalConditionedBootstrap",
        "MoneyRevenueCropNativeProductionSegmentContracts.cs");
    private static readonly string Validation = ReadRepositoryFile(
        "experiments",
        "StardewAI.GoalConditionedBootstrap",
        "MoneyRevenueCropNativeProductionSegmentBuilder.Validation.cs");
    private static readonly string Program = ReadRepositoryFile(
        "experiments",
        "StardewAI.GoalConditionedBootstrap",
        "Program.cs");
    private static readonly string Runner = ReadRepositoryFile(
        "scripts",
        "Invoke-RuntimeMoneyCropNativeProduction.ps1");

    [Fact]
    public void SegmentReceiptRebuildsTheExistingTeacherAndExecutionChain()
    {
        var sources = Builder + Validation;
        foreach (var requiredBuilder in new[]
        {
            "MoneyRevenueCropProductionTeacherDecisionBuilder.Build",
            "MoneyRevenueCropProductionDecisionReceiptBuilder.Build",
            "MoneyRevenueCropProductionObligationPlanBuilder.Build",
            "MoneyRevenueCropNativeProductionRunPlanBuilder.Build",
            "MoneyRevenueCropProductionProofCheckpointBuilder.Build",
            "MoneyRevenueRecurrenceProofBuilder.Build"
        })
        {
            Assert.Contains(requiredBuilder, sources, StringComparison.Ordinal);
        }

        Assert.Contains(
            "MoneyThresholdRuntimeModIsolationVerifier.HashDirectory",
            sources,
            StringComparison.Ordinal);
        Assert.Contains(
            "GrandpaEvaluationGoalEvaluator.MoneyThresholds",
            sources,
            StringComparison.Ordinal);
        Assert.Contains(
            "BootstrapArtifactGuard.EqualJson",
            sources,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "api/v1/training/execute",
            sources,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SegmentReceiptCannotClaimComparisonCellOrFormalTraining()
    {
        Assert.Contains(
            "TeacherComparisonVerified = false",
            Builder,
            StringComparison.Ordinal);
        Assert.Contains(
            "FormalProductTrainingAuthorized = false",
            Builder,
            StringComparison.Ordinal);
        Assert.Contains(
            "It does not plan or execute actions, create a Teacher comparison",
            Contracts,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "TeacherComparisonVerified = true",
            Builder,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "FormalProductTrainingAuthorized = true",
            Builder + Contracts,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RunnerPersistsTheFinalSnapshotAndSaveBoundary()
    {
        Assert.Contains(
            "money_crop_native_production_run_summary.v2",
            Runner,
            StringComparison.Ordinal);
        Assert.Contains("isolated_save_sha256_after", Runner,
            StringComparison.Ordinal);
        Assert.Contains("final_snapshot_path", Runner,
            StringComparison.Ordinal);
        Assert.Contains("final_snapshot_sha256", Runner,
            StringComparison.Ordinal);
        Assert.Contains("final_state_hash", Runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "build-money-revenue-crop-native-production-segment",
            Program,
            StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(
                new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(path))
                return File.ReadAllText(path);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Repository file was not found: " + Path.Combine(segments));
    }
}

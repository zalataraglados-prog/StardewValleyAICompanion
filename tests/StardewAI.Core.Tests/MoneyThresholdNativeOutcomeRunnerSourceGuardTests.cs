namespace StardewAI.Core.Tests;

public sealed class MoneyThresholdNativeOutcomeRunnerSourceGuardTests
{
    private static readonly string Source = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "Invoke-RuntimeMoneyThresholdNativeOutcome.ps1"));
    private static readonly string BatchSource = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "Update-MoneyThresholdNativeOutcomeBatch.ps1"));
    private static readonly string ComparisonPlanSource = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "Initialize-MoneyRevenueComparisonWork.ps1"));

    [Fact]
    public void RunnerUsesImmutableSourceSaveAndAnIsolatedClone()
    {
        Assert.Contains("Get-DirectoryContentHash -Path $sourceSave", Source,
            StringComparison.Ordinal);
        Assert.Contains("Copy-Item -LiteralPath $sourceSave", Source,
            StringComparison.Ordinal);
        Assert.Contains("$env:STARDEWAI_TEST_SAVES = $isolatedSaves", Source,
            StringComparison.Ordinal);
        Assert.Contains("source_save_unchanged", Source,
            StringComparison.Ordinal);
        Assert.Contains("Stop-RuntimeEvidenceProcesses", Source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RunnerUsesProductionSelectionAndNativeVerificationOnly()
    {
        Assert.Contains("build-money-revenue-candidate-denominator", Source,
            StringComparison.Ordinal);
        Assert.Contains("build-money-threshold-native-outcome-selection", Source,
            StringComparison.Ordinal);
        Assert.Contains("build-money-threshold-teacher-corpus", Source,
            StringComparison.Ordinal);
        Assert.Contains("--skip-training", Source, StringComparison.Ordinal);
        Assert.Contains("$selection.objective_parameters", Source,
            StringComparison.Ordinal);
        Assert.Contains("Wait-NativeGameTime", Source,
            StringComparison.Ordinal);
        Assert.Contains("effective_open_time", Source,
            StringComparison.Ordinal);

        Assert.DoesNotContain("debug.advance_time_to", Source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("debug.setup_sale_target", Source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("api/v1/training/execute", Source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-RuntimeSaleMainlineSmoke", Source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BatchReplaysRunsAndRebuildsTheCanonicalWorkPlan()
    {
        Assert.Contains("build-money-threshold-native-outcome-batch",
            BatchSource, StringComparison.Ordinal);
        Assert.Contains("build-money-threshold-evidence-work-plan",
            BatchSource, StringComparison.Ordinal);
        Assert.Contains("run_summary_paths = $summaryPaths",
            BatchSource, StringComparison.Ordinal);
        Assert.Contains(
            "money_threshold_native_outcome_batch_request.v2",
            BatchSource, StringComparison.Ordinal);
        Assert.Contains("comparison_admissions = $comparisonAdmissions",
            BatchSource, StringComparison.Ordinal);
        Assert.Contains(
            "money_threshold_comparison_admission_index.v1",
            BatchSource, StringComparison.Ordinal);
        Assert.Contains("formal_product_training_authorized = $false",
            BatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("debug.", BatchSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("StardewModdingAPI", BatchSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ComparisonInitializationOnlyBuildsVerifiedPlanningArtifacts()
    {
        Assert.Contains("build-money-revenue-alternative-denominator",
            ComparisonPlanSource, StringComparison.Ordinal);
        Assert.Contains("build-money-revenue-comparison-collection-plan",
            ComparisonPlanSource, StringComparison.Ordinal);
        Assert.Contains("native_run_summary_path = $summaryPath",
            ComparisonPlanSource, StringComparison.Ordinal);
        Assert.Contains("formal_product_training_authorized = $false",
            ComparisonPlanSource, StringComparison.Ordinal);
        Assert.DoesNotContain("debug.", ComparisonPlanSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("StardewModdingAPI", ComparisonPlanSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("training/execute", ComparisonPlanSource,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(
                new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(path))
                return path;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Repository file was not found: " + Path.Combine(segments));
    }
}

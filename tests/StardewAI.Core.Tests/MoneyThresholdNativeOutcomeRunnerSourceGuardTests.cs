namespace StardewAI.Core.Tests;

public sealed class MoneyThresholdNativeOutcomeRunnerSourceGuardTests
{
    private static readonly string Source = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "Invoke-RuntimeMoneyThresholdNativeOutcome.ps1"));

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

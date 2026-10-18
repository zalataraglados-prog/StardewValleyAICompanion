namespace StardewAI.Core.Tests;

public sealed class MoneyCropNativeProductionRunnerSourceGuardTests
{
    private static readonly string Runner = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "Invoke-RuntimeMoneyCropNativeProduction.ps1"));
    private static readonly string CropRuntime = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "lib",
            "RuntimeMoneyCropNative.ps1"));
    private static readonly string CommonRuntime = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "lib",
            "RuntimeEvidenceCommon.ps1"));

    [Fact]
    public void RunnerUsesImmutableSaveAndExactMinimalModSet()
    {
        Assert.Contains("Assert-RuntimeSourceSaveSlot", Runner,
            StringComparison.Ordinal);
        Assert.Contains("Copy-Item -LiteralPath $sourceSave", Runner,
            StringComparison.Ordinal);
        Assert.Contains("Get-RuntimeExactModAllowlist", Runner,
            StringComparison.Ordinal);
        Assert.Contains("\"StardewAI.TransparentBridge\"", Runner,
            StringComparison.Ordinal);
        Assert.Contains("\"StardewAI.RuntimeTestHarness\"", Runner,
            StringComparison.Ordinal);
        Assert.Contains("$env:STARDEWAI_SUPPRESS_LOCAL_RENDER = \"1\"",
            Runner, StringComparison.Ordinal);
        Assert.Contains("$env:SDL_AUDIODRIVER = \"dummy\"", Runner,
            StringComparison.Ordinal);
        Assert.Contains("source_save_unchanged", Runner,
            StringComparison.Ordinal);
        Assert.Contains("money_crop_native_source_preflight.v1", Runner,
            StringComparison.Ordinal);
        Assert.Contains("$initialStateHash -eq [string]$runPlan.source_state_hash",
            Runner, StringComparison.Ordinal);
        Assert.Contains("$initialDay -eq [int]$runPlan.source_total_day",
            Runner, StringComparison.Ordinal);
        Assert.Contains("Stop-RuntimeEvidenceProcesses", Runner,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RunnerCompilesExistingActionsAndRoutesWithoutASecondExecutor()
    {
        Assert.Contains(
            "build-money-revenue-crop-native-production-run-plan",
            Runner, StringComparison.Ordinal);
        Assert.Contains("Invoke-RuntimeCropActionBatch", Runner,
            StringComparison.Ordinal);
        Assert.Contains("Invoke-RuntimeCropRoute", Runner,
            StringComparison.Ordinal);
        Assert.Contains("api/v1/small-model/action-queue/compile",
            CropRuntime, StringComparison.Ordinal);
        Assert.Contains("api/v1/small-model/plan/action-queue/compile",
            CropRuntime, StringComparison.Ordinal);
        Assert.Contains("executor.traverse_connector", CropRuntime,
            StringComparison.Ordinal);
        Assert.Contains("-ExecutionTarget \"runtime_harness\"", CropRuntime,
            StringComparison.Ordinal);
        Assert.Contains("crop_needs_watering", CropRuntime,
            StringComparison.Ordinal);
        Assert.Contains("ConvertTo-RuntimeCropSeedId", CropRuntime,
            StringComparison.Ordinal);
        Assert.DoesNotContain("api/v1/training/execute", Runner + CropRuntime,
            StringComparison.Ordinal);
        Assert.DoesNotContain("debug.", Runner + CropRuntime,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RunnerRetainsExactProofChainWithoutTrainingAuthorization()
    {
        Assert.Contains("supporting_route_to_crop", Runner,
            StringComparison.Ordinal);
        Assert.Contains("supporting_route_to_sleep", Runner,
            StringComparison.Ordinal);
        Assert.Contains("native_day_rollover", Runner,
            StringComparison.Ordinal);
        Assert.Contains("build-money-revenue-crop-planting-layout-proof",
            Runner, StringComparison.Ordinal);
        Assert.Contains("build-money-revenue-crop-daily-capacity-proof",
            Runner, StringComparison.Ordinal);
        Assert.Contains("build-money-revenue-crop-native-lifecycle-proof",
            Runner, StringComparison.Ordinal);
        Assert.Contains("build-money-revenue-crop-output-retention-proof",
            Runner, StringComparison.Ordinal);
        Assert.Contains(
            "build-money-revenue-crop-production-proof-checkpoint",
            Runner, StringComparison.Ordinal);
        Assert.DoesNotContain("formal_product_training_authorized = $true",
            Runner, StringComparison.Ordinal);
    }

    [Fact]
    public void CommonQueueHelperKeepsProductDefaultAndAllowsHarnessExplicitly()
    {
        Assert.Contains("[string] $ExecutionTarget = \"product\"",
            CommonRuntime, StringComparison.Ordinal);
        Assert.Contains("[ValidateSet(\"product\", \"runtime_harness\")]",
            CommonRuntime, StringComparison.Ordinal);
        Assert.Contains("$Context.ExecutorRoot", CommonRuntime,
            StringComparison.Ordinal);
        Assert.Contains("$arguments.Add(\"--use-product-executor\")",
            CommonRuntime, StringComparison.Ordinal);
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

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
    private static readonly string SeedAcquisitionRuntime = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "lib",
            "RuntimeMoneyCropSeedAcquisition.ps1"));
    private static readonly string ProductionInput = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "lib",
            "RuntimeMoneyCropProductionInput.ps1"));

    [Fact]
    public void RunnerUsesImmutableSaveAndExactMinimalModSet()
    {
        Assert.Contains("[string] $ProductionInputManifestPath", Runner,
            StringComparison.Ordinal);
        Assert.DoesNotContain("[string] $SourceSaveSlotPath", Runner,
            StringComparison.Ordinal);
        Assert.DoesNotContain("[string] $RunPlanRequestPath", Runner,
            StringComparison.Ordinal);
        Assert.Contains("Read-MoneyCropNativeProductionInputManifest", Runner,
            StringComparison.Ordinal);
        Assert.Equal(3,
            Runner.Split(
                "Read-MoneyCropNativeProductionInputManifest",
                StringSplitOptions.None).Length - 1);
        Assert.Contains(
            "production input manifest changed before process startup",
            Runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "production input manifest changed during native production",
            Runner,
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
        Assert.DoesNotContain(
            "build-money-revenue-crop-native-production-run-plan",
            Runner, StringComparison.Ordinal);
        Assert.Contains("Copy-Item -LiteralPath $productionInput.RunPlanPath",
            Runner, StringComparison.Ordinal);
        Assert.Contains("Invoke-RuntimeCropActionBatch", Runner,
            StringComparison.Ordinal);
        Assert.Contains("Invoke-RuntimeCropSeedAcquisition", Runner,
            StringComparison.Ordinal);
        Assert.Contains("Invoke-RuntimeCropSitePreparation", Runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "build-money-revenue-crop-site-preparation-plan",
            CropRuntime, StringComparison.Ordinal);
        Assert.Contains("Invoke-RuntimeCropSitePreparationAction",
            CropRuntime, StringComparison.Ordinal);
        Assert.Contains("executor.till_soil", CropRuntime,
            StringComparison.Ordinal);
        Assert.Contains("executor.clear_obstacle", CropRuntime,
            StringComparison.Ordinal);
        Assert.Contains("executor.break_farm_resource_clump", CropRuntime,
            StringComparison.Ordinal);
        Assert.Contains("fresh=true", CropRuntime,
            StringComparison.Ordinal);
        Assert.Contains("$step = $pending[0]", CropRuntime,
            StringComparison.Ordinal);
        Assert.True(
            Runner.IndexOf(
                "Invoke-RuntimeCropSitePreparation",
                StringComparison.Ordinal) <
            Runner.IndexOf(
                "daily-capacity-day-$totalDay.json",
                StringComparison.Ordinal),
            "Site preparation must finish before the planting-day capacity boundary is captured.");
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
    public void SeedShortfallsReuseBoundedPurchasePlanningAndExactInventoryDeltas()
    {
        Assert.Contains("Invoke-RuntimeRanking", SeedAcquisitionRuntime,
            StringComparison.Ordinal);
        Assert.Contains("Invoke-RuntimeDailyPlanStep", SeedAcquisitionRuntime,
            StringComparison.Ordinal);
        Assert.Contains("economy.buy_supplies", SeedAcquisitionRuntime,
            StringComparison.Ordinal);
        Assert.Contains("continuation.quantity", SeedAcquisitionRuntime,
            StringComparison.Ordinal);
        Assert.Contains("value = \"1\"", SeedAcquisitionRuntime,
            StringComparison.Ordinal);
        Assert.Contains("Import-RuntimeSnapshotFile", SeedAcquisitionRuntime,
            StringComparison.Ordinal);
        Assert.Contains("PurchaseSliceCount", SeedAcquisitionRuntime,
            StringComparison.Ordinal);
        Assert.Contains("exceeded its exact target quantity",
            SeedAcquisitionRuntime, StringComparison.Ordinal);
        Assert.DoesNotContain("addItemToInventory", SeedAcquisitionRuntime,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Money", SeedAcquisitionRuntime,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestVerifierClosesEveryProductionInputLineage()
    {
        Assert.Contains(
            "money_crop_native_production_input_manifest.v1",
            ProductionInput,
            StringComparison.Ordinal);
        Assert.Contains("Assert-RuntimeFileIdentity", ProductionInput,
            StringComparison.Ordinal);
        foreach (var property in new[]
        {
            "decision_request_sha256",
            "decision_receipt_sha256",
            "snapshot_sha256",
            "strategy_ledger_sha256",
            "crop_production_obligation_request_sha256",
            "crop_production_obligation_plan_sha256",
            "crop_native_run_plan_request_sha256",
            "crop_native_run_plan_sha256",
            "source_save_tree_sha256"
        })
        {
            Assert.Contains(property, ProductionInput,
                StringComparison.Ordinal);
        }
        Assert.Contains("Get-RuntimeRawJsonProperty", ProductionInput,
            StringComparison.Ordinal);
        Assert.Contains("Assert-MoneyCropInputIdSet", ProductionInput,
            StringComparison.Ordinal);
        Assert.Contains("formal_product_training_authorized", ProductionInput,
            StringComparison.Ordinal);
        Assert.Contains("seed_quantity_shortfall", ProductionInput,
            StringComparison.Ordinal);
        Assert.Contains("seed_supply_deadline_total_day", ProductionInput,
            StringComparison.Ordinal);
        Assert.Contains(
            "economy.buy_supplies_one_unit_fresh_snapshot_slices",
            ProductionInput,
            StringComparison.Ordinal);
        Assert.DoesNotContain("formal_product_training_authorized = $true",
            ProductionInput,
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

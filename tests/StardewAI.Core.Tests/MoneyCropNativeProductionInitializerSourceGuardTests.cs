namespace StardewAI.Core.Tests;

public sealed class MoneyCropNativeProductionInitializerSourceGuardTests
{
    private static readonly string Initializer = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "Initialize-MoneyRevenueCropNativeProduction.ps1"));
    private static readonly string TeacherInitializer = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "Initialize-MoneyRevenueCropProductionTeacherDecision.ps1"));
    private static readonly string TeacherBoundaryCapture = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "Capture-MoneyRevenueCropProductionTeacherBoundary.ps1"));

    [Fact]
    public void TeacherBoundaryCaptureUsesTheImmutableCompleteFarmBoundary()
    {
        Assert.Contains("Assert-RuntimeSourceSaveSlot", TeacherBoundaryCapture,
            StringComparison.Ordinal);
        Assert.Contains("Get-RuntimeExactModAllowlist", TeacherBoundaryCapture,
            StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE",
            TeacherBoundaryCapture, StringComparison.Ordinal);
        Assert.Contains("profile=full&fresh=true", TeacherBoundaryCapture,
            StringComparison.Ordinal);
        Assert.Contains("profile=farm_layout", TeacherBoundaryCapture,
            StringComparison.Ordinal);
        Assert.Contains("$minY += 32", TeacherBoundaryCapture,
            StringComparison.Ordinal);
        Assert.Contains("$minX += 32", TeacherBoundaryCapture,
            StringComparison.Ordinal);
        Assert.Contains("map_width", TeacherBoundaryCapture,
            StringComparison.Ordinal);
        Assert.Contains("map_height", TeacherBoundaryCapture,
            StringComparison.Ordinal);
        Assert.Contains(
            "Initialize-MoneyRevenueCropProductionTeacherDecision.ps1",
            TeacherBoundaryCapture,
            StringComparison.Ordinal);
        Assert.Contains("Initialize-MoneyRevenueCropNativeProduction.ps1",
            TeacherBoundaryCapture, StringComparison.Ordinal);
        Assert.Contains("source save changed during capture",
            TeacherBoundaryCapture, StringComparison.Ordinal);
        Assert.DoesNotContain("formal_product_training_authorized = $true",
            TeacherBoundaryCapture, StringComparison.Ordinal);
    }

    [Fact]
    public void TeacherInitializerBuildsTheTypedDecisionWithoutHandwrittenCommitments()
    {
        Assert.Contains(
            "money_revenue_crop_production_teacher_request.v1",
            TeacherInitializer,
            StringComparison.Ordinal);
        Assert.Contains(
            "build-money-revenue-crop-production-teacher-decision",
            TeacherInitializer,
            StringComparison.Ordinal);
        Assert.Contains(
            "money_revenue_crop_production_decision_request.v2",
            TeacherInitializer,
            StringComparison.Ordinal);
        Assert.Contains("farm_layout_snapshot_paths", TeacherInitializer,
            StringComparison.Ordinal);
        Assert.Contains("teacher_request_sha256", TeacherInitializer,
            StringComparison.Ordinal);
        Assert.Contains("teacher_candidate_evaluations", TeacherInitializer,
            StringComparison.Ordinal);
        Assert.DoesNotContain("commitments =", TeacherInitializer,
            StringComparison.Ordinal);
        Assert.DoesNotContain("formal_product_training_authorized = $true",
            TeacherInitializer,
            StringComparison.Ordinal);
    }

    [Fact]
    public void InitializerUsesTheTypedDecisionAndExistingPlanBuilders()
    {
        Assert.Contains(
            "build-money-revenue-crop-production-decision-receipt",
            Initializer,
            StringComparison.Ordinal);
        Assert.Contains(
            "build-money-revenue-crop-production-obligation-plan",
            Initializer,
            StringComparison.Ordinal);
        Assert.Contains(
            "build-money-revenue-crop-native-production-run-plan",
            Initializer,
            StringComparison.Ordinal);
        Assert.Contains("Write-RawJsonProperty", Initializer,
            StringComparison.Ordinal);
        Assert.Contains("-PropertyName \"final_ledger\"", Initializer,
            StringComparison.Ordinal);
        Assert.True(
            Initializer.IndexOf(
                "build-money-revenue-crop-production-decision-receipt",
                StringComparison.Ordinal) <
            Initializer.IndexOf(
                "build-money-revenue-crop-production-obligation-plan",
                StringComparison.Ordinal));
        Assert.True(
            Initializer.IndexOf(
                "build-money-revenue-crop-production-obligation-plan",
                StringComparison.Ordinal) <
            Initializer.IndexOf(
                "build-money-revenue-crop-native-production-run-plan",
                StringComparison.Ordinal));
    }

    [Fact]
    public void InitializerBindsEveryInputWithoutAuthorizingTraining()
    {
        Assert.Contains("Assert-RuntimeSourceSaveSlot", Initializer,
            StringComparison.Ordinal);
        Assert.Contains("Get-DirectoryContentHash", Initializer,
            StringComparison.Ordinal);
        Assert.Contains("Get-RuntimeFileSha256", Initializer,
            StringComparison.Ordinal);
        Assert.Contains(
            "money_crop_native_production_input_manifest.v1",
            Initializer,
            StringComparison.Ordinal);
        Assert.Contains("source_save_tree_sha256", Initializer,
            StringComparison.Ordinal);
        Assert.Contains("decision_receipt_sha256", Initializer,
            StringComparison.Ordinal);
        Assert.Contains("teacher_request_sha256", Initializer,
            StringComparison.Ordinal);
        Assert.Contains("teacher_selection_policy", Initializer,
            StringComparison.Ordinal);
        Assert.Contains("crop_native_run_plan_request_sha256", Initializer,
            StringComparison.Ordinal);
        Assert.Contains("crop_native_run_plan_sha256", Initializer,
            StringComparison.Ordinal);
        Assert.Contains("seed_acquisition_intents", Initializer,
            StringComparison.Ordinal);
        Assert.Contains("teacher_candidate_evaluations", Initializer,
            StringComparison.Ordinal);
        Assert.Contains("max_unit_price", Initializer,
            StringComparison.Ordinal);
        Assert.Contains("already exists", Initializer,
            StringComparison.Ordinal);
        Assert.DoesNotContain("formal_product_training_authorized = $true",
            Initializer,
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

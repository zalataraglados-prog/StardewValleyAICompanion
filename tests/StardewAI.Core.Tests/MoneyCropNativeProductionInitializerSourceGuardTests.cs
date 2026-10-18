namespace StardewAI.Core.Tests;

public sealed class MoneyCropNativeProductionInitializerSourceGuardTests
{
    private static readonly string Initializer = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "Initialize-MoneyRevenueCropNativeProduction.ps1"));

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
        Assert.Contains("crop_native_run_plan_request_sha256", Initializer,
            StringComparison.Ordinal);
        Assert.Contains("crop_native_run_plan_sha256", Initializer,
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

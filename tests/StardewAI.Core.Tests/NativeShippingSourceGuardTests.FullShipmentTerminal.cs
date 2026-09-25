namespace StardewAI.Core.Tests;

public sealed partial class NativeShippingSourceGuardTests
{
    private static readonly string FullShipmentTerminalFixtureSource =
        RuntimeHarnessSources.LoadFile("ModEntry.FullShipmentFixture.cs");
    private static readonly string FullShipmentTerminalSmokeSource =
        File.ReadAllText(FindRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentTerminalSmoke.ps1"));

    [Fact]
    public void FullShipmentTerminalFixtureUsesNativeEligibilityAndExactDenominator()
    {
        var source = FullShipmentTerminalFixtureSource;
        Assert.Contains("Object.isPotentialBasicShipped", source, StringComparison.Ordinal);
        Assert.Contains("Category != -7", source, StringComparison.Ordinal);
        Assert.Contains("Category != -2", source, StringComparison.Ordinal);
        Assert.Contains(
            "FullShipmentExpectedEligibleItemCount",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment_fixture_native_denominator_drift",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FullShipmentTerminalFixtureIsolatedMutationCannotBeMistakenForExecution()
    {
        var source = FullShipmentTerminalFixtureSource;
        Assert.Contains(
            "debug_setup_full_shipment_terminal",
            source,
            StringComparison.Ordinal);
        Assert.Contains("master.basicShipped", source, StringComparison.Ordinal);
        Assert.Contains("master.achievements.Remove(34)", source, StringComparison.Ordinal);
        Assert.Contains(
            "ExecuteSetupShippingTarget(request)",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "executor.ship_inventory_item_to_bin",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.getAchievement(34)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FullShipmentTerminalSmokeUsesExistingShippingAndSleepCompilers()
    {
        var script = FullShipmentTerminalSmokeSource;
        Assert.Contains(
            "economy.ship_items",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "ship_inventory_item_to_bin",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "recovery.stabilize_day",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "recovery_sleep_immediately",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "build-full-shipment-terminal-settlement-receipt",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain("basicShipped", script, StringComparison.Ordinal);
        Assert.DoesNotContain("achievements.Remove", script, StringComparison.Ordinal);
    }

    [Fact]
    public void FullShipmentTerminalSmokeIsIsolatedAndChecksEveryTerminalFact()
    {
        var script = FullShipmentTerminalSmokeSource;
        Assert.Contains("Copy-Item -LiteralPath $sourceSavePath", script, StringComparison.Ordinal);
        Assert.Contains("Get-DirectoryContentHash -Path $sourceSavePath", script, StringComparison.Ordinal);
        Assert.Contains("$sourceSaveHashAfter -ne $sourceSaveHashBefore", script, StringComparison.Ordinal);
        Assert.Contains("source_save_preserved = $true", script, StringComparison.Ordinal);
        Assert.Contains("source_save_hash_before = $sourceSaveHashBefore", script, StringComparison.Ordinal);
        Assert.Contains("source_save_hash_after = $sourceSaveHashAfter", script, StringComparison.Ordinal);
        Assert.Contains("-ExpectedShippedCount 153", script, StringComparison.Ordinal);
        Assert.Contains("-ExpectedShippedCount 154", script, StringComparison.Ordinal);
        Assert.Contains("-ExpectedTerminalBinCount 1", script, StringComparison.Ordinal);
        Assert.Contains("-ExpectedTerminalBinCount 0", script, StringComparison.Ordinal);
        Assert.Contains("-ExpectedAchievement $true", script, StringComparison.Ordinal);
        Assert.Contains("training_label_eligible", script, StringComparison.Ordinal);
    }
}

namespace StardewAI.Backend.Tests;

public sealed class RuntimeFarmhouseUpgradeSmokeSourceGuardTests
{
    [Fact]
    public void HighLevelSmokeCoversAllNativeUpgradeTuplesWithoutVisibleGameByDefault()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "scripts", "Invoke-RuntimeFarmhouseUpgradeDailyPlanSmoke.ps1"));

        Assert.Contains("housing.advance_farmhouse", script, StringComparison.Ordinal);
        Assert.Contains("executor.purchase_farmhouse_upgrade", script, StringComparison.Ordinal);
        Assert.Contains("debug.setup_farmhouse_upgrade", script, StringComparison.Ordinal);
        Assert.Contains("@(0, 1, 2", script, StringComparison.Ordinal);
        Assert.Contains("GameLocation.checkAction_Carpenter_completed", script, StringComparison.Ordinal);
        Assert.Contains("GameLocation.answerDialogue_upgrade_Yes_completed", script, StringComparison.Ordinal);
        Assert.Contains("days_until_farmhouse_upgrade", script, StringComparison.Ordinal);
        Assert.Contains("-WindowStyle $windowStyle", script, StringComparison.Ordinal);
        Assert.Contains("if ($VisibleGame) { \"Normal\" } else { \"Hidden\" }", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionExecutorUsesNativeDialogueAndKeepsDirectMutationInsideDebugFixture()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.MarriageHouse.cs"));
        var fixtureMarker = source.IndexOf("private TrainingExecutionResult ExecuteSetupFarmhouseUpgradeFixture", StringComparison.Ordinal);
        Assert.True(fixtureMarker > 0);
        var production = source[..fixtureMarker];

        Assert.Contains("active.House.checkAction", production, StringComparison.Ordinal);
        Assert.Contains("active.House.answerDialogue(response)", production, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Money -=", production, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.HouseUpgradeLevel = request", production, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.daysUntilHouseUpgrade.Value = 3", production, StringComparison.Ordinal);
        Assert.Contains("debug_setup_farmhouse_upgrade", source[fixtureMarker..], StringComparison.Ordinal);
        Assert.Contains("FarmhouseFixtureRobinTile", source[fixtureMarker..], StringComparison.Ordinal);
        Assert.Contains("robin.TilePoint != actionTile.Value && robin.TilePoint != standTile.Value", source[fixtureMarker..], StringComparison.Ordinal);
    }

    [Fact]
    public void LiveTrainingTransportMapsTheTypedCarpenterAction()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.LiveTrainingLoop", "Program.RuntimeExecution.cs"));

        Assert.Contains("ReadQueueParameterString(item, \"join_action_raw\")", source, StringComparison.Ordinal);
        Assert.Contains("ReadQueueParameterString(item, \"carpenter_action_raw\")", source, StringComparison.Ordinal);
        Assert.Contains("executionRequest.JoinActionRaw = joinActionRaw", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}

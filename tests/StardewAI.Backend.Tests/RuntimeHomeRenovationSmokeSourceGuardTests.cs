namespace StardewAI.Backend.Tests;

public sealed class RuntimeHomeRenovationSmokeSourceGuardTests
{
    [Fact]
    public void HiddenSmokeUsesTheLiveEighteenEntryCatalogAndCoversRefundSubbranches()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "scripts", "Invoke-RuntimeHomeRenovationDailyPlanSmoke.ps1"));

        Assert.Contains("housing.renovate", script, StringComparison.Ordinal);
        Assert.Contains("executor.renovate_home", script, StringComparison.Ordinal);
        Assert.Contains("debug.setup_home_renovation", script, StringComparison.Ordinal);
        Assert.Contains("$liveIds.Count -ne 18", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-HomeRenovationCase $negativeId $false", script, StringComparison.Ordinal);
        Assert.Contains("native_RenovateMenu_hover_and_world_region_click_completed", script, StringComparison.Ordinal);
        Assert.Contains("if ($VisibleGame) { \"Normal\" } else { \"Hidden\" }", script, StringComparison.Ordinal);
        Assert.Contains("SDL_AUDIODRIVER", script, StringComparison.Ordinal);
        Assert.Contains("ALSOFT_DRIVERS", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionExecutorUsesNativeMenusAndDirectStateConstructionStaysInDebugFixture()
    {
        var root = FindRepositoryRoot();
        var production = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.HomeRenovations.cs"));
        var fixture = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.HomeRenovationFixture.cs"));

        Assert.Contains("active.Service.checkAction", production, StringComparison.Ordinal);
        Assert.Contains("active.Service.answerDialogue(response)", production, StringComparison.Ordinal);
        Assert.Contains("shop.receiveLeftClick", production, StringComparison.Ordinal);
        Assert.Contains("menu.performHoverAction", production, StringComparison.Ordinal);
        Assert.Contains("menu.receiveLeftClick", production, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Money -=", production, StringComparison.Ordinal);
        Assert.DoesNotContain("mailReceived.Add", production, StringComparison.Ordinal);
        Assert.DoesNotContain("field.Value =", production, StringComparison.Ordinal);
        Assert.Contains("debug_setup_home_renovation", fixture, StringComparison.Ordinal);
        Assert.Contains("SetHomeRenovationRequirement", fixture, StringComparison.Ordinal);
        Assert.Contains("ClearHomeRenovationRegion", fixture, StringComparison.Ordinal);
    }

    [Fact]
    public void TypedTransportAndPlayerCommandInvocationSourceReachTheRuntime()
    {
        var root = FindRepositoryRoot();
        var transport = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.LiveTrainingLoop", "Program.RuntimeExecution.HomeRenovations.cs"));
        var queue = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.LiveTrainingLoop", "Program.QueueBuilding.cs"));

        Assert.Contains("request.RenovationId", transport, StringComparison.Ordinal);
        Assert.Contains("request.RenovationProjectionFingerprint", transport, StringComparison.Ordinal);
        Assert.Contains("invocation_source = options.DailyPlanInvocationSource", queue, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}

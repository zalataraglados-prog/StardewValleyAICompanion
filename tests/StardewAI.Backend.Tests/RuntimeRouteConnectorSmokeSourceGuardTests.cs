namespace StardewAI.Backend.Tests;

public sealed class RuntimeRouteConnectorSmokeSourceGuardTests
{
    private static readonly string Script = RuntimeHarnessSources.RepositoryFile(
        "scripts",
        "Invoke-RuntimeRouteConnectorSmoke.ps1");

    [Fact]
    public void HighLevelModeUsesProductionCandidatePlanQueueAndNativeExecutor()
    {
        Assert.Contains("[switch] $HighLevelVisit", Script, StringComparison.Ordinal);
        Assert.Contains("exploration.visit_location", Script, StringComparison.Ordinal);
        Assert.Contains("route_connector_tile", Script, StringComparison.Ordinal);
        Assert.Contains("traverse_connector", Script, StringComparison.Ordinal);
        Assert.Contains("executor.traverse_connector", Script, StringComparison.Ordinal);
        Assert.Contains("ranking-response-0001.json", Script, StringComparison.Ordinal);
        Assert.Contains("daily-plan-response-0001.json", Script, StringComparison.Ordinal);
        Assert.Contains("compiled-queue-0001.json", Script, StringComparison.Ordinal);
        Assert.Contains("EVD-218", Script, StringComparison.Ordinal);
    }

    [Fact]
    public void HighLevelModeIsIsolatedSilentAndCleansUpProcesses()
    {
        Assert.Contains("STARDEWAI_SAVE_ISOLATION_PATH", Script, StringComparison.Ordinal);
        Assert.Contains("$env:SDL_AUDIODRIVER = \"dummy\"", Script, StringComparison.Ordinal);
        Assert.Contains("$env:ALSOFT_DRIVERS = \"null\"", Script, StringComparison.Ordinal);
        Assert.Contains("-WindowStyle Hidden", Script, StringComparison.Ordinal);
        Assert.Contains("finally {", Script, StringComparison.Ordinal);
        Assert.Contains("Stop-Process", Script, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.", Script, StringComparison.Ordinal);
    }
}

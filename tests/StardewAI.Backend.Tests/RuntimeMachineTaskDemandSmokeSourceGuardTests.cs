namespace StardewAI.Backend.Tests;

public sealed class RuntimeMachineTaskDemandSmokeSourceGuardTests
{
    private static readonly string Script = RuntimeHarnessSources.RepositoryFile(
        "scripts",
        "Invoke-RuntimeMachineTaskDemandSmoke.ps1");

    [Fact]
    public void SmokeUsesNativeSourceThenReceiptForBothTaskFamilies()
    {
        Assert.Contains("debug.setup_collection_task_fixture", Script, StringComparison.Ordinal);
        Assert.Contains("ordinary_quest", Script, StringComparison.Ordinal);
        Assert.Contains("special_order", Script, StringComparison.Ordinal);
        Assert.Contains("executor.load_machine_input", Script, StringComparison.Ordinal);
        Assert.Contains("executor.collect_machine_output", Script, StringComparison.Ordinal);
        Assert.Contains("quest_acquisition_source_step", Script, StringComparison.Ordinal);
        Assert.Contains("quest_acquisition_target_step", Script, StringComparison.Ordinal);
        Assert.Contains("progress_after_load", Script, StringComparison.Ordinal);
        Assert.Contains("progress_after_collect", Script, StringComparison.Ordinal);
        Assert.DoesNotContain("numberCollected.Value =", Script, StringComparison.Ordinal);
        Assert.DoesNotContain("SetCount(", Script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeIsIsolatedSilentAndAlwaysStopsTheGame()
    {
        Assert.Contains("STARDEWAI_SAVE_ISOLATION_PATH", Script, StringComparison.Ordinal);
        Assert.Contains("$env:SDL_AUDIODRIVER = \"dummy\"", Script, StringComparison.Ordinal);
        Assert.Contains("$env:ALSOFT_DRIVERS = \"null\"", Script, StringComparison.Ordinal);
        Assert.Contains("-WindowStyle Hidden", Script, StringComparison.Ordinal);
        Assert.Contains("finally {", Script, StringComparison.Ordinal);
        Assert.Contains("Stop-Process", Script, StringComparison.Ordinal);
    }
}

namespace StardewAI.Backend.Tests;

public sealed class RuntimeSupportedMachineCapacityLifecycleSmokeSourceGuardTests
{
    private static readonly string Script = RuntimeHarnessSources.RepositoryFile(
        "scripts",
        "Invoke-RuntimeSupportedMachineCapacityLifecycleSmoke.ps1");

    [Fact]
    public void SmokeUsesOneHighLevelOptionAcrossNativeLifecycleStages()
    {
        Assert.Contains(
            "farm.establish_supported_machine_capacity",
            Script,
            StringComparison.Ordinal);
        Assert.Contains(
            "executor.craft_machine_item",
            Script,
            StringComparison.Ordinal);
        Assert.Contains(
            "executor.place_machine",
            Script,
            StringComparison.Ordinal);
        Assert.Contains(
            "executor.load_machine_input",
            Script,
            StringComparison.Ordinal);
        Assert.Contains("craft_selected", Script, StringComparison.Ordinal);
        Assert.Contains("placement_bound", Script, StringComparison.Ordinal);
        Assert.Contains(
            "exact_target_machine_processing_observed",
            Script,
            StringComparison.Ordinal);
        Assert.Contains(
            "live-training-feature-rows.jsonl",
            Script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeIsIsolatedSilentAndAlwaysCleansUp()
    {
        Assert.Contains(
            "STARDEWAI_SAVE_ISOLATION_PATH",
            Script,
            StringComparison.Ordinal);
        Assert.Contains(
            "STARDEWAI_STRATEGY_LEDGER_DIR",
            Script,
            StringComparison.Ordinal);
        Assert.Contains(
            "$env:SDL_AUDIODRIVER = \"dummy\"",
            Script,
            StringComparison.Ordinal);
        Assert.Contains(
            "$env:ALSOFT_DRIVERS = \"null\"",
            Script,
            StringComparison.Ordinal);
        Assert.Contains("-WindowStyle Hidden", Script, StringComparison.Ordinal);
        Assert.Contains("finally {", Script, StringComparison.Ordinal);
        Assert.Contains("Stop-Process", Script, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Game1.player.",
            Script,
            StringComparison.Ordinal);
    }
}

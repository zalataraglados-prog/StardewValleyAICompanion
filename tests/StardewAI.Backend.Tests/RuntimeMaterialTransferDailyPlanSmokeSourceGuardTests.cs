namespace StardewAI.Backend.Tests;

public sealed class RuntimeMaterialTransferDailyPlanSmokeSourceGuardTests
{
    [Fact]
    public void SmokeUsesExplicitHighLevelCandidateAndNativeBidirectionalEvidence()
    {
        var script = RuntimeHarnessSources.RepositoryFile(
            "scripts",
            "Invoke-RuntimeMaterialTransferDailyPlanSmoke.ps1");

        Assert.Contains("--daily-plan-candidate-options\", \"inventory.transfer_item", script, StringComparison.Ordinal);
        Assert.Contains("chest-to-player", script, StringComparison.Ordinal);
        Assert.Contains("player-to-chest", script, StringComparison.Ordinal);
        Assert.Contains("executor.transfer_material", script, StringComparison.Ordinal);
        Assert.Contains("material_transfer_native_menu_opened", script, StringComparison.Ordinal);
        Assert.Contains("material_transfer_native_lock_released", script, StringComparison.Ordinal);
        Assert.Contains("plan-execution-episode-0001.json", script, StringComparison.Ordinal);
        Assert.Contains("live-training-feature-rows.jsonl", script, StringComparison.Ordinal);
        Assert.Contains("WindowStyle Hidden", script, StringComparison.Ordinal);
        Assert.Contains("SDL_AUDIODRIVER = \"dummy\"", script, StringComparison.Ordinal);
        Assert.Contains("debug_fixture_excluded_from_action_evidence", script, StringComparison.Ordinal);
        Assert.Contains("debug.setup_material_transfer_target", script, StringComparison.Ordinal);
        Assert.Contains("Dynamic material transfer fixture did not report", script, StringComparison.Ordinal);
        Assert.Contains("material_transfer_source_projection_drifted", script, StringComparison.Ordinal);
        Assert.Contains("negative-stale-result.json", script, StringComparison.Ordinal);
    }
}

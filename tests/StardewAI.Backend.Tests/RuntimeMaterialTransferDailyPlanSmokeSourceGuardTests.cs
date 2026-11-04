namespace StardewAI.Backend.Tests;

public sealed class RuntimeMaterialTransferDailyPlanSmokeSourceGuardTests
{
    [Fact]
    public void SmokeUsesExplicitHighLevelCandidateAndNativeBidirectionalEvidence()
    {
        var script = RuntimeHarnessSources.RepositoryFile(
            "scripts",
            "Invoke-RuntimeMaterialTransferDailyPlanSmoke.ps1");

        var helper = RuntimeHarnessSources.RepositoryFile("scripts", "lib", "RuntimeMaterialTransferSmoke.ps1");
        var common = RuntimeHarnessSources.RepositoryFile("scripts", "lib", "RuntimeEvidenceCommon.ps1");
        Assert.Contains("-OptionId 'inventory.transfer_item'", helper, StringComparison.Ordinal);
        Assert.Contains("Invoke-RuntimeRanking", helper, StringComparison.Ordinal);
        Assert.Contains("Invoke-RuntimeDailyPlanStep", helper, StringComparison.Ordinal);
        Assert.Contains("chest-to-player", script, StringComparison.Ordinal);
        Assert.Contains("player-to-chest", script, StringComparison.Ordinal);
        Assert.Contains("executor.transfer_material", helper, StringComparison.Ordinal);
        Assert.Contains("material_transfer_native_menu_opened", helper, StringComparison.Ordinal);
        Assert.Contains("material_transfer_native_lock_released", helper, StringComparison.Ordinal);
        Assert.Contains("plan-execution-episode-0001.json", helper, StringComparison.Ordinal);
        Assert.Contains("--skip-training", common, StringComparison.Ordinal);
        Assert.Contains("Start-RuntimeEvidenceProcess", script, StringComparison.Ordinal);
        Assert.Contains("SDL_AUDIODRIVER = 'dummy'", script, StringComparison.Ordinal);
        Assert.Contains("debug_fixture_excluded_from_action_evidence", script, StringComparison.Ordinal);
        Assert.Contains("debug.setup_material_transfer_target", script, StringComparison.Ordinal);
        Assert.Contains("Dynamic material transfer fixture did not report", script, StringComparison.Ordinal);
        Assert.Contains("material_transfer_source_projection_drifted", helper, StringComparison.Ordinal);
        Assert.Contains("negative-stale-result.json", helper, StringComparison.Ordinal);
        Assert.Contains("GetMethod('Project')", helper, StringComparison.Ordinal);
        Assert.Contains("GetMethod('VerifyNativeSlots')", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeCopiesTheSourceAndUsesAnExactModSetAndDedicatedLedger()
    {
        var script = RuntimeHarnessSources.RepositoryFile("scripts", "Invoke-RuntimeMaterialTransferDailyPlanSmoke.ps1");
        Assert.Contains("Assert-RuntimeSourceSaveSlot", script, StringComparison.Ordinal);
        Assert.Contains("Copy-Item -LiteralPath $sourceSave", script, StringComparison.Ordinal);
        Assert.Contains("'isolated-saves'", script, StringComparison.Ordinal);
        Assert.Contains("Get-RuntimeExactModAllowlist", script, StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_STRATEGY_LEDGER_DIR = $ledgerDirectory", script, StringComparison.Ordinal);
        Assert.Contains("$ledger.revision -ne 0", script, StringComparison.Ordinal);
        Assert.Contains("Save-RuntimeProcessEnvironment", script, StringComparison.Ordinal);
        Assert.Contains("Restore-RuntimeProcessEnvironment", script, StringComparison.Ordinal);
        Assert.Contains("Stop-RuntimeEvidenceProcesses", script, StringComparison.Ordinal);
        Assert.Contains("-GamePath $gameDirectory", script, StringComparison.Ordinal);
        Assert.Contains("-p:GamePath=$gameDirectory", script, StringComparison.Ordinal);
        Assert.DoesNotContain("--train-every", script, StringComparison.Ordinal);
        Assert.DoesNotContain("training_dataset_count", script, StringComparison.Ordinal);
        Assert.DoesNotContain("\"run\", \"--no-restore\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SparseSetupIsExplicitAndRejectedBeforeAnyNativeWorldMutation()
    {
        var fixture = RuntimeHarnessSources.File("ModEntry.MaterialInventoryGraph.cs");
        var contents = RuntimeHarnessSources.File("ModEntry.MaterialTransfer.Fixture.cs");
        Assert.True(fixture.IndexOf("material_transfer_fixture_profile_unsupported", StringComparison.Ordinal) <
            fixture.IndexOf("farm.objects.Remove(tile)", StringComparison.Ordinal));
        Assert.Contains("5 => wood", contents, StringComparison.Ordinal);
        Assert.Contains("9 => ItemRegistry.Create", contents, StringComparison.Ordinal);
        Assert.Contains("item is null", contents, StringComparison.Ordinal);
        Assert.Contains("MaterialTransferFixtureContentsMatch", fixture, StringComparison.Ordinal);
    }

    [Fact]
    public void FixtureProfileIsOptionalAndDoesNotChangeLegacyRequestJson()
    {
        var request = new StardewAI.Contracts.Training.TrainingExecutionRequest();
        Assert.Null(request.FixtureMaterialTransferProfile);
        Assert.DoesNotContain("fixture_material_transfer_profile", System.Text.Json.JsonSerializer.Serialize(request), StringComparison.Ordinal);
        var parsed = System.Text.Json.JsonSerializer.Deserialize<StardewAI.Contracts.Training.TrainingExecutionRequest>(
            "{\"fixture_material_transfer_profile\":\"sparse_source\"}");
        Assert.Equal("sparse_source", parsed!.FixtureMaterialTransferProfile);
    }
}

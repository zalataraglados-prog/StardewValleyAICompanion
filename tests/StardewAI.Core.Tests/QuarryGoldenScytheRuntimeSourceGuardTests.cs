namespace StardewAI.Core.Tests;

public sealed class QuarryGoldenScytheRuntimeSourceGuardTests
{
    [Fact]
    public void RuntimeLoopUsesOnlyTheTwoRequiredSmokeMods()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeQuarryGoldenScytheLoop.ps1");

        Assert.Contains(
            "$env:SMAPI_MODS_PATH = $smokeModsPath",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "loaded_mod_allowlist = @(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"StardewAI.TransparentBridge\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"StardewAI.RuntimeTestHarness\"",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\"JunimoTestClient\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"--bridge-snapshot-url\", \"http://127.0.0.1:8765/api/v1/snapshot?profile=mining\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"--executor-timeout-seconds\", \"600\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"--use-daily-plan\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"--daily-plan-candidate-options\", \"mining.acquire_golden_scythe\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"--daily-plan-candidate-kind\", \"mining_acquire_golden_scythe_plan_envelope\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"--daily-plan-candidate-id\", \"mining:acquire_golden_scythe\"",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\"--use-parameterized-action\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "option_id = \"executor.close_menu\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "$worldSnapshot = Clear-TransientMenus",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Clear-TransientMenus -Snapshot $postSetupWorld",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "$runtimeSaves = Join-Path $runDirectory \"isolated-saves\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Copy-Item -LiteralPath $oldSave -Destination $currentSave -Force",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Remove-Item -Recurse",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "continued Quarry Mine clearance after the reward had already been claimed",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Golden Scythe claim and native Quarry Mine exit were both verified",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"combat_disengaged_transit_target\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"combat_target_not_found_or_moved\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"mine_stone_target_not_breakable_stone\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "[bool]$execution.after_snapshot_fresh -and",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "[bool]$execution.state_hash_changed",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LiveLoopPreservesPurposeProfileForBeforeAndAfterIngest()
    {
        var program = ReadRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.cs");
        var runtime = ReadRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.RuntimeExecution.cs");
        var http = ReadRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.JsonHttp.cs");

        Assert.Contains("SnapshotIngestUrl(options)", program);
        Assert.Contains("SnapshotIngestUrl(options)", runtime);
        Assert.Contains(
            "ingestUrl + \"?profile=\"",
            http,
            StringComparison.Ordinal);
        Assert.Contains(
            "Timeout = TimeSpan.FromSeconds(options.ExecutorTimeoutSeconds)",
            program,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NativeExitPromptIsConfirmedBeforeMovementStateGates()
    {
        var source = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Mining.Traversal.cs");
        var promptIndex = source.IndexOf(
            "if (active.PromptOpened)",
            StringComparison.Ordinal);
        var movementGateIndex = source.IndexOf(
            "if (Game1.player.UsingTool ||",
            promptIndex,
            StringComparison.Ordinal);

        Assert.True(promptIndex >= 0);
        Assert.True(movementGateIndex > promptIndex);
        Assert.Contains(
            "answerDialogueAction(",
            source[promptIndex..movementGateIndex],
            StringComparison.Ordinal);
    }

    [Fact]
    public void MeleeReachDoesNotAssumeWeaponRangeCanCrossMineObstacles()
    {
        var source = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Mining.Combat.cs");
        var start = source.IndexOf(
            "private static bool IsMonsterWithinCombatReach",
            StringComparison.Ordinal);
        var end = source.IndexOf(
            "private static int TrackCombatProgress",
            start,
            StringComparison.Ordinal);
        var reachSource = source[start..end];

        Assert.Contains(
            "AreAdjacent(Game1.player.TilePoint, target.TilePoint)",
            reachSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "targetBox.Intersects(Game1.player.GetBoundingBox())",
            reachSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "addedAreaOfEffect",
            reachSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CombatReusesNativeHeavyHitterInputSemantics()
    {
        var source = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Mining.Combat.cs");

        Assert.Contains(
            "HeavyHitterInputButton(active.Weapon)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TryStartReactiveMineCombat(",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "private void MoveTowardCombatTarget",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MineStoneWaitsForSharedNativeToolLifecycleSettlement()
    {
        var source = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MiningResources.cs");
        var state = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.State.Mining.cs");
        var mineStoneState = state[
            state.IndexOf("private sealed class ActiveMineStone", StringComparison.Ordinal)..
            state.IndexOf("private sealed class ActiveResourceClump", StringComparison.Ordinal)];

        Assert.Contains(
            "active.Lifecycle.Advance(ObserveNativeToolAction())",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "NativeToolActionCommand.CycleCompleted",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TickRemovedMineStone(active)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "public NativeToolActionLifecycle Lifecycle { get; } = new();",
            mineStoneState,
            StringComparison.Ordinal);
        Assert.DoesNotContain("BeginIssued", mineStoneState, StringComparison.Ordinal);
        Assert.DoesNotContain("ReleaseIssued", mineStoneState, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(
            AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
            !File.Exists(
                Path.Combine(
                    directory.FullName,
                    "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return File.ReadAllText(
            Path.Combine(
                directory?.FullName ??
                    throw new InvalidOperationException(
                        "Cannot find repository root."),
                Path.Combine(segments)));
    }
}

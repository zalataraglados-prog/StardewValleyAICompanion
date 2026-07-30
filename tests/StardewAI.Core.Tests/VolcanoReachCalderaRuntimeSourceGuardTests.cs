namespace StardewAI.Core.Tests;

public sealed class VolcanoReachCalderaRuntimeSourceGuardTests
{
    [Fact]
    public void RuntimeLoopUsesOnlyTheTwoRequiredSmokeMods()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeVolcanoReachCalderaLoop.ps1");

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
            "\"--bridge-snapshot-url\", \"http://127.0.0.1:8765/api/v1/snapshot?profile=volcano&fresh=true\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"--executor-timeout-seconds\", \"600\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"volcano_obstacle_unsafe_monster_window\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"volcano_cooling_unsafe_monster_window\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"volcano_cooling_path_unavailable:movement_no_collision_safe_path\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"volcano_combat_dynamic_path_unavailable:unreachable_target\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"volcano_combat_disengaged_transit_target\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"volcano_combat_movement_budget_exceeded\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"volcano_movement_unsafe_monster_window\"",
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
        Assert.Contains(
            "STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "[switch] $FreezeClockWhileExecutorIdle",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "continuous_world_during_actions_snapshots_and_external_orchestration",
            source,
            StringComparison.Ordinal);

        var harness = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.cs");
        Assert.Contains(
            "Game1.paused = true;",
            harness,
            StringComparison.Ordinal);
        Assert.Contains(
            "else if (executorIdlePauseApplied)",
            harness,
            StringComparison.Ordinal);
        Assert.Contains(
            "Game1.paused = false;",
            harness,
            StringComparison.Ordinal);
        Assert.Contains(
            "Game1.options.pauseWhenOutOfFocus = false;",
            harness,
            StringComparison.Ordinal);

        var movement = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MovementSleep.cs");
        Assert.Contains(
            "!HasReachedTurnCenter(currentTile, move.CurrentDirection.Value)",
            movement,
            StringComparison.Ordinal);
        Assert.Contains(
            "Game1.player.forceCanMove();",
            ReadRepositoryFile(
                "tools",
                "StardewAI.RuntimeTestHarness",
                "ModEntry.MovementSleep.PathingInput.cs"),
            StringComparison.Ordinal);

        var bridge = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "ModEntry.cs");
        Assert.Contains(
            "SnapshotForceRefresh(request)",
            bridge,
            StringComparison.Ordinal);
        Assert.Contains(
            "!group.Key.ForceRefresh",
            bridge,
            StringComparison.Ordinal);
        var volcanoProfileStart = bridge.IndexOf(
            "if (profile is \"volcano\")",
            StringComparison.Ordinal);
        var volcanoProfileEnd = bridge.IndexOf(
            "return domains;",
            volcanoProfileStart,
            StringComparison.Ordinal);
        var volcanoProfile = bridge[
            volcanoProfileStart..volcanoProfileEnd];
        Assert.Contains(
            "domains.Add(\"volcano\");",
            volcanoProfile,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "domains.Add(\"current_location\");",
            volcanoProfile,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "domains.Add(\"locations\");",
            volcanoProfile,
            StringComparison.Ordinal);
    }

    [Fact]
    public void VolcanoCombatReusesNativeMeleeInputSemantics()
    {
        var source = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Volcano.Combat.cs");

        Assert.Contains(
            "HeavyHitterInputButton(active.Weapon)",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "TryApplySmapiButtonOverride(SButton.C",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "allowRemovableObstacles: true",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TickVolcanoCombatClearance(active, volcano)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "volcano_combat_route_crosses_connector",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "volcano.combat.route_clearance[",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TryStartEmergencyCombatFood(volcano)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TryStartReactiveVolcanoCombat(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "internal-reactive-volcano-combat",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "private void TickVolcanoAutoCombat()",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"hostile_location_idle_guard\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TickVolcanoCombatLootSweep(active, volcano)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "!active.DebrisBefore.Contains(debris)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "combat_drops_collected_by_native_proximity",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "activeEmergencyCombatFood is not null",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "active.LastNoProgressReason",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "active.Target.isGlider.Value",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "active.LastProgressTargetPosition",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ShouldDisengageCombatIntent(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TryResolveCombatIntent(request, out var combatIntent)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "active.InitialTargetTile",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "volcano_combat_defeat_animation_settle_timeout",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "active.DefeatSettleTicks++",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TickVolcanoCombatDefeatDialogue(active)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "dialogue.characterDialogue is not null",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "volcano_combat_defeat_interrupted_by_non_incidental_menu",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TryApplySmapiLeftButtonOverride(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ResetVolcanoCombatClearance(active)",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "volcano_combat_clearance_target_no_longer_adjacent",
            source,
            StringComparison.Ordinal);

        var recovery = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Mining.CombatRecovery.cs");
        Assert.Contains(
            "EmergencyCombatFoodWasConsumed(active)",
            recovery,
            StringComparison.Ordinal);
        Assert.Contains(
            "native_food_consumed_animation_recovered",
            recovery,
            StringComparison.Ordinal);
        Assert.Contains(
            "Game1.player.completelyStopAnimatingOrDoingAction();",
            recovery,
            StringComparison.Ordinal);
    }

    [Fact]
    public void VolcanoBridgeSeparatesStaticAndDynamicCollision()
    {
        var source = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "VolcanoReadAdapter.cs");
        var planner = ReadRepositoryFile(
            "src",
            "StardewAI.Core",
            "Execution",
            "VolcanoFloorStepPlanner.cs");

        Assert.Contains(
            "static_blocked_rows = staticRows",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "volcano.isTilePassable(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "staticRow[x] = cooled ||",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "static_blocked_rows",
            planner,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "!monsterByTile.ContainsKey(next)",
            planner,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "return immediateThreat;",
            planner,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "BuildRouteMonsterPlan(",
            planner,
            StringComparison.Ordinal);
    }

    [Fact]
    public void VolcanoMovementYieldsToImmediateCombat()
    {
        var source = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MovementSleep.cs");

        Assert.Contains(
            "Game1.currentLocation is VolcanoDungeon volcano",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ImmediateVolcanoThreat(volcano)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "volcano_movement_unsafe_monster_window",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "connectorCommitReady",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (ReplanTileMove(move, avoidSoftObstacles: true))",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ManhattanDistance(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TickMovementIncidentalDialogue(move)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "movement_interrupted_by_non_incidental_menu",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TryApplySmapiLeftButtonOverride(",
            source,
            StringComparison.Ordinal);
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

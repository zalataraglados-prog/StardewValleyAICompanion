using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed partial class MiningFloorStepPlannerTests
{
    [Fact]
    public void RuntimeMineStoneUsesNativeToolLifecycleWithoutDirectToolFunction()
    {
        var source = RuntimeHarnessSources.All;
        var start = source.IndexOf("private void StartMineStone", StringComparison.Ordinal);
        var end = source.IndexOf("\n    private ", start + 1, StringComparison.Ordinal);
        var mineStoneSource = source[start..end];
        var tickStart = source.IndexOf("private void TickMineStoneCore", StringComparison.Ordinal);
        var tickEnd = source.IndexOf("\n    private ", tickStart + 1, StringComparison.Ordinal);
        var tickMineStoneSource = source[tickStart..tickEnd];
        var removedStart = source.IndexOf("private void TickRemovedMineStone", StringComparison.Ordinal);
        var removedEnd = source.IndexOf("\n    private ", removedStart + 1, StringComparison.Ordinal);
        var removedMineStoneSource = source[removedStart..removedEnd];
        var advanceStart = source.IndexOf("private bool AdvanceMineStonePath", StringComparison.Ordinal);
        var advanceEnd = source.IndexOf("\n    private ", advanceStart + 1, StringComparison.Ordinal);
        var advanceMineStoneSource = source[advanceStart..advanceEnd];

        Assert.Contains("executor.mine_stone", source, StringComparison.Ordinal);
        Assert.Contains("Game1.player.BeginUsingTool()", tickMineStoneSource, StringComparison.Ordinal);
        Assert.Contains("Game1.player.EndUsingTool()", tickMineStoneSource, StringComparison.Ordinal);
        Assert.Contains("active.Lifecycle.Advance(ObserveNativeToolAction())", tickMineStoneSource, StringComparison.Ordinal);
        Assert.Contains("TickRemovedMineStone(active)", tickMineStoneSource, StringComparison.Ordinal);
        Assert.Contains("NativeToolActionCommand.CycleCompleted", removedMineStoneSource, StringComparison.Ordinal);
        Assert.Contains("active.ObservedHealth.Add(0)", removedMineStoneSource, StringComparison.Ordinal);
        Assert.Contains("native_pickaxe_lifecycle_removed_breakable_stone", source, StringComparison.Ordinal);
        Assert.Contains("ImmediateMiningThreat(mine)", tickMineStoneSource, StringComparison.Ordinal);
        Assert.Contains("active.CombatInterrupted = true", tickMineStoneSource, StringComparison.Ordinal);
        Assert.Contains("active.ElapsedTicks - active.CombatInterruptedTicks", tickMineStoneSource, StringComparison.Ordinal);
        Assert.Contains("AdvanceMineStonePath(active)", tickMineStoneSource, StringComparison.Ordinal);
        Assert.Contains("active.MovementTiles++", advanceMineStoneSource, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "active.MovementTiles += ManhattanDistance",
            tickMineStoneSource,
            StringComparison.Ordinal);
        Assert.Contains("activeMineStone?.CombatInterrupted == true", source, StringComparison.Ordinal);
        Assert.Contains("TickDeferredCombatRestore()", source, StringComparison.Ordinal);
        Assert.Contains("TryStartReactiveMineCombat(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".DoFunction(", mineStoneSource, StringComparison.Ordinal);
        Assert.DoesNotContain("objects.Remove", mineStoneSource, StringComparison.Ordinal);

        var smoke = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "scripts",
            "Invoke-RuntimeMiningSnapshotSmoke.ps1"));
        Assert.Contains("[switch] $MineOneStone", smoke, StringComparison.Ordinal);
        Assert.Contains("option_id = \"executor.mine_stone\"", smoke, StringComparison.Ordinal);
        Assert.Contains("mine_stone_native_swing_count", smoke, StringComparison.Ordinal);
        Assert.Contains("terminal zero-health observation", smoke, StringComparison.Ordinal);
        Assert.Contains("mine_stone_removed", smoke, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeMineStoneUsesCompilerStandTileAndReplansDynamicObstacles()
    {
        var source = RuntimeHarnessSources.All;
        var start = source.IndexOf("private void StartMineStone", StringComparison.Ordinal);
        var end = source.IndexOf("\n    private ", start + 1, StringComparison.Ordinal);
        var mineStoneSource = source[start..end];
        var tickStart = source.IndexOf("private void TickMineStoneCore", StringComparison.Ordinal);
        var tickEnd = source.IndexOf("\n    private ", tickStart + 1, StringComparison.Ordinal);
        var tickMineStoneSource = source[tickStart..tickEnd];
        var pathStart = source.IndexOf("private static List<Point>? BuildCompilerAdjacentPath", StringComparison.Ordinal);
        var pathEnd = source.IndexOf("\n    private ", pathStart + 1, StringComparison.Ordinal);
        var pathSource = source[pathStart..pathEnd];

        Assert.Contains("request.StandTileX", mineStoneSource, StringComparison.Ordinal);
        Assert.Contains("BuildCompilerAdjacentPath", mineStoneSource, StringComparison.Ordinal);
        Assert.Contains("avoidSoftObstacles: true", pathSource, StringComparison.Ordinal);
        Assert.Contains("allowRemovableObstacles: false", pathSource, StringComparison.Ordinal);
        Assert.Contains("TryReplanMineStone", tickMineStoneSource, StringComparison.Ordinal);
        Assert.Contains("mine_stone_dynamic_path_unavailable", tickMineStoneSource, StringComparison.Ordinal);
        Assert.Contains("path ?? new List<Point>()", mineStoneSource, StringComparison.Ordinal);
        Assert.Contains("active.CombatInterruptedTicks++", tickMineStoneSource, StringComparison.Ordinal);
        Assert.Contains("StopAllMovement();", tickMineStoneSource, StringComparison.Ordinal);
        Assert.DoesNotContain("mine_stone_path_changed", mineStoneSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeMineTransitionsUseCompilerStandTilesWithoutImplicitClearance()
    {
        var source = RuntimeHarnessSources.All;
        foreach (var method in new[] { "StartDescendLadder", "StartDescendShaft", "StartExitMine" })
        {
            var start = source.IndexOf("private void " + method, StringComparison.Ordinal);
            var end = source.IndexOf("\n    private ", start + 1, StringComparison.Ordinal);
            var methodSource = source[start..end];
            Assert.Contains("request.StandTileX", methodSource, StringComparison.Ordinal);
            Assert.Contains(method == "StartExitMine" ? "BuildCompilerMineExitPath" : "BuildCompilerAdjacentPath", methodSource, StringComparison.Ordinal);
        }

        var helperStart = source.IndexOf("private static List<Point>? BuildCompilerAdjacentPath", StringComparison.Ordinal);
        var helperEnd = source.IndexOf("\n    private void TickMineStone", helperStart, StringComparison.Ordinal);
        var helperSource = source[helperStart..helperEnd];
        Assert.Contains("avoidSoftObstacles: true", helperSource, StringComparison.Ordinal);
        Assert.Contains("allowRemovableObstacles: false", helperSource, StringComparison.Ordinal);
        Assert.Contains("TryReplanDescendLadder", source, StringComparison.Ordinal);
        Assert.Contains("TryReplanDescendShaft", source, StringComparison.Ordinal);
        Assert.Contains("TryReplanExitMine", source, StringComparison.Ordinal);
        Assert.Contains("IsNativeExitMinePrompt(active, postClaimDialogue)", source, StringComparison.Ordinal);
        Assert.Contains("active.PromptOpened = true;", source, StringComparison.Ordinal);
        Assert.Contains("IsGoldenScytheClaimDialogue(active, postClaimDialogue)", source, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiLeftButtonOverride(pressed: true", source, StringComparison.Ordinal);
        Assert.Contains("exit_mine_unexpected_dialogue_before_move", source, StringComparison.Ordinal);
        Assert.Contains("if (activeEmergencyCombatFood is not null)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeMiningCalibrationLoadoutIsSandboxScopedAndRuntimeDataDriven()
    {
        var root = FindRepositoryRoot();
        var source = RuntimeHarnessSources.All;
        var smoke = File.ReadAllText(Path.Combine(root, "scripts", "Invoke-RuntimeMiningSnapshotSmoke.ps1"));
        var loop = File.ReadAllText(Path.Combine(root, "scripts", "Invoke-RuntimeMiningReachDepthLoop.ps1"));
        var start = source.IndexOf("private static MiningCalibrationLoadoutFacts EnsureMiningCalibrationLoadout", StringComparison.Ordinal);
        var end = source.IndexOf("private static MineFishingFixtureFacts EnsureMineFishingFixtureEquipment", start, StringComparison.Ordinal);
        var loadoutSource = source[start..end];

        Assert.Contains("STARDEWAI_MINING_CALIBRATION_LOADOUT", source, StringComparison.Ordinal);
        Assert.Contains("Game1.objectData.Keys", loadoutSource, StringComparison.Ordinal);
        Assert.Contains("new MeleeWeapon(itemId.ToString())", loadoutSource, StringComparison.Ordinal);
        Assert.Contains("healthRecoveredOnConsumption()", loadoutSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.health =", loadoutSource, StringComparison.Ordinal);
        Assert.Contains("[switch] $MiningCalibrationLoadout", smoke, StringComparison.Ordinal);
        Assert.Contains("[switch] $VerifyTransitCombatDisengagement", smoke, StringComparison.Ordinal);
        Assert.Contains("combat_disengaged_transit_target", smoke, StringComparison.Ordinal);
        Assert.Contains("transit_combat_target_remained", smoke, StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_MINING_CALIBRATION_LOADOUT", smoke, StringComparison.Ordinal);
        Assert.Contains("-MiningCalibrationLoadout", loop, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeBreakContainerUsesNativeHeavyHitterInputAndVerifiesRemoval()
    {
        var root = FindRepositoryRoot();
        var source = RuntimeHarnessSources.All;
        var driverSource = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "NativeHeavyHitterAction.cs"));
        var start = source.IndexOf("private void StartBreakContainer", StringComparison.Ordinal);
        var end = source.IndexOf("private static bool ImmediateMiningThreat", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var containerSource = source[start..end];

        Assert.Contains("executor.break_container", source, StringComparison.Ordinal);
        Assert.Contains("obj is not BreakableContainer", containerSource, StringComparison.Ordinal);
        Assert.Contains("tool.isHeavyHitter()", containerSource, StringComparison.Ordinal);
        Assert.Contains("TryTickNativeHeavyHitterAction", containerSource, StringComparison.Ordinal);
        Assert.Contains("tool is MeleeWeapon ? SButton.MouseLeft : SButton.C", driverSource, StringComparison.Ordinal);
        Assert.Contains("native_heavy_hitter_input_removed_container", containerSource, StringComparison.Ordinal);
        Assert.Contains("released_contents_left_as_game_debris", containerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("performToolAction(", containerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("objects.Remove", containerSource, StringComparison.Ordinal);

        var smoke = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "Invoke-RuntimeMiningSnapshotSmoke.ps1"));
        Assert.Contains("[switch] $BreakOneContainer", smoke, StringComparison.Ordinal);
        Assert.Contains("[switch] $ForceBreakableContainerFixture", smoke, StringComparison.Ordinal);
        Assert.Contains("option_id = \"executor.break_container\"", smoke, StringComparison.Ordinal);
        Assert.Contains("break_container_removed", smoke, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeCombatUsesFarmerInputAndPreservesTypedFeedback()
    {
        var root = FindRepositoryRoot();
        var source = RuntimeHarnessSources.All;
        var start = source.IndexOf("private void StartCombatMonster", StringComparison.Ordinal);
        var end = source.IndexOf("private void StartSetupMiningFloor", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var combatSource = source[start..end];

        Assert.Contains("executor.combat_monster", source, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiButtonOverride(SButton.C, pressed: true", combatSource, StringComparison.Ordinal);
        Assert.Contains("PriorityQueue<Point, int>", source, StringComparison.Ordinal);
        Assert.Contains("MovementTraversalCost(location, next)", source, StringComparison.Ordinal);
        Assert.Contains("pickaxe.UpgradeLevel + 1", source, StringComparison.Ordinal);

        var bridgeSource = MiningReadAdapterSources.All;
        Assert.Contains("[\"resource_clumps\"]", bridgeSource, StringComparison.Ordinal);
        Assert.Contains("ResourceClumpRequirement", bridgeSource, StringComparison.Ordinal);
        Assert.Contains("minimum_upgrade_level", bridgeSource, StringComparison.Ordinal);
        Assert.Contains("stone_damage_per_hit", bridgeSource, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiButtonOverride(SButton.C, pressed: false", combatSource, StringComparison.Ordinal);
        Assert.Contains("RuntimeHelpers.GetHashCode(monster)", combatSource, StringComparison.Ordinal);
        Assert.Contains("CombatTargetHealthSequence", combatSource, StringComparison.Ordinal);
        Assert.Contains("CombatPlayerHealthSequence", combatSource, StringComparison.Ordinal);
        Assert.Contains("combat_disengaged_transit_target", combatSource, StringComparison.Ordinal);
        Assert.Contains("ShouldDisengageCombatIntent(", combatSource, StringComparison.Ordinal);
        Assert.Contains("slingshot_disengaged_transit_target", source, StringComparison.Ordinal);
        Assert.Contains("TryStartReactiveMineCombat(", combatSource, StringComparison.Ordinal);
        Assert.Contains("TrainingCombatIntents.TransitSelfDefense", combatSource, StringComparison.Ordinal);
        Assert.Contains("activeCombatMonster = new ActiveCombatMonster(", combatSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private void MoveTowardCombatTarget", combatSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private void TickManualAutoCombatClearance", combatSource, StringComparison.Ordinal);
        Assert.Contains("AreAdjacent(Game1.player.TilePoint, target.TilePoint)", combatSource, StringComparison.Ordinal);
        Assert.Contains("active.Target.GetBoundingBox().Center", combatSource, StringComparison.Ordinal);
        Assert.Contains("BuildAdjacentToolPath(mine, targetTile", combatSource, StringComparison.Ordinal);
        Assert.Contains(".OrderBy(weapon => CombatExpectedHitCount(weapon, target))", combatSource, StringComparison.Ordinal);
        Assert.Contains("private static int CombatExpectedHitCount", combatSource, StringComparison.Ordinal);
        Assert.Contains("Game1.player.maxHealth * 3 / 4", source, StringComparison.Ordinal);
        Assert.Contains("Game1.player.UsingTool", source, StringComparison.Ordinal);
        Assert.Contains("Game1.player.FarmerSprite.PauseForSingleAnimation", source, StringComparison.Ordinal);
        Assert.Contains("StopAllMovement();", combatSource, StringComparison.Ordinal);
        Assert.Contains("ResolveCombatWeapon(target, request.CombatWeaponSlotIndex", combatSource, StringComparison.Ordinal);
        Assert.Contains("weapon.enchantments.Any", combatSource, StringComparison.Ordinal);
        Assert.Contains("AreAdjacent(Game1.player.TilePoint, target.TilePoint)", combatSource, StringComparison.Ordinal);
        Assert.Contains("TrackCombatProgress(active) > 600", combatSource, StringComparison.Ordinal);
        Assert.Contains("combat_no_movement_or_damage_progress", combatSource, StringComparison.Ordinal);
        Assert.Contains("active.Target.isGlider.Value", combatSource, StringComparison.Ordinal);
        Assert.Contains("combat_glider_waiting_for_native_approach", combatSource, StringComparison.Ordinal);
        Assert.Contains("out var pathReason, avoidSoftObstacles: true", combatSource, StringComparison.Ordinal);
        Assert.Contains("ApplyExecutorMovementInput", source, StringComparison.Ordinal);
        Assert.Contains("SButton.W, SButton.D, SButton.S, SButton.A", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.MovePosition", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Position +=", source, StringComparison.Ordinal);
        Assert.DoesNotContain("beforePosition", source, StringComparison.Ordinal);
        Assert.Contains("movedSinceLastTick", source, StringComparison.Ordinal);
        Assert.Contains("ObserveCombatMovement(active)", combatSource, StringComparison.Ordinal);
        Assert.Contains("BeginCombatClearance(active, mine, next)", combatSource, StringComparison.Ordinal);
        Assert.Contains("TickCombatClearance(active, mine)", combatSource, StringComparison.Ordinal);
        Assert.Contains("obj is BreakableContainer", source, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiButtonOverride(SButton.C, pressed: true", combatSource, StringComparison.Ordinal);
        Assert.DoesNotContain("damageMonster(", combatSource, StringComparison.Ordinal);
        Assert.DoesNotContain("takeDamage(", combatSource, StringComparison.Ordinal);
        Assert.DoesNotContain("characters.Remove", combatSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Target.Health =", combatSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.FireTool()", combatSource, StringComparison.Ordinal);

        var smoke = File.ReadAllText(Path.Combine(root, "scripts", "Invoke-RuntimeMiningSnapshotSmoke.ps1"));
        Assert.Contains("[switch] $CombatOneMonster", smoke, StringComparison.Ordinal);
        Assert.Contains("target_runtime_identity", smoke, StringComparison.Ordinal);
        Assert.Contains("combat_target_health_sequence", smoke, StringComparison.Ordinal);
        Assert.Contains("combat_target_removed", smoke, StringComparison.Ordinal);
        Assert.Contains("-TimeoutSeconds 150", smoke, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeSlingshotAndBombUseNativeInputWithoutDirectDamageOrExplosionCalls()
    {
        var source = RuntimeHarnessSources.All;
        var shootStart = source.IndexOf("private void StartShootMonster", StringComparison.Ordinal);
        var bombStart = source.IndexOf("private void StartPlaceBomb", shootStart, StringComparison.Ordinal);
        var meleeStart = source.IndexOf("private void StartCombatMonster", bombStart, StringComparison.Ordinal);
        Assert.True(shootStart >= 0 && bombStart > shootStart && meleeStart > bombStart);
        var shootSource = source[shootStart..bombStart];
        var bombSource = source[bombStart..meleeStart];

        Assert.Contains("executor.shoot_monster", source, StringComparison.Ordinal);
        Assert.Contains("Game1.player.BeginUsingTool()", shootSource, StringComparison.Ordinal);
        Assert.Contains("active.Slingshot.onRelease", shootSource, StringComparison.Ordinal);
        Assert.Contains("HoldTicks < 20", shootSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.setMousePosition", shootSource, StringComparison.Ordinal);
        Assert.Contains("AimPrepared", shootSource, StringComparison.Ordinal);
        Assert.Contains("SlingshotAimPatch.AimWorldPixel", shootSource, StringComparison.Ordinal);
        Assert.Contains("HasClearProjectilePath", shootSource, StringComparison.Ordinal);
        Assert.Contains("ExplosiveAmmoAreaIsSafe", shootSource, StringComparison.Ordinal);
        Assert.Contains("explosive_ammo_player_inside_target_motion_envelope", shootSource, StringComparison.Ordinal);
        Assert.Contains("explosive_ammo_other_farmer_inside_target_motion_envelope", shootSource, StringComparison.Ordinal);
        Assert.Contains("explosive_ammo_protected_object_inside_target_motion_envelope", shootSource, StringComparison.Ordinal);
        Assert.Contains("explosive_ammo_terrain_feature_inside_target_motion_envelope", shootSource, StringComparison.Ordinal);
        Assert.DoesNotContain("damageMonster(", shootSource, StringComparison.Ordinal);
        Assert.DoesNotContain("takeDamage(", shootSource, StringComparison.Ordinal);

        Assert.Contains("executor.place_bomb", source, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiRightButtonOverride(pressed: true", bombSource, StringComparison.Ordinal);
        Assert.Contains("PlaceBombStage.AimPlacement", bombSource, StringComparison.Ordinal);
        Assert.Contains("PrepareNativeBombPlacement", bombSource, StringComparison.Ordinal);
        Assert.Contains("Game1.player.TilePoint != active.Stand", bombSource, StringComparison.Ordinal);
        Assert.Contains("AreAdjacent(active.Stand, active.Target)", bombSource, StringComparison.Ordinal);
        Assert.Contains("Game1.player.FacingDirection != placementDirection", bombSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.GetGrabTile()", bombSource, StringComparison.Ordinal);
        Assert.Contains("PlacementCursorPatch.ScreenPixel", bombSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.setMousePosition", bombSource, StringComparison.Ordinal);
        Assert.Contains("TickBombPathMovement", bombSource, StringComparison.Ordinal);
        Assert.Contains("bomb_escape_finished_inside_damage_square", bombSource, StringComparison.Ordinal);
        Assert.Contains("bomb_target_terminal_state_not_ready", bombSource, StringComparison.Ordinal);
        Assert.Contains("natural_explosion_finalized_target_monster", bombSource, StringComparison.Ordinal);
        Assert.Contains("bomb_target_outside_damage_square", bombSource, StringComparison.Ordinal);
        Assert.Contains("CombatTerminalState = active.TerminalState", bombSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".explode(", bombSource, StringComparison.Ordinal);
        Assert.DoesNotContain("placementAction(", bombSource, StringComparison.Ordinal);

        var meleeSource = source[meleeStart..];
        Assert.Contains("knockdown_requires_bomb_finish", meleeSource, StringComparison.Ordinal);
        Assert.Contains("native_melee_knocked_down_mummy_for_bomb_finish", meleeSource, StringComparison.Ordinal);
        Assert.Contains("mummy.reviveTimer.Value > 0", meleeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TransparentSlingshotProjectionPublishesExplosiveAreaSafetyAndUtility()
    {
        var source = MiningReadAdapterSources.All;
        var start = source.IndexOf("private static object ReadSlingshotAttackProjection", StringComparison.Ordinal);
        var end = source.IndexOf("private static MeleeDamageDistribution BuildSlingshotDamageDistribution", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var projectionSource = source[start..end];

        Assert.Contains("ReadExplosiveAmmoAreaProjection", projectionSource, StringComparison.Ordinal);
        Assert.Contains("explosive_area_safe", projectionSource, StringComparison.Ordinal);
        Assert.Contains("explosive_area_useful_object_hits", projectionSource, StringComparison.Ordinal);
        Assert.Contains("explosive_area_additional_monster_hits", projectionSource, StringComparison.Ordinal);
        Assert.Contains("explosive_area_protected_object_hits", projectionSource, StringComparison.Ordinal);
        Assert.Contains("explosive_area_protected_terrain_feature_hits", projectionSource, StringComparison.Ordinal);
        Assert.Contains("explosive_area_other_farmer_hits", projectionSource, StringComparison.Ordinal);
        Assert.Contains("safe_across_current_target_plus_one_tile_motion_envelope", projectionSource, StringComparison.Ordinal);
        Assert.Contains("complete_direct_projectile_damage_with_exact_area_safety_and_utility_but_uncomposed_area_damage", projectionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeRecoveryUsesNativeEatLifecycleWithoutDirectHealthOrInventoryMutation()
    {
        var root = FindRepositoryRoot();
        var source = RuntimeHarnessSources.All;
        var start = source.IndexOf("private void StartConsumeFood", StringComparison.Ordinal);
        var end = source.IndexOf("private void StartCombatMonster", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var recoverySource = source[start..end];

        Assert.Contains("executor.consume_food", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ImmediateMiningThreat(mine)", recoverySource, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiRightButtonOverride(pressed: true", recoverySource, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiButtonOverride(SButton.Y, pressed: true", recoverySource, StringComparison.Ordinal);
        Assert.Contains("WaitForPromptClose", recoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("answerDialogueAction(\"Eat_Yes\"", recoverySource, StringComparison.Ordinal);
        Assert.Contains("Game1.player.isEating", recoverySource, StringComparison.Ordinal);
        Assert.Contains("active.PreInputSettleTicks++", recoverySource, StringComparison.Ordinal);
        Assert.Contains("consume_food_pre_input_animation_timeout", recoverySource, StringComparison.Ordinal);
        Assert.Contains("consume_food_health_not_recovered", recoverySource, StringComparison.Ordinal);
        Assert.Contains("RestoreConsumeFoodSlot(active)", recoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("eatHeldObject(", recoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("eatObject(", recoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.health =", recoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.health +=", recoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("reduceActiveItemByOne", recoverySource, StringComparison.Ordinal);

        Assert.Contains(
            "TryStartEmergencyCombatFood",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "healthRecoveredOnConsumption()",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ReleaseEmergencyCombatFoodConfirmationButton",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Emergency combat recovery",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TickEmergencyCombatFood();\n        TickCombatMonster();",
            source.Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal);

        var combatTickStart = source.IndexOf(
            "private void TickCombatMonster()",
            StringComparison.Ordinal);
        var combatTickEnd = source.IndexOf(
            "\n    private ",
            combatTickStart + 1,
            StringComparison.Ordinal);
        var combatTickSource = source[combatTickStart..combatTickEnd];
        Assert.Contains(
            "if (activeEmergencyCombatFood is not null)",
            combatTickSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "TryStartEmergencyCombatFood(mine)",
            combatTickSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "BeginCombatClearance(active, mine, next)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "SelectClearanceTool(mine, target)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "active.ClearanceSwings >= 64",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "BeginManualAutoCombatClearance",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimePickupWalksIntoNaturalCollectionAndCombatInterruptsWithoutDirectCollect()
    {
        var source = RuntimeHarnessSources.All;
        var start = source.IndexOf("private void StartPickupDebris", StringComparison.Ordinal);
        var end = source.IndexOf("private static Debris? DebrisAt", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var pickupSource = source[start..end];

        Assert.Contains("TryBuildTilePath", pickupSource, StringComparison.Ordinal);
        Assert.Contains("BuildAdjacentToolPath(", pickupSource, StringComparison.Ordinal);
        Assert.Contains("allowRemovableObstacles: false", pickupSource, StringComparison.Ordinal);
        Assert.Contains("ManhattanDistance(Game1.player.TilePoint, target) <= 1", pickupSource, StringComparison.Ordinal);
        Assert.Contains("MovePlayerForTick()", pickupSource, StringComparison.Ordinal);
        Assert.Contains("ImmediateMiningThreat(mine)", pickupSource, StringComparison.Ordinal);
        Assert.Contains("active.CombatInterrupted = true", pickupSource, StringComparison.Ordinal);
        Assert.Contains("activePickupDebris?.CombatInterrupted == true", source, StringComparison.Ordinal);
        Assert.Contains("active.TransientBusyTicks++", pickupSource, StringComparison.Ordinal);
        Assert.Contains("pickup_debris_transient_tool_state_timeout", pickupSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.UsingTool || !Game1.player.CanMove)", pickupSource[..pickupSource.IndexOf("activePickupDebris = new ActivePickupDebris", StringComparison.Ordinal)], StringComparison.Ordinal);
        Assert.Contains("game_update_naturally_collected_chunk", pickupSource, StringComparison.Ordinal);
        Assert.Contains("target_chunk_absent_after_native_proximity_collection", pickupSource, StringComparison.Ordinal);
        Assert.Contains("inventory_item_count_increased_since_snapshot", pickupSource, StringComparison.Ordinal);
        Assert.Contains("ManhattanDistance(Game1.player.TilePoint, target) <= 3", pickupSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".collect(", pickupSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Chunks.Remove", pickupSource, StringComparison.Ordinal);
        Assert.DoesNotContain("debris.Remove", pickupSource, StringComparison.Ordinal);
    }

    [Fact]
    public void OpportunisticDebrisSkipsNonItemVisualsAndRuntimeRebindsStableIdentity()
    {
        var root = FindRepositoryRoot();
        var bridge = MiningReadAdapterSources.All;
        var planner = MiningFloorPlannerSources.All;
        var runtime = RuntimeHarnessSources.All;

        Assert.Contains("is_collectible_item_debris", bridge, StringComparison.Ordinal);
        Assert.Contains("non_item_visual_or_numeric_debris", bridge, StringComparison.Ordinal);
        Assert.Contains("string.IsNullOrWhiteSpace(qualifiedItemId)", planner, StringComparison.Ordinal);
        Assert.Contains("pickup_debris_item_identity_required", runtime, StringComparison.Ordinal);
        Assert.Contains("TryRebindPickupDebris(", runtime, StringComparison.Ordinal);
        Assert.Contains("maximumNaturalChunkDriftTiles = 3", runtime, StringComparison.Ordinal);
        Assert.Contains("candidates[0].Distance == candidates[1].Distance", runtime, StringComparison.Ordinal);
        Assert.Contains("Concat(indexes.Where(index => index != debrisIndex.Value))", runtime, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistrySeparatesMiningMechanicalPrimitivesFromSmallModelGoal()
    {
        var registry = new StardewAI.Core.OptionRegistry.OptionRegistry();

        foreach (var optionId in new[] { "executor.mine_stone", "executor.break_container", "executor.combat_monster", "executor.shoot_monster", "executor.place_bomb", "executor.place_staircase", "executor.consume_food", "executor.descend_ladder", "executor.descend_shaft", "executor.exit_mine" })
        {
            var option = registry.GetRequired(optionId);
            Assert.Equal(CompilerResponsibilities.FullActionExpansion, option.CompilerResponsibility);
            Assert.Equal(TrainingRoles.ExecutorCalibration, option.TrainingRole);
        }

        Assert.Equal(OptionBehaviorCategories.Mechanical, registry.GetRequired("executor.mine_stone").BehaviorCategory);
        Assert.Equal(OptionBehaviorCategories.Mechanical, registry.GetRequired("executor.combat_monster").BehaviorCategory);
        Assert.Equal(OptionBehaviorCategories.Mechanical, registry.GetRequired("executor.shoot_monster").BehaviorCategory);
        Assert.Equal(OptionBehaviorCategories.Mechanical, registry.GetRequired("executor.place_bomb").BehaviorCategory);
        Assert.Equal(OptionBehaviorCategories.Mechanical, registry.GetRequired("executor.place_staircase").BehaviorCategory);
        Assert.Equal(OptionBehaviorCategories.Mechanical, registry.GetRequired("executor.break_container").BehaviorCategory);
        Assert.Equal(OptionBehaviorCategories.Recovery, registry.GetRequired("executor.consume_food").BehaviorCategory);
        Assert.Equal(OptionBehaviorCategories.Mechanical, registry.GetRequired("executor.descend_ladder").BehaviorCategory);
        Assert.Equal(OptionBehaviorCategories.Mechanical, registry.GetRequired("executor.descend_shaft").BehaviorCategory);
        Assert.Equal(OptionBehaviorCategories.Recovery, registry.GetRequired("executor.exit_mine").BehaviorCategory);
    }

    [Fact]
    public void StaircaseClosureUsesExactBigCraftableAndNativeInputOnly()
    {
        var bridge = MiningReadAdapterSources.All;
        var runtime = RuntimeHarnessSources.All;
        var start = runtime.IndexOf(
            "private void StartPlaceStaircase",
            StringComparison.Ordinal);
        var end = runtime.IndexOf(
            "private static string StaircaseObservedEffect",
            start,
            StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var staircaseSource = runtime[start..end];

        Assert.Contains("CountItem(Game1.player, \"(BC)71\")", bridge, StringComparison.Ordinal);
        Assert.Contains("MineShaft.shouldCreateLadderOnThisLevel", bridge, StringComparison.Ordinal);
        Assert.Contains("exact_native_direct_tile_subset_no_recursive_relocation", bridge, StringComparison.Ordinal);
        Assert.DoesNotContain("CountItem(Game1.player, \"(O)71\")", bridge, StringComparison.Ordinal);

        Assert.Contains("TryApplySmapiRightButtonOverride", staircaseSource, StringComparison.Ordinal);
        Assert.Contains("PlacementCursorPatch.ScreenPixel", staircaseSource, StringComparison.Ordinal);
        Assert.Contains("active.MaxMovementTiles", staircaseSource, StringComparison.Ordinal);
        Assert.Contains("staircase_native_placement_not_observed", staircaseSource, StringComparison.Ordinal);
        Assert.DoesNotContain("createLadder", staircaseSource, StringComparison.Ordinal);
        Assert.DoesNotContain("recursiveTryToCreateLadderDown", staircaseSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.enterMine(", staircaseSource, StringComparison.Ordinal);

        var root = FindRepositoryRoot();
        var loop = File.ReadAllText(Path.Combine(
            root,
            "scripts",
            "Invoke-RuntimeMiningReachDepthLoop.ps1"));
        Assert.Contains(
            "[switch] $AllowStaircaseConsumption",
            loop,
            StringComparison.Ordinal);
        Assert.Contains(
            "resource_preservation_policy=",
            loop,
            StringComparison.Ordinal);
        Assert.Contains(
            "allow_staircase_consumption",
            loop,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeLadderUsesBfsAndNativeCheckActionWithoutDirectMineTransition()
    {
        var source = RuntimeHarnessSources.All;
        var start = source.IndexOf("private void StartDescendLadder", StringComparison.Ordinal);
        var end = source.IndexOf("private void StartConsumeFood", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var ladderSource = source[start..end];

        Assert.Contains("BuildCompilerAdjacentPath", ladderSource, StringComparison.Ordinal);
        Assert.Contains("MovePlayerForTick()", ladderSource, StringComparison.Ordinal);
        Assert.Contains("getTileIndexAt(active.Target.X, active.Target.Y, \"Buildings\", \"mine\") != 173", ladderSource, StringComparison.Ordinal);
        Assert.Contains("active.MineBefore.checkAction", ladderSource, StringComparison.Ordinal);
        Assert.Contains("afterMine.mineLevel == active.MineLevelBefore + 1", ladderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.enterMine(", ladderSource, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\.mineLevel\s*=(?!=)", ladderSource);
    }

    [Fact]
    public void RuntimeShaftUsesNativePromptAndVerifiesPreviewWithoutDirectTransition()
    {
        var source = RuntimeHarnessSources.All;
        var start = source.IndexOf("private void StartDescendShaft", StringComparison.Ordinal);
        var end = source.IndexOf("private void StartConsumeFood", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var shaftSource = source[start..end];

        Assert.Contains("BuildCompilerAdjacentPath", shaftSource, StringComparison.Ordinal);
        Assert.Contains("getTileIndexAt(target.X, target.Y, \"Buildings\", \"mine\") != 174", shaftSource, StringComparison.Ordinal);
        Assert.Contains("active.MineBefore.checkAction", shaftSource, StringComparison.Ordinal);
        Assert.Contains("answerDialogueAction(\"Shaft_Jump\"", shaftSource, StringComparison.Ordinal);
        Assert.Contains("afterMine.mineLevel == active.ExpectedMineLevelAfter", shaftSource, StringComparison.Ordinal);
        Assert.Contains("Game1.player.health != active.ExpectedHealthAfter", shaftSource, StringComparison.Ordinal);
        Assert.Contains("mine.getMineArea() != MineShaft.desertArea", shaftSource, StringComparison.Ordinal);
        Assert.Contains("mine.mineLevel <= MineShaft.bottomOfMineLevel", shaftSource, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiLeftButtonOverride(pressed: true", shaftSource, StringComparison.Ordinal);
        Assert.Contains("native_fall_dialogue_advanced_by_input", shaftSource, StringComparison.Ordinal);
        Assert.DoesNotContain("enterMineShaft(", shaftSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.enterMine(", shaftSource, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\.mineLevel\s*=(?!=)", shaftSource);
        Assert.DoesNotMatch(@"(?m)^\s*(?:Game1\.)?player\.health\s*=(?!=)", shaftSource);
    }

    [Fact]
    public void RuntimeMineExitUsesNativePromptWithoutDirectWarp()
    {
        var source = RuntimeHarnessSources.All;
        var start = source.IndexOf("private void StartExitMine", StringComparison.Ordinal);
        var end = source.IndexOf("private void StartConsumeFood", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var exitSource = source[start..end];

        Assert.Contains("getTileIndexAt(target.X, target.Y, \"Buildings\", \"mine\") != 115", exitSource, StringComparison.Ordinal);
        Assert.Contains("active.MineBefore.checkAction", exitSource, StringComparison.Ordinal);
        Assert.Contains("answerDialogueAction(\"ExitMine_Leave\"", exitSource, StringComparison.Ordinal);
        Assert.Contains("ExpectedMineExitDestination", exitSource, StringComparison.Ordinal);
        Assert.Contains("BuildCompilerMineExitPath", exitSource, StringComparison.Ordinal);
        Assert.Contains("is < 1 or > 2", exitSource, StringComparison.Ordinal);
        Assert.Contains("active.PreMoveSettleTicks++", exitSource, StringComparison.Ordinal);
        Assert.Contains("exit_mine_pre_move_animation_timeout", exitSource, StringComparison.Ordinal);
        Assert.DoesNotContain("exit_mine_tool_or_menu_conflict", exitSource, StringComparison.Ordinal);
        Assert.Contains("maxMovementTiles * 90 + 600", source, StringComparison.Ordinal);
        Assert.DoesNotContain("warpFarmer(", exitSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.enterMine(", exitSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeSkullKeyClaimUsesNativeTwoStageChestActionWithoutDirectProgressMutation()
    {
        var root = FindRepositoryRoot();
        var runtimeSource = RuntimeHarnessSources.All;
        var start = runtimeSource.IndexOf("private void StartSkullKeyChestInteraction", StringComparison.Ordinal);
        var end = runtimeSource.IndexOf("private TrainingExecutionResult ExecuteInteract", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var claimSource = runtimeSource[start..end];

        Assert.Contains("SkullKeyChestStage.OpenChest", claimSource, StringComparison.Ordinal);
        Assert.Contains("SkullKeyChestStage.ClaimItem", claimSource, StringComparison.Ordinal);
        Assert.True(claimSource.Split("mine.checkAction(", StringSplitOptions.None).Length >= 3);
        Assert.Contains("Game1.player.hasSkullKey", claimSource, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"Game1\.player\.hasSkullKey\s*=", claimSource);

        var adapterSource = MiningReadAdapterSources.All;
        Assert.Contains("mine.overlayObjects", adapterSource, StringComparison.Ordinal);
        Assert.Contains("item.which.Value == 4", adapterSource, StringComparison.Ordinal);

        var loopSource = File.ReadAllText(Path.Combine(root, "scripts", "Invoke-RuntimeMiningReachDepthLoop.ps1"));
        Assert.Contains("[switch] $AcquireSkullKey", loopSource, StringComparison.Ordinal);
        Assert.Contains("mining.obtain_skull_key", loopSource, StringComparison.Ordinal);
        Assert.Contains("$skullKeyTransitionObserved", loopSource, StringComparison.Ordinal);
        Assert.Contains("before player.has_skull_key became true", loopSource, StringComparison.Ordinal);
    }

    private static MiningFloorStepPlan Plan(
        string ladders = "[]",
        string shafts = "[]",
        string exits = "[]",
        string staircasePlacement = "{}",
        string goldenScytheAltars = "[]",
        string objects = "[]",
        string monsters = "[]",
        bool mustKillAll = false,
        bool goldenScytheApplicable = false,
        bool goldenScytheClaimed = false,
        string skullKeyRewardChests = "[]",
        string resourceClumps = "[]",
        bool skullKeyApplicable = false,
        bool hasSkullKey = false,
        int? mineLevel = null,
        string[]? rows = null,
        string[]? staticRows = null,
        string resources = "{\"health\":100,\"max_health\":100,\"energy\":220,\"current_time\":1200,\"selected_slot_index\":4,\"inventory_capacity\":{\"empty_slots\":12},\"food_slots\":[]}",
        string mineKind = "ordinary_mines",
        MiningFloorObjective? objective = null)
    {
        rows ??= new[] { "111111", "100001", "100001", "100001", "111111" };
        staticRows ??= rows;
        var rowsJson = JsonSerializer.Serialize(rows);
        var staticRowsJson = JsonSerializer.Serialize(staticRows);
        var width = rows[0].Length;
        var height = rows.Length;
        var json = """
        {
          "player": {
            "has_skull_key": {"status":"available","value":HAS_SKULL_KEY}
          },
          "mining": {
            "current_mine": {"status":"available","value":{"mine_level":MINE_LEVEL,"mine_kind":"MINE_KIND"}},
            "tiles": {"status":"available","value":{"player_tile":{"tile_x":1,"tile_y":2},"ladders":LADDERS,"shafts":SHAFTS,"exits":EXITS,"staircase_placement":STAIRCASE_PLACEMENT,"golden_scythe_altars":GOLDEN_SCYTHE_ALTARS,"collision_context":{"status":"available","encoding":"row_major_strings_1_blocked_0_passable","width":WIDTH,"height":HEIGHT,"static_blocked_rows":STATICROWS,"blocked_rows":ROWS}}},
            "objects": {"status":"available","value":OBJECTS},
            "resource_clumps": {"status":"available","value":RESOURCE_CLUMPS},
            "monsters": {"status":"available","value":MONSTERS},
            "floor_objectives": {"status":"available","value":{"must_kill_all_monsters_to_advance":MUST_KILL_ALL,"golden_scythe_applicable":GOLDEN_SCYTHE_APPLICABLE,"golden_scythe_claimed":GOLDEN_SCYTHE_CLAIMED,"skull_key_applicable":SKULL_KEY_APPLICABLE,"skull_key_acquired":HAS_SKULL_KEY,"skull_key_reward_chests":SKULL_KEY_REWARD_CHESTS}},
            "reward_chests": {"status":"available","value":[]},
            "player_resources": {"status":"available","value":RESOURCES}
          }
        }
        """
            .Replace("LADDERS", ladders, StringComparison.Ordinal)
            .Replace("SHAFTS", shafts, StringComparison.Ordinal)
            .Replace("EXITS", exits, StringComparison.Ordinal)
            .Replace("STAIRCASE_PLACEMENT", staircasePlacement, StringComparison.Ordinal)
            .Replace("GOLDEN_SCYTHE_ALTARS", goldenScytheAltars, StringComparison.Ordinal)
            .Replace("SKULL_KEY_REWARD_CHESTS", skullKeyRewardChests, StringComparison.Ordinal)
            .Replace("WIDTH", width.ToString(), StringComparison.Ordinal)
            .Replace("HEIGHT", height.ToString(), StringComparison.Ordinal)
            .Replace("STATICROWS", staticRowsJson, StringComparison.Ordinal)
            .Replace("ROWS", rowsJson, StringComparison.Ordinal)
            .Replace("OBJECTS", objects, StringComparison.Ordinal)
            .Replace("RESOURCE_CLUMPS", resourceClumps, StringComparison.Ordinal)
            .Replace("MONSTERS", monsters, StringComparison.Ordinal)
            .Replace("RESOURCES", resources, StringComparison.Ordinal)
            .Replace("MINE_LEVEL", (mineLevel ?? (mineKind == "skull_cavern" ? 121 : mineKind == "quarry_mine" ? 77377 : 40)).ToString(), StringComparison.Ordinal)
            .Replace("MINE_KIND", mineKind, StringComparison.Ordinal)
            .Replace("MUST_KILL_ALL", mustKillAll.ToString().ToLowerInvariant(), StringComparison.Ordinal)
            .Replace("GOLDEN_SCYTHE_APPLICABLE", goldenScytheApplicable.ToString().ToLowerInvariant(), StringComparison.Ordinal)
            .Replace("GOLDEN_SCYTHE_CLAIMED", goldenScytheClaimed.ToString().ToLowerInvariant(), StringComparison.Ordinal)
            .Replace("SKULL_KEY_APPLICABLE", skullKeyApplicable.ToString().ToLowerInvariant(), StringComparison.Ordinal)
            .Replace("HAS_SKULL_KEY", hasSkullKey.ToString().ToLowerInvariant(), StringComparison.Ordinal);
        return new MiningFloorStepPlanner().Plan(Snapshot(json), objective ?? new MiningFloorObjective());
    }

    private static MiningFloorStepPlan ObjectivePlan(
        MiningFloorObjective objective,
        string objects = "[]",
        string monsters = "[]",
        string debris = "[]",
        string resources = "{\"health\":100,\"max_health\":100,\"selected_slot_index\":4,\"food_slots\":[]}",
        string dropCatalogs = "[]",
        string playerInventory = "[]")
    {
        var json = """
        {
          "player": {
            "inventory": {"status":"available","value":PLAYER_INVENTORY}
          },
          "mining": {
            "tiles": {"status":"available","value":{"player_tile":{"tile_x":1,"tile_y":2},"ladders":[],"collision_context":{"status":"available","encoding":"row_major_strings_1_blocked_0_passable","width":8,"height":5,"blocked_rows":["11111111","10000001","10000001","10000001","11111111"]}}},
            "objects": {"status":"available","value":OBJECTS},
            "resource_clumps": {"status":"available","value":[]},
            "monsters": {"status":"available","value":MONSTERS},
            "monster_drop_catalogs": {"status":"available","value":DROP_CATALOGS},
            "debris": {"status":"available","value":DEBRIS},
            "floor_objectives": {"status":"available","value":{"must_kill_all_monsters_to_advance":false}},
            "reward_chests": {"status":"available","value":[]},
            "player_resources": {"status":"available","value":RESOURCES}
          }
        }
        """
            .Replace("OBJECTS", objects, StringComparison.Ordinal)
            .Replace("MONSTERS", monsters, StringComparison.Ordinal)
            .Replace("DROP_CATALOGS", dropCatalogs, StringComparison.Ordinal)
            .Replace("DEBRIS", debris, StringComparison.Ordinal)
            .Replace("PLAYER_INVENTORY", playerInventory, StringComparison.Ordinal)
            .Replace("RESOURCES", resources, StringComparison.Ordinal);
        return new MiningFloorStepPlanner().Plan(Snapshot(json), objective);
    }

    private static SnapshotEnvelope Snapshot(string stateJson)
    {
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = "test",
            GameTick = 1,
            State = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stateJson, JsonOptions)!
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }}

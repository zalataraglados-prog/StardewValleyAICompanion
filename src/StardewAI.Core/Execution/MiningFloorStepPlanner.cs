using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Execution
{
    public sealed partial class MiningFloorStepPlanner
    {
        private static readonly (int X, int Y)[] Directions =
        {
            (0, -1),
            (-1, 0),
            (1, 0),
            (0, 1)
        };

        public MiningFloorStepPlan Plan(SnapshotEnvelope snapshot)
        {
            return Plan(snapshot, new MiningFloorObjective());
        }

        public MiningFloorStepPlan Plan(SnapshotEnvelope snapshot, MiningFloorObjective objective)
        {
            if (!snapshot.State.TryGetValue("mining", out var mining) || mining.ValueKind != JsonValueKind.Object)
            {
                return Blocked("mining_state_unavailable");
            }

            if (!TryFieldValue(mining, "tiles", out var tiles) ||
                !TryFieldValue(mining, "objects", out var objects) ||
                !TryFieldValue(mining, "resource_clumps", out var resourceClumps) ||
                !TryFieldValue(mining, "monsters", out var monsters) ||
                !TryFieldValue(mining, "floor_objectives", out var objectives) ||
                !TryFieldValue(mining, "reward_chests", out var rewardChests))
            {
                return Blocked("mining_required_group_unavailable");
            }
            if (rewardChests.ValueKind != JsonValueKind.Array)
            {
                return Blocked("mining_reward_chests_invalid");
            }

            if (!TryCollision(tiles, out var grid, out var start))
            {
                return Blocked("mining_collision_context_unavailable");
            }

            var search = Search(grid, start);
            var hasResources = TryFieldValue(mining, "player_resources", out var resources);
            var restoreSlot = hasResources ? ReadInt(resources, "selected_slot_index") : null;
            JsonElement? playerInventory = snapshot.State.TryGetValue("player", out var player) &&
                                           TryFieldValue(player, "inventory", out var inventory)
                ? inventory
                : null;
            var movementTileDurationMs = hasResources ? ReadMovementTileDuration(resources) : null;
            var bombFinisherAvailable = hasResources && HasAvailableBomb(resources);
            var mustKillAll = ReadBool(objectives, "must_kill_all_monsters_to_advance");
            var monsterDropCatalogs = objective.Kind == MiningObjectiveKinds.CollectMonsterDrop
                ? ReadMonsterDropCatalogs(mining)
                : null;

            var currentDepth = TryFieldValue(mining, "current_mine", out var currentMine)
                ? ReadInt(currentMine, "mine_level")
                : null;
            var currentMineKind = currentMine.ValueKind == JsonValueKind.Object
                ? ReadString(currentMine, "mine_kind")
                : string.Empty;

            if (objective.Kind == MiningObjectiveKinds.AcquireSkullKey)
            {
                if (!string.Equals(currentMineKind, "ordinary_mines", StringComparison.Ordinal) ||
                    !currentDepth.HasValue || currentDepth.Value < 1 || currentDepth.Value > 120)
                {
                    return Blocked("skull_key_requires_ordinary_mines_1_120");
                }

                if (SnapshotBool(snapshot, "player", "has_skull_key") == true)
                {
                    return SelectMineExit(tiles, search, grid, "skull_key_acquired_exit_ordinary_mines") ??
                        Blocked("skull_key_acquired_but_native_exit_unreachable");
                }

                if (currentDepth.Value == 120)
                {
                    if (!ReadBool(objectives, "skull_key_applicable"))
                    {
                        return Blocked("skull_key_floor_120_reward_not_applicable");
                    }
                    if (!objectives.TryGetProperty("skull_key_reward_chests", out var skullKeyRewardChests) ||
                        skullKeyRewardChests.ValueKind != JsonValueKind.Array ||
                        skullKeyRewardChests.GetArrayLength() == 0)
                    {
                        return Blocked("skull_key_reward_chest_unavailable");
                    }

                    var rewardStep = SelectSkullKeyChestStep(skullKeyRewardChests, search, grid);
                    return rewardStep ?? Blocked("skull_key_reward_chest_unreachable");
                }
            }

            var mandatoryRetreatReason = hasResources ? MandatoryRetreatReason(resources, objective, currentDepth) : string.Empty;
            var targetDepthOnlyRetreat = string.Equals(mandatoryRetreatReason, "retreat_required:target_depth_reached", StringComparison.Ordinal);
            if (!string.IsNullOrEmpty(mandatoryRetreatReason) && !targetDepthOnlyRetreat)
            {
                var retreat = SelectMineExit(tiles, search, grid, mandatoryRetreatReason);
                return retreat ?? Blocked("retreat_required_but_exit_unreachable:" + mandatoryRetreatReason);
            }

            if (hasResources && NeedsHealing(
                    resources,
                    monsters,
                    objective,
                    out var healthReason,
                    out var requiredHealth,
                    out var maximumMonsterDamage))
            {
                var immediateThreat = SelectImmediateThreat(
                    monsters,
                    search,
                    grid,
                    start,
                    objective.ThreatRadiusTiles,
                    bombFinisherAvailable,
                    movementTileDurationMs);
                var currentHealth = ReadInt(resources, "health") ?? 0;
                if (immediateThreat is not null &&
                    currentHealth > maximumMonsterDamage)
                {
                    immediateThreat.Reason =
                        "immediate_monster_threat_preempts_recovery";
                    immediateThreat.SafetyWindowStatus =
                        "blocked_by_immediate_monster_threat";
                    immediateThreat.RestoreSlotIndex = restoreSlot;
                    return immediateThreat;
                }

                var healing = SelectFood(
                    resources,
                    requiredHealth,
                    restoreSlot);
                if (healing is not null)
                {
                    healing.Reason = healthReason;
                    if (immediateThreat is not null)
                    {
                        healing.SafetyWindowStatus =
                            "critical_one_hit_recovery_preempts_immediate_threat";
                    }
                    return healing;
                }
                var retreat = SelectMineExit(tiles, search, grid, "retreat_unsafe_health_without_recovery_food");
                return retreat ?? Blocked("unsafe_health_without_recovery_food_and_exit_unreachable");
            }

            if (objective.Kind == MiningObjectiveKinds.AcquireGoldenScythe)
            {
                if (!string.Equals(currentMineKind, "quarry_mine", StringComparison.Ordinal) || currentDepth != 77377)
                {
                    return Blocked("golden_scythe_requires_quarry_mine_77377");
                }
                if (!hasResources)
                {
                    return Blocked("golden_scythe_player_resources_unavailable");
                }
                if (!ReadBool(objectives, "golden_scythe_applicable"))
                {
                    return Blocked("golden_scythe_not_applicable_to_loaded_floor");
                }

                var claimed = ReadBool(objectives, "golden_scythe_claimed");
                if (claimed)
                {
                    var exit = SelectMineExit(
                        tiles,
                        search,
                        grid,
                        "golden_scythe_acquired_exit_quarry_mine");
                    if (exit is not null)
                    {
                        return exit;
                    }
                }
                else if (ReadInventoryEmptySlots(resources) <= 0)
                {
                    return Blocked("golden_scythe_inventory_full");
                }
                var altars = default(JsonElement);
                if (!claimed &&
                    (!tiles.TryGetProperty("golden_scythe_altars", out altars) ||
                     altars.ValueKind != JsonValueKind.Array ||
                     altars.GetArrayLength() == 0))
                {
                    return Blocked("golden_scythe_altar_action_unavailable");
                }

                var threat = SelectImmediateThreat(
                    monsters,
                    search,
                    grid,
                    start,
                    objective.ThreatRadiusTiles,
                    bombFinisherAvailable,
                    movementTileDurationMs);
                if (threat is not null)
                {
                    threat.Reason =
                        "golden_scythe_route_interrupted_by_immediate_monster_threat";
                    threat.SafetyWindowStatus =
                        "blocked_by_immediate_monster_threat";
                    threat.RestoreSlotIndex = restoreSlot;
                    return threat;
                }

                if (claimed)
                {
                    var exitBlocker = SelectMonster(
                        monsters,
                        search,
                        grid,
                        "golden_scythe_exit_route_blocked_by_dynamic_monster",
                        movementTileDurationMs: movementTileDurationMs,
                        bombFinisherAvailable: bombFinisherAvailable,
                        combatIntent:
                            TrainingCombatIntents.TransitRouteClearance);
                    if (exitBlocker is not null)
                    {
                        exitBlocker.SafetyWindowStatus =
                            "mandatory_exit_route_dynamic_blocker";
                        exitBlocker.RestoreSlotIndex = restoreSlot;
                        return exitBlocker;
                    }

                    var exitRouteStone = SelectStone(
                        objects,
                        search,
                        grid);
                    if (exitRouteStone is not null)
                    {
                        if (exitRouteStone.EstimatedMovementTiles >
                            ObjectiveApproachHorizonTiles)
                        {
                            return BuildObjectiveApproachStep(
                                exitRouteStone,
                                MiningFloorStepKinds.MoveToMineExitRoute,
                                "approach_golden_scythe_exit_route_clearance",
                                "golden_scythe_exit_route_clearance_replan_required");
                        }

                        exitRouteStone.Reason =
                            "golden_scythe_exit_route_blocked_by_removable_stone";
                        return exitRouteStone;
                    }
                }

                if (!claimed)
                {
                    var altarStep = SelectGoldenScytheAltarStep(
                        altars,
                        search,
                        grid);
                    if (altarStep is not null)
                    {
                        return altarStep;
                    }
                }

                var routeClump = SelectResourceClump(
                    resourceClumps,
                    search,
                    grid);
                if (routeClump is not null)
                {
                    if (routeClump.EstimatedMovementTiles >
                        ObjectiveApproachHorizonTiles)
                    {
                        var approach = BuildObjectiveApproachStep(
                            routeClump,
                            claimed
                                ? MiningFloorStepKinds.MoveToMineExitRoute
                                : MiningFloorStepKinds.MoveToGoldenScytheAltar,
                            claimed
                                ? "approach_golden_scythe_exit_route_clearance"
                                : "approach_golden_scythe_route_clearance",
                            claimed
                                ? "golden_scythe_exit_route_clearance_replan_required"
                                : "golden_scythe_route_clearance_replan_required");
                        approach.TargetQualifiedItemId = "(W)53";
                        return approach;
                    }

                    routeClump.Reason =
                        claimed
                            ? "golden_scythe_exit_route_blocked_by_removable_resource_clump"
                            : "golden_scythe_route_blocked_by_removable_resource_clump";
                    return routeClump;
                }
            }

            if (bombFinisherAvailable)
            {
                var mummyFinisher = SelectMummyBombFinisher(
                    objects,
                    monsters,
                    resources,
                    search,
                    grid,
                    movementTileDurationMs,
                    objective,
                    mustKillAll);
                if (mummyFinisher is not null)
                {
                    return mummyFinisher;
                }
            }

            if (objective.Kind == MiningObjectiveKinds.CollectMonsterDrop)
            {
                if (TryFieldValue(mining, "debris", out var monsterDebris))
                {
                    var existingDrop = SelectDebris(monsterDebris, search, grid, objective.TargetQualifiedItemIds, restoreSlot, playerInventory);
                    if (existingDrop is not null)
                    {
                        existingDrop.Reason = "target_monster_drop_already_on_floor";
                        return existingDrop;
                    }
                }

                return SelectMonster(monsters, search, grid, "target_drop_monster_reachable", objective.TargetQualifiedItemIds, monsterDropCatalogs, movementTileDurationMs, bombFinisherAvailable) ??
                    Blocked("no_reachable_monster_with_possible_target_drop");
            }

            if (objective.Kind == MiningObjectiveKinds.SlayNamedMonster)
            {
                var questTarget = SelectMonster(
                    monsters,
                    search,
                    grid,
                    "quest_target_monster_reachable",
                    movementTileDurationMs: movementTileDurationMs,
                    bombFinisherAvailable: bombFinisherAvailable,
                    targetMonsterNameFragments: objective.TargetMonsterNameFragments,
                    matchAnySlimeName: objective.MatchAnySlimeName);
                if (questTarget is not null)
                {
                    questTarget.SourceMatchStatus = objective.MatchAnySlimeName
                        ? "native_quest15_slime_name_match"
                        : "native_monster_name_contains";
                    return questTarget;
                }
            }

            if (objective.Kind == MiningObjectiveKinds.CollectResourceOrArtifact)
            {
                if (TryFieldValue(mining, "debris", out var debris))
                {
                    var pickup = SelectDebris(debris, search, grid, objective.TargetQualifiedItemIds, restoreSlot, playerInventory);
                    if (pickup is not null)
                    {
                        return pickup;
                    }
                }

                var threat = SelectImmediateThreat(
                    monsters,
                    search,
                    grid,
                    start,
                    objective.ThreatRadiusTiles,
                    bombFinisherAvailable,
                    movementTileDurationMs);
                if (threat is not null)
                {
                    threat.Reason = "unsafe_tool_window_combat_interrupt";
                    threat.SafetyWindowStatus =
                        "blocked_by_immediate_monster_threat";
                    threat.RestoreSlotIndex = restoreSlot;
                    return threat;
                }

                return SelectTargetObject(objects, search, grid, objective.TargetQualifiedItemIds, objective.TargetSourceQualifiedItemIds, restoreSlot) ??
                    Blocked("no_reachable_target_resource_or_artifact_source");
            }

            if (objective.Kind is MiningObjectiveKinds.ReachDepth or MiningObjectiveKinds.AcquireSkullKey)
            {
                var reward = SelectMineRewardChest(rewardChests, search, grid);
                if (reward is not null)
                {
                    var rewardThreat = SelectImmediateThreat(
                        monsters,
                        search,
                        grid,
                        start,
                        objective.ThreatRadiusTiles,
                        bombFinisherAvailable,
                        movementTileDurationMs);
                    if (rewardThreat is not null)
                    {
                        rewardThreat.Reason =
                            "mandatory_reward_chest_interrupted_by_immediate_monster_threat";
                        rewardThreat.SafetyWindowStatus =
                            "blocked_by_immediate_monster_threat";
                        rewardThreat.RestoreSlotIndex = restoreSlot;
                        return rewardThreat;
                    }
                    return reward;
                }
            }

            if (targetDepthOnlyRetreat)
            {
                return SelectMineExit(tiles, search, grid, mandatoryRetreatReason) ??
                    Blocked("target_depth_reached_but_native_exit_unreachable");
            }

            if (TryFieldValue(mining, "debris", out var opportunisticDebris) &&
                SelectImmediateThreat(monsters, search, grid, start, objective.ThreatRadiusTiles, bombFinisherAvailable, movementTileDurationMs) is null)
            {
                var pickup = SelectDebris(opportunisticDebris, search, grid, Array.Empty<string>(), restoreSlot, playerInventory, maximumDistance: 3);
                if (pickup is not null)
                {
                    pickup.Reason = "opportunistic_debris_within_three_tiles";
                    return pickup;
                }
            }

            var shaftPlan = SelectShaft(tiles, search, grid, resources, hasResources, objective.MinimumReserveHealth, currentMineKind);
            if (shaftPlan is not null)
            {
                return shaftPlan;
            }

            var ladderPlan = SelectActionTile(tiles, "ladders", MiningFloorStepKinds.DescendLadder, "reachable_ladder_available", search, grid);
            if (ladderPlan is not null)
            {
                return ladderPlan;
            }

            if (hasResources)
            {
                var bombPlan = SelectBombCluster(objects, monsters, resources, search, grid, start, movementTileDurationMs);
                if (bombPlan is not null)
                {
                    return bombPlan;
                }
            }

            var containerPlan = SelectContainer(objects, search, grid, maximumDistance: 4);
            if (containerPlan is not null)
            {
                return containerPlan;
            }

            var resourceClumpPlan = SelectResourceClump(resourceClumps, search, grid);

            if (mustKillAll)
            {
                return SelectMonster(monsters, search, grid, "kill_all_floor_requires_combat", movementTileDurationMs: movementTileDurationMs, bombFinisherAvailable: bombFinisherAvailable) ??
                    Blocked("kill_all_floor_has_no_reachable_monster");
            }

            var stonePlan = SelectStone(objects, search, grid);
            if (stonePlan is not null)
            {
                return stonePlan;
            }

            if (resourceClumpPlan is not null)
            {
                return resourceClumpPlan;
            }

            var combatPlan = SelectMonster(monsters, search, grid, "no_reachable_stone_clear_dynamic_monster", movementTileDurationMs: movementTileDurationMs, bombFinisherAvailable: bombFinisherAvailable);
            if (combatPlan is not null)
            {
                return combatPlan;
            }

            var unsafeShaft = SelectShaft(
                tiles,
                search,
                grid,
                resources,
                hasResources,
                minimumReserveHealth: 0,
                currentMineKind: currentMineKind,
                requireReserve: false);
            if (unsafeShaft is not null && hasResources)
            {
                var shaftRequiredHealth = (unsafeShaft.ExpectedHealthCost ?? 0) + Math.Max(1, objective.MinimumReserveHealth);
                var maxHealth = ReadInt(resources, "max_health") ?? 0;
                if (shaftRequiredHealth > maxHealth)
                {
                    return Blocked("shaft_health_reserve_unreachable_at_max_health");
                }
                var healing = SelectFood(
                    resources,
                    shaftRequiredHealth,
                    restoreSlot);
                if (healing is not null)
                {
                    healing.Reason = "shaft_health_reserve_requires_recovery";
                    return healing;
                }
                return Blocked("shaft_health_reserve_not_met");
            }

            return Blocked("no_reachable_ladder_shaft_stone_or_monster");
        }

    }
}

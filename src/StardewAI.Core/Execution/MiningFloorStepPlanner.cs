using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

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
                !TryFieldValue(mining, "floor_objectives", out var objectives))
            {
                return Blocked("mining_required_group_unavailable");
            }

            if (!TryCollision(tiles, out var grid, out var start))
            {
                return Blocked("mining_collision_context_unavailable");
            }

            var search = Search(grid, start);
            var hasResources = TryFieldValue(mining, "player_resources", out var resources);
            var restoreSlot = hasResources ? ReadInt(resources, "selected_slot_index") : null;
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
                    if (!objectives.TryGetProperty("skull_key_reward_chests", out var rewardChests) ||
                        rewardChests.ValueKind != JsonValueKind.Array ||
                        rewardChests.GetArrayLength() == 0)
                    {
                        return Blocked("skull_key_reward_chest_unavailable");
                    }

                    var rewardStep = SelectSkullKeyChestStep(rewardChests, search, grid);
                    return rewardStep ?? Blocked("skull_key_reward_chest_unreachable");
                }
            }

            var mandatoryRetreatReason = hasResources ? MandatoryRetreatReason(resources, objective, currentDepth) : string.Empty;
            if (!string.IsNullOrEmpty(mandatoryRetreatReason))
            {
                var retreat = SelectMineExit(tiles, search, grid, mandatoryRetreatReason);
                return retreat ?? Blocked("retreat_required_but_exit_unreachable:" + mandatoryRetreatReason);
            }

            if (hasResources && NeedsHealing(resources, monsters, objective, out var healthReason))
            {
                var healing = SelectFood(resources, objective.MinimumReserveHealth, restoreSlot);
                if (healing is not null)
                {
                    healing.Reason = healthReason;
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
                    return SelectMineExit(tiles, search, grid, "golden_scythe_acquired_exit_quarry_mine") ??
                        Blocked("golden_scythe_acquired_but_native_exit_unreachable");
                }
                if (!claimed && ReadInventoryEmptySlots(resources) <= 0)
                {
                    return Blocked("golden_scythe_inventory_full");
                }
                if (!tiles.TryGetProperty("golden_scythe_altars", out var altars) ||
                    altars.ValueKind != JsonValueKind.Array ||
                    altars.GetArrayLength() == 0)
                {
                    return Blocked("golden_scythe_altar_action_unavailable");
                }

                var threat = SelectImmediateThreat(monsters, search, grid, start, objective.ThreatRadiusTiles, bombFinisherAvailable, movementTileDurationMs);
                if (threat is not null)
                {
                    threat.Reason = "golden_scythe_route_interrupted_by_immediate_monster_threat";
                    threat.SafetyWindowStatus = "blocked_by_immediate_monster_threat";
                    threat.RestoreSlotIndex = restoreSlot;
                    return threat;
                }

                var altarStep = SelectGoldenScytheAltarStep(altars, search, grid);
                if (altarStep is not null)
                {
                    return altarStep;
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
                    var existingDrop = SelectDebris(monsterDebris, search, grid, objective.TargetQualifiedItemIds, restoreSlot);
                    if (existingDrop is not null)
                    {
                        existingDrop.Reason = "target_monster_drop_already_on_floor";
                        return existingDrop;
                    }
                }

                return SelectMonster(monsters, search, grid, "target_drop_monster_reachable", objective.TargetQualifiedItemIds, monsterDropCatalogs, movementTileDurationMs, bombFinisherAvailable) ??
                    Blocked("no_reachable_monster_with_possible_target_drop");
            }

            if (objective.Kind == MiningObjectiveKinds.CollectResourceOrArtifact)
            {
                if (TryFieldValue(mining, "debris", out var debris))
                {
                    var pickup = SelectDebris(debris, search, grid, objective.TargetQualifiedItemIds, restoreSlot);
                    if (pickup is not null)
                    {
                        return pickup;
                    }
                }

                var threat = SelectImmediateThreat(monsters, search, grid, start, objective.ThreatRadiusTiles, bombFinisherAvailable, movementTileDurationMs);
                if (threat is not null)
                {
                    threat.Reason = "unsafe_tool_window_combat_interrupt";
                    threat.SafetyWindowStatus = "blocked_by_immediate_monster_threat";
                    threat.RestoreSlotIndex = restoreSlot;
                    return threat;
                }

                return SelectTargetObject(objects, search, grid, objective.TargetQualifiedItemIds, objective.TargetSourceQualifiedItemIds, restoreSlot) ??
                    Blocked("no_reachable_target_resource_or_artifact_source");
            }

            if (TryFieldValue(mining, "debris", out var opportunisticDebris) &&
                SelectImmediateThreat(monsters, search, grid, start, objective.ThreatRadiusTiles, bombFinisherAvailable, movementTileDurationMs) is null)
            {
                var pickup = SelectDebris(opportunisticDebris, search, grid, Array.Empty<string>(), restoreSlot, maximumDistance: 3);
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
            if (objective.Kind == MiningObjectiveKinds.AcquireGoldenScythe && resourceClumpPlan is not null)
            {
                resourceClumpPlan.Reason = "golden_scythe_route_blocked_by_removable_resource_clump";
                return resourceClumpPlan;
            }

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
                var requiredHealth = (unsafeShaft.ExpectedHealthCost ?? 0) + Math.Max(1, objective.MinimumReserveHealth);
                var maxHealth = ReadInt(resources, "max_health") ?? 0;
                if (requiredHealth > maxHealth)
                {
                    return Blocked("shaft_health_reserve_unreachable_at_max_health");
                }
                var healing = SelectFood(resources, requiredHealth, restoreSlot);
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

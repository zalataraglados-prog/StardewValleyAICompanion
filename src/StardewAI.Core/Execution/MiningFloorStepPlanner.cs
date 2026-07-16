using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Execution
{
    public static class MiningFloorStepKinds
    {
        public const string DescendLadder = "descend_ladder";
        public const string DescendShaft = "descend_shaft";
        public const string ExitMine = "exit_mine";
        public const string MineStone = "mine_stone";
        public const string BreakContainer = "break_container";
        public const string CombatMonster = "combat_monster";
        public const string ShootMonster = "shoot_monster";
        public const string PlaceBomb = "place_bomb";
        public const string PickupDebris = "pickup_debris";
        public const string ConsumeFood = "consume_food";
        public const string Blocked = "blocked";
    }

    public static class MiningObjectiveKinds
    {
        public const string ReachDepth = "reach_depth";
        public const string CollectResourceOrArtifact = "collect_resource_or_artifact";
        public const string CollectMonsterDrop = "collect_monster_drop";
    }

    public sealed class MiningFloorObjective
    {
        public string Kind { get; set; } = MiningObjectiveKinds.ReachDepth;

        public string[] TargetQualifiedItemIds { get; set; } = Array.Empty<string>();

        public string[] TargetSourceQualifiedItemIds { get; set; } = Array.Empty<string>();

        public int MinimumReserveHealth { get; set; }

        public int ThreatRadiusTiles { get; set; } = 3;

        public int? LatestExitTime { get; set; }

        public int? MinimumReserveEnergy { get; set; }

        public int? TargetDepth { get; set; }
    }

    public sealed class MiningPathTile
    {
        public int X { get; set; }

        public int Y { get; set; }
    }

    public sealed class MiningFloorStepPlan
    {
        public string Status { get; set; } = "blocked";

        public string StepKind { get; set; } = MiningFloorStepKinds.Blocked;

        public string Reason { get; set; } = string.Empty;

        public int? TargetTileX { get; set; }

        public int? TargetTileY { get; set; }

        public int? StandTileX { get; set; }

        public int? StandTileY { get; set; }

        public int EstimatedMovementTiles { get; set; }

        public int EstimatedToolSwings { get; set; }

        public bool DeterministicLadderAfterBreak { get; set; }

        public string TargetRuntimeIdentity { get; set; } = string.Empty;

        public string TargetRuntimeType { get; set; } = string.Empty;

        public string TargetName { get; set; } = string.Empty;

        public string RequiredWeaponEnchantmentRuntimeType { get; set; } = string.Empty;

        public int? CombatWeaponSlotIndex { get; set; }

        public string CombatMethod { get; set; } = string.Empty;

        public string CombatTerminalState { get; set; } = string.Empty;

        public int? SlingshotSlotIndex { get; set; }

        public string SlingshotAmmoQualifiedItemId { get; set; } = string.Empty;

        public int? BombSlotIndex { get; set; }

        public string BombQualifiedItemId { get; set; } = string.Empty;

        public int? BombRadiusTiles { get; set; }

        public int? EscapeTileX { get; set; }

        public int? EscapeTileY { get; set; }

        public int? ExpectedBombObjectHits { get; set; }

        public int? ExpectedBombMonsterHits { get; set; }

        public double? ExpectedCombatAttacks { get; set; }

        public double? ExpectedCombatDurationMs { get; set; }

        public double? EstimatedTargetCostMs { get; set; }

        public string CombatDurationStatus { get; set; } = string.Empty;

        public string TargetQualifiedItemId { get; set; } = string.Empty;

        public string[] ExpectedDropQualifiedItemIds { get; set; } = Array.Empty<string>();

        public string SourceMatchStatus { get; set; } = string.Empty;

        public double? TargetDropChancePreview { get; set; }

        public string TargetDropProbabilityStatus { get; set; } = string.Empty;

        public double? TargetExpectedQuantityPerKill { get; set; }

        public double? TargetDropEfficiencyScore { get; set; }

        public int? FoodSlotIndex { get; set; }

        public int? DebrisIndex { get; set; }

        public int? RestoreSlotIndex { get; set; }

        public int? ExpectedMineLevelDelta { get; set; }

        public int? ExpectedMineLevelAfter { get; set; }

        public int? ExpectedHealthCost { get; set; }

        public int? ExpectedHealthAfter { get; set; }

        public string ExpectedTargetLocation { get; set; } = string.Empty;

        public int? ExpectedArrivalTileX { get; set; }

        public int? ExpectedArrivalTileY { get; set; }

        public string SafetyWindowStatus { get; set; } = "not_required";

        public MiningPathTile[] Path { get; set; } = Array.Empty<MiningPathTile>();
    }

    public sealed class MiningFloorStepPlanner
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

            var shaftPlan = SelectShaft(tiles, search, grid, resources, hasResources, objective.MinimumReserveHealth);
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

            var combatPlan = SelectMonster(monsters, search, grid, "no_reachable_stone_clear_dynamic_monster", movementTileDurationMs: movementTileDurationMs, bombFinisherAvailable: bombFinisherAvailable);
            if (combatPlan is not null)
            {
                return combatPlan;
            }

            var unsafeShaft = SelectShaft(tiles, search, grid, resources, hasResources, minimumReserveHealth: 0, requireReserve: false);
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

        private static MiningFloorStepPlan? SelectShaft(
            JsonElement tiles,
            SearchResult search,
            bool[,] grid,
            JsonElement resources,
            bool hasResources,
            int minimumReserveHealth,
            bool requireReserve = true)
        {
            if (!tiles.TryGetProperty("shafts", out var shafts) || shafts.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return shafts.EnumerateArray()
                .Select(shaft => new { Shaft = shaft, Candidate = TargetCandidate(shaft, search, grid, estimatedSwings: 0, deterministicLadder: false) })
                .Where(row => row.Candidate is not null)
                .Where(row => !requireReserve || (hasResources && (ReadInt(row.Shaft, "expected_health_after") ?? 0) >= Math.Max(1, minimumReserveHealth)))
                .OrderBy(row => row.Candidate!.Distance)
                .ThenBy(row => row.Candidate!.TargetY)
                .ThenBy(row => row.Candidate!.TargetX)
                .Select(row =>
                {
                    var plan = Build(MiningFloorStepKinds.DescendShaft, "reachable_safe_shaft_available", row.Candidate!);
                    plan.ExpectedMineLevelDelta = ReadInt(row.Shaft, "expected_level_delta");
                    plan.ExpectedMineLevelAfter = ReadInt(row.Shaft, "expected_mine_level_after");
                    plan.ExpectedHealthCost = ReadInt(row.Shaft, "expected_health_cost");
                    plan.ExpectedHealthAfter = ReadInt(row.Shaft, "expected_health_after");
                    plan.SafetyWindowStatus = requireReserve ? "shaft_health_reserve_verified" : "shaft_requires_recovery";
                    return plan;
                })
                .FirstOrDefault();
        }

        private static MiningFloorStepPlan? SelectMineExit(JsonElement tiles, SearchResult search, bool[,] grid, string reason)
        {
            if (!tiles.TryGetProperty("exits", out var exits) || exits.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return exits.EnumerateArray()
                .Select(exit => new { Exit = exit, Candidate = TargetCandidate(exit, search, grid, estimatedSwings: 0, deterministicLadder: false) })
                .Where(row => row.Candidate is not null)
                .OrderBy(row => row.Candidate!.Distance)
                .ThenBy(row => row.Candidate!.TargetY)
                .ThenBy(row => row.Candidate!.TargetX)
                .Select(row =>
                {
                    var plan = Build(MiningFloorStepKinds.ExitMine, reason, row.Candidate!);
                    if (row.Exit.TryGetProperty("expected_destination", out var destination) && destination.ValueKind == JsonValueKind.Object)
                    {
                        plan.ExpectedTargetLocation = ReadString(destination, "location_id");
                        plan.ExpectedArrivalTileX = ReadInt(destination, "tile_x");
                        plan.ExpectedArrivalTileY = ReadInt(destination, "tile_y");
                    }
                    plan.SafetyWindowStatus = "mandatory_retreat_native_exit";
                    return plan;
                })
                .FirstOrDefault();
        }

        private static string MandatoryRetreatReason(JsonElement resources, MiningFloorObjective objective, int? currentDepth)
        {
            var reasons = new List<string>();
            if (objective.TargetDepth.HasValue && currentDepth.HasValue && currentDepth.Value >= objective.TargetDepth.Value)
            {
                reasons.Add("target_depth_reached");
            }
            if (objective.LatestExitTime.HasValue && (ReadInt(resources, "current_time") ?? 0) >= objective.LatestExitTime.Value)
            {
                reasons.Add("latest_exit_time_reached");
            }
            if (objective.MinimumReserveEnergy.HasValue && (ReadDouble(resources, "energy") ?? 0) <= objective.MinimumReserveEnergy.Value)
            {
                reasons.Add("minimum_reserve_energy_reached");
            }
            return reasons.Count == 0 ? string.Empty : "retreat_required:" + string.Join(",", reasons);
        }

        private static MiningFloorStepPlan? SelectActionTile(JsonElement tiles, string propertyName, string stepKind, string reason, SearchResult search, bool[,] grid)
        {
            if (!tiles.TryGetProperty(propertyName, out var candidates) || candidates.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return candidates.EnumerateArray()
                .Select(candidate => TargetCandidate(candidate, search, grid, estimatedSwings: 0, deterministicLadder: false))
                .Where(candidate => candidate is not null)
                .OrderBy(candidate => candidate!.Distance)
                .ThenBy(candidate => candidate!.TargetY)
                .ThenBy(candidate => candidate!.TargetX)
                .Select(candidate => Build(stepKind, reason, candidate!))
                .FirstOrDefault();
        }

        private static MiningFloorStepPlan? SelectMonster(
            JsonElement monsters,
            SearchResult search,
            bool[,] grid,
            string reason,
            string[]? targetDropIds = null,
            IReadOnlyDictionary<string, MonsterDropCatalogInfo>? dropCatalogs = null,
            double? movementTileDurationMs = null,
            bool bombFinisherAvailable = false)
        {
            if (monsters.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var targets = targetDropIds is { Length: > 0 }
                ? new HashSet<string>(targetDropIds, StringComparer.OrdinalIgnoreCase)
                : null;
            return monsters.EnumerateArray()
                .Select(monster =>
                {
                    var possible = ExpandMonsterPossibleDrops(monster, dropCatalogs);
                    return new
                    {
                        Monster = monster,
                        Candidate = TargetCandidate(monster, search, grid, estimatedSwings: 0, deterministicLadder: false),
                        Match = BuildMonsterDropMatch(monster, targets, possible, dropCatalogs),
                        Combat = ReadBestCombatProjection(monster, search.Start, grid, movementTileDurationMs, bombFinisherAvailable)
                    };
                })
                .Where(row => targets is null || row.Match.MatchedIds.Length > 0)
                .Where(row => !IsRevivingMummy(row.Monster) || row.Combat is not null)
                .Where(row => CanDefeatWithAvailableCombat(row.Monster, row.Combat))
                .Where(row => row.Candidate is not null)
                .OrderBy(row => targets is null || row.Match.IsGuaranteed ? 0 : row.Match.ChanceKnown ? 1 : 2)
                .ThenByDescending(row => row.Match.Efficiency(row.Candidate!.Distance, row.Combat?.DurationMs, movementTileDurationMs))
                .ThenByDescending(row => row.Match.ExpectedQuantityPerKill ?? -1d)
                .ThenBy(row => row.Candidate!.Distance)
                .ThenBy(row => row.Candidate!.TargetY)
                .ThenBy(row => row.Candidate!.TargetX)
                .Select(row =>
                {
                    var plan = row.Combat?.Method == "slingshot"
                        ? BuildRangedCombat(reason, row.Candidate!, search.Start)
                        : Build(MiningFloorStepKinds.CombatMonster, reason, row.Candidate!);
                    plan.TargetRuntimeIdentity = ReadString(row.Monster, "runtime_identity");
                    plan.TargetRuntimeType = ReadString(row.Monster, "runtime_type");
                    plan.TargetName = ReadString(row.Monster, "name");
                    plan.CombatMethod = row.Combat?.Method ?? "melee";
                    plan.CombatTerminalState = row.Combat?.TerminalEffect ?? "defeat";
                    plan.RequiredWeaponEnchantmentRuntimeType = plan.CombatMethod == "melee" ? ReadRequiredWeaponEnchantment(row.Monster) : string.Empty;
                    plan.CombatWeaponSlotIndex = plan.CombatMethod == "melee" ? row.Combat?.SlotIndex : null;
                    plan.SlingshotSlotIndex = plan.CombatMethod == "slingshot" ? row.Combat?.SlotIndex : null;
                    plan.SlingshotAmmoQualifiedItemId = plan.CombatMethod == "slingshot" ? row.Combat?.AmmoQualifiedItemId ?? string.Empty : string.Empty;
                    plan.ExpectedCombatAttacks = row.Combat?.ExpectedAttacks;
                    plan.ExpectedCombatDurationMs = row.Combat?.DurationMs;
                    var movementDistance = plan.CombatMethod == "slingshot" ? 0 : row.Candidate!.Distance;
                    plan.EstimatedTargetCostMs = row.Combat is not null && movementTileDurationMs.HasValue
                        ? row.Combat.DurationMs + movementDistance * movementTileDurationMs.Value
                        : null;
                    plan.CombatDurationStatus = row.Combat is null
                        ? "unavailable_no_complete_active_melee_projection"
                        : plan.CombatMethod == "slingshot"
                            ? "decompiled_full_charge_plus_clear_current_projectile_line"
                            : movementTileDurationMs.HasValue ? "exact_active_melee_plus_unobstructed_bfs_movement" : "exact_active_melee_only";
                    plan.TargetQualifiedItemId = targets is null ? string.Empty : row.Match.TargetId;
                    plan.ExpectedDropQualifiedItemIds = targets is null ? Array.Empty<string>() : row.Match.MatchedIds;
                    plan.SourceMatchStatus = targets is null
                        ? string.Empty
                        : row.Match.IsGuaranteed ? "guaranteed_monster_drop" : "conditional_monster_drop";
                    plan.TargetDropChancePreview = targets is null ? null : row.Match.Chance;
                    plan.TargetDropProbabilityStatus = targets is null ? string.Empty : row.Match.ProbabilityStatus;
                    plan.TargetExpectedQuantityPerKill = targets is null ? null : row.Match.ExpectedQuantityPerKill;
                    plan.TargetDropEfficiencyScore = targets is null || !row.Match.ChanceKnown
                        ? null
                        : row.Match.Efficiency(row.Candidate!.Distance, row.Combat?.DurationMs, movementTileDurationMs);
                    return plan;
                })
                .FirstOrDefault();
        }

        private static bool CanDefeatWithAvailableCombat(JsonElement monster, MonsterCombatProjectionInfo? combat)
        {
            if (combat is not null)
            {
                return true;
            }
            return !monster.TryGetProperty("melee_damage_semantics", out var semantics) ||
                semantics.ValueKind != JsonValueKind.Object ||
                !semantics.TryGetProperty("can_defeat_with_available_melee_weapon", out var value) ||
                value.ValueKind != JsonValueKind.False;
        }

        private static string ReadRequiredWeaponEnchantment(JsonElement monster)
        {
            return monster.TryGetProperty("melee_damage_semantics", out var semantics) && semantics.ValueKind == JsonValueKind.Object
                ? ReadString(semantics, "required_weapon_enchantment_runtime_type")
                : string.Empty;
        }

        private static double? ReadMovementTileDuration(JsonElement resources)
        {
            return resources.TryGetProperty("cardinal_movement", out var movement) && movement.ValueKind == JsonValueKind.Object
                ? ReadDouble(movement, "tile_duration_ms")
                : null;
        }

        private static MonsterCombatProjectionInfo? ReadBestCombatProjection(
            JsonElement monster,
            (int X, int Y) playerTile,
            bool[,] grid,
            double? movementTileDurationMs,
            bool bombFinisherAvailable)
        {
            var melee = ReadCombatProjections(monster, "melee_attack_projections", "melee", "expected_attacks_to_defeat",
                    "exact_active_melee_phase_excluding_movement")
                .Concat(bombFinisherAvailable
                    ? ReadCombatProjections(monster, "melee_attack_projections", "melee", "expected_attacks_to_defeat",
                        "exact_active_melee_phase_to_mummy_knockdown_excluding_movement")
                    : Array.Empty<MonsterCombatProjectionInfo>())
                .ToArray();
            var slingshot = ReadCombatProjections(monster, "slingshot_attack_projections", "slingshot", "expected_shots_to_defeat",
                    "exact_charge_phase_excluding_projectile_travel_and_reposition")
                .Where(projection =>
                    !string.Equals(projection.AmmoQualifiedItemId, "(O)441", StringComparison.Ordinal) ||
                    projection.ExplosiveAreaSafe && projection.ExplosiveAreaHasAdditionalValue)
                .Where(projection => projection.AmmoStack >= Math.Ceiling(projection.ExpectedAttacks ?? double.MaxValue))
                .Where(_ => HasClearProjectileLine(playerTile, (ReadInt(monster, "tile_x") ?? -1, ReadInt(monster, "tile_y") ?? -1), grid))
                .ToArray();
            var meleeWithMovement = melee.Select(projection => projection.WithSelectionCost(
                projection.DurationMs + (movementTileDurationMs ?? 0d) * Math.Max(0,
                    Math.Abs((ReadInt(monster, "tile_x") ?? playerTile.X) - playerTile.X) +
                    Math.Abs((ReadInt(monster, "tile_y") ?? playerTile.Y) - playerTile.Y) - 1)));
            var rangedWithPolicy = slingshot
                .Where(projection =>
                    Math.Abs((ReadInt(monster, "tile_x") ?? playerTile.X) - playerTile.X) +
                    Math.Abs((ReadInt(monster, "tile_y") ?? playerTile.Y) - playerTile.Y) >= 4)
                .Select(projection => projection.WithSelectionCost(
                    string.Equals(projection.AmmoQualifiedItemId, "(O)441", StringComparison.Ordinal)
                        ? projection.DurationMs / projection.ExplosiveAreaValueMultiplier
                        : projection.DurationMs));
            return meleeWithMovement.Concat(rangedWithPolicy)
                .OrderBy(projection => projection.SelectionCostMs)
                .ThenBy(projection => projection.Method == "melee" ? 0 : 1)
                .ThenBy(projection => projection.SlotIndex)
                .FirstOrDefault();
        }

        private static IEnumerable<MonsterCombatProjectionInfo> ReadCombatProjections(
            JsonElement monster,
            string propertyName,
            string method,
            string expectedAttacksProperty,
            string requiredDurationStatus)
        {
            if (!monster.TryGetProperty(propertyName, out var projections) || projections.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }
            foreach (var projection in projections.EnumerateArray())
            {
                var explicitDefeatGate = projection.TryGetProperty("can_defeat_with_this_weapon", out var defeatValue);
                var terminalEffect = ReadString(projection, "terminal_effect");
                var acceptedTerminal = terminalEffect == "defeat" ||
                    terminalEffect == "knockdown_requires_bomb_finish";
                if (!string.Equals(ReadString(projection, "duration_status"), requiredDurationStatus, StringComparison.Ordinal) ||
                    explicitDefeatGate && defeatValue.ValueKind != JsonValueKind.True && !acceptedTerminal)
                {
                    continue;
                }
                var parsed = new MonsterCombatProjectionInfo(
                    method,
                    ReadInt(projection, "slot_index"),
                    ReadDouble(projection, expectedAttacksProperty),
                    ReadDouble(projection, "expected_active_damage_duration_ms"),
                    ReadString(projection, "ammo_qualified_item_id"),
                    ReadInt(projection, "ammo_stack") ?? 0,
                    terminalEffect: string.IsNullOrWhiteSpace(terminalEffect) ? "defeat" : terminalEffect,
                    explosiveAreaSafe: ReadBool(projection, "explosive_area_safe"),
                    explosiveAreaHasAdditionalValue: ReadBool(projection, "explosive_area_has_additional_value"),
                    explosiveAreaUsefulObjectHits: ReadInt(projection, "explosive_area_useful_object_hits") ?? 0,
                    explosiveAreaAdditionalMonsterHits: ReadInt(projection, "explosive_area_additional_monster_hits") ?? 0);
                if (parsed.SlotIndex.HasValue && parsed.ExpectedAttacks.HasValue && parsed.DurationMs.HasValue && parsed.DurationMs.Value >= 0d)
                {
                    yield return parsed;
                }
            }
        }

        private static MonsterDropMatch BuildMonsterDropMatch(
            JsonElement monster,
            HashSet<string>? targets,
            string[] possible,
            IReadOnlyDictionary<string, MonsterDropCatalogInfo>? dropCatalogs)
        {
            if (targets is null)
            {
                return new MonsterDropMatch();
            }
            var matched = possible.Where(targets.Contains).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var guaranteed = new HashSet<string>(ReadStrings(monster, "guaranteed_drop_qualified_item_ids"), StringComparer.OrdinalIgnoreCase);
            var guaranteedTarget = matched.FirstOrDefault(guaranteed.Contains);
            if (!string.IsNullOrWhiteSpace(guaranteedTarget))
            {
                return new MonsterDropMatch
                {
                    MatchedIds = matched,
                    TargetId = guaranteedTarget,
                    IsGuaranteed = true,
                    Chance = 1d,
                    ProbabilityStatus = "guaranteed_from_live_projection"
                };
            }

            var bestTarget = string.Empty;
            double? bestChance = null;
            double? bestExpectedQuantity = null;
            var bestMatchingRuleCount = 0;
            foreach (var target in matched)
            {
                var targetRules = ReadExactTargetProbabilityRules(monster, target, dropCatalogs).ToArray();
                if (targetRules.Length == 0)
                {
                    continue;
                }
                var chance = targetRules.Max(rule => rule.Chance);
                var expectedQuantity = targetRules.All(rule => rule.ExpectedQuantity.HasValue)
                    ? targetRules.Sum(rule => rule.ExpectedQuantity!.Value)
                    : (double?)null;
                if (!bestChance.HasValue || chance > bestChance.Value ||
                    chance == bestChance.Value && (expectedQuantity ?? -1d) > (bestExpectedQuantity ?? -1d))
                {
                    bestTarget = target;
                    bestChance = chance;
                    bestExpectedQuantity = expectedQuantity;
                    bestMatchingRuleCount = targetRules.Length;
                }
            }

            return new MonsterDropMatch
            {
                MatchedIds = matched,
                TargetId = !string.IsNullOrWhiteSpace(bestTarget) ? bestTarget : matched.FirstOrDefault() ?? string.Empty,
                Chance = bestChance,
                ExpectedQuantityPerKill = bestExpectedQuantity,
                ProbabilityStatus = bestChance.HasValue
                    ? bestMatchingRuleCount == 1 ? "exact_current_snapshot" : "best_exact_rule_lower_bound_multiple_sources"
                    : "unavailable_no_stable_per_identity_probability"
            };
        }

        private static IEnumerable<TargetProbabilityRule> ReadExactTargetProbabilityRules(
            JsonElement monster,
            string target,
            IReadOnlyDictionary<string, MonsterDropCatalogInfo>? dropCatalogs)
        {
            if (!monster.TryGetProperty("drop_probability_rules", out var rules) || rules.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }
            foreach (var rule in rules.EnumerateArray())
            {
                var itemSelectionStatus = ReadString(rule, "item_selection_status");
                if (!string.Equals(ReadString(rule, "probability_status"), "exact_current_state_formula", StringComparison.Ordinal) ||
                    itemSelectionStatus.Contains("current_position", StringComparison.Ordinal) ||
                    itemSelectionStatus.Contains("current_death_tile", StringComparison.Ordinal))
                {
                    continue;
                }
                var directMatch = ReadStrings(rule, "qualified_item_ids").Contains(target, StringComparer.OrdinalIgnoreCase);
                var catalogKey = ReadString(rule, "catalog_key");
                var catalogMatch = false;
                MonsterDropCatalogEntryInfo? catalogEntry = null;
                if (!string.IsNullOrWhiteSpace(catalogKey) &&
                    dropCatalogs is not null &&
                    dropCatalogs.TryGetValue(catalogKey, out var catalog) &&
                    catalog.Ids.Contains(target, StringComparer.OrdinalIgnoreCase))
                {
                    catalogMatch = true;
                    if (catalog.SelectionEntries.TryGetValue(target, out var parsedEntry))
                    {
                        catalogEntry = parsedEntry;
                    }
                }
                if (!directMatch && !catalogMatch)
                {
                    continue;
                }
                var chance = ReadDouble(rule, "per_identity_chance");
                double? expectedQuantity = ReadDouble(rule, "expected_quantity_per_kill");
                if (!chance.HasValue && catalogMatch && catalogEntry is not null)
                {
                    var eventChance = ReadDouble(rule, "effective_per_kill_chance");
                    if (eventChance.HasValue)
                    {
                        chance = eventChance.Value * catalogEntry.ConditionalSelectionChance;
                        var expectedEvents = ReadDouble(rule, "expected_events_per_kill") ?? eventChance.Value;
                        expectedQuantity = expectedEvents * catalogEntry.ConditionalSelectionChance * catalogEntry.ConditionalExpectedQuantity;
                    }
                }
                if (!chance.HasValue)
                {
                    continue;
                }
                yield return new TargetProbabilityRule(chance.Value, expectedQuantity);
            }
        }

        private static string[] ExpandMonsterPossibleDrops(JsonElement monster, IReadOnlyDictionary<string, MonsterDropCatalogInfo>? dropCatalogs)
        {
            var possible = new HashSet<string>(
                ReadStringsWithLegacyFallback(monster, "possible_drop_qualified_item_ids", "selected_drop_qualified_item_ids"),
                StringComparer.OrdinalIgnoreCase);
            if (dropCatalogs is null)
            {
                return possible.OrderBy(id => id, StringComparer.Ordinal).ToArray();
            }
            foreach (var key in ReadStrings(monster, "conditional_drop_catalog_keys"))
            {
                if (dropCatalogs.TryGetValue(key, out var catalog))
                {
                    possible.UnionWith(catalog.Ids);
                }
            }
            return possible.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        }

        private static IReadOnlyDictionary<string, MonsterDropCatalogInfo> ReadMonsterDropCatalogs(JsonElement mining)
        {
            var result = new Dictionary<string, MonsterDropCatalogInfo>(StringComparer.Ordinal);
            if (!TryFieldValue(mining, "monster_drop_catalogs", out var catalogs) || catalogs.ValueKind != JsonValueKind.Array)
            {
                return result;
            }
            foreach (var catalog in catalogs.EnumerateArray())
            {
                var key = ReadString(catalog, "key");
                var completeness = ReadString(catalog, "item_identity_completeness");
                if (string.IsNullOrWhiteSpace(key) ||
                    !ReadBool(catalog, "active") ||
                    !completeness.StartsWith("complete", StringComparison.Ordinal))
                {
                    continue;
                }
                var ids = ReadStrings(catalog, "possible_qualified_item_ids");
                var selectionEntries = new Dictionary<string, MonsterDropCatalogEntryInfo>(StringComparer.OrdinalIgnoreCase);
                var probabilityCompleteness = ReadString(catalog, "selection_probability_completeness");
                if (probabilityCompleteness.StartsWith("complete", StringComparison.Ordinal) &&
                    catalog.TryGetProperty("selection_probability_entries", out var entries) &&
                    entries.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in entries.EnumerateArray())
                    {
                        var id = ReadString(entry, "qualified_item_id");
                        var chance = ReadDouble(entry, "conditional_selection_chance");
                        var expectedQuantity = ReadDouble(entry, "conditional_expected_quantity") ?? 1d;
                        var status = ReadString(entry, "probability_status");
                        if (!string.IsNullOrWhiteSpace(id) && chance.HasValue && chance.Value >= 0d && chance.Value <= 1d && expectedQuantity > 0d &&
                            (string.Equals(status, "exact_decompiled_weight_with_loaded_furniture_fallback", StringComparison.Ordinal) ||
                             string.Equals(status, "exact_uniform_loaded_catalog", StringComparison.Ordinal) ||
                             string.Equals(status, "exact_decompiled_hard_mine_treasure_tree", StringComparison.Ordinal)))
                        {
                            selectionEntries[id] = new MonsterDropCatalogEntryInfo(chance.Value, expectedQuantity);
                        }
                    }
                }
                var idSet = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
                var probabilityMass = selectionEntries.Values.Sum(entry => entry.ConditionalSelectionChance);
                if (selectionEntries.Count != idSet.Count ||
                    selectionEntries.Keys.Any(id => !idSet.Contains(id)) ||
                    Math.Abs(probabilityMass - 1d) > 0.000000001d)
                {
                    selectionEntries.Clear();
                }
                result[key] = new MonsterDropCatalogInfo(ids, selectionEntries);
            }
            return result;
        }

        private static MiningFloorStepPlan? SelectImmediateThreat(
            JsonElement monsters,
            SearchResult search,
            bool[,] grid,
            (int X, int Y) start,
            int radiusTiles,
            bool bombFinisherAvailable,
            double? movementTileDurationMs)
        {
            return monsters.EnumerateArray()
                .Where(monster =>
                {
                    var x = ReadInt(monster, "tile_x");
                    var y = ReadInt(monster, "tile_y");
                    return x.HasValue && y.HasValue && Math.Abs(x.Value - start.X) + Math.Abs(y.Value - start.Y) <= Math.Max(1, radiusTiles);
                })
                .Select(monster => new
                {
                    Monster = monster,
                    Candidate = TargetCandidate(monster, search, grid, 0, false),
                    Combat = ReadBestCombatProjection(monster, start, grid, movementTileDurationMs, bombFinisherAvailable)
                })
                .Where(row => row.Candidate is not null)
                .OrderBy(row => row.Candidate!.Distance)
                .Select(row =>
                {
                    var plan = Build(MiningFloorStepKinds.CombatMonster, "immediate_monster_threat", row.Candidate!);
                    plan.TargetRuntimeIdentity = ReadString(row.Monster, "runtime_identity");
                    plan.TargetRuntimeType = ReadString(row.Monster, "runtime_type");
                    plan.TargetName = ReadString(row.Monster, "name");
                    plan.CombatMethod = "melee";
                    plan.CombatTerminalState = row.Combat?.TerminalEffect ?? "defeat";
                    plan.RequiredWeaponEnchantmentRuntimeType = ReadRequiredWeaponEnchantment(row.Monster);
                    plan.CombatWeaponSlotIndex = row.Combat?.Method == "melee" ? row.Combat.SlotIndex : null;
                    return plan;
                })
                .FirstOrDefault();
        }

        private static MiningFloorStepPlan? SelectTargetObject(
            JsonElement objects,
            SearchResult search,
            bool[,] grid,
            string[] targetDropIds,
            string[] sourceIds,
            int? restoreSlot)
        {
            var requestedDrops = new HashSet<string>(targetDropIds, StringComparer.OrdinalIgnoreCase);
            var explicitSources = new HashSet<string>(sourceIds, StringComparer.OrdinalIgnoreCase);
            if (requestedDrops.Count == 0 && explicitSources.Count == 0)
            {
                return null;
            }

            return objects.EnumerateArray()
                .Select(obj => BuildObjectSourceMatch(obj, requestedDrops, explicitSources, search, grid))
                .Where(row => row is not null && row.Candidate is not null)
                .OrderBy(row => row!.MatchRank)
                .ThenBy(row => row!.Candidate!.Distance + row.Candidate.Swings)
                .Select(row =>
                {
                    var stepKind = ReadBool(row!.Object, "is_container") ? MiningFloorStepKinds.BreakContainer : MiningFloorStepKinds.MineStone;
                    var plan = Build(stepKind, "target_resource_or_artifact_source_reachable", row.Candidate!);
                    plan.TargetQualifiedItemId = ReadString(row.Object, "qualified_item_id");
                    plan.ExpectedDropQualifiedItemIds = row.MatchedDropIds;
                    plan.SourceMatchStatus = row.MatchStatus;
                    plan.RestoreSlotIndex = restoreSlot;
                    plan.SafetyWindowStatus = "clear_at_snapshot";
                    return plan;
                })
                .FirstOrDefault();
        }

        private static ObjectSourceMatch? BuildObjectSourceMatch(
            JsonElement obj,
            HashSet<string> requestedDrops,
            HashSet<string> explicitSources,
            SearchResult search,
            bool[,] grid)
        {
            var sourceId = ReadString(obj, "qualified_item_id");
            var guaranteed = new HashSet<string>(ReadStrings(obj, "guaranteed_drop_qualified_item_ids"), StringComparer.OrdinalIgnoreCase);
            var possible = new HashSet<string>(ReadStrings(obj, "possible_drop_qualified_item_ids"), StringComparer.OrdinalIgnoreCase);
            var matchedGuaranteed = requestedDrops.Where(guaranteed.Contains).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var matchedPossible = requestedDrops.Where(possible.Contains).OrderBy(id => id, StringComparer.Ordinal).ToArray();

            string matchStatus;
            int matchRank;
            string[] matchedDropIds;
            if (explicitSources.Contains(sourceId))
            {
                matchStatus = "explicit_source_id";
                matchRank = 0;
                matchedDropIds = matchedPossible;
            }
            else if (matchedGuaranteed.Length > 0)
            {
                matchStatus = "guaranteed_drop";
                matchRank = 0;
                matchedDropIds = matchedGuaranteed;
            }
            else if (matchedPossible.Length > 0)
            {
                matchStatus = "conditional_drop";
                matchRank = 1;
                matchedDropIds = matchedPossible;
            }
            else
            {
                return null;
            }

            return new ObjectSourceMatch
            {
                Object = obj,
                Candidate = TargetCandidate(obj, search, grid, Math.Max(1, ReadInt(obj, "best_pickaxe_hits_remaining") ?? 1), false),
                MatchRank = matchRank,
                MatchStatus = matchStatus,
                MatchedDropIds = matchedDropIds
            };
        }

        private static MiningFloorStepPlan? SelectDebris(JsonElement debris, SearchResult search, bool[,] grid, string[] targetIds, int? restoreSlot, int? maximumDistance = null)
        {
            var targets = new HashSet<string>(targetIds, StringComparer.OrdinalIgnoreCase);
            MiningFloorStepPlan? best = null;
            foreach (var row in debris.EnumerateArray())
            {
                var qualifiedItemId = ReadString(row, "qualified_item_id");
                if (targets.Count > 0 && !targets.Contains(qualifiedItemId) ||
                    !row.TryGetProperty("chunks", out var chunks) || chunks.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var chunk in chunks.EnumerateArray())
                {
                    var candidate = WalkCandidate(chunk, search, grid);
                    if (candidate is null || maximumDistance.HasValue && candidate.Distance > maximumDistance.Value || best is not null && candidate.Distance >= best.EstimatedMovementTiles)
                    {
                        continue;
                    }

                    var plan = Build(MiningFloorStepKinds.PickupDebris, "target_debris_reachable", candidate);
                    plan.TargetQualifiedItemId = qualifiedItemId;
                    plan.DebrisIndex = ReadInt(row, "debris_index");
                    plan.RestoreSlotIndex = restoreSlot;
                    best = plan;
                }
            }
            return best;
        }

        private static MiningFloorStepPlan? SelectContainer(JsonElement objects, SearchResult search, bool[,] grid, int? maximumDistance = null)
        {
            return objects.EnumerateArray()
                .Where(obj => ReadBool(obj, "is_container"))
                .Select(obj => new
                {
                    Object = obj,
                    Candidate = TargetCandidate(obj, search, grid, Math.Max(1, ReadInt(obj, "health_or_hits_remaining") ?? 3), false)
                })
                .Where(row => row.Candidate is not null && (!maximumDistance.HasValue || row.Candidate.Distance <= maximumDistance.Value))
                .OrderBy(row => row.Candidate!.Distance + row.Candidate.Swings)
                .ThenBy(row => ReadInt(row.Object, "tile_y") ?? int.MaxValue)
                .ThenBy(row => ReadInt(row.Object, "tile_x") ?? int.MaxValue)
                .Select(row =>
                {
                    var plan = Build(MiningFloorStepKinds.BreakContainer, "opportunistic_breakable_container_within_four_tiles", row.Candidate!);
                    plan.TargetQualifiedItemId = ReadString(row.Object, "qualified_item_id");
                    plan.SafetyWindowStatus = "clear_at_snapshot";
                    return plan;
                })
                .FirstOrDefault();
        }

        private static bool HasAvailableBomb(JsonElement resources)
        {
            return resources.TryGetProperty("bomb_slots", out var bombSlots) &&
                bombSlots.ValueKind == JsonValueKind.Array &&
                bombSlots.EnumerateArray().Any(bomb =>
                    (ReadInt(bomb, "stack") ?? 0) > 0 &&
                    (ReadInt(bomb, "radius_tiles") ?? 0) > 0 &&
                    ReadInt(bomb, "slot_index").HasValue);
        }

        private static MiningFloorStepPlan? SelectMummyBombFinisher(
            JsonElement objects,
            JsonElement monsters,
            JsonElement resources,
            SearchResult search,
            bool[,] grid,
            double? movementTileDurationMs,
            MiningFloorObjective objective,
            bool mustKillAll)
        {
            if (monsters.ValueKind != JsonValueKind.Array ||
                !resources.TryGetProperty("bomb_slots", out var bombSlots) ||
                bombSlots.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            var protectedObjects = objects.ValueKind == JsonValueKind.Array
                ? objects.EnumerateArray()
                    .Where(obj => ReadBool(obj, "is_placed_staircase") ||
                        (!ReadBool(obj, "is_breakable_stone") && !ReadBool(obj, "is_container")))
                    .Select(obj => (X: ReadInt(obj, "tile_x"), Y: ReadInt(obj, "tile_y")))
                    .Where(row => row.X.HasValue && row.Y.HasValue)
                    .Select(row => (X: row.X!.Value, Y: row.Y!.Value))
                    .ToArray()
                : Array.Empty<(int X, int Y)>();

            MummyBombFinisherCandidate? best = null;
            foreach (var monster in monsters.EnumerateArray())
            {
                if (!monster.TryGetProperty("bomb_damage_semantics", out var semantics) ||
                    !string.Equals(ReadString(semantics, "special_effect"), "bomb_finalizes_reviving_mummy", StringComparison.Ordinal))
                {
                    continue;
                }
                if (!MummyFinishRequired(monster, search.Start, objective, mustKillAll))
                {
                    continue;
                }
                var targetX = ReadInt(monster, "tile_x");
                var targetY = ReadInt(monster, "tile_y");
                if (!targetX.HasValue || !targetY.HasValue)
                {
                    continue;
                }
                foreach (var bomb in bombSlots.EnumerateArray().OrderBy(bomb => ReadInt(bomb, "radius_tiles") ?? int.MaxValue))
                {
                    var radius = ReadInt(bomb, "radius_tiles") ?? 0;
                    var slot = ReadInt(bomb, "slot_index");
                    if (radius <= 0 || !slot.HasValue || (ReadInt(bomb, "stack") ?? 0) <= 0)
                    {
                        continue;
                    }
                    foreach (var center in MummyBombPlacementCenters(targetX.Value, targetY.Value, radius))
                    {
                        if (!InBounds(grid, center.X, center.Y) ||
                            grid[center.X, center.Y] ||
                            protectedObjects.Any(obj => obj.X == center.X && obj.Y == center.Y) ||
                            objects.ValueKind == JsonValueKind.Array && objects.EnumerateArray().Any(obj =>
                                ReadInt(obj, "tile_x") == center.X && ReadInt(obj, "tile_y") == center.Y))
                        {
                            continue;
                        }
                        var placement = TargetCandidate(center.X, center.Y, search, grid, 0, false);
                        if (placement is null ||
                            protectedObjects.Any(obj => ExactExplosionMask(radius).Contains((obj.X - center.X, obj.Y - center.Y))))
                        {
                            continue;
                        }
                        var escapeSearch = Search(grid, (placement.StandX, placement.StandY));
                        var escape = escapeSearch.Distance
                            .Select(entry =>
                            {
                                var values = entry.Key.Split(',');
                                return new
                                {
                                    X = int.Parse(values[0], CultureInfo.InvariantCulture),
                                    Y = int.Parse(values[1], CultureInfo.InvariantCulture),
                                    Distance = entry.Value
                                };
                            })
                            .Where(tile => Math.Abs(tile.X - center.X) > radius || Math.Abs(tile.Y - center.Y) > radius)
                            .OrderBy(tile => tile.Distance)
                            .ThenBy(tile => tile.Y)
                            .ThenBy(tile => tile.X)
                            .FirstOrDefault();
                        var maximumEscapeTiles = movementTileDurationMs.HasValue && movementTileDurationMs.Value > 0d
                            ? Math.Max(2, (int)Math.Floor(1900d / movementTileDurationMs.Value))
                            : 6;
                        if (escape is null || escape.Distance > maximumEscapeTiles)
                        {
                            continue;
                        }
                        var candidate = new MummyBombFinisherCandidate(monster, bomb, placement, escape.X, escape.Y, escape.Distance);
                        if (best is null || radius < best.Radius ||
                            radius == best.Radius && candidate.TotalDistance < best.TotalDistance)
                        {
                            best = candidate;
                        }
                    }
                }
            }
            if (best is null)
            {
                return null;
            }
            var plan = Build(MiningFloorStepKinds.PlaceBomb, "reviving_mummy_requires_bomb_finish", best.Placement);
            plan.TargetRuntimeIdentity = ReadString(best.Monster, "runtime_identity");
            plan.TargetRuntimeType = ReadString(best.Monster, "runtime_type");
            plan.TargetName = ReadString(best.Monster, "name");
            plan.CombatMethod = "bomb";
            plan.CombatTerminalState = "mummy_finalized";
            plan.BombSlotIndex = ReadInt(best.Bomb, "slot_index");
            plan.BombQualifiedItemId = ReadString(best.Bomb, "qualified_item_id");
            plan.BombRadiusTiles = best.Radius;
            plan.EscapeTileX = best.EscapeX;
            plan.EscapeTileY = best.EscapeY;
            plan.ExpectedBombMonsterHits = 1;
            plan.SafetyWindowStatus = "mummy_bomb_finish_fuse_escape_verified";
            return plan;
        }

        private static IEnumerable<(int X, int Y)> MummyBombPlacementCenters(int targetX, int targetY, int radius)
        {
            return Enumerable.Range(-radius, radius * 2 + 1)
                .SelectMany(offsetX => Enumerable.Range(-radius, radius * 2 + 1)
                    .Select(offsetY => (X: targetX + offsetX, Y: targetY + offsetY, OffsetX: offsetX, OffsetY: offsetY)))
                .Where(row => row.OffsetX != 0 || row.OffsetY != 0)
                .Where(row => Math.Abs(row.OffsetX) <= radius && Math.Abs(row.OffsetY) <= radius)
                .OrderBy(row => Math.Abs(row.OffsetX) + Math.Abs(row.OffsetY))
                .ThenBy(row => row.Y)
                .ThenBy(row => row.X)
                .Select(row => (row.X, row.Y));
        }

        private static bool MummyFinishRequired(
            JsonElement monster,
            (int X, int Y) playerTile,
            MiningFloorObjective objective,
            bool mustKillAll)
        {
            if (mustKillAll)
            {
                return true;
            }
            if (objective.Kind == MiningObjectiveKinds.CollectMonsterDrop)
            {
                var targets = new HashSet<string>(objective.TargetQualifiedItemIds ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
                return targets.Count > 0 && ReadStrings(monster, "possible_drop_qualified_item_ids").Any(targets.Contains);
            }
            var x = ReadInt(monster, "tile_x");
            var y = ReadInt(monster, "tile_y");
            return x.HasValue && y.HasValue &&
                Math.Abs(x.Value - playerTile.X) + Math.Abs(y.Value - playerTile.Y) <= Math.Max(1, objective.ThreatRadiusTiles);
        }

        private static bool IsRevivingMummy(JsonElement monster)
        {
            return monster.TryGetProperty("bomb_damage_semantics", out var semantics) &&
                string.Equals(ReadString(semantics, "special_effect"), "bomb_finalizes_reviving_mummy", StringComparison.Ordinal);
        }

        private static MiningFloorStepPlan? SelectBombCluster(
            JsonElement objects,
            JsonElement monsters,
            JsonElement resources,
            SearchResult search,
            bool[,] grid,
            (int X, int Y) start,
            double? movementTileDurationMs)
        {
            if (!resources.TryGetProperty("bomb_slots", out var bombSlots) || bombSlots.ValueKind != JsonValueKind.Array ||
                objects.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var objectRows = objects.EnumerateArray()
                .Select(obj => new
                {
                    X = ReadInt(obj, "tile_x"),
                    Y = ReadInt(obj, "tile_y"),
                    IsUseful = ReadBool(obj, "is_breakable_stone") || ReadBool(obj, "is_container"),
                    IsProtected = ReadBool(obj, "is_placed_staircase") ||
                        (!ReadBool(obj, "is_breakable_stone") && !ReadBool(obj, "is_container"))
                })
                .Where(row => row.X.HasValue && row.Y.HasValue)
                .ToArray();
            var monsterRows = monsters.ValueKind == JsonValueKind.Array
                ? monsters.EnumerateArray().Select(monster => (X: ReadInt(monster, "tile_x"), Y: ReadInt(monster, "tile_y"))).ToArray()
                : Array.Empty<(int? X, int? Y)>();

            BombCandidate? best = null;
            foreach (var bomb in bombSlots.EnumerateArray())
            {
                var radius = ReadInt(bomb, "radius_tiles") ?? 0;
                var slotIndex = ReadInt(bomb, "slot_index");
                var stack = ReadInt(bomb, "stack") ?? 0;
                if (radius <= 0 || !slotIndex.HasValue || stack <= 0)
                {
                    continue;
                }
                var mask = ExactExplosionMask(radius);
                var minimumUsefulObjects = radius switch { <= 3 => 4, <= 5 => 7, _ => 10 };
                foreach (var pair in search.Distance.OrderBy(pair => pair.Value))
                {
                    var split = pair.Key.Split(',');
                    var center = (X: int.Parse(split[0], CultureInfo.InvariantCulture), Y: int.Parse(split[1], CultureInfo.InvariantCulture));
                    if (!InBounds(grid, center.X, center.Y) || grid[center.X, center.Y])
                    {
                        continue;
                    }
                    var candidate = TargetCandidate(center.X, center.Y, search, grid, 0, false);
                    if (candidate is null)
                    {
                        continue;
                    }
                    var affectedObjects = objectRows.Count(row => mask.Contains((row.X!.Value - center.X, row.Y!.Value - center.Y)));
                    var usefulObjects = objectRows.Count(row => row.IsUseful && mask.Contains((row.X!.Value - center.X, row.Y!.Value - center.Y)));
                    var protectedObjects = objectRows.Count(row => row.IsProtected && mask.Contains((row.X!.Value - center.X, row.Y!.Value - center.Y)));
                    var affectedMonsters = monsterRows.Count(row => row.X.HasValue && row.Y.HasValue &&
                        Math.Abs(row.X.Value - center.X) <= radius && Math.Abs(row.Y.Value - center.Y) <= radius);
                    if (protectedObjects > 0 || affectedObjects != usefulObjects ||
                        usefulObjects < minimumUsefulObjects && !(usefulObjects >= 2 && affectedMonsters >= 2))
                    {
                        continue;
                    }

                    var placementSearch = Search(grid, (candidate.StandX, candidate.StandY));
                    var escape = placementSearch.Distance
                        .Select(entry =>
                        {
                            var values = entry.Key.Split(',');
                            return new { X = int.Parse(values[0], CultureInfo.InvariantCulture), Y = int.Parse(values[1], CultureInfo.InvariantCulture), Distance = entry.Value };
                        })
                        .Where(tile => Math.Abs(tile.X - center.X) > radius || Math.Abs(tile.Y - center.Y) > radius)
                        .OrderBy(tile => tile.Distance)
                        .ThenBy(tile => tile.Y)
                        .ThenBy(tile => tile.X)
                        .FirstOrDefault();
                    var maximumEscapeTiles = movementTileDurationMs.HasValue && movementTileDurationMs.Value > 0d
                        ? Math.Max(2, (int)Math.Floor(1900d / movementTileDurationMs.Value))
                        : 6;
                    if (escape is null || escape.Distance > maximumEscapeTiles)
                    {
                        continue;
                    }

                    var score = usefulObjects * 10 + affectedMonsters * 4 - candidate.Distance - radius;
                    var row = new BombCandidate(
                        candidate,
                        slotIndex.Value,
                        ReadString(bomb, "qualified_item_id"),
                        radius,
                        escape.X,
                        escape.Y,
                        usefulObjects,
                        affectedMonsters,
                        score);
                    if (best is null || row.Score > best.Score ||
                        row.Score == best.Score && row.Candidate.Distance < best.Candidate.Distance)
                    {
                        best = row;
                    }
                }
            }

            if (best is null)
            {
                return null;
            }
            var plan = Build(MiningFloorStepKinds.PlaceBomb, "dense_breakable_cluster_with_verified_fuse_escape", best.Candidate);
            plan.BombSlotIndex = best.SlotIndex;
            plan.BombQualifiedItemId = best.QualifiedItemId;
            plan.BombRadiusTiles = best.Radius;
            plan.EscapeTileX = best.EscapeX;
            plan.EscapeTileY = best.EscapeY;
            plan.ExpectedBombObjectHits = best.ObjectHits;
            plan.ExpectedBombMonsterHits = best.MonsterHits;
            plan.SafetyWindowStatus = "bomb_fuse_escape_path_verified";
            return plan;
        }

        private static HashSet<(int X, int Y)> ExactExplosionMask(int radius)
        {
            var outline = new bool[radius * 2 + 1, radius * 2 + 1];
            var decision = 1 - radius;
            var deltaEast = 1;
            var deltaSouthEast = -2 * radius;
            var x = 0;
            var y = radius;
            outline[radius, radius + radius] = true;
            outline[radius, 0] = true;
            outline[radius + radius, radius] = true;
            outline[0, radius] = true;
            while (x < y)
            {
                if (decision >= 0)
                {
                    y--;
                    deltaSouthEast += 2;
                    decision += deltaSouthEast;
                }
                x++;
                deltaEast += 2;
                decision += deltaEast;
                outline[radius + x, radius + y] = true;
                outline[radius - x, radius + y] = true;
                outline[radius + x, radius - y] = true;
                outline[radius - x, radius - y] = true;
                outline[radius + y, radius + x] = true;
                outline[radius - y, radius + x] = true;
                outline[radius + y, radius - x] = true;
                outline[radius - y, radius - x] = true;
            }

            var affected = new HashSet<(int X, int Y)>();
            var fill = 0;
            for (var column = 0; column < radius * 2 + 1; column++)
            {
                for (var row = 0; row < radius * 2 + 1; row++)
                {
                    var include = false;
                    if (column == 0 || row == 0 || column == radius * 2 || row == radius * 2)
                    {
                        fill = outline[column, row] ? 1 : 0;
                    }
                    else if (outline[column, row])
                    {
                        fill += row <= radius ? 1 : -1;
                        include = fill <= 0;
                    }
                    if (fill >= 1)
                    {
                        include = true;
                    }
                    if (include)
                    {
                        affected.Add((column - radius, row - radius));
                    }
                }
            }
            return affected;
        }

        private static Candidate? WalkCandidate(JsonElement element, SearchResult search, bool[,] grid)
        {
            var x = ReadInt(element, "tile_x");
            var y = ReadInt(element, "tile_y");
            if (!x.HasValue || !y.HasValue || !InBounds(grid, x.Value, y.Value) || grid[x.Value, y.Value] || !search.Distance.TryGetValue(Key(x.Value, y.Value), out var distance))
            {
                return null;
            }
            return new Candidate(x.Value, y.Value, x.Value, y.Value, distance, 0, false, search.PathTo(x.Value, y.Value));
        }

        private static bool NeedsHealing(JsonElement resources, JsonElement monsters, MiningFloorObjective objective, out string reason)
        {
            var health = ReadInt(resources, "health") ?? 0;
            var maxDamage = monsters.ValueKind == JsonValueKind.Array
                ? monsters.EnumerateArray().Select(monster => ReadInt(monster, "damage_to_farmer") ?? 0).DefaultIfEmpty(0).Max()
                : 0;
            var floor = Math.Max(objective.MinimumReserveHealth, maxDamage * 2);
            reason = health <= floor ? "health_below_two_hit_or_configured_reserve" : string.Empty;
            return health <= floor;
        }

        private static MiningFloorStepPlan? SelectFood(JsonElement resources, int minimumReserveHealth, int? restoreSlot)
        {
            if (!resources.TryGetProperty("food_slots", out var foods) || foods.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            var health = ReadInt(resources, "health") ?? 0;
            return foods.EnumerateArray()
                .Where(food => (ReadInt(food, "health_recovery") ?? 0) > 0)
                .OrderBy(food => Math.Abs(health + (ReadInt(food, "health_recovery") ?? 0) - Math.Max(health, minimumReserveHealth)))
                .ThenBy(food => ReadInt(food, "sell_price") ?? int.MaxValue)
                .Select(food => new MiningFloorStepPlan
                {
                    Status = "ready",
                    StepKind = MiningFloorStepKinds.ConsumeFood,
                    Reason = "health_recovery_required",
                    FoodSlotIndex = ReadInt(food, "slot_index"),
                    RestoreSlotIndex = restoreSlot,
                    TargetQualifiedItemId = ReadString(food, "qualified_item_id"),
                    SafetyWindowStatus = "native_eating_lifecycle_handles_recovery_window"
                })
                .FirstOrDefault();
        }

        private static MiningFloorStepPlan? SelectStone(JsonElement objects, SearchResult search, bool[,] grid)
        {
            if (objects.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return objects.EnumerateArray()
                .Where(obj => ReadBool(obj, "is_breakable_stone"))
                .Select(obj =>
                {
                    var swings = Math.Max(1, ReadInt(obj, "best_pickaxe_hits_remaining") ?? 1);
                    var deterministic = obj.TryGetProperty("ladder_preview", out var preview) &&
                        preview.ValueKind == JsonValueKind.Object &&
                        ReadBool(preview, "creates_ladder");
                    return TargetCandidate(obj, search, grid, swings, deterministic);
                })
                .Where(candidate => candidate is not null)
                .OrderBy(candidate => candidate!.DeterministicLadder ? 0 : 1)
                .ThenBy(candidate => candidate!.Distance + candidate.Swings)
                .ThenBy(candidate => candidate!.Swings)
                .ThenBy(candidate => candidate!.Distance)
                .ThenBy(candidate => candidate!.TargetY)
                .ThenBy(candidate => candidate!.TargetX)
                .Select(candidate => Build(
                    MiningFloorStepKinds.MineStone,
                    candidate!.DeterministicLadder ? "deterministic_ladder_stone_reachable" : "lowest_reachable_movement_and_swing_cost",
                    candidate))
                .FirstOrDefault();
        }

        private static Candidate? TargetCandidate(JsonElement element, SearchResult search, bool[,] grid, int estimatedSwings, bool deterministicLadder)
        {
            var targetX = ReadInt(element, "tile_x");
            var targetY = ReadInt(element, "tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                return null;
            }
            return TargetCandidate(targetX.Value, targetY.Value, search, grid, estimatedSwings, deterministicLadder);
        }

        private static Candidate? TargetCandidate(int targetX, int targetY, SearchResult search, bool[,] grid, int estimatedSwings, bool deterministicLadder)
        {
            Candidate? best = null;
            foreach (var direction in Directions)
            {
                var standX = targetX + direction.X;
                var standY = targetY + direction.Y;
                if (!InBounds(grid, standX, standY) || grid[standX, standY] || !search.Distance.TryGetValue(Key(standX, standY), out var distance))
                {
                    continue;
                }

                var candidate = new Candidate(targetX, targetY, standX, standY, distance, estimatedSwings, deterministicLadder, search.PathTo(standX, standY));
                if (best is null || candidate.Distance < best.Distance ||
                    candidate.Distance == best.Distance && (candidate.StandY < best.StandY || candidate.StandY == best.StandY && candidate.StandX < best.StandX))
                {
                    best = candidate;
                }
            }

            return best;
        }

        private static MiningFloorStepPlan BuildRangedCombat(string reason, Candidate target, (int X, int Y) playerTile)
        {
            return new MiningFloorStepPlan
            {
                Status = "ready",
                StepKind = MiningFloorStepKinds.ShootMonster,
                Reason = reason,
                TargetTileX = target.TargetX,
                TargetTileY = target.TargetY,
                StandTileX = playerTile.X,
                StandTileY = playerTile.Y,
                EstimatedMovementTiles = 0,
                Path = new[] { new MiningPathTile { X = playerTile.X, Y = playerTile.Y } }
            };
        }

        private static bool HasClearProjectileLine((int X, int Y) start, (int X, int Y) target, bool[,] grid)
        {
            if (!InBounds(grid, target.X, target.Y))
            {
                return false;
            }
            var x = start.X;
            var y = start.Y;
            var deltaX = Math.Abs(target.X - start.X);
            var stepX = start.X < target.X ? 1 : -1;
            var deltaY = -Math.Abs(target.Y - start.Y);
            var stepY = start.Y < target.Y ? 1 : -1;
            var error = deltaX + deltaY;
            while (x != target.X || y != target.Y)
            {
                var doubled = 2 * error;
                if (doubled >= deltaY)
                {
                    error += deltaY;
                    x += stepX;
                }
                if (doubled <= deltaX)
                {
                    error += deltaX;
                    y += stepY;
                }
                if ((x != target.X || y != target.Y) && (!InBounds(grid, x, y) || grid[x, y]))
                {
                    return false;
                }
            }
            return true;
        }

        private static MiningFloorStepPlan Build(string stepKind, string reason, Candidate candidate)
        {
            return new MiningFloorStepPlan
            {
                Status = "ready",
                StepKind = stepKind,
                Reason = reason,
                TargetTileX = candidate.TargetX,
                TargetTileY = candidate.TargetY,
                StandTileX = candidate.StandX,
                StandTileY = candidate.StandY,
                EstimatedMovementTiles = candidate.Distance,
                EstimatedToolSwings = candidate.Swings,
                DeterministicLadderAfterBreak = candidate.DeterministicLadder,
                Path = candidate.Path
            };
        }

        private static MiningFloorStepPlan Blocked(string reason)
        {
            return new MiningFloorStepPlan { Reason = reason };
        }

        private static bool TryCollision(JsonElement tiles, out bool[,] grid, out (int X, int Y) start)
        {
            grid = new bool[0, 0];
            start = default;
            if (!tiles.TryGetProperty("player_tile", out var playerTile) ||
                !tiles.TryGetProperty("collision_context", out var collision) ||
                ReadString(collision, "status") != "available" ||
                ReadString(collision, "encoding") != "row_major_strings_1_blocked_0_passable" ||
                !collision.TryGetProperty("blocked_rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var width = ReadInt(collision, "width") ?? 0;
            var height = ReadInt(collision, "height") ?? 0;
            var rowValues = rows.EnumerateArray().Select(row => row.GetString() ?? string.Empty).ToArray();
            if (width <= 0 || height <= 0 || rowValues.Length != height || rowValues.Any(row => row.Length != width || row.Any(value => value != '0' && value != '1')))
            {
                return false;
            }

            grid = new bool[width, height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    grid[x, y] = rowValues[y][x] == '1';
                }
            }

            var startX = ReadInt(playerTile, "tile_x");
            var startY = ReadInt(playerTile, "tile_y");
            if (!startX.HasValue || !startY.HasValue || !InBounds(grid, startX.Value, startY.Value))
            {
                return false;
            }

            grid[startX.Value, startY.Value] = false;
            start = (startX.Value, startY.Value);
            return true;
        }

        private static SearchResult Search(bool[,] grid, (int X, int Y) start)
        {
            var distance = new Dictionary<string, int>(StringComparer.Ordinal) { [Key(start.X, start.Y)] = 0 };
            var previous = new Dictionary<string, string>(StringComparer.Ordinal);
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var direction in Directions)
                {
                    var next = (X: current.X + direction.X, Y: current.Y + direction.Y);
                    var key = Key(next.X, next.Y);
                    if (!InBounds(grid, next.X, next.Y) || grid[next.X, next.Y] || distance.ContainsKey(key))
                    {
                        continue;
                    }

                    distance[key] = distance[Key(current.X, current.Y)] + 1;
                    previous[key] = Key(current.X, current.Y);
                    queue.Enqueue(next);
                }
            }

            return new SearchResult(start, distance, previous);
        }

        private static bool TryFieldValue(JsonElement section, string field, out JsonElement value)
        {
            value = default;
            return section.TryGetProperty(field, out var envelope) &&
                envelope.ValueKind == JsonValueKind.Object &&
                (ReadString(envelope, "status") == "available" || ReadString(envelope, "status") == "derived") &&
                envelope.TryGetProperty("value", out value);
        }

        private static bool InBounds(bool[,] grid, int x, int y)
        {
            return x >= 0 && y >= 0 && x < grid.GetLength(0) && y < grid.GetLength(1);
        }

        private static string Key(int x, int y) => x + "," + y;

        private static int? ReadInt(JsonElement element, string property)
        {
            return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.TryGetInt32(out var parsed)
                ? parsed
                : (int?)null;
        }

        private static double? ReadDouble(JsonElement element, string property)
        {
            return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.TryGetDouble(out var parsed)
                ? parsed
                : (double?)null;
        }

        private static bool ReadBool(JsonElement element, string property)
        {
            return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
        }

        private static string ReadString(JsonElement element, string property)
        {
            return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
        }

        private static string[] ReadStrings(JsonElement element, string property)
        {
            return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
                ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? string.Empty).ToArray()
                : Array.Empty<string>();
        }

        private static string[] ReadStringsWithLegacyFallback(JsonElement element, string property, string fallbackProperty)
        {
            return element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(property, out var value) &&
                value.ValueKind == JsonValueKind.Array
                    ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? string.Empty).ToArray()
                    : ReadStrings(element, fallbackProperty);
        }

        private sealed class ObjectSourceMatch
        {
            public JsonElement Object { get; set; }

            public Candidate? Candidate { get; set; }

            public int MatchRank { get; set; }

            public string MatchStatus { get; set; } = string.Empty;

            public string[] MatchedDropIds { get; set; } = Array.Empty<string>();
        }

        private sealed class MonsterDropMatch
        {
            public string[] MatchedIds { get; set; } = Array.Empty<string>();

            public string TargetId { get; set; } = string.Empty;

            public bool IsGuaranteed { get; set; }

            public double? Chance { get; set; }

            public bool ChanceKnown => Chance.HasValue;

            public double? ExpectedQuantityPerKill { get; set; }

            public string ProbabilityStatus { get; set; } = string.Empty;

            public double Efficiency(int distance, double? combatDurationMs = null, double? movementTileDurationMs = null)
            {
                if (!Chance.HasValue)
                {
                    return -1d;
                }
                return combatDurationMs.HasValue && movementTileDurationMs.HasValue
                    ? Chance.Value / Math.Max(1d, combatDurationMs.Value + Math.Max(0, distance) * movementTileDurationMs.Value)
                    : Chance.Value / (Math.Max(0, distance) + 1d);
            }
        }

        private sealed class MonsterCombatProjectionInfo
        {
            public MonsterCombatProjectionInfo(
                string method,
                int? slotIndex,
                double? expectedAttacks,
                double? durationMs,
                string ammoQualifiedItemId,
                int ammoStack,
                double? selectionCostMs = null,
                string terminalEffect = "defeat",
                bool explosiveAreaSafe = false,
                bool explosiveAreaHasAdditionalValue = false,
                int explosiveAreaUsefulObjectHits = 0,
                int explosiveAreaAdditionalMonsterHits = 0)
            {
                Method = method;
                SlotIndex = slotIndex;
                ExpectedAttacks = expectedAttacks;
                DurationMs = durationMs;
                AmmoQualifiedItemId = ammoQualifiedItemId;
                AmmoStack = ammoStack;
                SelectionCostMs = selectionCostMs ?? durationMs ?? double.MaxValue;
                TerminalEffect = terminalEffect;
                ExplosiveAreaSafe = explosiveAreaSafe;
                ExplosiveAreaHasAdditionalValue = explosiveAreaHasAdditionalValue;
                ExplosiveAreaUsefulObjectHits = explosiveAreaUsefulObjectHits;
                ExplosiveAreaAdditionalMonsterHits = explosiveAreaAdditionalMonsterHits;
            }

            public string Method { get; }

            public int? SlotIndex { get; }

            public double? ExpectedAttacks { get; }

            public double? DurationMs { get; }

            public string AmmoQualifiedItemId { get; }

            public int AmmoStack { get; }

            public double SelectionCostMs { get; }

            public string TerminalEffect { get; }

            public bool ExplosiveAreaSafe { get; }

            public bool ExplosiveAreaHasAdditionalValue { get; }

            public int ExplosiveAreaUsefulObjectHits { get; }

            public int ExplosiveAreaAdditionalMonsterHits { get; }

            public double ExplosiveAreaValueMultiplier => Math.Min(
                3d,
                1d + ExplosiveAreaAdditionalMonsterHits + ExplosiveAreaUsefulObjectHits * 0.25d);

            public MonsterCombatProjectionInfo WithSelectionCost(double? selectionCostMs)
            {
                return new MonsterCombatProjectionInfo(
                    Method,
                    SlotIndex,
                    ExpectedAttacks,
                    DurationMs,
                    AmmoQualifiedItemId,
                    AmmoStack,
                    selectionCostMs,
                    TerminalEffect,
                    ExplosiveAreaSafe,
                    ExplosiveAreaHasAdditionalValue,
                    ExplosiveAreaUsefulObjectHits,
                    ExplosiveAreaAdditionalMonsterHits);
            }
        }

        private sealed class BombCandidate
        {
            public BombCandidate(Candidate candidate, int slotIndex, string qualifiedItemId, int radius, int escapeX, int escapeY, int objectHits, int monsterHits, int score)
            {
                Candidate = candidate;
                SlotIndex = slotIndex;
                QualifiedItemId = qualifiedItemId;
                Radius = radius;
                EscapeX = escapeX;
                EscapeY = escapeY;
                ObjectHits = objectHits;
                MonsterHits = monsterHits;
                Score = score;
            }

            public Candidate Candidate { get; }
            public int SlotIndex { get; }
            public string QualifiedItemId { get; }
            public int Radius { get; }
            public int EscapeX { get; }
            public int EscapeY { get; }
            public int ObjectHits { get; }
            public int MonsterHits { get; }
            public int Score { get; }
        }

        private sealed class MummyBombFinisherCandidate
        {
            public MummyBombFinisherCandidate(JsonElement monster, JsonElement bomb, Candidate placement, int escapeX, int escapeY, int escapeDistance)
            {
                Monster = monster;
                Bomb = bomb;
                Placement = placement;
                EscapeX = escapeX;
                EscapeY = escapeY;
                EscapeDistance = escapeDistance;
            }

            public JsonElement Monster { get; }
            public JsonElement Bomb { get; }
            public Candidate Placement { get; }
            public int Radius => ReadInt(Bomb, "radius_tiles") ?? 0;
            public int EscapeX { get; }
            public int EscapeY { get; }
            public int EscapeDistance { get; }
            public int TotalDistance => Placement.Distance + EscapeDistance;
        }

        private sealed class MonsterDropCatalogInfo
        {
            public MonsterDropCatalogInfo(string[] ids, IReadOnlyDictionary<string, MonsterDropCatalogEntryInfo> selectionEntries)
            {
                Ids = ids;
                SelectionEntries = selectionEntries;
            }

            public string[] Ids { get; }

            public IReadOnlyDictionary<string, MonsterDropCatalogEntryInfo> SelectionEntries { get; }
        }

        private sealed class MonsterDropCatalogEntryInfo
        {
            public MonsterDropCatalogEntryInfo(double conditionalSelectionChance, double conditionalExpectedQuantity)
            {
                ConditionalSelectionChance = conditionalSelectionChance;
                ConditionalExpectedQuantity = conditionalExpectedQuantity;
            }

            public double ConditionalSelectionChance { get; }

            public double ConditionalExpectedQuantity { get; }
        }

        private sealed class TargetProbabilityRule
        {
            public TargetProbabilityRule(double chance, double? expectedQuantity)
            {
                Chance = chance;
                ExpectedQuantity = expectedQuantity;
            }

            public double Chance { get; }

            public double? ExpectedQuantity { get; }
        }

        private sealed class SearchResult
        {
            private readonly (int X, int Y) start;
            private readonly Dictionary<string, string> previous;

            public SearchResult((int X, int Y) start, Dictionary<string, int> distance, Dictionary<string, string> previous)
            {
                this.start = start;
                Distance = distance;
                this.previous = previous;
            }

            public Dictionary<string, int> Distance { get; }

            public (int X, int Y) Start => start;

            public MiningPathTile[] PathTo(int x, int y)
            {
                var path = new List<MiningPathTile>();
                var key = Key(x, y);
                while (true)
                {
                    var split = key.Split(',');
                    path.Add(new MiningPathTile { X = int.Parse(split[0]), Y = int.Parse(split[1]) });
                    if (key == Key(start.X, start.Y))
                    {
                        break;
                    }

                    if (!previous.TryGetValue(key, out key))
                    {
                        return Array.Empty<MiningPathTile>();
                    }
                }

                path.Reverse();
                return path.ToArray();
            }
        }

        private sealed class Candidate
        {
            public Candidate(int targetX, int targetY, int standX, int standY, int distance, int swings, bool deterministicLadder, MiningPathTile[] path)
            {
                TargetX = targetX;
                TargetY = targetY;
                StandX = standX;
                StandY = standY;
                Distance = distance;
                Swings = swings;
                DeterministicLadder = deterministicLadder;
                Path = path;
            }

            public int TargetX { get; }
            public int TargetY { get; }
            public int StandX { get; }
            public int StandY { get; }
            public int Distance { get; }
            public int Swings { get; }
            public bool DeterministicLadder { get; }
            public MiningPathTile[] Path { get; }
        }
    }

    public static class MiningFloorStepCompiler
    {
        public static string ExecutionOptionId(MiningFloorStepPlan plan)
        {
            return plan.StepKind switch
            {
                MiningFloorStepKinds.MineStone => "executor.mine_stone",
                MiningFloorStepKinds.BreakContainer => "executor.break_container",
                MiningFloorStepKinds.CombatMonster => "executor.combat_monster",
                MiningFloorStepKinds.ShootMonster => "executor.shoot_monster",
                MiningFloorStepKinds.PlaceBomb => "executor.place_bomb",
                MiningFloorStepKinds.PickupDebris => "executor.pickup_debris",
                MiningFloorStepKinds.ConsumeFood => "executor.consume_food",
                MiningFloorStepKinds.DescendLadder => "executor.descend_ladder",
                MiningFloorStepKinds.DescendShaft => "executor.descend_shaft",
                MiningFloorStepKinds.ExitMine => "executor.exit_mine",
                _ => string.Empty
            };
        }

        public static SmallModelActionParameter[] BuildExecutionParameters(MiningFloorStepPlan plan)
        {
            var parameters = new List<SmallModelActionParameter>
            {
                Parameter("execution_option_id", ExecutionOptionId(plan)),
                Parameter("mining_step_kind", plan.StepKind),
                Parameter("mining_step_reason", plan.Reason),
                Parameter("estimated_movement_tiles", plan.EstimatedMovementTiles.ToString()),
                Parameter("estimated_tool_swings", plan.EstimatedToolSwings.ToString()),
                Parameter("safety_window_status", plan.SafetyWindowStatus)
            };
            Add(parameters, "target_tile_x", plan.TargetTileX);
            Add(parameters, "target_tile_y", plan.TargetTileY);
            Add(parameters, "stand_tile_x", plan.StandTileX);
            Add(parameters, "stand_tile_y", plan.StandTileY);
            Add(parameters, "max_movement_tiles", plan.EstimatedMovementTiles > 0 ? Math.Max(8, plan.EstimatedMovementTiles + 8) : (int?)null);
            Add(parameters, "max_tool_swings", plan.EstimatedToolSwings > 0 ? Math.Max(1, plan.EstimatedToolSwings + 2) : (int?)null);
            Add(parameters, "debris_index", plan.DebrisIndex);
            Add(parameters, "slot_index", plan.FoodSlotIndex);
            Add(parameters, "restore_slot_index", plan.RestoreSlotIndex);
            Add(parameters, "expected_mine_level_delta", plan.ExpectedMineLevelDelta);
            Add(parameters, "expected_mine_level_after", plan.ExpectedMineLevelAfter);
            Add(parameters, "expected_health_cost", plan.ExpectedHealthCost);
            Add(parameters, "expected_health_after", plan.ExpectedHealthAfter);
            Add(parameters, "expected_target_location", plan.ExpectedTargetLocation);
            Add(parameters, "expected_arrival_tile_x", plan.ExpectedArrivalTileX);
            Add(parameters, "expected_arrival_tile_y", plan.ExpectedArrivalTileY);
            Add(parameters, "qualified_item_id", plan.TargetQualifiedItemId);
            Add(parameters, "expected_drop_qualified_item_ids", string.Join(",", plan.ExpectedDropQualifiedItemIds));
            Add(parameters, "source_match_status", plan.SourceMatchStatus);
            Add(parameters, "target_drop_chance_preview", plan.TargetDropChancePreview);
            Add(parameters, "target_drop_probability_status", plan.TargetDropProbabilityStatus);
            Add(parameters, "target_expected_quantity_per_kill", plan.TargetExpectedQuantityPerKill);
            Add(parameters, "target_drop_efficiency_score", plan.TargetDropEfficiencyScore);
            Add(parameters, "target_runtime_identity", plan.TargetRuntimeIdentity);
            Add(parameters, "target_runtime_type", plan.TargetRuntimeType);
            Add(parameters, "target_name", plan.TargetName);
            Add(parameters, "required_weapon_enchantment_runtime_type", plan.RequiredWeaponEnchantmentRuntimeType);
            Add(parameters, "combat_weapon_slot_index", plan.CombatWeaponSlotIndex);
            Add(parameters, "combat_method", plan.CombatMethod);
            Add(parameters, "combat_terminal_state", plan.CombatTerminalState);
            Add(parameters, "slingshot_slot_index", plan.SlingshotSlotIndex);
            Add(parameters, "slingshot_ammo_qualified_item_id", plan.SlingshotAmmoQualifiedItemId);
            Add(parameters, "bomb_slot_index", plan.BombSlotIndex);
            Add(parameters, "bomb_qualified_item_id", plan.BombQualifiedItemId);
            Add(parameters, "bomb_radius_tiles", plan.BombRadiusTiles);
            Add(parameters, "escape_tile_x", plan.EscapeTileX);
            Add(parameters, "escape_tile_y", plan.EscapeTileY);
            Add(parameters, "expected_bomb_object_hits", plan.ExpectedBombObjectHits);
            Add(parameters, "expected_bomb_monster_hits", plan.ExpectedBombMonsterHits);
            Add(parameters, "expected_combat_attacks", plan.ExpectedCombatAttacks);
            Add(parameters, "expected_combat_duration_ms", plan.ExpectedCombatDurationMs);
            Add(parameters, "estimated_target_cost_ms", plan.EstimatedTargetCostMs);
            Add(parameters, "combat_duration_status", plan.CombatDurationStatus);
            return parameters.ToArray();
        }

        private static void Add(List<SmallModelActionParameter> parameters, string name, int? value)
        {
            if (value.HasValue)
            {
                parameters.Add(Parameter(name, value.Value.ToString()));
            }
        }

        private static void Add(List<SmallModelActionParameter> parameters, string name, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parameters.Add(Parameter(name, value));
            }
        }

        private static void Add(List<SmallModelActionParameter> parameters, string name, double? value)
        {
            if (value.HasValue)
            {
                parameters.Add(Parameter(name, value.Value.ToString("R", CultureInfo.InvariantCulture)));
            }
        }

        private static SmallModelActionParameter Parameter(string name, string value)
        {
            return new SmallModelActionParameter { Name = name, Value = value };
        }
    }
}

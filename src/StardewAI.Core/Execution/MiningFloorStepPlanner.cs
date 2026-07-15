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
        public const string CombatMonster = "combat_monster";
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

            if (objective.Kind == MiningObjectiveKinds.CollectMonsterDrop)
            {
                return SelectMonster(monsters, search, grid, "target_drop_monster_reachable", objective.TargetQualifiedItemIds, monsterDropCatalogs) ??
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

                var threat = SelectImmediateThreat(monsters, search, grid, start, objective.ThreatRadiusTiles);
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

            var mustKillAll = ReadBool(objectives, "must_kill_all_monsters_to_advance");
            if (mustKillAll)
            {
                return SelectMonster(monsters, search, grid, "kill_all_floor_requires_combat") ??
                    Blocked("kill_all_floor_has_no_reachable_monster");
            }

            var stonePlan = SelectStone(objects, search, grid);
            if (stonePlan is not null)
            {
                return stonePlan;
            }

            var combatPlan = SelectMonster(monsters, search, grid, "no_reachable_stone_clear_dynamic_monster");
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
            IReadOnlyDictionary<string, MonsterDropCatalogInfo>? dropCatalogs = null)
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
                        Match = BuildMonsterDropMatch(monster, targets, possible, dropCatalogs)
                    };
                })
                .Where(row => targets is null || row.Match.MatchedIds.Length > 0)
                .Where(row => row.Candidate is not null)
                .OrderBy(row => targets is null || row.Match.IsGuaranteed ? 0 : row.Match.ChanceKnown ? 1 : 2)
                .ThenByDescending(row => row.Match.IsGuaranteed ? 1d : row.Match.Efficiency(row.Candidate!.Distance))
                .ThenByDescending(row => row.Match.ExpectedQuantityPerKill ?? -1d)
                .ThenBy(row => row.Candidate!.Distance)
                .ThenBy(row => row.Candidate!.TargetY)
                .ThenBy(row => row.Candidate!.TargetX)
                .Select(row =>
                {
                    var plan = Build(MiningFloorStepKinds.CombatMonster, reason, row.Candidate!);
                    plan.TargetRuntimeIdentity = ReadString(row.Monster, "runtime_identity");
                    plan.TargetRuntimeType = ReadString(row.Monster, "runtime_type");
                    plan.TargetName = ReadString(row.Monster, "name");
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
                        : row.Match.Efficiency(row.Candidate!.Distance);
                    return plan;
                })
                .FirstOrDefault();
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
                double? catalogSelectionChance = null;
                if (!string.IsNullOrWhiteSpace(catalogKey) &&
                    dropCatalogs is not null &&
                    dropCatalogs.TryGetValue(catalogKey, out var catalog) &&
                    catalog.Ids.Contains(target, StringComparer.OrdinalIgnoreCase))
                {
                    catalogMatch = true;
                    if (catalog.ConditionalSelectionChances.TryGetValue(target, out var parsedSelectionChance))
                    {
                        catalogSelectionChance = parsedSelectionChance;
                    }
                }
                if (!directMatch && !catalogMatch)
                {
                    continue;
                }
                var chance = ReadDouble(rule, "per_identity_chance");
                double? expectedQuantity = ReadDouble(rule, "expected_quantity_per_kill");
                if (!chance.HasValue && catalogMatch && catalogSelectionChance.HasValue)
                {
                    var eventChance = ReadDouble(rule, "effective_per_kill_chance");
                    if (eventChance.HasValue)
                    {
                        chance = eventChance.Value * catalogSelectionChance.Value;
                        expectedQuantity = chance;
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
                var selectionChances = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                var probabilityCompleteness = ReadString(catalog, "selection_probability_completeness");
                if (probabilityCompleteness.StartsWith("complete", StringComparison.Ordinal) &&
                    catalog.TryGetProperty("selection_probability_entries", out var entries) &&
                    entries.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in entries.EnumerateArray())
                    {
                        var id = ReadString(entry, "qualified_item_id");
                        var chance = ReadDouble(entry, "conditional_selection_chance");
                        var status = ReadString(entry, "probability_status");
                        if (!string.IsNullOrWhiteSpace(id) && chance.HasValue && chance.Value >= 0d && chance.Value <= 1d &&
                            (string.Equals(status, "exact_decompiled_weight_with_loaded_furniture_fallback", StringComparison.Ordinal) ||
                             string.Equals(status, "exact_uniform_loaded_catalog", StringComparison.Ordinal) ||
                             string.Equals(status, "exact_decompiled_hard_mine_treasure_tree", StringComparison.Ordinal)))
                        {
                            selectionChances[id] = chance.Value;
                        }
                    }
                }
                var idSet = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
                var probabilityMass = selectionChances.Values.Sum();
                if (selectionChances.Count != idSet.Count ||
                    selectionChances.Keys.Any(id => !idSet.Contains(id)) ||
                    Math.Abs(probabilityMass - 1d) > 0.000000001d)
                {
                    selectionChances.Clear();
                }
                result[key] = new MonsterDropCatalogInfo(ids, selectionChances);
            }
            return result;
        }

        private static MiningFloorStepPlan? SelectImmediateThreat(JsonElement monsters, SearchResult search, bool[,] grid, (int X, int Y) start, int radiusTiles)
        {
            return monsters.EnumerateArray()
                .Where(monster =>
                {
                    var x = ReadInt(monster, "tile_x");
                    var y = ReadInt(monster, "tile_y");
                    return x.HasValue && y.HasValue && Math.Abs(x.Value - start.X) + Math.Abs(y.Value - start.Y) <= Math.Max(1, radiusTiles);
                })
                .Select(monster => new { Monster = monster, Candidate = TargetCandidate(monster, search, grid, 0, false) })
                .Where(row => row.Candidate is not null)
                .OrderBy(row => row.Candidate!.Distance)
                .Select(row =>
                {
                    var plan = Build(MiningFloorStepKinds.CombatMonster, "immediate_monster_threat", row.Candidate!);
                    plan.TargetRuntimeIdentity = ReadString(row.Monster, "runtime_identity");
                    plan.TargetRuntimeType = ReadString(row.Monster, "runtime_type");
                    plan.TargetName = ReadString(row.Monster, "name");
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
                    var plan = Build(MiningFloorStepKinds.MineStone, "target_resource_or_artifact_source_reachable", row!.Candidate!);
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

        private static MiningFloorStepPlan? SelectDebris(JsonElement debris, SearchResult search, bool[,] grid, string[] targetIds, int? restoreSlot)
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
                    if (candidate is null || best is not null && candidate.Distance >= best.EstimatedMovementTiles)
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

            Candidate? best = null;
            foreach (var direction in Directions)
            {
                var standX = targetX.Value + direction.X;
                var standY = targetY.Value + direction.Y;
                if (!InBounds(grid, standX, standY) || grid[standX, standY] || !search.Distance.TryGetValue(Key(standX, standY), out var distance))
                {
                    continue;
                }

                var candidate = new Candidate(targetX.Value, targetY.Value, standX, standY, distance, estimatedSwings, deterministicLadder, search.PathTo(standX, standY));
                if (best is null || candidate.Distance < best.Distance ||
                    candidate.Distance == best.Distance && (candidate.StandY < best.StandY || candidate.StandY == best.StandY && candidate.StandX < best.StandX))
                {
                    best = candidate;
                }
            }

            return best;
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

            public double Efficiency(int distance)
            {
                return Chance.HasValue ? Chance.Value / (Math.Max(0, distance) + 1d) : -1d;
            }
        }

        private sealed class MonsterDropCatalogInfo
        {
            public MonsterDropCatalogInfo(string[] ids, IReadOnlyDictionary<string, double> conditionalSelectionChances)
            {
                Ids = ids;
                ConditionalSelectionChances = conditionalSelectionChances;
            }

            public string[] Ids { get; }

            public IReadOnlyDictionary<string, double> ConditionalSelectionChances { get; }
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
                MiningFloorStepKinds.CombatMonster => "executor.combat_monster",
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Execution
{
    public sealed partial class MiningFloorStepPlanner
    {
        private static MiningFloorStepPlan? SelectMonster(
            JsonElement monsters,
            SearchResult search,
            bool[,] grid,
            string reason,
            string[]? targetDropIds = null,
            IReadOnlyDictionary<string, MonsterDropCatalogInfo>? dropCatalogs = null,
            double? movementTileDurationMs = null,
            bool bombFinisherAvailable = false,
            string[]? targetMonsterNameFragments = null,
            bool matchAnySlimeName = false,
            string combatIntent = TrainingCombatIntents.TargetDefeat)
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
                .Where(row => targetMonsterNameFragments is not { Length: > 0 } ||
                    QuestMonsterTargetRules.Matches(
                        ReadString(row.Monster, "name"),
                        targetMonsterNameFragments,
                        matchAnySlimeName))
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
                    plan.CombatIntent = combatIntent;
                    if (string.Equals(plan.CombatTerminalState, "defeat", StringComparison.Ordinal))
                    {
                        plan.SkillExperienceSkillId = "combat";
                        plan.ExpectedSkillExperience = ReadInt(row.Monster, "combat_experience_on_defeat");
                        plan.SkillExperienceMinimum = plan.ExpectedSkillExperience;
                        plan.SkillExperienceMaximum = plan.ExpectedSkillExperience;
                        plan.SkillExperienceCondition = ReadString(row.Monster, "combat_experience_condition");
                        plan.SkillExperienceProjectionStatus = "exact_for_native_monster_defeat";
                    }
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
                .OrderBy(CombatExposureWindows)
                .ThenBy(projection => projection.SelectionCostMs)
                .ThenBy(projection => projection.Method == "melee" ? 0 : 1)
                .ThenBy(projection => projection.SlotIndex)
                .FirstOrDefault();
        }

        private static int CombatExposureWindows(
            MonsterCombatProjectionInfo projection)
        {
            return Math.Max(
                1,
                (int)Math.Ceiling(
                    (projection.ExpectedAttacks ?? double.MaxValue) / 4d));
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
                    plan.CombatIntent =
                        TrainingCombatIntents.TransitSelfDefense;
                    plan.RequiredWeaponEnchantmentRuntimeType = ReadRequiredWeaponEnchantment(row.Monster);
                    plan.CombatWeaponSlotIndex = row.Combat?.Method == "melee" ? row.Combat.SlotIndex : null;
                    plan.ExpectedCombatAttacks = row.Combat?.ExpectedAttacks;
                    plan.ExpectedCombatDurationMs = row.Combat?.DurationMs;
                    plan.EstimatedTargetCostMs =
                        row.Combat is not null &&
                        movementTileDurationMs.HasValue
                            ? row.Combat.DurationMs +
                                row.Candidate!.Distance *
                                movementTileDurationMs.Value
                            : null;
                    plan.CombatDurationStatus = row.Combat is null
                        ? "unavailable_no_complete_active_melee_projection"
                        : movementTileDurationMs.HasValue
                            ? "exact_active_melee_plus_unobstructed_bfs_movement"
                            : "exact_active_melee_only";
                    if (string.Equals(
                            plan.CombatTerminalState,
                            "defeat",
                            StringComparison.Ordinal))
                    {
                        plan.SkillExperienceSkillId = "combat";
                        plan.ExpectedSkillExperience = ReadInt(
                            row.Monster,
                            "combat_experience_on_defeat");
                        plan.SkillExperienceMinimum =
                            plan.ExpectedSkillExperience;
                        plan.SkillExperienceMaximum =
                            plan.ExpectedSkillExperience;
                        plan.SkillExperienceCondition = ReadString(
                            row.Monster,
                            "combat_experience_condition");
                        plan.SkillExperienceProjectionStatus =
                            "exact_for_native_monster_defeat";
                    }
                    return plan;
                })
                .FirstOrDefault();
        }

    }
}

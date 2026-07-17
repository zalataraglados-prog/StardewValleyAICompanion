using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Plans;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Goals;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using StardewAI.Core.Verifier;
using StardewAI.Core.WorldModel;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static string[] ValidateCatchFishPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.catch_fish")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var locationId = ReadParameter(action, "location_id") ?? ReadParameter(action, "target_location") ?? string.Empty;
            var standX = ReadIntParameter(action, "stand_tile_x");
            var standY = ReadIntParameter(action, "stand_tile_y");
            var bobberX = ReadIntParameter(action, "bobber_tile_x");
            var bobberY = ReadIntParameter(action, "bobber_tile_y");
            var rodSlot = ReadIntParameter(action, "rod_slot_index");
            var ruleKey = ReadParameter(action, "rule_key") ?? string.Empty;
            var expectedQualifiedItemId = ReadParameter(action, "expected_qualified_item_id") ?? string.Empty;
            var outcomeDistributionJson = ReadParameter(action, "outcome_distribution_json") ?? string.Empty;
            var distributionComplete = bool.TryParse(ReadParameter(action, "outcome_distribution_complete"), out var parsedDistributionComplete) && parsedDistributionComplete;
            var castDirection = ReadIntParameter(action, "cast_direction");
            var targetCastingPower = ReadDoubleParameter(action, "target_casting_power");
            var maxCastRequested = bool.TryParse(ReadParameter(action, "max_cast_requested"), out var parsedMaxCastRequested) && parsedMaxCastRequested;

            if (string.IsNullOrWhiteSpace(locationId)) reasons.Add("fishing_location_id_required");
            if (!standX.HasValue || !standY.HasValue) reasons.Add("fishing_stand_tile_required");
            if (!bobberX.HasValue || !bobberY.HasValue) reasons.Add("fishing_bobber_tile_required");
            if (!rodSlot.HasValue) reasons.Add("fishing_rod_slot_required");
            if (string.IsNullOrWhiteSpace(ruleKey)) reasons.Add("fishing_rule_key_required");
            if (!ruleKey.StartsWith("distribution:", StringComparison.Ordinal)) reasons.Add("fishing_distribution_key_required");
            if (!distributionComplete) reasons.Add("fishing_outcome_distribution_incomplete");
            if (string.IsNullOrWhiteSpace(outcomeDistributionJson)) reasons.Add("fishing_outcome_distribution_required");
            if (!string.IsNullOrWhiteSpace(expectedQualifiedItemId)) reasons.Add("fishing_expected_item_must_be_unconstrained");
            if (reasons.Count > 0)
            {
                return reasons.ToArray();
            }

            if (!string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), locationId, StringComparison.Ordinal))
            {
                reasons.Add("fishing_location_mismatch");
            }
            if ((ReadStateFieldDoubleOptional(snapshot, "player", "energy") ?? 0) <= 1)
            {
                reasons.Add("fishing_energy_too_low");
            }
            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("active_menu_blocks_fishing");
            }

            var activeCast = ReadStateFieldValue(snapshot, "fishing", "active_cast_state");
            if (activeCast.HasValue && ReadBool(activeCast.Value, "in_use"))
            {
                reasons.Add("fishing_rod_already_in_use");
            }

            var dx = bobberX!.Value - standX!.Value;
            var dy = bobberY!.Value - standY!.Value;
            if (dx != 0 && dy != 0 || dx == 0 && dy == 0)
            {
                reasons.Add("fishing_cast_not_cardinal");
            }
            else
            {
                var direction = dx > 0 ? 1 : dx < 0 ? 3 : dy > 0 ? 2 : 0;
                var distance = Math.Abs(dx) + Math.Abs(dy);
                var fishingLevel = ReadFishingLevel(snapshot);
                var addedDistance = fishingLevel >= 15 ? 4 : fishingLevel >= 8 ? 3 : fishingLevel >= 4 ? 2 : fishingLevel >= 1 ? 1 : 0;
                var maxDistance = direction is 1 or 3 ? addedDistance + 4 : addedDistance + 3;
                var computedPower = Math.Clamp(distance / (double)maxDistance, 0d, 1d);
                if (distance < 2 || distance > maxDistance)
                {
                    reasons.Add("fishing_cast_out_of_range");
                }
                if (castDirection.HasValue && castDirection.Value != direction)
                {
                    reasons.Add("fishing_cast_direction_mismatch");
                }
                if (targetCastingPower.HasValue && Math.Abs(targetCastingPower.Value - computedPower) > 0.001d)
                {
                    reasons.Add("fishing_target_casting_power_mismatch");
                }
                if (maxCastRequested != (distance == maxDistance))
                {
                    reasons.Add("fishing_max_cast_requested_mismatch");
                }
            }

            if (FishingCollisionGridBlocks(snapshot, standX.Value, standY.Value))
            {
                reasons.Add("fishing_stand_tile_collision_blocked");
            }

            var rods = ReadStateFieldValue(snapshot, "fishing", "rod_inventory");
            var rodExists = rods.HasValue && rods.Value.ValueKind == JsonValueKind.Array && rods.Value.EnumerateArray().Any(rod =>
                ReadInt(rod, "slot_index") == rodSlot!.Value &&
                !string.IsNullOrWhiteSpace(ReadString(rod, "qualified_item_id")));
            if (!rodExists)
            {
                reasons.Add("fishing_rod_slot_not_found");
            }

            var fishableTiles = ReadStateFieldValue(snapshot, "fishing", "fishable_tiles");
            var bobberTileIndex = -1;
            var bobberWaterDepth = 0;
            if (fishableTiles.HasValue && fishableTiles.Value.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var tile in fishableTiles.Value.EnumerateArray())
                {
                    if (ReadInt(tile, "tile_x") == bobberX.Value && ReadInt(tile, "tile_y") == bobberY.Value)
                    {
                        bobberTileIndex = index;
                        bobberWaterDepth = ReadInt(tile, "water_depth");
                        break;
                    }
                    index++;
                }
            }
            if (bobberTileIndex < 0)
            {
                reasons.Add("fishing_bobber_tile_not_fishable");
            }

            var transparentContextAllows = distributionComplete && !string.IsNullOrWhiteSpace(outcomeDistributionJson) &&
                FishingDistributionContextAllows(
                    snapshot,
                    locationId,
                    rodSlot.GetValueOrDefault(),
                    standX.GetValueOrDefault(),
                    standY.GetValueOrDefault(),
                    bobberX.GetValueOrDefault(),
                    bobberY.GetValueOrDefault(),
                    ruleKey,
                    outcomeDistributionJson);
            if (!transparentContextAllows)
            {
                reasons.Add("fishing_rule_context_no_longer_allows_candidate");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidateMiningReachDepthPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "mining.reach_depth")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>(MiningReachDepthCandidateBuilder.MissingMiningGroups(snapshot));
            var targetDepth = ReadIntParameter(action, "target_depth");
            var currentMine = ReadStateFieldValue(snapshot, "mining", "current_mine");
            var currentDepth = currentMine.HasValue ? ReadInt(currentMine.Value, "mine_level") : 0;
            var currentFamily = currentMine.HasValue ? ReadString(currentMine.Value, "mine_kind") : string.Empty;
            reasons.AddRange(MiningReachDepthCandidateBuilder.ValidateTarget(currentDepth, currentFamily, targetDepth, ReadParameter(action, "target_location_family")));

            var floorStep = new MiningFloorStepPlanner().Plan(snapshot, new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.ReachDepth,
                MinimumReserveHealth = ReadIntParameter(action, "minimum_reserve_health") ?? 0,
                MinimumReserveEnergy = ReadIntParameter(action, "minimum_reserve_energy"),
                LatestExitTime = ReadIntParameter(action, "latest_exit_time"),
                TargetDepth = targetDepth
            });
            var executionOptionId = MiningFloorStepCompiler.ExecutionOptionId(floorStep);
            if (!string.Equals(floorStep.Status, "ready", StringComparison.Ordinal))
            {
                reasons.Add(floorStep.Reason);
            }
            else if (string.IsNullOrWhiteSpace(executionOptionId))
            {
                reasons.Add(floorStep.StepKind == MiningFloorStepKinds.DescendLadder
                    ? "mining_descend_ladder_executor_not_implemented"
                    : "mining_floor_step_executor_not_implemented:" + floorStep.StepKind);
            }
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidateMiningGoldenScythePlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "mining.acquire_golden_scythe")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>(MiningReachDepthCandidateBuilder.MissingMiningGroups(snapshot));
            var currentMine = ReadStateFieldValue(snapshot, "mining", "current_mine");
            if (currentMine.HasValue)
            {
                reasons.AddRange(MiningGoldenScytheCandidateBuilder.ValidateCurrentMine(currentMine.Value));
            }

            var floorStep = new MiningFloorStepPlanner().Plan(snapshot, MiningGoldenScytheCandidateBuilder.Objective(action.Parameters));
            var executionOptionId = MiningFloorStepCompiler.ExecutionOptionId(floorStep);
            if (!string.Equals(floorStep.Status, "ready", StringComparison.Ordinal))
            {
                reasons.Add(floorStep.Reason);
            }
            else if (string.IsNullOrWhiteSpace(executionOptionId))
            {
                reasons.Add("golden_scythe_floor_step_executor_not_implemented:" + floorStep.StepKind);
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidateMiningSkullKeyPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "mining.obtain_skull_key")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>(MiningReachDepthCandidateBuilder.MissingMiningGroups(snapshot));
            var currentMine = ReadStateFieldValue(snapshot, "mining", "current_mine");
            if (currentMine.HasValue)
            {
                reasons.AddRange(MiningSkullKeyCandidateBuilder.ValidateCurrentMine(currentMine.Value));
            }

            var floorStep = new MiningFloorStepPlanner().Plan(snapshot, MiningSkullKeyCandidateBuilder.Objective(action.Parameters));
            var executionOptionId = MiningFloorStepCompiler.ExecutionOptionId(floorStep);
            if (!string.Equals(floorStep.Status, "ready", StringComparison.Ordinal))
            {
                reasons.Add(floorStep.Reason);
            }
            else if (string.IsNullOrWhiteSpace(executionOptionId))
            {
                reasons.Add("skull_key_floor_step_executor_not_implemented:" + floorStep.StepKind);
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidateVolcanoReachCalderaPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "volcano.reach_caldera")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>(VolcanoReachCalderaCandidateBuilder.MissingVolcanoGroups(snapshot));
            var floorStep = new VolcanoFloorStepPlanner().Plan(snapshot);
            var executionOptionId = VolcanoFloorStepCompiler.ExecutionOptionId(floorStep);
            if (!string.Equals(floorStep.Status, "ready", StringComparison.Ordinal))
            {
                reasons.Add(floorStep.Reason);
            }
            else if (string.IsNullOrWhiteSpace(executionOptionId))
            {
                reasons.Add("volcano_floor_step_executor_not_implemented:" + floorStep.StepKind);
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidateCoolVolcanoLavaPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.cool_volcano_lava")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var standX = ReadIntParameter(action, "stand_tile_x");
            var standY = ReadIntParameter(action, "stand_tile_y");
            var wateringCanSlot = ReadIntParameter(action, "watering_can_slot_index");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                reasons.Add("volcano_cooling_target_tile_required");
            }
            if (!standX.HasValue || !standY.HasValue)
            {
                reasons.Add("volcano_cooling_stand_tile_required");
            }
            else if (targetX.HasValue && targetY.HasValue &&
                Math.Abs(targetX.Value - standX.Value) + Math.Abs(targetY.Value - standY.Value) != 1)
            {
                reasons.Add("volcano_cooling_stand_tile_not_adjacent");
            }
            if (!wateringCanSlot.HasValue)
            {
                reasons.Add("volcano_cooling_watering_can_slot_required");
            }

            var currentLevel = ReadStateFieldValue(snapshot, "volcano", "current_level");
            if (!currentLevel.HasValue || currentLevel.Value.ValueKind != JsonValueKind.Object)
            {
                reasons.Add("volcano_current_level_unavailable");
            }
            else if (ReadInt(currentLevel.Value, "level") == 5 || !ReadBool(currentLevel.Value, "lava_cooling_enabled"))
            {
                reasons.Add("volcano_level_five_cooling_disabled");
            }

            var tiles = ReadStateFieldValue(snapshot, "volcano", "tiles");
            if (targetX.HasValue && targetY.HasValue &&
                (!tiles.HasValue || tiles.Value.ValueKind != JsonValueKind.Object ||
                 !tiles.Value.TryGetProperty("coolable_uncooled_tiles", out var coolable) ||
                 coolable.ValueKind != JsonValueKind.Array ||
                 !coolable.EnumerateArray().Any(tile =>
                     ReadInt(tile, "tile_x") == targetX.Value && ReadInt(tile, "tile_y") == targetY.Value)))
            {
                reasons.Add("volcano_cooling_target_not_currently_coolable");
            }

            var resources = ReadStateFieldValue(snapshot, "volcano", "player_resources");
            if (wateringCanSlot.HasValue &&
                (!resources.HasValue || resources.Value.ValueKind != JsonValueKind.Object ||
                 !resources.Value.TryGetProperty("watering_can_slots", out var wateringCans) ||
                 wateringCans.ValueKind != JsonValueKind.Array ||
                 !wateringCans.EnumerateArray().Any(slot =>
                     ReadInt(slot, "slot_index") == wateringCanSlot.Value && ReadBool(slot, "can_cool_lava_now"))))
            {
                reasons.Add("volcano_cooling_watering_can_unavailable_or_empty");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidateVolcanoNativePrimitivePlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var isStone = action.OptionId == "executor.break_volcano_stone";
            var isContainer = action.OptionId == "executor.break_volcano_container";
            var isCombat = action.OptionId == "executor.combat_volcano_monster";
            if (!isStone && !isContainer && !isCombat)
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var standX = ReadIntParameter(action, "stand_tile_x");
            var standY = ReadIntParameter(action, "stand_tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                reasons.Add("volcano_primitive_target_tile_required");
            }
            if (!standX.HasValue || !standY.HasValue)
            {
                reasons.Add("volcano_primitive_stand_tile_required");
            }
            else if (targetX.HasValue && targetY.HasValue &&
                Math.Abs(targetX.Value - standX.Value) + Math.Abs(targetY.Value - standY.Value) != 1)
            {
                reasons.Add("volcano_primitive_stand_tile_not_adjacent");
            }

            var currentLevel = ReadStateFieldValue(snapshot, "volcano", "current_level");
            if (!currentLevel.HasValue || currentLevel.Value.ValueKind != JsonValueKind.Object)
            {
                reasons.Add("volcano_current_level_unavailable");
            }

            var resources = ReadStateFieldValue(snapshot, "volcano", "player_resources");
            if (isStone || isContainer)
            {
                var toolSlot = ReadIntParameter(action, "tool_slot_index");
                if (!toolSlot.HasValue)
                {
                    reasons.Add("volcano_primitive_tool_slot_required");
                }
                else
                {
                    var slotsProperty = isStone ? "pickaxe_slots" : "heavy_hitter_slots";
                    if (!resources.HasValue || resources.Value.ValueKind != JsonValueKind.Object ||
                        !resources.Value.TryGetProperty(slotsProperty, out var slots) ||
                        slots.ValueKind != JsonValueKind.Array ||
                        !slots.EnumerateArray().Any(slot => ReadInt(slot, "slot_index") == toolSlot.Value))
                    {
                        reasons.Add(isStone ? "volcano_break_stone_pickaxe_unavailable" : "volcano_break_container_heavy_hitter_unavailable");
                    }
                }

                var objects = ReadStateFieldValue(snapshot, "volcano", "objects");
                if (targetX.HasValue && targetY.HasValue &&
                    (!objects.HasValue || objects.Value.ValueKind != JsonValueKind.Array ||
                     !objects.Value.EnumerateArray().Any(item =>
                         ReadInt(item, "tile_x") == targetX.Value &&
                         ReadInt(item, "tile_y") == targetY.Value &&
                         ReadBool(item, isStone ? "is_breakable_stone" : "is_breakable_container") &&
                         (string.IsNullOrWhiteSpace(ReadParameter(action, "qualified_item_id")) ||
                          string.Equals(ReadString(item, "qualified_item_id"), ReadParameter(action, "qualified_item_id"), StringComparison.Ordinal)))))
                {
                    reasons.Add(isStone ? "volcano_break_stone_target_not_live" : "volcano_break_container_target_not_live");
                }
            }

            if (isCombat)
            {
                var runtimeIdentity = ReadParameter(action, "target_runtime_identity");
                var runtimeType = ReadParameter(action, "target_runtime_type");
                var targetName = ReadParameter(action, "target_name");
                var weaponSlot = ReadIntParameter(action, "combat_weapon_slot_index");
                if (string.IsNullOrWhiteSpace(runtimeIdentity) || string.IsNullOrWhiteSpace(runtimeType) || string.IsNullOrWhiteSpace(targetName))
                {
                    reasons.Add("volcano_combat_target_identity_required");
                }
                if (!weaponSlot.HasValue)
                {
                    reasons.Add("volcano_combat_weapon_slot_required");
                }
                else if (!resources.HasValue || resources.Value.ValueKind != JsonValueKind.Object ||
                    !resources.Value.TryGetProperty("weapon_slots", out var weapons) ||
                    weapons.ValueKind != JsonValueKind.Array ||
                    !weapons.EnumerateArray().Any(slot =>
                        ReadInt(slot, "slot_index") == weaponSlot.Value && !ReadBool(slot, "is_scythe")))
                {
                    reasons.Add("volcano_combat_melee_weapon_unavailable");
                }

                var monsters = ReadStateFieldValue(snapshot, "volcano", "monsters");
                if (!string.IsNullOrWhiteSpace(runtimeIdentity) &&
                    (!monsters.HasValue || monsters.Value.ValueKind != JsonValueKind.Array ||
                     !monsters.Value.EnumerateArray().Any(monster =>
                         string.Equals(ReadString(monster, "runtime_identity"), runtimeIdentity, StringComparison.Ordinal) &&
                         string.Equals(ReadString(monster, "runtime_type"), runtimeType, StringComparison.Ordinal) &&
                         string.Equals(ReadString(monster, "name"), targetName, StringComparison.Ordinal) &&
                         ReadBool(monster, "melee_executor_supported"))))
                {
                    reasons.Add("volcano_combat_target_not_live_or_supported");
                }
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static bool FishingDistributionContextAllows(
            SnapshotEnvelope snapshot,
            string locationId,
            int rodSlot,
            int standX,
            int standY,
            int bobberX,
            int bobberY,
            string distributionKey,
            string outcomeDistributionJson)
        {
            return FishingEventCandidateBuilder.Build(snapshot).Any(candidate =>
                candidate.Available &&
                candidate.Kind == "catch_fish" &&
                string.Equals(candidate.LocationId, locationId, StringComparison.Ordinal) &&
                candidate.SlotIndex == rodSlot &&
                candidate.TileX == standX &&
                candidate.TileY == standY &&
                FishingCandidateParameterInt(candidate, "bobber_tile_x") == bobberX &&
                FishingCandidateParameterInt(candidate, "bobber_tile_y") == bobberY &&
                string.Equals(FishingCandidateParameter(candidate, "rule_key"), distributionKey, StringComparison.Ordinal) &&
                string.Equals(FishingCandidateParameter(candidate, "outcome_distribution_json"), outcomeDistributionJson, StringComparison.Ordinal));
        }

        private static string FishingCandidateParameter(EventCandidate candidate, string name)
        {
            return candidate.Parameters.FirstOrDefault(parameter => parameter.Name == name)?.Value ?? string.Empty;
        }

        private static int? FishingCandidateParameterInt(EventCandidate candidate, string name)
        {
            return int.TryParse(FishingCandidateParameter(candidate, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
        }

        private static bool FishingRuleContextAllows(
            SnapshotEnvelope snapshot,
            int rodSlot,
            string ruleKey,
            string expectedQualifiedItemId,
            int bobberTileIndex,
            int standX,
            int standY)
        {
            if (bobberTileIndex < 0 || !FishingBaseCatchPathAllows(snapshot, rodSlot, bobberTileIndex))
            {
                return false;
            }
            var contexts = ReadStateFieldValue(snapshot, "fishing", "rod_contexts");
            if (!contexts.HasValue || contexts.Value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var context in contexts.Value.EnumerateArray())
            {
                if (ReadInt(context, "rod_slot_index") != rodSlot || !ReadBool(context, "complete") ||
                    !ReadBool(context, "special_catch_sources_complete") ||
                    !context.TryGetProperty("spawn_rules", out var spawnRules) ||
                    spawnRules.ValueKind != JsonValueKind.Object ||
                    !ReadBool(spawnRules, "item_query_resolution_complete") ||
                    !spawnRules.TryGetProperty("rules", out var rules) || rules.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var rule in rules.EnumerateArray())
                {
                    if (!string.Equals(ReadString(rule, "rule_key"), ruleKey, StringComparison.Ordinal) ||
                        !ReadBool(rule, "condition_met") ||
                        FishingRuleHasFixedBlock(rule) ||
                        !FishingPlayerRectangleAllows(rule, standX, standY) ||
                        !JsonIntArrayContains(rule, "eligible_fishable_tile_indices", bobberTileIndex) ||
                        !rule.TryGetProperty("outputs", out var outputs) || outputs.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    return outputs.EnumerateArray().Any(output =>
                        ReadBool(output, "resolution_complete") &&
                        ReadBool(output, "output_eligible_before_random_rolls") &&
                        (string.IsNullOrWhiteSpace(expectedQualifiedItemId) ||
                         string.Equals(ReadString(output, "qualified_item_id"), expectedQualifiedItemId, StringComparison.Ordinal)));
                }
            }
            return false;
        }

        private static bool FishingSpecialContextAllows(
            SnapshotEnvelope snapshot,
            int rodSlot,
            string specialSource,
            string expectedQualifiedItemId,
            int bobberTileIndex,
            int bobberWaterDepth)
        {
            if (bobberTileIndex < 0)
            {
                return false;
            }
            var contexts = ReadStateFieldValue(snapshot, "fishing", "rod_contexts");
            if (!contexts.HasValue || contexts.Value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var context in contexts.Value.EnumerateArray())
            {
                if (ReadInt(context, "rod_slot_index") != rodSlot || !ReadBool(context, "complete") ||
                    !ReadBool(context, "special_catch_sources_complete") ||
                    !context.TryGetProperty("special_catch_sources", out var sources) || sources.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (specialSource == "fish_frenzy" &&
                    sources.TryGetProperty("fish_frenzy", out var frenzy) &&
                    ReadBool(frenzy, "active") &&
                    string.Equals(ReadString(frenzy, "qualified_item_id"), expectedQualifiedItemId, StringComparison.Ordinal) &&
                    JsonIntArrayContains(frenzy, "eligible_fishable_tile_indices", bobberTileIndex))
                {
                    return true;
                }

                if (specialSource == "base_fallback" &&
                    FishingBaseCatchPathAllows(snapshot, rodSlot, bobberTileIndex) &&
                    sources.TryGetProperty("fallbacks", out var fallbacks) &&
                    context.TryGetProperty("spawn_rules", out var spawnRules) &&
                    spawnRules.TryGetProperty("evaluation_context", out var evaluationContext))
                {
                    var tutorial = ReadBool(evaluationContext, "is_tutorial_catch");
                    var fallbackQualifiedItemId = tutorial
                        ? ReadString(fallbacks, "tutorial_location_data_fallback_qualified_item_id")
                        : ReadString(fallbacks, "no_location_data_match_qualified_item_id");
                    if (string.Equals(fallbackQualifiedItemId, expectedQualifiedItemId, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                if (specialSource.StartsWith("fish_pond:", StringComparison.Ordinal) &&
                    sources.TryGetProperty("fish_ponds", out var ponds) && ponds.ValueKind == JsonValueKind.Array &&
                    ponds.EnumerateArray().Any(pond =>
                        ReadBool(pond, "catch_available") &&
                        string.Equals("fish_pond:" + ReadInt(pond, "tile_x") + "," + ReadInt(pond, "tile_y"), specialSource, StringComparison.Ordinal) &&
                        string.Equals(ReadString(pond, "fish_qualified_item_id"), expectedQualifiedItemId, StringComparison.Ordinal) &&
                        JsonIntArrayContains(pond, "fishable_tile_indices", bobberTileIndex)))
                {
                    return true;
                }

                if (sources.TryGetProperty("location_get_fish_override", out var locationOverride) &&
                    locationOverride.ValueKind == JsonValueKind.Object &&
                    locationOverride.TryGetProperty("handlers", out var handlers) && handlers.ValueKind == JsonValueKind.Array)
                {
                    foreach (var handler in handlers.EnumerateArray())
                    {
                        if (specialSource == "mine_shaft_fishing" &&
                            ReadString(handler, "handler") == "mine_shaft_fishing" &&
                            MineShaftFishingOutputAllows(handler, expectedQualifiedItemId, bobberWaterDepth))
                        {
                            return true;
                        }
                        if (!string.Equals(ReadString(handler, "handler"), specialSource, StringComparison.Ordinal) ||
                            !ReadBool(handler, "eligible_before_catch") ||
                            !string.Equals(ReadString(handler, "qualified_item_id"), expectedQualifiedItemId, StringComparison.Ordinal))
                        {
                            continue;
                        }
                        if (!handler.TryGetProperty("fishable_tile_indices", out var indices) ||
                            indices.ValueKind != JsonValueKind.Array || indices.GetArrayLength() == 0 ||
                            JsonIntArrayContains(handler, "fishable_tile_indices", bobberTileIndex))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static bool FishingBaseCatchPathAllows(SnapshotEnvelope snapshot, int rodSlot, int bobberTileIndex)
        {
            if (bobberTileIndex < 0)
            {
                return false;
            }
            var contexts = ReadStateFieldValue(snapshot, "fishing", "rod_contexts");
            if (!contexts.HasValue || contexts.Value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var context in contexts.Value.EnumerateArray())
            {
                if (ReadInt(context, "rod_slot_index") != rodSlot || !ReadBool(context, "complete") ||
                    !ReadBool(context, "special_catch_sources_complete") ||
                    !context.TryGetProperty("special_catch_sources", out var sources))
                {
                    continue;
                }

                if (sources.TryGetProperty("location_get_fish_override", out var locationOverride) &&
                    locationOverride.TryGetProperty("handlers", out var handlers) && handlers.ValueKind == JsonValueKind.Array)
                {
                    foreach (var handler in handlers.EnumerateArray())
                    {
                        var name = ReadString(handler, "handler");
                        if (name == "mine_shaft_fishing")
                        {
                            if (ReadBool(handler, "uses_training_rod") || ReadInt(handler, "mine_area") == 80)
                            {
                                return false;
                            }
                            continue;
                        }
                        if (name == "island_southeast_stardrop_pool_walnut" &&
                            JsonIntArrayContains(handler, "fishable_tile_indices", bobberTileIndex) &&
                            (ReadBool(handler, "eligible_before_catch") || ReadBool(handler, "matched_pool_without_reward_returns_null")))
                        {
                            return false;
                        }
                        if (ReadBool(handler, "eligible_before_catch") &&
                            (!handler.TryGetProperty("fishable_tile_indices", out var indices) ||
                             indices.ValueKind != JsonValueKind.Array || indices.GetArrayLength() == 0 ||
                             JsonIntArrayContains(handler, "fishable_tile_indices", bobberTileIndex)))
                        {
                            return false;
                        }
                    }
                }

                if (sources.TryGetProperty("fish_ponds", out var ponds) && ponds.ValueKind == JsonValueKind.Array &&
                    ponds.EnumerateArray().Any(pond => ReadBool(pond, "catch_available") &&
                        JsonIntArrayContains(pond, "fishable_tile_indices", bobberTileIndex)))
                {
                    return false;
                }
                if (sources.TryGetProperty("fish_frenzy", out var frenzy) && ReadBool(frenzy, "active") &&
                    JsonIntArrayContains(frenzy, "eligible_fishable_tile_indices", bobberTileIndex))
                {
                    return false;
                }
                return true;
            }
            return false;
        }

        private static bool MineShaftFishingOutputAllows(JsonElement handler, string expectedQualifiedItemId, int waterDepth)
        {
            var usesTrainingRod = ReadBool(handler, "uses_training_rod");
            var mineArea = ReadInt(handler, "mine_area");
            var specialChance = MineShaftSpecialChanceAtDepth(handler, waterDepth);
            if (!usesTrainingRod &&
                string.Equals(ReadString(handler, "special_fish_qualified_item_id"), expectedQualifiedItemId, StringComparison.Ordinal) &&
                specialChance > 0d)
            {
                return true;
            }
            if (!usesTrainingRod && mineArea == 80 && expectedQualifiedItemId == "(O)CaveJelly" &&
                specialChance < 1d && ReadDouble(handler, "lava_area_cave_jelly_chance") > 0d)
            {
                return true;
            }
            if ((usesTrainingRod || mineArea == 80) &&
                (usesTrainingRod || specialChance < 1d) &&
                (usesTrainingRod || ReadDouble(handler, "lava_area_cave_jelly_chance") < 1d) &&
                expectedQualifiedItemId.StartsWith("(O)", StringComparison.Ordinal) &&
                int.TryParse(expectedQualifiedItemId.Substring(3), NumberStyles.Integer, CultureInfo.InvariantCulture, out var itemId) &&
                handler.TryGetProperty("mine_trash_item_id_range_inclusive", out var range) && range.ValueKind == JsonValueKind.Array)
            {
                var values = range.EnumerateArray().Where(value => value.TryGetInt32(out _)).Select(value => value.GetInt32()).ToArray();
                return values.Length == 2 && itemId >= values[0] && itemId <= values[1];
            }
            return false;
        }

        private static double MineShaftSpecialChanceAtDepth(JsonElement handler, int waterDepth)
        {
            if (!handler.TryGetProperty("special_fish_chance_by_water_depth", out var chances) || chances.ValueKind != JsonValueKind.Array)
            {
                return 0d;
            }
            foreach (var chance in chances.EnumerateArray())
            {
                if (ReadInt(chance, "water_depth") == waterDepth)
                {
                    return Math.Clamp(ReadDouble(chance, "special_fish_chance"), 0d, 1d);
                }
            }
            return 0d;
        }

        private static bool FishingRuleHasFixedBlock(JsonElement rule)
        {
            return rule.TryGetProperty("blocking_reasons", out var reasons) && reasons.ValueKind == JsonValueKind.Array &&
                reasons.EnumerateArray().Any(reason =>
                    reason.ValueKind == JsonValueKind.String &&
                    !string.Equals(reason.GetString(), "player_position_mismatch", StringComparison.Ordinal));
        }

        private static bool FishingPlayerRectangleAllows(JsonElement rule, int standX, int standY)
        {
            if (!rule.TryGetProperty("player_position", out var rectangle) || rectangle.ValueKind == JsonValueKind.Null)
            {
                return true;
            }
            if (rectangle.ValueKind != JsonValueKind.Object)
            {
                return false;
            }
            var x = ReadInt(rectangle, "x");
            var y = ReadInt(rectangle, "y");
            return standX >= x && standY >= y &&
                standX < x + ReadInt(rectangle, "width") && standY < y + ReadInt(rectangle, "height");
        }

        private static bool JsonIntArrayContains(JsonElement value, string property, int expected)
        {
            return value.TryGetProperty(property, out var items) && items.ValueKind == JsonValueKind.Array &&
                items.EnumerateArray().Any(item => item.TryGetInt32(out var number) && number == expected);
        }

        private static int ReadFishingLevel(SnapshotEnvelope snapshot)
        {
            var context = ReadStateFieldValue(snapshot, "fishing", "location_context");
            return context.HasValue ? ReadInt(context.Value, "fishing_level") : 0;
        }

        private static bool FishingCollisionGridBlocks(SnapshotEnvelope snapshot, int x, int y)
        {
            var grid = ReadStateFieldValue(snapshot, "locations", "collision_grid");
            if (!grid.HasValue || grid.Value.ValueKind != JsonValueKind.Object)
            {
                return true;
            }
            var width = ReadInt(grid.Value, "width");
            var height = ReadInt(grid.Value, "height");
            if (width <= 0 || height <= 0 || x < 0 || y < 0 || x >= width || y >= height)
            {
                return true;
            }
            return grid.Value.TryGetProperty("notable_tiles", out var tiles) && tiles.ValueKind == JsonValueKind.Array &&
                tiles.EnumerateArray().Any(tile =>
                    ReadInt(tile, "tile_x") == x && ReadInt(tile, "tile_y") == y && ReadBool(tile, "collision_blocked"));
        }

        private static bool HasRuntimeShopStockRecheckParameters(SmallModelAction action)
        {
            return !string.IsNullOrWhiteSpace(ReadParameter(action, "qualified_item_id")) &&
                !string.IsNullOrWhiteSpace(ReadParameter(action, "shop_item_id")) &&
                !string.IsNullOrWhiteSpace(ReadParameter(action, "expected_shop_id")) &&
                ReadIntParameter(action, "quantity") == 1 &&
                ReadIntParameter(action, "max_unit_price").HasValue &&
                string.Equals(ReadParameter(action, "compiler_context.active_menu_type_before_step"), "ShopMenu", StringComparison.Ordinal);
        }

        private static bool DialogueResponseOpensExpectedShop(string? expectedDialogueKey, string? responseKey, string? expectedShopId)
        {
            return (string.Equals(expectedDialogueKey, "Blacksmith", StringComparison.Ordinal) &&
                    string.Equals(responseKey, "Shop", StringComparison.Ordinal) &&
                    (string.IsNullOrWhiteSpace(expectedShopId) || string.Equals(expectedShopId, "Blacksmith", StringComparison.OrdinalIgnoreCase))) ||
                (string.Equals(expectedDialogueKey, "carpenter", StringComparison.Ordinal) &&
                    string.Equals(responseKey, "Shop", StringComparison.Ordinal) &&
                    (string.IsNullOrWhiteSpace(expectedShopId) || string.Equals(expectedShopId, "Carpenter", StringComparison.OrdinalIgnoreCase))) ||
                (string.Equals(expectedDialogueKey, "Marnie", StringComparison.Ordinal) &&
                    string.Equals(responseKey, "Supplies", StringComparison.Ordinal) &&
                    (string.IsNullOrWhiteSpace(expectedShopId) || string.Equals(expectedShopId, "AnimalShop", StringComparison.OrdinalIgnoreCase))) ||
                (string.Equals(expectedDialogueKey, "adventureGuild", StringComparison.Ordinal) &&
                    string.Equals(responseKey, "Shop", StringComparison.Ordinal) &&
                    (string.IsNullOrWhiteSpace(expectedShopId) || string.Equals(expectedShopId, "AdventureShop", StringComparison.OrdinalIgnoreCase)));
        }

    }
}

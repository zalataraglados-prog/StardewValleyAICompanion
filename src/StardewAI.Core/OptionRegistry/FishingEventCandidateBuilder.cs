using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;

namespace StardewAI.Core.OptionRegistry
{
    internal static partial class FishingEventCandidateBuilder
    {
        private const int EstimatedCatchTicks = 1800;

        public static EventCandidate[] Build(SnapshotEnvelope snapshot)
        {
            var locationContext = FieldValue(snapshot, "fishing", "location_context");
            var tilesValue = FieldValue(snapshot, "fishing", "fishable_tiles");
            var rodContextsValue = FieldValue(snapshot, "fishing", "rod_contexts");
            var activeCast = FieldValue(snapshot, "fishing", "active_cast_state");
            var gridValue = FieldValue(snapshot, "locations", "collision_grid");
            var locationId = String(locationContext, "location_id");
            var playerLocation = StateString(snapshot, "player", "location_id");

            var prerequisiteBlocks = new List<string>();
            if (Bool(locationContext, "can_fish_here") != true)
            {
                prerequisiteBlocks.Add("current_location_not_fishable");
            }
            if (tilesValue is null || tilesValue.Value.ValueKind != JsonValueKind.Array || tilesValue.Value.GetArrayLength() == 0)
            {
                prerequisiteBlocks.Add("no_fishable_tiles");
            }
            if (rodContextsValue is null || rodContextsValue.Value.ValueKind != JsonValueKind.Array || rodContextsValue.Value.GetArrayLength() == 0)
            {
                prerequisiteBlocks.Add("no_fishing_rod_contexts");
            }
            if (Bool(activeCast, "in_use") == true)
            {
                prerequisiteBlocks.Add("fishing_rod_already_in_use");
            }
            if (ActiveMenuOpen(snapshot))
            {
                prerequisiteBlocks.Add("active_menu_blocks_fishing");
            }
            if (string.IsNullOrWhiteSpace(locationId) || !string.Equals(locationId, playerLocation, StringComparison.Ordinal))
            {
                prerequisiteBlocks.Add("fishing_location_context_mismatch");
            }
            if (gridValue is null || gridValue.Value.ValueKind != JsonValueKind.Object)
            {
                prerequisiteBlocks.Add("fishing_collision_grid_unavailable");
            }
            if (prerequisiteBlocks.Count > 0)
            {
                return new[] { Blocked(locationId, prerequisiteBlocks) };
            }

            var tiles = tilesValue!.Value.EnumerateArray()
                .Select((tile, index) => new FishableTile(index, Int(tile, "tile_x"), Int(tile, "tile_y"), Int(tile, "water_depth")))
                .ToArray();
            var grid = CollisionGrid.Read(gridValue!.Value);
            var playerX = StateInt(snapshot, "player", "tile_x");
            var playerY = StateInt(snapshot, "player", "tile_y");
            var routeDistances = grid.RouteDistances(playerX, playerY);
            var fishingLevel = Int(locationContext, "fishing_level");
            var energyCost = Math.Max(1, (int)Math.Ceiling(8d - fishingLevel * 0.1d));
            var candidates = new List<EventCandidate>();
            var contextBlocks = new List<string>();

            foreach (var rodContext in rodContextsValue!.Value.EnumerateArray())
            {
                if (Bool(rodContext, "complete") != true || Bool(rodContext, "special_catch_sources_complete") != true)
                {
                    contextBlocks.Add("fishing_rod_context_incomplete");
                    continue;
                }

                var rodSlot = Int(rodContext, "rod_slot_index");
                var rodQualifiedId = String(rodContext, "rod_qualified_item_id");
                var reservedTileIndices = AddSpecialCandidates(
                    rodContext,
                    tiles,
                    fishingLevel,
                    grid,
                    routeDistances,
                    locationId,
                    rodSlot,
                    rodQualifiedId,
                    energyCost,
                    candidates);
                if (!rodContext.TryGetProperty("spawn_rules", out var spawnRules) ||
                    spawnRules.ValueKind != JsonValueKind.Object ||
                    Bool(spawnRules, "item_query_resolution_complete") != true ||
                    !spawnRules.TryGetProperty("rules", out var rules) ||
                    rules.ValueKind != JsonValueKind.Array)
                {
                    contextBlocks.Add("fishing_spawn_rules_missing_or_unresolved_for_rod");
                    continue;
                }

                var normalCast = FindBestCast(
                    tiles.Where(tile => !reservedTileIndices.Contains(tile.Index)),
                    default,
                    fishingLevel,
                    grid,
                    routeDistances);
                if (normalCast is not null)
                {
                    foreach (var rule in rules.EnumerateArray())
                    {
                        var fixedRuleBlocks = Strings(rule, "blocking_reasons")
                            .Where(reason => reason != "player_position_mismatch")
                            .ToArray();
                        var eligibleIndices = Ints(rule, "eligible_fishable_tile_indices");
                        if (fixedRuleBlocks.Length > 0 || Bool(rule, "condition_met") != true ||
                            !eligibleIndices.Contains(normalCast.Bobber.Index) ||
                            !RectangleContains(rule.TryGetProperty("player_position", out var playerRectangle) ? playerRectangle : default, normalCast.StandX, normalCast.StandY) ||
                            !rule.TryGetProperty("outputs", out var outputs) || outputs.ValueKind != JsonValueKind.Array)
                        {
                            continue;
                        }

                        foreach (var output in outputs.EnumerateArray())
                        {
                            var resolutionStatus = String(output, "resolution_status");
                            if (Bool(output, "resolution_complete") != true ||
                                Bool(output, "output_eligible_before_random_rolls") != true ||
                                resolutionStatus is not ("direct_item" or "vanilla_secret_note_or_item"))
                            {
                                continue;
                            }

                            var ruleKey = String(rule, "rule_key");
                            var qualifiedItemId = String(output, "qualified_item_id");
                            var outputIndex = Int(output, "output_index");
                            var chanceFactors = new[]
                                {
                                    Double(rule, "effective_spawn_chance_preview"),
                                    ChanceAtDepth(output, normalCast.Bobber.WaterDepth),
                                    Double(output, "output_local_chance_preview")
                                }
                                .Where(chance => chance.HasValue)
                                .Select(chance => chance!.Value)
                                .ToArray();
                            double? expectedChance = chanceFactors.Length > 0
                                ? chanceFactors.Aggregate(1d, (product, chance) => product * chance)
                                : null;
                            var fallbackMultiplier = BaseCatchFallbackMultiplier(rodContext, normalCast.Bobber.WaterDepth);
                            if (fallbackMultiplier <= 0d)
                            {
                                continue;
                            }
                            if (expectedChance.HasValue)
                            {
                                expectedChance *= fallbackMultiplier;
                            }
                            candidates.Add(OutcomeCandidate(
                                locationId,
                                rodSlot,
                                rodQualifiedId,
                                energyCost,
                                "rule",
                                ruleKey,
                                outputIndex,
                                String(output, "item_id"),
                                qualifiedItemId,
                                normalCast,
                                expectedChance,
                                expectedChance.HasValue ? "rule_local_preview" : "unresolved_rule_local_probability",
                                IntNullable(output, "effective_fish_difficulty"),
                                Bool(rule, "is_boss_fish") == true,
                                MaximumRawFishQuality(rodContext)));
                        }
                    }

                    AddBaseFallbackCandidate(
                        rodContext,
                        spawnRules,
                        normalCast,
                        locationId,
                        rodSlot,
                        rodQualifiedId,
                        energyCost,
                        candidates);
                }
            }

            if (candidates.Count > 0)
            {
                return AggregateMechanicalCandidates(candidates)
                    .OrderBy(candidate => candidate.EstimatedTicks)
                    .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                    .ToArray();
            }

            contextBlocks.Add("no_reachable_eligible_fishing_output");
            return new[] { Blocked(locationId, contextBlocks.Distinct(StringComparer.Ordinal)) };
        }

        private static HashSet<int> AddSpecialCandidates(
            JsonElement rodContext,
            FishableTile[] tiles,
            int fishingLevel,
            CollisionGrid grid,
            IReadOnlyDictionary<string, int> routeDistances,
            string locationId,
            int rodSlot,
            string rodQualifiedId,
            int energyCost,
            ICollection<EventCandidate> candidates)
        {
            var reserved = new HashSet<int>();
            if (!rodContext.TryGetProperty("special_catch_sources", out var sources) || sources.ValueKind != JsonValueKind.Object)
            {
                return reserved;
            }

            if (sources.TryGetProperty("location_get_fish_override", out var locationOverride) &&
                locationOverride.ValueKind == JsonValueKind.Object &&
                locationOverride.TryGetProperty("handlers", out var handlers) && handlers.ValueKind == JsonValueKind.Array)
            {
                foreach (var handler in handlers.EnumerateArray())
                {
                    var handlerName = String(handler, "handler");
                    if (handlerName == "mine_shaft_fishing")
                    {
                        if (AddMineShaftCandidates(
                            handler,
                            tiles,
                            fishingLevel,
                            grid,
                            routeDistances,
                            locationId,
                            rodSlot,
                            rodQualifiedId,
                            energyCost,
                            candidates))
                        {
                            foreach (var tile in tiles)
                            {
                                reserved.Add(tile.Index);
                            }
                        }
                        continue;
                    }
                    var handlerIndices = Ints(handler, "fishable_tile_indices");
                    if (handlerName == "island_southeast_stardrop_pool_walnut" &&
                        Bool(handler, "matched_pool_without_reward_returns_null") == true)
                    {
                        foreach (var tileIndex in handlerIndices)
                        {
                            reserved.Add(tileIndex);
                        }
                        continue;
                    }
                    var qualifiedItemId = String(handler, "qualified_item_id");
                    var direct = handlerName == "railroad_carolines_necklace" && Bool(handler, "eligible_before_catch") == true ||
                        handlerName == "island_southeast_stardrop_pool_walnut" && Bool(handler, "eligible_before_catch") == true ||
                        handlerName == "island_fishing_limited_walnut" && Bool(handler, "eligible_before_catch") == true;
                    if (!direct || string.IsNullOrWhiteSpace(qualifiedItemId))
                    {
                        continue;
                    }

                    var eligible = handlerIndices.Length > 0
                        ? tiles.Where(tile => handlerIndices.Contains(tile.Index) && !reserved.Contains(tile.Index)).ToArray()
                        : tiles.Where(tile => !reserved.Contains(tile.Index)).ToArray();
                    var cast = FindBestCast(eligible, default, fishingLevel, grid, routeDistances);
                    if (cast is null)
                    {
                        continue;
                    }
                    foreach (var tile in eligible)
                    {
                        reserved.Add(tile.Index);
                    }
                    candidates.Add(SpecialCandidate(
                        locationId,
                        rodSlot,
                        rodQualifiedId,
                        energyCost,
                        handlerName,
                        qualifiedItemId,
                        cast,
                        BaseCatchFallbackMultiplier(rodContext, cast.Bobber.WaterDepth)));
                }
            }

            if (sources.TryGetProperty("fish_ponds", out var ponds) && ponds.ValueKind == JsonValueKind.Array)
            {
                foreach (var pond in ponds.EnumerateArray())
                {
                    if (Bool(pond, "catch_available") != true)
                    {
                        continue;
                    }
                    var indices = Ints(pond, "fishable_tile_indices");
                    var eligible = tiles.Where(tile => indices.Contains(tile.Index) && !reserved.Contains(tile.Index)).ToArray();
                    var cast = FindBestCast(eligible, default, fishingLevel, grid, routeDistances);
                    var qualifiedItemId = String(pond, "fish_qualified_item_id");
                    if (cast is null || string.IsNullOrWhiteSpace(qualifiedItemId))
                    {
                        continue;
                    }
                    foreach (var tile in eligible)
                    {
                        reserved.Add(tile.Index);
                    }
                    candidates.Add(SpecialCandidate(
                        locationId,
                        rodSlot,
                        rodQualifiedId,
                        energyCost,
                        "fish_pond:" + Int(pond, "tile_x") + "," + Int(pond, "tile_y"),
                        qualifiedItemId,
                        cast,
                        BaseCatchFallbackMultiplier(rodContext, cast.Bobber.WaterDepth)));
                }
            }

            if (sources.TryGetProperty("fish_frenzy", out var frenzy) && Bool(frenzy, "active") == true)
            {
                var indices = Ints(frenzy, "eligible_fishable_tile_indices");
                var eligible = tiles.Where(tile => indices.Contains(tile.Index) && !reserved.Contains(tile.Index)).ToArray();
                var cast = FindBestCast(eligible, default, fishingLevel, grid, routeDistances);
                var qualifiedItemId = String(frenzy, "qualified_item_id");
                if (cast is not null && !string.IsNullOrWhiteSpace(qualifiedItemId))
                {
                    foreach (var tile in eligible)
                    {
                        reserved.Add(tile.Index);
                    }
                    candidates.Add(SpecialCandidate(
                        locationId,
                        rodSlot,
                        rodQualifiedId,
                        energyCost,
                        "fish_frenzy",
                        qualifiedItemId,
                        cast,
                        1d));
                }
            }

            return reserved;
        }

        private static void AddBaseFallbackCandidate(
            JsonElement rodContext,
            JsonElement spawnRules,
            CastSelection cast,
            string locationId,
            int rodSlot,
            string rodQualifiedId,
            int energyCost,
            ICollection<EventCandidate> candidates)
        {
            if (BaseCatchFallbackMultiplier(rodContext, cast.Bobber.WaterDepth) <= 0d ||
                !rodContext.TryGetProperty("special_catch_sources", out var sources) ||
                !sources.TryGetProperty("fallbacks", out var fallbacks))
            {
                return;
            }

            var tutorial = spawnRules.TryGetProperty("evaluation_context", out var evaluationContext) &&
                Bool(evaluationContext, "is_tutorial_catch") == true;
            var qualifiedItemId = tutorial
                ? String(fallbacks, "tutorial_location_data_fallback_qualified_item_id")
                : String(fallbacks, "no_location_data_match_qualified_item_id");
            if (string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                return;
            }

            candidates.Add(OutcomeCandidate(
                locationId,
                rodSlot,
                rodQualifiedId,
                energyCost,
                "special",
                "base_fallback",
                0,
                string.Empty,
                qualifiedItemId,
                cast,
                null,
                "unresolved_composed_fallthrough"));
        }

        private static bool AddMineShaftCandidates(
            JsonElement handler,
            FishableTile[] tiles,
            int fishingLevel,
            CollisionGrid grid,
            IReadOnlyDictionary<string, int> routeDistances,
            string locationId,
            int rodSlot,
            string rodQualifiedId,
            int energyCost,
            ICollection<EventCandidate> candidates)
        {
            var cast = FindBestCast(tiles, default, fishingLevel, grid, routeDistances);
            if (cast is null)
            {
                return false;
            }

            var usesTrainingRod = Bool(handler, "uses_training_rod") == true;
            var mineArea = Int(handler, "mine_area");
            var specialChance = Math.Clamp(MineSpecialChanceAtDepth(handler, cast.Bobber.WaterDepth), 0d, 1d);
            var specialQualifiedItemId = String(handler, "special_fish_qualified_item_id");
            if (!usesTrainingRod && specialChance > 0d && !string.IsNullOrWhiteSpace(specialQualifiedItemId))
            {
                candidates.Add(SpecialCandidate(
                    locationId,
                    rodSlot,
                    rodQualifiedId,
                    energyCost,
                    "mine_shaft_fishing",
                    specialQualifiedItemId,
                    cast,
                    specialChance));
            }

            if (!usesTrainingRod && mineArea != 80)
            {
                return false;
            }

            var remainingChance = usesTrainingRod ? 1d : 1d - specialChance;
            var caveJellyChance = usesTrainingRod
                ? 0d
                : Math.Clamp(Double(handler, "lava_area_cave_jelly_chance") ?? 0d, 0d, 1d);
            var weightedCaveJellyChance = remainingChance * caveJellyChance;
            if (weightedCaveJellyChance > 0d)
            {
                candidates.Add(SpecialCandidate(
                    locationId,
                    rodSlot,
                    rodQualifiedId,
                    energyCost,
                    "mine_shaft_fishing",
                    "(O)CaveJelly",
                    cast,
                    weightedCaveJellyChance));
            }

            var trashIds = Ints(handler, "mine_trash_item_id_range_inclusive");
            if (trashIds.Length == 2 && trashIds[1] >= trashIds[0])
            {
                var trashCount = trashIds[1] - trashIds[0] + 1;
                var perTrashChance = remainingChance * (1d - caveJellyChance) / trashCount;
                for (var itemId = trashIds[0]; perTrashChance > 0d && itemId <= trashIds[1]; itemId++)
                {
                    candidates.Add(SpecialCandidate(
                        locationId,
                        rodSlot,
                        rodQualifiedId,
                        energyCost,
                        "mine_shaft_fishing",
                        "(O)" + itemId.ToString(CultureInfo.InvariantCulture),
                        cast,
                        perTrashChance));
                }
            }

            return true;
        }

        private static double BaseCatchFallbackMultiplier(JsonElement rodContext, int waterDepth)
        {
            if (!rodContext.TryGetProperty("special_catch_sources", out var sources) ||
                !sources.TryGetProperty("location_get_fish_override", out var locationOverride) ||
                !locationOverride.TryGetProperty("handlers", out var handlers) || handlers.ValueKind != JsonValueKind.Array)
            {
                return 1d;
            }

            foreach (var handler in handlers.EnumerateArray())
            {
                if (String(handler, "handler") != "mine_shaft_fishing")
                {
                    continue;
                }
                if (Bool(handler, "uses_training_rod") == true || Int(handler, "mine_area") == 80)
                {
                    return 0d;
                }
                return 1d - Math.Clamp(MineSpecialChanceAtDepth(handler, waterDepth), 0d, 1d);
            }
            return 1d;
        }

        private static double MineSpecialChanceAtDepth(JsonElement handler, int waterDepth)
        {
            if (!handler.TryGetProperty("special_fish_chance_by_water_depth", out var values) || values.ValueKind != JsonValueKind.Array)
            {
                return 0d;
            }
            foreach (var value in values.EnumerateArray())
            {
                if (Int(value, "water_depth") == waterDepth)
                {
                    return Double(value, "special_fish_chance") ?? 0d;
                }
            }
            return 0d;
        }

        private static EventCandidate SpecialCandidate(
            string locationId,
            int rodSlot,
            string rodQualifiedId,
            int energyCost,
            string source,
            string qualifiedItemId,
            CastSelection cast,
            double? chance)
        {
            return OutcomeCandidate(
                locationId,
                rodSlot,
                rodQualifiedId,
                energyCost,
                "special",
                source,
                0,
                string.Empty,
                qualifiedItemId,
                cast,
                chance,
                chance.HasValue ? "composed_preview" : "unresolved_composed_fallthrough");
        }

        private static EventCandidate OutcomeCandidate(
            string locationId,
            int rodSlot,
            string rodQualifiedId,
            int energyCost,
            string sourceKind,
            string sourceKey,
            int outcomeIndex,
            string itemId,
            string qualifiedItemId,
            CastSelection cast,
            double? chance,
            string probabilityStatus,
            int? effectiveFishDifficulty = null,
            bool isBossFish = false,
            int? maximumRawFishQuality = null)
        {
            return new EventCandidate
            {
                CandidateId = $"fishing:outcome:{sourceKind}:{sourceKey}:{qualifiedItemId}:{rodSlot}:{cast.StandX},{cast.StandY}:{cast.Bobber.X},{cast.Bobber.Y}",
                Kind = "catch_fish",
                Available = true,
                LocationId = locationId,
                TileX = cast.StandX,
                TileY = cast.StandY,
                ItemId = itemId,
                QualifiedItemId = qualifiedItemId,
                SlotIndex = rodSlot,
                Quantity = 1,
                ExpectedEffect = "internal_fishing_outcome_projection;possible_qualified_item_id=" + qualifiedItemId,
                EstimatedTicks = EstimatedCatchTicks + cast.RouteDistance * 12,
                EnergyCost = energyCost,
                AvailabilityClass = "ready_now",
                AllowedNow = true,
                AllowedToday = true,
                Parameters = new[]
                {
                    Parameter("location_id", locationId),
                    Parameter("stand_tile_x", cast.StandX),
                    Parameter("stand_tile_y", cast.StandY),
                    Parameter("bobber_tile_x", cast.Bobber.X),
                    Parameter("bobber_tile_y", cast.Bobber.Y),
                    Parameter("rod_slot_index", rodSlot),
                    Parameter("rod_qualified_item_id", rodQualifiedId),
                    Parameter("source_kind", sourceKind),
                    Parameter("source_key", sourceKey),
                    Parameter("outcome_index", outcomeIndex),
                    Parameter("expected_qualified_item_id", qualifiedItemId),
                    Parameter("result_is_stochastic", !chance.HasValue || chance.Value < 1d),
                    Parameter("cast_direction", cast.Direction),
                    Parameter("cast_distance_tiles", cast.Distance),
                    Parameter("target_casting_power", cast.TargetCastingPower),
                    Parameter("max_cast_requested", cast.MaxCastRequested),
                    Parameter("route_distance_tiles", cast.RouteDistance),
                    Parameter("water_depth", cast.Bobber.WaterDepth),
                    Parameter("rule_local_catch_chance_preview", chance),
                    Parameter("rule_local_probability_status", probabilityStatus)
                }.Concat(FishingOutcomeExperienceParameters(
                    effectiveFishDifficulty,
                    isBossFish,
                    maximumRawFishQuality,
                    cast.Bobber.WaterDepth)).ToArray()
            };
        }

        private static IEnumerable<EventCandidate> AggregateMechanicalCandidates(IEnumerable<EventCandidate> projectedOutcomes)
        {
            return projectedOutcomes
                .GroupBy(candidate => string.Join("|", new[]
                {
                    candidate.LocationId,
                    candidate.SlotIndex?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    candidate.TileX?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    candidate.TileY?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    CandidateParameter(candidate, "bobber_tile_x"),
                    CandidateParameter(candidate, "bobber_tile_y"),
                    CandidateParameter(candidate, "cast_direction"),
                    CandidateParameter(candidate, "cast_distance_tiles")
                }), StringComparer.Ordinal)
                .Select(group =>
                {
                    var first = group.OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal).First();
                    var outcomes = group
                        .Select(candidate => new FishingOutcomeProjection
                        {
                            source_kind = CandidateParameter(candidate, "source_kind"),
                            source_key = CandidateParameter(candidate, "source_key"),
                            outcome_index = CandidateInt(candidate, "outcome_index") ?? 0,
                            item_id = candidate.ItemId,
                            qualified_item_id = candidate.QualifiedItemId,
                            chance_preview = ParameterDouble(candidate, "rule_local_catch_chance_preview"),
                            probability_status = CandidateParameter(candidate, "rule_local_probability_status"),
                            effective_fish_difficulty = CandidateInt(candidate, "effective_fish_difficulty"),
                            is_boss_fish = CandidateBool(candidate, "is_boss_fish"),
                            maximum_raw_fish_quality = CandidateInt(candidate, "maximum_raw_fish_quality")
                        })
                        .GroupBy(outcome => string.Join("|", outcome.source_kind, outcome.source_key, outcome.outcome_index, outcome.qualified_item_id), StringComparer.Ordinal)
                        .Select(outcomeGroup => outcomeGroup.First())
                        .OrderBy(outcome => outcome.source_kind, StringComparer.Ordinal)
                        .ThenBy(outcome => outcome.source_key, StringComparer.Ordinal)
                        .ThenBy(outcome => outcome.outcome_index)
                        .ThenBy(outcome => outcome.qualified_item_id, StringComparer.Ordinal)
                        .ToArray();
                    var distributionJson = JsonSerializer.Serialize(outcomes);
                    var possibleIdsJson = JsonSerializer.Serialize(outcomes
                        .Select(outcome => outcome.qualified_item_id)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray());
                    var allProbabilitiesKnown = outcomes.All(outcome => outcome.chance_preview.HasValue);
                    var knownMass = outcomes.Where(outcome => outcome.chance_preview.HasValue).Sum(outcome => outcome.chance_preview!.Value);
                    var onlyChance = outcomes.Length == 1 ? outcomes[0].chance_preview : null;
                    var stochastic = outcomes.Length != 1 || !onlyChance.HasValue || onlyChance.GetValueOrDefault() < 1d;
                    var distributionKey = "distribution:" + group.Key;
                    return new EventCandidate
                    {
                        CandidateId = "fishing:attempt:" + group.Key,
                        Kind = "catch_fish",
                        Available = true,
                        LocationId = first.LocationId,
                        TileX = first.TileX,
                        TileY = first.TileY,
                        SlotIndex = first.SlotIndex,
                        Quantity = 1,
                        ExpectedEffect = "legal_fishing_attempt_completed;catch_result_observed;outcome_distribution_count=" + outcomes.Length + ";result_is_stochastic=" + stochastic.ToString().ToLowerInvariant(),
                        EstimatedTicks = first.EstimatedTicks,
                        EnergyCost = first.EnergyCost,
                        AvailabilityClass = first.AvailabilityClass,
                        AllowedNow = first.AllowedNow,
                        AllowedToday = first.AllowedToday,
                        Parameters = new[]
                        {
                            Parameter("location_id", first.LocationId),
                            Parameter("stand_tile_x", first.TileX),
                            Parameter("stand_tile_y", first.TileY),
                            Parameter("bobber_tile_x", CandidateParameter(first, "bobber_tile_x")),
                            Parameter("bobber_tile_y", CandidateParameter(first, "bobber_tile_y")),
                            Parameter("rod_slot_index", first.SlotIndex),
                            Parameter("rod_qualified_item_id", CandidateParameter(first, "rod_qualified_item_id")),
                            Parameter("rule_key", distributionKey),
                            Parameter("expected_qualified_item_id", string.Empty),
                            Parameter("result_is_stochastic", stochastic),
                            Parameter("cast_direction", CandidateParameter(first, "cast_direction")),
                            Parameter("cast_distance_tiles", CandidateParameter(first, "cast_distance_tiles")),
                            Parameter("target_casting_power", CandidateParameter(first, "target_casting_power")),
                            Parameter("max_cast_requested", CandidateParameter(first, "max_cast_requested")),
                            Parameter("route_distance_tiles", CandidateParameter(first, "route_distance_tiles")),
                            Parameter("water_depth", CandidateParameter(first, "water_depth")),
                            Parameter("outcome_distribution_complete", true),
                            Parameter("outcome_distribution_json", distributionJson),
                            Parameter("possible_qualified_item_ids_json", possibleIdsJson),
                            Parameter("outcome_local_chance_preview_sum", knownMass),
                            Parameter("outcome_probability_status", allProbabilitiesKnown ? "all_local_previews_known" : "partial_unknown_fallthrough")
                        }.Concat(AggregatedFishingExperienceParameters(outcomes, first)).ToArray()
                    };
                });
        }

        private static string CandidateParameter(EventCandidate candidate, string name)
        {
            return candidate.Parameters.FirstOrDefault(parameter => parameter.Name == name)?.Value ?? string.Empty;
        }

        private static int? CandidateInt(EventCandidate candidate, string name)
        {
            return int.TryParse(CandidateParameter(candidate, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
        }

        private static bool CandidateBool(EventCandidate candidate, string name)
        {
            return bool.TryParse(CandidateParameter(candidate, name), out var value) && value;
        }

        private static int? IntNullable(JsonElement element, string property)
        {
            return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)
                ? parsed
                : null;
        }

        private static int MaximumRawFishQuality(JsonElement rodContext)
        {
            if (Bool(rodContext, "uses_training_rod") == true)
            {
                return 0;
            }

            return Int(rodContext, "quality_bobber_count") > 0 ? 4 : 2;
        }

        private sealed class FishingOutcomeProjection
        {
            public string source_kind { get; set; } = string.Empty;
            public string source_key { get; set; } = string.Empty;
            public int outcome_index { get; set; }
            public string item_id { get; set; } = string.Empty;
            public string qualified_item_id { get; set; } = string.Empty;
            public double? chance_preview { get; set; }
            public string probability_status { get; set; } = string.Empty;
            public int? effective_fish_difficulty { get; set; }
            public bool is_boss_fish { get; set; }
            public int? maximum_raw_fish_quality { get; set; }
        }

        private static CastSelection? FindBestCast(
            IEnumerable<FishableTile> eligibleTiles,
            JsonElement playerRectangle,
            int fishingLevel,
            CollisionGrid grid,
            IReadOnlyDictionary<string, int> routeDistances)
        {
            var addedDistance = fishingLevel >= 15 ? 4 : fishingLevel >= 8 ? 3 : fishingLevel >= 4 ? 2 : fishingLevel >= 1 ? 1 : 0;
            var selections = new List<CastSelection>();
            foreach (var bobber in eligibleTiles)
            {
                AddDirection(0, 0, 1, addedDistance + 3);
                AddDirection(1, -1, 0, addedDistance + 4);
                AddDirection(2, 0, -1, addedDistance + 3);
                AddDirection(3, 1, 0, addedDistance + 4);

                void AddDirection(int direction, int standOffsetX, int standOffsetY, int maxDistance)
                {
                    for (var distance = 2; distance <= maxDistance; distance++)
                    {
                        var standX = bobber.X + standOffsetX * distance;
                        var standY = bobber.Y + standOffsetY * distance;
                        if (grid.Blocked(standX, standY) || !RectangleContains(playerRectangle, standX, standY))
                        {
                            continue;
                        }

                        if (!routeDistances.TryGetValue(Key(standX, standY), out var routeDistance))
                        {
                            continue;
                        }

                        selections.Add(new CastSelection(standX, standY, direction, distance, maxDistance, routeDistance, bobber));
                    }
                }
            }

            return selections
                .OrderByDescending(selection => selection.MaxCastRequested)
                .ThenByDescending(selection => selection.Bobber.WaterDepth)
                .ThenBy(selection => selection.RouteDistance)
                .ThenBy(selection => selection.Distance)
                .ThenBy(selection => selection.StandY)
                .ThenBy(selection => selection.StandX)
                .FirstOrDefault();
        }

        private static bool RectangleContains(JsonElement rectangle, int x, int y)
        {
            if (rectangle.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                return true;
            }

            return x >= Int(rectangle, "x") && y >= Int(rectangle, "y") &&
                x < Int(rectangle, "x") + Int(rectangle, "width") &&
                y < Int(rectangle, "y") + Int(rectangle, "height");
        }

        private static double? ChanceAtDepth(JsonElement output, int waterDepth)
        {
            if (!output.TryGetProperty("data_fish_chance_by_water_depth", out var values) || values.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var value in values.EnumerateArray())
            {
                if (Int(value, "water_depth") == waterDepth)
                {
                    return Double(value, "chance_preview");
                }
            }
            return null;
        }

        private static EventCandidate Blocked(string locationId, IEnumerable<string> reasons)
        {
            return new EventCandidate
            {
                CandidateId = "fishing:catch:blocked",
                Kind = "catch_fish",
                Available = false,
                LocationId = locationId,
                ExpectedEffect = "no_fishing_action_compiled",
                BlockReasons = reasons.Where(reason => !string.IsNullOrWhiteSpace(reason)).Distinct(StringComparer.Ordinal).ToArray()
            };
        }

        private static JsonElement? FieldValue(SnapshotEnvelope snapshot, string sectionName, string fieldName)
        {
            if (!snapshot.State.TryGetValue(sectionName, out var section) || section.ValueKind != JsonValueKind.Object ||
                !section.TryGetProperty(fieldName, out var field) || field.ValueKind != JsonValueKind.Object ||
                !field.TryGetProperty("value", out var value))
            {
                return null;
            }
            return value;
        }

        private static string StateString(SnapshotEnvelope snapshot, string section, string field)
        {
            var value = FieldValue(snapshot, section, field);
            return value?.ValueKind == JsonValueKind.String ? value.Value.GetString() ?? string.Empty : string.Empty;
        }

        private static int StateInt(SnapshotEnvelope snapshot, string section, string field)
        {
            var value = FieldValue(snapshot, section, field);
            return value?.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var result) ? result : 0;
        }

        private static bool ActiveMenuOpen(SnapshotEnvelope snapshot)
        {
            var menu = FieldValue(snapshot, "menus", "active_menu");
            if (menu is null)
            {
                return false;
            }
            if (menu.Value.ValueKind == JsonValueKind.String)
            {
                return !string.Equals(menu.Value.GetString(), "none", StringComparison.OrdinalIgnoreCase);
            }
            return Bool(menu, "is_open") == true;
        }

        private static string String(JsonElement? value, string property)
        {
            return value.HasValue && value.Value.ValueKind == JsonValueKind.Object &&
                value.Value.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.String
                    ? item.GetString() ?? string.Empty
                    : string.Empty;
        }

        private static int Int(JsonElement? value, string property)
        {
            return value.HasValue && value.Value.ValueKind == JsonValueKind.Object &&
                value.Value.TryGetProperty(property, out var item) && item.TryGetInt32(out var result)
                    ? result
                    : 0;
        }

        private static bool? Bool(JsonElement? value, string property)
        {
            if (!value.HasValue || value.Value.ValueKind != JsonValueKind.Object || !value.Value.TryGetProperty(property, out var item))
            {
                return null;
            }
            return item.ValueKind == JsonValueKind.True ? true : item.ValueKind == JsonValueKind.False ? false : null;
        }

        private static double? Double(JsonElement value, string property)
        {
            return value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var item) && item.TryGetDouble(out var result)
                ? result
                : null;
        }

        private static string[] Strings(JsonElement value, string property)
        {
            return value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var items) && items.ValueKind == JsonValueKind.Array
                ? items.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? string.Empty).ToArray()
                : Array.Empty<string>();
        }

        private static int[] Ints(JsonElement value, string property)
        {
            return value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var items) && items.ValueKind == JsonValueKind.Array
                ? items.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Number).Select(item => item.GetInt32()).ToArray()
                : Array.Empty<int>();
        }

        private static SmallModelActionParameter Parameter(string name, object? value)
        {
            return new SmallModelActionParameter
            {
                Name = name,
                Value = value switch
                {
                    null => string.Empty,
                    double number => number.ToString("0.########", CultureInfo.InvariantCulture),
                    _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
                }
            };
        }

        private static double? ParameterDouble(EventCandidate candidate, string name)
        {
            var raw = candidate.Parameters.FirstOrDefault(parameter => parameter.Name == name)?.Value;
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
        }

        private static string Key(int x, int y) => x + "," + y;

        private sealed class FishableTile
        {
            public FishableTile(int index, int x, int y, int waterDepth)
            {
                Index = index;
                X = x;
                Y = y;
                WaterDepth = waterDepth;
            }

            public int Index { get; }
            public int X { get; }
            public int Y { get; }
            public int WaterDepth { get; }
        }

        private sealed class CastSelection
        {
            public CastSelection(int standX, int standY, int direction, int distance, int maxDistance, int routeDistance, FishableTile bobber)
            {
                StandX = standX;
                StandY = standY;
                Direction = direction;
                Distance = distance;
                MaxDistance = maxDistance;
                RouteDistance = routeDistance;
                Bobber = bobber;
            }

            public int StandX { get; }
            public int StandY { get; }
            public int Direction { get; }
            public int Distance { get; }
            public int MaxDistance { get; }
            public double TargetCastingPower => Math.Clamp(Distance / (double)MaxDistance, 0d, 1d);
            public bool MaxCastRequested => Distance == MaxDistance;
            public int RouteDistance { get; }
            public FishableTile Bobber { get; }
        }

        private sealed class CollisionGrid
        {
            private readonly HashSet<string> blocked;

            private CollisionGrid(int width, int height, HashSet<string> blocked)
            {
                Width = width;
                Height = height;
                this.blocked = blocked;
            }

            public int Width { get; }
            public int Height { get; }

            public static CollisionGrid Read(JsonElement value)
            {
                var blocked = new HashSet<string>(StringComparer.Ordinal);
                if (value.TryGetProperty("notable_tiles", out var tiles) && tiles.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tile in tiles.EnumerateArray())
                    {
                        if (Bool(tile, "collision_blocked") == true)
                        {
                            blocked.Add(Key(Int(tile, "tile_x"), Int(tile, "tile_y")));
                        }
                    }
                }
                return new CollisionGrid(Int(value, "width"), Int(value, "height"), blocked);
            }

            public bool Blocked(int x, int y)
            {
                return x < 0 || y < 0 || x >= Width || y >= Height || blocked.Contains(Key(x, y));
            }

            public IReadOnlyDictionary<string, int> RouteDistances(int startX, int startY)
            {
                var result = new Dictionary<string, int>(StringComparer.Ordinal);
                if (Width <= 0 || Height <= 0 || Blocked(startX, startY))
                {
                    return result;
                }

                var queue = new Queue<(int X, int Y)>();
                queue.Enqueue((startX, startY));
                result[Key(startX, startY)] = 0;
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    var nextDistance = result[Key(current.X, current.Y)] + 1;
                    Visit(current.X + 1, current.Y);
                    Visit(current.X - 1, current.Y);
                    Visit(current.X, current.Y + 1);
                    Visit(current.X, current.Y - 1);

                    void Visit(int x, int y)
                    {
                        var key = Key(x, y);
                        if (Blocked(x, y) || result.ContainsKey(key))
                        {
                            return;
                        }
                        result[key] = nextDistance;
                        queue.Enqueue((x, y));
                    }
                }
                return result;
            }
        }
    }
}

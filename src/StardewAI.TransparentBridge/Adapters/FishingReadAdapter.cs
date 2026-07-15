using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData;
using StardewValley.GameData.Locations;
using StardewValley.Internal;
using StardewValley.Locations;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class FishingReadAdapter : ReadAdapterBase
{
    public override string Domain => "fishing";
    public override int Priority => 35;

    public override StateAdapterResult Collect(long tick)
    {
        var player = Context.IsWorldReady ? Game1.player : null;
        var location = Context.IsWorldReady ? Game1.currentLocation : null;
        if (player is null || location is null)
        {
            return Section("fishing", new Dictionary<string, object>
            {
                ["location_context"] = Unavailable("world_not_ready", "Game1.currentLocation", tick),
                ["fishable_tiles"] = Unavailable("world_not_ready", "Game1.currentLocation.isTileFishable", tick),
                ["rod_inventory"] = Unavailable("world_not_ready", "Game1.player.Items as FishingRod", tick),
                ["rod_contexts"] = Unavailable("world_not_ready", "Game1.player.Items as FishingRod; Data/Locations Fish; GameLocation.getFish", tick),
                ["active_cast_state"] = Unavailable("world_not_ready", "Game1.player.CurrentTool as FishingRod", tick),
                ["spawn_rules"] = Unavailable("world_not_ready", "Data/Locations Fish and Data/Fish", tick),
                ["special_catch_sources"] = Unavailable("world_not_ready", "GameLocation.getFish", tick)
            }, new[]
            {
                "fishing.location_context",
                "fishing.fishable_tiles",
                "fishing.rod_inventory",
                "fishing.rod_contexts",
                "fishing.active_cast_state",
                "fishing.spawn_rules",
                "fishing.special_catch_sources"
            }, "unavailable");
        }

        var dimensions = MapDimensions(location);
        var canFishHere = location.canFishHere();
        var fishableTiles = dimensions.HasValue
            ? ReadFishableTiles(location, dimensions.Value.Width, dimensions.Value.Height, canFishHere)
            : null;
        var currentRod = player.CurrentTool as FishingRod;
        object? spawnRules = null;
        SpecialCatchSourcesProjection? specialCatchSources = null;
        RodFishingContextsProjection? rodContexts = null;
        string? spawnRuleFailure = null;
        string? specialCatchSourcesFailure = null;
        string? rodContextsFailure = null;
        if (fishableTiles is not null)
        {
            try
            {
                spawnRules = ReadSpawnRules(location, player, currentRod, fishableTiles);
            }
            catch (Exception ex)
            {
                spawnRuleFailure = $"{ex.GetType().Name}: {ex.Message}";
            }

            try
            {
                specialCatchSources = ReadSpecialCatchSources(location, player, currentRod, fishableTiles);
            }
            catch (Exception ex)
            {
                specialCatchSourcesFailure = $"{ex.GetType().Name}: {ex.Message}";
            }

            try
            {
                rodContexts = ReadRodContexts(location, player, currentRod, fishableTiles);
            }
            catch (Exception ex)
            {
                rodContextsFailure = $"{ex.GetType().Name}: {ex.Message}";
            }
        }

        var unavailable = new List<string>();
        if (fishableTiles is null)
        {
            unavailable.Add("fishing.fishable_tiles");
        }
        if (spawnRules is null)
        {
            unavailable.Add("fishing.spawn_rules");
        }
        if (specialCatchSources is null)
        {
            unavailable.Add("fishing.special_catch_sources");
        }
        else if (!specialCatchSources.Complete)
        {
            unavailable.Add("fishing.special_catch_sources.location_override");
        }
        if (rodContexts is null)
        {
            unavailable.Add("fishing.rod_contexts");
        }
        else if (!rodContexts.Complete)
        {
            unavailable.Add("fishing.rod_contexts.incomplete_rule_or_override_context");
        }
        object spawnRulesEnvelope;
        if (spawnRules is null)
        {
            spawnRulesEnvelope = Unavailable(
                spawnRuleFailure ?? "fishable_tile_scan_unavailable",
                "GameLocation.GetFishFromLocationData; Data/Locations Fish; Data/Fish",
                tick);
        }
        else
        {
            spawnRulesEnvelope = Field(
                spawnRules,
                "Game1.locationData[Default].Fish; GameLocation.GetData().Fish; DataLoader.Fish; GameStateQuery; ItemQueryResolver registry",
                tick);
        }
        object specialCatchSourcesEnvelope;
        if (specialCatchSources is null)
        {
            specialCatchSourcesEnvelope = Unavailable(
                specialCatchSourcesFailure ?? "fishable_tile_scan_unavailable",
                "GameLocation.getFish",
                tick);
        }
        else
        {
            specialCatchSourcesEnvelope = Field(
                specialCatchSources.Value,
                "GameLocation.getFish; FishPond fields; GameLocation.fishFrenzyFish/fishSplashPoint",
                tick);
        }
        object rodContextsEnvelope;
        if (rodContexts is null)
        {
            rodContextsEnvelope = Unavailable(
                rodContextsFailure ?? "fishable_tile_scan_unavailable",
                "Game1.player.Items as FishingRod; Data/Locations Fish; GameLocation.getFish",
                tick);
        }
        else
        {
            rodContextsEnvelope = Field(
                rodContexts.Rows,
                "Game1.player.Items as FishingRod; Data/Locations Fish; Data/Fish; GameLocation.getFish",
                tick);
        }

        return Section("fishing", new Dictionary<string, object>
        {
            ["location_context"] = Field(new
            {
                location_id = location.NameOrUniqueName,
                location_type = location.GetType().FullName,
                can_fish_here = canFishHere,
                map_width = dimensions?.Width,
                map_height = dimensions?.Height,
                fishable_tile_count = fishableTiles?.Length,
                fishing_level = player.FishingLevel,
                luck_level = player.LuckLevel,
                daily_luck = player.DailyLuck,
                scan_policy = "complete_current_map_no_cap"
            }, "Game1.currentLocation.canFishHere; Farmer.FishingLevel/LuckLevel/DailyLuck", tick),
            ["fishable_tiles"] = Field(fishableTiles, "GameLocation.isTileFishable; FishingRod.distanceToLand; GameLocation.TryGetFishAreaForTile", tick),
            ["rod_inventory"] = Field(ReadRodInventory(player, currentRod), "Game1.player.Items as StardewValley.Tools.FishingRod", tick),
            ["rod_contexts"] = rodContextsEnvelope,
            ["active_cast_state"] = Field(ReadActiveCastState(currentRod), "Game1.player.CurrentTool as FishingRod runtime fields", tick),
            ["spawn_rules"] = spawnRulesEnvelope,
            ["special_catch_sources"] = specialCatchSourcesEnvelope
        }, unavailable, unavailable.Count == 0 ? "complete" : "partial");
    }

    private static (int Width, int Height)? MapDimensions(GameLocation location)
    {
        var layers = location.map?.Layers.Cast<xTile.Layers.Layer>().ToArray();
        if (layers is null || layers.Length == 0)
        {
            return null;
        }

        var width = layers.Max(layer => layer.LayerWidth);
        var height = layers.Max(layer => layer.LayerHeight);
        return width > 0 && height > 0 ? (width, height) : null;
    }

    private static FishingTileReadRow[] ReadFishableTiles(GameLocation location, int width, int height, bool canFishHere)
    {
        if (!canFishHere)
        {
            return Array.Empty<FishingTileReadRow>();
        }

        return FishingTileScanner.Scan(
            width,
            height,
            location.isTileFishable,
            (x, y) => FishingRod.distanceToLand(x, y, location),
            (x, y) =>
            {
                if (!location.TryGetFishAreaForTile(new Microsoft.Xna.Framework.Vector2(x, y), out var areaId, out _))
                {
                    return new FishingAreaRead(null, null);
                }

                return new FishingAreaRead(areaId, location.GetFishingAreaDisplayName(areaId));
            });
    }

    private static object ReadSpawnRules(
        GameLocation location,
        Farmer player,
        FishingRod? selectedRod,
        FishingTileReadRow[] fishableTiles)
    {
        var defaultData = Game1.locationData.TryGetValue("Default", out var defaultLocationData)
            ? defaultLocationData
            : null;
        var locationData = location.GetData();
        var locationDataKey = location is MineShaft ? "UndergroundMine" : location.NameOrUniqueName;
        var fishData = DataLoader.Fish(Game1.content);
        var season = Game1.GetSeasonForLocation(location).ToString();
        var bait = selectedRod?.GetBait();
        var hasMagicBait = selectedRod?.HasMagicBait() == true;
        var hasCuriosityLure = selectedRod?.HasCuriosityLure() == true;
        var targetedFishId = bait?.QualifiedItemId == "(O)SpecificBait"
            && bait.preservedParentSheetIndex.Value is not null
                ? "(O)" + bait.preservedParentSheetIndex.Value
                : null;
        var usesTrainingRod = selectedRod?.QualifiedItemId == "(T)TrainingRod";
        var isTutorialCatch = player.fishCaught.Length == 0;
        var tileIndices = fishableTiles
            .Select((tile, index) => (tile, index))
            .ToDictionary(pair => (pair.tile.TileX, pair.tile.TileY), pair => pair.index);
        var sources = new List<(string Source, IReadOnlyList<SpawnFishData> Rules)>();
        if (defaultData?.Fish is { Count: > 0 })
        {
            sources.Add(("Data/Locations:Default", defaultData.Fish));
        }
        if (locationData?.Fish is { Count: > 0 })
        {
            sources.Add(($"Data/Locations:{locationDataKey}", locationData.Fish));
        }

        var rows = new List<object>();
        var unresolvedRules = new List<string>();
        foreach (var source in sources)
        {
            for (var sourceIndex = 0; sourceIndex < source.Rules.Count; sourceIndex++)
            {
                var spawn = source.Rules[sourceIndex];
                var ruleKey = $"{source.Source}#{sourceIndex}:{spawn.Id}";
                var seed = StableSeed($"{location.NameOrUniqueName}|{Game1.year}|{Game1.currentSeason}|{Game1.dayOfMonth}|{Game1.timeOfDay}|{ruleKey}");
                var conditionRandom = new Random(seed);
                var ignoreQueryKeys = hasMagicBait ? GameStateQuery.MagicBaitIgnoreQueryKeys : null;
                var conditionMet = string.IsNullOrWhiteSpace(spawn.Condition)
                    || GameStateQuery.CheckConditions(
                        spawn.Condition,
                        location,
                        null,
                        null,
                        null,
                        conditionRandom,
                        ignoreQueryKeys);
                var ruleSpec = new FishingRuleEligibilitySpec
                {
                    Season = spawn.Season?.ToString(),
                    FishAreaId = spawn.FishAreaId,
                    BobberPosition = ReadRectangle(spawn.BobberPosition),
                    PlayerPosition = ReadRectangle(spawn.PlayerPosition),
                    MinFishingLevel = spawn.MinFishingLevel,
                    MinDistanceFromShore = spawn.MinDistanceFromShore,
                    MaxDistanceFromShore = spawn.MaxDistanceFromShore,
                    RequireMagicBait = spawn.RequireMagicBait,
                    ConditionMet = conditionMet
                };
                var eligibility = FishingSpawnRuleEvaluator.Evaluate(
                    ruleSpec,
                    new FishingRuleEligibilityContext
                    {
                        Season = season,
                        PlayerTileX = player.TilePoint.X,
                        PlayerTileY = player.TilePoint.Y,
                        FishingLevel = player.FishingLevel,
                        HasMagicBait = hasMagicBait
                    },
                    fishableTiles);
                var targetedByBait = spawn.ItemId is not null && spawn.ItemId == targetedFishId;
                var chanceRandom = new Random(seed ^ 0x45D9F3B);
                var spawnChance = spawn.GetChance(
                    hasCuriosityLure,
                    player.DailyLuck,
                    player.LuckLevel,
                    (value, modifiers, mode) => Utility.ApplyQuantityModifiers(
                        value,
                        modifiers,
                        mode,
                        location,
                        player,
                        null,
                        null,
                        chanceRandom),
                    targetedByBait);
                var outputProjections = ReadSpawnOutputs(
                    spawn,
                    player,
                    location,
                    fishData,
                    eligibility.EligibleTiles,
                    hasMagicBait,
                    hasCuriosityLure,
                    targetedFishId,
                    usesTrainingRod,
                    isTutorialCatch,
                    seed);
                if (outputProjections.Any(output => !output.ResolutionComplete))
                {
                    unresolvedRules.Add(ruleKey);
                }
                var outputs = outputProjections.Select(output => output.Value).ToArray();

                rows.Add(new
                {
                    rule_key = ruleKey,
                    source = source.Source,
                    source_index = sourceIndex,
                    id = spawn.Id,
                    precedence = spawn.Precedence,
                    same_precedence_order = "randomized_by_game_at_catch_time",
                    item_id = spawn.ItemId,
                    random_item_ids = spawn.RandomItemId?.ToArray() ?? Array.Empty<string>(),
                    item_selection_mode = spawn.RandomItemId is { Count: > 0 } ? "random_item_id" : "item_id",
                    per_item_condition = spawn.PerItemCondition,
                    condition = spawn.Condition,
                    condition_met = conditionMet,
                    condition_evaluation = "non_mutating_local_rng",
                    condition_preview_seed = seed,
                    season = spawn.Season?.ToString(),
                    fish_area_id = spawn.FishAreaId,
                    bobber_position = ReadRectangle(spawn.BobberPosition),
                    player_position = ReadRectangle(spawn.PlayerPosition),
                    min_fishing_level = spawn.MinFishingLevel,
                    min_distance_from_shore = spawn.MinDistanceFromShore,
                    max_distance_from_shore = spawn.MaxDistanceFromShore,
                    require_magic_bait = spawn.RequireMagicBait,
                    catch_limit = spawn.CatchLimit,
                    can_use_training_rod = spawn.CanUseTrainingRod,
                    is_boss_fish = spawn.IsBossFish,
                    set_flag_on_catch = spawn.SetFlagOnCatch,
                    ignore_fish_data_requirements = spawn.IgnoreFishDataRequirements,
                    can_be_inherited = spawn.CanBeInherited,
                    use_fish_caught_seeded_random = spawn.UseFishCaughtSeededRandom,
                    base_spawn_chance = spawn.Chance,
                    effective_spawn_chance_preview = spawnChance,
                    spawn_chance_roll_pending = true,
                    apply_daily_luck = spawn.ApplyDailyLuck,
                    curiosity_lure_buff = spawn.CuriosityLureBuff,
                    specific_bait_buff = spawn.SpecificBaitBuff,
                    specific_bait_multiplier = spawn.SpecificBaitMultiplier,
                    chance_boost_per_luck_level = spawn.ChanceBoostPerLuckLevel,
                    chance_modifier_mode = spawn.ChanceModifierMode.ToString(),
                    chance_modifiers = ReadQuantityModifiers(spawn.ChanceModifiers),
                    eligible_before_random_rolls = eligibility.EligibleBeforeRandomRolls,
                    blocking_reasons = eligibility.BlockingReasons,
                    eligible_fishable_tile_indices = eligibility.EligibleTiles
                        .Select(tile => tileIndices[(tile.TileX, tile.TileY)])
                        .ToArray(),
                    outputs
                });
            }
        }

        return new
        {
            location_id = location.NameOrUniqueName,
            default_rule_count = defaultData?.Fish?.Count ?? 0,
            location_rule_count = locationData?.Fish?.Count ?? 0,
            combined_rule_count = rows.Count,
            inventory_complete = true,
            ordering_policy = "precedence_then_runtime_rng_within_precedence",
            evaluation_context = new
            {
                season,
                time_of_day = Game1.timeOfDay,
                is_raining = location.IsRainingHere(),
                player_tile_x = player.TilePoint.X,
                player_tile_y = player.TilePoint.Y,
                fishing_level = player.FishingLevel,
                luck_level = player.LuckLevel,
                daily_luck = player.DailyLuck,
                selected_rod_qualified_item_id = selectedRod?.QualifiedItemId,
                has_magic_bait = hasMagicBait,
                has_curiosity_lure = hasCuriosityLure,
                targeted_fish_qualified_item_id = targetedFishId,
                uses_training_rod = usesTrainingRod,
                is_tutorial_catch = isTutorialCatch,
                context_mode = "selected_rod_for_next_cast"
            },
            random_policy = new
            {
                consumes_game_rng = false,
                chance_rolls_executed = false,
                previews_use_stable_local_rng = true
            },
            item_query_resolution_complete = unresolvedRules.Count == 0,
            unresolved_rule_keys = unresolvedRules.ToArray(),
            rules = rows.ToArray()
        };
    }

    private static SpecialCatchSourcesProjection ReadSpecialCatchSources(
        GameLocation location,
        Farmer player,
        FishingRod? selectedRod,
        FishingTileReadRow[] fishableTiles)
    {
        var tileIndices = fishableTiles
            .Select((tile, index) => (tile, index))
            .ToDictionary(pair => (pair.tile.TileX, pair.tile.TileY), pair => pair.index);
        var ponds = location.buildings
            .OfType<FishPond>()
            .Select(pond => new
            {
                building_type = pond.GetType().FullName,
                tile_x = pond.tileX.Value,
                tile_y = pond.tileY.Value,
                tiles_wide = pond.tilesWide.Value,
                tiles_high = pond.tilesHigh.Value,
                days_of_construction_left = pond.daysOfConstructionLeft.Value,
                fish_item_id = pond.fishType.Value,
                fish_qualified_item_id = string.IsNullOrWhiteSpace(pond.fishType.Value)
                    ? null
                    : "(O)" + pond.fishType.Value,
                fish_count = pond.FishCount,
                catch_available = pond.daysOfConstructionLeft.Value <= 0 && pond.FishCount > 0,
                catch_effect = "decrements_fish_count_by_one",
                fishable_tile_indices = fishableTiles
                    .Where(tile => pond.isTileFishable(new Vector2(tile.TileX, tile.TileY)))
                    .Select(tile => tileIndices[(tile.TileX, tile.TileY)])
                    .ToArray()
            })
            .ToArray();
        var frenzyItemId = location.fishFrenzyFish.Value;
        var frenzyPoint = location.fishSplashPoint.Value;
        var frenzyActive = !string.IsNullOrWhiteSpace(frenzyItemId);
        var overrideMethod = location.GetType()
            .GetMethods()
            .FirstOrDefault(method => method.Name == nameof(GameLocation.getFish) && method.GetParameters().Length == 7);
        var overrideDeclaringType = overrideMethod?.DeclaringType;
        var hasLocationOverride = overrideDeclaringType is not null && overrideDeclaringType != typeof(GameLocation);
        var overrideRead = ReadLocationOverride(
            location,
            player,
            selectedRod,
            fishableTiles,
            tileIndices,
            overrideDeclaringType);

        return new SpecialCatchSourcesProjection(new
        {
            priority_order = new[]
            {
                "location_get_fish_override",
                "fish_pond",
                "fish_frenzy",
                "data_locations_spawn_rules",
                "trash_fallback"
            },
            location_get_fish_override = new
            {
                present = hasLocationOverride,
                runtime_location_type = location.GetType().FullName,
                declaring_type = overrideDeclaringType?.FullName,
                transparent_handler_available = overrideRead.Complete,
                handlers = overrideRead.Handlers,
                reason = !overrideRead.Complete
                    ? "location_specific_or_modded_getFish_override_not_decoded"
                    : null
            },
            fish_ponds = ponds,
            fish_frenzy = new
            {
                active = frenzyActive,
                qualified_item_id = frenzyActive ? frenzyItemId : null,
                center_tile_x = frenzyPoint.X,
                center_tile_y = frenzyPoint.Y,
                radius_tiles = 2,
                eligible_fishable_tile_indices = frenzyActive
                    ? fishableTiles
                        .Where(tile => Vector2.Distance(
                            new Vector2(tile.TileX, tile.TileY),
                            new Vector2(frenzyPoint.X, frenzyPoint.Y)) <= 2f)
                        .Select(tile => tileIndices[(tile.TileX, tile.TileY)])
                        .ToArray()
                    : Array.Empty<int>()
            },
            fallbacks = new
            {
                tutorial_location_data_fallback_qualified_item_id = "(O)145",
                no_location_data_match_qualified_item_id = "(O)168"
            }
        }, !hasLocationOverride || overrideRead.Complete);
    }

    private static RodFishingContextsProjection ReadRodContexts(
        GameLocation location,
        Farmer player,
        FishingRod? currentRod,
        FishingTileReadRow[] fishableTiles)
    {
        var rows = new List<object>();
        var complete = true;
        foreach (var entry in player.Items.Select((item, slotIndex) => new { item, slotIndex }))
        {
            if (entry.item is not FishingRod rod)
            {
                continue;
            }

            object? spawnRules = null;
            SpecialCatchSourcesProjection? specialSources = null;
            string? failure = null;
            try
            {
                spawnRules = ReadSpawnRules(location, player, rod, fishableTiles);
                specialSources = ReadSpecialCatchSources(location, player, rod, fishableTiles);
            }
            catch (Exception ex)
            {
                failure = $"{ex.GetType().Name}: {ex.Message}";
            }

            var contextComplete = spawnRules is not null && specialSources is not null && specialSources.Complete;
            complete &= contextComplete;
            rows.Add(new
            {
                rod_slot_index = entry.slotIndex,
                rod_qualified_item_id = rod.QualifiedItemId,
                rod_upgrade_level = rod.UpgradeLevel,
                selected = ReferenceEquals(rod, currentRod),
                complete = contextComplete,
                failure,
                spawn_rules = spawnRules,
                special_catch_sources = specialSources?.Value,
                special_catch_sources_complete = specialSources?.Complete == true
            });
        }

        return new RodFishingContextsProjection(rows.ToArray(), complete);
    }

    private static SpawnOutputProjection[] ReadSpawnOutputs(
        SpawnFishData spawn,
        Farmer player,
        GameLocation location,
        IReadOnlyDictionary<string, string> fishData,
        IReadOnlyList<FishingTileReadRow> eligibleTiles,
        bool hasMagicBait,
        bool hasCuriosityLure,
        string? targetedFishId,
        bool usesTrainingRod,
        bool isTutorialCatch,
        int seed)
    {
        var rawOutputs = spawn.RandomItemId is { Count: > 0 }
            ? spawn.RandomItemId
            : string.IsNullOrWhiteSpace(spawn.ItemId)
                ? new List<string>()
                : new List<string> { spawn.ItemId };
        if (rawOutputs.Count == 0)
        {
            return new[]
            {
                new SpawnOutputProjection(new
                {
                    output_index = 0,
                    raw = (string?)null,
                    resolution_complete = false,
                    resolution_status = "missing_item_spawn_fields",
                    reason = "spawn_rule_has_no_item_id_or_random_item_ids"
                }, false)
            };
        }

        return rawOutputs
            .Select((raw, index) => ReadSpawnOutput(
                raw,
                index,
                spawn,
                player,
                location,
                fishData,
                eligibleTiles,
                hasMagicBait,
                hasCuriosityLure,
                targetedFishId,
                usesTrainingRod,
                isTutorialCatch,
                seed))
            .ToArray();
    }

    private static LocationOverrideProjection ReadLocationOverride(
        GameLocation location,
        Farmer player,
        FishingRod? selectedRod,
        IReadOnlyList<FishingTileReadRow> fishableTiles,
        IReadOnlyDictionary<(int X, int Y), int> tileIndices,
        Type? overrideDeclaringType)
    {
        if (overrideDeclaringType is null || overrideDeclaringType == typeof(GameLocation))
        {
            return new LocationOverrideProjection(Array.Empty<object>(), true);
        }

        var handlers = new List<object>();
        switch (location)
        {
            case Railroad:
            {
                var necklaceEligible = Game1.player.secretNotesSeen.Contains(GameLocation.NECKLACE_SECRET_NOTE_INDEX)
                    && !Game1.player.hasOrWillReceiveMail(GameLocation.CAROLINES_NECKLACE_MAIL);
                handlers.Add(new
                {
                    handler = "railroad_carolines_necklace",
                    eligible_before_catch = necklaceEligible,
                    qualified_item_id = GameLocation.CAROLINES_NECKLACE_ITEM_QID,
                    required_secret_note_index = GameLocation.NECKLACE_SECRET_NOTE_INDEX,
                    necklace_mail_already_received_or_pending = Game1.player.hasOrWillReceiveMail(GameLocation.CAROLINES_NECKLACE_MAIL),
                    catch_side_effects = new[]
                    {
                        "add_carolines_necklace_mail_for_tomorrow",
                        "add_quest_128",
                        "add_quest_129"
                    },
                    fallback = "base_getFish"
                });
                return new LocationOverrideProjection(handlers.ToArray(), overrideDeclaringType == typeof(Railroad));
            }
            case MineShaft mine:
            {
                var mineArea = mine.getMineArea();
                var bait = selectedRod?.GetBait();
                var baitName = bait?.Name ?? string.Empty;
                var usesTrainingRod = selectedRod?.QualifiedItemId.Contains("TrainingRod", StringComparison.Ordinal) == true;
                var hasCuriosityLure = selectedRod?.HasCuriosityLure() == true;
                var specialQualifiedItemId = mineArea switch
                {
                    0 or 10 => "(O)158",
                    40 => "(O)161",
                    80 => "(O)162",
                    _ => null
                };
                var specialFishData = string.IsNullOrWhiteSpace(specialQualifiedItemId)
                    ? null
                    : ItemRegistry.GetData(specialQualifiedItemId);
                var specialFishInternalName = specialFishData?.InternalName ?? string.Empty;
                var specificBaitNameConditionComplete = specialQualifiedItemId is null
                    || (specialFishData is not null && !string.IsNullOrWhiteSpace(specialFishInternalName));
                var specificBaitNameConditionMatched = specificBaitNameConditionComplete
                    && !string.IsNullOrWhiteSpace(specialFishInternalName)
                    && baitName.Contains(specialFishInternalName, StringComparison.Ordinal);
                var baitBonus = specificBaitNameConditionMatched ? 10d : 0d;
                var specialChanceByWaterDepth = fishableTiles
                    .Select(tile => tile.WaterDepth)
                    .Distinct()
                    .OrderBy(waterDepth => waterDepth)
                    .Select(waterDepth =>
                {
                    var factor = 1d + 0.4d * player.FishingLevel + 0.1d * waterDepth + baitBonus + (hasCuriosityLure ? 5d : 0d);
                    var chance = mineArea switch
                    {
                        0 or 10 => 0.02d + 0.01d * factor,
                        40 => 0.015d + 0.009d * factor,
                        80 => 0.01d + 0.008d * factor,
                        _ => 0d
                    };
                    return new
                    {
                        water_depth = waterDepth,
                        special_fish_chance = chance
                    };
                }).ToArray();
                handlers.Add(new
                {
                    handler = "mine_shaft_fishing",
                    mine_area = mineArea,
                    uses_training_rod = usesTrainingRod,
                    has_curiosity_lure = hasCuriosityLure,
                    bait_internal_name = baitName,
                    bait_display_name = bait?.DisplayName,
                    bait_qualified_item_id = bait?.QualifiedItemId,
                    bait_preserved_parent_sheet_index = bait?.preservedParentSheetIndex.Value,
                    special_fish_internal_name = specialFishInternalName,
                    specific_bait_name_condition_complete = specificBaitNameConditionComplete,
                    specific_bait_name_condition_matched = specificBaitNameConditionMatched,
                    specific_bait_name_condition_source = "decompiled MineShaft.getFish fishingRod.GetBait()?.Name.Contains(target internal name)",
                    special_fish_qualified_item_id = specialQualifiedItemId,
                    special_fish_chance_by_water_depth = specialChanceByWaterDepth,
                    silver_quality_chance = player.FishingLevel / 10f,
                    gold_quality_chance = player.FishingLevel / 50f + player.LuckLevel / 100f,
                    lava_area_cave_jelly_chance = mineArea == 80 ? 0.05d + player.LuckLevel * 0.05d : (double?)null,
                    mine_trash_item_id_range_inclusive = new[] { 167, 172 },
                    fallback = usesTrainingRod
                        ? "mine_random_trash"
                        : mineArea == 80
                            ? "cave_jelly_then_mine_random_trash"
                            : "Data/Locations:UndergroundMine"
                });
                return new LocationOverrideProjection(
                    handlers.ToArray(),
                    overrideDeclaringType == typeof(MineShaft) && specificBaitNameConditionComplete);
            }
            case IslandSouthEast southEast:
            {
                var poolTileIndices = fishableTiles
                    .Where(tile => tile.TileX >= 18 && tile.TileX <= 20 && tile.TileY >= 20 && tile.TileY <= 22)
                    .Select(tile => tileIndices[(tile.TileX, tile.TileY)])
                    .ToArray();
                handlers.Add(new
                {
                    handler = "island_southeast_stardrop_pool_walnut",
                    fishable_tile_indices = poolTileIndices,
                    already_fished = southEast.fishedWalnut.Value,
                    qualified_item_id = "(O)73",
                    eligible_before_catch = !southEast.fishedWalnut.Value,
                    multiplayer_delivery = Game1.IsMultiplayer ? "fishWalnutEvent" : "direct_item",
                    matched_pool_without_reward_returns_null = southEast.fishedWalnut.Value,
                    fallback_outside_pool = "IslandLocation.getFish"
                });
                handlers.Add(ReadIslandFishingWalnut(location));
                return new LocationOverrideProjection(handlers.ToArray(), overrideDeclaringType == typeof(IslandSouthEast));
            }
            case IslandLocation:
                handlers.Add(ReadIslandFishingWalnut(location));
                return new LocationOverrideProjection(handlers.ToArray(), overrideDeclaringType == typeof(IslandLocation));
            case Farm farm:
            {
                var raw = farm.GetMapPropertySplitBySpaces("FarmFishLocationOverride");
                var chance = 0f;
                var valid = raw.Length >= 2
                    && float.TryParse(
                        raw[1],
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out chance);
                handlers.Add(new
                {
                    handler = "farm_fish_location_override",
                    map_property_tokens = raw,
                    parse_valid = valid,
                    target_location = raw.FirstOrDefault(),
                    chance = valid ? chance : 0f,
                    chance_roll_pending = valid && chance > 0f,
                    target_rule_distribution_transparent = !valid || chance <= 0f,
                    unresolved_reason = valid && chance > 0f
                        ? "target_location_getFish_distribution_not_projected_for_farm_bobber_coordinates"
                        : null,
                    fallback = "base_getFish_current_farm"
                });
                return new LocationOverrideProjection(
                    handlers.ToArray(),
                    overrideDeclaringType == typeof(Farm) && (!valid || chance <= 0f));
            }
            default:
                return new LocationOverrideProjection(Array.Empty<object>(), false);
        }
    }

    private static object ReadIslandFishingWalnut(GameLocation location)
    {
        var dropCount = Game1.player.team.limitedNutDrops.TryGetValue("IslandFishing", out var current)
            ? current
            : 0;
        var seededRoll = Utility.CreateRandom(
            Game1.stats.DaysPlayed,
            Game1.stats.TimesFished,
            Game1.uniqueIDForThisGame).NextDouble();
        return new
        {
            handler = "island_fishing_limited_walnut",
            qualified_item_id = "(O)73",
            seeded_roll = seededRoll,
            chance = 0.15d,
            current_limited_drop_count = dropCount,
            catch_limit = 5,
            eligible_before_catch = seededRoll < 0.15d && dropCount < 5,
            multiplayer_delivery = Game1.IsMultiplayer ? "RequestLimitedNutDrops" : "direct_item",
            location_id = location.NameOrUniqueName,
            fallback = "base_getFish"
        };
    }

    private static SpawnOutputProjection ReadSpawnOutput(
        string raw,
        int outputIndex,
        SpawnFishData spawn,
        Farmer player,
        GameLocation location,
        IReadOnlyDictionary<string, string> fishData,
        IReadOnlyList<FishingTileReadRow> eligibleTiles,
        bool hasMagicBait,
        bool hasCuriosityLure,
        string? targetedFishId,
        bool usesTrainingRod,
        bool isTutorialCatch,
        int seed)
    {
        var queryParts = raw.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (queryParts.Length > 0 && string.Equals(queryParts[0], "SECRET_NOTE_OR_ITEM", StringComparison.Ordinal))
        {
            if (queryParts.Length > 1 && !string.IsNullOrWhiteSpace(queryParts[1]))
            {
                return new SpawnOutputProjection(new
                {
                    output_index = outputIndex,
                    raw,
                    resolution_complete = false,
                    resolution_status = "vanilla_secret_note_alternate_query_not_enumerated",
                    item_query_key = queryParts[0],
                    alternate_item_query = queryParts[1],
                    reason = "alternate_item_query_requires_separate_side_effect_free_resolver_audit"
                }, false);
            }

            var islandNotes = location.InIslandContext();
            var noteQualifiedItemId = islandNotes ? "(O)842" : "(O)79";
            var unlocked = location.HasUnlockedAreaSecretNotes(player);
            var unseenNoteIds = Utility.GetUnseenSecretNotes(player, islandNotes, out var totalNotes);
            var inventoryCount = player.Items.CountId(noteQualifiedItemId);
            var availableNoteCount = Math.Max(0, unseenNoteIds.Length - inventoryCount);
            var festivalEventBlocks = location.currentEvent?.isFestival == true;
            var noteChance = availableNoteCount <= 0
                ? 0f
                : GameLocation.LAST_SECRET_NOTE_CHANCE +
                  (GameLocation.FIRST_SECRET_NOTE_CHANCE - GameLocation.LAST_SECRET_NOTE_CHANCE) *
                  ((float)(availableNoteCount - 1) / Math.Max(1, totalNotes - 1));
            var eligible = unlocked && !festivalEventBlocks && availableNoteCount > 0 && !isTutorialCatch;
            return new SpawnOutputProjection(new
            {
                output_index = outputIndex,
                raw,
                resolution_complete = true,
                resolution_status = "vanilla_secret_note_or_item",
                item_query_key = queryParts[0],
                item_id = islandNotes ? "842" : "79",
                qualified_item_id = noteQualifiedItemId,
                island_journal_scrap = islandNotes,
                area_secret_notes_unlocked = unlocked,
                festival_event_blocks = festivalEventBlocks,
                unseen_note_ids = unseenNoteIds,
                total_note_count = totalNotes,
                matching_notes_in_inventory = inventoryCount,
                available_note_count = availableNoteCount,
                output_local_chance_preview = noteChance,
                output_local_chance_roll_pending = eligible,
                output_eligible_before_random_rolls = eligible,
                output_blocking_reasons = new[]
                {
                    !unlocked ? "area_secret_notes_not_unlocked" : null,
                    festivalEventBlocks ? "festival_event_blocks_secret_note" : null,
                    availableNoteCount <= 0 ? "no_unseen_secret_note_available" : null,
                    isTutorialCatch ? "secret_note_not_valid_for_tutorial_catch" : null
                }.Where(reason => reason is not null).ToArray(),
                data_fish_chance_roll_pending = false,
                data_fish_chance_by_water_depth = Array.Empty<object>()
            }, true);
        }

        var parsedItem = ItemRegistry.GetData(raw);
        if (parsedItem is null)
        {
            var queryKey = raw.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return new SpawnOutputProjection(new
            {
                output_index = outputIndex,
                raw,
                resolution_complete = false,
                resolution_status = "item_query_output_not_enumerated",
                item_query_key = queryKey,
                item_query_registered = queryKey is not null && ItemQueryResolver.ItemResolvers.ContainsKey(queryKey),
                per_item_condition = spawn.PerItemCondition,
                reason = "arbitrary_item_query_resolvers_are_not_executed_by_the_read_adapter"
            }, false);
        }

        string? rawFishData = null;
        var hasFishData = parsedItem.QualifiedItemId.StartsWith("(O)", StringComparison.Ordinal)
            && fishData.TryGetValue(parsedItem.ItemId, out rawFishData);
        var requirements = hasFishData
            ? FishingDataFishRuleParser.Parse(rawFishData!)
            : null;
        FishingDataFishEligibilityRead genericEligibility;
        if (requirements is null)
        {
            genericEligibility = new FishingDataFishEligibilityRead
            {
                EligibleBeforeRandomRoll = !isTutorialCatch,
                BlockingReasons = isTutorialCatch
                    ? new[] { "item_missing_data_fish_tutorial_entry" }
                    : Array.Empty<string>()
            };
        }
        else
        {
            genericEligibility = FishingDataFishRuleParser.Evaluate(
                requirements,
                new FishingDataFishEligibilityContext
                {
                    TimeOfDay = Game1.timeOfDay,
                    IsRaining = location.IsRainingHere(),
                    FishingLevel = player.FishingLevel,
                    HasMagicBait = hasMagicBait,
                    UsesTrainingRod = usesTrainingRod,
                    IsTutorialCatch = isTutorialCatch
                },
                spawn.CanUseTrainingRod,
                spawn.IgnoreFishDataRequirements);
        }

        var catchCount = player.fishCaught.TryGetValue(parsedItem.QualifiedItemId, out var caught)
            && caught.Length > 0
                ? caught[0]
                : 0;
        var catchLimitReached = spawn.CatchLimit > -1 && catchCount >= spawn.CatchLimit;
        var targetedByBait = spawn.ItemId == targetedFishId;
        var genericChanceByTile = requirements is not null
            && requirements.ParseStatus == "parsed"
            && !spawn.IgnoreFishDataRequirements
                ? eligibleTiles
                    .Select(tile => tile.WaterDepth)
                    .Distinct()
                    .OrderBy(waterDepth => waterDepth)
                    .Select(waterDepth => new
                {
                    water_depth = waterDepth,
                    chance_preview = CalculateDataFishChance(
                        requirements,
                        waterDepth,
                        spawn,
                        player,
                        location,
                        usesTrainingRod,
                        hasCuriosityLure,
                        targetedByBait,
                        seed ^ waterDepth)
                }).ToArray()
                : Array.Empty<object>();

        return new SpawnOutputProjection(new
        {
            output_index = outputIndex,
            raw,
            resolution_complete = true,
            resolution_status = "direct_item",
            item_id = parsedItem.ItemId,
            qualified_item_id = parsedItem.QualifiedItemId,
            internal_name = parsedItem.InternalName,
            display_name = parsedItem.DisplayName,
            category = parsedItem.Category,
            object_type = parsedItem.ObjectType,
            catch_count = catchCount,
            catch_limit_reached = catchLimitReached,
            output_eligible_before_random_rolls = genericEligibility.EligibleBeforeRandomRoll && !catchLimitReached,
            output_blocking_reasons = genericEligibility.BlockingReasons
                .Concat(catchLimitReached ? new[] { "catch_limit_reached" } : Array.Empty<string>())
                .ToArray(),
            data_fish_requirements = requirements,
            data_fish_chance_roll_pending = requirements is not null && !spawn.IgnoreFishDataRequirements,
            data_fish_chance_by_water_depth = genericChanceByTile
        }, true);
    }

    private static float? CalculateDataFishChance(
        FishingDataFishRequirementsRead requirements,
        int waterDepth,
        SpawnFishData spawn,
        Farmer player,
        GameLocation location,
        bool usesTrainingRod,
        bool hasCuriosityLure,
        bool targetedByBait,
        int seed)
    {
        if (!requirements.BaseChance.HasValue
            || !requirements.MaxDepth.HasValue
            || !requirements.DepthMultiplier.HasValue)
        {
            return null;
        }

        var chance = requirements.BaseChance.Value;
        var depthPenalty = requirements.DepthMultiplier.Value * chance;
        chance -= Math.Max(0, requirements.MaxDepth.Value - waterDepth) * depthPenalty;
        chance += player.FishingLevel / 50f;
        if (usesTrainingRod)
        {
            chance *= 1.1f;
        }
        chance = Math.Min(chance, 0.9f);
        if (chance < 0.25f && hasCuriosityLure)
        {
            chance = spawn.CuriosityLureBuff > -1f
                ? chance + spawn.CuriosityLureBuff
                : (0.25f - 0.08f) / 0.25f * chance + (0.25f - 0.08f) / 2f;
        }
        if (targetedByBait)
        {
            chance *= 1.66f;
        }
        if (spawn.ApplyDailyLuck)
        {
            chance += (float)player.DailyLuck;
        }
        if (spawn.ChanceModifiers is { Count: > 0 })
        {
            chance = Utility.ApplyQuantityModifiers(
                chance,
                spawn.ChanceModifiers,
                spawn.ChanceModifierMode,
                location,
                player,
                null,
                null,
                new Random(seed));
        }
        return chance;
    }

    private static object[] ReadQuantityModifiers(IReadOnlyList<QuantityModifier>? modifiers)
    {
        return modifiers?.Select(modifier => (object)new
        {
            id = modifier.Id,
            condition = modifier.Condition,
            modification = modifier.Modification.ToString(),
            amount = modifier.Amount,
            random_amount = modifier.RandomAmount?.ToArray() ?? Array.Empty<float>()
        }).ToArray() ?? Array.Empty<object>();
    }

    private static FishingRectangleRead? ReadRectangle(Rectangle? rectangle)
    {
        return rectangle.HasValue
            ? new FishingRectangleRead
            {
                X = rectangle.Value.X,
                Y = rectangle.Value.Y,
                Width = rectangle.Value.Width,
                Height = rectangle.Value.Height
            }
            : null;
    }

    private static int StableSeed(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var character in value)
            {
                hash ^= character;
                hash *= 16777619;
            }
            return (int)(hash & 0x7FFFFFFF);
        }
    }

    private sealed record SpawnOutputProjection(object Value, bool ResolutionComplete);

    private sealed record SpecialCatchSourcesProjection(object Value, bool Complete);

    private sealed record RodFishingContextsProjection(object[] Rows, bool Complete);

    private sealed record LocationOverrideProjection(object[] Handlers, bool Complete);

    private static object[] ReadRodInventory(Farmer player, FishingRod? currentRod)
    {
        return player.Items
            .Select((item, index) => item is FishingRod rod ? ReadRod(rod, index, ReferenceEquals(rod, currentRod)) : null)
            .Where(row => row is not null)
            .Cast<object>()
            .ToArray();
    }

    private static object ReadRod(FishingRod rod, int slotIndex, bool selected)
    {
        var bait = rod.GetBait();
        return new
        {
            slot_index = slotIndex,
            selected,
            item_id = rod.ItemId,
            qualified_item_id = rod.QualifiedItemId,
            display_name = rod.DisplayName,
            upgrade_level = rod.UpgradeLevel,
            attachment_slot_count = rod.AttachmentSlotsCount,
            can_use_bait = rod.CanUseBait(),
            can_use_tackle = rod.CanUseTackle(),
            has_magic_bait = rod.HasMagicBait(),
            has_curiosity_lure = rod.HasCuriosityLure(),
            bait = ReadAttachment(bait),
            tackle = rod.GetTackle().Where(item => item is not null).Select(ReadAttachment).ToArray(),
            in_use = selected && rod.inUse()
        };
    }

    private static object? ReadAttachment(StardewValley.Object? item)
    {
        return item is null
            ? null
            : new
            {
                item_id = item.ItemId,
                qualified_item_id = item.QualifiedItemId,
                internal_name = item.Name,
                display_name = item.DisplayName,
                stack = item.Stack,
                quality = item.Quality,
                category = item.Category,
                preserved_parent_sheet_index = item.preservedParentSheetIndex.Value
            };
    }

    private static object ReadActiveCastState(FishingRod? rod)
    {
        return new
        {
            rod_selected = rod is not null,
            in_use = rod?.inUse() == true,
            is_fishing = rod?.isFishing == true,
            is_casting = rod?.isCasting == true,
            is_timing_cast = rod?.isTimingCast == true,
            is_nibbling = rod?.isNibbling == true,
            hit = rod?.hit == true,
            is_reeling = rod?.isReeling == true,
            pulling_out_of_water = rod?.pullingOutOfWater == true,
            fish_caught = rod?.fishCaught == true,
            showing_treasure = rod?.showingTreasure == true,
            cast_direction = rod is null ? (int?)null : rod.CastDirection,
            bobber_tile_x = rod is null ? (int?)null : (int)(rod.bobber.X / Game1.tileSize),
            bobber_tile_y = rod is null ? (int?)null : (int)(rod.bobber.Y / Game1.tileSize),
            clear_water_distance = rod?.clearWaterDistance,
            casting_power = rod?.castingPower,
            time_until_bite_ms = rod?.timeUntilFishingBite,
            bite_accumulator_ms = rod?.fishingBiteAccumulator,
            nibble_accumulator_ms = rod?.fishingNibbleAccumulator,
            nibble_window_remaining_ms = rod?.timeUntilFishingNibbleDone,
            caught_qualified_item_id = rod?.whichFish?.QualifiedItemId,
            fish_size = rod?.fishSize,
            fish_quality = rod?.fishQuality,
            number_caught = rod?.numberOfFishCaught,
            treasure_caught = rod?.treasureCaught == true,
            golden_treasure = rod?.goldenTreasure == true,
            boss_fish = rod?.bossFish == true,
            record_size = rod?.recordSize == true,
            last_catch_was_junk = rod?.lastCatchWasJunk == true,
            from_fish_pond = rod?.fromFishPond == true
        };
    }
}

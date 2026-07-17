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

public sealed partial class FishingReadAdapter : ReadAdapterBase
{
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

}

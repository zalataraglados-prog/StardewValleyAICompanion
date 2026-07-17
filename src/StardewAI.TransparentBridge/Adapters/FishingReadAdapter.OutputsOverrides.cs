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

}

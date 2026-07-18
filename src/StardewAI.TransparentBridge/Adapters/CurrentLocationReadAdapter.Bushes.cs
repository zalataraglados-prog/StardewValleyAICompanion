using System.Globalization;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    private static object[] ReadLargeTerrainFeatures(GameLocation location)
    {
        return location.largeTerrainFeatures
            .OrderBy(feature => feature.Tile.Y)
            .ThenBy(feature => feature.Tile.X)
            .Select(feature => ReadLargeTerrainFeature(location, feature))
            .ToArray();
    }

    private static object ReadLargeTerrainFeature(GameLocation location, LargeTerrainFeature feature)
    {
        if (feature is not Bush bush)
        {
            var bounds = feature.getBoundingBox();
            return new
            {
                tile_x = (int)feature.Tile.X,
                tile_y = (int)feature.Tile.Y,
                runtime_type = feature.GetType().FullName,
                bounding_tile_width = bounds.Width / Game1.tileSize,
                bounding_tile_height = bounds.Height / Game1.tileSize,
                is_bush = false
            };
        }

        return ReadBush(location, bush);
    }

    private static object ReadBush(GameLocation location, Bush bush)
    {
        var exactVanillaType = bush.GetType() == typeof(Bush);
        var bounds = bush.getBoundingBox();
        var size = bush.size.Value;
        var branch = size switch
        {
            3 => "tea_leaf",
            4 => "golden_walnut",
            _ => "ordinary_berry"
        };
        var outputId = exactVanillaType ? bush.GetShakeOffItem() ?? string.Empty : string.Empty;
        var quantity = size is 3 or 4 ? 1 : 1 + Game1.player.ForagingLevel / 4;
        var quality = size is 3 or 4 ? 0 : Game1.player.professions.Contains(16) ? 4 : 0;
        var foragingExperience = size is 3 or 4 ? 0 : quantity;
        var nutKey = size == 4
            ? "Bush_" + location.Name + "_" + bush.Tile.X.ToString(CultureInfo.CurrentCulture) + "_" + bush.Tile.Y.ToString(CultureInfo.CurrentCulture)
            : string.Empty;
        var nutCollected = size == 4 && Game1.player.team.collectedNutTracker.Contains(nutKey);
        var status = !exactVanillaType
            ? "custom_bush_runtime_type"
            : bush.townBush.Value
                ? "town_bush_not_harvestable"
                : !bush.readyForHarvest()
                    ? "bush_not_ready"
                    : !bush.inBloom()
                        ? "bush_not_in_bloom"
                        : bush.shakeTimer > 0f
                            ? "bush_shake_cooldown_active"
                            : string.IsNullOrWhiteSpace(outputId)
                                ? "bush_output_identity_unavailable"
                                : nutCollected
                                    ? "golden_walnut_already_collected"
                                    : "ready";

        return new
        {
            tile_x = (int)bush.Tile.X,
            tile_y = (int)bush.Tile.Y,
            runtime_type = bush.GetType().FullName,
            bounding_tile_width = bounds.Width / Game1.tileSize,
            bounding_tile_height = bounds.Height / Game1.tileSize,
            is_bush = true,
            bush_size = size,
            bush_kind = branch,
            date_planted = bush.datePlanted.Value,
            age_days = bush.getAge(),
            town_bush = bush.townBush.Value,
            in_pot = bush.inPot.Value,
            is_sheltered = bush.IsSheltered(),
            ready_for_harvest = bush.readyForHarvest(),
            in_bloom = bush.inBloom(),
            shake_timer = bush.shakeTimer,
            tile_sheet_offset_before = bush.tileSheetOffset.Value,
            tile_sheet_offset_expected_after = 0,
            bush_harvest_status = status,
            bush_projection_status = exactVanillaType ? "exact_from_native_bush_shake" : "unsupported_custom_runtime_type",
            bush_output_qualified_item_id = outputId,
            bush_output_quantity_min = quantity,
            bush_output_quantity_max = quantity,
            bush_output_quality = quality,
            bush_foraging_experience_on_success_min = foragingExperience,
            bush_foraging_experience_on_success_max = foragingExperience,
            bush_nut_key = nutKey,
            bush_nut_collected_before = nutCollected,
            bush_nut_collected_expected_after = size == 4
        };
    }
}

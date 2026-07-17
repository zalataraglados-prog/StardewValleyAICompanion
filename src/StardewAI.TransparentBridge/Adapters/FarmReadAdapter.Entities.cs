using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Machines;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewAI.TransparentBridge.State;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter : ReadAdapterBase
{
    private static object[] ReadChests(Farm farm)
    {
        return farm.objects.Pairs
            .Where(pair => pair.Value is Chest)
            .Select(pair =>
            {
                var chest = (Chest)pair.Value;
                return new
                {
                    tile_x = (int)pair.Key.X,
                    tile_y = (int)pair.Key.Y,
                    qualified_item_id = chest.QualifiedItemId,
                    display_name = chest.DisplayName,
                    special_chest_type = chest.SpecialChestType.ToString(),
                    item_count = chest.Items.Count,
                    items = chest.Items
                        .Select((item, index) => new
                        {
                            slot_index = index,
                            item = SummarizeItem(item)
                        })
                        .ToArray()
                };
            })
            .OrderBy(chest => chest.tile_y)
            .ThenBy(chest => chest.tile_x)
            .ToArray();
    }

    private static object[] ReadAnimals(Farm farm)
    {
        return farm.animals.Pairs
            .Select(pair => new
            {
                animal_id = pair.Key,
                name = pair.Value.Name,
                display_name = pair.Value.displayName,
                type = pair.Value.type.Value,
                building_type_i_live_in = pair.Value.buildingTypeILiveIn.Value,
                age = pair.Value.age.Value,
                friendship_toward_farmer = pair.Value.friendshipTowardFarmer.Value,
                produce_quality = pair.Value.produceQuality.Value,
                tile_x = pair.Value.TilePoint.X,
                tile_y = pair.Value.TilePoint.Y
            })
            .OrderBy(animal => animal.animal_id)
            .ToArray();
    }

    private static object[] ReadResourceClumps(Farm farm)
    {
        return farm.resourceClumps
            .Select(clump =>
            {
                var experience = ReadGiantCropExperience(clump, Game1.player);
                return new
                {
                    tile_x = (int)clump.Tile.X,
                    tile_y = (int)clump.Tile.Y,
                    runtime_type = clump.GetType().FullName,
                    parent_sheet_index = clump.parentSheetIndex.Value,
                    width = clump.width.Value,
                    height = clump.height.Value,
                    health = clump.health.Value,
                    is_giant_crop = clump is GiantCrop,
                    giant_crop_id = clump is GiantCrop giant ? giant.Id : string.Empty,
                    required_tool = clump is GiantCrop ? "axe" : string.Empty,
                    executor_status = clump is GiantCrop ? "runtime_verified" : string.Empty,
                    harvest_experience_skill_id = experience.SkillId,
                    harvest_experience_skill_index = experience.SkillIndex,
                    harvest_experience_on_success_min = experience.Minimum,
                    harvest_experience_on_success_max = experience.Maximum,
                    harvest_experience_condition = experience.Condition,
                    harvest_experience_projection_status = experience.Status
                };
            })
            .OrderBy(clump => clump.tile_y)
            .ThenBy(clump => clump.tile_x)
            .ToArray();
    }

    private static object[] ReadDebris(Farm farm)
    {
        return farm.debris
            .Select((debris, index) => new
            {
                debris_index = index,
                debris_type = debris.debrisType.Value.ToString(),
                chunk_type = debris.chunkType.Value,
                item_id = debris.itemId.Value,
                qualified_item_id = debris.item?.QualifiedItemId ?? debris.itemId.Value,
                item_quality = debris.itemQuality,
                item = SummarizeItem(debris.item),
                chunk_count = debris.Chunks.Count,
                chunk_final_y_level = debris.chunkFinalYLevel,
                chunk_final_y_target = debris.chunkFinalYTarget,
                time_since_done_bouncing = debris.timeSinceDoneBouncing,
                is_sinking = debris.isSinking.Value,
                is_essential_item = debris.isEssentialItem(),
                pickup_executor_status = "covered_for_runtime_collect",
                chunks = debris.Chunks
                    .Select((chunk, chunkIndex) => new
                    {
                        chunk_index = chunkIndex,
                        pixel_x = (int)MathF.Round(chunk.position.X),
                        pixel_y = (int)MathF.Round(chunk.position.Y),
                        tile_x = (int)((chunk.position.X + 32f) / Game1.tileSize),
                        tile_y = (int)((chunk.position.Y + 32f) / Game1.tileSize),
                        x_velocity = chunk.xVelocity.Value,
                        y_velocity = chunk.yVelocity.Value,
                        bounces = chunk.bounces,
                        has_passed_resting_line_once = chunk.hasPassedRestingLineOnce.Value,
                        alpha = chunk.alpha
                    })
                    .ToArray()
            })
            .ToArray();
    }

    private static object[] ReadWarps(Farm farm)
    {
        return farm.warps
            .Select(warp => new
            {
                x = warp.X,
                y = warp.Y,
                target_name = warp.TargetName,
                target_x = warp.TargetX,
                target_y = warp.TargetY
            })
            .OrderBy(warp => warp.y)
            .ThenBy(warp => warp.x)
            .ToArray();
    }

}

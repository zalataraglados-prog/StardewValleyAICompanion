using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Machines;
using StardewValley.GameData.FarmAnimals;
using StardewValley.Objects;
using StardewValley.Tools;
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
        var locations = Game1.locations
            .Concat(farm.buildings.Select(building => building.GetIndoors()).Where(location => location is not null)!)
            .Append(farm)
            .Distinct()
            .ToArray();
        var animals = locations
            .SelectMany(location => location.animals.Pairs.Select(pair => new
            {
                AnimalId = pair.Key,
                Animal = pair.Value,
                LocationId = location.NameOrUniqueName
            }))
            .GroupBy(entry => entry.AnimalId)
            .Select(group => group.First())
            .ToArray();

        return animals
            .OrderBy(entry => entry.AnimalId)
            .Select(entry => ReadAnimal(entry.AnimalId, entry.Animal, entry.LocationId, Game1.player))
            .ToArray();
    }

    private static object ReadAnimal(long animalId, FarmAnimal animal, string locationId, Farmer player)
    {
        var data = animal.GetAnimalData();
        var harvestTool = data?.HarvestTool ?? string.Empty;
        var toolSlot = player.Items
            .Select((item, index) => new { item, index })
            .FirstOrDefault(entry => entry.item is Tool tool && string.Equals(tool.Name, harvestTool, StringComparison.Ordinal))
            ?.index ?? -1;
        var currentProduce = animal.currentProduce.Value ?? string.Empty;
        var output = string.IsNullOrWhiteSpace(currentProduce)
            ? null
            : ItemRegistry.Create<StardewValley.Object>("(O)" + currentProduce);
        if (output is not null)
        {
            output.CanBeSetDown = false;
            output.Quality = animal.produceQuality.Value;
            output.Stack = animal.hasEatenAnimalCracker.Value ? 2 : 1;
            output.HasBeenInInventory = true;
        }
        var outputProjection = output is null ? null : ClearanceOutputItemProjection.From(output);
        var outputItemsJson = outputProjection is null
            ? string.Empty
            : System.Text.Json.JsonSerializer.Serialize(new[] { outputProjection });
        var inventoryAcceptsOutput = output is not null && player.couldInventoryAcceptThisItem(output);
        var statIncrements = output is null || data?.StatToIncrementOnProduce is null
            ? Array.Empty<object>()
            : data.StatToIncrementOnProduce
                .Where(stat => (stat.RequiredItemId is null || ItemRegistry.HasItemId(output, stat.RequiredItemId)) &&
                    (stat.RequiredTags is null || stat.RequiredTags.Count == 0 || ItemContextTagManager.DoAllTagsMatch(stat.RequiredTags, output.GetContextTags())))
                .Select(stat => (object)new
                {
                    stat_name = stat.StatName,
                    amount = output.Stack,
                    before = Game1.stats.Get(stat.StatName),
                    after = Game1.stats.Get(stat.StatName) + (uint)output.Stack
                })
                .ToArray();
        var adult = animal.isAdult();
        var supportedTool = harvestTool is "Milk Pail" or "Shears";
        var harvestStatus = data is null
            ? "animal_data_unavailable"
            : data.HarvestType != FarmAnimalHarvestType.HarvestWithTool
                ? "animal_produce_not_tool_harvested"
                : !supportedTool
                    ? "unsupported_animal_harvest_tool"
                    : string.IsNullOrWhiteSpace(currentProduce)
                        ? "animal_produce_not_ready"
                        : !adult
                            ? "animal_not_adult"
                            : toolSlot < 0
                                ? "animal_harvest_tool_missing"
                                : !inventoryAcceptsOutput
                                    ? "animal_product_inventory_cannot_accept_output"
                                    : "ready";

        return new
        {
            animal_id = animalId,
            runtime_type = animal.GetType().FullName,
            location_id = locationId,
            name = animal.Name,
            display_name = animal.displayName,
            type = animal.type.Value,
            owner_id = animal.ownerID.Value,
            building_type_i_live_in = animal.buildingTypeILiveIn.Value,
            age = animal.age.Value,
            days_to_mature = data?.DaysToMature,
            is_adult = adult,
            friendship_toward_farmer = animal.friendshipTowardFarmer.Value,
            friendship_after_harvest = Math.Min(1000, animal.friendshipTowardFarmer.Value + 5),
            produce_quality = animal.produceQuality.Value,
            current_produce_item_id = currentProduce,
            current_produce_qualified_item_id = output?.QualifiedItemId ?? string.Empty,
            has_eaten_animal_cracker = animal.hasEatenAnimalCracker.Value,
            harvest_type = data?.HarvestType.ToString() ?? string.Empty,
            harvest_tool = harvestTool,
            harvest_tool_runtime_type = toolSlot >= 0 ? player.Items[toolSlot]?.GetType().FullName ?? string.Empty : string.Empty,
            harvest_tool_slot_index = toolSlot,
            harvest_status = harvestStatus,
            inventory_accepts_harvest_output = inventoryAcceptsOutput,
            harvest_output_runtime_type = outputProjection?.RuntimeType ?? string.Empty,
            harvest_output_qualified_item_id = outputProjection?.QualifiedItemId ?? string.Empty,
            harvest_output_quality = outputProjection?.Quality ?? 0,
            harvest_output_quantity = outputProjection?.Quantity ?? 0,
            harvest_output_unit_state_sha256 = outputProjection?.UnitStateSha256 ?? string.Empty,
            harvest_expected_output_items_json = outputItemsJson,
            harvest_stat_increments_json = System.Text.Json.JsonSerializer.Serialize(statIncrements),
            harvest_energy_cost = 4,
            harvest_farming_experience_delta = 5,
            harvest_friendship_delta = 5,
            harvest_projection_status = outputProjection is null ? "unavailable" : "exact",
            tile_x = animal.TilePoint.X,
            tile_y = animal.TilePoint.Y
        };
    }

    private static object[] ReadResourceClumps(Farm farm)
    {
        return farm.resourceClumps
            .Select(clump =>
            {
                var experience = clump is GiantCrop
                    ? ReadGiantCropExperience(clump, Game1.player)
                    : ReadFarmResourceClumpExperience(clump);
                var clearance = ReadFarmResourceClumpClearance(clump, Game1.player);
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
                    clear_kind = clearance.ClearKind,
                    minimum_tool_upgrade_level = clearance.MinimumToolUpgradeLevel,
                    tool_slot_index = clearance.ToolSlotIndex,
                    tool_upgrade_level = clearance.ToolUpgradeLevel,
                    expected_tool_hits_to_clear = clearance.ExpectedToolHits,
                    clear_obstacle_executor_status = clearance.Status,
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

using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    private static object ReadFruitTreeDetails(Vector2 tile, FruitTree tree)
    {
        var exactVanillaType = tree.GetType() == typeof(FruitTree);
        var quality = tree.GetQuality();
        var lightning = tree.struckByLightningCountdown.Value > 0;
        var outputs = tree.fruit
            .Where(item => item is not null)
            .Select(item => new FruitTreeProjectedOutput(
                lightning ? "(O)382" : item.QualifiedItemId,
                quality,
                lightning ? 1 : Math.Max(1, item.Stack)))
            .GroupBy(output => new { output.QualifiedItemId, output.Quality })
            .Select(group => new
            {
                qualified_item_id = group.Key.QualifiedItemId,
                quality = group.Key.Quality,
                quantity = group.Sum(output => output.Quantity)
            })
            .OrderBy(output => output.qualified_item_id, StringComparer.Ordinal)
            .ThenBy(output => output.quality)
            .ToArray();
        var outputQuantity = outputs.Sum(output => output.quantity);
        var status = !exactVanillaType
            ? "custom_fruit_tree_runtime_type"
            : tree.stump.Value
                ? "fruit_tree_is_stump"
                : tree.growthStage.Value < FruitTree.treeStage
                    ? "fruit_tree_not_mature"
                    : tree.fruit.Count == 0
                        ? "fruit_tree_has_no_fruit"
                        : tree.fruit.Any(item => item is null)
                            ? "fruit_tree_contains_transient_null_fruit"
                            : tree.maxShake != 0f
                                ? "fruit_tree_shake_in_progress"
                                : outputQuantity <= 0
                                    ? "fruit_tree_output_projection_unavailable"
                                    : "ready";

        return new
        {
            tile_x = (int)tile.X,
            tile_y = (int)tile.Y,
            type = tree.GetType().FullName,
            runtime_type = tree.GetType().FullName,
            is_fruit_tree = true,
            fruit_tree_id = tree.treeId.Value,
            growth_stage = tree.growthStage.Value,
            days_until_mature = tree.daysUntilMature.Value,
            health = tree.health.Value,
            stump = tree.stump.Value,
            falling = tree.falling.Value,
            destroy_pending = tree.destroy,
            fruit_count = tree.fruit.Count,
            fruit_capacity = FruitTree.maxFruitsOnTrees,
            fruits = tree.fruit.Select((item, index) => new
            {
                fruit_index = index,
                item = SummarizeItem(item),
                qualified_item_id = item?.QualifiedItemId ?? string.Empty,
                stack = item?.Stack ?? 0,
                stored_quality = item?.Quality,
                expected_drop_qualified_item_id = lightning ? "(O)382" : item?.QualifiedItemId ?? string.Empty,
                expected_drop_quality = quality,
                expected_drop_quantity = item is null ? 0 : lightning ? 1 : Math.Max(1, item.Stack)
            }).ToArray(),
            struck_by_lightning_countdown = tree.struckByLightningCountdown.Value,
            struck_by_lightning = lightning,
            ignores_seasons_here = tree.IgnoresSeasonsHere(),
            is_in_season_here = tree.IsInSeasonHere(),
            is_winter_tree_here = tree.IsWinterTreeHere(),
            greenhouse_tile_tree = tree.GreenHouseTileTree,
            max_shake = tree.maxShake,
            shake_timer = tree.shakeTimer,
            fruit_tree_harvest_status = status,
            fruit_tree_projection_status = exactVanillaType
                ? "exact_from_native_fruit_tree_performUseAction_and_shake"
                : "unsupported_custom_runtime_type",
            fruit_tree_expected_outputs = outputs,
            fruit_tree_expected_outputs_json_contract = "array grouped by qualified_item_id and quality with exact quantity",
            fruit_tree_expected_output_quantity_total = outputQuantity,
            fruit_tree_expected_fruit_count_after = 0,
            fruit_tree_expected_foraging_experience_delta = 0,
            fruit_tree_output_delivery = "native_world_debris_then_automatic_pickup_possible",
            fruit_tree_native_contract = "GameLocation.checkAction -> FruitTree.performUseAction -> FruitTree.shake; no direct fruit, debris, inventory, or skill mutation",
            source = "FruitTree live net/item fields; FruitTree.GetQuality/performUseAction/shake decompiled vanilla 1.6.15"
        };
    }

    private sealed record FruitTreeProjectedOutput(
        string QualifiedItemId,
        int Quality,
        int Quantity);
}

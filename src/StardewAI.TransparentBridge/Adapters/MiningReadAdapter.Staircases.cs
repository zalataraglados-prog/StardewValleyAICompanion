using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class MiningReadAdapter
{
    private static object ReadStaircasePlacement(
        MineShaft mine,
        xTile.Map? loadedMap)
    {
        var allowed = mine.shouldCreateLadderOnThisLevel();
        var staircaseCount = CountItem(Game1.player, "(BC)71");
        if (!allowed || staircaseCount <= 0 || loadedMap is null ||
            loadedMap.Layers.Count == 0)
        {
            return new
            {
                status = allowed
                    ? staircaseCount > 0
                        ? "unavailable_loaded_map_field_null"
                        : "unavailable_no_staircase_inventory"
                    : "blocked_native_floor_rule",
                native_floor_rule_allows = allowed,
                staircase_count = staircaseCount,
                qualified_item_id = "(BC)71",
                candidates = Array.Empty<object>(),
                projection_status =
                    "exact_native_direct_tile_subset_no_recursive_relocation",
                source =
                    "MineShaft.shouldCreateLadderOnThisLevel; Object.placementAction; MineShaft.recursiveTryToCreateLadderDown"
            };
        }

        var layer = loadedMap.Layers[0];
        var candidates = new List<object>();
        for (var y = 0; y < layer.LayerHeight; y++)
        {
            for (var x = 0; x < layer.LayerWidth; x++)
            {
                var tile = new Vector2(x, y);
                if (mine.IsTileOccupiedBy(tile) ||
                    !mine.isTileOnClearAndSolidGround(tile) ||
                    !string.Equals(
                        mine.doesTileHaveProperty(x, y, "Type", "Back"),
                        "Stone",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                candidates.Add(new
                {
                    target_tile_x = x,
                    target_tile_y = y,
                    expected_ladder_tile_x = x,
                    expected_ladder_tile_y = y,
                    native_search_iteration = 1,
                    target_rule_status =
                        "exact_first_recursive_candidate_direct_tile",
                    source =
                        "MineShaft.recursiveTryToCreateLadderDown first dequeue branch"
                });
            }
        }

        return new
        {
            status = candidates.Count > 0
                ? "available"
                : "blocked_no_direct_native_tile",
            native_floor_rule_allows = true,
            staircase_count = staircaseCount,
            qualified_item_id = "(BC)71",
            candidates = candidates.ToArray(),
            native_recursive_search_max_iterations = 16,
            projection_status =
                "exact_native_direct_tile_subset_no_recursive_relocation",
            source =
                "MineShaft.shouldCreateLadderOnThisLevel; Object.placementAction; MineShaft.recursiveTryToCreateLadderDown"
        };
    }
}

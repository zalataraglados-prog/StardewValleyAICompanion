using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static bool StaticNativePlacementCompatible(
        StardewValley.Object probe,
        GameLocation location,
        Vector2 tile)
    {
        if (!probe.canBePlacedHere(
                location,
                tile,
                ~(CollisionMask.Characters | CollisionMask.Farmers)))
        {
            return false;
        }

        if (!probe.IsTapper())
            return true;

        return location.terrainFeatures.TryGetValue(tile, out var feature) &&
            feature is Tree tree &&
            tree.growthStage.Value >= Tree.treeStage &&
            !tree.stump.Value &&
            !location.objects.ContainsKey(tile) &&
            (!tree.isTemporaryGreenRainTree.Value ||
             location.GetSeason() != Season.Summer) &&
            tree.GetData()?.CanBeTapped() == true;
    }

    private static object[] ReadNativeTapperPlacementTargets(
        StardewValley.Object probe,
        GameLocation location)
    {
        if (!probe.IsTapper())
            return Array.Empty<object>();

        return location.terrainFeatures.Pairs
            .Where(pair => pair.Value is Tree &&
                StaticNativePlacementCompatible(
                    probe,
                    location,
                    pair.Key))
            .OrderBy(pair => pair.Key.Y)
            .ThenBy(pair => pair.Key.X)
            .Select(pair => ReadNativeTapperPlacementTarget(
                pair.Key,
                (Tree)pair.Value))
            .ToArray();
    }

    private static object ReadNativeTapperPlacementTarget(
        Vector2 tile,
        Tree tree)
    {
        var data = tree.GetData();
        var outputRows = data?.TapItems?
            .Select((row, index) => new
            {
                source_id = "wild_tree:" + tree.treeType.Value + ":" + index,
                item_qualified_ids = TapperLiteralObjectIds(row.ItemId)
                    .Concat(row.RandomItemId is null
                        ? Array.Empty<string>()
                        : row.RandomItemId.SelectMany(TapperLiteralObjectIds))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                days_until_ready = row.DaysUntilReady,
                condition = row.Condition ?? string.Empty,
                previous_item_ids = row.PreviousItemId?.ToArray() ??
                    Array.Empty<string>(),
                season = row.Season?.ToString() ?? string.Empty,
                days_until_ready_modifier_count =
                    row.DaysUntilReadyModifiers?.Count ?? 0
            })
            .ToArray() ?? Array.Empty<object>();
        return new
        {
            tile_x = (int)tile.X,
            tile_y = (int)tile.Y,
            tree_type = tree.treeType.Value,
            growth_stage = tree.growthStage.Value,
            stump = tree.stump.Value,
            tapped = tree.tapped.Value,
            is_temporary_green_rain_tree =
                tree.isTemporaryGreenRainTree.Value,
            can_be_tapped = data?.CanBeTapped() == true,
            output_rows = outputRows,
            projection_status =
                "native_object_placement_action_compatible_tapper_target"
        };
    }

    private static string[] TapperLiteralObjectIds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        return raw.Split(
                '|',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Select(value => value.StartsWith("(O)", StringComparison.Ordinal)
                ? value
                : int.TryParse(value, out _)
                    ? "(O)" + value
                    : string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }
}

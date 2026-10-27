using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private static object ReadWildTreeTapperProductionContext(
        GameLocation location,
        Vector2 tile,
        StardewValley.Object machine)
    {
        if (!machine.IsTapper())
        {
            return new
            {
                projection_status = "not_applicable_not_tapper"
            };
        }
        if (!location.terrainFeatures.TryGetValue(tile, out var feature) ||
            feature is not Tree tree ||
            tree.GetType() != typeof(Tree) ||
            !tree.tapped.Value)
        {
            return new
            {
                projection_status =
                    "blocked_live_same_tile_tapped_base_tree_unavailable"
            };
        }

        var data = tree.GetData();
        var pineRows = data?.TapItems?
            .Select((row, index) => new { Row = row, Index = index })
            .Where(value =>
                TapperLiteralObjectIds(value.Row.ItemId)
                    .Concat(value.Row.RandomItemId is null
                        ? Array.Empty<string>()
                        : value.Row.RandomItemId.SelectMany(
                            TapperLiteralObjectIds))
                    .Contains("(O)726", StringComparer.Ordinal))
            .ToArray();
        var exactPineCycle =
            string.Equals(tree.treeType.Value, "3", StringComparison.Ordinal) &&
            data?.CanBeTapped() == true &&
            pineRows is { Length: 1 } &&
            pineRows[0].Index == 0 &&
            pineRows[0].Row.DaysUntilReady == 5 &&
            string.IsNullOrWhiteSpace(pineRows[0].Row.Condition) &&
            (pineRows[0].Row.PreviousItemId is null ||
             pineRows[0].Row.PreviousItemId.Count == 0) &&
            pineRows[0].Row.Season is null &&
            (pineRows[0].Row.DaysUntilReadyModifiers is null ||
             pineRows[0].Row.DaysUntilReadyModifiers.Count == 0);
        var output = machine.heldObject.Value;
        return new
        {
            projection_status = exactPineCycle
                ? "exact_locked_base_pine_tapper_cycle"
                : "transparent_non_pine_or_dynamic_tapper_cycle",
            tree_type = tree.treeType.Value,
            tree_growth_stage = tree.growthStage.Value,
            tree_stump = tree.stump.Value,
            tree_tapped = tree.tapped.Value,
            machine_qualified_item_id = machine.QualifiedItemId,
            current_output_qualified_item_id =
                output?.QualifiedItemId ?? string.Empty,
            current_output_stack = output?.Stack ?? 0,
            ready_for_harvest = machine.readyForHarvest.Value,
            minutes_until_ready = machine.MinutesUntilReady,
            exact_output_qualified_item_id = exactPineCycle
                ? "(O)726"
                : string.Empty,
            exact_base_cycle_days = exactPineCycle ? 5 : 0,
            source_id = exactPineCycle
                ? "wild_tree:3:0"
                : string.Empty,
            native_contract =
                "Tree.UpdateTapperProduct->TryGetTapperOutput->Utility.CalculateMinutesUntilMorning"
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

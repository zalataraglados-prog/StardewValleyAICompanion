using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    internal static JsonObject ReadTerminalShippingBin(SnapshotEnvelope source, CropTeacherTerminalShippingTarget target)
    {
        var bins = Array(Value(Field(source, "farm", "shipping_bins")!), "terminal shipping bins").OfType<JsonObject>().Where(row =>
            Int(row, "tile_x") == target.BinTileX && Int(row, "tile_y") == target.BinTileY).ToArray();
        Require(bins is [var bin] && bin["completed"]?.GetValue<bool>() == true &&
                bin["bin_scope"]?.GetValue<string>() == target.NativeVariants?.BinScope &&
                bin["player_id"]?.ToJsonString().Trim('"') == target.PlayerId,
            "Existing shipping bank identity/custody differs.");
        VerifyShippingBinTotals(bins[0]);
        return bins[0];
    }

    internal static void VerifyTerminalShippingContents(SnapshotEnvelope initial, SnapshotEnvelope current,
        CropTeacherTerminalShippingTarget target, int count)
    {
        var expected = ReadTerminalShippingBin(initial, target)["contents"]!.DeepClone().AsArray();
        var actual = ReadTerminalShippingBin(current, target)["contents"]!.AsArray();
        var inventory = Rows(Value(Field(initial, "player", "inventory")!));
        // Extend the pinned original variants by only this crop's accepted deposits, including same-item prior stock.
        for (var index = 0; index < count; index++)
        {
            var selected = target.VariantAt(index);
            var item = inventory[selected.SlotIndex];
            var itemId = item["item_id"]!.GetValue<string>();
            var row = expected.OfType<JsonObject>().SingleOrDefault(row => row["qualified_item_id"]?.GetValue<string>() == target.QualifiedItemId &&
                Int(row, "quality") == selected.Quality && Int(row, "unit_sell_to_store_price") == selected.UnitPrice);
            if (row is null)
            {
                row = new JsonObject { ["item_id"] = itemId, ["qualified_item_id"] = target.QualifiedItemId,
                    ["quality"] = selected.Quality, ["unit_sell_to_store_price"] = selected.UnitPrice,
                    ["count"] = 0, ["total_sell_to_store_price"] = 0L };
                expected.Add(row);
            }
            Require(row["item_id"]?.GetValue<string>() == itemId, "Existing shipping item identity changed.");
            row["count"] = checked(Int(row, "count") + 1);
            row["total_sell_to_store_price"] = checked(ShippingLong(row, "total_sell_to_store_price") + selected.UnitPrice);
        }
        Require(JsonNode.DeepEquals(new JsonArray(OrderedShippingContents(expected).Select(row => (JsonNode)row.DeepClone()).ToArray()),
                new JsonArray(OrderedShippingContents(actual).Select(row => (JsonNode)row.DeepClone()).ToArray())),
            "Existing shipping contents differ from the pinned baseline plus accepted crop units.");
    }
}

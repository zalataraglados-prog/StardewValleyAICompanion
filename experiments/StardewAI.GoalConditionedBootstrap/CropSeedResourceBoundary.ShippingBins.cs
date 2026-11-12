using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    internal static void VerifyShippingBinsAndPersistentState(SnapshotEnvelope before, SnapshotEnvelope after,
        NativeShippingDepositEvidence deposit)
    {
        var expected = Field(before, "farm", "shipping_bins")!;
        var actual = Field(after, "farm", "shipping_bins")!;
        var bins = Array(Value(expected), "shipping bins");
        Require(bins.All(row => row is JsonObject) && bins.OfType<JsonObject>()
                .Select(row => (Int(row, "tile_x"), Int(row, "tile_y"))).Distinct().Count() == bins.Count,
            "Shipping physical bin identities are invalid.");
        var selected = bins.OfType<JsonObject>().Single(row => Int(row, "tile_x") == deposit.BinTileX && Int(row, "tile_y") == deposit.BinTileY);
        var scope = selected["bin_scope"]?.GetValue<string>();
        Require(scope is "shared" or "personal", "Shipping bank scope is unavailable.");
        string Actor(JsonObject bin) => bin["player_id"]?.ToJsonString().Trim('"') ?? string.Empty;
        Require(Actor(selected) == before.PlayerId.Value, "Shipping bank actor differs from the snapshot.");
        var echoes = bins.OfType<JsonObject>().Where(bin => bin["bin_scope"]?.GetValue<string>() == scope && Actor(bin) == Actor(selected)).ToArray();
        var originalContents = selected["contents"]!.DeepClone();
        var rawItem = Rows(Value(Field(before, "player", "inventory")!))[deposit.SlotIndex!.Value];
        var itemId = rawItem["item_id"]?.GetValue<string>() ?? throw new InvalidDataException("Shipping source item id missing.");
        foreach (var bin in echoes)
        {
            VerifyShippingBinTotals(bin);
            Require(JsonNode.DeepEquals(originalContents, bin["contents"]) &&
                    ShippingLong(bin, "pending_settlement_value") == deposit.BeforePendingSettlementValue,
                "Shipping physical endpoints disagree about their shared inventory bank.");
            var contents = Array(bin["contents"], "shipping contents");
            var matches = contents.OfType<JsonObject>().Where(row => row["qualified_item_id"]?.GetValue<string>() == deposit.QualifiedItemId &&
                Int(row, "quality") == deposit.Quality && Int(row, "unit_sell_to_store_price") == deposit.UnitPrice).ToArray();
            Require(matches.Length <= 1, "Shipping selected variant is duplicated.");
            JsonObject variant;
            if (matches is [var prior])
            {
                variant = prior;
                Require(variant["item_id"]?.GetValue<string>() == itemId, "Shipping selected variant item identity drifted.");
                variant["count"] = checked(Int(variant, "count") + 1);
                variant["total_sell_to_store_price"] = checked(ShippingLong(variant, "total_sell_to_store_price") + deposit.UnitPrice!.Value);
            }
            else
            {
                variant = new JsonObject { ["item_id"] = itemId, ["qualified_item_id"] = deposit.QualifiedItemId,
                    ["quality"] = deposit.Quality!.Value, ["count"] = 1, ["unit_sell_to_store_price"] = deposit.UnitPrice!.Value,
                    ["total_sell_to_store_price"] = deposit.UnitPrice.Value };
                contents.Add(variant);
            }
            bin["contents"] = new JsonArray(OrderedShippingContents(contents).Select(row => (JsonNode)row.DeepClone()).ToArray());
            bin["contents_total_count"] = checked(Int(bin, "contents_total_count") + 1);
            bin["contents_distinct_item_count"] = contents.OfType<JsonObject>().Select(row => row["qualified_item_id"]!.GetValue<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase).Count();
            bin["contents_distinct_settlement_variant_count"] = contents.Count;
            bin["pending_settlement_value"] = checked(ShippingLong(bin, "pending_settlement_value") + deposit.UnitPrice!.Value);
            bin["contents_signature"] = ShippingContentsSignature(Array(bin["contents"], "projected shipping contents"));
            Require(ShippingLong(bin, "pending_settlement_value") == deposit.AfterPendingSettlementValue,
                "Shipping bank value differs from verified native evidence.");
        }
        // Preserve the original contract's route-derived bin geometry normalization, and nothing more.
        var priorProjection = FullShipmentRecoverySettlementBoundaryProjector.ProjectExpectedResourceEnvelope("farm", "shipping_bins", JsonSerializer.SerializeToElement(expected));
        var nextProjection = FullShipmentRecoverySettlementBoundaryProjector.ProjectExpectedResourceEnvelope("farm", "shipping_bins", JsonSerializer.SerializeToElement(actual));
        Require(SnapshotHash.CanonicalizeSemantic(priorProjection) == SnapshotHash.CanonicalizeSemantic(nextProjection),
            "Shipping changed unrelated bin contents, bank identities, persistent geometry or metadata.");

        // Only the independently proved bin value is rebound in a comparison view; retain its after envelope.
        var comparisonField = Field(after, "farm", "shipping_bins")!;
        comparisonField["value"] = Value(Field(before, "farm", "shipping_bins")!).DeepClone();
        var farm = JsonNode.Parse(after.State["farm"].GetRawText())!.AsObject();
        farm["shipping_bins"] = comparisonField;
        var view = new SnapshotEnvelope { SaveId = after.SaveId, PlayerId = after.PlayerId, GameVersion = after.GameVersion,
            State = new Dictionary<string, JsonElement>(after.State, StringComparer.Ordinal) };
        view.State["farm"] = JsonSerializer.SerializeToElement(farm);
        Require(FullShipmentRecoverySettlementBoundaryProjector.TryProjectExpectedResourceMutationInvariant(before, out var first) &&
                FullShipmentRecoverySettlementBoundaryProjector.TryProjectExpectedResourceMutationInvariant(view, out var next) && first == next,
            "Shipping changed unrelated persistent resources/state.");
    }

    private static IEnumerable<JsonObject> OrderedShippingContents(JsonArray contents) => contents.OfType<JsonObject>()
        .OrderBy(row => row["qualified_item_id"]!.GetValue<string>(), StringComparer.Ordinal)
        .ThenBy(row => Int(row, "quality")).ThenBy(row => Int(row, "unit_sell_to_store_price"))
        .ThenBy(row => row["item_id"]!.GetValue<string>(), StringComparer.Ordinal);

    private static void VerifyShippingBinTotals(JsonObject bin)
    {
        var contents = Array(bin["contents"], "shipping content rows");
        Require(contents.All(row => row is JsonObject) && bin["contents_truncated"]?.GetValue<bool>() == false &&
                bin["pending_settlement_value_complete"]?.GetValue<bool>() == true &&
                bin["pending_settlement_timing"]?.GetValue<string>() == "next_native_day_transition",
            "Shipping bank contents are incomplete.");
        var identities = new HashSet<(string, int, int)>();
        foreach (var row in contents.OfType<JsonObject>())
        {
            var id = row["qualified_item_id"]?.GetValue<string>() ?? string.Empty;
            var count = Int(row, "count"); var quality = Int(row, "quality"); var price = Int(row, "unit_sell_to_store_price");
            Require(id.Length > 0 && row["item_id"] is JsonValue && count > 0 && quality >= 0 && price >= 0 &&
                    identities.Add((id.ToUpperInvariant(), quality, price)) &&
                    ShippingLong(row, "total_sell_to_store_price") == checked((long)count * price),
                "Shipping content variant/quantity/value is invalid.");
        }
        Require(Int(bin, "contents_total_count") == contents.OfType<JsonObject>().Sum(row => Int(row, "count")) &&
                Int(bin, "contents_distinct_item_count") == contents.OfType<JsonObject>().Select(row => row["qualified_item_id"]!.GetValue<string>()).Distinct(StringComparer.OrdinalIgnoreCase).Count() &&
                Int(bin, "contents_distinct_settlement_variant_count") == contents.Count &&
                ShippingLong(bin, "pending_settlement_value") == contents.OfType<JsonObject>().Sum(row => ShippingLong(row, "total_sell_to_store_price")) &&
                bin["contents_signature"]?.GetValue<string>() == ShippingContentsSignature(contents),
            "Shipping bank totals or original contents signature are inconsistent.");
    }

    private static string ShippingContentsSignature(JsonArray contents)
    {
        // FarmReadAdapter.ComputeContentsSignature: ordered qid|quality|unit price|count + newline, SHA256 UTF8.
        var text = new StringBuilder();
        foreach (var row in OrderedShippingContents(contents))
            text.Append(row["qualified_item_id"]!.GetValue<string>()).Append('|')
                .Append(Int(row, "quality").ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(Int(row, "unit_sell_to_store_price").ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(Int(row, "count").ToString(CultureInfo.InvariantCulture)).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))).ToLowerInvariant();
    }

    private static long ShippingLong(JsonObject row, string name) => row[name] is JsonValue value && value.TryGetValue<long>(out var number)
        ? number : throw new InvalidDataException("Shipping integer missing: " + name);
}

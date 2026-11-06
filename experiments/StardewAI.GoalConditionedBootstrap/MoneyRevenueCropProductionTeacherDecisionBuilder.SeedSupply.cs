using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropProductionTeacherDecisionBuilder
{
    private static SeedStock[] ReadSeedStock(SnapshotEnvelope snapshot)
    {
        var admittedPurchases = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.buy_supplies" })
            .Options.Single().EventCandidates
            .Where(value => value.Available)
            .Select(value => new SeedPurchaseKey(
                value.ShopId,
                value.QualifiedItemId,
                value.UnitPrice,
                value.Parameters.FirstOrDefault(parameter =>
                    parameter.Name == "continuation.stock_id")?.Value ??
                    string.Empty,
                value.Parameters.FirstOrDefault(parameter =>
                    parameter.Name == "continuation.output_stack_per_purchase")
                    ?.Value ?? string.Empty,
                value.Parameters.FirstOrDefault(parameter =>
                    parameter.Name == "continuation.output_quality")?.Value ??
                    string.Empty))
            .ToHashSet();
        var shops = RequiredProperty(
            ReadStateValue(snapshot, "locations", "shops"), "shops");
        Require(shops.ValueKind == JsonValueKind.Array,
            "Money Revenue crop-production Teacher shop catalog is unavailable.");
        return shops.EnumerateArray().SelectMany(shop =>
        {
            var shopId = ReadString(shop, "shop_id");
            if (!shop.TryGetProperty("stock_preview", out var preview) ||
                !preview.TryGetProperty("entries", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
                return Array.Empty<SeedStock>();
            return entries.EnumerateArray()
                .Where(value =>
                    ReadOptionalBool(value,
                        "executor_purchase_preview_enabled") == true &&
                    ReadOptionalInt(value, "currency") == 0 &&
                    ReadOptionalInt(value, "price") > 0 &&
                    ReadOptionalInt(value, "stack") == 1 &&
                    ReadOptionalInt(value, "quality") == 0 &&
                    ReadOptionalBool(value, "infinite_stock").HasValue &&
                    (ReadOptionalBool(value, "infinite_stock") == true ||
                        ReadOptionalInt(value, "stock") > 0) &&
                    ReadString(value, "qualified_item_id")
                        .StartsWith("(O)", StringComparison.Ordinal) &&
                    admittedPurchases.Contains(new SeedPurchaseKey(
                        shopId,
                        ReadString(value, "qualified_item_id"),
                        ReadInt(value, "price"),
                        value.TryGetProperty("synced_key", out var stockId) &&
                            stockId.ValueKind == JsonValueKind.String
                                ? stockId.GetString() ?? string.Empty
                                : string.Empty,
                        ReadInt(value, "stack").ToString(
                            CultureInfo.InvariantCulture),
                        ReadInt(value, "quality").ToString(
                            CultureInfo.InvariantCulture))))
                .Select(value => new SeedStock(
                    shopId,
                    ReadString(value, "qualified_item_id"),
                    ReadInt(value, "price"),
                    ReadOptionalBool(value, "infinite_stock") == true
                        ? int.MaxValue
                        : ReadInt(value, "stock")))
                .ToArray();
        })
        // The existing run plan chooses one shop/item/price channel, not a
        // portfolio of stock rows. Never add indistinguishable row quantities.
        .GroupBy(value => (value.ShopId, value.QualifiedItemId, value.UnitPrice))
        .Select(group => group.First() with
        {
            AvailablePurchaseCount = group.Min(value =>
                value.AvailablePurchaseCount)
        }).ToArray();
    }

    private sealed record SeedStock(
        string ShopId,
        string QualifiedItemId,
        int UnitPrice,
        int AvailablePurchaseCount);

    private sealed record SeedPurchaseKey(
        string ShopId,
        string QualifiedItemId,
        int UnitPrice,
        string StockId,
        string OutputStack,
        string OutputQuality);
}

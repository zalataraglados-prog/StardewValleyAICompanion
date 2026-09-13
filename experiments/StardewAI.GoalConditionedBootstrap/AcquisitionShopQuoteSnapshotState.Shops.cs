using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed partial class AcquisitionShopQuoteSnapshotState
{
    private static ShopReadResult ReadShops(JsonElement state)
    {
        if (!TryFieldValue(state, "locations", "shops", out var value) ||
            value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty("shops", out var shopRows) ||
            shopRows.ValueKind != JsonValueKind.Array ||
            ReadInt(value, "shop_count") != shopRows.GetArrayLength())
        {
            return ShopReadResult.Blocked(
                "current_native_shop_quotes_missing_or_incomplete");
        }

        var shopIds = new HashSet<string>(StringComparer.Ordinal);
        var result = new Dictionary<string, AcquisitionShopQuote>(
            StringComparer.Ordinal);
        foreach (var shop in shopRows.EnumerateArray())
        {
            var shopId = ReadString(shop, "shop_id");
            if (string.IsNullOrWhiteSpace(shopId) || !shopIds.Add(shopId) ||
                !shop.TryGetProperty("stock_preview", out var preview) ||
                preview.ValueKind != JsonValueKind.Object ||
                ReadString(preview, "kind") != "shop_stock_preview" ||
                ReadString(preview, "shop_id") != shopId ||
                !preview.TryGetProperty("entries", out var entries) ||
                entries.ValueKind != JsonValueKind.Array ||
                ReadInt(preview, "entry_count") != entries.GetArrayLength())
            {
                return ShopReadResult.Blocked(
                    "current_native_shop_quote_contract_invalid");
            }
            var currency = ReadInt(preview, "currency");
            if (!currency.HasValue || !CurrencyKeys.ContainsKey(currency.Value))
            {
                return ShopReadResult.Blocked(
                    "current_native_shop_quote_currency_invalid");
            }
            foreach (var entry in entries.EnumerateArray())
            {
                var quote = ReadQuote(shopId, currency.Value, entry);
                if (quote is null || !result.TryAdd(
                        QuoteKey(
                            quote.ShopId,
                            quote.StockId,
                            quote.QualifiedItemId),
                        quote))
                {
                    return ShopReadResult.Blocked(
                        "current_native_shop_quote_entry_invalid_or_duplicate");
                }
            }
        }
        return new(true, result, Array.Empty<string>());
    }

    private static AcquisitionShopQuote? ReadQuote(
        string shopId,
        int currencyId,
        JsonElement entry)
    {
        var stockId = ReadString(entry, "synced_key");
        var itemId = ReadString(entry, "qualified_item_id");
        var entryCurrency = ReadInt(entry, "currency");
        var price = ReadInt(entry, "price");
        var stock = ReadInt(entry, "stock");
        var infinite = ReadBool(entry, "infinite_stock");
        var canBuy = ReadBool(entry, "can_buy_item");
        if (string.IsNullOrWhiteSpace(stockId) ||
            string.IsNullOrWhiteSpace(itemId) ||
            entryCurrency != currencyId || !price.HasValue || price < 0 ||
            !stock.HasValue || !infinite.HasValue || !canBuy.HasValue ||
            (infinite == true && stock != int.MaxValue) ||
            (infinite == false && stock < 0))
        {
            return null;
        }

        var tradeItem = ReadNullableString(
            entry,
            "trade_item_qualified_id");
        var tradeCount = ReadInt(entry, "effective_trade_item_count");
        if ((tradeItem is null && tradeCount.HasValue) ||
            (tradeItem is not null &&
                (string.IsNullOrWhiteSpace(tradeItem) ||
                 !tradeCount.HasValue || tradeCount <= 0)))
        {
            return null;
        }
        return new AcquisitionShopQuote(
            shopId,
            stockId,
            itemId,
            currencyId,
            price.Value,
            stock.Value,
            infinite.Value,
            canBuy.Value,
            tradeItem,
            tradeCount);
    }

    private sealed record ShopReadResult(
        bool Available,
        IReadOnlyDictionary<string, AcquisitionShopQuote> Quotes,
        string[] BlockingReasons)
    {
        public static ShopReadResult Blocked(string reason) => new(
            false,
            new Dictionary<string, AcquisitionShopQuote>(
                StringComparer.Ordinal),
            new[] { reason });
    }
}

using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed partial class AcquisitionShopQuoteSnapshotState
{
    private static readonly IReadOnlyDictionary<int, string> CurrencyKeys =
        NativeShopCurrencies.All.ToDictionary(
            row => row.Id,
            row => row.Key);

    private readonly Lazy<CurrencyReadResult> currencies;
    private readonly Lazy<ShopReadResult> shops;

    public AcquisitionShopQuoteSnapshotState(JsonElement state)
    {
        currencies = new Lazy<CurrencyReadResult>(
            () => ReadCurrencies(state));
        shops = new Lazy<ShopReadResult>(() => ReadShops(state));
    }

    public AcquisitionCurrencyBalanceLookup CurrencyBalance(int currencyId)
    {
        var state = currencies.Value;
        if (!state.Available)
            return new(false, currencyId, string.Empty, null,
                state.BlockingReasons);
        if (!state.Balances.TryGetValue(currencyId, out var row))
        {
            return new(
                false,
                currencyId,
                string.Empty,
                null,
                new[]
                {
                    "unsupported_shop_currency_id:" +
                    currencyId.ToString(CultureInfo.InvariantCulture)
                });
        }
        return new(true, currencyId, row.CurrencyKey, row.Balance,
            Array.Empty<string>());
    }

    public AcquisitionShopQuoteLookup ShopQuote(
        AcquisitionShopSourceEvidence source,
        string qualifiedItemId)
    {
        var state = shops.Value;
        if (!state.Available)
            return new(false, false, null, state.BlockingReasons);
        var key = QuoteKey(source.ShopId, source.StockId, qualifiedItemId);
        return state.Quotes.TryGetValue(key, out var quote)
            ? new(true, true, quote, Array.Empty<string>())
            : new(
                true,
                false,
                null,
                new[]
                {
                    "current_native_shop_quote_missing:" +
                    source.ShopId + ":" + source.StockId
                });
    }

    private static string QuoteKey(
        string shopId,
        string stockId,
        string qualifiedItemId) =>
        shopId + "\u001f" + stockId + "\u001f" + qualifiedItemId;
}

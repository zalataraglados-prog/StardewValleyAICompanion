using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed partial class AcquisitionShopQuoteSnapshotState
{
    private static CurrencyReadResult ReadCurrencies(JsonElement state)
    {
        if (!TryFieldValue(
                state,
                "player",
                "shop_currency_balances",
                out var value) ||
            value.ValueKind != JsonValueKind.Object ||
            ReadString(value, "schema_version") !=
                "shop_currency_balances.v1" ||
            ReadString(value, "projection_status") !=
                "complete_locked_base_1.6.15_shop_menu_currency_domain" ||
            !value.TryGetProperty("rows", out var rows) ||
            rows.ValueKind != JsonValueKind.Array ||
            !value.TryGetProperty("supported_currency_ids", out var supported) ||
            supported.ValueKind != JsonValueKind.Array)
        {
            return CurrencyReadResult.Blocked(
                "shop_currency_balances_missing_or_incomplete");
        }

        var supportedIds = supported.EnumerateArray()
            .Where(row => row.TryGetInt32(out _))
            .Select(row => row.GetInt32())
            .ToArray();
        if (supportedIds.Length != CurrencyKeys.Count ||
            !supportedIds.ToHashSet().SetEquals(CurrencyKeys.Keys))
        {
            return CurrencyReadResult.Blocked(
                "shop_currency_domain_drifted");
        }

        var result = new Dictionary<int, CurrencyRow>();
        foreach (var row in rows.EnumerateArray())
        {
            var id = ReadInt(row, "currency_id");
            var key = ReadString(row, "currency_key");
            var balance = ReadInt(row, "balance");
            if (!id.HasValue || !CurrencyKeys.TryGetValue(id.Value,
                    out var expectedKey) || key != expectedKey ||
                !balance.HasValue || balance < 0 ||
                !result.TryAdd(id.Value, new CurrencyRow(key, balance.Value)))
            {
                return CurrencyReadResult.Blocked(
                    "shop_currency_balance_row_invalid");
            }
        }
        return result.Count == CurrencyKeys.Count
            ? new(true, result, Array.Empty<string>())
            : CurrencyReadResult.Blocked(
                "shop_currency_balance_row_count_invalid");
    }

    private sealed record CurrencyRow(string CurrencyKey, int Balance);

    private sealed record CurrencyReadResult(
        bool Available,
        IReadOnlyDictionary<int, CurrencyRow> Balances,
        string[] BlockingReasons)
    {
        public static CurrencyReadResult Blocked(string reason) => new(
            false,
            new Dictionary<int, CurrencyRow>(),
            new[] { reason });
    }
}

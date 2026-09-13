using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Strategy;

public sealed class NativeCurrencySupplyProjectionResult
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "native_currency_supply_projection.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("balances")]
    public NativeCurrencySupplyBalance[] Balances { get; set; } =
        Array.Empty<NativeCurrencySupplyBalance>();

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
}

public sealed class NativeCurrencySupplyBalance
{
    public NativeCurrencySupplyBalance(
        int currencyId,
        string currencyKey,
        int totalAmount,
        int reservedAmount,
        int availableAmount)
    {
        CurrencyId = currencyId;
        CurrencyKey = currencyKey;
        TotalAmount = totalAmount;
        ReservedAmount = reservedAmount;
        AvailableAmount = availableAmount;
    }

    [JsonPropertyName("currency_id")]
    public int CurrencyId { get; }

    [JsonPropertyName("currency_key")]
    public string CurrencyKey { get; }

    [JsonPropertyName("total_amount")]
    public int TotalAmount { get; }

    [JsonPropertyName("reserved_amount")]
    public int ReservedAmount { get; }

    [JsonPropertyName("available_amount")]
    public int AvailableAmount { get; }
}

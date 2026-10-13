using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record MoneyRevenueRecurrencePrefixEvidence(
    [property: JsonPropertyName("save_id")] string SaveId,
    [property: JsonPropertyName("player_id")] string PlayerId,
    [property: JsonPropertyName("initial_state_hash")] string InitialStateHash,
    [property: JsonPropertyName("final_state_hash")] string FinalStateHash,
    [property: JsonPropertyName("initial_total_day")] int InitialTotalDay,
    [property: JsonPropertyName("final_total_day")] int FinalTotalDay,
    [property: JsonPropertyName("initial_money")] int InitialMoney,
    [property: JsonPropertyName("final_money")] int FinalMoney,
    [property: JsonPropertyName("initial_total_money_earned")]
        uint InitialTotalMoneyEarned,
    [property: JsonPropertyName("final_total_money_earned")]
        uint FinalTotalMoneyEarned,
    [property: JsonPropertyName("total_revenue")] int TotalRevenue,
    [property: JsonPropertyName("immediate_shop_sale_count")]
        int ImmediateShopSaleCount,
    [property: JsonPropertyName("native_shipping_settlement_count")]
        int NativeShippingSettlementCount,
    [property: JsonPropertyName("transitions")]
        MoneyRevenueTransitionEvidence[] Transitions)
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion => "money_revenue_recurrence_prefix.v1";

    [JsonPropertyName("covers_both_revenue_kinds")]
    public bool CoversBothRevenueKinds =>
        ImmediateShopSaleCount > 0 &&
        NativeShippingSettlementCount > 0;
}

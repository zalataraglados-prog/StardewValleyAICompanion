namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueTransitionKinds
{
    public const string ImmediateShopSale = "immediate_shop_sale";
    public const string NativeShippingDaySettlement =
        "native_shipping_day_settlement";
}

public sealed record MoneyRevenueCheckpoint(
    string StateHash,
    string SaveId,
    string PlayerId,
    int TotalDay,
    int Money,
    uint TotalMoneyEarned,
    string ShippingScopeId,
    int PendingShippingSettlementValue,
    string ShippingContentsSignature);

public sealed record MoneyRevenueTransitionEvidence(
    string TransitionKind,
    MoneyRevenueCheckpoint Before,
    MoneyRevenueCheckpoint After,
    int MoneyDelta,
    uint TotalMoneyEarnedDelta,
    int ConsumedPendingShippingValue);

namespace StardewAI.GoalConditionedBootstrap;

internal sealed record AcquisitionShopQuote(
    string ShopId,
    string StockId,
    string QualifiedItemId,
    int CurrencyId,
    int Price,
    int Stock,
    bool InfiniteStock,
    bool CanBuyItem,
    string? TradeItemQualifiedId,
    int? TradeItemCount);

internal sealed record AcquisitionShopQuoteLookup(
    bool EvidenceAvailable,
    bool Found,
    AcquisitionShopQuote? Quote,
    string[] BlockingReasons);

internal sealed record AcquisitionCurrencyBalanceLookup(
    bool EvidenceAvailable,
    int CurrencyId,
    string CurrencyKey,
    int? Balance,
    string[] BlockingReasons);

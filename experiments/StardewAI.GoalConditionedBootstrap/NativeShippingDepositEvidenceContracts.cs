namespace StardewAI.GoalConditionedBootstrap;

internal sealed record NativeShippingDepositEvidence
{
    internal string QueueItemId { get; init; } = string.Empty;
    internal int? QueueItemIndex { get; init; }
    internal string QualifiedItemId { get; init; } = string.Empty;
    internal int? SlotIndex { get; init; }
    internal int? Quantity { get; init; }
    internal int? Quality { get; init; }
    internal int? UnitPrice { get; init; }
    internal string BinLocation { get; init; } = string.Empty;
    internal int? BinTileX { get; init; }
    internal int? BinTileY { get; init; }
    internal string BinScope { get; init; } = string.Empty;
    internal int? BeforeInventorySlotQuantity { get; init; }
    internal int? AfterInventorySlotQuantity { get; init; }
    internal int? BeforeInventoryItemQuantity { get; init; }
    internal int? AfterInventoryItemQuantity { get; init; }
    internal int? BeforeBinVariantQuantity { get; init; }
    internal int? AfterBinVariantQuantity { get; init; }
    internal int? BeforeBinItemQuantity { get; init; }
    internal int? AfterBinItemQuantity { get; init; }
    internal long? BeforePendingSettlementValue { get; init; }
    internal long? AfterPendingSettlementValue { get; init; }
    internal long? BeforeMoney { get; init; }
    internal long? AfterMoney { get; init; }
    internal long? BeforeTotalMoneyEarned { get; init; }
    internal long? AfterTotalMoneyEarned { get; init; }
    internal string[] BlockingReasons { get; init; } = Array.Empty<string>();

    internal bool Verified => BlockingReasons.Length == 0;
}

internal sealed record NativeShippingDepositInventoryRead(
    int SlotQuantity,
    int ItemQuantity,
    int Quality,
    int UnitPrice);

internal sealed record NativeShippingDepositBinRead(
    string Scope,
    string PlayerId,
    int VariantQuantity,
    int ItemQuantity,
    long PendingSettlementValue);

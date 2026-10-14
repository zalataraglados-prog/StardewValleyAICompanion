using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class NativeShippingDepositEvidenceVerifier
{
    private const string ShippingOptionId =
        "executor.ship_inventory_item_to_bin";

    internal static NativeShippingDepositEvidence Verify(
        ActionQueueItem[] queueItems,
        QueueExecutionReceiptEnvelope? receipt,
        SnapshotEnvelope before,
        SnapshotEnvelope after,
        string expectedQualifiedItemId,
        int expectedQuantity)
    {
        var reasons = new List<string>();
        if (string.IsNullOrWhiteSpace(expectedQualifiedItemId) ||
            expectedQuantity <= 0)
        {
            reasons.Add("shipping_deposit_expected_identity_invalid");
        }

        var matches = queueItems
            .Select((item, index) => (Item: item, Index: index))
            .Where(value => string.Equals(
                    value.Item.OptionId,
                    ShippingOptionId,
                    StringComparison.Ordinal) &&
                ParameterEquals(
                    value.Item,
                    "qualified_item_id",
                    expectedQualifiedItemId))
            .ToArray();
        if (matches.Length != 1)
            reasons.Add("shipping_deposit_queue_item_not_unique");

        ActionQueueItem? queueItem = null;
        int? queueItemIndex = null;
        if (matches.Length == 1)
        {
            queueItem = matches[0].Item;
            queueItemIndex = matches[0].Index;
            if (matches[0].Index != queueItems.Length - 1)
                reasons.Add("shipping_deposit_queue_item_not_terminal");
        }

        var qualifiedItemId = ReadUniqueParameter(
            queueItem,
            "qualified_item_id",
            reasons);
        var slotIndex = ReadUniqueIntParameter(
            queueItem,
            "slot_index",
            minimum: 0,
            reasons);
        var quantity = ReadUniqueIntParameter(
            queueItem,
            "quantity",
            minimum: 1,
            reasons);
        var unitPrice = ReadUniqueIntParameter(
            queueItem,
            "expected_unit_price",
            minimum: 1,
            reasons);
        var binLocation = ReadUniqueParameter(
            queueItem,
            "bin_location",
            reasons);
        var binTileX = ReadUniqueIntParameter(
            queueItem,
            "bin_tile_x",
            minimum: 0,
            reasons);
        var binTileY = ReadUniqueIntParameter(
            queueItem,
            "bin_tile_y",
            minimum: 0,
            reasons);

        if (!string.Equals(
                qualifiedItemId,
                expectedQualifiedItemId,
                StringComparison.Ordinal))
        {
            reasons.Add("shipping_deposit_qualified_item_mismatch");
        }
        if (quantity != expectedQuantity)
            reasons.Add("shipping_deposit_quantity_mismatch");
        if (quantity != 1)
            reasons.Add("shipping_deposit_native_quantity_not_one");
        if (!string.Equals(binLocation, "Farm", StringComparison.Ordinal))
            reasons.Add("shipping_deposit_bin_location_not_farm");

        VerifyReceipt(
            queueItems,
            queueItem,
            queueItemIndex,
            receipt,
            before,
            after,
            expectedQualifiedItemId,
            expectedQuantity,
            reasons);

        NativeShippingDepositInventoryRead? beforeInventory = null;
        NativeShippingDepositInventoryRead? afterInventory = null;
        NativeShippingDepositBinRead? beforeBin = null;
        NativeShippingDepositBinRead? afterBin = null;
        if (slotIndex.HasValue && unitPrice.HasValue &&
            binTileX.HasValue && binTileY.HasValue)
        {
            beforeInventory = ReadInventory(
                before,
                slotIndex.Value,
                expectedQualifiedItemId,
                expectedEmpty: false,
                reasons,
                "before");
            var quality = beforeInventory?.Quality;
            afterInventory = quality.HasValue
                ? ReadInventory(
                    after,
                    slotIndex.Value,
                    expectedQualifiedItemId,
                    expectedEmpty: beforeInventory!.SlotQuantity ==
                        expectedQuantity,
                    reasons,
                    "after",
                    quality.Value,
                    unitPrice.Value)
                : null;
            if (beforeInventory is not null &&
                beforeInventory.UnitPrice != unitPrice.Value)
            {
                reasons.Add("shipping_deposit_inventory_unit_price_mismatch");
            }
            if (quality.HasValue)
            {
                beforeBin = ReadBin(
                    before,
                    binTileX.Value,
                    binTileY.Value,
                    expectedQualifiedItemId,
                    quality.Value,
                    unitPrice.Value,
                    reasons,
                    "before");
                afterBin = ReadBin(
                    after,
                    binTileX.Value,
                    binTileY.Value,
                    expectedQualifiedItemId,
                    quality.Value,
                    unitPrice.Value,
                    reasons,
                    "after");
            }
        }

        VerifyDeltas(
            beforeInventory,
            afterInventory,
            beforeBin,
            afterBin,
            expectedQuantity,
            unitPrice,
            reasons);
        VerifyChangedFactsAgainstSnapshots(
            receipt,
            queueItemIndex,
            expectedQualifiedItemId,
            beforeInventory,
            afterInventory,
            beforeBin,
            afterBin,
            reasons);

        var beforeMoney = ReadStateLong(before, "player", "money");
        var afterMoney = ReadStateLong(after, "player", "money");
        var beforeEarned = ReadStateLong(
            before,
            "player",
            "total_money_earned");
        var afterEarned = ReadStateLong(
            after,
            "player",
            "total_money_earned");
        VerifyUnchanged(beforeMoney, afterMoney,
            "shipping_deposit_money", reasons);
        VerifyUnchanged(beforeEarned, afterEarned,
            "shipping_deposit_total_money_earned", reasons);
        VerifyUnchanged(
            ReadStateLong(before, "time", "total_days"),
            ReadStateLong(after, "time", "total_days"),
            "shipping_deposit_total_day",
            reasons);

        var distinct = reasons
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new NativeShippingDepositEvidence
        {
            QueueItemId = queueItem?.QueueItemId ?? string.Empty,
            QueueItemIndex = queueItemIndex,
            QualifiedItemId = qualifiedItemId,
            SlotIndex = slotIndex,
            Quantity = quantity,
            Quality = beforeInventory?.Quality,
            UnitPrice = unitPrice,
            BinLocation = binLocation,
            BinTileX = binTileX,
            BinTileY = binTileY,
            BinScope = beforeBin?.Scope ?? string.Empty,
            BeforeInventorySlotQuantity = beforeInventory?.SlotQuantity,
            AfterInventorySlotQuantity = afterInventory?.SlotQuantity,
            BeforeInventoryItemQuantity = beforeInventory?.ItemQuantity,
            AfterInventoryItemQuantity = afterInventory?.ItemQuantity,
            BeforeBinVariantQuantity = beforeBin?.VariantQuantity,
            AfterBinVariantQuantity = afterBin?.VariantQuantity,
            BeforeBinItemQuantity = beforeBin?.ItemQuantity,
            AfterBinItemQuantity = afterBin?.ItemQuantity,
            BeforePendingSettlementValue =
                beforeBin?.PendingSettlementValue,
            AfterPendingSettlementValue = afterBin?.PendingSettlementValue,
            BeforeMoney = beforeMoney,
            AfterMoney = afterMoney,
            BeforeTotalMoneyEarned = beforeEarned,
            AfterTotalMoneyEarned = afterEarned,
            BlockingReasons = distinct
        };
    }

}

using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class NativeShippingDepositEvidenceVerifier
{
    // Called only with a frame returned by the original immutable native-work reader.
    // Physical shipping does not imply completion of the enclosing typed objective.
    internal static NativeShippingDepositEvidence VerifyNativeWorkFrame(NativeExecutionActionFrame frame, string itemId)
    {
        var reasons = new List<string>();
        var item = frame.Item; var before = frame.Before; var after = frame.After;
        if (item.OptionId != ShippingOptionId || frame.Result.OptionId != ShippingOptionId ||
            frame.Result.Status != "applied" || frame.Result.PrimitiveVerificationStatus != "verified" ||
            frame.Result.PrimitiveKind != "ship_inventory_item_to_bin" || frame.Result.QueueItemId != item.QueueItemId)
            reasons.Add("shipping_native_work_frame_identity_invalid");
        var qualified = ReadUniqueParameter(item, "qualified_item_id", reasons);
        var slot = ReadUniqueIntParameter(item, "slot_index", 0, reasons);
        var quantity = ReadUniqueIntParameter(item, "quantity", 1, reasons);
        var price = ReadUniqueIntParameter(item, "expected_unit_price", 1, reasons);
        var location = ReadUniqueParameter(item, "target_location", reasons);
        var x = ReadUniqueIntParameter(item, "target_tile_x", 0, reasons);
        var y = ReadUniqueIntParameter(item, "target_tile_y", 0, reasons);
        if (string.IsNullOrWhiteSpace(itemId) || qualified != itemId || quantity != 1 || location != "Farm")
            reasons.Add("shipping_native_work_frame_target_invalid");
        NativeShippingDepositInventoryRead? first = null, next = null;
        NativeShippingDepositBinRead? priorBin = null, bin = null;
        if (slot.HasValue && price.HasValue && x.HasValue && y.HasValue)
        {
            first = ReadInventory(before, slot.Value, itemId, false, reasons, "before");
            if (first is not null)
            {
                if (first.UnitPrice != price.Value) reasons.Add("shipping_deposit_inventory_unit_price_mismatch");
                next = ReadInventory(after, slot.Value, itemId, first.SlotQuantity == 1, reasons, "after", first.Quality, price.Value);
                priorBin = ReadBin(before, x.Value, y.Value, itemId, first.Quality, price.Value, reasons, "before");
                bin = ReadBin(after, x.Value, y.Value, itemId, first.Quality, price.Value, reasons, "after");
            }
        }
        VerifyDeltas(first, next, priorBin, bin, 1, price, reasons);
        var facts = JsonSerializer.SerializeToElement(frame.Result.ChangedFacts);
        var stockFact = ReadChangedFact(facts, "player.inventory." + itemId + ".count");
        var binFact = ReadChangedFact(facts, "farm.shipping_bin." + itemId + ".count");
        if (stockFact is null || binFact is null) reasons.Add("shipping_deposit_changed_fact_missing");
        else if (first is not null && next is not null && priorBin is not null && bin is not null &&
            (stockFact.Value.Before != first.ItemQuantity || stockFact.Value.After != next.ItemQuantity ||
             binFact.Value.Before != priorBin.ItemQuantity || binFact.Value.After != bin.ItemQuantity))
            reasons.Add("shipping_deposit_changed_fact_snapshot_mismatch");
        var money = ReadStateLong(before, "player", "money"); var nextMoney = ReadStateLong(after, "player", "money");
        var earned = ReadStateLong(before, "player", "total_money_earned"); var nextEarned = ReadStateLong(after, "player", "total_money_earned");
        VerifyUnchanged(money, nextMoney, "shipping_deposit_money", reasons);
        VerifyUnchanged(earned, nextEarned, "shipping_deposit_total_money_earned", reasons);
        VerifyUnchanged(ReadStateLong(before, "time", "total_days"), ReadStateLong(after, "time", "total_days"), "shipping_deposit_total_day", reasons);
        return new()
        {
            QueueItemId = item.QueueItemId, QualifiedItemId = qualified, SlotIndex = slot, Quantity = quantity,
            Quality = first?.Quality, UnitPrice = price, BinLocation = location, BinTileX = x, BinTileY = y,
            BinScope = priorBin?.Scope ?? string.Empty, BeforeInventorySlotQuantity = first?.SlotQuantity,
            AfterInventorySlotQuantity = next?.SlotQuantity, BeforeInventoryItemQuantity = first?.ItemQuantity,
            AfterInventoryItemQuantity = next?.ItemQuantity, BeforeBinVariantQuantity = priorBin?.VariantQuantity,
            AfterBinVariantQuantity = bin?.VariantQuantity, BeforeBinItemQuantity = priorBin?.ItemQuantity,
            AfterBinItemQuantity = bin?.ItemQuantity, BeforePendingSettlementValue = priorBin?.PendingSettlementValue,
            AfterPendingSettlementValue = bin?.PendingSettlementValue, BeforeMoney = money, AfterMoney = nextMoney,
            BeforeTotalMoneyEarned = earned, AfterTotalMoneyEarned = nextEarned,
            BlockingReasons = reasons.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
        };
    }
}

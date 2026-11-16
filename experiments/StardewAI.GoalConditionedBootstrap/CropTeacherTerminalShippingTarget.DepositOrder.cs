namespace StardewAI.GoalConditionedBootstrap;

// Linked values retain structural equality when an owner journal is reopened.
public sealed record CropTeacherTerminalDepositOrder(int SlotIndex, CropTeacherTerminalDepositOrder? Next = null);

public sealed partial record CropTeacherTerminalShippingTarget
{
    private void ValidateNativeDepositOrder()
    {
        var remaining = new Dictionary<int, int>();
        for (var variant = NativeVariants!.First; variant is not null; variant = variant.Next)
            remaining.Add(variant.SlotIndex, variant.PlannedQuantity);
        var count = 0;
        for (var order = NativeVariants.DepositOrder; order is not null; order = order.Next)
        {
            if (++count > RequiredQuantity || !remaining.TryGetValue(order.SlotIndex, out var quantity) || quantity <= 0)
                throw new InvalidDataException("Native terminal deposit order repeats, exceeds or changes its selected quantities.");
            remaining[order.SlotIndex] = quantity - 1;
        }
        if (count != RequiredQuantity || remaining.Values.Any(quantity => quantity != 0))
            throw new InvalidDataException("Native terminal deposit order does not cover the whole obligation.");
    }

    private CropTeacherTerminalDepositOrder DepositOrderAt(int count)
    {
        var order = NativeVariants!.DepositOrder;
        for (; count > 0; count--) order = order!.Next;
        return order!;
    }

    internal int CountOrderedDeposits(int slotIndex, int count)
    {
        if (count < 0 || count > RequiredQuantity || NativeVariants?.DepositOrder is null)
            throw new InvalidDataException("Native terminal ordered cursor is outside its explicit plan.");
        var consumed = 0;
        for (var order = NativeVariants!.DepositOrder; count > 0; count--, order = order!.Next)
            if (order!.SlotIndex == slotIndex) consumed++;
        return consumed;
    }
}

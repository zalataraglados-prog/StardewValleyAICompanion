using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueTerminalCandidateRebindBuilder
{
    internal static MoneyRevenueIncreasedInventorySlot[] IncreasedSlots(
        SnapshotEnvelope before,
        SnapshotEnvelope after,
        string qualifiedItemId,
        int minimumQuality,
        int requiredAmount,
        int expectedNetIncrease)
    {
        Require(!string.IsNullOrWhiteSpace(qualifiedItemId) &&
                minimumQuality >= 0 &&
                requiredAmount > 0 &&
                expectedNetIncrease >= requiredAmount,
            "Money Revenue terminal inventory transition is invalid.");
        var beforeSlots = ReadSlots(before, qualifiedItemId, minimumQuality);
        var afterSlots = ReadSlots(after, qualifiedItemId, minimumQuality);
        try
        {
            var slotIds = beforeSlots.Keys.Concat(afterSlots.Keys)
                .Distinct()
                .Order()
                .ToArray();
            var deltas = slotIds.Select(slotIndex =>
            {
                var beforeQuantity = beforeSlots.TryGetValue(
                    slotIndex,
                    out var beforeSlot)
                    ? beforeSlot.Quantity
                    : 0;
                var afterQuantity = afterSlots.TryGetValue(
                    slotIndex,
                    out var afterSlot)
                    ? afterSlot.Quantity
                    : 0;
                return new
                {
                    SlotIndex = slotIndex,
                    Quality = afterSlot?.Quality ?? beforeSlot?.Quality ?? 0,
                    BeforeQuantity = beforeQuantity,
                    AfterQuantity = afterQuantity,
                    Increase = checked(afterQuantity - beforeQuantity)
                };
            }).ToArray();
            var netIncrease = checked(deltas.Sum(row => row.Increase));
            var positiveIncrease = checked(deltas
                .Where(row => row.Increase > 0)
                .Sum(row => row.Increase));
            Require(netIncrease == expectedNetIncrease &&
                    positiveIncrease >= requiredAmount,
                "Money Revenue terminal inventory slot deltas do not equal the verified receipt.");
            var increased = deltas.Where(row => row.Increase > 0)
                .Select(row => new MoneyRevenueIncreasedInventorySlot(
                    row.SlotIndex,
                    row.Quality,
                    row.BeforeQuantity,
                    row.AfterQuantity,
                    row.Increase))
                .ToArray();
            Require(increased.Length > 0,
                "Money Revenue terminal receipt has no positively changed inventory slot.");
            return increased;
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException(
                "Money Revenue terminal inventory slot delta overflowed.",
                exception);
        }
    }

    private static IReadOnlyDictionary<int, InventorySlotQuantity> ReadSlots(
        SnapshotEnvelope snapshot,
        string qualifiedItemId,
        int minimumQuality)
    {
        if (!snapshot.State.TryGetValue("player", out var player) ||
            player.ValueKind != JsonValueKind.Object ||
            !player.TryGetProperty("inventory", out var inventory) ||
            inventory.ValueKind != JsonValueKind.Object ||
            !inventory.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.String ||
            status.GetString() is not ("available" or "derived") ||
            !inventory.TryGetProperty("value", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "Money Revenue terminal inventory is missing or unavailable.");
        }
        var result = new Dictionary<int, InventorySlotQuantity>();
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object ||
                !row.TryGetProperty("qualified_item_id", out var itemId) ||
                itemId.ValueKind != JsonValueKind.String ||
                !string.Equals(
                    itemId.GetString(),
                    qualifiedItemId,
                    StringComparison.Ordinal))
            {
                continue;
            }
            if (!row.TryGetProperty("slot_index", out var slot) ||
                !slot.TryGetInt32(out var slotIndex) ||
                slotIndex < 0 ||
                !row.TryGetProperty("quality", out var qualityValue) ||
                !qualityValue.TryGetInt32(out var quality) ||
                quality < 0 ||
                !row.TryGetProperty("stack", out var stackValue) ||
                !stackValue.TryGetInt32(out var stack) ||
                stack <= 0)
            {
                throw new InvalidDataException(
                    "Money Revenue terminal inventory contains an invalid matching row.");
            }
            if (quality < minimumQuality)
                continue;
            Require(result.TryAdd(
                    slotIndex,
                    new InventorySlotQuantity(quality, stack)),
                "Money Revenue terminal inventory contains a duplicate matching slot.");
        }
        return result;
    }

    private sealed record InventorySlotQuantity(int Quality, int Quantity);
}

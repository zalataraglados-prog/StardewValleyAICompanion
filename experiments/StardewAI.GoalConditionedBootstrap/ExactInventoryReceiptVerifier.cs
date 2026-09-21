using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static class ExactInventoryReceiptVerifier
{
    public static ExactInventoryReceiptEvidence Verify(
        SnapshotEnvelope before,
        SnapshotEnvelope after,
        string qualifiedItemId,
        int requiredQuantity,
        int minimumQuality)
    {
        var reasons = new List<string>();
        if (string.IsNullOrWhiteSpace(qualifiedItemId) ||
            requiredQuantity <= 0 ||
            minimumQuality < 0)
        {
            reasons.Add("inventory_receipt_requirement_invalid");
        }
        var beforeRead = ReadCount(before, qualifiedItemId, minimumQuality);
        var afterRead = ReadCount(after, qualifiedItemId, minimumQuality);
        reasons.AddRange(beforeRead.BlockingReasons);
        reasons.AddRange(afterRead.BlockingReasons);
        if (!beforeRead.Count.HasValue || !afterRead.Count.HasValue)
            reasons.Add("inventory_receipt_count_unavailable");

        int? increase = null;
        if (beforeRead.Count.HasValue && afterRead.Count.HasValue)
        {
            try
            {
                increase = checked(afterRead.Count.Value - beforeRead.Count.Value);
            }
            catch (OverflowException)
            {
                reasons.Add("inventory_receipt_delta_overflow");
            }
        }
        var distinct = reasons.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new ExactInventoryReceiptEvidence(
            qualifiedItemId,
            requiredQuantity,
            minimumQuality,
            beforeRead.Count,
            afterRead.Count,
            increase,
            distinct.Length == 0,
            distinct.Length == 0 &&
                increase.HasValue &&
                increase.Value >= requiredQuantity,
            distinct);
    }

    public static int? InventoryCount(
        SnapshotEnvelope snapshot,
        string qualifiedItemId,
        int minimumQuality) =>
        ReadCount(snapshot, qualifiedItemId, minimumQuality).Count;

    private static InventoryCountRead ReadCount(
        SnapshotEnvelope snapshot,
        string qualifiedItemId,
        int minimumQuality)
    {
        if (!TryStateValue(snapshot, "player", "inventory", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return InventoryCountRead.Blocked(
                "inventory_receipt_inventory_missing_or_unavailable");
        }
        try
        {
            var count = 0;
            foreach (var row in rows.EnumerateArray().Where(row =>
                         ReadString(row, "qualified_item_id") ==
                            qualifiedItemId))
            {
                if (!TryReadInt(row, "quality", out var quality) ||
                    !TryReadInt(row, "stack", out var stack) ||
                    quality < 0 ||
                    stack <= 0)
                {
                    return InventoryCountRead.Blocked(
                        "inventory_receipt_matching_row_invalid");
                }
                if (quality >= minimumQuality)
                    count = checked(count + stack);
            }
            return new InventoryCountRead(count, Array.Empty<string>());
        }
        catch (OverflowException)
        {
            return InventoryCountRead.Blocked(
                "inventory_receipt_count_overflow");
        }
    }

    private static bool TryStateValue(
        SnapshotEnvelope snapshot,
        string section,
        string field,
        out JsonElement value)
    {
        value = default;
        return snapshot.State.TryGetValue(section, out var sectionValue) &&
            sectionValue.ValueKind == JsonValueKind.Object &&
            sectionValue.TryGetProperty(field, out var envelope) &&
            envelope.ValueKind == JsonValueKind.Object &&
            envelope.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String &&
            status.GetString() is "available" or "derived" &&
            envelope.TryGetProperty("value", out value);
    }

    private static string ReadString(JsonElement value, string property) =>
        value.TryGetProperty(property, out var field) &&
        field.ValueKind == JsonValueKind.String
            ? field.GetString() ?? string.Empty
            : string.Empty;

    private static bool TryReadInt(
        JsonElement value,
        string property,
        out int result)
    {
        result = 0;
        return value.TryGetProperty(property, out var field) &&
            field.TryGetInt32(out result);
    }

    private sealed record InventoryCountRead(
        int? Count,
        string[] BlockingReasons)
    {
        public static InventoryCountRead Blocked(string reason) =>
            new(null, new[] { reason });
    }
}

internal sealed record ExactInventoryReceiptEvidence(
    string QualifiedItemId,
    int RequiredQuantity,
    int MinimumQuality,
    int? BeforeQuantity,
    int? AfterQuantity,
    int? QuantityIncrease,
    bool Resolved,
    bool Verified,
    string[] BlockingReasons);

using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static class FullShipmentSettlementVerifier
{
    internal static FullShipmentProgressCheckpoint Project(
        IReadOnlyCollection<string> requiredQualifiedItemIds,
        SnapshotEnvelope snapshot)
    {
        var reasons = new List<string>();
        var required = requiredQualifiedItemIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
        if (required.Count == 0 ||
            required.Count != requiredQualifiedItemIds.Count)
        {
            reasons.Add(
                "full_shipment_settlement_requirement_authority_invalid");
        }
        var progress = ReadProgress(snapshot, required, reasons, "checkpoint");
        var shipping = ReadShippingCollection(
            snapshot,
            reasons,
            "checkpoint");
        var achievements = ReadAchievements(
            snapshot,
            reasons,
            "checkpoint");
        var totalDay = ReadTotalDay(snapshot, reasons, "checkpoint");
        if (progress is null || shipping is null || achievements is null ||
            !totalDay.HasValue || reasons.Count > 0)
        {
            throw new InvalidDataException(
                "Full Shipment recurrence checkpoint is invalid: " +
                string.Join(",", reasons.Distinct(StringComparer.Ordinal)));
        }
        return new FullShipmentProgressCheckpoint(
            progress.ShippedItemCount,
            progress.Complete,
            progress.MissingQualifiedItemIds,
            progress.Items,
            shipping,
            achievements.Contains(34),
            totalDay.Value);
    }

    internal static int ProjectUniformBinCount(
        SnapshotEnvelope snapshot,
        string qualifiedItemId)
    {
        var reasons = new List<string>();
        var result = ReadUniformBinCount(
            snapshot,
            qualifiedItemId,
            reasons,
            "checkpoint");
        if (!result.HasValue || reasons.Count > 0)
        {
            throw new InvalidDataException(
                "Full Shipment recurrence bin checkpoint is invalid: " +
                string.Join(",", reasons.Distinct(StringComparer.Ordinal)));
        }
        return result.Value;
    }

    public static FullShipmentSettlementEvidence Verify(
        IReadOnlyCollection<string> requiredQualifiedItemIds,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var reasons = new List<string>();
        var required = requiredQualifiedItemIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
        if (required.Count == 0 || required.Count !=
            requiredQualifiedItemIds.Count)
        {
            reasons.Add("full_shipment_settlement_requirement_authority_invalid");
        }

        var beforeProgress = ReadProgress(before, required, reasons, "before");
        var afterProgress = ReadProgress(after, required, reasons, "after");
        var settledQualifiedItemId = string.Empty;
        var settledItemId = string.Empty;
        if (beforeProgress is not null && afterProgress is not null)
        {
            var newlyShipped = beforeProgress.Items
                .Where(pair => !pair.Value.Shipped &&
                    afterProgress.Items[pair.Key].Shipped)
                .Select(pair => pair.Key)
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (beforeProgress.Complete ||
                newlyShipped.Length != 1 ||
                afterProgress.ShippedItemCount !=
                    beforeProgress.ShippedItemCount + 1 ||
                afterProgress.MissingQualifiedItemIds.Length !=
                    beforeProgress.MissingQualifiedItemIds.Length - 1)
            {
                reasons.Add(
                    "full_shipment_settlement_not_exactly_one_new_item");
            }
            else
            {
                settledQualifiedItemId = newlyShipped[0];
                settledItemId = beforeProgress.Items[settledQualifiedItemId]
                    .ItemId;
                VerifyItemTransition(
                    beforeProgress,
                    afterProgress,
                    settledQualifiedItemId,
                    reasons);
            }
            if (beforeProgress.Items.Any(pair =>
                    pair.Key != settledQualifiedItemId &&
                    (pair.Value.Shipped !=
                        afterProgress.Items[pair.Key].Shipped ||
                     afterProgress.Items[pair.Key].Count < pair.Value.Count)))
            {
                reasons.Add(
                    "full_shipment_settlement_existing_progress_regressed");
            }
        }

        var beforeShipping = ReadShippingCollection(
            before,
            reasons,
            "before");
        var afterShipping = ReadShippingCollection(after, reasons, "after");
        int? beforeSettledItemShippedCount = null;
        int? afterSettledItemShippedCount = null;
        if (settledItemId.Length > 0 &&
            beforeShipping is not null &&
            afterShipping is not null)
        {
            beforeSettledItemShippedCount = beforeShipping.GetValueOrDefault(
                settledItemId);
            afterSettledItemShippedCount = afterShipping.GetValueOrDefault(
                settledItemId);
            if (beforeSettledItemShippedCount != 0 ||
                afterSettledItemShippedCount != 1)
            {
                reasons.Add(
                    "full_shipment_settlement_native_shipping_count_transition_mismatch");
            }
            if (beforeShipping.Any(pair => pair.Key != settledItemId &&
                    afterShipping.GetValueOrDefault(pair.Key) < pair.Value))
            {
                reasons.Add(
                    "full_shipment_settlement_shipping_collection_regressed");
            }
        }

        var beforeBinCount = settledQualifiedItemId.Length > 0
            ? ReadUniformBinCount(
                before,
                settledQualifiedItemId,
                reasons,
                "before")
            : null;
        var afterBinCount = settledQualifiedItemId.Length > 0
            ? ReadUniformBinCount(
                after,
                settledQualifiedItemId,
                reasons,
                "after")
            : null;
        if (beforeBinCount.HasValue && afterBinCount.HasValue &&
            (beforeBinCount.Value != 1 || afterBinCount.Value != 0))
        {
            reasons.Add(
                "full_shipment_settlement_shipping_bin_settlement_mismatch");
        }

        var beforeAchievements = ReadAchievements(before, reasons, "before");
        var afterAchievements = ReadAchievements(after, reasons, "after");
        var achievementBefore = beforeAchievements?.Contains(34);
        var achievementAfter = afterAchievements?.Contains(34);
        var terminalTransition = afterProgress?.Complete == true;
        if (beforeAchievements is not null && afterAchievements is not null)
        {
            if (achievementBefore != false ||
                achievementAfter != terminalTransition)
            {
                reasons.Add(
                    "full_shipment_settlement_achievement_34_state_mismatch");
            }
            if (beforeAchievements.Except(afterAchievements).Any())
            {
                reasons.Add("full_shipment_settlement_achievement_set_regressed");
            }
        }

        var beforeDay = ReadTotalDay(before, reasons, "before");
        var afterDay = ReadTotalDay(after, reasons, "after");
        if (beforeDay.HasValue && afterDay.HasValue &&
            afterDay.Value != beforeDay.Value + 1)
        {
            reasons.Add("full_shipment_settlement_native_day_transition_missing");
        }

        var distinct = reasons
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new FullShipmentSettlementEvidence
        {
            RequiredItemCount = required.Count,
            SettledItemId = settledItemId,
            SettledQualifiedItemId = settledQualifiedItemId,
            BeforeShippedItemCount = beforeProgress?.ShippedItemCount,
            AfterShippedItemCount = afterProgress?.ShippedItemCount,
            BeforeMissingItemCount =
                beforeProgress?.MissingQualifiedItemIds.Length,
            AfterMissingItemCount =
                afterProgress?.MissingQualifiedItemIds.Length,
            BeforeSettledItemShippedCount = beforeSettledItemShippedCount,
            AfterSettledItemShippedCount = afterSettledItemShippedCount,
            BeforeSettledItemBinCount = beforeBinCount,
            AfterSettledItemBinCount = afterBinCount,
            BeforeTotalDay = beforeDay,
            AfterTotalDay = afterDay,
            Achievement34Before = achievementBefore,
            Achievement34After = achievementAfter,
            TerminalTransition = terminalTransition,
            Verified = distinct.Length == 0,
            BlockingReasons = distinct
        };
    }

    private static void VerifyItemTransition(
        ProgressProjection before,
        ProgressProjection after,
        string settledQualifiedItemId,
        ICollection<string> reasons)
    {
        var beforeSettled = before.Items[settledQualifiedItemId];
        var afterSettled = after.Items[settledQualifiedItemId];
        if (beforeSettled.Shipped || beforeSettled.Count != 0 ||
            !afterSettled.Shipped || afterSettled.Count != 1)
        {
            reasons.Add(
                "full_shipment_settlement_item_progress_transition_mismatch");
        }
    }

    private static ProgressProjection? ReadProgress(
        SnapshotEnvelope snapshot,
        IReadOnlySet<string> required,
        ICollection<string> reasons,
        string phase)
    {
        if (!TryStateValue(
                snapshot,
                "world_progress",
                "full_shipment_progress",
                out var value) ||
            value.ValueKind != JsonValueKind.Object ||
            !TryReadInt(value, "eligible_item_count", out var eligible) ||
            !TryReadInt(
                value,
                "shipped_eligible_item_count",
                out var shipped) ||
            !TryReadInt(value, "missing_item_count", out var missing) ||
            !TryReadDouble(value, "completion_ratio", out var ratio) ||
            !TryReadBool(value, "complete", out var complete) ||
            !value.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array ||
            !value.TryGetProperty("missing_item_ids", out var missingIds) ||
            missingIds.ValueKind != JsonValueKind.Array)
        {
            reasons.Add(
                $"full_shipment_settlement_{phase}_progress_unavailable");
            return null;
        }

        var rows = new Dictionary<string, ShipmentItem>(StringComparer.Ordinal);
        foreach (var item in items.EnumerateArray())
        {
            var itemId = ReadString(item, "item_id");
            var qualifiedItemId = ReadString(item, "qualified_item_id");
            if (itemId.Length == 0 ||
                qualifiedItemId != "(O)" + itemId ||
                !TryReadInt(item, "current_shipped_count", out var count) ||
                count < 0 ||
                !TryReadBool(item, "shipped", out var itemShipped) ||
                itemShipped != (count > 0) ||
                !rows.TryAdd(
                    qualifiedItemId,
                    new ShipmentItem(itemId, count, itemShipped)))
            {
                reasons.Add(
                    $"full_shipment_settlement_{phase}_item_rows_invalid");
                return null;
            }
        }
        var reportedMissingIds = missingIds.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();
        var actualMissing = rows
            .Where(pair => !pair.Value.Shipped)
            .Select(pair => pair.Value.ItemId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var actualMissingQualified = rows
            .Where(pair => !pair.Value.Shipped)
            .Select(pair => pair.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var expectedRatio = rows.Count == 0
            ? 0d
            : rows.Count(pair => pair.Value.Shipped) / (double)rows.Count;
        if (!required.SetEquals(rows.Keys) ||
            eligible != required.Count ||
            items.GetArrayLength() != required.Count ||
            shipped != rows.Count(pair => pair.Value.Shipped) ||
            missing != actualMissing.Length ||
            complete != (actualMissing.Length == 0) ||
            Math.Abs(ratio - expectedRatio) > 0.0000001 ||
            reportedMissingIds.Length != actualMissing.Length ||
            !reportedMissingIds.Order(StringComparer.Ordinal)
                .SequenceEqual(actualMissing, StringComparer.Ordinal))
        {
            reasons.Add(
                $"full_shipment_settlement_{phase}_denominator_or_aggregate_mismatch");
            return null;
        }
        return new ProgressProjection(
            shipped,
            complete,
            actualMissingQualified,
            rows);
    }

    private static Dictionary<string, int>? ReadShippingCollection(
        SnapshotEnvelope snapshot,
        ICollection<string> reasons,
        string phase)
    {
        if (!TryStateValue(
                snapshot,
                "world_progress",
                "shipping_collection",
                out var value) ||
            value.ValueKind != JsonValueKind.Object)
        {
            reasons.Add(
                $"full_shipment_settlement_{phase}_shipping_collection_unavailable");
            return null;
        }
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!property.Value.TryGetInt32(out var count) || count < 0)
            {
                reasons.Add(
                    $"full_shipment_settlement_{phase}_shipping_collection_invalid");
                return null;
            }
            result.Add(property.Name, count);
        }
        return result;
    }

    private static int? ReadUniformBinCount(
        SnapshotEnvelope snapshot,
        string qualifiedItemId,
        ICollection<string> reasons,
        string phase)
    {
        if (!TryStateValue(snapshot, "farm", "shipping_bins", out var value) ||
            value.ValueKind != JsonValueKind.Array ||
            value.GetArrayLength() == 0)
        {
            reasons.Add(
                $"full_shipment_settlement_{phase}_shipping_bins_unavailable");
            return null;
        }
        var counts = new List<int>();
        foreach (var bin in value.EnumerateArray())
        {
            if (!bin.TryGetProperty("contents", out var contents) ||
                contents.ValueKind != JsonValueKind.Array)
            {
                reasons.Add(
                    $"full_shipment_settlement_{phase}_shipping_bin_contents_unavailable");
                return null;
            }
            var count = 0;
            foreach (var row in contents.EnumerateArray().Where(row =>
                         ReadString(row, "qualified_item_id") ==
                         qualifiedItemId))
            {
                if (!TryReadInt(row, "count", out var stack) || stack < 0)
                {
                    reasons.Add(
                        $"full_shipment_settlement_{phase}_shipping_bin_contents_invalid");
                    return null;
                }
                count += stack;
            }
            counts.Add(count);
        }
        if (counts.Distinct().Count() != 1)
        {
            reasons.Add(
                $"full_shipment_settlement_{phase}_shipping_bin_views_disagree");
            return null;
        }
        return counts[0];
    }

    private static HashSet<int>? ReadAchievements(
        SnapshotEnvelope snapshot,
        ICollection<string> reasons,
        string phase)
    {
        if (!TryStateValue(
                snapshot,
                "world_progress",
                "achievements",
                out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            reasons.Add($"full_shipment_settlement_{phase}_achievements_unavailable");
            return null;
        }
        var result = new HashSet<int>();
        foreach (var item in value.EnumerateArray())
        {
            if (!item.TryGetInt32(out var id) || id < 0 || !result.Add(id))
            {
                reasons.Add($"full_shipment_settlement_{phase}_achievements_invalid");
                return null;
            }
        }
        return result;
    }

    private static int? ReadTotalDay(
        SnapshotEnvelope snapshot,
        ICollection<string> reasons,
        string phase)
    {
        if (!TryStateValue(snapshot, "time", "total_days", out var value) ||
            !value.TryGetInt32(out var result) || result < 0)
        {
            reasons.Add($"full_shipment_settlement_{phase}_total_day_unavailable");
            return null;
        }
        return result;
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

    private static bool TryReadInt(
        JsonElement row,
        string property,
        out int value)
    {
        value = 0;
        return row.TryGetProperty(property, out var field) &&
            field.TryGetInt32(out value);
    }

    private static bool TryReadDouble(
        JsonElement row,
        string property,
        out double value)
    {
        value = 0;
        return row.TryGetProperty(property, out var field) &&
            field.TryGetDouble(out value);
    }

    private static bool TryReadBool(
        JsonElement row,
        string property,
        out bool value)
    {
        value = false;
        if (!row.TryGetProperty(property, out var field) ||
            field.ValueKind is not (
                JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }
        value = field.GetBoolean();
        return true;
    }

    private static string ReadString(JsonElement row, string property) =>
        row.TryGetProperty(property, out var field) &&
        field.ValueKind == JsonValueKind.String
            ? field.GetString() ?? string.Empty
            : string.Empty;

    internal sealed record ShipmentItem(
        string ItemId,
        int Count,
        bool Shipped);

    private sealed record ProgressProjection(
        int ShippedItemCount,
        bool Complete,
        string[] MissingQualifiedItemIds,
        IReadOnlyDictionary<string, ShipmentItem> Items);
}

internal sealed record FullShipmentProgressCheckpoint(
    int ShippedItemCount,
    bool Complete,
    string[] MissingQualifiedItemIds,
    IReadOnlyDictionary<string, FullShipmentSettlementVerifier.ShipmentItem>
        Items,
    IReadOnlyDictionary<string, int> ShippingCollection,
    bool Achievement34,
    int TotalDay);

using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class NativeShippingDepositEvidenceVerifier
{
    private static NativeShippingDepositInventoryRead? ReadInventory(
        SnapshotEnvelope snapshot,
        int slotIndex,
        string qualifiedItemId,
        bool expectedEmpty,
        ICollection<string> reasons,
        string boundary,
        int? expectedQuality = null,
        int? expectedUnitPrice = null)
    {
        if (!TryStateValue(snapshot, "player", "inventory", out var inventory) ||
            inventory.ValueKind != JsonValueKind.Array)
        {
            reasons.Add("shipping_deposit_" + boundary +
                "_inventory_unavailable");
            return null;
        }
        var slots = inventory.EnumerateArray()
            .Where(row => ReadInt(row, "slot_index") == slotIndex)
            .ToArray();
        if (slots.Length != 1)
        {
            reasons.Add("shipping_deposit_" + boundary +
                "_inventory_slot_not_unique");
            return null;
        }
        var slot = slots[0];
        var slotQualifiedItemId = ReadString(slot, "qualified_item_id");
        var empty = ReadBool(slot, "is_empty") == true ||
            string.IsNullOrWhiteSpace(slotQualifiedItemId);
        if (expectedEmpty && empty)
        {
            return new NativeShippingDepositInventoryRead(
                0,
                CountInventoryItem(inventory, qualifiedItemId, reasons, boundary),
                expectedQuality ?? 0,
                expectedUnitPrice ?? 0);
        }
        if (empty || !string.Equals(
                slotQualifiedItemId,
                qualifiedItemId,
                StringComparison.Ordinal) ||
            !TryReadNonNegativeInt(slot, "stack", out var stack) ||
            stack <= 0 ||
            !TryReadNonNegativeInt(slot, "quality", out var quality) ||
            !TryReadNonNegativeInt(
                slot,
                "sell_to_store_price",
                out var unitPrice) ||
            unitPrice <= 0)
        {
            reasons.Add("shipping_deposit_" + boundary +
                "_inventory_slot_identity_invalid");
            return null;
        }
        if (expectedQuality.HasValue && quality != expectedQuality.Value)
            reasons.Add("shipping_deposit_inventory_quality_drifted");
        if (expectedUnitPrice.HasValue && unitPrice != expectedUnitPrice.Value)
            reasons.Add("shipping_deposit_inventory_unit_price_drifted");
        return new NativeShippingDepositInventoryRead(
            stack,
            CountInventoryItem(inventory, qualifiedItemId, reasons, boundary),
            quality,
            unitPrice);
    }

    private static int CountInventoryItem(
        JsonElement inventory,
        string qualifiedItemId,
        ICollection<string> reasons,
        string boundary)
    {
        try
        {
            return inventory.EnumerateArray()
                .Where(row => string.Equals(
                    ReadString(row, "qualified_item_id"),
                    qualifiedItemId,
                    StringComparison.Ordinal))
                .Aggregate(0, (count, row) =>
                {
                    if (!TryReadNonNegativeInt(row, "stack", out var stack))
                    {
                        throw new InvalidDataException(
                            "Matching inventory stack is invalid.");
                    }
                    return checked(count + stack);
                });
        }
        catch (Exception exception) when (exception is InvalidDataException or
            OverflowException)
        {
            reasons.Add("shipping_deposit_" + boundary +
                "_inventory_item_count_invalid");
            return -1;
        }
    }

    private static NativeShippingDepositBinRead? ReadBin(
        SnapshotEnvelope snapshot,
        int tileX,
        int tileY,
        string qualifiedItemId,
        int quality,
        int unitPrice,
        ICollection<string> reasons,
        string boundary)
    {
        if (!TryStateValue(snapshot, "farm", "shipping_bins", out var bins) ||
            bins.ValueKind != JsonValueKind.Array)
        {
            reasons.Add("shipping_deposit_" + boundary + "_bins_unavailable");
            return null;
        }
        var endpoints = bins.EnumerateArray()
            .Where(row => ReadInt(row, "tile_x") == tileX &&
                ReadInt(row, "tile_y") == tileY)
            .ToArray();
        if (endpoints.Length != 1)
        {
            reasons.Add("shipping_deposit_" + boundary +
                "_bin_endpoint_not_unique");
            return null;
        }
        var endpoint = endpoints[0];
        if (!endpoint.TryGetProperty("contents", out var contents) ||
            contents.ValueKind != JsonValueKind.Array ||
            ReadBool(endpoint, "completed") != true ||
            ReadBool(endpoint, "contents_truncated") != false ||
            ReadBool(endpoint, "pending_settlement_value_complete") != true ||
            !TryReadLong(endpoint, "pending_settlement_value",
                out var pendingValue) || pendingValue < 0)
        {
            reasons.Add("shipping_deposit_" + boundary +
                "_bin_contents_incomplete");
            return null;
        }
        var scope = ReadString(endpoint, "bin_scope");
        var playerId = ReadLongOrString(endpoint, "player_id");
        if (scope is not ("shared" or "personal") ||
            string.IsNullOrWhiteSpace(playerId) ||
            !string.Equals(playerId, snapshot.PlayerId.Value,
                StringComparison.Ordinal))
        {
            reasons.Add("shipping_deposit_" + boundary +
                "_bin_scope_unavailable");
        }
        try
        {
            var contentRows = contents.EnumerateArray().ToArray();
            var calculatedPendingValue = contentRows.Aggregate(
                0L,
                (total, row) => checked(total +
                    RequiredNonNegativeLong(
                        row,
                        "total_sell_to_store_price")));
            if (calculatedPendingValue != pendingValue)
            {
                reasons.Add("shipping_deposit_" + boundary +
                    "_pending_value_content_mismatch");
            }
            foreach (var row in contentRows)
            {
                var rowCount = RequiredNonNegativeInt(row, "count");
                var rowUnitPrice = RequiredNonNegativeInt(
                    row,
                    "unit_sell_to_store_price");
                var rowTotal = RequiredNonNegativeLong(
                    row,
                    "total_sell_to_store_price");
                if (rowTotal != checked((long)rowCount * rowUnitPrice))
                {
                    reasons.Add("shipping_deposit_" + boundary +
                        "_bin_row_value_mismatch");
                }
            }
            var itemRows = contentRows
                .Where(row => string.Equals(
                    ReadString(row, "qualified_item_id"),
                    qualifiedItemId,
                    StringComparison.Ordinal))
                .ToArray();
            var itemQuantity = itemRows.Aggregate(0, (count, row) =>
                checked(count + RequiredNonNegativeInt(row, "count")));
            var variantRows = itemRows
                .Where(row => ReadInt(row, "quality") == quality &&
                    ReadInt(row, "unit_sell_to_store_price") == unitPrice)
                .ToArray();
            if (variantRows.Length > 1)
            {
                reasons.Add("shipping_deposit_" + boundary +
                    "_bin_variant_not_unique");
            }
            var variantQuantity = variantRows.Length == 0
                ? 0
                : RequiredNonNegativeInt(variantRows[0], "count");
            return new NativeShippingDepositBinRead(
                scope,
                playerId,
                variantQuantity,
                itemQuantity,
                pendingValue);
        }
        catch (Exception exception) when (exception is InvalidDataException or
            OverflowException)
        {
            reasons.Add("shipping_deposit_" + boundary +
                "_bin_item_count_invalid");
            return null;
        }
    }

    private static (int Before, int After)? ReadChangedFact(
        JsonElement changedFacts,
        string path)
    {
        if (changedFacts.ValueKind != JsonValueKind.Array)
            return null;
        var matches = changedFacts.EnumerateArray()
            .Where(value => string.Equals(
                ReadString(value, "path"),
                path,
                StringComparison.Ordinal))
            .ToArray();
        return matches.Length == 1 &&
            TryReadInt(matches[0], "before", out var before) &&
            TryReadInt(matches[0], "after", out var after)
                ? (before, after)
                : null;
    }

}

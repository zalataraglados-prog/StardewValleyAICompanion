using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    private static bool TryReadAccessShop(
        JsonElement access,
        string shopId,
        int rowIndex,
        JsonElement sourceRow,
        out JsonElement shop,
        out JsonElement stockRow,
        out string error)
    {
        shop = default;
        stockRow = default;
        error = string.Empty;
        if (ReadString(access, "schema_version") !=
                "stardewai.access_constraint_index.v1" ||
            !access.TryGetProperty("summary", out var summary) ||
            ReadInt(summary, "blockingIssueCount") != 0 ||
            !access.TryGetProperty("shops", out var accessShops) ||
            accessShops.ValueKind != JsonValueKind.Array)
        {
            error = "access_constraint_index_is_incomplete";
            return false;
        }

        foreach (var candidate in accessShops.EnumerateArray())
        {
            if (ReadString(candidate, "shopId") != shopId)
                continue;
            shop = candidate;
            break;
        }
        if (shop.ValueKind != JsonValueKind.Object ||
            !shop.TryGetProperty("stock", out var stock) ||
            stock.ValueKind != JsonValueKind.Array ||
            rowIndex >= stock.GetArrayLength())
        {
            error = "access_constraint_shop_stock_row_not_found";
            return false;
        }

        stockRow = stock[rowIndex];
        if (ReadString(stockRow, "id") != ReadString(sourceRow, "Id") ||
            ReadString(stockRow, "itemId") != ReadString(sourceRow, "ItemId") ||
            ReadNullableString(stockRow, "condition") !=
                ReadNullableString(sourceRow, "Condition") ||
            ReadNullableString(stockRow, "perItemCondition") !=
                ReadNullableString(sourceRow, "PerItemCondition"))
        {
            error = "access_constraint_shop_stock_row_drifted";
            return false;
        }
        return true;
    }

    private static bool NativeConditionRecordComplete(
        JsonElement accessRow,
        string property,
        string? rawCondition)
    {
        if (string.IsNullOrWhiteSpace(rawCondition))
            return !accessRow.TryGetProperty(property, out var absent) ||
                absent.ValueKind == JsonValueKind.Null;
        if (!accessRow.TryGetProperty(property, out var parsed) ||
            parsed.ValueKind != JsonValueKind.Object ||
            !parsed.TryGetProperty("clauses", out var clauses) ||
            clauses.ValueKind != JsonValueKind.Array ||
            clauses.GetArrayLength() == 0)
        {
            return false;
        }
        return clauses.EnumerateArray().All(clause =>
            (!clause.TryGetProperty("error", out var error) ||
             error.ValueKind == JsonValueKind.Null) &&
            clause.TryGetProperty("handler", out var handler) &&
            handler.ValueKind == JsonValueKind.Object &&
            ReadString(handler, "canonicalKey").Length > 0);
    }

    private static AcquisitionShopOwnerEvidence[] ReadShopOwners(JsonElement shop) =>
        shop.TryGetProperty("owners", out var owners) &&
        owners.ValueKind == JsonValueKind.Array
            ? owners.EnumerateArray().Select(owner => new AcquisitionShopOwnerEvidence(
                ReadNullableString(owner, "id"),
                ReadNullableString(owner, "name"),
                ReadInt(owner, "type"),
                ReadNullableString(owner, "condition"))).ToArray()
            : Array.Empty<AcquisitionShopOwnerEvidence>();

    private static AcquisitionShopInteractionEndpointEvidence[] ReadShopEndpoints(
        JsonElement access,
        string shopId) =>
        access.TryGetProperty("shop_endpoints", out var endpoints) &&
        endpoints.ValueKind == JsonValueKind.Array
            ? endpoints.EnumerateArray()
                .Where(endpoint => ReadString(endpoint, "shopId") == shopId)
                .Select(endpoint => new AcquisitionShopInteractionEndpointEvidence(
                    ReadString(endpoint, "mapAsset"),
                    ReadString(endpoint, "layer"),
                    ReadInt(endpoint, "x"),
                    ReadInt(endpoint, "y"),
                    ReadString(endpoint, "handlerKey"),
                    ReadString(endpoint, "rawAction"),
                    ReadString(endpoint, "resolution")))
                .OrderBy(endpoint => endpoint.MapAsset, StringComparer.Ordinal)
                .ThenBy(endpoint => endpoint.Y)
                .ThenBy(endpoint => endpoint.X)
                .ToArray()
            : Array.Empty<AcquisitionShopInteractionEndpointEvidence>();

    private static AcquisitionShopDoorWindowEvidence[] ReadShopDoorWindows(
        JsonElement access,
        IReadOnlyList<AcquisitionShopInteractionEndpointEvidence> endpoints)
    {
        if (!access.TryGetProperty("door_windows", out var doors) ||
            doors.ValueKind != JsonValueKind.Array)
            return Array.Empty<AcquisitionShopDoorWindowEvidence>();
        var destinations = endpoints
            .Select(endpoint => endpoint.MapAsset.Replace('\\', '/'))
            .Select(map => map[(map.LastIndexOf('/') + 1)..])
            .ToHashSet(StringComparer.Ordinal);
        return doors.EnumerateArray()
            .Where(door => destinations.Contains(
                ReadString(door, "destinationLocation")))
            .Select(door => new AcquisitionShopDoorWindowEvidence(
                ReadString(door, "mapAsset"),
                ReadInt(door, "x"),
                ReadInt(door, "y"),
                ReadString(door, "destinationLocation"),
                ReadInt(door, "destinationX"),
                ReadInt(door, "destinationY"),
                ReadInt(door, "openTime"),
                ReadInt(door, "closeTime"),
                ReadNullableString(door, "requiredNpc"),
                ReadInt(door, "minimumFriendship"),
                ReadString(door, "rawAction")))
            .OrderBy(door => door.MapAsset, StringComparer.Ordinal)
            .ThenBy(door => door.Y)
            .ThenBy(door => door.X)
            .ToArray();
    }
}

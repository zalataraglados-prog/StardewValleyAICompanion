using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    private static CalendarSourceResolution ResolveShopWindows(
        string qualifiedItemId,
        AcquisitionRequirementRouteLowering route,
        JsonElement shops,
        JsonElement accessConstraints,
        int deadlineTotalDayExclusive)
    {
        if (!TryParseShopSource(route, out var shopId, out var rowIndex))
        {
            return BlockShop("blocked_authoritative_shop_row_not_found",
                "shop_route_source_identity_is_malformed");
        }
        if (!shops.TryGetProperty(shopId, out var shop))
        {
            return BlockShop("blocked_authoritative_shop_row_not_found",
                "shop_id_not_found_in_runtime_data_shops");
        }
        if (!shop.TryGetProperty("Items", out var items) ||
            items.ValueKind != JsonValueKind.Array ||
            rowIndex < 0 || rowIndex >= items.GetArrayLength())
        {
            return BlockShop("blocked_authoritative_shop_row_not_found",
                "shop_stock_row_not_found_in_runtime_data_shops");
        }

        var row = items[rowIndex];
        if (!string.Equals(ReadString(row, "ItemId"), qualifiedItemId,
                StringComparison.Ordinal))
        {
            return BlockShop("blocked_authoritative_shop_item_mismatch",
                "shop_row_item_id_does_not_match_requirement");
        }

        if (!TryReadAccessShop(accessConstraints, shopId, rowIndex, row,
                out var accessShop, out var accessRow, out var accessError))
        {
            return BlockShop("blocked_authoritative_shop_access_binding",
                accessError);
        }

        if (!TryBuildShopCalendarProjection(
                new[]
                {
                    ReadNullableString(row, "Condition"),
                    ReadNullableString(row, "PerItemCondition")
                },
                out var calendar,
                out var calendarErrors))
        {
            return new CalendarSourceResolution(
                "blocked_unparsed_shop_calendar_condition",
                string.Empty,
                Array.Empty<AuthoritativeCalendarSourceWindow>(),
                calendarErrors);
        }

        var conditionHandlersComplete =
            NativeConditionRecordComplete(accessRow, "parsedCondition",
                ReadNullableString(row, "Condition")) &&
            NativeConditionRecordComplete(accessRow, "parsedPerItemCondition",
                ReadNullableString(row, "PerItemCondition"));
        if (!conditionHandlersComplete)
        {
            return BlockShop("blocked_unresolved_native_shop_condition",
                "shop_condition_is_not_bound_to_a_native_query_handler");
        }

        var endpoints = ReadShopEndpoints(accessConstraints, shopId);
        var doorWindows = ReadShopDoorWindows(accessConstraints, endpoints);
        var owners = ReadShopOwners(accessShop);
        var stochastic = HasStochasticShopSource(row, calendar.DynamicConditions);
        var windows = ExpandShopWindows(
            shopId,
            rowIndex,
            ReadString(row, "Id"),
            calendar,
            deadlineTotalDayExclusive,
            stochastic);
        if (windows.Length == 0)
        {
            return BlockShop("blocked_shop_calendar_after_deadline",
                "shop_stock_condition_has_no_window_before_deadline");
        }

        var source = new AcquisitionShopSourceEvidence(
            shopId,
            rowIndex,
            ReadString(row, "Id"),
            Sha256(row.GetRawText()),
            ReadInt(shop, "Currency"),
            ReadString(row, "ItemId"),
            ReadNullableString(row, "RandomItemId"),
            ReadInt(row, "Price"),
            ReadInt(row, "AvailableStock"),
            ReadInt(row, "AvailableStockLimit"),
            ReadNullableString(row, "TradeItemId"),
            ReadInt(row, "TradeItemAmount"),
            ReadBool(row, "IsRecipe"),
            ReadNullableString(row, "Condition"),
            ReadNullableString(row, "PerItemCondition"),
            ReadBool(row, "AvoidRepeat"),
            ReadBool(row, "UseObjectDataPrice"),
            ReadNullableBool(row, "ApplyProfitMargins"),
            ReadBool(row, "IgnoreShopPriceModifiers"),
            ReadStringArray(row, "ActionsOnPurchase"),
            conditionHandlersComplete,
            RequiresItemQueryResolution(row),
            RequiresPriceModifierResolution(shop, row),
            RequiresStockModifierResolution(row),
            owners.Any(owner => owner.Type is 0 or 1),
            true,
            true,
            owners,
            endpoints,
            doorWindows);

        return new CalendarSourceResolution(
            ResolvedStatus,
            "runtime_shop_stock_calendar_projection",
            windows,
            Array.Empty<string>(),
            ShopSource: source);
    }

    private static CalendarSourceResolution BlockShop(string status, string reason) =>
        new(
            status,
            string.Empty,
            Array.Empty<AuthoritativeCalendarSourceWindow>(),
            new[] { reason });

    private static bool TryParseShopSource(
        AcquisitionRequirementRouteLowering route,
        out string shopId,
        out int rowIndex)
    {
        shopId = string.Empty;
        rowIndex = -1;
        if (!string.Equals(route.SourceAsset, "Data/Shops", StringComparison.Ordinal) ||
            !route.SourceId.StartsWith("shop:", StringComparison.Ordinal))
        {
            return false;
        }

        shopId = route.SourceId["shop:".Length..];
        var prefix = "payload." + shopId + ".Items[";
        return shopId.Length > 0 &&
            route.SourcePath.StartsWith(prefix, StringComparison.Ordinal) &&
            route.SourcePath.EndsWith(']') &&
            int.TryParse(
                route.SourcePath[prefix.Length..^1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out rowIndex);
    }

    private static bool RequiresItemQueryResolution(JsonElement row) =>
        !string.IsNullOrWhiteSpace(ReadNullableString(row, "RandomItemId")) ||
        row.TryGetProperty("MaxItems", out var maxItems) &&
        maxItems.ValueKind != JsonValueKind.Null;

    private static bool RequiresPriceModifierResolution(
        JsonElement shop,
        JsonElement row) =>
        ReadInt(row, "Price") < 0 ||
        !ReadBool(row, "IgnoreShopPriceModifiers") && HasRows(shop, "PriceModifiers") ||
        HasRows(row, "PriceModifiers") ||
        ReadNullableBool(row, "ApplyProfitMargins") != false;

    private static bool RequiresStockModifierResolution(JsonElement row) =>
        HasRows(row, "AvailableStockModifiers") ||
        ReadInt(row, "AvailableStock") >= 0 ||
        ReadInt(row, "AvailableStockLimit") != 0;

    private static bool HasRows(JsonElement value, string property) =>
        value.TryGetProperty(property, out var rows) &&
        rows.ValueKind == JsonValueKind.Array &&
        rows.GetArrayLength() > 0;

    private static bool HasStochasticShopSource(
        JsonElement row,
        IEnumerable<string> dynamicConditions) =>
        RequiresItemQueryResolution(row) ||
        dynamicConditions.Any(condition =>
            condition.Contains("RANDOM", StringComparison.Ordinal) ||
            condition.Contains("CHOICE", StringComparison.Ordinal));

    private static string? ReadNullableString(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string[] ReadStringArray(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .ToArray()
            : Array.Empty<string>();

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

}

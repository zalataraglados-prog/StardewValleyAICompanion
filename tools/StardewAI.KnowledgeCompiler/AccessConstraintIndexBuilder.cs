using System.Globalization;
using System.Text.Json;

namespace StardewAI.KnowledgeCompiler;

internal sealed class AccessConstraintIndexBuilder
{
    private static readonly IReadOnlyDictionary<string, string> DirectShopActions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AdventureShop"] = "AdventureShop",
            ["AnimalShop"] = "AnimalShop",
            ["Blacksmith"] = "Blacksmith",
            ["Carpenter"] = "Carpenter",
            ["ClubShop"] = "Casino",
            ["HospitalShop"] = "Hospital",
            ["JojaShop"] = "Joja",
            ["QiGemShop"] = "QiGemShop",
            ["Saloon"] = "Saloon"
        };

    public AccessConstraintIndex Build(
        IReadOnlyDictionary<string, PayloadAsset> payloads,
        MapTopologyIndex topology,
        IReadOnlyList<NativeConditionRecord> conditions)
    {
        var issues = new List<AccessConstraintIssue>();
        var shops = BuildShops(payloads, conditions, issues);
        var doors = new List<DoorAccessWindow>();
        var endpoints = new List<ShopInteractionEndpoint>();
        BuildMapAccess(topology, doors, endpoints, issues);
        var schedules = BuildSchedules(payloads, issues);
        var knownShops = shops.Select(row => row.ShopId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var endpoint in endpoints.Where(row =>
                     row.ShopId is not null && !knownShops.Contains(row.ShopId)))
        {
            issues.Add(new(
                "blocking",
                "map_shop_endpoint_unknown_shop",
                endpoint.MapAsset,
                $"shop={endpoint.ShopId};action={endpoint.RawAction}"));
        }

        return new(
            shops,
            doors.OrderBy(row => row.MapAsset, StringComparer.Ordinal)
                .ThenBy(row => row.Y)
                .ThenBy(row => row.X)
                .ToArray(),
            endpoints.OrderBy(row => row.MapAsset, StringComparer.Ordinal)
                .ThenBy(row => row.Y)
                .ThenBy(row => row.X)
                .ToArray(),
            schedules,
            issues,
            new(
                shops.Count,
                shops.Sum(row => row.Owners.Count),
                shops.Sum(row => row.Stock.Count),
                doors.Count,
                endpoints.Count,
                endpoints.Count(row => row.ShopId is not null),
                schedules.Count,
                schedules.Sum(row => row.Entries.Count),
                schedules.Sum(row => row.Entries.Sum(entry => entry.Segments.Count)),
                issues.Count(row => row.Severity == "blocking")));
    }

    private static IReadOnlyList<ShopAccessRecord> BuildShops(
        IReadOnlyDictionary<string, PayloadAsset> payloads,
        IReadOnlyList<NativeConditionRecord> conditions,
        ICollection<AccessConstraintIssue> issues)
    {
        if (!payloads.TryGetValue("Data/Shops", out var asset) ||
            asset.Payload.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("blocking", "shop_payload_missing", "Data/Shops", string.Empty));
            return Array.Empty<ShopAccessRecord>();
        }

        var result = new List<ShopAccessRecord>();
        foreach (var shop in asset.Payload.EnumerateObject())
        {
            var path = "payload." + shop.Name;
            var owners = Elements(shop.Value, "Owners").Select((owner, index) => new ShopOwnerRecord(
                String(owner, "Id"),
                String(owner, "Name"),
                Int(owner, "Type"),
                String(owner, "Condition"),
                ConditionAt(conditions, $"{path}.Owners[{index}].Condition")))
                .ToArray();
            var stock = Elements(shop.Value, "Items").Select((item, index) => new ShopStockRecord(
                String(item, "Id"),
                String(item, "ItemId"),
                String(item, "RandomItemId"),
                Int(item, "Price"),
                Int(item, "AvailableStock"),
                String(item, "TradeItemId"),
                Int(item, "TradeItemAmount"),
                Bool(item, "IsRecipe"),
                String(item, "Condition"),
                String(item, "PerItemCondition"),
                ConditionAt(conditions, $"{path}.Items[{index}].Condition"),
                ConditionAt(conditions, $"{path}.Items[{index}].PerItemCondition"),
                item.Clone()))
                .ToArray();
            result.Add(new(
                shop.Name,
                Int(shop.Value, "Currency"),
                owners,
                stock,
                conditions.Where(row =>
                        string.Equals(row.SourceAsset, "Data/Shops", StringComparison.OrdinalIgnoreCase) &&
                        row.SourcePath.StartsWith(path + ".", StringComparison.Ordinal))
                    .ToArray(),
                shop.Value.Clone()));
        }
        return result.OrderBy(row => row.ShopId, StringComparer.Ordinal).ToArray();
    }

    private static void BuildMapAccess(
        MapTopologyIndex topology,
        ICollection<DoorAccessWindow> doors,
        ICollection<ShopInteractionEndpoint> endpoints,
        ICollection<AccessConstraintIssue> issues)
    {
        foreach (var map in topology.Maps)
        {
            foreach (var interaction in map.Interactions.Where(row =>
                         row.EffectiveUnderNativePropertyPrecedence &&
                         row.PropertyName == "Action"))
            {
                var tokens = SplitQuoteAware(interaction.Value);
                if (tokens.Count == 0)
                    continue;

                if (tokens[0].Equals("LockedDoorWarp", StringComparison.OrdinalIgnoreCase))
                {
                    if (tokens.Count < 6 ||
                        !Int(tokens[1], out var destinationX) ||
                        !Int(tokens[2], out var destinationY) ||
                        !Int(tokens[4], out var openTime) ||
                        !Int(tokens[5], out var closeTime) ||
                        tokens.Count > 7 && !Int(tokens[7], out _))
                    {
                        issues.Add(new(
                            "blocking",
                            "locked_door_warp_parse_failure",
                            map.AssetName,
                            $"tile={interaction.X},{interaction.Y};action={interaction.Value}"));
                        continue;
                    }
                    doors.Add(new(
                        map.AssetName,
                        interaction.X,
                        interaction.Y,
                        tokens[3],
                        destinationX,
                        destinationY,
                        openTime,
                        closeTime,
                        tokens.Count > 6 ? tokens[6] : null,
                        tokens.Count > 7 ? int.Parse(tokens[7], CultureInfo.InvariantCulture) : 0,
                        interaction.Value));
                    continue;
                }

                var shopId = ResolveShopId(tokens);
                if (shopId is not null || IsShopInteraction(tokens[0]))
                {
                    endpoints.Add(new(
                        map.AssetName,
                        interaction.Layer,
                        interaction.X,
                        interaction.Y,
                        shopId,
                        tokens[0],
                        tokens,
                        interaction.Value,
                        shopId is null
                            ? "native_location_handler_context_resolution"
                            : "exact_1.6.15_GameLocation_handler_mapping"));
                }
            }
        }
    }

    private static string? ResolveShopId(IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0)
            return null;
        if (tokens[0].Equals("Shop", StringComparison.OrdinalIgnoreCase) && tokens.Count > 1)
            return tokens[1].Equals("shop", StringComparison.OrdinalIgnoreCase) ? null : tokens[1];
        if (tokens[0].Equals("Buy", StringComparison.OrdinalIgnoreCase) && tokens.Count > 1)
        {
            return tokens[1] switch
            {
                "General" => "SeedShop",
                "Fish" => "FishShop",
                "SandyShop" => "Sandy",
                _ => null
            };
        }
        return DirectShopActions.TryGetValue(tokens[0], out var shopId) ? shopId : null;
    }

    private static bool IsShopInteraction(string command) =>
        DirectShopActions.ContainsKey(command) ||
        command.Equals("Shop", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("Buy", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("DesertEggShop", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<NpcScheduleAsset> BuildSchedules(
        IReadOnlyDictionary<string, PayloadAsset> payloads,
        ICollection<AccessConstraintIssue> issues)
    {
        var result = new List<NpcScheduleAsset>();
        foreach (var asset in payloads.Values.Where(row =>
                     row.AssetName.StartsWith("Characters/schedules/", StringComparison.OrdinalIgnoreCase)))
        {
            if (asset.Payload.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new(
                    "blocking",
                    "npc_schedule_payload_not_object",
                    asset.AssetName,
                    asset.Payload.ValueKind.ToString()));
                continue;
            }
            var npc = asset.AssetName["Characters/schedules/".Length..];
            var entries = new List<NpcScheduleEntry>();
            foreach (var entry in asset.Payload.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.String)
                {
                    issues.Add(new(
                        "blocking",
                        "npc_schedule_value_not_string",
                        asset.AssetName + ":" + entry.Name,
                        entry.Value.ValueKind.ToString()));
                    continue;
                }
                var raw = entry.Value.GetString() ?? string.Empty;
                entries.Add(new(
                    entry.Name,
                    Hashing.Sha256(raw),
                    SplitSchedule(raw, asset.AssetName, entry.Name, issues)));
            }
            result.Add(new(
                asset.AssetName,
                npc,
                "NPC.TryLoadSchedule exact precedence; selection remains day/weather/festival/marriage/mail/world-state context-bound",
                entries.OrderBy(row => row.ScheduleKey, StringComparer.Ordinal).ToArray()));
        }
        return result.OrderBy(row => row.NpcName, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<NpcScheduleSegment> SplitSchedule(
        string raw,
        string asset,
        string key,
        ICollection<AccessConstraintIssue> issues)
    {
        var result = new List<NpcScheduleSegment>();
        var segments = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            var tokens = segment.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var isLocationReplacement = key.EndsWith("_Replacement", StringComparison.Ordinal);
            var kind = isLocationReplacement
                ? "location_replacement"
                : segment.Contains("GOTO", StringComparison.Ordinal)
                ? "goto"
                : segment.Contains("NOT", StringComparison.Ordinal)
                    ? "not_guard"
                    : segment.Contains("MAIL", StringComparison.Ordinal)
                        ? "mail_guard"
                        : "route";
            int? time = null;
            string? location = null;
            int? x = null;
            int? y = null;
            int? facing = null;
            var arrival = false;
            if (isLocationReplacement)
            {
                if (tokens.Length < 4 ||
                    !Int(tokens[1], out var parsedX) ||
                    !Int(tokens[2], out var parsedY) ||
                    !Int(tokens[3], out var parsedFacing))
                {
                    issues.Add(new(
                        "blocking",
                        "npc_schedule_location_replacement_invalid",
                        asset + ":" + key,
                        segment));
                }
                else
                {
                    location = tokens[0];
                    x = parsedX;
                    y = parsedY;
                    facing = parsedFacing;
                }
            }
            else if (kind == "route")
            {
                var timeToken = tokens.FirstOrDefault() ?? string.Empty;
                arrival = timeToken.StartsWith('a');
                if (arrival)
                    timeToken = timeToken[1..];
                if (!Int(timeToken, out var parsedTime))
                {
                    issues.Add(new(
                        "blocking",
                        "npc_schedule_time_invalid",
                        asset + ":" + key,
                        segment));
                }
                else
                {
                    time = parsedTime;
                }

                if (tokens.Length > 1)
                {
                    location = tokens[1];
                    if (location != "bed" && !Int(location, out _) && tokens.Length >= 4)
                    {
                        if (Int(tokens[2], out var parsedX) && Int(tokens[3], out var parsedY))
                        {
                            x = parsedX;
                            y = parsedY;
                            if (tokens.Length > 4 && Int(tokens[4], out var parsedFacing))
                                facing = parsedFacing;
                        }
                        else
                        {
                            issues.Add(new(
                                "blocking",
                                "npc_schedule_tile_invalid",
                                asset + ":" + key,
                                segment));
                        }
                    }
                }
            }
            result.Add(new(index, kind, segment, tokens, time, arrival, location, x, y, facing));
        }
        return result;
    }

    private static NativeConditionRecord? ConditionAt(
        IReadOnlyList<NativeConditionRecord> conditions,
        string path) =>
        conditions.FirstOrDefault(row =>
            string.Equals(row.SourceAsset, "Data/Shops", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(row.SourcePath, path, StringComparison.Ordinal));

    private static IReadOnlyList<JsonElement> Elements(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array
            ? property.EnumerateArray().Select(row => row.Clone()).ToArray()
            : Array.Empty<JsonElement>();

    private static IReadOnlyList<string> SplitQuoteAware(string input)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        foreach (var character in input)
        {
            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }
            if (character == ' ' && !quoted)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }
            current.Append(character);
        }
        if (current.Length > 0)
            result.Add(current.ToString());
        return result;
    }

    private static bool Int(string value, out int result) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    private static string? String(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static int? Int(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var result)
            ? result
            : null;

    private static bool? Bool(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property) &&
        property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : null;
}

internal sealed record AccessConstraintIndex(
    IReadOnlyList<ShopAccessRecord> Shops,
    IReadOnlyList<DoorAccessWindow> DoorWindows,
    IReadOnlyList<ShopInteractionEndpoint> ShopEndpoints,
    IReadOnlyList<NpcScheduleAsset> NpcSchedules,
    IReadOnlyList<AccessConstraintIssue> Issues,
    AccessConstraintSummary Summary);

internal sealed record AccessConstraintSummary(
    int ShopCount,
    int ShopOwnerCount,
    int ShopStockRowCount,
    int DoorWindowCount,
    int ShopEndpointCount,
    int DirectShopEndpointCount,
    int NpcScheduleAssetCount,
    int NpcScheduleEntryCount,
    int NpcScheduleSegmentCount,
    int BlockingIssueCount);

internal sealed record ShopAccessRecord(
    string ShopId,
    int? Currency,
    IReadOnlyList<ShopOwnerRecord> Owners,
    IReadOnlyList<ShopStockRecord> Stock,
    IReadOnlyList<NativeConditionRecord> Conditions,
    JsonElement Definition);

internal sealed record ShopOwnerRecord(
    string? Id,
    string? Name,
    int? Type,
    string? Condition,
    NativeConditionRecord? ParsedCondition);

internal sealed record ShopStockRecord(
    string? Id,
    string? ItemId,
    string? RandomItemId,
    int? Price,
    int? AvailableStock,
    string? TradeItemId,
    int? TradeItemAmount,
    bool? IsRecipe,
    string? Condition,
    string? PerItemCondition,
    NativeConditionRecord? ParsedCondition,
    NativeConditionRecord? ParsedPerItemCondition,
    JsonElement Definition);

internal sealed record DoorAccessWindow(
    string MapAsset,
    int X,
    int Y,
    string DestinationLocation,
    int DestinationX,
    int DestinationY,
    int OpenTime,
    int CloseTime,
    string? RequiredNpc,
    int MinimumFriendship,
    string RawAction);

internal sealed record ShopInteractionEndpoint(
    string MapAsset,
    string Layer,
    int X,
    int Y,
    string? ShopId,
    string HandlerKey,
    IReadOnlyList<string> Tokens,
    string RawAction,
    string Resolution);

internal sealed record NpcScheduleAsset(
    string AssetName,
    string NpcName,
    string SelectionAuthority,
    IReadOnlyList<NpcScheduleEntry> Entries);

internal sealed record NpcScheduleEntry(
    string ScheduleKey,
    string RawSha256,
    IReadOnlyList<NpcScheduleSegment> Segments);

internal sealed record NpcScheduleSegment(
    int Index,
    string Kind,
    string Raw,
    IReadOnlyList<string> Tokens,
    int? Time,
    bool ArrivalTime,
    string? Location,
    int? X,
    int? Y,
    int? Facing);

internal sealed record AccessConstraintIssue(
    string Severity,
    string Code,
    string Subject,
    string Detail);

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] HomeRenovationCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] intent)
    {
        var renovationId = HomeRenovationIntentParameter(intent, "renovation_id");
        var reason = HomeRenovationIntentParameter(intent, "renovation_reason");
        var selectedIndex = ParseHomeRenovationIntentInt(intent, "selected_index");
        var confirmed = HomeRenovationIntentParameter(intent, "confirm_renovation") == "true";
        if (string.IsNullOrWhiteSpace(renovationId) || string.IsNullOrWhiteSpace(reason) ||
            !selectedIndex.HasValue || !confirmed)
            return Array.Empty<EventCandidate>();

        var catalog = HomeRenovationCatalog(snapshot);
        var option = catalog.HasValue
            ? HomeRenovationOption(catalog.Value, renovationId)
            : null;
        if (!catalog.HasValue || !option.HasValue)
            return Array.Empty<EventCandidate>();
        var destructive = ReadBool(option.Value, "is_destructive") == true;
        if (destructive && HomeRenovationIntentParameter(intent, "confirm_destructive") != "true")
            return Array.Empty<EventCandidate>();
        var region = HomeRenovationRegion(option.Value, selectedIndex.Value);
        if (!region.HasValue)
            return Array.Empty<EventCandidate>();

        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var serviceLocation = ReadString(catalog.Value, "service_location_id");
        if (!string.Equals(currentLocation, serviceLocation, StringComparison.OrdinalIgnoreCase))
        {
            if (ReadString(catalog.Value, "service_status") != "route_to_carpenter_service_required")
                return Array.Empty<EventCandidate>();
            return HomeRenovationRouteCandidate(
                snapshot, option.Value, selectedIndex.Value, reason,
                HomeRenovationIntentParameter(intent, "confirm_destructive"), currentLocation, serviceLocation);
        }

        var actionX = NullableReadInt(catalog.Value, "service_action_tile_x");
        var actionY = NullableReadInt(catalog.Value, "service_action_tile_y");
        var stand = actionX.HasValue && actionY.HasValue
            ? FindBestStandTile(snapshot, actionX.Value, actionY.Value)
            : null;
        var reasons = new List<string>();
        if (ReadString(catalog.Value, "projection_status") != "complete_live_native_home_renovation_catalog")
            reasons.Add("home_renovation_catalog_incomplete_or_drifted");
        if (ReadString(catalog.Value, "service_status") != "ready")
            reasons.Add("home_renovation_service_not_ready:" + ReadString(catalog.Value, "service_status"));
        if (ReadString(option.Value, "availability_status") != "available_in_native_renovation_shop" ||
            ReadBool(option.Value, "native_menu_available") != true)
            reasons.Add("home_renovation_option_not_available");
        if (ReadBool(option.Value, "check_for_obstructions") == true && ReadString(region.Value, "obstruction_status") != "clear")
            reasons.Add("home_renovation_selected_region_obstructed");
        if (!actionX.HasValue || !actionY.HasValue || stand is null)
            reasons.Add("home_renovation_service_stand_unavailable");

        var parameters = actionX.HasValue && actionY.HasValue && stand is not null
            ? HomeRenovationParameters(catalog.Value, option.Value, region.Value, selectedIndex.Value,
                reason, destructive, actionX.Value, actionY.Value, stand)
            : Array.Empty<SmallModelActionParameter>();
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "home-renovation:" + renovationId + ":" + selectedIndex.Value,
                Kind = "renovate_home",
                Available = reasons.Count == 0,
                AllowedNow = reasons.Count == 0,
                LocationId = serviceLocation,
                TileX = actionX,
                TileY = actionY,
                DisplayName = ReadString(option.Value, "display_name"),
                Quantity = 1,
                EstimatedTicks = 1800,
                EnergyCost = 0,
                AvailabilityClass = "explicit_player_command_native_home_renovation",
                ExpectedEffect = HomeRenovationExpectedEffect(catalog.Value, option.Value, selectedIndex.Value),
                BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = parameters
            }
        };
    }

    private EventCandidate[] HomeRenovationRouteCandidate(
        SnapshotEnvelope snapshot,
        JsonElement option,
        int selectedIndex,
        string reason,
        string destructiveConfirmation,
        string currentLocation,
        string serviceLocation)
    {
        var plan = FindResolvedRoutePlan(snapshot, currentLocation, serviceLocation,
            RouteConnectorCandidates(snapshot, int.MaxValue).Where(value => value.Kind == "route_connector_tile").ToArray());
        if (plan?.FirstConnectorCandidate is null)
            return Array.Empty<EventCandidate>();
        var continuation = new[]
        {
            Parameter("continuation.option_id", "housing.renovate"),
            Parameter("continuation.renovation_id", ReadString(option, "renovation_id")),
            Parameter("continuation.selected_index", selectedIndex.ToString(CultureInfo.InvariantCulture)),
            Parameter("continuation.renovation_reason", reason),
            Parameter("continuation.confirm_renovation", "true"),
            Parameter("continuation.confirm_destructive", destructiveConfirmation)
        };
        return new[]
        {
            CloneCandidate(plan.FirstConnectorCandidate,
                candidateId: "home-renovation-route:" + ReadString(option, "renovation_id") + ":" + currentLocation,
                expectedEffect: plan.FirstConnectorCandidate.ExpectedEffect + ";home_renovation_service_location=" + serviceLocation,
                parameters: plan.FirstConnectorCandidate.Parameters.Concat(continuation).ToArray(),
                availabilityClass: "home_renovation_rolling_route")
        };
    }

    private static SmallModelActionParameter[] HomeRenovationParameters(
        JsonElement catalog,
        JsonElement option,
        JsonElement region,
        int selectedIndex,
        string reason,
        bool destructive,
        int actionX,
        int actionY,
        CandidateTile stand) => new[]
    {
        Parameter("renovation_id", ReadString(option, "renovation_id")),
        Parameter("selected_index", selectedIndex.ToString(CultureInfo.InvariantCulture)),
        Parameter("renovation_reason", reason),
        Parameter("confirm_renovation", "true"),
        Parameter("confirm_destructive", destructive ? "true" : "false"),
        Parameter("is_destructive", destructive ? "true" : "false"),
        Parameter("home_location_id", ReadString(catalog, "home_location_id")),
        Parameter("home_runtime_type", ReadString(catalog, "home_runtime_type")),
        Parameter("expected_house_upgrade_level", ReadInt(catalog, "house_upgrade_level").ToString(CultureInfo.InvariantCulture)),
        Parameter("data_payload_sha256", ReadString(catalog, "data_payload_sha256")),
        Parameter("data_contract_status", ReadString(catalog, "data_contract_status")),
        Parameter("native_available_renovation_ids_json", catalog.GetProperty("native_available_renovation_ids").GetRawText()),
        Parameter("native_shop_index", ReadInt(option, "native_shop_index").ToString(CultureInfo.InvariantCulture)),
        Parameter("room_id", ReadString(option, "room_id")),
        Parameter("animation_type", ReadString(option, "animation_type")),
        Parameter("check_for_obstructions", ReadBool(option, "check_for_obstructions") == true ? "true" : "false"),
        Parameter("price", ReadInt(option, "price").ToString(CultureInfo.InvariantCulture)),
        Parameter("expected_money_before", ReadInt(option, "money_before").ToString(CultureInfo.InvariantCulture)),
        Parameter("expected_money_after", ReadInt(option, "expected_money_after").ToString(CultureInfo.InvariantCulture)),
        Parameter("first_purchase_mail_id", ReadString(option, "first_purchase_mail_id")),
        Parameter("first_purchase_mail_before", ReadBool(option, "first_purchase_mail_before") == true ? "true" : "false"),
        Parameter("expected_first_purchase_mail_after", ReadBool(option, "expected_first_purchase_mail_after") == true ? "true" : "false"),
        Parameter("refund_eligible", ReadBool(option, "refund_eligible") == true ? "true" : "false"),
        Parameter("requirements_json", option.GetProperty("requirements").GetRawText()),
        Parameter("renovate_actions_json", option.GetProperty("renovate_actions").GetRawText()),
        Parameter("selected_region_rectangles_json", region.GetProperty("rectangles").GetRawText()),
        Parameter("selected_region_obstruction_status", ReadString(region, "obstruction_status")),
        Parameter("projection_fingerprint", ReadString(option, "projection_fingerprint")),
        Parameter("target_location", ReadString(catalog, "service_location_id")),
        Parameter("target_tile_x", actionX.ToString(CultureInfo.InvariantCulture)),
        Parameter("target_tile_y", actionY.ToString(CultureInfo.InvariantCulture)),
        Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)),
        Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)),
        Parameter("builder_action_raw", ReadString(catalog, "service_action_raw")),
        Parameter("native_contract", ReadString(catalog, "native_contract")),
        Parameter("max_movement_tiles", "512")
    };

    private static string HomeRenovationExpectedEffect(JsonElement catalog, JsonElement option, int selectedIndex) =>
        "home=" + ReadString(catalog, "home_location_id") +
        ";renovation=" + ReadString(option, "renovation_id") +
        ";selected_index=" + selectedIndex +
        ";money=" + ReadInt(option, "expected_money_after") +
        ";first_purchase_mail=" + ReadBool(option, "expected_first_purchase_mail_after") +
        ";native_renovation_actions_and_map_update_verified=true";

    private static JsonElement? HomeRenovationCatalog(SnapshotEnvelope snapshot)
    {
        var progress = ReadStateFieldValue(snapshot, "world_progress", "marriage_house");
        return progress.HasValue && progress.Value.ValueKind == JsonValueKind.Object &&
            progress.Value.TryGetProperty("home_renovations", out var catalog) && catalog.ValueKind == JsonValueKind.Object
                ? catalog
                : null;
    }

    private static JsonElement? HomeRenovationOption(JsonElement catalog, string id)
    {
        if (!catalog.TryGetProperty("options", out var options) || options.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var option in options.EnumerateArray())
        {
            if (option.ValueKind == JsonValueKind.Object && ReadString(option, "renovation_id") == id)
                return option;
        }
        return null;
    }

    private static JsonElement? HomeRenovationRegion(JsonElement option, int selectedIndex)
    {
        if (!option.TryGetProperty("regions", out var regions) || regions.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var region in regions.EnumerateArray())
        {
            if (region.ValueKind == JsonValueKind.Object && ReadInt(region, "selected_index") == selectedIndex)
                return region;
        }
        return null;
    }

    private static string HomeRenovationIntentParameter(SmallModelActionParameter[] parameters, string name)
    {
        var value = IntentParameter(parameters, name);
        return string.IsNullOrWhiteSpace(value)
            ? IntentParameter(parameters, "continuation." + name)
            : value;
    }

    private static int? ParseHomeRenovationIntentInt(SmallModelActionParameter[] parameters, string name) =>
        int.TryParse(HomeRenovationIntentParameter(parameters, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}

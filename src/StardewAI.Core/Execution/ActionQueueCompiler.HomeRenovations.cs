using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static readonly string[] HomeRenovationBoundParameterNames =
    {
        "home_location_id", "home_runtime_type", "expected_house_upgrade_level", "data_payload_sha256",
        "data_contract_status", "native_available_renovation_ids_json", "native_shop_index", "room_id",
        "animation_type", "is_destructive", "check_for_obstructions", "price", "expected_money_before",
        "expected_money_after", "first_purchase_mail_id", "first_purchase_mail_before",
        "expected_first_purchase_mail_after", "refund_eligible", "requirements_json", "renovate_actions_json",
        "selected_region_rectangles_json", "selected_region_obstruction_status", "projection_fingerprint",
        "target_location", "target_tile_x", "target_tile_y", "stand_tile_x", "stand_tile_y",
        "builder_action_raw", "native_contract", "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildHomeRenovationParameters(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var parameters = action.Parameters
            .Where(parameter => !HomeRenovationBoundParameterNames.Contains(parameter.Name, StringComparer.Ordinal))
            .ToList();
        var target = SelectHomeRenovationTarget(action, snapshot);
        if (target is null)
            return parameters.ToArray();
        var catalog = target.Catalog;
        var option = target.Option;
        var region = target.Region;
        parameters.AddRange(new[]
        {
            Parameter("home_location_id", ReadString(catalog, "home_location_id")),
            Parameter("home_runtime_type", ReadString(catalog, "home_runtime_type")),
            Parameter("expected_house_upgrade_level", ReadInt(catalog, "house_upgrade_level").ToString(CultureInfo.InvariantCulture)),
            Parameter("data_payload_sha256", ReadString(catalog, "data_payload_sha256")),
            Parameter("data_contract_status", ReadString(catalog, "data_contract_status")),
            Parameter("native_available_renovation_ids_json", catalog.GetProperty("native_available_renovation_ids").GetRawText()),
            Parameter("native_shop_index", ReadInt(option, "native_shop_index").ToString(CultureInfo.InvariantCulture)),
            Parameter("room_id", ReadString(option, "room_id")),
            Parameter("animation_type", ReadString(option, "animation_type")),
            Parameter("is_destructive", ReadBool(option, "is_destructive") == true ? "true" : "false"),
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
            Parameter("target_tile_x", ReadInt(catalog, "service_action_tile_x").ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", ReadInt(catalog, "service_action_tile_y").ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", target.StandX.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", target.StandY.ToString(CultureInfo.InvariantCulture)),
            Parameter("builder_action_raw", ReadString(catalog, "service_action_raw")),
            Parameter("native_contract", ReadString(catalog, "native_contract")),
            Parameter("max_movement_tiles", "512")
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileHomeRenovationStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = BoundHomeRenovationAction(action, snapshot);
        var id = ReadParameter(bound, "renovation_id");
        var selectedIndex = ReadIntParameter(bound, "selected_index");
        if (string.IsNullOrWhiteSpace(id) || !selectedIndex.HasValue)
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step("renovate_home", ReadParameter(bound, "home_location_id") + ":" + id + ":" + selectedIndex.Value,
                "home_renovation=" + id + ";selected_index=" + selectedIndex.Value +
                ";money=" + ReadParameter(bound, "expected_money_after") +
                ";native_actions_map_event_and_return_verified=true", 1800)
        };
    }

    private static string[] ValidateHomeRenovationPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId is not ("housing.renovate" or "executor.renovate_home"))
            return Array.Empty<string>();
        var reasons = new List<string>();
        var id = ReadParameter(action, "renovation_id");
        var selectedIndex = ReadIntParameter(action, "selected_index");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(ReadParameter(action, "renovation_reason")) ||
            !selectedIndex.HasValue || ReadParameter(action, "confirm_renovation") != "true")
            reasons.Add("home_renovation_exact_id_region_reason_and_confirmation_required");
        var target = SelectHomeRenovationTarget(action, snapshot);
        if (target is null)
        {
            reasons.Add("home_renovation_target_not_found_or_projection_drifted");
            return reasons.ToArray();
        }
        if (ReadString(target.Catalog, "projection_status") != "complete_live_native_home_renovation_catalog" ||
            ReadString(target.Catalog, "data_contract_status") != "exact_locked_base_1.6.15")
            reasons.Add("home_renovation_catalog_not_exact");
        if (ReadString(target.Catalog, "service_status") != "ready" ||
            !string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), ReadString(target.Catalog, "service_location_id"), StringComparison.OrdinalIgnoreCase))
            reasons.Add("home_renovation_service_not_ready");
        if (ReadString(target.Option, "availability_status") != "available_in_native_renovation_shop" ||
            ReadBool(target.Option, "native_menu_available") != true)
            reasons.Add("home_renovation_option_not_available");
        var destructive = ReadBool(target.Option, "is_destructive") == true;
        if (destructive && ReadParameter(action, "confirm_destructive") != "true")
            reasons.Add("home_renovation_destructive_confirmation_required");
        if (ReadBool(target.Option, "check_for_obstructions") == true && ReadString(target.Region, "obstruction_status") != "clear")
            reasons.Add("home_renovation_region_obstructed");
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("home_renovation_menu_must_be_clear");

        var bound = BoundHomeRenovationAction(action, snapshot);
        if (ReadParameter(bound, "data_payload_sha256") != "26bdcd0681a57c1f749d249ad9305ffa1d58c433c86c1a0b954d0052c6d5d40b" ||
            ReadParameter(bound, "builder_action_raw") != "Carpenter" ||
            string.IsNullOrWhiteSpace(ReadParameter(bound, "projection_fingerprint")) ||
            string.IsNullOrWhiteSpace(ReadParameter(bound, "requirements_json")) ||
            string.IsNullOrWhiteSpace(ReadParameter(bound, "renovate_actions_json")) ||
            string.IsNullOrWhiteSpace(ReadParameter(bound, "selected_region_rectangles_json")) ||
            ReadIntParameter(bound, "native_shop_index") is null ||
            ReadIntParameter(bound, "expected_house_upgrade_level") is < 2 ||
            ReadIntParameter(bound, "expected_money_before") is null || ReadIntParameter(bound, "expected_money_after") is null ||
            string.IsNullOrWhiteSpace(ReadParameter(bound, "native_contract")))
            reasons.Add("home_renovation_complete_typed_projection_required");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundHomeRenovationAction(SmallModelAction action, SnapshotEnvelope snapshot) => new()
    {
        ActionId = action.ActionId,
        OptionId = action.OptionId,
        Rationale = action.Rationale,
        Parameters = BuildHomeRenovationParameters(action, snapshot)
    };

    private static HomeRenovationCompilerTarget? SelectHomeRenovationTarget(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var id = ReadParameter(action, "renovation_id");
        var selectedIndex = ReadIntParameter(action, "selected_index");
        var progress = ReadStateFieldValue(snapshot, "world_progress", "marriage_house");
        if (string.IsNullOrWhiteSpace(id) || !selectedIndex.HasValue || !progress.HasValue ||
            progress.Value.ValueKind != JsonValueKind.Object ||
            !progress.Value.TryGetProperty("home_renovations", out var catalog) || catalog.ValueKind != JsonValueKind.Object ||
            !catalog.TryGetProperty("options", out var options) || options.ValueKind != JsonValueKind.Array)
            return null;
        var option = options.EnumerateArray().FirstOrDefault(value =>
            value.ValueKind == JsonValueKind.Object && ReadString(value, "renovation_id") == id);
        if (option.ValueKind != JsonValueKind.Object || !option.TryGetProperty("regions", out var regions) || regions.ValueKind != JsonValueKind.Array)
            return null;
        var region = regions.EnumerateArray().FirstOrDefault(value =>
            value.ValueKind == JsonValueKind.Object && ReadInt(value, "selected_index") == selectedIndex.Value);
        if (region.ValueKind != JsonValueKind.Object)
            return null;
        var actionX = NullableHomeRenovationInt(catalog, "service_action_tile_x");
        var actionY = NullableHomeRenovationInt(catalog, "service_action_tile_y");
        if (!actionX.HasValue || !actionY.HasValue)
            return null;
        var requestedStandX = ReadIntParameter(action, "stand_tile_x");
        var requestedStandY = ReadIntParameter(action, "stand_tile_y");
        var stand = requestedStandX.HasValue && requestedStandY.HasValue &&
            Math.Abs(actionX.Value - requestedStandX.Value) + Math.Abs(actionY.Value - requestedStandY.Value) == 1 &&
            SleepStandTileReachable(snapshot, requestedStandX.Value, requestedStandY.Value)
                ? new SleepStandTile(requestedStandX.Value, requestedStandY.Value)
                : FindBestSleepStandTile(snapshot, actionX.Value, actionY.Value);
        return stand is null
            ? null
            : new HomeRenovationCompilerTarget(catalog, option, region, stand.X, stand.Y);
    }

    private static int? NullableHomeRenovationInt(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    private sealed record HomeRenovationCompilerTarget(JsonElement Catalog, JsonElement Option, JsonElement Region, int StandX, int StandY);
}

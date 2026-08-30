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
    private const string BobberSelectionCompilerNativeContract =
        "FishShop_Bobbers_checkAction->ChooseFromIconsMenu(bobbers)->receiveLeftClick_exact_unlocked_icon->Farmer.bobberStyle_and_usingRandomizedBobber_receipt->native_close_button";

    private static readonly string[] BobberSelectionBoundParameterNames =
    {
        "bobber_projection_fingerprint", "bobber_style_before", "bobber_random_before", "bobber_random_after",
        "bobber_fish_caught_species_count", "bobber_native_unlock_quotient", "target_location",
        "target_tile_x", "target_tile_y", "stand_tile_x", "stand_tile_y", "bobber_action_raw",
        "expected_menu_type_after", "expected_menu_kind", "native_contract", "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildBobberSelectionParameters(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var parameters = action.Parameters
            .Where(parameter => !BobberSelectionBoundParameterNames.Contains(parameter.Name, StringComparer.Ordinal))
            .ToList();
        var styleId = ReadIntParameter(action, "bobber_style_id");
        var projection = ReadStateFieldValue(snapshot, "player", "bobber_selection");
        if (!styleId.HasValue || !projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return parameters.ToArray();
        var style = BobberCompilerStyle(projection.Value, styleId.Value);
        var target = ResolveBobberCompilerTarget(projection.Value, action, snapshot);
        if (!style.HasValue || ReadBool(style.Value, "unlocked") != true || target is null)
            return parameters.ToArray();
        parameters.AddRange(new[]
        {
            Parameter("bobber_projection_fingerprint", ReadString(projection.Value, "projection_fingerprint")),
            Parameter("bobber_style_before", ReadInt(projection.Value, "current_style_id").ToString(CultureInfo.InvariantCulture)),
            Parameter("bobber_random_before", (ReadBool(projection.Value, "using_randomized_bobber") == true).ToString().ToLowerInvariant()),
            Parameter("bobber_random_after", (styleId.Value == -2).ToString().ToLowerInvariant()),
            Parameter("bobber_fish_caught_species_count", ReadInt(projection.Value, "fish_caught_species_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("bobber_native_unlock_quotient", ReadInt(projection.Value, "native_unlock_quotient").ToString(CultureInfo.InvariantCulture)),
            Parameter("target_location", ReadString(projection.Value, "location_id")),
            Parameter("target_tile_x", target.TargetX.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", target.TargetY.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", target.StandX.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", target.StandY.ToString(CultureInfo.InvariantCulture)),
            Parameter("bobber_action_raw", target.ActionRaw),
            Parameter("expected_menu_type_after", "ChooseFromIconsMenu"),
            Parameter("expected_menu_kind", "bobbers"),
            Parameter("native_contract", ReadString(projection.Value, "native_contract")),
            Parameter("max_movement_tiles", "512")
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileBobberSelectionStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = BoundBobberSelectionAction(action, snapshot);
        var styleId = ReadIntParameter(bound, "bobber_style_id");
        var x = ReadIntParameter(bound, "target_tile_x");
        var y = ReadIntParameter(bound, "target_tile_y");
        if (!styleId.HasValue || !x.HasValue || !y.HasValue)
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step("choose_bobber_style",
                ReadParameter(bound, "target_location") + "(" + x + "," + y + "):style=" + styleId,
                "bobber_style_id=" + styleId + ";using_randomized_bobber=" +
                (styleId == -2).ToString().ToLowerInvariant() + ";native_receipt_verified=true", 420)
        };
    }

    private static string[] ValidateBobberSelectionPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId is not ("player.choose_bobber" or "executor.choose_bobber_style"))
            return Array.Empty<string>();
        var reasons = new List<string>();
        var styleId = ReadIntParameter(action, "bobber_style_id");
        if (styleId is null or < -2 or > 38 || styleId == -1 ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "bobber_reason")) ||
            ReadParameter(action, "confirm_bobber_style") != "true")
            reasons.Add("bobber_selection_exact_style_reason_and_confirmation_required");
        var projection = ReadStateFieldValue(snapshot, "player", "bobber_selection");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return reasons.Append("bobber_selection_projection_unavailable").ToArray();
        var bobber = projection.Value;
        var style = styleId.HasValue ? BobberCompilerStyle(bobber, styleId.Value) : null;
        if (ReadString(bobber, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(bobber, "invocation_policy") != "player_command_only" ||
            ReadString(bobber, "service_status") != "ready" ||
            !string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), "FishShop", StringComparison.OrdinalIgnoreCase))
            reasons.Add("bobber_selection_native_service_not_ready");
        if (!style.HasValue || ReadBool(style.Value, "unlocked") != true)
            reasons.Add("bobber_selection_style_locked_or_unknown");
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("bobber_selection_menu_must_be_clear");

        var bound = BoundBobberSelectionAction(action, snapshot);
        var target = ResolveBobberCompilerTarget(bobber, action, snapshot);
        if (target is null || ReadParameter(bound, "bobber_projection_fingerprint") != ReadString(bobber, "projection_fingerprint") ||
            ReadIntParameter(bound, "bobber_fish_caught_species_count") != ReadInt(bobber, "fish_caught_species_count") ||
            ReadIntParameter(bound, "bobber_native_unlock_quotient") != ReadInt(bobber, "native_unlock_quotient") ||
            ReadIntParameter(bound, "target_tile_x") != target?.TargetX || ReadIntParameter(bound, "target_tile_y") != target?.TargetY ||
            ReadIntParameter(bound, "stand_tile_x") != target?.StandX || ReadIntParameter(bound, "stand_tile_y") != target?.StandY ||
            ReadParameter(bound, "bobber_action_raw") != "Bobbers" ||
            ReadParameter(bound, "native_contract") != BobberSelectionCompilerNativeContract)
            reasons.Add("bobber_selection_complete_fresh_typed_projection_required");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundBobberSelectionAction(SmallModelAction action, SnapshotEnvelope snapshot) => new()
    {
        ActionId = action.ActionId,
        OptionId = action.OptionId,
        Rationale = action.Rationale,
        Parameters = BuildBobberSelectionParameters(action, snapshot)
    };

    private static JsonElement? BobberCompilerStyle(JsonElement projection, int styleId)
    {
        if (!projection.TryGetProperty("styles", out var styles) || styles.ValueKind != JsonValueKind.Array)
            return null;
        var row = styles.EnumerateArray().FirstOrDefault(value =>
            value.ValueKind == JsonValueKind.Object && ReadInt(value, "style_id") == styleId);
        return row.ValueKind == JsonValueKind.Object ? row : null;
    }

    private static BobberCompilerTarget? ResolveBobberCompilerTarget(
        JsonElement projection,
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        if (!projection.TryGetProperty("action_tiles", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return null;
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        return rows.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object &&
                ReadString(row, "action_raw") == "Bobbers")
            .Select(row =>
            {
                var x = ReadInt(row, "tile_x");
                var y = ReadInt(row, "tile_y");
                var requestedX = ReadIntParameter(action, "stand_tile_x");
                var requestedY = ReadIntParameter(action, "stand_tile_y");
                var stand = requestedX.HasValue && requestedY.HasValue &&
                    Math.Abs(x - requestedX.Value) + Math.Abs(y - requestedY.Value) == 1 &&
                    SleepStandTileReachable(snapshot, requestedX.Value, requestedY.Value)
                        ? new SleepStandTile(requestedX.Value, requestedY.Value)
                        : FindBestSleepStandTile(snapshot, x, y);
                return stand is null ? null : new BobberCompilerTarget(x, y, stand.X, stand.Y,
                    ReadString(row, "action_raw"), Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y));
            })
            .Where(target => target is not null)
            .OrderBy(target => target!.Distance).ThenBy(target => target!.TargetY).ThenBy(target => target!.TargetX)
            .FirstOrDefault();
    }

    private sealed record BobberCompilerTarget(int TargetX, int TargetY, int StandX, int StandY, string ActionRaw, int Distance);
}

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
    private const string PlayerCustomizationCompilerNativeContract =
        "wizard_shrine:shared_route->WizardShrine_checkAction->answerDialogue(Yes)->CharacterCustomization(Source.Wizard)_native_controls->OK;desert_makeover:shared_route->walk_onto_DesertMakeover_TouchAction->native_skippable_Event->onEventFinished_ReceiveMakeOver";

    private static readonly string[] PlayerCustomizationBoundNames =
    {
        "customization_projection_fingerprint", "customization_price_gold", "customization_money_before",
        "customization_stylist_name", "customization_passive_festival_day", "customization_free_inventory_slots",
        "customization_equipped_item_count", "customization_expected_outfit_index", "customization_uses_player_seed",
        "customization_special_laurel_outfit", "customization_expected_hat_qid", "customization_expected_hat_color",
        "customization_expected_shirt_qid", "customization_expected_shirt_color", "customization_expected_pants_qid",
        "customization_expected_pants_color", "target_location", "target_tile_x", "target_tile_y",
        "stand_tile_x", "stand_tile_y", "customization_action_raw", "customization_action_token",
        "expected_menu_type_after", "expected_menu_kind", "native_contract", "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildPlayerCustomizationParameters(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var parameters = action.Parameters
            .Where(parameter => !PlayerCustomizationBoundNames.Contains(parameter.Name, StringComparer.Ordinal)).ToList();
        var mode = ReadParameter(action, "customization_mode");
        var projection = ReadStateFieldValue(snapshot, "player", "customization");
        if (mode is not ("wizard_shrine" or "desert_makeover") || !projection.HasValue ||
            projection.Value.ValueKind != JsonValueKind.Object)
            return parameters.ToArray();
        var branchName = mode == "wizard_shrine" ? "wizard_shrine" : "desert_makeover";
        if (!projection.Value.TryGetProperty(branchName, out var branch) || branch.ValueKind != JsonValueKind.Object)
            return parameters.ToArray();
        var target = ResolveCustomizationCompilerTarget(branch, action, snapshot, mode ?? string.Empty);
        if (target is null)
            return parameters.ToArray();
        parameters.AddRange(new[]
        {
            Parameter("customization_projection_fingerprint", ReadString(projection.Value, "projection_fingerprint")),
            Parameter("target_location", ReadString(branch, "location_id")),
            Parameter("target_tile_x", target.TargetX.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", target.TargetY.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", target.StandX.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", target.StandY.ToString(CultureInfo.InvariantCulture)),
            Parameter("customization_action_raw", target.ActionRaw), Parameter("customization_action_token", target.Token),
            Parameter("expected_menu_type_after", mode == "wizard_shrine" ? "CharacterCustomization" : "none"),
            Parameter("expected_menu_kind", mode == "wizard_shrine" ? "wizard" : "desert_makeover_event"),
            Parameter("native_contract", ReadString(projection.Value, "native_contract")), Parameter("max_movement_tiles", "512")
        });
        if (mode == "wizard_shrine")
        {
            parameters.Add(Parameter("customization_price_gold", ReadInt(branch, "price_gold").ToString(CultureInfo.InvariantCulture)));
            parameters.Add(Parameter("customization_money_before", ReadInt(branch, "money_before").ToString(CultureInfo.InvariantCulture)));
        }
        else
        {
            var parts = PlayerCustomizationCompilerParts(branch);
            parameters.AddRange(new[]
            {
                Parameter("customization_stylist_name", ReadString(branch, "stylist_name")),
                Parameter("customization_passive_festival_day", ReadInt(branch, "passive_festival_day").ToString(CultureInfo.InvariantCulture)),
                Parameter("customization_free_inventory_slots", ReadInt(branch, "free_inventory_slots").ToString(CultureInfo.InvariantCulture)),
                Parameter("customization_equipped_item_count", ReadInt(branch, "equipped_item_count").ToString(CultureInfo.InvariantCulture)),
                Parameter("customization_expected_outfit_index", ReadInt(branch, "expected_outfit_index").ToString(CultureInfo.InvariantCulture)),
                Parameter("customization_uses_player_seed", (ReadBool(branch, "uses_player_seed") == true).ToString().ToLowerInvariant()),
                Parameter("customization_special_laurel_outfit", (ReadBool(branch, "special_laurel_outfit") == true).ToString().ToLowerInvariant()),
                Parameter("customization_expected_hat_qid", parts.GetValueOrDefault("hat").Qid),
                Parameter("customization_expected_hat_color", parts.GetValueOrDefault("hat").Color),
                Parameter("customization_expected_shirt_qid", parts.GetValueOrDefault("shirt").Qid),
                Parameter("customization_expected_shirt_color", parts.GetValueOrDefault("shirt").Color),
                Parameter("customization_expected_pants_qid", parts.GetValueOrDefault("pants").Qid),
                Parameter("customization_expected_pants_color", parts.GetValueOrDefault("pants").Color)
            });
        }
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompilePlayerCustomizationStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = BoundPlayerCustomizationAction(action, snapshot);
        var mode = ReadParameter(bound, "customization_mode");
        var x = ReadIntParameter(bound, "target_tile_x");
        var y = ReadIntParameter(bound, "target_tile_y");
        if (mode is not ("wizard_shrine" or "desert_makeover") || !x.HasValue || !y.HasValue)
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step("customize_player", ReadParameter(bound, "target_location") + "(" + x + "," + y + "):mode=" + mode,
                mode == "wizard_shrine" ? "wizard_shrine_exact_character_state_applied=true"
                    : "desert_makeover_expected_outfit_applied=true", mode == "wizard_shrine" ? 900 : 1500)
        };
    }

    private static string[] ValidatePlayerCustomizationPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId is not ("player.customize" or "executor.customize_player"))
            return Array.Empty<string>();
        var reasons = new List<string>();
        var mode = ReadParameter(action, "customization_mode");
        if (mode is not ("wizard_shrine" or "desert_makeover") ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "customization_reason")) ||
            ReadParameter(action, "confirm_customization") != "true")
            reasons.Add("player_customization_exact_mode_reason_and_confirmation_required");
        var projection = ReadStateFieldValue(snapshot, "player", "customization");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return reasons.Append("player_customization_projection_unavailable").ToArray();
        var branchName = mode == "wizard_shrine" ? "wizard_shrine" : "desert_makeover";
        if (!projection.Value.TryGetProperty(branchName, out var branch) || branch.ValueKind != JsonValueKind.Object)
            return reasons.Append("player_customization_mode_projection_unavailable").ToArray();
        if (ReadString(projection.Value, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(projection.Value, "invocation_policy") != "player_command_only" ||
            ReadString(branch, "service_status") != "ready" ||
            !string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), ReadString(branch, "location_id"), StringComparison.OrdinalIgnoreCase))
            reasons.Add("player_customization_native_service_not_ready");
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("player_customization_menu_must_be_clear");
        var bound = BoundPlayerCustomizationAction(action, snapshot);
        var target = ResolveCustomizationCompilerTarget(branch, action, snapshot, mode ?? string.Empty);
        if (target is null || ReadParameter(bound, "customization_projection_fingerprint") != ReadString(projection.Value, "projection_fingerprint") ||
            ReadParameter(bound, "target_location") != ReadString(branch, "location_id") ||
            ReadIntParameter(bound, "target_tile_x") != target?.TargetX || ReadIntParameter(bound, "target_tile_y") != target?.TargetY ||
            ReadIntParameter(bound, "stand_tile_x") != target?.StandX || ReadIntParameter(bound, "stand_tile_y") != target?.StandY ||
            ReadParameter(bound, "customization_action_raw") != target?.ActionRaw ||
            ReadParameter(bound, "customization_action_token") != target?.Token ||
            ReadParameter(bound, "native_contract") != PlayerCustomizationCompilerNativeContract)
            reasons.Add("player_customization_complete_fresh_typed_projection_required");
        if (mode == "wizard_shrine")
            reasons.AddRange(ValidateWizardCustomizationTarget(bound, projection.Value, branch));
        else
            reasons.AddRange(ValidateDesertMakeoverBinding(bound, branch));
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<string> ValidateWizardCustomizationTarget(
        SmallModelAction bound, JsonElement projection, JsonElement branch)
    {
        var reasons = new List<string>();
        var name = ReadParameter(bound, "customization_name");
        var favorite = ReadParameter(bound, "customization_favorite_thing");
        var gender = ReadParameter(bound, "customization_gender");
        var skin = ReadIntParameter(bound, "customization_skin_index");
        var hair = ReadIntParameter(bound, "customization_hair_style_id");
        var accessory = ReadIntParameter(bound, "customization_accessory_index");
        var sliders = new[] { "customization_eye_hue", "customization_eye_saturation", "customization_eye_value",
            "customization_hair_hue", "customization_hair_saturation", "customization_hair_value" }
            .Select(name => ReadIntParameter(bound, name)).ToArray();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(favorite) || name.Any(char.IsControl) ||
            favorite.Any(char.IsControl) || gender is not ("male" or "female") || skin is not (>= 0 and <= 23) ||
            accessory is not (>= -1 and <= 29) || sliders.Any(value => value is not (>= 0 and <= 100)) || !hair.HasValue ||
            !branch.TryGetProperty("hair_style_ids", out var styles) || !styles.EnumerateArray().Any(row => row.GetInt32() == hair.Value))
            reasons.Add("player_customization_wizard_target_outside_native_control_domain");
        if (ReadIntParameter(bound, "customization_price_gold") != 500 ||
            ReadIntParameter(bound, "customization_money_before") != ReadInt(branch, "money_before") || ReadInt(branch, "money_before") < 500)
            reasons.Add("player_customization_wizard_exact_500g_prestate_required");
        return reasons;
    }

    private static IEnumerable<string> ValidateDesertMakeoverBinding(SmallModelAction bound, JsonElement branch)
    {
        var parts = PlayerCustomizationCompilerParts(branch);
        var exact = ReadBool(branch, "expected_outfit_available") == true &&
            ReadParameter(bound, "customization_stylist_name") == ReadString(branch, "stylist_name") &&
            ReadIntParameter(bound, "customization_passive_festival_day") == ReadInt(branch, "passive_festival_day") &&
            ReadIntParameter(bound, "customization_free_inventory_slots") == ReadInt(branch, "free_inventory_slots") &&
            ReadIntParameter(bound, "customization_equipped_item_count") == ReadInt(branch, "equipped_item_count") &&
            ReadInt(branch, "free_inventory_slots") >= ReadInt(branch, "equipped_item_count") &&
            ReadIntParameter(bound, "customization_expected_outfit_index") == ReadInt(branch, "expected_outfit_index") &&
            ReadParameter(bound, "customization_expected_hat_qid") == parts.GetValueOrDefault("hat").Qid &&
            ReadParameter(bound, "customization_expected_shirt_qid") == parts.GetValueOrDefault("shirt").Qid &&
            ReadParameter(bound, "customization_expected_pants_qid") == parts.GetValueOrDefault("pants").Qid;
        return exact ? Array.Empty<string>() : new[] { "player_customization_desert_exact_stylist_inventory_rng_and_outfit_binding_required" };
    }

    private static SmallModelAction BoundPlayerCustomizationAction(SmallModelAction action, SnapshotEnvelope snapshot) => new()
    {
        ActionId = action.ActionId, OptionId = action.OptionId, Rationale = action.Rationale,
        Parameters = BuildPlayerCustomizationParameters(action, snapshot)
    };

    private static PlayerCustomizationCompilerTarget? ResolveCustomizationCompilerTarget(
        JsonElement branch, SmallModelAction action, SnapshotEnvelope snapshot, string mode)
    {
        var collectionName = mode == "wizard_shrine" ? "action_tiles" : "touch_tiles";
        var token = mode == "wizard_shrine" ? "WizardShrine" : "DesertMakeover";
        if (!branch.TryGetProperty(collectionName, out var rows) || rows.ValueKind != JsonValueKind.Array)
            return null;
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        return rows.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object && ReadString(row, "action_token") == token)
            .Select(row =>
            {
                var x = ReadInt(row, "tile_x"); var y = ReadInt(row, "tile_y");
                SleepStandTile? stand;
                if (mode == "desert_makeover")
                    stand = SleepStandTileReachable(snapshot, x, y) ? new SleepStandTile(x, y) : null;
                else
                {
                    var requestedX = ReadIntParameter(action, "stand_tile_x"); var requestedY = ReadIntParameter(action, "stand_tile_y");
                    stand = requestedX.HasValue && requestedY.HasValue && Math.Abs(x - requestedX.Value) + Math.Abs(y - requestedY.Value) == 1 &&
                        SleepStandTileReachable(snapshot, requestedX.Value, requestedY.Value)
                        ? new SleepStandTile(requestedX.Value, requestedY.Value) : FindBestSleepStandTile(snapshot, x, y);
                }
                return stand is null ? null : new PlayerCustomizationCompilerTarget(x, y, stand.X, stand.Y,
                    ReadString(row, "action_raw"), token, Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y));
            }).Where(target => target is not null).OrderBy(target => target!.Distance).ThenBy(target => target!.TargetY)
            .ThenBy(target => target!.TargetX).FirstOrDefault();
    }

    private static Dictionary<string, PlayerCustomizationCompilerPart> PlayerCustomizationCompilerParts(JsonElement branch)
    {
        var result = new Dictionary<string, PlayerCustomizationCompilerPart>(StringComparer.Ordinal)
        {
            ["hat"] = new(string.Empty, string.Empty), ["shirt"] = new(string.Empty, string.Empty), ["pants"] = new(string.Empty, string.Empty)
        };
        if (!branch.TryGetProperty("expected_parts", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return result;
        foreach (var row in rows.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object))
            result[ReadString(row, "slot")] = new(ReadString(row, "qualified_item_id"), ReadString(row, "color"));
        return result;
    }

    private sealed record PlayerCustomizationCompilerTarget(int TargetX, int TargetY, int StandX, int StandY,
        string ActionRaw, string Token, int Distance);
    private sealed record PlayerCustomizationCompilerPart(string Qid, string Color);
}

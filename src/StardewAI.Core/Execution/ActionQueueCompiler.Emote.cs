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
    private const string PlayerEmoteCompilerNativeContract =
        "EmoteMenu.ConfirmSelection->ChatBox.textBoxEnter('/emote '+key)->ChatCommands.Emote->Farmer.CanEmote->Farmer.netDoEmote->doEmoteEvent->Farmer.performPlayerEmote->performedEmotes_and_native_icon_or_animation";

    private static readonly string[] PlayerEmoteBoundParameterNames =
    {
        "emote_projection_fingerprint", "emote_option_fingerprint", "emote_index", "emote_icon_index",
        "emote_has_animation", "emote_animation_facing_direction", "emote_animation_duration_milliseconds",
        "emote_hidden", "emote_performed_entry_before", "emote_performed_value_before", "emote_player_id",
        "emote_language_code", "emote_network_role", "emote_chat_input_width_pixels",
        "emote_chat_input_content_width_pixels", "emote_native_input", "native_contract"
    };

    private static SmallModelActionParameter[] BuildPlayerEmoteParameters(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var parameters = action.Parameters
            .Where(parameter => !PlayerEmoteBoundParameterNames.Contains(parameter.Name, StringComparer.Ordinal)).ToList();
        var key = ReadParameter(action, "emote_key");
        var projection = ReadStateFieldValue(snapshot, "player", "emote");
        if (string.IsNullOrWhiteSpace(key) || !projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return parameters.ToArray();
        var option = PlayerEmoteCompilerOption(projection.Value, key);
        if (!option.HasValue || ReadBool(option.Value, "native_command_accepted") != true)
            return parameters.ToArray();
        parameters.AddRange(new[]
        {
            Parameter("emote_projection_fingerprint", ReadString(projection.Value, "projection_fingerprint")),
            Parameter("emote_option_fingerprint", ReadString(option.Value, "option_fingerprint")),
            Parameter("emote_index", ReadInt(option.Value, "emote_index").ToString(CultureInfo.InvariantCulture)),
            Parameter("emote_icon_index", ReadInt(option.Value, "icon_index").ToString(CultureInfo.InvariantCulture)),
            Parameter("emote_has_animation", (ReadBool(option.Value, "has_animation") == true).ToString().ToLowerInvariant()),
            Parameter("emote_animation_facing_direction", ReadInt(option.Value, "animation_facing_direction").ToString(CultureInfo.InvariantCulture)),
            Parameter("emote_animation_duration_milliseconds", ReadInt(option.Value, "animation_duration_milliseconds").ToString(CultureInfo.InvariantCulture)),
            Parameter("emote_hidden", (ReadBool(option.Value, "hidden") == true).ToString().ToLowerInvariant()),
            Parameter("emote_performed_entry_before", (ReadBool(option.Value, "performed_entry_present") == true).ToString().ToLowerInvariant()),
            Parameter("emote_performed_value_before", (ReadBool(option.Value, "performed_value") == true).ToString().ToLowerInvariant()),
            Parameter("emote_player_id", ReadLong(projection.Value, "player_id").ToString(CultureInfo.InvariantCulture)),
            Parameter("emote_language_code", ReadInt(projection.Value, "language_code").ToString(CultureInfo.InvariantCulture)),
            Parameter("emote_network_role", ReadString(projection.Value, "network_role")),
            Parameter("emote_chat_input_width_pixels", ReadInt(projection.Value, "chat_input_width_pixels").ToString(CultureInfo.InvariantCulture)),
            Parameter("emote_chat_input_content_width_pixels", ReadInt(projection.Value, "chat_input_content_width_pixels").ToString(CultureInfo.InvariantCulture)),
            Parameter("emote_native_input", "/emote " + key),
            Parameter("native_contract", ReadString(projection.Value, "native_contract"))
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompilePlayerEmoteStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = BoundPlayerEmoteAction(action, snapshot);
        var key = ReadParameter(bound, "emote_key");
        var index = ReadIntParameter(bound, "emote_index");
        if (string.IsNullOrWhiteSpace(key) || !index.HasValue) return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step("perform_emote", "player=" + ReadParameter(bound, "emote_player_id") + ":emote=" + key,
                "performed_emote_recorded=true;native_icon_or_animation_observed=true",
                Math.Max(180, (ReadIntParameter(bound, "emote_animation_duration_milliseconds") ?? 0) / 16 + 120))
        };
    }

    private static string[] ValidatePlayerEmotePlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId is not ("social.emote" or "executor.perform_emote")) return Array.Empty<string>();
        var reasons = new List<string>();
        var key = ReadParameter(action, "emote_key");
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(ReadParameter(action, "emote_reason")) ||
            ReadParameter(action, "confirm_emote") != "true")
            reasons.Add("emote_exact_key_reason_and_confirmation_required");
        var projection = ReadStateFieldValue(snapshot, "player", "emote");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return reasons.Append("emote_projection_unavailable").ToArray();
        var emote = projection.Value;
        var option = string.IsNullOrWhiteSpace(key) ? null : PlayerEmoteCompilerOption(emote, key);
        if (ReadString(emote, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(emote, "invocation_policy") != "player_command_only" ||
            !HasCompleteLockedPlayerEmoteCompilerCatalog(emote) ||
            ReadString(emote, "service_status") != "ready" ||
            ReadBool(emote, "can_emote_native") != true || ReadBool(emote, "menu_clear") != true ||
            ReadBool(emote, "chat_box_present") != true || ReadBool(emote, "chat_box_active") == true ||
            ReadBool(emote, "is_emoting") == true || ReadBool(emote, "is_emote_animating") == true)
            reasons.Add("emote_native_service_not_ready");
        if (!option.HasValue || ReadBool(option.Value, "native_command_accepted") != true)
            reasons.Add("emote_unknown_or_unaccepted_native_key");
        if (ActionSeesActiveMenuOpen(action, snapshot)) reasons.Add("emote_menu_must_be_clear");

        var bound = BoundPlayerEmoteAction(action, snapshot);
        if (!option.HasValue ||
            ReadParameter(bound, "emote_projection_fingerprint") != ReadString(emote, "projection_fingerprint") ||
            ReadParameter(bound, "emote_option_fingerprint") != ReadString(option.Value, "option_fingerprint") ||
            ReadIntParameter(bound, "emote_index") != ReadInt(option.Value, "emote_index") ||
            ReadIntParameter(bound, "emote_icon_index") != ReadInt(option.Value, "icon_index") ||
            ReadBoolParameter(bound, "emote_has_animation") != ReadBool(option.Value, "has_animation") ||
            ReadIntParameter(bound, "emote_animation_facing_direction") != ReadInt(option.Value, "animation_facing_direction") ||
            ReadIntParameter(bound, "emote_animation_duration_milliseconds") != ReadInt(option.Value, "animation_duration_milliseconds") ||
            ReadBoolParameter(bound, "emote_hidden") != ReadBool(option.Value, "hidden") ||
            ReadBoolParameter(bound, "emote_performed_entry_before") != ReadBool(option.Value, "performed_entry_present") ||
            ReadBoolParameter(bound, "emote_performed_value_before") != ReadBool(option.Value, "performed_value") ||
            ReadLongParameter(bound, "emote_player_id") != ReadLong(emote, "player_id") ||
            ReadIntParameter(bound, "emote_language_code") != ReadInt(emote, "language_code") ||
            ReadParameter(bound, "emote_network_role") != ReadString(emote, "network_role") ||
            ReadIntParameter(bound, "emote_chat_input_width_pixels") != ReadInt(emote, "chat_input_width_pixels") ||
            ReadIntParameter(bound, "emote_chat_input_content_width_pixels") != ReadInt(emote, "chat_input_content_width_pixels") ||
            ReadParameter(bound, "emote_native_input") != "/emote " + key ||
            ReadParameter(bound, "native_contract") != PlayerEmoteCompilerNativeContract)
            reasons.Add("emote_complete_fresh_typed_projection_required");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundPlayerEmoteAction(SmallModelAction action, SnapshotEnvelope snapshot) => new()
    {
        ActionId = action.ActionId,
        OptionId = action.OptionId,
        Rationale = action.Rationale,
        Parameters = BuildPlayerEmoteParameters(action, snapshot)
    };

    private static JsonElement? PlayerEmoteCompilerOption(JsonElement projection, string key)
    {
        if (!projection.TryGetProperty("emotes", out var options) || options.ValueKind != JsonValueKind.Array)
            return null;
        var row = options.EnumerateArray().FirstOrDefault(value => value.ValueKind == JsonValueKind.Object &&
            ReadString(value, "emote_key") == key);
        return row.ValueKind == JsonValueKind.Object ? row : null;
    }

    private static bool HasCompleteLockedPlayerEmoteCompilerCatalog(JsonElement projection)
    {
        if (!projection.TryGetProperty("emotes", out var options) || options.ValueKind != JsonValueKind.Array)
            return false;
        var rows = options.EnumerateArray().ToArray();
        return rows.Select(row => ReadString(row, "emote_key"))
                .SequenceEqual(PlayerEmoteIdentity.LockedBaseEmoteKeys, StringComparer.Ordinal) &&
            rows.Where(row => ReadBool(row, "hidden") == true).Select(row => ReadString(row, "emote_key"))
                .SequenceEqual(PlayerEmoteIdentity.LockedBaseHiddenEmoteKeys, StringComparer.Ordinal);
    }
}

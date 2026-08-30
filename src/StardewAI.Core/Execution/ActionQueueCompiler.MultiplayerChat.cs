using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private const string MultiplayerChatCompilerNativeContract =
        "ChatBox_activate_then_ChatTextBox_character_input_under_880px_then_textBoxEnter_TextBox_then_global_AllPlayers_or_compiler_owned_message_private_then_Multiplayer_type10_network_dispatch_and_sender_local_ChatMessage_receipt";

    private static readonly string[] MultiplayerChatBoundParameterNames =
    {
        "chat_message_sha256", "chat_message_utf16_length", "chat_projection_fingerprint",
        "chat_sender_player_id", "chat_sender_display_name", "chat_sender_default_color", "chat_language_code",
        "chat_network_role", "chat_recipient_player_id", "chat_recipient_display_name",
        "chat_recipient_command_name", "chat_expected_wire_recipient_id", "chat_expected_kind",
        "chat_network_message_type", "chat_message_count_before", "chat_message_limit",
        "chat_input_width_pixels", "chat_input_content_width_pixels", "chat_native_route", "native_contract"
    };

    private static SmallModelActionParameter[] BuildMultiplayerChatParameters(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var parameters = action.Parameters
            .Where(parameter => !MultiplayerChatBoundParameterNames.Contains(parameter.Name, StringComparer.Ordinal))
            .ToList();
        var projection = ReadStateFieldValue(snapshot, "player", "multiplayer_chat");
        var scope = ReadParameter(action, "chat_scope") ?? string.Empty;
        var message = ReadParameter(action, "chat_message_text") ?? string.Empty;
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            scope is not ("global" or "private") || !MultiplayerChatCompilerMessageIsValid(message))
            return parameters.ToArray();

        var recipientId = scope == "private" ? ReadParameter(action, "chat_recipient_player_id") ?? string.Empty : string.Empty;
        var recipient = scope == "private" ? FindMultiplayerChatCompilerRecipient(projection.Value, recipientId) : null;
        if (scope == "private" && (!recipient.HasValue || !MultiplayerChatCompilerFirstMatchIsTarget(projection.Value, recipient.Value, message)))
            return parameters.ToArray();
        parameters.AddRange(new[]
        {
            Parameter("chat_message_sha256", MultiplayerChatCompilerSha256(message)),
            Parameter("chat_message_utf16_length", message.Length.ToString(CultureInfo.InvariantCulture)),
            Parameter("chat_projection_fingerprint", ReadString(projection.Value, "projection_fingerprint")),
            Parameter("chat_sender_player_id", ReadString(projection.Value, "sender_player_id")),
            Parameter("chat_sender_display_name", ReadString(projection.Value, "sender_display_name")),
            Parameter("chat_sender_default_color", ReadString(projection.Value, "sender_default_chat_color")),
            Parameter("chat_language_code", ReadInt(projection.Value, "language_code").ToString(CultureInfo.InvariantCulture)),
            Parameter("chat_network_role", ReadString(projection.Value, "network_role")),
            Parameter("chat_recipient_player_id", recipient.HasValue ? ReadString(recipient.Value, "player_id") : string.Empty),
            Parameter("chat_recipient_display_name", recipient.HasValue ? ReadString(recipient.Value, "display_name") : string.Empty),
            Parameter("chat_recipient_command_name", recipient.HasValue ? ReadString(recipient.Value, "native_command_name") : string.Empty),
            Parameter("chat_expected_wire_recipient_id", scope == "global" ? ReadString(projection.Value, "all_players_recipient_id") : recipientId),
            Parameter("chat_expected_kind", (scope == "global" ? ReadInt(projection.Value, "global_chat_kind") : ReadInt(projection.Value, "private_chat_kind")).ToString(CultureInfo.InvariantCulture)),
            Parameter("chat_network_message_type", ReadInt(projection.Value, "network_message_type").ToString(CultureInfo.InvariantCulture)),
            Parameter("chat_message_count_before", ReadInt(projection.Value, "chat_message_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("chat_message_limit", ReadInt(projection.Value, "chat_message_limit").ToString(CultureInfo.InvariantCulture)),
            Parameter("chat_input_width_pixels", ReadInt(projection.Value, "input_width_pixels").ToString(CultureInfo.InvariantCulture)),
            Parameter("chat_input_content_width_pixels", ReadInt(projection.Value, "input_content_width_pixels").ToString(CultureInfo.InvariantCulture)),
            Parameter("chat_native_route", scope == "global" ? "global_all_players" : "compiler_owned_message_private"),
            Parameter("native_contract", ReadString(projection.Value, "native_contract"))
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileMultiplayerChatStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = BoundMultiplayerChatAction(action, snapshot);
        var scope = ReadParameter(bound, "chat_scope");
        var digest = ReadParameter(bound, "chat_message_sha256");
        if (scope is not ("global" or "private") || string.IsNullOrWhiteSpace(digest))
            return Array.Empty<CompiledActionStep>();
        var target = scope == "private"
            ? "private:recipient=" + ReadParameter(bound, "chat_recipient_player_id")
            : "global:recipient=0";
        return new[]
        {
            Step("send_multiplayer_chat", target,
                "message_sha256=" + digest + ";native_type10_dispatch=true;sender_local_receipt_verified=true", 60)
        };
    }

    private static string[] ValidateMultiplayerChatPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId is not ("multiplayer.send_chat" or "executor.send_multiplayer_chat"))
            return Array.Empty<string>();
        var reasons = new List<string>();
        var scope = ReadParameter(action, "chat_scope") ?? string.Empty;
        var message = ReadParameter(action, "chat_message_text") ?? string.Empty;
        if (scope is not ("global" or "private") || string.IsNullOrWhiteSpace(ReadParameter(action, "chat_reason")) ||
            ReadParameter(action, "confirm_chat") != "true" || !MultiplayerChatCompilerMessageIsValid(message))
            reasons.Add("multiplayer_chat_exact_scope_reason_text_and_confirmation_required");
        if (scope == "private" && message != string.Join(" ", MultiplayerChatCompilerSplit(message)))
            reasons.Add("multiplayer_chat_private_text_must_survive_native_space_tokenization_exactly");

        var projection = ReadStateFieldValue(snapshot, "player", "multiplayer_chat");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return reasons.Append("multiplayer_chat_projection_unavailable").ToArray();
        var chat = projection.Value;
        var recipient = scope == "private"
            ? FindMultiplayerChatCompilerRecipient(chat, ReadParameter(action, "chat_recipient_player_id") ?? string.Empty)
            : null;
        if (ReadString(chat, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(chat, "invocation_policy") != "player_command_only" ||
            ReadString(chat, "service_status") != "ready" || ReadBool(chat, "is_multiplayer") != true ||
            ReadString(chat, "network_role") is not ("server" or "client"))
            reasons.Add("multiplayer_chat_native_service_not_ready");
        if (scope == "private" && (!recipient.HasValue ||
            ReadString(recipient.Value, "private_base_gate_status") != "payload_dependent_first_match_validation_required" ||
            !MultiplayerChatCompilerFirstMatchIsTarget(chat, recipient.Value, message)))
            reasons.Add("multiplayer_chat_private_recipient_not_exact_active_native_first_match");
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("multiplayer_chat_menu_must_be_clear");

        var bound = BoundMultiplayerChatAction(action, snapshot);
        if ((ReadParameter(bound, "chat_projection_fingerprint") ?? string.Empty).Length != 64 ||
            ReadParameter(bound, "chat_projection_fingerprint") != ReadString(chat, "projection_fingerprint") ||
            ReadParameter(bound, "chat_message_sha256") != MultiplayerChatCompilerSha256(message) ||
            ReadIntParameter(bound, "chat_message_utf16_length") != message.Length ||
            ReadParameter(bound, "chat_sender_player_id") != ReadString(chat, "sender_player_id") ||
            ReadParameter(bound, "chat_sender_display_name") != ReadString(chat, "sender_display_name") ||
            ReadParameter(bound, "chat_sender_default_color") != ReadString(chat, "sender_default_chat_color") ||
            ReadIntParameter(bound, "chat_language_code") != ReadInt(chat, "language_code") ||
            ReadParameter(bound, "chat_network_role") != ReadString(chat, "network_role") ||
            ReadParameter(bound, "chat_expected_wire_recipient_id") != (scope == "global"
                ? ReadString(chat, "all_players_recipient_id")
                : recipient.HasValue ? ReadString(recipient.Value, "player_id") : string.Empty) ||
            ReadIntParameter(bound, "chat_expected_kind") != (scope == "global"
                ? ReadInt(chat, "global_chat_kind")
                : ReadInt(chat, "private_chat_kind")) ||
            ReadIntParameter(bound, "chat_network_message_type") != 10 ||
            ReadIntParameter(bound, "chat_message_count_before") != ReadInt(chat, "chat_message_count") ||
            ReadIntParameter(bound, "chat_message_limit") != 10 ||
            ReadIntParameter(bound, "chat_input_width_pixels") != ReadInt(chat, "input_width_pixels") ||
            ReadIntParameter(bound, "chat_input_content_width_pixels") != ReadInt(chat, "input_content_width_pixels") ||
            ReadParameter(bound, "chat_native_route") != (scope == "global"
                ? "global_all_players"
                : "compiler_owned_message_private") ||
            ReadParameter(bound, "native_contract") != MultiplayerChatCompilerNativeContract)
            reasons.Add("multiplayer_chat_complete_fresh_typed_projection_required");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundMultiplayerChatAction(SmallModelAction action, SnapshotEnvelope snapshot) => new()
    {
        ActionId = action.ActionId,
        OptionId = action.OptionId,
        Rationale = action.Rationale,
        Parameters = BuildMultiplayerChatParameters(action, snapshot)
    };

    private static JsonElement? FindMultiplayerChatCompilerRecipient(JsonElement chat, string playerId)
    {
        if (!chat.TryGetProperty("online_recipients", out var recipients) || recipients.ValueKind != JsonValueKind.Array)
            return null;
        var row = recipients.EnumerateArray().FirstOrDefault(value => value.ValueKind == JsonValueKind.Object &&
            ReadString(value, "player_id") == playerId);
        return row.ValueKind == JsonValueKind.Object ? row : null;
    }

    private static bool MultiplayerChatCompilerFirstMatchIsTarget(JsonElement chat, JsonElement target, string message)
    {
        if (!chat.TryGetProperty("online_recipients", out var recipients) || recipients.ValueKind != JsonValueKind.Array)
            return false;
        var commandTokens = MultiplayerChatCompilerSplit(ReadString(target, "native_command_name") + " " + message);
        foreach (var row in recipients.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.Object)
                     .OrderBy(value => ReadInt(value, "native_enumeration_index")))
        {
            if (ReadString(row, "private_base_gate_status") != "payload_dependent_first_match_validation_required")
                continue;
            var nameTokens = MultiplayerChatCompilerSplit(ReadString(row, "native_command_name"));
            if (nameTokens.Length <= commandTokens.Length && nameTokens.Select((token, index) =>
                    string.Equals(token, commandTokens[index], StringComparison.OrdinalIgnoreCase)).All(matches => matches))
                return ReadString(row, "player_id") == ReadString(target, "player_id");
        }
        return false;
    }

    private static bool MultiplayerChatCompilerMessageIsValid(string message) =>
        !string.IsNullOrWhiteSpace(message) && message[0] != '/' &&
        message.All(character => !char.IsControl(character));

    private static string[] MultiplayerChatCompilerSplit(string value) =>
        value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static string MultiplayerChatCompilerSha256(string value)
    {
        using var sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value))
            .Select(valueByte => valueByte.ToString("x2", CultureInfo.InvariantCulture)));
    }
}

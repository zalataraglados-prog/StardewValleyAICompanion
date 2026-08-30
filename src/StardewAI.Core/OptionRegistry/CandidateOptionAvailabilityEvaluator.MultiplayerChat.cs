using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] MultiplayerChatCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] intent)
    {
        var scope = ChatIntent(intent, "chat_scope");
        var reason = ChatIntent(intent, "chat_reason");
        var message = ChatIntent(intent, "chat_message_text");
        if (scope is not ("global" or "private") || string.IsNullOrWhiteSpace(reason) ||
            ChatIntent(intent, "confirm_chat") != "true" || !MultiplayerChatMessageIsValid(message) ||
            scope == "private" && !MultiplayerChatPrivateTextIsStable(message))
            return Array.Empty<EventCandidate>();

        var projection = ReadStateFieldValue(snapshot, "player", "multiplayer_chat");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            ReadString(projection.Value, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(projection.Value, "invocation_policy") != "player_command_only" ||
            ReadString(projection.Value, "service_status") != "ready")
            return Array.Empty<EventCandidate>();

        var recipientId = scope == "private" ? ChatIntent(intent, "chat_recipient_player_id") : string.Empty;
        var recipient = scope == "private" ? MultiplayerChatRecipient(projection.Value, recipientId) : null;
        if (scope == "private" && (!recipient.HasValue ||
            ReadString(recipient.Value, "private_base_gate_status") != "payload_dependent_first_match_validation_required" ||
            !MultiplayerChatNativeFirstMatchIsTarget(projection.Value, recipient.Value, message)))
            return Array.Empty<EventCandidate>();

        var parameters = MultiplayerChatCandidateParameters(projection.Value, scope, reason, message, recipient);
        var reasons = CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
        {
            OptionId = "multiplayer.send_chat",
            Parameters = parameters,
            InvocationSource = OptionInvocationSource.PlayerCommand,
            ExplicitConfirmationGranted = true
        }).Distinct(StringComparer.Ordinal).ToArray();
        var digest = MultiplayerChatCoreSha256(message);
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "multiplayer-chat:" + scope + ":" + digest[..12],
                Kind = "send_multiplayer_chat",
                Available = reasons.Length == 0,
                AllowedNow = reasons.Length == 0,
                AllowedToday = reasons.Length == 0,
                LocationId = ReadStateFieldString(snapshot, "player", "location_id"),
                EstimatedTicks = 60,
                EnergyCost = 0,
                AvailabilityClass = "explicit_player_command_native_multiplayer_chat",
                ExpectedEffect = "chat_scope=" + scope + ";message_sha256=" + digest +
                    ";native_type10_dispatch=true;sender_local_receipt=true",
                BlockReasons = reasons,
                Parameters = parameters
            }
        };
    }

    private static SmallModelActionParameter[] MultiplayerChatCandidateParameters(
        JsonElement projection,
        string scope,
        string reason,
        string message,
        JsonElement? recipient)
    {
        var recipientId = recipient.HasValue ? ReadString(recipient.Value, "player_id") : string.Empty;
        return new[]
        {
            Parameter("chat_scope", scope),
            Parameter("chat_reason", reason),
            Parameter("confirm_chat", "true"),
            Parameter("chat_message_text", message),
            Parameter("chat_message_sha256", MultiplayerChatCoreSha256(message)),
            Parameter("chat_message_utf16_length", message.Length.ToString(CultureInfo.InvariantCulture)),
            Parameter("chat_projection_fingerprint", ReadString(projection, "projection_fingerprint")),
            Parameter("chat_sender_player_id", ReadString(projection, "sender_player_id")),
            Parameter("chat_sender_display_name", ReadString(projection, "sender_display_name")),
            Parameter("chat_sender_default_color", ReadString(projection, "sender_default_chat_color")),
            Parameter("chat_language_code", ReadInt(projection, "language_code").ToString(CultureInfo.InvariantCulture)),
            Parameter("chat_network_role", ReadString(projection, "network_role")),
            Parameter("chat_recipient_player_id", recipientId),
            Parameter("chat_recipient_display_name", recipient.HasValue ? ReadString(recipient.Value, "display_name") : string.Empty),
            Parameter("chat_recipient_command_name", recipient.HasValue ? ReadString(recipient.Value, "native_command_name") : string.Empty),
            Parameter("chat_expected_wire_recipient_id", scope == "global" ? ReadString(projection, "all_players_recipient_id") : recipientId),
            Parameter("chat_expected_kind", (scope == "global" ? ReadInt(projection, "global_chat_kind") : ReadInt(projection, "private_chat_kind")).ToString(CultureInfo.InvariantCulture)),
            Parameter("chat_network_message_type", ReadInt(projection, "network_message_type").ToString(CultureInfo.InvariantCulture)),
            Parameter("chat_message_count_before", ReadInt(projection, "chat_message_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("chat_message_limit", ReadInt(projection, "chat_message_limit").ToString(CultureInfo.InvariantCulture)),
            Parameter("chat_input_width_pixels", ReadInt(projection, "input_width_pixels").ToString(CultureInfo.InvariantCulture)),
            Parameter("chat_input_content_width_pixels", ReadInt(projection, "input_content_width_pixels").ToString(CultureInfo.InvariantCulture)),
            Parameter("chat_native_route", scope == "global" ? "global_all_players" : "compiler_owned_message_private"),
            Parameter("native_contract", ReadString(projection, "native_contract"))
        };
    }

    private static JsonElement? MultiplayerChatRecipient(JsonElement projection, string playerId)
    {
        if (!projection.TryGetProperty("online_recipients", out var recipients) || recipients.ValueKind != JsonValueKind.Array)
            return null;
        var row = recipients.EnumerateArray().FirstOrDefault(value => value.ValueKind == JsonValueKind.Object &&
            ReadString(value, "player_id") == playerId);
        return row.ValueKind == JsonValueKind.Object ? row : null;
    }

    private static bool MultiplayerChatNativeFirstMatchIsTarget(
        JsonElement projection,
        JsonElement target,
        string message)
    {
        if (!projection.TryGetProperty("online_recipients", out var recipients) || recipients.ValueKind != JsonValueKind.Array)
            return false;
        var commandTokens = MultiplayerChatSplit(ReadString(target, "native_command_name") + " " + message);
        foreach (var row in recipients.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.Object)
                     .OrderBy(value => ReadInt(value, "native_enumeration_index")))
        {
            if (ReadString(row, "private_base_gate_status") != "payload_dependent_first_match_validation_required")
                continue;
            var nameTokens = MultiplayerChatSplit(ReadString(row, "native_command_name"));
            if (nameTokens.Length <= commandTokens.Length && nameTokens.Select((token, index) =>
                    string.Equals(token, commandTokens[index], StringComparison.OrdinalIgnoreCase)).All(matches => matches))
                return ReadString(row, "player_id") == ReadString(target, "player_id");
        }
        return false;
    }

    private static bool MultiplayerChatMessageIsValid(string message) =>
        !string.IsNullOrWhiteSpace(message) && message[0] != '/' &&
        message.All(character => !char.IsControl(character));

    private static bool MultiplayerChatPrivateTextIsStable(string message) =>
        message == string.Join(" ", MultiplayerChatSplit(message));

    private static string[] MultiplayerChatSplit(string value) =>
        value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static string ChatIntent(SmallModelActionParameter[] intent, string name) =>
        intent.FirstOrDefault(parameter => parameter.Name == name)?.Value ?? string.Empty;

    private static string MultiplayerChatCoreSha256(string value)
    {
        using var sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value))
            .Select(valueByte => valueByte.ToString("x2", CultureInfo.InvariantCulture)));
    }
}

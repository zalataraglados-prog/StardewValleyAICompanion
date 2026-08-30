using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("chat_scope")]
    public string ChatScope { get; set; } = string.Empty;

    [JsonPropertyName("chat_reason")]
    public string ChatReason { get; set; } = string.Empty;

    [JsonPropertyName("confirm_chat")]
    public bool? ConfirmChat { get; set; }

    [JsonPropertyName("chat_message_text")]
    public string ChatMessageText { get; set; } = string.Empty;

    [JsonPropertyName("chat_message_sha256")]
    public string ChatMessageSha256 { get; set; } = string.Empty;

    [JsonPropertyName("chat_message_utf16_length")]
    public int? ChatMessageUtf16Length { get; set; }

    [JsonPropertyName("chat_projection_fingerprint")]
    public string ChatProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("chat_sender_player_id")]
    public string ChatSenderPlayerId { get; set; } = string.Empty;

    [JsonPropertyName("chat_sender_display_name")]
    public string ChatSenderDisplayName { get; set; } = string.Empty;

    [JsonPropertyName("chat_sender_default_color")]
    public string ChatSenderDefaultColor { get; set; } = string.Empty;

    [JsonPropertyName("chat_language_code")]
    public int? ChatLanguageCode { get; set; }

    [JsonPropertyName("chat_network_role")]
    public string ChatNetworkRole { get; set; } = string.Empty;

    [JsonPropertyName("chat_recipient_player_id")]
    public string ChatRecipientPlayerId { get; set; } = string.Empty;

    [JsonPropertyName("chat_recipient_display_name")]
    public string ChatRecipientDisplayName { get; set; } = string.Empty;

    [JsonPropertyName("chat_recipient_command_name")]
    public string ChatRecipientCommandName { get; set; } = string.Empty;

    [JsonPropertyName("chat_expected_wire_recipient_id")]
    public string ChatExpectedWireRecipientId { get; set; } = string.Empty;

    [JsonPropertyName("chat_expected_kind")]
    public int? ChatExpectedKind { get; set; }

    [JsonPropertyName("chat_network_message_type")]
    public int? ChatNetworkMessageType { get; set; }

    [JsonPropertyName("chat_message_count_before")]
    public int? ChatMessageCountBefore { get; set; }

    [JsonPropertyName("chat_message_limit")]
    public int? ChatMessageLimit { get; set; }

    [JsonPropertyName("chat_input_width_pixels")]
    public int? ChatInputWidthPixels { get; set; }

    [JsonPropertyName("chat_input_content_width_pixels")]
    public int? ChatInputContentWidthPixels { get; set; }

    [JsonPropertyName("chat_native_route")]
    public string ChatNativeRoute { get; set; } = string.Empty;
}

public sealed partial class TrainingExecutionResult
{
    [JsonPropertyName("chat_scope")]
    public string ChatScope { get; set; } = string.Empty;

    [JsonPropertyName("chat_recipient_player_id")]
    public string ChatRecipientPlayerId { get; set; } = string.Empty;

    [JsonPropertyName("chat_requested_message_sha256")]
    public string ChatRequestedMessageSha256 { get; set; } = string.Empty;

    [JsonPropertyName("chat_filtered_message_sha256")]
    public string ChatFilteredMessageSha256 { get; set; } = string.Empty;

    [JsonPropertyName("chat_requested_message_utf16_length")]
    public int? ChatRequestedMessageUtf16Length { get; set; }

    [JsonPropertyName("chat_filtered_message_utf16_length")]
    public int? ChatFilteredMessageUtf16Length { get; set; }

    [JsonPropertyName("chat_messages_before")]
    public int? ChatMessagesBefore { get; set; }

    [JsonPropertyName("chat_messages_after")]
    public int? ChatMessagesAfter { get; set; }

    [JsonPropertyName("chat_expected_kind")]
    public int? ChatExpectedKind { get; set; }

    [JsonPropertyName("chat_observed_kind")]
    public int? ChatObservedKind { get; set; }

    [JsonPropertyName("chat_language_code")]
    public int? ChatLanguageCode { get; set; }

    [JsonPropertyName("chat_network_role")]
    public string ChatNetworkRole { get; set; } = string.Empty;

    [JsonPropertyName("chat_native_route")]
    public string ChatNativeRoute { get; set; } = string.Empty;

    [JsonPropertyName("chat_local_receipt_verified")]
    public bool? ChatLocalReceiptVerified { get; set; }
}

using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyMultiplayerChatRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId is not ("executor.send_multiplayer_chat" or "multiplayer.send_chat" or
            "debug.setup_multiplayer_chat"))
            return;

        request.ChatScope = ReadQueueParameterString(item, "chat_scope");
        request.ChatReason = ReadQueueParameterString(item, "chat_reason");
        request.ConfirmChat = ReadQueueParameterBool(item, "confirm_chat");
        request.ChatMessageText = ReadQueueParameterString(item, "chat_message_text");
        request.ChatMessageSha256 = ReadQueueParameterString(item, "chat_message_sha256");
        request.ChatMessageUtf16Length = ReadQueueParameterInt(item, "chat_message_utf16_length");
        request.ChatProjectionFingerprint = ReadQueueParameterString(item, "chat_projection_fingerprint");
        request.ChatSenderPlayerId = ReadQueueParameterString(item, "chat_sender_player_id");
        request.ChatSenderDisplayName = ReadQueueParameterString(item, "chat_sender_display_name");
        request.ChatSenderDefaultColor = ReadQueueParameterString(item, "chat_sender_default_color");
        request.ChatLanguageCode = ReadQueueParameterInt(item, "chat_language_code");
        request.ChatNetworkRole = ReadQueueParameterString(item, "chat_network_role");
        request.ChatRecipientPlayerId = ReadQueueParameterString(item, "chat_recipient_player_id");
        request.ChatRecipientDisplayName = ReadQueueParameterString(item, "chat_recipient_display_name");
        request.ChatRecipientCommandName = ReadQueueParameterString(item, "chat_recipient_command_name");
        request.ChatExpectedWireRecipientId = ReadQueueParameterString(item, "chat_expected_wire_recipient_id");
        request.ChatExpectedKind = ReadQueueParameterInt(item, "chat_expected_kind");
        request.ChatNetworkMessageType = ReadQueueParameterInt(item, "chat_network_message_type");
        request.ChatMessageCountBefore = ReadQueueParameterInt(item, "chat_message_count_before");
        request.ChatMessageLimit = ReadQueueParameterInt(item, "chat_message_limit");
        request.ChatInputWidthPixels = ReadQueueParameterInt(item, "chat_input_width_pixels");
        request.ChatInputContentWidthPixels = ReadQueueParameterInt(item, "chat_input_content_width_pixels");
        request.ChatNativeRoute = ReadQueueParameterString(item, "chat_native_route");
    }
}

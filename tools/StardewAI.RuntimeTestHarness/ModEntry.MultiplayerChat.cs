using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string MultiplayerChatRuntimeNativeContract =
        "ChatBox_activate_then_ChatTextBox_character_input_under_880px_then_textBoxEnter_TextBox_then_global_AllPlayers_or_compiler_owned_message_private_then_Multiplayer_type10_network_dispatch_and_sender_local_ChatMessage_receipt";

    private TrainingExecutionResult ExecuteSendMultiplayerChat(TrainingExecutionRequest request)
    {
        var started = DateTimeOffset.UtcNow.ToString("O");
        var genericReasons = ValidateExecutionRequest(request);
        var reasons = genericReasons.Concat(ValidateMultiplayerChatRequest(request)).Distinct(StringComparer.Ordinal).ToArray();
        if (reasons.Length > 0)
            return MultiplayerChatResult(request, started, false, string.Empty, 0, 0, reasons);

        var chat = Game1.chatBox;
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.currentMinigame is not null ||
            chat is null || chat.isActive())
            return MultiplayerChatResult(request, started, false, string.Empty, 0, 0,
                "multiplayer_chat_player_menu_or_chat_box_not_ready");

        var liveReasons = ValidateMultiplayerChatLiveState(request);
        if (liveReasons.Length > 0)
            return MultiplayerChatResult(request, started, false, string.Empty, chat.messages.Count, chat.messages.Count, liveReasons);

        var privateMessage = request.ChatScope == "private";
        var nativeInput = privateMessage
            ? "/message " + request.ChatRecipientCommandName + " " + request.ChatMessageText
            : request.ChatMessageText;
        var beforeCount = chat.messages.Count;
        var beforeLast = beforeCount == 0 ? null : chat.messages[^1];
        chat.activate();
        foreach (var character in nativeInput)
            chat.chatBox.RecieveTextInput(character);

        var typedInput = ChatMessage.makeMessagePlaintext(chat.chatBox.finalText, include_color_information: false);
        if (!string.Equals(typedInput, nativeInput, StringComparison.Ordinal))
        {
            chat.clickAway();
            return MultiplayerChatResult(request, started, false, string.Empty, beforeCount, chat.messages.Count,
                "multiplayer_chat_native_input_width_or_strict_platform_filter_rejected_text");
        }

        var wirePayload = privateMessage
            ? Utility.FilterDirtyWords(request.ChatMessageText)
            : Utility.FilterDirtyWords(ChatMessage.makeMessagePlaintext(chat.chatBox.finalText, include_color_information: true));
        var expectedKind = privateMessage ? 3 : 0;
        var expectedReceipt = BuildExpectedMultiplayerChatReceipt(chat, expectedKind, wirePayload);
        chat.textBoxEnter(chat.chatBox);

        var afterCount = chat.messages.Count;
        var afterLast = afterCount == 0 ? null : chat.messages[^1];
        var expectedCount = Math.Min(beforeCount + 1, request.ChatMessageLimit!.Value);
        var nativeInputReset = chat.chatBox.currentWidth == 0f &&
            ChatMessage.makeMessagePlaintext(chat.chatBox.finalText, include_color_information: false).Length == 0;
        var verified = !chat.isActive() && nativeInputReset &&
            afterCount == expectedCount && afterLast is not null && !ReferenceEquals(afterLast, beforeLast) &&
            MultiplayerChatReceiptEquals(afterLast, expectedReceipt);
        return MultiplayerChatResult(request, started, verified, wirePayload, beforeCount, afterCount,
            verified
                ? Array.Empty<string>()
                : new[] { "multiplayer_chat_sender_local_native_receipt_mismatch" });
    }

    private static string[] ValidateMultiplayerChatRequest(TrainingExecutionRequest request)
    {
        var reasons = new List<string>();
        if (request.ChatScope is not ("global" or "private") || string.IsNullOrWhiteSpace(request.ChatReason) ||
            request.ConfirmChat != true || !MultiplayerChatRuntimeMessageIsValid(request.ChatMessageText))
            reasons.Add("multiplayer_chat_exact_scope_reason_text_and_confirmation_required");
        if (request.ChatMessageSha256.Length != 64 || request.ChatProjectionFingerprint.Length != 64 ||
            request.ChatMessageUtf16Length != request.ChatMessageText.Length ||
            request.ChatMessageSha256 != MultiplayerChatRuntimeSha256(request.ChatMessageText) ||
            !request.ChatLanguageCode.HasValue || !request.ChatExpectedKind.HasValue ||
            request.ChatNetworkMessageType != 10 || request.ChatMessageLimit != 10 ||
            !request.ChatInputWidthPixels.HasValue || !request.ChatInputContentWidthPixels.HasValue ||
            request.NativeContract != MultiplayerChatRuntimeNativeContract)
            reasons.Add("multiplayer_chat_complete_typed_projection_required");
        if (request.ChatScope == "global" &&
            (request.ChatExpectedWireRecipientId != Multiplayer.AllPlayers.ToString(CultureInfo.InvariantCulture) ||
             request.ChatExpectedKind != 0 || request.ChatNativeRoute != "global_all_players"))
            reasons.Add("multiplayer_chat_global_route_contract_mismatch");
        if (request.ChatScope == "private" &&
            (string.IsNullOrWhiteSpace(request.ChatRecipientPlayerId) ||
             string.IsNullOrWhiteSpace(request.ChatRecipientDisplayName) ||
             string.IsNullOrWhiteSpace(request.ChatRecipientCommandName) ||
             request.ChatExpectedWireRecipientId != request.ChatRecipientPlayerId ||
             request.ChatExpectedKind != 3 || request.ChatNativeRoute != "compiler_owned_message_private" ||
             request.ChatMessageText != string.Join(" ", ArgUtility.SplitBySpace(request.ChatMessageText))))
            reasons.Add("multiplayer_chat_private_exact_target_and_stable_text_required");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string[] ValidateMultiplayerChatLiveState(TrainingExecutionRequest request)
    {
        var reasons = new List<string>();
        var chat = Game1.chatBox;
        var networkRole = Game1.IsServer ? "server" : Game1.IsClient ? "client" : "none";
        if (!Game1.IsMultiplayer || networkRole == "none" || request.ChatNetworkRole != networkRole ||
            request.ChatSenderPlayerId != Game1.player.UniqueMultiplayerID.ToString(CultureInfo.InvariantCulture) ||
            request.ChatSenderDisplayName != Game1.player.displayName ||
            request.ChatSenderDefaultColor != (Game1.player.defaultChatColor ?? string.Empty) ||
            request.ChatLanguageCode != (int)LocalizedContentManager.CurrentLanguageCode ||
            request.ChatMessageCountBefore != chat.messages.Count || request.ChatInputWidthPixels != chat.chatBox.Width ||
            request.ChatMessageLimit != chat.maxMessages ||
            request.ChatInputContentWidthPixels != chat.chatBox.Width - 16)
            reasons.Add("multiplayer_chat_live_projection_drifted");

        if (request.ChatScope == "private")
        {
            if (!long.TryParse(request.ChatRecipientPlayerId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var recipientId) ||
                !Game1.otherFarmers.TryGetValue(recipientId, out var target) || !target.isActive() ||
                target.displayName != request.ChatRecipientDisplayName ||
                string.Join(" ", ArgUtility.SplitBySpace(target.displayName)) != request.ChatRecipientCommandName)
            {
                reasons.Add("multiplayer_chat_private_recipient_drifted");
            }
            else
            {
                var command = new[] { "message" }.Concat(
                    ArgUtility.SplitBySpace(request.ChatRecipientCommandName + " " + request.ChatMessageText)).ToArray();
                var matchingIndex = 0;
                var nativeMatch = Game1.chatBox.findMatchingFarmer(command, ref matchingIndex);
                if (!ReferenceEquals(nativeMatch, target) || matchingIndex != ArgUtility.SplitBySpace(target.displayName).Length)
                    reasons.Add("multiplayer_chat_private_native_first_match_drifted");
            }
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static ChatMessage BuildExpectedMultiplayerChatReceipt(ChatBox chat, int kind, string wirePayload)
    {
        var language = LocalizedContentManager.CurrentLanguageCode;
        var expected = new ChatMessage
        {
            color = chat.messageColor(kind),
            language = language
        };
        var formatted = chat.formatMessage(Game1.player.UniqueMultiplayerID, kind, wirePayload);
        expected.parseMessageForEmoji(Game1.parseText(formatted, chat.chatBox.Font, chat.chatBox.Width - 16));
        return expected;
    }

    private static bool MultiplayerChatReceiptEquals(ChatMessage actual, ChatMessage expected) =>
        actual.language == expected.language && actual.color == expected.color &&
        actual.message.Count == expected.message.Count && actual.message.Zip(expected.message).All(pair =>
            pair.First.emojiIndex == pair.Second.emojiIndex &&
            string.Equals(pair.First.message, pair.Second.message, StringComparison.Ordinal));

    private static TrainingExecutionResult MultiplayerChatResult(
        TrainingExecutionRequest request,
        string started,
        bool verified,
        string filteredPayload,
        int beforeCount,
        int afterCount,
        params string[] reasons)
    {
        var filteredDigest = string.IsNullOrEmpty(filteredPayload) ? string.Empty : MultiplayerChatRuntimeSha256(filteredPayload);
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "send_multiplayer_chat",
            PrimitiveVerificationStatus = verified ? "verified" : "blocked",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_ChatTextBox_character_input_verified", "native_type10_dispatch_branch_invoked", "sender_local_ChatMessage_receipt_verified" }
                : reasons,
            RequestedEffect = "chat_scope=" + request.ChatScope + ";message_sha256=" + request.ChatMessageSha256,
            ObservedEffect = "chat_scope=" + request.ChatScope + ";filtered_message_sha256=" + filteredDigest +
                ";messages_before=" + beforeCount + ";messages_after=" + afterCount +
                ";remote_delivery_receipt=not_fabricated",
            ChatScope = request.ChatScope,
            ChatRecipientPlayerId = request.ChatRecipientPlayerId,
            ChatRequestedMessageSha256 = request.ChatMessageSha256,
            ChatFilteredMessageSha256 = filteredDigest,
            ChatRequestedMessageUtf16Length = request.ChatMessageText.Length,
            ChatFilteredMessageUtf16Length = string.IsNullOrEmpty(filteredPayload) ? null : filteredPayload.Length,
            ChatMessagesBefore = beforeCount,
            ChatMessagesAfter = afterCount,
            ChatExpectedKind = request.ChatExpectedKind,
            ChatObservedKind = verified ? request.ChatExpectedKind : null,
            ChatLanguageCode = request.ChatLanguageCode,
            ChatNetworkRole = request.ChatNetworkRole,
            ChatNativeRoute = request.ChatNativeRoute,
            ChatLocalReceiptVerified = verified,
            BlockReasons = verified ? Array.Empty<string>() : reasons
        };
    }

    private static bool MultiplayerChatRuntimeMessageIsValid(string message) =>
        !string.IsNullOrWhiteSpace(message) && message[0] != '/' &&
        message.All(character => !char.IsControl(character));

    private static string MultiplayerChatRuntimeSha256(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

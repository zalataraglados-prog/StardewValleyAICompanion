using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewModdingAPI;
using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    internal const string MultiplayerChatNativeContract =
        "ChatBox_activate_then_ChatTextBox_character_input_under_880px_then_textBoxEnter_TextBox_then_global_AllPlayers_or_compiler_owned_message_private_then_Multiplayer_type10_network_dispatch_and_sender_local_ChatMessage_receipt";

    private static object ReadMultiplayerChat(Farmer? player)
    {
        if (player is null || !Context.IsWorldReady || Game1.chatBox is null)
        {
            return new
            {
                schema_version = "multiplayer_chat.v1",
                projection_status = "unavailable_world_player_or_chat_box",
                online_recipients = Array.Empty<object>()
            };
        }

        var chat = Game1.chatBox;
        var recipients = Game1.otherFarmers.Values.Select((farmer, index) =>
        {
            var displayTokens = ArgUtility.SplitBySpace(farmer.displayName);
            var active = farmer.isActive();
            return new
            {
                native_enumeration_index = index,
                player_id = farmer.UniqueMultiplayerID.ToString(CultureInfo.InvariantCulture),
                name = farmer.Name,
                display_name = farmer.displayName,
                display_name_tokens = displayTokens,
                native_command_name = string.Join(" ", displayTokens),
                native_match_token_count = displayTokens.Length,
                is_active = active,
                private_base_gate_status = !active
                    ? "blocked_not_active"
                    : displayTokens.Length == 0
                        ? "blocked_blank_display_name"
                        : "payload_dependent_first_match_validation_required"
            };
        }).ToArray();
        var networkRole = Game1.IsServer ? "server" : Game1.IsClient ? "client" : "none";
        var activeMenu = Game1.activeClickableMenu?.GetType().FullName ?? "none";
        var activeMinigame = Game1.currentMinigame?.GetType().FullName ?? "none";
        var serviceStatus = !Game1.IsMultiplayer
            ? "blocked_not_multiplayer"
            : networkRole == "none"
                ? "blocked_no_network_role"
                : activeMenu != "none"
                    ? "blocked_active_menu"
                    : Game1.dialogueUp
                        ? "blocked_dialogue"
                        : activeMinigame != "none"
                            ? "blocked_active_minigame"
                            : chat.isActive()
                                ? "blocked_chat_box_already_active"
                                : "ready";
        var projectionBody = new
        {
            sender = player.UniqueMultiplayerID,
            player.displayName,
            player.defaultChatColor,
            language = (int)LocalizedContentManager.CurrentLanguageCode,
            networkRole,
            isMultiplayer = Game1.IsMultiplayer,
            isServer = Game1.IsServer,
            isClient = Game1.IsClient,
            chatActive = chat.isActive(),
            activeMenu,
            dialogueUp = Game1.dialogueUp,
            activeMinigame,
            messageCount = chat.messages.Count,
            messageLimit = chat.maxMessages,
            recipients
        };

        return new
        {
            schema_version = "multiplayer_chat.v1",
            projection_status = "complete_locked_base_1.6.15",
            projection_fingerprint = MultiplayerChatSha256(JsonSerializer.Serialize(projectionBody)),
            invocation_policy = "player_command_only",
            service_status = serviceStatus,
            network_role = networkRole,
            is_multiplayer = Game1.IsMultiplayer,
            is_server = Game1.IsServer,
            is_client = Game1.IsClient,
            sender_player_id = player.UniqueMultiplayerID.ToString(CultureInfo.InvariantCulture),
            sender_display_name = player.displayName,
            sender_default_chat_color = player.defaultChatColor ?? string.Empty,
            language_code = (int)LocalizedContentManager.CurrentLanguageCode,
            all_players_recipient_id = Multiplayer.AllPlayers.ToString(CultureInfo.InvariantCulture),
            global_chat_kind = 0,
            private_chat_kind = 3,
            network_message_type = 10,
            chat_box_present = true,
            chat_box_active = chat.isActive(),
            chat_message_count = chat.messages.Count,
            chat_message_limit = chat.maxMessages,
            chat_message_display_ticks = 600,
            input_width_pixels = chat.chatBox.Width,
            input_content_width_pixels = chat.chatBox.Width - 16,
            input_width_rule = "current_width_plus_next_character_width_strictly_less_than_input_content_width_pixels",
            content_filter_policy = "ChatTextBox_strict_platform_filter_then_Program_sdk_FilterDirtyWords",
            command_policy = "leading_slash_blocked_except_compiler_owned_message_private",
            private_target_policy = "exact_player_id_then_payload_dependent_native_first_display_name_prefix_match_must_resolve_same_active_player",
            reply_policy = "unsupported_transient_last_sender_state",
            emote_policy = "separate_social.emote_action",
            online_recipients = recipients,
            native_contract = MultiplayerChatNativeContract,
            direct_transport_policy = "production_executor_must_use_ChatBox_textBoxEnter_and_must_not_call_sendChatMessage_or_receiveChatMessage_directly"
        };
    }

    private static string MultiplayerChatSha256(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyPlayerEmoteRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId is not ("social.emote" or "executor.perform_emote" or "debug.setup_player_emote"))
            return;

        request.EmoteKey = ReadQueueParameterString(item, "emote_key");
        request.EmoteReason = ReadQueueParameterString(item, "emote_reason");
        request.ConfirmEmote = ReadQueueParameterBool(item, "confirm_emote");
        request.EmoteProjectionFingerprint = ReadQueueParameterString(item, "emote_projection_fingerprint");
        request.EmoteOptionFingerprint = ReadQueueParameterString(item, "emote_option_fingerprint");
        request.EmoteIndex = ReadQueueParameterInt(item, "emote_index");
        request.EmoteIconIndex = ReadQueueParameterInt(item, "emote_icon_index");
        request.EmoteHasAnimation = ReadQueueParameterBool(item, "emote_has_animation");
        request.EmoteAnimationFacingDirection = ReadQueueParameterInt(item, "emote_animation_facing_direction");
        request.EmoteAnimationDurationMilliseconds = ReadQueueParameterInt(item, "emote_animation_duration_milliseconds");
        request.EmoteHidden = ReadQueueParameterBool(item, "emote_hidden");
        request.EmotePerformedEntryBefore = ReadQueueParameterBool(item, "emote_performed_entry_before");
        request.EmotePerformedValueBefore = ReadQueueParameterBool(item, "emote_performed_value_before");
        request.EmotePlayerId = ReadQueueParameterLong(item, "emote_player_id");
        request.EmoteLanguageCode = ReadQueueParameterInt(item, "emote_language_code");
        request.EmoteNetworkRole = ReadQueueParameterString(item, "emote_network_role");
        request.EmoteChatInputWidthPixels = ReadQueueParameterInt(item, "emote_chat_input_width_pixels");
        request.EmoteChatInputContentWidthPixels = ReadQueueParameterInt(item, "emote_chat_input_content_width_pixels");
        request.EmoteNativeInput = ReadQueueParameterString(item, "emote_native_input");
    }
}

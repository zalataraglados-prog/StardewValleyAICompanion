using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("emote_key")]
    public string EmoteKey { get; set; } = string.Empty;

    [JsonPropertyName("emote_reason")]
    public string EmoteReason { get; set; } = string.Empty;

    [JsonPropertyName("confirm_emote")]
    public bool? ConfirmEmote { get; set; }

    [JsonPropertyName("emote_projection_fingerprint")]
    public string EmoteProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("emote_option_fingerprint")]
    public string EmoteOptionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("emote_index")]
    public int? EmoteIndex { get; set; }

    [JsonPropertyName("emote_icon_index")]
    public int? EmoteIconIndex { get; set; }

    [JsonPropertyName("emote_has_animation")]
    public bool? EmoteHasAnimation { get; set; }

    [JsonPropertyName("emote_animation_facing_direction")]
    public int? EmoteAnimationFacingDirection { get; set; }

    [JsonPropertyName("emote_animation_duration_milliseconds")]
    public int? EmoteAnimationDurationMilliseconds { get; set; }

    [JsonPropertyName("emote_hidden")]
    public bool? EmoteHidden { get; set; }

    [JsonPropertyName("emote_performed_entry_before")]
    public bool? EmotePerformedEntryBefore { get; set; }

    [JsonPropertyName("emote_performed_value_before")]
    public bool? EmotePerformedValueBefore { get; set; }

    [JsonPropertyName("emote_player_id")]
    public long? EmotePlayerId { get; set; }

    [JsonPropertyName("emote_language_code")]
    public int? EmoteLanguageCode { get; set; }

    [JsonPropertyName("emote_network_role")]
    public string EmoteNetworkRole { get; set; } = string.Empty;

    [JsonPropertyName("emote_chat_input_width_pixels")]
    public int? EmoteChatInputWidthPixels { get; set; }

    [JsonPropertyName("emote_chat_input_content_width_pixels")]
    public int? EmoteChatInputContentWidthPixels { get; set; }

    [JsonPropertyName("emote_native_input")]
    public string EmoteNativeInput { get; set; } = string.Empty;
}

public sealed partial class TrainingExecutionResult
{
    [JsonPropertyName("emote_key")]
    public string EmoteKey { get; set; } = string.Empty;

    [JsonPropertyName("emote_index")]
    public int? EmoteIndex { get; set; }

    [JsonPropertyName("emote_icon_index")]
    public int? EmoteIconIndex { get; set; }

    [JsonPropertyName("emote_performed_entry_after")]
    public bool? EmotePerformedEntryAfter { get; set; }

    [JsonPropertyName("emote_performed_value_after")]
    public bool? EmotePerformedValueAfter { get; set; }

    [JsonPropertyName("emote_icon_receipt_observed")]
    public bool? EmoteIconReceiptObserved { get; set; }

    [JsonPropertyName("emote_animation_receipt_observed")]
    public bool? EmoteAnimationReceiptObserved { get; set; }

    [JsonPropertyName("emote_current_icon_index_after")]
    public int? EmoteCurrentIconIndexAfter { get; set; }

    [JsonPropertyName("emote_network_role")]
    public string EmoteNetworkRole { get; set; } = string.Empty;

    [JsonPropertyName("emote_native_command_receipt_verified")]
    public bool? EmoteNativeCommandReceiptVerified { get; set; }
}

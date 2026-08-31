using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.State;

public sealed class PlayerEmoteProjectionRef
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "player_emote.v1";

    [JsonPropertyName("projection_status")]
    public string ProjectionStatus { get; set; } = "unavailable";

    [JsonPropertyName("invocation_policy")]
    public string InvocationPolicy { get; set; } = "player_command_only";

    [JsonPropertyName("native_contract")]
    public string NativeContract { get; set; } = string.Empty;

    [JsonPropertyName("service_status")]
    public string ServiceStatus { get; set; } = "unavailable";

    [JsonPropertyName("player_id")]
    public long PlayerId { get; set; }

    [JsonPropertyName("language_code")]
    public int LanguageCode { get; set; }

    [JsonPropertyName("network_role")]
    public string NetworkRole { get; set; } = "none";

    [JsonPropertyName("can_emote_native")]
    public bool CanEmoteNative { get; set; }

    [JsonPropertyName("chat_box_present")]
    public bool ChatBoxPresent { get; set; }

    [JsonPropertyName("chat_box_active")]
    public bool ChatBoxActive { get; set; }

    [JsonPropertyName("chat_input_width_pixels")]
    public int ChatInputWidthPixels { get; set; }

    [JsonPropertyName("chat_input_content_width_pixels")]
    public int ChatInputContentWidthPixels { get; set; }

    [JsonPropertyName("menu_clear")]
    public bool MenuClear { get; set; }

    [JsonPropertyName("dialogue_up")]
    public bool DialogueUp { get; set; }

    [JsonPropertyName("active_minigame_type")]
    public string ActiveMinigameType { get; set; } = "none";

    [JsonPropertyName("is_emoting")]
    public bool IsEmoting { get; set; }

    [JsonPropertyName("is_emote_animating")]
    public bool IsEmoteAnimating { get; set; }

    [JsonPropertyName("current_emote_icon_index")]
    public int CurrentEmoteIconIndex { get; set; }

    [JsonPropertyName("current_emote_frame_index")]
    public int CurrentEmoteFrameIndex { get; set; }

    [JsonPropertyName("facing_direction")]
    public int FacingDirection { get; set; }

    [JsonPropertyName("raw_favorites")]
    public string[] RawFavorites { get; set; } = Array.Empty<string>();

    [JsonPropertyName("effective_favorites")]
    public string[] EffectiveFavorites { get; set; } = Array.Empty<string>();

    [JsonPropertyName("performed_emote_keys")]
    public string[] PerformedEmoteKeys { get; set; } = Array.Empty<string>();

    [JsonPropertyName("emotes")]
    public PlayerEmoteOptionRef[] Emotes { get; set; } = Array.Empty<PlayerEmoteOptionRef>();

    [JsonPropertyName("projection_fingerprint")]
    public string ProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("blocked_diagnostics")]
    public string[] BlockedDiagnostics { get; set; } = Array.Empty<string>();
}

public sealed class PlayerEmoteOptionRef
{
    [JsonPropertyName("emote_index")]
    public int EmoteIndex { get; set; }

    [JsonPropertyName("emote_key")]
    public string EmoteKey { get; set; } = string.Empty;

    [JsonPropertyName("display_name_key")]
    public string DisplayNameKey { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("icon_index")]
    public int IconIndex { get; set; }

    [JsonPropertyName("hidden")]
    public bool Hidden { get; set; }

    [JsonPropertyName("has_animation")]
    public bool HasAnimation { get; set; }

    [JsonPropertyName("animation_facing_direction")]
    public int AnimationFacingDirection { get; set; }

    [JsonPropertyName("animation_frames")]
    public PlayerEmoteAnimationFrameRef[] AnimationFrames { get; set; } = Array.Empty<PlayerEmoteAnimationFrameRef>();

    [JsonPropertyName("animation_duration_milliseconds")]
    public int AnimationDurationMilliseconds { get; set; }

    [JsonPropertyName("performed_entry_present")]
    public bool PerformedEntryPresent { get; set; }

    [JsonPropertyName("performed_value")]
    public bool PerformedValue { get; set; }

    [JsonPropertyName("selector_visible")]
    public bool SelectorVisible { get; set; }

    [JsonPropertyName("favorite_slots")]
    public int[] FavoriteSlots { get; set; } = Array.Empty<int>();

    [JsonPropertyName("native_command_accepted")]
    public bool NativeCommandAccepted { get; set; }

    [JsonPropertyName("option_fingerprint")]
    public string OptionFingerprint { get; set; } = string.Empty;
}

public sealed class PlayerEmoteAnimationFrameRef
{
    [JsonPropertyName("frame_index")]
    public int FrameIndex { get; set; }

    [JsonPropertyName("duration_milliseconds")]
    public int DurationMilliseconds { get; set; }

    [JsonPropertyName("position_offset")]
    public int PositionOffset { get; set; }

    [JsonPropertyName("arm_offset")]
    public int ArmOffset { get; set; }

    [JsonPropertyName("x_offset")]
    public int XOffset { get; set; }

    [JsonPropertyName("flip")]
    public bool Flip { get; set; }

    [JsonPropertyName("has_start_callback")]
    public bool HasStartCallback { get; set; }

    [JsonPropertyName("has_end_callback")]
    public bool HasEndCallback { get; set; }
}

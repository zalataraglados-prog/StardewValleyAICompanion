using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("action_origin_debris_state")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ActionOriginDebrisState { get; set; }
}

public sealed partial class ActionOriginObservedDebris
{
    [JsonPropertyName("raw_state")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ActionOriginDebrisRawState? RawState { get; set; }
}

public sealed class ActionOriginDebrisRawState
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; set; } = string.Empty;
    [JsonPropertyName("identity")] public ActionOriginDebrisIdentityState? Identity { get; set; }
    [JsonPropertyName("motion")] public ActionOriginDebrisMotionState? Motion { get; set; }
    [JsonPropertyName("visual")] public ActionOriginDebrisVisualState? Visual { get; set; }
    [JsonPropertyName("chunks")] public ActionOriginRawChunkState[]? Chunks { get; set; }
    [JsonPropertyName("event_occurrences")] public int? EventOccurrences { get; set; }
}

public sealed class ActionOriginDebrisIdentityState
{
    [JsonPropertyName("debris_type")] public int? DebrisType { get; set; }
    [JsonPropertyName("chunk_type")] public int? ChunkType { get; set; }
    [JsonPropertyName("item_id")] public ActionOriginNullableText? ItemId { get; set; }
    [JsonPropertyName("item_quality")] public int? ItemQuality { get; set; }
    [JsonPropertyName("held_item")] public ActionOriginNativeItemReference? HeldItem { get; set; }
    [JsonPropertyName("dropped_by_player_id")] public long? DroppedByPlayerId { get; set; }
    [JsonPropertyName("target_defined")] public bool? TargetDefined { get; set; }
    [JsonPropertyName("target_uid")] public long? TargetUid { get; set; }
    [JsonPropertyName("hover")] public ActionOriginCharacterReference? Hover { get; set; }
}

public sealed class ActionOriginCharacterReference
{
    [JsonPropertyName("is_null")] public bool? IsNull { get; set; }
    [JsonPropertyName("reference_id")] public string? ReferenceId { get; set; }
    [JsonPropertyName("runtime_type")] public string? RuntimeType { get; set; }
}

public sealed class ActionOriginDebrisMotionState
{
    [JsonPropertyName("relative_x_bits")] public int? RelativeXBits { get; set; }
    [JsonPropertyName("final_y_level")] public int? FinalYLevel { get; set; }
    [JsonPropertyName("final_y_target")] public int? FinalYTarget { get; set; }
    [JsonPropertyName("bounce_time_bits")] public int? TimeSinceDoneBouncingBits { get; set; }
    [JsonPropertyName("scale_bits")] public int? ScaleBits { get; set; }
    [JsonPropertyName("move_towards_player")] public bool? MoveTowardsPlayer { get; set; }
    [JsonPropertyName("moving_up")] public bool? MovingUp { get; set; }
    [JsonPropertyName("flopping_fish")] public bool? FloppingFish { get; set; }
    [JsonPropertyName("is_fishable")] public bool? IsFishable { get; set; }
    [JsonPropertyName("moving_final_y_level")] public bool? MovingFinalYLevel { get; set; }
    [JsonPropertyName("is_sinking")] public bool? IsSinking { get; set; }
    [JsonPropertyName("return_to_dropper_timer")] public int? ReturnToDropperTimer { get; set; }
}

public sealed class ActionOriginDebrisVisualState
{
    [JsonPropertyName("source_rect_size")] public int? SourceRectSize { get; set; }
    [JsonPropertyName("message")] public ActionOriginNullableText? Message { get; set; }
    [JsonPropertyName("non_sprite_color")] public uint? NonSpriteColor { get; set; }
    [JsonPropertyName("chunks_color")] public uint? ChunksColor { get; set; }
    [JsonPropertyName("animation_timer_bits")] public int? AnimationTimerBits { get; set; }
    [JsonPropertyName("sprite_sheet_name")] public ActionOriginNullableText? SpriteSheetName { get; set; }
    [JsonPropertyName("texture_cached")] public bool? TextureCached { get; set; }
}

public sealed class ActionOriginRawChunkState
{
    [JsonPropertyName("chunk_id")] public string? ChunkId { get; set; }
    [JsonPropertyName("position")] public ActionOriginChunkPositionState? Position { get; set; }
    [JsonPropertyName("x_velocity_bits")] public int? XVelocityBits { get; set; }
    [JsonPropertyName("y_velocity_bits")] public int? YVelocityBits { get; set; }
    [JsonPropertyName("passed_resting_line")] public bool? PassedRestingLine { get; set; }
    [JsonPropertyName("bounces")] public int? Bounces { get; set; }
    [JsonPropertyName("bob_bits")] public int? BobBits { get; set; }
    [JsonPropertyName("sink_timer")] public int? SinkTimer { get; set; }
    [JsonPropertyName("random_offset")] public int? RandomOffset { get; set; }
    [JsonPropertyName("hit_wall")] public bool? HitWall { get; set; }
    [JsonPropertyName("x_sprite_sheet")] public int? XSpriteSheet { get; set; }
    [JsonPropertyName("y_sprite_sheet")] public int? YSpriteSheet { get; set; }
    [JsonPropertyName("rotation_bits")] public int? RotationBits { get; set; }
    [JsonPropertyName("rotation_velocity_bits")] public int? RotationVelocityBits { get; set; }
    [JsonPropertyName("scale_bits")] public int? ScaleBits { get; set; }
    [JsonPropertyName("alpha_bits")] public int? AlphaBits { get; set; }
}

public sealed class ActionOriginChunkPositionState
{
    [JsonPropertyName("reference_id")] public string? ReferenceId { get; set; }
    [JsonPropertyName("x_bits")] public int? XBits { get; set; }
    [JsonPropertyName("y_bits")] public int? YBits { get; set; }
    [JsonPropertyName("paused")] public bool? Paused { get; set; }
    [JsonPropertyName("moving")] public bool? Moving { get; set; }
    [JsonPropertyName("extrapolation_enabled")] public bool? ExtrapolationEnabled { get; set; }
    [JsonPropertyName("pause_interpolation_wait")] public bool? PauseInterpolationWait { get; set; }
    [JsonPropertyName("incoming")] public ActionOriginPauseEventState[]? Incoming { get; set; }
    [JsonPropertyName("outgoing")] public ActionOriginPauseEventState[]? Outgoing { get; set; }
    [JsonPropertyName("pause_handler_count")] public int? PauseHandlerCount { get; set; }
    [JsonPropertyName("change_handler_count")] public int? ChangeHandlerCount { get; set; }
    [JsonPropertyName("visible_change_handler_count")] public int? VisibleChangeHandlerCount { get; set; }
}

public sealed class ActionOriginPauseEventState
{
    [JsonPropertyName("reference_id")] public string? ReferenceId { get; set; }
    [JsonPropertyName("argument")] public bool? Argument { get; set; }
    [JsonPropertyName("timestamp")] public uint? Timestamp { get; set; }
}

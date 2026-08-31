using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.State;

public sealed class StoryEventProjectionRef
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "story_event.v1";

    [JsonPropertyName("projection_status")]
    public string ProjectionStatus { get; set; } = "unavailable";

    [JsonPropertyName("projection_fingerprint")]
    public string ProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("native_contract")]
    public string NativeContract { get; set; } = string.Empty;

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("event_id")]
    public string EventId { get; set; } = string.Empty;

    [JsonPropertyName("location_id")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("current_command_index")]
    public int CurrentCommandIndex { get; set; }

    [JsonPropertyName("command_count")]
    public int CommandCount { get; set; }

    [JsonPropertyName("current_command_raw")]
    public string CurrentCommandRaw { get; set; } = string.Empty;

    [JsonPropertyName("current_command_name")]
    public string CurrentCommandName { get; set; } = string.Empty;

    [JsonPropertyName("commands")]
    public StoryEventCommandRef[] Commands { get; set; } = Array.Empty<StoryEventCommandRef>();

    [JsonPropertyName("dialogue_question_key")]
    public string DialogueQuestionKey { get; set; } = string.Empty;

    [JsonPropertyName("dialogue_responses")]
    public StoryEventResponseRef[] DialogueResponses { get; set; } = Array.Empty<StoryEventResponseRef>();

    [JsonPropertyName("active_minigame_type")]
    public string ActiveMinigameType { get; set; } = string.Empty;

    [JsonPropertyName("active_minigame_id")]
    public string ActiveMinigameId { get; set; } = string.Empty;

    [JsonPropertyName("active_minigame_native_contract")]
    public string ActiveMinigameNativeContract { get; set; } = string.Empty;

    [JsonPropertyName("active_minigame_owner_kind")]
    public string ActiveMinigameOwnerKind { get; set; } = string.Empty;

    [JsonPropertyName("active_minigame_execution_mode")]
    public string ActiveMinigameExecutionMode { get; set; } = string.Empty;

    [JsonPropertyName("active_minigame_support_status")]
    public string ActiveMinigameSupportStatus { get; set; } = string.Empty;

    [JsonPropertyName("active_minigame_supported")]
    public bool ActiveMinigameSupported { get; set; }

    [JsonPropertyName("active_minigame_requires_model_response")]
    public bool ActiveMinigameRequiresModelResponse { get; set; }

    [JsonPropertyName("active_minigame_block_reason")]
    public string ActiveMinigameBlockReason { get; set; } = string.Empty;

    [JsonPropertyName("player_control_sequence")]
    public bool PlayerControlSequence { get; set; }

    [JsonPropertyName("player_control_sequence_id")]
    public string PlayerControlSequenceId { get; set; } = string.Empty;

    [JsonPropertyName("boundary_kind")]
    public string BoundaryKind { get; set; } = string.Empty;

    [JsonPropertyName("blocked_diagnostics")]
    public string[] BlockedDiagnostics { get; set; } = Array.Empty<string>();
}

public sealed class StoryEventCommandRef
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("raw")]
    public string Raw { get; set; } = string.Empty;

    [JsonPropertyName("command_name")]
    public string CommandName { get; set; } = string.Empty;

    [JsonPropertyName("resolved_command_name")]
    public string ResolvedCommandName { get; set; } = string.Empty;

    [JsonPropertyName("is_initialization_field")]
    public bool IsInitializationField { get; set; }

    [JsonPropertyName("is_current")]
    public bool IsCurrent { get; set; }
}

public sealed class StoryEventResponseRef
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("response_key")]
    public string ResponseKey { get; set; } = string.Empty;

    [JsonPropertyName("response_text")]
    public string ResponseText { get; set; } = string.Empty;

    [JsonPropertyName("hotkey")]
    public string Hotkey { get; set; } = string.Empty;
}

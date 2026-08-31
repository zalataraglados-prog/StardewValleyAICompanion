using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("story_event_projection_fingerprint")]
    public string StoryEventProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("story_event_id")]
    public string StoryEventId { get; set; } = string.Empty;

    [JsonPropertyName("story_event_location_id")]
    public string StoryEventLocationId { get; set; } = string.Empty;

    [JsonPropertyName("story_event_command_index")]
    public int? StoryEventCommandIndex { get; set; }

    [JsonPropertyName("story_event_command_raw")]
    public string StoryEventCommandRaw { get; set; } = string.Empty;

    [JsonPropertyName("story_event_boundary_kind")]
    public string StoryEventBoundaryKind { get; set; } = string.Empty;

    [JsonPropertyName("story_event_question_key")]
    public string StoryEventQuestionKey { get; set; } = string.Empty;

    [JsonPropertyName("story_event_response_index")]
    public int? StoryEventResponseIndex { get; set; }

    [JsonPropertyName("story_event_response_key")]
    public string StoryEventResponseKey { get; set; } = string.Empty;

    [JsonPropertyName("story_event_minigame_native_contract")]
    public string StoryEventMinigameNativeContract { get; set; } = string.Empty;

    [JsonPropertyName("story_event_minigame_type")]
    public string StoryEventMinigameType { get; set; } = string.Empty;

    [JsonPropertyName("story_event_minigame_id")]
    public string StoryEventMinigameId { get; set; } = string.Empty;

    [JsonPropertyName("story_event_minigame_owner_kind")]
    public string StoryEventMinigameOwnerKind { get; set; } = string.Empty;

    [JsonPropertyName("story_event_minigame_execution_mode")]
    public string StoryEventMinigameExecutionMode { get; set; } = string.Empty;
}

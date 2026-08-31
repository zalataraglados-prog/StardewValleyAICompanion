using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyStoryEventRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId is not ("executor.advance_story_event" or "executor.advance_story_event_minigame"))
            return;
        request.StoryEventProjectionFingerprint = ReadQueueParameterString(item, "story_event_projection_fingerprint");
        request.StoryEventId = ReadQueueParameterString(item, "story_event_id");
        request.StoryEventLocationId = ReadQueueParameterString(item, "story_event_location_id");
        request.StoryEventCommandIndex = ReadQueueParameterInt(item, "story_event_command_index");
        request.StoryEventCommandRaw = ReadQueueParameterString(item, "story_event_command_raw");
        request.StoryEventBoundaryKind = ReadQueueParameterString(item, "story_event_boundary_kind");
        request.StoryEventQuestionKey = ReadQueueParameterString(item, "story_event_question_key");
        request.StoryEventResponseIndex = ReadQueueParameterInt(item, "story_event_response_index");
        request.StoryEventResponseKey = ReadQueueParameterString(item, "story_event_response_key");
        request.StoryEventMinigameNativeContract = ReadQueueParameterString(item, "story_event_minigame_native_contract");
        request.StoryEventMinigameType = ReadQueueParameterString(item, "story_event_minigame_type");
        request.StoryEventMinigameId = ReadQueueParameterString(item, "story_event_minigame_id");
        request.StoryEventMinigameOwnerKind = ReadQueueParameterString(item, "story_event_minigame_owner_kind");
        request.StoryEventMinigameExecutionMode = ReadQueueParameterString(item, "story_event_minigame_execution_mode");
    }
}

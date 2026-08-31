using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyStoryEventRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId != "executor.advance_story_event")
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
    }
}

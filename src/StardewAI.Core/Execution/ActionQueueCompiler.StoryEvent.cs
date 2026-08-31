using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private const string CompilerStoryEventNativeContract =
        "live_Event_Update_tryEventCommand_and_DialogueBox_native_input_until_event_end_or_fresh_decision_minigame_or_player_control_boundary_without_skipEvent_or_direct_event_state_mutation";

    private static CompiledActionStep[] CompileAdvanceStoryEventStep(SmallModelAction action, SnapshotEnvelope _) =>
        new[]
        {
            Step(
                "advance_story_event",
                "Event:id=" + ReadParameter(action, "story_event_id") +
                ":command=" + ReadParameter(action, "story_event_command_index") +
                ":response=" + (ReadParameter(action, "story_event_response_key") is { Length: > 0 } response ? response : "none"),
                "native_event_progressed_to_end_or_next_fresh_boundary=true",
                7200)
        };

    private static string[] ValidateAdvanceStoryEventPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.advance_story_event")
            return Array.Empty<string>();
        var projection = ReadStateFieldValue(snapshot, "player", "story_event");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return new[] { "story_event_projection_unavailable" };
        var row = projection.Value;
        var reasons = new List<string>();
        if (ReadString(row, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(row, "native_contract") != CompilerStoryEventNativeContract ||
            ReadParameter(action, "story_event_native_contract") != CompilerStoryEventNativeContract ||
            ReadString(row, "projection_fingerprint") != ReadParameter(action, "story_event_projection_fingerprint") ||
            ReadBool(row, "active") != true || ReadBool(row, "event_up") != true ||
            ReadBool(row, "is_festival") == true || ReadBool(row, "skipped") == true ||
            ReadString(row, "event_id") != ReadParameter(action, "story_event_id") ||
            ReadString(row, "location_id") != ReadParameter(action, "story_event_location_id") ||
            ReadInt(row, "current_command_index", -1) != ReadIntParameter(action, "story_event_command_index") ||
            ReadString(row, "current_command_raw") != ReadParameter(action, "story_event_command_raw") ||
            ReadString(row, "boundary_kind") != ReadParameter(action, "story_event_boundary_kind"))
            reasons.Add("story_event_projection_drifted");
        if (!string.IsNullOrWhiteSpace(ReadString(row, "active_minigame_type")))
            reasons.Add("story_event_minigame_requires_separate_action");
        if (ReadBool(row, "player_control_sequence") == true)
            reasons.Add("story_event_player_control_requires_exact_sequence_handler");

        var boundary = ReadString(row, "boundary_kind");
        var responseKey = ReadParameter(action, "story_event_response_key") ?? string.Empty;
        var responseIndex = ReadIntParameter(action, "story_event_response_index");
        if (boundary == "dialogue_decision")
        {
            if (ReadString(row, "dialogue_question_key") != ReadParameter(action, "story_event_question_key") ||
                !StoryEventResponseMatches(row, responseIndex, responseKey))
                reasons.Add("story_event_dialogue_response_drifted");
        }
        else if (!string.IsNullOrWhiteSpace(responseKey) || responseIndex.HasValue)
        {
            reasons.Add("story_event_response_supplied_without_live_decision");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool StoryEventResponseMatches(JsonElement projection, int? responseIndex, string responseKey) =>
        responseIndex is >= 0 && !string.IsNullOrWhiteSpace(responseKey) &&
        projection.TryGetProperty("dialogue_responses", out var responses) && responses.ValueKind == JsonValueKind.Array &&
        responses.EnumerateArray().Any(response => response.ValueKind == JsonValueKind.Object &&
            ReadInt(response, "index", -1) == responseIndex &&
            ReadString(response, "response_key") == responseKey);
}

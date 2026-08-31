using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private const string StoryEventNativeContract =
        "live_Event_Update_tryEventCommand_and_DialogueBox_native_input_until_event_end_or_fresh_decision_minigame_or_player_control_boundary_without_skipEvent_or_direct_event_state_mutation";

    private EventCandidate[] StoryEventCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] boundParameters)
    {
        var projection = ReadStateFieldValue(snapshot, "player", "story_event");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return Array.Empty<EventCandidate>();
        var row = projection.Value;
        if (ReadString(row, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(row, "native_contract") != StoryEventNativeContract ||
            ReadBool(row, "active") != true || ReadBool(row, "event_up") != true ||
            ReadBool(row, "is_festival") == true || ReadBool(row, "skipped") == true ||
            !string.IsNullOrWhiteSpace(ReadString(row, "active_minigame_type")) ||
            ReadBool(row, "player_control_sequence") == true)
            return Array.Empty<EventCandidate>();

        var eventId = ReadString(row, "event_id");
        var boundEventId = ReadParameter(boundParameters, "continuation.story_event_id");
        if (string.IsNullOrWhiteSpace(eventId) ||
            !string.IsNullOrWhiteSpace(boundEventId) && !string.Equals(boundEventId, eventId, StringComparison.Ordinal))
            return Array.Empty<EventCandidate>();

        var common = StoryEventParameters(row);
        if (ReadString(row, "boundary_kind") != "dialogue_decision")
        {
            return new[]
            {
                StoryEventCandidate(
                    "advance_story_event_automatic",
                    eventId,
                    ReadString(row, "location_id"),
                    "native_event_progressed_to_end_or_next_fresh_boundary=true",
                    common)
            };
        }

        if (!row.TryGetProperty("dialogue_responses", out var responses) || responses.ValueKind != JsonValueKind.Array)
            return Array.Empty<EventCandidate>();
        if (string.IsNullOrWhiteSpace(ReadString(row, "dialogue_question_key")))
            return Array.Empty<EventCandidate>();
        var boundResponse = ReadParameter(boundParameters, "continuation.story_event_response_key");
        return responses.EnumerateArray()
            .Where(response => response.ValueKind == JsonValueKind.Object)
            .Where(response => ReadInt(response, "index", -1) >= 0)
            .Where(response => !string.IsNullOrWhiteSpace(ReadString(response, "response_key")))
            .Where(response => string.IsNullOrWhiteSpace(boundResponse) ||
                string.Equals(ReadString(response, "response_key"), boundResponse, StringComparison.Ordinal))
            .Select(response =>
            {
                var responseIndex = ReadInt(response, "index", -1);
                var responseKey = ReadString(response, "response_key");
                var parameters = common.Concat(new[]
                {
                    Parameter("story_event_question_key", ReadString(row, "dialogue_question_key")),
                    Parameter("story_event_response_index", responseIndex.ToString(CultureInfo.InvariantCulture)),
                    Parameter("story_event_response_key", responseKey),
                    Parameter("story_event_response_text", ReadString(response, "response_text")),
                    Parameter("continuation.story_event_response_key", responseKey)
                }).ToArray();
                return StoryEventCandidate(
                    "advance_story_event_choice",
                    eventId,
                    ReadString(row, "location_id"),
                    "native_event_dialogue_response_selected=true;event_progressed_to_end_or_next_fresh_boundary=true",
                    parameters,
                    responseIndex);
            })
            .OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
    }

    private static EventCandidate StoryEventCandidate(
        string kind,
        string eventId,
        string locationId,
        string expectedEffect,
        SmallModelActionParameter[] parameters,
        int responseIndex = -1)
    {
        return new EventCandidate
        {
            CandidateId = "story-event:" + eventId + ":" + kind + ":" +
                (responseIndex < 0 ? "continue" : responseIndex.ToString(CultureInfo.InvariantCulture)),
            Kind = kind,
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            LocationId = locationId,
            EstimatedTicks = 60,
            EnergyCost = 0,
            AvailabilityClass = "active_native_story_event_boundary",
            ExpectedEffect = expectedEffect,
            Parameters = parameters
        };
    }

    private static SmallModelActionParameter[] StoryEventParameters(JsonElement row) =>
        new[]
        {
            Parameter("continuation.option_id", "story.advance_event"),
            Parameter("continuation.story_event_id", ReadString(row, "event_id")),
            Parameter("story_event_projection_fingerprint", ReadString(row, "projection_fingerprint")),
            Parameter("story_event_id", ReadString(row, "event_id")),
            Parameter("story_event_location_id", ReadString(row, "location_id")),
            Parameter("story_event_command_index", ReadInt(row, "current_command_index", -1).ToString(CultureInfo.InvariantCulture)),
            Parameter("story_event_command_raw", ReadString(row, "current_command_raw")),
            Parameter("story_event_boundary_kind", ReadString(row, "boundary_kind")),
            Parameter("story_event_native_contract", ReadString(row, "native_contract")),
            Parameter("story_event_max_runtime_ticks", "7200")
        };
}

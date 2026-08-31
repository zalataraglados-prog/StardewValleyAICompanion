using System;
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
    private const string StoryEventMinigameNativeContract =
        "live_Game1_currentMinigame_native_tick_and_event_Update_with_exact_DialogueBox_input_until_minigame_end_or_fresh_decision_without_forceQuit_manual_tick_or_direct_event_minigame_state_mutation";

    private EventCandidate[] StoryEventMinigameCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] boundParameters)
    {
        var projection = ReadStateFieldValue(snapshot, "player", "story_event");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return Array.Empty<EventCandidate>();
        var row = projection.Value;
        var type = ReadString(row, "active_minigame_type");
        var owner = ReadString(row, "active_minigame_owner_kind");
        if (ReadString(row, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadBool(row, "active_minigame_supported") != true ||
            ReadString(row, "active_minigame_support_status") != "supported" ||
            ReadString(row, "active_minigame_native_contract") != StoryEventMinigameNativeContract ||
            string.IsNullOrWhiteSpace(type) ||
            owner is not ("event_script" or "world_cinematic") ||
            owner == "event_script" && (ReadBool(row, "active") != true || ReadBool(row, "event_up") != true ||
                ReadBool(row, "is_festival") == true))
            return Array.Empty<EventCandidate>();

        var boundType = ReadParameter(boundParameters, "continuation.story_event_minigame_type");
        if (!string.IsNullOrWhiteSpace(boundType) && !string.Equals(boundType, type, StringComparison.Ordinal))
            return Array.Empty<EventCandidate>();

        var common = StoryEventMinigameParameters(row);
        if (ReadBool(row, "active_minigame_requires_model_response") != true)
        {
            return new[]
            {
                StoryEventMinigameCandidate(
                    "advance_story_event_minigame_passive",
                    row,
                    "native_minigame_completed_or_reached_fresh_dialogue_boundary=true",
                    common)
            };
        }

        if (!row.TryGetProperty("dialogue_responses", out var responses) || responses.ValueKind != JsonValueKind.Array ||
            string.IsNullOrWhiteSpace(ReadString(row, "dialogue_question_key")))
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
                var index = ReadInt(response, "index", -1);
                var key = ReadString(response, "response_key");
                return StoryEventMinigameCandidate(
                    "advance_story_event_minigame_choice",
                    row,
                    "native_minigame_dialogue_response_selected=true;minigame_completed_or_reached_fresh_boundary=true",
                    common.Concat(new[]
                    {
                        Parameter("story_event_question_key", ReadString(row, "dialogue_question_key")),
                        Parameter("story_event_response_index", index.ToString(CultureInfo.InvariantCulture)),
                        Parameter("story_event_response_key", key),
                        Parameter("story_event_response_text", ReadString(response, "response_text")),
                        Parameter("continuation.story_event_response_key", key)
                    }).ToArray(),
                    index);
            })
            .OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
    }

    private static EventCandidate StoryEventMinigameCandidate(
        string kind,
        JsonElement row,
        string expectedEffect,
        SmallModelActionParameter[] parameters,
        int responseIndex = -1)
    {
        var type = ReadString(row, "active_minigame_type");
        return new EventCandidate
        {
            CandidateId = "story-event-minigame:" + type + ":" + kind + ":" +
                (responseIndex < 0 ? "continue" : responseIndex.ToString(CultureInfo.InvariantCulture)),
            Kind = kind,
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            LocationId = ReadString(row, "location_id"),
            EstimatedTicks = 3600,
            EnergyCost = 0,
            AvailabilityClass = "active_native_story_minigame_boundary",
            ExpectedEffect = expectedEffect,
            Parameters = parameters
        };
    }

    private static SmallModelActionParameter[] StoryEventMinigameParameters(JsonElement row) =>
        new[]
        {
            Parameter("continuation.option_id", "story.advance_event_minigame"),
            Parameter("continuation.story_event_id", ReadString(row, "event_id")),
            Parameter("continuation.story_event_minigame_type", ReadString(row, "active_minigame_type")),
            Parameter("story_event_projection_fingerprint", ReadString(row, "projection_fingerprint")),
            Parameter("story_event_id", ReadString(row, "event_id")),
            Parameter("story_event_location_id", ReadString(row, "location_id")),
            Parameter("story_event_command_index", ReadInt(row, "current_command_index", -1).ToString(CultureInfo.InvariantCulture)),
            Parameter("story_event_command_raw", ReadString(row, "current_command_raw")),
            Parameter("story_event_boundary_kind", ReadString(row, "boundary_kind")),
            Parameter("story_event_minigame_native_contract", ReadString(row, "active_minigame_native_contract")),
            Parameter("story_event_minigame_type", ReadString(row, "active_minigame_type")),
            Parameter("story_event_minigame_id", ReadString(row, "active_minigame_id")),
            Parameter("story_event_minigame_owner_kind", ReadString(row, "active_minigame_owner_kind")),
            Parameter("story_event_minigame_execution_mode", ReadString(row, "active_minigame_execution_mode")),
            Parameter("story_event_max_runtime_ticks", "10800")
        };
}

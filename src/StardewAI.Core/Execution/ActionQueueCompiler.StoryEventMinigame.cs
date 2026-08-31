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
    private const string CompilerStoryEventMinigameNativeContract =
        "live_Game1_currentMinigame_native_tick_and_event_Update_with_exact_DialogueBox_input_until_minigame_end_or_fresh_decision_without_forceQuit_manual_tick_or_direct_event_minigame_state_mutation";

    private static CompiledActionStep[] CompileAdvanceStoryEventMinigameStep(
        SmallModelAction action,
        SnapshotEnvelope _) =>
        new[]
        {
            Step(
                "advance_story_event_minigame",
                "IMinigame:type=" + ReadParameter(action, "story_event_minigame_type") +
                ":event=" + (ReadParameter(action, "story_event_id") is { Length: > 0 } id ? id : "none") +
                ":response=" + (ReadParameter(action, "story_event_response_key") is { Length: > 0 } response ? response : "none"),
                "native_minigame_completed_or_reached_fresh_boundary=true",
                10800)
        };

    private static string[] ValidateAdvanceStoryEventMinigamePlan(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.advance_story_event_minigame")
            return Array.Empty<string>();
        var projection = ReadStateFieldValue(snapshot, "player", "story_event");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return new[] { "story_event_minigame_projection_unavailable" };
        var row = projection.Value;
        var reasons = new List<string>();
        var owner = ReadString(row, "active_minigame_owner_kind");
        if (ReadString(row, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(row, "projection_fingerprint") != ReadParameter(action, "story_event_projection_fingerprint") ||
            ReadBool(row, "active_minigame_supported") != true ||
            ReadString(row, "active_minigame_support_status") != "supported" ||
            ReadString(row, "active_minigame_native_contract") != CompilerStoryEventMinigameNativeContract ||
            ReadParameter(action, "story_event_minigame_native_contract") != CompilerStoryEventMinigameNativeContract ||
            ReadString(row, "active_minigame_type") != ReadParameter(action, "story_event_minigame_type") ||
            ReadString(row, "active_minigame_id") != ReadParameter(action, "story_event_minigame_id") ||
            owner != ReadParameter(action, "story_event_minigame_owner_kind") ||
            ReadString(row, "active_minigame_execution_mode") != ReadParameter(action, "story_event_minigame_execution_mode") ||
            ReadString(row, "location_id") != ReadParameter(action, "story_event_location_id") ||
            ReadString(row, "boundary_kind") != ReadParameter(action, "story_event_boundary_kind"))
            reasons.Add("story_event_minigame_projection_drifted");
        if (owner == "event_script" &&
            (ReadBool(row, "active") != true || ReadBool(row, "event_up") != true ||
             ReadBool(row, "is_festival") == true ||
             ReadString(row, "event_id") != ReadParameter(action, "story_event_id") ||
             ReadInt(row, "current_command_index", -1) != ReadIntParameter(action, "story_event_command_index") ||
             ReadString(row, "current_command_raw") != ReadParameter(action, "story_event_command_raw")))
            reasons.Add("story_event_minigame_event_boundary_drifted");
        if (owner is not ("event_script" or "world_cinematic"))
            reasons.Add("story_event_minigame_owner_unsupported");

        var responseKey = ReadParameter(action, "story_event_response_key") ?? string.Empty;
        var responseIndex = ReadIntParameter(action, "story_event_response_index");
        if (ReadBool(row, "active_minigame_requires_model_response") == true)
        {
            if (ReadString(row, "dialogue_question_key") != ReadParameter(action, "story_event_question_key") ||
                !StoryEventResponseMatches(row, responseIndex, responseKey))
                reasons.Add("story_event_minigame_dialogue_response_drifted");
        }
        else if (!string.IsNullOrWhiteSpace(responseKey) || responseIndex.HasValue)
        {
            reasons.Add("story_event_minigame_response_supplied_without_live_decision");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }
}

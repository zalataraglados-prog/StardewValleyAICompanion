using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> PlayerEmoteSteps(PolicyEventCandidatePrediction candidate)
    {
        if (string.IsNullOrWhiteSpace(CandidateParameter(candidate, "emote_key")) ||
            string.IsNullOrWhiteSpace(CandidateParameter(candidate, "emote_reason")) ||
            CandidateParameter(candidate, "confirm_emote") != "true")
            return Array.Empty<SmallModelPlanStep>();
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "perform_emote", 0),
                Kind = "perform_emote",
                TargetLocation = candidate.LocationId,
                EstimatedMinutes = 1,
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "explicit_player_command_reason_and_confirmation_still_authorized=true",
                    "native_CanEmote_and_chat_input_service_ready=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "player_command_only_and_excluded_from_autonomous_candidates_and_strategy_training",
                    "compiler_rebinds_exact_live_22_entry_Farmer_EMOTES_catalog",
                    "native_ChatBox_character_input_and_textBoxEnter_emote_command_only",
                    "no_direct_netDoEmote_performPlayerEmote_doEmote_or_performedEmotes_mutation",
                    "do_not_overlap_an_active_icon_or_animation_emote"
                },
                FailurePolicy = new[] { "clear_only_owned_chat_input_refresh_snapshot_and_require_fresh_player_command" },
                Parameters = candidate.Parameters
            }
        };
    }
}

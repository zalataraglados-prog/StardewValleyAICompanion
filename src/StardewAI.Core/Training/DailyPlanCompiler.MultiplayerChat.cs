using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> MultiplayerChatSteps(PolicyEventCandidatePrediction candidate)
    {
        var scope = CandidateParameter(candidate, "chat_scope");
        if (scope is not ("global" or "private") ||
            string.IsNullOrWhiteSpace(CandidateParameter(candidate, "chat_reason")) ||
            string.IsNullOrWhiteSpace(CandidateParameter(candidate, "chat_message_text")) ||
            CandidateParameter(candidate, "confirm_chat") != "true")
            return Array.Empty<SmallModelPlanStep>();
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "send_multiplayer_chat", 0),
                Kind = "send_multiplayer_chat",
                TargetLocation = candidate.LocationId,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "explicit_player_chat_command_and_exact_text_still_authorized=true",
                    "complete_fresh_multiplayer_chat_projection_matches=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "player_command_only_and_explicit_confirmation_required",
                    "leading_slash_reply_admin_cheat_and_emote_commands_forbidden",
                    "private_recipient_requires_exact_player_id_and_payload_dependent_native_first_match",
                    "native_ChatTextBox_character_input_width_gate_and_content_filters_remain_game_owned",
                    "native_ChatBox_textBoxEnter_then_Multiplayer_type10_dispatch_only",
                    "no_direct_sendChatMessage_receiveChatMessage_or_remote_delivery_fabrication"
                },
                FailurePolicy = new[] { "clear_native_chat_input_refresh_snapshot_and_require_fresh_player_command" },
                Parameters = candidate.Parameters
            }
        };
    }
}

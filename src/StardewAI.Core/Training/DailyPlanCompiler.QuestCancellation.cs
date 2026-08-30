using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> QuestCancellationSteps(PolicyEventCandidatePrediction candidate) =>
        new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "cancel_quest", 0),
                Kind = "cancel_quest",
                EstimatedMinutes = 0,
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "explicit_player_confirmation=true",
                    "exact_cancellable_ordinary_quest_present=true",
                    "menus.active_menu.is_open=false"
                },
                ExpectedEffects = new[] { "exact_ordinary_quest_removed=true", "quest.accepted=false" },
                SafetyConstraints = new[]
                {
                    "player_command_only",
                    "native_QuestLog_row_and_cancel_button_clicks_only",
                    "never_cancel_special_order_completed_hidden_or_non_cancellable_quest",
                    "do_not_write_quest_or_acceptedDailyQuest_state_directly"
                },
                FailurePolicy = new[] { "close_owned_quest_log", "refresh_snapshot_and_require_new_confirmation" },
                Parameters = candidate.Parameters
            }
        };
}

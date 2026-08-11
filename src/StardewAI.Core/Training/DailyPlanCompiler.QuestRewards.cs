using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> QuestRewardClaimSteps(
        PolicyEventCandidatePrediction candidate)
    {
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "claim_quest_reward", 0),
                Kind = "claim_quest_reward",
                EstimatedMinutes = 0,
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "menus.active_menu.is_open=false",
                    "exact_claimable_quest_money_reward_present=true"
                },
                ExpectedEffects = new[]
                {
                    CandidateParameter(candidate, "quest_reward_fingerprint") + ":claimed",
                    "player.money=" + CandidateParameter(candidate, "expected_money_before") + "+" +
                        CandidateParameter(candidate, "quest_money_reward_expected")
                },
                SafetyConstraints = new[]
                {
                    "native_QuestLog_receiveLeftClick_only",
                    "exact_quest_identity_must_rebind",
                    "do_not_write_money_or_quest_fields_directly"
                },
                FailurePolicy = new[] { "close_native_menu_if_owned", "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}

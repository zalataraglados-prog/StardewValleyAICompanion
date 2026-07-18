using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> JojaDevelopmentSteps(PolicyEventCandidatePrediction candidate)
    {
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, candidate.Kind, 0),
                Kind = candidate.Kind,
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "joja_development_projection_still_matches=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_JojaMart_JoinJoja_action_only",
                    "native_dialogue_and_JojaCDMenu_callbacks_only",
                    "purchase_exactly_one_verified_membership_or_project",
                    "no_direct_money_mail_event_quest_or_world_mutation"
                },
                FailurePolicy = new[] { "close_joja_menu_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}

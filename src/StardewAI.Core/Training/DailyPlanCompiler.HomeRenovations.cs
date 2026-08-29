using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> HomeRenovationSteps(PolicyEventCandidatePrediction candidate)
    {
        if (string.IsNullOrWhiteSpace(CandidateParameter(candidate, "renovation_id")) ||
            string.IsNullOrWhiteSpace(CandidateParameter(candidate, "renovation_reason")) ||
            CandidateParameter(candidate, "confirm_renovation") != "true")
            return Array.Empty<SmallModelPlanStep>();
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "renovate_home", 0),
                Kind = "renovate_home",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "complete_live_home_renovation_projection_still_matches=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "explicit_player_command_and_operation_confirmation_only",
                    "destructive_branch_requires_destructive_confirmation",
                    "native_Carpenter_Renovate_HouseRenovations_RenovateMenu_only",
                    "fresh_menu_order_region_obstruction_money_and_action_rebind",
                    "no_direct_money_mail_NetInt_map_furniture_menu_viewport_or_event_mutation"
                },
                FailurePolicy = new[] { "return_from_native_renovation_view_close_menu_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}

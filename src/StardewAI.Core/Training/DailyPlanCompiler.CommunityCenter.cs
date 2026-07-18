using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> CommunityCenterDonationSteps(PolicyEventCandidatePrediction candidate)
    {
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "donate_community_center_item", 0),
                Kind = "donate_community_center_item",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "community_center_bundle_projection_still_matches=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "route_state_must_allow_community_center",
                    "native_CommunityCenter_checkBundle_only",
                    "native_JunimoNoteMenu_receiveLeftClick_only",
                    "donate_exactly_one_verified_bundle_ingredient",
                    "no_direct_bundle_inventory_reward_mail_or_route_mutation"
                },
                FailurePolicy = new[] { "close_junimo_note_menu_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}

using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> MuseumDonationSteps(PolicyEventCandidatePrediction candidate)
    {
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "donate_museum_item", 0),
                Kind = "donate_museum_item",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "museum_donation_projection_still_matches=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_LibraryMuseum_OpenDonationMenu_only",
                    "native_MuseumMenu_receiveLeftClick_only",
                    "donate_exactly_one_verified_inventory_item",
                    "native_menu_close_settles_rewards",
                    "no_direct_museum_inventory_achievement_mail_or_event_mutation"
                },
                FailurePolicy = new[] { "close_museum_menu_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}

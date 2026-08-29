using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> FieldOfficeDonationSteps(PolicyEventCandidatePrediction candidate) =>
        new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "donate_field_office_piece", 0),
                Kind = "donate_field_office_piece",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "field_office_donation_projection_still_matches=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_FieldOfficeDesk_mutex_only",
                    "native_Safari_Donate_response_only",
                    "native_FieldOfficeMenu_inventory_and_exact_holder_click_only",
                    "donate_exactly_one_verified_fossil",
                    "no_direct_piece_reward_nut_mail_or_finale_mutation"
                },
                FailurePolicy = new[] { "close_field_office_menu_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
}

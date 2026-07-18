using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> PetCareSteps(PolicyEventCandidatePrediction candidate)
    {
        if (candidate.Kind == "pet_daily_interaction")
        {
            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "pet_safe_slot", 0),
                    Kind = "select_safe_item_slot",
                    EstimatedMinutes = 1,
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "safe_toolbar_slot_available=true" },
                    ExpectedEffects = new[] { "player.current_item_is_not_hat_or_butterfly_powder=true" },
                    SafetyConstraints = new[] { "do_not_consume_or_drop_selected_item" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = new[] { Parameter("safe_slot_index", CandidateParameter(candidate, "safe_slot_index")) }
                },
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "pet_daily_interaction", 1),
                    Kind = "pet_interact",
                    TargetLocation = candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "pet_projection_still_matches=true" },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[] { "native_Pet.checkAction_only", "exact_pet_guid", "dynamic_pet_tile_replan", "no_direct_pet_friendship_mail_or_gift_mutation" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = candidate.Parameters
                }
            };
        }

        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "fill_pet_bowl", 0),
                Kind = "fill_pet_bowl",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "pet_bowl_projection_still_matches=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[] { "native_WateringCan_lifecycle_only", "verify_immediate_watered_state", "keep_friendship_and_mail_as_delayed_settlement" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}

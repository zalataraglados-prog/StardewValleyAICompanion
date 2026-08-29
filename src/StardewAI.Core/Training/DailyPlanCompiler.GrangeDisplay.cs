using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> GrangeDisplaySteps(PolicyEventCandidatePrediction candidate)
    {
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "manage_grange_display", 0),
                Kind = "manage_grange_display",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "grange_projection_still_matches=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "one_fresh_snapshot_display_mutation_only",
                    "native_Event_checkAction_and_StorageContainer_clicks_only",
                    "shared_grange_mutex_must_be_acquired_and_released",
                    "never_start_grange_judging",
                    "no_direct_team_display_inventory_score_or_event_mutation"
                },
                FailurePolicy = new[] { "close_grange_menu_release_mutex_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}

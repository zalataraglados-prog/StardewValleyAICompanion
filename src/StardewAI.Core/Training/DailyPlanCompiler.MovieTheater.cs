using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> MovieTheaterSteps(PolicyEventCandidatePrediction candidate)
    {
        if (candidate.Kind == "watch_movie_wait_guest")
        {
            var waitTicks = Math.Clamp(CandidateInt(candidate, "retry_wait_ticks") ?? 120, 1, MaxWaitTicksPerStep);
            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "watch_movie_wait_guest", 0),
                    Kind = "wait_ticks",
                    WaitTicks = waitTicks,
                    EstimatedMinutes = Math.Max(1, waitTicks / 60),
                    Preconditions = new[] { "same_movie_objective_active=true", "invited_guest_fulfillment_pending=true" },
                    ExpectedEffects = new[] { "native_invited_guest_spawn_polled=true", "fresh_snapshot_replan_required=true" },
                    SafetyConstraints = new[] { "native_movie_invitation_only", "do_not_skip_or_mutate_movie_state" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = candidate.Parameters
                }
            };
        }

        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, candidate.Kind, 0),
                Kind = "watch_movie",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "movie_objective_fresh_projection_still_matches=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_movie_ticket_invitation_entrance_concession_and_screening_paths_only",
                    "never_skip_MovieTheaterScreening_event",
                    "no_direct_inventory_money_friendship_movie_week_invitation_or_mutex_mutation"
                },
                FailurePolicy = new[] { "release_native_input_and_mutex_if_held_then_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}

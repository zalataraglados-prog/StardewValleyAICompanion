using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> GeodeProcessingSteps(PolicyEventCandidatePrediction candidate) => new[]
    {
        new SmallModelPlanStep
        {
            StepId = StepId(candidate, "crack_geode", 0), Kind = "crack_geode",
            TargetLocation = candidate.LocationId, TargetTileX = candidate.TileX, TargetTileY = candidate.TileY,
            EstimatedMinutes = Math.Max(1, TicksToMinutes(candidate.EstimatedTicks)),
            Preconditions = new[] { "candidate_id:" + candidate.CandidateId,
                "one_locked_base_geode_and_native_blacksmith_service_remain_ready=true",
                "fresh_counter_seed_inventory_money_and_output_projection_required=true" },
            ExpectedEffects = new[] { candidate.ExpectedEffect },
            SafetyConstraints = new[] { "one_geode_per_native_action", "shared_route_and_fresh_compile_rebind",
                "complete_output_family_only_where_locked_native_code_consumes_shared_rng",
                "no_direct_money_inventory_stats_mail_or_team_state_mutation" },
            FailurePolicy = new[] { "stop_native_input_refresh_snapshot_and_replan" },
            Parameters = candidate.Parameters
        }
    };
}

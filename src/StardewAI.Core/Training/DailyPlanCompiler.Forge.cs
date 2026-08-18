using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> ForgeSteps(PolicyEventCandidatePrediction candidate)
    {
        var names = new[]
        {
            "forge_candidate_id", "forge_operation", "forge_reason", "forge_source_id", "forge_source_kind",
            "location_id", "interaction_tile_x", "interaction_tile_y", "stand_tile_x", "stand_tile_y",
            "left_source_id", "left_state_json", "right_source_id", "right_state_json", "forge_shard_cost",
            "forge_shard_refund", "forge_shard_count_before", "times_enchanted_before", "times_enchanted_after",
            "forge_output_contract_kind", "expected_output_state_json", "random_outcome_contract_json", "max_movement_tiles"
        };
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "forge_item", 0), Kind = "forge_item", TargetLocation = candidate.LocationId,
                EstimatedMinutes = Math.Max(1, TicksToMinutes(candidate.EstimatedTicks)),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "exact_live_forge_inputs_and_source_rebound=true", "menus.active_menu.is_open=false" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_ForgeMenu_clicks_only", "one_forge_or_unforge_then_fresh_snapshot",
                    "no_direct_inventory_enchantment_ring_stat_achievement_or_money_mutation",
                    "runtime_verify_exact_inputs_shards_stats_output_and_random_result_domain"
                },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = names.Select(name => Parameter(name, CandidateParameter(candidate, name))).ToArray()
            }
        };
    }
}

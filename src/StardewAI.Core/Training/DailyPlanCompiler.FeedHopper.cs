using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> FeedHopperWithdrawalSteps(PolicyEventCandidatePrediction candidate) =>
        new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "withdraw_feed_hopper_hay", 0),
                Kind = "withdraw_feed_hopper_hay",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "exact_base_feed_hopper_still_present=true",
                    "unfed_animal_count>0",
                    "silo_hay_and_trough_capacity_positive=true",
                    "inventory_accepts_exact_native_withdrawal=true",
                    "destructive_object_trap_preamble=false"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "candidate_selects_one_exact_feed_hopper",
                    "compiler_rebinds_silo_animals_trough_inventory_safe_slot_and_adjacent_stand_from_fresh_snapshot",
                    "one_native_GameLocation_checkAction_only",
                    "never_directly_mutate_silo_hay_or_player_inventory_in_production",
                    "refresh_snapshot_before_any_hay_placement_followup"
                },
                FailurePolicy = new[] { "stop_restore_selected_slot_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
}

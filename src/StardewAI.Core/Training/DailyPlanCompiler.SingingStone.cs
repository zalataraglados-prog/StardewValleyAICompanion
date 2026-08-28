using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> SingingStoneSteps(PolicyEventCandidatePrediction candidate) =>
        new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "play_singing_stone", 0),
                Kind = "play_singing_stone",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "exact_base_singing_stone_still_present=true",
                    "safe_toolbar_slot_available=true",
                    "player_explicitly_requested_sound=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "candidate_selects_one_exact_singing_stone",
                    "compiler_rebinds_identity_safe_slot_and_adjacent_stand_from_fresh_snapshot",
                    "one_native_GameLocation_checkAction_only",
                    "shared_rng_pitch_is_distribution_only_and_must_not_be_guessed",
                    "not_enabled_for_autonomous_daily_planning_or_policy_training"
                },
                FailurePolicy = new[] { "stop_restore_selected_slot_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
}

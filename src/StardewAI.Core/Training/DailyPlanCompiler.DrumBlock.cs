using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> DrumBlockSteps(PolicyEventCandidatePrediction candidate) => new[]
    {
        new SmallModelPlanStep
        {
            StepId = StepId(candidate, "tune_drum_block", 0), Kind = "tune_drum_block", TargetLocation = candidate.LocationId,
            TargetTileX = candidate.TileX, TargetTileY = candidate.TileY, EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
            Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "exact_base_drum_block_still_present=true", "safe_toolbar_slot_available=true", "player_explicitly_requested_tuning=true" },
            ExpectedEffects = new[] { candidate.ExpectedEffect },
            SafetyConstraints = new[] { "compiler_rebinds_tone_identity_safe_slot_and_stand", "one_native_GameLocation_checkAction_only", "adjacent_playback_is_not_tuning", "not_enabled_for_autonomous_daily_planning_or_policy_training" },
            FailurePolicy = new[] { "stop_restore_selected_slot_refresh_snapshot_and_replan" }, Parameters = candidate.Parameters
        }
    };
}

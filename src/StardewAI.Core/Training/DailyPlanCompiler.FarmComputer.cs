using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> FarmComputerReportSteps(PolicyEventCandidatePrediction candidate) =>
        new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "read_farm_computer_report", 0),
                Kind = "read_farm_computer_report",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "exact_base_farm_computer_still_present=true",
                    "safe_toolbar_slot_available=true",
                    "player_explicitly_requested_report=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "compiler_rebinds_exact_root_aggregate_digest_target_and_stand_from_fresh_snapshot",
                    "one_native_GameLocation_checkAction_only",
                    "native_delayed_DialogueBox_must_match_transparent_report_digest",
                    "structured_report_is_read_directly_and_never_requires_autonomous_menu_use",
                    "not_enabled_for_autonomous_daily_planning_or_policy_training"
                },
                FailurePolicy = new[] { "stop_restore_selected_slot_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
}

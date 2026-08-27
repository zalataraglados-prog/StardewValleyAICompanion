using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> HousePlantRotationSteps(PolicyEventCandidatePrediction candidate) =>
        new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "rotate_house_plant", 0),
                Kind = "rotate_house_plant",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "exact_base_house_plant_still_present=true",
                    "empty_toolbar_slot_available=true",
                    "player_authorized_decoration_change=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "candidate_selects_one_exact_house_plant",
                    "compiler_rebinds_visual_frame_empty_slot_and_adjacent_stand_from_fresh_snapshot",
                    "one_native_GameLocation_checkAction_only",
                    "never_directly_mutate_parent_sheet_index_in_production",
                    "not_enabled_for_autonomous_daily_planning"
                },
                FailurePolicy = new[] { "stop_restore_selected_slot_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
}

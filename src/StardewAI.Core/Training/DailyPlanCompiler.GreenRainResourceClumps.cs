using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> ClearGreenRainResourceClumpSteps(PolicyEventCandidatePrediction candidate)
    {
        var stand = ParseCoordinate(candidate.ExpectedEffect, "resource_clump_stand_tile=");
        var hit = ParseCoordinate(candidate.ExpectedEffect, "resource_clump_hit_tile=");
        var anchor = ParseCoordinate(candidate.ExpectedEffect, "resource_clump_tile=");
        if (!stand.HasValue || !hit.HasValue || !anchor.HasValue)
        {
            return Array.Empty<SmallModelPlanStep>();
        }

        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "break_current_location_resource_clump", 0),
                Kind = "break_current_location_resource_clump",
                TargetLocation = candidate.LocationId,
                TargetTileX = hit.Value.X,
                TargetTileY = hit.Value.Y,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "exact_loaded_green_rain_resource_clump_still_matches=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[] { "native_cross_frame_axe_lifecycle", "transparent_perimeter_stand_tile", "no_direct_clump_drop_or_experience_mutation" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = new[]
                {
                    Parameter("stand_tile_x", stand.Value.X.ToString()),
                    Parameter("stand_tile_y", stand.Value.Y.ToString()),
                    Parameter("resource_clump_tile_x", anchor.Value.X.ToString()),
                    Parameter("resource_clump_tile_y", anchor.Value.Y.ToString()),
                    Parameter("resource_clump_width", ParseValue(candidate.ExpectedEffect, "resource_clump_width=")),
                    Parameter("resource_clump_height", ParseValue(candidate.ExpectedEffect, "resource_clump_height=")),
                    Parameter("resource_clump_parent_sheet_index", ParseValue(candidate.ExpectedEffect, "resource_clump_parent_sheet_index=")),
                    Parameter("target_runtime_type", CandidateParameter(candidate, "target_runtime_type")),
                    Parameter("tool_slot_index", ParseValue(candidate.ExpectedEffect, "tool_slot_index=")),
                    Parameter("required_tool_kind", "axe"),
                    Parameter("max_tool_swings", ParseValue(candidate.ExpectedEffect, "max_tool_swings=")),
                    Parameter("max_movement_tiles", "512"),
                    Parameter("expected_output_items_json", CandidateParameter(candidate, "expected_output_items_json")),
                    Parameter("expected_foraging_experience_delta", "15"),
                    Parameter("output_distribution_status", CandidateParameter(candidate, "output_distribution_status")),
                    Parameter("possible_secret_note_qualified_item_id", CandidateParameter(candidate, "possible_secret_note_qualified_item_id")),
                    Parameter("unseen_secret_note_count", CandidateParameter(candidate, "unseen_secret_note_count")),
                    Parameter("total_secret_note_count", CandidateParameter(candidate, "total_secret_note_count")),
                    Parameter("secret_note_outer_roll_probability", CandidateParameter(candidate, "secret_note_outer_roll_probability")),
                    Parameter("secret_note_inner_roll_probability", CandidateParameter(candidate, "secret_note_inner_roll_probability")),
                    Parameter("secret_note_combined_probability", CandidateParameter(candidate, "secret_note_combined_probability")),
                    Parameter("secret_note_projection_status", CandidateParameter(candidate, "secret_note_projection_status")),
                    Parameter("native_contract", CandidateParameter(candidate, "native_contract")),
                    Parameter("skill_experience_skill_id", "foraging"),
                    Parameter("skill_experience_on_success_min", "15"),
                    Parameter("skill_experience_on_success_max", "15"),
                    Parameter("skill_experience_condition", "native_axe_destroys_exact_green_rain_resource_clump"),
                    Parameter("skill_experience_projection_status", "exact")
                }
            }
        };
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        private static IEnumerable<SmallModelPlanStep> HarvestGingerSteps(PolicyEventCandidatePrediction candidate)
        {
            var stand = ParseCoordinate(candidate.ExpectedEffect, "ginger_stand_tile=");
            if (!candidate.TileX.HasValue || !candidate.TileY.HasValue || !stand.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "harvest_ginger", 0),
                    Kind = "harvest_ginger",
                    TargetLocation = candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "target_is_exact_ginger_forage_crop=true" },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[] { "native_hoe_lifecycle_only", "transparent_adjacent_stand_tile", "no_direct_crop_debris_or_skill_mutation" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = new[]
                    {
                        Parameter("stand_tile_x", stand.Value.X.ToString()),
                        Parameter("stand_tile_y", stand.Value.Y.ToString()),
                        Parameter("required_tool_kind", "Hoe"),
                        Parameter("tool_slot_index", candidate.SlotIndex?.ToString() ?? ParseValue(candidate.ExpectedEffect, "tool_slot_index=")),
                        Parameter("qualified_item_id", "(O)829"),
                        Parameter("quantity", "1"),
                        Parameter("expected_output_quality", "0"),
                        Parameter("expected_energy_cost", ParseValue(candidate.ExpectedEffect, "expected_energy_cost=")),
                        Parameter("expected_foraging_experience_delta", "7"),
                        Parameter("expected_hoe_dirt_state_after", ParseValue(candidate.ExpectedEffect, "expected_hoe_dirt_state_after=")),
                        Parameter("ginger_projection_status", ParseValue(candidate.ExpectedEffect, "ginger_projection_status=")),
                        Parameter("max_movement_tiles", ParseValue(candidate.ExpectedEffect, "max_movement_tiles="))
                    }.Concat(candidate.Parameters.Where(parameter =>
                        parameter.Name.StartsWith("quest_", StringComparison.Ordinal))).ToArray()
                }
            };
        }
    }
}

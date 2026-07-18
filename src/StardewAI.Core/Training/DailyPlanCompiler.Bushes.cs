using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        private static IEnumerable<SmallModelPlanStep> HarvestBushSteps(PolicyEventCandidatePrediction candidate)
        {
            var stand = ParseCoordinate(candidate.ExpectedEffect, "bush_stand_tile=");
            var interaction = ParseCoordinate(candidate.ExpectedEffect, "bush_interaction_tile=");
            if (!candidate.TileX.HasValue || !candidate.TileY.HasValue || !stand.HasValue || !interaction.HasValue ||
                string.IsNullOrWhiteSpace(candidate.QualifiedItemId))
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "harvest_bush", 0),
                    Kind = "harvest_bush",
                    TargetLocation = candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "exact_bush_projection_still_ready=true" },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[] { "native_checkAction_and_Bush.performUseAction_only", "transparent_perimeter_interaction", "no_direct_bush_debris_inventory_nut_or_skill_mutation" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = new[]
                    {
                        Parameter("interaction_tile_x", interaction.Value.X.ToString()),
                        Parameter("interaction_tile_y", interaction.Value.Y.ToString()),
                        Parameter("stand_tile_x", stand.Value.X.ToString()),
                        Parameter("stand_tile_y", stand.Value.Y.ToString()),
                        Parameter("target_runtime_type", "StardewValley.TerrainFeatures.Bush"),
                        Parameter("bush_kind", ParseValue(candidate.ExpectedEffect, "bush_kind=")),
                        Parameter("qualified_item_id", candidate.QualifiedItemId),
                        Parameter("quantity", Math.Max(1, candidate.Quantity).ToString()),
                        Parameter("expected_output_quality", ParseValue(candidate.ExpectedEffect, "expected_output_quality=")),
                        Parameter("expected_foraging_experience_delta", ParseValue(candidate.ExpectedEffect, "expected_foraging_experience_delta=")),
                        Parameter("expected_tile_sheet_offset_after", "0"),
                        Parameter("bush_nut_key", ParseValue(candidate.ExpectedEffect, "bush_nut_key=")),
                        Parameter("bush_nut_collected_expected_after", ParseValue(candidate.ExpectedEffect, "bush_nut_collected_expected_after=")),
                        Parameter("bush_projection_status", ParseValue(candidate.ExpectedEffect, "bush_projection_status=")),
                        Parameter("max_movement_tiles", ParseValue(candidate.ExpectedEffect, "max_movement_tiles="))
                    }
                }
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        private const string FruitTreeHarvestNativeContract =
            "GameLocation.checkAction -> FruitTree.performUseAction -> FruitTree.shake; no direct fruit, debris, inventory, or skill mutation";

        private static IEnumerable<SmallModelPlanStep> HarvestFruitTreeSteps(
            PolicyEventCandidatePrediction candidate)
        {
            var stand = ParseCoordinate(candidate.ExpectedEffect, "fruit_tree_stand_tile=");
            var interaction = ParseCoordinate(candidate.ExpectedEffect, "fruit_tree_interaction_tile=");
            if (!candidate.TileX.HasValue || !candidate.TileY.HasValue || !stand.HasValue || !interaction.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "harvest_fruit_tree", 0),
                    Kind = "harvest_fruit_tree",
                    TargetLocation = candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "exact_fruit_tree_projection_still_ready=true" },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[]
                    {
                        "native_checkAction_FruitTree.performUseAction_and_shake_only",
                        "transparent_adjacent_interaction",
                        "no_direct_fruit_tree_debris_inventory_or_skill_mutation"
                    },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = new[]
                    {
                        Parameter("interaction_tile_x", interaction.Value.X.ToString()),
                        Parameter("interaction_tile_y", interaction.Value.Y.ToString()),
                        Parameter("stand_tile_x", stand.Value.X.ToString()),
                        Parameter("stand_tile_y", stand.Value.Y.ToString()),
                        Parameter("target_runtime_type", "StardewValley.TerrainFeatures.FruitTree"),
                        Parameter("fruit_tree_id", ParseValue(candidate.ExpectedEffect, "fruit_tree_id=")),
                        Parameter("qualified_item_id", candidate.QualifiedItemId),
                        Parameter("quantity", Math.Max(1, candidate.Quantity).ToString()),
                        Parameter("expected_fruit_count_before", ParseValue(candidate.ExpectedEffect, "expected_fruit_count_before=")),
                        Parameter("expected_fruit_count_after", ParseValue(candidate.ExpectedEffect, "expected_fruit_count_after=")),
                        Parameter("expected_output_items_json", ParseValue(candidate.ExpectedEffect, "expected_output_items_json=")),
                        Parameter("expected_foraging_experience_delta", ParseValue(candidate.ExpectedEffect, "expected_foraging_experience_delta=")),
                        Parameter("fruit_tree_projection_status", ParseValue(candidate.ExpectedEffect, "fruit_tree_projection_status=")),
                        Parameter("fruit_tree_native_contract", FruitTreeHarvestNativeContract),
                        Parameter("max_movement_tiles", ParseValue(candidate.ExpectedEffect, "max_movement_tiles="))
                    }.Concat(candidate.Parameters.Where(parameter =>
                        parameter.Name.StartsWith("quest_", StringComparison.Ordinal))).ToArray()
                }
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        private static IEnumerable<SmallModelPlanStep> CollectSpawnedObjectSteps(PolicyEventCandidatePrediction candidate)
        {
            var stand = ParseCoordinate(candidate.ExpectedEffect, "spawned_object_stand_tile=");
            if (!candidate.TileX.HasValue || !candidate.TileY.HasValue ||
                !stand.HasValue || string.IsNullOrWhiteSpace(candidate.QualifiedItemId))
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var parameters = new List<SmallModelActionParameter>
            {
                Parameter("stand_tile_x", stand.Value.X.ToString()),
                Parameter("stand_tile_y", stand.Value.Y.ToString()),
                Parameter("qualified_item_id", candidate.QualifiedItemId),
                Parameter("quantity", Math.Max(1, candidate.Quantity).ToString()),
                Parameter("projected_harvest_quality", ParseValue(candidate.ExpectedEffect, "projected_harvest_quality=")),
                Parameter("projected_gatherer_duplicate", ParseValue(candidate.ExpectedEffect, "projected_gatherer_duplicate=")),
                Parameter("foraging_experience_on_success_min", ParseValue(candidate.ExpectedEffect, "foraging_experience_on_success_min=")),
                Parameter("foraging_experience_on_success_max", ParseValue(candidate.ExpectedEffect, "foraging_experience_on_success_max=")),
                Parameter("farming_experience_on_success_min", ParseValue(candidate.ExpectedEffect, "farming_experience_on_success_min=")),
                Parameter("farming_experience_on_success_max", ParseValue(candidate.ExpectedEffect, "farming_experience_on_success_max=")),
                Parameter("harvest_experience_status", ParseValue(candidate.ExpectedEffect, "harvest_experience_status=")),
                Parameter("harvest_experience_basis", ParseValue(candidate.ExpectedEffect, "harvest_experience_basis=")),
                Parameter("max_movement_tiles", ParseValue(candidate.ExpectedEffect, "max_movement_tiles="))
            };
            parameters.AddRange(candidate.Parameters.Where(parameter =>
                parameter.Name.StartsWith("quest_", StringComparison.Ordinal)));
            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "collect_spawned_object", 0),
                    Kind = "collect_spawned_object",
                    TargetLocation = candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "spawned_object_identity_still_matches=true" },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[] { "native_checkAction_only", "transparent_adjacent_stand_tile", "no_direct_object_or_inventory_mutation" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = parameters.ToArray()
                }
            };
        }
    }
}

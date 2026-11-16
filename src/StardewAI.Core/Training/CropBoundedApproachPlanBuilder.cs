using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StardewAI.Core.Training;

public static class CropBoundedApproachPlanBuilder
{
    public static JsonObject Build(string stateHash, string candidateId, string sourceModel, string purposeConstraint,
        int tileX, int tileY, string executionMode, JsonNode actor)
    {
        if (actor is null) throw new ArgumentNullException(nameof(actor));
        return JsonSerializer.SerializeToNode(new
        {
            schema_version = "small_model_plan.v1", plan_id = "plan:" + candidateId,
            source_model = sourceModel, state_hash = stateHash, goal_id = "daily.closed_loop",
            execution_mode = executionMode, actor = actor.DeepClone(), plan_type = "mechanical_plan",
            steps = new[]
            {
                new
                {
                    step_id = candidateId + ":move", kind = "move_to_tile", target_location = "Farm",
                    target_tile_x = tileX, target_tile_y = tileY, estimated_minutes = 12,
                    preconditions = new[] { "candidate_id:" + candidateId, "world_ready" },
                    expected_effects = new[] { $"player.tile={tileX},{tileY}", "fresh_snapshot_replan_required=true" },
                    safety_constraints = new[] { "transparent_current_map_route_only", "bounded_route_repair_clears=4", purposeConstraint },
                    failure_policy = new[] { "refresh_snapshot_and_replan" },
                    parameters = new[]
                    {
                        new { name = "max_route_repair_clears", value = "4" },
                        new { name = "max_route_repair_minutes", value = "12" },
                        new { name = "fresh_snapshot_replan_required", value = "true" }
                    }
                }
            },
            candidate_audit = Array.Empty<object>()
        })!.AsObject();
    }
}

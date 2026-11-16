using System.Text.Json.Nodes;
using StardewAI.Core.Training;

namespace StardewAI.Backend.Tests;

public sealed class CropBoundedApproachPlanBuilderTests
{
    [Theory]
    [InlineData("authoritative_crop_site_approach.v1", "site_preparation_starts_only_after_approach")]
    [InlineData("authoritative_crop_watering_refill_approach.v1", "water_crop_retries_only_after_native_refill_approach")]
    public void OriginalRecipeRetainsExactTargetBudgetRepairAndPurpose(string model, string purpose)
    {
        var actor = new JsonObject { ["kind"] = "original-actor", ["unknown"] = new JsonObject { ["retained"] = true } };
        var parent = new JsonObject { ["actor"] = actor };
        var original = parent.ToJsonString();
        var plan = CropBoundedApproachPlanBuilder.Build("source", "candidate", model, purpose, 73, 17, "original-mode", actor);
        Assert.Equal(original, parent.ToJsonString());
        Assert.True(JsonNode.DeepEquals(actor, plan["actor"]));
        Assert.NotSame(actor, plan["actor"]);
        Assert.Equal("source", plan["state_hash"]!.GetValue<string>());
        Assert.Equal("plan:candidate", plan["plan_id"]!.GetValue<string>());
        Assert.Equal(model, plan["source_model"]!.GetValue<string>());
        Assert.Equal("original-mode", plan["execution_mode"]!.GetValue<string>());
        Assert.Empty(plan["candidate_audit"]!.AsArray());
        var step = Assert.Single(plan["steps"]!.AsArray())!;
        Assert.Equal("Farm", step["target_location"]!.GetValue<string>());
        Assert.Equal(73, step["target_tile_x"]!.GetValue<int>()); Assert.Equal(17, step["target_tile_y"]!.GetValue<int>());
        Assert.Equal(12, step["estimated_minutes"]!.GetValue<int>());
        Assert.Equal(new[] { "transparent_current_map_route_only", "bounded_route_repair_clears=4", purpose },
            step["safety_constraints"]!.AsArray().Select(node => node!.GetValue<string>()));
        Assert.Equal(new[] { "candidate_id:candidate", "world_ready" }, step["preconditions"]!.AsArray().Select(node => node!.GetValue<string>()));
        Assert.Equal(new[] { "player.tile=73,17", "fresh_snapshot_replan_required=true" },
            step["expected_effects"]!.AsArray().Select(node => node!.GetValue<string>()));
        var parameters = step["parameters"]!.AsArray().ToDictionary(node => node!["name"]!.GetValue<string>(), node => node!["value"]!.GetValue<string>());
        Assert.Equal(3, parameters.Count);
        Assert.Equal("4", parameters["max_route_repair_clears"]); Assert.Equal("12", parameters["max_route_repair_minutes"]);
        Assert.Equal("true", parameters["fresh_snapshot_replan_required"]);
        Assert.Equal(new[] { "refresh_snapshot_and_replan" }, step["failure_policy"]!.AsArray().Select(node => node!.GetValue<string>()));
    }
}

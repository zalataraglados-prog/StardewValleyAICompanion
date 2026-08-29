using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class FruitTreeHarvestMainlineTests
{
    [Fact]
    public void ExactThreeFruitTreeFlowsThroughNativeShakeQueue()
    {
        var snapshot = Snapshot(StateJson("ready", 3, "(O)638", 2));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "foraging.harvest_fruit_tree" },
            true);
        var candidate = Assert.Single(availability.Options.Single().EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("harvest_fruit_tree", candidate.Kind);
        Assert.Equal(3, candidate.Quantity);
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "expected_output_items_json");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        Assert.Equal("harvest_fruit_tree", Assert.Single(plan.Steps).Kind);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.harvest_fruit_tree", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("harvest_fruit_tree", Assert.Single(item.NormalizedCommand.Steps).StepType);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "fruit_tree_native_contract" &&
            parameter.Value == "GameLocation.checkAction -> FruitTree.performUseAction -> FruitTree.shake; no direct fruit, debris, inventory, or skill mutation");
    }

    [Fact]
    public void CompilerRejectsFruitTreeReadinessDrift()
    {
        var initial = Snapshot(StateJson("ready", 1, "(O)638", 0));
        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            new CandidateOptionAvailabilityEvaluator().Evaluate(
                initial,
                new[] { "foraging.harvest_fruit_tree" },
                true));
        var plan = new DailyPlanCompiler().Compile(ranked, initial.StateHash);
        var drifted = Snapshot(StateJson("fruit_tree_has_no_fruit", 0, "", 0));
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("harvest_fruit_tree_not_ready_by_transparent_state", queue.Items.Single().BlockingReasons);
    }

    private static string StateJson(string status, int fruitCount, string outputId, int quality)
    {
        var outputs = fruitCount > 0
            ? $$$"""[{"qualified_item_id":"{{{outputId}}}","quality":{{{quality}}},"quantity":{{{fruitCount}}}}]"""
            : "[]";
        return $$$"""
        {
          "player": {
            "location_id":{"value":"Farm","status":"available"},
            "tile_x":{"value":10,"status":"available"},
            "tile_y":{"value":10,"status":"available"}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}},
          "current_location":{
            "debris":{"value":[],"status":"available"},
            "terrain_features":{"value":[{
              "tile_x":12,"tile_y":10,"type":"StardewValley.TerrainFeatures.FruitTree",
              "runtime_type":"StardewValley.TerrainFeatures.FruitTree","is_fruit_tree":true,
              "fruit_tree_id":"628","growth_stage":4,"stump":false,"fruit_count":{{{fruitCount}}},
              "max_shake":0,"fruit_tree_harvest_status":"{{{status}}}",
              "fruit_tree_projection_status":"exact_from_native_fruit_tree_performUseAction_and_shake",
              "fruit_tree_expected_outputs":{{{outputs}}},
              "fruit_tree_expected_output_quantity_total":{{{fruitCount}}},
              "fruit_tree_expected_fruit_count_after":0,
              "fruit_tree_expected_foraging_experience_delta":0,
              "fruit_tree_native_contract":"GameLocation.checkAction -> FruitTree.performUseAction -> FruitTree.shake; no direct fruit, debris, inventory, or skill mutation"
            }],"status":"available"}
          },
          "locations":{
            "collision_grid":{"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available"},
            "route_action_branch_coverage":{"value":{"rows":[]},"status":"available"}
          }
        }
        """;
    }

    private static SnapshotEnvelope Snapshot(string json)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-30T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

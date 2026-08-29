using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class WildTreeProductMainlineTests
{
    [Fact]
    public void ExactSeedReadyTreeFlowsThroughNativeShakeQueue()
    {
        var snapshot = Snapshot(StateJson("ready", true, false, 4));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "foraging.harvest_tree_product" }, true);
        var candidate = Assert.Single(availability.Options.Single().EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("harvest_tree_product", candidate.Kind);
        Assert.Equal("(O)309", candidate.QualifiedItemId);

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        Assert.Equal("harvest_tree_product", Assert.Single(plan.Steps).Kind);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.harvest_tree_product", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("harvest_tree_product", Assert.Single(item.NormalizedCommand.Steps).StepType);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "safe_slot_index" && parameter.Value == "4");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "tree_product_output_domain_contract" && parameter.Value == "complete_stochastic_native_branch_domain_no_rng_consumed");
    }

    [Fact]
    public void CompilerRejectsSeedAndSafeSlotDrift()
    {
        var initial = Snapshot(StateJson("ready", true, false, 4));
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(),
            new CandidateOptionAvailabilityEvaluator().Evaluate(initial, new[] { "foraging.harvest_tree_product" }, true));
        var plan = new DailyPlanCompiler().Compile(ranked, initial.StateHash);
        var drifted = Snapshot(StateJson("blocked_tree_has_no_seed", false, false, 5));
        plan.StateHash = drifted.StateHash;
        var queue = new ActionQueueCompiler().Compile(plan, drifted);
        Assert.Equal("blocked", queue.Status);
        Assert.Contains("harvest_tree_product_not_ready_by_transparent_state", queue.Items.Single().BlockingReasons);
        Assert.Contains("harvest_tree_product_safe_slot_drifted", queue.Items.Single().BlockingReasons);
    }

    [Fact]
    public void TypedExecutionFieldsRoundTrip()
    {
        var request = new TrainingExecutionRequest
        {
            FixtureWildTreeProductProfile = "fall_hazelnut",
            TreeProductTreeType = "2",
            ExpectedTreeHasSeedBefore = true,
            TreeProductOutputDomainContract = "complete_stochastic_native_branch_domain_no_rng_consumed"
        };
        var json = JsonSerializer.Serialize(request, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(json, JsonOptions)!;
        Assert.Equal("fall_hazelnut", roundTrip.FixtureWildTreeProductProfile);
        Assert.Equal("2", roundTrip.TreeProductTreeType);
        Assert.True(roundTrip.ExpectedTreeHasSeedBefore);
    }

    private static string StateJson(string status, bool hasSeed, bool wasShaken, int safeSlot) => $$$"""
    {
      "player": {
        "location_id":{"value":"Farm","status":"available"}, "tile_x":{"value":10,"status":"available"}, "tile_y":{"value":10,"status":"available"},
        "skills_detail":{"value":{"foraging":{"level":1}},"status":"available"},
        "safe_item_context":{"value":{"current_tool_index":1,"active_object_selected":false,"safe_slot_available":true,"safe_slot_index":{{{safeSlot}}},"safe_slot_kind":"empty"},"status":"available"}
      },
      "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}},
      "current_location":{"debris":{"value":[],"status":"available"},"terrain_features":{"value":[{
        "tile_x":12,"tile_y":10,"type":"StardewValley.TerrainFeatures.Tree","runtime_type":"StardewValley.TerrainFeatures.Tree","tree_type":"1",
        "growth_stage":5,"stump":false,"tapped":false,"has_seed":{{{hasSeed.ToString().ToLowerInvariant()}}},"was_shaken_today":{{{wasShaken.ToString().ToLowerInvariant()}}},"max_shake":0,
        "tree_product_harvest_status":"{{{status}}}","tree_product_data_contract_status":"exact_locked_base_1.6.15","tree_product_branch":"default_seed",
        "tree_product_guaranteed_outputs":[{"qualified_item_id":"(O)309","quality":0,"quantity":1,"branch":"default_seed"}],
        "tree_product_optional_output_domain":[],"tree_product_output_distribution_status":"complete_stochastic_native_branch_domain_no_rng_consumed",
        "tree_product_expected_has_seed_after":false,"tree_product_expected_was_shaken_today_after":true,"tree_product_expected_foraging_experience_delta":0,
        "tree_product_safe_slot_index":{{{safeSlot}}},"tree_product_restore_slot_index":1,
        "tree_product_projection_status":"exact_from_native_tree_performUseAction_shake_and_locked_wild_tree_data",
        "tree_product_native_contract":"GameLocation.checkAction -> Tree.performUseAction -> Tree.shake; exact base Data/WildTrees seed branch; no direct tree, RNG, debris, inventory, or skill mutation"
      }],"status":"available"}},
      "locations":{"collision_grid":{"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available"},"route_action_branch_coverage":{"value":{"rows":[]},"status":"available"}}
    }
    """;

    private static SnapshotEnvelope Snapshot(string json)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope { StateHash = SnapshotHash.ComputeStateHash(state), GameTick = 1, RealTimestamp = "2026-08-30T00:00:00Z", Completeness = "complete", State = state };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

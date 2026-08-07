using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class AnimalProductMainlineTests
{
    private const string OutputHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void ReadyMilkAnimalFlowsFromTransparentCandidateThroughQueue()
    {
        var snapshot = Snapshot(StateJson(quality: 2));
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.collect_animal_products" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(availability.Options.Single(option => option.OptionId == "farm.collect_animal_products").EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("collect_animal_product", candidate.Kind);
        Assert.Equal("(O)184", candidate.QualifiedItemId);
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "required_tool_kind" && parameter.Value == "Milk Pail");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "expected_skill_experience_delta" && parameter.Value == "5");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "expected_energy_delta" && parameter.Value == "-4");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var planStep = Assert.Single(plan.Steps);
        Assert.Equal("collect_animal_product", planStep.Kind);
        Assert.Contains("no_direct_animal_or_inventory_mutation", planStep.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.collect_animal_product", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("collect_animal_product", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void CompilerRejectsAnimalOutputQualityDrift()
    {
        var initial = Snapshot(StateJson(quality: 2));
        var candidate = Assert.Single(new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            new CandidateOptionAvailabilityEvaluator().Evaluate(initial, new[] { "farm.collect_animal_products" }, true)));
        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, initial.StateHash);
        var drifted = Snapshot(StateJson(quality: 0));
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("collect_animal_product_output_projection_drifted", queue.Items.Single().BlockingReasons);
    }

    [Fact]
    public void AnimalCrackerDoublesExactOutputAndStatProjection()
    {
        var snapshot = Snapshot(StateJson(quality: 2, hasAnimalCracker: true));
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.collect_animal_products" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(availability.Options.Single(option =>
            option.OptionId == "farm.collect_animal_products").EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal(2, candidate.Quantity);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "expected_animal_cracker_multiplier" && parameter.Value == "2");
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "expected_stat_increments_json" && parameter.Value.Contains("\"amount\":2", StringComparison.Ordinal));

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.Empty(Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void CustomFarmAnimalSubclassIsExcludedUpstream()
    {
        var snapshot = Snapshot(StateJson(
            quality: 2,
            runtimeType: "ExampleMod.CustomMilkAnimal"));
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.collect_animal_products" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(availability.Options.Single(option =>
            option.OptionId == "farm.collect_animal_products").EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("unsupported_animal_runtime_type", candidate.BlockReasons);
    }

    [Fact]
    public void CustomAnimalProductRuntimeTypeIsExcludedUpstream()
    {
        var snapshot = Snapshot(StateJson(
            quality: 2,
            outputRuntimeType: "ExampleMod.CustomAnimalProduct"));
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.collect_animal_products" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(availability.Options.Single(option =>
            option.OptionId == "farm.collect_animal_products").EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("unsupported_animal_product_runtime_type", candidate.BlockReasons);
    }

    [Fact]
    public void CustomAnimalHarvestToolRuntimeTypeIsExcludedUpstream()
    {
        var snapshot = Snapshot(StateJson(
            quality: 2,
            toolRuntimeType: "ExampleMod.CustomMilkPail"));
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.collect_animal_products" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(availability.Options.Single(option =>
            option.OptionId == "farm.collect_animal_products").EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("unsupported_animal_harvest_tool_runtime_type", candidate.BlockReasons);
    }

    private static string StateJson(
        int quality,
        bool hasAnimalCracker = false,
        string runtimeType = "StardewValley.FarmAnimal",
        string outputRuntimeType = "StardewValley.Object",
        string toolRuntimeType = "StardewValley.Tools.MilkPail")
    {
        var quantity = hasAnimalCracker ? 2 : 1;
        var outputItems = JsonSerializer.Serialize(new[]
        {
            new { RuntimeType = "StardewValley.Object", QualifiedItemId = "(O)184", Quality = quality, UnitStateSha256 = OutputHash, Quantity = quantity }
        });
        var statIncrements = JsonSerializer.Serialize(new[] { new { stat_name = "CowMilk", amount = quantity, before = 3, after = 3 + quantity } });
        return """
        {
          "player": {
            "location_id":{"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy":{"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {"animals":{"value":[{
            "animal_id":123,"runtime_type":"ANIMAL_RUNTIME_TYPE","location_id":"Farm","name":"Bessie","display_name":"Bessie","type":"White Cow",
            "age":10,"days_to_mature":5,"is_adult":true,"friendship_toward_farmer":500,"friendship_after_harvest":505,
            "produce_quality":OUTPUT_QUALITY,"current_produce_item_id":"184","current_produce_qualified_item_id":"(O)184","has_eaten_animal_cracker":HAS_ANIMAL_CRACKER,
            "harvest_type":"HarvestWithTool","harvest_tool":"Milk Pail","harvest_tool_runtime_type":"TOOL_RUNTIME_TYPE","harvest_tool_slot_index":4,
            "harvest_status":"ready","inventory_accepts_harvest_output":true,"harvest_output_runtime_type":"OUTPUT_RUNTIME_TYPE",
            "harvest_output_qualified_item_id":"(O)184","harvest_output_quality":OUTPUT_QUALITY,"harvest_output_quantity":OUTPUT_QUANTITY,
            "harvest_output_unit_state_sha256":"OUTPUT_HASH","harvest_expected_output_items_json":OUTPUT_ITEMS,
            "harvest_stat_increments_json":STAT_INCREMENTS,"harvest_energy_cost":4,"harvest_farming_experience_delta":5,
            "harvest_friendship_delta":5,"harvest_projection_status":"exact","tile_x":12,"tile_y":10
          }],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "current_location":{"map":{"value":{"location_id":"Farm","width":100,"height":100},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "locations":{
            "collision_grid":{"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage":{"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
        .Replace("OUTPUT_QUALITY", quality.ToString())
        .Replace("OUTPUT_QUANTITY", quantity.ToString())
        .Replace("HAS_ANIMAL_CRACKER", hasAnimalCracker.ToString().ToLowerInvariant())
        .Replace("ANIMAL_RUNTIME_TYPE", runtimeType)
        .Replace("OUTPUT_RUNTIME_TYPE", outputRuntimeType)
        .Replace("TOOL_RUNTIME_TYPE", toolRuntimeType)
        .Replace("OUTPUT_HASH", OutputHash)
        .Replace("OUTPUT_ITEMS", JsonSerializer.Serialize(outputItems))
        .Replace("STAT_INCREMENTS", JsonSerializer.Serialize(statIncrements));
    }

    private static SnapshotEnvelope Snapshot(string json)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-18T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

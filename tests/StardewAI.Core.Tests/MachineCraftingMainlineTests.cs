using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class MachineCraftingMainlineTests
{
    [Fact]
    public void LearnedMachineRecipeFlowsThroughCandidatePlanAndQueue()
    {
        var snapshot = Snapshot(timesCrafted: 2, ready: true);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(availability.Options[0].EventCandidates.Where(row => row.Kind == "craft_machine_item"));

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("Keg", Parameter(candidate.Parameters, "recipe_name"));
        Assert.Equal("(BC)12", Parameter(candidate.Parameters, "output_qualified_item_id"));
        Assert.Contains("reverse_slot_consumption_plan", Parameter(candidate.Parameters, "ingredient_rows_json"));

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            snapshot.StateHash);
        var step = Assert.Single(plan.Steps.Where(row => row.Kind == "craft_machine_item"));
        Assert.Equal("Keg", Parameter(step.Parameters, "recipe_name"));

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items.Where(row => row.OptionId == "executor.craft_machine_item"));
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("pending", item.Status);
        Assert.Equal("craft_machine_item", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void CompilerRejectsChangedRecipeCountOrMaterialPlan()
    {
        var initial = Snapshot(timesCrafted: 2, ready: true);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(initial, new[] { "farm.process_machines" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(availability.Options[0].EventCandidates.Where(row => row.Kind == "craft_machine_item"));
        var plan = new DailyPlanCompiler().Compile(new[] { new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability).Single(row => row.CandidateId == candidate.CandidateId) }, initial.StateHash);
        var drifted = Snapshot(timesCrafted: 3, ready: true);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("craft_machine_item_projection_drifted", Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void OutputThatCannotFitAfterConsumptionIsExcludedUpstream()
    {
        var snapshot = Snapshot(timesCrafted: 2, ready: false);
        var candidate = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, includeExecutorCalibrationOptions: true)
            .Options[0].EventCandidates.Where(row => row.Kind == "craft_machine_item"));

        Assert.False(candidate.Available);
        Assert.Contains("machine_recipe_not_ready_for_native_personal_crafting", candidate.BlockReasons);
        Assert.Contains("machine_recipe_output_cannot_fit_after_material_consumption", candidate.BlockReasons);
    }

    private static SnapshotEnvelope Snapshot(int timesCrafted, bool ready)
    {
        var status = ready ? "ready_for_native_personal_crafting_menu" : "blocked_output_cannot_fit_after_material_consumption";
        var stateJson = """
        {
          "player": {
            "location_id": {"value":"FarmHouse","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[{"slot_index":0,"item_id":"388","qualified_item_id":"(O)388","stack":30,"quality":0,"maximum_stack_size":999,"is_empty":false},{"slot_index":1,"item_id":"390","qualified_item_id":"(O)390","stack":1,"quality":0,"maximum_stack_size":999,"is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"occupied_stacks":2,"empty_slots":0,"has_empty_slot":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "machine_crafting": {"value":{"projection_status":"complete_known_machine_recipe_projection","rows":[{
              "recipe_name":"Keg","times_crafted":TIMES_CRAFTED,"output_selection_status":"exact_single_machine_output","output_item_id":"12","output_qualified_item_id":"(BC)12","output_count_per_craft":1,
              "ingredient_rows":[{"requirement_id_or_category":"388","required_count":30,"available_count_before_this_ingredient":30,"satisfied":true,"reverse_slot_consumption_plan":[{"slot_index":0,"qualified_item_id":"(O)388","amount":30}]}],
              "has_ingredients_for_one":true,"craftable_count_from_player_inventory":1,"output_inventory_acceptance_after_material_consumption":READY,"craft_candidate_status":"STATUS"
            }]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {"machines":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "menus": {"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}}
        }
        """
        .Replace("TIMES_CRAFTED", timesCrafted.ToString())
        .Replace("READY", ready.ToString().ToLowerInvariant())
        .Replace("STATUS", status);
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stateJson, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-19T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static string Parameter(IEnumerable<StardewAI.Contracts.Execution.SmallModelActionParameter> parameters, string name) =>
        parameters.Single(parameter => parameter.Name == name).Value;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
}

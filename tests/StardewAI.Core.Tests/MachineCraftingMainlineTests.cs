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

    [Theory]
    [InlineData(0, 1, true, "priority_task_requirement", "300")]
    [InlineData(2, 1, false, "production_capacity_requirement", "200")]
    [InlineData(0, 0, false, "collection_path_requirement", "100")]
    public void MachineDemandUsesTaskThenProductionThenCollectionPriority(
        int timesCrafted,
        int potentialInputs,
        bool includeQuest,
        string expectedClass,
        string expectedPriority)
    {
        var snapshot = Snapshot(timesCrafted, ready: true, potentialInputs: potentialInputs, includeQuest: includeQuest);
        var candidate = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, includeExecutorCalibrationOptions: true)
            .Options[0].EventCandidates.Where(row => row.Kind == "craft_machine_item"));

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal(expectedClass, Parameter(candidate.Parameters, "machine_demand_class"));
        Assert.Equal(expectedPriority, Parameter(candidate.Parameters, "machine_demand_priority"));
    }

    [Fact]
    public void AlreadyCraftedMachineWithoutCapacityOrTaskDemandIsExcludedUpstream()
    {
        var snapshot = Snapshot(timesCrafted: 2, ready: true, potentialInputs: 0, includeQuest: false);
        var candidate = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, includeExecutorCalibrationOptions: true)
            .Options[0].EventCandidates.Where(row => row.Kind == "craft_machine_item"));

        Assert.False(candidate.Available);
        Assert.Contains("machine_recipe_has_no_proven_task_production_or_collection_requirement", candidate.BlockReasons);
    }

    [Fact]
    public void MachinePlacementUsesNativeLocationTopologyInsteadOfFarmAllowlist()
    {
        var topology = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters", "MachineLocationTopology.cs"));
        var placement = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.MachinePlacement.cs"));
        var crafting = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.MachineCrafting.cs"));

        Assert.Contains("Utility.ForEachLocation", topology, StringComparison.Ordinal);
        Assert.Contains("includeInteriors: true", topology, StringComparison.Ordinal);
        Assert.Contains("includeGenerated: false", topology, StringComparison.Ordinal);
        Assert.Contains("location.ParentBuilding", topology, StringComparison.Ordinal);
        Assert.Contains("location.IsGreenhouse", topology, StringComparison.Ordinal);
        Assert.Contains("no_map_name_allowlist", placement, StringComparison.Ordinal);
        Assert.Contains("probe.canBePlacedHere", placement, StringComparison.Ordinal);
        Assert.Contains("Utility.playerCanPlaceItemHere", placement, StringComparison.Ordinal);
        Assert.DoesNotContain("potential_farm", crafting, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("with_Farm_context", crafting, StringComparison.Ordinal);
    }

    private static SnapshotEnvelope Snapshot(int timesCrafted, bool ready, int potentialInputs = 1, bool includeQuest = false)
    {
        var status = ready ? "ready_for_native_personal_crafting_menu" : "blocked_output_cannot_fit_after_material_consumption";
        var stateJson = """
        {
          "player": {
            "location_id": {"value":"FarmHouse","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[{"slot_index":0,"item_id":"388","qualified_item_id":"(O)388","stack":30,"quality":0,"maximum_stack_size":999,"is_empty":false},{"slot_index":1,"item_id":"390","qualified_item_id":"(O)390","stack":1,"quality":0,"maximum_stack_size":999,"is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"occupied_stacks":2,"empty_slots":0,"has_empty_slot":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "machine_crafting": {"value":{"projection_status":"complete_known_machine_recipe_projection","rows":[{
              "recipe_name":"Keg","times_crafted":TIMES_CRAFTED,"output_selection_status":"exact_single_machine_output","output_item_id":"12","output_qualified_item_id":"(BC)12","output_count_per_craft":1,"output_context_tags":["item_keg"],
              "ingredient_rows":[{"requirement_id_or_category":"388","required_count":30,"available_count_before_this_ingredient":30,"satisfied":true,"reverse_slot_consumption_plan":[{"slot_index":0,"qualified_item_id":"(O)388","amount":30}]}],
              "has_ingredients_for_one":true,"craftable_count_from_player_inventory":1,"potential_loadable_input_count":POTENTIAL_INPUTS,"output_inventory_acceptance_after_material_consumption":READY,"craft_candidate_status":"STATUS"
            }]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {"machines":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "quests": {
            "active_quests":{"value":ACTIVE_QUESTS,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "special_orders":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}}
        }
        """
        .Replace("TIMES_CRAFTED", timesCrafted.ToString())
        .Replace("POTENTIAL_INPUTS", potentialInputs.ToString())
        .Replace("ACTIVE_QUESTS", includeQuest
            ? "[{\"id\":\"craft-keg\",\"completed\":false,\"runtime_type\":\"CraftingQuest\",\"per_type_fields\":{\"item_id\":\"12\"}}]"
            : "[]")
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

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Repository file not found.", Path.Combine(parts));
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
}

using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Contracts.Strategy;
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
    public void WorkbenchSourceFlowsThroughCandidatePlanAndCompilerOwnedTopology()
    {
        var snapshot = WithWorkbenchSource(Snapshot(timesCrafted: 2, ready: true));
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(availability.Options[0].EventCandidates.Where(row =>
            row.Kind == "craft_machine_item" &&
            Parameter(row.Parameters, "crafting_source") == "native_workbench_crafting_menu"));

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("access:workbench:FarmHouse:5,5", Parameter(candidate.Parameters, "workbench_access_point_id"));
        Assert.Equal("[\"chest:FarmHouse:4,5\"]", Parameter(candidate.Parameters, "workbench_container_node_ids_json"));
        Assert.Contains("native_consumption_plan", Parameter(candidate.Parameters, "ingredient_rows_json"));

        var ranked = new EventCandidateRanker()
            .Rank(new BaselineTrainingReport(), availability)
            .Where(row => row.CandidateId == candidate.CandidateId)
            .ToArray();
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.True(
            queue.Status == "pending",
            string.Join(";", queue.Items.SelectMany(row => row.BlockingReasons)));
        Assert.Empty(Assert.Single(queue.Items).BlockingReasons);

        var parameter = Assert.Single(plan.Steps)
            .Parameters.Single(row => row.Name == "workbench_container_node_ids_json");
        parameter.Value = "[\"chest:FarmHouse:6,5\"]";
        var drifted = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.Contains(
            "craft_machine_item_workbench_projection_drifted",
            Assert.Single(drifted.Items).BlockingReasons);
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

    [Fact]
    public void ActiveMaterialReservationBlocksCraftThatWouldConsumeCommittedSlot()
    {
        var snapshot = Snapshot(timesCrafted: 2, ready: true);
        var ledger = MaterialLedger(
            revision: 1,
            StrategyCommitmentStatuses.Active,
            nodeId: "player:123",
            slotIndex: 0,
            qualifiedItemId: "(O)388",
            quantity: 1);
        var candidate = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[] { "farm.process_machines" },
                includeExecutorCalibrationOptions: true,
                ledger)
            .Options[0].EventCandidates.Where(row => row.Kind == "craft_machine_item"));

        Assert.False(candidate.Available);
        Assert.Contains(
            "machine_recipe_material_reserved_for_other_goal:player:123#0",
            candidate.BlockReasons);
        Assert.Equal("blocked", Parameter(candidate.Parameters, "material_reservation_guard_status"));
        Assert.Contains("keg-wood", Parameter(candidate.Parameters, "material_reservation_ids_json"));
    }

    [Fact]
    public void CancelledMaterialReservationDoesNotBlockCraft()
    {
        var snapshot = Snapshot(timesCrafted: 2, ready: true);
        var ledger = MaterialLedger(
            revision: 2,
            StrategyCommitmentStatuses.Cancelled,
            nodeId: "player:123",
            slotIndex: 0,
            qualifiedItemId: "(O)388",
            quantity: 30);
        var candidate = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, true, ledger)
            .Options[0].EventCandidates.Where(row => row.Kind == "craft_machine_item"));

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal(
            "ready_no_active_material_reservations",
            Parameter(candidate.Parameters, "material_reservation_guard_status"));
    }

    [Fact]
    public void CompilerRejectsChangedMaterialReservationLedger()
    {
        var snapshot = Snapshot(timesCrafted: 2, ready: true);
        var initialLedger = MaterialLedger(
            revision: 1,
            StrategyCommitmentStatuses.Active,
            nodeId: "player:123",
            slotIndex: 1,
            qualifiedItemId: "(O)390",
            quantity: 1);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, true, initialLedger);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            snapshot.StateHash);
        var initialQueue = new ActionQueueCompiler().Compile(plan, snapshot, initialLedger);
        Assert.Equal("pending", initialQueue.Status);

        var revisedLedger = MaterialLedger(
            revision: 2,
            StrategyCommitmentStatuses.Cancelled,
            nodeId: "player:123",
            slotIndex: 1,
            qualifiedItemId: "(O)390",
            quantity: 1);
        var staleQueue = new ActionQueueCompiler().Compile(plan, snapshot, revisedLedger);

        Assert.Equal("blocked", staleQueue.Status);
        Assert.Contains(
            "craft_machine_item_material_reservation_projection_drifted",
            Assert.Single(staleQueue.Items).BlockingReasons);
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
    public void FactoryCapacityBuildIsDeferredUntilLatestUsefulWindow()
    {
        var snapshot = Snapshot(
            timesCrafted: 2,
            ready: true,
            potentialInputs: 5,
            cropDaysUntilHarvest: 20,
            processMinutes: 4000);
        var candidate = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, includeExecutorCalibrationOptions: true)
            .Options[0].EventCandidates.Where(row => row.Kind == "craft_machine_item"));

        Assert.False(candidate.Available);
        Assert.Equal("factory_scale_batch", Parameter(candidate.Parameters, "machine_scale"));
        Assert.Equal("deferred_until_latest_build_window", Parameter(candidate.Parameters, "machine_demand_class"));
        Assert.Equal("deferred_too_early_machine_would_idle", Parameter(candidate.Parameters, "machine_timing_status"));
        Assert.Contains("machine_build_deferred_too_early", candidate.BlockReasons);
    }

    [Fact]
    public void FactoryCapacityBuildOpensWhenLatestWindowIsReached()
    {
        var snapshot = Snapshot(
            timesCrafted: 2,
            ready: true,
            potentialInputs: 5,
            cropDaysUntilHarvest: 5,
            processMinutes: 4000);
        var candidate = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, includeExecutorCalibrationOptions: true)
            .Options[0].EventCandidates.Where(row => row.Kind == "craft_machine_item"));

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("factory_scale_batch", Parameter(candidate.Parameters, "machine_scale"));
        Assert.Equal("production_capacity_requirement", Parameter(candidate.Parameters, "machine_demand_class"));
        Assert.Equal("true", Parameter(candidate.Parameters, "machine_build_window_open"));
        Assert.Equal("3", Parameter(candidate.Parameters, "required_additional_machine_count"));
    }

    [Fact]
    public void ExistingFleetThatClearsBacklogBeforeHarvestSuppressesExpansion()
    {
        var snapshot = Snapshot(
            timesCrafted: 2,
            ready: true,
            potentialInputs: 2,
            cropDaysUntilHarvest: 3,
            processMinutes: 1000,
            placedMachineCount: 1);
        var candidate = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, includeExecutorCalibrationOptions: true)
            .Options[0].EventCandidates.Where(row => row.Kind == "craft_machine_item"));

        Assert.False(candidate.Available);
        Assert.Equal("deferred_existing_fleet_clears_backlog_within_horizon", Parameter(candidate.Parameters, "machine_timing_status"));
        Assert.Equal("0", Parameter(candidate.Parameters, "capacity_deficit_units"));
    }

    [Fact]
    public void NativeRaccoonSmokedFishRequirementCreatesCollectionScaleTaskDemand()
    {
        var snapshot = Snapshot(
            timesCrafted: 2,
            ready: true,
            potentialInputs: 0,
            processMinutes: 50,
            recipeName: "Fish Smoker",
            machineQualifiedId: "(BC)FishSmoker",
            machineItemId: "FishSmoker",
            inputQualifiedId: "(O)136",
            inputItemId: "136",
            predictedOutputQualifiedId: "(O)SmokedFish",
            predictedOutputItemId: "SmokedFish",
            predictedPreservedItemId: "136",
            includeRaccoonRequest: true);
        var candidate = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, includeExecutorCalibrationOptions: true)
            .Options[0].EventCandidates.Where(row => row.Kind == "craft_machine_item"));

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("collection_scale_one_off", Parameter(candidate.Parameters, "machine_scale"));
        Assert.Equal("priority_task_requirement", Parameter(candidate.Parameters, "machine_demand_class"));
        Assert.Contains("raccoon_bundle:ingredient:1", Parameter(candidate.Parameters, "priority_task_sources_json"));
    }

    [Fact]
    public void CrossSeasonCommitmentDrivesFactoryWindowAndCompilerRejectsCancellation()
    {
        var snapshot = Snapshot(
            timesCrafted: 2,
            ready: true,
            potentialInputs: 5,
            processMinutes: 4000,
            totalDays: 100);
        var ledger = CommitmentLedger(1, StrategyCommitmentStatuses.Active);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, true, ledger);
        var candidate = Assert.Single(availability.Options[0].EventCandidates.Where(row => row.Kind == "craft_machine_item"));

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("factory_scale_batch", Parameter(candidate.Parameters, "machine_scale"));
        Assert.Equal("committed_strategy_ledger", Parameter(candidate.Parameters, "next_arrival_source"));
        Assert.Equal("1", Parameter(candidate.Parameters, "commitment_ledger_revision"));
        Assert.Contains("year2-spring-crop", Parameter(candidate.Parameters, "commitment_ids_json"));

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot, ledger);
        Assert.Equal("pending", queue.Status);

        var cancelled = CommitmentLedger(2, StrategyCommitmentStatuses.Cancelled);
        var staleQueue = new ActionQueueCompiler().Compile(plan, snapshot, cancelled);
        Assert.Equal("blocked", staleQueue.Status);
        Assert.Contains("craft_machine_item_demand_projection_drifted", Assert.Single(staleQueue.Items).BlockingReasons);
    }

    [Fact]
    public void FutureCommittedCropWithoutInventoryUsesStaticNativeTriggerAndRecurringThroughput()
    {
        var snapshot = Snapshot(
            timesCrafted: 2,
            ready: true,
            potentialInputs: 0,
            processMinutes: 1000,
            totalDays: 105,
            includeStaticFruitTrigger: true);
        var candidate = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, true, CommitmentLedger(1, StrategyCommitmentStatuses.Active))
            .Options[0].EventCandidates.Where(row => row.Kind == "craft_machine_item"));

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("0", Parameter(candidate.Parameters, "potential_input_count"));
        Assert.Equal("committed_strategy_ledger", Parameter(candidate.Parameters, "next_arrival_source"));
        Assert.Equal("4", Parameter(candidate.Parameters, "next_arrival_service_interval_days"));
        Assert.Equal("0", Parameter(candidate.Parameters, "capacity_between_arrival_waves"));
        Assert.Equal("5", Parameter(candidate.Parameters, "arrival_wave_capacity_deficit_units"));
        Assert.Equal("1", Parameter(candidate.Parameters, "required_additional_machine_count"));
        Assert.Contains("static_native_machine_trigger", Parameter(candidate.Parameters, "machine_horizon_status"));
    }

    [Fact]
    public void DynamicNativeTriggerIsNotGuessedForFutureCrop()
    {
        var snapshot = Snapshot(
            timesCrafted: 2,
            ready: true,
            potentialInputs: 0,
            processMinutes: 1000,
            totalDays: 105,
            includeStaticFruitTrigger: true,
            triggerCondition: "SEASON spring");
        var candidate = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, true, CommitmentLedger(1, StrategyCommitmentStatuses.Active))
            .Options[0].EventCandidates.Where(row => row.Kind == "craft_machine_item"));

        Assert.False(candidate.Available);
        Assert.Equal("no_proven_current_requirement", Parameter(candidate.Parameters, "machine_demand_class"));
    }

    [Fact]
    public void ExistingFleetThatServicesCommittedRegrowWaveSuppressesExpansion()
    {
        var snapshot = Snapshot(
            timesCrafted: 2,
            ready: true,
            potentialInputs: 0,
            processMinutes: 1000,
            placedMachineCount: 1,
            totalDays: 105,
            includeStaticFruitTrigger: true);
        var candidate = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, true, CommitmentLedger(1, StrategyCommitmentStatuses.Active))
            .Options[0].EventCandidates.Where(row => row.Kind == "craft_machine_item"));

        Assert.False(candidate.Available);
        Assert.Equal("6", Parameter(candidate.Parameters, "capacity_between_arrival_waves"));
        Assert.Equal("0", Parameter(candidate.Parameters, "arrival_wave_capacity_deficit_units"));
        Assert.Equal("0", Parameter(candidate.Parameters, "required_additional_machine_count"));
    }

    [Fact]
    public void FutureCapabilityWithUncommittedAdditionalMachineInputsFailsClosed()
    {
        var snapshot = Snapshot(
            timesCrafted: 2,
            ready: true,
            potentialInputs: 0,
            processMinutes: 1000,
            totalDays: 105,
            includeStaticFruitTrigger: true,
            machineAdditionalConsumedCount: 1);
        var candidate = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, true, CommitmentLedger(1, StrategyCommitmentStatuses.Active))
            .Options[0].EventCandidates.Where(row => row.Kind == "craft_machine_item"));

        Assert.False(candidate.Available);
        Assert.Equal("no_proven_current_requirement", Parameter(candidate.Parameters, "machine_demand_class"));
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
        var machineCatalog = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters", "FarmReadAdapter.Machines.cs"));

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
        Assert.Contains("ReadMachineOutputItemList(ReadMemberValue(rule!, \"OutputItem\")", machineCatalog, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadMemberValue(rule!, \"OutputItems\")", machineCatalog, StringComparison.Ordinal);
        Assert.Contains("ReadMemberValue(machineData, \"AdditionalConsumedItems\")", machineCatalog, StringComparison.Ordinal);
    }

    private static SnapshotEnvelope Snapshot(
        int timesCrafted,
        bool ready,
        int potentialInputs = 1,
        bool includeQuest = false,
        int cropDaysUntilHarvest = -1,
        int processMinutes = 1000,
        int placedMachineCount = 0,
        string recipeName = "Keg",
        string machineQualifiedId = "(BC)12",
        string machineItemId = "12",
        string inputQualifiedId = "(O)262",
        string inputItemId = "262",
        string predictedOutputQualifiedId = "(O)346",
        string predictedOutputItemId = "346",
        string predictedPreservedItemId = "",
        bool includeRaccoonRequest = false,
        int totalDays = 0,
        bool includeStaticFruitTrigger = false,
        string triggerCondition = "",
        int machineAdditionalConsumedCount = 0)
    {
        var status = ready ? "ready_for_native_personal_crafting_menu" : "blocked_output_cannot_fit_after_material_consumption";
        var stateJson = """
        {
          "player": {
            "location_id": {"value":"FarmHouse","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[{"slot_index":0,"item_id":"388","qualified_item_id":"(O)388","stack":30,"quality":0,"maximum_stack_size":999,"is_empty":false},{"slot_index":1,"item_id":"390","qualified_item_id":"(O)390","stack":1,"quality":0,"maximum_stack_size":999,"is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"occupied_stacks":2,"empty_slots":0,"has_empty_slot":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "machine_crafting": {"value":{"projection_status":"complete_known_machine_recipe_projection","rows":[{
              "recipe_name":"RECIPE_NAME","times_crafted":TIMES_CRAFTED,"output_selection_status":"exact_single_machine_output","output_item_id":"MACHINE_ITEM_ID","output_qualified_item_id":"MACHINE_QUALIFIED_ID","output_count_per_craft":1,"output_context_tags":["item_machine"],"output_machine_data":{"status":"available","additional_consumed_item_count":MACHINE_ADDITIONAL_COUNT,"additional_consumed_items":[],"prevent_time_pass_count":0,"ready_time_modifier_count":0,"only_complete_overnight":false,"output_rules":[{"minutes_until_ready":PROCESS_MINUTES,"triggers":MACHINE_TRIGGERS,"output_item":{"item_id":"PREDICTED_OUTPUT_ITEM_ID","qualified_item_id":"PREDICTED_OUTPUT_QUALIFIED_ID","preserve_id":"PREDICTED_PRESERVED_ITEM_ID"}}]},
              "ingredient_rows":[{"requirement_id_or_category":"388","required_count":30,"available_count_before_this_ingredient":30,"satisfied":true,"reverse_slot_consumption_plan":[{"slot_index":0,"qualified_item_id":"(O)388","amount":30}]}],
              "has_ingredients_for_one":true,"craftable_count_from_player_inventory":1,"potential_loadable_input_count":POTENTIAL_INPUTS,"potential_loadable_inputs":POTENTIAL_ROWS,"output_inventory_acceptance_after_material_consumption":READY,"craft_candidate_status":"STATUS"
            }]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "crops":{"value":CROP_ROWS,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "machines":{"value":MACHINE_ROWS,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "material_inventory_graph":{"value":{"schema_version":"material_inventory_graph.v1","status":"available","player_id":123,"inventory_nodes":[{"node_id":"player:123","inventory_kind":"player_inventory","supply_state":"available","owner_player_id":123,"ownership_class":"actor_owned","actor_use_authorized":true,"slots":[{"slot_index":0,"item_id":"388","qualified_item_id":"(O)388","stack":30},{"slot_index":1,"item_id":"390","qualified_item_id":"(O)390","stack":1}]}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "quests": {
            "active_quests":{"value":ACTIVE_QUESTS,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "special_orders":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress":{"raccoon_request":{"value":RACCOON_REQUEST,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "time":{"time":{"value":600,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},"total_days":{"value":TOTAL_DAYS,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "menus": {"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}}
        }
        """
        .Replace("TIMES_CRAFTED", timesCrafted.ToString())
        .Replace("TOTAL_DAYS", totalDays.ToString())
        .Replace("POTENTIAL_INPUTS", potentialInputs.ToString())
        .Replace("POTENTIAL_ROWS", potentialInputs > 0
            ? "[{\"slot_index\":2,\"item_id\":\"" + inputItemId + "\",\"qualified_item_id\":\"" + inputQualifiedId + "\",\"stack\":" + potentialInputs + ",\"accepting_contexts\":[{\"location_id\":\"Farm\",\"predicted_output\":{\"status\":\"available\",\"effective_minutes_until_ready\":" + processMinutes + ",\"preserved_item_id\":\"" + predictedPreservedItemId + "\",\"item\":{\"item_id\":\"" + predictedOutputItemId + "\",\"qualified_item_id\":\"" + predictedOutputQualifiedId + "\"}}}]}]"
            : "[]")
        .Replace("RECIPE_NAME", recipeName)
        .Replace("MACHINE_QUALIFIED_ID", machineQualifiedId)
        .Replace("MACHINE_ITEM_ID", machineItemId)
        .Replace("PROCESS_MINUTES", processMinutes.ToString())
        .Replace("PREDICTED_OUTPUT_ITEM_ID", predictedOutputItemId)
        .Replace("PREDICTED_OUTPUT_QUALIFIED_ID", predictedOutputQualifiedId)
        .Replace("PREDICTED_PRESERVED_ITEM_ID", predictedPreservedItemId)
        .Replace("MACHINE_ADDITIONAL_COUNT", machineAdditionalConsumedCount.ToString())
        .Replace("MACHINE_TRIGGERS", includeStaticFruitTrigger
            ? "[{\"trigger\":\"ItemPlacedInMachine\",\"condition\":\"" + triggerCondition + "\",\"required_item_id\":\"\",\"required_tags\":[\"category_fruits\"],\"required_count\":1}]"
            : "[]")
        .Replace("CROP_ROWS", cropDaysUntilHarvest >= 0
            ? "[{\"harvest_item_id\":\"" + inputItemId + "\",\"harvest_item_qualified_id\":\"" + inputQualifiedId + "\",\"days_until_next_harvest_if_watered\":" + cropDaysUntilHarvest + ",\"harvest_min_stack\":1,\"dead\":false}]"
            : "[]")
        .Replace("MACHINE_ROWS", "[" + string.Join(",", Enumerable.Range(0, placedMachineCount).Select(index => "{\"qualified_item_id\":\"" + machineQualifiedId + "\",\"minutes_until_ready\":-1,\"ready_for_harvest\":false}")) + "]")
        .Replace("RACCOON_REQUEST", includeRaccoonRequest
            ? "{\"projection_status\":\"exact_native_Raccoon.GetBundle\",\"request_available\":true,\"ingredients\":[{\"ingredient_index\":1,\"item_id\":\"SmokedFish\",\"preserves_item_id\":\"136\",\"completed\":false}]}"
            : "null")
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

    private static StrategyCommitmentLedger CommitmentLedger(int revision, string status) => new()
    {
        LedgerId = "strategy-ledger:test",
        Revision = revision,
        CropPlantingCommitments = new[]
        {
            new CropPlantingCommitment
            {
                CommitmentId = "year2-spring-crop",
                Revision = revision,
                Status = status,
                SeedId = "472",
                HarvestItemId = "262",
                HarvestItemQualifiedId = "(O)262",
                HarvestContextTags = new[] { "category_fruits", "item_apple" },
                TileCount = 5,
                PlantingTotalDay = 97,
                FirstHarvestTotalDay = 105,
                RegrowDays = 4,
                LastInSeasonHarvestTotalDay = 117,
                MinimumUnitsPerWave = 5
            }
        }
    };

    private static StrategyCommitmentLedger MaterialLedger(
        int revision,
        string status,
        string nodeId,
        int slotIndex,
        string qualifiedItemId,
        int quantity) => new()
        {
            LedgerId = "strategy-ledger:test",
            Revision = revision,
            MaterialReservations = new[]
            {
                new MaterialReservation
                {
                    ReservationId = "keg-wood",
                    Revision = revision,
                    Status = status,
                    SourceDecisionId = "strategy.keg",
                    GoalId = "goal.keg",
                    OwnerPlayerId = 123,
                    NodeId = nodeId,
                    SlotIndex = slotIndex,
                    QualifiedItemId = qualifiedItemId,
                    Quantity = quantity,
                    Purpose = "test reservation"
                }
            }
        };

    private static SnapshotEnvelope WithWorkbenchSource(SnapshotEnvelope snapshot)
    {
        var root = JsonNode.Parse(JsonSerializer.Serialize(snapshot.State))!.AsObject();
        var player = root["player"]!.AsObject();
        player["tile_x"] = JsonNode.Parse("""{"value":5,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}""");
        player["tile_y"] = JsonNode.Parse("""{"value":6,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}""");
        var row = player["machine_crafting"]!["value"]!["rows"]![0]!.AsObject();
        row["workbench_crafting_sources"] = JsonNode.Parse("""
        [{
          "workbench_access_point_id":"access:workbench:FarmHouse:5,5",
          "location_id":"FarmHouse",
          "tile_x":5,
          "tile_y":5,
          "projection_status":"exact_native_player_then_container_reverse_slot_consumption",
          "blocking_reasons":[],
          "native_container_node_ids":["chest:FarmHouse:4,5"],
          "ingredient_rows":[{
            "requirement_id_or_category":"388",
            "required_count":30,
            "available_count_before_this_ingredient":30,
            "satisfied":true,
            "native_consumption_plan":[{
              "source_node_id":"chest:FarmHouse:4,5",
              "slot_index":0,
              "qualified_item_id":"(O)388",
              "amount":30
            }]
          }],
          "has_ingredients_for_one":true,
          "craftable_count":1,
          "output_inventory_acceptance_after_material_consumption":true,
          "craft_candidate_status":"ready_for_native_workbench_crafting_menu"
        }]
        """);
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            root.ToJsonString(),
            JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = snapshot.SchemaVersion,
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = snapshot.GameTick,
            RealTimestamp = snapshot.RealTimestamp,
            Completeness = snapshot.Completeness,
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

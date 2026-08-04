using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class MachineFamilyMainlineTests
{
    public static IEnumerable<object[]> Families => new[]
    {
        new object[] { new MachineFamily("Preserves Jar", "(BC)15", "24", "(O)24", "342", "(O)342", 35, 210, 4000) },
        new object[] { new MachineFamily("Keg", "(BC)12", "262", "(O)262", "346", "(O)346", 15, 200, 1750) },
        new object[] { new MachineFamily("Mayonnaise Machine", "(BC)24", "176", "(O)176", "306", "(O)306", 50, 190, 180) },
        new object[] { new MachineFamily("Cheese Press", "(BC)16", "184", "(O)184", "424", "(O)424", 125, 230, 200) },
        new object[] { new MachineFamily("Furnace", "(BC)13", "378", "(O)378", "334", "(O)334", 5, 60, 30) },
        new object[] { new MachineFamily("Charcoal Kiln", "(BC)114", "388", "(O)388", "382", "(O)382", 2, 15, 30) }
    };

    [Fact]
    public void PurposeLimitedPredictionCarriesAdditionalConsumptionForValueGate()
    {
        using var machine = JsonDocument.Parse(
            """{"machine_data":{"status":"blocked","reason":"machine_profile_minimal_skips_machine_data"}}""");
        using var completeInput = JsonDocument.Parse(
            """{"sale_price":50,"predicted_output":{"sale_price":400,"stack":1,"required_count":1,"additional_consumed_item_count":0}}""");
        using var incompleteInput = JsonDocument.Parse(
            """{"sale_price":50,"predicted_output":{"sale_price":400,"stack":1,"required_count":1}}""");

        Assert.Equal(
            350,
            MachineSupportIntentProjection.CurrentInputNetValue(
                machine.RootElement,
                completeInput.RootElement));
        Assert.Null(
            MachineSupportIntentProjection.CurrentInputNetValue(
                machine.RootElement,
                incompleteInput.RootElement));
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void SixMachineFamiliesFlowFromCandidateThroughPlanAndQueue(MachineFamily family)
    {
        var snapshot = Snapshot(FamilySnapshot(family));
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, includeExecutorCalibrationOptions: true);
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);

        var collectCandidate = Assert.Single(ranked.Where(candidate => candidate.CandidateId.StartsWith("machine-output:Farm:64,15", StringComparison.Ordinal)));
        Assert.Equal("collect_machine_output_tile", collectCandidate.Kind);
        Assert.True(collectCandidate.Available);
        Assert.Equal(family.OutputQualifiedId, collectCandidate.QualifiedItemId);
        Assert.Contains("move_to_adjacent=63,15", collectCandidate.ExpectedEffect);
        Assert.Contains("output_total_value=" + family.OutputSalePrice, collectCandidate.ExpectedEffect);

        var collectPlan = new DailyPlanCompiler().Compile(new[] { collectCandidate }, snapshot.StateHash);
        Assert.Equal(new[] { "move_to_tile", "collect_machine_output" }, collectPlan.Steps.Select(step => step.Kind).ToArray());
        Assert.Contains(collectPlan.Steps[1].Parameters, parameter => parameter.Name == "qualified_item_id" && parameter.Value == family.OutputQualifiedId);
        Assert.Contains(collectPlan.Steps[1].Parameters, parameter => parameter.Name == "output_total_value" && parameter.Value == family.OutputSalePrice.ToString());

        var collectQueue = new ActionQueueCompiler().Compile(collectPlan, snapshot);
        Assert.True(collectQueue.Status == "pending", string.Join(";", collectQueue.Items.SelectMany(item => item.BlockingReasons)));
        var collectStep = Assert.Single(collectQueue.Items.Single(item => item.OptionId == "executor.collect_machine_output").NormalizedCommand.Steps);
        Assert.Equal("collect_machine_output", collectStep.StepType);
        Assert.Equal("Farm(64,15):" + family.OutputQualifiedId, collectStep.Target);
        Assert.Contains("output_total_value=" + family.OutputSalePrice, collectStep.ExpectedEffect);

        var loadCandidate = Assert.Single(ranked.Where(candidate => candidate.CandidateId.StartsWith("machine-input:Farm:66,15", StringComparison.Ordinal)));
        Assert.Equal("load_machine_input_tile", loadCandidate.Kind);
        Assert.True(loadCandidate.Available);
        Assert.Equal(family.InputQualifiedId, loadCandidate.QualifiedItemId);
        Assert.Equal(0, loadCandidate.SlotIndex);
        Assert.Contains("move_to_adjacent=65,15", loadCandidate.ExpectedEffect);
        Assert.Contains("predicted_output_qualified_item_id=" + family.OutputQualifiedId, loadCandidate.ExpectedEffect);
        Assert.Contains("predicted_output_total_value=" + family.OutputSalePrice, loadCandidate.ExpectedEffect);
        Assert.Contains("predicted_minutes_until_ready=" + family.MinutesUntilReady, loadCandidate.ExpectedEffect);

        var loadPlan = new DailyPlanCompiler().Compile(new[] { loadCandidate }, snapshot.StateHash);
        Assert.Equal(new[] { "move_to_tile", "load_machine_input" }, loadPlan.Steps.Select(step => step.Kind).ToArray());
        Assert.Contains(loadPlan.Steps[1].Parameters, parameter => parameter.Name == "input_slot_index" && parameter.Value == "0");
        Assert.Contains(loadPlan.Steps[1].Parameters, parameter => parameter.Name == "qualified_item_id" && parameter.Value == family.InputQualifiedId);
        Assert.Contains(loadPlan.Steps[1].Parameters, parameter => parameter.Name == "predicted_output_qualified_item_id" && parameter.Value == family.OutputQualifiedId);
        Assert.Contains(loadPlan.Steps[1].Parameters, parameter => parameter.Name == "predicted_minutes_until_ready" && parameter.Value == family.MinutesUntilReady.ToString());

        var loadQueue = new ActionQueueCompiler().Compile(loadPlan, snapshot);
        Assert.True(loadQueue.Status == "pending", string.Join(";", loadQueue.Items.SelectMany(item => item.BlockingReasons)));
        var loadItem = loadQueue.Items.Single(item => item.OptionId == "executor.load_machine_input");
        Assert.Contains(loadItem.NormalizedCommand.Parameters, parameter => parameter.Name == "input_slot_index" && parameter.Value == "0");
        Assert.Contains(loadItem.NormalizedCommand.Parameters, parameter => parameter.Name == "qualified_item_id" && parameter.Value == family.InputQualifiedId);
        Assert.Contains(loadItem.NormalizedCommand.Parameters, parameter => parameter.Name == "predicted_output_qualified_item_id" && parameter.Value == family.OutputQualifiedId);
        Assert.Contains(loadItem.NormalizedCommand.Parameters, parameter => parameter.Name == "predicted_minutes_until_ready" && parameter.Value == family.MinutesUntilReady.ToString());
        var loadStep = Assert.Single(loadItem.NormalizedCommand.Steps);
        Assert.Equal("load_machine_input", loadStep.StepType);
        Assert.Equal("Farm(66,15):slot0:" + family.InputQualifiedId, loadStep.Target);
        Assert.Contains("predicted_output_qualified_item_id=" + family.OutputQualifiedId, loadStep.ExpectedEffect);
        Assert.Contains("predicted_minutes_until_ready=" + family.MinutesUntilReady, loadStep.ExpectedEffect);
    }

    [Fact]
    public void MachineOutputOptionExposesOnlyTheExistingReadyOutputChain()
    {
        var family = (MachineFamily)Families
            .Single(row => ((MachineFamily)row[0]).DisplayName == "Keg")[0];
        var snapshot = Snapshot(FamilySnapshot(family));
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.collect_machine_outputs" });
        var option = Assert.Single(availability.Options);

        Assert.NotEmpty(option.EventCandidates);
        Assert.All(option.EventCandidates, candidate =>
            Assert.Equal("collect_machine_output_tile", candidate.Kind));
        var candidate = Assert.Single(option.EventCandidates.Where(row => row.Available));
        Assert.Equal("machine-output:Farm:64,15:(O)346", candidate.CandidateId);

        var ranked = Assert.Single(new EventCandidateRanker()
            .Rank(new BaselineTrainingReport(), availability));
        var plan = new DailyPlanCompiler().Compile([ranked], snapshot.StateHash);
        Assert.Equal(
            new[] { "move_to_tile", "collect_machine_output" },
            plan.Steps.Select(step => step.Kind).ToArray());

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.Equal("pending", queue.Status);
        Assert.Contains(queue.Items, item =>
            item.OptionId == "executor.collect_machine_output");
        Assert.DoesNotContain(queue.Items, item =>
            item.OptionId == "executor.load_machine_input" ||
            item.OptionId == "executor.craft_machine_item" ||
            item.OptionId == "executor.place_machine" ||
            item.OptionId == "executor.remove_machine");
    }

    [Fact]
    public void SupportedMachineLoadInheritsExactPositiveIntent()
    {
        var family = (MachineFamily)Families
            .Single(row =>
                ((MachineFamily)row[0]).DisplayName == "Keg")[0];
        var snapshot = Snapshot(FamilySnapshot(family));
        var ledger = MachineSupportLedger(family);
        var availability =
            new CandidateOptionAvailabilityEvaluator()
                .Evaluate(
                    snapshot,
                    new[] { "farm.process_machines" },
                    includeExecutorCalibrationOptions: true,
                    ledger);
        var candidate = Assert.Single(
            availability.Options[0].EventCandidates.Where(row =>
                row.Kind == "load_machine_input_tile" &&
                row.TileX == 66 &&
                row.TileY == 15));

        Assert.Equal(
            "active",
            Parameter(
                candidate.Parameters,
                "machine_support_continuation_status"));
        Assert.Equal(
            "machine-support:money:keg",
            Parameter(
                candidate.Parameters,
                "machine_support_intent_id"));
        Assert.Equal(
            (family.OutputSalePrice - family.InputSalePrice)
                .ToString(),
            Parameter(
                candidate.Parameters,
                "machine_support_current_input_net_benefit"));

        var ranked = Assert.Single(
            new EventCandidateRanker()
                .Rank(
                    new BaselineTrainingReport(),
                    availability,
                    "goal.economy.earn_money")
                .Where(row =>
                    row.CandidateId == candidate.CandidateId));
        var plan = new DailyPlanCompiler().Compile(
            [ranked],
            snapshot.StateHash,
            "goal.economy.earn_money");
        var queue = new ActionQueueCompiler().Compile(
            plan,
            snapshot,
            ledger);

        Assert.True(
            queue.Status == "pending",
            string.Join(
                ";",
                queue.Items.SelectMany(item =>
                    item.BlockingReasons)));
        var load = queue.Items.Single(item =>
            item.OptionId == "executor.load_machine_input");
        Assert.Contains(
            load.NormalizedCommand.Parameters,
            parameter =>
                parameter.Name ==
                    "machine_support_intent_id" &&
                parameter.Value ==
                    "machine-support:money:keg");
        var dispatch =
            new ActionQueueDispatchReadinessService()
                .Evaluate(
                    queue,
                    load,
                    ledger,
                    snapshot.StateHash);
        Assert.True(
            dispatch.Ready,
            string.Join(";", dispatch.BlockingReasons));

        plan.Steps.Single(step =>
            step.Kind == "load_machine_input").Parameters
            .Single(parameter =>
                parameter.Name ==
                    "machine_support_current_input_net_benefit")
            .Value = "999999";
        var tampered = new ActionQueueCompiler().Compile(
            plan,
            snapshot,
            ledger);
        Assert.Contains(
            "load_machine_input_support_intent_drifted",
            tampered.Items.Single(item =>
                item.OptionId ==
                    "executor.load_machine_input")
                .BlockingReasons);
    }

    [Fact]
    public void SupportedMachineInputOptionRequiresIntentAndReusesLoadChain()
    {
        var family = (MachineFamily)Families
            .Single(row => ((MachineFamily)row[0]).DisplayName == "Keg")[0];
        var snapshot = Snapshot(FamilySnapshot(family));
        var evaluator = new CandidateOptionAvailabilityEvaluator();
        var withoutIntent = evaluator.Evaluate(
            snapshot,
            new[] { "farm.load_supported_machine_input" });

        Assert.Empty(Assert.Single(withoutIntent.Options).EventCandidates);

        var ledger = MachineSupportLedger(family);
        var availability = evaluator.Evaluate(
            snapshot,
            new[] { "farm.load_supported_machine_input" },
            commitmentLedger: ledger);
        var option = Assert.Single(availability.Options);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("load_machine_input_tile", candidate.Kind);
        Assert.Equal("active", Parameter(
            candidate.Parameters,
            "machine_support_continuation_status"));
        Assert.Equal("ready_no_active_material_reservations", Parameter(
            candidate.Parameters,
            "material_reservation_guard_status"));

        var ranked = Assert.Single(new EventCandidateRanker()
            .Rank(new BaselineTrainingReport(), availability));
        var plan = new DailyPlanCompiler().Compile(
            [ranked],
            snapshot.StateHash,
            "goal.economy.earn_money");
        Assert.Equal(
            new[] { "move_to_tile", "load_machine_input" },
            plan.Steps.Select(step => step.Kind).ToArray());

        var queue = new ActionQueueCompiler().Compile(plan, snapshot, ledger);
        Assert.Equal("pending", queue.Status);
        var load = queue.Items.Single(item =>
            item.OptionId == "executor.load_machine_input");
        Assert.Equal("ready_no_active_material_reservations", Parameter(
            load.NormalizedCommand.Parameters,
            "material_reservation_guard_status"));
        var dispatch = new ActionQueueDispatchReadinessService().Evaluate(
            queue,
            load,
            ledger,
            snapshot.StateHash);
        Assert.True(dispatch.Ready, string.Join(";", dispatch.BlockingReasons));
    }

    [Fact]
    public void SupportedMachineInputRejectsQuantityReservedForAnotherGoal()
    {
        var family = (MachineFamily)Families
            .Single(row => ((MachineFamily)row[0]).DisplayName == "Keg")[0];
        var snapshot = Snapshot(MaterialGraphSnapshot(family));
        var ledger = MachineSupportLedger(family);
        ledger.MaterialReservations =
        [
            new MaterialReservation
            {
                ReservationId = "reserve:keg-input",
                Revision = 1,
                Status = StrategyCommitmentStatuses.Active,
                NodeId = "player:123",
                SlotIndex = 0,
                QualifiedItemId = family.InputQualifiedId,
                Quantity = 2,
                Purpose = "another committed goal"
            }
        ];

        var candidate = Assert.Single(
            new CandidateOptionAvailabilityEvaluator()
                .Evaluate(
                    snapshot,
                    new[] { "farm.load_supported_machine_input" },
                    commitmentLedger: ledger)
                .Options[0]
                .EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains(
            "machine_input_reserved_for_other_goal",
            candidate.BlockReasons);
        Assert.Equal("blocked", Parameter(
            candidate.Parameters,
            "material_reservation_guard_status"));
        Assert.Equal("[\"reserve:keg-input\"]", Parameter(
            candidate.Parameters,
            "material_reservation_ids_json"));
    }

    [Fact]
    public void SupportedMachineLoadRejectsNonPositiveCurrentInput()
    {
        var family = (MachineFamily)Families
            .Single(row =>
                ((MachineFamily)row[0]).DisplayName == "Keg")[0];
        var nonPositiveFamily = family with
        {
            OutputSalePrice = family.InputSalePrice
        };
        var snapshot = Snapshot(
            FamilySnapshot(nonPositiveFamily));
        var candidate = Assert.Single(
            new CandidateOptionAvailabilityEvaluator()
                .Evaluate(
                    snapshot,
                    new[] { "farm.process_machines" },
                    includeExecutorCalibrationOptions: true,
                    MachineSupportLedger(family))
                .Options[0].EventCandidates.Where(row =>
                    row.Kind == "load_machine_input_tile" &&
                    row.TileX == 66 &&
                    row.TileY == 15));

        Assert.False(candidate.Available);
        Assert.Contains(
            "machine_support_current_input_not_positive",
            candidate.BlockReasons);
        Assert.Equal(
            "blocked_current_input_not_positive",
            Parameter(
                candidate.Parameters,
                "machine_support_continuation_status"));
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void SixMachineFamiliesExcludeBlockedInvalidAndUnreachableCasesUpstream(MachineFamily family)
    {
        var snapshot = Snapshot(BlockedFamilySnapshot(family));
        var candidates = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, includeExecutorCalibrationOptions: true)
            .Options[0]
            .EventCandidates;

        var busyLoad = Assert.Single(candidates.Where(candidate => candidate.CandidateId.StartsWith("machine-input:Farm:70,15", StringComparison.Ordinal)));
        Assert.False(busyLoad.Available);
        Assert.Contains("machine_input_target_busy", busyLoad.BlockReasons);

        Assert.DoesNotContain(candidates, candidate =>
            candidate.CandidateId.StartsWith("machine-input:Farm:72,15", StringComparison.Ordinal) &&
            candidate.QualifiedItemId == family.InputQualifiedId);

        var unavailableOutput = Assert.Single(candidates.Where(candidate => candidate.CandidateId.StartsWith("machine-output:Farm:74,15", StringComparison.Ordinal)));
        Assert.False(unavailableOutput.Available);
        Assert.Contains("machine_output_item_unavailable", unavailableOutput.BlockReasons);

        var fullInventoryOutput = Assert.Single(candidates.Where(candidate => candidate.CandidateId.StartsWith("machine-output:Farm:76,15", StringComparison.Ordinal)));
        Assert.False(fullInventoryOutput.Available);
        Assert.Contains("machine_output_inventory_cannot_accept_item", fullInventoryOutput.BlockReasons);

        var unreachableOutput = Assert.Single(candidates.Where(candidate => candidate.CandidateId.StartsWith("machine-output:Farm:80,15", StringComparison.Ordinal)));
        Assert.False(unreachableOutput.Available);
        Assert.Contains("machine_adjacent_stand_tile_occupied_by_machine", unreachableOutput.BlockReasons);

        var unreachableLoad = Assert.Single(candidates.Where(candidate => candidate.CandidateId.StartsWith("machine-input:Farm:80,15", StringComparison.Ordinal)));
        Assert.False(unreachableLoad.Available);
        Assert.Contains("machine_adjacent_stand_tile_occupied_by_machine", unreachableLoad.BlockReasons);
    }

    [Fact]
    public void MachineInputWithoutExecutionSemanticsIsBlockedUpstream()
    {
        var family = (MachineFamily)Families.First()[0];
        var row = MachineRow(64, 15, family, ready: false, minutes: -1, held: false, loadable: true)
            .Replace(
                "\"machine_execution_semantics\":{\"status\":\"available\",\"execution_status\":\"available_data_driven\",\"input_dispatch_kind\":\"base_object_data_driven\",\"prediction_training_status\":\"exact_current_snapshot_probe_supported\"},",
                string.Empty);
        var snapshot = Snapshot(BaseSnapshot(
            "63",
            "15",
            """[{"slot_index":0,"item_id":"24","qualified_item_id":"(O)24","stack":2,"quality":0,"maximum_stack_size":999,"is_empty":false}]""",
            "1",
            "1",
            "true",
            row));

        var candidate = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, includeExecutorCalibrationOptions: true)
            .Options[0]
            .EventCandidates
            .Single(candidate => candidate.Kind == "load_machine_input_tile");

        Assert.False(candidate.Available);
        Assert.Contains("machine_execution_semantics_not_supported", candidate.BlockReasons);
    }

    private static string FamilySnapshot(MachineFamily family)
    {
        return BaseSnapshot(
            "63", "15",
            """
            [{"slot_index":0,"item_id":"INPUT_ID","qualified_item_id":"INPUT_QID","stack":2,"quality":0,"maximum_stack_size":999,"is_empty":false},{"slot_index":1,"is_empty":true}]
            """
            .Replace("INPUT_ID", family.InputItemId)
            .Replace("INPUT_QID", family.InputQualifiedId),
            "2", "1", "true",
            MachineRow(64, 15, family, ready: true, minutes: 0, held: true, loadable: false) + "," +
            MachineRow(66, 15, family, ready: false, minutes: -1, held: false, loadable: true));
    }

    private static string MaterialGraphSnapshot(MachineFamily family)
    {
        return FamilySnapshot(family).Replace(
            "\"machines\": {\"value\"",
            "\"material_inventory_graph\": {\"value\":{" +
            "\"schema_version\":\"material_inventory_graph.v1\"," +
            "\"status\":\"available\",\"player_id\":123," +
            "\"inventory_nodes\":[{\"node_id\":\"player:123\"," +
            "\"inventory_kind\":\"player\",\"supply_state\":\"available\"," +
            "\"actor_use_authorized\":true,\"slots\":[{" +
            "\"slot_index\":0,\"qualified_item_id\":\"" +
            family.InputQualifiedId + "\",\"stack\":2}]}]}" +
            ",\"status\":\"available\",\"source\":{" +
            "\"kind\":\"game_object\",\"path\":\"test\"}," +
            "\"adapter\":\"test\",\"read_at_tick\":1,\"confidence\":1}," +
            "\"machines\": {\"value\"",
            StringComparison.Ordinal);
    }

    private static string BlockedFamilySnapshot(MachineFamily family)
    {
        return BaseSnapshot(
            "69", "15",
            """
            [{"slot_index":0,"item_id":"390","qualified_item_id":"(O)390","stack":999,"quality":0,"maximum_stack_size":999,"is_empty":false},{"slot_index":1,"item_id":"382","qualified_item_id":"(O)382","stack":999,"quality":0,"maximum_stack_size":999,"is_empty":false}]
            """,
            "2", "0", "false",
            MachineRow(70, 15, family, ready: false, minutes: 10, held: false, loadable: true) + "," +
            MachineRow(72, 15, family, ready: false, minutes: -1, held: false, loadable: false) + "," +
            MachineRow(74, 15, family, ready: true, minutes: 0, held: false, loadable: false) + "," +
            MachineRow(76, 15, family, ready: true, minutes: 0, held: true, loadable: false) + "," +
            MachineRow(80, 15, family, ready: true, minutes: 0, held: true, loadable: true) + "," +
            Occupier(79, 15) + "," + Occupier(81, 15) + "," + Occupier(80, 14) + "," + Occupier(80, 16));
    }

    private static string BaseSnapshot(string playerX, string playerY, string inventory, string occupiedStacks, string emptySlots, string hasEmptySlot, string machines)
    {
        return """
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":PLAYER_X,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":PLAYER_Y,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"occupied_stacks":OCCUPIED_STACKS,"empty_slots":EMPTY_SLOTS,"has_empty_slot":HAS_EMPTY_SLOT},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":INVENTORY,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "machines": {"value":[MACHINES],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "map": {"value":{"location_id":"Farm","width":100,"height":100},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
        .Replace("PLAYER_X", playerX)
        .Replace("PLAYER_Y", playerY)
        .Replace("OCCUPIED_STACKS", occupiedStacks)
        .Replace("EMPTY_SLOTS", emptySlots)
        .Replace("HAS_EMPTY_SLOT", hasEmptySlot)
        .Replace("INVENTORY", inventory)
        .Replace("MACHINES", machines);
    }

    private static string MachineRow(int x, int y, MachineFamily family, bool ready, int minutes, bool held, bool loadable)
    {
        return """
        {"location_id":"Farm","location_kind":"farm_outdoor","machine_has_input":true,"tile_x":TILE_X,"tile_y":TILE_Y,"qualified_item_id":"MACHINE_QID","display_name":"MACHINE_NAME","ready_for_harvest":READY,"minutes_until_ready":MINUTES,"machine_execution_semantics":{"status":"available","execution_status":"available_data_driven","input_dispatch_kind":"base_object_data_driven","prediction_training_status":"exact_current_snapshot_probe_supported"},"harvest_experience_raw":"","harvest_experience_entries":[],"harvest_experience_deltas":[],"harvest_experience_deltas_json":"[]","harvest_mastery_experience_delta":0,"harvest_experience_projection_status":"exact_no_configured_experience","machine_data":{"status":"available","has_output":true,"additional_consumed_item_count":0,"output_rule_count":1,"output_rules":[{"id":"family_rule","required_item_id":"INPUT_QID","minutes_until_ready":DURATION,"output_item":{"item_id":"OUTPUT_ID","qualified_item_id":"OUTPUT_QID","stack":1,"sale_price":OUTPUT_PRICE}}]},"held_item":HELD,"loadable_inputs":LOADABLE}
        """
        .Replace("TILE_X", x.ToString())
        .Replace("TILE_Y", y.ToString())
        .Replace("MACHINE_QID", family.MachineQualifiedId)
        .Replace("MACHINE_NAME", family.DisplayName)
        .Replace("READY", ready.ToString().ToLowerInvariant())
        .Replace("MINUTES", minutes.ToString())
        .Replace("INPUT_QID", family.InputQualifiedId)
        .Replace("DURATION", family.MinutesUntilReady.ToString())
        .Replace("OUTPUT_ID", family.OutputItemId)
        .Replace("OUTPUT_QID", family.OutputQualifiedId)
        .Replace("OUTPUT_PRICE", family.OutputSalePrice.ToString())
        .Replace("HELD", held ? HeldItem(family) : "null")
        .Replace("LOADABLE", loadable ? LoadableInput(family) : "[]");
    }

    private static string HeldItem(MachineFamily family)
    {
        return """
        {"item_id":"OUTPUT_ID","qualified_item_id":"OUTPUT_QID","stack":1,"quality":0,"sale_price":OUTPUT_PRICE,"maximum_stack_size":999}
        """
        .Replace("OUTPUT_ID", family.OutputItemId)
        .Replace("OUTPUT_QID", family.OutputQualifiedId)
        .Replace("OUTPUT_PRICE", family.OutputSalePrice.ToString());
    }

    private static string LoadableInput(MachineFamily family)
    {
        return """
        [{"slot_index":0,"item_id":"INPUT_ID","qualified_item_id":"INPUT_QID","stack":2,"quality":0,"sale_price":INPUT_PRICE,"predicted_output":{"status":"available","training_eligibility_status":"exact_current_snapshot_probe_supported","source":"MachineDataUtility.GetOutputItem(probe:true)","matched_rule_id":"family_rule","required_item_id":"INPUT_QID","required_count":1,"additional_consumed_item_count":0,"effective_minutes_until_ready":DURATION,"item":{"item_id":"OUTPUT_ID","qualified_item_id":"OUTPUT_QID","stack":1,"quality":0,"sale_price":OUTPUT_PRICE},"sale_price":OUTPUT_PRICE,"stack":1,"quality":0},"probe_source":"Object.performObjectDropInAction(probe:true)","load_executor_status":"covered_for_runtime_load"}]
        """
        .Replace("INPUT_ID", family.InputItemId)
        .Replace("INPUT_QID", family.InputQualifiedId)
        .Replace("INPUT_PRICE", family.InputSalePrice.ToString())
        .Replace("DURATION", family.MinutesUntilReady.ToString())
        .Replace("OUTPUT_ID", family.OutputItemId)
        .Replace("OUTPUT_QID", family.OutputQualifiedId)
        .Replace("OUTPUT_PRICE", family.OutputSalePrice.ToString());
    }

    private static string Occupier(int x, int y)
    {
        return """
        {"location_id":"Farm","location_kind":"farm_outdoor","machine_has_input":true,"tile_x":TILE_X,"tile_y":TILE_Y,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":false,"minutes_until_ready":10,"held_item":null,"loadable_inputs":[]}
        """
        .Replace("TILE_X", x.ToString())
        .Replace("TILE_Y", y.ToString());
    }

    private static SnapshotEnvelope Snapshot(string stateJson)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stateJson, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-13T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static StrategyCommitmentLedger MachineSupportLedger(
        MachineFamily family) => new()
        {
            LedgerId = "ledger:machine-support",
            Revision = 2,
            SourceStateHash = "state:placement",
            MachineSupportIntents =
            [
                new MachineSupportIntent
                {
                    IntentId = "machine-support:money:keg",
                    Revision = 2,
                    Status = StrategyCommitmentStatuses.Active,
                    Stage =
                        MachineSupportIntentStages.PlacementBound,
                    SourceDecisionId = "machine-place:keg",
                    SourceStateHash = "state:placement",
                    GoalId = "goal.economy.earn_money",
                    QualifiedItemId = family.MachineQualifiedId,
                    ItemId = family.MachineQualifiedId[
                        "(BC)".Length..],
                    DemandClass =
                        "production_capacity_requirement",
                    SupportKind =
                        "machine_capacity_current_backlog",
                    EvidenceStatus = "complete",
                    GrossBenefit = 400,
                    OpportunityCost = 60,
                    NetBenefit = 340,
                    SupportScore = 0.034,
                    RequiredAdditionalMachineCount = 1,
                    TargetLocationId = "Farm",
                    TargetTileX = 66,
                    TargetTileY = 15
                }
            ]
        };

    private static string Parameter(
        IEnumerable<SmallModelActionParameter> parameters,
        string name) =>
        parameters.Single(parameter =>
            parameter.Name == name).Value;

    public sealed record MachineFamily(
        string DisplayName,
        string MachineQualifiedId,
        string InputItemId,
        string InputQualifiedId,
        string OutputItemId,
        string OutputQualifiedId,
        int InputSalePrice,
        int OutputSalePrice,
        int MinutesUntilReady);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

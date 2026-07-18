using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
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
        {"location_id":"Farm","location_kind":"farm_outdoor","machine_has_input":true,"tile_x":TILE_X,"tile_y":TILE_Y,"qualified_item_id":"MACHINE_QID","display_name":"MACHINE_NAME","ready_for_harvest":READY,"minutes_until_ready":MINUTES,"harvest_experience_raw":"","harvest_experience_entries":[],"harvest_experience_deltas":[],"harvest_experience_deltas_json":"[]","harvest_mastery_experience_delta":0,"harvest_experience_projection_status":"exact_no_configured_experience","machine_data":{"status":"available","has_output":true,"output_rule_count":1,"output_rules":[{"id":"family_rule","required_item_id":"INPUT_QID","minutes_until_ready":DURATION,"output_item":{"item_id":"OUTPUT_ID","qualified_item_id":"OUTPUT_QID","stack":1,"sale_price":OUTPUT_PRICE}}]},"held_item":HELD,"loadable_inputs":LOADABLE}
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
        [{"slot_index":0,"item_id":"INPUT_ID","qualified_item_id":"INPUT_QID","stack":2,"quality":0,"sale_price":INPUT_PRICE,"predicted_output":{"status":"available","source":"MachineDataUtility.GetOutputItem(probe:true)","matched_rule_id":"family_rule","required_item_id":"INPUT_QID","effective_minutes_until_ready":DURATION,"item":{"item_id":"OUTPUT_ID","qualified_item_id":"OUTPUT_QID","stack":1,"quality":0,"sale_price":OUTPUT_PRICE},"sale_price":OUTPUT_PRICE,"stack":1,"quality":0},"probe_source":"Object.performObjectDropInAction(probe:true)"}]
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

using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class StorageCraftingMainlineTests
{
    [Fact]
    public void NoOrdinaryStorageFlowsThroughNativeCraftingChain()
    {
        var snapshot = Snapshot();
        var availability =
            new CandidateOptionAvailabilityEvaluator()
                .Evaluate(
                    snapshot,
                    new[] { "farm.process_machines" },
                    includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(
            availability.Options[0].EventCandidates.Where(
                row => row.Kind == "craft_storage_item"));

        Assert.True(
            candidate.Available,
            string.Join(";", candidate.BlockReasons));
        Assert.Equal(
            "bootstrap_ordinary_storage",
            Parameter(
                candidate.Parameters,
                "storage_demand_class"));
        Assert.Equal(
            "(BC)130",
            candidate.QualifiedItemId);

        var ranked = new EventCandidateRanker()
            .Rank(
                new BaselineTrainingReport(),
                availability)
            .Where(row =>
                row.CandidateId == candidate.CandidateId)
            .ToArray();
        var plan = new DailyPlanCompiler().Compile(
            ranked,
            snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("craft_storage_item", step.Kind);

        var queue = new ActionQueueCompiler().Compile(
            plan,
            snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal(
            "executor.craft_storage_item",
            item.OptionId);
        Assert.Equal("pending", item.Status);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal(
            "craft_storage_item",
            Assert.Single(
                item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void InventoryStorageSuppressesDuplicateAcquisition()
    {
        var snapshot = Mutate(
            Snapshot(),
            root =>
            {
                root["player"]!["storage_placement"]![
                    "value"]!["inventory_storage_count"] = 1;
                root["player"]!["storage_placement"]![
                    "value"]!["rows"] = JsonNode.Parse(
                    """[{"ordinary_material_storage":true}]""");
            });

        var candidate = Assert.Single(
            new CandidateOptionAvailabilityEvaluator()
                .Evaluate(
                    snapshot,
                    new[] { "farm.process_machines" },
                    true)
                .Options[0].EventCandidates.Where(
                    row => row.Kind ==
                        "craft_storage_item"));

        Assert.False(candidate.Available);
        Assert.Contains(
            "storage_item_already_in_inventory_requires_placement",
            candidate.BlockReasons);
    }

    [Fact]
    public void ExistingFreeOrdinaryCapacitySuppressesAcquisition()
    {
        var snapshot = Mutate(
            Snapshot(),
            root =>
            {
                root["farm"]!["chests"]!["value"] =
                    JsonNode.Parse(StorageJson(
                        occupied: 1,
                        free: 35));
                root["farm"]!["material_inventory_graph"]![
                    "value"] = JsonNode.Parse(GraphJson(
                        occupied: 1));
            });

        var demand =
            StorageExpansionDemandProjection.Evaluate(
                snapshot);

        Assert.Equal("available", demand.Status);
        Assert.Equal(
            "ordinary_storage_capacity_available",
            demand.DemandClass);
        Assert.False(demand.AcquisitionRequired);
        Assert.Equal(
            35,
            demand.ImmediatelyUsableOrdinaryFreeStackSlotCount);
    }

    [Fact]
    public void CapacityExhaustionRequiresAnotherOrdinaryStorage()
    {
        var snapshot = Mutate(
            Snapshot(),
            root =>
            {
                root["farm"]!["chests"]!["value"] =
                    JsonNode.Parse(StorageJson(
                        occupied: 36,
                        free: 0));
                root["farm"]!["material_inventory_graph"]![
                    "value"] = JsonNode.Parse(GraphJson(
                        occupied: 36));
            });

        var demand =
            StorageExpansionDemandProjection.Evaluate(
                snapshot);

        Assert.True(demand.AcquisitionRequired);
        Assert.Equal(
            "ordinary_storage_capacity_exhausted",
            demand.DemandClass);
    }

    [Fact]
    public void CompilerRejectsStorageDemandDrift()
    {
        var initial = Snapshot();
        var availability =
            new CandidateOptionAvailabilityEvaluator()
                .Evaluate(
                    initial,
                    new[] { "farm.process_machines" },
                    true);
        var candidate = Assert.Single(
            availability.Options[0].EventCandidates.Where(
                row => row.Kind ==
                    "craft_storage_item"));
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker()
                .Rank(
                    new BaselineTrainingReport(),
                    availability)
                .Where(row =>
                    row.CandidateId ==
                    candidate.CandidateId)
                .ToArray(),
            initial.StateHash);
        var drifted = Mutate(
            initial,
            root =>
                root["player"]!["storage_placement"]![
                    "value"]!["rows"] = JsonNode.Parse(
                    """[{"ordinary_material_storage":true}]"""));
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(
            plan,
            drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "craft_storage_item_demand_projection_drifted",
            Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void StorageCraftDispatchBindsLatestStrategyLedger()
    {
        var snapshot = Snapshot();
        var ledger = new StrategyCommitmentLedger
        {
            LedgerId = "strategy-ledger:storage",
            Revision = 7
        };
        var availability =
            new CandidateOptionAvailabilityEvaluator()
                .Evaluate(
                    snapshot,
                    new[] { "farm.process_machines" },
                    true,
                    ledger);
        var candidate = Assert.Single(
            availability.Options[0].EventCandidates.Where(
                row => row.Kind ==
                    "craft_storage_item"));
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker()
                .Rank(
                    new BaselineTrainingReport(),
                    availability)
                .Where(row =>
                    row.CandidateId ==
                    candidate.CandidateId)
                .ToArray(),
            snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(
            plan,
            snapshot,
            ledger);
        var item = Assert.Single(queue.Items);

        var readiness =
            new ActionQueueDispatchReadinessService()
                .Evaluate(
                    queue,
                    item,
                    ledger,
                    snapshot.StateHash);

        Assert.True(
            readiness.Ready,
            string.Join(";", readiness.BlockingReasons));
        Assert.Equal("ready", readiness.Status);
    }

    private static SnapshotEnvelope Snapshot()
    {
        var stateJson = """
        {
          "player":{
            "location_id":{"value":"FarmHouse","status":"available"},
            "inventory":{"value":[{"slot_index":0,"item_id":"388","qualified_item_id":"(O)388","stack":50,"quality":0,"maximum_stack_size":999,"is_empty":false}],"status":"available"},
            "inventory_capacity":{"value":{"empty_slots":1,"has_empty_slot":true},"status":"available"},
            "storage_placement":{"value":{"schema_version":"storage_placement.v1","projection_status":"complete_inventory_player_chests_across_persistent_player_locations","inventory_storage_count":0,"rows":[]},"status":"available"},
            "storage_crafting":{"value":{"schema_version":"storage_crafting.v1","projection_status":"complete_known_storage_recipe_projection","rows":[{
              "recipe_name":"Chest","times_crafted":0,"output_selection_status":"exact_single_native_storage_output",
              "output_item_id":"130","output_qualified_item_id":"(BC)130","output_count_per_craft":1,
              "native_storage_branch":"native_object_placement_normal_chest","special_chest_type":"None","actual_capacity":36,
              "ordinary_material_storage":true,
              "ingredient_rows":[{"requirement_id_or_category":"388","required_count":50,"available_count_before_this_ingredient":50,"satisfied":true,"reverse_slot_consumption_plan":[{"slot_index":0,"qualified_item_id":"(O)388","amount":50}]}],
              "workbench_crafting_sources":[],"has_ingredients_for_one":true,"craftable_count_from_player_inventory":1,
              "output_inventory_acceptance_after_material_consumption":true,
              "craft_candidate_status":"ready_for_native_personal_crafting_menu"
            }]},"status":"available"}
          },
          "farm":{
            "machines":{"value":[],"status":"available"},
            "chests":{"value":{
              "schema_version":"storage_infrastructure.v1","status":"available","source_graph_schema_version":"material_inventory_graph.v1",
              "source_graph_player_id":123,"inventory_node_reference":"farm.material_inventory_graph.inventory_nodes[node_id]",
              "access_points":[],"access_point_count":0,"distinct_inventory_node_count":0,"actor_authorized_access_point_count":0,
              "locked_access_point_count":0,"removable_empty_access_point_count":0,"nonempty_shove_access_point_count":0,
              "content_duplication_policy":"reference_canonical_material_graph_nodes"
            },"status":"available"},
            "material_inventory_graph":{"value":{"schema_version":"material_inventory_graph.v1","status":"available","player_id":123,"inventory_nodes":[]},"status":"available"}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}}
        }
        """;
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(stateJson)!;
        return Envelope(state);
    }

    private static SnapshotEnvelope Mutate(
        SnapshotEnvelope source,
        Action<JsonObject> mutation)
    {
        var root = JsonNode.Parse(
            JsonSerializer.Serialize(source.State))!.AsObject();
        mutation(root);
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(
            root.ToJsonString())!;
        return Envelope(state);
    }

    private static SnapshotEnvelope Envelope(
        Dictionary<string, JsonElement> state) => new()
    {
        SchemaVersion = "snapshot.v1",
        StateHash = SnapshotHash.ComputeStateHash(state),
        GameTick = 1,
        RealTimestamp = "2026-07-25T00:00:00Z",
        Completeness = "complete",
        State = state
    };

    private static string StorageJson(
        int occupied,
        int free) => $$"""
        {
          "schema_version":"storage_infrastructure.v1","status":"available",
          "source_graph_schema_version":"material_inventory_graph.v1","source_graph_player_id":123,
          "inventory_node_reference":"farm.material_inventory_graph.inventory_nodes[node_id]",
          "access_points":[{
            "access_point_id":"access:placed_chest:Farm:1,1","node_id":"chest:Farm:1,1","access_kind":"placed_chest",
            "location_id":"Farm","location_kind":"farm","tile_x":1,"tile_y":1,"qualified_item_id":"(BC)130",
            "special_chest_type":"None","capacity":36,"occupied_slot_count":{{occupied}},"free_slot_count":{{free}},
            "is_player_chest":true,"is_fridge":false,"actor_use_authorized":true,"locked_by_other_player":false,
            "relocation_status":"native_shove_available_nonempty"
          }],
          "access_point_count":1,"distinct_inventory_node_count":1,"actor_authorized_access_point_count":1,
          "locked_access_point_count":0,"removable_empty_access_point_count":0,"nonempty_shove_access_point_count":1,
          "content_duplication_policy":"reference_canonical_material_graph_nodes"
        }
        """;

    private static string GraphJson(int occupied)
    {
        var slots = string.Join(
            ",",
            Enumerable.Range(0, occupied).Select(
                index => $$"""
                {"slot_index":{{index}},"item_id":"388","qualified_item_id":"(O)388","stack":1}
                """));
        return $$"""
        {
          "schema_version":"material_inventory_graph.v1","status":"available","player_id":123,
          "inventory_nodes":[{
            "node_id":"chest:Farm:1,1","inventory_kind":"chest","supply_state":"available",
            "owner_player_id":123,"ownership_class":"actor_owned","actor_use_authorized":true,
            "capacity":36,"slots":[{{slots}}]
          }]
        }
        """;
    }

    private static string Parameter(
        IEnumerable<
            StardewAI.Contracts.Execution.SmallModelActionParameter>
            parameters,
        string name) =>
        parameters.Single(row => row.Name == name).Value;
}

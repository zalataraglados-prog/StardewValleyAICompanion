using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class MachineTaskCapacityMainlineTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactOrdinaryAndSpecialTaskDemandSelectsExistingCraftChain(
        bool specialOrder)
    {
        var snapshot = Snapshot(specialOrder);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                ["farm.establish_supported_machine_capacity"],
                true,
                Ledger());
        var candidate = Assert.Single(
            Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("craft_machine_item", candidate.Kind);
        Assert.Equal(
            "priority_task_requirement",
            Parameter(candidate.Parameters, "machine_demand_class"));
        Assert.Equal(
            "active_collection_task",
            Parameter(
                candidate.Parameters,
                "material_reservation_request_class"));
        Assert.Equal(
            "300",
            Parameter(
                candidate.Parameters,
                "material_reservation_request_priority"));

        var ranked = Assert.Single(new EventCandidateRanker().Rank(
            new(),
            availability,
            "goal.grandpa_max_score_year3"));
        Assert.Equal(
            ExplicitGoalSupportProjection.TaskSupportStatus,
            Parameter(ranked.Parameters, "goal_support_status"));
        Assert.StartsWith(
            "machine-support:goal.grandpa_max_score_year3:",
            Parameter(ranked.Parameters, "machine_support_intent_id"));
    }

    [Fact]
    public void BoundTaskCraftReusesTheExistingCompiler()
    {
        var snapshot = Snapshot(specialOrder: false);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                ["farm.establish_supported_machine_capacity"],
                true,
                Ledger());
        var ranked = Assert.Single(new EventCandidateRanker().Rank(
            new(),
            availability,
            "goal.grandpa_max_score_year3"));
        var plan = new DailyPlanCompiler().Compile(
            [ranked],
            snapshot.StateHash,
            "goal.grandpa_max_score_year3");
        var step = Assert.Single(plan.Steps);
        var intentId = Parameter(
            step.Parameters,
            "machine_support_intent_id");
        var boundLedger = new StrategyCommitmentLedger
        {
            LedgerId = "ledger:task-capacity",
            Revision = 2,
            SourceStateHash = snapshot.StateHash,
            MachineSupportIntents =
            [
                new MachineSupportIntent
                {
                    IntentId = intentId,
                    Revision = 1,
                    Status = StrategyCommitmentStatuses.Active,
                    Stage = MachineSupportIntentStages.CraftSelected,
                    SourceStateHash = snapshot.StateHash,
                    GoalId = "goal.grandpa_max_score_year3",
                    QualifiedItemId = "(BC)12",
                    ItemId = "12",
                    DemandClass = "priority_task_requirement",
                    SupportKind =
                        "machine_capacity_active_collection_task",
                    EvidenceStatus =
                        "[\"ordinary_quest:ResourceCollectionQuest:96\"]",
                    TaskSourcesJson =
                        "[\"ordinary_quest:ResourceCollectionQuest:96\"]",
                    SupportScore = 0.12,
                    RequiredAdditionalMachineCount = 1
                }
            ]
        };
        Set(step, "commitment_ledger_id", boundLedger.LedgerId);
        Set(step, "commitment_ledger_revision", "2");
        Set(step, "material_reservation_ledger_id", boundLedger.LedgerId);
        Set(step, "material_reservation_ledger_revision", "2");
        Set(step, "machine_support_intent_revision", "1");
        Set(
            step,
            "machine_support_intent_stage",
            MachineSupportIntentStages.CraftSelected);
        Set(
            step,
            "machine_support_intent_source_state_hash",
            snapshot.StateHash);

        var queue = new ActionQueueCompiler().Compile(
            plan,
            snapshot,
            boundLedger);

        Assert.Equal("pending", queue.Status);
        Assert.Equal(
            "executor.craft_machine_item",
            Assert.Single(queue.Items).OptionId);
    }

    [Fact]
    public void SpecialOrderMatchesPredictedOutputTagsNotMachineTags()
    {
        var accepted = Assert.Single(Candidate(Snapshot(specialOrder: true)));
        var rejected = Candidate(Snapshot(
            specialOrder: true,
            predictedOutputTag: "not_artisan_good"));

        Assert.Equal("craft_machine_item", accepted.Kind);
        Assert.DoesNotContain(rejected, candidate => string.Equals(
            Parameter(candidate.Parameters, "machine_demand_class"),
            "priority_task_requirement",
            StringComparison.Ordinal));
    }

    [Fact]
    public void ExistingPlacedMachineSuppressesDuplicateTaskCapacityBuild()
    {
        var candidates = Candidate(Snapshot(
            specialOrder: false,
            placedMachineCount: 1));

        Assert.DoesNotContain(candidates, candidate => string.Equals(
            Parameter(candidate.Parameters, "machine_demand_class"),
            "priority_task_requirement",
            StringComparison.Ordinal));
    }

    [Fact]
    public void InventoryMachineStartsTheExistingPlacementChainWithoutCrafting()
    {
        var snapshot = Snapshot(
            specialOrder: false,
            inventoryMachine: true);
        var ledger = Ledger();
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                ["farm.establish_supported_machine_capacity"],
                true,
                ledger);
        var candidate = Assert.Single(
            Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("place_machine_item", candidate.Kind);
        Assert.Equal("(BC)12", candidate.QualifiedItemId);
        Assert.Equal(
            "true",
            Parameter(
                candidate.Parameters,
                "machine_task_capacity_action_required"));
        Assert.Equal(
            "0",
            Parameter(candidate.Parameters, "placed_same_machine_count"));
        Assert.Equal(
            "1",
            Parameter(candidate.Parameters, "inventory_same_machine_count"));
        Assert.Equal(
            "1",
            Parameter(
                candidate.Parameters,
                "required_additional_machine_count"));
        var ranked = Assert.Single(new EventCandidateRanker().Rank(
            new(),
            availability,
            "goal.grandpa_max_score_year3"));
        Assert.Equal(
            ExplicitGoalSupportProjection.TaskSupportStatus,
            Parameter(ranked.Parameters, "goal_support_status"));
        Assert.EndsWith(
            ":fleet=0:required=1",
            Parameter(
                ranked.Parameters,
                "machine_support_intent_id"),
            StringComparison.Ordinal);

        var plan = new DailyPlanCompiler().Compile(
            [ranked],
            snapshot.StateHash,
            "goal.grandpa_max_score_year3");
        var placeStep = plan.Steps.Single(step =>
            step.Kind == "place_machine_item");
        var intent = new MachineSupportIntent
        {
            IntentId = Parameter(
                placeStep.Parameters,
                "machine_support_intent_id"),
            Revision = 1,
            Status = StrategyCommitmentStatuses.Active,
            Stage = MachineSupportIntentStages.PlacementBound,
            SourceStateHash = snapshot.StateHash,
            GoalId = "goal.grandpa_max_score_year3",
            QualifiedItemId = "(BC)12",
            ItemId = "12",
            DemandClass = "priority_task_requirement",
            SupportKind = "machine_capacity_active_collection_task",
            EvidenceStatus =
                "[\"ordinary_quest:ResourceCollectionQuest:96\"]",
            TaskSourcesJson =
                "[\"ordinary_quest:ResourceCollectionQuest:96\"]",
            SupportScore = 0.12,
            RequiredAdditionalMachineCount = 1,
            TargetLocationId = placeStep.TargetLocation,
            TargetTileX = placeStep.TargetTileX,
            TargetTileY = placeStep.TargetTileY
        };
        var boundLedger = new StrategyCommitmentLedger
        {
            LedgerId = ledger.LedgerId,
            Revision = 2,
            SourceStateHash = snapshot.StateHash,
            MachineSupportIntents = [intent]
        };
        SetOrAdd(placeStep, "commitment_ledger_id", boundLedger.LedgerId);
        SetOrAdd(placeStep, "commitment_ledger_revision", "2");
        SetOrAdd(
            placeStep,
            "material_reservation_ledger_id",
            boundLedger.LedgerId);
        SetOrAdd(
            placeStep,
            "material_reservation_ledger_revision",
            "2");
        foreach (var parameter in MachineSupportIntentProjection.Parameters(
                     MachineSupportIntentProjection.Placement(intent)))
        {
            SetOrAdd(placeStep, parameter.Name, parameter.Value);
        }

        var queue = new ActionQueueCompiler().Compile(
            plan,
            snapshot,
            boundLedger);
        var place = queue.Items.Single(item =>
            item.OptionId == "executor.place_machine");
        Assert.Equal("pending", place.Status);
        Assert.Empty(place.BlockingReasons);

        var root = JsonNode.Parse(
            JsonSerializer.Serialize(snapshot.State))!.AsObject();
        root["quests"]!["active_quests"]!["value"] =
            new JsonArray();
        var driftedState = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(root.ToJsonString())!;
        var drifted = new SnapshotEnvelope
        {
            SchemaVersion = snapshot.SchemaVersion,
            StateHash = SnapshotHash.ComputeStateHash(driftedState),
            GameTick = 2,
            RealTimestamp = snapshot.RealTimestamp,
            Completeness = snapshot.Completeness,
            State = driftedState
        };
        var blocked = new ActionQueueCompiler().Compile(
            plan,
            drifted,
            boundLedger);
        var blockedPlace = blocked.Items.Single(item =>
            item.OptionId == "executor.place_machine");
        Assert.Contains(
            "place_machine_task_support_demand_drifted",
            blockedPlace.BlockingReasons);
    }

    [Fact]
    public void AdditionalConsumptionFailsClosedForTaskCapacity()
    {
        var candidates = Candidate(Snapshot(
            specialOrder: false,
            additionalConsumedItemCount: 1));

        Assert.DoesNotContain(candidates, candidate => string.Equals(
            Parameter(candidate.Parameters, "machine_demand_class"),
            "priority_task_requirement",
            StringComparison.Ordinal));
    }

    [Fact]
    public void QuestForMachineItselfDoesNotImpersonateOutputDemand()
    {
        var candidates = Candidate(Snapshot(
            specialOrder: false,
            ordinaryRequiredItemId: "(BC)12"));

        Assert.DoesNotContain(candidates, candidate => string.Equals(
            Parameter(candidate.Parameters, "machine_demand_class"),
            "priority_task_requirement",
            StringComparison.Ordinal));
    }

    [Fact]
    public void MixedUnsupportedTaskSourceCannotBecomeExactTaskSupport()
    {
        var support = ExplicitGoalSupportProjection.Read(
            "craft_machine_item",
            "machine_demand_class=priority_task_requirement;" +
            "required_additional_machine_count=1;" +
            "priority_task_sources_json=[\"ordinary_quest:" +
            "ResourceCollectionQuest:96\",\"raccoon_bundle:" +
            "ingredient:0\"];material_reservation_request_priority=300;" +
            "material_reservation_request_class=active_collection_task",
            "goal.grandpa_max_score_year3");

        Assert.False(ExplicitGoalSupportProjection.IsSupported(support));
        Assert.Equal(
            "blocked_inexact_or_stale_task_capacity_demand",
            support.Status);
    }

    private static StardewAI.Contracts.Options.EventCandidate[] Candidate(
        SnapshotEnvelope snapshot) =>
        new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                ["farm.establish_supported_machine_capacity"],
                true,
                Ledger())
            .Options.Single().EventCandidates;

    private static StrategyCommitmentLedger Ledger() => new()
    {
        LedgerId = "ledger:task-capacity",
        Revision = 1
    };

    private static SnapshotEnvelope Snapshot(
        bool specialOrder,
        string predictedOutputTag = "artisan_good",
        int placedMachineCount = 0,
        bool inventoryMachine = false,
        int additionalConsumedItemCount = 0,
        string ordinaryRequiredItemId = "(O)346")
    {
        var taskState = specialOrder
            ? """
              "active_quests":{"value":[],"status":"available"},
              "special_orders":{"value":[{"quest_key":"MachineOrder","quest_state":"InProgress","objectives":[{"current_count":0,"max_count":1,"runtime_type":"CollectObjective","complete":false,"per_type_fields":{"available":true,"acceptable_context_tag_sets":["artisan_good"]}}]}],"status":"available"}
              """
            : """
              "active_quests":{"value":[{"id":"96","runtime_type":"ResourceCollectionQuest","accepted":true,"completed":false,"per_type_fields":{"available":true,"item_id":"ORDINARY_REQUIRED_ITEM_ID","number_collected":0,"number_required":1,"current_count":0,"target_count":1}}],"status":"available"},
              "special_orders":{"value":[],"status":"available"}
              """;
        var placementRows = inventoryMachine
            ? """
              [{"inventory_slot_index":4,"item_id":"12","qualified_item_id":"(BC)12","stack":1,"locations":[{"location_id":"AdventureGuild","location_is_current":false,"machine_operational_context_valid":true,"placement_probe_status":"native_legal_tiles_available","static_legal_tile_count":1,"static_legal_tile_ranges":[{"y":5,"start_x":7,"end_x":7}]},{"location_id":"FarmHouse","location_is_current":true,"machine_operational_context_valid":true,"placement_probe_status":"native_legal_tiles_available","static_legal_tile_count":2,"static_legal_tile_ranges":[{"y":5,"start_x":7,"end_x":8}]}]}]
              """
            : "[]";
        var inventoryMachineRow = inventoryMachine
            ? ",{" +
              "\"slot_index\":4,\"item_id\":\"12\",\"qualified_item_id\":\"(BC)12\",\"stack\":1,\"quality\":0,\"maximum_stack_size\":999,\"is_empty\":false}"
            : string.Empty;
        var graphMachineRow = inventoryMachine
            ? ",{" +
              "\"slot_index\":4,\"item_id\":\"12\",\"qualified_item_id\":\"(BC)12\",\"stack\":1}"
            : string.Empty;
        var machineRows = "[" + string.Join(",", Enumerable.Range(
            0,
            placedMachineCount).Select(index =>
                "{\"location_id\":\"Farm\",\"tile_x\":" +
                (20 + index) +
                ",\"tile_y\":20,\"qualified_item_id\":\"(BC)12\",\"minutes_until_ready\":0,\"ready_for_harvest\":false}")) + "]";
        var json = """
        {
          "player":{
            "location_id":{"value":"FarmHouse","status":"available"},
            "tile_x":{"value":6,"status":"available"},
            "tile_y":{"value":5,"status":"available"},
            "inventory":{"value":[{"slot_index":0,"item_id":"388","qualified_item_id":"(O)388","stack":30,"quality":0,"maximum_stack_size":999,"is_empty":false},{"slot_index":2,"item_id":"262","qualified_item_id":"(O)262","stack":2,"quality":0,"maximum_stack_size":999,"is_empty":false}INVENTORY_MACHINE_ROW],"status":"available"},
            "inventory_capacity":{"value":{"occupied_stacks":2,"empty_slots":10,"has_empty_slot":true},"status":"available"},
            "machine_placement":{"value":{"projection_status":"complete_all_inventory_machines_across_loaded_persistent_locations","static_projection_fingerprint":"task-capacity-layout","rows":PLACEMENT_ROWS},"status":"available"},
            "machine_crafting":{"value":{"projection_status":"complete_known_machine_recipe_projection","rows":[{
              "recipe_name":"Keg","times_crafted":2,"output_item_id":"12","output_qualified_item_id":"(BC)12","output_count_per_craft":1,"output_context_tags":["item_machine"],
              "output_machine_data":{"status":"available","additional_consumed_item_count":ADDITIONAL_COUNT,"output_rules":[]},
              "ingredient_rows":[{"required_count":30,"reverse_slot_consumption_plan":[{"slot_index":0,"qualified_item_id":"(O)388","amount":30,"unit_sale_price":2,"total_sale_value":60}]}],
              "potential_loadable_input_count":1,"potential_loadable_inputs":[{"slot_index":2,"item_id":"262","qualified_item_id":"(O)262","stack":2,"unit_sale_price":25,"accepting_contexts":[{"predicted_output":{"status":"available","training_eligibility_status":"exact_current_snapshot_probe_supported","additional_consumed_item_count":ADDITIONAL_COUNT,"effective_minutes_until_ready":2250,"required_count":1,"sale_price":200,"stack":1,"output_context_tags":["OUTPUT_TAG","id_o_346"],"item":{"item_id":"346","qualified_item_id":"(O)346","context_tags":["OUTPUT_TAG","id_o_346"]}}}]}],
              "output_inventory_acceptance_after_material_consumption":true,"craft_candidate_status":"ready_for_native_personal_crafting_menu"
            }]},"status":"available"}
          },
          "farm":{
            "crops":{"value":[],"status":"available"},
            "machines":{"value":MACHINE_ROWS,"status":"available"},
            "material_inventory_graph":{"value":{"schema_version":"material_inventory_graph.v1","status":"available","player_id":123,"inventory_nodes":[{"node_id":"player:123","supply_state":"available","actor_use_authorized":true,"slots":[{"slot_index":0,"item_id":"388","qualified_item_id":"(O)388","stack":30},{"slot_index":2,"item_id":"262","qualified_item_id":"(O)262","stack":2}GRAPH_MACHINE_ROW]}]},"status":"available"}
          },
          "quests":{TASK_STATE},
          "current_location":{"map":{"value":{"width":20,"height":20},"status":"available"}},
          "locations":{"collision_grid":{"value":{"location_id":"FarmHouse","width":20,"height":20,"notable_tiles":[]},"status":"available"},"route_action_branch_coverage":{"value":{"rows":[]},"status":"available"}},
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}},
          "time":{"time":{"value":600,"status":"available"},"total_days":{"value":10,"status":"available"}}
        }
        """
        .Replace("TASK_STATE", taskState, StringComparison.Ordinal)
        .Replace("PLACEMENT_ROWS", placementRows, StringComparison.Ordinal)
        .Replace("INVENTORY_MACHINE_ROW", inventoryMachineRow, StringComparison.Ordinal)
        .Replace("GRAPH_MACHINE_ROW", graphMachineRow, StringComparison.Ordinal)
        .Replace("MACHINE_ROWS", machineRows, StringComparison.Ordinal)
        .Replace("OUTPUT_TAG", predictedOutputTag, StringComparison.Ordinal)
        .Replace(
            "ORDINARY_REQUIRED_ITEM_ID",
            ordinaryRequiredItemId,
            StringComparison.Ordinal)
        .Replace(
            "ADDITIONAL_COUNT",
            additionalConsumedItemCount.ToString(),
            StringComparison.Ordinal);
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(json)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-05T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static string Parameter(
        IEnumerable<StardewAI.Contracts.Execution.SmallModelActionParameter>
            parameters,
        string name) =>
        parameters.Single(parameter => parameter.Name == name).Value;

    private static void Set(
        StardewAI.Contracts.Execution.SmallModelPlanStep step,
        string name,
        string value)
    {
        var parameter = step.Parameters.Single(row => row.Name == name);
        parameter.Value = value;
    }

    private static void SetOrAdd(
        StardewAI.Contracts.Execution.SmallModelPlanStep step,
        string name,
        string value)
    {
        var parameter = step.Parameters.FirstOrDefault(row =>
            row.Name == name);
        if (parameter is not null)
        {
            parameter.Value = value;
            return;
        }

        step.Parameters = step.Parameters.Append(
            new StardewAI.Contracts.Execution.SmallModelActionParameter
            {
                Name = name,
                Value = value
            }).ToArray();
    }
}

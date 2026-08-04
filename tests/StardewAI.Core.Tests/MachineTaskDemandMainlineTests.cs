using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class CandidateOptionAvailabilityEvaluatorTests
{
    [Fact]
    public void OrdinaryCollectionTaskBindsExactMachineInputAsSource()
    {
        var snapshot = MachineTaskSnapshot(specialOrder: false);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.fulfill_machine_task_demand" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("load_machine_input_tile", candidate.Kind);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_acquisition_source_step" &&
            parameter.Value == "true");
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "predicted_output_qualified_item_id" &&
            parameter.Value == "(O)346");

        var plan = CompilePlan(availability, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var load = Assert.Single(queue.Items.Where(item =>
            item.OptionId == "executor.load_machine_input"));

        Assert.Equal("pending", queue.Status);
        Assert.Empty(load.BlockingReasons);
        Assert.Contains(load.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "quest_candidate_id");
        Assert.Contains(load.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "predicted_output_context_tags_json" &&
            parameter.Value == "[\"artisan_good\",\"id_o_346\"]");
    }

    [Fact]
    public void SpecialOrderCollectBindsExactMachineInputTagsAsSource()
    {
        var snapshot = MachineTaskSnapshot(specialOrder: true);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.fulfill_machine_task_demand" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("load_machine_input_tile", candidate.Kind);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_family" &&
            parameter.Value == "special_order");

        var queue = new ActionQueueCompiler().Compile(
            CompilePlan(availability, snapshot.StateHash),
            snapshot);
        var load = Assert.Single(queue.Items.Where(item =>
            item.OptionId == "executor.load_machine_input"));

        Assert.Equal("pending", queue.Status);
        Assert.Empty(load.BlockingReasons);
        Assert.Contains(load.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "quest_acceptable_context_tag_sets_json" &&
            parameter.Value == "[\"artisan_good\"]");
    }

    [Fact]
    public void TaskMachineInputWithAdditionalConsumptionIsExcludedUpstream()
    {
        var snapshot = MachineTaskSnapshot(
            specialOrder: false,
            additionalConsumedItemCount: 1);
        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.fulfill_machine_task_demand" }, true)
            .Options);

        Assert.False(option.Available);
        Assert.Empty(option.EventCandidates);
        Assert.Contains("no_machine_task_demand_candidates", option.BlockingReasons);
    }

    [Fact]
    public void ActiveMaterialReservationBlocksTaskMachineInput()
    {
        var snapshot = MachineTaskSnapshot(specialOrder: false);
        var ledger = new StrategyCommitmentLedger
        {
            LedgerId = "ledger:task-machine",
            Revision = 4,
            MaterialReservations =
            [
                new MaterialReservation
                {
                    ReservationId = "reserved:wheat",
                    Status = StrategyCommitmentStatuses.Active
                }
            ]
        };
        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[] { "farm.fulfill_machine_task_demand" },
                true,
                ledger)
            .Options);
        var candidate = Assert.Single(option.EventCandidates);

        Assert.False(option.Available);
        Assert.False(candidate.Available);
        Assert.Contains(
            "task_machine_input_active_material_reservations_require_projection",
            candidate.BlockReasons);
    }

    [Fact]
    public void ReadyTaskMachineOutputUsesExistingReceiptChain()
    {
        var snapshot = ResourceCollectionSnapshot(MachineCollectionDomainState());
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.fulfill_machine_task_demand" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("collect_machine_output_tile", candidate.Kind);

        var queue = new ActionQueueCompiler().Compile(
            CompilePlan(availability, snapshot.StateHash),
            snapshot);
        Assert.Equal("pending", queue.Status);
        Assert.Single(queue.Items.Where(item =>
            item.OptionId == "executor.collect_machine_output"));
    }

    private static StardewAI.Contracts.Execution.SmallModelPlanEnvelope CompilePlan(
        StardewAI.Contracts.Options.OptionAvailabilityEnvelope availability,
        string stateHash) => new DailyPlanCompiler().Compile(
        new EventCandidateRanker().Rank(
            new StardewAI.Contracts.Training.BaselineTrainingReport(),
            availability),
        stateHash);

    private static StardewAI.Contracts.State.SnapshotEnvelope MachineTaskSnapshot(
        bool specialOrder,
        int additionalConsumedItemCount = 0)
    {
        var questState = specialOrder
            ? """
              "active_quests":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "special_orders":{"value":[{"quest_key":"MachineOrder","quest_name":"Machine Order","quest_state":"InProgress","objectives":[{"description":"Collect artisan good","current_count":0,"max_count":1,"runtime_type":"CollectObjective","fail_on_completion":false,"complete":false,"per_type_fields":{"available":true,"acceptable_context_tag_sets":["artisan_good"]}}],"rewards":[]}],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              """
            : """
              "active_quests":{"value":[{"id":"96","quest_type":10,"runtime_type":"ResourceCollectionQuest","accepted":true,"completed":false,"per_type_fields":{"available":true,"item_id":"(O)346","target_npc":"Robin","number_collected":0,"number_required":1,"target_count":1,"current_count":0}}],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "special_orders":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              """;
        return Snapshot(
            """
            {
              "player":{
                "location_id":{"value":"Farm","status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "tile_x":{"value":18,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "tile_y":{"value":20,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "energy":{"value":270,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "inventory":{"value":[{"slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":2,"quality":0,"maximum_stack_size":999,"is_empty":false}],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "inventory_capacity":{"value":{"occupied_stacks":1,"empty_slots":11,"has_empty_slot":true},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "farm":{"machines":{"value":[{
                "location_id":"Farm","location_kind":"farm_outdoor","tile_x":20,"tile_y":20,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":false,"minutes_until_ready":0,
                "machine_execution_semantics":{"status":"available","execution_status":"available_data_driven","input_dispatch_kind":"base_object_data_driven","prediction_training_status":"exact_current_snapshot_probe_supported"},
                "machine_data":{"status":"available","has_output":true,"additional_consumed_item_count":ADDITIONAL_COUNT,"output_rule_count":1},
                "held_item":null,
                "loadable_inputs":[{"slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":2,"quality":0,"sale_price":25,"predicted_output":{"status":"available","training_eligibility_status":"exact_current_snapshot_probe_supported","source":"MachineDataUtility.GetOutputItem(probe:true)","matched_rule_id":"keg_wheat","required_item_id":"(O)262","required_count":1,"additional_consumed_item_count":ADDITIONAL_COUNT,"effective_minutes_until_ready":2250,"output_context_tags":["artisan_good","id_o_346"],"item":{"item_id":"346","qualified_item_id":"(O)346","stack":1,"quality":0,"sale_price":200,"context_tags":["artisan_good","id_o_346"]},"sale_price":200,"stack":1,"quality":0},"probe_source":"Object.performObjectDropInAction(probe:true)","load_executor_status":"covered_for_runtime_load"}]
              }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
              "quests":{QUEST_STATE,"completed_special_orders":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},"accepted_special_order_types":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},"mail_received":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
              "time":{"time":{"value":1200,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
              "menus":{"active_menu":{"value":{"is_open":false},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
              "locations":{"collision_grid":{"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},"route_action_branch_coverage":{"value":{"rows":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
              "current_location":{"map":{"value":{"location_id":"Farm","width":100,"height":100},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
              "world_progress":{"community_center":{"value":{},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},"achievements":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}}
            }
            """
            .Replace("QUEST_STATE", questState, StringComparison.Ordinal)
            .Replace(
                "ADDITIONAL_COUNT",
                additionalConsumedItemCount.ToString(),
                StringComparison.Ordinal));
    }
}

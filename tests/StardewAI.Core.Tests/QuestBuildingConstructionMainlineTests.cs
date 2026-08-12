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

public sealed class QuestBuildingConstructionMainlineTests
{
    [Fact]
    public void ReadyHaveBuildingQuestCompilesToSoleNativeConstructionExecutor()
    {
        var snapshot = Snapshot("ready_for_native_carpenter_menu", 0);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "quest.advance" }, true);
        var candidate = Assert.Single(availability.Options[0].EventCandidates.Where(value => value.Kind == "construct_quest_building"));
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("Coop", Parameter(candidate, "construction_building_type"));
        Assert.Equal("7", Parameter(candidate, "building_tile_x"));

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability)
                .Where(value => value.CandidateId == candidate.CandidateId),
            snapshot.StateHash);
        Assert.Equal("construct_quest_building", Assert.Single(plan.Steps).Kind);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.True(item.Status == "pending", string.Join(";", item.BlockingReasons));
        Assert.Equal("executor.construct_building", item.OptionId);
        Assert.Equal("construct_building", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void ConstructionProjectionDriftBlocksDispatch()
    {
        var snapshot = Snapshot("ready_for_native_carpenter_menu", 0);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "quest.advance" }, true);
        var candidate = Assert.Single(availability.Options[0].EventCandidates.Where(value => value.Kind == "construct_quest_building"));
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability)
                .Where(value => value.CandidateId == candidate.CandidateId), snapshot.StateHash);
        var root = JsonNode.Parse(JsonSerializer.Serialize(snapshot.State))!.AsObject();
        root["player"]!["quest_building_construction"]!["value"]!["rows"]![0]!["placement_tile_x"] = 8;
        var drifted = Envelope(JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(root.ToJsonString())!);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);
        Assert.Equal("blocked", queue.Status);
        Assert.Contains("construct_building_projection_drifted", Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void InProgressConstructionUsesExistingRecoveryCandidate()
    {
        var snapshot = Snapshot("construction_in_progress", 3);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "quest.advance" }, true);
        var candidate = Assert.Single(availability.Options[0].EventCandidates.Where(value => value.Available));
        Assert.Equal("recovery_refresh_plan", candidate.Kind);
        Assert.Equal("3", Parameter(candidate, "construction_days_left"));
    }

    [Fact]
    public void ActiveMaterialReservationBlocksConstructionBeforePlanning()
    {
        var snapshot = Snapshot("ready_for_native_carpenter_menu", 0);
        var ledger = new StrategyCommitmentLedger
        {
            LedgerId = "strategy-ledger:test",
            Revision = 4,
            MaterialReservations = new[]
            {
                new MaterialReservation
                {
                    ReservationId = "reserved-wood",
                    Revision = 4,
                    Status = StrategyCommitmentStatuses.Active,
                    SourceDecisionId = "strategy.machine",
                    GoalId = "goal.machine",
                    OwnerPlayerId = 123,
                    NodeId = "player:123",
                    SlotIndex = 0,
                    QualifiedItemId = "(O)388",
                    Quantity = 100,
                    Purpose = "machine capacity"
                }
            }
        };

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "quest.advance" },
            true,
            ledger);
        var candidate = Assert.Single(availability.Options[0].EventCandidates
            .Where(value => value.Kind == "construct_quest_building"));

        Assert.False(candidate.Available);
        Assert.Contains(
            "machine_recipe_material_reserved_for_other_goal:player:123#0",
            candidate.BlockReasons);
        Assert.Equal("blocked", Parameter(candidate, "material_reservation_guard_status"));
        Assert.Contains("reserved-wood", Parameter(candidate, "material_reservation_ids_json"));
    }

    private static SnapshotEnvelope Snapshot(string actionStatus, int daysLeft)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>($$$"""
        {
          "player":{
            "location_id":{"value":"ScienceHouse","status":"available"},
            "tile_x":{"value":8,"status":"available"},
            "tile_y":{"value":10,"status":"available"},
            "inventory":{"value":[],"status":"available"},
            "quest_building_construction":{"value":{"projection_status":"complete_active_building_quest_projection","active_target_count":1,"rows":[{
              "quest_id":"7","quest_runtime_type":"HaveBuildingQuest","target_building_type":"Coop",
              "constructed_marker_present":false,"matching_building_count":{{{(daysLeft > 0 ? 1 : 0)}}},"matching_under_construction":{{{(daysLeft > 0 ? "true" : "false")}}},"construction_days_left":{{{daysLeft}}},
              "builder":"Robin","building_to_upgrade":"","build_days":3,"build_cost":4000,
              "build_materials":[
                {"qualified_item_id":"(O)388","required_count":300,"available_count":350,"satisfied":true,"reverse_slot_consumption_plan":[{"slot_index":0,"qualified_item_id":"(O)388","amount":300}]},
                {"qualified_item_id":"(O)390","required_count":100,"available_count":125,"satisfied":true,"reverse_slot_consumption_plan":[{"slot_index":1,"qualified_item_id":"(O)390","amount":100}]}
              ],
              "expected_money_before":9000,"expected_money_after":5000,"service_location_id":"ScienceHouse","service_is_current_location":true,
              "carpenter_action_tile_x":10,"carpenter_action_tile_y":10,"carpenter_action_raw":"Carpenter","robin_present_at_counter":true,
              "placement_location_id":"Farm","placement_tile_x":7,"placement_tile_y":12,"placement_verification":"static_native_predicates_passed_runtime_recheck_required",
              "action_status":"{{{actionStatus}}}"
            }]},"status":"available"}
          },
          "quests":{
            "active_quests":{"value":[{"id":"7","title":"Raising Animals","quest_type":8,"accepted":true,"completed":false,"runtime_type":"HaveBuildingQuest","per_type_fields":{"available":true,"building_type":"Coop","target_count":0,"current_count":0}}],"status":"available"},
            "special_orders":{"value":[],"status":"available"},"completed_special_orders":{"value":[],"status":"available"},"accepted_special_order_types":{"value":[],"status":"available"},"mail_received":{"value":[],"status":"available"}
          },
          "time":{"time":{"value":900,"status":"available"}},
          "world_progress":{"community_center":{"value":{},"status":"available"},"achievements":{"value":[],"status":"available"}},
          "farm":{"material_inventory_graph":{"value":{"schema_version":"material_inventory_graph.v1","status":"available","player_id":123,"inventory_nodes":[{"node_id":"player:123","inventory_kind":"player_inventory","supply_state":"available","owner_player_id":123,"ownership_class":"actor_owned","actor_use_authorized":true,"slots":[{"slot_index":0,"item_id":"388","qualified_item_id":"(O)388","stack":350},{"slot_index":1,"item_id":"390","qualified_item_id":"(O)390","stack":125}]}]},"status":"available"}},
          "locations":{"collision_grid":{"value":{"location_id":"ScienceHouse","width":64,"height":64,"notable_tiles":[]},"status":"available"}},
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}}
        }
        """)!;
        return Envelope(state);
    }

    private static SnapshotEnvelope Envelope(Dictionary<string, JsonElement> state) => new()
    {
        SchemaVersion = "snapshot.v1",
        StateHash = SnapshotHash.ComputeStateHash(state),
        GameTick = 1,
        RealTimestamp = "2026-08-10T00:00:00Z",
        Completeness = "complete",
        State = state
    };

    private static string Parameter(StardewAI.Contracts.Options.EventCandidate candidate, string name) =>
        candidate.Parameters.Single(value => value.Name == name).Value;
}

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

public sealed class QuestCraftingMainlineTests
{
    [Fact]
    public void ActiveCraftingQuestCompilesThroughTypedSharedNativeCraftingPath()
    {
        var snapshot = Snapshot();
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[] { "quest.advance" },
                includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(
            availability.Options[0].EventCandidates.Where(row =>
                row.Kind == "craft_quest_item"));

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("quest:craft-fixture:CraftingQuest:bound:quest-craft:craft-fixture:Field Snack:personal", candidate.CandidateId);
        Assert.Equal("craft-fixture", Parameter(candidate, "quest_id"));
        Assert.Equal("CraftingQuest", Parameter(candidate, "quest_runtime_type"));
        Assert.Equal("(O)403", candidate.QualifiedItemId);

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(
                    new BaselineTrainingReport(),
                    availability)
                .Where(row => row.CandidateId == candidate.CandidateId)
                .ToArray(),
            snapshot.StateHash);
        Assert.Equal("craft_quest_item", Assert.Single(plan.Steps).Kind);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.craft_quest_item", item.OptionId);
        Assert.Equal("pending", item.Status);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal(
            "craft_quest_item",
            Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void CompilerBlocksCraftingQuestTargetDrift()
    {
        var snapshot = Snapshot();
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, true);
        var candidate = Assert.Single(
            availability.Options[0].EventCandidates.Where(row =>
                row.Kind == "craft_quest_item"));
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(
                    new BaselineTrainingReport(),
                    availability)
                .Where(row => row.CandidateId == candidate.CandidateId)
                .ToArray(),
            snapshot.StateHash);
        var root = JsonNode.Parse(JsonSerializer.Serialize(snapshot.State))!
            .AsObject();
        root["player"]!["quest_crafting"]!["value"]!["rows"]![0]![
            "target_qualified_item_id"] = "(O)388";
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            root.ToJsonString())!;
        var drifted = Envelope(state);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "craft_quest_item_projection_drifted",
            Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void QuestEventDispatchPreservesMaterialCommitmentLedger()
    {
        var ledger = new StrategyCommitmentLedger
        {
            LedgerId = "strategy-ledger:quest-crafting",
            Revision = 3,
            MaterialReservations =
            [
                new MaterialReservation
                {
                    ReservationId = "reserved-field-snack-material",
                    Status = StrategyCommitmentStatuses.Active
                }
            ]
        };

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                Snapshot(),
                new[] { "quest.advance" },
                includeExecutorCalibrationOptions: true,
                commitmentLedger: ledger);
        var candidate = Assert.Single(
            availability.Options[0].EventCandidates.Where(row =>
                row.Kind == "craft_quest_item"));

        Assert.False(candidate.Available);
        Assert.Contains(
            "material_inventory_graph_unavailable",
            candidate.BlockReasons);
        Assert.Equal(
            ledger.LedgerId,
            Parameter(candidate, "material_reservation_ledger_id"));
        Assert.Equal(
            ledger.Revision.ToString(),
            Parameter(candidate, "material_reservation_ledger_revision"));
    }

    private static SnapshotEnvelope Snapshot()
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            """
            {
              "player":{
                "location_id":{"value":"FarmHouse","status":"available"},
                "inventory":{"value":[{"slot_index":0,"item_id":"309","qualified_item_id":"(O)309","stack":1,"is_empty":false},{"slot_index":1,"item_id":"310","qualified_item_id":"(O)310","stack":1,"is_empty":false},{"slot_index":2,"item_id":"311","qualified_item_id":"(O)311","stack":1,"is_empty":false}],"status":"available"},
                "quest_crafting":{"value":{"projection_status":"complete_active_crafting_quest_targeted_recipe_projection","active_target_count":1,"rows":[{
                  "quest_id":"craft-fixture","quest_runtime_type":"CraftingQuest","target_qualified_item_id":"(O)403",
                  "recipe_name":"Field Snack","times_crafted":0,"output_item_id":"403","output_qualified_item_id":"(O)403","output_count_per_craft":1,
                  "ingredient_rows":[
                    {"requirement_id_or_category":"309","required_count":1,"available_count_before_this_ingredient":1,"satisfied":true,"reverse_slot_consumption_plan":[{"slot_index":0,"qualified_item_id":"(O)309","amount":1}]},
                    {"requirement_id_or_category":"310","required_count":1,"available_count_before_this_ingredient":1,"satisfied":true,"reverse_slot_consumption_plan":[{"slot_index":1,"qualified_item_id":"(O)310","amount":1}]},
                    {"requirement_id_or_category":"311","required_count":1,"available_count_before_this_ingredient":1,"satisfied":true,"reverse_slot_consumption_plan":[{"slot_index":2,"qualified_item_id":"(O)311","amount":1}]}
                  ],
                  "has_ingredients_for_one":true,"craftable_count_from_player_inventory":1,
                  "output_inventory_acceptance_after_material_consumption":true,
                  "craft_candidate_status":"ready_for_native_personal_crafting_menu",
                  "workbench_crafting_sources":[]
                }]},"status":"available"}
              },
              "quests":{
                "active_quests":{"value":[{"id":"craft-fixture","title":"Craft","quest_type":2,"accepted":true,"completed":false,"runtime_type":"CraftingQuest","per_type_fields":{"available":true,"item_id":"(O)403","target_count":0,"current_count":0}}],"status":"available"},
                "special_orders":{"value":[],"status":"available"},
                "completed_special_orders":{"value":[],"status":"available"},
                "accepted_special_order_types":{"value":[],"status":"available"},
                "mail_received":{"value":[],"status":"available"}
              },
              "time":{"time":{"value":900,"status":"available"}},
              "world_progress":{
                "community_center":{"value":{},"status":"available"},
                "achievements":{"value":[],"status":"available"}
              },
              "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}}
            }
            """)!;
        return Envelope(state);
    }

    private static SnapshotEnvelope Envelope(
        Dictionary<string, JsonElement> state) => new()
    {
        SchemaVersion = "snapshot.v1",
        StateHash = SnapshotHash.ComputeStateHash(state),
        GameTick = 1,
        RealTimestamp = "2026-08-10T00:00:00Z",
        Completeness = "complete",
        State = state
    };

    private static string Parameter(
        StardewAI.Contracts.Options.EventCandidate candidate,
        string name) =>
        candidate.Parameters.Single(row => row.Name == name).Value;
}

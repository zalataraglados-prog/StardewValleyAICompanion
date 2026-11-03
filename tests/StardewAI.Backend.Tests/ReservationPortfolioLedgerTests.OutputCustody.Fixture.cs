using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Strategy;
using StardewAI.Core.Training;
using StardewAI.Core.Execution;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    private sealed record CustodyFixture(SnapshotEnvelope Snapshot, StrategyCommitmentLedger Ledger, ReservationPortfolioLedgerService Service);

    private static CustodyFixture MintMuseumCustody(bool protectSpare = false)
    {
        var snapshot = MuseumCustodySnapshot();
        var service = new ReservationPortfolioLedgerService();
        var commit = service.Commit(null, snapshot, Request(snapshot, 20, 300), "fixture-commit");
        Assert.True(commit.Accepted, string.Join(";", commit.Errors));
        var request = SettlementRequest(snapshot, 1);
        request.OutputCustodyClaims = new[] { MuseumOutputClaim() };
        var result = service.SettleCompletedRoute(commit.Ledger, snapshot, request, "fixture-native-acquisition-settled");
        Assert.True(result.Accepted, string.Join(";", result.Errors));
        var ledger = result.Ledger!;
        if (protectSpare)
        {
            var spare = new MaterialReservationLedgerService().Upsert(ledger, snapshot, new()
            {
                StateHash = snapshot.StateHash, ExpectedLedgerRevision = ledger.Revision, ReservationId = "other-goal-spare",
                SourceDecisionId = "other.goal", GoalId = "other.goal", NodeId = "player:123", SlotIndex = 1,
                QualifiedItemId = "(O)96", Quantity = 1, Purpose = "other goal keeps the spare"
            }, "fixture-other-goal");
            Assert.True(spare.Accepted, string.Join(";", spare.Errors));
            ledger = spare.Ledger!;
        }
        return new(snapshot, ledger, service);
    }

    private static ReservationPortfolioOutputCustodyClaim MuseumOutputClaim() => new()
    {
        NodeId = "player:123", SlotIndex = 1, QualifiedItemId = "(O)96", Quantity = 1,
        ConsumerOptionId = MaterialOutputCustodyContract.MuseumConsumer
    };

    private static SnapshotEnvelope MuseumCustodySnapshot()
    {
        var snapshot = Snapshot();
        JsonNode Field(object value) => JsonSerializer.SerializeToNode(new
        {
            value, status = "available", confidence = 1, adapter = "test", read_at_tick = 1,
            source = new { kind = "game_object", path = "synthetic_custody_fixture" }
        })!;
        var player = JsonSerializer.SerializeToNode(snapshot.State["player"])!;
        player["location_id"] = Field("ArchaeologyHouse");
        player["tile_x"] = Field(8);
        player["tile_y"] = Field(9);
        player["inventory"] = Field(new[] { new { slot_index = 1, qualified_item_id = "(O)96", stack = 2 } });
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
        snapshot.State["world_progress"] = JsonSerializer.SerializeToElement(new Dictionary<string, JsonNode>
        {
            ["museum"] = Field(new
            {
                donated_count = 59, total_donatable_items = 95, collection_complete = false,
                donatable_items = new[] { new { qualified_item_id = "(O)96", donated = false } },
                complete_collection_achievement_received = false, field_guide_quest_present = false, field_guide_quest_completed = false,
                rusty_key_donation_threshold = 60, rusty_key_reward_id = "museum60", rusty_key_reward_action = "MarkEventSeen Host 295672",
                rusty_key_reward_claimed = false, rusty_key_prerequisite_event_seen = false, rusty_key_event_seen = false, has_rusty_key = false,
                museum_location_id = "ArchaeologyHouse", museum_is_current_location = true, museum_mutex_locked = false,
                gunther_action_tile_x = 10, gunther_action_tile_y = 9, gunther_action_raw = "Gunther",
                free_donation_tile_x = 4, free_donation_tile_y = 4, free_donation_tile_count = 1, pending_reward_ids = Array.Empty<string>(),
                donation_candidates = new[] { new
                {
                    slot_index = 1, item_id = "96", qualified_item_id = "(O)96", display_name = "synthetic scroll", runtime_type = "StardewValley.Object",
                    stack_before = 2, stack_after = 1, donated_count_before = 59, donated_count_after = 60,
                    completes_collection = false, reaches_rusty_key_threshold = true, expected_complete_collection_achievement_after = false,
                    field_guide_quest_present_before = false, field_guide_quest_completed_before = false, expected_field_guide_quest_completed_after = false,
                    pending_reward_ids_before = Array.Empty<string>(), pending_reward_ids_after = Array.Empty<string>(), newly_pending_reward_ids = Array.Empty<string>(),
                    auto_applied_reward_ids = new[] { "museum60" }, auto_applied_reward_actions = new[] { "MarkEventSeen Host 295672" },
                    reward_projection_status = "ready", action_status = "ready"
                } }
            })
        });
        snapshot.State["locations"] = JsonSerializer.SerializeToElement(new Dictionary<string, JsonNode>
        {
            ["collision_grid"] = Field(new { location_id = "ArchaeologyHouse", width = 64, height = 64, notable_tiles = Array.Empty<object>() })
        });
        snapshot.State["menus"] = JsonSerializer.SerializeToElement(new Dictionary<string, JsonNode>
        {
            ["active_menu"] = Field(new { is_open = false, type = "none" })
        });
        var farm = JsonSerializer.SerializeToNode(snapshot.State["farm"])!;
        farm["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!["slots"]!.AsArray().Add(
            JsonSerializer.SerializeToNode(new { slot_index = 1, item_id = "96", qualified_item_id = "(O)96", stack = 2 }));
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm);
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }

    private static ActionQueueItem CustodyDonationItem(CustodyFixture fixture, string goalId = "goal.grandpa_21")
    {
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(fixture.Snapshot, new[] { "museum.donate_items" }, true, fixture.Ledger);
        var plan = new DailyPlanCompiler().Compile(new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            fixture.Snapshot.StateHash, goalId: goalId);
        return Assert.Single(new ActionQueueCompiler().Compile(plan, fixture.Snapshot, fixture.Ledger).Items);
    }

    private static SnapshotEnvelope AfterCustodyDonation(SnapshotEnvelope before)
    {
        var after = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(before))!;
        var player = JsonSerializer.SerializeToNode(after.State["player"])!;
        player["inventory"]!["value"]![0]!["stack"] = 1;
        after.State["player"] = JsonSerializer.SerializeToElement(player);
        var world = JsonSerializer.SerializeToNode(after.State["world_progress"])!;
        world["museum"]!["value"]!["donated_count"] = 60;
        world["museum"]!["value"]!["donatable_items"]![0]!["donated"] = true;
        after.State["world_progress"] = JsonSerializer.SerializeToElement(world);
        var farm = JsonSerializer.SerializeToNode(after.State["farm"])!;
        farm["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!["slots"]![1]!["stack"] = 1;
        after.State["farm"] = JsonSerializer.SerializeToElement(farm);
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        return after;
    }

    private static QueueExecutionStepReceipt CustodyReceipt(ActionQueueItem item, SnapshotEnvelope before, SnapshotEnvelope after) => new()
    {
        QueueId = "fixture-custody-queue", QueueItemId = item.QueueItemId, OptionId = item.OptionId,
        Status = "applied", PrimitiveVerificationStatus = "verified", AfterSnapshotFresh = true,
        SourceStateHash = before.StateHash, AfterStateHash = after.StateHash,
        ChangedFacts = JsonSerializer.SerializeToElement(Array.Empty<object>())
    };
}

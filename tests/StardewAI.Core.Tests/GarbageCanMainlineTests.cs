using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class GarbageCanMainlineTests
{
    [Fact]
    public void ExactGarbageCanProjectionFlowsThroughNativeQueue()
    {
        var snapshot = Snapshot(StateJson("ready", false, 7, "projection-a"));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "foraging.rummage_garbage" },
            true);
        var candidate = Assert.Single(availability.Options.Single().EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("rummage_garbage", candidate.Kind);
        Assert.Equal("(O)311", candidate.QualifiedItemId);
        Assert.Equal(1, candidate.Quantity);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "garbage_can_data_payload_sha256" &&
            parameter.Value == "34621d9c92c472019c6e0a6bae4ac86a62576b7bccae4b9191590ed11e46911f");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        Assert.Equal("rummage_garbage", Assert.Single(plan.Steps).Kind);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.rummage_garbage", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("rummage_garbage", Assert.Single(item.NormalizedCommand.Steps).StepType);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "garbage_can_projection_fingerprint" && parameter.Value == "projection-a");
    }

    [Fact]
    public void CompilerRejectsCheckedStatusAndProjectionDrift()
    {
        var initial = Snapshot(StateJson("ready", false, 7, "projection-a"));
        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            new CandidateOptionAvailabilityEvaluator().Evaluate(
                initial,
                new[] { "foraging.rummage_garbage" },
                true));
        var plan = new DailyPlanCompiler().Compile(ranked, initial.StateHash);
        var drifted = Snapshot(StateJson("blocked_already_checked_today", true, 8, "projection-b"));
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("rummage_garbage_not_ready_by_transparent_state", queue.Items.Single().BlockingReasons);
        Assert.Contains("rummage_garbage_projection_drifted", queue.Items.Single().BlockingReasons);
    }

    [Fact]
    public void TypedExecutionFieldsRoundTrip()
    {
        var request = new TrainingExecutionRequest
        {
            FixtureGarbageCanProfile = "desert_multiple",
            GarbageCanAction = "Garbage DesertFestival",
            GarbageCanId = "DesertFestival",
            ExpectedCheckedTodayBefore = false,
            PredictedItemProduced = true,
            ExpectedOutputJson = "{\"qualified_item_id\":\"(O)CalicoEgg\",\"quantity\":8}",
            ReactingNpcJson = "null"
        };
        var json = JsonSerializer.Serialize(request, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(json, JsonOptions)!;

        Assert.Equal("desert_multiple", roundTrip.FixtureGarbageCanProfile);
        Assert.Equal("Garbage DesertFestival", roundTrip.GarbageCanAction);
        Assert.False(roundTrip.ExpectedCheckedTodayBefore);
        Assert.True(roundTrip.PredictedItemProduced);
        Assert.Equal("null", roundTrip.ReactingNpcJson);
    }

    private static string StateJson(string status, bool checkedToday, int checkedCount, string fingerprint) => $$$"""
    {
      "player": {
        "location_id":{"value":"Town","status":"available"},
        "tile_x":{"value":96,"status":"available"},
        "tile_y":{"value":80,"status":"available"},
        "inventory":{"value":[],"status":"available"},
        "safe_item_context":{"value":{"current_tool_index":1,"safe_slot_available":true,"safe_slot_index":4,"safe_slot_kind":"empty"},"status":"available"},
        "friendships":{"value":[],"status":"available"}
      },
      "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}},
      "current_location":{
        "characters":{"value":[],"status":"available"},
        "debris":{"value":[],"status":"available"},
        "garbage_cans":{"value":[{
        "tile_x":97,"tile_y":80,"action":"Garbage Blacksmith","garbage_can_id":"Blacksmith","garbage_can_id_known":true,
        "checked_today":{{{checkedToday.ToString().ToLowerInvariant()}}},"expected_checked_today_after":true,
        "trash_cans_checked_before":{{{checkedCount}}},"expected_trash_cans_checked_delta":1,
        "daily_luck":0.025,"alleyway_buffet_read":false,"predicted_item_produced":true,
        "selected_entry_id":"Base_Standard","selected_ignore_base_chance":false,"selected_mega_success":false,"selected_double_mega_success":false,
        "output_delivery":"single_debris","expected_output":{"qualified_item_id":"(O)311","quality":0,"quantity":1,"context_tags":["item_trash"]},
        "reacting_npc":null,"safe_slot_index":4,"restore_slot_index":1,
        "data_payload_sha256":"34621d9c92c472019c6e0a6bae4ac86a62576b7bccae4b9191590ed11e46911f",
        "data_contract_status":"exact_locked_base_1.6.15","prediction_status":"exact_native_non_mutating_prediction",
        "native_contract":"GameLocation.checkAction -> performAction Garbage -> CheckGarbage -> TryGetGarbageItem -> CheckedGarbage/stat/output/native NPC reaction; no direct checked-set, stat, friendship, inventory, debris, or RNG mutation",
        "projection_fingerprint":"{{{fingerprint}}}","rummage_status":"{{{status}}}"
      }],"status":"available"}},
      "locations":{
        "collision_grid":{"value":{"location_id":"Town","width":120,"height":100,"notable_tiles":[]},"status":"available"},
        "route_action_branch_coverage":{"value":{"rows":[]},"status":"available"}
      }
    }
    """;

    private static SnapshotEnvelope Snapshot(string json)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-30T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class IslandGourmandMainlineTests
{
    [Theory]
    [InlineData(false, "introduce_request", 0, 0)]
    [InlineData(true, "complete_request", 0, 1)]
    public void ReadyCropFlowsThroughOneTypedGourmandPrimitive(
        bool talked,
        string expectedStage,
        int requestBefore,
        int requestAfter)
    {
        var snapshot = Snapshot(talked: talked, requestIndex: requestBefore);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "island.complete_gourmand_request" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("advance_gourmand_request", candidate.Kind);
        AssertParameter(candidate.Parameters, "gourmand_interaction_stage", expectedStage);
        AssertParameter(candidate.Parameters, "request_index_after", requestAfter.ToString());
        AssertParameter(candidate.Parameters, "required_crop_item_id", "254");

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("advance_gourmand_request", step.Kind);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.advance_gourmand_request", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("advance_gourmand_request", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void MissingReadyCropIsExcludedUpstream()
    {
        var snapshot = Snapshot(talked: true, requestIndex: 0, ready: false);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "island.complete_gourmand_request" }, true);

        Assert.Empty(Assert.Single(availability.Options).EventCandidates);
    }

    [Fact]
    public void FreshCompilerRejectsRequestIdentityDrift()
    {
        var original = Snapshot(talked: true, requestIndex: 0);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(original, new[] { "island.complete_gourmand_request" }, true);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            original.StateHash);
        var drifted = Snapshot(talked: true, requestIndex: 1);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("gourmand_request_projection_drifted", Assert.Single(queue.Items).BlockingReasons);
    }

    private static SnapshotEnvelope Snapshot(bool talked, int requestIndex, bool ready = true)
    {
        var cropId = requestIndex switch
        {
            0 => "254",
            1 => "262",
            _ => "248"
        };
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"IslandFarmCave","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":4,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":4,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress":{
            "island_gourmand":{"value":{
              "schema_version":"island_gourmand_progress.v1","projection_status":"exact_native_active_request_projected",
              "location_id":"IslandFarmCave","is_current_location":true,"host_authority":true,"menu_clear":true,
              "mutex_projection_available":true,"mutex_locked":false,"mutex_held_by_local_player":false,"triggered_gourmand":false,
              "required_mail_key":"Island_UpgradeHouse","required_mail_satisfied":true,
              "talked_to_gourmand_received_or_pending":{{{talked.ToString().ToLowerInvariant()}}},
              "requests_fulfilled":{{{requestIndex}}},"total_requests":3,"complete":false,"active_request_index":{{{requestIndex}}},
              "required_crop_item_id":"{{{cropId}}}","required_crop_qualified_item_id":"(O){{{cropId}}}","required_crop_display_name":"test",
              "request_state":"{{{(ready ? "ready_matching_crop_present" : "matching_crop_growing")}}}",
              "matching_crop_count":1,"ready_matching_crop_count":{{{(ready ? 1 : 0)}}},
              "matching_crops":[{"tile_x":10,"tile_y":10,"harvest_item_id":"{{{cropId}}}","current_phase":5,"final_phase_index":5,"fully_grown":false,"day_of_current_phase":0,"ready_for_request":{{{ready.ToString().ToLowerInvariant()}}}}],
              "action_tiles":[{"tile_x":4,"tile_y":3,"action":"Gourmand"}],
              "native_contract":"IslandFarmCave.performAction_Gourmand_then_Gourmand_Yes_then_OnRequestGourmandCheck_then_native_event_then_GiveReward"
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "golden_walnuts":{"value":{"found":0,"current":0},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations":{
            "collision_grid":{"value":{"location_id":"IslandFarmCave","width":16,"height":16,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}}
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "transparent_state.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-10-23T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static void AssertParameter(
        StardewAI.Contracts.Execution.SmallModelActionParameter[] parameters,
        string name,
        string value) =>
        Assert.Contains(parameters, parameter => parameter.Name == name && parameter.Value == value);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

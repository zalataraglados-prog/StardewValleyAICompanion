using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class PetCareMainlineTests
{
    private const string PetId = "11111111-2222-3333-4444-555555555555";

    [Fact]
    public void MaximumFriendshipPetStillFlowsForNativeGiftOpportunity()
    {
        var snapshot = Snapshot(PetStateJson());
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.care_for_pets" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(availability.Options.Single(option => option.OptionId == "farm.care_for_pets").EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("pet_daily_interaction", candidate.Kind);
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "pet_gift_trigger_expected" && parameter.Value == "true");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "expected_friendship_before" && parameter.Value == "1000");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "expected_friendship_after" && parameter.Value == "1000");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        Assert.Equal(new[] { "select_safe_item_slot", "pet_interact" }, plan.Steps.Select(step => step.Kind));

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.True(queue.Status == "pending", string.Join(";", queue.Items.SelectMany(item => item.BlockingReasons)));
        Assert.Equal(new[] { "executor.select_safe_item_slot", "executor.pet_interact" }, queue.Items.Select(item => item.OptionId));
        Assert.All(queue.Items, item => Assert.Empty(item.BlockingReasons));
    }

    [Fact]
    public void ReadyPetBowlFlowsWithTypedDelayedSettlementProjection()
    {
        var snapshot = Snapshot(PetBowlStateJson());
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.care_for_pets" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(availability.Options.Single(option => option.OptionId == "farm.care_for_pets").EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("fill_pet_bowl", candidate.Kind);
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "expected_next_day_friendship_after" && parameter.Value == "1000");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "expected_next_day_pet_love_mail" && parameter.Value == "true");

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("fill_pet_bowl", step.Kind);
        Assert.Contains("keep_friendship_and_mail_as_delayed_settlement", step.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.fill_pet_bowl", item.OptionId);
        Assert.Empty(item.BlockingReasons);
    }

    [Fact]
    public void CustomPetRuntimeIsExcludedUpstream()
    {
        var snapshot = Snapshot(PetStateJson().Replace("StardewValley.Characters.Pet", "ExampleMod.CustomPet"));
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.care_for_pets" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(availability.Options.Single(option => option.OptionId == "farm.care_for_pets").EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("unsupported_pet_runtime_type", candidate.BlockReasons);
    }

    private static string PetStateJson()
    {
        return CommonState("""
        "pets":{"value":[{
          "pet_id":"PET_ID","runtime_type":"StardewValley.Characters.Pet","native_check_action_declaring_type":"StardewValley.Characters.Pet",
          "native_check_action_supported":true,"location_id":"Farm","tile_x":12,"tile_y":10,"name":"EvdPet","display_name":"EvdPet",
          "friendship_toward_farmer":1000,"friendship_after_daily_interaction":1000,"granted_friendship_for_pet":false,
          "granted_friendship_after_daily_interaction":true,"last_pet_day_for_player":null,"current_total_days":44,
          "times_pet_before":7,"times_pet_after_daily_interaction":8,"daily_interaction_friendship_delta":0,"safe_slot_index":3,
          "pet_love_mail_before":false,"pet_love_mail_after_daily_interaction":true,
          "marnie_pet_adoption_mail_before_or_pending":false,"marnie_pet_adoption_mail_after_daily_interaction":true,
          "gift_trigger_will_succeed":true,"gift_selection_status":"runtime_observed_global_rng_selection","action_status":"ready"
        }],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "pet_bowls":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
        """).Replace("PET_ID", PetId);
    }

    private static string PetBowlStateJson()
    {
        return CommonState("""
        "pets":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
        "pet_bowls":{"value":[{
          "location_id":"Farm","runtime_type":"StardewValley.Buildings.PetBowl","building_tile_x":18,"building_tile_y":18,
          "action_tile_x":20,"action_tile_y":20,"watered":false,"assigned_pet_id":"PET_ID","assigned_pet_present":true,
          "friendship_before_next_day":994,"friendship_after_fill_and_next_day_update":1000,"delayed_friendship_delta":6,
          "pet_love_mail_before":false,"pet_love_mail_after_fill_and_next_day_update":true,
          "marnie_pet_adoption_mail_before_or_pending":false,"marnie_pet_adoption_mail_after_fill_and_next_day_update":true,
          "delayed_settlement":"Pet.dayUpdate consumes watered=true and applies min(1000,friendship+6)",
          "watering_can_slot_index":4,"watering_can_water_left":40,"watering_can_bottomless":false,
          "watering_can_runtime_type":"StardewValley.Tools.WateringCan",
          "expected_watering_can_water_after":39,"watering_energy_cost":2,"action_status":"ready"
        }],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
        """).Replace("PET_ID", PetId);
    }

    private static string CommonState(string farmField)
    {
        return """
        {
          "player":{
            "location_id":{"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            ,"energy":{"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            ,"inventory":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            ,"current_tool_index":{"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            ,"active_object_qualified_id":{"value":null,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            ,"safe_item_context":{"value":{"safe_slot_available":true,"safe_slot_index":3},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm":{FARM_FIELD},
          "quests":{"mail_received":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "current_location":{"map":{"value":{"location_id":"Farm","width":100,"height":100},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "locations":{
            "collision_grid":{"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage":{"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """.Replace("FARM_FIELD", farmField);
    }

    private static SnapshotEnvelope Snapshot(string json)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-07T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

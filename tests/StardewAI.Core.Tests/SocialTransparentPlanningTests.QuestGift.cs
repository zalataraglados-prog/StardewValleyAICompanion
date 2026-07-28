using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class SocialTransparentPlanningTests
{
    [Fact]
    public void SpecialOrderGiftObjectiveReusesExactNativeSocialGiftChain()
    {
        var inventory = """
        [{"slot_index":0,"item_id":"66","qualified_item_id":"(O)66","display_name":"Amethyst","stack":1,"quality":0,
          "maximum_stack_size":999,"is_object":true,"object_quest_item":false,"object_big_craftable":false,
          "is_furniture":false,"is_wallpaper":false,"protected_from_auto_sell":false,"can_be_given_as_gift":true,
          "base_tag_not_giftable":false,"context_tags":["category_gem","item_amethyst"],"is_empty":false}]
        """;
        var snapshot = CompleteSocialSnapshot(inventoryValue: inventory);
        AddQuestFields(snapshot, "Loved");

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("quest_npc_interaction", candidate.Kind);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_interaction_kind" && parameter.Value == "gift");
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_gift_minimum_like_level" && parameter.Value == "Loved");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        var terminal = Assert.Single(queue.Items.Where(item =>
            item.OptionId == "executor.quest_npc_interact"));
        Assert.Empty(terminal.BlockingReasons);
        Assert.Contains(terminal.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "quest_interaction_kind" && parameter.Value == "gift");
    }

    [Fact]
    public void SpecialOrderGiftObjectiveFailsClosedBelowNativeMinimumLikeLevel()
    {
        var inventory = """
        [{"slot_index":0,"item_id":"66","qualified_item_id":"(O)66","display_name":"Amethyst","stack":1,"quality":0,
          "maximum_stack_size":999,"is_object":true,"object_quest_item":false,"object_big_craftable":false,
          "is_furniture":false,"is_wallpaper":false,"protected_from_auto_sell":false,"can_be_given_as_gift":true,
          "base_tag_not_giftable":false,"context_tags":["category_gem"],"is_empty":false}]
        """;
        var taste = """
        {"value":[{"npc_name":"Abigail","slot_index":0,"qualified_item_id":"(O)66","quality":0,"taste":"like",
          "expected_friendship_delta":"45","expected_friendship_delta_complete":true,"complete":true}],
         "status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
        """;
        var snapshot = CompleteSocialSnapshot(
            inventoryValue: inventory,
            giftTasteField: taste);
        AddQuestFields(snapshot, "Loved");

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true)
            .Options).EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("special_order_matching_gift_not_available", candidate.BlockReasons);
    }

    private static void AddQuestFields(
        StardewAI.Contracts.State.SnapshotEnvelope snapshot,
        string minimumLikeLevel)
    {
        snapshot.State["quests"] = QuestFields(minimumLikeLevel);
        snapshot.State["world_progress"] = JsonSerializer.Deserialize<JsonElement>(
            """
            {
              "community_center":{"value":{},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "achievements":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            }
            """);
    }

    private static JsonElement QuestFields(string minimumLikeLevel)
    {
        return JsonSerializer.Deserialize<JsonElement>(
            """
            {
              "active_quests":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "special_orders":{"value":[{
                "quest_key":"GemGifts","quest_name":"Gem Gifts","quest_state":"InProgress","special_rule":"","is_island_order":0,
                "objectives":[{
                  "description":"Give loved gems","current_count":0,"max_count":2,"runtime_type":"GiftObjective",
                  "fail_on_completion":false,"complete":false,
                  "per_type_fields":{
                    "available":true,
                    "acceptable_context_tag_sets":["category_gem"],
                    "minimum_like_level":"MINIMUM_LIKE_LEVEL"
                  }
                }],
                "rewards":[{"runtime_type":"MoneyReward","available":true,"amount":500}]
              }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "completed_special_orders":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "accepted_special_order_types":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "mail_received":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            }
            """.Replace("MINIMUM_LIKE_LEVEL", minimumLikeLevel, StringComparison.Ordinal));
    }
}

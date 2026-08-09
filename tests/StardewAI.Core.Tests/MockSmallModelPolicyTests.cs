using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.MockModel;

namespace StardewAI.Core.Tests;

public sealed class MockSmallModelPolicyTests
{
    [Fact]
    public void ClassifierSeparatesMechanicalAndParameterizedMechanicalTasks()
    {
        var classifier = new TaskIntentClassifier();

        var crops = classifier.Classify("water crops");
        var mining = classifier.Classify("mine to level 40");

        Assert.Equal(TaskIntentCategory.Mechanical, crops.Category);
        Assert.Equal("farm.maintain_crops", crops.OptionId);
        Assert.Equal(TaskIntentCategory.ParameterizedMechanical, mining.Category);
        Assert.Equal("exploration.visit_location", mining.OptionId);
        Assert.Contains(mining.Parameters, item => item.Name == "target_depth" && item.Value == "40");
    }

    [Fact]
    public void ClassifierMapsGrandpaGoalToStrategyOption()
    {
        var classifier = new TaskIntentClassifier();

        var grandpa = classifier.Classify("grandpa_max_score_year3");

        Assert.Equal(TaskIntentCategory.EconomicStrategic, grandpa.Category);
        Assert.Equal("strategy.grandpa_progress", grandpa.OptionId);
        Assert.Contains(grandpa.Parameters, item =>
            item.Name == "strategic_goal" &&
            item.Value == "grandpa_max_score_year3");
        Assert.Contains(grandpa.Parameters, item =>
            item.Name == "requires_direction_selection" &&
            item.Value == "true");
        Assert.Contains(grandpa.Parameters, item =>
            item.Name == "classifier_note" &&
            item.Value == "direction_deferred_to_snapshot_aware_policy");
    }

    [Fact]
    public void MockPolicyEmitsOfficialSmallModelActionContract()
    {
        var snapshot = Snapshot();

        var output = new MockSmallModelPolicy().Generate(snapshot, "water crops", "training_singleplayer");
        var queue = new ActionQueueCompiler().Compile(output, snapshot);

        Assert.Equal("small_model_action.v1", output.SchemaVersion);
        Assert.Equal("mock-small-model.rule.v1", output.SourceModel);
        Assert.Equal(snapshot.StateHash, output.StateHash);
        Assert.Equal("farm.maintain_crops", output.Actions[0].OptionId);
        Assert.Contains(output.Actions[0].Parameters, item =>
            item.Name == "intent_category" &&
            item.Value == TaskIntentCategory.Mechanical);
        Assert.Equal("blocked", queue.Status);
        Assert.Empty(queue.Items[0].NormalizedCommand.Steps);
    }

    [Fact]
    public void MockPolicyCompilesGrandpaGoalAsStrategyRequestNotMechanicalSteps()
    {
        var snapshot = Snapshot();

        var output = new MockSmallModelPolicy().Generate(snapshot, "grandpa_max_score_year3", "training_singleplayer");
        var queue = new ActionQueueCompiler().Compile(output, snapshot);

        Assert.Equal("strategy.grandpa_progress", output.Actions[0].OptionId);
        Assert.Equal("long_term_strategic", queue.Items[0].BehaviorCategory);
        Assert.Equal("plan_validation", queue.Items[0].CompilerResponsibility);
        Assert.Equal("strategy_value", queue.Items[0].TrainingRole);
        Assert.Equal("strategy_plan", queue.Items[0].NormalizedCommand.CommandType);
        Assert.Empty(queue.Items[0].NormalizedCommand.Steps);
        var plan = Assert.Single(queue.Items[0].NormalizedCommand.StrategyPlan);
        Assert.Equal("earn_money", plan.DirectionId);
        Assert.Equal(240, plan.RequiredMinutes);
    }

    private static SnapshotEnvelope Snapshot()
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
        {
          "identity": {
            "save_id": {"value":"test-save","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "player_id": {"value":"test-player","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "time": {
            "year": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "season": {"value":"spring","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "day": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "time": {"value":600,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "weather": {"value":"sunny","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "total_money_earned": {"value":10000,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "level": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_skull_key": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_rusty_key": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "married_or_roommate": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "farmhouse_upgrade_level": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "crops": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "grandpa_score": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress": {
            "achievements": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "community_center": {"value":{"location_accessible":false,"completed":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "joja_membership": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "npcs": {
            "friendships": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "quests": {
            "mail_received": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":null,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "transport": {
            "event_stream_websocket": {"value":"ws://localhost/test","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-05T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.MockModel;
using StardewAI.Core.WorldModel;

namespace StardewAI.Core.Tests;

public sealed class TimeBudgetValidatorTests
{
    [Fact]
    public void MiningUsesPerfectHumanProfileInsteadOfLowLevelFailurePenalty()
    {
        var snapshot = Snapshot(610);
        var output = new MockSmallModelPolicy().Generate(snapshot, "mine to level 40", "training_singleplayer");
        var queue = new ActionQueueCompiler().Compile(output, snapshot);
        var model = new WorldModelProjector().Project(snapshot, "mine to level 40", "training");

        var report = new TimeBudgetValidator().Validate(model, queue);

        Assert.Equal("perfect_human_player", report.ExecutionProfile);
        Assert.True(report.FitsRequired);
        Assert.Equal("exploration.visit_location", report.Items[0].OptionId);
        Assert.Contains(report.Items[0].Notes, item => item == "execution_profile_assumes_perfect_human_player_inputs");
        Assert.DoesNotContain(report.BlockReasons, item => item.Contains("danger"));
    }

    [Fact]
    public void BlocksRequiredWorkWhenPastUsableDayBudget()
    {
        var snapshot = Snapshot(2500);
        var output = new MockSmallModelPolicy().Generate(snapshot, "mine to level 40", "training_singleplayer");
        var queue = new ActionQueueCompiler().Compile(output, snapshot);
        var model = new WorldModelProjector().Project(snapshot, "mine to level 40", "training");

        var report = new TimeBudgetValidator().Validate(model, queue);

        Assert.False(report.FitsRequired);
        Assert.Contains("required_work_exceeds_time_budget", report.BlockReasons);
    }

    [Fact]
    public void SeparatesRequiredAndOptionalBudgetFit()
    {
        var snapshot = Snapshot(2400);
        var model = new WorldModelProjector().Project(snapshot, "required recovery plus optional quest", "training");
        var queue = new ActionQueueEnvelope
        {
            QueueId = "queue.test",
            StateHash = snapshot.StateHash,
            Status = "pending",
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef
            {
                ActorId = "training_farmer.main",
                ActorType = "training_farmer",
                ControlSurface = "training_sandbox"
            },
            Items = new[]
            {
                Item("recovery.stabilize_day", "required"),
                Item("quest.advance", "optional")
            }
        };

        var report = new TimeBudgetValidator().Validate(model, queue);

        Assert.True(report.FitsRequired);
        Assert.False(report.FitsRequiredPlusOptional);
        Assert.DoesNotContain("required_work_exceeds_time_budget", report.BlockReasons);
        Assert.Contains("required_plus_optional_exceeds_time_budget", report.BlockReasons);
    }

    private static ActionQueueItem Item(string optionId, string role)
    {
        return new ActionQueueItem
        {
            QueueItemId = "queue_item." + optionId,
            OptionId = optionId,
            Status = "pending",
            NormalizedCommand = new NormalizedCommand
            {
                OptionId = optionId,
                Parameters = new[]
                {
                    new SmallModelActionParameter
                    {
                        Name = "schedule_role",
                        Value = role
                    }
                }
            }
        };
    }

    private static SnapshotEnvelope Snapshot(int time)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>($$"""
        {
          "identity": {
            "save_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "player_id": {"value":"123","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "time": {
            "season": {"value":"spring","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "day": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "time": {"value":{{time}},"status":"available","source":{"kind":"game_object","path":"Game1.timeOfDay"},"adapter":"test","read_at_tick":1,"confidence":1},
            "weather": {"value":"sun","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"Game1.player.Stamina"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "crops": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "transport": {
            "event_stream_websocket": {"value":{"endpoint":"ws://127.0.0.1:8766/api/v1/events/ws"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"Farm":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
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

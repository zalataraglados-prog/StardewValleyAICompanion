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
        Assert.False(report.FitsRequired);
        Assert.Equal("exploration.visit_location", report.Items[0].OptionId);
        Assert.Equal("mining_perfect_executor.runtime_calibration_pending", report.Items[0].Estimator);
        Assert.Equal(-1, report.Items[0].EstimatedMinutes);
        Assert.Contains(report.Items[0].Notes, item => item == "rolling_floor_step_executor_implemented");
        Assert.Contains(report.BlockReasons, item => item == "time_budget_contains_unknown_duration");
        Assert.Contains(report.Items[0].Notes, item => item.StartsWith("assumption_domain:mining_and_combat"));
        Assert.DoesNotContain(report.BlockReasons, item => item.Contains("danger"));
    }

    [Fact]
    public void FishingUsesPerfectExecutorDurationModel()
    {
        var snapshot = Snapshot(610);
        var output = new MockSmallModelPolicy().Generate(snapshot, "catch 3 fish", "training_singleplayer");
        var queue = new ActionQueueCompiler().Compile(output, snapshot);
        var model = new WorldModelProjector().Project(snapshot, "catch 3 fish", "training");

        var report = new TimeBudgetValidator().Validate(model, queue);

        Assert.True(report.FitsRequired);
        Assert.Equal("fishing_perfect_executor.v1", report.Items[0].Estimator);
        Assert.Equal(51, report.Items[0].EstimatedMinutes);
        Assert.Contains(report.Items[0].Notes, item => item.StartsWith("assumption_domain:fishing"));
        Assert.Contains(report.Items[0].Notes, item => item.Contains("bad_bobber_control"));
    }

    [Fact]
    public void GenericExplorationUsesNavigationPerfectExecutorModel()
    {
        var snapshot = Snapshot(610);
        var model = new WorldModelProjector().Project(snapshot, "visit forest", "training");
        var queue = new ActionQueueEnvelope
        {
            QueueId = "queue.navigation",
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
                new ActionQueueItem
                {
                    QueueItemId = "queue_item.visit",
                    OptionId = "exploration.visit_location",
                    Status = "pending",
                    NormalizedCommand = new NormalizedCommand
                    {
                        OptionId = "exploration.visit_location",
                        Parameters = new[]
                        {
                            new SmallModelActionParameter { Name = "route_tiles", Value = "90" }
                        }
                    }
                }
            }
        };

        var report = new TimeBudgetValidator().Validate(model, queue);

        Assert.Equal("navigation_perfect_executor.v1", report.Items[0].Estimator);
        Assert.Equal(40, report.Items[0].EstimatedMinutes);
        Assert.Contains(report.Items[0].Notes, item => item.StartsWith("assumption_domain:navigation"));
        Assert.Contains(report.Items[0].Notes, item => item.Contains("walking_into_walls"));
    }

    [Fact]
    public void MiningUnknownDurationBlocksWithoutFabricatingPastBudgetCost()
    {
        var snapshot = Snapshot(2500);
        var output = new MockSmallModelPolicy().Generate(snapshot, "mine to level 40", "training_singleplayer");
        var queue = new ActionQueueCompiler().Compile(output, snapshot);
        var model = new WorldModelProjector().Project(snapshot, "mine to level 40", "training");

        var report = new TimeBudgetValidator().Validate(model, queue);

        Assert.False(report.FitsRequired);
        Assert.Contains("time_budget_contains_unknown_duration", report.BlockReasons);
        Assert.DoesNotContain("required_work_exceeds_time_budget", report.BlockReasons);
        Assert.Equal("mining_perfect_executor.runtime_calibration_pending", report.Items[0].Estimator);
    }

    [Fact]
    public void SeparatesRequiredAndOptionalBudgetFit()
    {
        var snapshot = Snapshot(2400);
        var model = new WorldModelProjector().Project(snapshot, "required recovery plus optional quest", "training");
        var recovery = Item("recovery.stabilize_day", "required");
        recovery.NormalizedCommand.Parameters = recovery.NormalizedCommand.Parameters.Concat(new[]
        {
            new SmallModelActionParameter { Name = "execution_option_id", Value = "executor.wait_ticks" },
            new SmallModelActionParameter { Name = "wait_ticks", Value = "30" }
        }).ToArray();
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
                recovery,
                Item("quest.advance", "optional")
            }
        };

        var report = new TimeBudgetValidator().Validate(model, queue);

        Assert.True(report.FitsRequired);
        Assert.False(report.FitsRequiredPlusOptional);
        Assert.DoesNotContain("required_work_exceeds_time_budget", report.BlockReasons);
        Assert.Contains("time_budget_contains_unknown_optional_duration", report.BlockReasons);
    }

    [Fact]
    public void StrategyPlanMinutesParticipateInTimeBudget()
    {
        var snapshot = Snapshot(2400);
        var model = new WorldModelProjector().Project(snapshot, "grandpa strategy", "training");
        var queue = new ActionQueueEnvelope
        {
            QueueId = "queue.strategy",
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
                new ActionQueueItem
                {
                    QueueItemId = "queue_item.strategy",
                    OptionId = "strategy.grandpa_progress",
                    Status = "pending",
                    NormalizedCommand = new NormalizedCommand
                    {
                        OptionId = "strategy.grandpa_progress",
                        StrategyPlan = new[]
                        {
                            new StrategyPlanStep
                            {
                                DirectionId = "raise_friendships",
                                Domain = "social",
                                PotentialPoints = 2,
                                PriorityScore = 1.9,
                                FeedbackKey = "grandpa.friendships",
                                RequiredMinutes = 180,
                                OptionalMinutes = 20,
                                HardPreconditions = new[] { "npc_friendship_facts_available" }
                            }
                        }
                    }
                }
            }
        };

        var report = new TimeBudgetValidator().Validate(model, queue);

        Assert.False(report.FitsRequired);
        Assert.False(report.FitsRequiredPlusOptional);
        Assert.Contains("required_work_exceeds_time_budget", report.BlockReasons);
        Assert.Contains(report.Items, item =>
            item.ScheduleRole == "required" &&
            item.EstimatedMinutes == 180 &&
            item.Notes.Contains("strategy_direction:raise_friendships"));
        Assert.Contains(report.Items, item =>
            item.ScheduleRole == "optional" &&
            item.EstimatedMinutes == 20);
    }

    [Fact]
    public void SocialOptionsUseUnknownDurationSentinelInsteadOfFreeZeroCost()
    {
        var snapshot = Snapshot(610);
        var model = new WorldModelProjector().Project(snapshot, "social plan", "training");
        var queue = new ActionQueueEnvelope
        {
            QueueId = "queue.social",
            StateHash = snapshot.StateHash,
            Status = "pending",
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef
            {
                ActorId = "training_farmer.main",
                ActorType = "training_farmer",
                ControlSurface = "training_sandbox"
            },
            Items = new[] { Item("social.gift_npc", "required") }
        };

        var report = new TimeBudgetValidator().Validate(model, queue);

        Assert.False(report.FitsRequired);
        Assert.Equal(-1, Assert.Single(report.Items).EstimatedMinutes);
        Assert.Contains("time_budget_contains_unknown_duration", report.BlockReasons);
    }

    [Fact]
    public void RecoveryConnectorUsesTransparentCurrentStepEstimate()
    {
        var snapshot = Snapshot(2300);
        var model = new WorldModelProjector().Project(snapshot, "return home", "training");
        var item = Item("recovery.stabilize_day", "required");
        item.NormalizedCommand.Parameters = item.NormalizedCommand.Parameters.Concat(new[]
        {
            new SmallModelActionParameter { Name = "execution_option_id", Value = "executor.traverse_connector" },
            new SmallModelActionParameter { Name = "estimated_ticks", Value = "180" },
            new SmallModelActionParameter { Name = "estimated_minutes", Value = "3" }
        }).ToArray();
        var queue = new ActionQueueEnvelope
        {
            QueueId = "queue.recovery.route",
            StateHash = snapshot.StateHash,
            Status = "pending",
            Items = new[] { item }
        };

        var report = new TimeBudgetValidator().Validate(model, queue);

        Assert.Equal(3, Assert.Single(report.Items).EstimatedMinutes);
        Assert.Equal("transparent_current_connector_path.v1", report.Items[0].Estimator);
    }

    [Fact]
    public void RecoverySleepUsesCompiledMacroStepDuration()
    {
        var snapshot = Snapshot(2300);
        var model = new WorldModelProjector().Project(snapshot, "sleep", "training");
        var item = Item("recovery.stabilize_day", "required");
        item.NormalizedCommand.Parameters = item.NormalizedCommand.Parameters.Concat(new[]
        {
            new SmallModelActionParameter { Name = "execution_option_id", Value = "executor.sleep" }
        }).ToArray();
        item.NormalizedCommand.Steps = new[]
        {
            new CompiledActionStep { StepType = "move_to_bed_adjacent", EstimatedTicks = 120 },
            new CompiledActionStep { StepType = "step_onto_sleep_touch_tile", EstimatedTicks = 30 },
            new CompiledActionStep { StepType = "confirm_sleep_yes", EstimatedTicks = 120 }
        };
        var queue = new ActionQueueEnvelope
        {
            QueueId = "queue.recovery.sleep",
            StateHash = snapshot.StateHash,
            Status = "pending",
            Items = new[] { item }
        };

        var report = new TimeBudgetValidator().Validate(model, queue);

        Assert.Equal(7, Assert.Single(report.Items).EstimatedMinutes);
        Assert.Equal("recovery_sleep_macro_steps.v2", report.Items[0].Estimator);
    }

    [Fact]
    public void GameClockTickConversionMatchesLockedRuntimeConstants()
    {
        Assert.Equal(1, GameClockBudgetPolicy.TicksToGameMinutes(42));
        Assert.Equal(2, GameClockBudgetPolicy.TicksToGameMinutes(60));
        Assert.Equal(10, GameClockBudgetPolicy.ClockMinutesBetween(1850, 1900));
        Assert.True(GameClockBudgetPolicy.RecoveryWindowStarted(1900));
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
            "collision_grid": {"value":{"Farm":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"unsupported_for_route_training_count":0,"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
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

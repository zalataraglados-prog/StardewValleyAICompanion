using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed partial class ActionQueueCompilerTests
{
    [Fact]
    public void CompileTurnsTerminalSleepPlanStepIntoCompilerOwnedSleepMacro()
    {
        var snapshot = SleepSnapshot();
        var plan = Plan(snapshot.StateHash,
            new SmallModelPlanStep
            {
                StepId = "plan.step.sleep",
                Kind = "sleep"
            });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.sleep", item.OptionId);
        Assert.Equal("compiled_action_steps", item.NormalizedCommand.CommandType);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "compiler_context.is_terminal_step" && parameter.Value == "true");
        var steps = item.NormalizedCommand.Steps;
        Assert.Equal(3, steps.Length);
        Assert.Equal("move_to_bed_adjacent", steps[0].StepType);
        Assert.Equal("FarmHouse(42,23)", steps[0].Target);
        Assert.Equal("step_onto_sleep_touch_tile", steps[1].StepType);
        Assert.Equal("FarmHouse(43,23)", steps[1].Target);
        Assert.Equal("TouchAction=Sleep;menus.sleep_prompt_context.prompt_open=true", steps[1].ExpectedEffect);
        Assert.Equal("confirm_sleep_yes", steps[2].StepType);
        Assert.Equal("menus.sleep_prompt_context", steps[2].Target);
        Assert.Equal("day_safely_ended", steps[2].ExpectedEffect);
    }

    [Fact]
    public void CompileBlocksSleepPlanStepWhenItIsNotTerminal()
    {
        var snapshot = SleepSnapshot();
        var plan = Plan(snapshot.StateHash,
            new SmallModelPlanStep
            {
                StepId = "plan.step.sleep",
                Kind = "sleep"
            },
            new SmallModelPlanStep
            {
                StepId = "plan.step.wait",
                Kind = "wait_ticks",
                WaitTicks = 30
            });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Equal("executor.sleep", queue.Items[0].OptionId);
        Assert.Contains("sleep_action_must_be_terminal", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileBlocksDirectSleepActionWithoutTerminalPlanContext()
    {
        var snapshot = SleepSnapshot();

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.sleep"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("sleep_action_must_be_terminal", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileRecoveryLateNightAutoAppendsCompilerOwnedSleepMacro()
    {
        var snapshot = SleepSnapshot();

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "recovery.stabilize_day"), snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("recovery.stabilize_day", item.OptionId);
        var steps = item.NormalizedCommand.Steps;
        Assert.Equal(3, steps.Length);
        Assert.Equal("move_to_bed_adjacent", steps[0].StepType);
        Assert.Equal("FarmHouse(42,23)", steps[0].Target);
        Assert.Equal("step_onto_sleep_touch_tile", steps[1].StepType);
        Assert.Equal("FarmHouse(43,23)", steps[1].Target);
        Assert.Equal("confirm_sleep_yes", steps[2].StepType);
    }

    [Fact]
    public void CompileRecoveryLateNightBlocksWhenOutsideAndRouteGraphIsUnavailable()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":2300,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Town","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":42,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":23,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "home_context": {"value":{"home_available":true,"home_location_id":"FarmHouse","current_location_id":"Town","current_location_is_home":false,"entry_tile_x":27,"entry_tile_y":30,"bed_tile_x":43,"bed_tile_y":23,"bed_tile_has_bed":true,"sleep_executor_enabled":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sleep_prompt_context": {"value":{"prompt_open":false,"can_confirm_sleep":false,"confirm_executor_enabled":false,"confirm_action_key":"Sleep_Yes"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Town","width":70,"height":46,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "recovery.stabilize_day"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("recovery_route_graph_unavailable", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileRecoveryLateNightEmitsOneTransparentConnectorThenReplans()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":2300,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Town","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":42,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":23,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "home_context": {"value":{"home_available":true,"home_location_id":"FarmHouse","current_location_id":"Town","current_location_is_home":false,"bed_tile_x":43,"bed_tile_y":23,"bed_tile_has_bed":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sleep_prompt_context": {"value":{"prompt_open":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Town","width":70,"height":46,"notable_tiles":[{"tile_x":43,"tile_y":23,"collision_blocked":false}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors": {"value":{"location_id":"Town","connectors":[{"kind":"warp","tile_x":43,"tile_y":23,"target_location":"Farm","target_x":10,"target_y":11,"resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_gate_context": {"value":{"location_id":"Town","action_gates":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_graph": {"value":{"edges":[
              {"kind":"warp","from_location":"Town","from_x":43,"from_y":23,"target_location":"Farm","target_x":10,"target_y":11,"resolved":true},
              {"kind":"building_door","from_location":"Farm","from_x":64,"from_y":14,"target_location":"FarmHouse","target_x":3,"target_y":9,"resolved":true}
            ]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "recovery.stabilize_day"), snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "execution_option_id" && parameter.Value == "executor.traverse_connector");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "connector_kind" && parameter.Value == "warp");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "expected_target_location" && parameter.Value == "Farm");
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("traverse_connector", step.StepType);
        Assert.Contains("rolling_horizon_replan=true", step.ExpectedEffect);
    }

    [Fact]
    public void CompileRecoveryLateNightDoesNotUseActiveObjectAsSleepGate()
    {
        var snapshot = SleepSnapshot("(O)472", sleepPromptOpen: false);

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "recovery.stabilize_day"), snapshot);

        Assert.Equal("pending", queue.Status);
        Assert.DoesNotContain("sleep_interact_active_object_must_be_clear", queue.Items[0].BlockingReasons);
        Assert.Contains(queue.Items[0].NormalizedCommand.Steps, step => step.StepType == "step_onto_sleep_touch_tile");
    }

    [Fact]
    public void CompileRecoveryLateNightBlocksWhenSleepPromptIsAlreadyOpen()
    {
        var snapshot = SleepSnapshot("", sleepPromptOpen: true);

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "recovery.stabilize_day"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("sleep_confirm_executor_requires_compiler_terminal_macro", queue.Items[0].BlockingReasons);
    }

    [Theory]
    [InlineData("GameMenu")]
    [InlineData("InventoryMenu")]
    [InlineData("OptionsPage")]
    [InlineData("DialogueBox")]
    public void CompileRecoveryLateNightBlocksSleepMacroWhenAnyMenuIsOpen(string menuType)
    {
        var snapshot = SleepSnapshot(activeMenuOpen: true, activeMenuType: menuType);

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "recovery.stabilize_day"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("sleep_prompt_menu_must_be_clear", queue.Items[0].BlockingReasons);
        Assert.DoesNotContain(queue.Items[0].NormalizedCommand.Steps, step => step.StepType == "move_to_bed_adjacent");
        Assert.DoesNotContain(queue.Items[0].NormalizedCommand.Steps, step => step.StepType == "step_onto_sleep_touch_tile");
    }

    [Fact]
    public void CompileTerminalSleepBlocksWhenInventoryMenuIsOpen()
    {
        var snapshot = SleepSnapshot(activeMenuOpen: true, activeMenuType: "InventoryMenu");
        var plan = Plan(snapshot.StateHash,
            new SmallModelPlanStep
            {
                StepId = "plan.step.sleep",
                Kind = "sleep"
            });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("sleep_prompt_menu_must_be_clear", queue.Items[0].BlockingReasons);
        Assert.DoesNotContain(queue.Items[0].NormalizedCommand.Steps, step => step.StepType == "step_onto_sleep_touch_tile");
    }

    [Fact]
    public void CompileTurnsFaceDirectionPlanIntoExecutorPrimitiveQueueItem()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"FarmHouse","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "facing_direction": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var plan = new SmallModelPlanEnvelope
        {
            PlanId = "plan.test.face",
            SourceModel = "small-model.test",
            StateHash = snapshot.StateHash,
            GoalId = "goal.autonomous.singleplayer",
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef
            {
                ActorId = "training_farmer.main",
                ActorType = "training_farmer",
                ControlSurface = "training_sandbox"
            },
            Steps = new[]
            {
                new SmallModelPlanStep
                {
                    StepId = "plan.step.face.down",
                    Kind = "face_direction",
                    Direction = 2,
                    EstimatedMinutes = 1,
                    ExpectedEffects = new[] { "player_faces_down_or_blocked" }
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.face_direction", item.OptionId);
        Assert.Equal("executor_calibration", item.TrainingRole);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "direction" && parameter.Value == "2");
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("face_direction", step.StepType);
        Assert.Equal("2", step.Target);
        Assert.Equal("player_facing_direction_changed", step.ExpectedEffect);
    }

    [Fact]
    public void CompileTurnsWaitTicksPlanIntoExecutorPrimitiveQueueItem()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var plan = new SmallModelPlanEnvelope
        {
            PlanId = "plan.test.wait",
            SourceModel = "small-model.test",
            StateHash = snapshot.StateHash,
            GoalId = "goal.autonomous.singleplayer",
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef
            {
                ActorId = "training_farmer.main",
                ActorType = "training_farmer",
                ControlSurface = "training_sandbox"
            },
            Steps = new[]
            {
                new SmallModelPlanStep
                {
                    StepId = "plan.step.wait.30",
                    Kind = "wait_ticks",
                    WaitTicks = 30,
                    EstimatedMinutes = 1,
                    ExpectedEffects = new[] { "ticks_elapsed_or_blocked" }
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.wait_ticks", item.OptionId);
        Assert.Equal("executor_calibration", item.TrainingRole);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "wait_ticks" && parameter.Value == "30");
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("wait_ticks", step.StepType);
        Assert.Equal("30", step.Target);
        Assert.Equal("ticks_elapsed_without_mutation", step.ExpectedEffect);
    }

    [Fact]
    public void CompileSelectSafeItemSlotUsesTransparentSafeSlotContext()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "current_tool_index": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"(O)472","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "safe_item_context": {"value":{"active_object_selected":true,"safe_slot_available":true,"safe_slot_index":10,"safe_slot_kind":"empty"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.select_safe_item_slot"), snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.select_safe_item_slot", item.OptionId);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "safe_slot_index" && parameter.Value == "10");
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("select_safe_item_slot", step.StepType);
        Assert.Equal("10", step.Target);
        Assert.Equal("player.current_tool_index=10;player.active_object_qualified_id=null", step.ExpectedEffect);
    }

    [Fact]
    public void CompileBlocksSelectSafeItemSlotWhenSafeSlotUnavailable()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "current_tool_index": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"(O)472","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "safe_item_context": {"value":{"active_object_selected":true,"safe_slot_available":false,"safe_slot_index":null,"safe_slot_kind":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.select_safe_item_slot"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("safe_item_slot_unavailable", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileCloseMenuAllowsWhitelistedActiveMenu()
    {
        var snapshot = CloseMenuSnapshot(true, "GameMenu");

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.close_menu"), snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.close_menu", item.OptionId);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "compiler_context.active_menu_type" && parameter.Value == "GameMenu");
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("close_menu", step.StepType);
        Assert.Equal("active_menu:GameMenu", step.Target);
        Assert.Equal("menus.active_menu.is_open=false", step.ExpectedEffect);
    }

    [Fact]
    public void CompileCloseMenuNoOpsWhenNoMenuIsOpen()
    {
        var snapshot = CloseMenuSnapshot(false, "none");

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.close_menu"), snapshot);

        Assert.Equal("pending", queue.Status);
        var step = Assert.Single(queue.Items[0].NormalizedCommand.Steps);
        Assert.Equal("close_menu", step.StepType);
        Assert.Equal("active_menu:none", step.Target);
    }

    [Fact]
    public void CompileCloseMenuBlocksSleepPrompt()
    {
        var snapshot = CloseMenuSnapshot(true, "DialogueBox", sleepPromptOpen: true);

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.close_menu"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("close_menu_sleep_prompt_unsupported", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileCloseMenuBlocksDialogueWithMissingTransparentFields()
    {
        var snapshot = CloseMenuSnapshot(true, "DialogueBox");

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.close_menu"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("dialogue_close_is_question_field_missing_or_ambiguous", queue.Items[0].BlockingReasons);
        Assert.Contains("dialogue_close_response_count_field_missing_or_ambiguous", queue.Items[0].BlockingReasons);
        Assert.Contains("dialogue_close_transitioning_field_missing_or_ambiguous", queue.Items[0].BlockingReasons);
        Assert.Contains("dialogue_close_character_present_field_missing_or_ambiguous", queue.Items[0].BlockingReasons);
        Assert.Contains("dialogue_close_event_up_field_missing_or_ambiguous", queue.Items[0].BlockingReasons);
        Assert.Contains("dialogue_close_speaker_name_field_missing", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileCloseMenuAllowsSafeOrdinaryDialogue()
    {
        var snapshot = SafeOrdinaryDialogueSnapshot();

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.close_menu"), snapshot);

        Assert.Equal("pending", queue.Status);
        Assert.Empty(queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileCloseMenuBlocksDialogueWithQuestion()
    {
        var snapshot = SafeOrdinaryDialogueSnapshot(inject: @"""dialogue_is_question"": true");

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.close_menu"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("dialogue_close_is_question_true", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileCloseMenuBlocksDialogueWithResponses()
    {
        var snapshot = SafeOrdinaryDialogueSnapshot(inject: @"""dialogue_response_count"": 3");

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.close_menu"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("dialogue_close_responses_present:3", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileCloseMenuBlocksDialogueWithLastQuestionKey()
    {
        var snapshot = SafeOrdinaryDialogueSnapshot(inject: @"""last_question_key"": ""Blacksmith""");

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.close_menu"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(queue.Items[0].BlockingReasons, reason => reason.StartsWith("dialogue_close_last_question_key_present:"));
    }

    [Fact]
    public void CompileCloseMenuBlocksDialogueWithSleepPrompt()
    {
        var snapshot = SafeOrdinaryDialogueSnapshot(inject: @"""is_sleep_prompt"": true");

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.close_menu"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("dialogue_close_is_sleep_prompt", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileCloseMenuBlocksDialogueWithEventUpTrue()
    {
        var snapshot = SafeOrdinaryDialogueSnapshot(inject: @"""event_up"": true");

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.close_menu"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("dialogue_close_event_up_true", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileCloseMenuBlocksDialogueWithEventUpMissing()
    {
        var json = """
        {
          "menus": {
            "active_menu": {"value":{"is_open":true,"type":"DialogueBox","last_question_key":null,"is_sleep_prompt":false,"dialogue_is_question":false,"dialogue_response_count":0,"dialogue_transitioning":false,"dialogue_safety_timer":0,"dialogue_character_present":true,"dialogue_speaker_name":"Lewis"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sleep_prompt_context": {"value":{"prompt_open":false,"can_confirm_sleep":false,"confirm_executor_enabled":false,"confirm_action_key":"Sleep_Yes"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """;
        var snapshot = Snapshot(json);

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.close_menu"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("dialogue_close_event_up_field_missing_or_ambiguous", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileCloseMenuBlocksDialogueWithCharacterPresentFalse()
    {
        var snapshot = SafeOrdinaryDialogueSnapshot(inject: @"""dialogue_character_present"": false");

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.close_menu"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("dialogue_close_character_present_false", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileCloseMenuBlocksDialogueWithSpeakerNameEmpty()
    {
        var snapshot = SafeOrdinaryDialogueSnapshot(inject: @"""dialogue_speaker_name"": """"");

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.close_menu"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("dialogue_close_speaker_name_empty", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileCloseMenuBlocksDialogueWithSpeakerNameMissing()
    {
        var json = """
        {
          "menus": {
            "active_menu": {"value":{"is_open":true,"type":"DialogueBox","last_question_key":null,"is_sleep_prompt":false,"event_up":false,"dialogue_is_question":false,"dialogue_response_count":0,"dialogue_transitioning":false,"dialogue_safety_timer":0,"dialogue_character_present":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sleep_prompt_context": {"value":{"prompt_open":false,"can_confirm_sleep":false,"confirm_executor_enabled":false,"confirm_action_key":"Sleep_Yes"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """;
        var snapshot = Snapshot(json);

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.close_menu"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("dialogue_close_speaker_name_field_missing", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileCloseMenuBlocksUnknownMenuType()
    {
        var snapshot = CloseMenuSnapshot(true, "CraftingPage");

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.close_menu"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("close_menu_type_not_whitelisted", queue.Items[0].BlockingReasons);
    }

    private static SnapshotEnvelope SafeOrdinaryDialogueSnapshot(string inject = "")
    {
        var json = """
        {
          "menus": {
            "active_menu": {"value":{"is_open":true,"type":"DialogueBox","last_question_key":null,"is_sleep_prompt":false,"event_up":false,"dialogue_is_question":false,"dialogue_response_count":0,"dialogue_transitioning":false,"dialogue_safety_timer":0,"dialogue_character_present":true,"dialogue_speaker_name":"Lewis" INJECT},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sleep_prompt_context": {"value":{"prompt_open":false,"can_confirm_sleep":false,"confirm_executor_enabled":false,"confirm_action_key":"Sleep_Yes"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """;
        if (!string.IsNullOrWhiteSpace(inject))
        {
            json = json.Replace("INJECT", "," + inject);
        }
        else
        {
            json = json.Replace("INJECT", "");
        }

        return Snapshot(json);
    }

    [Fact]
    public void CompileBlocksNonMenuPlanStepWhenSnapshotMenuIsOpen()
    {
        var snapshot = MenuAndTimeSnapshot(true, "GameMenu");
        var plan = Plan(snapshot.StateHash,
            new SmallModelPlanStep
            {
                StepId = "plan.step.wait",
                Kind = "wait_ticks",
                WaitTicks = 30
            });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("active_menu_must_be_closed_before_action", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileAllowsPlanStepAfterBalancedCloseMenuStep()
    {
        var snapshot = MenuAndTimeSnapshot(true, "GameMenu");
        var plan = Plan(snapshot.StateHash,
            new SmallModelPlanStep
            {
                StepId = "plan.step.close",
                Kind = "close_menu"
            },
            new SmallModelPlanStep
            {
                StepId = "plan.step.wait",
                Kind = "wait_ticks",
                WaitTicks = 30
            });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.close_menu", queue.Items[0].OptionId);
        Assert.Equal("executor.wait_ticks", queue.Items[1].OptionId);
        Assert.Empty(queue.Items[1].BlockingReasons);
    }

}

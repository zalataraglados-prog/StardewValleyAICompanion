using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class NativeControlSettleRecoveryTests
{
    [Fact]
    public void ExactPostSleepNativeControlGapCompilesToOneBoundedWait()
    {
        AssertBoundedWait(Snapshot());
    }

    [Fact]
    public void ExactV93Iteration240CompilesToOneBoundedWaitWithoutAuthorizingMovement()
    {
        AssertBoundedWait(V93Snapshot());
    }

    [Theory]
    [InlineData("runtime_calibration_compatible", "false")]
    [InlineData("route_timing_ready_now", "true")]
    [InlineData("native_control_gate_open", "true")]
    [InlineData("player_can_move_now", "false")]
    [InlineData("player_using_tool_now", "true")]
    [InlineData("immobilizing_buff_19_active", "true")]
    [InlineData("current_event_active", "true")]
    [InlineData("event_up", "true")]
    [InlineData("game_paused", "true")]
    [InlineData("dialogue_up", "false")]
    [InlineData("global_fade", "true")]
    [InlineData("freeze_controls", "true")]
    [InlineData("is_warping", "true")]
    [InlineData("player_freeze_pause", "0")]
    [InlineData("player_freeze_pause", "null")]
    public void PartialOrUnsafeV93StateDoesNotCreateRecovery(string field, string value)
    {
        var snapshot = V93Snapshot(root =>
            root["state"]!["player"]!["movement_timing_context"]!["value"]![field] = JsonNode.Parse(value));
        Assert.Null(NativeControlSettleRecovery.TryCreate(snapshot));
    }

    [Theory]
    [InlineData("time", "time", "value", "610")]
    [InlineData("time", "time", "status", "\"stale\"")]
    [InlineData("player", "location_id", "value", "\"Town\"")]
    [InlineData("player", "movement_timing_context", "status", "\"unavailable\"")]
    [InlineData("menus", "active_menu", "status", "\"stale\"")]
    public void V93RecoveryRequiresReadablePostSleepContext(string domain, string field, string key, string value)
    {
        var snapshot = V93Snapshot(root => root["state"]![domain]![field]![key] = JsonNode.Parse(value));
        Assert.Null(NativeControlSettleRecovery.TryCreate(snapshot));
    }

    [Theory]
    [InlineData("is_open", "true")]
    [InlineData("type", "\"DialogueBox\"")]
    [InlineData("last_question_key", "\"Other\"")]
    public void V93RecoveryDoesNotAcknowledgeAnActiveOrUnknownDialogue(string field, string value)
    {
        var snapshot = V93Snapshot(root =>
            root["state"]!["menus"]!["active_menu"]!["value"]![field] = JsonNode.Parse(value));
        Assert.Null(NativeControlSettleRecovery.TryCreate(snapshot));
    }

    private static void AssertBoundedWait(SnapshotEnvelope snapshot)
    {
        var source = Assert.IsType<EventCandidate>(
            NativeControlSettleRecovery.TryCreate(snapshot));
        var availability = new OptionAvailabilityEnvelope
        {
            StateHash = snapshot.StateHash,
            CurrentTime = 600,
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "recovery.stabilize_day",
                    Available = true,
                    EventCandidates = new[] { source }
                }
            }
        };
        var candidate = Assert.Single(new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            availability,
            "goal.grandpa_max_score_year3"));

        Assert.Equal(
            NativeControlSettleRecovery.CandidateId,
            candidate.CandidateId);
        Assert.Equal(
            NativeControlSettleRecovery.CandidateKind,
            candidate.Kind);
        var plan = new DailyPlanCompiler().Compile(
            new[] { candidate },
            snapshot.StateHash,
            maxCandidates: 1);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("wait_ticks", step.Kind);
        Assert.Equal(NativeControlSettleRecovery.WaitTicks, step.WaitTicks);
        Assert.Contains("wait_only_recovery_candidate", step.SafetyConstraints);
        Assert.Contains("refresh_snapshot_and_replan", step.FailurePolicy);
        Assert.Contains("fresh_snapshot_replan_required=true", source.ExpectedEffect);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.True(queue.Status == "pending", JsonSerializer.Serialize(new
        {
            queue.Status, queue.CompilerDiagnostics, item.BlockingReasons, item.MissingStateFactors
        }));
        Assert.Equal("pending", item.Status);
        Assert.Equal("executor.wait_ticks", item.OptionId);
        Assert.Contains(item.NormalizedCommand.Parameters, value =>
            value.Name == "wait_ticks" &&
            value.Value == NativeControlSettleRecovery.WaitTicks.ToString());
    }

    [Fact]
    public void ExactDayStartFadeControlGapCreatesOneBoundedWait()
    {
        var snapshot = Snapshot(
            nativeGateOpen: false,
            playerCanMove: true,
            globalFade: true,
            freezePause: 0);

        var source = Assert.IsType<EventCandidate>(
            NativeControlSettleRecovery.TryCreate(snapshot));

        Assert.Equal(NativeControlSettleRecovery.CandidateId, source.CandidateId);
        Assert.Equal(NativeControlSettleRecovery.CandidateKind, source.Kind);
        Assert.Equal(NativeControlSettleRecovery.WaitTicks, source.EstimatedTicks);
        Assert.Contains(source.Parameters, value =>
            value.Name == "execution_option_id" &&
            value.Value == "executor.wait_ticks");
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public void PartialDayStartFadeStateDoesNotCreateRecovery(
        bool nativeGateOpen,
        bool playerCanMove,
        bool globalFade)
    {
        Assert.Null(NativeControlSettleRecovery.TryCreate(Snapshot(
            nativeGateOpen: nativeGateOpen,
            playerCanMove: playerCanMove,
            globalFade: globalFade,
            freezePause: 0)));
    }

    [Theory]
    [InlineData(false, false, true, false, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(true, false, true, true, false)]
    [InlineData(true, false, true, false, true)]
    public void NonExactOrUnsafeMovementStateDoesNotCreateRecovery(
        bool compatible,
        bool ready,
        bool nativeGateOpen,
        bool usingTool,
        bool menuOpen)
    {
        Assert.Null(NativeControlSettleRecovery.TryCreate(Snapshot(
            compatible,
            ready,
            nativeGateOpen,
            usingTool,
            menuOpen)));
    }

    [Fact]
    public void ActiveLegacyDialogueDoesNotCreateNativeControlRecovery()
    {
        Assert.Null(NativeControlSettleRecovery.TryCreate(Snapshot(
            dialogueUp: true)));
    }

    // Relevant fields transcribed verbatim from stage-one-collection-snapshot-0240.json
    // in the frozen v93 run; no dependency on local runtime files in the test suite.
    internal static SnapshotEnvelope V93Snapshot(Action<JsonObject>? change = null)
    {
        var root = JsonNode.Parse("""
        {
          "schema_version":"snapshot.v1","game_version":"1.6.15","game_tick":125695,
          "state_hash":"a2b8e03d61623572a43f546fb80aec76cec6a307196eaba9bf1c9c84016c6492",
          "in_game_time":{"value":600,"status":"available","source":{"kind":"game_object","path":"Game1.timeOfDay"},"adapter":"vanilla_1_6","read_at_tick":125695,"confidence":1},
          "state":{
            "time":{
              "time":{"value":600,"status":"available","source":{"kind":"game_object","path":"Game1.timeOfDay"},"adapter":"vanilla_1_6","read_at_tick":125695,"confidence":1},
              "total_days":{"value":17,"status":"available","source":{"kind":"game_object","path":"Game1.Date.TotalDays"},"adapter":"vanilla_1_6","read_at_tick":125695,"confidence":1}
            },
            "player":{
              "location_id":{"value":"FarmHouse","status":"available","source":{"kind":"game_object","path":"Game1.player.currentLocation.NameOrUniqueName"},"adapter":"vanilla_1_6","read_at_tick":125695,"confidence":1},
              "tile_x":{"value":9,"status":"available","source":{"kind":"game_object","path":"Game1.player.TilePoint.X"},"adapter":"vanilla_1_6","read_at_tick":125695,"confidence":1},
              "tile_y":{"value":9,"status":"available","source":{"kind":"game_object","path":"Game1.player.TilePoint.Y"},"adapter":"vanilla_1_6","read_at_tick":125695,"confidence":1},
              "movement_timing_context":{"status":"available","read_at_tick":125695,"confidence":1,"value":{
                "projection_status":"exact_current_player_native_cardinal_movement_context",
                "base_speed_now":5,"added_speed_now":0,"buff_speed_now":0,"temporary_speed_buff_now":0,
                "current_native_on_foot_speed_scalar":5,"minimum_vanilla_terrain_temporary_speed_buff":-3,
                "permanent_book_speed":0,"riding_horse_now":false,"immobilizing_buff_19_active":false,
                "current_event_active":false,"event_up":false,"game_paused":false,"dialogue_up":true,
                "global_fade":false,"freeze_controls":false,"is_warping":false,"player_freeze_pause":100,
                "native_control_gate_open":false,"player_can_move_now":true,"player_using_tool_now":false,
                "tile_size_pixels":64,"real_milliseconds_per_game_minute":700,
                "native_cardinal_movement_multiplier":0.066,"conservative_on_foot_speed_scalar":2,
                "theoretical_upper_bound_game_minutes_per_tile":0.6926406926406926,
                "runtime_calibration_compatible":true,"route_timing_ready_now":false,
                "scope":"ordinary_on_foot_cardinal_input_without_collision_dialogue_or_clearance_delay",
                "source":"Farmer.getMovementSpeed cardinal non-event branch; Farmer.addedSpeed; HoeDirt.doCollisionAction; Grass.doCollisionAction; Game1.realMilliSecondsPerGameMinute"
              }}
            },
            "menus":{"active_menu":{"status":"available","read_at_tick":125695,"confidence":1,"value":{
              "is_open":false,"type":"none","full_type":null,"last_question_key":"Sleep",
              "is_sleep_prompt":false,"is_tent_sleep_prompt":false
            }}}
          }
        }
        """)!.AsObject();
        change?.Invoke(root);
        return root.Deserialize<SnapshotEnvelope>()!;
    }

    private static SnapshotEnvelope Snapshot(
        bool compatible = true,
        bool ready = false,
        bool nativeGateOpen = true,
        bool usingTool = false,
        bool menuOpen = false,
        bool playerCanMove = false,
        bool globalFade = false,
        int freezePause = -12,
        bool dialogueUp = false)
    {
        var json = $$"""
        {
          "schema_version": "snapshot.v1",
          "state_hash": "state.post-sleep",
          "in_game_time": { "value": 600, "status": "available" },
          "state": {
            "time": {
              "time": { "value": 600, "status": "available" }
            },
            "player": {
              "location_id": { "value": "FarmHouse", "status": "available" },
              "movement_timing_context": {
                "status": "available",
                "value": {
                  "projection_status": "exact_current_player_native_cardinal_movement_context",
                  "scope": "ordinary_on_foot_cardinal_input_without_collision_dialogue_or_clearance_delay",
                  "runtime_calibration_compatible": {{compatible.ToString().ToLowerInvariant()}},
                  "route_timing_ready_now": {{ready.ToString().ToLowerInvariant()}},
                  "native_control_gate_open": {{nativeGateOpen.ToString().ToLowerInvariant()}},
                  "player_can_move_now": {{playerCanMove.ToString().ToLowerInvariant()}},
                  "player_using_tool_now": {{usingTool.ToString().ToLowerInvariant()}},
                  "immobilizing_buff_19_active": false,
                  "current_event_active": false,
                  "event_up": false,
                  "game_paused": false,
                  "dialogue_up": {{dialogueUp.ToString().ToLowerInvariant()}},
                  "global_fade": {{globalFade.ToString().ToLowerInvariant()}},
                  "freeze_controls": false,
                  "is_warping": false,
                  "player_freeze_pause": {{freezePause}}
                }
              }
            },
            "menus": {
              "active_menu": {
                "status": "available",
                "value": { "is_open": {{menuOpen.ToString().ToLowerInvariant()}}, "type": "none" }
              }
            }
          }
        }
        """;
        return JsonSerializer.Deserialize<SnapshotEnvelope>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
}

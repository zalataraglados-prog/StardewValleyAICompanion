using System.Text.Json;
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
        var snapshot = Snapshot();
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
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
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

    private static SnapshotEnvelope Snapshot(
        bool compatible = true,
        bool ready = false,
        bool nativeGateOpen = true,
        bool usingTool = false,
        bool menuOpen = false,
        bool playerCanMove = false,
        bool globalFade = false,
        int freezePause = -12)
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

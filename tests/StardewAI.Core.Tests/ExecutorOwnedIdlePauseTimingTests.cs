using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Training;
using StardewAI.RuntimePrimitives;

namespace StardewAI.Core.Tests;

public sealed class ExecutorOwnedIdlePauseTimingTests
{
    [Fact]
    public void ActualSmapiReadApiIsTopLevelPublicAndOnlyExportsTheReadMethod()
    {
        var type = typeof(ExecutorPlanningPauseReadApi);
        Assert.True(type.IsPublic);
        Assert.False(type.IsNested);
        var methods = type.GetMethods(System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
        Assert.Equal("ReadPlanningPauseEvidence", Assert.Single(methods).Name);
        var calls = 0;
        var api = new ExecutorPlanningPauseReadApi(() => { calls++; return "read-only"; });
        Assert.Equal("read-only", api.ReadPlanningPauseEvidence());
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ExactOwnedPauseLoadsCalibrationWithoutClaimingCurrentNativeReadiness()
    {
        var context = Context();
        var result = Load(context);
        Assert.Equal(FutureRouteTimingCalibrationLoadStatus.Loaded, result.Status);
        Assert.NotNull(result.Calibration);
        Assert.False(context["value"]!["route_timing_ready_now"]!.GetValue<bool>());
        Assert.True(context["value"]!["game_paused"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("game_paused", "false")]
    [InlineData("player_can_move_now", "false")]
    [InlineData("player_using_tool_now", "true")]
    [InlineData("player_freeze_pause", "1")]
    [InlineData("dialogue_up", "true")]
    [InlineData("global_fade", "true")]
    [InlineData("freeze_controls", "true")]
    [InlineData("is_warping", "true")]
    [InlineData("current_event_active", "true")]
    [InlineData("event_up", "true")]
    [InlineData("immobilizing_buff_19_active", "true")]
    [InlineData("player_id", "\"another-player\"")]
    [InlineData("game_unique_id", "\"another-world\"")]
    [InlineData("runtime_calibration_compatible", "false")]
    [InlineData("theoretical_upper_bound_game_minutes_per_tile", "1.01")]
    public void OwnedPauseDoesNotOverrideNativeOrCalibrationRejections(string field, string json)
    {
        var context = Context();
        context["value"]![field] = JsonNode.Parse(json);
        Assert.Equal(FutureRouteTimingCalibrationLoadStatus.Blocked, Load(context).Status);
    }

    [Theory]
    [InlineData("schema_version", "\"unknown\"")]
    [InlineData("owner_mod_id", "\"some-other-mod\"")]
    [InlineData("release_policy", "\"eventually\"")]
    [InlineData("applied", "false")]
    [InlineData("applied", "\"true\"")]
    [InlineData("player_id", "\"other\"")]
    [InlineData("game_unique_id", "\"other\"")]
    [InlineData("pause_epoch", "0")]
    [InlineData("pause_epoch", "-1")]
    [InlineData("pause_epoch", "\"1\"")]
    [InlineData("pause_epoch", "null")]
    public void MissingOrMismatchedOwnerEvidenceFailsClosed(string field, string json)
    {
        var context = Context();
        var pause = context["value"]!["executor_owned_idle_pause"]!;
        pause[field] = JsonNode.Parse(json);
        Assert.Equal(FutureRouteTimingCalibrationLoadStatus.Blocked, Load(context).Status);
        pause.AsObject().Remove(field);
        Assert.Equal(FutureRouteTimingCalibrationLoadStatus.Blocked, Load(context).Status);
    }

    [Fact]
    public void OrdinaryPauseWithoutApiEvidenceStillBlocks()
    {
        var context = Context();
        context["value"]!.AsObject().Remove("executor_owned_idle_pause");
        var result = Load(context);
        Assert.Equal(FutureRouteTimingCalibrationLoadStatus.Blocked, result.Status);
        Assert.Contains("future_route_movement_timing_context_not_ready", result.BlockingReasons);
    }

    private static FutureRouteTimingCalibrationLoadResult Load(JsonNode context) =>
        new FutureRouteTimingCalibrationLoader().Load(
            FutureRouteDateEvidenceProducerTests.CalibrationArtifactJson(),
            JsonSerializer.SerializeToElement(context), "1.6.15", 6);

    private static JsonNode Context()
    {
        var context = JsonNode.Parse(FutureRouteDateEvidenceProducerTests.MovementTimingContext(ready: false).GetRawText())!;
        var movement = context["value"]!;
        movement["player_id"] = "123";
        movement["game_unique_id"] = "456";
        movement["game_paused"] = true;
        movement["player_can_move_now"] = true;
        movement["player_using_tool_now"] = false;
        movement["player_freeze_pause"] = 0;
        foreach (var field in new[] { "dialogue_up", "global_fade", "freeze_controls", "is_warping",
                     "current_event_active", "event_up", "immobilizing_buff_19_active" })
            movement[field] = false;
        movement["executor_owned_idle_pause"] = new JsonObject
        {
            ["schema_version"] = "executor_owned_idle_pause.v1",
            ["owner_mod_id"] = "StardewAI.RuntimeTestHarness",
            ["release_policy"] = "release_before_pending_execution_update",
            ["applied"] = true, ["player_id"] = "123", ["game_unique_id"] = "456", ["pause_epoch"] = 1
        };
        return context;
    }
}

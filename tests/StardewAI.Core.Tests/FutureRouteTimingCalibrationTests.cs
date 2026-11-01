using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class FutureRouteTimingCalibrationTests
{
    [Fact]
    public void PassedRuntimeArtifactAndCompatibleCurrentStateLoadAsConservativeEvidence()
    {
        var result = new FutureRouteTimingCalibrationLoader().Load(
            FutureRouteDateEvidenceProducerTests.CalibrationArtifactJson(),
            FutureRouteDateEvidenceProducerTests.MovementTimingContext(),
            "1.6.15",
            412);

        Assert.Equal(FutureRouteTimingCalibrationLoadStatus.Loaded, result.Status);
        var calibration = Assert.IsType<FutureRouteTimingCalibration>(result.Calibration);
        Assert.True(calibration.StateComplete);
        Assert.Equal(412, calibration.TotalDays);
        Assert.Equal(223, calibration.CalibrationCaptureTotalDays);
        Assert.Equal((1, 1), (
            calibration.GameMinuteNumeratorPerTile,
            calibration.GameMinuteDenominatorPerTile));
        Assert.Equal(2, calibration.ConnectorTransitionGameMinutes);
        Assert.Equal("1.6.15", calibration.GameVersion);
        Assert.Equal(64, calibration.EvidenceSha256.Length);
        Assert.Contains(calibration.EvidenceSha256, calibration.EvidenceId);
    }

    [Fact]
    public void ArtifactForAnotherGameVersionFailsClosed()
    {
        var result = new FutureRouteTimingCalibrationLoader().Load(
            FutureRouteDateEvidenceProducerTests.CalibrationArtifactJson(),
            FutureRouteDateEvidenceProducerTests.MovementTimingContext(),
            "1.6.16",
            12);

        Assert.Equal(FutureRouteTimingCalibrationLoadStatus.Blocked, result.Status);
        Assert.Null(result.Calibration);
        Assert.Contains(
            "future_route_timing_calibration_game_version_mismatch",
            result.BlockingReasons);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void CurrentMovementStateOutsideCalibrationScopeFailsClosed(
        bool compatible,
        bool ready)
    {
        var result = new FutureRouteTimingCalibrationLoader().Load(
            FutureRouteDateEvidenceProducerTests.CalibrationArtifactJson(),
            FutureRouteDateEvidenceProducerTests.MovementTimingContext(
                compatible,
                ready),
            "1.6.15",
            12);

        Assert.Equal(FutureRouteTimingCalibrationLoadStatus.Blocked, result.Status);
        Assert.Contains(
            "future_route_movement_timing_context_outside_calibration_scope",
            result.BlockingReasons);
    }

    [Fact]
    public void ExactV93TransientControlStateBlocksMovementWithoutMislabelingCalibration()
    {
        var snapshot = NativeControlSettleRecoveryTests.V93Snapshot();
        var result = new FutureRouteTimingCalibrationLoader().Load(
            FutureRouteDateEvidenceProducerTests.CalibrationArtifactJson(),
            snapshot.State["player"].GetProperty("movement_timing_context"),
            snapshot.GameVersion,
            17);

        Assert.Equal(FutureRouteTimingCalibrationLoadStatus.Blocked, result.Status);
        Assert.Null(result.Calibration);
        Assert.Equal(new[] { "future_route_movement_timing_context_not_ready" }, result.BlockingReasons);
    }

    [Theory]
    [InlineData("runtime_calibration_compatible", "false")]
    [InlineData("theoretical_upper_bound_game_minutes_per_tile", "1.01")]
    [InlineData("scope", "\"unsupported_scope\"")]
    public void V93ControlRecoveryCannotAuthorizeIncompatibleTiming(string field, string value)
    {
        var snapshot = NativeControlSettleRecoveryTests.V93Snapshot(root =>
            root["state"]!["player"]!["movement_timing_context"]!["value"]![field] =
                System.Text.Json.Nodes.JsonNode.Parse(value));
        var result = new FutureRouteTimingCalibrationLoader().Load(
            FutureRouteDateEvidenceProducerTests.CalibrationArtifactJson(),
            snapshot.State["player"].GetProperty("movement_timing_context"), "1.6.15", 17);

        Assert.Equal(FutureRouteTimingCalibrationLoadStatus.Blocked, result.Status);
        Assert.Null(result.Calibration);
        Assert.Contains("future_route_movement_timing_context_outside_calibration_scope", result.BlockingReasons);
    }

    [Fact]
    public void SettledV93ContextMustBeReboundBeforeMovementCalibrationLoads()
    {
        var snapshot = NativeControlSettleRecoveryTests.V93Snapshot(root =>
        {
            // Model the required subsequent observation, not native progress
            // claimed from the frozen v93 snapshot or from elapsed wait ticks.
            root["state_hash"] = "state.v93-fresh-settled";
            root["game_tick"] = 125725;
            var movement = root["state"]!["player"]!["movement_timing_context"]!["value"]!;
            movement["dialogue_up"] = false;
            movement["player_freeze_pause"] = 0;
            movement["native_control_gate_open"] = true;
            movement["route_timing_ready_now"] = true;
        });
        Assert.Null(NativeControlSettleRecovery.TryCreate(snapshot));
        var result = new FutureRouteTimingCalibrationLoader().Load(
            FutureRouteDateEvidenceProducerTests.CalibrationArtifactJson(),
            snapshot.State["player"].GetProperty("movement_timing_context"), "1.6.15", 17);

        Assert.Equal(FutureRouteTimingCalibrationLoadStatus.Loaded, result.Status);
        Assert.NotNull(result.Calibration);
        Assert.Empty(result.BlockingReasons);
    }

    [Fact]
    public void ProvenZeroGameMinuteShopMenuCloseAllowsTemporarilyPausedMovement()
    {
        var result = new FutureRouteTimingCalibrationLoader().Load(
            FutureRouteDateEvidenceProducerTests.CalibrationArtifactJson(),
            FutureRouteDateEvidenceProducerTests.MovementTimingContext(
                compatible: true,
                ready: false),
            "1.6.15",
            12,
            zeroGameMinuteShopMenuCloseRecoveryAvailable: true);

        Assert.Equal(FutureRouteTimingCalibrationLoadStatus.Loaded, result.Status);
        Assert.NotNull(result.Calibration);
    }

    [Fact]
    public void ShopMenuCloseRecoveryDoesNotBypassIncompatibleCalibration()
    {
        var result = new FutureRouteTimingCalibrationLoader().Load(
            FutureRouteDateEvidenceProducerTests.CalibrationArtifactJson(),
            FutureRouteDateEvidenceProducerTests.MovementTimingContext(
                compatible: false,
                ready: false),
            "1.6.15",
            12,
            zeroGameMinuteShopMenuCloseRecoveryAvailable: true);

        Assert.Equal(FutureRouteTimingCalibrationLoadStatus.Blocked, result.Status);
        Assert.Contains(
            "future_route_movement_timing_context_outside_calibration_scope",
            result.BlockingReasons);
    }
}

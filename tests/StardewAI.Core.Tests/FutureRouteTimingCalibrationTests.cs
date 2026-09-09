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
    [InlineData(true, false)]
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
}

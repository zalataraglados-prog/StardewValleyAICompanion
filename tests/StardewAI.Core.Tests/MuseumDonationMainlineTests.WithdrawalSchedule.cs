using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Strategy;

namespace StardewAI.Core.Tests;

public sealed partial class MuseumDonationMainlineTests
{
    [Fact]
    public void LocalWithdrawalUsesTheCompiledStandAndCompleteInteractionBeforeMuseumTravel()
    {
        var snapshot = RouteScheduleSnapshot();
        var before = JsonSerializer.Serialize(snapshot, JsonOptions);
        var schedule = new MuseumDonationRouteScheduleBuilder(snapshot, FutureRouteDateEvidenceProducerTests.CalibrationArtifactJson())
            .BuildWithdrawal(TransferStand(), "Town", 8, 10, 1);

        Assert.True(schedule.CurrentDayWithdrawalDonationRouteReady, string.Join(";", schedule.BlockingReasons));
        Assert.Equal(1000, schedule.ChestArrivalByTime);
        Assert.Equal(1008, schedule.WithdrawalCompletionByTime);
        Assert.Equal(9, schedule.WithdrawalStandTileX);
        Assert.Equal(10, schedule.WithdrawalStandTileY);
        Assert.Equal(1008, schedule.DonationRouteSchedule!.DepartureTime);
        Assert.True(schedule.RequiresFreshTransferPreflight);
        Assert.False(schedule.NativeWithdrawalVerified);
        Assert.Equal(before, JsonSerializer.Serialize(snapshot, JsonOptions));
    }

    [Fact]
    public void RemoteWithdrawalProvesBothLegsFromTheActualSelectedFirstEdge()
    {
        var snapshot = RouteScheduleSnapshot();
        var step = Assert.Single(RouteCandidates(snapshot));
        var schedule = new MuseumDonationRouteScheduleBuilder(snapshot, FutureRouteDateEvidenceProducerTests.CalibrationArtifactJson())
            .BuildWithdrawal(step, "Road", 5, 11, 1);

        Assert.True(schedule.CurrentDayWithdrawalDonationRouteReady, string.Join(";", schedule.BlockingReasons));
        Assert.Single(Assert.Single(schedule.ChestApproachScenario!.ProducedPaths!));
        Assert.Equal("Road", schedule.DonationRouteSchedule!.RouteScenario!.StartLocation);
        Assert.Equal(schedule.WithdrawalCompletionByTime, schedule.DonationRouteSchedule.DepartureTime);
    }

    [Fact]
    public void WithdrawalDelayCannotBorrowAnEarlierMuseumEntranceTime()
    {
        var snapshot = RouteScheduleSnapshot(close: 1015);
        var step = Assert.Single(RouteCandidates(snapshot));
        var builder = new MuseumDonationRouteScheduleBuilder(snapshot, FutureRouteDateEvidenceProducerTests.CalibrationArtifactJson());
        Assert.True(builder.Build(step).CurrentDayRouteReady);
        var schedule = builder.BuildWithdrawal(step, "Road", 5, 11, 1);

        Assert.False(schedule.CurrentDayWithdrawalDonationRouteReady);
        Assert.Equal(1010, schedule.WithdrawalCompletionByTime);
        Assert.Contains(schedule.BlockingReasons, reason => reason.StartsWith("future_route_connector_closed_before_arrival", StringComparison.Ordinal));
    }

    [Fact]
    public void AFeasibleUnselectedPrefixCannotProveTheSelectedTripToTheChest()
    {
        var snapshot = RouteScheduleSnapshot(alternative: true);
        var builder = new MuseumDonationRouteScheduleBuilder(snapshot, FutureRouteDateEvidenceProducerTests.CalibrationArtifactJson());
        var candidates = RouteCandidates(snapshot);
        Assert.True(builder.BuildWithdrawal(candidates.Single(row => row.TileX == 10), "Road", 5, 11, 1).CurrentDayWithdrawalDonationRouteReady);
        var wrong = builder.BuildWithdrawal(candidates.Single(row => row.TileX == 11), "Road", 5, 11, 1);

        Assert.False(wrong.CurrentDayWithdrawalDonationRouteReady);
        Assert.Null(wrong.ChestApproachScenario);
        Assert.NotEmpty(wrong.BlockingReasons);
    }

    [Theory]
    [InlineData("duplicate_stand", "museum_withdrawal_compiled_stand_mismatch")]
    [InlineData("wrong_chest", "museum_withdrawal_compiled_stand_mismatch")]
    [InlineData("invalid_quantity", "museum_withdrawal_target_invalid")]
    [InlineData("clearance", "museum_withdrawal_navigation_timing_unavailable")]
    public void InvalidOrUnprovedWithdrawalDetailsFailClosed(string mode, string expected)
    {
        var candidate = TransferStand();
        if (mode == "duplicate_stand") candidate.Parameters = candidate.Parameters.Append(candidate.Parameters[0]).ToArray();
        if (mode == "wrong_chest") candidate.TileX = 7;
        if (mode == "clearance") candidate.Kind = "clear_route_obstacle";
        var result = new MuseumDonationRouteScheduleBuilder(RouteScheduleSnapshot(), FutureRouteDateEvidenceProducerTests.CalibrationArtifactJson())
            .BuildWithdrawal(candidate, "Town", 8, 10, mode == "invalid_quantity" ? 0 : 1);

        Assert.False(result.CurrentDayWithdrawalDonationRouteReady);
        Assert.Contains(expected, result.BlockingReasons);
    }

    private static PolicyEventCandidatePrediction TransferStand() => new()
    {
        CandidateId = "synthetic_transfer", Kind = "transfer_inventory_item", LocationId = "Town", TileX = 8, TileY = 10,
        Parameters = new[]
        {
            new SmallModelActionParameter { Name = "stand_tile_x", Value = "9" },
            new SmallModelActionParameter { Name = "stand_tile_y", Value = "10" }
        }
    };
}

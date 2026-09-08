using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class NpcFuturePresenceWindowResolverTests
{
    [Fact]
    public void ExactTravelTimingProducesOnlyStationaryEndpointWindows()
    {
        var schedule = Schedule(
            Endpoint(0, 900, 910, "SeedShop", 39, 5),
            Endpoint(1, 1030, 1050, "Town", 73, 54),
            Endpoint(2, 1630, 1640, "SeedShop", 3, 6));

        var result = new NpcFuturePresenceWindowResolver().Resolve(schedule);

        Assert.Equal(NpcFuturePresenceWindowResolutionStatus.Exact, result.Status);
        Assert.Equal(3, result.Windows.Length);
        Assert.Equal((910, 1030, "SeedShop"),
            (result.Windows[0].WindowStartTime,
             result.Windows[0].WindowEndTimeExclusive,
             result.Windows[0].LocationName));
        Assert.All(result.Windows, window => Assert.True(window.HasStableInterval));
    }

    [Fact]
    public void TransitThatRunsIntoNextDepartureIsReportedAsNonStable()
    {
        var schedule = Schedule(
            Endpoint(0, 900, 1100, "Town", 1, 1),
            Endpoint(1, 1030, 1040, "Forest", 2, 2));

        var result = new NpcFuturePresenceWindowResolver().Resolve(schedule);

        Assert.Equal(NpcFuturePresenceWindowResolutionStatus.Exact, result.Status);
        Assert.False(result.Windows[0].HasStableInterval);
        Assert.True(result.Windows[1].HasStableInterval);
    }

    [Fact]
    public void StaticEndpointsWithoutNativeTravelEvidenceRemainBlocked()
    {
        var schedule = Schedule(Endpoint(0, 900, null, "SeedShop", 39, 5));
        schedule.TravelTimesResolved = false;

        var result = new NpcFuturePresenceWindowResolver().Resolve(schedule);

        Assert.Equal(NpcFuturePresenceWindowResolutionStatus.Blocked, result.Status);
        Assert.Equal(
            "future_presence_travel_timing_incomplete",
            Assert.Single(result.BlockingReasons));
    }

    [Fact]
    public void ExplicitInitialPositionEndsWhenFirstMovementStarts()
    {
        var initial = Endpoint(-1, 0, null, "SeedShop", 1, 9);
        initial.IsInitialPosition = true;
        var schedule = Schedule(
            initial,
            Endpoint(0, 900, 910, "Town", 73, 54));

        var result = new NpcFuturePresenceWindowResolver().Resolve(schedule);

        Assert.Equal((600, 900, true),
            (result.Windows[0].WindowStartTime,
             result.Windows[0].WindowEndTimeExclusive,
             result.Windows[0].IsInitialPosition));
    }

    private static NpcFutureScheduleResolution Schedule(
        params NpcFutureScheduleEndpoint[] endpoints) => new()
    {
        Status = NpcFutureScheduleResolutionStatus.Exact,
        NpcName = "Abigail",
        SelectedScheduleKey = "spring",
        ResolvedScheduleKey = "spring",
        TravelTimesResolved = true,
        Endpoints = endpoints
    };

    private static NpcFutureScheduleEndpoint Endpoint(
        int ordinal,
        int departure,
        int? arrival,
        string location,
        int x,
        int y) => new()
    {
        ScheduleEntryOrdinal = ordinal,
        ScheduledDepartureTime = departure,
        ScheduledArrivalTime = arrival,
        NativeTravelGameMinutes = arrival.HasValue ? 10 : null,
        LocationName = location,
        TileX = x,
        TileY = y,
        FacingDirection = 2,
        EndBehaviorComplete = true
    };
}

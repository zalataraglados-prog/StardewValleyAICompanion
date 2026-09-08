using System.Text.Json;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class NpcCurrentScheduleProjectionVerifierTests
{
    [Fact]
    public void ExactStaticProjectionMatchesCurrentNativeScheduleAndCapturesRouteCounts()
    {
        var projection = Projection("spring", "spring");
        var native = JsonSerializer.SerializeToElement(new[]
        {
            new
            {
                name = "Abigail",
                schedule_key = "spring",
                follow_schedule = true,
                ignore_schedule_today = false,
                schedule_loaded = true,
                entries = new[]
                {
                    new { time = 900, target_location_name = "SeedShop", target_tile_x = 39, target_tile_y = 5, facing_direction = 0, route_count = 18 },
                    new { time = 1030, target_location_name = "SeedShop", target_tile_x = 2, target_tile_y = 20, facing_direction = 3, route_count = 42 }
                }
            }
        });

        var result = new NpcCurrentScheduleProjectionVerifier().Verify(projection, native);

        Assert.Equal("pass", result.Status);
        Assert.Equal(2, result.VerifiedEntryCount);
        Assert.Equal(new[] { 18, 42 }, result.NativeRoutePointCounts);
    }

    [Fact]
    public void NativeScheduleKeyMatchesOriginallySelectedKeyRatherThanGotoTarget()
    {
        var projection = Projection("11_6", "spring");
        var native = Native("11_6", 900, "SeedShop", 39, 5, 0);

        var result = new NpcCurrentScheduleProjectionVerifier().Verify(projection, native);

        Assert.Equal("pass", result.Status);
        Assert.Equal("11_6", result.NativeScheduleKey);
        Assert.Equal("spring", result.ProjectedResolvedScheduleKey);
    }

    [Fact]
    public void EndpointMismatchFailsClosed()
    {
        var projection = Projection("spring", "spring");
        var native = Native("spring", 900, "Town", 39, 5, 0);

        var result = new NpcCurrentScheduleProjectionVerifier().Verify(projection, native);

        Assert.Equal("mismatch", result.Status);
        Assert.Equal("native_schedule_endpoint_mismatch:0", Assert.Single(result.Issues));
    }

    [Fact]
    public void ConditionalFutureProjectionCannotBePromotedByCurrentVerifier()
    {
        var projection = Projection("rain", "rain");
        projection.Status = NpcFutureScheduleResolutionStatus.Conditional;

        var result = new NpcCurrentScheduleProjectionVerifier().Verify(projection, Native("rain", 900, "SeedShop", 39, 5, 0));

        Assert.Equal("blocked", result.Status);
        Assert.Equal("schedule_projection_not_exact", Assert.Single(result.Issues));
    }

    [Fact]
    public void InconsistentProjectedTravelArrivalFailsClosed()
    {
        var projection = Projection("spring", "spring");
        projection.TravelTimesResolved = true;
        foreach (var endpoint in projection.Endpoints)
        {
            endpoint.NativeRoutePointCount = endpoint.ScheduleEntryOrdinal == 0 ? 18 : 42;
            endpoint.NativeAdjacentRoutePixelDistance = endpoint.ScheduleEntryOrdinal == 0
                ? 14 * 64
                : 38 * 64;
            endpoint.NativeTravelGameMinutes = 10;
            endpoint.ScheduledArrivalTime = endpoint.ScheduleEntryOrdinal == 0 ? 999 : 1040;
        }
        var native = JsonSerializer.SerializeToElement(new[]
        {
            new
            {
                name = "Abigail",
                schedule_key = "spring",
                follow_schedule = true,
                ignore_schedule_today = false,
                schedule_loaded = true,
                entries = new[]
                {
                    new { time = 900, target_location_name = "SeedShop", target_tile_x = 39, target_tile_y = 5, facing_direction = 0, route_count = 18, adjacent_route_pixel_distance = 14 * 64 },
                    new { time = 1030, target_location_name = "SeedShop", target_tile_x = 2, target_tile_y = 20, facing_direction = 3, route_count = 42, adjacent_route_pixel_distance = 38 * 64 }
                }
            }
        });

        var result = new NpcCurrentScheduleProjectionVerifier().Verify(
            projection,
            native);

        Assert.Equal("mismatch", result.Status);
        Assert.Equal(
            "native_schedule_travel_path_evidence_mismatch:0",
            Assert.Single(result.Issues));
    }

    private static NpcFutureScheduleResolution Projection(string selectedKey, string resolvedKey) => new()
    {
        Status = NpcFutureScheduleResolutionStatus.Exact,
        NpcName = "Abigail",
        SelectedScheduleKey = selectedKey,
        ResolvedScheduleKey = resolvedKey,
        NativePathRoutesResolved = false,
        ArrivalTimesResolved = false,
        Endpoints = new[]
        {
            new NpcFutureScheduleEndpoint
            {
                CommandIndex = 0,
                ScheduledDepartureTime = 900,
                ScheduleEntryOrdinal = 0,
                LocationName = "SeedShop",
                TileX = 39,
                TileY = 5,
                FacingDirection = 0
            },
            new NpcFutureScheduleEndpoint
            {
                CommandIndex = 1,
                ScheduledDepartureTime = 1030,
                ScheduleEntryOrdinal = 1,
                LocationName = "SeedShop",
                TileX = 2,
                TileY = 20,
                FacingDirection = 3
            }
        }
    };

    private static JsonElement Native(
        string key,
        int time,
        string location,
        int tileX,
        int tileY,
        int facing) => JsonSerializer.SerializeToElement(new[]
    {
        new
        {
            name = "Abigail",
            schedule_key = key,
            follow_schedule = true,
            ignore_schedule_today = false,
            schedule_loaded = true,
            entries = new[]
            {
                new { time, target_location_name = location, target_tile_x = tileX, target_tile_y = tileY, facing_direction = facing, route_count = 18 },
                new { time = 1030, target_location_name = "SeedShop", target_tile_x = 2, target_tile_y = 20, facing_direction = 3, route_count = 42 }
            }
        }
    });
}

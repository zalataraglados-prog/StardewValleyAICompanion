using System.Linq;
using System.Text.Json;
using StardewAI.Core.Training;
using Xunit;

namespace StardewAI.Core.Tests;

public sealed class NpcCurrentLoadedSchedulePresenceResolverTests
{
    [Fact]
    public void ResolvesRemainingWindowsFromCurrentLoadedNativeSchedule()
    {
        using var snapshot = BuildSnapshot(
            gameTime: 910,
            contextTime: 910,
            adjacentPixels: 14 * 64);

        var result = new NpcCurrentLoadedSchedulePresenceResolver().Resolve(
            snapshot.RootElement,
            "Marnie");

        Assert.Equal("pass", result.Status);
        Assert.Equal(
            "ExactCurrentLoadedNativeSchedule",
            result.ProjectionStatus);
        var presence = Assert.IsType<NpcFuturePresenceWindowResolution>(
            result.Presence);
        Assert.Equal(
            NpcFuturePresenceWindowResolutionStatus.Exact,
            presence.Status);
        Assert.Equal("Sun", presence.SelectedScheduleKey);
        Assert.Collection(
            presence.Windows,
            current =>
            {
                Assert.Equal(910, current.WindowStartTime);
                Assert.Equal(1200, current.WindowEndTimeExclusive);
                Assert.True(current.HasStableInterval);
                Assert.True(current.EndpointBehaviorComplete);
            },
            sleep =>
            {
                Assert.Equal(1200, sleep.WindowStartTime);
                Assert.Equal(2600, sleep.WindowEndTimeExclusive);
                Assert.True(sleep.HasStableInterval);
                Assert.False(sleep.EndpointBehaviorComplete);
            });
    }

    [Fact]
    public void RejectsSnapshotAndScheduleContextTimeMismatch()
    {
        using var snapshot = BuildSnapshot(
            gameTime: 910,
            contextTime: 900,
            adjacentPixels: 14 * 64);

        var result = new NpcCurrentLoadedSchedulePresenceResolver().Resolve(
            snapshot.RootElement,
            "Marnie");

        Assert.Equal("blocked", result.Status);
        Assert.Contains(
            "current_loaded_schedule_snapshot_inputs_incomplete",
            result.BlockingReasons);
    }

    [Fact]
    public void RejectsNativeTimingThatCannotRepresentWholeTiles()
    {
        using var snapshot = BuildSnapshot(
            gameTime: 910,
            contextTime: 910,
            adjacentPixels: 65);

        var result = new NpcCurrentLoadedSchedulePresenceResolver().Resolve(
            snapshot.RootElement,
            "Marnie");

        Assert.Equal("blocked", result.Status);
        Assert.Equal(
            "current_loaded_schedule_entry_invalid:0",
            Assert.Single(result.BlockingReasons));
    }

    [Fact]
    public void PreservesNativeSpecialSleepFacingValue()
    {
        using var snapshot = BuildSnapshot(
            gameTime: 910,
            contextTime: 910,
            adjacentPixels: 14 * 64,
            sleepFacing: 11);

        var result = new NpcCurrentLoadedSchedulePresenceResolver().Resolve(
            snapshot.RootElement,
            "Marnie");

        Assert.Equal("pass", result.Status);
        var presence = Assert.IsType<NpcFuturePresenceWindowResolution>(
            result.Presence);
        Assert.Equal(11, presence.Windows.Last().FacingDirection);
        Assert.False(presence.Windows.Last().EndpointBehaviorComplete);
    }

    private static JsonDocument BuildSnapshot(
        int gameTime,
        int contextTime,
        int adjacentPixels,
        int sleepFacing = 3)
    {
        return JsonDocument.Parse(
            $$"""
            {
              "state": {
                "time": {
                  "total_days": { "status": "available", "value": 223 },
                  "time": { "status": "available", "value": {{gameTime}} }
                },
                "npcs": {
                  "schedule_catalog": {
                    "status": "available",
                    "value": {
                      "current_selection_context": {
                        "capture_total_days": 223,
                        "game_time": {{contextTime}},
                        "real_milliseconds_per_game_ten_minutes": 7000
                      }
                    }
                  },
                  "schedules": {
                    "status": "available",
                    "value": [
                      {
                        "name": "Marnie",
                        "schedule_key": "Sun",
                        "follow_schedule": true,
                        "ignore_schedule_today": false,
                        "schedule_loaded": true,
                        "entries": [
                          {
                            "time": 900,
                            "target_location_name": "AnimalShop",
                            "target_tile_x": 12,
                            "target_tile_y": 14,
                            "facing_direction": 2,
                            "end_behavior": null,
                            "end_message": null,
                            "route_count": 14,
                            "adjacent_route_pixel_distance": {{adjacentPixels}}
                          },
                          {
                            "time": 1200,
                            "target_location_name": "AnimalShop",
                            "target_tile_x": 12,
                            "target_tile_y": 5,
                            "facing_direction": {{sleepFacing}},
                            "end_behavior": "marnie_sleep",
                            "end_message": null,
                            "route_count": 0,
                            "adjacent_route_pixel_distance": 0
                          }
                        ]
                      }
                    ]
                  }
                }
              }
            }
            """);
    }
}

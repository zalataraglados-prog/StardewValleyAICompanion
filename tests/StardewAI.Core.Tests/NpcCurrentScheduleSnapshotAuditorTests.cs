using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class NpcCurrentScheduleSnapshotAuditorTests
{
    [Fact]
    public void DayStartSnapshotVerifiesExactNativeSchedule()
    {
        var snapshot = Snapshot(
            new Dictionary<string, string>
            {
                ["spring"] = "900 SeedShop 39 5 0/1030 SeedShop 2 20 3"
            },
            "spring",
            new[]
            {
                new { time = 900, target_location_name = "SeedShop", target_tile_x = 39, target_tile_y = 5, facing_direction = 0, route_count = 18, adjacent_route_pixel_distance = 14 * 64 },
                new { time = 1030, target_location_name = "SeedShop", target_tile_x = 2, target_tile_y = 20, facing_direction = 3, route_count = 42, adjacent_route_pixel_distance = 38 * 64 }
            });

        var report = new NpcCurrentScheduleSnapshotAuditor().Audit(snapshot);

        Assert.Equal("pass", report.Status);
        Assert.Equal(1, report.VerifiedScheduleCount);
        Assert.Equal(0, report.MismatchCount);
        Assert.Equal(2, Assert.Single(report.Rows).VerifiedEntryCount);
        Assert.Equal(2, report.VerifiedTravelTimeEntryCount);
    }

    [Fact]
    public void RealizedRain2BranchIsVerifiedWithoutPromotingFutureRandomness()
    {
        var snapshot = Snapshot(
            new Dictionary<string, string>
            {
                ["rain"] = "900 SeedShop 9 5 0",
                ["rain2"] = "900 SeedShop 34 5 0",
                ["spring"] = "900 SeedShop 39 5 0"
            },
            "rain2",
            new[]
            {
                new { time = 900, target_location_name = "SeedShop", target_tile_x = 34, target_tile_y = 5, facing_direction = 0, route_count = 18, adjacent_route_pixel_distance = 14 * 64 }
            },
            raining: true);

        var report = new NpcCurrentScheduleSnapshotAuditor().Audit(snapshot);

        Assert.Equal("pass", report.Status);
        var row = Assert.Single(report.Rows);
        Assert.True(row.RealizedRandomBranch);
        Assert.Equal("Conditional", row.ProjectionStatus);
        Assert.Equal("rain2", row.NativeScheduleKey);
    }

    [Fact]
    public void NonDayStartSnapshotFailsClosed()
    {
        var snapshot = Snapshot(
            new Dictionary<string, string> { ["spring"] = "900 SeedShop 39 5 0" },
            "spring",
            new[]
            {
                new { time = 900, target_location_name = "SeedShop", target_tile_x = 39, target_tile_y = 5, facing_direction = 0, route_count = 18, adjacent_route_pixel_distance = 14 * 64 }
            },
            gameTime: 610);

        var report = new NpcCurrentScheduleSnapshotAuditor().Audit(snapshot);

        Assert.Equal("blocked", report.Status);
        Assert.Equal("current_schedule_audit_requires_day_start_0600", Assert.Single(report.Issues));
    }

    [Fact]
    public void ArrivalTimeProjectionRebindsNativeRoutePixelsAndDepartureTime()
    {
        var snapshot = Snapshot(
            new Dictionary<string, string>
            {
                ["spring"] = "a1000 Town 10 10 2"
            },
            "spring",
            new object[]
            {
                new
                {
                    time = 950,
                    target_location_name = "Town",
                    target_tile_x = 10,
                    target_tile_y = 10,
                    facing_direction = 2,
                    route_count = 16,
                    adjacent_route_pixel_distance = 14 * 64
                }
            });

        var report = new NpcCurrentScheduleSnapshotAuditor().Audit(snapshot);

        Assert.Equal("pass", report.Status);
        Assert.Equal(1, report.VerifiedScheduleCount);
        Assert.Equal(0, report.MismatchCount);
        Assert.Equal(1, report.VerifiedArrivalTimeEntryCount);
    }

    private static JsonElement Snapshot(
        IReadOnlyDictionary<string, string> rawEntries,
        string scheduleKey,
        object[] nativeEntries,
        bool raining = false,
        int gameTime = 600)
    {
        var npcName = "Abigail";
        var assetName = "Characters/schedules/Abigail";
        var entries = rawEntries
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new
            {
                schedule_key = entry.Key,
                raw_schedule = entry.Value,
                raw_schedule_sha256 = Sha256(entry.Value)
            })
            .ToArray();
        var fingerprint = new StringBuilder()
            .Append(npcName).Append('\u001f')
            .Append(assetName).Append('\u001f')
            .Append("<present>").Append('\u001e');
        foreach (var entry in entries)
        {
            fingerprint.Append(npcName).Append('\u001f')
                .Append(entry.schedule_key).Append('\u001f')
                .Append(entry.raw_schedule_sha256).Append('\u001e');
        }

        var catalog = new
        {
            population_owner = "Utility.ForEachVillager(includeEventActors:false)",
            selection_owner = "NPC.TryLoadSchedule",
            parser_owner = "NPC.parseMasterSchedule",
            projection_status = "complete_live_master_schedule_catalog_conditional_future_resolution_pending",
            catalog_sha256 = Sha256(fingerprint.ToString()),
            current_selection_context = new
            {
                status = "complete_live_current_inputs_except_unobserved_rain2_roll",
                year = 1,
                season = "spring",
                day_of_month = 1,
                weekday = "Mon",
                game_time = gameTime,
                real_milliseconds_per_game_ten_minutes = 7000,
                day_start_capture = gameTime == 600,
                is_green_rain = false,
                valley_is_raining = raining,
                active_passive_festivals = Array.Empty<object>(),
                current_player_mail_received = Array.Empty<string>(),
                master_player_mail_received = Array.Empty<string>(),
                mail_or_world_state_conditions = Array.Empty<object>(),
                location_accessibility = new[]
                {
                    new { location_name = "CommunityCenter", accessible = false },
                    new { location_name = "JojaMart", accessible = true },
                    new { location_name = "Railroad", accessible = false }
                }
            },
            villagers = new[]
            {
                new
                {
                    npc_name = npcName,
                    is_villager = true,
                    event_actor = false,
                    schedule_asset_name = assetName,
                    default_map = "SeedShop",
                    default_tile_x = 1,
                    default_tile_y = 9,
                    currently_married = false,
                    island_schedule_name = string.Empty,
                    all_player_friendship_points = 0,
                    maximum_farmer_friendship_hearts = 0,
                    current_location_weather_available = true,
                    current_location_is_raining = raining,
                    master_schedule_present = true,
                    master_schedule_entry_count = entries.Length,
                    master_schedule_entries = entries
                }
            }
        };
        var schedules = new[]
        {
            new
            {
                name = npcName,
                schedule_key = scheduleKey,
                follow_schedule = true,
                ignore_schedule_today = false,
                schedule_loaded = true,
                entries = nativeEntries
            }
        };
        return JsonSerializer.SerializeToElement(new
        {
            state = new
            {
                npcs = new
                {
                    schedule_catalog = new { status = "available", value = catalog },
                    schedules = new { status = "available", value = schedules }
                }
            }
        });
    }

    private static string Sha256(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

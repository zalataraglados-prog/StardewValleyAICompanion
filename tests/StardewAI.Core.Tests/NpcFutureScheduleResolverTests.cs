using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class NpcFutureScheduleResolverTests
{
    private const string AbigailSpring =
        "900 SeedShop 39 5 0/1030 SeedShop 2 20 3/1300 Town 73 54 2/1630 SeedShop 3 6 0 abigail_videogames/1930 SeedShop 1 9 3 abigail_sleep";

    [Fact]
    public void ExactDateEntryIsSelectedAndParsedFromLockedAbigailAsset()
    {
        var catalog = Catalog("Abigail", "SeedShop", 1, 9, new Dictionary<string, string>
        {
            ["spring"] = AbigailSpring,
            ["spring_4"] = "900 SeedShop 11 5 0 \"Strings\\schedules\\Abigail:spring_4.000\"/1230 Hospital 13 14 0 \"Strings\\schedules\\Abigail:spring_4.001\"/1330 Hospital 4 6 1 \"Strings\\schedules\\Abigail:spring_4.002\"/1600 SeedShop 10 5 0/2000 SeedShop 1 9 3 abigail_sleep"
        });

        var result = new NpcFutureScheduleResolver().Resolve(catalog, "Abigail", Scenario(4, "Thu"));

        Assert.Equal(NpcFutureScheduleResolutionStatus.Exact, result.Status);
        Assert.Equal("spring_4", result.SelectedScheduleKey);
        Assert.Equal("spring_4", result.ResolvedScheduleKey);
        Assert.Equal(5, result.Endpoints.Length);
        Assert.Equal((1230, "Hospital", 13, 14),
            (result.Endpoints[1].ScheduledDepartureTime, result.Endpoints[1].LocationName, result.Endpoints[1].TileX, result.Endpoints[1].TileY));
    }

    [Fact]
    public void HeartSpecificDateGotoUsesNativeTwoHeartSelectorStep()
    {
        var catalog = Catalog("Abigail", "SeedShop", 1, 9, new Dictionary<string, string>
        {
            ["11_6"] = "GOTO spring",
            ["spring"] = AbigailSpring
        });
        var scenario = Scenario(11, "Thu");
        scenario.AllPlayerFriendshipPoints = 1500;

        var result = new NpcFutureScheduleResolver().Resolve(catalog, "Abigail", scenario);

        Assert.Equal(NpcFutureScheduleResolutionStatus.Exact, result.Status);
        Assert.Equal("11_6", result.SelectedScheduleKey);
        Assert.Equal("spring", result.ResolvedScheduleKey);
        Assert.Contains("goto:spring", result.ResolutionTrace);
    }

    [Theory]
    [InlineData(5, "11")]
    [InlineData(6, "spring")]
    public void NotFriendshipControlMatchesLockedAbigailFallback(int sebastianHearts, string resolvedKey)
    {
        var catalog = Catalog("Abigail", "SeedShop", 1, 9, new Dictionary<string, string>
        {
            ["11"] = "NOT friendship Sebastian 6/1000 SebastianRoom 5 4 2 abigail_sit_down/1700 SeedShop 1 9 3 abigail_sleep",
            ["spring"] = AbigailSpring
        });
        var scenario = Scenario(11, "Thu");
        scenario.MaximumFarmerFriendshipHearts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Sebastian"] = sebastianHearts
        };

        var result = new NpcFutureScheduleResolver().Resolve(catalog, "Abigail", scenario);

        Assert.Equal(NpcFutureScheduleResolutionStatus.Exact, result.Status);
        Assert.Equal(resolvedKey, result.ResolvedScheduleKey);
        Assert.Equal(resolvedKey == "11" ? "SebastianRoom" : "SeedShop", result.Endpoints[0].LocationName);
    }

    [Theory]
    [InlineData(false, "Sun_normal")]
    [InlineData(true, "Sun")]
    public void MailControlUsesMasterMailOrWorldStateBeforeParsingEndpoints(bool mailReceived, string resolvedKey)
    {
        var catalog = Catalog("Kent", "SamHouse", 21, 5, new Dictionary<string, string>
        {
            ["spring"] = "700 Town 42 102 2/1030 SamHouse 8 12 2/1400 Town 6 72 1/1700 SamHouse 2 21 0/1900 SamHouse 8 5 3/2100 Town 6 89 2/2300 SamHouse 21 5 3 kent_sleep",
            ["Sun"] = "MAIL saloonSportsRoom/GOTO Sun_normal/800 SamHouse 8 5 3/830 SeedShop 36 20 0/1110 Saloon 35 8 0/1500 SamHouse 2 21 0/1900 SamHouse 8 5 3/2100 Town 6 89 2/2300 SamHouse 21 5 3 kent_sleep",
            ["Sun_normal"] = "800 SamHouse 8 5 3/1010 SeedShop 36 20 0/1400 SeedShop 5 19 0/1600 SamHouse 2 21 0/1900 SamHouse 8 5 3/2100 Town 6 89 2/2300 SamHouse 21 5 3 kent_sleep"
        });
        var scenario = Scenario(7, "Sun");
        if (mailReceived)
            scenario.MasterPlayerMailReceived!.Add("saloonSportsRoom");

        var result = new NpcFutureScheduleResolver().Resolve(catalog, "Kent", scenario);

        Assert.Equal(NpcFutureScheduleResolutionStatus.Exact, result.Status);
        Assert.Equal("Sun", result.SelectedScheduleKey);
        Assert.Equal(resolvedKey, result.ResolvedScheduleKey);
        Assert.Equal(800, result.Endpoints[0].ScheduledDepartureTime);
    }

    [Fact]
    public void UnknownRain2RollProducesTwoExplicitAlternatives()
    {
        var catalog = Catalog("Abigail", "SeedShop", 1, 9, new Dictionary<string, string>
        {
            ["rain"] = "900 SeedShop 9 5 0/1100 SeedShop 13 20 0/2200 SeedShop 1 9 3 abigail_sleep",
            ["rain2"] = "900 SeedShop 34 5 0/1400 Saloon 42 17 2 abigail_sit_down/2000 SeedShop 1 9 3 abigail_sleep",
            ["spring"] = AbigailSpring
        });
        var scenario = Scenario(2, "Tue");
        scenario.NpcLocationIsRaining = true;
        scenario.Rain2Roll = null;

        var result = new NpcFutureScheduleResolver().Resolve(catalog, "Abigail", scenario);

        Assert.Equal(NpcFutureScheduleResolutionStatus.Conditional, result.Status);
        Assert.Equal("rain2_random_branch_unknown", Assert.Single(result.BlockingReasons));
        Assert.Equal(new[] { "rain2", "rain" }, result.Alternatives.Select(row => row.SelectedScheduleKey).ToArray());
        Assert.All(result.Alternatives, row => Assert.Equal(NpcFutureScheduleResolutionStatus.Exact, row.Status));
    }

    [Fact]
    public void InaccessibleJojaMartUsesLockedPamReplacementEntry()
    {
        var catalog = Catalog("Pam", "Trailer", 15, 4, new Dictionary<string, string>
        {
            ["JojaMart_Replacement"] = "SeedShop 2 24 3",
            ["spring"] = "800 Trailer 15 4 2 pam_sit_down/1200 JojaMart 6 19 1/1600 Saloon 7 18 1/2400 Trailer 15 4 2 pam_sleep"
        });
        var scenario = Scenario(1, "Mon");
        scenario.CurrentPlayerMailReceived = new HashSet<string>(StringComparer.Ordinal);
        scenario.LocationAccessibility = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["JojaMart"] = false
        };

        var result = new NpcFutureScheduleResolver().Resolve(catalog, "Pam", scenario);

        Assert.Equal(NpcFutureScheduleResolutionStatus.Exact, result.Status);
        var replacement = Assert.Single(result.Endpoints, endpoint => endpoint.ScheduledDepartureTime == 1200);
        Assert.Equal(("SeedShop", 2, 24, 3),
            (replacement.LocationName, replacement.TileX, replacement.TileY, replacement.FacingDirection));
    }

    [Fact]
    public void ArrivalTimeCommandFromLockedMaruAssetBlocksInsteadOfGuessingPathLength()
    {
        var catalog = Catalog("Maru", "ScienceHouse", 2, 4, new Dictionary<string, string>
        {
            ["DesertFestival_2"] = "610 ScienceHouse 6 4 0/a1000 Desert 40 41 1 square_1_5_1 \"Strings\\1_6_Strings:DesertFestival_Maru\"/2350 bed",
            ["spring"] = "900 ScienceHouse 2 4 2/2200 ScienceHouse 2 4 2"
        });
        var scenario = Scenario(15, "Mon");
        scenario.ActivePassiveFestivals = new[]
        {
            new NpcPassiveFestivalScenario { FestivalId = "DesertFestival", DayIndex = 2 }
        };

        var result = new NpcFutureScheduleResolver().Resolve(catalog, "Maru", scenario);

        Assert.Equal(NpcFutureScheduleResolutionStatus.Blocked, result.Status);
        Assert.Equal("DesertFestival_2", result.SelectedScheduleKey);
        Assert.Equal("future_schedule_arrival_time_requires_native_path_length", Assert.Single(result.BlockingReasons));
    }

    [Fact]
    public void ArrivalTimeUsesBoundNativeAdjacentRoutePixelsAndNativeClockFormula()
    {
        var catalog = Catalog("Maru", "ScienceHouse", 2, 4, new Dictionary<string, string>
        {
            ["DesertFestival_2"] = "610 ScienceHouse 6 4 0/a1000 Desert 40 41 1 square_1_5_1 \"Strings\\1_6_Strings:DesertFestival_Maru\"/2350 bed",
            ["spring"] = "900 ScienceHouse 2 4 2"
        });
        var scenario = Scenario(15, "Mon");
        scenario.ActivePassiveFestivals = new[]
        {
            new NpcPassiveFestivalScenario { FestivalId = "DesertFestival", DayIndex = 2 }
        };
        scenario.RealMillisecondsPerGameTenMinutes = 7000;
        scenario.NativePathTimings = new[]
        {
            new NpcSchedulePathTimingEvidence
            {
                ScheduleKey = "DesertFestival_2",
                ScheduleEntryOrdinal = 1,
                TargetLocationName = "Desert",
                TargetTileX = 40,
                TargetTileY = 41,
                FacingDirection = 1,
                NativeRoutePointCount = 16,
                AdjacentRoutePixelDistance = 14 * 64
            }
        };

        var result = new NpcFutureScheduleResolver().Resolve(catalog, "Maru", scenario);

        Assert.Equal(NpcFutureScheduleResolutionStatus.Exact, result.Status);
        Assert.True(result.ArrivalTimesResolved);
        var arrival = Assert.Single(result.Endpoints, endpoint => endpoint.ArrivalTimeRequested);
        Assert.Equal(1000, arrival.RequestedArrivalTime);
        Assert.Equal(950, arrival.ScheduledDepartureTime);
        Assert.Equal(10, arrival.NativeTravelGameMinutes);
        Assert.Equal(1000, arrival.ScheduledArrivalTime);
        Assert.Equal(14 * 64, arrival.NativeAdjacentRoutePixelDistance);
    }

    [Fact]
    public void CompleteNativePathEvidenceResolvesEveryOrdinaryEndpointArrival()
    {
        var catalog = Catalog("Abigail", "SeedShop", 1, 9, new Dictionary<string, string>
        {
            ["spring"] = "900 SeedShop 39 5 0/1030 Town 73 54 2"
        });
        var scenario = Scenario(1, "Mon");
        scenario.RealMillisecondsPerGameTenMinutes = 7000;
        scenario.NativePathTimingEvidenceComplete = true;
        scenario.NativePathTimings = new[]
        {
            PathTiming("spring", 0, "SeedShop", 39, 5, 0, 18, 14 * 64),
            PathTiming("spring", 1, "Town", 73, 54, 2, 34, 28 * 64)
        };

        var result = new NpcFutureScheduleResolver().Resolve(
            catalog,
            "Abigail",
            scenario);

        Assert.Equal(NpcFutureScheduleResolutionStatus.Exact, result.Status);
        Assert.True(result.TravelTimesResolved);
        Assert.Equal(new int?[] { 910, 1050 },
            result.Endpoints.Select(endpoint => endpoint.ScheduledArrivalTime).ToArray());
        Assert.Equal(new int?[] { 10, 20 },
            result.Endpoints.Select(endpoint => endpoint.NativeTravelGameMinutes).ToArray());
    }

    [Fact]
    public void ClaimedCompleteNativePathEvidenceFailsClosedWhenAnEndpointIsMissing()
    {
        var catalog = Catalog("Abigail", "SeedShop", 1, 9, new Dictionary<string, string>
        {
            ["spring"] = "900 SeedShop 39 5 0/1030 Town 73 54 2"
        });
        var scenario = Scenario(1, "Mon");
        scenario.RealMillisecondsPerGameTenMinutes = 7000;
        scenario.NativePathTimingEvidenceComplete = true;
        scenario.NativePathTimings = new[]
        {
            PathTiming("spring", 0, "SeedShop", 39, 5, 0, 18, 14 * 64)
        };

        var result = new NpcFutureScheduleResolver().Resolve(
            catalog,
            "Abigail",
            scenario);

        Assert.Equal(NpcFutureScheduleResolutionStatus.Blocked, result.Status);
        Assert.Equal(
            "future_schedule_arrival_path_timing_evidence_missing",
            Assert.Single(result.BlockingReasons));
    }

    [Fact]
    public void InaccessibleLocationFallbackDiscardsPrefixBeforeRequiringRealizedPathTiming()
    {
        var catalog = Catalog("Penny", "Trailer", 4, 9, new Dictionary<string, string>
        {
            ["spring"] = "800 Town 35 89 2 penny_read/1230 Trailer 12 6 0 penny_dishes",
            ["winter"] = "900 Trailer 1 8 0/1030 CommunityCenter 23 17 2 penny_read"
        });
        var scenario = Scenario(28, "Sun");
        scenario.Season = "winter";
        scenario.LocationAccessibility = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["CommunityCenter"] = false
        };
        scenario.RealMillisecondsPerGameTenMinutes = 7000;
        scenario.NativePathTimingEvidenceComplete = true;
        scenario.NativePathTimings = new[]
        {
            PathTiming("winter", 0, "Town", 35, 89, 2, 80, 78 * 64),
            PathTiming("winter", 1, "Trailer", 12, 6, 0, 63, 61 * 64)
        };

        var result = new NpcFutureScheduleResolver().Resolve(catalog, "Penny", scenario);

        Assert.Equal(NpcFutureScheduleResolutionStatus.Exact, result.Status);
        Assert.Equal("winter", result.SelectedScheduleKey);
        Assert.Equal("spring", result.ResolvedScheduleKey);
        Assert.True(result.TravelTimesResolved);
        Assert.Equal(new[] { "Town", "Trailer" },
            result.Endpoints.Select(endpoint => endpoint.LocationName).ToArray());
        Assert.Contains("inaccessible:CommunityCenter:fallback:spring", result.ResolutionTrace);
    }

    [Fact]
    public void MarriedNpcDoesNotFallThroughToOrdinarySeasonSchedule()
    {
        var catalog = Catalog("Abigail", "SeedShop", 1, 9, new Dictionary<string, string>
        {
            ["spring"] = AbigailSpring
        });
        var scenario = Scenario(1, "Mon");
        scenario.IsMarried = true;

        var result = new NpcFutureScheduleResolver().Resolve(catalog, "Abigail", scenario);

        Assert.Equal(NpcFutureScheduleResolutionStatus.NoSchedule, result.Status);
        Assert.Empty(result.SelectedScheduleKey);
    }

    [Fact]
    public void TamperedRawEntryFailsCatalogIntegrityBeforeSelection()
    {
        var catalog = Catalog(
            "Abigail",
            "SeedShop",
            1,
            9,
            new Dictionary<string, string> { ["spring"] = AbigailSpring },
            tamperRawAfterHash: true);

        var result = new NpcFutureScheduleResolver().Resolve(catalog, "Abigail", Scenario(1, "Mon"));

        Assert.Equal(NpcFutureScheduleResolutionStatus.Blocked, result.Status);
        Assert.Equal("future_schedule_entry_sha256_mismatch", Assert.Single(result.BlockingReasons));
    }

    [Fact]
    public void UnrelatedDuplicateNativeInstancesWithoutSchedulesDoNotInvalidateTargetCatalog()
    {
        var catalog = Catalog(
            "Abigail",
            "SeedShop",
            1,
            9,
            new Dictionary<string, string> { ["spring"] = AbigailSpring },
            includeDuplicateUnscheduledNpc: true);

        var result = new NpcFutureScheduleResolver().Resolve(
            catalog,
            "Abigail",
            Scenario(1, "Mon"));

        Assert.Equal(NpcFutureScheduleResolutionStatus.Exact, result.Status);
        Assert.Equal("spring", result.SelectedScheduleKey);
    }

    private static NpcFutureScheduleScenario Scenario(int day, string weekday) => new()
    {
        Year = 1,
        Season = "spring",
        DayOfMonth = day,
        Weekday = weekday,
        IsGreenRain = false,
        IsMarried = false,
        IslandScheduleStateComplete = true,
        ActivePassiveFestivals = Array.Empty<NpcPassiveFestivalScenario>(),
        AllPlayerFriendshipPoints = 0,
        ValleyIsRaining = false,
        NpcLocationIsRaining = false,
        Rain2Roll = false,
        CurrentPlayerMailReceived = new HashSet<string>(StringComparer.Ordinal),
        MasterPlayerMailReceived = new HashSet<string>(StringComparer.Ordinal),
        WorldStateIds = new HashSet<string>(StringComparer.Ordinal),
        MaximumFarmerFriendshipHearts = new Dictionary<string, int>(StringComparer.Ordinal),
        LocationAccessibility = new Dictionary<string, bool>(StringComparer.Ordinal)
    };

    private static NpcSchedulePathTimingEvidence PathTiming(
        string scheduleKey,
        int ordinal,
        string location,
        int tileX,
        int tileY,
        int facing,
        int routePoints,
        int adjacentPixels) => new()
    {
        ScheduleKey = scheduleKey,
        ScheduleEntryOrdinal = ordinal,
        TargetLocationName = location,
        TargetTileX = tileX,
        TargetTileY = tileY,
        FacingDirection = facing,
        NativeRoutePointCount = routePoints,
        AdjacentRoutePixelDistance = adjacentPixels
    };

    private static JsonElement Catalog(
        string npcName,
        string defaultMap,
        int defaultTileX,
        int defaultTileY,
        IReadOnlyDictionary<string, string> rawEntries,
        bool tamperRawAfterHash = false,
        bool includeDuplicateUnscheduledNpc = false)
    {
        var entries = rawEntries
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new
            {
                schedule_key = entry.Key,
                raw_schedule = tamperRawAfterHash && entry.Key == rawEntries.Keys.First()
                    ? entry.Value + "/9999 Farm 0 0 2"
                    : entry.Value,
                raw_schedule_sha256 = Sha256(entry.Value)
            })
            .ToArray();
        var assetName = "Characters/schedules/" + npcName;
        var fingerprint = new StringBuilder()
            .Append(npcName).Append('\u001f')
            .Append(assetName).Append('\u001f')
            .Append("<present>").Append('\u001e');
        foreach (var entry in rawEntries
                     .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(entry => entry.Key, StringComparer.Ordinal))
        {
            fingerprint
                .Append(npcName).Append('\u001f')
                .Append(entry.Key).Append('\u001f')
                .Append(Sha256(entry.Value)).Append('\u001e');
        }

        if (includeDuplicateUnscheduledNpc)
        {
            fingerprint
                .Append("Mister Qi").Append('\u001f')
                .Append("Characters/schedules/Mister Qi").Append('\u001f')
                .Append("<missing>").Append('\u001e')
                .Append("Mister Qi").Append('\u001f')
                .Append("Characters/schedules/Mister Qi").Append('\u001f')
                .Append("<missing>").Append('\u001e');
        }

        var villagers = new List<object>
        {
            new
            {
                npc_name = npcName,
                is_villager = true,
                event_actor = false,
                schedule_asset_name = assetName,
                default_map = defaultMap,
                default_tile_x = defaultTileX,
                default_tile_y = defaultTileY,
                master_schedule_present = true,
                master_schedule_entry_count = entries.Length,
                master_schedule_entries = entries
            }
        };
        if (includeDuplicateUnscheduledNpc)
        {
            villagers.Add(new
            {
                npc_name = "Mister Qi",
                is_villager = true,
                event_actor = false,
                schedule_asset_name = "Characters/schedules/Mister Qi",
                default_map = "Club",
                default_tile_x = 8,
                default_tile_y = 4,
                master_schedule_present = false,
                master_schedule_entry_count = 0,
                master_schedule_entries = Array.Empty<object>()
            });
            villagers.Add(new
            {
                npc_name = "Mister Qi",
                is_villager = true,
                event_actor = false,
                schedule_asset_name = "Characters/schedules/Mister Qi",
                default_map = "QiNutRoom",
                default_tile_x = 7,
                default_tile_y = 4,
                master_schedule_present = false,
                master_schedule_entry_count = 0,
                master_schedule_entries = Array.Empty<object>()
            });
        }

        return JsonSerializer.SerializeToElement(new
        {
            population_owner = "Utility.ForEachVillager(includeEventActors:false)",
            selection_owner = "NPC.TryLoadSchedule",
            parser_owner = "NPC.parseMasterSchedule",
            catalog_sha256 = Sha256(fingerprint.ToString()),
            projection_status = "complete_live_master_schedule_catalog_conditional_future_resolution_pending",
            villagers = villagers.ToArray()
        });
    }

    private static string Sha256(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

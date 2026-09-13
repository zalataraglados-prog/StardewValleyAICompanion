using System.Text.Json;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class FutureRouteDateEvidenceProducerTests
{
    [Fact]
    public void ExactTargetModeRoutesToTheRequestedStandTile()
    {
        var request = Request();
        request.RequireExactTargetTile = true;

        var production = new FutureRouteDateEvidenceProducer().Produce(
            RouteGraph(),
            DateEvidence(totalDays: 12),
            request,
            Timing());

        Assert.Equal(
            FutureRouteDateEvidenceProductionStatus.Produced,
            production.Status);
        Assert.Equal(906, production.GuaranteedArrivalByTime);
        var approach = Assert.Single(
            Assert.IsType<FutureRouteAccessScenario>(production.Scenario)
                .ApproachEvidence!);
        Assert.Equal(
            (request.TargetTileX, request.TargetTileY),
            (approach.StandTileX, approach.StandTileY));
    }

    [Fact]
    public void ExactTargetModeFailsClosedWhenRequestedStandIsUnreachable()
    {
        var request = Request();
        request.TargetTileX = 99;
        request.RequireExactTargetTile = true;

        var production = new FutureRouteDateEvidenceProducer().Produce(
            RouteGraph(),
            DateEvidence(totalDays: 12),
            request,
            Timing());

        Assert.Equal(
            FutureRouteDateEvidenceProductionStatus.Blocked,
            production.Status);
        Assert.Null(production.GuaranteedArrivalByTime);
        Assert.Contains(
            "future_route_exact_target_tile_unreachable",
            production.BlockingReasons);
    }

    [Fact]
    public void DateBoundNativeGridAndGateProduceConservativeContactProof()
    {
        var graph = RouteGraph();
        var request = Request();
        var production = new FutureRouteDateEvidenceProducer().Produce(
            graph,
            DateEvidence(totalDays: 12),
            request,
            Timing());

        Assert.Equal(FutureRouteDateEvidenceProductionStatus.Produced, production.Status);
        Assert.Equal(1, production.SuccessfulRouteVariantCount);
        var scenario = Assert.IsType<FutureRouteAccessScenario>(production.Scenario);
        var segment = Assert.Single(scenario.SegmentEvidence!);
        Assert.Equal(2, segment.ApproachTravelGameMinutes);
        Assert.Equal((900, 1700), (segment.OpenTime, segment.CloseTimeExclusive));
        Assert.Equal(
            FutureRouteTravelTimingEvidenceKind.ConservativeUpperBound,
            segment.TimingEvidenceKind);

        var route = new FutureRouteAccessWindowResolver().Resolve(
            graph,
            scenario,
            "Town",
            4,
            2);

        Assert.Equal(FutureRouteAccessResolutionStatus.ConservativeUpperBound, route.Status);
        Assert.Null(route.EarliestArrivalTime);
        Assert.Equal(905, route.GuaranteedArrivalByTime);
        Assert.Equal(58, route.WaitGameMinutes);
        Assert.Equal((3, 2), (route.StandTileX, route.StandTileY));

        var presence = new NpcFuturePresenceWindowResolution
        {
            Status = NpcFuturePresenceWindowResolutionStatus.Exact,
            NpcName = "Abigail",
            SelectedScheduleKey = "spring",
            Windows = new[]
            {
                new NpcFuturePresenceWindow
                {
                    ScheduleEntryOrdinal = 1,
                    LocationName = "Town",
                    TileX = 4,
                    TileY = 2,
                    WindowStartTime = 900,
                    WindowEndTimeExclusive = 1000,
                    HasStableInterval = true,
                    EndpointBehaviorComplete = true
                }
            }
        };
        var contact = new NpcFutureContactWindowResolver().Resolve(
            graph,
            scenario,
            presence,
            "talk",
            new[]
            {
                new FutureNpcContactEligibilityEvidence
                {
                    TotalDays = 12,
                    NpcName = "Abigail",
                    SelectedScheduleKey = "spring",
                    ScheduleEntryOrdinal = 1,
                    LocationName = "Town",
                    TileX = 4,
                    TileY = 2,
                    EligibleFromTime = 600,
                    EligibleUntilTimeExclusive = 1000,
                    StateComplete = true,
                    TalkAllowed = true
                }
            });

        Assert.Equal(
            NpcFutureContactWindowResolutionStatus.ConservativeUpperBound,
            contact.Status);
        var window = Assert.Single(contact.Windows);
        Assert.Equal(905, window.EarliestInteractionTime);
        Assert.Equal(
            FutureRouteTravelTimingEvidenceKind.ConservativeUpperBound,
            window.RouteTimingEvidenceKind);
    }

    [Fact]
    public void EvidenceCapturedForAnotherDateFailsClosed()
    {
        var production = new FutureRouteDateEvidenceProducer().Produce(
            RouteGraph(),
            DateEvidence(totalDays: 11),
            Request(),
            Timing());

        Assert.Equal(FutureRouteDateEvidenceProductionStatus.Blocked, production.Status);
        Assert.Contains(
            "social_route_date_evidence_date_or_schema_mismatch",
            production.BlockingReasons);
    }

    [Fact]
    public void FeasibleSearchUsesLongerTopologicalTailWhenShorterTailIsDateBlocked()
    {
        var graph = JsonSerializer.SerializeToElement(new
        {
            edges = new object[]
            {
                Edge("warp", "A", 3, 2, "B", 0, 2),
                Edge("action_warp", "B", 3, 2, "C", 0, 2),
                Edge("warp", "C", 3, 2, "Target", 0, 2),
                Edge("action_warp", "B", 2, 4, "D", 0, 2),
                Edge("warp", "D", 3, 2, "E", 0, 2),
                Edge("warp", "E", 3, 2, "Target", 0, 2)
            }
        });
        var evidence = JsonSerializer.SerializeToElement(new
        {
            schema_version = "social_route_date_evidence.v2",
            capture_total_days = 12,
            all_location_static_walkability_complete = true,
            projection_status =
                "current_date_static_route_and_gate_evidence_complete_movement_timing_pending",
            location_count = 6,
            locations = new object[]
            {
                Location("A", Array.Empty<object>()),
                Location("B", new object[]
                {
                    Gate(3, 2, "C", allowed: false),
                    Gate(2, 4, "D", allowed: true)
                }),
                Location("C", Array.Empty<object>()),
                Location("D", Array.Empty<object>()),
                Location("E", Array.Empty<object>()),
                Location("Target", Array.Empty<object>())
            }
        });
        var request = new FutureRouteDateEvidenceRequest
        {
            TotalDays = 12,
            StartLocation = "A",
            StartTileX = 0,
            StartTileY = 2,
            EarliestDepartureTime = 800,
            TargetLocation = "Target",
            TargetTileX = 2,
            TargetTileY = 2
        };

        var production = new FutureRouteDateEvidenceProducer().Produce(
            graph,
            evidence,
            request,
            Timing());

        Assert.Equal(FutureRouteDateEvidenceProductionStatus.Produced, production.Status);
        var path = Assert.Single(Assert.IsType<FutureRouteAccessScenario>(production.Scenario).ProducedPaths!);
        Assert.Equal(new[] { "A", "B", "D", "E" },
            path.Select(edge => edge.FromLocation).ToArray());
        Assert.Equal("Target", path[^1].TargetLocation);

        var presence = new NpcFuturePresenceWindowResolution
        {
            Status = NpcFuturePresenceWindowResolutionStatus.Exact,
            NpcName = "Abigail",
            SelectedScheduleKey = "spring",
            Windows = new[]
            {
                new NpcFuturePresenceWindow
                {
                    ScheduleEntryOrdinal = 1,
                    LocationName = "Target",
                    TileX = 2,
                    TileY = 2,
                    WindowStartTime = 600,
                    WindowEndTimeExclusive = 1700,
                    HasStableInterval = true,
                    EndpointBehaviorComplete = true
                }
            }
        };
        var verification = new FutureSocialItineraryVerifier().Verify(
            graph,
            Assert.IsType<FutureRouteAccessScenario>(production.Scenario),
            new[]
            {
                new FutureSocialItineraryVisit
                {
                    VisitId = "visit-abigail",
                    NpcName = "Abigail",
                    InteractionKind = "talk",
                    Presence = presence,
                    EligibilityEvidence = new[]
                    {
                        new FutureNpcContactEligibilityEvidence
                        {
                            TotalDays = 12,
                            NpcName = "Abigail",
                            SelectedScheduleKey = "spring",
                            ScheduleEntryOrdinal = 1,
                            LocationName = "Target",
                            TileX = 2,
                            TileY = 2,
                            EligibleFromTime = 600,
                            EligibleUntilTimeExclusive = 1700,
                            StateComplete = true,
                            TalkAllowed = true
                        }
                    }
                }
            });
        Assert.NotEqual(
            FutureSocialItineraryVerificationStatus.Blocked,
            verification.Status);
        Assert.Single(verification.Steps);
    }

    [Fact]
    public void VersionTwoEvidenceGroupsMultipleActionRecordsOnOneBlockedTile()
    {
        var source = UnsupportedActionEvidence(
            new object[]
            {
                UnsupportedTile(2, 2, "Action first", "TouchAction second")
            },
            tileCount: 1,
            recordCount: 2);

        var created = SocialRouteDateEvidenceIndex.TryCreate(
            source,
            12,
            out var index,
            out var reasons);

        Assert.True(created, string.Join(",", reasons));
        Assert.True(index.TryGetMap("A", out var map));
        Assert.Null(map.ShortestDistance(0, 2, 2, 2));
    }

    [Fact]
    public void ObservedNonWalkableOriginCanExitWithoutCrossingBlockedTiles()
    {
        var source = JsonSerializer.SerializeToElement(new
        {
            schema_version = "social_route_date_evidence.v2",
            capture_total_days = 12,
            all_location_static_walkability_complete = true,
            projection_status =
                "current_date_static_route_and_gate_evidence_complete_movement_timing_pending",
            location_count = 1,
            locations = new object[]
            {
                new
                {
                    location_id = "FarmHouse",
                    map_width = 5,
                    map_height = 5,
                    projection_status =
                        "exact_current_date_static_native_walkability",
                    static_walkable_tile_count = 24,
                    static_walkable_tile_ranges = new object[]
                    {
                        new { y = 0, start_x = 0, end_x = 4 },
                        new { y = 1, start_x = 0, end_x = 4 },
                        new { y = 2, start_x = 1, end_x = 4 },
                        new { y = 3, start_x = 0, end_x = 4 },
                        new { y = 4, start_x = 0, end_x = 4 }
                    },
                    build_conditions = (string?)null,
                    build_conditions_met = (bool?)null,
                    unsupported_route_action_record_count = 0,
                    unsupported_route_action_tile_count = 0,
                    unsupported_route_action_tiles = Array.Empty<object>(),
                    action_gates = Array.Empty<object>()
                }
            }
        });

        Assert.True(SocialRouteDateEvidenceIndex.TryCreate(
            source, 12, out var index, out var reasons),
            string.Join(",", reasons));
        Assert.True(index.TryGetMap("FarmHouse", out var map));
        Assert.Equal(2, map.ShortestDistance(0, 2, 2, 2));
        Assert.Null(map.ShortestDistance(-1, 2, 2, 2));
    }

    [Fact]
    public void VersionTwoEvidenceRejectsDuplicateBlockedTileRowsAndRecordCountMismatch()
    {
        var duplicate = UnsupportedActionEvidence(
            new object[]
            {
                UnsupportedTile(2, 2, "Action first"),
                UnsupportedTile(2, 2, "TouchAction second")
            },
            tileCount: 2,
            recordCount: 2);
        var badCount = UnsupportedActionEvidence(
            new object[]
            {
                UnsupportedTile(2, 2, "Action first", "TouchAction second")
            },
            tileCount: 1,
            recordCount: 1);

        Assert.False(SocialRouteDateEvidenceIndex.TryCreate(
            duplicate, 12, out _, out var duplicateReasons));
        Assert.Contains(
            "social_route_date_unsupported_action_tiles_invalid:A",
            duplicateReasons);
        Assert.False(SocialRouteDateEvidenceIndex.TryCreate(
            badCount, 12, out _, out var countReasons));
        Assert.Contains(
            "social_route_date_unsupported_action_tiles_invalid:A",
            countReasons);
    }

    [Fact]
    public void SharedConnectorStandResolverKeepsCompilerAndTeacherSemanticsAligned()
    {
        var boundary = Assert.Single(
            RouteConnectorStandTileResolver.ResolveCandidates(
                "warp", -1, 3, 5, 5));
        Assert.Equal((0, 3), (boundary.X, boundary.Y));

        var building = Assert.Single(
            RouteConnectorStandTileResolver.ResolveCandidates(
                "building_door", 2, 2, 5, 5));
        Assert.Equal((2, 3), (building.X, building.Y));

        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "StardewAI.Core",
            "Execution",
            "ActionQueueCompiler.Routing.cs"));
        Assert.Contains(
            "RouteConnectorStandTileResolver.ResolveCandidates",
            source,
            StringComparison.Ordinal);
    }

    private static FutureRouteDateEvidenceRequest Request() => new()
    {
        TotalDays = 12,
        StartLocation = "FarmHouse",
        StartTileX = 0,
        StartTileY = 2,
        EarliestDepartureTime = 800,
        TargetLocation = "Town",
        TargetTileX = 4,
        TargetTileY = 2
    };

    private static FutureRouteTimingCalibration Timing()
    {
        var loaded = new FutureRouteTimingCalibrationLoader().Load(
            CalibrationArtifactJson(),
            MovementTimingContext(),
            "1.6.15",
            12);
        return Assert.IsType<FutureRouteTimingCalibration>(loaded.Calibration);
    }

    internal static string CalibrationArtifactJson() =>
        JsonSerializer.Serialize(new
        {
            schema_version = "stardewai.runtime_movement_timing_calibration.v1",
            status = "passed",
            game_version = "1.6.15",
            capture_total_days = 223,
            sample_count = 3,
            total_manhattan_tiles = 66,
            maximum_observed_game_minutes_per_tile = 0.29,
            native_milliseconds_per_game_minute = 700,
            conservative_game_minute_numerator_per_tile = 1,
            conservative_game_minute_denominator_per_tile = 1,
            connector_sample_count = 2,
            connector_kinds = new[] { "building_door", "warp" },
            maximum_observed_connector_game_minutes = 1.04,
            conservative_connector_transition_game_minutes = 2,
            calibration_evidence_kind = "conservative_upper_bound",
            connector_transition_game_minutes_status =
                "runtime_proven_building_door_and_step_warp",
            movement_context_projection_status =
                "exact_current_player_native_cardinal_movement_context",
            movement_context_scope =
                "ordinary_on_foot_cardinal_input_without_collision_dialogue_or_clearance_delay",
            source_save_fingerprint_before = "ABC",
            source_save_fingerprint_after = "ABC",
            source_save_untouched = true
        });

    internal static JsonElement MovementTimingContext(
        bool compatible = true,
        bool ready = true) =>
        JsonSerializer.SerializeToElement(new
        {
            status = "available",
            value = new
            {
                projection_status =
                    "exact_current_player_native_cardinal_movement_context",
                runtime_calibration_compatible = compatible,
                route_timing_ready_now = ready,
                real_milliseconds_per_game_minute = 700,
                theoretical_upper_bound_game_minutes_per_tile = 0.7,
                scope =
                    "ordinary_on_foot_cardinal_input_without_collision_dialogue_or_clearance_delay"
            }
        });

    private static JsonElement RouteGraph() => JsonSerializer.SerializeToElement(new
    {
        edges = new[]
        {
            new
            {
                kind = "locked_door_warp",
                from_location = "FarmHouse",
                from_x = 3,
                from_y = 2,
                target_location = "Town",
                target_x = 0,
                target_y = 2,
                resolved = true
            }
        }
    });

    private static JsonElement DateEvidence(int totalDays) =>
        JsonSerializer.SerializeToElement(new
        {
            schema_version = "social_route_date_evidence.v2",
            capture_total_days = totalDays,
            all_location_static_walkability_complete = true,
            projection_status =
                "current_date_static_route_and_gate_evidence_complete_movement_timing_pending",
            location_count = 2,
            locations = new object[]
            {
                Location(
                    "FarmHouse",
                    new object[]
                    {
                        new
                        {
                            kind = "locked_door_warp",
                            tile_x = 3,
                            tile_y = 2,
                            target_location = "Town",
                            allowed_on_capture_date = true,
                            time_unrestricted_on_capture_date = false,
                            effective_open_time = 900,
                            effective_close_time = 1700
                        }
                    }),
                Location("Town", Array.Empty<object>())
            }
        });

    private static object Location(string id, object[] gates) => new
    {
        location_id = id,
        map_width = 5,
        map_height = 5,
        projection_status = "exact_current_date_static_native_walkability",
        static_walkable_tile_count = 25,
        static_walkable_tile_ranges = Enumerable.Range(0, 5)
            .Select(y => new { y, start_x = 0, end_x = 4 })
            .ToArray(),
        build_conditions = (string?)null,
        build_conditions_met = (bool?)null,
        unsupported_route_action_record_count = 0,
        unsupported_route_action_tile_count = 0,
        unsupported_route_action_tiles = Array.Empty<object>(),
        action_gates = gates
    };

    private static object Edge(
        string kind,
        string from,
        int fromX,
        int fromY,
        string target,
        int targetX,
        int targetY) => new
    {
        kind,
        from_location = from,
        from_x = fromX,
        from_y = fromY,
        target_location = target,
        target_x = targetX,
        target_y = targetY,
        resolved = true
    };

    private static object Gate(int x, int y, string target, bool allowed) => new
    {
        kind = "warp_action",
        tile_x = x,
        tile_y = y,
        target_location = target,
        allowed_on_capture_date = allowed
    };

    private static JsonElement UnsupportedActionEvidence(
        object[] tiles,
        int tileCount,
        int recordCount) => JsonSerializer.SerializeToElement(new
    {
        schema_version = "social_route_date_evidence.v2",
        capture_total_days = 12,
        all_location_static_walkability_complete = true,
        projection_status =
            "current_date_static_route_and_gate_evidence_complete_movement_timing_pending",
        location_count = 1,
        locations = new object[]
        {
            new
            {
                location_id = "A",
                map_width = 5,
                map_height = 5,
                projection_status = "exact_current_date_static_native_walkability",
                static_walkable_tile_count = 25,
                static_walkable_tile_ranges = Enumerable.Range(0, 5)
                    .Select(y => new { y, start_x = 0, end_x = 4 })
                    .ToArray(),
                build_conditions = (string?)null,
                build_conditions_met = (bool?)null,
                unsupported_route_action_record_count = recordCount,
                unsupported_route_action_tile_count = tileCount,
                unsupported_route_action_tiles = tiles,
                action_gates = Array.Empty<object>()
            }
        }
    });

    private static object UnsupportedTile(int x, int y, params string[] actions) => new
    {
        tile_x = x,
        tile_y = y,
        action_record_count = actions.Length,
        actions = actions.Select((raw, index) => new
        {
            source_property = index == 0 ? "Buildings.Action" : "Back.TouchAction",
            raw_action = raw
        }).ToArray()
    };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(
                   directory.FullName,
                   "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ??
            throw new InvalidOperationException("Cannot find repository root.");
    }
}

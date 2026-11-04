using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Strategy;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class MuseumDonationMainlineTests
{
    [Fact]
    public void DonationScheduleChecksLaterGateAndCounterInsteadOfOnlyTheFirstConnector()
    {
        var snapshot = RouteScheduleSnapshot();
        var candidate = Assert.Single(RouteCandidates(snapshot));
        var schedule = new MuseumDonationRouteScheduleBuilder(snapshot,
            FutureRouteDateEvidenceProducerTests.CalibrationArtifactJson()).Build(candidate);

        Assert.True(schedule.CurrentDayRouteReady, string.Join(";", schedule.BlockingReasons));
        Assert.Equal(2, schedule.RouteScenario!.SegmentEvidence!.Length);
        Assert.Equal("Road", schedule.RouteScenario.SegmentEvidence[1].FromLocation);
        Assert.True(schedule.CounterArrivalByTime > 1009);
        Assert.Equal(6, StardewAI.Core.Execution.GameClockBudgetPolicy.ClockMinutesBetween(
            schedule.CounterArrivalByTime!.Value, schedule.DonationCompletionByTime!.Value));
        Assert.False(schedule.NativeDonationVerified);
        Assert.False(schedule.NativeKeyHandoffVerified);
    }

    [Fact]
    public void LaterMuseumDoorClosingAtArrivalRejectsTheDeparture()
    {
        var snapshot = RouteScheduleSnapshot(close: 1008);
        var schedule = new MuseumDonationRouteScheduleBuilder(snapshot,
            FutureRouteDateEvidenceProducerTests.CalibrationArtifactJson()).Build(Assert.Single(RouteCandidates(snapshot)));

        Assert.False(schedule.CurrentDayRouteReady);
        Assert.Null(schedule.RouteScenario);
        Assert.Contains(schedule.BlockingReasons, reason => reason.StartsWith("future_route_connector_closed_before_arrival", StringComparison.Ordinal));
    }

    [Fact]
    public void EntryCloseDoesNotBecomeAnIndoorServiceDeadline()
    {
        var snapshot = RouteScheduleSnapshot(close: 1009);
        var schedule = new MuseumDonationRouteScheduleBuilder(snapshot,
            FutureRouteDateEvidenceProducerTests.CalibrationArtifactJson()).Build(Assert.Single(RouteCandidates(snapshot)));

        Assert.True(schedule.CurrentDayRouteReady, string.Join(";", schedule.BlockingReasons));
        Assert.True(schedule.DonationCompletionByTime > 1009);
    }

    [Fact]
    public void AlternativeFirstConnectorHasItsOwnProofAndCannotLendItToTheBlockedPrefix()
    {
        var snapshot = RouteScheduleSnapshot(close: 1008, alternative: true);
        var candidates = RouteCandidates(snapshot);
        Assert.Equal(2, candidates.Length);
        var builder = new MuseumDonationRouteScheduleBuilder(snapshot, FutureRouteDateEvidenceProducerTests.CalibrationArtifactJson());
        var blocked = builder.Build(candidates.Single(candidate => candidate.TileX == 10));
        var legal = builder.Build(candidates.Single(candidate => candidate.TileX == 11));

        Assert.False(blocked.CurrentDayRouteReady);
        Assert.True(legal.CurrentDayRouteReady, string.Join(";", legal.BlockingReasons));
        Assert.Equal("AltRoad", legal.RouteScenario!.ProducedPaths![0][0].TargetLocation);
        Assert.Equal(11, legal.RouteScenario.ProducedPaths[0][0].FromX);
        Assert.Same(legal, builder.Build(candidates.Single(candidate => candidate.TileX == 11)));
    }

    [Fact]
    public void PlannedContinuationStartsAtTheTerminalInsteadOfReusingThePlayerPositionOrClock()
    {
        var snapshot = RouteScheduleSnapshot();
        var before = JsonSerializer.Serialize(snapshot, JsonOptions);
        var builder = new MuseumDonationRouteScheduleBuilder(snapshot, FutureRouteDateEvidenceProducerTests.CalibrationArtifactJson());
        var first = builder.BuildContinuation(12, "Road", 5, 12, 1100);
        var later = builder.BuildContinuation(12, "Road", 5, 12, 1200);

        Assert.True(first.CurrentDayRouteReady, string.Join(";", first.BlockingReasons));
        Assert.True(later.CurrentDayRouteReady, string.Join(";", later.BlockingReasons));
        Assert.Equal("Road", first.RouteScenario!.StartLocation);
        Assert.Equal(1100, first.DepartureTime);
        Assert.Equal(1200, later.DepartureTime);
        Assert.True(later.CounterArrivalByTime > first.CounterArrivalByTime);
        Assert.NotSame(first, later);
        Assert.Same(first, builder.BuildContinuation(12, "Road", 5, 12, 1100));
        Assert.Equal(before, JsonSerializer.Serialize(snapshot, JsonOptions));
    }

    [Theory]
    [InlineData(13, "Road", 5, 12, 1100)]
    [InlineData(12, "Road", 5, 12, 959)]
    [InlineData(12, "Road", 5, 12, 1060)]
    [InlineData(12, "Road", -1, 12, 1100)]
    [InlineData(12, "", 5, 12, 1100)]
    public void PlannedContinuationCannotBorrowAnotherDateOrInvalidEndpoint(int day, string location, int x, int y, int time)
    {
        var builder = new MuseumDonationRouteScheduleBuilder(RouteScheduleSnapshot(), FutureRouteDateEvidenceProducerTests.CalibrationArtifactJson());
        var result = builder.BuildContinuation(day, location, x, y, time);

        Assert.False(result.CurrentDayRouteReady);
        Assert.Contains("museum_donation_planned_departure_invalid", result.BlockingReasons);
        Assert.Null(result.RouteScenario);
    }

    [Theory]
    [InlineData("missing_calibration")]
    [InlineData("missing_date_evidence")]
    [InlineData("stale_date")]
    [InlineData("missing_counter")]
    [InlineData("late_completion")]
    [InlineData("changed_arrival")]
    [InlineData("duplicate_binding")]
    public void DonationRouteDoesNotInventMissingOrChangedEvidence(string mode)
    {
        var snapshot = RouteScheduleSnapshot();
        var candidate = Assert.Single(RouteCandidates(snapshot));
        var state = JsonSerializer.SerializeToNode(snapshot.State, JsonOptions)!.AsObject();
        switch (mode)
        {
            case "missing_date_evidence": state["locations"]!.AsObject().Remove("social_route_date_evidence"); break;
            case "stale_date": state["locations"]!["social_route_date_evidence"]!["value"]!["capture_total_days"] = 13; break;
            case "missing_counter": state["world_progress"]!["museum"]!["value"]!["gunther_action_tile_x"] = null; break;
            case "late_completion":
                state["time"]!["time"]!["value"] = 1850;
                state["locations"]!["social_route_date_evidence"]!["value"]!["locations"]![1]!["action_gates"]![0]!["effective_close_time"] = 2600;
                break;
            case "changed_arrival": candidate.Parameters.Single(parameter => parameter.Name == "expected_arrival_tile_x").Value = "99"; break;
            case "duplicate_binding": candidate.Parameters = candidate.Parameters.Append(candidate.Parameters.Single(parameter => parameter.Name == "connector_kind")).ToArray(); break;
        }
        snapshot.State = state.Deserialize<Dictionary<string, JsonElement>>(JsonOptions)!;
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        var result = new MuseumDonationRouteScheduleBuilder(snapshot,
            mode == "missing_calibration" ? string.Empty : FutureRouteDateEvidenceProducerTests.CalibrationArtifactJson()).Build(candidate);

        Assert.False(result.CurrentDayRouteReady);
        var expected = mode switch
        {
            "missing_calibration" => "future_route_timing_calibration_input_invalid",
            "missing_date_evidence" => "social_route_date_evidence_unavailable",
            "stale_date" => "social_route_date_evidence_date_or_schema_mismatch",
            "missing_counter" => "museum_donation_route_counter_projection_unavailable",
            "late_completion" => "museum_donation_full_route_exceeds_day_budget",
            "changed_arrival" => "future_route_required_first_edge_invalid",
            _ => "museum_donation_route_first_connector_incomplete"
        };
        Assert.Contains(expected, result.BlockingReasons);
        Assert.Null(result.RouteScenario);
    }

    private static PolicyEventCandidatePrediction[] RouteCandidates(SnapshotEnvelope snapshot) =>
        new EventCandidateRanker().Rank(new BaselineTrainingReport(),
            new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "museum.donate_items" }, true));

    private static SnapshotEnvelope RouteScheduleSnapshot(int close = 1700, bool alternative = false)
    {
        var snapshot = RemoteSnapshot(Snapshot(59, 95));
        snapshot.GameVersion = "1.6.15";
        var root = JsonSerializer.SerializeToNode(snapshot.State, JsonOptions)!.AsObject();
        root["time"]!["time"]!["value"] = 1000;
        root["time"]!.AsObject()["total_days"] = FieldNode(12);
        root["player"]!.AsObject()["movement_timing_context"] = JsonSerializer.SerializeToNode(FutureRouteDateEvidenceProducerTests.MovementTimingContext());
        var edges = new List<object>
        {
            Edge("warp", "Town", 10, "Road"), Edge("locked_door_warp", "Road", 10, "ArchaeologyHouse")
        };
        var maps = new List<object>
        {
            Map("Town", Array.Empty<object>()),
            Map("Road", new[] { Gate(close) }), Map("ArchaeologyHouse", Array.Empty<object>())
        };
        var connectors = new List<object> { Connector(10, "Road") };
        if (alternative)
        {
            edges.Add(Edge("warp", "Town", 11, "AltRoad"));
            edges.Add(Edge("warp", "AltRoad", 10, "ArchaeologyHouse"));
            maps.Add(Map("AltRoad", Array.Empty<object>()));
            connectors.Add(Connector(11, "AltRoad"));
        }
        var locations = root["locations"]!.AsObject();
        locations["route_graph"] = FieldNode(new { status = "complete", edges });
        locations["route_connectors"] = FieldNode(new { location_id = "Town", connectors });
        locations["social_route_date_evidence"] = FieldNode(new
        {
            schema_version = "social_route_date_evidence.v2", capture_total_days = 12,
            all_location_static_walkability_complete = true,
            projection_status = "current_date_static_route_and_gate_evidence_complete_movement_timing_pending",
            location_count = maps.Count, locations = maps
        });
        snapshot.State = root.Deserialize<Dictionary<string, JsonElement>>(JsonOptions)!;
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;

        static object Edge(string kind, string from, int x, string target) => new
        {
            kind, from_location = from, from_x = x, from_y = 10,
            target_location = target, target_x = 5, target_y = 12, resolved = true
        };
        static object Connector(int x, string target) => new
        {
            kind = "warp", tile_x = x, tile_y = 10, target_location = target, target_x = 5, target_y = 12, resolved = true
        };
        static object Gate(int close) => new
        {
            kind = "locked_door_warp", tile_x = 10, tile_y = 10, target_location = "ArchaeologyHouse",
            allowed_on_capture_date = true, time_unrestricted_on_capture_date = false,
            effective_open_time = 900, effective_close_time = close
        };
        static object Map(string id, object[] gates) => new
        {
            location_id = id, map_width = 64, map_height = 64,
            projection_status = "exact_current_date_static_native_walkability", static_walkable_tile_count = 4096,
            static_walkable_tile_ranges = Enumerable.Range(0, 64).Select(y => new { y, start_x = 0, end_x = 63 }).ToArray(),
            build_conditions = (string?)null, build_conditions_met = (bool?)null,
            unsupported_route_action_record_count = 0, unsupported_route_action_tile_count = 0,
            unsupported_route_action_tiles = Array.Empty<object>(), action_gates = gates
        };
    }
}

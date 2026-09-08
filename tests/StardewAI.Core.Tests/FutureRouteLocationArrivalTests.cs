using System.Text.Json;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class FutureRouteDateEvidenceProducerTests
{
    [Fact]
    public void LocationArrivalAccumulatesEveryConnectorWithoutInventingTerminalApproach()
    {
        var graph = JsonSerializer.SerializeToElement(new
        {
            edges = new object[]
            {
                Edge("building_door", "Farm", 2, 1, "Town", 0, 2),
                Edge("building_door", "Town", 3, 1, "Beach", 0, 2)
            }
        });
        var evidence = JsonSerializer.SerializeToElement(new
        {
            schema_version = "social_route_date_evidence.v2",
            capture_total_days = 12,
            all_location_static_walkability_complete = true,
            projection_status =
                "current_date_static_route_and_gate_evidence_complete_movement_timing_pending",
            location_count = 3,
            locations = new object[]
            {
                Location("Farm", Array.Empty<object>()),
                Location("Town", Array.Empty<object>()),
                Location("Beach", Array.Empty<object>())
            }
        });

        var result = new FutureRouteDateEvidenceProducer()
            .ProduceLocationArrival(
                graph,
                evidence,
                new FutureLocationRouteDateEvidenceRequest
                {
                    TotalDays = 12,
                    StartLocation = "Farm",
                    StartTileX = 0,
                    StartTileY = 2,
                    EarliestDepartureTime = 800,
                    TargetLocation = "Beach"
                },
                Timing());

        Assert.Equal(
            FutureRouteDateEvidenceProductionStatus.Produced,
            result.Status);
        Assert.Equal(809, result.GuaranteedArrivalByTime);
        Assert.Equal(2, result.Path.Length);
        Assert.Equal(2, result.SegmentEvidence.Length);
        Assert.Equal(new[] { 2, 3 }, result.SegmentEvidence
            .Select(segment => segment.ApproachTravelGameMinutes));
        Assert.All(result.SegmentEvidence, segment =>
            Assert.Equal(2, segment.ConnectorTransitionGameMinutes));
        Assert.Equal(
            "current_date_full_connector_path_to_target_location_without_terminal_local_approach",
            result.Scope);
    }

    [Fact]
    public void LocationArrivalAlreadyAtTargetHasNoSyntheticRouteCost()
    {
        var result = new FutureRouteDateEvidenceProducer()
            .ProduceLocationArrival(
                JsonSerializer.SerializeToElement(new
                {
                    edges = Array.Empty<object>()
                }),
                JsonSerializer.SerializeToElement(new
                {
                    schema_version = "social_route_date_evidence.v2",
                    capture_total_days = 12,
                    all_location_static_walkability_complete = true,
                    projection_status =
                        "current_date_static_route_and_gate_evidence_complete_movement_timing_pending",
                    location_count = 1,
                    locations = new object[]
                    {
                        Location("Beach", Array.Empty<object>())
                    }
                }),
                new FutureLocationRouteDateEvidenceRequest
                {
                    TotalDays = 12,
                    StartLocation = "Beach",
                    StartTileX = 1,
                    StartTileY = 2,
                    EarliestDepartureTime = 2550,
                    TargetLocation = "Beach"
                },
                Timing());

        Assert.Equal(
            FutureRouteDateEvidenceProductionStatus.Produced,
            result.Status);
        Assert.Equal(2550, result.GuaranteedArrivalByTime);
        Assert.Empty(result.Path);
        Assert.Empty(result.SegmentEvidence);
    }

    [Fact]
    public void LocationArrivalBatchFailsClosedWithoutMislabelingValidRows()
    {
        var results = new FutureRouteDateEvidenceProducer()
            .ProduceLocationArrivals(
                JsonSerializer.SerializeToElement(new
                {
                    edges = Array.Empty<object>()
                }),
                DateEvidence(totalDays: 12),
                new[]
                {
                    new FutureLocationRouteDateEvidenceRequest
                    {
                        TotalDays = 12,
                        StartLocation = "Beach",
                        StartTileX = 1,
                        StartTileY = 2,
                        EarliestDepartureTime = 900,
                        TargetLocation = "Beach"
                    },
                    new FutureLocationRouteDateEvidenceRequest
                    {
                        TotalDays = 12,
                        StartLocation = "Beach",
                        StartTileX = 1,
                        StartTileY = 2,
                        EarliestDepartureTime = 900,
                        TargetLocation = string.Empty
                    }
                },
                Timing());

        Assert.Equal(2, results.Length);
        Assert.Contains(
            "future_location_route_batch_contains_invalid_request",
            results[0].BlockingReasons);
        Assert.Contains(
            "future_location_route_evidence_request_invalid",
            results[1].BlockingReasons);
    }
}

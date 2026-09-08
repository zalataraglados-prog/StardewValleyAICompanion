using System.Text.Json;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class FutureRouteAccessWindowResolverTests
{
    [Fact]
    public void DateBoundSegmentsWaitForGateAndReachExactAdjacentStand()
    {
        var graph = RouteGraph();
        var scenario = Scenario();

        var result = new FutureRouteAccessWindowResolver().Resolve(
            graph,
            scenario,
            "Town",
            73,
            54);

        Assert.Equal(FutureRouteAccessResolutionStatus.Exact, result.Status);
        Assert.Equal(920, result.EarliestArrivalTime);
        Assert.Equal(150, result.WaitGameMinutes);
        Assert.Equal(2, result.Path.Length);
        Assert.Equal((72, 54), (result.StandTileX, result.StandTileY));
    }

    [Fact]
    public void EvidenceFromAnotherDateCannotAuthorizeRoute()
    {
        var scenario = Scenario();
        scenario.SegmentEvidence![1].TotalDays++;

        var result = new FutureRouteAccessWindowResolver().Resolve(
            RouteGraph(),
            scenario,
            "Town",
            73,
            54);

        Assert.Equal(FutureRouteAccessResolutionStatus.Blocked, result.Status);
        Assert.Contains("future_route_segment_evidence_missing", result.BlockingReasons);
    }

    [Fact]
    public void MissingFinalTileApproachCannotBeReplacedByMapTopology()
    {
        var scenario = Scenario();
        scenario.ApproachEvidence = Array.Empty<FutureRouteApproachEvidence>();

        var result = new FutureRouteAccessWindowResolver().Resolve(
            RouteGraph(),
            scenario,
            "Town",
            73,
            54);

        Assert.Equal(FutureRouteAccessResolutionStatus.Blocked, result.Status);
        Assert.Contains(
            "future_route_target_approach_evidence_missing",
            result.BlockingReasons);
    }

    [Fact]
    public void NonAdjacentFinalStandCannotAuthorizeInteractionRoute()
    {
        var scenario = Scenario();
        scenario.ApproachEvidence![0].StandTileX = 70;
        scenario.ApproachEvidence[0].StandTileY = 54;

        var result = new FutureRouteAccessWindowResolver().Resolve(
            RouteGraph(),
            scenario,
            "Town",
            73,
            54);

        Assert.Equal(FutureRouteAccessResolutionStatus.Blocked, result.Status);
        Assert.Contains(
            "future_route_target_stand_evidence_invalid",
            result.BlockingReasons);
    }

    [Fact]
    public void SameMapRouteStillRequiresExactAdjacentStandEvidence()
    {
        var scenario = new FutureRouteAccessScenario
        {
            TotalDays = 12,
            StartLocation = "Town",
            StartTileX = 10,
            StartTileY = 20,
            EarliestDepartureTime = 800,
            SegmentEvidence = Array.Empty<FutureRouteSegmentEvidence>(),
            ApproachEvidence = new[]
            {
                new FutureRouteApproachEvidence
                {
                    TotalDays = 12,
                    LocationName = "Town",
                    FromTileX = 10,
                    FromTileY = 20,
                    TargetTileX = 73,
                    TargetTileY = 54,
                    StandTileX = 72,
                    StandTileY = 54,
                    TravelGameMinutes = 30,
                    TraversabilityComplete = true
                }
            }
        };

        var result = new FutureRouteAccessWindowResolver().Resolve(
            RouteGraph(),
            scenario,
            "Town",
            73,
            54);

        Assert.Equal(FutureRouteAccessResolutionStatus.Exact, result.Status);
        Assert.Equal(830, result.EarliestArrivalTime);
        Assert.Equal((72, 54), (result.StandTileX, result.StandTileY));
        Assert.Empty(result.Path);
    }

    [Fact]
    public void ExactRouteIntersectsStationaryNpcWindow()
    {
        var presence = new NpcFuturePresenceWindowResolution
        {
            Status = NpcFuturePresenceWindowResolutionStatus.Exact,
            NpcName = "Abigail",
            Windows = new[]
            {
                new NpcFuturePresenceWindow
                {
                    ScheduleEntryOrdinal = 2,
                    LocationName = "Town",
                    TileX = 73,
                    TileY = 54,
                    WindowStartTime = 900,
                    WindowEndTimeExclusive = 1030,
                    HasStableInterval = true,
                    EndpointBehaviorComplete = true
                }
            }
        };

        var result = new NpcFutureContactWindowResolver().Resolve(
            RouteGraph(),
            Scenario(),
            presence,
            "talk",
            Eligibility(presence));

        Assert.Equal(NpcFutureContactWindowResolutionStatus.Exact, result.Status);
        var window = Assert.Single(result.Windows);
        Assert.Equal(920, window.EarliestInteractionTime);
        Assert.Equal(2, window.RouteConnectorCount);
        Assert.Equal((72, 54), (window.StandTileX, window.StandTileY));
    }

    [Fact]
    public void RouteArrivingAfterNpcDepartureDoesNotCreateTrainingWindow()
    {
        var scenario = Scenario();
        scenario.ApproachEvidence![0].TravelGameMinutes = 200;
        var presence = new NpcFuturePresenceWindowResolution
        {
            Status = NpcFuturePresenceWindowResolutionStatus.Exact,
            NpcName = "Abigail",
            Windows = new[]
            {
                new NpcFuturePresenceWindow
                {
                    LocationName = "Town",
                    TileX = 73,
                    TileY = 54,
                    WindowStartTime = 900,
                    WindowEndTimeExclusive = 1030,
                    HasStableInterval = true,
                    EndpointBehaviorComplete = true
                }
            }
        };

        var result = new NpcFutureContactWindowResolver().Resolve(
            RouteGraph(),
            scenario,
            presence,
            "talk",
            Eligibility(presence));

        Assert.Equal(NpcFutureContactWindowResolutionStatus.Blocked, result.Status);
        Assert.Contains(
            "future_contact_player_arrives_after_npc_departure",
            result.BlockingReasons);
    }

    [Fact]
    public void PresenceAndRouteWithoutInteractionEvidenceCannotCreateContactWindow()
    {
        var presence = new NpcFuturePresenceWindowResolution
        {
            Status = NpcFuturePresenceWindowResolutionStatus.Exact,
            NpcName = "Abigail",
            SelectedScheduleKey = "spring",
            Windows = new[]
            {
                new NpcFuturePresenceWindow
                {
                    ScheduleEntryOrdinal = 2,
                    LocationName = "Town",
                    TileX = 73,
                    TileY = 54,
                    WindowStartTime = 900,
                    WindowEndTimeExclusive = 1030,
                    HasStableInterval = true,
                    EndpointBehaviorComplete = true
                }
            }
        };

        var result = new NpcFutureContactWindowResolver().Resolve(
            RouteGraph(),
            Scenario(),
            presence,
            "talk",
            Array.Empty<FutureNpcContactEligibilityEvidence>());

        Assert.Equal(NpcFutureContactWindowResolutionStatus.Blocked, result.Status);
        Assert.Contains(
            "future_contact_interaction_evidence_missing",
            result.BlockingReasons);
    }

    [Fact]
    public void OrderedItineraryRebindsRouteFromPriorPlayerStandAndTime()
    {
        var firstPresence = Presence("Abigail", "spring", 2, 73, 54, 900, 1030);
        var secondPresence = Presence("Leah", "spring", 1, 50, 50, 900, 1200);
        var scenario = Scenario();
        scenario.ApproachEvidence = scenario.ApproachEvidence!.Concat(new[]
        {
            new FutureRouteApproachEvidence
            {
                TotalDays = 12,
                LocationName = "Town",
                FromTileX = 72,
                FromTileY = 54,
                TargetTileX = 50,
                TargetTileY = 50,
                StandTileX = 51,
                StandTileY = 50,
                TravelGameMinutes = 10,
                TraversabilityComplete = true
            }
        }).ToArray();
        var visits = new[]
        {
            Visit("visit-abigail", firstPresence),
            Visit("visit-leah", secondPresence)
        };

        var result = new FutureSocialItineraryVerifier().Verify(
            RouteGraph(),
            scenario,
            visits);

        Assert.Equal(FutureSocialItineraryVerificationStatus.Exact, result.Status);
        Assert.Equal(2, result.Steps.Length);
        Assert.Equal((920, 930),
            (result.Steps[0].InteractionStartTime,
             result.Steps[0].InteractionEndTime));
        Assert.Equal((72, 54),
            (result.Steps[0].StandTileX,
             result.Steps[0].StandTileY));
        Assert.Equal((940, 950, 0),
            (result.Steps[1].InteractionStartTime,
             result.Steps[1].InteractionEndTime,
             result.Steps[1].RouteConnectorCount));
        Assert.Equal(950, result.CompletionTime);
    }

    [Fact]
    public void IndividuallyReachableVisitsFailWhenProposedOrderMissesSecondWindow()
    {
        var firstPresence = Presence("Abigail", "spring", 2, 73, 54, 900, 1030);
        var secondPresence = Presence("Leah", "spring", 1, 50, 50, 900, 935);
        var scenario = Scenario();
        scenario.ApproachEvidence = scenario.ApproachEvidence!.Concat(new[]
        {
            new FutureRouteApproachEvidence
            {
                TotalDays = 12,
                LocationName = "Town",
                FromTileX = 72,
                FromTileY = 54,
                TargetTileX = 50,
                TargetTileY = 50,
                StandTileX = 51,
                StandTileY = 50,
                TravelGameMinutes = 10,
                TraversabilityComplete = true
            }
        }).ToArray();

        var result = new FutureSocialItineraryVerifier().Verify(
            RouteGraph(),
            scenario,
            new[]
            {
                Visit("visit-abigail", firstPresence),
                Visit("visit-leah", secondPresence)
            });

        Assert.Equal(FutureSocialItineraryVerificationStatus.Blocked, result.Status);
        Assert.Single(result.Steps);
        Assert.Contains(result.BlockingReasons, reason =>
            reason.EndsWith(":visit-leah", StringComparison.Ordinal));
    }

    private static JsonElement RouteGraph() => JsonSerializer.SerializeToElement(new
    {
        edges = new object[]
        {
            new
            {
                kind = "warp",
                from_location = "FarmHouse",
                from_x = 2,
                from_y = 3,
                target_location = "Farm",
                target_x = 64,
                target_y = 15,
                resolved = true
            },
            new
            {
                kind = "locked_door_warp",
                from_location = "Farm",
                from_x = 70,
                from_y = 15,
                target_location = "Town",
                target_x = 5,
                target_y = 10,
                resolved = true
            }
        }
    });

    private static FutureRouteAccessScenario Scenario() => new()
    {
        TotalDays = 12,
        StartLocation = "FarmHouse",
        StartTileX = 1,
        StartTileY = 1,
        EarliestDepartureTime = 600,
        SegmentEvidence = new[]
        {
            Segment(
                "warp", "FarmHouse", 2, 3,
                "Farm", 64, 15,
                1, 1, 10, null, null),
            Segment(
                "locked_door_warp", "Farm", 70, 15,
                "Town", 5, 10,
                64, 15, 20, 900, 1700)
        },
        ApproachEvidence = new[]
        {
            new FutureRouteApproachEvidence
            {
                TotalDays = 12,
                LocationName = "Town",
                FromTileX = 5,
                FromTileY = 10,
                TargetTileX = 73,
                TargetTileY = 54,
                StandTileX = 72,
                StandTileY = 54,
                TravelGameMinutes = 20,
                TraversabilityComplete = true
            }
        }
    };

    private static FutureNpcContactEligibilityEvidence[] Eligibility(
        NpcFuturePresenceWindowResolution presence) => presence.Windows.Select(window =>
            new FutureNpcContactEligibilityEvidence
            {
                TotalDays = 12,
                NpcName = presence.NpcName,
                SelectedScheduleKey = presence.SelectedScheduleKey,
                ScheduleEntryOrdinal = window.ScheduleEntryOrdinal,
                LocationName = window.LocationName,
                TileX = window.TileX,
                TileY = window.TileY,
                EligibleFromTime = window.WindowStartTime,
                EligibleUntilTimeExclusive = window.WindowEndTimeExclusive,
                StateComplete = true,
                TalkAllowed = true,
                GiftAllowed = true
            }).ToArray();

    private static NpcFuturePresenceWindowResolution Presence(
        string npcName,
        string scheduleKey,
        int ordinal,
        int tileX,
        int tileY,
        int start,
        int end) => new()
    {
        Status = NpcFuturePresenceWindowResolutionStatus.Exact,
        NpcName = npcName,
        SelectedScheduleKey = scheduleKey,
        ResolvedScheduleKey = scheduleKey,
        Windows = new[]
        {
            new NpcFuturePresenceWindow
            {
                ScheduleEntryOrdinal = ordinal,
                LocationName = "Town",
                TileX = tileX,
                TileY = tileY,
                WindowStartTime = start,
                WindowEndTimeExclusive = end,
                HasStableInterval = true,
                EndpointBehaviorComplete = true
            }
        }
    };

    private static FutureSocialItineraryVisit Visit(
        string visitId,
        NpcFuturePresenceWindowResolution presence) => new()
    {
        VisitId = visitId,
        NpcName = presence.NpcName,
        InteractionKind = "talk",
        InteractionGameMinutes = 10,
        Presence = presence,
        EligibilityEvidence = Eligibility(presence)
    };

    private static FutureRouteSegmentEvidence Segment(
        string kind,
        string fromLocation,
        int fromX,
        int fromY,
        string targetLocation,
        int targetX,
        int targetY,
        int approachFromX,
        int approachFromY,
        int approachMinutes,
        int? openTime,
        int? closeTime) => new()
    {
        TotalDays = 12,
        Kind = kind,
        FromLocation = fromLocation,
        FromTileX = fromX,
        FromTileY = fromY,
        TargetLocation = targetLocation,
        TargetTileX = targetX,
        TargetTileY = targetY,
        ApproachFromTileX = approachFromX,
        ApproachFromTileY = approachFromY,
        ApproachTravelGameMinutes = approachMinutes,
        ConnectorTransitionGameMinutes = 0,
        TraversabilityComplete = true,
        GateStateComplete = true,
        AllowedOnDate = true,
        OpenTime = openTime,
        CloseTimeExclusive = closeTime
    };
}

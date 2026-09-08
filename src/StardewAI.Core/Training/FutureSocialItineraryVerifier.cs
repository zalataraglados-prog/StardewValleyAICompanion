using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace StardewAI.Core.Training
{
    public enum FutureSocialItineraryVerificationStatus
    {
        Exact,
        ConservativeUpperBound,
        Blocked
    }

    public sealed class FutureSocialItineraryVisit
    {
        public string VisitId { get; set; } = string.Empty;

        public string NpcName { get; set; } = string.Empty;

        public string InteractionKind { get; set; } = string.Empty;

        public int InteractionGameMinutes { get; set; }

        public NpcFuturePresenceWindowResolution Presence { get; set; } = new();

        public FutureNpcContactEligibilityEvidence[] EligibilityEvidence { get; set; } =
            Array.Empty<FutureNpcContactEligibilityEvidence>();
    }

    public sealed class FutureSocialItineraryStep
    {
        public string VisitId { get; set; } = string.Empty;

        public string NpcName { get; set; } = string.Empty;

        public string InteractionKind { get; set; } = string.Empty;

        public string LocationName { get; set; } = string.Empty;

        public int TileX { get; set; }

        public int TileY { get; set; }

        public int StandTileX { get; set; }

        public int StandTileY { get; set; }

        public int ArrivalTime { get; set; }

        public int InteractionStartTime { get; set; }

        public int InteractionEndTime { get; set; }

        public int RouteConnectorCount { get; set; }

        public int RouteWaitGameMinutes { get; set; }

        public FutureRouteTravelTimingEvidenceKind RouteTimingEvidenceKind { get; set; } =
            FutureRouteTravelTimingEvidenceKind.ExactDuration;
    }

    public sealed class FutureSocialItineraryVerification
    {
        public FutureSocialItineraryVerificationStatus Status { get; set; }

        public int TotalDays { get; set; }

        public int? CompletionTime { get; set; }

        public FutureSocialItineraryStep[] Steps { get; set; } =
            Array.Empty<FutureSocialItineraryStep>();

        public string[] BlockingReasons { get; set; } = Array.Empty<string>();

        public string Scope { get; set; } =
            "verify_model_proposed_order_against_exact_future_contact_evidence";
    }

    public sealed class FutureSocialItineraryVerifier
    {
        public FutureSocialItineraryVerification Verify(
            JsonElement routeGraph,
            FutureRouteAccessScenario initialRouteScenario,
            FutureSocialItineraryVisit[] visits)
        {
            if (initialRouteScenario is null || visits is null || visits.Length == 0)
                return Blocked(initialRouteScenario?.TotalDays ?? 0, "future_itinerary_input_missing");
            if (visits.Any(visit => visit is null ||
                    string.IsNullOrWhiteSpace(visit.VisitId) ||
                    string.IsNullOrWhiteSpace(visit.NpcName) ||
                    visit.InteractionKind is not ("talk" or "gift") ||
                    visit.InteractionGameMinutes < 0 ||
                    visit.Presence is null ||
                    !string.Equals(visit.Presence.NpcName, visit.NpcName, StringComparison.Ordinal)))
            {
                return Blocked(initialRouteScenario.TotalDays, "future_itinerary_visit_invalid");
            }
            if (visits.Select(visit => visit.VisitId).Distinct(StringComparer.Ordinal).Count() != visits.Length)
                return Blocked(initialRouteScenario.TotalDays, "future_itinerary_visit_id_duplicate");
            if (visits.GroupBy(
                    visit => visit.NpcName + "\n" + visit.InteractionKind,
                    StringComparer.Ordinal).Any(group => group.Count() > 1))
            {
                return Blocked(initialRouteScenario.TotalDays, "future_itinerary_daily_interaction_duplicate");
            }

            var currentLocation = initialRouteScenario.StartLocation;
            var currentX = initialRouteScenario.StartTileX;
            var currentY = initialRouteScenario.StartTileY;
            var currentTime = initialRouteScenario.EarliestDepartureTime;
            var steps = new List<FutureSocialItineraryStep>();
            var timingKind = FutureRouteTravelTimingEvidenceKind.ExactDuration;
            foreach (var visit in visits)
            {
                var scenario = new FutureRouteAccessScenario
                {
                    TotalDays = initialRouteScenario.TotalDays,
                    StartLocation = currentLocation,
                    StartTileX = currentX,
                    StartTileY = currentY,
                    EarliestDepartureTime = currentTime,
                    SegmentEvidence = initialRouteScenario.SegmentEvidence,
                    ApproachEvidence = initialRouteScenario.ApproachEvidence,
                    ProducedPaths = initialRouteScenario.ProducedPaths
                };
                var contact = new NpcFutureContactWindowResolver().Resolve(
                    routeGraph,
                    scenario,
                    visit.Presence,
                    visit.InteractionKind,
                    visit.EligibilityEvidence);
                if (contact.Status == NpcFutureContactWindowResolutionStatus.Blocked ||
                    contact.Windows.Length == 0)
                {
                    return Blocked(
                        initialRouteScenario.TotalDays,
                        contact.BlockingReasons.Length == 0
                            ? new[] { "future_itinerary_contact_window_missing:" + visit.VisitId }
                            : contact.BlockingReasons.Select(reason =>
                                reason + ":" + visit.VisitId).ToArray(),
                        steps);
                }

                var window = contact.Windows[0];
                if (window.RouteTimingEvidenceKind ==
                    FutureRouteTravelTimingEvidenceKind.ConservativeUpperBound)
                {
                    timingKind = FutureRouteTravelTimingEvidenceKind.ConservativeUpperBound;
                }
                var interactionEnd = AddGameMinutes(
                    window.EarliestInteractionTime,
                    visit.InteractionGameMinutes);
                if (ToMinutes(interactionEnd) >
                        ToMinutes(window.NpcPresentUntilTimeExclusive) ||
                    ToMinutes(interactionEnd) >
                        ToMinutes(window.InteractionEligibleUntilTimeExclusive))
                {
                    return Blocked(
                        initialRouteScenario.TotalDays,
                        "future_itinerary_interaction_exceeds_window:" + visit.VisitId,
                        steps);
                }

                steps.Add(new FutureSocialItineraryStep
                {
                    VisitId = visit.VisitId,
                    NpcName = visit.NpcName,
                    InteractionKind = visit.InteractionKind,
                    LocationName = window.LocationName,
                    TileX = window.TileX,
                    TileY = window.TileY,
                    StandTileX = window.StandTileX,
                    StandTileY = window.StandTileY,
                    ArrivalTime = window.EarliestPlayerArrivalTime,
                    InteractionStartTime = window.EarliestInteractionTime,
                    InteractionEndTime = interactionEnd,
                    RouteConnectorCount = window.RouteConnectorCount,
                    RouteWaitGameMinutes = window.RouteWaitGameMinutes,
                    RouteTimingEvidenceKind = window.RouteTimingEvidenceKind
                });
                currentLocation = window.LocationName;
                currentX = window.StandTileX;
                currentY = window.StandTileY;
                currentTime = interactionEnd;
            }

            return new FutureSocialItineraryVerification
            {
                Status = timingKind == FutureRouteTravelTimingEvidenceKind.ExactDuration
                    ? FutureSocialItineraryVerificationStatus.Exact
                    : FutureSocialItineraryVerificationStatus.ConservativeUpperBound,
                TotalDays = initialRouteScenario.TotalDays,
                CompletionTime = currentTime,
                Steps = steps.ToArray()
            };
        }

        private static FutureSocialItineraryVerification Blocked(
            int totalDays,
            string reason,
            IReadOnlyCollection<FutureSocialItineraryStep>? steps = null) =>
            Blocked(totalDays, new[] { reason }, steps);

        private static FutureSocialItineraryVerification Blocked(
            int totalDays,
            string[] reasons,
            IReadOnlyCollection<FutureSocialItineraryStep>? steps = null) => new()
        {
            Status = FutureSocialItineraryVerificationStatus.Blocked,
            TotalDays = totalDays,
            Steps = steps?.ToArray() ?? Array.Empty<FutureSocialItineraryStep>(),
            BlockingReasons = reasons
        };

        private static int AddGameMinutes(int time, int minutes)
        {
            var total = ToMinutes(time) + minutes;
            return total / 60 * 100 + total % 60;
        }

        private static int ToMinutes(int time) => time / 100 * 60 + time % 100;
    }
}

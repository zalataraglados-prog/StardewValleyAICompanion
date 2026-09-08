using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace StardewAI.Core.Training
{
    public enum NpcFutureContactWindowResolutionStatus
    {
        Exact,
        ConservativeUpperBound,
        Blocked
    }

    public sealed class NpcFutureContactWindow
    {
        public string InteractionKind { get; set; } = string.Empty;

        public int ScheduleEntryOrdinal { get; set; }

        public string LocationName { get; set; } = string.Empty;

        public int TileX { get; set; }

        public int TileY { get; set; }

        public int StandTileX { get; set; }

        public int StandTileY { get; set; }

        public int NpcPresentFromTime { get; set; }

        public int NpcPresentUntilTimeExclusive { get; set; }

        public int InteractionEligibleUntilTimeExclusive { get; set; }

        public int EarliestPlayerArrivalTime { get; set; }

        public FutureRouteTravelTimingEvidenceKind RouteTimingEvidenceKind { get; set; } =
            FutureRouteTravelTimingEvidenceKind.ExactDuration;

        public int EarliestInteractionTime { get; set; }

        public int RouteConnectorCount { get; set; }

        public int RouteWaitGameMinutes { get; set; }
    }

    public sealed class NpcFutureContactWindowResolution
    {
        public NpcFutureContactWindowResolutionStatus Status { get; set; }

        public string NpcName { get; set; } = string.Empty;

        public NpcFutureContactWindow[] Windows { get; set; } =
            Array.Empty<NpcFutureContactWindow>();

        public string[] BlockingReasons { get; set; } = Array.Empty<string>();

        public string Scope { get; set; } =
            "future_stationary_npc_presence_intersected_with_date_bound_player_route_evidence";
    }

    public sealed class FutureNpcContactEligibilityEvidence
    {
        public int TotalDays { get; set; }

        public string NpcName { get; set; } = string.Empty;

        public string SelectedScheduleKey { get; set; } = string.Empty;

        public int ScheduleEntryOrdinal { get; set; }

        public string LocationName { get; set; } = string.Empty;

        public int TileX { get; set; }

        public int TileY { get; set; }

        public int EligibleFromTime { get; set; }

        public int EligibleUntilTimeExclusive { get; set; }

        public bool StateComplete { get; set; }

        public bool TalkAllowed { get; set; }

        public bool GiftAllowed { get; set; }

        public string SocialQueryCondition { get; set; } = string.Empty;

        public bool SocialQueryValueOnCaptureDate { get; set; }

        public bool SocialQueryStableThroughDay { get; set; }

        public string SocialQueryEvidenceKind { get; set; } = string.Empty;
    }

    public sealed class NpcFutureContactWindowResolver
    {
        public NpcFutureContactWindowResolution Resolve(
            JsonElement routeGraph,
            FutureRouteAccessScenario routeScenario,
            NpcFuturePresenceWindowResolution presence,
            string interactionKind,
            FutureNpcContactEligibilityEvidence[] eligibilityEvidence)
        {
            if (routeScenario is null || presence is null || eligibilityEvidence is null)
                return Blocked(presence?.NpcName ?? string.Empty, "future_contact_input_missing");
            if (presence.Status != NpcFuturePresenceWindowResolutionStatus.Exact)
                return Blocked(presence.NpcName, "future_contact_presence_not_exact");
            if (interactionKind is not ("talk" or "gift"))
                return Blocked(presence.NpcName, "future_contact_interaction_kind_invalid");

            var windows = new List<NpcFutureContactWindow>();
            var failures = new List<string>();
            foreach (var presenceWindow in presence.Windows)
            {
                if (!presenceWindow.HasStableInterval)
                {
                    failures.Add("future_contact_npc_not_stationary");
                    continue;
                }
                if (!presenceWindow.EndpointBehaviorComplete)
                {
                    failures.Add("future_contact_endpoint_behavior_incomplete");
                    continue;
                }

                var route = new FutureRouteAccessWindowResolver().Resolve(
                    routeGraph,
                    routeScenario,
                    presenceWindow.LocationName,
                    presenceWindow.TileX,
                    presenceWindow.TileY);
                if (route.Status == FutureRouteAccessResolutionStatus.Blocked ||
                    !route.GuaranteedArrivalByTime.HasValue ||
                    !route.StandTileX.HasValue ||
                    !route.StandTileY.HasValue)
                {
                    failures.AddRange(route.BlockingReasons);
                    continue;
                }

                var interactionTime = LaterTime(
                    route.GuaranteedArrivalByTime.Value,
                    presenceWindow.WindowStartTime);
                var eligibilityMatches = eligibilityEvidence.Where(evidence =>
                    evidence.TotalDays == routeScenario.TotalDays &&
                    string.Equals(evidence.NpcName, presence.NpcName, StringComparison.Ordinal) &&
                    string.Equals(evidence.SelectedScheduleKey, presence.SelectedScheduleKey, StringComparison.Ordinal) &&
                    evidence.ScheduleEntryOrdinal == presenceWindow.ScheduleEntryOrdinal &&
                    string.Equals(evidence.LocationName, presenceWindow.LocationName, StringComparison.Ordinal) &&
                    evidence.TileX == presenceWindow.TileX &&
                    evidence.TileY == presenceWindow.TileY)
                    .ToArray();
                if (eligibilityMatches.Length != 1)
                {
                    failures.Add(eligibilityMatches.Length == 0
                        ? "future_contact_interaction_evidence_missing"
                        : "future_contact_interaction_evidence_ambiguous");
                    continue;
                }
                var eligibility = eligibilityMatches[0];
                if (!IsValidTime(eligibility.EligibleFromTime) ||
                    !IsValidTime(eligibility.EligibleUntilTimeExclusive) ||
                    ToMinutes(eligibility.EligibleFromTime) >=
                        ToMinutes(eligibility.EligibleUntilTimeExclusive))
                {
                    failures.Add("future_contact_interaction_window_invalid");
                    continue;
                }
                if (!eligibility.StateComplete)
                {
                    failures.Add("future_contact_interaction_state_incomplete");
                    continue;
                }
                if ((interactionKind == "talk" && !eligibility.TalkAllowed) ||
                    (interactionKind == "gift" && !eligibility.GiftAllowed))
                {
                    failures.Add("future_contact_interaction_not_allowed");
                    continue;
                }
                interactionTime = LaterTime(
                    interactionTime,
                    eligibility.EligibleFromTime);
                if (ToMinutes(interactionTime) >=
                        ToMinutes(presenceWindow.WindowEndTimeExclusive) ||
                    ToMinutes(interactionTime) >=
                        ToMinutes(eligibility.EligibleUntilTimeExclusive))
                {
                    failures.Add("future_contact_player_arrives_after_npc_departure");
                    continue;
                }

                windows.Add(new NpcFutureContactWindow
                {
                    InteractionKind = interactionKind,
                    ScheduleEntryOrdinal = presenceWindow.ScheduleEntryOrdinal,
                    LocationName = presenceWindow.LocationName,
                    TileX = presenceWindow.TileX,
                    TileY = presenceWindow.TileY,
                    StandTileX = route.StandTileX.Value,
                    StandTileY = route.StandTileY.Value,
                    NpcPresentFromTime = presenceWindow.WindowStartTime,
                    NpcPresentUntilTimeExclusive = presenceWindow.WindowEndTimeExclusive,
                    InteractionEligibleUntilTimeExclusive =
                        eligibility.EligibleUntilTimeExclusive,
                    EarliestPlayerArrivalTime = route.GuaranteedArrivalByTime.Value,
                    RouteTimingEvidenceKind = route.TimingEvidenceKind,
                    EarliestInteractionTime = interactionTime,
                    RouteConnectorCount = route.Path.Length,
                    RouteWaitGameMinutes = route.WaitGameMinutes
                });
            }

            if (windows.Count == 0)
            {
                return Blocked(
                    presence.NpcName,
                    failures.Count == 0
                        ? new[] { "future_contact_window_missing" }
                        : failures.Distinct(StringComparer.Ordinal).ToArray());
            }

            return new NpcFutureContactWindowResolution
            {
                Status = windows.All(window =>
                    window.RouteTimingEvidenceKind ==
                        FutureRouteTravelTimingEvidenceKind.ExactDuration)
                    ? NpcFutureContactWindowResolutionStatus.Exact
                    : NpcFutureContactWindowResolutionStatus.ConservativeUpperBound,
                NpcName = presence.NpcName,
                Windows = windows
                    .OrderBy(window =>
                        window.RouteTimingEvidenceKind ==
                            FutureRouteTravelTimingEvidenceKind.ExactDuration
                            ? 0
                            : 1)
                    .ThenBy(window => ToMinutes(window.EarliestInteractionTime))
                    .ThenBy(window => window.RouteConnectorCount)
                    .ThenBy(window => window.LocationName, StringComparer.Ordinal)
                    .ToArray(),
                BlockingReasons = failures.Distinct(StringComparer.Ordinal).ToArray()
            };
        }

        private static NpcFutureContactWindowResolution Blocked(
            string npcName,
            string reason) => Blocked(npcName, new[] { reason });

        private static NpcFutureContactWindowResolution Blocked(
            string npcName,
            string[] reasons) => new()
        {
            Status = NpcFutureContactWindowResolutionStatus.Blocked,
            NpcName = npcName,
            BlockingReasons = reasons
        };

        private static int LaterTime(int left, int right) =>
            ToMinutes(left) >= ToMinutes(right) ? left : right;

        private static int ToMinutes(int time) => time / 100 * 60 + time % 100;

        private static bool IsValidTime(int value) =>
            value >= 0 && value <= 2800 && value % 100 < 60;
    }
}

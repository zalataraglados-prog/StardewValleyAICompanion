using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Training
{
    public sealed partial class CurrentSocialDayItineraryPlanner
    {
        private static Dictionary<string, TalkTransition> ReadTalkTransitions(
            SnapshotEnvelope snapshot)
        {
            return SocialCandidateBuilder.Build(
                    snapshot,
                    "social.talk_npc",
                    int.MaxValue)
                .Select(candidate => new
                {
                    NpcName = ReadParameter(
                        candidate.Parameters,
                        "npc_name"),
                    PointsBefore = ReadIntParameter(
                        candidate.Parameters,
                        "friendship_points_before"),
                    Delta = ReadIntParameter(
                        candidate.Parameters,
                        "expected_friendship_delta"),
                    PointsAfter = ReadIntParameter(
                        candidate.Parameters,
                        "expected_friendship_points_after")
                })
                .Where(value =>
                    !string.IsNullOrWhiteSpace(value.NpcName) &&
                    value.PointsBefore.HasValue &&
                    value.Delta.HasValue &&
                    value.PointsAfter.HasValue)
                .GroupBy(value => value.NpcName!, StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .ToDictionary(
                    group => group.Key,
                    group => new TalkTransition
                    {
                        PointsBefore = group.Single().PointsBefore!.Value,
                        Delta = group.Single().Delta!.Value,
                        PointsAfter = group.Single().PointsAfter!.Value
                    },
                    StringComparer.Ordinal);
        }

        private static FutureSocialItineraryVisit BuildVisit(
            CurrentSocialContactOpportunity opportunity,
            int totalDays) => new()
        {
            VisitId = opportunity.OpportunityId,
            NpcName = opportunity.NpcName,
            InteractionKind = "talk",
            InteractionGameMinutes = 0,
            Presence = new NpcFuturePresenceWindowResolution
            {
                Status = NpcFuturePresenceWindowResolutionStatus.Exact,
                NpcName = opportunity.NpcName,
                SelectedScheduleKey = opportunity.SelectedScheduleKey,
                ResolvedScheduleKey = opportunity.SelectedScheduleKey,
                Windows = new[]
                {
                    new NpcFuturePresenceWindow
                    {
                        ScheduleEntryOrdinal =
                            opportunity.ScheduleEntryOrdinal,
                        LocationName = opportunity.LocationName,
                        TileX = opportunity.NpcTileX,
                        TileY = opportunity.NpcTileY,
                        WindowStartTime = opportunity.NpcPresentFromTime,
                        WindowEndTimeExclusive =
                            opportunity.NpcPresentUntilTimeExclusive,
                        HasStableInterval = true,
                        EndpointBehaviorComplete = true
                    }
                }
            },
            EligibilityEvidence = new[]
            {
                new FutureNpcContactEligibilityEvidence
                {
                    TotalDays = totalDays,
                    NpcName = opportunity.NpcName,
                    SelectedScheduleKey = opportunity.SelectedScheduleKey,
                    ScheduleEntryOrdinal = opportunity.ScheduleEntryOrdinal,
                    LocationName = opportunity.LocationName,
                    TileX = opportunity.NpcTileX,
                    TileY = opportunity.NpcTileY,
                    EligibleFromTime =
                        opportunity.InteractionEligibleFromTime,
                    EligibleUntilTimeExclusive =
                        opportunity.InteractionEligibleUntilTimeExclusive,
                    StateComplete = true,
                    TalkAllowed = true,
                    GiftAllowed = false,
                    SocialQueryValueOnCaptureDate = true,
                    SocialQueryStableThroughDay = true,
                    SocialQueryEvidenceKind =
                        "copied_from_current_social_contact_frontier_v3"
                }
            }
        };

        private static bool TryReadPortfolio(
            JsonElement progress,
            out int qualifyingCount,
            out FriendshipProgressRow[] rows,
            out string reason)
        {
            qualifyingCount = 0;
            rows = Array.Empty<FriendshipProgressRow>();
            reason = "current_social_itinerary_friendship_progress_incomplete";
            if (!TryReadInt(progress, "threshold_points", out var threshold) ||
                threshold != FriendshipThreshold ||
                !TryReadInt(progress, "maximum_points", out var maximum) ||
                maximum != 999999 ||
                !TryReadBool(progress, "romance_only", out var romanceOnly) ||
                romanceOnly ||
                !TryReadInt(
                    progress,
                    "qualifying_count",
                    out qualifyingCount) ||
                qualifyingCount < 0 ||
                !string.Equals(
                    ReadString(progress, "projection_status"),
                    "complete_live_native_iteration",
                    StringComparison.Ordinal) ||
                !progress.TryGetProperty(
                    "eligible_villager_rows",
                    out var rowArray) ||
                rowArray.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var parsed = new List<FriendshipProgressRow>();
            foreach (var row in rowArray.EnumerateArray())
            {
                var npcName = ReadString(row, "npc_name");
                if (npcName.Length == 0 ||
                    !TryReadBool(row, "is_villager", out var villager) ||
                    !villager ||
                    !TryReadBool(row, "event_actor", out var eventActor) ||
                    eventActor ||
                    !TryReadBool(row, "qualifies", out var qualifies) ||
                    !row.TryGetProperty(
                        "friendship_points",
                        out var pointsNode) ||
                    pointsNode.ValueKind is not
                        (JsonValueKind.Number or JsonValueKind.Null))
                {
                    return false;
                }
                var points = pointsNode.ValueKind == JsonValueKind.Number &&
                    pointsNode.TryGetInt32(out var parsedPoints)
                        ? parsedPoints
                        : 0;
                if (points < 0 || qualifies != (points >= threshold))
                    return false;
                parsed.Add(new FriendshipProgressRow
                {
                    NpcName = npcName,
                    Points = points,
                    Qualifies = qualifies
                });
            }
            if (parsed.Count(value => value.Qualifies) != qualifyingCount)
                return false;

            rows = parsed
                .GroupBy(value => value.NpcName, StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .Select(group => group.Single())
                .ToArray();
            reason = string.Empty;
            return true;
        }

        private static bool TryReadInputs(
            JsonElement snapshot,
            out string gameVersion,
            out int totalDays,
            out string startLocation,
            out int startX,
            out int startY,
            out int startTime,
            out JsonElement movementContext,
            out JsonElement routeGraph,
            out JsonElement routeDateEvidence,
            out JsonElement friendshipProgress,
            out SnapshotEnvelope envelope,
            out string reason)
        {
            gameVersion = string.Empty;
            totalDays = -1;
            startLocation = string.Empty;
            startX = -1;
            startY = -1;
            startTime = -1;
            movementContext = default;
            routeGraph = default;
            routeDateEvidence = default;
            friendshipProgress = default;
            envelope = new SnapshotEnvelope();
            reason = "current_social_itinerary_snapshot_inputs_incomplete";
            if (snapshot.ValueKind != JsonValueKind.Object ||
                !snapshot.TryGetProperty(
                    "game_version",
                    out var gameVersionNode) ||
                gameVersionNode.ValueKind != JsonValueKind.String ||
                !NpcCurrentScheduleSnapshotAuditor.TryReadFieldValue(
                    snapshot,
                    "time",
                    "total_days",
                    out var totalDaysNode) ||
                !totalDaysNode.TryGetInt32(out totalDays) ||
                !NpcCurrentScheduleSnapshotAuditor.TryReadFieldValue(
                    snapshot,
                    "time",
                    "time",
                    out var timeNode) ||
                !timeNode.TryGetInt32(out startTime) ||
                !NpcCurrentScheduleSnapshotAuditor.TryReadFieldValue(
                    snapshot,
                    "player",
                    "location_id",
                    out var locationNode) ||
                locationNode.ValueKind != JsonValueKind.String ||
                !NpcCurrentScheduleSnapshotAuditor.TryReadFieldValue(
                    snapshot,
                    "player",
                    "tile_x",
                    out var xNode) ||
                !xNode.TryGetInt32(out startX) ||
                !NpcCurrentScheduleSnapshotAuditor.TryReadFieldValue(
                    snapshot,
                    "player",
                    "tile_y",
                    out var yNode) ||
                !yNode.TryGetInt32(out startY) ||
                !TryReadFieldNode(
                    snapshot,
                    "player",
                    "movement_timing_context",
                    out movementContext) ||
                !NpcCurrentScheduleSnapshotAuditor.TryReadFieldValue(
                    snapshot,
                    "locations",
                    "route_graph",
                    out routeGraph) ||
                !TryReadFieldNode(
                    snapshot,
                    "locations",
                    "social_route_date_evidence",
                    out routeDateEvidence) ||
                !NpcCurrentScheduleSnapshotAuditor.TryReadFieldValue(
                    snapshot,
                    "npcs",
                    "grandpa_friendship_progress",
                    out friendshipProgress) ||
                !JsonSnapshotEnvelopeAdapter.TryCreate(
                    snapshot,
                    out envelope))
            {
                return false;
            }
            gameVersion = gameVersionNode.GetString() ?? string.Empty;
            startLocation = locationNode.GetString() ?? string.Empty;
            if (gameVersion.Length == 0 ||
                startLocation.Length == 0 ||
                totalDays < 0 ||
                !IsValidCurrentDayTime(startTime))
            {
                reason =
                    "current_social_itinerary_requires_valid_current_day_snapshot_time";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private static bool TryReadFieldNode(
            JsonElement snapshot,
            string domain,
            string field,
            out JsonElement fieldNode)
        {
            fieldNode = default;
            return snapshot.ValueKind == JsonValueKind.Object &&
                snapshot.TryGetProperty("state", out var state) &&
                state.ValueKind == JsonValueKind.Object &&
                state.TryGetProperty(domain, out var domainNode) &&
                domainNode.ValueKind == JsonValueKind.Object &&
                domainNode.TryGetProperty(field, out fieldNode) &&
                fieldNode.ValueKind == JsonValueKind.Object;
        }

        private static bool TryReadInt(
            JsonElement source,
            string name,
            out int value)
        {
            value = 0;
            return source.TryGetProperty(name, out var property) &&
                property.ValueKind == JsonValueKind.Number &&
                property.TryGetInt32(out value);
        }

        private static bool TryReadBool(
            JsonElement source,
            string name,
            out bool value)
        {
            value = false;
            if (!source.TryGetProperty(name, out var property) ||
                property.ValueKind is not
                    (JsonValueKind.True or JsonValueKind.False))
            {
                return false;
            }
            value = property.GetBoolean();
            return true;
        }

        private static string ReadString(
            JsonElement source,
            string name) =>
            source.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;

        private static int ToMinutes(int time) =>
            time / 100 * 60 + time % 100;

        private static bool IsValidCurrentDayTime(int time)
        {
            var hour = time / 100;
            var minute = time % 100;
            return time >= 600 &&
                time <= 2600 &&
                hour <= 26 &&
                minute >= 0 &&
                minute < 60 &&
                minute % 10 == 0;
        }

        private static CurrentSocialDayItineraryPlan Blocked(
            int totalDays,
            string reason) => Blocked(totalDays, new[] { reason });

        private static CurrentSocialDayItineraryPlan Blocked(
            int totalDays,
            string[] reasons) => new()
        {
            TotalDays = totalDays,
            BlockingReasons = reasons
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray()
        };
    }
}

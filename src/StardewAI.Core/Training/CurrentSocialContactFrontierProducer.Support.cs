using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Training
{
    public sealed partial class CurrentSocialContactFrontierProducer
    {
        private static NpcFutureContactWindowResolution? AddOpportunity(
            ICollection<CurrentSocialContactOpportunity> target,
            JsonElement routeGraph,
            FutureRouteAccessScenario routeScenario,
            NpcFuturePresenceWindowResolution presence,
            FutureNpcContactEligibilityEvidence[] eligibility,
            string interactionKind,
            string timingEvidenceId,
            bool executionReady,
            string executionReadiness,
            SocialGiftInventoryBinding? giftBinding = null)
        {
            if ((interactionKind == "talk" &&
                 eligibility.All(value => !value.TalkAllowed)) ||
                (interactionKind == "gift" &&
                 eligibility.All(value => !value.GiftAllowed)))
            {
                return null;
            }
            var contact = new NpcFutureContactWindowResolver().Resolve(
                routeGraph,
                routeScenario,
                presence,
                interactionKind,
                eligibility);
            if (contact.Status == NpcFutureContactWindowResolutionStatus.Blocked)
                return contact;
            foreach (var window in contact.Windows)
            {
                var windowEnd = FromMinutes(Math.Min(
                    ToMinutes(window.NpcPresentUntilTimeExclusive),
                    ToMinutes(window.InteractionEligibleUntilTimeExclusive)));
                target.Add(new CurrentSocialContactOpportunity
                {
                    OpportunityId = string.Join(".", new object[]
                    {
                        "social",
                        interactionKind,
                        presence.NpcName,
                        window.ScheduleEntryOrdinal,
                        window.LocationName,
                        window.TileX,
                        window.TileY,
                        giftBinding is null
                            ? "none"
                            : "slot" + giftBinding.SlotIndex + "." +
                                giftBinding.QualifiedItemId
                    }),
                    NpcName = presence.NpcName,
                    InteractionKind = interactionKind,
                    ExecutionReady = executionReady,
                    ExecutionReadiness = executionReadiness,
                    ScheduleEntryOrdinal = window.ScheduleEntryOrdinal,
                    SelectedScheduleKey = presence.SelectedScheduleKey,
                    LocationName = window.LocationName,
                    NpcTileX = window.TileX,
                    NpcTileY = window.TileY,
                    StandTileX = window.StandTileX,
                    StandTileY = window.StandTileY,
                    EarliestInteractionTime = window.EarliestInteractionTime,
                    WindowEndTimeExclusive = windowEnd,
                    NpcPresentFromTime = window.NpcPresentFromTime,
                    NpcPresentUntilTimeExclusive =
                        window.NpcPresentUntilTimeExclusive,
                    InteractionEligibleFromTime = eligibility
                        .Single(value =>
                            value.ScheduleEntryOrdinal ==
                                window.ScheduleEntryOrdinal)
                        .EligibleFromTime,
                    InteractionEligibleUntilTimeExclusive =
                        window.InteractionEligibleUntilTimeExclusive,
                    RouteConnectorCount = window.RouteConnectorCount,
                    RouteWaitGameMinutes = window.RouteWaitGameMinutes,
                    RouteTimingEvidenceKind = window.RouteTimingEvidenceKind,
                    TimingEvidenceId = timingEvidenceId,
                    TimingPreconditions = new[]
                    {
                        "collision_grid_and_route_graph_match_capture",
                        "route_connectors_follow_calibrated_vanilla_control_surface",
                        "no_unmodeled_dialogue_collision_or_clearance_delay",
                        "npc_presence_window_and_native_eligibility_remain_valid"
                    },
                    GiftSlotIndex = giftBinding?.SlotIndex,
                    GiftItemId = giftBinding?.ItemId ?? string.Empty,
                    GiftQualifiedItemId =
                        giftBinding?.QualifiedItemId ?? string.Empty,
                    GiftQuality = giftBinding?.Quality,
                    GiftStackBefore = giftBinding?.StackBefore,
                    GiftTaste = giftBinding?.GiftTaste ?? string.Empty,
                    ExpectedFriendshipDelta =
                        giftBinding?.ExpectedFriendshipDelta
                });
            }
            return contact;
        }

        private static void RecordContactFailure(
            NpcFutureContactWindowResolution? resolution,
            string interactionKind,
            int scheduleEntryOrdinal,
            ICollection<string> blockingReasons,
            ISet<int> excludedWindowOrdinals,
            ISet<string> exclusionReasons)
        {
            var reasons = resolution?.BlockingReasons ?? Array.Empty<string>();
            if (reasons.Length > 0 && reasons.All(IsResolvedContactExclusion))
            {
                excludedWindowOrdinals.Add(scheduleEntryOrdinal);
                foreach (var reason in reasons)
                {
                    exclusionReasons.Add(
                        interactionKind + ":" + reason + ":" +
                            scheduleEntryOrdinal);
                }
                return;
            }

            if (reasons.Length == 0)
            {
                blockingReasons.Add(
                    "current_social_" + interactionKind +
                    "_contact_resolution_failed:" + scheduleEntryOrdinal);
                return;
            }
            foreach (var reason in reasons)
            {
                blockingReasons.Add(
                    interactionKind + ":" + reason + ":" +
                        scheduleEntryOrdinal);
            }
        }

        private static bool IsResolvedRouteExclusion(string reason) =>
            reason is
                "future_route_connector_approach_unreachable" or
                "future_route_target_adjacent_stand_unreachable" or
                "future_route_connector_not_allowed_on_date" or
                "future_route_connector_closed_before_arrival" or
                "future_route_connector_arrival_outside_supported_day" or
                "future_route_source_map_inaccessible_on_date" or
                "future_route_target_map_inaccessible_on_date";

        private static bool IsResolvedContactExclusion(string reason) =>
            reason is
                "future_contact_player_arrives_after_npc_departure" or
                "future_contact_interaction_not_allowed" or
                "future_route_segment_not_allowed_on_date" or
                "future_route_segment_gate_closed_before_arrival" or
                "future_route_arrival_outside_supported_day";

        private static bool IsNativeSocializationDisabled(JsonElement row) =>
            string.Equals(
                ReadString(row, "can_socialize_condition").Trim(),
                "FALSE",
                StringComparison.OrdinalIgnoreCase);

        private static NpcFuturePresenceWindowResolution SingleWindowPresence(
            NpcFuturePresenceWindowResolution source,
            NpcFuturePresenceWindow window) => new()
        {
            Status = NpcFuturePresenceWindowResolutionStatus.Exact,
            NpcName = source.NpcName,
            SelectedScheduleKey = source.SelectedScheduleKey,
            ResolvedScheduleKey = source.ResolvedScheduleKey,
            Windows = new[] { window }
        };

        private static CurrentSocialNpcCoverage BlockedNpc(
            string npcName,
            string reason,
            string scheduleStatus = "Blocked") =>
            BlockedNpc(npcName, new[] { reason }, scheduleStatus);

        private static CurrentSocialNpcCoverage BlockedNpc(
            string npcName,
            string[] reasons,
            string scheduleStatus = "Blocked") => new()
        {
            NpcName = npcName,
            Status = "blocked",
            ScheduleProjectionStatus = scheduleStatus,
            BlockingReasons = reasons
        };

        private static CurrentSocialContactFrontier Blocked(
            string reason) => Blocked(new[] { reason });

        private static CurrentSocialContactFrontier Blocked(
            string[] reasons) => new()
        {
            Status = "blocked",
            RankingAdmissionReady = false,
            BlockingReasons = reasons
        };

        private static bool TryReadInputs(
            JsonElement snapshot,
            out int totalDays,
            out int gameTime,
            out string gameVersion,
            out string startLocation,
            out int startX,
            out int startY,
            out JsonElement catalogField,
            out JsonElement catalog,
            out JsonElement schedules,
            out JsonElement routeGraph,
            out JsonElement routeDateEvidence,
            out JsonElement movementContext,
            out string reason)
        {
            totalDays = -1;
            gameTime = -1;
            gameVersion = string.Empty;
            startLocation = string.Empty;
            startX = 0;
            startY = 0;
            catalogField = default;
            catalog = default;
            schedules = default;
            routeGraph = default;
            routeDateEvidence = default;
            movementContext = default;
            reason = "current_social_snapshot_inputs_incomplete";
            if (snapshot.ValueKind != JsonValueKind.Object ||
                !snapshot.TryGetProperty("game_version", out var gameVersionNode) ||
                gameVersionNode.ValueKind != JsonValueKind.String ||
                !snapshot.TryGetProperty("state", out var state) ||
                !TryReadField(state, "time", "total_days", out var totalDaysNode) ||
                !TryReadField(state, "time", "time", out var gameTimeNode) ||
                !TryReadField(state, "player", "location_id", out var locationNode) ||
                !TryReadField(state, "player", "tile_x", out var xNode) ||
                !TryReadField(state, "player", "tile_y", out var yNode) ||
                !TryReadFieldNode(state, "player", "movement_timing_context", out movementContext) ||
                !TryReadFieldNode(state, "npcs", "schedule_catalog", out catalogField) ||
                !TryUnwrap(catalogField, out catalog) ||
                !TryReadField(state, "npcs", "schedules", out schedules) ||
                !TryReadField(state, "locations", "route_graph", out routeGraph) ||
                !TryReadFieldNode(state, "locations", "social_route_date_evidence", out routeDateEvidence) ||
                !totalDaysNode.TryGetInt32(out totalDays) ||
                !gameTimeNode.TryGetInt32(out gameTime) ||
                locationNode.ValueKind != JsonValueKind.String ||
                !xNode.TryGetInt32(out startX) ||
                !yNode.TryGetInt32(out startY) ||
                !catalog.TryGetProperty("villagers", out var villagers) ||
                villagers.ValueKind != JsonValueKind.Array)
            {
                return false;
            }
            gameVersion = gameVersionNode.GetString() ?? string.Empty;
            startLocation = locationNode.GetString() ?? string.Empty;
            if (gameVersion.Length == 0 || startLocation.Length == 0 ||
                totalDays < 0 || !IsCurrentDayTime(gameTime))
            {
                reason =
                    "current_social_frontier_requires_valid_current_day_snapshot";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private static bool IsCurrentDayTime(int value) =>
            value >= 600 &&
            value <= 2600 &&
            value % 100 < 60 &&
            value % 10 == 0;

        private static bool TryReadFieldNode(
            JsonElement state,
            string domain,
            string name,
            out JsonElement field)
        {
            field = default;
            return state.ValueKind == JsonValueKind.Object &&
                state.TryGetProperty(domain, out var domainNode) &&
                domainNode.ValueKind == JsonValueKind.Object &&
                domainNode.TryGetProperty(name, out field) &&
                field.ValueKind == JsonValueKind.Object;
        }

        private static bool TryReadField(
            JsonElement state,
            string domain,
            string name,
            out JsonElement value)
        {
            value = default;
            return TryReadFieldNode(state, domain, name, out var field) &&
                TryUnwrap(field, out value);
        }

        private static bool TryUnwrap(
            JsonElement field,
            out JsonElement value)
        {
            value = default;
            return ReadString(field, "status") is "available" or "derived" &&
                field.TryGetProperty("value", out value);
        }

        private static string ReadString(JsonElement source, string name) =>
            source.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;

        private static bool? ReadBool(JsonElement source, string name) =>
            source.TryGetProperty(name, out var property) &&
            property.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? property.GetBoolean()
                : null;

        private static int ToMinutes(int time) =>
            time / 100 * 60 + time % 100;

        private static int FromMinutes(int minutes) =>
            minutes / 60 * 100 + minutes % 60;

    }
}

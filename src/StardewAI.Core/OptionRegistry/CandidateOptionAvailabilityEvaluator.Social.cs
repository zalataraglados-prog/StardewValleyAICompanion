using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Verifier;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private EventCandidate[] SocialCandidates(
            SnapshotEnvelope snapshot,
            string optionId,
            string[] missingStateFactors,
            SmallModelActionParameter[] boundParameters)
        {
            var candidates = missingStateFactors.Any(factor => factor != "npcs.schedules")
                ? Array.Empty<EventCandidate>()
                : SocialCandidateBuilder.Build(snapshot, optionId, int.MaxValue);
            var continuationNpc = ReadParameter(boundParameters, "continuation.npc_name");
            var continuationTarget = ReadParameter(boundParameters, "continuation.target_location");
            var hasCurrentContinuationNpc = candidates.Any(candidate =>
                string.Equals(ReadParameter(candidate.Parameters, "npc_name"), continuationNpc, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(continuationNpc) &&
                !string.IsNullOrWhiteSpace(continuationTarget) &&
                !hasCurrentContinuationNpc)
            {
                candidates = candidates
                    .Concat(new[] { LastObservedSocialContinuationCandidate(optionId, continuationNpc, continuationTarget, boundParameters) })
                    .ToArray();
            }
            if (candidates.Length == 0) return candidates;

            var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
            var routeCandidates = RouteConnectorCandidates(snapshot, int.MaxValue)
                .Where(candidate => candidate.Kind == "route_connector_tile")
                .ToArray();
            var routed = candidates
                .Select(candidate =>
                    string.IsNullOrWhiteSpace(candidate.LocationId) ||
                    string.Equals(candidate.LocationId, currentLocation, StringComparison.OrdinalIgnoreCase)
                        ? candidate
                        : SocialRouteCandidate(snapshot, optionId, candidate, currentLocation, routeCandidates))
                .OrderByDescending(candidate => candidate.Available)
                .ThenBy(candidate => candidate.LocationId, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .Take(64)
                .ToArray();
            if (!string.IsNullOrWhiteSpace(continuationNpc))
            {
                routed = routed
                    .Select(candidate => string.Equals(ReadParameter(candidate.Parameters, "npc_name"), continuationNpc, StringComparison.OrdinalIgnoreCase) &&
                        !candidate.Available &&
                        candidate.BlockReasons.Length > 0 &&
                        candidate.BlockReasons.All(reason => reason == "social_menu_must_be_clear")
                            ? SocialContinuationCloseMenu(optionId, candidate)
                            : string.Equals(ReadParameter(candidate.Parameters, "npc_name"), continuationNpc, StringComparison.OrdinalIgnoreCase) &&
                        !candidate.Available &&
                        string.Equals(candidate.LocationId, currentLocation, StringComparison.OrdinalIgnoreCase) &&
                        candidate.BlockReasons.Length > 0 &&
                        candidate.BlockReasons.All(IsRetryableSocialContinuationBlock)
                            ? SocialContinuationRetryWait(optionId, candidate)
                            : candidate)
                    .ToArray();
            }
            return routed;
        }

        private static EventCandidate SocialContinuationCloseMenu(string optionId, EventCandidate candidate)
        {
            var npcName = ReadParameter(candidate.Parameters, "npc_name");
            return new EventCandidate
            {
                CandidateId = candidate.CandidateId + ":continuation-close-menu",
                Kind = "recovery_close_menu",
                Available = true,
                LocationId = candidate.LocationId,
                ExpectedEffect = "menu_not_blocking_same_social_objective;fresh_snapshot_replan_required=true",
                EstimatedTicks = 10,
                EnergyCost = 0,
                AvailabilityClass = "social_continuation_menu_recovery",
                GateReasons = candidate.BlockReasons,
                BlockReasons = Array.Empty<string>(),
                Parameters = new[]
                {
                    Parameter("execution_option_id", "executor.close_menu"),
                    Parameter("continuation.option_id", optionId),
                    Parameter("continuation.npc_name", npcName),
                    Parameter("continuation.target_location", candidate.LocationId),
                    Parameter("social_route.position_source", "npcs.social_interaction.current_loaded_instance"),
                    Parameter("social_route.future_schedule_projection", "not_used"),
                    Parameter("social_continuation_dialogue_recovery", "true")
                }
            };
        }

        private static bool IsRetryableSocialContinuationBlock(string reason)
        {
            return reason is "social_no_reachable_adjacent_stand_tile" or
                "social_npc_not_in_player_location" or
                "social_npc_not_in_player_location_stand_skipped" or
                "social_npc_busy" or
                "social_npc_has_controller" or
                "social_npc_sleeping";
        }

        private static EventCandidate SocialContinuationRetryWait(string optionId, EventCandidate candidate)
        {
            var npcName = ReadParameter(candidate.Parameters, "npc_name");
            var parameters = new List<SmallModelActionParameter>(candidate.Parameters)
            {
                Parameter("continuation.option_id", optionId),
                Parameter("continuation.npc_name", npcName),
                Parameter("continuation.target_location", candidate.LocationId),
                Parameter("social_route.position_source", "npcs.social_interaction.current_loaded_instance"),
                Parameter("social_route.future_schedule_projection", "not_used"),
                Parameter("retry_wait_ticks", "600")
            };
            return new EventCandidate
            {
                CandidateId = candidate.CandidateId + ":continuation-retry-wait",
                Kind = "social_continuation_retry_wait",
                Available = true,
                LocationId = candidate.LocationId,
                TileX = candidate.TileX,
                TileY = candidate.TileY,
                ExpectedEffect = "same_social_objective_retained=true;fresh_snapshot_replan_required=true;future_schedule_projection_not_used=true",
                EstimatedTicks = 600,
                EnergyCost = 0,
                AvailabilityClass = "current_loaded_social_target_temporarily_unreachable_retry",
                GateReasons = candidate.BlockReasons,
                BlockReasons = Array.Empty<string>(),
                Parameters = parameters.ToArray()
            };
        }

        private static EventCandidate LastObservedSocialContinuationCandidate(
            string optionId,
            string npcName,
            string targetLocation,
            SmallModelActionParameter[] boundParameters)
        {
            var parameters = new List<SmallModelActionParameter>
            {
                Parameter("npc_name", npcName),
                Parameter("social_route.position_source", "continuation.last_observed_current_loaded_instance")
            };
            foreach (var name in new[]
            {
                "continuation.slot_index",
                "continuation.qualified_item_id",
                "continuation.observed_state_hash",
                "continuation.observed_game_time"
            })
            {
                var value = ReadParameter(boundParameters, name);
                if (!string.IsNullOrWhiteSpace(value)) parameters.Add(Parameter(name, value));
            }

            return new EventCandidate
            {
                CandidateId = "social:continuation:" + optionId + ":" + npcName,
                Kind = optionId == "social.gift_npc" ? "social_gift_current" : "social_talk_current",
                Available = false,
                LocationId = targetLocation,
                ExpectedEffect = "social_target_from_last_observed_loaded_instance=true;fresh_snapshot_required_after_each_connector=true",
                AvailabilityClass = "last_observed_social_target_route_only",
                BlockReasons = new[] { "social_npc_not_in_player_location" },
                Parameters = parameters.ToArray()
            };
        }

        private EventCandidate SocialRouteCandidate(
            SnapshotEnvelope snapshot,
            string optionId,
            EventCandidate socialCandidate,
            string currentLocation,
            EventCandidate[] routeCandidates)
        {
            var remainingReasons = socialCandidate.BlockReasons
                .Where(reason => reason != "social_npc_not_in_player_location" &&
                    reason != "social_npc_not_in_player_location_stand_skipped")
                .ToList();
            var routePlan = FindResolvedSocialRoutePlan(
                snapshot,
                currentLocation,
                socialCandidate.LocationId,
                routeCandidates);
            if (routePlan is null)
            {
                remainingReasons.Add("social_cross_map_route_unavailable");
                return CopySocialRouteCandidate(
                    socialCandidate,
                    optionId,
                    currentLocation,
                    null,
                    0,
                    remainingReasons);
            }

            var routeCandidate = routePlan.FirstConnectorCandidate;
            if (routeCandidate is null)
            {
                remainingReasons.Add("social_cross_map_first_connector_not_available");
            }
            else
            {
                remainingReasons.AddRange(routeCandidate.BlockReasons);
            }

            return CopySocialRouteCandidate(
                socialCandidate,
                optionId,
                currentLocation,
                routeCandidate,
                routePlan.Path.Length,
                remainingReasons);
        }

        private static EventCandidate CopySocialRouteCandidate(
            EventCandidate socialCandidate,
            string optionId,
            string currentLocation,
            EventCandidate? routeCandidate,
            int remainingConnectorCount,
            IEnumerable<string> blockReasons)
        {
            var npcName = ReadParameter(socialCandidate.Parameters, "npc_name");
            var reasons = blockReasons
                .Where(reason => !string.IsNullOrWhiteSpace(reason))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var routeParameters = routeCandidate?.Parameters ?? Array.Empty<SmallModelActionParameter>();
            var continuationParameters = new List<SmallModelActionParameter>
            {
                Parameter("continuation.option_id", optionId),
                Parameter("continuation.npc_name", npcName),
                Parameter("continuation.target_location", socialCandidate.LocationId),
                Parameter("social_route.remaining_connector_count", remainingConnectorCount.ToString()),
                Parameter("social_route.position_source", string.IsNullOrWhiteSpace(ReadParameter(socialCandidate.Parameters, "social_route.position_source"))
                    ? "npcs.social_interaction.current_loaded_instance"
                    : ReadParameter(socialCandidate.Parameters, "social_route.position_source")),
                Parameter("social_route.future_schedule_projection", "not_used")
            };
            var slotIndex = ReadParameter(socialCandidate.Parameters, "slot_index");
            var qualifiedItemId = ReadParameter(socialCandidate.Parameters, "qualified_item_id");
            if (!string.IsNullOrWhiteSpace(slotIndex))
            {
                continuationParameters.Add(Parameter("continuation.slot_index", slotIndex));
            }
            if (!string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                continuationParameters.Add(Parameter("continuation.qualified_item_id", qualifiedItemId));
            }

            var expectedTargetLocation = ReadParameter(routeParameters, "expected_target_location");
            return new EventCandidate
            {
                CandidateId = socialCandidate.CandidateId + ":route:" + currentLocation + ":" +
                    (routeCandidate?.TileX?.ToString() ?? "none") + "," +
                    (routeCandidate?.TileY?.ToString() ?? "none"),
                Kind = "route_connector_tile",
                Available = routeCandidate is not null && reasons.Length == 0,
                LocationId = currentLocation,
                TileX = routeCandidate?.TileX,
                TileY = routeCandidate?.TileY,
                ExpectedEffect = "social_route_target_npc=" + npcName +
                    ";social_target_location=" + socialCandidate.LocationId +
                    ";next_location=" + expectedTargetLocation +
                    ";one_connector_then_fresh_snapshot=true" +
                    ";future_schedule_projection_not_used=true",
                ItemId = socialCandidate.ItemId,
                QualifiedItemId = socialCandidate.QualifiedItemId,
                SlotIndex = socialCandidate.SlotIndex,
                Quantity = socialCandidate.Quantity,
                EstimatedTicks = routeCandidate?.EstimatedTicks ?? -1,
                EnergyCost = 0,
                AvailabilityClass = routeCandidate is not null && reasons.Length == 0
                    ? "current_loaded_npc_cross_map_route_step"
                    : routeCandidate?.AllowedToday == true
                        ? "current_loaded_npc_cross_map_route_deferred"
                        : "current_loaded_npc_cross_map_route_blocked",
                AllowedNow = routeCandidate?.AllowedNow,
                AllowedToday = routeCandidate?.AllowedToday,
                NextOpenTime = routeCandidate?.NextOpenTime,
                EffectiveOpenTime = routeCandidate?.EffectiveOpenTime,
                ClosesAt = routeCandidate?.ClosesAt,
                WaitCost = routeCandidate?.WaitCost,
                GateReasons = routeCandidate?.GateReasons ?? Array.Empty<string>(),
                BlockReasons = reasons,
                Parameters = routeParameters
                    .Concat(continuationParameters)
                    .ToArray()
            };
        }

        private static bool ArrivalMatches(
            IEnumerable<SmallModelActionParameter> parameters,
            int? targetX,
            int? targetY)
        {
            var arrivalX = ReadParameterInt(parameters, "expected_arrival_tile_x");
            var arrivalY = ReadParameterInt(parameters, "expected_arrival_tile_y");
            return targetX.HasValue && targetY.HasValue
                ? arrivalX == targetX && arrivalY == targetY
                : !arrivalX.HasValue && !arrivalY.HasValue;
        }

        private static SocialRoutePlan? FindResolvedSocialRoutePlan(
            SnapshotEnvelope snapshot,
            string startLocation,
            string targetLocation,
            EventCandidate[] routeCandidates)
        {
            var graph = ReadStateFieldValue(snapshot, "locations", "route_graph");
            if (!graph.HasValue ||
                graph.Value.ValueKind != JsonValueKind.Object ||
                !graph.Value.TryGetProperty("edges", out var edgesElement) ||
                edgesElement.ValueKind != JsonValueKind.Array ||
                string.IsNullOrWhiteSpace(startLocation) ||
                string.IsNullOrWhiteSpace(targetLocation))
            {
                return null;
            }

            var edges = edgesElement.EnumerateArray()
                .Where(edge => edge.ValueKind == JsonValueKind.Object && ReadBool(edge, "resolved") == true)
                .Select(edge => new SocialRouteEdge(
                    ReadString(edge, "kind").ToLowerInvariant(),
                    ReadString(edge, "from_location"),
                    ReadString(edge, "target_location"),
                    ReadNullableInt(edge, "from_x"),
                    ReadNullableInt(edge, "from_y"),
                    ReadNullableInt(edge, "target_x"),
                    ReadNullableInt(edge, "target_y")))
                .Where(edge =>
                    !string.IsNullOrWhiteSpace(edge.Kind) &&
                    !string.IsNullOrWhiteSpace(edge.FromLocation) &&
                    !string.IsNullOrWhiteSpace(edge.TargetLocation) &&
                    edge.FromX.HasValue &&
                    edge.FromY.HasValue)
                .ToArray();
            var adjacency = edges
                .GroupBy(edge => edge.FromLocation, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderBy(edge => edge.TargetLocation, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(edge => edge.Kind, StringComparer.Ordinal)
                        .ThenBy(edge => edge.FromY)
                        .ThenBy(edge => edge.FromX)
                        .ToArray(),
                    StringComparer.OrdinalIgnoreCase);
            if (!adjacency.TryGetValue(startLocation, out var firstEdges))
            {
                return null;
            }

            var plans = new List<SocialRoutePlan>();
            foreach (var firstEdge in firstEdges)
            {
                var tail = FindShortestSocialRouteTail(adjacency, firstEdge.TargetLocation, targetLocation);
                if (tail is null)
                {
                    continue;
                }

                var firstConnectorCandidate = routeCandidates.FirstOrDefault(candidate =>
                    candidate.TileX == firstEdge.FromX &&
                    candidate.TileY == firstEdge.FromY &&
                    string.Equals(ReadParameter(candidate.Parameters, "connector_kind"), firstEdge.Kind, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(ReadParameter(candidate.Parameters, "expected_target_location"), firstEdge.TargetLocation, StringComparison.OrdinalIgnoreCase) &&
                    ArrivalMatches(candidate.Parameters, firstEdge.TargetX, firstEdge.TargetY));
                plans.Add(new SocialRoutePlan(
                    new[] { firstEdge }.Concat(tail).ToArray(),
                    firstConnectorCandidate));
            }

            return plans
                .OrderByDescending(plan => plan.FirstConnectorCandidate is not null &&
                    (plan.FirstConnectorCandidate.Available || plan.FirstConnectorCandidate.AllowedToday == true))
                .ThenByDescending(plan => plan.FirstConnectorCandidate is not null)
                .ThenBy(plan => plan.Path.Length)
                .ThenByDescending(plan => plan.FirstConnectorCandidate?.Available == true)
                .ThenBy(plan => plan.Path[0].TargetLocation, StringComparer.OrdinalIgnoreCase)
                .ThenBy(plan => plan.Path[0].Kind, StringComparer.Ordinal)
                .ThenBy(plan => plan.Path[0].FromY)
                .ThenBy(plan => plan.Path[0].FromX)
                .FirstOrDefault();
        }

        private static SocialRouteEdge[]? FindShortestSocialRouteTail(
            IReadOnlyDictionary<string, SocialRouteEdge[]> adjacency,
            string startLocation,
            string targetLocation)
        {
            if (string.Equals(startLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
            {
                return Array.Empty<SocialRouteEdge>();
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { startLocation };
            var queue = new Queue<(string Location, SocialRouteEdge[] Path)>();
            queue.Enqueue((startLocation, Array.Empty<SocialRouteEdge>()));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!adjacency.TryGetValue(current.Location, out var outgoing))
                {
                    continue;
                }

                foreach (var edge in outgoing)
                {
                    var path = current.Path.Concat(new[] { edge }).ToArray();
                    if (string.Equals(edge.TargetLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
                    {
                        return path;
                    }
                    if (visited.Add(edge.TargetLocation))
                    {
                        queue.Enqueue((edge.TargetLocation, path));
                    }
                }
            }

            return null;
        }

        private sealed class SocialRoutePlan
        {
            public SocialRoutePlan(SocialRouteEdge[] path, EventCandidate? firstConnectorCandidate)
            {
                Path = path;
                FirstConnectorCandidate = firstConnectorCandidate;
            }

            public SocialRouteEdge[] Path { get; }
            public EventCandidate? FirstConnectorCandidate { get; }
        }

        private sealed class SocialRouteEdge
        {
            public SocialRouteEdge(
                string kind,
                string fromLocation,
                string targetLocation,
                int? fromX,
                int? fromY,
                int? targetX,
                int? targetY)
            {
                Kind = kind;
                FromLocation = fromLocation;
                TargetLocation = targetLocation;
                FromX = fromX;
                FromY = fromY;
                TargetX = targetX;
                TargetY = targetY;
            }

            public string Kind { get; }
            public string FromLocation { get; }
            public string TargetLocation { get; }
            public int? FromX { get; }
            public int? FromY { get; }
            public int? TargetX { get; }
            public int? TargetY { get; }
        }

    }
}

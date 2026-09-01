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
        private const int MaxSocialContinuationRetryCount = 12;

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
                            ? SocialContinuationRetryCandidate(
                                snapshot,
                                optionId,
                                candidate,
                                boundParameters)
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

        private static EventCandidate SocialContinuationRetryCandidate(
            SnapshotEnvelope snapshot,
            string optionId,
            EventCandidate candidate,
            SmallModelActionParameter[] boundParameters)
        {
            var npcName = ReadParameter(candidate.Parameters, "npc_name");
            var currentGameTime = ReadStateFieldIntOptional(
                snapshot,
                "time",
                "time");
            var retryCount = int.TryParse(
                ReadParameter(boundParameters, "continuation.retry_count"),
                out var parsedRetryCount)
                    ? Math.Max(0, parsedRetryCount)
                    : 0;
            var priorRetryGameTime = int.TryParse(
                ReadParameter(
                    boundParameters,
                    "continuation.retry_game_time"),
                out var parsedRetryGameTime)
                    ? parsedRetryGameTime
                    : (int?)null;
            var parameters = candidate.Parameters
                .Where(parameter => parameter.Name is not
                    "continuation.option_id" and not
                    "continuation.npc_name" and not
                    "continuation.target_location" and not
                    "continuation.retry_count" and not
                    "continuation.retry_game_time")
                .ToList();
            parameters.AddRange(new[]
            {
                Parameter("continuation.option_id", optionId),
                Parameter("continuation.npc_name", npcName),
                Parameter("continuation.target_location", candidate.LocationId),
                Parameter("social_route.position_source", "npcs.social_interaction.current_loaded_instance"),
                Parameter("social_route.future_schedule_projection", "not_used"),
                Parameter("retry_wait_ticks", "600"),
                Parameter(
                    "continuation.retry_count",
                    (retryCount + 1).ToString()),
                Parameter(
                    "continuation.retry_game_time",
                    currentGameTime?.ToString() ?? string.Empty)
            });

            var retryBlock = currentGameTime is null
                ? "social_continuation_game_time_unavailable"
                : retryCount >= MaxSocialContinuationRetryCount
                    ? "social_continuation_retry_budget_exhausted"
                    : retryCount > 0 &&
                        priorRetryGameTime == currentGameTime
                        ? "social_continuation_game_time_not_advancing"
                        : string.Empty;
            if (!string.IsNullOrWhiteSpace(retryBlock))
            {
                return new EventCandidate
                {
                    CandidateId = candidate.CandidateId +
                        ":continuation-retry-blocked",
                    Kind = "social_continuation_retry_wait",
                    Available = false,
                    LocationId = candidate.LocationId,
                    TileX = candidate.TileX,
                    TileY = candidate.TileY,
                    ExpectedEffect =
                        "same_social_objective_released_for_day=true",
                    EstimatedTicks = 0,
                    EnergyCost = 0,
                    AvailabilityClass =
                        "social_continuation_retry_exhausted",
                    GateReasons = candidate.BlockReasons,
                    BlockReasons = new[] { retryBlock },
                    Parameters = parameters.ToArray()
                };
            }

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
                "continuation.partnership_action_kind",
                "continuation.observed_state_hash",
                "continuation.observed_game_time",
                "continuation.retry_count",
                "continuation.retry_game_time"
            })
            {
                var value = ReadParameter(boundParameters, name);
                if (!string.IsNullOrWhiteSpace(value)) parameters.Add(Parameter(name, value));
            }

            return new EventCandidate
            {
                CandidateId = "social:continuation:" + optionId + ":" + npcName,
                Kind = optionId == "social.gift_npc"
                    ? "social_gift_current"
                    : optionId == "social.advance_partnership"
                        ? PartnershipContinuationCandidateKind(ReadParameter(boundParameters, "continuation.partnership_action_kind"))
                        : "social_talk_current",
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
                    reason != "social_npc_not_in_player_location_stand_skipped" &&
                    reason != "partnership_npc_not_in_player_location")
                .ToList();
            var routePlan = FindResolvedRoutePlan(
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

            var routeCandidate = routePlan.FirstActionCandidate;
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

        private static string PartnershipContinuationCandidateKind(string actionKind)
        {
            return actionKind switch
            {
                "bouquet" => "partnership_bouquet_current",
                "propose_marriage" => "partnership_propose_marriage_current",
                "propose_roommate" => "partnership_propose_roommate_current",
                _ => string.Empty
            };
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
            var partnershipActionKind = ReadParameter(socialCandidate.Parameters, "partnership_action_kind");
            if (!string.IsNullOrWhiteSpace(slotIndex))
            {
                continuationParameters.Add(Parameter("continuation.slot_index", slotIndex));
            }
            if (!string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                continuationParameters.Add(Parameter("continuation.qualified_item_id", qualifiedItemId));
            }
            if (!string.IsNullOrWhiteSpace(partnershipActionKind))
            {
                continuationParameters.Add(Parameter("continuation.partnership_action_kind", partnershipActionKind));
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

    }
}

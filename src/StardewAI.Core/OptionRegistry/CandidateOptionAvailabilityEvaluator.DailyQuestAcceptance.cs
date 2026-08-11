using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] DailyQuestAcceptanceCandidates(SnapshotEnvelope snapshot)
    {
        var state = ReadStateFieldValue(snapshot, "quests", "daily_quest_offer");
        if (!state.HasValue || state.Value.ValueKind != JsonValueKind.Object)
        {
            return new[] { BlockedDailyQuestAcceptanceCandidate("daily_quest_offer_transparent_state_missing") };
        }

        var offer = state.Value;
        var reasons = ReadDailyQuestStringArray(offer, "blocked_diagnostics").ToList();
        var canAccept = ReadBool(offer, "can_accept") == true;
        var menuClear = ReadBool(offer, "menu_clear") == true;
        var boardLocation = ReadString(offer, "board_location_id");
        var boardX = ReadNullableInt(offer, "board_action_tile_x");
        var boardY = ReadNullableInt(offer, "board_action_tile_y");
        var standX = ReadNullableInt(offer, "stand_tile_x");
        var standY = ReadNullableInt(offer, "stand_tile_y");
        var fingerprint = ReadString(offer, "offer_fingerprint");
        var quest = offer.TryGetProperty("quest", out var questValue) && questValue.ValueKind == JsonValueKind.Object
            ? questValue
            : default;
        var questId = quest.ValueKind == JsonValueKind.Object ? ReadString(quest, "id") : string.Empty;
        var runtimeType = quest.ValueKind == JsonValueKind.Object ? ReadString(quest, "runtime_type") : string.Empty;
        var title = quest.ValueKind == JsonValueKind.Object ? ReadString(quest, "title") : string.Empty;
        var currentObjective = quest.ValueKind == JsonValueKind.Object ? ReadString(quest, "current_objective") : string.Empty;
        if (!canAccept)
        {
            reasons.Add("daily_quest_native_can_accept_false");
        }
        if (!menuClear)
        {
            reasons.Add("daily_quest_menu_or_dialogue_not_clear");
        }
        if (string.IsNullOrWhiteSpace(boardLocation) || !boardX.HasValue || !boardY.HasValue ||
            !standX.HasValue || !standY.HasValue)
        {
            reasons.Add("daily_quest_board_endpoint_incomplete");
        }
        if (string.IsNullOrWhiteSpace(runtimeType) || string.IsNullOrWhiteSpace(fingerprint))
        {
            reasons.Add("daily_quest_offer_identity_incomplete");
        }

        var identityParameters = new[]
        {
            Parameter("quest_candidate_id", "daily_quest_offer:" + fingerprint),
            Parameter("quest_family", "ordinary_quest"),
            Parameter("quest_id", questId),
            Parameter("quest_runtime_type", runtimeType),
            Parameter("quest_interaction_kind", "accept_daily"),
            Parameter("quest_offer_fingerprint", fingerprint),
            Parameter("quest_offer_title", title),
            Parameter("quest_offer_current_objective", currentObjective)
        };
        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        if (!string.Equals(currentLocation, boardLocation, StringComparison.OrdinalIgnoreCase))
        {
            var routePlan = FindResolvedRoutePlan(
                snapshot,
                currentLocation,
                boardLocation,
                RouteConnectorCandidates(snapshot));
            var route = routePlan?.FirstConnectorCandidate;
            if (route is null)
            {
                reasons.Add("daily_quest_board_route_unavailable");
                return new[] { BlockedDailyQuestAcceptanceCandidate(reasons.ToArray()) };
            }

            reasons.AddRange(route.BlockReasons);
            var routeReasons = reasons.Distinct(StringComparer.Ordinal).ToArray();
            return new[]
            {
                new EventCandidate
                {
                    CandidateId = "quest.accept_daily:route:" + fingerprint + ":" + currentLocation + ":" + route.TileX + "," + route.TileY,
                    Kind = "route_connector_tile",
                    Available = route.Available && routeReasons.Length == 0,
                    LocationId = currentLocation,
                    TileX = route.TileX,
                    TileY = route.TileY,
                    ExpectedEffect = "daily_quest_board_route_target=" + boardLocation + ";one_connector_then_fresh_snapshot=true",
                    EstimatedTicks = route.EstimatedTicks,
                    EnergyCost = 0,
                    AvailabilityClass = "daily_quest_cross_map_route_step",
                    AllowedNow = route.AllowedNow,
                    AllowedToday = route.AllowedToday,
                    NextOpenTime = route.NextOpenTime,
                    EffectiveOpenTime = route.EffectiveOpenTime,
                    ClosesAt = route.ClosesAt,
                    WaitCost = route.WaitCost,
                    GateReasons = route.GateReasons,
                    BlockReasons = routeReasons,
                    Parameters = route.Parameters
                        .Concat(new[]
                        {
                            Parameter("continuation.option_id", "quest.accept_daily"),
                            Parameter("continuation.target_location", boardLocation)
                        })
                        .Concat(identityParameters)
                        .ToArray()
                }
            };
        }

        var playerX = ReadStateFieldIntOptional(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldIntOptional(snapshot, "player", "tile_y");
        if (playerX != standX || playerY != standY)
        {
            var approachReasons = reasons.Distinct(StringComparer.Ordinal).ToArray();
            return new[]
            {
                new EventCandidate
                {
                    CandidateId = "quest.accept_daily:approach:" + fingerprint,
                    Kind = "daily_quest_board_approach",
                    Available = approachReasons.Length == 0,
                    LocationId = boardLocation,
                    TileX = standX,
                    TileY = standY,
                    DisplayName = title,
                    ExpectedEffect = "player_at_daily_quest_board_stand_tile=true;fresh_snapshot_replan_required=true",
                    EstimatedTicks = 180,
                    EnergyCost = 0,
                    AvailabilityClass = "daily_quest_board_approach",
                    BlockReasons = approachReasons,
                    Parameters = new[]
                        {
                            Parameter("target_tile_x", standX?.ToString() ?? string.Empty),
                            Parameter("target_tile_y", standY?.ToString() ?? string.Empty),
                            Parameter("board_action_tile_x", boardX?.ToString() ?? string.Empty),
                            Parameter("board_action_tile_y", boardY?.ToString() ?? string.Empty),
                            Parameter("max_movement_tiles", "96")
                        }
                        .Concat(identityParameters)
                        .ToArray()
                }
            };
        }

        var terminalReasons = reasons.Distinct(StringComparer.Ordinal).ToArray();
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "quest.accept_daily:" + fingerprint,
                Kind = "accept_daily_quest",
                Available = terminalReasons.Length == 0,
                LocationId = boardLocation,
                TileX = boardX,
                TileY = boardY,
                DisplayName = title,
                ExpectedEffect = "native_daily_quest_added_to_actor_quest_log=true;accepted_daily_quest=true;days_left=2",
                EstimatedTicks = 180,
                EnergyCost = 0,
                AvailabilityClass = terminalReasons.Length == 0 ? "native_daily_quest_offer_ready" : "native_daily_quest_offer_blocked",
                BlockReasons = terminalReasons,
                Parameters = new[]
                    {
                        Parameter("target_tile_x", boardX?.ToString() ?? string.Empty),
                        Parameter("target_tile_y", boardY?.ToString() ?? string.Empty),
                        Parameter("stand_tile_x", standX?.ToString() ?? string.Empty),
                        Parameter("stand_tile_y", standY?.ToString() ?? string.Empty),
                        Parameter("expected_action_type", "Billboard"),
                        Parameter("max_movement_tiles", "96")
                    }
                    .Concat(identityParameters)
                    .ToArray()
            }
        };
    }

    private static EventCandidate BlockedDailyQuestAcceptanceCandidate(params string[] reasons) => new()
    {
        CandidateId = "quest.accept_daily:blocked",
        Kind = "accept_daily_quest",
        Available = false,
        LocationId = "Town",
        ExpectedEffect = "daily_quest_not_accepted",
        AvailabilityClass = "daily_quest_offer_blocked",
        BlockReasons = reasons
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Distinct(StringComparer.Ordinal)
            .ToArray()
    };

    private static string[] ReadDailyQuestStringArray(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray()
            : Array.Empty<string>();
    }
}

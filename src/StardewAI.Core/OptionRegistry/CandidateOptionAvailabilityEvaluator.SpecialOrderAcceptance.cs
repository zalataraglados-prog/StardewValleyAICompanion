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
    private EventCandidate[] SpecialOrderAcceptanceCandidates(SnapshotEnvelope snapshot)
    {
        var state = ReadStateFieldValue(snapshot, "quests", "special_order_boards");
        if (!state.HasValue || state.Value.ValueKind != JsonValueKind.Array)
        {
            return new[] { BlockedSpecialOrderCandidate("special_order_boards_transparent_state_missing") };
        }

        var boards = state.Value.EnumerateArray()
            .Where(board => board.ValueKind == JsonValueKind.Object)
            .ToArray();
        if (boards.Length == 0)
        {
            return new[] { BlockedSpecialOrderCandidate("no_loaded_special_order_board_endpoints") };
        }

        var candidates = boards
            .SelectMany(board => SpecialOrderBoardCandidates(snapshot, board))
            .ToArray();
        return candidates.Length == 0
            ? new[] { BlockedSpecialOrderCandidate("no_special_order_board_stage_candidates") }
            : candidates;
    }

    private EventCandidate[] SpecialOrderBoardCandidates(SnapshotEnvelope snapshot, JsonElement board)
    {
        var boardType = ReadString(board, "board_type");
        var location = ReadString(board, "location_id");
        var actionToken = ReadString(board, "action_token");
        var actionX = ReadNullableInt(board, "action_tile_x");
        var actionY = ReadNullableInt(board, "action_tile_y");
        var standX = ReadNullableInt(board, "stand_tile_x");
        var standY = ReadNullableInt(board, "stand_tile_y");
        var unlocked = ReadBool(board, "unlocked") == true;
        var accepted = ReadBool(board, "accepted_this_cycle") == true;
        var menuOpen = ReadBool(board, "menu_open") == true;
        var dialogueReady = ReadBool(board, "dialogue_ready_for_board") == true;
        var reasons = ReadDailyQuestStringArray(board, "blocked_diagnostics")
            .Where(reason => reason != "special_order_offers_not_materialized")
            .ToList();
        if (!unlocked) reasons.Add("special_order_board_locked");
        if (accepted) reasons.Add("special_order_type_already_accepted_this_cycle");
        if (string.IsNullOrWhiteSpace(location) || string.IsNullOrWhiteSpace(actionToken) ||
            !actionX.HasValue || !actionY.HasValue || !standX.HasValue || !standY.HasValue)
        {
            reasons.Add("special_order_board_endpoint_incomplete");
        }

        var boardKey = string.IsNullOrEmpty(boardType) ? "town" : boardType;
        var boardParameters = new[]
        {
            Parameter("special_order_board_type", boardType),
            Parameter("special_order_board_key", boardKey),
            Parameter("special_order_action_token", actionToken),
            Parameter("board_action_tile_x", actionX?.ToString() ?? string.Empty),
            Parameter("board_action_tile_y", actionY?.ToString() ?? string.Empty),
            Parameter("stand_tile_x", standX?.ToString() ?? string.Empty),
            Parameter("stand_tile_y", standY?.ToString() ?? string.Empty)
        };
        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        if (!string.Equals(currentLocation, location, StringComparison.OrdinalIgnoreCase))
        {
            var route = FindResolvedRoutePlan(
                snapshot,
                currentLocation,
                location,
                RouteConnectorCandidates(snapshot))?.FirstConnectorCandidate;
            if (route is null)
            {
                reasons.Add("special_order_board_route_unavailable");
                return new[] { BlockedSpecialOrderCandidate(reasons.ToArray()) };
            }

            reasons.AddRange(route.BlockReasons);
            var routeReasons = reasons.Distinct(StringComparer.Ordinal).ToArray();
            return new[]
            {
                new EventCandidate
                {
                    CandidateId = "quest.accept_special_order:route:" + boardKey + ":" + currentLocation + ":" + route.TileX + "," + route.TileY,
                    Kind = "route_connector_tile",
                    Available = route.Available && routeReasons.Length == 0,
                    LocationId = currentLocation,
                    TileX = route.TileX,
                    TileY = route.TileY,
                    ExpectedEffect = "special_order_board_route_target=" + location + ";one_connector_then_fresh_snapshot=true",
                    EstimatedTicks = route.EstimatedTicks,
                    EnergyCost = 0,
                    AvailabilityClass = "special_order_cross_map_route_step",
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
                            Parameter("continuation.option_id", "quest.accept_special_order"),
                            Parameter("continuation.target_location", location)
                        })
                        .Concat(boardParameters)
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
                    CandidateId = "quest.accept_special_order:approach:" + boardKey,
                    Kind = "special_order_board_approach",
                    Available = approachReasons.Length == 0,
                    LocationId = location,
                    TileX = standX,
                    TileY = standY,
                    ExpectedEffect = "player_at_special_order_board_stand_tile=true;fresh_snapshot_replan_required=true",
                    EstimatedTicks = 240,
                    EnergyCost = 0,
                    AvailabilityClass = "special_order_board_approach",
                    BlockReasons = approachReasons,
                    Parameters = boardParameters
                }
            };
        }

        if (dialogueReady)
        {
            var dialogueReasons = reasons.Distinct(StringComparer.Ordinal).ToArray();
            return new[]
            {
                new EventCandidate
                {
                    CandidateId = "quest.accept_special_order:dialogue:" + boardKey,
                    Kind = "special_order_board_dialogue_advance",
                    Available = dialogueReasons.Length == 0,
                    LocationId = location,
                    DisplayName = "Marlon special-order board dialogue",
                    ExpectedEffect = "active_menu.type=SpecialOrdersBoard;fresh_snapshot_replan_required=true",
                    EstimatedTicks = 120,
                    EnergyCost = 0,
                    AvailabilityClass = "special_order_board_native_dialogue",
                    BlockReasons = dialogueReasons,
                    Parameters = boardParameters
                }
            };
        }

        if (!menuOpen)
        {
            var openReasons = reasons.Distinct(StringComparer.Ordinal).ToArray();
            return new[]
            {
                new EventCandidate
                {
                    CandidateId = "quest.accept_special_order:open:" + boardKey,
                    Kind = "special_order_board_open",
                    Available = openReasons.Length == 0,
                    LocationId = location,
                    TileX = actionX,
                    TileY = actionY,
                    ExpectedEffect = "native_special_order_board_interaction_started=true;fresh_snapshot_replan_required=true",
                    EstimatedTicks = 120,
                    EnergyCost = 0,
                    AvailabilityClass = "special_order_board_open",
                    BlockReasons = openReasons,
                    Parameters = boardParameters
                }
            };
        }

        var offers = board.TryGetProperty("offers", out var offerArray) && offerArray.ValueKind == JsonValueKind.Array
            ? offerArray.EnumerateArray().Where(offer => offer.ValueKind == JsonValueKind.Object).ToArray()
            : Array.Empty<JsonElement>();
        if (offers.Length == 0)
        {
            reasons.Add("special_order_board_has_no_visible_offers");
            return new[] { BlockedSpecialOrderCandidate(reasons.ToArray()) };
        }

        return offers.Select(offer => SpecialOrderOfferCandidate(location, boardParameters, reasons, offer)).ToArray();
    }

    private static EventCandidate SpecialOrderOfferCandidate(
        string location,
        SmallModelActionParameter[] boardParameters,
        List<string> baseReasons,
        JsonElement offer)
    {
        var index = ReadInt(offer, "selection_index");
        var side = ReadString(offer, "selection_side");
        var fingerprint = ReadString(offer, "offer_fingerprint");
        var order = offer.TryGetProperty("order", out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : default;
        var questKey = order.ValueKind == JsonValueKind.Object ? ReadString(order, "quest_key") : string.Empty;
        var questName = order.ValueKind == JsonValueKind.Object ? ReadString(order, "quest_name") : string.Empty;
        var requester = order.ValueKind == JsonValueKind.Object ? ReadString(order, "requester") : string.Empty;
        var generationSeed = order.ValueKind == JsonValueKind.Object ? ReadInt(order, "generation_seed") : 0;
        var dueDate = order.ValueKind == JsonValueKind.Object ? ReadInt(order, "due_date") : 0;
        var duration = order.ValueKind == JsonValueKind.Object ? ReadString(order, "duration") : string.Empty;
        var reasons = new List<string>(baseReasons);
        if (index is < 0 or > 1 || string.IsNullOrWhiteSpace(side)) reasons.Add("special_order_selection_index_invalid");
        if (string.IsNullOrWhiteSpace(questKey) || string.IsNullOrWhiteSpace(fingerprint)) reasons.Add("special_order_offer_identity_incomplete");
        var terminalReasons = reasons.Distinct(StringComparer.Ordinal).ToArray();
        return new EventCandidate
        {
            CandidateId = "quest.accept_special_order:" + fingerprint,
            Kind = "accept_special_order",
            Available = terminalReasons.Length == 0,
            LocationId = location,
            DisplayName = questName,
            ExpectedEffect = "native_special_order_added_to_team=true;accepted_special_order_type=true",
            EstimatedTicks = 60,
            EnergyCost = 0,
            AvailabilityClass = "native_special_order_offer",
            BlockReasons = terminalReasons,
            Parameters = boardParameters.Concat(new[]
            {
                Parameter("quest_candidate_id", "special_order_offer:" + fingerprint),
                Parameter("quest_family", "special_order"),
                Parameter("quest_key", questKey),
                Parameter("quest_interaction_kind", "accept_special_order"),
                Parameter("quest_offer_fingerprint", fingerprint),
                Parameter("quest_offer_title", questName),
                Parameter("special_order_requester", requester),
                Parameter("special_order_generation_seed", generationSeed.ToString()),
                Parameter("special_order_due_date", dueDate.ToString()),
                Parameter("special_order_duration", duration),
                Parameter("special_order_selection_index", index.ToString()),
                Parameter("special_order_selection_side", side)
            }).ToArray()
        };
    }

    private static EventCandidate BlockedSpecialOrderCandidate(params string[] reasons) => new()
    {
        CandidateId = "quest.accept_special_order:blocked",
        Kind = "accept_special_order",
        Available = false,
        ExpectedEffect = "special_order_not_accepted",
        AvailabilityClass = "special_order_acceptance_blocked",
        BlockReasons = reasons
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Distinct(StringComparer.Ordinal)
            .ToArray()
    };
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] PrizeTicketRewardCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] intent)
    {
        var projection = ReadStateFieldValue(snapshot, "player", "prize_ticket_reward");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            ReadString(projection.Value, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(projection.Value, "invocation_policy") != "autonomous_positive_reward")
            return Array.Empty<EventCandidate>();

        var expectedLevel = PrizeTicketIntent(intent, "expected_prize_level");
        var expectedReward = PrizeTicketIntent(intent, "expected_reward_fingerprint");
        if ((!string.IsNullOrWhiteSpace(expectedLevel) && expectedLevel != ReadInt(projection.Value, "current_prize_level").ToString(CultureInfo.InvariantCulture)) ||
            (!string.IsNullOrWhiteSpace(expectedReward) && expectedReward != ReadString(projection.Value, "current_reward_fingerprint")))
            return Array.Empty<EventCandidate>();

        var stage = ReadString(projection.Value, "stage");
        var targetLocation = ReadString(projection.Value, "target_location_id");
        if (stage is not ("collect_pending_ticket" or "redeem_prize") || string.IsNullOrWhiteSpace(targetLocation))
            return Array.Empty<EventCandidate>();
        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        if (!string.Equals(currentLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
            return PrizeTicketRewardRouteCandidates(snapshot, projection.Value, currentLocation, targetLocation);

        var endpoints = PrizeTicketActionTiles(projection.Value, stage)
            .Select(tile => new { tile, stand = FindBestStandTile(snapshot, tile.X, tile.Y) })
            .Where(row => row.stand is not null)
            .OrderBy(row => Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - row.stand!.X) +
                Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - row.stand!.Y))
            .ThenBy(row => row.tile.Y).ThenBy(row => row.tile.X)
            .FirstOrDefault();
        var reasons = PrizeTicketRewardStringArray(projection.Value, "blocked_diagnostics").ToList();
        if (ReadString(projection.Value, "service_status") != "ready")
            reasons.Add("prize_ticket_reward_service_not_ready:" + ReadString(projection.Value, "service_status"));
        if (endpoints is null) reasons.Add("prize_ticket_reward_no_reachable_native_endpoint");
        if (stage == "collect_pending_ticket" && ReadBool(projection.Value, "pending_ticket_capacity_sufficient") != true)
            reasons.Add("prize_ticket_pending_ticket_capacity_not_proven");
        var parameters = endpoints is null
            ? Array.Empty<SmallModelActionParameter>()
            : PrizeTicketRewardCandidateParameters(projection.Value, endpoints.tile, endpoints.stand!);
        reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
        {
            OptionId = "rewards.claim_prize_ticket",
            Parameters = parameters
        }));
        var blocking = reasons.Distinct(StringComparer.Ordinal).ToArray();
        var rewardFingerprint = ReadString(projection.Value, "current_reward_fingerprint");
        var candidateFingerprint = rewardFingerprint[..Math.Min(12, rewardFingerprint.Length)];
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "prize-ticket-reward:" + stage + ":" + candidateFingerprint,
                Kind = "claim_prize_ticket",
                Available = blocking.Length == 0,
                AllowedNow = blocking.Length == 0,
                AllowedToday = blocking.Length == 0,
                LocationId = targetLocation,
                TileX = endpoints?.tile.X,
                TileY = endpoints?.tile.Y,
                DisplayName = stage == "redeem_prize" ? "Redeem one Prize Ticket reward" : "Collect one earned Special Order Prize Ticket",
                EstimatedTicks = stage == "redeem_prize" ? 480 : 240,
                EnergyCost = 0,
                AvailabilityClass = "autonomous_positive_native_prize_ticket_reward_single_stage",
                ExpectedEffect = stage == "redeem_prize"
                    ? "PrizeTicket=-1;ticketPrizesClaimed=+1;exact_current_reward=inventory_or_debris"
                    : "specialOrderPrizeTickets=-1;inventory_PrizeTicket=+1;continue_to_redeem_prize",
                BlockReasons = blocking,
                Parameters = parameters
            }
        };
    }

    private EventCandidate[] PrizeTicketRewardRouteCandidates(
        SnapshotEnvelope snapshot,
        JsonElement projection,
        string currentLocation,
        string targetLocation)
    {
        if (ReadString(projection, "service_status") != "route_required" ||
            PrizeTicketActionTiles(projection, ReadString(projection, "stage")).Length == 0)
            return Array.Empty<EventCandidate>();
        var route = FindResolvedRoutePlan(snapshot, currentLocation, targetLocation,
            RouteConnectorCandidates(snapshot, int.MaxValue).Where(candidate => candidate.Kind == "route_connector_tile").ToArray());
        if (route?.FirstConnectorCandidate is null) return Array.Empty<EventCandidate>();
        return new[]
        {
            CloneCandidate(route.FirstConnectorCandidate,
                candidateId: "prize-ticket-route:" + ReadString(projection, "stage") + ":" + currentLocation,
                expectedEffect: route.FirstConnectorCandidate.ExpectedEffect + ";prize_ticket_reward_continuation=true",
                parameters: route.FirstConnectorCandidate.Parameters.Concat(PrizeTicketRewardContinuationParameters(projection)).ToArray(),
                availabilityClass: "prize_ticket_reward_rolling_route")
        };
    }

    private static SmallModelActionParameter[] PrizeTicketRewardCandidateParameters(
        JsonElement projection,
        PrizeTicketActionTile tile,
        CandidateTile stand)
    {
        var reward = projection.GetProperty("current_reward");
        var parameters = new List<SmallModelActionParameter>
        {
            Parameter("prize_ticket_stage", ReadString(projection, "stage")),
            Parameter("prize_ticket_projection_fingerprint", ReadString(projection, "projection_fingerprint")),
            Parameter("prize_ticket_current_reward_fingerprint", ReadString(projection, "current_reward_fingerprint")),
            Parameter("prize_ticket_preview_json", projection.GetProperty("preview_track").GetRawText()),
            Parameter("prize_ticket_inventory_count_before", ReadInt(projection, "inventory_ticket_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("prize_ticket_pending_count_before", ReadInt(projection, "pending_special_order_ticket_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("prize_ticket_claimed_count_before", ReadInt(projection, "ticket_prizes_claimed").ToString(CultureInfo.InvariantCulture)),
            Parameter("prize_ticket_prize_level", ReadInt(projection, "current_prize_level").ToString(CultureInfo.InvariantCulture)),
            Parameter("prize_ticket_reward_qualified_item_id", ReadString(reward, "qualified_item_id")),
            Parameter("prize_ticket_reward_item_id", ReadString(reward, "item_id")),
            Parameter("prize_ticket_reward_stack", ReadInt(reward, "stack").ToString(CultureInfo.InvariantCulture)),
            Parameter("prize_ticket_reward_quality", ReadInt(reward, "quality").ToString(CultureInfo.InvariantCulture)),
            Parameter("prize_ticket_reward_runtime_type", ReadString(reward, "runtime_type")),
            Parameter("prize_ticket_inventory_max_items", ReadInt(projection, "inventory_max_items").ToString(CultureInfo.InvariantCulture)),
            Parameter("prize_ticket_inventory_occupied_slots", ReadInt(projection, "inventory_occupied_slots").ToString(CultureInfo.InvariantCulture)),
            Parameter("prize_ticket_pending_capacity_sufficient", (ReadBool(projection, "pending_ticket_capacity_sufficient") == true).ToString().ToLowerInvariant()),
            Parameter("target_location", tile.LocationId),
            Parameter("target_tile_x", tile.X.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", tile.Y.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)),
            Parameter("prize_ticket_action_raw", tile.ActionRaw),
            Parameter("native_contract", ReadString(projection, "native_contract")),
            Parameter("max_movement_tiles", "512")
        };
        parameters.AddRange(PrizeTicketRewardContinuationParameters(projection));
        return parameters.ToArray();
    }

    private static SmallModelActionParameter[] PrizeTicketRewardContinuationParameters(JsonElement projection) => new[]
    {
        Parameter("continuation.option_id", "rewards.claim_prize_ticket"),
        Parameter("continuation.expected_prize_level", ReadInt(projection, "current_prize_level").ToString(CultureInfo.InvariantCulture)),
        Parameter("continuation.expected_reward_fingerprint", ReadString(projection, "current_reward_fingerprint"))
    };

    private static PrizeTicketActionTile[] PrizeTicketActionTiles(JsonElement projection, string stage)
    {
        var property = stage == "redeem_prize" ? "prize_machine_action_tiles" : "special_order_ticket_action_tiles";
        if (!projection.TryGetProperty(property, out var rows) || rows.ValueKind != JsonValueKind.Array)
            return Array.Empty<PrizeTicketActionTile>();
        return rows.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object)
            .Select(row => new PrizeTicketActionTile(ReadString(row, "location_id"), ReadInt(row, "tile_x"), ReadInt(row, "tile_y"), ReadString(row, "action_raw")))
            .ToArray();
    }

    private static string PrizeTicketIntent(SmallModelActionParameter[] intent, string name) =>
        intent.FirstOrDefault(parameter => parameter.Name == "continuation." + name || parameter.Name == name)?.Value ?? string.Empty;

    private static string[] PrizeTicketRewardStringArray(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty).Where(item => item.Length > 0).ToArray()
            : Array.Empty<string>();

    private sealed record PrizeTicketActionTile(string LocationId, int X, int Y, string ActionRaw);
}

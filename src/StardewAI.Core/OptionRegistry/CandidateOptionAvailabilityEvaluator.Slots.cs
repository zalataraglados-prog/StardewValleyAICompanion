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
    private EventCandidate[] SlotsCandidates(SnapshotEnvelope snapshot)
    {
        var projection = ReadStateFieldValue(snapshot, "player", "slots");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return Array.Empty<EventCandidate>();
        var row = projection.Value;
        if (ReadString(row, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadBool(row, "automatic_currency_demand") != true ||
            ReadBool(row, "has_club_card") != true ||
            ReadInt(row, "remaining_club_coin_demand") <= 0)
            return Array.Empty<EventCandidate>();

        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var targetLocation = ReadString(row, "location_id");
        if (!string.Equals(currentLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
            return SlotsRouteCandidates(snapshot, row, currentLocation, targetLocation);

        var tile = ReadSlotsTiles(row)
            .Select(value => new { tile = value, stand = FindBestStandTile(snapshot, value.X, value.Y) })
            .Where(value => value.stand is not null)
            .OrderBy(value => Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - value.stand!.X) +
                Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - value.stand!.Y))
            .FirstOrDefault();
        var reasons = new List<string>();
        if (ReadString(row, "gate_status") != "ready")
            reasons.Add(ReadString(row, "gate_status"));
        if (tile is null)
            reasons.Add("slots_machine_has_no_reachable_stand_tile");
        if (!SlotsProjectionIsTyped(row))
            reasons.Add("slots_typed_projection_invalid");

        var bet = ReadInt(row, "recommended_bet");
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "slots:" + bet + ":" + ReadInt(row, "times_played"),
                Kind = "play_slots",
                Available = reasons.Count == 0,
                AllowedNow = reasons.Count == 0,
                AllowedToday = reasons.Count == 0,
                LocationId = targetLocation,
                TileX = tile?.tile.X,
                TileY = tile?.tile.Y,
                EstimatedTicks = 1800,
                EnergyCost = 0,
                AvailabilityClass = "transparent_native_slots_collection_currency_spin",
                ExpectedEffect = "slots_native_stochastic_spin_settled;expected_net_coin_delta=" +
                    ReadDouble(row, "expected_net_coin_delta").ToString("R", CultureInfo.InvariantCulture) +
                    ";target_club_coins=" + ReadInt(row, "target_club_coins"),
                BlockReasons = reasons.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = tile is null
                    ? Array.Empty<SmallModelActionParameter>()
                    : SlotsParameters(row, tile.tile, tile.stand!)
            }
        };
    }

    private EventCandidate[] SlotsRouteCandidates(
        SnapshotEnvelope snapshot,
        JsonElement projection,
        string currentLocation,
        string targetLocation)
    {
        if (ReadString(projection, "gate_status") != "route_to_club_required")
            return Array.Empty<EventCandidate>();
        var route = FindResolvedRoutePlan(snapshot, currentLocation, targetLocation,
            RouteConnectorCandidates(snapshot, int.MaxValue).Where(value => value.Kind == "route_connector_tile").ToArray());
        if (route?.FirstActionCandidate is null)
            return Array.Empty<EventCandidate>();
        return new[]
        {
            CloneCandidate(
                route.FirstActionCandidate,
                candidateId: "slots-route:" + currentLocation,
                expectedEffect: route.FirstActionCandidate.ExpectedEffect + ";slots_currency_demand=true",
                parameters: route.FirstActionCandidate.Parameters.Concat(new[]
                {
                    Parameter("continuation.option_id", "minigame.play_slots"),
                    Parameter("continuation.slots_target_club_coins", ReadInt(projection, "target_club_coins").ToString(CultureInfo.InvariantCulture)),
                    Parameter("continuation.slots_target_item_id", ReadString(projection, "target_qualified_item_id"))
                }).ToArray(),
                availabilityClass: "slots_rolling_route")
        };
    }

    private static SmallModelActionParameter[] SlotsParameters(JsonElement projection, SlotsTile tile, CandidateTile stand) =>
        new[]
        {
            Parameter("target_location", ReadString(projection, "location_id")),
            Parameter("target_tile_x", tile.X.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", tile.Y.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)),
            Parameter("slots_action_raw", tile.ActionRaw),
            Parameter("slots_action_token", tile.ActionToken),
            Parameter("slots_projection_fingerprint", ReadString(projection, "projection_fingerprint")),
            Parameter("slots_bet", ReadInt(projection, "recommended_bet").ToString(CultureInfo.InvariantCulture)),
            Parameter("slots_club_coins_before", ReadInt(projection, "club_coins").ToString(CultureInfo.InvariantCulture)),
            Parameter("slots_target_club_coins", ReadInt(projection, "target_club_coins").ToString(CultureInfo.InvariantCulture)),
            Parameter("slots_remaining_club_coin_demand", ReadInt(projection, "remaining_club_coin_demand").ToString(CultureInfo.InvariantCulture)),
            Parameter("slots_target_item_id", ReadString(projection, "target_qualified_item_id")),
            Parameter("slots_times_played_before", ReadInt(projection, "times_played").ToString(CultureInfo.InvariantCulture)),
            Parameter("slots_daily_luck", ReadDouble(projection, "daily_luck").ToString("R", CultureInfo.InvariantCulture)),
            Parameter("slots_luck_level", ReadInt(projection, "luck_level").ToString(CultureInfo.InvariantCulture)),
            Parameter("slots_luck_multiplier", ReadDouble(projection, "luck_multiplier").ToString("R", CultureInfo.InvariantCulture)),
            Parameter("slots_expected_payout_multiplier", ReadDouble(projection, "expected_payout_multiplier").ToString("R", CultureInfo.InvariantCulture)),
            Parameter("slots_expected_net_coin_delta", ReadDouble(projection, "expected_net_coin_delta").ToString("R", CultureInfo.InvariantCulture)),
            Parameter("slots_payout_rows_json", ReadRawSlotsProjection(projection, "payout_rows")),
            Parameter("slots_rng_contract", ReadString(projection, "rng_contract")),
            Parameter("slots_exit_policy", ReadString(projection, "exit_policy")),
            Parameter("native_contract", ReadString(projection, "native_contract")),
            Parameter("max_movement_tiles", "512")
        };

    private static bool SlotsProjectionIsTyped(JsonElement projection)
    {
        var bet = ReadInt(projection, "recommended_bet");
        var coins = ReadInt(projection, "club_coins");
        var luckMultiplier = ReadDouble(projection, "luck_multiplier");
        var expectedPayout = ReadDouble(projection, "expected_payout_multiplier");
        var expectedNet = ReadDouble(projection, "expected_net_coin_delta");
        return bet is 10 or 100 && coins >= bet &&
            ReadString(projection, "target_qualified_item_id") == "(BC)126" &&
            ReadInt(projection, "target_club_coins") == 10000 &&
            Math.Abs(luckMultiplier - (1d + ReadDouble(projection, "daily_luck") * 2d + ReadInt(projection, "luck_level") * 0.08d)) < 1e-12 &&
            expectedPayout > 0d && Math.Abs(expectedNet - bet * (expectedPayout - 1d)) < 1e-8 &&
            ReadString(projection, "rng_contract") == "shared_Game1.random_live_feedback_not_stable_future_prediction" &&
            ReadString(projection, "exit_policy") == "done_after_one_native_settlement" &&
            ReadString(projection, "native_contract") == "ClubSlots_checkAction_then_native_Slots_10_or_100_spin_then_native_random_settlement_then_done" &&
            ReadRawSlotsProjection(projection, "payout_rows") != "[]";
    }

    private static SlotsTile[] ReadSlotsTiles(JsonElement projection)
    {
        if (!projection.TryGetProperty("interaction_tiles", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return Array.Empty<SlotsTile>();
        return rows.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object)
            .Select(row => new SlotsTile(
                ReadInt(row, "tile_x"),
                ReadInt(row, "tile_y"),
                ReadString(row, "action_raw"),
                ReadString(row, "action_token")))
            .Where(row => row.ActionToken == "ClubSlots")
            .ToArray();
    }

    private static string ReadRawSlotsProjection(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) ? value.GetRawText() : "[]";

    private sealed record SlotsTile(int X, int Y, string ActionRaw, string ActionToken);
}

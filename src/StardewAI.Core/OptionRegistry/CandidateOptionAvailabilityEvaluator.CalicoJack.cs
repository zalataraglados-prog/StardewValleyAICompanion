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
    private EventCandidate[] CalicoJackCandidates(SnapshotEnvelope snapshot)
    {
        var projection = ReadStateFieldValue(snapshot, "player", "calico_jack");
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
            return CalicoJackRouteCandidates(snapshot, row, currentLocation, targetLocation);

        var bet = ReadInt(row, "recommended_bet");
        var tile = ReadCalicoJackTiles(row)
            .Where(value => value.Bet == bet)
            .Select(value => new { tile = value, stand = FindBestStandTile(snapshot, value.X, value.Y) })
            .Where(value => value.stand is not null)
            .OrderBy(value => Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - value.stand!.X) +
                Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - value.stand!.Y))
            .FirstOrDefault();
        var reasons = new List<string>();
        if (ReadString(row, "gate_status") != "ready")
            reasons.Add(ReadString(row, "gate_status"));
        if (tile is null)
            reasons.Add("calico_jack_recommended_table_has_no_reachable_stand_tile");
        if (!CalicoJackProjectionIsTyped(row, bet))
            reasons.Add("calico_jack_typed_projection_invalid");

        var expectedDelta = ReadInt(ReadRequiredObject(row, "next_round"), "coin_delta_per_low_bet") * bet / 100;
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "calico-jack:" + (bet == 1000 ? "high" : "low") + ":" + ReadInt(row, "next_times_played_seed"),
                Kind = "play_calico_jack",
                Available = reasons.Count == 0,
                AllowedNow = reasons.Count == 0,
                AllowedToday = reasons.Count == 0,
                LocationId = targetLocation,
                TileX = tile?.tile.X,
                TileY = tile?.tile.Y,
                EstimatedTicks = 2400,
                EnergyCost = 0,
                AvailabilityClass = "transparent_native_calico_jack_collection_currency_round",
                ExpectedEffect = "calico_jack_native_round_settled;club_coins_delta=" + expectedDelta +
                    ";target_club_coins=" + ReadInt(row, "target_club_coins") +
                    ";projected_outcome=" + ReadString(ReadRequiredObject(row, "next_round"), "projected_outcome"),
                BlockReasons = reasons.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = tile is null
                    ? Array.Empty<SmallModelActionParameter>()
                    : CalicoJackParameters(row, tile.tile, tile.stand!, expectedDelta)
            }
        };
    }

    private EventCandidate[] CalicoJackRouteCandidates(
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
                candidateId: "calico-jack-route:" + currentLocation,
                expectedEffect: route.FirstActionCandidate.ExpectedEffect + ";calico_jack_currency_demand=true",
                parameters: route.FirstActionCandidate.Parameters.Concat(new[]
                {
                    Parameter("continuation.option_id", "minigame.play_calico_jack"),
                    Parameter("continuation.calico_target_club_coins", ReadInt(projection, "target_club_coins").ToString(CultureInfo.InvariantCulture)),
                    Parameter("continuation.calico_target_item_id", ReadString(projection, "target_qualified_item_id"))
                }).ToArray(),
                availabilityClass: "calico_jack_rolling_route")
        };
    }

    private static SmallModelActionParameter[] CalicoJackParameters(
        JsonElement projection,
        CalicoJackTile tile,
        CandidateTile stand,
        int expectedDelta)
    {
        var next = ReadRequiredObject(projection, "next_round");
        return new[]
        {
            Parameter("target_location", ReadString(projection, "location_id")),
            Parameter("target_tile_x", tile.X.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", tile.Y.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)),
            Parameter("calico_action_raw", tile.ActionRaw),
            Parameter("calico_action_token", tile.ActionToken),
            Parameter("calico_table_kind", tile.TableKind),
            Parameter("calico_bet", tile.Bet.ToString(CultureInfo.InvariantCulture)),
            Parameter("calico_dialogue_key", tile.DialogueKey),
            Parameter("calico_play_response_key", tile.PlayResponseKey),
            Parameter("calico_projection_fingerprint", ReadString(projection, "projection_fingerprint")),
            Parameter("calico_club_coins_before", ReadInt(projection, "club_coins").ToString(CultureInfo.InvariantCulture)),
            Parameter("calico_target_club_coins", ReadInt(projection, "target_club_coins").ToString(CultureInfo.InvariantCulture)),
            Parameter("calico_remaining_club_coin_demand", ReadInt(projection, "remaining_club_coin_demand").ToString(CultureInfo.InvariantCulture)),
            Parameter("calico_target_item_id", ReadString(projection, "target_qualified_item_id")),
            Parameter("calico_times_played_seed", ReadInt(next, "times_played_seed").ToString(CultureInfo.InvariantCulture)),
            Parameter("calico_days_played_seed", ReadInt(projection, "days_played_seed").ToString(CultureInfo.InvariantCulture)),
            Parameter("calico_unique_game_id_seed", ReadString(projection, "unique_game_id_seed")),
            Parameter("calico_daily_luck", ReadDouble(projection, "daily_luck").ToString("R", CultureInfo.InvariantCulture)),
            Parameter("calico_luck_level", ReadInt(projection, "luck_level").ToString(CultureInfo.InvariantCulture)),
            Parameter("calico_player_cards_json", ReadRaw(next, "player_cards")),
            Parameter("calico_dealer_cards_json", ReadRaw(next, "dealer_cards_including_hidden")),
            Parameter("calico_recommended_first_action", ReadString(next, "recommended_first_action")),
            Parameter("calico_projected_next_hit_card", ReadInt(next, "projected_next_hit_card").ToString(CultureInfo.InvariantCulture)),
            Parameter("calico_coin_delta_per_low_bet", ReadInt(next, "coin_delta_per_low_bet").ToString(CultureInfo.InvariantCulture)),
            Parameter("calico_expected_coin_delta", expectedDelta.ToString(CultureInfo.InvariantCulture)),
            Parameter("calico_projected_outcome", ReadString(next, "projected_outcome")),
            Parameter("calico_decision_policy", "exact_seed_replay_hidden_card_and_future_draw_max_coin_delta"),
            Parameter("calico_exit_policy", "quit_after_one_native_settlement"),
            Parameter("native_contract", "ClubCards_or_BlackJack_checkAction_then_CalicoJack_Play_then_native_CalicoJack_hit_or_stand_then_native_settlement_then_quit"),
            Parameter("max_movement_tiles", "512")
        };
    }

    private static bool CalicoJackProjectionIsTyped(JsonElement projection, int bet)
    {
        if (bet is not (100 or 1000) || ReadInt(projection, "club_coins") < bet ||
            ReadString(projection, "recommended_table_kind") != (bet == 1000 ? "high_stakes" : "low_stakes") ||
            ReadString(projection, "target_qualified_item_id") != "(BC)126" ||
            ReadInt(projection, "target_club_coins") != 10000 ||
            ReadString(projection, "native_contract") != "ClubCards_or_BlackJack_checkAction_then_CalicoJack_Play_then_native_CalicoJack_hit_or_stand_then_native_settlement_then_quit")
            return false;
        var next = ReadRequiredObject(projection, "next_round");
        return ReadInt(next, "times_played_seed") == ReadInt(projection, "next_times_played_seed") &&
            ReadString(next, "recommended_first_action") is "hit" or "stand" &&
            ReadString(next, "projected_outcome") is "player_calico_jack" or "player_bust" or "dealer_bust" or "draw" or "player_higher" or "dealer_higher" &&
            ReadRaw(next, "player_cards") != "[]" && ReadRaw(next, "dealer_cards_including_hidden") != "[]";
    }

    private static CalicoJackTile[] ReadCalicoJackTiles(JsonElement projection)
    {
        if (!projection.TryGetProperty("interaction_tiles", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return Array.Empty<CalicoJackTile>();
        return rows.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object)
            .Select(row => new CalicoJackTile(
                ReadInt(row, "tile_x"),
                ReadInt(row, "tile_y"),
                ReadString(row, "action_raw"),
                ReadString(row, "action_token"),
                ReadString(row, "table_kind"),
                ReadInt(row, "bet"),
                ReadString(row, "dialogue_key"),
                ReadString(row, "play_response_key")))
            .ToArray();
    }

    private static JsonElement ReadRequiredObject(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Object ? value : default;

    private static string ReadRaw(JsonElement row, string property) =>
        row.ValueKind == JsonValueKind.Object && row.TryGetProperty(property, out var value) ? value.GetRawText() : "[]";

    private sealed record CalicoJackTile(
        int X,
        int Y,
        string ActionRaw,
        string ActionToken,
        string TableKind,
        int Bet,
        string DialogueKey,
        string PlayResponseKey);
}

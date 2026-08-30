using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static CompiledActionStep[] CompilePlayCalicoJackStep(SmallModelAction action)
    {
        var bet = ReadIntParameter(action, "calico_bet");
        var expectedDelta = ReadIntParameter(action, "calico_expected_coin_delta");
        if (bet is not (100 or 1000) || !expectedDelta.HasValue)
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step(
                "play_calico_jack",
                "CalicoJack:" + ReadParameter(action, "calico_table_kind") + ":bet=" + bet.Value +
                    ":seed=" + ReadParameter(action, "calico_times_played_seed"),
                "native_round_settled;club_coins_delta=" + expectedDelta.Value + ";minigame_closed=true",
                2400)
        };
    }

    private static string[] ValidateCalicoJackPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.play_calico_jack")
            return Array.Empty<string>();
        var reasons = new List<string>();
        var actionX = ReadIntParameter(action, "target_tile_x");
        var actionY = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var bet = ReadIntParameter(action, "calico_bet");
        var coinsBefore = ReadIntParameter(action, "calico_club_coins_before");
        var targetCoins = ReadIntParameter(action, "calico_target_club_coins");
        var expectedDelta = ReadIntParameter(action, "calico_expected_coin_delta");
        var deltaPerLowBet = ReadIntParameter(action, "calico_coin_delta_per_low_bet");
        var dailyLuck = ReadDoubleParameter(action, "calico_daily_luck");
        if (!actionX.HasValue || !actionY.HasValue || !standX.HasValue || !standY.HasValue ||
            Math.Abs(actionX.Value - standX.Value) + Math.Abs(actionY.Value - standY.Value) != 1 ||
            bet is not (100 or 1000) || !coinsBefore.HasValue || coinsBefore.Value < bet.Value || targetCoins != 10000 ||
            !expectedDelta.HasValue || !deltaPerLowBet.HasValue || !dailyLuck.HasValue ||
            expectedDelta.Value != deltaPerLowBet.Value * bet.Value / 100 ||
            ReadParameter(action, "calico_target_item_id") != "(BC)126" ||
            ReadParameter(action, "calico_action_token") is not ("ClubCards" or "BlackJack") ||
            ReadParameter(action, "calico_table_kind") != (bet == 1000 ? "high_stakes" : "low_stakes") ||
            ReadParameter(action, "calico_dialogue_key") != (bet == 1000 ? "CalicoJackHS" : "CalicoJack") ||
            ReadParameter(action, "calico_play_response_key") != "Play" ||
            ReadParameter(action, "calico_recommended_first_action") is not ("hit" or "stand") ||
            ReadParameter(action, "calico_decision_policy") != "exact_seed_replay_hidden_card_and_future_draw_max_coin_delta" ||
            ReadParameter(action, "calico_exit_policy") != "quit_after_one_native_settlement" ||
            ReadParameter(action, "native_contract") != "ClubCards_or_BlackJack_checkAction_then_CalicoJack_Play_then_native_CalicoJack_hit_or_stand_then_native_settlement_then_quit")
            return new[] { "calico_jack_typed_projection_required" };

        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("calico_jack_menu_must_be_clear");
        var location = ReadParameter(action, "target_location");
        if (!string.Equals(location, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            reasons.Add("calico_jack_target_location_mismatch");

        var projection = ReadStateFieldValue(snapshot, "player", "calico_jack");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return reasons.Append("calico_jack_projection_unavailable").ToArray();
        var row = projection.Value;
        var next = row.TryGetProperty("next_round", out var nextValue) && nextValue.ValueKind == JsonValueKind.Object
            ? nextValue
            : default;
        if (ReadString(row, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(row, "gate_status") != "ready" ||
            ReadBool(row, "automatic_currency_demand") != true ||
            ReadBool(row, "has_club_card") != true ||
            ReadString(row, "location_id") != location ||
            ReadInt(row, "club_coins") != coinsBefore ||
            ReadInt(row, "recommended_bet") != bet ||
            ReadString(row, "projection_fingerprint") != ReadParameter(action, "calico_projection_fingerprint") ||
            ReadInt(row, "target_club_coins") != targetCoins ||
            ReadInt(row, "remaining_club_coin_demand") != ReadIntParameter(action, "calico_remaining_club_coin_demand") ||
            ReadInt(row, "next_times_played_seed") != ReadIntParameter(action, "calico_times_played_seed") ||
            ReadInt(row, "days_played_seed") != ReadIntParameter(action, "calico_days_played_seed") ||
            ReadString(row, "unique_game_id_seed") != ReadParameter(action, "calico_unique_game_id_seed") ||
            Math.Abs(ReadDouble(row, "daily_luck") - dailyLuck.Value) > 1e-12 ||
            ReadInt(row, "luck_level") != ReadIntParameter(action, "calico_luck_level") ||
            ReadInt(next, "times_played_seed") != ReadIntParameter(action, "calico_times_played_seed") ||
            ReadRawJson(next, "player_cards") != ReadParameter(action, "calico_player_cards_json") ||
            ReadRawJson(next, "dealer_cards_including_hidden") != ReadParameter(action, "calico_dealer_cards_json") ||
            ReadString(next, "recommended_first_action") != ReadParameter(action, "calico_recommended_first_action") ||
            ReadInt(next, "projected_next_hit_card") != ReadIntParameter(action, "calico_projected_next_hit_card") ||
            ReadInt(next, "coin_delta_per_low_bet") != deltaPerLowBet ||
            ReadString(next, "projected_outcome") != ReadParameter(action, "calico_projected_outcome") ||
            !CalicoJackTileMatches(row, actionX.Value, actionY.Value,
                ReadParameter(action, "calico_action_raw") ?? string.Empty, bet.Value))
            reasons.Add("calico_jack_projection_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool CalicoJackTileMatches(JsonElement projection, int x, int y, string actionRaw, int bet)
    {
        return projection.TryGetProperty("interaction_tiles", out var rows) && rows.ValueKind == JsonValueKind.Array &&
            rows.EnumerateArray().Any(row => row.ValueKind == JsonValueKind.Object &&
                ReadInt(row, "tile_x") == x && ReadInt(row, "tile_y") == y &&
                ReadString(row, "action_raw") == actionRaw && ReadInt(row, "bet") == bet);
    }

    private static string ReadRawJson(JsonElement row, string property) =>
        row.ValueKind == JsonValueKind.Object && row.TryGetProperty(property, out var value) ? value.GetRawText() : string.Empty;
}

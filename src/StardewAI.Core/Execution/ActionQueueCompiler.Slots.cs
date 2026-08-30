using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private const string SlotsCompilerNativeContract =
        "ClubSlots_checkAction_then_native_Slots_10_or_100_spin_then_native_random_settlement_then_done";

    private static CompiledActionStep[] CompilePlaySlotsStep(SmallModelAction action)
    {
        var bet = ReadIntParameter(action, "slots_bet");
        if (bet is not (10 or 100))
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step(
                "play_slots",
                "Slots:bet=" + bet.Value + ":times_played_before=" + ReadParameter(action, "slots_times_played_before"),
                "native_stochastic_spin_settled;coin_delta_observed=true;minigame_closed=true",
                1800)
        };
    }

    private static string[] ValidateSlotsPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.play_slots")
            return Array.Empty<string>();
        var reasons = new List<string>();
        var actionX = ReadIntParameter(action, "target_tile_x");
        var actionY = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var bet = ReadIntParameter(action, "slots_bet");
        var coinsBefore = ReadIntParameter(action, "slots_club_coins_before");
        var dailyLuck = ReadDoubleParameter(action, "slots_daily_luck");
        var luckLevel = ReadIntParameter(action, "slots_luck_level");
        var luckMultiplier = ReadDoubleParameter(action, "slots_luck_multiplier");
        var expectedPayout = ReadDoubleParameter(action, "slots_expected_payout_multiplier");
        var expectedNet = ReadDoubleParameter(action, "slots_expected_net_coin_delta");
        if (!actionX.HasValue || !actionY.HasValue || !standX.HasValue || !standY.HasValue ||
            Math.Abs(actionX.Value - standX.Value) + Math.Abs(actionY.Value - standY.Value) != 1 ||
            bet is not (10 or 100) || !coinsBefore.HasValue || coinsBefore.Value < bet.Value ||
            ReadIntParameter(action, "slots_target_club_coins") != 10000 ||
            ReadIntParameter(action, "slots_remaining_club_coin_demand") <= 0 ||
            ReadParameter(action, "slots_target_item_id") != "(BC)126" ||
            ReadParameter(action, "slots_action_token") != "ClubSlots" ||
            !dailyLuck.HasValue || !luckLevel.HasValue || !luckMultiplier.HasValue || !expectedPayout.HasValue || !expectedNet.HasValue ||
            Math.Abs(luckMultiplier.Value - (1d + dailyLuck.Value * 2d + luckLevel.Value * 0.08d)) > 1e-12 ||
            Math.Abs(expectedNet.Value - bet.Value * (expectedPayout.Value - 1d)) > 1e-8 ||
            ReadParameter(action, "slots_rng_contract") != "shared_Game1.random_live_feedback_not_stable_future_prediction" ||
            ReadParameter(action, "slots_exit_policy") != "done_after_one_native_settlement" ||
            ReadParameter(action, "native_contract") != SlotsCompilerNativeContract ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "slots_payout_rows_json")))
            return new[] { "slots_typed_projection_required" };

        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("slots_menu_must_be_clear");
        var location = ReadParameter(action, "target_location");
        if (!string.Equals(location, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            reasons.Add("slots_target_location_mismatch");

        var projection = ReadStateFieldValue(snapshot, "player", "slots");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return reasons.Append("slots_projection_unavailable").ToArray();
        var row = projection.Value;
        if (ReadString(row, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(row, "gate_status") != "ready" ||
            ReadBool(row, "automatic_currency_demand") != true ||
            ReadBool(row, "has_club_card") != true ||
            ReadString(row, "location_id") != location ||
            ReadInt(row, "club_coins") != coinsBefore ||
            ReadInt(row, "recommended_bet") != bet ||
            ReadString(row, "projection_fingerprint") != ReadParameter(action, "slots_projection_fingerprint") ||
            ReadInt(row, "target_club_coins") != ReadIntParameter(action, "slots_target_club_coins") ||
            ReadInt(row, "remaining_club_coin_demand") != ReadIntParameter(action, "slots_remaining_club_coin_demand") ||
            ReadInt(row, "times_played") != ReadIntParameter(action, "slots_times_played_before") ||
            Math.Abs(ReadDouble(row, "daily_luck") - dailyLuck.Value) > 1e-12 ||
            ReadInt(row, "luck_level") != luckLevel ||
            Math.Abs(ReadDouble(row, "luck_multiplier") - luckMultiplier.Value) > 1e-12 ||
            Math.Abs(ReadDouble(row, "expected_payout_multiplier") - expectedPayout.Value) > 1e-12 ||
            Math.Abs(ReadDouble(row, "expected_net_coin_delta") - expectedNet.Value) > 1e-8 ||
            ReadRawSlotsJson(row, "payout_rows") != ReadParameter(action, "slots_payout_rows_json") ||
            !SlotsTileMatches(row, actionX.Value, actionY.Value, ReadParameter(action, "slots_action_raw") ?? string.Empty))
            reasons.Add("slots_projection_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool SlotsTileMatches(JsonElement projection, int x, int y, string actionRaw) =>
        projection.TryGetProperty("interaction_tiles", out var rows) && rows.ValueKind == JsonValueKind.Array &&
        rows.EnumerateArray().Any(row => row.ValueKind == JsonValueKind.Object &&
            ReadInt(row, "tile_x") == x && ReadInt(row, "tile_y") == y &&
            ReadString(row, "action_raw") == actionRaw && ReadString(row, "action_token") == "ClubSlots");

    private static string ReadRawSlotsJson(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) ? value.GetRawText() : string.Empty;
}

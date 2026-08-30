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
    private const string CompilerCraneNativeContract =
        "MovieTheater_CraneGame_checkAction_then_yes_500g_then_native_CraneGame_directional_input_then_native_ItemGrabMenu_rewards";

    private static CompiledActionStep[] CompilePlayCraneGameStep(SmallModelAction action)
    {
        if (ReadIntParameter(action, "crane_fee_gold") != 500 || ReadIntParameter(action, "crane_attempts") != 3)
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step(
                "play_crane_game",
                "CraneGame:fee=500:attempts=3:policy=" + ReadParameter(action, "crane_selection_policy"),
                "money_delta=-500;native_crane_attempts=3;native_reward_menu_settled=true",
                4200)
        };
    }

    private static string[] ValidateCraneGamePlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.play_crane_game")
            return Array.Empty<string>();
        var x = ReadIntParameter(action, "target_tile_x");
        var y = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        if (!x.HasValue || !y.HasValue || !standX.HasValue || !standY.HasValue ||
            Math.Abs(x.Value - standX.Value) + Math.Abs(y.Value - standY.Value) != 1 ||
            ReadParameter(action, "target_location") != "MovieTheater" ||
            ReadParameter(action, "crane_action_raw") != "CraneGame" ||
            ReadParameter(action, "crane_action_token") != "CraneGame" ||
            ReadParameter(action, "crane_yes_response_key") != "Yes" ||
            ReadIntParameter(action, "crane_fee_gold") != 500 ||
            ReadIntParameter(action, "crane_money_before") is not >= 500 ||
            ReadIntParameter(action, "crane_empty_slots_before") is not >= 3 ||
            ReadIntParameter(action, "crane_attempts") != 3 ||
            ReadIntParameter(action, "crane_timer_ticks_per_attempt") != 900 ||
            ReadParameter(action, "crane_selection_policy") != "best_reachable_live_prize_nonlarge_stationary_then_distance;refresh_each_attempt" ||
            ReadParameter(action, "crane_exit_policy") != "finish_three_attempts_then_collect_all_native_rewards" ||
            ReadParameter(action, "native_contract") != CompilerCraneNativeContract)
            return new[] { "crane_game_typed_projection_required" };

        var reasons = new List<string>();
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("crane_game_menu_must_be_clear");
        if (ReadStateFieldString(snapshot, "player", "location_id") != "MovieTheater")
            reasons.Add("crane_game_target_location_mismatch");
        var projection = ReadStateFieldValue(snapshot, "player", "crane_game");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return reasons.Append("crane_game_projection_unavailable").ToArray();
        var row = projection.Value;
        if (ReadString(row, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(row, "gate_status") != "ready" ||
            ReadString(row, "invocation_policy") != "player_command_only" ||
            ReadBool(row, "machine_occupied") ||
            ReadInt(row, "money") != ReadIntParameter(action, "crane_money_before") ||
            ReadInt(row, "inventory_empty_slots") != ReadIntParameter(action, "crane_empty_slots_before") ||
            ReadString(row, "projection_fingerprint") != ReadParameter(action, "crane_projection_fingerprint") ||
            ReadString(row, "native_contract") != CompilerCraneNativeContract ||
            !CraneGameTileMatches(row, x.Value, y.Value))
            reasons.Add("crane_game_projection_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool CraneGameTileMatches(JsonElement projection, int x, int y) =>
        projection.TryGetProperty("interaction_tiles", out var rows) && rows.ValueKind == JsonValueKind.Array &&
        rows.EnumerateArray().Any(row => row.ValueKind == JsonValueKind.Object &&
            ReadInt(row, "tile_x") == x && ReadInt(row, "tile_y") == y &&
            ReadString(row, "action_raw") == "CraneGame");
}

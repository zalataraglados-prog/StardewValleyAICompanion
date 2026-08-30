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
    private const string CompilerDartsNativeContract =
        "IslandSouthEastCave_DartsGame_checkAction_then_yes_then_native_Darts_mouse_aim_charge_release_then_native_limited_nut_drop";

    private static CompiledActionStep[] CompilePlayDartsStep(SmallModelAction action)
    {
        if (ReadIntParameter(action, "darts_starting_points") != 301 ||
            ReadParameter(action, "darts_perfect_score_plan") != "T20,T20,T20,T20,T17,D5")
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step(
                "play_darts",
                "Darts:301:plan=T20,T20,T20,T20,T17,D5:darts=" + ReadParameter(action, "darts_starting_dart_count"),
                "darts_limited_nut_drop_delta=1;native_score=0;throws<=6;session_closed=true",
                2400)
        };
    }

    private static string[] ValidateDartsGamePlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.play_darts")
            return Array.Empty<string>();
        var x = ReadIntParameter(action, "target_tile_x");
        var y = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var droppedBefore = ReadIntParameter(action, "darts_limited_nut_dropped_before");
        if (!x.HasValue || !y.HasValue || !standX.HasValue || !standY.HasValue || !droppedBefore.HasValue ||
            Math.Abs(x.Value - standX.Value) + Math.Abs(y.Value - standY.Value) != 1 ||
            ReadParameter(action, "target_location") != "IslandSouthEastCave" ||
            ReadParameter(action, "darts_action_raw") != "DartsGame" ||
            ReadParameter(action, "darts_action_token") != "DartsGame" ||
            ReadParameter(action, "darts_yes_response_key") != "Yes" ||
            ReadParameter(action, "darts_limited_nut_key") != "Darts" ||
            ReadIntParameter(action, "darts_limited_nut_limit") != 3 ||
            droppedBefore is < 0 or >= 3 ||
            ReadIntParameter(action, "darts_limited_nut_dropped_after") != droppedBefore + 1 ||
            ReadIntParameter(action, "darts_starting_dart_count") != (droppedBefore switch { 1 => 15, 2 => 10, _ => 20 }) ||
            ReadIntParameter(action, "darts_starting_points") != 301 ||
            ReadIntParameter(action, "darts_perfect_victory_max_throws") != 6 ||
            ReadParameter(action, "darts_perfect_score_plan") != "T20,T20,T20,T20,T17,D5" ||
            ReadParameter(action, "darts_charge_release_threshold") != "0.02" ||
            ReadParameter(action, "native_contract") != CompilerDartsNativeContract)
            return new[] { "darts_game_typed_projection_required" };

        var reasons = new List<string>();
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("darts_game_menu_must_be_clear");
        if (ReadStateFieldString(snapshot, "player", "location_id") != "IslandSouthEastCave")
            reasons.Add("darts_game_target_location_mismatch");
        var projection = ReadStateFieldValue(snapshot, "player", "darts_game");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return reasons.Append("darts_game_projection_unavailable").ToArray();
        var row = projection.Value;
        if (ReadString(row, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(row, "gate_status") != "ready" ||
            ReadString(row, "invocation_policy") != "autonomous_progression" ||
            ReadBool(row, "pirate_night") != true ||
            ReadInt(row, "limited_nut_dropped_before") != droppedBefore ||
            ReadInt(row, "starting_dart_count") != ReadIntParameter(action, "darts_starting_dart_count") ||
            ReadString(row, "projection_fingerprint") != ReadParameter(action, "darts_projection_fingerprint") ||
            ReadString(row, "native_contract") != CompilerDartsNativeContract ||
            !DartsGameTileMatches(row, x.Value, y.Value))
            reasons.Add("darts_game_projection_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool DartsGameTileMatches(JsonElement projection, int x, int y) =>
        projection.TryGetProperty("interaction_tiles", out var rows) && rows.ValueKind == JsonValueKind.Array &&
        rows.EnumerateArray().Any(row => row.ValueKind == JsonValueKind.Object &&
            ReadInt(row, "tile_x") == x && ReadInt(row, "tile_y") == y &&
            ReadString(row, "action_raw") == "DartsGame");
}

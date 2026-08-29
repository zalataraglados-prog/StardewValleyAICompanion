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
    private const string FairStrengthNativeContract =
        "Event.checkAction(festival_fall16_buildings_540,player_tile_x_29)->StrengthGame.receiveLeftClick->FarmerSprite.animateOnce(168,80ms,8)->StrengthGame.afterSwingAnimation->power>=99->festivalScore+1->native_result_dialogue_and_exit";

    private static CompiledActionStep[] CompileFairStrengthGameStep(SmallModelAction action)
    {
        if (ReadIntParameter(action, "interaction_tile_x") is not { } x ||
            ReadIntParameter(action, "interaction_tile_y") is not { } y)
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step(
                "play_fair_strength_game",
                "fair_strength_game:interaction=" + x + "," + y + ";entry_fee=0;target_power>=99",
                "festival_score=+1;remaining_star_token_demand=1->0;native_max_power_result=true",
                300)
        };
    }

    private static string[] ValidateFairStrengthGamePlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.play_fair_strength_game")
            return Array.Empty<string>();

        var reasons = new List<string>();
        var interactionX = ReadIntParameter(action, "interaction_tile_x");
        var interactionY = ReadIntParameter(action, "interaction_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var scoreBefore = ReadIntParameter(action, "festival_score_before");
        var remainingDemand = ReadIntParameter(action, "remaining_star_token_demand");
        if (!interactionX.HasValue || !interactionY.HasValue || !standX.HasValue || !standY.HasValue ||
            Math.Abs(interactionX.Value - standX.Value) + Math.Abs(interactionY.Value - standY.Value) != 1 ||
            standX.Value != 29 || !scoreBefore.HasValue || remainingDemand != 1 ||
            ReadIntParameter(action, "entry_fee_money") != 0 ||
            ReadIntParameter(action, "expected_reward_star_tokens") != 1 ||
            ReadDoubleParameter(action, "perfect_power_minimum") != 99d ||
            ReadDoubleParameter(action, "power_maximum") != 100d ||
            ReadIntParameter(action, "required_player_tile_x") != 29 ||
            ReadIntParameter(action, "swing_start_frame") != 168 ||
            ReadDoubleParameter(action, "swing_interval_ms") != 80d ||
            ReadIntParameter(action, "swing_frame_count") != 8 ||
            ReadDoubleParameter(action, "perfect_result_delay_ms") != 2000d ||
            ReadParameter(action, "festival_id") != "festival_fall16" ||
            ReadParameter(action, "execution_strategy") != "native_predictive_single_click_max_power" ||
            ReadParameter(action, "native_contract") != FairStrengthNativeContract ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "fair_strength_projection_fingerprint")))
            return new[] { "fair_strength_game_typed_projection_required" };

        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("fair_strength_game_menu_must_be_clear");
        if (!string.Equals(ReadParameter(action, "target_location"),
                ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.Ordinal))
            reasons.Add("fair_strength_game_location_mismatch");

        var projection = ReadStateFieldValue(snapshot, "player", "fair_strength_game");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            ReadString(projection.Value, "projection_fingerprint") != ReadParameter(action, "fair_strength_projection_fingerprint") ||
            ReadString(projection.Value, "gate_status") != "ready" ||
            ReadString(projection.Value, "festival_id") != "festival_fall16" ||
            ReadString(projection.Value, "festival_location_id") != ReadParameter(action, "target_location") ||
            ReadInt(projection.Value, "festival_score") != scoreBefore.Value ||
            ReadInt(projection.Value, "remaining_star_token_demand") != 1 ||
            ReadString(projection.Value, "native_contract") != FairStrengthNativeContract)
            reasons.Add("fair_strength_game_projection_drifted");
        if (!FairStrengthProjectionContainsEndpoint(projection, interactionX.Value, interactionY.Value, standX.Value, standY.Value))
            reasons.Add("fair_strength_game_endpoint_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool FairStrengthProjectionContainsEndpoint(JsonElement? projection, int x, int y, int standX, int standY)
    {
        if (!projection.HasValue || !projection.Value.TryGetProperty("interaction_tiles", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
            return false;
        return rows.EnumerateArray().Any(row =>
            ReadInt(row, "tile_x", -1) == x && ReadInt(row, "tile_y", -1) == y &&
            ReadInt(row, "tile_index", -1) == 540 &&
            ReadInt(row, "stand_tile_x", -1) == standX && ReadInt(row, "stand_tile_y", -1) == standY &&
            standX == 29);
    }
}

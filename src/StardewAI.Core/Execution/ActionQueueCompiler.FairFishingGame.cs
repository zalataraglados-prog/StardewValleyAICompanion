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
    private const string FairFishingNativeContract =
        "Event.checkAction(festival_fall16_buildings_503_504)->DialogueBox(fishingGame:Play).receiveLeftClick->Event.answerDialogue(fishingGame,0)->Money-50->globalFadeToBlack(FishingGame.startMe)->native_100000ms_FishingGame_input_session->perfection_score_reward->festivalScore";

    private static CompiledActionStep[] CompileFairFishingGameStep(SmallModelAction action)
    {
        if (ReadIntParameter(action, "interaction_tile_x") is not { } x ||
            ReadIntParameter(action, "interaction_tile_y") is not { } y)
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step(
                "play_fair_fishing_game",
                "fair_fishing_game:interaction=" + x + "," + y + ";entry_fee=50;duration_ms=100000",
                "money=-50;festival_score=+native_fishing_game_reward;stochastic_native_result_formula=verified",
                7200)
        };
    }

    private static string[] ValidateFairFishingGamePlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.play_fair_fishing_game")
            return Array.Empty<string>();

        var reasons = new List<string>();
        var interactionX = ReadIntParameter(action, "interaction_tile_x");
        var interactionY = ReadIntParameter(action, "interaction_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var moneyBefore = ReadIntParameter(action, "money_before");
        var scoreBefore = ReadIntParameter(action, "festival_score_before");
        var remainingDemand = ReadIntParameter(action, "remaining_star_token_demand");
        if (!interactionX.HasValue || !interactionY.HasValue || !standX.HasValue || !standY.HasValue ||
            Math.Abs(interactionX.Value - standX.Value) + Math.Abs(interactionY.Value - standY.Value) != 1 ||
            !moneyBefore.HasValue || moneyBefore.Value < 50 || !scoreBefore.HasValue ||
            !remainingDemand.HasValue || remainingDemand.Value <= 0 ||
            ReadIntParameter(action, "entry_fee_money") != 50 ||
            ReadIntParameter(action, "game_duration_ms") != 100000 ||
            ReadIntParameter(action, "results_duration_ms") != 11100 ||
            ReadParameter(action, "festival_id") != "festival_fall16" ||
            ReadParameter(action, "dialogue_key") != "fishingGame" ||
            ReadParameter(action, "play_response_key") != "Play" ||
            ReadParameter(action, "execution_strategy") != "native_predictive_legal_input" ||
            ReadParameter(action, "native_contract") != FairFishingNativeContract ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "fair_fishing_projection_fingerprint")))
            return new[] { "fair_fishing_game_typed_projection_required" };

        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("fair_fishing_game_menu_must_be_clear");
        if (!string.Equals(ReadParameter(action, "target_location"),
                ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.Ordinal))
            reasons.Add("fair_fishing_game_location_mismatch");

        var projection = ReadStateFieldValue(snapshot, "player", "fair_fishing_game");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            ReadString(projection.Value, "projection_fingerprint") != ReadParameter(action, "fair_fishing_projection_fingerprint") ||
            ReadString(projection.Value, "gate_status") != "ready" ||
            ReadString(projection.Value, "festival_id") != "festival_fall16" ||
            ReadString(projection.Value, "festival_location_id") != ReadParameter(action, "target_location") ||
            ReadInt(projection.Value, "player_money") != moneyBefore.Value ||
            ReadInt(projection.Value, "festival_score") != scoreBefore.Value ||
            ReadInt(projection.Value, "remaining_star_token_demand") != remainingDemand.Value ||
            ReadString(projection.Value, "native_contract") != FairFishingNativeContract)
            reasons.Add("fair_fishing_game_projection_drifted");
        if (!FairFishingProjectionContainsInteractionTile(projection, interactionX.Value, interactionY.Value))
            reasons.Add("fair_fishing_game_interaction_tile_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool FairFishingProjectionContainsInteractionTile(JsonElement? projection, int x, int y)
    {
        if (!projection.HasValue || !projection.Value.TryGetProperty("interaction_tiles", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
            return false;
        return rows.EnumerateArray().Any(row =>
            ReadInt(row, "tile_x", -1) == x && ReadInt(row, "tile_y", -1) == y &&
            ReadInt(row, "tile_index", -1) is 503 or 504);
    }
}

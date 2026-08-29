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
    private const string FairWheelNativeContract =
        "Event.checkAction(festival_fall16_buildings_308_309)->DialogueBox(wheelBet:Green).receiveLeftClick->Event.answerDialogue(wheelBet,1)->NumberSelectionMenu(wager_1_to_festivalScore).receiveLeftClick(ok)->Event.betStarTokens->WheelSpinGame(1000ms,green)->native_random_spin->festivalScore+(win?wager:-wager)->native_result_text_and_exit";

    private static CompiledActionStep[] CompileFairWheelSpinStep(SmallModelAction action)
    {
        if (ReadIntParameter(action, "interaction_tile_x") is not { } x ||
            ReadIntParameter(action, "interaction_tile_y") is not { } y ||
            ReadIntParameter(action, "wager_star_tokens") is not { } wager)
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step(
                "spin_fair_wheel",
                "fair_wheel:interaction=" + x + "," + y + ";color=green;wager=" + wager,
                "festival_score=stochastic_plus_or_minus_" + wager + ";native_result_and_exit=true",
                900)
        };
    }

    private static string[] ValidateFairWheelSpinPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.spin_fair_wheel")
            return Array.Empty<string>();

        var reasons = new List<string>();
        var interactionX = ReadIntParameter(action, "interaction_tile_x");
        var interactionY = ReadIntParameter(action, "interaction_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var scoreBefore = ReadIntParameter(action, "festival_score_before");
        var remainingDemand = ReadIntParameter(action, "remaining_star_token_demand");
        var wager = ReadIntParameter(action, "wager_star_tokens");
        if (!interactionX.HasValue || !interactionY.HasValue || !standX.HasValue || !standY.HasValue ||
            Math.Abs(interactionX.Value - standX.Value) + Math.Abs(interactionY.Value - standY.Value) != 1 ||
            !scoreBefore.HasValue || scoreBefore < 2 || !remainingDemand.HasValue || remainingDemand < 2 ||
            !wager.HasValue || wager < 1 ||
            wager != Math.Min(remainingDemand.Value, scoreBefore.Value * 7 / 15) ||
            ReadParameter(action, "festival_id") != "festival_fall16" ||
            ReadParameter(action, "selected_color") != "green" ||
            ReadIntParameter(action, "base_green_wins") != 22 ||
            ReadIntParameter(action, "base_orange_wins") != 8 ||
            ReadIntParameter(action, "base_outcome_count") != 30 ||
            ReadIntParameter(action, "prestart_duration_ms") != 1000 ||
            ReadIntParameter(action, "result_duration_ms") != 2500 ||
            ReadParameter(action, "dialogue_key") != "wheelBet" ||
            ReadParameter(action, "response_key") != "Green" ||
            ReadParameter(action, "wager_policy") != "green_zero_luck_kelly_7_of_15_capped_by_remaining_stardrop_demand" ||
            ReadParameter(action, "native_contract") != FairWheelNativeContract ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "fair_wheel_projection_fingerprint")))
            return new[] { "fair_wheel_typed_projection_required" };

        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("fair_wheel_menu_must_be_clear");
        if (!string.Equals(ReadParameter(action, "target_location"),
                ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.Ordinal))
            reasons.Add("fair_wheel_location_mismatch");

        var projection = ReadStateFieldValue(snapshot, "player", "fair_wheel_spin");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            ReadString(projection.Value, "projection_fingerprint") != ReadParameter(action, "fair_wheel_projection_fingerprint") ||
            ReadString(projection.Value, "gate_status") != "ready" ||
            ReadString(projection.Value, "festival_id") != "festival_fall16" ||
            ReadString(projection.Value, "festival_location_id") != ReadParameter(action, "target_location") ||
            ReadInt(projection.Value, "festival_score") != scoreBefore.Value ||
            ReadInt(projection.Value, "remaining_star_token_demand") != remainingDemand.Value ||
            ReadInt(projection.Value, "wager_star_tokens") != wager.Value ||
            ReadString(projection.Value, "native_contract") != FairWheelNativeContract)
            reasons.Add("fair_wheel_projection_drifted");
        if (!FairWheelProjectionContainsEndpoint(projection, interactionX.Value, interactionY.Value, standX.Value, standY.Value))
            reasons.Add("fair_wheel_endpoint_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool FairWheelProjectionContainsEndpoint(JsonElement? projection, int x, int y, int standX, int standY)
    {
        if (!projection.HasValue || !projection.Value.TryGetProperty("interaction_tiles", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
            return false;
        return rows.EnumerateArray().Any(row =>
            ReadInt(row, "tile_x", -1) == x && ReadInt(row, "tile_y", -1) == y &&
            ReadInt(row, "tile_index", -1) is 308 or 309 &&
            ReadInt(row, "stand_tile_x", -1) == standX && ReadInt(row, "stand_tile_y", -1) == standY);
    }
}

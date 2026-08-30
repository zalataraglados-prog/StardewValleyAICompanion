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
    private static string[] ValidatePrairieKingPlan(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.play_prairie_king")
            return Array.Empty<string>();
        var x = ReadIntParameter(action, "target_tile_x");
        var y = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var completedBefore = ReadLongParameterExact(action, "prairie_king_completed_before");
        var completedWithoutDyingBefore = ReadLongParameterExact(action, "prairie_king_completed_without_dying_before");
        if (!x.HasValue || !y.HasValue || !standX.HasValue || !standY.HasValue ||
            !completedBefore.HasValue || completedWithoutDyingBefore != 0 ||
            Math.Abs(x.Value - standX.Value) + Math.Abs(y.Value - standY.Value) != 1 ||
            ReadParameter(action, "target_location") != "Saloon" ||
            ReadParameter(action, "prairie_king_action_raw") != "Arcade_Prairie" ||
            ReadParameter(action, "prairie_king_action_token") != "Arcade_Prairie" ||
            ReadParameter(action, "prairie_king_completion_goal") != "complete_without_dying" ||
            ReadIntParameter(action, "prairie_king_equivalent_duration_ticks") != 108000 ||
            ReadIntParameter(action, "prairie_king_equivalent_acceleration") != 60 ||
            ReadParameter(action, "prairie_king_equivalent_contract") !=
                "Saloon_Arcade_Prairie_checkAction_optional_CowboyGame_NewGame_then_timed_equivalent_then_AbigailGame_usePowerup_minus3_native_phase1_settlement" ||
            ReadParameter(action, "minigame_id") != "PrairieKing")
            return new[] { "prairie_king_typed_projection_required" };

        var projection = ReadStateFieldValue(snapshot, "player", "prairie_king");
        var errors = new List<string>();
        if (!projection.HasValue || projection.Value.ValueKind != System.Text.Json.JsonValueKind.Object)
            return new[] { "prairie_king_projection_required" };
        var row = projection.Value;
        if (ReadString(row, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(row, "invocation_policy") != "autonomous_timed_equivalent" ||
            ReadString(row, "native_proxy_policy") != "post_core_training_player_command_only")
            errors.Add("prairie_king_policy_projection_invalid");
        if (ActionSeesActiveMenuOpen(action, snapshot))
            errors.Add("prairie_king_menu_must_be_clear");
        if (ReadStateFieldString(snapshot, "player", "location_id") != "Saloon")
            errors.Add("prairie_king_target_location_mismatch");
        if (ReadString(row, "gate_status") != "ready" ||
            ReadInt64(row, "completed_before") != completedBefore ||
            ReadInt64(row, "completed_without_dying_before") != 0 ||
            ReadString(row, "projection_fingerprint") != ReadParameter(action, "prairie_king_projection_fingerprint") ||
            ReadString(row, "dialogue_key") != ReadParameter(action, "prairie_king_dialogue_key") ||
            ReadString(row, "dialogue_response_key") != ReadParameter(action, "prairie_king_dialogue_response_key") ||
            ReadString(row, "equivalent_contract") != ReadParameter(action, "prairie_king_equivalent_contract") ||
            !PrairieKingTileMatches(row, x.Value, y.Value))
            errors.Add("prairie_king_projection_drifted");
        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static CompiledActionStep[] CompilePlayPrairieKingStep(SmallModelAction action)
    {
        var duration = ReadParameter(action, "prairie_king_equivalent_duration_ticks");
        if (!int.TryParse(duration, NumberStyles.Integer, CultureInfo.InvariantCulture, out var durationTicks) ||
            durationTicks != 108000 ||
            ReadParameter(action, "prairie_king_completion_goal") != "complete_without_dying")
            return Array.Empty<CompiledActionStep>();

        return new[]
        {
            Step(
                "play_prairie_king",
                "PrairieKing:timed_equivalent:complete_without_dying",
                "native_AbigailGame_phase1_completion_stats_mail_and_achievements_observed",
                durationTicks)
        };
    }

    private static bool PrairieKingTileMatches(JsonElement projection, int x, int y) =>
        projection.TryGetProperty("interaction_tiles", out var rows) && rows.ValueKind == JsonValueKind.Array &&
        rows.EnumerateArray().Any(row => row.ValueKind == JsonValueKind.Object &&
            ReadInt(row, "tile_x") == x && ReadInt(row, "tile_y") == y &&
            ReadString(row, "action_raw") == "Arcade_Prairie");
}

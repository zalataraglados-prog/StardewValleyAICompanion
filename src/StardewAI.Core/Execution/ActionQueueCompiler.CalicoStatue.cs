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
    private const string CalicoStatueCompilerNativeContract =
        "MineShaft_Buildings_284_checkAction_then_recentlyActivatedCalicoStatue_event_then_master_seeded_effect_rating_and_native_side_effect_receipt";

    private static readonly string[] CalicoStatueBoundParameterNames =
    {
        "calico_statue_projection_fingerprint", "calico_statue_effect_key", "calico_statue_strategy_polarity",
        "calico_statue_exact_effect", "calico_statue_calico_egg_reward", "calico_statue_current_effects_csv",
        "calico_statue_expected_effects_after_csv", "calico_statue_total_activated_before",
        "calico_statue_next_activation_number", "calico_statue_rating_before", "calico_statue_expected_rating_after",
        "calico_statue_average_daily_luck", "calico_statue_days_played", "calico_statue_unique_game_id_half",
        "calico_statue_use_legacy_random", "calico_statue_mine_level", "calico_statue_festival_day",
        "calico_statue_tile_index_before", "calico_statue_tile_index_after", "calico_statue_eggs_before",
        "calico_statue_health_before", "calico_statue_max_health", "calico_statue_stamina_before",
        "calico_statue_max_stamina", "target_location", "target_tile_x", "target_tile_y", "stand_tile_x",
        "stand_tile_y", "interaction_kind", "expected_action_type", "native_contract", "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildCalicoStatueParameters(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var parameters = action.Parameters
            .Where(parameter => !CalicoStatueBoundParameterNames.Contains(parameter.Name, StringComparer.Ordinal))
            .ToList();
        var projection = ReadStateFieldValue(snapshot, "mining", "calico_statue");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return parameters.ToArray();
        var target = ResolveCalicoStatueCompilerTarget(projection.Value, snapshot);
        if (target is null)
            return parameters.ToArray();
        var effect = projection.Value.GetProperty("projected_effect");
        parameters.AddRange(new[]
        {
            Parameter("calico_statue_projection_fingerprint", ReadString(projection.Value, "projection_fingerprint")),
            Parameter("calico_statue_effect_key", ReadString(effect, "effect_key")),
            Parameter("calico_statue_strategy_polarity", ReadString(effect, "strategy_polarity")),
            Parameter("calico_statue_exact_effect", ReadString(effect, "exact_effect")),
            Parameter("calico_statue_calico_egg_reward", ReadInt(effect, "calico_egg_reward").ToString()),
            Parameter("calico_statue_current_effects_csv", ReadString(projection.Value, "current_effects_csv")),
            Parameter("calico_statue_expected_effects_after_csv", ReadString(projection.Value, "expected_effects_after_csv")),
            Parameter("calico_statue_total_activated_before", ReadInt(projection.Value, "total_activated_today_before").ToString()),
            Parameter("calico_statue_next_activation_number", ReadInt(projection.Value, "next_activation_number").ToString()),
            Parameter("calico_statue_rating_before", ReadInt(projection.Value, "rating_before").ToString()),
            Parameter("calico_statue_expected_rating_after", ReadInt(projection.Value, "expected_rating_after").ToString()),
            Parameter("calico_statue_average_daily_luck", ReadDouble(projection.Value, "average_daily_luck").ToString("R", CultureInfo.InvariantCulture)),
            Parameter("calico_statue_days_played", ReadInt(projection.Value, "days_played").ToString()),
            Parameter("calico_statue_unique_game_id_half", ReadString(projection.Value, "unique_game_id_half")),
            Parameter("calico_statue_use_legacy_random", (ReadBool(projection.Value, "use_legacy_random") == true).ToString().ToLowerInvariant()),
            Parameter("calico_statue_mine_level", ReadInt(projection.Value, "mine_level").ToString()),
            Parameter("calico_statue_festival_day", ReadInt(projection.Value, "desert_festival_day").ToString()),
            Parameter("calico_statue_tile_index_before", ReadInt(projection.Value, "target_tile_index_before").ToString()),
            Parameter("calico_statue_tile_index_after", ReadInt(projection.Value, "target_tile_index_after").ToString()),
            Parameter("calico_statue_eggs_before", ReadInt(projection.Value, "calico_eggs_before").ToString()),
            Parameter("calico_statue_health_before", ReadInt(projection.Value, "health_before").ToString()),
            Parameter("calico_statue_max_health", ReadInt(projection.Value, "max_health").ToString()),
            Parameter("calico_statue_stamina_before", ReadDouble(projection.Value, "stamina_before").ToString("R", CultureInfo.InvariantCulture)),
            Parameter("calico_statue_max_stamina", ReadDouble(projection.Value, "max_stamina").ToString("R", CultureInfo.InvariantCulture)),
            Parameter("target_location", ReadString(projection.Value, "location_id")),
            Parameter("target_tile_x", target.TargetX.ToString()),
            Parameter("target_tile_y", target.TargetY.ToString()),
            Parameter("stand_tile_x", target.StandX.ToString()),
            Parameter("stand_tile_y", target.StandY.ToString()),
            Parameter("interaction_kind", ReadString(projection.Value, "interaction_kind")),
            Parameter("expected_action_type", ReadString(projection.Value, "expected_action_type")),
            Parameter("native_contract", ReadString(projection.Value, "native_contract")),
            Parameter("max_movement_tiles", "512")
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileCalicoStatueStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = BoundCalicoStatueAction(action, snapshot);
        var effectId = ReadIntParameter(bound, "calico_statue_accepted_effect_id");
        var x = ReadIntParameter(bound, "target_tile_x");
        var y = ReadIntParameter(bound, "target_tile_y");
        if (!effectId.HasValue || !x.HasValue || !y.HasValue)
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step(
                "activate_calico_statue",
                ReadParameter(bound, "target_location") + "(" + x.Value + "," + y.Value + "):effect=" + effectId.Value,
                "team.calico_egg_skull_cavern_rating+=1;effect_id=" + effectId.Value + ";native_receipt_verified=true",
                900)
        };
    }

    private static string[] ValidateCalicoStatuePlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId is not ("mining.activate_calico_statue" or "executor.activate_calico_statue"))
            return Array.Empty<string>();
        var acceptedEffectId = ReadIntParameter(action, "calico_statue_accepted_effect_id");
        if (!acceptedEffectId.HasValue || acceptedEffectId.Value is < 0 or > 17)
            return new[] { "calico_statue_accepted_effect_id_0_17_required_from_small_model" };
        var projection = ReadStateFieldValue(snapshot, "mining", "calico_statue");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return new[] { "calico_statue_projection_unavailable" };
        var reasons = new List<string>();
        if (!string.Equals(ReadString(projection.Value, "gate_status"), "ready", StringComparison.Ordinal))
            reasons.Add("calico_statue_not_ready:" + ReadString(projection.Value, "gate_status"));
        if (ReadInt(projection.Value, "projected_effect_id") != acceptedEffectId.Value)
            reasons.Add("calico_statue_projected_effect_changed_replan_required");
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("calico_statue_menu_must_be_clear");
        var bound = BoundCalicoStatueAction(action, snapshot);
        var target = ResolveCalicoStatueCompilerTarget(projection.Value, snapshot);
        var effect = projection.Value.GetProperty("projected_effect");
        if (target is null ||
            ReadIntParameter(bound, "target_tile_x") != target.TargetX ||
            ReadIntParameter(bound, "target_tile_y") != target.TargetY ||
            ReadIntParameter(bound, "stand_tile_x") != target.StandX ||
            ReadIntParameter(bound, "stand_tile_y") != target.StandY ||
            ReadParameter(bound, "target_location") != ReadString(projection.Value, "location_id") ||
            ReadParameter(bound, "calico_statue_projection_fingerprint") != ReadString(projection.Value, "projection_fingerprint") ||
            ReadParameter(bound, "calico_statue_effect_key") != ReadString(effect, "effect_key") ||
            ReadParameter(bound, "calico_statue_expected_effects_after_csv") != ReadString(projection.Value, "expected_effects_after_csv") ||
            ReadIntParameter(bound, "calico_statue_total_activated_before") != ReadInt(projection.Value, "total_activated_today_before") ||
            ReadIntParameter(bound, "calico_statue_rating_before") != ReadInt(projection.Value, "rating_before") ||
            ReadIntParameter(bound, "calico_statue_mine_level") != ReadInt(projection.Value, "mine_level") ||
            ReadIntParameter(bound, "calico_statue_tile_index_before") != 284 ||
            ReadIntParameter(bound, "calico_statue_tile_index_after") != 285 ||
            ReadParameter(bound, "native_contract") != CalicoStatueCompilerNativeContract)
        {
            reasons.Add("calico_statue_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundCalicoStatueAction(SmallModelAction action, SnapshotEnvelope snapshot) => new()
    {
        ActionId = action.ActionId,
        OptionId = action.OptionId,
        Rationale = action.Rationale,
        Parameters = BuildCalicoStatueParameters(action, snapshot)
    };

    private static CalicoStatueCompilerTarget? ResolveCalicoStatueCompilerTarget(
        JsonElement projection,
        SnapshotEnvelope snapshot)
    {
        if (!projection.TryGetProperty("stand_tiles", out var stands) || stands.ValueKind != JsonValueKind.Array)
            return null;
        var targetX = ReadInt(projection, "target_tile_x");
        var targetY = ReadInt(projection, "target_tile_y");
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        return stands.EnumerateArray()
            .Where(stand => stand.ValueKind == JsonValueKind.Object && ReadBool(stand, "available") == true)
            .Select(stand => new CalicoStatueCompilerTarget(
                targetX, targetY, ReadInt(stand, "tile_x"), ReadInt(stand, "tile_y"),
                Math.Abs(playerX - ReadInt(stand, "tile_x")) + Math.Abs(playerY - ReadInt(stand, "tile_y"))))
            .OrderBy(row => row.Distance)
            .ThenBy(row => row.StandY)
            .ThenBy(row => row.StandX)
            .FirstOrDefault();
    }

    private sealed record CalicoStatueCompilerTarget(
        int TargetX,
        int TargetY,
        int StandX,
        int StandY,
        int Distance);
}

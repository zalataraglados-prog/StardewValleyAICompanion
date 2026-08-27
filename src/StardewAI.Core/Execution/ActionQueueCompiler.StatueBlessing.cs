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
    private static readonly string[] StatueBlessingBoundParameterNames =
    {
        "statue_blessing_id", "statue_blessing_buff_id", "statue_blessing_effect_kind",
        "statue_blessing_exact_effect", "statue_blessing_days_played",
        "statue_blessing_random_upper_bound_exclusive", "target_location", "target_tile_x",
        "target_tile_y", "stand_tile_x", "stand_tile_y", "target_runtime_type",
        "qualified_item_id", "interaction_kind", "expected_action_type", "native_contract",
        "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildStatueBlessingParameters(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var parameters = action.Parameters
            .Where(parameter => !StatueBlessingBoundParameterNames.Contains(parameter.Name, StringComparer.Ordinal))
            .ToList();
        var projection = ReadStateFieldValue(snapshot, "current_location", "statue_blessing");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
        {
            return parameters.ToArray();
        }
        var target = SelectStatueBlessingCompilerTarget(projection.Value, snapshot);
        if (target is null || !projection.Value.TryGetProperty("blessing", out var blessing) || blessing.ValueKind != JsonValueKind.Object)
        {
            return parameters.ToArray();
        }
        parameters.AddRange(new[]
        {
            Parameter("statue_blessing_id", ReadInt(projection.Value, "blessing_id").ToString()),
            Parameter("statue_blessing_buff_id", ReadString(projection.Value, "buff_id")),
            Parameter("statue_blessing_effect_kind", ReadString(blessing, "kind")),
            Parameter("statue_blessing_exact_effect", ReadString(blessing, "exact_effect")),
            Parameter("statue_blessing_days_played", ReadInt(projection.Value, "days_played").ToString()),
            Parameter("statue_blessing_random_upper_bound_exclusive", ReadInt(projection.Value, "random_upper_bound_exclusive").ToString()),
            Parameter("target_location", ReadString(projection.Value, "location_id")),
            Parameter("target_tile_x", target.TargetX.ToString()),
            Parameter("target_tile_y", target.TargetY.ToString()),
            Parameter("stand_tile_x", target.StandX.ToString()),
            Parameter("stand_tile_y", target.StandY.ToString()),
            Parameter("target_runtime_type", target.RuntimeType),
            Parameter("qualified_item_id", ReadString(projection.Value, "qualified_item_id")),
            Parameter("interaction_kind", ReadString(projection.Value, "interaction_kind")),
            Parameter("expected_action_type", ReadString(projection.Value, "expected_action_type")),
            Parameter("native_contract", ReadString(projection.Value, "native_contract")),
            Parameter("max_movement_tiles", "512")
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileStatueBlessingStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = BoundStatueBlessingAction(action, snapshot);
        var id = ReadIntParameter(bound, "statue_blessing_id");
        var x = ReadIntParameter(bound, "target_tile_x");
        var y = ReadIntParameter(bound, "target_tile_y");
        if (!id.HasValue || !x.HasValue || !y.HasValue)
        {
            return Array.Empty<CompiledActionStep>();
        }
        return new[]
        {
            Step(
                "claim_statue_blessing",
                ReadParameter(bound, "target_location") + "(" + x.Value + "," + y.Value + "):blessing=" + id.Value,
                "player.has_buff[statue_of_blessings_" + id.Value + "]=true;player.has_been_blessed_today=true;valid_until_day_end=true;fresh_snapshot_replan_required=true",
                600)
        };
    }

    private static string[] ValidateStatueBlessingPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "rewards.claim_statue_blessing")
        {
            return Array.Empty<string>();
        }
        var reasons = new List<string>();
        var projection = ReadStateFieldValue(snapshot, "current_location", "statue_blessing");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
        {
            return new[] { "statue_blessing_projection_unavailable" };
        }
        if (!string.Equals(ReadString(projection.Value, "status"), "ready", StringComparison.Ordinal))
        {
            reasons.Add("statue_blessing_not_ready_by_transparent_state:" + ReadString(projection.Value, "status"));
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("statue_blessing_menu_must_be_clear");
        }
        var bound = BoundStatueBlessingAction(action, snapshot);
        var target = SelectStatueBlessingCompilerTarget(projection.Value, snapshot);
        if (target is null ||
            ReadIntParameter(bound, "statue_blessing_id") != ReadInt(projection.Value, "blessing_id") ||
            ReadIntParameter(bound, "statue_blessing_days_played") != ReadInt(projection.Value, "days_played") ||
            ReadIntParameter(bound, "statue_blessing_random_upper_bound_exclusive") != ReadInt(projection.Value, "random_upper_bound_exclusive") ||
            ReadIntParameter(bound, "target_tile_x") != target.TargetX ||
            ReadIntParameter(bound, "target_tile_y") != target.TargetY ||
            ReadIntParameter(bound, "stand_tile_x") != target.StandX ||
            ReadIntParameter(bound, "stand_tile_y") != target.StandY ||
            !string.Equals(ReadParameter(bound, "statue_blessing_buff_id"), ReadString(projection.Value, "buff_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "target_location"), ReadString(projection.Value, "location_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "target_runtime_type"), target.RuntimeType, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "qualified_item_id"), ReadString(projection.Value, "qualified_item_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "native_contract"), ReadString(projection.Value, "native_contract"), StringComparison.Ordinal))
        {
            reasons.Add("statue_blessing_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundStatueBlessingAction(SmallModelAction action, SnapshotEnvelope snapshot) =>
        new()
        {
            ActionId = action.ActionId,
            OptionId = action.OptionId,
            Rationale = action.Rationale,
            Parameters = BuildStatueBlessingParameters(action, snapshot)
        };

    private static StatueBlessingCompilerTarget? SelectStatueBlessingCompilerTarget(JsonElement projection, SnapshotEnvelope snapshot)
    {
        if (!projection.TryGetProperty("statues", out var statues) || statues.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        return statues.EnumerateArray()
            .SelectMany(statue => statue.TryGetProperty("stand_tiles", out var stands) && stands.ValueKind == JsonValueKind.Array
                ? stands.EnumerateArray()
                    .Where(stand => ReadBool(stand, "available") == true)
                    .Select(stand => new StatueBlessingCompilerTarget(
                        ReadInt(statue, "tile_x"), ReadInt(statue, "tile_y"),
                        ReadInt(stand, "tile_x"), ReadInt(stand, "tile_y"),
                        ReadString(statue, "target_runtime_type"),
                        Math.Abs(playerX - ReadInt(stand, "tile_x")) + Math.Abs(playerY - ReadInt(stand, "tile_y"))))
                : Enumerable.Empty<StatueBlessingCompilerTarget>())
            .OrderBy(row => row.Distance)
            .ThenBy(row => row.TargetY)
            .ThenBy(row => row.TargetX)
            .ThenBy(row => row.StandY)
            .ThenBy(row => row.StandX)
            .FirstOrDefault();
    }

    private sealed record StatueBlessingCompilerTarget(int TargetX, int TargetY, int StandX, int StandY, string RuntimeType, int Distance);
}

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
    private static readonly string[] DwarfKingStatueBoundParameterNames =
    {
        "dwarf_statue_power_source", "dwarf_statue_menu_index", "dwarf_statue_buff_id",
        "dwarf_statue_display_text", "dwarf_statue_effect_kind", "dwarf_statue_exact_effect",
        "dwarf_statue_offered_power_ids_csv", "dwarf_statue_days_played",
        "target_location", "target_tile_x", "target_tile_y", "stand_tile_x", "stand_tile_y",
        "target_runtime_type", "qualified_item_id", "expected_menu_type_after", "interaction_kind",
        "expected_action_type", "native_contract", "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildDwarfKingStatueParameters(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var requestedPowerId = ReadIntParameter(action, "dwarf_statue_power_id");
        var parameters = action.Parameters
            .Where(parameter => !DwarfKingStatueBoundParameterNames.Contains(parameter.Name, StringComparer.Ordinal))
            .ToList();
        var projection = ReadStateFieldValue(snapshot, "current_location", "dwarf_king_statue_power");
        if (!requestedPowerId.HasValue || !projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
        {
            return parameters.ToArray();
        }
        var offer = DwarfKingOffer(projection.Value, requestedPowerId.Value);
        var target = DwarfKingTarget(projection.Value, snapshot);
        if (!offer.HasValue || target is null)
        {
            return parameters.ToArray();
        }
        var effect = offer.Value.GetProperty("effect");
        parameters.AddRange(new[]
        {
            Parameter("dwarf_statue_power_source", "small_model_exact_offered_choice"),
            Parameter("dwarf_statue_menu_index", ReadInt(offer.Value, "menu_index").ToString()),
            Parameter("dwarf_statue_buff_id", ReadString(offer.Value, "buff_id")),
            Parameter("dwarf_statue_display_text", ReadString(offer.Value, "display_text")),
            Parameter("dwarf_statue_effect_kind", ReadString(effect, "kind")),
            Parameter("dwarf_statue_exact_effect", ReadString(effect, "exact_effect")),
            Parameter("dwarf_statue_offered_power_ids_csv", ReadString(projection.Value, "offered_power_ids_csv")),
            Parameter("dwarf_statue_days_played", ReadInt(projection.Value, "days_played").ToString()),
            Parameter("target_location", ReadString(projection.Value, "location_id")),
            Parameter("target_tile_x", target.TargetX.ToString()),
            Parameter("target_tile_y", target.TargetY.ToString()),
            Parameter("stand_tile_x", target.StandX.ToString()),
            Parameter("stand_tile_y", target.StandY.ToString()),
            Parameter("target_runtime_type", target.RuntimeType),
            Parameter("qualified_item_id", ReadString(projection.Value, "qualified_item_id")),
            Parameter("expected_menu_type_after", ReadString(projection.Value, "expected_menu_type")),
            Parameter("interaction_kind", "location_object"),
            Parameter("expected_action_type", "StatueOfTheDwarfKing"),
            Parameter("native_contract", ReadString(projection.Value, "native_contract")),
            Parameter("max_movement_tiles", "512")
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileDwarfKingStatuePowerStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = BoundDwarfKingStatueAction(action, snapshot);
        var powerId = ReadIntParameter(bound, "dwarf_statue_power_id");
        var x = ReadIntParameter(bound, "target_tile_x");
        var y = ReadIntParameter(bound, "target_tile_y");
        if (!powerId.HasValue || !x.HasValue || !y.HasValue)
        {
            return Array.Empty<CompiledActionStep>();
        }
        return new[]
        {
            Step(
                "choose_dwarf_statue_power",
                ReadParameter(bound, "target_location") + "(" + x.Value + "," + y.Value + "):power=" + powerId.Value,
                "player.has_buff[dwarfStatue_" + powerId.Value + "]=true;valid_until_day_end=true;fresh_snapshot_replan_required=true",
                720)
        };
    }

    private static string[] ValidateDwarfKingStatuePlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "mining.choose_dwarf_statue_power")
        {
            return Array.Empty<string>();
        }
        var reasons = new List<string>();
        var requestedPowerId = ReadIntParameter(action, "dwarf_statue_power_id");
        if (!requestedPowerId.HasValue || requestedPowerId.Value is < 0 or > 4)
        {
            reasons.Add("dwarf_statue_power_id_0_4_required_from_small_model");
            return reasons.ToArray();
        }
        var projection = ReadStateFieldValue(snapshot, "current_location", "dwarf_king_statue_power");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
        {
            return new[] { "dwarf_king_statue_projection_unavailable" };
        }
        if (!string.Equals(ReadString(projection.Value, "status"), "ready", StringComparison.Ordinal))
        {
            reasons.Add("dwarf_king_statue_not_ready_by_transparent_state:" + ReadString(projection.Value, "status"));
        }
        if (!DwarfKingOffer(projection.Value, requestedPowerId.Value).HasValue)
        {
            reasons.Add("dwarf_statue_power_id_not_in_exact_daily_offers");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("dwarf_king_statue_menu_must_be_clear");
        }
        var bound = BoundDwarfKingStatueAction(action, snapshot);
        var target = DwarfKingTarget(projection.Value, snapshot);
        if (target is null ||
            ReadIntParameter(bound, "target_tile_x") != target.TargetX ||
            ReadIntParameter(bound, "target_tile_y") != target.TargetY ||
            ReadIntParameter(bound, "stand_tile_x") != target.StandX ||
            ReadIntParameter(bound, "stand_tile_y") != target.StandY ||
            !string.Equals(ReadParameter(bound, "target_location"), ReadString(projection.Value, "location_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "target_runtime_type"), target.RuntimeType, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "qualified_item_id"), ReadString(projection.Value, "qualified_item_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "native_contract"), ReadString(projection.Value, "native_contract"), StringComparison.Ordinal) ||
            ReadIntParameter(bound, "dwarf_statue_days_played") != ReadInt(projection.Value, "days_played"))
        {
            reasons.Add("dwarf_king_statue_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundDwarfKingStatueAction(SmallModelAction action, SnapshotEnvelope snapshot) =>
        new()
        {
            ActionId = action.ActionId,
            OptionId = action.OptionId,
            Rationale = action.Rationale,
            Parameters = BuildDwarfKingStatueParameters(action, snapshot)
        };

    private static JsonElement? DwarfKingOffer(JsonElement projection, int powerId)
    {
        if (!projection.TryGetProperty("offers", out var offers) || offers.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        return offers.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object && ReadInt(row, "power_id") == powerId)
            .Select(row => (JsonElement?)row)
            .FirstOrDefault();
    }

    private static DwarfKingCompilerTarget? DwarfKingTarget(JsonElement projection, SnapshotEnvelope snapshot)
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
                    .Select(stand => new DwarfKingCompilerTarget(
                        ReadInt(statue, "tile_x"), ReadInt(statue, "tile_y"),
                        ReadInt(stand, "tile_x"), ReadInt(stand, "tile_y"),
                        ReadString(statue, "target_runtime_type"),
                        Math.Abs(playerX - ReadInt(stand, "tile_x")) + Math.Abs(playerY - ReadInt(stand, "tile_y"))))
                : Enumerable.Empty<DwarfKingCompilerTarget>())
            .OrderBy(row => row.Distance)
            .ThenBy(row => row.TargetY)
            .ThenBy(row => row.TargetX)
            .ThenBy(row => row.StandY)
            .ThenBy(row => row.StandX)
            .FirstOrDefault();
    }

    private sealed record DwarfKingCompilerTarget(
        int TargetX,
        int TargetY,
        int StandX,
        int StandY,
        string RuntimeType,
        int Distance);
}

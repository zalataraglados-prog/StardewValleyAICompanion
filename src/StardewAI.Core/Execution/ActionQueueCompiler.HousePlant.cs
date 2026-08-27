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
    private static readonly string[] HousePlantBoundParameterNames =
    {
        "target_location", "target_tile_x", "target_tile_y", "stand_tile_x", "stand_tile_y",
        "target_runtime_type", "item_id", "qualified_item_id", "house_plant_current_sprite_index",
        "house_plant_expected_sprite_index", "house_plant_expected_object_action_calls",
        "house_plant_expected_location_action_return", "safe_slot_index", "restore_slot_index",
        "interaction_kind", "expected_action_type", "native_contract", "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildHousePlantParameters(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var parameters = action.Parameters
            .Where(parameter => !HousePlantBoundParameterNames.Contains(parameter.Name, StringComparer.Ordinal))
            .ToList();
        var target = SelectHousePlantCompilerTarget(action, snapshot);
        var safeContext = ReadStateFieldValue(snapshot, "player", "safe_item_context");
        if (target is null || !safeContext.HasValue || safeContext.Value.ValueKind != JsonValueKind.Object ||
            !string.Equals(ReadString(safeContext.Value, "safe_slot_kind"), "empty", StringComparison.Ordinal))
        {
            return parameters.ToArray();
        }
        var safeSlot = ReadInt(safeContext.Value, "safe_slot_index");
        var restoreSlot = ReadInt(safeContext.Value, "current_tool_index");
        if (safeSlot is < 0 or > 11 || restoreSlot is < 0 or > 11)
        {
            return parameters.ToArray();
        }

        parameters.AddRange(new[]
        {
            Parameter("target_location", ReadStateFieldString(snapshot, "player", "location_id")),
            Parameter("target_tile_x", target.TargetX.ToString()),
            Parameter("target_tile_y", target.TargetY.ToString()),
            Parameter("stand_tile_x", target.StandX.ToString()),
            Parameter("stand_tile_y", target.StandY.ToString()),
            Parameter("target_runtime_type", target.RuntimeType),
            Parameter("item_id", target.ItemId),
            Parameter("qualified_item_id", target.QualifiedItemId),
            Parameter("house_plant_current_sprite_index", target.CurrentSpriteIndex.ToString()),
            Parameter("house_plant_expected_sprite_index", target.ExpectedSpriteIndex.ToString()),
            Parameter("house_plant_expected_object_action_calls", target.ExpectedObjectActionCalls.ToString()),
            Parameter("house_plant_expected_location_action_return", target.ExpectedLocationActionReturn ? "true" : "false"),
            Parameter("safe_slot_index", safeSlot.ToString()),
            Parameter("restore_slot_index", restoreSlot.ToString()),
            Parameter("interaction_kind", target.InteractionKind),
            Parameter("expected_action_type", target.ExpectedActionType),
            Parameter("native_contract", target.NativeContract),
            Parameter("max_movement_tiles", "512")
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileHousePlantStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = BoundHousePlantAction(action, snapshot);
        var x = ReadIntParameter(bound, "target_tile_x");
        var y = ReadIntParameter(bound, "target_tile_y");
        var expected = ReadIntParameter(bound, "house_plant_expected_sprite_index");
        if (!x.HasValue || !y.HasValue || !expected.HasValue)
        {
            return Array.Empty<CompiledActionStep>();
        }
        return new[]
        {
            Step(
                "rotate_house_plant",
                ReadParameter(bound, "target_location") + "(" + x.Value + "," + y.Value + "):sprite=" + expected.Value,
                "current_location.objects[" + x.Value + "," + y.Value + "].parent_sheet_index=" + expected.Value +
                    ";item_id_unchanged=true;qualified_item_id_unchanged=true;selected_slot_restored=true;fresh_snapshot_replan_required=true",
                600)
        };
    }

    private static string[] ValidateHousePlantPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "world.rotate_house_plant")
        {
            return Array.Empty<string>();
        }
        var reasons = new List<string>();
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("house_plant_menu_must_be_clear");
        }
        var safeContext = ReadStateFieldValue(snapshot, "player", "safe_item_context");
        if (!safeContext.HasValue || safeContext.Value.ValueKind != JsonValueKind.Object ||
            !string.Equals(ReadString(safeContext.Value, "safe_slot_kind"), "empty", StringComparison.Ordinal))
        {
            reasons.Add("house_plant_empty_toolbar_slot_required");
        }

        var target = SelectHousePlantCompilerTarget(action, snapshot);
        var bound = BoundHousePlantAction(action, snapshot);
        if (target is null ||
            ReadIntParameter(bound, "target_tile_x") != target.TargetX ||
            ReadIntParameter(bound, "target_tile_y") != target.TargetY ||
            ReadIntParameter(bound, "stand_tile_x") != target.StandX ||
            ReadIntParameter(bound, "stand_tile_y") != target.StandY ||
            ReadIntParameter(bound, "house_plant_current_sprite_index") != target.CurrentSpriteIndex ||
            ReadIntParameter(bound, "house_plant_expected_sprite_index") != target.ExpectedSpriteIndex ||
            ReadIntParameter(bound, "house_plant_expected_object_action_calls") != target.ExpectedObjectActionCalls ||
            ReadIntParameter(bound, "safe_slot_index") != (safeContext.HasValue ? ReadInt(safeContext.Value, "safe_slot_index") : -1) ||
            ReadIntParameter(bound, "restore_slot_index") != (safeContext.HasValue ? ReadInt(safeContext.Value, "current_tool_index") : -1) ||
            !string.Equals(ReadParameter(bound, "target_location"), ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "target_runtime_type"), target.RuntimeType, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "item_id"), target.ItemId, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "qualified_item_id"), target.QualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "interaction_kind"), target.InteractionKind, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "expected_action_type"), target.ExpectedActionType, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "native_contract"), target.NativeContract, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "house_plant_expected_location_action_return"), target.ExpectedLocationActionReturn ? "true" : "false", StringComparison.Ordinal))
        {
            reasons.Add("house_plant_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundHousePlantAction(SmallModelAction action, SnapshotEnvelope snapshot) =>
        new()
        {
            ActionId = action.ActionId,
            OptionId = action.OptionId,
            Rationale = action.Rationale,
            Parameters = BuildHousePlantParameters(action, snapshot)
        };

    private static HousePlantCompilerTarget? SelectHousePlantCompilerTarget(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var requestedX = ReadIntParameter(action, "target_tile_x");
        var requestedY = ReadIntParameter(action, "target_tile_y");
        var objects = ReadStateFieldValue(snapshot, "current_location", "objects");
        if (!requestedX.HasValue || !requestedY.HasValue || !objects.HasValue || objects.Value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        var row = objects.Value.EnumerateArray().FirstOrDefault(item =>
            ReadInt(item, "tile_x") == requestedX.Value &&
            ReadInt(item, "tile_y") == requestedY.Value &&
            item.TryGetProperty("house_plant_rotation", out var rotation) &&
            rotation.ValueKind == JsonValueKind.Object &&
            string.Equals(ReadString(rotation, "status"), "ready", StringComparison.Ordinal));
        if (row.ValueKind != JsonValueKind.Object ||
            !row.TryGetProperty("house_plant_rotation", out var projection) ||
            !projection.TryGetProperty("stand_tiles", out var stands) || stands.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var stand = stands.EnumerateArray()
            .Where(item => ReadBool(item, "available") == true)
            .Select(item => new
            {
                X = ReadInt(item, "tile_x"),
                Y = ReadInt(item, "tile_y"),
                Distance = Math.Abs(playerX - ReadInt(item, "tile_x")) + Math.Abs(playerY - ReadInt(item, "tile_y"))
            })
            .OrderBy(item => item.Distance)
            .ThenBy(item => item.Y)
            .ThenBy(item => item.X)
            .FirstOrDefault();
        if (stand is null)
        {
            return null;
        }
        return new HousePlantCompilerTarget(
            requestedX.Value,
            requestedY.Value,
            stand.X,
            stand.Y,
            ReadString(projection, "target_runtime_type"),
            ReadString(projection, "canonical_item_id"),
            ReadString(projection, "canonical_qualified_item_id"),
            ReadInt(projection, "current_sprite_index"),
            ReadInt(projection, "expected_sprite_index_after_native_location_action"),
            ReadInt(projection, "expected_object_check_for_action_call_count"),
            ReadBool(projection, "expected_native_location_action_return") == true,
            ReadString(projection, "interaction_kind"),
            ReadString(projection, "expected_action_type"),
            ReadString(projection, "native_contract"));
    }

    private sealed record HousePlantCompilerTarget(
        int TargetX,
        int TargetY,
        int StandX,
        int StandY,
        string RuntimeType,
        string ItemId,
        string QualifiedItemId,
        int CurrentSpriteIndex,
        int ExpectedSpriteIndex,
        int ExpectedObjectActionCalls,
        bool ExpectedLocationActionReturn,
        string InteractionKind,
        string ExpectedActionType,
        string NativeContract);
}

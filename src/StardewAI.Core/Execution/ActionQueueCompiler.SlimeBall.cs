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
    private static readonly string[] SlimeBallBoundParameterNames =
    {
        "target_location", "target_tile_x", "target_tile_y", "stand_tile_x", "stand_tile_y",
        "target_runtime_type", "item_id", "qualified_item_id", "required_fragility",
        "slime_ball_seed_days_played", "slime_ball_seed_unique_game_id",
        "slime_ball_expected_slime_quantity", "slime_ball_expected_petrified_slime_quantity",
        "slime_ball_expected_location_action_return", "safe_slot_index", "restore_slot_index",
        "interaction_kind", "expected_action_type", "native_contract", "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildSlimeBallParameters(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var parameters = action.Parameters
            .Where(parameter => !SlimeBallBoundParameterNames.Contains(parameter.Name, StringComparer.Ordinal))
            .ToList();
        var target = SelectSlimeBallCompilerTarget(action, snapshot);
        var safeContext = ReadNativeObjectCompilerSafeItemContext(snapshot);
        if (target is null || !safeContext.AllowsEmpty)
        {
            return parameters.ToArray();
        }
        var safeSlot = safeContext.SafeSlotIndex;
        var restoreSlot = safeContext.RestoreSlotIndex;

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
            Parameter("required_fragility", target.RequiredFragility.ToString()),
            Parameter("slime_ball_seed_days_played", target.SeedDaysPlayed.ToString()),
            Parameter("slime_ball_seed_unique_game_id", target.SeedUniqueGameId.ToString()),
            Parameter("slime_ball_expected_slime_quantity", target.ExpectedSlimeQuantity.ToString()),
            Parameter("slime_ball_expected_petrified_slime_quantity", target.ExpectedPetrifiedSlimeQuantity.ToString()),
            Parameter("slime_ball_expected_location_action_return", target.ExpectedLocationActionReturn ? "true" : "false"),
            Parameter("safe_slot_index", safeSlot.ToString()),
            Parameter("restore_slot_index", restoreSlot.ToString()),
            Parameter("interaction_kind", target.InteractionKind),
            Parameter("expected_action_type", target.ExpectedActionType),
            Parameter("native_contract", target.NativeContract),
            Parameter("max_movement_tiles", "512")
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileSlimeBallStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = BoundSlimeBallAction(action, snapshot);
        var x = ReadIntParameter(bound, "target_tile_x");
        var y = ReadIntParameter(bound, "target_tile_y");
        var slime = ReadIntParameter(bound, "slime_ball_expected_slime_quantity");
        var petrified = ReadIntParameter(bound, "slime_ball_expected_petrified_slime_quantity");
        if (!x.HasValue || !y.HasValue || !slime.HasValue || !petrified.HasValue)
        {
            return Array.Empty<CompiledActionStep>();
        }
        return new[]
        {
            Step(
                "collect_slime_ball",
                ReadParameter(bound, "target_location") + "(" + x.Value + "," + y.Value + ")",
                "current_location.objects[" + x.Value + "," + y.Value + "].present=false" +
                    ";conserved_output[(O)766]+=" + slime.Value +
                    ";conserved_output[(O)557]+=" + petrified.Value +
                    ";selected_slot_restored=true;fresh_snapshot_replan_required=true",
                600)
        };
    }

    private static string[] ValidateSlimeBallPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "farming.collect_slime_ball")
        {
            return Array.Empty<string>();
        }
        var reasons = new List<string>();
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("slime_ball_menu_must_be_clear");
        }
        var safeContext = ReadNativeObjectCompilerSafeItemContext(snapshot);
        if (!safeContext.AllowsEmpty)
        {
            reasons.Add("slime_ball_empty_toolbar_slot_required");
        }

        var target = SelectSlimeBallCompilerTarget(action, snapshot);
        var bound = BoundSlimeBallAction(action, snapshot);
        if (target is null ||
            ReadIntParameter(bound, "target_tile_x") != target.TargetX ||
            ReadIntParameter(bound, "target_tile_y") != target.TargetY ||
            ReadIntParameter(bound, "stand_tile_x") != target.StandX ||
            ReadIntParameter(bound, "stand_tile_y") != target.StandY ||
            ReadIntParameter(bound, "required_fragility") != target.RequiredFragility ||
            ReadLongParameter(bound, "slime_ball_seed_days_played") != target.SeedDaysPlayed ||
            ReadLongParameter(bound, "slime_ball_seed_unique_game_id") != target.SeedUniqueGameId ||
            ReadIntParameter(bound, "slime_ball_expected_slime_quantity") != target.ExpectedSlimeQuantity ||
            ReadIntParameter(bound, "slime_ball_expected_petrified_slime_quantity") != target.ExpectedPetrifiedSlimeQuantity ||
            ReadIntParameter(bound, "safe_slot_index") != safeContext.SafeSlotIndex ||
            ReadIntParameter(bound, "restore_slot_index") != safeContext.RestoreSlotIndex ||
            !string.Equals(ReadParameter(bound, "target_location"), ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "target_runtime_type"), target.RuntimeType, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "item_id"), target.ItemId, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "qualified_item_id"), target.QualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "interaction_kind"), target.InteractionKind, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "expected_action_type"), target.ExpectedActionType, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "native_contract"), target.NativeContract, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "slime_ball_expected_location_action_return"), target.ExpectedLocationActionReturn ? "true" : "false", StringComparison.Ordinal))
        {
            reasons.Add("slime_ball_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundSlimeBallAction(SmallModelAction action, SnapshotEnvelope snapshot) =>
        new()
        {
            ActionId = action.ActionId,
            OptionId = action.OptionId,
            Rationale = action.Rationale,
            Parameters = BuildSlimeBallParameters(action, snapshot)
        };

    private static SlimeBallCompilerTarget? SelectSlimeBallCompilerTarget(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var selected = SelectExactReadyNativeObjectProjection(
            action, snapshot, "slime_ball_collection");
        if (selected is null)
            return null;
        var projection = selected.Projection;
        return new SlimeBallCompilerTarget(
            selected.TargetX,
            selected.TargetY,
            selected.StandX,
            selected.StandY,
            ReadString(projection, "target_runtime_type"),
            ReadString(projection, "canonical_item_id"),
            ReadString(projection, "canonical_qualified_item_id"),
            ReadInt(projection, "required_fragility"),
            ReadLong(projection, "day_seed_days_played"),
            ReadLong(projection, "day_seed_unique_game_id"),
            ReadInt(projection, "expected_slime_quantity"),
            ReadInt(projection, "expected_petrified_slime_quantity"),
            ReadBool(projection, "expected_native_location_action_return") == true,
            ReadString(projection, "interaction_kind"),
            ReadString(projection, "expected_action_type"),
            ReadString(projection, "native_contract"));
    }

    private static long? ReadLongParameter(SmallModelAction action, string name) =>
        long.TryParse(ReadParameter(action, name), out var value) ? value : null;

    private static long ReadLong(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property) && property.TryGetInt64(out var result) ? result : 0L;

    private sealed record SlimeBallCompilerTarget(
        int TargetX,
        int TargetY,
        int StandX,
        int StandY,
        string RuntimeType,
        string ItemId,
        string QualifiedItemId,
        int RequiredFragility,
        long SeedDaysPlayed,
        long SeedUniqueGameId,
        int ExpectedSlimeQuantity,
        int ExpectedPetrifiedSlimeQuantity,
        bool ExpectedLocationActionReturn,
        string InteractionKind,
        string ExpectedActionType,
        string NativeContract);
}

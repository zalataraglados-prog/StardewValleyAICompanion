using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static readonly string[] FluteBlockBoundNames =
    {
        "target_location", "target_tile_x", "target_tile_y", "stand_tile_x", "stand_tile_y", "target_runtime_type", "item_id", "qualified_item_id",
        "safe_slot_index", "safe_slot_kind", "restore_slot_index", "flute_block_current_pitch_raw", "flute_block_current_pitch", "flute_block_next_pitch",
        "flute_block_pitch_min", "flute_block_pitch_max", "flute_block_pitch_step", "flute_block_pitch_state_count", "flute_block_sound_cue",
        "flute_block_expected_shake_timer", "flute_block_expected_scale_y", "flute_block_expected_location_action_return", "interaction_kind", "expected_action_type", "native_contract", "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildFluteBlockParameters(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var parameters = action.Parameters.Where(p => !FluteBlockBoundNames.Contains(p.Name, StringComparer.Ordinal)).ToList();
        var target = SelectFluteBlockTarget(action, snapshot);
        var safe = ReadNativeObjectCompilerSafeItemContext(snapshot);
        if (target is null || !safe.AllowsEmptyOrTool)
            return parameters.ToArray();
        parameters.AddRange(new[]
        {
            Parameter("target_location", ReadStateFieldString(snapshot, "player", "location_id")), Parameter("target_tile_x", target.X.ToString()), Parameter("target_tile_y", target.Y.ToString()),
            Parameter("stand_tile_x", target.StandX.ToString()), Parameter("stand_tile_y", target.StandY.ToString()), Parameter("target_runtime_type", target.RuntimeType),
            Parameter("item_id", target.ItemId), Parameter("qualified_item_id", target.QualifiedItemId), Parameter("safe_slot_index", safe.SafeSlotIndex.ToString()),
            Parameter("safe_slot_kind", safe.SafeSlotKind), Parameter("restore_slot_index", safe.RestoreSlotIndex.ToString()),
            Parameter("flute_block_current_pitch_raw", target.CurrentRaw), Parameter("flute_block_current_pitch", target.Current.ToString()), Parameter("flute_block_next_pitch", target.Next.ToString()),
            Parameter("flute_block_pitch_min", target.Min.ToString()), Parameter("flute_block_pitch_max", target.Max.ToString()), Parameter("flute_block_pitch_step", target.Step.ToString()),
            Parameter("flute_block_pitch_state_count", target.Count.ToString()), Parameter("flute_block_sound_cue", target.Sound),
            Parameter("flute_block_expected_shake_timer", target.Shake.ToString()), Parameter("flute_block_expected_scale_y", target.ScaleY),
            Parameter("flute_block_expected_location_action_return", target.Returns ? "true" : "false"), Parameter("interaction_kind", target.InteractionKind),
            Parameter("expected_action_type", target.ActionType), Parameter("native_contract", target.Contract), Parameter("max_movement_tiles", "512")
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileFluteBlockStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = BoundFluteBlockAction(action, snapshot);
        var x = ReadIntParameter(bound, "target_tile_x");
        var y = ReadIntParameter(bound, "target_tile_y");
        if (!x.HasValue || !y.HasValue)
            return Array.Empty<CompiledActionStep>();
        return new[] { Step("tune_flute_block", $"{ReadParameter(bound, "target_location")}({x},{y}):(O)464", $"pitch={ReadParameter(bound, "flute_block_next_pitch")};sound=flute;shake_timer=200;scale_y=1.3;selected_slot_restored=true", 600) };
    }

    private static string[] ValidateFluteBlockPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "world.tune_flute_block")
            return Array.Empty<string>();
        var reasons = new List<string>();
        if (ActionSeesActiveMenuOpen(action, snapshot)) reasons.Add("flute_block_menu_must_be_clear");
        var safe = ReadNativeObjectCompilerSafeItemContext(snapshot);
        if (!safe.AllowsEmptyOrTool) reasons.Add("flute_block_safe_toolbar_slot_required");
        var target = SelectFluteBlockTarget(action, snapshot);
        var bound = BoundFluteBlockAction(action, snapshot);
        if (target is null || ReadIntParameter(bound, "target_tile_x") != target.X || ReadIntParameter(bound, "target_tile_y") != target.Y ||
            ReadIntParameter(bound, "stand_tile_x") != target.StandX || ReadIntParameter(bound, "stand_tile_y") != target.StandY ||
            ReadIntParameter(bound, "safe_slot_index") != safe.SafeSlotIndex || ReadIntParameter(bound, "restore_slot_index") != safe.RestoreSlotIndex ||
            ReadIntParameter(bound, "flute_block_current_pitch") != target.Current || ReadIntParameter(bound, "flute_block_next_pitch") != target.Next ||
            ReadIntParameter(bound, "flute_block_pitch_min") != target.Min || ReadIntParameter(bound, "flute_block_pitch_max") != target.Max ||
            ReadIntParameter(bound, "flute_block_pitch_step") != target.Step || ReadIntParameter(bound, "flute_block_pitch_state_count") != target.Count ||
            ReadIntParameter(bound, "flute_block_expected_shake_timer") != target.Shake ||
            !string.Equals(ReadParameter(bound, "target_location"), ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "safe_slot_kind"), safe.SafeSlotKind, StringComparison.Ordinal) || !string.Equals(ReadParameter(bound, "flute_block_current_pitch_raw"), target.CurrentRaw, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "flute_block_sound_cue"), target.Sound, StringComparison.Ordinal) || !string.Equals(ReadParameter(bound, "flute_block_expected_scale_y"), target.ScaleY, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "flute_block_expected_location_action_return"), target.Returns ? "true" : "false", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "target_runtime_type"), target.RuntimeType, StringComparison.Ordinal) || !string.Equals(ReadParameter(bound, "item_id"), target.ItemId, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "qualified_item_id"), target.QualifiedItemId, StringComparison.Ordinal) || !string.Equals(ReadParameter(bound, "interaction_kind"), target.InteractionKind, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "expected_action_type"), target.ActionType, StringComparison.Ordinal) || !string.Equals(ReadParameter(bound, "native_contract"), target.Contract, StringComparison.Ordinal))
            reasons.Add("flute_block_projection_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundFluteBlockAction(SmallModelAction action, SnapshotEnvelope snapshot) => new() { ActionId = action.ActionId, OptionId = action.OptionId, Rationale = action.Rationale, Parameters = BuildFluteBlockParameters(action, snapshot) };

    private static FluteBlockTarget? SelectFluteBlockTarget(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var selected = SelectExactReadyNativeObjectProjection(action, snapshot, "flute_block_tuning");
        if (selected is null) return null;
        var p = selected.Projection;
        return new(selected.TargetX, selected.TargetY, selected.StandX, selected.StandY, ReadString(p, "target_runtime_type"), ReadString(p, "canonical_item_id"), ReadString(p, "canonical_qualified_item_id"),
            ReadString(p, "current_pitch_raw"), ReadInt(p, "current_pitch_parsed"), ReadInt(p, "next_pitch"), ReadInt(p, "pitch_min_inclusive"), ReadInt(p, "pitch_max_inclusive"),
            ReadInt(p, "pitch_step"), ReadInt(p, "pitch_state_count"), ReadString(p, "sound_cue"), ReadInt(p, "expected_shake_timer_immediately_after_action"),
            ReadDouble(p, "expected_scale_y_immediately_after_action").ToString("R", System.Globalization.CultureInfo.InvariantCulture), ReadBool(p, "expected_native_location_action_return") == true, ReadString(p, "interaction_kind"), ReadString(p, "expected_action_type"), ReadString(p, "native_contract"));
    }

    private sealed record FluteBlockTarget(int X, int Y, int StandX, int StandY, string RuntimeType, string ItemId, string QualifiedItemId, string CurrentRaw, int Current,
        int Next, int Min, int Max, int Step, int Count, string Sound, int Shake, string ScaleY, bool Returns, string InteractionKind, string ActionType, string Contract);
}

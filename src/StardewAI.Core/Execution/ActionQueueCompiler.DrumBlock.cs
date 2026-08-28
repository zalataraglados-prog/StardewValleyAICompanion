using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static readonly string[] DrumBlockBoundNames =
    {
        "target_location", "target_tile_x", "target_tile_y", "stand_tile_x", "stand_tile_y", "target_runtime_type", "item_id", "qualified_item_id",
        "safe_slot_index", "safe_slot_kind", "restore_slot_index", "drum_block_current_tone_raw", "drum_block_current_tone", "drum_block_next_tone",
        "drum_block_tone_min", "drum_block_tone_max", "drum_block_tone_step", "drum_block_tone_state_count", "drum_block_sound_cue",
        "drum_block_expected_shake_timer", "drum_block_expected_scale_y", "drum_block_expected_location_action_return", "interaction_kind", "expected_action_type", "native_contract", "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildDrumBlockParameters(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var parameters = action.Parameters.Where(p => !DrumBlockBoundNames.Contains(p.Name, StringComparer.Ordinal)).ToList();
        var target = SelectDrumBlockTarget(action, snapshot);
        var safe = ReadNativeObjectCompilerSafeItemContext(snapshot);
        if (target is null || !safe.AllowsEmptyOrTool) return parameters.ToArray();
        parameters.AddRange(new[]
        {
            Parameter("target_location", ReadStateFieldString(snapshot, "player", "location_id")), Parameter("target_tile_x", target.X.ToString()), Parameter("target_tile_y", target.Y.ToString()),
            Parameter("stand_tile_x", target.StandX.ToString()), Parameter("stand_tile_y", target.StandY.ToString()), Parameter("target_runtime_type", target.RuntimeType),
            Parameter("item_id", target.ItemId), Parameter("qualified_item_id", target.QualifiedItemId), Parameter("safe_slot_index", safe.SafeSlotIndex.ToString()),
            Parameter("safe_slot_kind", safe.SafeSlotKind), Parameter("restore_slot_index", safe.RestoreSlotIndex.ToString()),
            Parameter("drum_block_current_tone_raw", target.CurrentRaw), Parameter("drum_block_current_tone", target.Current.ToString()), Parameter("drum_block_next_tone", target.Next.ToString()),
            Parameter("drum_block_tone_min", target.Min.ToString()), Parameter("drum_block_tone_max", target.Max.ToString()), Parameter("drum_block_tone_step", target.Step.ToString()),
            Parameter("drum_block_tone_state_count", target.Count.ToString()), Parameter("drum_block_sound_cue", target.Sound),
            Parameter("drum_block_expected_shake_timer", target.Shake.ToString()), Parameter("drum_block_expected_scale_y", target.ScaleY),
            Parameter("drum_block_expected_location_action_return", target.Returns ? "true" : "false"), Parameter("interaction_kind", target.InteractionKind),
            Parameter("expected_action_type", target.ActionType), Parameter("native_contract", target.Contract), Parameter("max_movement_tiles", "512")
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileDrumBlockStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = BoundDrumBlockAction(action, snapshot);
        var x = ReadIntParameter(bound, "target_tile_x");
        var y = ReadIntParameter(bound, "target_tile_y");
        if (!x.HasValue || !y.HasValue) return Array.Empty<CompiledActionStep>();
        return new[] { Step("tune_drum_block", $"{ReadParameter(bound, "target_location")}({x},{y}):(O)463", $"tone={ReadParameter(bound, "drum_block_next_tone")};sound={ReadParameter(bound, "drum_block_sound_cue")};shake_timer=200;scale_y=1.3;selected_slot_restored=true", 600) };
    }

    private static string[] ValidateDrumBlockPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "world.tune_drum_block") return Array.Empty<string>();
        var reasons = new List<string>();
        if (ActionSeesActiveMenuOpen(action, snapshot)) reasons.Add("drum_block_menu_must_be_clear");
        var safe = ReadNativeObjectCompilerSafeItemContext(snapshot);
        if (!safe.AllowsEmptyOrTool) reasons.Add("drum_block_safe_toolbar_slot_required");
        var target = SelectDrumBlockTarget(action, snapshot);
        var bound = BoundDrumBlockAction(action, snapshot);
        if (target is null || ReadIntParameter(bound, "target_tile_x") != target.X || ReadIntParameter(bound, "target_tile_y") != target.Y ||
            ReadIntParameter(bound, "stand_tile_x") != target.StandX || ReadIntParameter(bound, "stand_tile_y") != target.StandY ||
            ReadIntParameter(bound, "safe_slot_index") != safe.SafeSlotIndex || ReadIntParameter(bound, "restore_slot_index") != safe.RestoreSlotIndex ||
            ReadIntParameter(bound, "drum_block_current_tone") != target.Current || ReadIntParameter(bound, "drum_block_next_tone") != target.Next ||
            ReadIntParameter(bound, "drum_block_tone_min") != target.Min || ReadIntParameter(bound, "drum_block_tone_max") != target.Max ||
            ReadIntParameter(bound, "drum_block_tone_step") != target.Step || ReadIntParameter(bound, "drum_block_tone_state_count") != target.Count ||
            ReadIntParameter(bound, "drum_block_expected_shake_timer") != target.Shake ||
            !string.Equals(ReadParameter(bound, "target_location"), ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "safe_slot_kind"), safe.SafeSlotKind, StringComparison.Ordinal) || !string.Equals(ReadParameter(bound, "drum_block_current_tone_raw"), target.CurrentRaw, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "drum_block_sound_cue"), target.Sound, StringComparison.Ordinal) || !string.Equals(ReadParameter(bound, "drum_block_expected_scale_y"), target.ScaleY, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "drum_block_expected_location_action_return"), target.Returns ? "true" : "false", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "target_runtime_type"), target.RuntimeType, StringComparison.Ordinal) || !string.Equals(ReadParameter(bound, "item_id"), target.ItemId, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "qualified_item_id"), target.QualifiedItemId, StringComparison.Ordinal) || !string.Equals(ReadParameter(bound, "interaction_kind"), target.InteractionKind, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "expected_action_type"), target.ActionType, StringComparison.Ordinal) || !string.Equals(ReadParameter(bound, "native_contract"), target.Contract, StringComparison.Ordinal))
            reasons.Add("drum_block_projection_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundDrumBlockAction(SmallModelAction action, SnapshotEnvelope snapshot) => new()
    {
        ActionId = action.ActionId, OptionId = action.OptionId, Rationale = action.Rationale, Parameters = BuildDrumBlockParameters(action, snapshot)
    };

    private static DrumBlockTarget? SelectDrumBlockTarget(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var selected = SelectExactReadyNativeObjectProjection(action, snapshot, "drum_block_tuning");
        if (selected is null) return null;
        var p = selected.Projection;
        return new(selected.TargetX, selected.TargetY, selected.StandX, selected.StandY, ReadString(p, "target_runtime_type"), ReadString(p, "canonical_item_id"), ReadString(p, "canonical_qualified_item_id"),
            ReadString(p, "current_tone_raw"), ReadInt(p, "current_tone_parsed"), ReadInt(p, "next_tone"), ReadInt(p, "tone_min_inclusive"), ReadInt(p, "tone_max_inclusive"),
            ReadInt(p, "tone_step"), ReadInt(p, "tone_state_count"), ReadString(p, "sound_cue"), ReadInt(p, "expected_shake_timer_immediately_after_action"),
            ReadDouble(p, "expected_scale_y_immediately_after_action").ToString("R", System.Globalization.CultureInfo.InvariantCulture), ReadBool(p, "expected_native_location_action_return") == true,
            ReadString(p, "interaction_kind"), ReadString(p, "expected_action_type"), ReadString(p, "native_contract"));
    }

    private sealed record DrumBlockTarget(int X, int Y, int StandX, int StandY, string RuntimeType, string ItemId, string QualifiedItemId, string CurrentRaw, int Current,
        int Next, int Min, int Max, int Step, int Count, string Sound, int Shake, string ScaleY, bool Returns, string InteractionKind, string ActionType, string Contract);
}

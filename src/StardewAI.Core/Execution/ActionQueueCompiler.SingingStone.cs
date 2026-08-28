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
    private static readonly string[] SingingStoneBoundParameterNames =
    {
        "target_location", "target_tile_x", "target_tile_y", "stand_tile_x", "stand_tile_y",
        "target_runtime_type", "item_id", "qualified_item_id", "safe_slot_index", "safe_slot_kind",
        "restore_slot_index", "singing_stone_sound_name", "singing_stone_pitch_rng_source",
        "singing_stone_exact_next_pitch_status", "singing_stone_pitch_min", "singing_stone_pitch_max",
        "singing_stone_pitch_step", "singing_stone_pitch_outcome_count",
        "singing_stone_expected_shake_timer", "singing_stone_expected_location_action_return",
        "interaction_kind", "expected_action_type", "native_contract", "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildSingingStoneParameters(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var parameters = action.Parameters
            .Where(parameter => !SingingStoneBoundParameterNames.Contains(parameter.Name, StringComparer.Ordinal))
            .ToList();
        var target = SelectSingingStoneCompilerTarget(action, snapshot);
        var safeContext = ReadNativeObjectCompilerSafeItemContext(snapshot);
        if (target is null || !safeContext.AllowsEmptyOrTool)
            return parameters.ToArray();
        var safeSlotKind = safeContext.SafeSlotKind;
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
            Parameter("safe_slot_index", safeSlot.ToString()),
            Parameter("safe_slot_kind", safeSlotKind),
            Parameter("restore_slot_index", restoreSlot.ToString()),
            Parameter("singing_stone_sound_name", target.SoundName),
            Parameter("singing_stone_pitch_rng_source", target.PitchRngSource),
            Parameter("singing_stone_exact_next_pitch_status", target.ExactNextPitchStatus),
            Parameter("singing_stone_pitch_min", target.PitchMin.ToString()),
            Parameter("singing_stone_pitch_max", target.PitchMax.ToString()),
            Parameter("singing_stone_pitch_step", target.PitchStep.ToString()),
            Parameter("singing_stone_pitch_outcome_count", target.PitchOutcomeCount.ToString()),
            Parameter("singing_stone_expected_shake_timer", target.ExpectedShakeTimer.ToString()),
            Parameter("singing_stone_expected_location_action_return", target.ExpectedLocationActionReturn ? "true" : "false"),
            Parameter("interaction_kind", target.InteractionKind),
            Parameter("expected_action_type", target.ExpectedActionType),
            Parameter("native_contract", target.NativeContract),
            Parameter("max_movement_tiles", "512")
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileSingingStoneStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = BoundSingingStoneAction(action, snapshot);
        var x = ReadIntParameter(bound, "target_tile_x");
        var y = ReadIntParameter(bound, "target_tile_y");
        if (!x.HasValue || !y.HasValue)
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step(
                "play_singing_stone",
                ReadParameter(bound, "target_location") + "(" + x.Value + "," + y.Value + "):(BC)94",
                "native_sound=crystal;pitch_distribution=uniform_0_2300_step_100" +
                    ";current_location.objects[" + x.Value + "," + y.Value + "].shake_timer=100" +
                    ";item_identity_unchanged=true;selected_slot_restored=true;fresh_snapshot_replan_required=true",
                600)
        };
    }

    private static string[] ValidateSingingStonePlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "world.play_singing_stone")
            return Array.Empty<string>();
        var reasons = new List<string>();
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("singing_stone_menu_must_be_clear");
        var safeContext = ReadNativeObjectCompilerSafeItemContext(snapshot);
        var safeSlotKind = safeContext.SafeSlotKind;
        if (!safeContext.AllowsEmptyOrTool)
        {
            reasons.Add("singing_stone_safe_toolbar_slot_required");
        }

        var target = SelectSingingStoneCompilerTarget(action, snapshot);
        var bound = BoundSingingStoneAction(action, snapshot);
        if (target is null ||
            ReadIntParameter(bound, "target_tile_x") != target.TargetX ||
            ReadIntParameter(bound, "target_tile_y") != target.TargetY ||
            ReadIntParameter(bound, "stand_tile_x") != target.StandX ||
            ReadIntParameter(bound, "stand_tile_y") != target.StandY ||
            ReadIntParameter(bound, "safe_slot_index") != safeContext.SafeSlotIndex ||
            ReadIntParameter(bound, "restore_slot_index") != safeContext.RestoreSlotIndex ||
            ReadIntParameter(bound, "singing_stone_pitch_min") != target.PitchMin ||
            ReadIntParameter(bound, "singing_stone_pitch_max") != target.PitchMax ||
            ReadIntParameter(bound, "singing_stone_pitch_step") != target.PitchStep ||
            ReadIntParameter(bound, "singing_stone_pitch_outcome_count") != target.PitchOutcomeCount ||
            ReadIntParameter(bound, "singing_stone_expected_shake_timer") != target.ExpectedShakeTimer ||
            !string.Equals(ReadParameter(bound, "target_location"), ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "target_runtime_type"), target.RuntimeType, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "item_id"), target.ItemId, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "qualified_item_id"), target.QualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "safe_slot_kind"), safeSlotKind, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "singing_stone_sound_name"), target.SoundName, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "singing_stone_pitch_rng_source"), target.PitchRngSource, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "singing_stone_exact_next_pitch_status"), target.ExactNextPitchStatus, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "singing_stone_expected_location_action_return"), target.ExpectedLocationActionReturn ? "true" : "false", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "interaction_kind"), target.InteractionKind, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "expected_action_type"), target.ExpectedActionType, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "native_contract"), target.NativeContract, StringComparison.Ordinal))
        {
            reasons.Add("singing_stone_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundSingingStoneAction(SmallModelAction action, SnapshotEnvelope snapshot) =>
        new()
        {
            ActionId = action.ActionId,
            OptionId = action.OptionId,
            Rationale = action.Rationale,
            Parameters = BuildSingingStoneParameters(action, snapshot)
        };

    private static SingingStoneCompilerTarget? SelectSingingStoneCompilerTarget(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var selected = SelectExactReadyNativeObjectProjection(
            action, snapshot, "singing_stone_interaction");
        if (selected is null)
            return null;
        var projection = selected.Projection;
        return new SingingStoneCompilerTarget(
            selected.TargetX,
            selected.TargetY,
            selected.StandX,
            selected.StandY,
            ReadString(projection, "target_runtime_type"),
            ReadString(projection, "canonical_item_id"),
            ReadString(projection, "canonical_qualified_item_id"),
            ReadString(projection, "sound_name"),
            ReadString(projection, "pitch_rng_source"),
            ReadString(projection, "exact_next_pitch_status"),
            ReadInt(projection, "pitch_min_inclusive"),
            ReadInt(projection, "pitch_max_inclusive"),
            ReadInt(projection, "pitch_step"),
            ReadInt(projection, "pitch_outcome_count"),
            ReadInt(projection, "expected_shake_timer_immediately_after_action"),
            ReadBool(projection, "expected_native_location_action_return") == true,
            ReadString(projection, "interaction_kind"),
            ReadString(projection, "expected_action_type"),
            ReadString(projection, "native_contract"));
    }

    private sealed record SingingStoneCompilerTarget(
        int TargetX,
        int TargetY,
        int StandX,
        int StandY,
        string RuntimeType,
        string ItemId,
        string QualifiedItemId,
        string SoundName,
        string PitchRngSource,
        string ExactNextPitchStatus,
        int PitchMin,
        int PitchMax,
        int PitchStep,
        int PitchOutcomeCount,
        int ExpectedShakeTimer,
        bool ExpectedLocationActionReturn,
        string InteractionKind,
        string ExpectedActionType,
        string NativeContract);
}

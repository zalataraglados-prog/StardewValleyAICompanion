using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static readonly string[] MiniObeliskBoundParameterNames =
    {
        "target_location", "target_tile_x", "target_tile_y", "stand_tile_x", "stand_tile_y",
        "target_runtime_type", "item_id", "qualified_item_id", "safe_slot_index", "safe_slot_kind",
        "restore_slot_index", "mini_obelisk_pair_member_index",
        "mini_obelisk_pair_first_tile_x", "mini_obelisk_pair_first_tile_y",
        "mini_obelisk_pair_second_tile_x", "mini_obelisk_pair_second_tile_y",
        "mini_obelisk_destination_tile_x", "mini_obelisk_destination_tile_y",
        "mini_obelisk_landing_tile_x", "mini_obelisk_landing_tile_y",
        "mini_obelisk_expected_delay_milliseconds", "mini_obelisk_expected_location_action_return",
        "interaction_kind", "expected_action_type", "native_contract", "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildMiniObeliskParameters(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var parameters = action.Parameters
            .Where(parameter => !MiniObeliskBoundParameterNames.Contains(parameter.Name, StringComparer.Ordinal))
            .ToList();
        var target = SelectMiniObeliskCompilerTarget(action, snapshot);
        var safeContext = ReadNativeObjectCompilerSafeItemContext(snapshot);
        if (target is null || !safeContext.AllowsEmptyOrTool)
            return parameters.ToArray();

        parameters.AddRange(new[]
        {
            Parameter("target_location", ReadStateFieldString(snapshot, "player", "location_id")),
            Parameter("target_tile_x", target.SourceX.ToString()),
            Parameter("target_tile_y", target.SourceY.ToString()),
            Parameter("stand_tile_x", target.StandX.ToString()),
            Parameter("stand_tile_y", target.StandY.ToString()),
            Parameter("target_runtime_type", target.RuntimeType),
            Parameter("item_id", target.ItemId),
            Parameter("qualified_item_id", target.QualifiedItemId),
            Parameter("safe_slot_index", safeContext.SafeSlotIndex.ToString()),
            Parameter("safe_slot_kind", safeContext.SafeSlotKind),
            Parameter("restore_slot_index", safeContext.RestoreSlotIndex.ToString()),
            Parameter("mini_obelisk_pair_member_index", target.PairMemberIndex.ToString()),
            Parameter("mini_obelisk_pair_first_tile_x", target.PairFirstX.ToString()),
            Parameter("mini_obelisk_pair_first_tile_y", target.PairFirstY.ToString()),
            Parameter("mini_obelisk_pair_second_tile_x", target.PairSecondX.ToString()),
            Parameter("mini_obelisk_pair_second_tile_y", target.PairSecondY.ToString()),
            Parameter("mini_obelisk_destination_tile_x", target.DestinationX.ToString()),
            Parameter("mini_obelisk_destination_tile_y", target.DestinationY.ToString()),
            Parameter("mini_obelisk_landing_tile_x", target.LandingX.ToString()),
            Parameter("mini_obelisk_landing_tile_y", target.LandingY.ToString()),
            Parameter("mini_obelisk_expected_delay_milliseconds", target.ExpectedDelayMilliseconds.ToString()),
            Parameter("mini_obelisk_expected_location_action_return", target.ExpectedLocationActionReturn ? "true" : "false"),
            Parameter("interaction_kind", target.InteractionKind),
            Parameter("expected_action_type", target.ExpectedActionType),
            Parameter("native_contract", target.NativeContract),
            Parameter("max_movement_tiles", "512")
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileMiniObeliskStep(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var bound = BoundMiniObeliskAction(action, snapshot);
        var sourceX = ReadIntParameter(bound, "target_tile_x");
        var sourceY = ReadIntParameter(bound, "target_tile_y");
        var landingX = ReadIntParameter(bound, "mini_obelisk_landing_tile_x");
        var landingY = ReadIntParameter(bound, "mini_obelisk_landing_tile_y");
        if (!sourceX.HasValue || !sourceY.HasValue || !landingX.HasValue || !landingY.HasValue)
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step(
                "use_mini_obelisk",
                ReadParameter(bound, "target_location") + "(" + sourceX.Value + "," + sourceY.Value +
                    ")->(" + landingX.Value + "," + landingY.Value + ")",
                "player.location_id=" + ReadParameter(bound, "target_location") +
                    ";player.tile=" + landingX.Value + "," + landingY.Value +
                    ";mini_obelisk_pair_identity_unchanged=true;selected_slot_restored=true;fresh_snapshot_replan_required=true",
                720)
        };
    }

    private static string[] ValidateMiniObeliskPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "movement.use_mini_obelisk")
            return Array.Empty<string>();
        var reasons = new List<string>();
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("mini_obelisk_menu_must_be_clear");
        var safeContext = ReadNativeObjectCompilerSafeItemContext(snapshot);
        if (!safeContext.AllowsEmptyOrTool)
            reasons.Add("mini_obelisk_safe_toolbar_slot_required");

        var target = SelectMiniObeliskCompilerTarget(action, snapshot);
        var bound = BoundMiniObeliskAction(action, snapshot);
        if (target is null ||
            ReadIntParameter(bound, "target_tile_x") != target.SourceX ||
            ReadIntParameter(bound, "target_tile_y") != target.SourceY ||
            ReadIntParameter(bound, "stand_tile_x") != target.StandX ||
            ReadIntParameter(bound, "stand_tile_y") != target.StandY ||
            ReadIntParameter(bound, "safe_slot_index") != safeContext.SafeSlotIndex ||
            ReadIntParameter(bound, "restore_slot_index") != safeContext.RestoreSlotIndex ||
            ReadIntParameter(bound, "mini_obelisk_pair_member_index") != target.PairMemberIndex ||
            ReadIntParameter(bound, "mini_obelisk_pair_first_tile_x") != target.PairFirstX ||
            ReadIntParameter(bound, "mini_obelisk_pair_first_tile_y") != target.PairFirstY ||
            ReadIntParameter(bound, "mini_obelisk_pair_second_tile_x") != target.PairSecondX ||
            ReadIntParameter(bound, "mini_obelisk_pair_second_tile_y") != target.PairSecondY ||
            ReadIntParameter(bound, "mini_obelisk_destination_tile_x") != target.DestinationX ||
            ReadIntParameter(bound, "mini_obelisk_destination_tile_y") != target.DestinationY ||
            ReadIntParameter(bound, "mini_obelisk_landing_tile_x") != target.LandingX ||
            ReadIntParameter(bound, "mini_obelisk_landing_tile_y") != target.LandingY ||
            ReadIntParameter(bound, "mini_obelisk_expected_delay_milliseconds") != target.ExpectedDelayMilliseconds ||
            !string.Equals(ReadParameter(bound, "target_location"), ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "target_runtime_type"), target.RuntimeType, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "item_id"), target.ItemId, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "qualified_item_id"), target.QualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "safe_slot_kind"), safeContext.SafeSlotKind, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "mini_obelisk_expected_location_action_return"), target.ExpectedLocationActionReturn ? "true" : "false", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "interaction_kind"), target.InteractionKind, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "expected_action_type"), target.ExpectedActionType, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "native_contract"), target.NativeContract, StringComparison.Ordinal))
        {
            reasons.Add("mini_obelisk_pair_destination_or_landing_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundMiniObeliskAction(SmallModelAction action, SnapshotEnvelope snapshot) =>
        new()
        {
            ActionId = action.ActionId,
            OptionId = action.OptionId,
            Rationale = action.Rationale,
            Parameters = BuildMiniObeliskParameters(action, snapshot)
        };

    private static MiniObeliskCompilerTarget? SelectMiniObeliskCompilerTarget(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var selected = SelectExactReadyNativeObjectProjection(action, snapshot, "mini_obelisk_use");
        if (selected is null)
            return null;
        var projection = selected.Projection;
        var stand = selected.Stand;
        return new MiniObeliskCompilerTarget(
            selected.TargetX,
            selected.TargetY,
            selected.StandX,
            selected.StandY,
            ReadString(projection, "target_runtime_type"),
            ReadString(projection, "canonical_item_id"),
            ReadString(projection, "canonical_qualified_item_id"),
            ReadInt(projection, "native_pair_member_index"),
            ReadInt(projection, "native_pair_first_tile_x"),
            ReadInt(projection, "native_pair_first_tile_y"),
            ReadInt(projection, "native_pair_second_tile_x"),
            ReadInt(projection, "native_pair_second_tile_y"),
            ReadInt(stand, "native_destination_tile_x"),
            ReadInt(stand, "native_destination_tile_y"),
            ReadInt(stand, "native_landing_tile_x"),
            ReadInt(stand, "native_landing_tile_y"),
            ReadInt(projection, "expected_delay_milliseconds"),
            ReadBool(projection, "expected_native_location_action_return") == true,
            ReadString(projection, "interaction_kind"),
            ReadString(projection, "expected_action_type"),
            ReadString(projection, "native_contract"));
    }

    private sealed record MiniObeliskCompilerTarget(
        int SourceX,
        int SourceY,
        int StandX,
        int StandY,
        string RuntimeType,
        string ItemId,
        string QualifiedItemId,
        int PairMemberIndex,
        int PairFirstX,
        int PairFirstY,
        int PairSecondX,
        int PairSecondY,
        int DestinationX,
        int DestinationY,
        int LandingX,
        int LandingY,
        int ExpectedDelayMilliseconds,
        bool ExpectedLocationActionReturn,
        string InteractionKind,
        string ExpectedActionType,
        string NativeContract);
}

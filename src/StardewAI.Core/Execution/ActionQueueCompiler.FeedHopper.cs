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
    private static readonly string[] FeedHopperBoundParameterNames =
    {
        "target_location", "target_tile_x", "target_tile_y", "stand_tile_x", "stand_tile_y",
        "target_runtime_type", "item_id", "qualified_item_id", "feed_hopper_hay_qualified_item_id",
        "feed_hopper_root_location_id", "feed_hopper_silo_hay_before", "feed_hopper_animal_count",
        "feed_hopper_animal_limit", "feed_hopper_placed_hay_count", "feed_hopper_unfed_animal_count",
        "feed_hopper_expected_withdrawal_quantity", "feed_hopper_expected_silo_hay_after",
        "feed_hopper_expected_location_action_return", "safe_slot_index", "safe_slot_kind",
        "restore_slot_index", "interaction_kind", "expected_action_type", "native_contract",
        "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildFeedHopperParameters(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var parameters = action.Parameters
            .Where(parameter => !FeedHopperBoundParameterNames.Contains(parameter.Name, StringComparer.Ordinal))
            .ToList();
        var target = SelectFeedHopperCompilerTarget(action, snapshot);
        var safeContext = ReadNativeObjectCompilerSafeItemContext(snapshot);
        if (target is null || !safeContext.AllowsEmptyOrTool)
            return parameters.ToArray();
        var safeKind = safeContext.SafeSlotKind;
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
            Parameter("feed_hopper_hay_qualified_item_id", target.HayQualifiedItemId),
            Parameter("feed_hopper_root_location_id", target.RootLocationId),
            Parameter("feed_hopper_silo_hay_before", target.SiloHayBefore.ToString()),
            Parameter("feed_hopper_animal_count", target.AnimalCount.ToString()),
            Parameter("feed_hopper_animal_limit", target.AnimalLimit.ToString()),
            Parameter("feed_hopper_placed_hay_count", target.PlacedHayCount.ToString()),
            Parameter("feed_hopper_unfed_animal_count", target.UnfedAnimalCount.ToString()),
            Parameter("feed_hopper_expected_withdrawal_quantity", target.ExpectedWithdrawal.ToString()),
            Parameter("feed_hopper_expected_silo_hay_after", target.ExpectedSiloHayAfter.ToString()),
            Parameter("feed_hopper_expected_location_action_return", target.ExpectedLocationActionReturn ? "true" : "false"),
            Parameter("safe_slot_index", safeSlot.ToString()),
            Parameter("safe_slot_kind", safeKind),
            Parameter("restore_slot_index", restoreSlot.ToString()),
            Parameter("interaction_kind", target.InteractionKind),
            Parameter("expected_action_type", target.ExpectedActionType),
            Parameter("native_contract", target.NativeContract),
            Parameter("max_movement_tiles", "512")
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileFeedHopperStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = BoundFeedHopperAction(action, snapshot);
        var x = ReadIntParameter(bound, "target_tile_x");
        var y = ReadIntParameter(bound, "target_tile_y");
        var quantity = ReadIntParameter(bound, "feed_hopper_expected_withdrawal_quantity");
        if (!x.HasValue || !y.HasValue || !quantity.HasValue || quantity <= 0)
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step(
                "withdraw_feed_hopper_hay",
                ReadParameter(bound, "target_location") + "(" + x.Value + "," + y.Value + "):(BC)99",
                "root_location.pieces_of_hay-=" + quantity.Value +
                    ";player.inventory[(O)178]+=" + quantity.Value +
                    ";feed_hopper_identity_unchanged=true;selected_slot_restored=true;fresh_snapshot_replan_required=true",
                600)
        };
    }

    private static string[] ValidateFeedHopperPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "animals.withdraw_feed_hopper_hay")
            return Array.Empty<string>();
        var reasons = new List<string>();
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("feed_hopper_menu_must_be_clear");
        var safeContext = ReadNativeObjectCompilerSafeItemContext(snapshot);
        var safeKind = safeContext.SafeSlotKind;
        if (!safeContext.AllowsEmptyOrTool)
        {
            reasons.Add("feed_hopper_safe_toolbar_slot_required");
        }

        var target = SelectFeedHopperCompilerTarget(action, snapshot);
        var bound = BoundFeedHopperAction(action, snapshot);
        if (target is null ||
            ReadIntParameter(bound, "target_tile_x") != target.TargetX ||
            ReadIntParameter(bound, "target_tile_y") != target.TargetY ||
            ReadIntParameter(bound, "stand_tile_x") != target.StandX ||
            ReadIntParameter(bound, "stand_tile_y") != target.StandY ||
            ReadIntParameter(bound, "safe_slot_index") != safeContext.SafeSlotIndex ||
            ReadIntParameter(bound, "restore_slot_index") != safeContext.RestoreSlotIndex ||
            ReadIntParameter(bound, "feed_hopper_silo_hay_before") != target.SiloHayBefore ||
            ReadIntParameter(bound, "feed_hopper_animal_count") != target.AnimalCount ||
            ReadIntParameter(bound, "feed_hopper_animal_limit") != target.AnimalLimit ||
            ReadIntParameter(bound, "feed_hopper_placed_hay_count") != target.PlacedHayCount ||
            ReadIntParameter(bound, "feed_hopper_unfed_animal_count") != target.UnfedAnimalCount ||
            ReadIntParameter(bound, "feed_hopper_expected_withdrawal_quantity") != target.ExpectedWithdrawal ||
            ReadIntParameter(bound, "feed_hopper_expected_silo_hay_after") != target.ExpectedSiloHayAfter ||
            !string.Equals(ReadParameter(bound, "target_location"), ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "target_runtime_type"), target.RuntimeType, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "item_id"), target.ItemId, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "qualified_item_id"), target.QualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "feed_hopper_hay_qualified_item_id"), target.HayQualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "feed_hopper_root_location_id"), target.RootLocationId, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "feed_hopper_expected_location_action_return"), target.ExpectedLocationActionReturn ? "true" : "false", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "safe_slot_kind"), safeKind, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "interaction_kind"), target.InteractionKind, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "expected_action_type"), target.ExpectedActionType, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "native_contract"), target.NativeContract, StringComparison.Ordinal))
        {
            reasons.Add("feed_hopper_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundFeedHopperAction(SmallModelAction action, SnapshotEnvelope snapshot) =>
        new()
        {
            ActionId = action.ActionId,
            OptionId = action.OptionId,
            Rationale = action.Rationale,
            Parameters = BuildFeedHopperParameters(action, snapshot)
        };

    private static FeedHopperCompilerTarget? SelectFeedHopperCompilerTarget(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var selected = SelectExactReadyNativeObjectProjection(
            action, snapshot, "feed_hopper_withdrawal");
        if (selected is null)
            return null;
        var value = selected.Projection;
        return new FeedHopperCompilerTarget(
            selected.TargetX, selected.TargetY, selected.StandX, selected.StandY,
            ReadString(value, "target_runtime_type"),
            ReadString(value, "canonical_item_id"),
            ReadString(value, "canonical_qualified_item_id"),
            ReadString(value, "hay_qualified_item_id"),
            ReadString(value, "root_location_id"),
            ReadInt(value, "silo_hay_before"),
            ReadInt(value, "animal_count"),
            ReadInt(value, "animal_limit"),
            ReadInt(value, "placed_hay_count"),
            ReadInt(value, "unfed_animal_count"),
            ReadInt(value, "expected_withdrawal_quantity"),
            ReadInt(value, "expected_silo_hay_after"),
            ReadBool(value, "expected_native_location_action_return") == true,
            ReadString(value, "interaction_kind"),
            ReadString(value, "expected_action_type"),
            ReadString(value, "native_contract"));
    }

    private sealed record FeedHopperCompilerTarget(
        int TargetX, int TargetY, int StandX, int StandY,
        string RuntimeType, string ItemId, string QualifiedItemId, string HayQualifiedItemId,
        string RootLocationId, int SiloHayBefore, int AnimalCount, int AnimalLimit,
        int PlacedHayCount, int UnfedAnimalCount, int ExpectedWithdrawal, int ExpectedSiloHayAfter,
        bool ExpectedLocationActionReturn, string InteractionKind, string ExpectedActionType,
        string NativeContract);
}

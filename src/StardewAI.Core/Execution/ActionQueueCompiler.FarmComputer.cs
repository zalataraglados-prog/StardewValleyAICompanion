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
    private static readonly string[] FarmComputerBoundParameterNames =
    {
        "target_location", "target_tile_x", "target_tile_y", "stand_tile_x", "stand_tile_y",
        "target_runtime_type", "item_id", "qualified_item_id", "safe_slot_index", "safe_slot_kind",
        "restore_slot_index", "farm_computer_root_location_id", "farm_computer_includes_hay",
        "farm_computer_pieces_of_hay", "farm_computer_hay_capacity", "farm_computer_total_crops",
        "farm_computer_crops_ready", "farm_computer_unwatered_crops", "farm_computer_greenhouse_crops_ready",
        "farm_computer_open_hoe_dirt", "farm_computer_total_forage", "farm_computer_machines_ready",
        "farm_computer_farm_cave_ready", "farm_computer_report_sha256", "farm_computer_expected_delay_ms",
        "farm_computer_expected_shake_timer", "farm_computer_expected_freeze_ms",
        "farm_computer_expected_location_action_return", "interaction_kind", "expected_action_type",
        "native_contract", "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildFarmComputerParameters(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var parameters = action.Parameters
            .Where(parameter => !FarmComputerBoundParameterNames.Contains(parameter.Name, StringComparer.Ordinal))
            .ToList();
        var target = SelectFarmComputerCompilerTarget(action, snapshot);
        var safeContext = ReadNativeObjectCompilerSafeItemContext(snapshot);
        if (target is null || !safeContext.AllowsEmptyOrTool)
            return parameters.ToArray();

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
            Parameter("safe_slot_index", safeContext.SafeSlotIndex.ToString()),
            Parameter("safe_slot_kind", safeContext.SafeSlotKind),
            Parameter("restore_slot_index", safeContext.RestoreSlotIndex.ToString()),
            Parameter("farm_computer_root_location_id", target.RootLocationId),
            Parameter("farm_computer_includes_hay", target.IncludesHay),
            Parameter("farm_computer_pieces_of_hay", target.PiecesOfHay),
            Parameter("farm_computer_hay_capacity", target.HayCapacity),
            Parameter("farm_computer_total_crops", target.TotalCrops),
            Parameter("farm_computer_crops_ready", target.CropsReady),
            Parameter("farm_computer_unwatered_crops", target.UnwateredCrops),
            Parameter("farm_computer_greenhouse_crops_ready", target.GreenhouseCropsReady),
            Parameter("farm_computer_open_hoe_dirt", target.OpenHoeDirt),
            Parameter("farm_computer_total_forage", target.TotalForage),
            Parameter("farm_computer_machines_ready", target.MachinesReady),
            Parameter("farm_computer_farm_cave_ready", target.FarmCaveReady),
            Parameter("farm_computer_report_sha256", target.ReportSha256),
            Parameter("farm_computer_expected_delay_ms", target.ExpectedDelayMs),
            Parameter("farm_computer_expected_shake_timer", target.ExpectedShakeTimer),
            Parameter("farm_computer_expected_freeze_ms", target.ExpectedFreezeMs),
            Parameter("farm_computer_expected_location_action_return", target.ExpectedLocationActionReturn),
            Parameter("interaction_kind", target.InteractionKind),
            Parameter("expected_action_type", target.ExpectedActionType),
            Parameter("native_contract", target.NativeContract),
            Parameter("max_movement_tiles", "512")
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileFarmComputerStep(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var bound = BoundFarmComputerAction(action, snapshot);
        var x = ReadIntParameter(bound, "target_tile_x");
        var y = ReadIntParameter(bound, "target_tile_y");
        if (!x.HasValue || !y.HasValue)
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step(
                "read_farm_computer_report",
                ReadParameter(bound, "target_location") + "(" + x.Value + "," + y.Value + "):(BC)239",
                "native_dialogue=FarmComputer;report_sha256=" + ReadParameter(bound, "farm_computer_report_sha256") +
                    ";structured_information_already_transparent=true;selected_slot_restored=true",
                900)
        };
    }

    private static string[] ValidateFarmComputerPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "farming.read_farm_computer_report")
            return Array.Empty<string>();
        var reasons = new List<string>();
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("farm_computer_menu_must_be_clear");
        var safeContext = ReadNativeObjectCompilerSafeItemContext(snapshot);
        if (!safeContext.AllowsEmptyOrTool)
            reasons.Add("farm_computer_safe_toolbar_slot_required");

        var target = SelectFarmComputerCompilerTarget(action, snapshot);
        var bound = BoundFarmComputerAction(action, snapshot);
        if (target is null ||
            ReadIntParameter(bound, "target_tile_x") != target.TargetX ||
            ReadIntParameter(bound, "target_tile_y") != target.TargetY ||
            ReadIntParameter(bound, "stand_tile_x") != target.StandX ||
            ReadIntParameter(bound, "stand_tile_y") != target.StandY ||
            ReadIntParameter(bound, "safe_slot_index") != safeContext.SafeSlotIndex ||
            ReadIntParameter(bound, "restore_slot_index") != safeContext.RestoreSlotIndex ||
            !string.Equals(ReadParameter(bound, "target_location"), ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "safe_slot_kind"), safeContext.SafeSlotKind, StringComparison.Ordinal) ||
            FarmComputerBoundParameterNames.Where(name => name.StartsWith("farm_computer_", StringComparison.Ordinal))
                .Any(name => !string.Equals(ReadParameter(bound, name), target.Value(name), StringComparison.Ordinal)) ||
            !string.Equals(ReadParameter(bound, "target_runtime_type"), target.RuntimeType, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "item_id"), target.ItemId, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "qualified_item_id"), target.QualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "interaction_kind"), target.InteractionKind, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "expected_action_type"), target.ExpectedActionType, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(bound, "native_contract"), target.NativeContract, StringComparison.Ordinal))
        {
            reasons.Add("farm_computer_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundFarmComputerAction(SmallModelAction action, SnapshotEnvelope snapshot) =>
        new()
        {
            ActionId = action.ActionId,
            OptionId = action.OptionId,
            Rationale = action.Rationale,
            Parameters = BuildFarmComputerParameters(action, snapshot)
        };

    private static FarmComputerCompilerTarget? SelectFarmComputerCompilerTarget(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var selected = SelectExactReadyNativeObjectProjection(action, snapshot, "farm_computer_report");
        if (selected is null)
            return null;
        var report = selected.Projection;
        return new FarmComputerCompilerTarget(
            selected.TargetX, selected.TargetY, selected.StandX, selected.StandY,
            ReadString(report, "target_runtime_type"), ReadString(report, "canonical_item_id"),
            ReadString(report, "canonical_qualified_item_id"), ReadString(report, "root_location_id"),
            BoolWire(ReadBool(report, "includes_hay")), NullableIntWire(ReadNullableInt(report, "pieces_of_hay")),
            NullableIntWire(ReadNullableInt(report, "hay_capacity")), ReadInt(report, "total_crops").ToString(),
            ReadInt(report, "crops_ready_for_harvest").ToString(), ReadInt(report, "unwatered_crops").ToString(),
            NullableIntWire(ReadNullableInt(report, "greenhouse_crops_ready_for_harvest")),
            ReadInt(report, "total_open_hoe_dirt").ToString(), NullableIntWire(ReadNullableInt(report, "total_forage_items")),
            ReadInt(report, "machines_ready_for_harvest").ToString(), NullableBoolWire(ReadNullableBool(report, "farm_cave_needs_harvesting")),
            ReadString(report, "report_sha256"), ReadInt(report, "expected_delay_milliseconds").ToString(),
            ReadInt(report, "expected_shake_timer_immediately_after_action").ToString(),
            ReadInt(report, "expected_player_freeze_milliseconds").ToString(),
            BoolWire(ReadBool(report, "expected_native_location_action_return")),
            ReadString(report, "interaction_kind"), ReadString(report, "expected_action_type"),
            ReadString(report, "native_contract"));
    }

    private static string NullableIntWire(int? value) => value?.ToString() ?? string.Empty;
    private static string NullableBoolWire(bool? value) => value.HasValue ? BoolWire(value) : string.Empty;
    private static string BoolWire(bool? value) => value == true ? "true" : "false";

    private sealed record FarmComputerCompilerTarget(
        int TargetX, int TargetY, int StandX, int StandY,
        string RuntimeType, string ItemId, string QualifiedItemId, string RootLocationId,
        string IncludesHay, string PiecesOfHay, string HayCapacity, string TotalCrops,
        string CropsReady, string UnwateredCrops, string GreenhouseCropsReady, string OpenHoeDirt,
        string TotalForage, string MachinesReady, string FarmCaveReady, string ReportSha256,
        string ExpectedDelayMs, string ExpectedShakeTimer, string ExpectedFreezeMs,
        string ExpectedLocationActionReturn, string InteractionKind, string ExpectedActionType,
        string NativeContract)
    {
        public string Value(string parameterName) => parameterName switch
        {
            "farm_computer_root_location_id" => RootLocationId,
            "farm_computer_includes_hay" => IncludesHay,
            "farm_computer_pieces_of_hay" => PiecesOfHay,
            "farm_computer_hay_capacity" => HayCapacity,
            "farm_computer_total_crops" => TotalCrops,
            "farm_computer_crops_ready" => CropsReady,
            "farm_computer_unwatered_crops" => UnwateredCrops,
            "farm_computer_greenhouse_crops_ready" => GreenhouseCropsReady,
            "farm_computer_open_hoe_dirt" => OpenHoeDirt,
            "farm_computer_total_forage" => TotalForage,
            "farm_computer_machines_ready" => MachinesReady,
            "farm_computer_farm_cave_ready" => FarmCaveReady,
            "farm_computer_report_sha256" => ReportSha256,
            "farm_computer_expected_delay_ms" => ExpectedDelayMs,
            "farm_computer_expected_shake_timer" => ExpectedShakeTimer,
            "farm_computer_expected_freeze_ms" => ExpectedFreezeMs,
            "farm_computer_expected_location_action_return" => ExpectedLocationActionReturn,
            _ => string.Empty
        };
    }
}

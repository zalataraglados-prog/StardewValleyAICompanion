using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static CompiledActionStep[] CompileConstructBuildingStep(SmallModelAction action)
    {
        var type = ReadParameter(action, "construction_building_type");
        var x = ReadIntParameter(action, "building_tile_x");
        var y = ReadIntParameter(action, "building_tile_y");
        return string.IsNullOrWhiteSpace(type) || !x.HasValue || !y.HasValue
            ? Array.Empty<CompiledActionStep>()
            : new[]
            {
                Step(
                    "construct_building",
                    "building:" + type + ":Farm:" + x + "," + y,
                    "farm.buildings[" + type + "].days_of_construction_left=" +
                    ReadParameter(action, "construction_build_days") +
                    ";fresh_snapshot_replan_required=true",
                    600)
            };
    }

    private static string[] ValidateConstructBuildingPlan(
        SmallModelAction action,
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger? commitmentLedger)
    {
        if (action.OptionId != "executor.construct_building")
        {
            return Array.Empty<string>();
        }
        var reasons = new List<string>();
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("construct_building_menu_must_be_clear");
        }
        ValidateQuestIdentityAgainstSnapshot(
            snapshot,
            ReadParameter(action, "quest_family") ?? string.Empty,
            ReadParameter(action, "quest_candidate_id") ?? string.Empty,
            ReadParameter(action, "quest_id") ?? string.Empty,
            ReadParameter(action, "quest_key") ?? string.Empty,
            ReadParameter(action, "quest_runtime_type") ?? string.Empty,
            ReadIntParameter(action, "quest_objective_index"),
            ReadIntParameter(action, "quest_expected_current_count"),
            ReadIntParameter(action, "quest_expected_target_count"),
            reasons);
        if (ReadParameter(action, "quest_family") != "ordinary_quest" ||
            ReadParameter(action, "quest_runtime_type") != "HaveBuildingQuest" ||
            ReadParameter(action, "quest_next_action") != "construct_building")
        {
            reasons.Add("construct_building_typed_quest_identity_required");
        }
        var row = QuestBuildingRow(snapshot, ReadParameter(action, "quest_id"));
        if (!row.HasValue)
        {
            reasons.Add("construct_building_projection_unavailable");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
        var expectedMaterials = row.Value.TryGetProperty("build_materials", out var materials)
            ? materials.GetRawText()
            : "[]";
        if (ReadString(row.Value, "action_status") != "ready_for_native_carpenter_menu" ||
            ReadParameter(action, "construction_building_type") != ReadString(row.Value, "target_building_type") ||
            ReadIntParameter(action, "construction_build_days") != ReadInt(row.Value, "build_days") ||
            ReadIntParameter(action, "construction_build_cost") != ReadInt(row.Value, "build_cost") ||
            ReadParameter(action, "construction_materials_json") != expectedMaterials ||
            ReadIntParameter(action, "expected_money_before") != ReadInt(row.Value, "expected_money_before") ||
            ReadIntParameter(action, "expected_money_after") != ReadInt(row.Value, "expected_money_after") ||
            ReadIntParameter(action, "target_tile_x") != NullableReadInt(row.Value, "carpenter_action_tile_x") ||
            ReadIntParameter(action, "target_tile_y") != NullableReadInt(row.Value, "carpenter_action_tile_y") ||
            ReadIntParameter(action, "building_tile_x") != NullableReadInt(row.Value, "placement_tile_x") ||
            ReadIntParameter(action, "building_tile_y") != NullableReadInt(row.Value, "placement_tile_y") ||
            ReadParameter(action, "placement_verification") != ReadString(row.Value, "placement_verification"))
        {
            reasons.Add("construct_building_projection_drifted");
        }
        var reservation = new MachineCraftingMaterialReservationGuard().Evaluate(
            snapshot,
            materials,
            usesWorkbench: false,
            commitmentLedger);
        if (!reservation.Ready)
        {
            reasons.AddRange(reservation.BlockingReasons);
        }
        if (ReadParameter(action, "commitment_ledger_id") != reservation.LedgerId ||
            ReadIntParameter(action, "commitment_ledger_revision") != reservation.LedgerRevision ||
            ReadParameter(action, "material_reservation_guard_status") != reservation.Status ||
            ReadParameter(action, "material_reservation_ledger_id") != reservation.LedgerId ||
            ReadIntParameter(action, "material_reservation_ledger_revision") != reservation.LedgerRevision ||
            ReadParameter(action, "material_reservation_ids_json") != JsonSerializer.Serialize(reservation.ReservationIds))
        {
            reasons.Add("construct_building_material_reservation_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static JsonElement? QuestBuildingRow(SnapshotEnvelope snapshot, string? questId)
    {
        var context = ReadStateFieldValue(snapshot, "player", "quest_building_construction");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object ||
            !context.Value.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Object && ReadString(row, "quest_id") == questId)
            {
                return row;
            }
        }
        return null;
    }
}

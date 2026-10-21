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
    private static CompiledActionStep[] CompileCraftCrabPotStep(
        SmallModelAction action)
    {
        var recipeName = ReadParameter(action, "recipe_name");
        var outputCount = ReadIntParameter(action, "output_count");
        if (string.IsNullOrWhiteSpace(recipeName) || !outputCount.HasValue)
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step(
                "craft_crab_pot",
                "recipe:" + recipeName + ":(O)710",
                "player.inventory.materials_consumed_by_native_recipe=true;" +
                "player.inventory.output_increases=(O)710:" +
                outputCount.Value +
                ";crab_pot_lifecycle_replan_for_placement=true",
                30)
        };
    }

    private static string[] ValidateCraftCrabPotPlan(
        SmallModelAction action,
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger? commitmentLedger)
    {
        if (action.OptionId != "executor.craft_crab_pot")
            return Array.Empty<string>();

        var reasons = new List<string>();
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("craft_crab_pot_menu_must_be_clear");
        var row = CrabPotCraftingRow(
            snapshot,
            ReadParameter(action, "recipe_name"));
        if (!row.HasValue)
        {
            reasons.Add(
                "craft_crab_pot_recipe_not_verified_by_transparent_state");
            return reasons.ToArray();
        }

        var usesWorkbench = string.Equals(
            ReadParameter(action, "crafting_source"),
            "native_workbench_crafting_menu",
            StringComparison.Ordinal);
        var source = usesWorkbench
            ? StorageWorkbenchCraftingSource(
                row.Value,
                ReadParameter(action, "workbench_access_point_id"))
            : row;
        if (!source.HasValue)
        {
            reasons.Add(
                "craft_crab_pot_source_not_verified_by_transparent_state");
            return reasons.ToArray();
        }

        var expectedReady = usesWorkbench
            ? "ready_for_native_workbench_crafting_menu"
            : "ready_for_native_personal_crafting_menu";
        var expectedIngredients = source.Value.TryGetProperty(
                "ingredient_rows",
                out var ingredients)
            ? ingredients.GetRawText()
            : "[]";
        if (!string.Equals(
                ReadString(source.Value, "craft_candidate_status"),
                expectedReady,
                StringComparison.Ordinal) ||
            ReadBool(
                source.Value,
                "output_inventory_acceptance_after_material_consumption") !=
            true)
        {
            reasons.Add("craft_crab_pot_recipe_not_ready");
        }
        if (!string.Equals(
                ReadParameter(action, "output_qualified_item_id"),
                "(O)710",
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadParameter(action, "output_item_id"),
                ReadString(row.Value, "output_item_id"),
                StringComparison.Ordinal) ||
            ReadIntParameter(action, "output_count") !=
                ReadInt(row.Value, "output_count_per_craft") ||
            ReadIntParameter(action, "times_crafted_before") !=
                ReadInt(row.Value, "times_crafted") ||
            !string.Equals(
                ReadParameter(action, "ingredient_rows_json"),
                expectedIngredients,
                StringComparison.Ordinal))
        {
            reasons.Add("craft_crab_pot_projection_drifted");
        }
        if (usesWorkbench)
            ValidateStorageWorkbenchBinding(action, source.Value, reasons);

        var placement = ReadStateFieldValue(
            snapshot,
            "player",
            "crab_pot_placement");
        if (!placement.HasValue ||
            placement.Value.ValueKind != JsonValueKind.Object ||
            ReadInt(placement.Value, "inventory_crab_pot_count") != 0 ||
            ReadInt(
                placement.Value,
                "potential_static_legal_tile_count") <= 0)
        {
            reasons.Add("craft_crab_pot_capacity_demand_drifted");
        }

        var reservation = new MachineCraftingMaterialReservationGuard()
            .Evaluate(
                snapshot,
                ingredients,
                usesWorkbench,
                commitmentLedger);
        if (!reservation.Ready)
            reasons.AddRange(reservation.BlockingReasons);
        if (!string.Equals(
                ReadParameter(action, "commitment_ledger_id"),
                reservation.LedgerId,
                StringComparison.Ordinal) ||
            ReadIntParameter(action, "commitment_ledger_revision") !=
                reservation.LedgerRevision ||
            !string.Equals(
                ReadParameter(action, "material_reservation_guard_status"),
                reservation.Status,
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadParameter(action, "material_reservation_ids_json"),
                JsonSerializer.Serialize(reservation.ReservationIds),
                StringComparison.Ordinal))
        {
            reasons.Add(
                "craft_crab_pot_material_reservation_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static JsonElement? CrabPotCraftingRow(
        SnapshotEnvelope snapshot,
        string? recipeName)
    {
        var context = ReadStateFieldValue(
            snapshot,
            "player",
            "crab_pot_crafting");
        if (!context.HasValue ||
            context.Value.ValueKind != JsonValueKind.Object ||
            !context.Value.TryGetProperty("rows", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        return rows.EnumerateArray().FirstOrDefault(row =>
            row.ValueKind == JsonValueKind.Object &&
            string.Equals(
                ReadString(row, "recipe_name"),
                recipeName,
                StringComparison.Ordinal));
    }
}

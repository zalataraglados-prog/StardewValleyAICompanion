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
    private static CompiledActionStep[] CompileExactSupportCraftingStep(
        SmallModelAction action,
        string stepType,
        string expectedQualifiedItemId,
        string continuationEffect)
    {
        var recipeName = ReadParameter(action, "recipe_name");
        var outputCount = ReadIntParameter(action, "output_count");
        if (string.IsNullOrWhiteSpace(recipeName) || !outputCount.HasValue)
            return Array.Empty<CompiledActionStep>();

        return new[]
        {
            Step(
                stepType,
                "recipe:" + recipeName + ":" + expectedQualifiedItemId,
                "player.inventory.materials_consumed_by_native_recipe=true;" +
                "player.inventory.output_increases=" +
                expectedQualifiedItemId + ":" + outputCount.Value + ";" +
                continuationEffect,
                30)
        };
    }

    private static ExactSupportCraftingBinding? ValidateExactSupportCraftingBinding(
        SmallModelAction action,
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger? commitmentLedger,
        string stateField,
        string expectedQualifiedItemId,
        string reasonPrefix,
        ICollection<string> reasons)
    {
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add(reasonPrefix + "_menu_must_be_clear");

        var row = ExactSupportCraftingRow(
            snapshot,
            stateField,
            ReadParameter(action, "recipe_name"));
        if (!row.HasValue)
        {
            reasons.Add(reasonPrefix + "_recipe_not_verified_by_transparent_state");
            return null;
        }

        var craftingSource = ReadParameter(action, "crafting_source");
        var usesWorkbench = string.Equals(
            craftingSource,
            "native_workbench_crafting_menu",
            StringComparison.Ordinal);
        var source = usesWorkbench
            ? StorageWorkbenchCraftingSource(
                row.Value,
                ReadParameter(action, "workbench_access_point_id"))
            : row;
        if (!source.HasValue)
        {
            reasons.Add(reasonPrefix + "_source_not_verified_by_transparent_state");
            return null;
        }

        var ingredients = source.Value.TryGetProperty(
                "ingredient_rows",
                out var ingredientRows)
            ? ingredientRows
            : default;
        var expectedIngredients = ingredients.ValueKind == JsonValueKind.Array
            ? ingredients.GetRawText()
            : "[]";
        var expectedReady = usesWorkbench
            ? "ready_for_native_workbench_crafting_menu"
            : "ready_for_native_personal_crafting_menu";
        if (!string.Equals(
                ReadString(source.Value, "craft_candidate_status"),
                expectedReady,
                StringComparison.Ordinal) ||
            ReadBool(
                source.Value,
                "output_inventory_acceptance_after_material_consumption") != true)
        {
            reasons.Add(reasonPrefix + "_recipe_not_ready");
        }

        if (!string.Equals(
                ReadString(row.Value, "output_qualified_item_id"),
                expectedQualifiedItemId,
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadParameter(action, "output_qualified_item_id"),
                expectedQualifiedItemId,
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
                StringComparison.Ordinal) ||
            !string.Equals(
                craftingSource,
                usesWorkbench
                    ? "native_workbench_crafting_menu"
                    : "native_personal_crafting_menu",
                StringComparison.Ordinal))
        {
            reasons.Add(reasonPrefix + "_projection_drifted");
        }
        if (usesWorkbench)
            ValidateStorageWorkbenchBinding(action, source.Value, reasons);

        var reservationAuthorization =
            ReadMaterialReservationAuthorization(action, reasonPrefix, reasons);
        var reservation = new MachineCraftingMaterialReservationGuard()
            .Evaluate(
                snapshot,
                ingredients,
                usesWorkbench,
                commitmentLedger,
                reservationAuthorization);
        if (!reservation.Ready)
            foreach (var reason in reservation.BlockingReasons)
                reasons.Add(reason);
        if (!string.Equals(
                ReadParameter(action, "commitment_ledger_id"),
                reservation.LedgerId,
                StringComparison.Ordinal) ||
            ReadIntParameter(action, "commitment_ledger_revision") !=
                reservation.LedgerRevision ||
            !string.Equals(
                ReadParameter(action, "material_reservation_ledger_id"),
                reservation.LedgerId,
                StringComparison.Ordinal) ||
            ReadIntParameter(
                action,
                "material_reservation_ledger_revision") !=
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
            reasons.Add(reasonPrefix +
                "_material_reservation_projection_drifted");
        }

        return new ExactSupportCraftingBinding(
            row.Value,
            source.Value,
            ingredients,
            usesWorkbench);
    }

    private static MaterialReservationConsumptionAuthorization?
        ReadMaterialReservationAuthorization(
            SmallModelAction action,
            string reasonPrefix,
            ICollection<string> reasons)
    {
        var goalId = ReadParameter(
            action,
            "material_reservation_goal_id");
        var sourceDecisionId = ReadParameter(
            action,
            "material_reservation_source_decision_id");
        var purpose = ReadParameter(
            action,
            "material_reservation_purpose");
        var suppliedCount = new[] { goalId, sourceDecisionId, purpose }
            .Count(value => !string.IsNullOrWhiteSpace(value));
        if (suppliedCount == 0)
            return null;
        if (suppliedCount != 3)
        {
            reasons.Add(
                reasonPrefix +
                "_material_reservation_authorization_incomplete");
            return null;
        }

        return new MaterialReservationConsumptionAuthorization(
            goalId!,
            sourceDecisionId!,
            purpose!);
    }

    private static JsonElement? ExactSupportCraftingRow(
        SnapshotEnvelope snapshot,
        string stateField,
        string? recipeName)
    {
        var context = ReadStateFieldValue(snapshot, "player", stateField);
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

    private sealed record ExactSupportCraftingBinding(
        JsonElement RecipeRow,
        JsonElement Source,
        JsonElement Ingredients,
        bool UsesWorkbench);
}
